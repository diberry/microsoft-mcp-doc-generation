// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Shared;

/// <summary>
/// Utility class for reading MCP CLI version from cli-version.json file
/// </summary>
public static class CliVersionReader
{
    /// <summary>
    /// Reads the CLI version from the cli-version.json file in the specified output directory
    /// </summary>
    /// <param name="outputDir">The root output directory containing the cli folder</param>
    /// <returns>The CLI version string, or "unknown" if the file is not found or cannot be read</returns>
    public static async Task<string> ReadCliVersionAsync(string outputDir)
    {
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return "unknown";
        }

        var versionFile = Path.Combine(outputDir, "cli", "cli-version.json");

        if (!File.Exists(versionFile))
        {
            Console.WriteLine($"Warning: CLI version file not found at {versionFile}");
            return "unknown";
        }

        try
        {
            var versionContent = await File.ReadAllTextAsync(versionFile);
            
            // Handle both JSON format and plain text format
            if (versionContent.Trim().StartsWith('{'))
            {
                // Try to parse as JSON
                using var jsonDoc = JsonDocument.Parse(versionContent);
                var root = jsonDoc.RootElement;
                
                // Try both lowercase and uppercase "version" property
                if (root.TryGetProperty("version", out var versionProp))
                {
                    return versionProp.GetString() ?? "unknown";
                }
                else if (root.TryGetProperty("Version", out var versionPropUppercase))
                {
                    return versionPropUppercase.GetString() ?? "unknown";
                }
                
                return "unknown";
            }
            else
            {
                // Plain text version
                return versionContent.Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not read CLI version from {versionFile}: {ex.Message}");
            return "unknown";
        }
    }

    /// <summary>
    /// Reads the CLI version synchronously from the cli-version.json file in the specified output directory
    /// </summary>
    /// <param name="outputDir">The root output directory containing the cli folder</param>
    /// <returns>The CLI version string, or "unknown" if the file is not found or cannot be read</returns>
    public static string ReadCliVersion(string outputDir)
    {
        return ReadCliVersionAsync(outputDir).GetAwaiter().GetResult();
    }
}
