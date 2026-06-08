using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

/// <summary>
/// Pipeline step that invokes <c>Scan-McpToolCoverage.ps1</c> to verify that all tools
/// in the CLI metadata have corresponding documentation in the assembled tool-family articles.
/// <para>
/// Gate mode is read from <c>mcp-tools/data/validation-gate-config.json</c>.
/// In <c>warn</c> mode, <c>warn</c> findings (missing params/annotation mismatches) do not
/// fail the step; <c>fail</c> (missing tools) always fails.
/// In <c>block</c> mode, both <c>warn</c> and <c>fail</c> findings fail the step.
/// </para>
/// </summary>
public sealed class CoverageAuditStep : ValidationStepBase
{
    private const string ScriptRelativePath = "validation/Scan-McpToolCoverage.ps1";
    private const string ArtifactFileNameConst = "coverage-audit.json";
    private const string CliOutputRelativePath = "cli/cli-output.json";

    protected override string ValidationDisplayName => "Coverage audit";
    protected override string ValidationId => "coverage-audit";
    protected override string ArtifactFileName => ArtifactFileNameConst;

    public CoverageAuditStep()
        : base(
            8,
            "Validate tool coverage",
            FailurePolicy.Warn,
            dependsOn: [0, 4, 7],
            expectedOutputs: [$"validation/{ArtifactFileNameConst}", "validation/validation-summary.md"])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        var currentNamespace = GetCurrentNamespace(context);
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var artifactFailures = new List<ArtifactFailure>();

        // Resolve paths
        var scriptPath = Path.GetFullPath(
            Path.Combine(context.McpToolsRoot, ScriptRelativePath));

        var toolsJsonPath = Path.Combine(context.OutputPath, CliOutputRelativePath);
        var articlesDir = Path.Combine(context.OutputPath, "tool-family");
        var validationDir = GetValidationDir(context.OutputPath);
        var outputJsonPath = Path.Combine(validationDir, ArtifactFileNameConst);

        // Verify prerequisites exist
        if (!File.Exists(toolsJsonPath))
        {
            warnings.Add($"CLI output not found at '{toolsJsonPath}'. Coverage audit skipped (bootstrap may not have run).");
            artifactFailures.Add(CreateArtifactFailure(
                ValidationId,
                ArtifactFileNameConst,
                $"CLI metadata (cli-output.json) not found for namespace '{currentNamespace}'.",
                warnings,
                [toolsJsonPath]));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        if (!Directory.Exists(articlesDir))
        {
            warnings.Add($"Articles directory not found at '{articlesDir}'. Coverage audit skipped.");
            artifactFailures.Add(CreateArtifactFailure(
                ValidationId,
                ArtifactFileNameConst,
                $"Tool-family articles directory not found for namespace '{currentNamespace}'.",
                warnings,
                [articlesDir]));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        // Read gate mode from config
        var gateMode = ReadGateMode(context.McpToolsRoot, warnings);

        // Generate a run identifier for this invocation
        var runId = Guid.NewGuid().ToString();

        // Clean stale artifacts before running
        EnsureValidationDirectory(validationDir);

        // Build and execute the script request
        var request = new ValidationScriptRequest(
            ScriptPath: scriptPath,
            RunId: runId,
            Namespace: currentNamespace,
            RepoRoot: context.RepoRoot,
            OutputRoot: context.OutputPath,
            OutputJsonPath: outputJsonPath,
            AdditionalArguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["-ToolsJsonPath"] = toolsJsonPath,
                ["-ArticlesDir"] = articlesDir,
            });

        var scriptRunner = new ValidationScriptRunner(context.ProcessRunner);
        ValidationScriptResult scriptResult;
        try
        {
            scriptResult = await scriptRunner.RunAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnings.Add($"Coverage audit script failed to launch: {ex.Message}");
            artifactFailures.Add(CreateArtifactFailure(
                ValidationId,
                ArtifactFileNameConst,
                "Script launch exception.",
                [$"Exception: {ex.Message}"],
                [scriptPath]));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        // Record a synthetic ProcessExecutionResult for the display command
        processResults.Add(new ProcessExecutionResult(
            "pwsh",
            ["-File", scriptPath, "-RunId", runId, "-Namespace", currentNamespace, "-OutputJson", outputJsonPath],
            context.RepoRoot,
            scriptResult.ExitCode,
            scriptResult.StdOut,
            scriptResult.StdErr,
            scriptResult.CompletedAt - scriptResult.StartedAt));

        // Normalize the result
        var normalized = ValidationResultNormalizer.Normalize(scriptResult, runId, currentNamespace);

        // Emit diagnostic warnings regardless of verdict
        foreach (var diagnostic in normalized.Diagnostics)
        {
            warnings.Add(diagnostic);
        }

        // Determine step success based on gate mode and verdict
        var success = DetermineSuccess(normalized.Verdict, gateMode, warnings, artifactFailures, outputJsonPath);

        // Write the validation summary
        WriteSummarySection(validationDir, currentNamespace, gateMode, normalized, outputJsonPath);

        return BuildResult(context, processResults, success, warnings, artifactFailures: artifactFailures);
    }
}
