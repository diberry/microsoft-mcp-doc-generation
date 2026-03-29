// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Unit tests for PromptTokenResolver — verifies token replacement,
/// caching, error handling, and no-op behavior for tokenless prompts.
/// </summary>
public class PromptTokenResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dataDir;

    public PromptTokenResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"prompt-resolver-test-{Guid.NewGuid():N}");
        _dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(_dataDir);

        // Write a test Acrolinx rules file
        File.WriteAllText(
            Path.Combine(_dataDir, "shared-acrolinx-rules.txt"),
            "## Acrolinx Rules\n- Use present tense\n- Use contractions\n");

        // Reset cache between tests
        PromptTokenResolver.ResetCache();
    }

    public void Dispose()
    {
        PromptTokenResolver.ResetCache();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Resolve_WithToken_ReplacesWithFileContent()
    {
        var prompt = "Some instructions\n\n{{ACROLINX_RULES}}\n\nMore instructions";

        var result = PromptTokenResolver.Resolve(prompt, _dataDir);

        Assert.Contains("Use present tense", result);
        Assert.Contains("Use contractions", result);
        Assert.DoesNotContain("{{ACROLINX_RULES}}", result);
        Assert.Contains("Some instructions", result);
        Assert.Contains("More instructions", result);
    }

    [Fact]
    public void Resolve_WithoutToken_ReturnsUnchanged()
    {
        var prompt = "No tokens here, just plain text.";

        var result = PromptTokenResolver.Resolve(prompt, _dataDir);

        Assert.Equal(prompt, result);
    }

    [Fact]
    public void Resolve_EmptyPrompt_ReturnsEmpty()
    {
        var result = PromptTokenResolver.Resolve(string.Empty, _dataDir);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Resolve_MultipleTokens_ReplacesAll()
    {
        var prompt = "Start\n{{ACROLINX_RULES}}\nMiddle\n{{ACROLINX_RULES}}\nEnd";

        var result = PromptTokenResolver.Resolve(prompt, _dataDir);

        // Both tokens replaced
        Assert.DoesNotContain("{{ACROLINX_RULES}}", result);
        // Content appears twice
        var count = result.Split("Use present tense").Length - 1;
        Assert.Equal(2, count);
    }

    [Fact]
    public void Resolve_MissingFile_ThrowsFileNotFoundWithClearMessage()
    {
        var emptyDataDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDataDir);

        var prompt = "{{ACROLINX_RULES}}";

        var ex = Assert.Throws<FileNotFoundException>(
            () => PromptTokenResolver.Resolve(prompt, emptyDataDir));

        Assert.Contains("shared-acrolinx-rules.txt", ex.Message);
        Assert.Contains("Ensure the data file is copied", ex.Message);
    }

    [Fact]
    public void Resolve_CachesFileContent_OnSubsequentCalls()
    {
        var prompt = "{{ACROLINX_RULES}}";

        // First call loads the file
        var result1 = PromptTokenResolver.Resolve(prompt, _dataDir);

        // Delete the file
        File.Delete(Path.Combine(_dataDir, "shared-acrolinx-rules.txt"));

        // Second call uses cache — no exception despite missing file
        var result2 = PromptTokenResolver.Resolve(prompt, _dataDir);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void ResetCache_ClearsCache_NextCallReloadsFile()
    {
        var prompt = "{{ACROLINX_RULES}}";

        // First call loads the file
        PromptTokenResolver.Resolve(prompt, _dataDir);

        // Update the file
        File.WriteAllText(
            Path.Combine(_dataDir, "shared-acrolinx-rules.txt"),
            "## Updated Rules\n- New rule here\n");

        // Reset cache
        PromptTokenResolver.ResetCache();

        // Next call reloads
        var result = PromptTokenResolver.Resolve(prompt, _dataDir);

        Assert.Contains("New rule here", result);
        Assert.DoesNotContain("Use present tense", result);
    }
}
