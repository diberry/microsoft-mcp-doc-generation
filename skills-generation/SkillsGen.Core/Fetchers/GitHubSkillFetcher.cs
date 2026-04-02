using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Fetchers;

public class GitHubSkillFetcher : ISkillSourceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubSkillFetcher> _logger;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly string _skillsPath;

    public GitHubSkillFetcher(
        HttpClient httpClient,
        ILogger<GitHubSkillFetcher> logger,
        string owner = "microsoft",
        string repo = "copilot-skills",
        string branch = "main",
        string skillsPath = "skills")
    {
        _httpClient = httpClient;
        _logger = logger;
        _owner = owner;
        _repo = repo;
        _branch = branch;
        _skillsPath = skillsPath;
    }

    public async Task<SkillSourceFiles?> FetchAsync(string skillName, CancellationToken ct = default)
    {
        try
        {
            var skillMd = await FetchFileAsync($"{_skillsPath}/{skillName}/SKILL.md", ct);
            if (skillMd == null)
            {
                _logger.LogWarning("SKILL.md not found for {Skill} on GitHub", skillName);
                return null;
            }

            var triggers = await FetchFileAsync($"{_skillsPath}/{skillName}/triggers.test.ts", ct);

            return new SkillSourceFiles(skillMd, triggers, $"github:{_owner}/{_repo}/{_skillsPath}/{skillName}", _branch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Skill} from GitHub", skillName);
            return null;
        }
    }

    private async Task<string?> FetchFileAsync(string path, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{path}?ref={_branch}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.github.v3.raw");
        request.Headers.Add("User-Agent", "SkillsGen");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsStringAsync(ct);
    }
}
