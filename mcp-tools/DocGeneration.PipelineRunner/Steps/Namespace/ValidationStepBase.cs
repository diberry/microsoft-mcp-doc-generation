using System.Text.Json;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

/// <summary>
/// Base class for validation pipeline steps (Steps 7 and 8) that share common patterns:
/// gate config reading, verdict-to-success mapping, stale artifact cleanup, and summary
/// markdown generation.
/// <para>
/// Both Step 7 (ArticleHealthValidator) and Step 8 (CoverageAudit) write to
/// <c>validation-summary.md</c>. Sequential execution is guaranteed by dependency
/// declarations (<c>dependsOn</c>). If parallelization is ever introduced, a
/// file-locking mechanism must be added to <see cref="WriteSummarySection"/>.
/// </para>
/// </summary>
public abstract class ValidationStepBase : NamespaceStepBase
{
    private const string GateConfigRelativePath = "data/validation-gate-config.json";
    private const string ValidationSubdir = "validation";
    private const string SummaryFileName = "validation-summary.md";

    /// <summary>
    /// A short display name for this validation step (e.g., "Article health", "Coverage audit").
    /// Used in warning messages and summary markdown.
    /// </summary>
    protected abstract string ValidationDisplayName { get; }

    /// <summary>
    /// The identifier used for artifact failure records (e.g., "article-health", "coverage-audit").
    /// </summary>
    protected abstract string ValidationId { get; }

    /// <summary>
    /// The file name of the JSON artifact produced by this step's script.
    /// </summary>
    protected abstract string ArtifactFileName { get; }

    protected ValidationStepBase(
        int id,
        string name,
        FailurePolicy failurePolicy,
        IReadOnlyList<int>? dependsOn = null,
        IReadOnlyList<string>? expectedOutputs = null)
        : base(
            id,
            name,
            failurePolicy,
            dependsOn,
            expectedOutputs: expectedOutputs)
    {
    }

    /// <summary>
    /// Reads the gate mode from <c>mcp-tools/data/validation-gate-config.json</c>.
    /// Falls back to "warn" if the file is missing, corrupt, or contains an unrecognized value.
    /// </summary>
    protected static string ReadGateMode(string mcpToolsRoot, List<string> warnings)
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

    /// <summary>
    /// Maps a <see cref="ValidationVerdict"/> and gate mode to a boolean success value.
    /// Adds appropriate warnings and artifact failures for non-pass verdicts.
    /// </summary>
    protected bool DetermineSuccess(
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
                    warnings.Add($"{ValidationDisplayName} verdict is 'warn' and gate mode is 'block'. Step failed.");
                    artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                    return false;
                }
                // warn mode: warn findings are non-blocking
                return true;

            case ValidationVerdict.Fail:
                warnings.Add($"{ValidationDisplayName} verdict is 'fail'. Step failed.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            case ValidationVerdict.ScriptError:
                warnings.Add($"{ValidationDisplayName} script encountered an execution error.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            case ValidationVerdict.ArtifactError:
                warnings.Add($"{ValidationDisplayName} artifact is missing, malformed, or stale.");
                artifactFailures.Add(CreateBlockingFailure(verdict, outputJsonPath));
                return false;

            default:
                warnings.Add($"Unknown validation verdict: {verdict}.");
                return false;
        }
    }

    /// <summary>
    /// Creates an <see cref="ArtifactFailure"/> for a blocking validation verdict.
    /// </summary>
    protected ArtifactFailure CreateBlockingFailure(ValidationVerdict verdict, string outputJsonPath)
        => ArtifactFailure.Create(
            ValidationId,
            ArtifactFileName,
            $"{ValidationDisplayName} validation returned '{verdict.ToString().ToLowerInvariant()}' verdict.",
            relatedPaths: [outputJsonPath]);

    /// <summary>
    /// Creates the validation directory and removes any stale artifact from a prior run.
    /// </summary>
    protected void EnsureValidationDirectory(string validationDir)
    {
        Directory.CreateDirectory(validationDir);
        var staleArtifact = Path.Combine(validationDir, ArtifactFileName);
        if (File.Exists(staleArtifact))
        {
            File.Delete(staleArtifact);
        }
    }

    /// <summary>
    /// Writes (or appends) a section to <c>validation-summary.md</c>.
    /// If the file already exists (e.g., a prior validation step wrote it), appends with a separator.
    /// If not, creates a fresh summary with header.
    /// </summary>
    protected void WriteSummarySection(
        string validationDir,
        string currentNamespace,
        string gateMode,
        ValidationNormalizedResult normalized,
        string artifactJsonPath)
    {
        try
        {
            var summaryPath = Path.Combine(validationDir, SummaryFileName);
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

            lines.Add($"## {ValidationDisplayName}");
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
            lines.Add($"- {ValidationDisplayName} JSON: `{artifactJsonPath}`");

            File.WriteAllLines(summaryPath, lines);
        }
        catch
        {
            // Non-critical: summary write failure should not affect step success
        }
    }

    /// <summary>
    /// Resolves the validation output directory path.
    /// </summary>
    protected static string GetValidationDir(string outputPath) =>
        Path.Combine(outputPath, ValidationSubdir);
}
