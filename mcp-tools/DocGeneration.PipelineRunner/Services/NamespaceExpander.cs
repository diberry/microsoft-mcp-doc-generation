namespace PipelineRunner.Services;

/// <summary>
/// Resolves a requested namespace string to a list of concrete CLI namespace names,
/// applying prefix-based expansion for decomposed (split) namespaces and supporting
/// the special <c>all</c> keyword.
/// </summary>
public static class NamespaceExpander
{
    private const string AllKeyword = "all";

    /// <summary>
    /// Expands the requested namespace to a concrete list of CLI namespace names.
    /// </summary>
    /// <param name="requestedNamespace">Value of <c>--namespace</c>, or <c>null</c> to process all.</param>
    /// <param name="brandEntries">All entries from <c>brand-to-server-mapping.json</c>.</param>
    /// <param name="availableCliNamespaces">Namespaces reported by the installed MCP CLI.</param>
    /// <returns>An <see cref="NamespaceExpansionResult"/> describing the outcome.</returns>
    public static NamespaceExpansionResult Expand(
        string? requestedNamespace,
        IReadOnlyList<BrandMappingEntry> brandEntries,
        IReadOnlyList<string> availableCliNamespaces)
    {
        // null or "all" → all available CLI namespaces
        if (requestedNamespace is null
            || string.Equals(requestedNamespace.Trim(), AllKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return NamespaceExpansionResult.All(availableCliNamespaces);
        }

        var normalized = requestedNamespace.Trim();

        // Exact match in brand mapping → return it as-is (standalone/existing behavior)
        var exactMatch = brandEntries.FirstOrDefault(e =>
            string.Equals(e.McpServerName, normalized, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
        {
            return NamespaceExpansionResult.Resolved([exactMatch.McpServerName], isExpanded: false);
        }

        // Prefix match: namespace is a parent prefix — find all "namespace_*" sub-entries
        var prefix = normalized + "_";
        var subEntries = brandEntries
            .Where(e => e.McpServerName is not null)
            .Where(e => e.McpServerName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.McpServerName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (subEntries.Length > 0)
        {
            // Keep only sub-entries that are actually available in the CLI namespace list
            var availableSet = new HashSet<string>(availableCliNamespaces, StringComparer.OrdinalIgnoreCase);
            var resolved = subEntries
                .Where(e => e.McpServerName is not null)
                .Where(e => availableSet.Contains(e.McpServerName))
                .Select(e => e.McpServerName)
                .ToArray();

            if (resolved.Length == 0)
            {
                // Brand mapping knows about them but CLI does not expose them yet
                return NamespaceExpansionResult.SubEntriesNotInCli(
                    normalized,
                    subEntries.Select(e => e.McpServerName).ToArray());
            }

            return NamespaceExpansionResult.Resolved(resolved, isExpanded: true);
        }

        // Not found in brand mapping at all — signal caller to apply CLI exact-match fallback
        return NamespaceExpansionResult.NotInBrandMapping(normalized);
    }
}

/// <summary>
/// Outcome of a <see cref="NamespaceExpander.Expand"/> call.
/// </summary>
public sealed class NamespaceExpansionResult
{
    private NamespaceExpansionResult() { }

    /// <summary>Resolved namespace names to process (populated for All and Resolved outcomes).</summary>
    public IReadOnlyList<string> Namespaces { get; private init; } = [];

    /// <summary>True when the result covers all available CLI namespaces.</summary>
    public bool IsAll { get; private init; }

    /// <summary>True when one or more specific namespaces were resolved.</summary>
    public bool IsResolved { get; private init; }

    /// <summary>
    /// True when the input was a parent prefix and multiple sub-namespaces were expanded.
    /// Always false for exact-match and all-namespace cases.
    /// </summary>
    public bool IsExpanded { get; private init; }

    /// <summary>True when the input was not found in brand-to-server-mapping.json.</summary>
    public bool IsNotInBrandMapping { get; private init; }

    /// <summary>True when brand-to-server-mapping.json has sub-entries but none are in the CLI list.</summary>
    public bool IsSubEntriesNotInCli { get; private init; }

    /// <summary>The namespace string that was requested (populated for error outcomes).</summary>
    public string? RequestedNamespace { get; private init; }

    /// <summary>Sub-entries found in brand mapping but absent from the CLI (populated for SubEntriesNotInCli).</summary>
    public IReadOnlyList<string> SubEntriesFound { get; private init; } = [];

    public static NamespaceExpansionResult All(IReadOnlyList<string> namespaces)
        => new() { IsAll = true, IsResolved = true, Namespaces = namespaces };

    public static NamespaceExpansionResult Resolved(IReadOnlyList<string> namespaces, bool isExpanded)
        => new() { IsResolved = true, IsExpanded = isExpanded, Namespaces = namespaces };

    public static NamespaceExpansionResult SubEntriesNotInCli(string requested, IReadOnlyList<string> subEntriesFound)
        => new() { IsSubEntriesNotInCli = true, RequestedNamespace = requested, SubEntriesFound = subEntriesFound };

    public static NamespaceExpansionResult NotInBrandMapping(string requested)
        => new() { IsNotInBrandMapping = true, RequestedNamespace = requested };
}
