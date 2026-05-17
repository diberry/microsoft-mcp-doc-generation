using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ChangelogGateTests
{
    private const string BaselineVersion = "1.2.3";

    // ─── New namespace (hasExistingArticle = false) ────────────────────────────

    [Fact]
    public async Task EvaluateAsync_NewNamespace_AlwaysProcesses()
    {
        var gate = CreateGate("""
            ## [1.2.3]
            - compute: new tool
            """);

        var result = await gate.EvaluateAsync("storage", BaselineVersion, "main", hasExistingArticle: false, CancellationToken.None);

        Assert.False(result.ShouldSkip);
        Assert.Contains("new namespace", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Namespace mentioned ───────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_NamespaceMentionedInRelevantSection_Processes()
    {
        var gate = CreateGate("""
            ## [1.2.3]
            ### Changed
            - storage: improved reliability
            - compute: new list command
            """);

        var result = await gate.EvaluateAsync("storage", BaselineVersion, "main", hasExistingArticle: true, CancellationToken.None);

        Assert.False(result.ShouldSkip);
        Assert.Contains("storage", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_NamespaceMentionedInUnreleasedSection_Processes()
    {
        var gate = CreateGate("""
            ## [Unreleased]
            - compute: added batch support

            ## [1.2.2]
            - storage: old fix
            """);

        var result = await gate.EvaluateAsync("compute", BaselineVersion, "main", hasExistingArticle: true, CancellationToken.None);

        Assert.False(result.ShouldSkip);
    }

    // ─── Namespace not mentioned ───────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_NamespaceNotMentionedInRelevantSections_Skips()
    {
        var gate = CreateGate("""
            ## [1.2.3]
            - compute: new tool
            - keyvault: updated descriptions

            ## [1.2.2]
            - storage: old fix
            """);

        var result = await gate.EvaluateAsync("storage", BaselineVersion, "main", hasExistingArticle: true, CancellationToken.None);

        Assert.True(result.ShouldSkip);
        Assert.Contains("storage", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_SkippedReason_MentionsBaselineVersion()
    {
        var gate = CreateGate("""
            ## [1.2.3]
            - compute: only compute changed
            """);

        var result = await gate.EvaluateAsync("advisor", BaselineVersion, "main", hasExistingArticle: true, CancellationToken.None);

        Assert.True(result.ShouldSkip);
        Assert.Contains(BaselineVersion, result.Reason, StringComparison.Ordinal);
    }

    // ─── Fetch failure fallback ────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_FetchThrows_ProcessesAsSafeFallback()
    {
        var gate = new ChangelogGate(static (_, _) => Task.FromException<string>(new HttpRequestException("Network unreachable")));

        var result = await gate.EvaluateAsync("storage", BaselineVersion, "main", hasExistingArticle: true, CancellationToken.None);

        Assert.False(result.ShouldSkip);
        Assert.Contains("fallback", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ─── No relevant sections fallback ────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_NoSectionsNewerThanBaseline_ProcessesAsSafeFallback()
    {
        // All sections are older than baseline "1.2.3"
        var gate = CreateGate("""
            ## [1.2.2]
            - storage: old entry

            ## [1.0.0]
            - compute: initial
            """);

        var result = await gate.EvaluateAsync("storage", BaselineVersion, "main", hasExistingArticle: true, CancellationToken.None);

        Assert.False(result.ShouldSkip);
        Assert.Contains("fallback", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_EmptyChangelog_ProcessesAsSafeFallback()
    {
        var gate = CreateGate(string.Empty);

        var result = await gate.EvaluateAsync("storage", BaselineVersion, "main", hasExistingArticle: true, CancellationToken.None);

        Assert.False(result.ShouldSkip);
    }

    // ─── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_CancellationRequested_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var gate = new ChangelogGate(async (_, ct) =>
        {
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
            return string.Empty;
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            gate.EvaluateAsync("storage", BaselineVersion, "main", hasExistingArticle: true, cts.Token));
    }

    // ─── BuildChangelogUrl ────────────────────────────────────────────────────

    [Fact]
    public void BuildChangelogUrl_ContainsBranchAndPath()
    {
        var url = ChangelogGate.BuildChangelogUrl("main");
        Assert.Contains("main", url, StringComparison.Ordinal);
        Assert.Contains("CHANGELOG.md", url, StringComparison.Ordinal);
        Assert.Contains("raw.githubusercontent.com", url, StringComparison.Ordinal);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ChangelogGate CreateGate(string content)
        => new((_, _) => Task.FromResult(content));
}
