using System.Text.RegularExpressions;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Validation;

public partial class DriftDetector : IDriftDetector
{
    // Sections that the template is expected to produce
    private static readonly HashSet<string> KnownTemplateSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "prerequisites",
        "when to use",
        "when to use this skill",
        "mcp tools",
        "example prompts",
        "what it provides",
        "related content",
    };

    public DriftReport DetectDrift(
        string skillName,
        string generatedContent,
        string publishedContent,
        string generatedPath,
        string publishedUrl)
    {
        var items = new List<DriftItem>();

        if (string.IsNullOrWhiteSpace(generatedContent))
        {
            items.Add(new DriftItem(skillName, "(entire page)", DriftSeverity.Error,
                DriftCategory.GenerationBug,
                "Generated content is empty",
                "Check generator pipeline — no output was produced"));
            return new DriftReport(skillName, generatedPath, publishedUrl, items, DateTime.UtcNow);
        }

        if (string.IsNullOrWhiteSpace(publishedContent))
        {
            items.Add(new DriftItem(skillName, "(entire page)", DriftSeverity.Info,
                DriftCategory.ContentPrStale,
                "Published content is empty or not yet available",
                "Content PR needs to be created and merged"));
            return new DriftReport(skillName, generatedPath, publishedUrl, items, DateTime.UtcNow);
        }

        var generatedSections = ExtractSections(generatedContent);
        var publishedSections = ExtractSections(publishedContent);

        // 1. Missing sections: published has it, generated doesn't
        foreach (var (name, _) in publishedSections)
        {
            if (!SectionExistsNormalized(generatedSections, name))
            {
                items.Add(new DriftItem(skillName, name, DriftSeverity.Error,
                    CategorizeSection(name),
                    $"Section '{name}' exists in published but missing from generated",
                    SuggestFix(name, "missing-from-generated")));
            }
        }

        // 2. Extra sections: generated has it, published doesn't
        foreach (var (name, _) in generatedSections)
        {
            if (!SectionExistsNormalized(publishedSections, name))
            {
                items.Add(new DriftItem(skillName, name, DriftSeverity.Info,
                    DriftCategory.ContentPrStale,
                    $"Section '{name}' exists in generated but not in published",
                    "Content PR needs update — regenerate and publish"));
            }
        }

        // 3. Content differences in shared sections
        foreach (var (name, genContent) in generatedSections)
        {
            var pubContent = FindSectionNormalized(publishedSections, name);
            if (pubContent is not null)
            {
                CompareContent(skillName, name, genContent, pubContent, items);
            }
        }

        return new DriftReport(skillName, generatedPath, publishedUrl, items, DateTime.UtcNow);
    }

    internal static Dictionary<string, string> ExtractSections(string content)
    {
        // Strip frontmatter
        var body = StripFrontmatter(content);

        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = H2Regex().Matches(body);

        for (var i = 0; i < matches.Count; i++)
        {
            var sectionName = matches[i].Groups[1].Value.Trim();
            var startIndex = matches[i].Index + matches[i].Length;
            var endIndex = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            var sectionContent = body[startIndex..endIndex].Trim();
            sections[sectionName] = sectionContent;
        }

        return sections;
    }

    internal static void CompareContent(
        string skillName,
        string sectionName,
        string generatedContent,
        string publishedContent,
        List<DriftItem> items)
    {
        // Bullet count comparison
        var genBullets = CountBullets(generatedContent);
        var pubBullets = CountBullets(publishedContent);
        if (pubBullets > genBullets && genBullets >= 0)
        {
            var category = pubBullets > genBullets
                ? DriftCategory.SourceDataGap
                : DriftCategory.ContentPrStale;
            items.Add(new DriftItem(skillName, sectionName, DriftSeverity.Warning,
                category,
                $"Bullet count differs: generated has {genBullets}, published has {pubBullets}",
                $"Review source SKILL.md for additional items in '{sectionName}'"));
        }

        // Table presence comparison
        var genHasTable = ContainsTable(generatedContent);
        var pubHasTable = ContainsTable(publishedContent);
        if (pubHasTable && !genHasTable)
        {
            items.Add(new DriftItem(skillName, sectionName, DriftSeverity.Warning,
                DriftCategory.GenerationBug,
                $"Published has a table in '{sectionName}' but generated does not",
                "Check template — may need table rendering support for this section"));
        }
        else if (genHasTable && !pubHasTable)
        {
            items.Add(new DriftItem(skillName, sectionName, DriftSeverity.Info,
                DriftCategory.ContentPrStale,
                $"Generated has a table in '{sectionName}' but published does not",
                "Content PR needs update — regenerate and publish"));
        }

        // Word count comparison (>50% delta = Warning)
        var genWords = CountWords(generatedContent);
        var pubWords = CountWords(publishedContent);
        if (genWords > 0 && pubWords > 0)
        {
            var maxWords = Math.Max(genWords, pubWords);
            var minWords = Math.Min(genWords, pubWords);
            var delta = (double)(maxWords - minWords) / maxWords;
            if (delta > 0.50)
            {
                items.Add(new DriftItem(skillName, sectionName, DriftSeverity.Warning,
                    DriftCategory.SourceDataGap,
                    $"Word count differs significantly: generated has {genWords} words, published has {pubWords} words ({delta:P0} delta)",
                    $"Review content in '{sectionName}' for completeness"));
            }
        }
    }

    internal static DriftCategory CategorizeSection(string sectionName)
    {
        var normalized = NormalizeSectionName(sectionName);
        return KnownTemplateSections.Any(known => NormalizeSectionName(known) == normalized)
            ? DriftCategory.GenerationBug
            : DriftCategory.SourceDataGap;
    }

    internal static string SuggestFix(string sectionName, string driftType)
    {
        if (driftType == "missing-from-generated")
        {
            var normalized = NormalizeSectionName(sectionName);
            var isKnown = KnownTemplateSections.Any(known => NormalizeSectionName(known) == normalized);
            return isKnown
                ? $"Check template rendering for '{sectionName}' — may be conditional on data that's missing from source SKILL.md"
                : "Add section to source SKILL.md at microsoft/azure-skills or add template support";
        }

        return $"Review content differences in '{sectionName}'";
    }

    private static string NormalizeSectionName(string name)
    {
        // Lowercase, strip trailing qualifiers like "this skill"
        var normalized = name.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"\s+this\s+skill$", "");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }

    private static bool SectionExistsNormalized(Dictionary<string, string> sections, string name)
    {
        var normalized = NormalizeSectionName(name);
        return sections.Keys.Any(k => NormalizeSectionName(k) == normalized);
    }

    private static string? FindSectionNormalized(Dictionary<string, string> sections, string name)
    {
        var normalized = NormalizeSectionName(name);
        var key = sections.Keys.FirstOrDefault(k => NormalizeSectionName(k) == normalized);
        return key is not null ? sections[key] : null;
    }

    private static string StripFrontmatter(string content)
    {
        return FrontmatterRegex().Replace(content, "").TrimStart();
    }

    private static int CountBullets(string content)
    {
        return BulletRegex().Matches(content).Count;
    }

    private static bool ContainsTable(string content)
    {
        return TableSeparatorRegex().IsMatch(content);
    }

    private static int CountWords(string text)
    {
        var cleaned = Regex.Replace(text, @"[#*|`\[\]\(\)\-]", " ");
        return cleaned.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    [GeneratedRegex(@"^---\s*\n.*?\n---\s*\n?", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex H2Regex();

    [GeneratedRegex(@"^\s*[-*]\s+", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\|[-:]+\|", RegexOptions.None)]
    private static partial Regex TableSeparatorRegex();
}
