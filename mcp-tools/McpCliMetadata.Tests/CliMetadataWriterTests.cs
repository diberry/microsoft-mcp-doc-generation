// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using DocGeneration.McpCliMetadata;
using Xunit;

namespace DocGeneration.McpCliMetadata.Tests;

/// <summary>
/// Verifies the single source-side sanitization contract: the CLI metadata
/// artifacts are stripped of raw control characters (e.g. 0x1A SUB) that the
/// upstream azmcp CLI can emit, BEFORE they are written to disk — so every
/// downstream reader gets valid JSON and no reader needs to sanitize.
/// </summary>
public sealed class CliMetadataWriterTests : IDisposable
{
    private readonly string _root;

    public CliMetadataWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "cli-metadata-writer-tests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task WriteArtifactsAsync_WithRawControlChars_WritesSanitizedParseableFiles()
    {
        var cliDir = Path.Combine(_root, "cli");
        var versionJson = "{\"version\":\"3.0.0-beta.23\"}";
        // Raw (unescaped) 0x1A inside string values in both tools and namespace JSON.
        var toolsJson =
            "{\"results\":[{\"command\":\"monitor workspace list\",\"name\":\"list\"," +
            "\"description\":\"List Log\u001A Analytics workspaces.\"}]}";
        var namespaceJson =
            "{\"results\":[{\"name\":\"monitor\",\"description\":\"Monitor\u001A service.\"}]}";

        await CliMetadataWriter.WriteArtifactsAsync(cliDir, versionJson, toolsJson, namespaceJson);

        foreach (var fileName in new[] { "cli-version.json", "cli-output.json", "cli-namespace.json" })
        {
            var content = await File.ReadAllTextAsync(Path.Combine(cliDir, fileName));
            Assert.DoesNotContain('\u001A', content);   // raw control char stripped at the source
            using var _ = JsonDocument.Parse(content);   // on-disk artifact is valid JSON
        }
    }

    [Fact]
    public async Task WriteArtifactsAsync_PreservesToolData()
    {
        var cliDir = Path.Combine(_root, "cli");

        await CliMetadataWriter.WriteArtifactsAsync(
            cliDir,
            "{\"version\":\"1.2.3\"}",
            "{\"results\":[{\"command\":\"storage account list\",\"name\":\"list\"}]}",
            "{\"results\":[{\"name\":\"storage\"}]}");

        using var tools = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(cliDir, "cli-output.json")));
        Assert.Equal(
            "storage account list",
            tools.RootElement.GetProperty("results")[0].GetProperty("command").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
