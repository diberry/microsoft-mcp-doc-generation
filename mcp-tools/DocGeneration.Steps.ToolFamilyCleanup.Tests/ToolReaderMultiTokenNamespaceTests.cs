// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests for multi-token namespace resolution in ToolReader.
/// Bug: ExtractFamilyNameFromContent only took the first whitespace-delimited token
/// of the @mcpcli command, so "extension azqr" → "extension" (wrong) and
/// "extension cli generate" → "extension" (wrong) instead of the correct
/// underscore-joined brand-mapping keys "extension_azqr" / "extension_cli_generate".
/// Fix: greedy longest-prefix brand-mapping lookup before falling back to first token.
/// </summary>
public class ToolReaderMultiTokenNamespaceTests : IDisposable
{
    private readonly string _tempDir;

    public ToolReaderMultiTokenNamespaceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"toolreader-multitok-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_ExtensionAzqrAnnotation_GroupsUnderExtensionAzqr()
    {
        // "extension azqr" is a 2-token namespace key stored as "extension_azqr" in brand mappings.
        // Before the fix, Split(' ')[0] returned "extension" → wrong family name.
        var content = "---\nms.topic: reference\n---\n# azqr<!-- @mcpcli extension azqr -->\n"
                    + "Runs Azure Quick Review CLI commands.\n";
        File.WriteAllText(Path.Combine(_tempDir, "azure-extension-compliance-review.md"), content);

        var reader = new ToolReader(_tempDir);
        var result = await reader.ReadAndGroupToolsAsync();

        Assert.Single(result);
        Assert.True(result.ContainsKey("extension_azqr"),
            $"Expected family 'extension_azqr' but got: [{string.Join(", ", result.Keys)}]");
        Assert.Single(result["extension_azqr"]);
        Assert.Equal("extension azqr", result["extension_azqr"][0].Command);
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_ExtensionCliGenerateAnnotation_GroupsUnderExtensionCliGenerate()
    {
        // "extension cli generate" is a 3-token namespace key stored as "extension_cli_generate".
        // Before the fix, Split(' ')[0] returned "extension" → wrong family name.
        var content = "---\nms.topic: reference\n---\n# generate<!-- @mcpcli extension cli generate -->\n"
                    + "Generates Azure CLI commands to accomplish a user goal.\n";
        File.WriteAllText(Path.Combine(_tempDir, "azure-extension-cli-generate.md"), content);

        var reader = new ToolReader(_tempDir);
        var result = await reader.ReadAndGroupToolsAsync();

        Assert.Single(result);
        Assert.True(result.ContainsKey("extension_cli_generate"),
            $"Expected family 'extension_cli_generate' but got: [{string.Join(", ", result.Keys)}]");
        Assert.Single(result["extension_cli_generate"]);
        Assert.Equal("extension cli generate", result["extension_cli_generate"][0].Command);
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_SingleTokenNamespace_StillGroupsCorrectly()
    {
        // Single-token namespaces like "advisor recommendation list" must still resolve to "advisor".
        // This is the existing behaviour and must not regress.
        var content = "<!-- @mcpcli advisor recommendation list -->\n\n# List Recommendations\n\nLists all advisor recommendations.\n";
        File.WriteAllText(Path.Combine(_tempDir, "advisor-recommendation-list.md"), content);

        var reader = new ToolReader(_tempDir);
        var result = await reader.ReadAndGroupToolsAsync();

        Assert.Single(result);
        Assert.True(result.ContainsKey("advisor"),
            $"Expected family 'advisor' but got: [{string.Join(", ", result.Keys)}]");
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_TwoExtensionNamespaces_GroupSeparatelyByFullNamespace()
    {
        // When both extension namespaces are present, each resolves to its own family.
        var azqrContent = "---\nms.topic: reference\n---\n# azqr<!-- @mcpcli extension azqr -->\n"
                        + "Runs compliance scans.\n";
        var cliContent = "---\nms.topic: reference\n---\n# generate<!-- @mcpcli extension cli generate -->\n"
                       + "Generates CLI commands.\n";

        File.WriteAllText(Path.Combine(_tempDir, "azure-extension-compliance-review.md"), azqrContent);
        File.WriteAllText(Path.Combine(_tempDir, "azure-extension-cli-generate.md"), cliContent);

        var reader = new ToolReader(_tempDir);
        var result = await reader.ReadAndGroupToolsAsync();

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("extension_azqr"),
            $"Expected 'extension_azqr' in keys: [{string.Join(", ", result.Keys)}]");
        Assert.True(result.ContainsKey("extension_cli_generate"),
            $"Expected 'extension_cli_generate' in keys: [{string.Join(", ", result.Keys)}]");
        Assert.Single(result["extension_azqr"]);
        Assert.Single(result["extension_cli_generate"]);
    }
}
