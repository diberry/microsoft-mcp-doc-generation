// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Configuration for the tool family cleanup process.
/// </summary>
public class CleanupConfiguration
{
    /// <summary>
    /// Directory containing the generated tool family markdown files to be cleaned.
    /// Default: ./generated/multi-page
    /// </summary>
    public string InputDirectory { get; set; } = "../generated/multi-page";

    /// <summary>
    /// Directory where individual prompts for each tool family file will be saved.
    /// Default: ./generated/tool-family-cleanup-prompts
    /// </summary>
    public string PromptsOutputDirectory { get; set; } = "../generated/tool-family-cleanup-prompts";

    /// <summary>
    /// Directory where cleaned markdown files will be saved.
    /// Default: ./generated/tool-family-cleanup
    /// </summary>
    public string CleanupOutputDirectory { get; set; } = "../generated/tool-family-cleanup";
}
