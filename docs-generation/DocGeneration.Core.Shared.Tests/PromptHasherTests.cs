using Xunit;
using Shared;

namespace Shared.Tests;

public class PromptHasherTests : IDisposable
{
    private readonly string _testDir;

    public PromptHasherTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"prompt-hasher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // --- ComputeHash tests ---

    [Fact]
    public void ComputeHash_ReturnsSameSha256_ForSameInput()
    {
        var content = "You are a helpful assistant that generates documentation.";
        var hash1 = PromptHasher.ComputeHash(content);
        var hash2 = PromptHasher.ComputeHash(content);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public void ComputeHash_ReturnsDifferentHash_ForDifferentInput()
    {
        var hash1 = PromptHasher.ComputeHash("prompt version 1");
        var hash2 = PromptHasher.ComputeHash("prompt version 2");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHex()
    {
        var hash = PromptHasher.ComputeHash("test content");

        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeHash_HandlesEmptyString()
    {
        var hash = PromptHasher.ComputeHash("");

        // SHA256 of empty string is well-known
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    // --- HashFileAsync tests ---

    [Fact]
    public async Task HashFileAsync_ReadsFile_ReturnsCorrectMetadata()
    {
        var content = "System prompt content for testing";
        var filePath = Path.Combine(_testDir, "system-prompt.txt");
        await File.WriteAllTextAsync(filePath, content);

        var snapshot = await PromptHasher.HashFileAsync(filePath);

        Assert.Equal("system-prompt.txt", snapshot.FileName);
        var readBack = await File.ReadAllTextAsync(filePath);
        Assert.Equal(PromptHasher.ComputeHash(readBack), snapshot.ContentHash);
        Assert.True(snapshot.SizeBytes > 0);
        Assert.True(snapshot.LastModified <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HashFileAsync_WithResolver_ExpandsTokensBeforeHashing()
    {
        // Create data dir with Acrolinx rules file
        var dataDir = Path.Combine(_testDir, "data");
        Directory.CreateDirectory(dataDir);
        var rulesContent = "Rule 1: Use active voice.\nRule 2: Be concise.";
        await File.WriteAllTextAsync(Path.Combine(dataDir, "shared-acrolinx-rules.txt"), rulesContent);

        // Create prompt file with token
        var promptContent = "Follow these rules:\n{{ACROLINX_RULES}}\nEnd of rules.";
        var promptPath = Path.Combine(_testDir, "user-prompt.txt");
        await File.WriteAllTextAsync(promptPath, promptContent);

        // Reset cache so our data dir is used (not a leftover from parallel tests)
        PromptTokenResolver.ResetCache();

        var snapshot = await PromptHasher.HashFileAsync(promptPath, dataDir);

        // Compute expected hash by manually resolving the token
        var rawContent = await File.ReadAllTextAsync(promptPath);
        var rulesRead = await File.ReadAllTextAsync(Path.Combine(dataDir, "shared-acrolinx-rules.txt"));
        var resolvedContent = rawContent.Replace("{{ACROLINX_RULES}}", rulesRead);
        var expectedHash = PromptHasher.ComputeHash(resolvedContent);
        Assert.Equal(expectedHash, snapshot.ContentHash);

        // Hash should differ from raw content hash (token was expanded)
        var rawHash = PromptHasher.ComputeHash(rawContent);
        Assert.NotEqual(rawHash, snapshot.ContentHash);
    }

    [Fact]
    public async Task HashFileAsync_WithoutResolver_HashesRawContent()
    {
        var content = "Prompt with {{ACROLINX_RULES}} token but no resolver";
        var filePath = Path.Combine(_testDir, "raw-prompt.txt");
        await File.WriteAllTextAsync(filePath, content);

        var snapshot = await PromptHasher.HashFileAsync(filePath);

        var readBack = await File.ReadAllTextAsync(filePath);
        Assert.Equal(PromptHasher.ComputeHash(readBack), snapshot.ContentHash);
    }

    [Fact]
    public async Task HashFileAsync_ThrowsForMissingFile()
    {
        var missingPath = Path.Combine(_testDir, "nonexistent.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => PromptHasher.HashFileAsync(missingPath));
    }
}
