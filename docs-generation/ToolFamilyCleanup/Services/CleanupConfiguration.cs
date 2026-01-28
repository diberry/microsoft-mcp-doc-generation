// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Configuration for the tool family cleanup process.
/// Supports both single-phase (full file) and multi-phase (tool-level assembly) processing.
/// </summary>
public class CleanupConfiguration
{
    // ===== Single-Phase Mode Paths =====
    
    /// <summary>
    /// Directory containing the generated tool family markdown files to be cleaned (single-phase mode).
    /// Default: ../generated/tool-family
    /// </summary>
    public string InputDirectory { get; set; } = "../generated/tool-family";

    /// <summary>
    /// Directory where individual prompts for each tool family file will be saved (single-phase mode).
    /// Default: ../generated/tool-family-cleanup-prompts
    /// </summary>
    public string PromptsOutputDirectory { get; set; } = "../generated/tool-family-cleanup-prompts";

    /// <summary>
    /// Directory where cleaned markdown files will be saved (single-phase mode).
    /// Default: ../generated/tool-family-cleaned
    /// </summary>
    public string CleanupOutputDirectory { get; set; } = "../generated/tool-family-cleaned";
    
    // ===== Multi-Phase Mode Paths =====
    
    /// <summary>
    /// Directory containing complete tool markdown files (multi-phase mode input).
    /// Default: ../generated/tools
    /// </summary>
    public string ToolsInputDirectory { get; set; } = "../generated/tools";
    
    /// <summary>
    /// Directory where AI-generated metadata sections will be saved (multi-phase mode).
    /// Default: ../generated/tool-family-metadata
    /// </summary>
    public string MetadataOutputDirectory { get; set; } = "../generated/tool-family-metadata";
    
    /// <summary>
    /// Directory where AI-generated related content sections will be saved (multi-phase mode).
    /// Default: ../generated/tool-family-related
    /// </summary>
    public string RelatedContentOutputDirectory { get; set; } = "../generated/tool-family-related";
    
    /// <summary>
    /// Directory where final stitched tool family files will be saved (multi-phase mode).
    /// Default: ../generated/tool-family-multifile
    /// </summary>
    public string MultiFileOutputDirectory { get; set; } = "../generated/tool-family-multifile";
}
