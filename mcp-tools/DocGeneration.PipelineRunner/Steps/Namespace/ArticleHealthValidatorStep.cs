using System.Text.Json;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

/// <summary>
/// Pipeline step that invokes <c>Test-ArticleHealth.ps1</c> against the assembled
/// tool-family articles for the current namespace and normalizes the result into a
/// pipeline verdict.
/// <para>
/// Gate mode is read from <c>mcp-tools/data/validation-gate-config.json</c>.
/// In <c>warn</c> mode, <c>warn</c> findings do not fail the step; <c>fail</c>,
/// <c>script_error</c>, and <c>artifact_error</c> do.
/// In <c>block</c> mode, both <c>warn</c> and <c>fail</c> findings fail the step.
/// </para>
/// </summary>
public sealed class ArticleHealthValidatorStep : ValidationStepBase
{
    private const string ScriptRelativePath = "validation/Test-ArticleHealth.ps1";
    private const string ArtifactFileNameConst = "article-health.json";

    protected override string ValidationDisplayName => "Article health";
    protected override string ValidationId => "article-health";
    protected override string ArtifactFileName => ArtifactFileNameConst;

    public ArticleHealthValidatorStep()
        : base(
            7,
            "Validate article health",
            FailurePolicy.Warn,
            dependsOn: [4],
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

        var toolFamilyDir = Path.Combine(context.OutputPath, "tool-family");
        var validationDir = GetValidationDir(context.OutputPath);
        var outputJsonPath = Path.Combine(validationDir, ArtifactFileNameConst);

        // Determine which article files exist for this namespace only
        var articlePaths = ResolveArticlePaths(toolFamilyDir, currentNamespace);

        if (articlePaths.Count == 0)
        {
            warnings.Add($"No tool-family article files found for namespace '{currentNamespace}' at '{toolFamilyDir}'. Article health validation skipped.");
            artifactFailures.Add(CreateArtifactFailure(
                ValidationId,
                ArtifactFileNameConst,
                $"No assembled articles found for namespace '{currentNamespace}'.",
                warnings,
                [toolFamilyDir]));
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
            ArticlePaths: articlePaths,
            AdditionalArguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

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
            warnings.Add($"Article health script failed to launch: {ex.Message}");
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

    private static IReadOnlyList<string> ResolveArticlePaths(string toolFamilyDir, string currentNamespace)
    {
        if (!Directory.Exists(toolFamilyDir))
        {
            return Array.Empty<string>();
        }

        // Filter to only articles belonging to the current namespace to prevent
        // cross-namespace contamination in multi-namespace runs.
        // Convention: tool-family articles are named {namespace}.md or {brand-name}.md.
        return Directory.GetFiles(toolFamilyDir, "*.md", SearchOption.TopDirectoryOnly)
            .Where(p => IsArticleForNamespace(p, currentNamespace))
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsArticleForNamespace(string filePath, string currentNamespace)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        // Match exact namespace name or namespace-prefixed files (e.g., "storage" matches "storage.md")
        return string.Equals(fileName, currentNamespace, StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith($"{currentNamespace}-", StringComparison.OrdinalIgnoreCase);
    }
}
