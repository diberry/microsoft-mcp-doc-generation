using System.Text.RegularExpressions;

namespace DocGeneration.PromptRegression.Tests.Infrastructure;

/// <summary>
/// Measurable quality indicators for generated markdown content.
/// All metrics are objective and deterministic — no subjective scoring.
/// </summary>
public sealed class QualityMetrics
{
    public int SectionCount { get; init; }
    public int WordCount { get; init; }
    public int CharCount { get; init; }
    public bool HasValidFrontmatter { get; init; }
    public IReadOnlyList<string> FrontmatterFields { get; init; } = [];
    public IReadOnlyList<string> MissingSections { get; init; } = [];
    public int ContractionCount { get; init; }
    public int ContractionOpportunities { get; init; }
    public int FutureTenseViolations { get; init; }
    public int FabricatedUrlCount { get; init; }
    public int BrandingViolations { get; init; }

    /// <summary>
    /// Ratio of contractions used to total contraction opportunities.
    /// Returns 0.0 when there are no opportunities (nothing to contract).
    /// </summary>
    public double ContractionRate =>
        ContractionOpportunities > 0
            ? (double)ContractionCount / ContractionOpportunities
            : 0.0;

    public static QualityMetrics Analyze(string content)
    {
        var frontmatter = ExtractFrontmatter(content);
        var body = StripFrontmatter(content);

        return new QualityMetrics
        {
            SectionCount = CountSections(body),
            WordCount = CountWords(body),
            CharCount = content.Length,
            HasValidFrontmatter = frontmatter is not null,
            FrontmatterFields = ExtractFrontmatterFields(frontmatter),
            MissingSections = FindMissingSections(body),
            ContractionCount = CountContractions(body),
            ContractionOpportunities = CountContractionOpportunities(body),
            FutureTenseViolations = CountFutureTense(body),
            FabricatedUrlCount = CountFabricatedUrls(body),
            BrandingViolations = CountBrandingViolations(body),
        };
    }

    private static int CountSections(string body)
    {
        return Regex.Matches(body, @"^#{1,3}\s+", RegexOptions.Multiline).Count;
    }

    private static readonly char[] WordSplitChars = [' ', '\n', '\r', '\t'];

    private static int CountWords(string body)
    {
        return body.Split(WordSplitChars, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string? ExtractFrontmatter(string content)
    {
        var match = Regex.Match(content, @"^---\s*\n(.*?)\n---", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string StripFrontmatter(string content) =>
        Shared.FrontmatterUtility.StripFrontmatter(content);

    private static IReadOnlyList<string> ExtractFrontmatterFields(string? frontmatter)
    {
        if (frontmatter is null) return [];
        return Regex.Matches(frontmatter, @"^(\w[\w.]+):", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    private static readonly string[] RequiredSections =
    [
        "## Prerequisites",
        "## Best practices",
        "## Related content"
    ];

    private static IReadOnlyList<string> FindMissingSections(string body)
    {
        return RequiredSections
            .Where(s => !body.Contains(s, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static readonly Regex ContractionRegex = new(
        @"\b(don't|doesn't|isn't|aren't|wasn't|weren't|can't|won't|shouldn't|wouldn't|couldn't|hasn't|haven't|hadn't|it's|you're|they're|we're|that's|there's|who's|what's|let's)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static int CountContractions(string body) =>
        ContractionRegex.Matches(body).Count;

    private static readonly Regex ContractionOpportunityRegex = new(
        @"\b(do not|does not|is not|are not|was not|were not|can not|cannot|will not|should not|would not|could not|has not|have not|had not|it is|you are|they are|we are|that is|there is|who is|what is|let us)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static int CountContractionOpportunities(string body)
    {
        var contractions = ContractionRegex.Matches(body).Count;
        var opportunities = ContractionOpportunityRegex.Matches(body).Count;
        return contractions + opportunities;
    }

    private static readonly Regex FutureTenseRegex = new(
        @"\bwill\s+(return|list|get|create|delete|update|show|display|provide|generate|produce|send|retrieve)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static int CountFutureTense(string body) =>
        FutureTenseRegex.Matches(body).Count;

    private static readonly Regex FabricatedUrlRegex = new(
        @"https?://learn\.microsoft\.com/[^\s)]*?/docs/[^\s)]*",
        RegexOptions.Compiled);

    private static int CountFabricatedUrls(string body) =>
        FabricatedUrlRegex.Matches(body).Count;

    private static readonly Regex[] BrandingViolationPatterns =
    [
        new(@"\bCosmosDB\b", RegexOptions.Compiled),
        new(@"\bAzure VMs\b", RegexOptions.Compiled),
        new(@"\bMSSQL\b", RegexOptions.Compiled),
        new(@"\bAzure Active Directory\b", RegexOptions.Compiled),
    ];

    private static int CountBrandingViolations(string body) =>
        BrandingViolationPatterns.Sum(p => p.Matches(body).Count);
}
