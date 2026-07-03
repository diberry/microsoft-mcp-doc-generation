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
    /// Appends <c>-{version}</c> and, when supplied, <c>-{yyyy-MM-dd-HHmmss}</c> to the
    /// final segment of <paramref name="outputDir"/> (e.g. <c>../generated-skills/</c> →
    /// <c>../generated-skills-1.1.72-2026-05-31-162525</c>), so each run lands in a
    /// version- and time-stamped directory. When <paramref name="version"/> is null/blank
    /// the version segment is omitted; when <paramref name="timestamp"/> is null the time
    /// segment is omitted. Returns <paramref name="outputDir"/> unchanged when neither is
    /// supplied.
    /// </summary>
    public static string ApplyVersionSuffix(string outputDir, string? version, DateTimeOffset? timestamp = null)
    {
        var hasVersion = !string.IsNullOrWhiteSpace(version);
        if (!hasVersion && timestamp is null)
            return outputDir;

        var trimmed = outputDir.TrimEnd('/', '\\');
        if (trimmed.Length == 0)
            return outputDir;

        if (hasVersion)
            trimmed = $"{trimmed}-{version!.Trim()}";

        if (timestamp is { } ts)
            trimmed = $"{trimmed}-{ts:yyyy-MM-dd-HHmmss}";

        return trimmed;
    }

    private static IEnumerable<string> EnumeratePluginJsonCandidates(string sourcePath)
    {
        yield return Path.Combine(sourcePath, "plugin.json");
        yield return Path.Combine(sourcePath, "..", "plugin.json");
    }
}
