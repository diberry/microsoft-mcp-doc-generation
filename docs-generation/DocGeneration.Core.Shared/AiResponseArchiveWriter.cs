// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Shared;

/// <summary>
/// Writes <see cref="AiResponseArchiveEntry"/> records to disk for audit trail.
/// Files are written to {outputDir}/ai-responses/{step}-{toolName}.json.
///
/// Archival is enabled by default. Set the environment variable
/// ARCHIVE_AI_RESPONSES=false (or "0") to disable writing.
/// </summary>
public static class AiResponseArchiveWriter
{
    /// <summary>Environment variable that controls whether archival is enabled.</summary>
    public const string EnvVarName = "ARCHIVE_AI_RESPONSES";

    /// <summary>Subdirectory under the output root where archive files are written.</summary>
    private const string ArchiveSubdir = "ai-responses";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Returns true if archival is enabled (default). Returns false only when
    /// the ARCHIVE_AI_RESPONSES environment variable is explicitly set to "false" or "0".
    /// </summary>
    public static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnvVarName);
        if (string.IsNullOrEmpty(value))
            return true;

        return !value.Equals("false", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("0", StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes an AI response archive entry to disk. The file is written to
    /// {outputDir}/ai-responses/{step}-{toolName}.json. Creates the directory
    /// if it does not exist. Overwrites any existing file for the same step+tool.
    /// No-op when archival is disabled via environment variable.
    /// </summary>
    /// <param name="outputDir">Root output directory for the generation run.</param>
    /// <param name="namespace">MCP namespace (for logging context; not used in file path).</param>
    /// <param name="entry">The archive entry to write.</param>
    public static async Task WriteAsync(string outputDir, string @namespace, AiResponseArchiveEntry entry)
    {
        if (!IsEnabled())
            return;

        var dir = Path.Combine(outputDir, ArchiveSubdir);
        Directory.CreateDirectory(dir);

        var fileName = $"{entry.Step}-{entry.ToolName}.json";
        var filePath = Path.Combine(dir, fileName);

        var json = JsonSerializer.Serialize(entry, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
}
