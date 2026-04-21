using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Data;

public record RelatedLink(string Title, string Url, string Category);

public record CuratedSkillData(List<RelatedLink> RelatedLinks, List<string> ExamplePrompts);

public class CuratedDataLoader
{
    /// <summary>
    /// Loads skill-related-links.json and skill-example-prompts.json from dataPath,
    /// merges them by skill name, and returns a combined dictionary.
    /// Missing files or missing skill entries are handled gracefully.
    /// </summary>
    public static Dictionary<string, CuratedSkillData> Load(string dataPath, ILogger logger)
    {
        var allLinks = LoadRelatedLinks(dataPath, logger);
        var allPrompts = LoadExamplePrompts(dataPath, logger);

        // Merge both sources into one dictionary keyed by skill name
        var allKeys = new HashSet<string>(allLinks.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(allPrompts.Keys);

        var result = new Dictionary<string, CuratedSkillData>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in allKeys)
        {
            allLinks.TryGetValue(key, out var links);
            allPrompts.TryGetValue(key, out var prompts);
            result[key] = new CuratedSkillData(
                links ?? [],
                prompts ?? []);
        }

        logger.LogInformation(
            "[curated-data] Loaded {SkillCount} skill entries ({LinksCount} with links, {PromptsCount} with prompts)",
            result.Count, allLinks.Count, allPrompts.Count);

        return result;
    }

    private static Dictionary<string, List<RelatedLink>> LoadRelatedLinks(string dataPath, ILogger logger)
    {
        var path = Path.Combine(dataPath, "skill-related-links.json");
        var result = new Dictionary<string, List<RelatedLink>>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            logger.LogWarning("⚠️ Missing {Filename} — curated related links unavailable", "skill-related-links.json");
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("skills", out var skills))
                return result;

            foreach (var skill in skills.EnumerateObject())
            {
                var links = new List<RelatedLink>();
                if (skill.Value.TryGetProperty("links", out var linksEl))
                {
                    foreach (var link in linksEl.EnumerateArray())
                    {
                        var title = link.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        var url = link.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                        var category = link.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "";
                        links.Add(new RelatedLink(title, url, category));
                    }
                }
                result[skill.Name] = links;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("⚠️ Failed to load {Filename}: {Error}", "skill-related-links.json", ex.Message);
        }

        return result;
    }

    private static Dictionary<string, List<string>> LoadExamplePrompts(string dataPath, ILogger logger)
    {
        var path = Path.Combine(dataPath, "skill-example-prompts.json");
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            logger.LogWarning("⚠️ Missing {Filename} — curated example prompts unavailable", "skill-example-prompts.json");
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var skill in doc.RootElement.EnumerateObject())
            {
                var prompts = skill.Value.EnumerateArray()
                    .Select(p => p.GetString() ?? "")
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                result[skill.Name] = prompts;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("⚠️ Failed to load {Filename}: {Error}", "skill-example-prompts.json", ex.Message);
        }

        return result;
    }
}
