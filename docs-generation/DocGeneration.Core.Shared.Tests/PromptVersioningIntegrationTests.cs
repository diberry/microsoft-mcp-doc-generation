using System.Text.Json;
using Xunit;
using Shared;

namespace Shared.Tests;

/// <summary>
/// Tests for StepResultFile v2 schema with prompt snapshots,
/// backward compatibility with v1, and StepResultWriter integration.
/// </summary>
public class PromptVersioningIntegrationTests : IDisposable
{
    private readonly string _testDir;

    public PromptVersioningIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"prompt-versioning-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // --- StepResultFile v2 serialization tests ---

    [Fact]
    public void StepResultFile_WithPromptSnapshots_RoundTrips()
    {
        var result = new StepResultFile
        {
            Version = 2,
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy",
            OutputFileCount = 5,
            Duration = "00:02:15.123",
            PromptSnapshots = new List<StepResultFile.PromptSnapshotRecord>
            {
                new()
                {
                    FileName = "system-prompt.txt",
                    ContentHash = "abc123def456",
                    SizeBytes = 1024
                },
                new()
                {
                    FileName = "user-prompt.txt",
                    ContentHash = "789xyz000111",
                    SizeBytes = 2048
                }
            }
        };

        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Version);
        Assert.NotNull(deserialized.PromptSnapshots);
        Assert.Equal(2, deserialized.PromptSnapshots!.Count);
        Assert.Equal("system-prompt.txt", deserialized.PromptSnapshots[0].FileName);
        Assert.Equal("abc123def456", deserialized.PromptSnapshots[0].ContentHash);
        Assert.Equal(1024, deserialized.PromptSnapshots[0].SizeBytes);
        Assert.Equal("user-prompt.txt", deserialized.PromptSnapshots[1].FileName);
    }

    [Fact]
    public void StepResultFile_V1Json_WithoutPromptSnapshots_DeserializesWithoutError()
    {
        // Simulates reading a v1 step-result.json that has no promptSnapshots field
        var v1Json = """
        {
          "version": 1,
          "status": "success",
          "step": "Step 3 - Tool Generation",
          "namespace": "deploy",
          "outputFileCount": 5,
          "warnings": [],
          "errors": [],
          "duration": "00:02:15.123"
        }
        """;

        var result = JsonSerializer.Deserialize<StepResultFile>(v1Json);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Version);
        Assert.Null(result.PromptSnapshots);
        Assert.Equal(StepResultStatus.Success, result.Status);
    }

    [Fact]
    public void StepResultFile_V2Json_WithPromptSnapshots_DeserializesCorrectly()
    {
        var v2Json = """
        {
          "version": 2,
          "status": "success",
          "step": "Step 2 - Example Prompts",
          "namespace": "storage",
          "outputFileCount": 10,
          "warnings": [],
          "errors": [],
          "duration": "00:05:00.000",
          "promptSnapshots": [
            {
              "fileName": "system-prompt.txt",
              "contentHash": "a1b2c3d4e5f6",
              "sizeBytes": 512
            }
          ]
        }
        """;

        var result = JsonSerializer.Deserialize<StepResultFile>(v2Json);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Version);
        Assert.NotNull(result.PromptSnapshots);
        Assert.Single(result.PromptSnapshots!);
        Assert.Equal("system-prompt.txt", result.PromptSnapshots[0].FileName);
        Assert.Equal("a1b2c3d4e5f6", result.PromptSnapshots[0].ContentHash);
        Assert.Equal(512, result.PromptSnapshots[0].SizeBytes);
    }

    [Fact]
    public void StepResultFile_PromptSnapshots_AppearsInJsonSchema()
    {
        var result = new StepResultFile
        {
            Version = 2,
            Status = StepResultStatus.Success,
            Step = "Step 3",
            Namespace = "deploy",
            PromptSnapshots = new List<StepResultFile.PromptSnapshotRecord>
            {
                new() { FileName = "test.txt", ContentHash = "abc", SizeBytes = 100 }
            }
        };

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("promptSnapshots", out var snapshotsEl));
        Assert.Equal(JsonValueKind.Array, snapshotsEl.ValueKind);
        Assert.Equal(1, snapshotsEl.GetArrayLength());

        var first = snapshotsEl[0];
        Assert.True(first.TryGetProperty("fileName", out _));
        Assert.True(first.TryGetProperty("contentHash", out _));
        Assert.True(first.TryGetProperty("sizeBytes", out _));
    }

    // --- StepResultWriter.AddPromptSnapshots tests ---

    [Fact]
    public void AddPromptSnapshots_ConvertsSnapshotsToRecords()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Step = "Step 3",
            Namespace = "deploy"
        };

        var snapshots = new[]
        {
            new PromptSnapshot("system.txt", "hash1", 100, DateTimeOffset.UtcNow),
            new PromptSnapshot("user.txt", "hash2", 200, DateTimeOffset.UtcNow)
        };

        StepResultWriter.AddPromptSnapshots(result, snapshots);

        Assert.NotNull(result.PromptSnapshots);
        Assert.Equal(2, result.PromptSnapshots!.Count);
        Assert.Equal("system.txt", result.PromptSnapshots[0].FileName);
        Assert.Equal("hash1", result.PromptSnapshots[0].ContentHash);
        Assert.Equal(100, result.PromptSnapshots[0].SizeBytes);
        Assert.Equal("user.txt", result.PromptSnapshots[1].FileName);
        Assert.Equal("hash2", result.PromptSnapshots[1].ContentHash);
        Assert.Equal(200, result.PromptSnapshots[1].SizeBytes);
    }

    [Fact]
    public void AddPromptSnapshots_SetsVersionTo2()
    {
        var result = new StepResultFile();
        var snapshots = new[]
        {
            new PromptSnapshot("test.txt", "hash", 50, DateTimeOffset.UtcNow)
        };

        StepResultWriter.AddPromptSnapshots(result, snapshots);

        Assert.Equal(2, result.Version);
    }

    // --- Write + Read round-trip with prompt snapshots ---

    [Fact]
    public void WriteAndRead_WithPromptSnapshots_PreservesData()
    {
        var result = new StepResultFile
        {
            Version = 2,
            Status = StepResultStatus.Success,
            Step = "Step 3",
            Namespace = "deploy",
            OutputFileCount = 5,
            Duration = "00:01:00.000",
            PromptSnapshots = new List<StepResultFile.PromptSnapshotRecord>
            {
                new()
                {
                    FileName = "system-prompt.txt",
                    ContentHash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
                    SizeBytes = 4096
                }
            }
        };

        StepResultWriter.Write(_testDir, result);
        var success = StepResultReader.TryRead(_testDir, out var loaded);

        Assert.True(success);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Version);
        Assert.NotNull(loaded.PromptSnapshots);
        Assert.Single(loaded.PromptSnapshots!);
        Assert.Equal("system-prompt.txt", loaded.PromptSnapshots[0].FileName);
        Assert.Equal("abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            loaded.PromptSnapshots[0].ContentHash);
        Assert.Equal(4096, loaded.PromptSnapshots[0].SizeBytes);
    }
}
