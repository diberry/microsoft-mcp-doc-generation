using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Fetchers;

public class ChangelogFetcher : IChangelogFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChangelogFetcher> _logger;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly string _changelogPath;
    private readonly string? _token;

    public ChangelogFetcher(
        HttpClient httpClient,
        ILogger<ChangelogFetcher> logger,
        string owner = "microsoft",
        string repo = "azure-skills",
        string branch = "main",
        string changelogPath = ".github/plugins/azure-skills/CHANGELOG.md",
        string? token = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _owner = owner;
        _repo = repo;
        _branch = branch;
        _changelogPath = changelogPath;
        _token = token;
    }

    public async Task<string?> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{_changelogPath}?ref={_branch}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.github.v3.raw");
            request.Headers.Add("User-Agent", "SkillsGen");
            if (!string.IsNullOrEmpty(_token))
                request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch CHANGELOG: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("Successfully fetched CHANGELOG from {Owner}/{Repo}", _owner, _repo);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch CHANGELOG from GitHub");
            return null;
        }
    }
}
