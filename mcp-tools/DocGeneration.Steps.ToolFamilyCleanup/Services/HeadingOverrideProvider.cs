// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Provides heading override lookups for MCP tool commands.
/// Loads heading-overrides.json at startup and serves exact-match overrides.
/// Override keys are full command strings (e.g., "compute vm create").
/// </summary>
public static class HeadingOverrideProvider
{
    private static readonly IReadOnlyDictionary<string, string> Overrides = LoadOverrides();

    /// <summary>
    /// Returns the override heading for a full command, or null if no override exists.
    /// Lookup is case-insensitive.
    /// </summary>
    public static string? GetOverride(string? fullCommand)
    {
        if (string.IsNullOrWhiteSpace(fullCommand))
            return null;

        Overrides.TryGetValue(fullCommand.Trim().ToLowerInvariant(), out var heading);
        return heading;
    }

    private static IReadOnlyDictionary<string, string> LoadOverrides()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Resource name: RootNamespace + "." + relative path with path separators → dots
        // Config\heading-overrides.json → DocGeneration.Steps.ToolFamilyCleanup.Config.heading-overrides.json
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("heading-overrides.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("overrides", out var overridesElement))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return overridesElement.Deserialize<Dictionary<string, string>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
