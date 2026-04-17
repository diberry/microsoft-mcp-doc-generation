// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Xunit;
using Shared;

namespace Shared.Tests;

public class AiResponseArchiveWriterTests : IDisposable
{
    private readonly string _testDir;
    private const string EnvVar = "ARCHIVE_AI_RESPONSES";

    public AiResponseArchiveWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ai-archive-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        // Ensure env var is unset at test start (default = enabled)
        Environment.SetEnvironmentVariable(EnvVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private static AiResponseArchiveEntry MakeEntry(string step = "step2", string toolName = "storage_blob_list") =>
        new()
        {
            Step = step,
            ToolName = toolName,
            PromptHash = "abc123",
            RawResponse = "{\"result\":\"ok\"}",
            Model = "gpt-4o-mini",
            Timestamp = new DateTimeOffset(2025, 7, 15, 10, 0, 0, TimeSpan.Zero),
            PromptTokens = 100,
            CompletionTokens = 200
        };

    [Fact]
    public async Task WriteAsync_CreatesFileAtExpectedPath()
    {
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(_testDir, "storage", entry);

        var expected = Path.Combine(_testDir, "ai-responses", "step2-storage_blob_list.json");
        Assert.True(File.Exists(expected), $"Expected file at {expected}");
    }

    [Fact]
    public async Task WriteAsync_CreatesDirectoryIfMissing()
    {
        var nested = Path.Combine(_testDir, "deep", "output");
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(nested, "advisor", entry);

        var dir = Path.Combine(nested, "ai-responses");
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public async Task WriteAsync_ProducesValidDeserializableJson()
    {
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(_testDir, "keyvault", entry);

        var filePath = Path.Combine(_testDir, "ai-responses", "step2-storage_blob_list.json");
        var json = await File.ReadAllTextAsync(filePath);
        var deserialized = JsonSerializer.Deserialize<AiResponseArchiveEntry>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("step2", deserialized!.Step);
        Assert.Equal("storage_blob_list", deserialized.ToolName);
        Assert.Equal("{\"result\":\"ok\"}", deserialized.RawResponse);
        Assert.Equal("gpt-4o-mini", deserialized.Model);
        Assert.Equal(100, deserialized.PromptTokens);
        Assert.Equal(200, deserialized.CompletionTokens);
    }

    [Fact]
    public async Task WriteAsync_ProducesIndentedJson()
    {
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(_testDir, "monitor", entry);

        var filePath = Path.Combine(_testDir, "ai-responses", "step2-storage_blob_list.json");
        var json = await File.ReadAllTextAsync(filePath);

        Assert.Contains("\n", json);
        Assert.Contains("\"step\"", json);
    }

    [Fact]
    public async Task WriteAsync_OverwritesExistingFile()
    {
        var entry1 = MakeEntry();
        entry1.RawResponse = "first";
        await AiResponseArchiveWriter.WriteAsync(_testDir, "storage", entry1);

        var entry2 = MakeEntry();
        entry2.RawResponse = "second";
        await AiResponseArchiveWriter.WriteAsync(_testDir, "storage", entry2);

        var filePath = Path.Combine(_testDir, "ai-responses", "step2-storage_blob_list.json");
        var json = await File.ReadAllTextAsync(filePath);
        var loaded = JsonSerializer.Deserialize<AiResponseArchiveEntry>(json);

        Assert.Equal("second", loaded!.RawResponse);
    }

    [Fact]
    public async Task WriteAsync_MultipleEntries_DontInterfere()
    {
        var entry1 = MakeEntry(step: "step2", toolName: "storage_blob_list");
        var entry2 = MakeEntry(step: "step3", toolName: "keyvault_secret_get");
        entry2.RawResponse = "keyvault response";

        await AiResponseArchiveWriter.WriteAsync(_testDir, "storage", entry1);
        await AiResponseArchiveWriter.WriteAsync(_testDir, "keyvault", entry2);

        var file1 = Path.Combine(_testDir, "ai-responses", "step2-storage_blob_list.json");
        var file2 = Path.Combine(_testDir, "ai-responses", "step3-keyvault_secret_get.json");

        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));

        var loaded1 = JsonSerializer.Deserialize<AiResponseArchiveEntry>(
            await File.ReadAllTextAsync(file1));
        var loaded2 = JsonSerializer.Deserialize<AiResponseArchiveEntry>(
            await File.ReadAllTextAsync(file2));

        Assert.Equal("{\"result\":\"ok\"}", loaded1!.RawResponse);
        Assert.Equal("keyvault response", loaded2!.RawResponse);
    }

    [Fact]
    public async Task WriteAsync_IsNoOp_WhenEnvVarSetToFalse()
    {
        Environment.SetEnvironmentVariable(EnvVar, "false");
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(_testDir, "storage", entry);

        var dir = Path.Combine(_testDir, "ai-responses");
        Assert.False(Directory.Exists(dir), "ai-responses dir should not be created when disabled");
    }

    [Fact]
    public async Task WriteAsync_Writes_WhenEnvVarSetToTrue()
    {
        Environment.SetEnvironmentVariable(EnvVar, "true");
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(_testDir, "cosmos", entry);

        var expected = Path.Combine(_testDir, "ai-responses", "step2-storage_blob_list.json");
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public async Task WriteAsync_Writes_WhenEnvVarNotSet()
    {
        // Default behavior: archival is enabled
        Environment.SetEnvironmentVariable(EnvVar, null);
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(_testDir, "speech", entry);

        var expected = Path.Combine(_testDir, "ai-responses", "step2-storage_blob_list.json");
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public async Task WriteAsync_IsNoOp_WhenEnvVarSetTo0()
    {
        Environment.SetEnvironmentVariable(EnvVar, "0");
        var entry = MakeEntry();

        await AiResponseArchiveWriter.WriteAsync(_testDir, "sql", entry);

        var dir = Path.Combine(_testDir, "ai-responses");
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void IsEnabled_ReturnsTrueByDefault()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        Assert.True(AiResponseArchiveWriter.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenSetToFalse()
    {
        Environment.SetEnvironmentVariable(EnvVar, "false");
        Assert.False(AiResponseArchiveWriter.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenSetTo0()
    {
        Environment.SetEnvironmentVariable(EnvVar, "0");
        Assert.False(AiResponseArchiveWriter.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenSetToTrue()
    {
        Environment.SetEnvironmentVariable(EnvVar, "true");
        Assert.True(AiResponseArchiveWriter.IsEnabled());
    }
}
