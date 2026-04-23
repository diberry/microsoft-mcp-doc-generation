namespace SkillsGen.Core.Models;

public record ChangelogEntry(
    string Version,
    DateOnly ReleaseDate,
    List<string> SkillsAdded,
    List<string> SkillsRemoved,
    List<string> SkillsChanged);
