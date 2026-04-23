namespace SkillsGen.Core.Fetchers;

public interface ISkillDirectoryLister
{
    Task<List<string>> ListSubdirectoriesAsync(string path, CancellationToken ct = default);
}
