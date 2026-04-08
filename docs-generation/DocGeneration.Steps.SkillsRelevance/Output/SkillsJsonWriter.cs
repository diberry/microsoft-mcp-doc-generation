// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using SkillsRelevance.Models;

namespace SkillsRelevance.Output;

/// <summary>
/// Writes skill relevance results to JSON files in the output directory.
/// </summary>
public static class SkillsJsonWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Writes a JSON file containing skill relevance data for a service.
    /// </summary>
    public static async Task WriteServiceSummaryJsonAsync(
        string outputDir,
        string serviceName,
        List<SkillInfo> relevantSkills,
        List<SkillSource> sources,
        DateTimeOffset? generatedAt = null)
    {
        Directory.CreateDirectory(outputDir);

        var timestamp = generatedAt ?? DateTimeOffset.UtcNow;
        var fileName = $"{OutputHelpers.SanitizeFileName(serviceName)}-skills-relevance.json";
        var filePath = Path.Combine(outputDir, fileName);

        var output = new SkillRelevanceOutput
        {
            Service = serviceName,
            GeneratedAt = timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Sources = sources.Select(s => new SkillSourceEntry
            {
                Name = s.DisplayName,
                Url = s.GetHtmlUrl()
            }).ToList(),
            Skills = relevantSkills.Select(s => new SkillRelevanceEntry
            {
                Name = s.Name,
                Source = s.SourceRepository,
                SourceUrl = s.SourceUrl,
                FileName = s.FileName,
                RelevanceScore = Math.Round(s.RelevanceScore, 2),
                RelevanceLevel = OutputHelpers.GetRelevanceLevel(s.RelevanceScore),
                Description = s.Description,
                Tags = s.Tags,
                LastUpdated = s.LastUpdated?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                RelevanceReasons = s.RelevanceReasons
            }).ToList()
        };

        var json = JsonSerializer.Serialize(output, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        Console.WriteLine($"  ✅ {fileName} ({relevantSkills.Count} skills, JSON)");
    }
}
