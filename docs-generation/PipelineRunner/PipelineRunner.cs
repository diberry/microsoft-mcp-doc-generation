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

    private readonly StepRegistry _stepRegistry;
    private readonly PipelineContextFactory _contextFactory;

    public PipelineRunner(StepRegistry stepRegistry, PipelineContextFactory contextFactory)
    {
        _stepRegistry = stepRegistry;
        _contextFactory = contextFactory;
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
        var stepRegistry = StepRegistry.CreateDefault(Path.Combine(resolvedRepoRoot, "docs-generation", "scripts"));
        return new PipelineRunner(stepRegistry, contextFactory);
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
        var dependencyErrors = ValidateDependencies(selectedSteps);

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

        var bootstrapExitCode = await BootstrapAsync(context, selectedSteps, cancellationToken);
        if (bootstrapExitCode != SuccessExitCode)
        {
            return bootstrapExitCode;
        }

        context.CliOutput = await context.CliMetadataLoader.LoadCliOutputAsync(context.OutputPath, cancellationToken);
        context.CliVersion = await context.CliMetadataLoader.LoadCliVersionAsync(context.OutputPath, cancellationToken);

        var availableNamespaces = await context.CliMetadataLoader.LoadNamespacesAsync(context.OutputPath, cancellationToken);
        if (!ResolveNamespaces(context, availableNamespaces, out var resolvedNamespaces, out var namespaceError))
        {
            context.Reports.Error(namespaceError!);
            return InvalidArgumentsExitCode;
        }

        context.SelectedNamespaces = resolvedNamespaces;
        context.Reports.Info($"Running {selectedSteps.Count} step(s) for {resolvedNamespaces.Count} namespace(s).");

        var warnings = new List<string>();
        foreach (var namespaceName in resolvedNamespaces)
        {
            context.Reports.Info($"Namespace: {namespaceName}");
            context.Items["Namespace"] = namespaceName;

            foreach (var step in selectedSteps)
            {
                context.Reports.Info($"  Step {step.Id}: {step.Name}");
                var result = await step.ExecuteAsync(context, cancellationToken);
                if (result.Success && !context.Request.SkipValidation && step.PostValidators.Count > 0)
                {
                    result = await RunPostValidatorsAsync(context, step, result, cancellationToken);
                }

                foreach (var warning in result.Warnings)
                {
                    warnings.Add(warning);
                    context.Reports.Warning(warning);
                }

                var stepExitCode = MapStepFailureExitCode(step.FailurePolicy, result.Success);
                if (stepExitCode != SuccessExitCode)
                {
                    context.Reports.Error($"Step {step.Id} failed.");
                    context.Workspaces.DeleteAll();
                    return stepExitCode;
                }
            }
        }

        if (warnings.Count > 0)
        {
            context.Reports.Info($"Pipeline completed with {warnings.Count} warning(s).");
        }
        else
        {
            context.Reports.Info("Pipeline completed successfully.");
        }

        context.Workspaces.DeleteAll();
        return SuccessExitCode;
    }

    public static int MapBootstrapExitCode(int exitCode)
        => exitCode switch
        {
            SuccessExitCode => SuccessExitCode,
            HumanReviewExitCode => HumanReviewExitCode,
            _ => FatalExitCode,
        };

    public static int MapStepFailureExitCode(FailurePolicy failurePolicy, bool stepSucceeded)
    {
        if (stepSucceeded || failurePolicy == FailurePolicy.Warn)
        {
            return SuccessExitCode;
        }

        return FatalExitCode;
    }

    private async Task<int> BootstrapAsync(
        PipelineContext context,
        IReadOnlyList<IPipelineStep> selectedSteps,
        CancellationToken cancellationToken)
    {
        var selectedDefinitions = selectedSteps.OfType<StepDefinition>().ToArray();
        if (selectedDefinitions.Any(step => step.RequiresAiConfiguration))
        {
            var probeResult = await context.AiCapabilityProbe.ProbeAsync(context.DocsGenerationRoot, cancellationToken);
            if (!probeResult.IsConfigured)
            {
                foreach (var missingKey in probeResult.MissingKeys)
                {
                    context.Reports.Error($"Missing required AI configuration: {missingKey}");
                }

                return FatalExitCode;
            }

            context.AiConfigured = true;
        }

        await context.BuildCoordinator.EnsureReadyAsync(
            Path.Combine(context.RepoRoot, "docs-generation.sln"),
            context.Request.SkipBuild,
            GetRequiredArtifacts(context.DocsGenerationRoot, selectedDefinitions),
            cancellationToken);

        var preflightScriptPath = Path.Combine(context.DocsGenerationRoot, "scripts", "preflight.ps1");
        var preflightArguments = new List<string>
        {
            "-OutputPath",
            context.OutputPath,
            "-SkipBuild",
            "-SkipEnvValidation",
        };

        var preflightResult = await context.ProcessRunner.RunPowerShellScriptAsync(
            preflightScriptPath,
            preflightArguments,
            context.RepoRoot,
            cancellationToken);

        if (!preflightResult.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(preflightResult.StandardError))
            {
                context.Reports.Error(preflightResult.StandardError.Trim());
            }

            return MapBootstrapExitCode(preflightResult.ExitCode);
        }

        return SuccessExitCode;
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
        out IReadOnlyList<string> resolvedNamespaces,
        out string? error)
    {
        if (!string.IsNullOrWhiteSpace(context.Request.Namespace))
        {
            var normalizedRequest = context.TargetMatcher.Normalize(context.Request.Namespace!);
            var match = availableNamespaces.FirstOrDefault(candidate => string.Equals(candidate, normalizedRequest, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                resolvedNamespaces = Array.Empty<string>();
                error = $"Unknown namespace '{context.Request.Namespace}'.";
                return false;
            }

            resolvedNamespaces = [match];
            error = null;
            return true;
        }

        resolvedNamespaces = availableNamespaces;
        error = null;
        return true;
    }

    private static IReadOnlyList<string> GetRequiredArtifacts(string docsGenerationRoot, IReadOnlyList<StepDefinition> selectedSteps)
    {
        var requiredProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BrandMapperValidator",
            "E2eTestPromptParser",
            "AzmcpCommandParser",
        };

        foreach (var step in selectedSteps)
        {
            foreach (var project in step.Id switch
            {
                1 => ["CSharpGenerator", "ToolGeneration_Raw"],
                2 => ["ExamplePromptGeneratorStandalone", "ExamplePromptValidator"],
                3 => ["ToolGeneration_Composed", "ToolGeneration_Improved"],
                4 => ["ToolFamilyCleanup"],
                5 => ["SkillsRelevance"],
                6 => ["HorizontalArticleGenerator"],
                _ => Array.Empty<string>(),
            })
            {
                requiredProjects.Add(project);
            }
        }

        return requiredProjects
            .Select(project => Path.Combine(docsGenerationRoot, project, "bin", "Release", "net9.0", $"{project}.dll"))
            .ToArray();
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
}
