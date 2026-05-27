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
public sealed class ArticleHealthValidatorStep : NamespaceStepBase
{
    private const string ScriptRelativePath = "validation/Test-ArticleHealth.ps1";
    private const string GateConfigRelativePath = "data/validation-gate-config.json";
    private const string ValidationSubdir = "validation";
    private const string ArtifactFileName = "article-health.json";

    public ArticleHealthValidatorStep()
        : base(
            7,
            "Validate article health",
            FailurePolicy.Warn,
            dependsOn: [4],
            expectedOutputs: [$"{ValidationSubdir}/{ArtifactFileName}", $"{ValidationSubdir}/validation-summary.md"])
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
        var validationDir = Path.Combine(context.OutputPath, ValidationSubdir);
        var outputJsonPath = Path.Combine(validationDir, ArtifactFileName);

        // Determine which article files exist for this namespace only
        var articlePaths = ResolveArticlePaths(toolFamilyDir, currentNamespace);

        if (articlePaths.Count == 0)
        {
            warnings.Add($"No tool-family article files found for namespace '{currentNamespace}' at '{toolFamilyDir}'. Article health validation skipped.");
            artifactFailures.Add(CreateArtifactFailure(
                "article-health",
                ArtifactFileName,
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
            throw; // Do not swallow cancellation requests
        }
        catch (Exception ex)
        {
            warnings.Add($"Article health script failed to launch: {ex.Message}");
            artifactFailures.Add(CreateArtifactFailure(
                "article-health",
                ArtifactFileName,
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
        WriteSummaryMarkdown(
            validationDir,
            currentNamespace,
            gateMode,
            normalized,
            outputJsonPath);

        return BuildResult(context, processResults, success, warnings, artifactFailures: artifactFailures);
    }

    private static bool DetermineSuccess(
        ValidationVerdict verdict,
        string gateMode,
        List<string> warnings,
        List<ArtifactFailure> artifactFailures,
        string outputJsonPath)
    {
        switch (verdict)
        {
            case ValidationVerdict.Pass:
                return true;

            case ValidationVerdict.Warn:
                if (string.Equals(gateMode, "block", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("Article health verdict is 'warn' and gate mode is 'block'. Step failed.");
                    artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                    return false;
                }
                // warn mode: warn findings are non-blocking
                return true;

            case ValidationVerdict.Fail:
                warnings.Add("Article health verdict is 'fail'. Step failed.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            case ValidationVerdict.ScriptError:
                warnings.Add("Article health script encountered an execution error.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            case ValidationVerdict.ArtifactError:
                warnings.Add("Article health artifact is missing, malformed, or stale.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            default:
                warnings.Add($"Unknown validation verdict: {verdict}.");
                return false;
        }
    }

    private static ArtifactFailure CreateBlockingFailure(ValidationVerdict verdict, string outputJsonPath)
        => ArtifactFailure.Create(
            "article-health",
            ArtifactFileName,
            $"Article health validation returned '{verdict.ToString().ToLowerInvariant()}' verdict.",
            relatedPaths: [outputJsonPath]);

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

    private static void EnsureValidationDirectory(string validationDir)
    {
        Directory.CreateDirectory(validationDir);
        // Remove stale artifact from a prior run to prevent false positives
        var staleArtifact = Path.Combine(validationDir, ArtifactFileName);
        if (File.Exists(staleArtifact))
        {
            File.Delete(staleArtifact);
        }
    }

    private static string ReadGateMode(string mcpToolsRoot, List<string> warnings)
    {
        var configPath = Path.Combine(mcpToolsRoot, GateConfigRelativePath);
        if (!File.Exists(configPath))
        {
            warnings.Add($"Gate config not found at '{configPath}'; defaulting to 'warn' mode.");
            return "warn";
        }

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("gateMode", out var gm))
            {
                var mode = gm.GetString()?.ToLowerInvariant() ?? "";
                if (mode is "warn" or "block")
                {
                    return mode;
                }

                warnings.Add($"Unrecognized gateMode value '{gm.GetString()}' in '{configPath}'; defaulting to 'warn'.");
                return "warn";
            }

            warnings.Add($"Gate config at '{configPath}' missing 'gateMode' property; defaulting to 'warn'.");
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to read gate config at '{configPath}': {ex.Message}. Defaulting to 'warn' mode.");
        }

        return "warn";
    }

    private static void WriteSummaryMarkdown(
        string validationDir,
        string currentNamespace,
        string gateMode,
        ValidationNormalizedResult normalized,
        string artifactJsonPath)
    {
        try
        {
            var summaryPath = Path.Combine(validationDir, "validation-summary.md");
            var lines = new List<string>
            {
                "# Validation Summary",
                string.Empty,
                $"**Namespace:** {currentNamespace}",
                $"**Gate mode:** {gateMode}",
                $"**Article health verdict:** {normalized.Verdict.ToString().ToLowerInvariant()}",
                $"**Final verdict:** {normalized.Verdict.ToString().ToLowerInvariant()}",
                string.Empty,
            };

            if (normalized.Diagnostics.Count > 0)
            {
                lines.Add("## Diagnostics");
                lines.Add(string.Empty);
                foreach (var d in normalized.Diagnostics)
                {
                    lines.Add($"- {d}");
                }

                lines.Add(string.Empty);
            }

            lines.Add("## Artifacts");
            lines.Add(string.Empty);
            lines.Add($"- Article health JSON: `{artifactJsonPath}`");

            File.WriteAllLines(summaryPath, lines);
        }
        catch
        {
            // Non-critical: summary write failure should not affect step success
        }
    }
}
