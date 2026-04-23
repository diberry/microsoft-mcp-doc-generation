namespace SkillsGen.Core.Fetchers;

public interface IChangelogFetcher
{
    Task<string?> FetchAsync(CancellationToken ct = default);
}
