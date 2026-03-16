// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text.Json;
using SkillsRelevance.Models;
using Shared;

namespace SkillsRelevance.Services;

/// <summary>
/// Fetches skill files from GitHub repositories using the GitHub REST API.
/// Supports optional GITHUB_TOKEN for higher rate limits.
/// </summary>
public class GitHubSkillsFetcher
{
    // Static HttpClient is the recommended pattern for console apps with short lifetimes.
    // It avoids socket exhaustion from repeated creation/disposal and is safe here
    // because authorization is set per-request (not on DefaultRequestHeaders).
    private static readonly HttpClient _httpClient = new();
    private readonly string? _githubToken;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GitHubSkillsFetcher(string? githubToken = null)
    {
        _githubToken = githubToken;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "azure-mcp-docs-generator/1.0");
        }
    }

    /// <summary>
    /// Fetches all skill files from a source repository.
    /// </summary>
    public async Task<List<(GitHubFileEntry Entry, string Content)>> FetchSkillsAsync(SkillSource source)
    {
        var results = new List<(GitHubFileEntry, string)>();

        try
        {
            var apiUrl = source.GetContentsApiUrl();
            LogFileHelper.WriteDebug($"Fetching skills from: {apiUrl}");

            var request = CreateRequest(apiUrl);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"  ⚠️  Could not access {source.DisplayName} (HTTP {(int)response.StatusCode}). Skipping.");
                LogFileHelper.WriteDebug($"HTTP {(int)response.StatusCode} from {apiUrl}");
                return results;
            }

            var json = await response.Content.ReadAsStringAsync();
            var entries = JsonSerializer.Deserialize<List<GitHubFileEntry>>(json, _jsonOptions);

            if (entries == null || entries.Count == 0)
            {
                LogFileHelper.WriteDebug($"No entries found in {apiUrl}");
                return results;
            }

            var skillFiles = entries.Where(e => e.IsSkillFile).ToList();
            LogFileHelper.WriteDebug($"Found {skillFiles.Count} skill files in {source.DisplayName}");

            foreach (var entry in skillFiles)
            {
                var content = await FetchFileContentAsync(entry);
                if (content != null)
                {
                    results.Add((entry, content));
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

            var request = CreateRequest(url);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                LogFileHelper.WriteDebug($"Failed to fetch content for {entry.Name}: HTTP {(int)response.StatusCode}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Exception fetching content for {entry.Name}: {ex.Message}");
            return null;
        }
    }

    private HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrEmpty(_githubToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _githubToken);
        }
        return request;
    }
}
