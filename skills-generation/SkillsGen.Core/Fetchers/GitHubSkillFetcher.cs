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
    private readonly string? _token;

    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000];

    public GitHubSkillFetcher(
        HttpClient httpClient,
        ILogger<GitHubSkillFetcher> logger,
        string owner = "microsoft",
        string repo = "GitHub-Copilot-for-Azure",
        string branch = "main",
        string skillsPath = "plugin/skills",
        string? token = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _owner = owner;
        _repo = repo;
        _branch = branch;
        _skillsPath = skillsPath;
        _token = token;
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

            var triggers = await FetchFileAsync($"tests/{skillName}/triggers.test.ts", ct);

            return new SkillSourceFiles(skillMd, triggers, $"github:{_owner}/{_repo}/{_skillsPath}/{skillName}", _branch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Skill} from GitHub", skillName);
            return null;
        }
    }

    internal async Task<string?> FetchFileAsync(string path, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{path}?ref={_branch}";

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.github.v3.raw");
            request.Headers.Add("User-Agent", "SkillsGen");
            if (!string.IsNullOrEmpty(_token))
                request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(ct);

            // Rate limited — retry with backoff
            if ((int)response.StatusCode == 429 || (int)response.StatusCode == 403)
            {
                if (attempt >= MaxRetries)
                {
                    _logger.LogWarning("Rate limited on {Path} after {Retries} retries, giving up", path, MaxRetries);
                    return null;
                }

                var retryAfterSeconds = 60;
                if (response.Headers.TryGetValues("Retry-After", out var values) &&
                    int.TryParse(values.FirstOrDefault(), out var parsed))
                {
                    retryAfterSeconds = parsed;
                }

                var delayMs = Math.Min(retryAfterSeconds * 1000, RetryDelaysMs[attempt]);
                _logger.LogWarning("Rate limited on {Path}, waiting {DelayMs}ms before retry {Attempt}/{Max}",
                    path, delayMs, attempt + 1, MaxRetries);
                await Task.Delay(delayMs, ct);
                continue;
            }

            // Other errors (404, 500, etc.) — return null immediately
            return null;
        }

        return null;
    }
}
