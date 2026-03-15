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
        context.PlannedSteps = selectedSteps;
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

        var warnings = new List<string>();
        var globalSteps = selectedSteps.Where(step => step.Scope == StepScope.Global).ToArray();
        var namespaceSteps = selectedSteps.Where(step => step.Scope == StepScope.Namespace).ToArray();

        foreach (var step in globalSteps)
        {
            var stepExitCode = await ExecuteStepAsync(context, step, warnings, cancellationToken);
            if (stepExitCode != SuccessExitCode)
            {
                context.Workspaces.DeleteAll();
                return stepExitCode;
            }
        }

        context.CliOutput ??= await context.CliMetadataLoader.LoadCliOutputAsync(context.OutputPath, cancellationToken);
        context.CliVersion ??= await context.CliMetadataLoader.LoadCliVersionAsync(context.OutputPath, cancellationToken);

        var availableNamespaces = await context.CliMetadataLoader.LoadNamespacesAsync(context.OutputPath, cancellationToken);
        if (!ResolveNamespaces(context, availableNamespaces, out var resolvedNamespaces, out var namespaceError))
        {
            context.Reports.Error(namespaceError!);
            context.Workspaces.DeleteAll();
            return InvalidArgumentsExitCode;
        }

        context.SelectedNamespaces = resolvedNamespaces;
        context.Reports.Info($"Running {selectedSteps.Count} step(s) for {resolvedNamespaces.Count} namespace(s).");

        if (namespaceSteps.Length == 0)
        {
            return CompleteRun(context, warnings);
        }

        foreach (var namespaceName in resolvedNamespaces)
        {
            context.Reports.Info($"Namespace: {namespaceName}");
            context.Items["Namespace"] = namespaceName;

            foreach (var step in namespaceSteps)
            {
                var stepExitCode = await ExecuteStepAsync(context, step, warnings, cancellationToken);
                if (stepExitCode != SuccessExitCode)
                {
                    context.Workspaces.DeleteAll();
                    return stepExitCode;
                }
            }
        }

        return CompleteRun(context, warnings);
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

    private static async Task<int> ExecuteStepAsync(
        PipelineContext context,
        IPipelineStep step,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        context.Reports.Info($"  Step {step.Id}: {step.Name}");
        
        // Determine max attempts (1 + MaxRetries)
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
            
            // If successful or no retries available, break out
            if (result.Success || attempt == maxAttempts)
            {
                break;
            }
            
            // Validation failed, but we have more retries
            context.Reports.Warning($"    Step {step.Id} validation failed, retrying ({attempt}/{maxAttempts - 1})");
        }

        foreach (var warning in result.Warnings)
        {
            warnings.Add(warning);
            context.Reports.Warning(warning);
        }

        var stepExitCode = MapStepFailureExitCode(step.FailurePolicy, result.Success, result.ExitCodeOverride);
        if (stepExitCode != SuccessExitCode)
        {
            context.Reports.Error($"Step {step.Id} failed.");
        }

        return stepExitCode;
    }

    private static int CompleteRun(PipelineContext context, IReadOnlyCollection<string> warnings)
    {
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
