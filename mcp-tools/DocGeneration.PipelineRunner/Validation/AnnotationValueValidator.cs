using System.Text.RegularExpressions;

namespace PipelineRunner.Validation;

/// <summary>
/// Validates that the annotation-table VALUES (✅/❌) rendered in an assembled
/// tool-family article match the source-of-truth boolean values from the CLI
/// <c>tools list</c> metadata (<c>metadata.&lt;field&gt;.value</c>). A mismatch means an
/// AI or post-processing step corrupted an emoji value; it is surfaced as a blocking
/// issue so stale/incorrect annotations are caught at generation time rather than in docs.
///
/// Column order is fixed and matches the annotation table header:
/// Destructive, Idempotent, Open World, Read Only, Secret, Local Required.
///
/// This type is service-agnostic: it associates each annotation value row with the
/// nearest preceding <c>&lt;!-- @mcpcli &lt;command&gt; --&gt;</c> marker and compares against the
/// expected values keyed by that same command. See #695.
/// </summary>
public static class AnnotationValueValidator
{
    /// <summary>Annotation columns in fixed left-to-right order.</summary>
    internal static readonly string[] ColumnFields =
        ["Destructive", "Idempotent", "Open World", "Read Only", "Secret", "Local Required"];

    private static readonly Regex McpCliRegex = new(
        @"^\s*<!--\s*@mcpcli\s+(.+?)\s*-->\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HeaderRowRegex = new(
        @"^\s*\|\s*Destructive\s*\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Scans the assembled article for annotation value rows and compares each against
    /// the expected CLI-metadata values for the command it belongs to.
    /// </summary>
    /// <param name="articleContent">The full assembled tool-family article markdown.</param>
    /// <param name="expectedByCommand">
    /// Map of MCP command string (as it appears in <c>@mcpcli</c> markers) to the six expected
    /// boolean values in <see cref="ColumnFields"/> order. Commands absent from the map are skipped
    /// (their structural correctness is the format/cross-reference validators' responsibility).
    /// </param>
    /// <returns>One blocking issue string per field whose article value disagrees with the metadata.</returns>
    public static IReadOnlyList<string> GetValueMismatchIssues(
        string articleContent,
        IReadOnlyDictionary<string, bool[]> expectedByCommand)
    {
        if (string.IsNullOrEmpty(articleContent) || expectedByCommand.Count == 0)
        {
            return Array.Empty<string>();
        }

        var lines = articleContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var issues = new List<string>();
        string? currentCommand = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var markerMatch = McpCliRegex.Match(lines[i]);
            if (markerMatch.Success)
            {
                currentCommand = NormalizeCommand(markerMatch.Groups[1].Value);
                continue;
            }

            if (!HeaderRowRegex.IsMatch(lines[i]) || i + 2 >= lines.Length)
            {
                continue;
            }

            // Header at i, separator at i+1, value row at i+2 (contiguous markdown table).
            var actual = ParseValueRow(lines[i + 2]);
            if (actual is null)
            {
                continue;
            }

            i += 2; // consume the header/separator/value rows

            if (currentCommand is null || !expectedByCommand.TryGetValue(currentCommand, out var expected))
            {
                continue;
            }

            for (var c = 0; c < ColumnFields.Length && c < expected.Length; c++)
            {
                if (actual[c] != expected[c])
                {
                    issues.Add(
                        $"🛑 {currentCommand}: annotation '{ColumnFields[c]}' value mismatch " +
                        $"(article shows {Emoji(actual[c])}, CLI metadata says {Emoji(expected[c])}). " +
                        "Regenerate the namespace; do not hand-edit annotation tables.");
                }
            }
        }

        return issues;
    }

    /// <summary>
    /// Parses a single annotation value row into six booleans. Returns <see langword="null"/>
    /// when the row is not a well-formed value row (wrong cell count, separator row, or a cell
    /// that is neither ✅ nor ❌) so callers skip it and leave structural checks to the format validator.
    /// </summary>
    internal static bool[]? ParseValueRow(string row)
    {
        var trimmed = row.Trim();
        if (!trimmed.StartsWith('|'))
        {
            return null;
        }

        var cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
        if (cells.Length != ColumnFields.Length)
        {
            return null;
        }

        var result = new bool[ColumnFields.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            switch (cells[i])
            {
                case "✅":
                    result[i] = true;
                    break;
                case "❌":
                    result[i] = false;
                    break;
                default:
                    return null;
            }
        }

        return result;
    }

    private static string Emoji(bool value) => value ? "✅" : "❌";

    private static string NormalizeCommand(string command) =>
        WhitespaceRegex.Replace(command.Trim(), " ");
}
