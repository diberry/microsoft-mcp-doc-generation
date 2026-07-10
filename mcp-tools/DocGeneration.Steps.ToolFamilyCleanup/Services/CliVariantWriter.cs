// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace DocGeneration.Steps.ToolFamilyCleanup.Services;

/// <summary>
/// Emits the two per-namespace tool-family variants:
///   • the canonical article (<c>{name}.md</c>) — plain MCP content, NO CLI tabs.
///   • the CLI variant (<c>{name}-cli.md</c>) — CLI tabs applied when available.
///
/// The canonical article is never modified by this writer. The CLI variant is
/// ALWAYS written when the canonical article exists, even when a namespace has no
/// CLI content or CLI tabs are disabled — in those cases the variant is an exact
/// copy of the canonical article. This guarantees exactly two files per namespace.
/// </summary>
public static class CliVariantWriter
{
    /// <summary>Suffix appended (before the extension) to the canonical file name for the CLI variant.</summary>
    public const string CliVariantSuffix = "-cli";

    /// <summary>
    /// Resolves the CLI-variant path that sits beside the canonical article.
    /// Example: <c>tool-family/storage.md</c> → <c>tool-family/storage-cli.md</c>.
    /// </summary>
    /// <param name="canonicalArticlePath">Path to the canonical <c>{name}.md</c> article.</param>
    /// <returns>The sibling <c>{name}-cli.md</c> path.</returns>
    public static string ResolveVariantPath(string canonicalArticlePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalArticlePath);

        var directory = Path.GetDirectoryName(canonicalArticlePath);
        var fileName = Path.GetFileNameWithoutExtension(canonicalArticlePath);
        var extension = Path.GetExtension(canonicalArticlePath); // includes leading '.', may be empty

        var variantFileName = $"{fileName}{CliVariantSuffix}{extension}";
        return string.IsNullOrEmpty(directory)
            ? variantFileName
            : Path.Combine(directory, variantFileName);
    }

    /// <summary>
    /// Builds the CLI-variant content for an article.
    /// Returns CLI-tabbed markdown when tabs are permitted and CLI content exists;
    /// otherwise returns the plain markdown unchanged (an exact copy of the canonical article).
    /// </summary>
    /// <param name="plainMarkdown">The canonical (plain) article markdown.</param>
    /// <param name="assembledContent">Assembled CLI content keyed by command, or <c>null</c> when none.</param>
    /// <param name="namespaceAllowed">Whether CLI tab generation is enabled for the namespace.</param>
    public static string BuildVariantContent(
        string plainMarkdown,
        IReadOnlyDictionary<string, string>? assembledContent,
        bool namespaceAllowed)
    {
        ArgumentNullException.ThrowIfNull(plainMarkdown);

        if (namespaceAllowed && assembledContent is { Count: > 0 })
        {
            return Shared.CliTabWrapper.ApplyTabsToFamilyArticle(plainMarkdown, assembledContent);
        }

        // Always-two guarantee: with no CLI data (or disabled), the CLI variant is a copy.
        return plainMarkdown;
    }

    /// <summary>
    /// Writes the CLI variant (<c>{name}-cli.md</c>) beside the canonical article, leaving the
    /// canonical article untouched. No-op when the canonical article does not exist.
    /// </summary>
    /// <param name="canonicalArticlePath">Path to the canonical <c>{name}.md</c> article.</param>
    /// <param name="assembledContent">Assembled CLI content keyed by command, or <c>null</c> when none.</param>
    /// <param name="namespaceAllowed">Whether CLI tab generation is enabled for the namespace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path of the CLI variant that was written, or <c>null</c> when the canonical article is missing.</returns>
    public static async Task<string?> WriteVariantsAsync(
        string canonicalArticlePath,
        IReadOnlyDictionary<string, string>? assembledContent,
        bool namespaceAllowed,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalArticlePath);

        if (!File.Exists(canonicalArticlePath))
        {
            return null;
        }

        var plainMarkdown = await File.ReadAllTextAsync(canonicalArticlePath, cancellationToken);
        var variantPath = ResolveVariantPath(canonicalArticlePath);
        var variantContent = BuildVariantContent(plainMarkdown, assembledContent, namespaceAllowed);

        await File.WriteAllTextAsync(variantPath, variantContent, Encoding.UTF8, cancellationToken);
        return variantPath;
    }
}
