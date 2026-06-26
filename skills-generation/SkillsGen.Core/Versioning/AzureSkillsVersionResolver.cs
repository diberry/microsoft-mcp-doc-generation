using System.Text.Json;

namespace SkillsGen.Core.Versioning;

/// <summary>
/// Resolves the "all up" Azure Skills package version from the source repository's
/// <c>plugin.json</c> (the overall <c>azure</c> plugin version, e.g. <c>1.1.72</c>) —
/// NOT the version of any individual skill — and applies it to the generated output
/// folder name so each run lands in a version-stamped directory.
/// </summary>
public static class AzureSkillsVersionResolver
{
    /// <summary>
    /// Finds <c>plugin.json</c> at the source path (or its parent) and returns its
    /// top-level <c>version</c> value. Returns <c>null</c> when the file is missing,
    /// unreadable, or has no usable version string.
    /// </summary>
    public static string? ResolveVersion(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return null;

        foreach (var candidate in EnumeratePluginJsonCandidates(sourcePath))
        {
            if (!File.Exists(candidate))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                if (doc.RootElement.TryGetProperty("version", out var versionEl)
                    && versionEl.ValueKind == JsonValueKind.String)
                {
                    var version = versionEl.GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version!.Trim();
                }
            }
            catch (JsonException)
            {
                // Malformed plugin.json — fall through to the next candidate / null.
            }
        }

        return null;
    }

    /// <summary>
    /// Appends <c>-{version}</c> to the final segment of <paramref name="outputDir"/>
    /// (e.g. <c>../generated-skills/</c> → <c>../generated-skills-1.1.72</c>). Returns
    /// <paramref name="outputDir"/> unchanged when <paramref name="version"/> is null or blank.
    /// </summary>
    public static string ApplyVersionSuffix(string outputDir, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return outputDir;

        var trimmed = outputDir.TrimEnd('/', '\\');
        if (trimmed.Length == 0)
            return outputDir;

        return $"{trimmed}-{version.Trim()}";
    }

    private static IEnumerable<string> EnumeratePluginJsonCandidates(string sourcePath)
    {
        yield return Path.Combine(sourcePath, "plugin.json");
        yield return Path.Combine(sourcePath, "..", "plugin.json");
    }
}
