// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SkillsRelevance.Services;

/// <summary>
/// Simple in-memory cache for GitHub API responses to avoid redundant fetches.
/// Caches responses for the duration of the application run.
/// </summary>
public class ResponseCache
{
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    private class CacheEntry
    {
        public string Content { get; set; } = string.Empty;
        public DateTime CachedAt { get; set; }
    }

    /// <summary>
    /// Gets a cached response if available.
    /// </summary>
    public bool TryGetValue(string url, out string content)
    {
        content = string.Empty;
        if (_cache.TryGetValue(url, out var entry))
        {
            content = entry.Content;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Stores a response in the cache.
    /// </summary>
    public void Set(string url, string content)
    {
        _cache[url] = new CacheEntry
        {
            Content = content,
            CachedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    public int Count => _cache.Count;
}
