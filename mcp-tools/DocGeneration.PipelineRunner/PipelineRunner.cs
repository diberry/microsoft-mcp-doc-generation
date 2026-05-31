using DocGeneration.Core.Tracing;
using HorizontalArticleGenerator.Builders;
using HorizontalArticleGenerator.Validation;
using PipelineRunner.Cli;
using PipelineRunner.Contracts;
using PipelineRunner.Context;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using Shared;
using Shared.Validation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ToolFamilyCleanup.Services;
using ToolGeneration_Improved.Services;
using ToolGeneration_Improved.Validation;

namespace PipelineRunner;

public sealed class PipelineRunner
{
    public const int SuccessExitCode = 0;
    public const int FatalExitCode = 1;
    public const int HumanReviewExitCode = 2;
    public const int InvalidArgumentsExitCode = 64;

    private static readonly Dictionary<int, StepClassification> StepClassifications = new()
    {
        { 0, StepClassification.Deterministic },
        { 1, StepClassification.Deterministic },
        { 2, StepClassification.AI },
        { 3, StepClassification.AI },
        { 4, StepClassification.Hybrid },
        { 5, StepClassification.Deterministic },
        { 6, StepClassification.AI },
    };

    private readonly StepRegistry _stepRegistry;
    private readonly PipelineContextFactory _contextFactory;
    private readonly IBrandMappingLoader _brandMappingLoader;
    private readonly IChangelogGate? _changelogGate;
    private readonly IFingerprintGate? _fingerprintGate;
    private readonly IPromptRegressionGate? _promptRegressionGate;
    private readonly ReducerRegistry? _preAiRegistry;
    private IPipelineTracer _currentTracer = NullTracer.Instance;

    public PipelineRunner(
        StepRegistry stepRegistry,
        PipelineContextFactory contextFactory,
        IChangelogGate? changelogGate = null,
        IFingerprintGate? fingerprintGate = null,
        IPromptRegressionGate? promptRegressionGate = null,
        IBrandMappingLoader? brandMappingLoader = null,
        ReducerRegistry? preAiRegistry = null)
    {
        _stepRegistry = stepRegistry;
        _contextFactory = contextFactory;
        _changelogGate = changelogGate;
        _fingerprintGate = fingerprintGate;
        _promptRegressionGate = promptRegressionGate;
        _brandMappingLoader = brandMappingLoader ?? new BrandMappingLoader();
        _preAiRegistry = preAiRegistry;
    }

    public static PipelineRunner CreateDefault(string? repoRoot = null, TextWriter? output = null, TextWriter? error = null, ReducerRegistry? preAiRegistry = null)
    {
        var reportWriter = new ConsoleReportWriter(output, error);
        var processRunner = new ProcessRunner();
        var workspaceManager = new WorkspaceManager();
        var cliMetadataLoader = new CliMetadataLoader();
        var targetMatcher = new TargetMatcher();
        var filteredCliWriter = new FilteredCliWriter(workspaceManager);
        var buildCoordinator = new BuildCoordinator(processRunner, reportWriter);
        var aiCapabilityProbe = new AiCapabilityProbe();
        var contextFactory = new PipelineContextFactory(
            processRunner,
            workspaceManager,
            cliMetadataLoader,
            targetMatcher,
            filteredCliWriter,
            buildCoordinator,
            aiCapabilityProbe,
            reportWriter,
            repoRoot);

        var resolvedRepoRoot = PipelineContextFactory.ResolveRepoRoot(repoRoot);
        var stepRegistry = StepRegistry.CreateDefault(Path.Combine(resolvedRepoRoot, "mcp-tools", "scripts"));
        var fingerprintGate = new FingerprintGate(processRunner, reportWriter);
        var promptRegressionGate = new PromptRegressionGate(processRunner, reportWriter);
        return new PipelineRunner(stepRegistry, contextFactory, new ChangelogGate(), fingerprintGate, promptRegressionGate, preAiRegistry: preAiRegistry);
    }

    public async Task<int> RunAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        var requestErrors = request.Validate(_stepRegistry.GetAllSteps().Select(step => step.Id).ToHashSet());
        if (requestErrors.Count > 0)
        {
            foreach (var error in requestErrors)
            {
                Console.Error.WriteLine(error);
            }

            return InvalidArgumentsExitCode;
        }

        if (request.Replay)
        {
            return await RunReplayAsync(request, cancellationToken);
        }

        if (request.Inspect)
        {
            return await RunInspectAsync(request, cancellationToken);
        }

        var context = await _contextFactory.CreateAsync(request, cancellationToken);
        var selectedSteps = _stepRegistry.GetOrderedSteps(request.Steps);
        context.PlannedSteps = selectedSteps;
        var dependencyErrors = request.SkipDependencyValidation
            ? Array.Empty<string>()
            : ValidateDependencies(selectedSteps);

        if (request.DryRun)
        {
            WriteDryRunPlan(context, selectedSteps, dependencyErrors);
            WriteDryRunStepResults(context, selectedSteps);
            return dependencyErrors.Count == 0 ? SuccessExitCode : InvalidArgumentsExitCode;
        }

        if (dependencyErrors.Count > 0)
        {
            foreach (var error in dependencyErrors)
            {
                context.Reports.Error(error);
            }

            return InvalidArgumentsExitCode;
        }

        var warnings = new List<string>();
        var criticalFailures = new List<CriticalFailureRecordReference>();
        var globalSteps = selectedSteps.Where(step => step.Scope == StepScope.Global).ToArray();
        var namespaceSteps = selectedSteps.Where(step => step.Scope == StepScope.Namespace).ToArray();

        // Tracing is scoped to the PipelineRunner process. Steps that shell out to standalone programs
        // are captured as step-level events only; those subprocesses need their own trace files for full AI-call detail.
        var globalTracer = new PipelineTracer("mcp-pipeline");
        context.Tracer = globalTracer;
        _currentTracer = globalTracer;
        try
        {
            foreach (var step in globalSteps)
            {
                var stepOutcome = await ExecuteStepAsync(context, step, warnings, cancellationToken);
                criticalFailures.AddRange(stepOutcome.PersistedFailures);
                if (stepOutcome.ExitCode != SuccessExitCode)
                {
                    return CompleteRun(context, warnings, criticalFailures, stepOutcome.ExitCode);
                }
            }
        }
        finally
        {
            await FlushTracerAsync(globalTracer, Path.Combine(context.OutputPath, "trace"));
            context.Tracer = NullTracer.Instance;
            _currentTracer = NullTracer.Instance;
        }

        context.CliOutput ??= await context.CliMetadataLoader.LoadCliOutputAsync(context.OutputPath, cancellationToken);
        context.CliVersion ??= await context.CliMetadataLoader.LoadCliVersionAsync(context.OutputPath, cancellationToken);

        var availableNamespaces = await context.CliMetadataLoader.LoadNamespacesAsync(context.OutputPath, cancellationToken);
        var brandEntries = await _brandMappingLoader.LoadAsync(context.McpToolsRoot, cancellationToken);

        if (!ResolveNamespaces(context, availableNamespaces, brandEntries, out var resolvedNamespaces, out var namespaceError))
        {
            context.Reports.Error(namespaceError!);
            context.Workspaces.DeleteAll();
            return InvalidArgumentsExitCode;
        }

        context.SelectedNamespaces = resolvedNamespaces;
        context.Reports.Info($"Running {selectedSteps.Count} step(s) for {resolvedNamespaces.Count} namespace(s).");

        if (namespaceSteps.Length == 0)
        {
            return CompleteRun(context, warnings, criticalFailures, SuccessExitCode);
        }

        foreach (var namespaceName in resolvedNamespaces)
        {
            var namespaceTracer = new PipelineTracer("mcp-pipeline");
            context.Tracer = namespaceTracer;
            _currentTracer = namespaceTracer;

            try
            {
                context.Reports.Info($"Namespace: {namespaceName}");
                context.Items["Namespace"] = namespaceName;

                if (!request.SkipChangelogGate && _changelogGate is not null)
                {
                    var hasExistingArticle = HasExistingArticle(context, namespaceName);
                    var gateResult = await _changelogGate.EvaluateAsync(
                        namespaceName,
                        context.CliVersion ?? string.Empty,
                        context.McpBranch,
                        hasExistingArticle,
                        cancellationToken);

                    if (gateResult.ShouldSkip)
                    {
                        context.Reports.Info($"  Skipped (changelog gate): {gateResult.Reason}");
                        continue;
                    }
                }

                foreach (var step in namespaceSteps)
                {
                    var stepOutcome = await ExecuteStepAsync(context, step, warnings, cancellationToken);
                    criticalFailures.AddRange(stepOutcome.PersistedFailures);
                    if (stepOutcome.ExitCode != SuccessExitCode)
                    {
                        return CompleteRun(context, warnings, criticalFailures, stepOutcome.ExitCode);
                    }
                }
            }
            finally
            {
                await FlushTracerAsync(namespaceTracer, GetNamespaceTraceOutputDirectory(context, namespaceName));
                context.Tracer = NullTracer.Instance;
                _currentTracer = NullTracer.Instance;
            }
        }

        var gatesExitCode = await RunValidationGatesAsync(context, warnings, criticalFailures, cancellationToken);
        if (gatesExitCode != SuccessExitCode)
        {
            return CompleteRun(context, warnings, criticalFailures, gatesExitCode);
        }

        return CompleteRun(context, warnings, criticalFailures, SuccessExitCode);
    }

    private async Task<int> RunInspectAsync(PipelineRequest request, CancellationToken cancellationToken)
    {
        var stepSlug = request.ReplayStepName!;
        var outputPath = request.OutputPath;
        var namespaceName = request.Namespace;
        var modelName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_NAME") ?? "gpt-4.1-mini";

        Console.WriteLine($"Inspect: step={stepSlug} namespace={namespaceName ?? "(all)"} output={outputPath} model={modelName}");
        Console.WriteLine();

        var rows = new List<InspectBudgetRow>();

        var exitCode = stepSlug switch
        {
            "tool-generation" => await InspectToolGenerationAsync(outputPath, namespaceName, rows, cancellationToken),
            "horizontal-articles" => await InspectHorizontalArticlesAsync(outputPath, namespaceName, rows, cancellationToken),
            "tool-family-cleanup" => await InspectToolFamilyCleanupAsync(outputPath, namespaceName, rows, cancellationToken),
            _ => ReportUnknownInspectStep(stepSlug),
        };

        if (request.WriteJsonOutput && rows.Count > 0)
        {
            await WriteInspectJsonAsync(outputPath, modelName, rows, cancellationToken);
        }

        return exitCode;
    }

    private static async Task WriteInspectJsonAsync(
        string outputPath,
        string modelName,
        IReadOnlyList<InspectBudgetRow> rows,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputPath);
        var jsonPath = Path.Combine(outputPath, "inspect-budget.json");
        var payload = new
        {
            model = modelName,
            rows = rows.Select(r => new
            {
                step = r.Step,
                @namespace = r.Namespace,
                estimatedTokens = r.EstimatedTokens,
                budget = r.Budget,
                headroom = r.Headroom,
                topItems = r.TopItems,
            }).ToArray(),
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
        Console.WriteLine($"Inspect JSON written to: {jsonPath}");
    }

    private static int ReportUnknownInspectStep(string stepSlug)
    {
        Console.Error.WriteLine($"Unknown inspect step '{stepSlug}'. Supported: tool-generation, horizontal-articles, tool-family-cleanup.");
        return InvalidArgumentsExitCode;
    }

    private static async Task<int> InspectToolGenerationAsync(
        string outputPath,
        string? namespaceName,
        IList<InspectBudgetRow> rows,
        CancellationToken cancellationToken)
    {
        var composedDir = Path.Combine(outputPath, "tools-composed");
        if (!Directory.Exists(composedDir))
        {
            Console.Error.WriteLine($"tools-composed directory not found: {composedDir}");
            Console.Error.WriteLine("Run step 3 (tool-generation) first to produce composed tool files.");
            return FatalExitCode;
        }

        var toolFiles = Directory.EnumerateFiles(composedDir, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (namespaceName is not null)
        {
            toolFiles = toolFiles
                .Where(f => Path.GetFileName(f).StartsWith(namespaceName + "-", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (toolFiles.Length == 0)
        {
            Console.WriteLine(namespaceName is null
                ? $"No composed tool files found in: {composedDir}"
                : $"No composed tool files found for namespace '{namespaceName}' in: {composedDir}");
            return SuccessExitCode;
        }

        var reducer = new ToolGenerationReducer();
        var validator = new ToolGenerationBudgetValidator();
        const int DefaultMaxTokens = 8000;

        PrintBudgetTableHeader("tool", "namespace", "estimatedTokens", "budget", "headroom", "topItems");

        var anyOverBudget = false;
        foreach (var filePath in toolFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(filePath);
            var ns = namespaceName ?? InferNamespace(fileName);

            var context = await reducer.ReduceAsync(composedDir, fileName, DefaultMaxTokens, cancellationToken);
            var result = await validator.ValidateAsync(context, cancellationToken);

            var estimated = result.EstimatedPromptTokens ?? 0;
            var budget = result.TokenBudget ?? ToolGenerationBudgetValidator.InputTokenBudget;
            var headroom = budget - estimated;
            var topSections = ExtractTopSections(context.ComposedContent, 5);
            var topItemsDisplay = topSections.Length > 0
                ? string.Join(", ", topSections.Select(s => $"{s.Name}:{s.EstimatedTokens:N0}"))
                : context.ToolName;

            PrintBudgetTableRow("tool-generation", ns, estimated, budget, headroom, topItemsDisplay);

            rows.Add(new InspectBudgetRow(
                "tool-generation",
                ns,
                estimated,
                budget,
                headroom,
                topSections.Length > 0
                    ? topSections.Select(s => $"{s.Name}:{s.EstimatedTokens:N0}").ToArray()
                    : [context.ToolName]));

            if (!result.WithinBudget)
            {
                anyOverBudget = true;
            }
        }

        return anyOverBudget ? FatalExitCode : SuccessExitCode;
    }

    private static async Task<int> InspectHorizontalArticlesAsync(
        string outputPath,
        string? namespaceName,
        IList<InspectBudgetRow> rows,
        CancellationToken cancellationToken)
    {
        if (namespaceName is null)
        {
            Console.Error.WriteLine("--namespace is required for --inspect --step horizontal-articles.");
            return InvalidArgumentsExitCode;
        }

        var builder = new ArticleOutlineBuilder();
        var validator = new ArticleOutlineBudgetValidator();

        var context = await builder.BuildAsync(outputPath, namespaceName, cancellationToken);
        var result = await validator.ValidateAsync(context, cancellationToken);

        var estimated = result.EstimatedPromptTokens ?? 0;
        var budget = result.TokenBudget ?? ArticleOutlineBudgetValidator.InputTokenBudget;
        var headroom = budget - estimated;

        var topItems = context.Sections
            .OrderByDescending(s => s.EvidenceItems.Sum(e => e.Length))
            .Take(5)
            .Select(s => s.Heading)
            .ToArray();

        PrintBudgetTableHeader("step", "namespace", "estimatedTokens", "budget", "headroom", "topItems");
        PrintBudgetTableRow("horizontal-articles", namespaceName, estimated, budget, headroom, string.Join(", ", topItems));

        rows.Add(new InspectBudgetRow(
            "horizontal-articles",
            namespaceName,
            estimated,
            budget,
            headroom,
            topItems));

        return result.WithinBudget ? SuccessExitCode : FatalExitCode;
    }

    private static async Task<int> InspectToolFamilyCleanupAsync(
        string outputPath,
        string? namespaceName,
        IList<InspectBudgetRow> rows,
        CancellationToken cancellationToken)
    {
        if (namespaceName is null)
        {
            Console.Error.WriteLine("--namespace is required for --inspect --step tool-family-cleanup.");
            return InvalidArgumentsExitCode;
        }

        var toolsDir = Path.Combine(outputPath, "tools");
        if (!Directory.Exists(toolsDir))
        {
            Console.Error.WriteLine($"tools directory not found: {toolsDir}");
            Console.Error.WriteLine("Run step 4 (tool-family-cleanup) first to produce tool files.");
            return FatalExitCode;
        }

        const int FamilyCleanupBudget = 150_000;
        const int CharsPerToken = 4;

        var builder = new FamilyStructureBuilder();
        var context = await builder.BuildAsync(toolsDir, namespaceName, h2HeadingsDirectory: null, cancellationToken);

        var totalChars = context.Sections.Sum(s => s.SourceContent.Length);
        var estimatedTokens = totalChars / CharsPerToken;
        var headroom = FamilyCleanupBudget - estimatedTokens;

        var topItems = context.Sections
            .OrderByDescending(s => s.SourceContent.Length)
            .Take(5)
            .Select(s => s.Heading)
            .ToArray();

        PrintBudgetTableHeader("step", "namespace", "estimatedTokens", "budget", "headroom", "topItems");

        var status = headroom >= 0 ? "✅" : "❌";
        var sb = new StringBuilder();
        sb.Append("tool-family-cleanup".PadRight(22));
        sb.Append(namespaceName.PadRight(18));
        sb.Append($"{estimatedTokens:N0}".PadRight(16));
        sb.Append($"{FamilyCleanupBudget:N0}".PadRight(12));
        sb.Append($"{headroom:+#;-#;0}".PadRight(12));
        sb.Append($"{status} {string.Join(", ", topItems)}");
        Console.WriteLine(sb.ToString());
        Console.WriteLine();

        rows.Add(new InspectBudgetRow(
            "tool-family-cleanup",
            namespaceName,
            estimatedTokens,
            FamilyCleanupBudget,
            headroom,
            topItems));

        return headroom >= 0 ? SuccessExitCode : FatalExitCode;
    }

    private static string InferNamespace(string fileName)
    {
        var dashIndex = fileName.IndexOf('-', StringComparison.Ordinal);
        return dashIndex < 0 ? fileName : fileName[..dashIndex];
    }

    private static void PrintBudgetTableHeader(string col1, string col2, string col3, string col4, string col5, string col6)
    {
        Console.WriteLine(
            col1.PadRight(22) +
            col2.PadRight(18) +
            col3.PadRight(16) +
            col4.PadRight(12) +
            col5.PadRight(12) +
            col6);
        Console.WriteLine(new string('-', 90));
    }

    private static void PrintBudgetTableRow(string step, string ns, int estimated, int budget, int headroom, string topItem)
    {
        var status = headroom >= 0 ? "✅" : "❌";
        Console.WriteLine(
            step.PadRight(22) +
            ns.PadRight(18) +
            $"{estimated:N0}".PadRight(16) +
            $"{budget:N0}".PadRight(12) +
            $"{headroom:+#;-#;0}".PadRight(12) +
            $"{status} {topItem}");
    }

    private static readonly Regex MarkdownH2Regex = new(@"^## (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Returns the top <paramref name="count"/> markdown H2 sections sorted by estimated token count.</summary>
    internal static MarkdownSection[] ExtractTopSections(string content, int count)
    {
        const int CharsPerToken = 4;
        var matches = MarkdownH2Regex.Matches(content);
        if (matches.Count == 0)
            return [];

        var sections = new List<MarkdownSection>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var heading = matches[i].Groups[1].Value.Trim();
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            var sectionLength = end - start;
            sections.Add(new MarkdownSection(heading, sectionLength / CharsPerToken));
        }

        return sections
            .OrderByDescending(s => s.EstimatedTokens)
            .Take(count)
            .ToArray();
    }

    /// <summary>A markdown section with its estimated token count.</summary>
    internal sealed record MarkdownSection(string Name, int EstimatedTokens);

    private async Task<int> RunReplayAsync(PipelineRequest request, CancellationToken cancellationToken)
    {
        var bootstrapContext = await _contextFactory.CreateAsync(request, cancellationToken);
        var replayOutputPath = WorkspaceManager.GetReplayWorkspace(bootstrapContext.RepoRoot, request.ReplayFromRunId!);
        if (!_stepRegistry.TryGetBySlug(request.ReplayStepName!, out var targetStep) || targetStep is null)
        {
            Console.Error.WriteLine($"Unknown replay step '{request.ReplayStepName}'.");
            return InvalidArgumentsExitCode;
        }

        var replayRequest = request with
        {
            OutputPath = replayOutputPath,
            Steps = [targetStep.Id],
        };

        var context = await _contextFactory.CreateAsync(replayRequest, cancellationToken);
        context.PlannedSteps = [targetStep];

        if (!TryValidateReplayUpstreamArtifacts(context, targetStep, out var missingUpstreamPath))
        {
            Console.Error.WriteLine($"upstream artifact not found: {missingUpstreamPath}");
            return FatalExitCode;
        }

        var warnings = new List<string>();
        var criticalFailures = new List<CriticalFailureRecordReference>();
        if (targetStep.Scope == StepScope.Global)
        {
            var stepOutcome = await ExecuteStepAsync(context, targetStep, warnings, cancellationToken);
            criticalFailures.AddRange(stepOutcome.PersistedFailures);
            return CompleteRun(context, warnings, criticalFailures, stepOutcome.ExitCode);
        }

        context.CliOutput ??= await context.CliMetadataLoader.LoadCliOutputAsync(context.OutputPath, cancellationToken);
        context.CliVersion ??= await context.CliMetadataLoader.LoadCliVersionAsync(context.OutputPath, cancellationToken);

        var availableNamespaces = await context.CliMetadataLoader.LoadNamespacesAsync(context.OutputPath, cancellationToken);
        var brandEntries = await _brandMappingLoader.LoadAsync(context.McpToolsRoot, cancellationToken);
        if (!ResolveNamespaces(context, availableNamespaces, brandEntries, out var resolvedNamespaces, out var namespaceError))
        {
            context.Reports.Error(namespaceError!);
            context.Workspaces.DeleteAll();
            return InvalidArgumentsExitCode;
        }

        context.SelectedNamespaces = resolvedNamespaces;
        context.Reports.Info($"Replaying step {targetStep.Id}: {targetStep.Name} from run '{request.ReplayFromRunId}'.");

        foreach (var namespaceName in resolvedNamespaces)
        {
            context.Items["Namespace"] = namespaceName;
            var stepOutcome = await ExecuteStepAsync(context, targetStep, warnings, cancellationToken);
            criticalFailures.AddRange(stepOutcome.PersistedFailures);
            if (stepOutcome.ExitCode != SuccessExitCode)
            {
                return CompleteRun(context, warnings, criticalFailures, stepOutcome.ExitCode);
            }
        }

        return CompleteRun(context, warnings, criticalFailures, SuccessExitCode);
    }

    private async Task<int> RunValidationGatesAsync(
        PipelineContext context,
        ICollection<string> warnings,
        ICollection<CriticalFailureRecordReference> criticalFailures,
        CancellationToken cancellationToken)
    {
        if (context.Request.RunFingerprintGate && _fingerprintGate is not null)
        {
            context.Reports.Info("Running fingerprint baseline gate...");
            var result = await _fingerprintGate.EvaluateAsync(
                context.RepoRoot,
                context.McpToolsRoot,
                cancellationToken);

            if (result.Success)
            {
                context.Reports.Info($"  ✅ Fingerprint gate passed: {result.Reason}");
            }
            else
            {
                context.Reports.Error($"  ❌ Fingerprint gate failed: {result.Reason}");
                return FatalExitCode;
            }
        }

        if (context.Request.RunPromptRegressionGate && _promptRegressionGate is not null)
        {
            context.Reports.Info("Running prompt regression gate...");
            var result = await _promptRegressionGate.EvaluateAsync(
                context.McpToolsRoot,
                cancellationToken);

            if (result.Success)
            {
                context.Reports.Info($"  ✅ Prompt regression gate passed: {result.Reason}");
            }
            else
            {
                context.Reports.Error($"  ❌ Prompt regression gate failed: {result.Reason}");
                return FatalExitCode;
            }
        }

        return SuccessExitCode;
    }

    public static int MapBootstrapExitCode(int exitCode)
        => exitCode switch
        {
            SuccessExitCode => SuccessExitCode,
            HumanReviewExitCode => HumanReviewExitCode,
            _ => FatalExitCode,
        };

    public static int MapStepFailureExitCode(FailurePolicy failurePolicy, bool stepSucceeded, int? exitCodeOverride = null)
    {
        if (stepSucceeded || failurePolicy == FailurePolicy.Warn)
        {
            return SuccessExitCode;
        }

        return exitCodeOverride switch
        {
            HumanReviewExitCode => HumanReviewExitCode,
            _ => FatalExitCode,
        };
    }

    private static async Task<StepResult> RunPostValidatorsAsync(
        PipelineContext context,
        IPipelineStep step,
        StepResult result,
        CancellationToken cancellationToken)
    {
        var success = result.Success;
        var warnings = result.Warnings.ToList();
        var validatorResults = result.ValidatorResults.ToList();

        foreach (var validator in step.PostValidators)
        {
            context.Reports.Info($"    Validator: {validator.Name}");

            ValidatorResult validatorResult;
            try
            {
                validatorResult = await validator.ValidateAsync(context, step, cancellationToken);
            }
            catch (Exception ex)
            {
                validatorResult = new ValidatorResult(
                    validator.Name,
                    false,
                    [$"Blocking: Validator '{validator.Name}' failed with an exception: {ex.Message}"]);
            }

            validatorResults.Add(validatorResult);
            warnings.AddRange(validatorResult.Warnings);
            success &= validatorResult.Success;
        }

        return result with
        {
            Success = success,
            Warnings = warnings,
            ValidatorResults = validatorResults,
        };
    }

    private async Task<StepExecutionOutcome> ExecuteStepAsync(
        PipelineContext context,
        IPipelineStep step,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        context.Reports.Info($"  Step {step.Id}: {step.Name}");

        var classification = StepClassifications.GetValueOrDefault(step.Id, StepClassification.Deterministic);
        var namespaceName = context.Items.GetValueOrDefault("Namespace") as string;
        using var handle = _currentTracer.StartStep(
            step.Name,
            classification,
            step.Scope == StepScope.Namespace ? namespaceName : null,
            $"stepId={step.Id}; maxRetries={step.MaxRetries}");

        try
        {
            if (_preAiRegistry is not null && !context.Request.SkipValidation)
            {
                var preAiResult = await TryRunPreAiGateAsync(_preAiRegistry, context, step, cancellationToken);
                if (preAiResult is not null)
                {
                    foreach (var w in preAiResult.Warnings)
                    {
                        warnings.Add(w);
                        context.Reports.Warning(w);
                    }
                    context.Reports.Warning($"  \u2296 Step {step.Id} skipped: pre-AI validation failed.");
                    var skippedPersisted = CriticalFailureRecorder.Persist(context, step, preAiResult);
                    TryWriteStepResultEnvelope(context, step, preAiResult, out _);
                    WriteObservabilityOutputs(context, step, preAiResult, classification);
                    handle.Complete("pre-ai validation failed \u2013 step skipped");
                    return new StepExecutionOutcome(SuccessExitCode, preAiResult, skippedPersisted);
                }
            }

            var maxAttempts = 1 + step.MaxRetries;
            var hasValidators = !context.Request.SkipValidation && step.PostValidators.Count > 0;

            StepResult result = null!;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    context.Reports.Warning($"    Retry attempt {attempt - 1}/{step.MaxRetries} for step {step.Id}");
                }

                result = await step.ExecuteAsync(context, cancellationToken);

                if (result.Success && hasValidators)
                {
                    result = await RunPostValidatorsAsync(context, step, result, cancellationToken);
                }

                if (result.Success || attempt == maxAttempts)
                {
                    break;
                }

                context.Reports.Warning($"    Step {step.Id} validation failed, retrying ({attempt}/{maxAttempts - 1})");
            }

            foreach (var warning in result.Warnings)
            {
                warnings.Add(warning);
                context.Reports.Warning(warning);
            }

            var persistedFailures = CriticalFailureRecorder.Persist(context, step, result);
            if (!TryWriteStepResultEnvelope(context, step, result, out var stepResultError))
            {
                if (step.FailurePolicy == FailurePolicy.Warn)
                {
                    warnings.Add(stepResultError);
                    context.Reports.Warning(stepResultError);
                }
                else
                {
                    context.Reports.Error($"FATAL: {stepResultError}");
                    handle.Fail(stepResultError);
                    return new StepExecutionOutcome(FatalExitCode, result, persistedFailures);
                }
            }

            WriteObservabilityOutputs(context, step, result, classification);

            var stepExitCode = MapStepFailureExitCode(step.FailurePolicy, result.Success, result.ExitCodeOverride);
            if (stepExitCode != SuccessExitCode)
            {
                context.Reports.Error($"Step {step.Id} failed.");
                handle.Fail($"Exit code {stepExitCode}");
            }
            else
            {
                handle.Complete(result.Success ? "completed" : "warning-only completion");
            }

            return new StepExecutionOutcome(stepExitCode, result, persistedFailures);
        }
        catch (Exception ex)
        {
            handle.Fail(ex.Message);
            throw;
        }
    }

    private static void WriteObservabilityOutputs(
        PipelineContext context,
        IPipelineStep step,
        StepResult result,
        StepClassification classification)
    {
        var directory = GetObservabilityDirectory(context.OutputPath, step);
        var validationStatus = GetValidationStatus(result.ValidatorResults);
        Directory.CreateDirectory(directory);

        var stepWorkspaceDirectory = GetStepWorkspaceDirectory(context, step);
        var stepResultPath = Path.Combine(stepWorkspaceDirectory, StepResultWriter.FileName);
        if (File.Exists(stepResultPath))
        {
            File.Copy(stepResultPath, Path.Combine(directory, StageOutputContract.StepResultFileName), overwrite: true);
        }
        else
        {
            StepResultWriter.Write(directory, BuildStepResultEnvelope(context, step, result));
        }

        ObservabilityWriter.WriteMetrics(
            directory,
            step.Name,
            result.Duration,
            inputCount: 0,
            outputCount: result.Outputs.Count,
            validationStatus.ToString().ToLowerInvariant());
        ObservabilityWriter.WriteValidation(directory, step.Name, result.ValidatorResults);
        ObservabilityWriter.WriteSummary(directory, step.Name, result.Success, result.Duration, result.Warnings);

        if (classification == StepClassification.Deterministic)
        {
            ObservabilityWriter.WritePromptPreviewNa(directory);
        }

        var contract = new StageOutputContract(
            step.Name,
            directory,
            IsDeterministic: classification == StepClassification.Deterministic);
        var missingFiles = context.Workspaces.AssertOutputContract(contract);
        if (missingFiles.Count > 0)
        {
            context.Reports.Warning(
                $"Step {step.Id} observability contract missing file(s): {string.Join(", ", missingFiles)}");
        }
    }

    public static StepResultFile BuildStepResultEnvelope(
        PipelineContext context,
        IPipelineStep step,
        StepResult result)
    {
        return new StepResultFile
        {
            Version = 3,
            SchemaVersion = "1.0",
            Status = GetStepResultStatus(result),
            Step = step.Name,
            StepName = GetStepIdentifierSlug(step),
            Namespace = ResolveNamespace(context),
            OutputFileCount = result.Outputs.Count,
            Warnings = GetWarnings(result),
            Errors = GetErrors(result),
            Duration = result.Duration.ToString("c"),
            DurationMs = (long)result.Duration.TotalMilliseconds,
            ValidationStatus = GetStepResultValidationStatus(step, result.ValidatorResults),
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            OutputArtifacts = BuildArtifactReferences(context.OutputPath, result.Outputs),
        };
    }

    private static bool TryWriteStepResultEnvelope(
        PipelineContext context,
        IPipelineStep step,
        StepResult result,
        out string error)
    {
        var stepWorkspaceDirectory = GetStepWorkspaceDirectory(context, step);
        Directory.CreateDirectory(stepWorkspaceDirectory);

        try
        {
            StepResultWriter.Write(stepWorkspaceDirectory, BuildStepResultEnvelope(context, step, result));
        }
        catch (Exception ex)
        {
            error = $"Step {step.Id} failed to write {StepResultWriter.FileName}: {ex.Message}";
            return false;
        }

        if (StepResultReader.TryRead(stepWorkspaceDirectory, out _))
        {
            error = string.Empty;
            return true;
        }

        error = $"Step {step.Id} did not produce {StepResultWriter.FileName} in '{stepWorkspaceDirectory}'.";
        return false;
    }

    private static void WriteDryRunStepResults(PipelineContext context, IReadOnlyList<IPipelineStep> selectedSteps)
    {
        foreach (var step in selectedSteps)
        {
            var stepWorkspaceDirectory = GetStepWorkspaceDirectory(context, step);
            Directory.CreateDirectory(stepWorkspaceDirectory);

            var dryRunOutputs = (step as StepDefinition)?.ExpectedOutputs
                ?? Array.Empty<string>();

            var resolvedOutputs = dryRunOutputs
                .Select(output => Path.Combine(context.OutputPath, output))
                .ToArray();

            StepResultWriter.Write(
                stepWorkspaceDirectory,
                BuildStepResultEnvelope(context, step, StepResult.DryRun(resolvedOutputs)));
        }
    }

    private static List<ArtifactReference>? BuildArtifactReferences(string outputRoot, IReadOnlyList<string> outputs)
    {
        var artifacts = new List<ArtifactReference>();
        foreach (var output in outputs)
        {
            if (!File.Exists(output))
            {
                continue;
            }

            artifacts.Add(new ArtifactReference
            {
                Path = GetRelativePath(outputRoot, output),
                Sha256 = ComputeFileHash(output),
            });
        }

        return artifacts.Count == 0 ? null : artifacts;
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static List<string> GetWarnings(StepResult result)
    {
        return result.Warnings
            .Concat(result.ValidatorResults.SelectMany(validator => validator.Warnings))
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> GetErrors(StepResult result)
    {
        if (result.Success)
        {
            return [];
        }

        var errors = result.ArtifactFailures
            .Select(failure => failure.Summary)
            .Where(summary => !string.IsNullOrWhiteSpace(summary))
            .ToList();

        if (errors.Count == 0)
        {
            errors.Add("Step execution reported failure.");
        }

        return errors;
    }

    private static ValidationStatus GetValidationStatus(IReadOnlyList<ValidatorResult> validatorResults)
    {
        if (validatorResults.Count == 0)
        {
            return ValidationStatus.Skipped;
        }

        return validatorResults.All(result => result.Success)
            ? ValidationStatus.Passed
            : ValidationStatus.Failed;
    }

    private static ValidationStatus? GetStepResultValidationStatus(
        IPipelineStep step,
        IReadOnlyList<ValidatorResult> validatorResults)
    {
        if (step.PostValidators.Count == 0 && validatorResults.Count == 0)
        {
            return null;
        }

        return GetValidationStatus(validatorResults);
    }

    private static StepResultStatus GetStepResultStatus(StepResult result)
    {
        if (result.Success)
        {
            return StepResultStatus.Success;
        }

        return result.Outputs.Count > 0
            ? StepResultStatus.Partial
            : StepResultStatus.Failure;
    }

    private static string ResolveNamespace(PipelineContext context)
        => context.Items.TryGetValue("Namespace", out var namespaceName)
            ? namespaceName as string ?? string.Empty
            : context.Request.Namespace ?? string.Empty;

    private static string GetObservabilityDirectory(string outputPath, IPipelineStep step)
        => Path.Combine(outputPath, "observability", $"{step.Id}-{Slugify(step.Name)}");

    internal static string GetStepIdentifierSlug(IPipelineStep step)
        => Slugify($"Step {step.Id} - {step.Name}");

    private static string GetStepWorkspaceDirectory(PipelineContext context, IPipelineStep step)
        => Path.Combine(context.OutputPath, GetStepIdentifierSlug(step));

    private static string GetRelativePath(string rootPath, string path)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);
        var normalizedPath = relativePath.StartsWith("..", StringComparison.Ordinal)
            ? path
            : relativePath;

        return normalizedPath.Replace('\\', '/');
    }

    private static string Slugify(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;
        var previousDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = character;
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                buffer[length++] = '-';
                previousDash = true;
            }
        }

        var sanitized = new string(buffer, 0, length).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? $"step-{Guid.NewGuid():N}" : sanitized;
    }

    // Flush is deliberately non-cancellable to ensure traces are written even when pipeline execution is cancelled.
    // Trace files are small (<20MB), so the final flush completes quickly.
    private static Task FlushTracerAsync(IPipelineTracer tracer, string outputDirectory)
    {
        return tracer.FlushAsync(outputDirectory, CancellationToken.None);
    }

    private static string GetNamespaceTraceOutputDirectory(PipelineContext context, string namespaceName)
    {
        var parentDirectory = Directory.GetParent(context.OutputPath)?.FullName;
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            parentDirectory = context.OutputPath;
        }

        return Path.Combine(parentDirectory, $"generated-{namespaceName}", "trace");
    }

    private static int CompleteRun(
        PipelineContext context,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<CriticalFailureRecordReference> criticalFailures,
        int exitCode)
    {
        WriteCriticalFailureSummary(context, criticalFailures, exitCode);

        if (exitCode == SuccessExitCode)
        {
            if (criticalFailures.Count > 0)
            {
                context.Reports.Warning($"Pipeline completed with {warnings.Count} warning(s) and {criticalFailures.Count} critical failure record(s).");
            }
            else if (warnings.Count > 0)
            {
                context.Reports.Info($"Pipeline completed with {warnings.Count} warning(s).");
            }
            else
            {
                context.Reports.Info("Pipeline completed successfully.");
            }
        }
        else
        {
            context.Reports.Error($"Pipeline stopped with {criticalFailures.Count} critical failure(s) recorded.");
        }

        context.Workspaces.DeleteAll();
        return exitCode;
    }

    private static void WriteCriticalFailureSummary(
        PipelineContext context,
        IReadOnlyCollection<CriticalFailureRecordReference> criticalFailures,
        int exitCode)
    {
        if (criticalFailures.Count == 0)
        {
            return;
        }

        if (exitCode == SuccessExitCode)
        {
            context.Reports.Warning("Critical failures summary:");
            foreach (var failure in criticalFailures)
            {
                context.Reports.Warning($"  - Artifact: {failure.ArtifactName} ({failure.ArtifactType})");
                context.Reports.Warning($"    Step: Step {failure.StepId} - {failure.StepName}");
                context.Reports.Warning($"    Error: {failure.Summary}");
                context.Reports.Warning($"    Record: {failure.RecordPath}");
            }

            return;
        }

        context.Reports.Error("Critical failures summary:");
        foreach (var failure in criticalFailures)
        {
            context.Reports.Error($"  - Artifact: {failure.ArtifactName} ({failure.ArtifactType})");
            context.Reports.Error($"    Step: Step {failure.StepId} - {failure.StepName}");
            context.Reports.Error($"    Error: {failure.Summary}");
            context.Reports.Error($"    Record: {failure.RecordPath}");
        }
    }

    private static async Task<StepResult?> TryRunPreAiGateAsync(
        ReducerRegistry preAiRegistry,
        PipelineContext context,
        IPipelineStep step,
        CancellationToken cancellationToken)
    {
        var reducer = preAiRegistry.GetReducer(step.Id);
        if (reducer is null)
        {
            return null;
        }

        object typedContext;
        try
        {
            typedContext = await reducer(context, cancellationToken);
        }
        catch
        {
            return null;
        }

        var validators = preAiRegistry.GetValidatorsForType(typedContext.GetType()).ToList();
        if (validators.Count == 0)
        {
            return null;
        }

        var allErrors = new List<ValidationError>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(typedContext, cancellationToken);
            allErrors.AddRange(result.Errors);
        }

        var isValid = allErrors.All(static e => e.Severity != ValidationSeverity.Error);
        if (isValid)
        {
            return null;
        }

        var errorMessages = allErrors
            .Where(static e => e.Severity >= ValidationSeverity.Error)
            .Select(static e => e.Message)
            .ToArray();

        return new StepResult(
            Success: true,
            Warnings: errorMessages,
            Duration: TimeSpan.Zero,
            Outputs: [],
            ProcessInvocations: [],
            ValidatorResults: [new ValidatorResult("pre-ai-validation", false, errorMessages)],
            ArtifactFailures: []);
    }

    private static IReadOnlyList<string> ValidateDependencies(IReadOnlyList<IPipelineStep> selectedSteps)
    {
        var selectedIds = selectedSteps.Select(step => step.Id).ToHashSet();
        var errors = new List<string>();

        foreach (var step in selectedSteps)
        {
            var missingDependencies = step.DependsOn.Where(dependency => !selectedIds.Contains(dependency)).ToArray();
            if (missingDependencies.Length > 0)
            {
                errors.Add($"Step {step.Id} requires step(s) {string.Join(", ", missingDependencies)} to be selected in the same run.");
            }
        }

        return errors;
    }

    private bool TryValidateReplayUpstreamArtifacts(PipelineContext context, IPipelineStep targetStep, out string missingUpstreamPath)
    {
        foreach (var dependencyId in targetStep.DependsOn)
        {
            var dependencyStep = _stepRegistry.GetStep(dependencyId);
            var dependencyDirectory = Path.Combine(context.OutputPath, GetStepIdentifierSlug(dependencyStep));
            var dependencyResultPath = Path.Combine(dependencyDirectory, StepResultWriter.FileName);
            if (!StepResultReader.TryRead(dependencyDirectory, out _))
            {
                missingUpstreamPath = dependencyResultPath;
                return false;
            }
        }

        missingUpstreamPath = string.Empty;
        return true;
    }

    private static bool ResolveNamespaces(
        PipelineContext context,
        IReadOnlyList<string> availableNamespaces,
        IReadOnlyList<BrandMappingEntry> brandEntries,
        out IReadOnlyList<string> resolvedNamespaces,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(context.Request.Namespace))
        {
            resolvedNamespaces = availableNamespaces;
            error = null;
            return true;
        }

        var expansion = NamespaceExpander.Expand(context.Request.Namespace, brandEntries, availableNamespaces);

        if (expansion.IsAll)
        {
            resolvedNamespaces = expansion.Namespaces;
            error = null;
            return true;
        }

        if (expansion.IsResolved)
        {
            if (expansion.IsExpanded)
            {
                context.Reports.Info(
                    $"Namespace '{context.Request.Namespace}' expanded to {expansion.Namespaces.Count} sub-namespace(s): " +
                    string.Join(", ", expansion.Namespaces));
            }

            resolvedNamespaces = expansion.Namespaces;
            error = null;
            return true;
        }

        if (expansion.IsSubEntriesNotInCli)
        {
            resolvedNamespaces = Array.Empty<string>();
            error = $"Namespace prefix '{context.Request.Namespace}' matched brand mapping entries " +
                    $"({string.Join(", ", expansion.SubEntriesFound)}) but none are available in the CLI namespace list.";
            return false;
        }

        // Not found in brand mapping — fall back to normalized CLI exact match for backward compatibility
        var normalizedRequest = context.TargetMatcher.Normalize(context.Request.Namespace!);
        var cliMatch = availableNamespaces.FirstOrDefault(candidate =>
            string.Equals(context.TargetMatcher.Normalize(candidate), normalizedRequest, StringComparison.OrdinalIgnoreCase));

        if (cliMatch is not null)
        {
            resolvedNamespaces = [cliMatch];
            error = null;
            return true;
        }

        resolvedNamespaces = Array.Empty<string>();
        error = $"Unknown namespace '{context.Request.Namespace}'.";
        return false;
    }


    private static void WriteDryRunPlan(
        PipelineContext context,
        IReadOnlyList<IPipelineStep> selectedSteps,
        IReadOnlyList<string> dependencyErrors)
    {
        var namespaces = context.SelectedNamespaces.Count > 0
            ? context.SelectedNamespaces
            : ["<all namespaces from CLI metadata>"];

        context.Reports.Info("Dry run plan:");
        context.Reports.Info($"  Output: {context.OutputPath}");
        context.Reports.Info($"  Namespaces: {string.Join(", ", namespaces)}");

        foreach (var step in selectedSteps)
        {
            if (step is StepDefinition definition)
            {
                context.Reports.Info($"  Step {step.Id}: {step.Name} [{step.Scope}, {step.FailurePolicy}, {definition.Implementation}]");
                if (step.DependsOn.Count > 0)
                {
                    context.Reports.Info($"    Depends on: {string.Join(", ", step.DependsOn)}");
                }
                if (step.PostValidators.Count > 0)
                {
                    context.Reports.Info($"    Post-validators: {string.Join(", ", step.PostValidators.Select(validator => validator.Name))}");
                }
            }
            else
            {
                context.Reports.Info($"  Step {step.Id}: {step.Name}");
            }
        }

        if (dependencyErrors.Count == 0)
        {
            context.Reports.Info("  Dependency check: passed");
        }
        else
        {
            context.Reports.Warning("Dependency check failed:");
            foreach (var error in dependencyErrors)
            {
                context.Reports.Warning($"  {error}");
            }
        }
    }

    private static bool HasExistingArticle(PipelineContext context, string namespaceName)
    {
        var toolFamilyDir = Path.Combine(context.OutputPath, "tool-family");
        if (!Directory.Exists(toolFamilyDir))
        {
            return false;
        }

        // Article filenames may differ due to brand mapping, but typically contain the namespace name
        var normalized = context.TargetMatcher.Normalize(namespaceName)
            .Replace(" ", "-", StringComparison.Ordinal)
            .ToLowerInvariant();

        return Directory.EnumerateFiles(toolFamilyDir, "*.md", SearchOption.TopDirectoryOnly)
            .Any(f => Path.GetFileNameWithoutExtension(f).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record StepExecutionOutcome(
        int ExitCode,
        StepResult Result,
        IReadOnlyList<CriticalFailureRecordReference> PersistedFailures);
}

/// <summary>A single row from an <c>--inspect</c> budget table run.</summary>
internal sealed record InspectBudgetRow(
    string Step,
    string Namespace,
    int EstimatedTokens,
    int Budget,
    int Headroom,
    string[] TopItems);
