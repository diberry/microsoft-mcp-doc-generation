using DocGeneration.Core.Tracing;
using PipelineRunner.Cli;
using PipelineRunner.Contracts;
using PipelineRunner.Context;
using PipelineRunner.Registry;
using PipelineRunner.Services;

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
    private IPipelineTracer _currentTracer = NullTracer.Instance;

    public PipelineRunner(
        StepRegistry stepRegistry,
        PipelineContextFactory contextFactory,
        IChangelogGate? changelogGate = null,
        IFingerprintGate? fingerprintGate = null,
        IPromptRegressionGate? promptRegressionGate = null,
        IBrandMappingLoader? brandMappingLoader = null)
    {
        _stepRegistry = stepRegistry;
        _contextFactory = contextFactory;
        _changelogGate = changelogGate;
        _fingerprintGate = fingerprintGate;
        _promptRegressionGate = promptRegressionGate;
        _brandMappingLoader = brandMappingLoader ?? new BrandMappingLoader();
    }

    public static PipelineRunner CreateDefault(string? repoRoot = null, TextWriter? output = null, TextWriter? error = null)
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
        return new PipelineRunner(stepRegistry, contextFactory, new ChangelogGate(), fingerprintGate, promptRegressionGate);
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

        var context = await _contextFactory.CreateAsync(request, cancellationToken);
        var selectedSteps = _stepRegistry.GetOrderedSteps(request.Steps);
        context.PlannedSteps = selectedSteps;
        var dependencyErrors = request.SkipDependencyValidation
            ? Array.Empty<string>()
            : ValidateDependencies(selectedSteps);

        if (request.DryRun)
        {
            WriteDryRunPlan(context, selectedSteps, dependencyErrors);
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
