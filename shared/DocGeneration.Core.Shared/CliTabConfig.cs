// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Configuration for CLI tab content generation.
/// Namespace allowlist controls which namespaces get CLI tabs.
/// Empty allowlist = feature disabled (kill switch).
/// </summary>
public class CliTabConfig
{
    /// <summary>
    /// Set of namespace names that should generate CLI tab content.
    /// Empty set means CLI tabs are disabled for all namespaces.
    /// </summary>
    public HashSet<string> AllowedNamespaces { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether CLI tab generation is enabled (allowlist is non-empty).
    /// </summary>
    public bool IsEnabled => AllowedNamespaces.Count > 0;

    /// <summary>
    /// Returns true if the given namespace should generate CLI tab content.
    /// </summary>
    public bool IsNamespaceAllowed(string namespaceName)
        => IsEnabled && AllowedNamespaces.Contains(namespaceName);

    /// <summary>
    /// Loads config from a JSON file. Returns default (disabled) config if file doesn't exist.
    /// </summary>
    public static CliTabConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return new CliTabConfig();

        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<CliTabConfig>(json)
            ?? new CliTabConfig();
    }

    /// <summary>
    /// Creates a config with the given allowed namespaces.
    /// </summary>
    public static CliTabConfig ForNamespaces(params string[] namespaces)
        => new() { AllowedNamespaces = new HashSet<string>(namespaces, StringComparer.OrdinalIgnoreCase) };
}
