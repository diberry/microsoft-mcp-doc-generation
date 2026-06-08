// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolGeneration_Improved.Models;

/// <summary>
/// Typed context for improving a single composed tool document.
/// </summary>
/// <param name="ToolName">The MCP tool command name.</param>
/// <param name="ComposedContent">The full composed markdown content for the tool.</param>
/// <param name="MaxTokens">The maximum token budget for the AI response.</param>
/// <param name="SchemaVersion">The reducer schema version.</param>
public sealed record ToolGenerationContext(
    string ToolName,
    string ComposedContent,
    int MaxTokens,
    string SchemaVersion = "1.0");
