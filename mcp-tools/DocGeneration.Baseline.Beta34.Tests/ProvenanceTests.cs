using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// T23: provenance completeness and pinned capture identity, plus the AI-provenance derivation guard
/// (Ellis blocking-4 / Cameron note 5).
/// </summary>
public sealed class ProvenanceTests
{
    private static readonly Regex Sha40 = new("^[0-9a-f]{40}$", RegexOptions.Compiled);

    // Semantic-version shape: MAJOR.MINOR.PATCH with optional pre-release / build metadata.
    private static readonly Regex SemVer =
        new(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.Compiled);

    // T23
    [Fact]
    public void T23_Provenance_Is_Complete_And_NonEmpty()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        Assert.NotNull(manifest.Provenance);
        Provenance p = manifest.Provenance!;

        Assert.Equal(BaselineContext.ExpectedAzureMcpBuild, p.AzureMcpBuild);
        Assert.Equal(BaselineContext.ExpectedSourceRunDir, p.SourceRunDir);

        Assert.False(string.IsNullOrWhiteSpace(p.RepoCommitSha), "repoCommitSha must be non-empty.");
        Assert.Matches(Sha40, p.RepoCommitSha!.ToLowerInvariant());

        Assert.False(string.IsNullOrWhiteSpace(p.CaptureTimestampUtc), "captureTimestampUtc must be non-empty.");
        Assert.True(
            DateTimeOffset.TryParse(p.CaptureTimestampUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _),
            $"captureTimestampUtc is not parseable: '{p.CaptureTimestampUtc}'.");

        // sanitizerVersion is carried by the manifest (AD-028) and MUST be modeled + asserted
        // (Cameron note 4): non-empty and semver-shaped.
        Assert.False(string.IsNullOrWhiteSpace(p.SanitizerVersion), "sanitizerVersion must be non-empty.");
        Assert.Matches(SemVer, p.SanitizerVersion!);
    }

    // Ai_Provenance_Is_Derived_From_RunLogs_Not_SampleEnv (Ellis blocking-4 / Cameron note 5)
    [Fact]
    public void Ai_Provenance_Is_Derived_From_RunLogs_Not_SampleEnv()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        Assert.NotNull(manifest.Provenance);
        JsonElement ai = manifest.Provenance!.Ai;
        Assert.Equal(JsonValueKind.Object, ai.ValueKind);

        Assert.Equal("run-log", GetString(ai, "source"));
        Assert.Equal("gpt-5-mini", GetString(ai, "model"));
        Assert.Equal("2025-03-01-preview", GetString(ai, "apiVersion"));

        // Both AI steps must independently report the run-log-derived values.
        foreach (string step in new[] { "step2ExamplePrompts", "step4ToolFamilyCleanup" })
        {
            Assert.True(ai.TryGetProperty(step, out JsonElement stepEl),
                $"provenance.ai is missing the '{step}' sub-object.");
            Assert.Equal("run-log", GetString(stepEl, "source"));
            Assert.Equal("gpt-5-mini", GetString(stepEl, "model"));
            Assert.Equal("2025-03-01-preview", GetString(stepEl, "apiVersion"));
        }

        // The stale sample.env / earlier-guess values must NOT appear ANYWHERE in the manifest bytes.
        string manifestText = File.ReadAllText(BaselineContext.ManifestPath);
        foreach (string forbidden in new[] { "gpt-4.1-mini", "gpt-4o", "2025-01-01-preview" })
        {
            Assert.DoesNotContain(forbidden, manifestText, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string GetString(JsonElement obj, string prop)
    {
        Assert.True(obj.TryGetProperty(prop, out JsonElement el), $"Missing property '{prop}'.");
        return el.GetString() ?? "";
    }
}
