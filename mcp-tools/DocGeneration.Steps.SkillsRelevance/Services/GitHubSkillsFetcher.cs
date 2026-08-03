// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text.Json;
using SkillsRelevance.Models;
using Shared;

namespace SkillsRelevance.Services;

/// <summary>
/// Fetches skill files from GitHub repositories using the GitHub REST API.
/// Respects unauthenticated rate limits (60 requests per hour) through throttling.
/// Caches responses to reduce redundant API calls.
/// </summary>
public class GitHubSkillsFetcher
{
    // Static HttpClient is the recommended pattern for console apps with short lifetimes.
    // It avoids socket exhaustion from repeated creation/disposal.
    private static readonly HttpClient _httpClient = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly RequestThrottler _throttler;
    private readonly ResponseCache _cache;
    private int _requestsMade = 0;

    public GitHubSkillsFetcher()
    {
        _throttler = new RequestThrottler();
        _cache = new ResponseCache();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "azure-mcp-docs-generator/1.0");
        }
    }

    /// <summary>
    /// Fetches all skill files from a source repository.
    /// Returns stubs for any namespaces that would exceed the rate limit budget.
    /// </summary>
    public async Task<List<(GitHubFileEntry Entry, string Content)>> FetchSkillsAsync(SkillSource source)
    {
        var results = new List<(GitHubFileEntry, string)>();

        try
        {
            var apiUrl = source.GetContentsApiUrl();
            LogFileHelper.WriteDebug($"Fetching skills from: {apiUrl}");

            // Check if we have enough requests remaining before proceeding
            var requestsRemaining = _throttler.GetRequestsRemaining();
            if (requestsRemaining <= 1)
            {
                Console.WriteLine($"  ⚠️  Rate limit budget exhausted. Skipping {source.DisplayName}.");
                LogFileHelper.WriteDebug($"Insufficient request budget (remaining: {requestsRemaining}) for {source.DisplayName}");
                return results;
            }

            // Apply throttling before making the request
            await _throttler.ThrottleAsync();
            _requestsMade++;

            var content = await FetchUrlAsync(apiUrl);
            if (content == null)
            {
                Console.WriteLine($"  ⚠️  Could not access {source.DisplayName}. Skipping.");
                return results;
            }

            var entries = JsonSerializer.Deserialize<List<GitHubFileEntry>>(content, _jsonOptions);

            if (entries == null || entries.Count == 0)
            {
                LogFileHelper.WriteDebug($"No entries found in {apiUrl}");
                return results;
            }

            var skillFiles = entries.Where(e => e.IsSkillFile).ToList();
            LogFileHelper.WriteDebug($"Found {skillFiles.Count} skill files in {source.DisplayName}");

            foreach (var entry in skillFiles)
            {
                // Check budget before each file fetch
                if (_throttler.GetRequestsRemaining() <= 0)
                {
                    LogFileHelper.WriteDebug($"Rate limit budget exhausted while fetching files from {source.DisplayName}");
                    Console.WriteLine($"  ⚠️  Rate limit budget exhausted. Remaining files from {source.DisplayName} skipped.");
                    break;
                }

                var fileContent = await FetchFileContentAsync(entry);
                if (fileContent != null)
                {
                    results.Add((entry, fileContent));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Error fetching from {source.DisplayName}: {ex.Message}");
            LogFileHelper.WriteDebug($"Exception fetching from {source.DisplayName}: {ex}");
        }

        return results;
    }

    private async Task<string?> FetchFileContentAsync(GitHubFileEntry entry)
    {
        try
        {
            var url = entry.DownloadUrl ?? entry.Url;
            if (string.IsNullOrEmpty(url)) return null;

            // Check cache first
            if (_cache.TryGetValue(url, out var cachedContent))
            {
                LogFileHelper.WriteDebug($"Cache hit for {entry.Name}");
                return cachedContent;
            }

            // Throttle before fetching
            await _throttler.ThrottleAsync();
            _requestsMade++;

            var content = await FetchUrlAsync(url);
            if (content != null)
            {
                _cache.Set(url, content);
            }

            return content;
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Exception fetching content for {entry.Name}: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> FetchUrlAsync(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                LogFileHelper.WriteDebug($"HTTP {(int)response.StatusCode} from {url}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Exception fetching {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the total number of API requests made during this run.
    /// </summary>
    public int GetRequestsMade() => _requestsMade;
}
