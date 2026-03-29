using System.Text.RegularExpressions;

namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Extracts structural and quality metrics from markdown content.
/// Self-contained — patterns adapted from PromptRegression.Tests.QualityMetrics
/// for use outside of xUnit test context.
/// </summary>
internal static partial class MarkdownAnalyzer
{
    /// <summary>
    /// Extracts an ArticleFingerprint from markdown file content.
    /// </summary>
    public static ArticleFingerprint AnalyzeArticle(string content, string fileName)
    {
        var body = StripFrontmatter(content);
        var h2Headings = ExtractH2Headings(body);
        var toolCount = ExtractToolCount(content);

        return new ArticleFingerprint
        {
            FileName = fileName,
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            WordCount = CountWords(body),
            SectionCount = CountSections(body),
            H2Headings = h2Headings,
            FrontmatterFields = ExtractFrontmatterFields(content),
            ToolCount = toolCount
        };
    }

    /// <summary>
    /// Extracts quality metrics from markdown content.
    /// </summary>
    public static QualityFingerprint AnalyzeQuality(string content)
    {
        var body = StripFrontmatter(content);
        var contractionCount = ContractionPattern().Matches(body).Count;
        var opportunityCount = ContractionOpportunityPattern().Matches(body).Count;
        var totalOpportunities = contractionCount + opportunityCount;

        return new QualityFingerprint
        {
            FutureTenseViolations = FutureTensePattern().Matches(body).Count,
            FabricatedUrlCount = FabricatedUrlPattern().Matches(body).Count,
            BrandingViolations = CountBrandingViolations(body),
            ContractionRate = totalOpportunities > 0
                ? (double)contractionCount / totalOpportunities
                : 0.0
        };
    }

    internal static string StripFrontmatter(string content)
    {
        var match = FrontmatterPattern().Match(content);
        return match.Success ? content[(match.Index + match.Length)..].TrimStart() : content;
    }

    internal static List<string> ExtractH2Headings(string body)
    {
        return H2Pattern().Matches(body)
            .Select(m => m.Value.Trim())
            .ToList();
    }

    internal static List<string> ExtractFrontmatterFields(string content)
    {
        var fmMatch = FrontmatterBlockPattern().Match(content);
        if (!fmMatch.Success) return [];

        var frontmatter = fmMatch.Groups[1].Value;
        return FrontmatterFieldPattern().Matches(frontmatter)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    internal static int CountWords(string body)
    {
        return body.Split(WordSplitChars, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    internal static int CountSections(string body)
    {
        return SectionPattern().Matches(body).Count;
    }

    internal static int? ExtractToolCount(string content)
    {
        var match = ToolCountPattern().Match(content);
        return match.Success && int.TryParse(match.Groups[1].Value, out var count)
            ? count
            : null;
    }

    private static int CountBrandingViolations(string body)
    {
        return CosmosDbPattern().Matches(body).Count
            + AzureVmsPattern().Matches(body).Count
            + MssqlPattern().Matches(body).Count
            + AzureAdPattern().Matches(body).Count;
    }

    private static readonly char[] WordSplitChars = [' ', '\n', '\r', '\t'];

    [GeneratedRegex(@"^---\s*\n.*?\n---\s*\n", RegexOptions.Singleline)]
    private static partial Regex FrontmatterPattern();

    [GeneratedRegex(@"^---\s*\n(.*?)\n---", RegexOptions.Singleline)]
    private static partial Regex FrontmatterBlockPattern();

    [GeneratedRegex(@"^(\w[\w.]+):", RegexOptions.Multiline)]
    private static partial Regex FrontmatterFieldPattern();

    [GeneratedRegex(@"^## .+", RegexOptions.Multiline)]
    private static partial Regex H2Pattern();

    [GeneratedRegex(@"^#{1,3}\s+", RegexOptions.Multiline)]
    private static partial Regex SectionPattern();

    [GeneratedRegex(@"tool_count:\s*(\d+)")]
    private static partial Regex ToolCountPattern();

    [GeneratedRegex(@"\bwill\s+(return|list|get|create|delete|update|show|display|provide|generate|produce|send|retrieve)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FutureTensePattern();

    [GeneratedRegex(@"https?://learn\.microsoft\.com/[^\s)]*?/docs/[^\s)]*")]
    private static partial Regex FabricatedUrlPattern();

    [GeneratedRegex(@"\b(don't|doesn't|isn't|aren't|wasn't|weren't|can't|won't|shouldn't|wouldn't|couldn't|hasn't|haven't|hadn't|it's|you're|they're|we're|that's|there's|who's|what's|let's)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ContractionPattern();

    [GeneratedRegex(@"\b(do not|does not|is not|are not|was not|were not|can not|cannot|will not|should not|would not|could not|has not|have not|had not|it is|you are|they are|we are|that is|there is|who is|what is|let us)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ContractionOpportunityPattern();

    [GeneratedRegex(@"\bCosmosDB\b")]
    private static partial Regex CosmosDbPattern();

    [GeneratedRegex(@"\bAzure VMs\b")]
    private static partial Regex AzureVmsPattern();

    [GeneratedRegex(@"\bMSSQL\b")]
    private static partial Regex MssqlPattern();

    [GeneratedRegex(@"\bAzure Active Directory\b")]
    private static partial Regex AzureAdPattern();
}
