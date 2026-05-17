namespace SkillsGen.Core.Models;

/// <summary>
/// Catalog of headings extracted from a source SKILL.md file.
/// </summary>
public class SkillOutline
{
    public List<HeadingEntry> Headings { get; init; } = [];
    public int UnmappedCount { get; init; }
    public DateTime CatalogedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A single heading entry extracted from SKILL.md.
/// </summary>
public class HeadingEntry
{
    public int Level { get; init; }
    public string Text { get; init; } = "";
    public string? MappedTo { get; init; }
}
