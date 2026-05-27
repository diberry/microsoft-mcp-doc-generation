using System.Text.Json;
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
public sealed class CoverageAuditStep : NamespaceStepBase
{
    private const string ScriptRelativePath = "validation/Scan-McpToolCoverage.ps1";
    private const string GateConfigRelativePath = "data/validation-gate-config.json";
    private const string ValidationSubdir = "validation";
    private const string ArtifactFileName = "coverage-audit.json";
    private const string CliOutputRelativePath = "cli/cli-output.json";

    public CoverageAuditStep()
        : base(
            8,
            "Validate tool coverage",
            FailurePolicy.Warn,
            dependsOn: [0, 4],
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

        var toolsJsonPath = Path.Combine(context.OutputPath, CliOutputRelativePath);
        var articlesDir = Path.Combine(context.OutputPath, "tool-family");
        var validationDir = Path.Combine(context.OutputPath, ValidationSubdir);
        var outputJsonPath = Path.Combine(validationDir, ArtifactFileName);

        // Verify prerequisites exist
        if (!File.Exists(toolsJsonPath))
        {
            warnings.Add($"CLI output not found at '{toolsJsonPath}'. Coverage audit skipped (bootstrap may not have run).");
            artifactFailures.Add(CreateArtifactFailure(
                "coverage-audit",
                ArtifactFileName,
                $"CLI metadata (cli-output.json) not found for namespace '{currentNamespace}'.",
                warnings,
                [toolsJsonPath]));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        if (!Directory.Exists(articlesDir))
        {
            warnings.Add($"Articles directory not found at '{articlesDir}'. Coverage audit skipped.");
            artifactFailures.Add(CreateArtifactFailure(
                "coverage-audit",
                ArtifactFileName,
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
        EnsureValidationDirectory(validationDir, outputJsonPath);

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
                "coverage-audit",
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
                    warnings.Add("Coverage audit verdict is 'warn' and gate mode is 'block'. Step failed.");
                    artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                    return false;
                }
                // warn mode: param/annotation gaps are non-blocking
                return true;

            case ValidationVerdict.Fail:
                warnings.Add("Coverage audit verdict is 'fail' (missing tools detected). Step failed.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            case ValidationVerdict.ScriptError:
                warnings.Add("Coverage audit script encountered an execution error.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            case ValidationVerdict.ArtifactError:
                warnings.Add("Coverage audit artifact is missing, malformed, or stale.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            default:
                warnings.Add($"Unknown validation verdict: {verdict}.");
                return false;
        }
    }

    private static ArtifactFailure CreateBlockingFailure(ValidationVerdict verdict, string outputJsonPath)
        => ArtifactFailure.Create(
            "coverage-audit",
            ArtifactFileName,
            $"Coverage audit validation returned '{verdict.ToString().ToLowerInvariant()}' verdict.",
            relatedPaths: [outputJsonPath]);

    private static void EnsureValidationDirectory(string validationDir, string artifactPath)
    {
        Directory.CreateDirectory(validationDir);
        if (File.Exists(artifactPath))
        {
            File.Delete(artifactPath);
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

            // Append to summary if article health already wrote it
            var lines = new List<string>();
            if (File.Exists(summaryPath))
            {
                lines.AddRange(File.ReadAllLines(summaryPath));
                lines.Add(string.Empty);
                lines.Add("---");
                lines.Add(string.Empty);
            }
            else
            {
                lines.Add("# Validation Summary");
                lines.Add(string.Empty);
                lines.Add($"**Namespace:** {currentNamespace}");
                lines.Add($"**Gate mode:** {gateMode}");
                lines.Add(string.Empty);
            }

            lines.Add("## Coverage Audit");
            lines.Add(string.Empty);
            lines.Add($"**Verdict:** {normalized.Verdict.ToString().ToLowerInvariant()}");
            lines.Add(string.Empty);

            if (normalized.Diagnostics.Count > 0)
            {
                lines.Add("### Diagnostics");
                lines.Add(string.Empty);
                foreach (var d in normalized.Diagnostics)
                {
                    lines.Add($"- {d}");
                }

                lines.Add(string.Empty);
            }

            lines.Add("### Artifacts");
            lines.Add(string.Empty);
            lines.Add($"- Coverage audit JSON: `{artifactJsonPath}`");

            File.WriteAllLines(summaryPath, lines);
        }
        catch
        {
            // Non-critical: summary write failure should not affect step success
        }
    }
}
