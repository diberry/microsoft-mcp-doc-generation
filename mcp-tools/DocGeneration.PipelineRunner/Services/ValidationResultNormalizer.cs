using System.Text.Json;

namespace PipelineRunner.Services;

/// <summary>
/// Pipeline-owned verdict produced by normalizing a raw script execution result.
/// </summary>
public enum ValidationVerdict
{
    Pass,
    Warn,
    Fail,
    ScriptError,
    ArtifactError,
}

/// <summary>
/// Normalized result returned to the pipeline step after verdict computation.
/// </summary>
public sealed record ValidationNormalizedResult(
    ValidationVerdict Verdict,
    string RunId,
    string Namespace,
    string? ScriptVerdict,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Converts raw script execution and artifact data into a pipeline-owned verdict.
/// Rules (from PRD §6.2 and §7):
/// - Script launch failure or missing artifact → <see cref="ValidationVerdict.ScriptError"/> or <see cref="ValidationVerdict.ArtifactError"/>.
/// - Malformed JSON, missing required fields, run-id mismatch, or stale timestamp → <see cref="ValidationVerdict.ArtifactError"/>.
/// - Artifact verdict drives the pipeline verdict; exit code signals execution success only.
/// </summary>
public static class ValidationResultNormalizer
{
    // Maximum age of a validation artifact before it is considered stale.
    private static readonly TimeSpan StaleArtifactThreshold = TimeSpan.FromHours(1);

    /// <summary>
    /// Normalizes a script execution result into a pipeline verdict.
    /// </summary>
    public static ValidationNormalizedResult Normalize(
        ValidationScriptResult scriptResult,
        string expectedRunId,
        string expectedNamespace)
    {
        var diagnostics = new List<string>();

        // Script failed to launch (exit code non-zero before artifact is checked)
        if (!scriptResult.JsonArtifactExists)
        {
            if (!scriptResult.Succeeded)
            {
                diagnostics.Add($"Script exited with code {scriptResult.ExitCode}.");
                if (!string.IsNullOrWhiteSpace(scriptResult.StdErr))
                {
                    diagnostics.Add(scriptResult.StdErr.Trim());
                }

                return new ValidationNormalizedResult(ValidationVerdict.ScriptError, expectedRunId, expectedNamespace, null, diagnostics);
            }

            diagnostics.Add($"Script succeeded (exit 0) but JSON artifact was not written to '{scriptResult.OutputJsonPath}'.");
            return new ValidationNormalizedResult(ValidationVerdict.ArtifactError, expectedRunId, expectedNamespace, null, diagnostics);
        }

        // Parse the artifact
        string jsonText;
        try
        {
            jsonText = File.ReadAllText(scriptResult.OutputJsonPath);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Cannot read artifact '{scriptResult.OutputJsonPath}': {ex.Message}");
            return new ValidationNormalizedResult(ValidationVerdict.ArtifactError, expectedRunId, expectedNamespace, null, diagnostics);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonText);
        }
        catch (JsonException ex)
        {
            diagnostics.Add($"Artifact JSON is malformed: {ex.Message}");
            return new ValidationNormalizedResult(ValidationVerdict.ArtifactError, expectedRunId, expectedNamespace, null, diagnostics);
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Verify schemaVersion
            if (!root.TryGetProperty("schemaVersion", out var sv) || sv.GetString() != "1.0")
            {
                diagnostics.Add("Artifact missing 'schemaVersion: 1.0'.");
                return new ValidationNormalizedResult(ValidationVerdict.ArtifactError, expectedRunId, expectedNamespace, null, diagnostics);
            }

            // Verify runId echo
            var artifactRunId = root.TryGetProperty("runId", out var rid) ? rid.GetString() ?? "" : "";
            if (!string.IsNullOrEmpty(expectedRunId) &&
                !string.Equals(artifactRunId, expectedRunId, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Run-id mismatch: expected '{expectedRunId}', artifact contains '{artifactRunId}'.");
                return new ValidationNormalizedResult(ValidationVerdict.ArtifactError, expectedRunId, expectedNamespace, null, diagnostics);
            }

            // Verify generatedAt is recent
            if (root.TryGetProperty("generatedAt", out var gat))
            {
                var generatedAtStr = gat.GetString();
                if (DateTimeOffset.TryParse(generatedAtStr, out var generatedAt))
                {
                    var age = DateTimeOffset.UtcNow - generatedAt;
                    if (age > StaleArtifactThreshold)
                    {
                        diagnostics.Add($"Artifact timestamp '{generatedAtStr}' is stale (age: {age:hh\\:mm\\:ss}).");
                        return new ValidationNormalizedResult(ValidationVerdict.ArtifactError, artifactRunId, expectedNamespace, null, diagnostics);
                    }
                }
            }

            // Read script-level verdict
            var scriptVerdict = root.TryGetProperty("verdict", out var v) ? v.GetString()?.ToLowerInvariant() ?? "" : "";
            if (string.IsNullOrEmpty(scriptVerdict))
            {
                diagnostics.Add("Artifact missing required 'verdict' field.");
                return new ValidationNormalizedResult(ValidationVerdict.ArtifactError, artifactRunId, expectedNamespace, null, diagnostics);
            }

            // Collect check-level diagnostics for warn/fail
            if (root.TryGetProperty("checks", out var checks))
            {
                foreach (var check in checks.EnumerateArray())
                {
                    var status = check.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    if (status is "warn" or "fail")
                    {
                        var name = check.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "(unknown)";
                        var detail = check.TryGetProperty("detail", out var d) ? d.GetString() ?? "" : "";
                        diagnostics.Add($"[{status.ToUpperInvariant()}] {name}{(string.IsNullOrWhiteSpace(detail) ? "" : $": {detail}")}");
                    }
                }
            }

            var verdict = scriptVerdict switch
            {
                "pass" => ValidationVerdict.Pass,
                "warn" => ValidationVerdict.Warn,
                "fail" => ValidationVerdict.Fail,
                _ => ValidationVerdict.ArtifactError,
            };

            if (verdict == ValidationVerdict.ArtifactError)
            {
                diagnostics.Add($"Artifact contains unrecognized verdict value: '{scriptVerdict}'.");
            }

            var resolvedNamespace = root.TryGetProperty("namespace", out var ns)
                ? ns.GetString() ?? expectedNamespace
                : expectedNamespace;

            return new ValidationNormalizedResult(verdict, artifactRunId, resolvedNamespace, scriptVerdict, diagnostics);
        }
    }
}
