using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// T23: provenance completeness and pinned capture identity.
/// </summary>
public sealed class ProvenanceTests
{
    private static readonly Regex Sha40 = new("^[0-9a-f]{40}$", RegexOptions.Compiled);

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
    }
}
