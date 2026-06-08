// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ToolGeneration_Improved.Models;

namespace ToolGeneration_Improved.Services;

/// <summary>
/// Reducer that deterministically extracts the inputs for the ToolGeneration AI stage
/// from upstream step envelopes, producing a typed <see cref="ToolGenerationContext"/>
/// for the pre-AI gate and the LLM call.
/// </summary>
public sealed class ToolGenerationReducer
{
    private const string CurrentSchemaVersion = "1.0";

    private static readonly Regex McpCliCommandRegex = new(
        @"<!--\s*@mcpcli\s+(?<command>.*?)\s*-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<ToolGenerationContext> ReduceAsync(
        string composedToolsDir,
        string toolFileName,
        int maxTokens,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(composedToolsDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolFileName);

        var composedFilePath = Path.Combine(composedToolsDir, toolFileName);
        if (!File.Exists(composedFilePath))
        {
            throw new FileNotFoundException($"Composed tool file not found: {composedFilePath}", composedFilePath);
        }

        var composedContent = await File.ReadAllTextAsync(composedFilePath, ct);
        var toolName = ExtractToolName(composedContent, toolFileName);

        return new ToolGenerationContext(
            toolName,
            composedContent,
            maxTokens,
            CurrentSchemaVersion);
    }

    private static string ExtractToolName(string composedContent, string toolFileName)
    {
        var match = McpCliCommandRegex.Match(composedContent);
        if (match.Success)
        {
            return match.Groups["command"].Value.Trim();
        }

        return Path.GetFileNameWithoutExtension(toolFileName);
    }
}
