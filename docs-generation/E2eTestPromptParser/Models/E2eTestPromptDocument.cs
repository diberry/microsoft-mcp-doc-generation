namespace E2eTestPromptParser.Models;

/// <summary>
/// The complete parsed result of an e2eTestPrompts.md file.
/// </summary>
public sealed class E2eTestPromptDocument
{
    /// <summary>
    /// The H1 title of the document.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// All service area sections (H2 headings) in document order.
    /// </summary>
    public IReadOnlyList<ServiceAreaSection> Sections { get; init; } = [];

    /// <summary>
    /// Flattened list of all test prompt entries across all sections.
    /// </summary>
    public IReadOnlyList<TestPromptEntry> AllEntries =>
        Sections.SelectMany(s => s.Entries).ToList();

    /// <summary>
    /// All distinct tool names across the entire document.
    /// </summary>
    public IReadOnlySet<string> ToolNames =>
        AllEntries.Select(e => e.ToolName).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Lookup entries by tool name. Returns empty if tool not found.
    /// </summary>
    public IReadOnlyList<TestPromptEntry> GetEntriesByToolName(string toolName) =>
        AllEntries.Where(e => e.ToolName.Equals(toolName, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// Find sections containing a specific tool name.
    /// </summary>
    public IReadOnlyList<ServiceAreaSection> GetSectionsByToolName(string toolName) =>
        Sections.Where(s => s.Entries.Any(e => e.ToolName.Equals(toolName, StringComparison.Ordinal))).ToList();

    /// <summary>
    /// Get the namespace prefix (text before first underscore) for each tool, mapped to sections.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TestPromptEntry>> GetEntriesByNamespace()
    {
        return AllEntries
            .GroupBy(e =>
            {
                var idx = e.ToolName.IndexOf('_', StringComparison.Ordinal);
                return idx > 0 ? e.ToolName[..idx] : e.ToolName;
            }, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TestPromptEntry>)g.ToList(),
                StringComparer.Ordinal);
    }
}
