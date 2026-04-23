// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Updates links in supported-azure-services.md when tool family files are renamed.
/// Validates service names against known list to catch typos.
/// Stub for TDD — implement to make SupportedServicesLinkUpdaterTests pass.
/// Fixes: #416 Item 5
/// </summary>
public static class SupportedServicesLinkUpdater
{
    /// <summary>
    /// Replaces an old filename with a new filename in supported-azure-services.md content.
    /// </summary>
    public static string UpdateLink(string supportedServicesContent, string oldFileName, string newFileName)
    {
        if (string.IsNullOrEmpty(supportedServicesContent))
            return supportedServicesContent ?? "";

        if (string.IsNullOrEmpty(oldFileName))
            return supportedServicesContent;

        // Use markdown link-aware replacement to avoid partial-match corruption
        // (e.g., renaming "storage.md" should not affect "cache-storage.md")
        // Match patterns: (oldFileName) or [text](path/oldFileName) 
        string result = supportedServicesContent;
        
        // Replace exact filename matches in markdown link syntax: ](path/oldFileName)
        // Also handle standalone references
        var oldPattern = System.Text.RegularExpressions.Regex.Escape(oldFileName);
        var linkPattern = $@"(?<=[(\s/]){oldPattern}(?=[\s)\]""#])";
        result = System.Text.RegularExpressions.Regex.Replace(result, linkPattern, newFileName);
        // Also handle end-of-line occurrences
        result = System.Text.RegularExpressions.Regex.Replace(result, $@"(?<=[(\s/]){oldPattern}$", newFileName, System.Text.RegularExpressions.RegexOptions.Multiline);

        // Handle root-relative paths: /azure/developer/azure-skills/skills/azure-storage
        if (oldFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            string oldPath = oldFileName[..^3];
            string newPath = newFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? newFileName[..^3]
                : newFileName;
            var oldPathPattern = System.Text.RegularExpressions.Regex.Escape(oldPath);
            var pathPattern = $@"(?<=[(\s/]){oldPathPattern}(?=[\s)\]""#])";
            result = System.Text.RegularExpressions.Regex.Replace(result, pathPattern, newPath);
            result = System.Text.RegularExpressions.Regex.Replace(result, $@"(?<=[(\s/]){oldPathPattern}$", newPath, System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        return result;
    }

    /// <summary>
    /// Validates service names against a known services list.
    /// Returns a list of error messages for any names not found.
    /// </summary>
    public static List<string> ValidateServiceNames(string[] serviceNames, string[] knownServices)
    {
        var errors = new List<string>();
        var knownSet = new HashSet<string>(knownServices, StringComparer.OrdinalIgnoreCase);

        foreach (var serviceName in serviceNames)
        {
            if (!knownSet.Contains(serviceName))
            {
                errors.Add($"Unknown service name: '{serviceName}'");
            }
        }

        return errors;
    }

    /// <summary>
    /// Checks whether a Learn URL is well-formed (relative .md link or site-root-relative path).
    /// </summary>
    public static bool IsWellFormedLearnUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Valid: site-root-relative (starts with /)
        if (url.StartsWith('/'))
            return true;

        // Valid: relative .md link
        if (url.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return true;

        // Invalid: full https:// URLs (per AD-017 convention)
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        return false;
    }
}
