// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates and manages redirect entries in .openpublishing.redirection.json format
/// when tool family files are renamed.
/// Stub for TDD — implement to make RedirectEntryGeneratorTests pass.
/// Fixes: #416 Item 6
/// </summary>
public static class RedirectEntryGenerator
{
    /// <summary>
    /// Generates a redirect entry from source to target path.
    /// </summary>
    public static RedirectEntry GenerateEntry(string sourcePath, string redirectUrl)
    {
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (string.IsNullOrEmpty(redirectUrl))
            throw new ArgumentException("Redirect URL cannot be empty", nameof(redirectUrl));

        if (sourcePath == redirectUrl)
            throw new ArgumentException("Source and redirect URL cannot be the same");

        if (!sourcePath.StartsWith('/'))
            throw new ArgumentException("Source path must start with '/'", nameof(sourcePath));

        if (!redirectUrl.StartsWith('/'))
            throw new ArgumentException("Redirect URL must start with '/'", nameof(redirectUrl));

        return new RedirectEntry(sourcePath, redirectUrl, RedirectDocumentId: false);
    }

    /// <summary>
    /// Creates a redirect entry from a file rename operation.
    /// Strips .md extension and prepends docset base path.
    /// </summary>
    public static RedirectEntry FromFileRename(string oldFileName, string newFileName, string docsetBasePath)
    {
        // Strip .md extension from filenames
        string oldName = oldFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? oldFileName[..^3]
            : oldFileName;

        string newName = newFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? newFileName[..^3]
            : newFileName;

        // Ensure docsetBasePath ends without trailing slash for consistent path building
        string basePath = docsetBasePath.TrimEnd('/');

        string sourcePath = $"{basePath}/{oldName}";
        string redirectUrl = $"{basePath}/{newName}";

        return GenerateEntry(sourcePath, redirectUrl);
    }

    /// <summary>
    /// Validates redirect chains. Returns errors for:
    /// - Redirect chains (A → B → C instead of A → C)
    /// - Dead ends (target page doesn't exist)
    /// </summary>
    public static List<string> ValidateChains(RedirectEntry[] redirects, HashSet<string> knownPages)
    {
        var errors = new List<string>();
        var redirectMap = redirects.ToDictionary(r => r.SourcePath, r => r.RedirectUrl);

        foreach (var redirect in redirects)
        {
            // Check for dead ends: target doesn't exist in known pages
            if (!knownPages.Contains(redirect.RedirectUrl) && !redirectMap.ContainsKey(redirect.RedirectUrl))
            {
                errors.Add($"Dead end redirect: '{redirect.SourcePath}' → '{redirect.RedirectUrl}' (target does not exist)");
            }

            // Check for redirect chains: target is also a source of another redirect
            if (redirectMap.ContainsKey(redirect.RedirectUrl))
            {
                errors.Add($"Redirect chain detected: '{redirect.SourcePath}' → '{redirect.RedirectUrl}' → '{redirectMap[redirect.RedirectUrl]}'");
            }
        }

        return errors;
    }

    /// <summary>
    /// Adds a redirect entry to the list if no duplicate source path exists.
    /// Returns true if added, false if duplicate.
    /// </summary>
    public static bool AddIfNotDuplicate(List<RedirectEntry> existing, RedirectEntry newEntry)
    {
        if (existing.Any(e => e.SourcePath == newEntry.SourcePath))
            return false;

        existing.Add(newEntry);
        return true;
    }

    /// <summary>
    /// Serializes redirect entries to .openpublishing.redirection.json format.
    /// </summary>
    public static string SerializeToOpenPublishingJson(RedirectEntry[] entries)
    {
        var wrapper = new RedirectionsWrapper
        {
            Redirections = entries.Select(e => new RedirectEntryDto
            {
                SourcePath = e.SourcePath,
                RedirectUrl = e.RedirectUrl,
                RedirectDocumentId = e.RedirectDocumentId
            }).ToArray()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        return JsonSerializer.Serialize(wrapper, options);
    }

    // DTO classes for JSON serialization
    private class RedirectionsWrapper
    {
        [JsonPropertyName("redirections")]
        public RedirectEntryDto[] Redirections { get; set; } = Array.Empty<RedirectEntryDto>();
    }

    private class RedirectEntryDto
    {
        [JsonPropertyName("source_path")]
        public string SourcePath { get; set; } = "";

        [JsonPropertyName("redirect_url")]
        public string RedirectUrl { get; set; } = "";

        [JsonPropertyName("redirect_document_id")]
        public bool RedirectDocumentId { get; set; }
    }
}

/// <summary>
/// Represents a single redirect entry in .openpublishing.redirection.json.
/// </summary>
public record RedirectEntry(string SourcePath, string RedirectUrl, bool RedirectDocumentId);
