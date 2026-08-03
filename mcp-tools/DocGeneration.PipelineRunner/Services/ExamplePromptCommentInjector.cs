// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace PipelineRunner.Services;

/// <summary>
/// Injects HTML comments into example prompt include files to highlight missing required parameters.
/// Comments are invisible in rendered markdown but visible in source, making gaps discoverable in
/// final article content and logs.
///
/// Design: Idempotent — calling multiple times produces same output. Comments include command+params
/// to enable deduplication and audit tracking.
/// </summary>
public class ExamplePromptCommentInjector
{
    private static readonly Regex WarningCommentRegex = new(
        @"<!--\s*⚠️\s*PIPELINE WARNING:.*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Injects an HTML comment into example prompt content when required parameters are missing.
    /// Comment is placed at the top of the file, before @mcpcli marker and content.
    ///
    /// Idempotent: calling twice with same params produces identical output (no duplicate comments).
    /// </summary>
    /// <param name="examplePromptContent">Original example prompt markdown content</param>
    /// <param name="command">Tool command (for identification and deduplication)</param>
    /// <param name="missingParams">Array of missing required parameter names</param>
    /// <returns>Content with injected warning comment, or original if no params missing</returns>
    public string InjectComment(string examplePromptContent, string command, params string[] missingParams)
    {
        ArgumentNullException.ThrowIfNull(examplePromptContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        // No-op if no params are missing
        if (missingParams == null || missingParams.Length == 0)
        {
            return examplePromptContent;
        }

        // Check if warning comment already exists (idempotent)
        if (WarningCommentRegex.IsMatch(examplePromptContent))
        {
            return examplePromptContent;
        }

        var formattedParams = FormatParameterList(missingParams);
        var warningComment = $"<!-- ⚠️ PIPELINE WARNING: AI-generated example prompts missing required parameter {formattedParams} — re-run Step 2 to regenerate -->";

        // Inject comment at the very top of the content
        return $"{warningComment}\n{examplePromptContent}";
    }

    /// <summary>
    /// Injects an HTML comment into an example prompt file on disk.
    /// Creates a backup with .bak extension before modifying the file.
    /// </summary>
    /// <param name="filePath">Path to the example prompt file</param>
    /// <param name="command">Tool command</param>
    /// <param name="missingParams">Missing required parameter names</param>
    public void InjectCommentToFile(string filePath, string command, params string[] missingParams)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        // No-op if no params missing
        if (missingParams == null || missingParams.Length == 0)
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Example prompt file not found: {filePath}");
        }

        var originalContent = File.ReadAllText(filePath);
        var modifiedContent = InjectComment(originalContent, command, missingParams);

        // Only write if content actually changed (avoids unnecessary I/O and file timestamp updates)
        if (modifiedContent != originalContent)
        {
            // Create backup
            var backupPath = $"{filePath}.bak";
            File.WriteAllText(backupPath, originalContent);

            // Write modified content
            File.WriteAllText(filePath, modifiedContent);
        }
    }

    /// <summary>
    /// Formats parameter names for display in HTML comment.
    /// Examples:
    /// - Single: 'account-name'
    /// - Dual: 'account-name' and 'vault-name'
    /// - Multiple: 'account-name', 'vault-name', and 'resource-group'
    /// </summary>
    private static string FormatParameterList(string[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return string.Empty;
        }

        if (parameters.Length == 1)
        {
            return $"'{parameters[0]}'";
        }

        if (parameters.Length == 2)
        {
            return $"'{parameters[0]}' and '{parameters[1]}'";
        }

        // Multiple parameters: comma-separated with "and" before last
        var quoted = parameters.Select(p => $"'{p}'").ToArray();
        var allButLast = string.Join(", ", quoted[..^1]);
        return $"{allButLast}, and {quoted[^1]}";
    }
}
