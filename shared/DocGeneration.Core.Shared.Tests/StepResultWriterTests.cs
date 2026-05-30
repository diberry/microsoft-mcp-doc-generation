using System.Text.Json;
using Xunit;
using Shared;

namespace Shared.Tests;

public class StepResultWriterTests : IDisposable
{
    private readonly string _testDir;

    public StepResultWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"step-result-writer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void Write_CreatesFileOnDisk()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy",
            OutputFileCount = 5,
            Duration = "00:02:15.123"
        };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        Assert.True(File.Exists(filePath), $"Expected step-result.json at {filePath}");
    }

    [Fact]
    public void Write_ProducesValidJson()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy",
            OutputFileCount = 5,
            Duration = "00:02:15.123"
        };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        var json = File.ReadAllText(filePath);
        var deserialized = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(StepResultStatus.Success, deserialized!.Status);
        Assert.Equal("deploy", deserialized.Namespace);
        Assert.Equal(5, deserialized.OutputFileCount);
    }

    [Fact]
    public void Write_OverwritesExistingFile()
    {
        var first = new StepResultFile
        {
            Status = StepResultStatus.Failure,
            Step = "Step 3",
            Namespace = "deploy",
            Errors = new List<string> { "error" }
        };
        StepResultWriter.Write(_testDir, first);

        var second = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Step = "Step 3",
            Namespace = "deploy",
            OutputFileCount = 10
        };
        StepResultWriter.Write(_testDir, second);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        var json = File.ReadAllText(filePath);
        var loaded = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(loaded);
        Assert.Equal(StepResultStatus.Success, loaded!.Status);
        Assert.Equal(10, loaded.OutputFileCount);
        Assert.Empty(loaded.Errors);
    }

    [Fact]
    public void Write_CreatesDirectoryIfNeeded()
    {
        var nestedDir = Path.Combine(_testDir, "nested", "deep");
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Step = "Step 6",
            Namespace = "advisor"
        };

        StepResultWriter.Write(nestedDir, result);

        var filePath = Path.Combine(nestedDir, StepResultWriter.FileName);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void Write_ProducesIndentedJson()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy"
        };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        var json = File.ReadAllText(filePath);

        // Indented JSON has newlines
        Assert.Contains("\n", json);
        // And the property names appear on separate lines
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"status\"", json);
    }

    [Fact]
    public void FileName_IsStepResultJson()
    {
        Assert.Equal("step-result.json", StepResultWriter.FileName);
    }

    // ── Phase 1 Point 3: New envelope field serialization tests ──────────────

    [Fact]
    public void Write_NullEnvelopeFields_SerializeAsNull()
    {
        var result = new StepResultFile { Status = StepResultStatus.Success, Namespace = "advisor" };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out var sv));
        Assert.Equal(JsonValueKind.Null, sv.ValueKind);
        Assert.True(root.TryGetProperty("tokenUsageEnvelope", out var te));
        Assert.Equal(JsonValueKind.Null, te.ValueKind);
        Assert.True(root.TryGetProperty("promptArchivePath", out var pa));
        Assert.Equal(JsonValueKind.Null, pa.ValueKind);
        Assert.True(root.TryGetProperty("durationMs", out var dm));
        Assert.Equal(JsonValueKind.Null, dm.ValueKind);
    }

    [Fact]
    public void Write_PopulatedEnvelopeFields_SerializeCorrectly()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Namespace = "monitor",
            SchemaVersion = "1.0",
            StepName = "step-2-example-prompts",
            ValidationStatus = ValidationStatus.Passed,
            TokenUsageEnvelope = new TokenUsageEnvelope { PromptTokens = 750, CompletionTokens = 350 },
            PromptArchivePath = "archives/step2.zip",
            DurationMs = 62_000L,
            Timestamp = "2026-05-29T10:00:00Z",
            InputArtifacts = new List<ArtifactReference>
            {
                new() { Path = "input/monitor.json", Sha256 = "ff00aa" }
            },
            OutputArtifacts = new List<ArtifactReference>
            {
                new() { Path = "output/monitor-list.md", Sha256 = "bb1122" }
            }
        };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        var readBack = JsonSerializer.Deserialize<StepResultFile>(File.ReadAllText(filePath));

        Assert.NotNull(readBack);
        Assert.Equal("1.0", readBack!.SchemaVersion);
        Assert.Equal("step-2-example-prompts", readBack.StepName);
        Assert.Equal(ValidationStatus.Passed, readBack.ValidationStatus);
        Assert.Equal(750, readBack.TokenUsageEnvelope!.PromptTokens);
        Assert.Equal(350, readBack.TokenUsageEnvelope.CompletionTokens);
        Assert.Equal("archives/step2.zip", readBack.PromptArchivePath);
        Assert.Equal(62_000L, readBack.DurationMs);
        Assert.Equal("2026-05-29T10:00:00Z", readBack.Timestamp);
        Assert.Single(readBack.InputArtifacts!);
        Assert.Equal("input/monitor.json", readBack.InputArtifacts![0].Path);
        Assert.Single(readBack.OutputArtifacts!);
    }

    [Fact]
    public void Write_UniqueOutputArtifactPaths_Succeeds()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Namespace = "storage",
            OutputArtifacts = new List<ArtifactReference>
            {
                new() { Path = "output/storage-list.md", Sha256 = "aa11" },
                new() { Path = "output/storage-show.md", Sha256 = "bb22" }
            }
        };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void Write_DuplicateOutputArtifactPaths_ThrowsArtifactPathCollisionException()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Namespace = "monitor",
            OutputArtifacts = new List<ArtifactReference>
            {
                new() { Path = "output/health.md", Sha256 = "aa11" },
                new() { Path = "output/health.md", Sha256 = "bb22" }
            }
        };

        var ex = Assert.Throws<ArtifactPathCollisionException>(() => StepResultWriter.Write(_testDir, result));

        Assert.Equal("output/health.md", ex.DuplicatePath);
    }

    [Fact]
    public void Write_NullOutputArtifacts_Succeeds()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Namespace = "aks",
            OutputArtifacts = null
        };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void Write_EmptyOutputArtifacts_Succeeds()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Success,
            Namespace = "sql",
            OutputArtifacts = new List<ArtifactReference>()
        };

        StepResultWriter.Write(_testDir, result);

        var filePath = Path.Combine(_testDir, StepResultWriter.FileName);
        Assert.True(File.Exists(filePath));
    }
}
