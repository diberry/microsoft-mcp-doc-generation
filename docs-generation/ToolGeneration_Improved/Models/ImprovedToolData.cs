// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolGeneration_Improved.Models;

/// <summary>
/// Represents an improved tool file with AI enhancements
/// </summary>
public class ImprovedToolData
{
    public string FileName { get; set; } = "";
    public string OriginalContent { get; set; } = "";
    public string ImprovedContent { get; set; } = "";
    public bool WasImproved { get; set; }
    public string? ErrorMessage { get; set; }
}
