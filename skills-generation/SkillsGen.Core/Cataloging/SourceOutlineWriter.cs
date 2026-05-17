using System.Text.Json;
using System.Text.Json.Serialization;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Cataloging;

public class SourceOutlineWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly string _dataDir;

    public SourceOutlineWriter(string dataDir = "data")
    {
        _dataDir = dataDir;
    }

    public void Write(Dictionary<string, SkillOutline> catalog)
    {
        Directory.CreateDirectory(_dataDir);
        var path = Path.Combine(_dataDir, "source-outlines.json");

        // Serialize as an object keyed by skill name
        var serializable = catalog.ToDictionary(
            kvp => kvp.Key,
            kvp => new SkillOutlineDto(kvp.Value));

        var json = JsonSerializer.Serialize(serializable, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>DTO that matches the PRD §3 output format.</summary>
    private sealed class SkillOutlineDto
    {
        [JsonPropertyName("headings")]
        public List<HeadingEntryDto> Headings { get; }

        [JsonPropertyName("unmappedCount")]
        public int UnmappedCount { get; }

        [JsonPropertyName("catalogedAt")]
        public string CatalogedAt { get; }

        public SkillOutlineDto(SkillOutline outline)
        {
            Headings = outline.Headings
                .Select(h => new HeadingEntryDto(h))
                .ToList();
            UnmappedCount = outline.UnmappedCount;
            CatalogedAt = outline.CatalogedAt.ToString("o");
        }
    }

    private sealed class HeadingEntryDto
    {
        [JsonPropertyName("level")]
        public int Level { get; }

        [JsonPropertyName("text")]
        public string Text { get; }

        [JsonPropertyName("mappedTo")]
        public string? MappedTo { get; }

        public HeadingEntryDto(HeadingEntry entry)
        {
            Level = entry.Level;
            Text = entry.Text;
            MappedTo = entry.MappedTo;
        }
    }
}
