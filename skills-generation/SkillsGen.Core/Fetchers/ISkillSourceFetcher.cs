namespace SkillsGen.Core.Fetchers;
using SkillsGen.Core.Models;

public record SkillSourceFiles(
    string SkillMarkdown,
    string? TriggersTestSource,
    string SourcePath,
    string? SourceSha,
    HashSet<string> FileExtensions = null!)
{
    public HashSet<string> FileExtensions { get; init; } = FileExtensions ?? [];
}

public interface ISkillSourceFetcher
{
    Task<SkillSourceFiles?> FetchAsync(string skillName, CancellationToken ct = default);
}
