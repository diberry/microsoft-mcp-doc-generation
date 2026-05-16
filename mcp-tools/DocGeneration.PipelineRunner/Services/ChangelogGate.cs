using System.Text;

namespace PipelineRunner.Services;

/// <summary>
/// Fetches and evaluates the upstream CHANGELOG to decide whether a namespace
/// has changes in versions at or newer than the current CLI version.
/// </summary>
public sealed class ChangelogGate : IChangelogGate
{
    /// <summary>Path within the microsoft/mcp repo to the Azure MCP Server CHANGELOG.</summary>
    internal const string ChangelogRelativePath = "servers/Azure.Mcp.Server/CHANGELOG.md";

    /// <summary>Shared client to avoid socket exhaustion. Mirrors the pattern in BootstrapStep.</summary>
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "AzureMcpDocGen/1.0" } },
    };

    private readonly Func<string, CancellationToken, Task<string>>? _contentFetcher;

    /// <summary>Production constructor — uses the shared <see cref="HttpClient"/>.</summary>
    public ChangelogGate() { }

    /// <summary>
    /// Test constructor — injects a content fetcher so unit tests can provide canned CHANGELOG text
    /// without making real HTTP calls.
    /// </summary>
    internal ChangelogGate(Func<string, CancellationToken, Task<string>> contentFetcher)
    {
        _contentFetcher = contentFetcher;
    }

    /// <summary>Builds the raw GitHub URL for the CHANGELOG on the given branch.</summary>
    internal static string BuildChangelogUrl(string branch)
        => $"https://raw.githubusercontent.com/microsoft/mcp/{branch}/{ChangelogRelativePath}";

    /// <inheritdoc />
    public async Task<ChangelogGateResult> EvaluateAsync(
        string namespaceName,
        string baselineVersion,
        string mcpBranch,
        bool hasExistingArticle,
        CancellationToken cancellationToken)
    {
        // New namespaces (no existing article) are always processed regardless of CHANGELOG
        if (!hasExistingArticle)
        {
            return ChangelogGateResult.Process($"namespace '{namespaceName}' has no existing article; processing as new namespace");
        }

        string content;
        try
        {
            var url = BuildChangelogUrl(mcpBranch);
            content = _contentFetcher is not null
                ? await _contentFetcher(url, cancellationToken)
                : await SharedHttpClient.GetStringAsync(url, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Network or timeout failure — fall through and process (conservative)
            return ChangelogGateResult.Process($"CHANGELOG unavailable ({ex.GetType().Name}: {ex.Message}); processing as safe fallback");
        }

        var sections = ChangelogParser.ParseSections(content);
        var relevantSections = sections
            .Where(s => ChangelogParser.IsVersionRelevantFor(s.Version, baselineVersion))
            .ToArray();

        if (relevantSections.Length == 0)
        {
            // No sections at or newer than baseline — all entries are older; process conservatively
            return ChangelogGateResult.Process("no CHANGELOG sections at or newer than baseline version; processing as safe fallback");
        }

        var combinedContent = string.Join("\n", relevantSections.Select(s => s.Content));

        if (ChangelogParser.HasMentionOf(combinedContent, namespaceName))
        {
            return ChangelogGateResult.Process($"namespace '{namespaceName}' found in CHANGELOG");
        }

        return ChangelogGateResult.Skip(
            $"namespace '{namespaceName}' not mentioned in CHANGELOG for versions >= {baselineVersion}; skipping to avoid unnecessary PR");
    }
}
