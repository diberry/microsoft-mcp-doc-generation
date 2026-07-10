// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;

namespace DocGeneration.McpCliMetadata;

/// <summary>
/// Writes the three CLI metadata artifacts — <c>cli-version.json</c>,
/// <c>cli-output.json</c>, and <c>cli-namespace.json</c> — to the cli directory.
/// This is the single source-side sanitization point: the upstream azmcp CLI can
/// emit raw control characters (notably 0x1A SUB) inside JSON string values, which
/// <see cref="System.Text.Json"/> rejects. Stripping them here, before the files
/// reach disk, guarantees every downstream reader receives valid JSON — so no
/// reader needs to sanitize.
/// </summary>
internal static class CliMetadataWriter
{
    internal static async Task WriteArtifactsAsync(
        string cliDirectory,
        string versionJson,
        string toolsJson,
        string namespaceJson,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cliDirectory);
        await File.WriteAllTextAsync(Path.Combine(cliDirectory, "cli-version.json"), Sanitize(versionJson), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(cliDirectory, "cli-output.json"), Sanitize(toolsJson), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(cliDirectory, "cli-namespace.json"), Sanitize(namespaceJson), cancellationToken);
    }

    internal static string Sanitize(string json)
        => JsonControlCharacterSanitizer.StripInvalidControlCharacters(json);
}
