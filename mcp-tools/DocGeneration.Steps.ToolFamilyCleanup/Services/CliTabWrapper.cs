// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Canonical implementation moved to DocGeneration.Core.Shared (Shared.CliTabWrapper).
// This file re-exports to avoid breaking existing consumers in this project.

using Shared;

namespace DocGeneration.Steps.ToolFamilyCleanup.Services;

/// <summary>
/// Thin re-export of <see cref="Shared.CliTabWrapper"/>.
/// Kept for backward-compatibility with code that imports the old namespace.
/// </summary>
public static class CliTabWrapper
{
    /// <inheritdoc cref="Shared.CliTabWrapper.WrapWithTabs"/>
    public static string WrapWithTabs(string mcpContent, string? cliContent)
        => Shared.CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

    /// <inheritdoc cref="Shared.CliTabWrapper.WrapWithTabsAndExtractDescription"/>
    public static (string TabBlock, string? Description) WrapWithTabsAndExtractDescription(string mcpContent, string? cliContent)
        => Shared.CliTabWrapper.WrapWithTabsAndExtractDescription(mcpContent, cliContent);

    /// <inheritdoc cref="Shared.CliTabWrapper.ApplyTabsToFamilyArticle"/>
    public static string ApplyTabsToFamilyArticle(
        string familyMarkdown,
        IReadOnlyDictionary<string, string> cliContentByCommand)
        => Shared.CliTabWrapper.ApplyTabsToFamilyArticle(familyMarkdown, cliContentByCommand);
}
