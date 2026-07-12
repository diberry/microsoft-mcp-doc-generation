using System.Text.Json;
using Xunit;
using Shared;

namespace Shared.Tests;

public class StepResultReaderTests : IDisposable
{
    private readonly string _testDir;

    public StepResultReaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"step-result-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private void WriteStepResult(StepResultFile result)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, json);
    }

    [Fact]
    public void TryRead_ReturnsTrue_WhenFileExists()
    {
        WriteStepResult(new StepResultFile
        {
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy",
            OutputFileCount = 5,
            Duration = "00:02:15.123"
        });

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(StepResultStatus.Success, result!.Status);
        Assert.Equal("Step 3 - Tool Generation", result.Step);
        Assert.Equal("deploy", result.Namespace);
        Assert.Equal(5, result.OutputFileCount);
        Assert.Equal("00:02:15.123", result.Duration);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenNoFile()
    {
        // _testDir exists but has no step-result.json
        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenDirectoryDoesNotExist()
    {
        var nonExistent = Path.Combine(_testDir, "does-not-exist");

        var found = StepResultReader.TryRead(nonExistent, out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenFileIsInvalidJson()
    {
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, "this is not json {{{");

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenFileIsEmptyJson()
    {
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, "");

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void TryRead_ReadsPartialStatus()
    {
        WriteStepResult(new StepResultFile
        {
            Status = StepResultStatus.Partial,
            Step = "Step 4",
            Namespace = "storage",
            OutputFileCount = 3,
            Warnings = new List<string> { "File X had issues" },
            Errors = new List<string> { "File Y failed" }
        });

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.Equal(StepResultStatus.Partial, result!.Status);
        Assert.Single(result.Warnings);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void TryRead_ReadsFailureWithErrors()
    {
        WriteStepResult(new StepResultFile
        {
            Status = StepResultStatus.Failure,
            Step = "Step 6",
            Namespace = "cosmos",
            Errors = new List<string> { "AI call failed", "Timeout" }
        });

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.Equal(StepResultStatus.Failure, result!.Status);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("AI call failed", result.Errors);
        Assert.Contains("Timeout", result.Errors);
    }

    [Fact]
    public void Exists_ReturnsTrue_WhenFilePresent()
    {
        WriteStepResult(new StepResultFile { Status = StepResultStatus.Success });

        Assert.True(StepResultReader.Exists(_testDir));
    }

    [Fact]
    public void Exists_ReturnsFalse_WhenFileMissing()
    {
        Assert.False(StepResultReader.Exists(_testDir));
    }

    [Fact]
    public void Exists_ReturnsFalse_WhenDirectoryMissing()
    {
        Assert.False(StepResultReader.Exists(Path.Combine(_testDir, "nope")));
    }

    // ── Phase 1 Point 3: Schema version awareness tests ──────────────────────

    [Fact]
    public void TryRead_LegacyV0_NoSchemaVersion_Succeeds()
    {
        // Legacy file without schemaVersion field — should be treated as v0, no exception
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, """
        {
          "version": 1,
          "status": "success",
          "step": "Step 1",
          "namespace": "monitor",
          "outputFileCount": 2,
          "warnings": [],
          "errors": [],
          "duration": "00:00:30.000"
        }
        """);

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(StepResultStatus.Success, result!.Status);
        Assert.Null(result.SchemaVersion);
    }

    [Fact]
    public void TryRead_SchemaVersion1_0_Succeeds()
    {
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, """
        {
          "version": 1,
          "schemaVersion": "1.0",
          "status": "success",
          "step": "Step 3",
          "namespace": "keyvault",
          "outputFileCount": 5,
          "warnings": [],
          "errors": [],
          "duration": "00:02:00.000"
        }
        """);

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("1.0", result!.SchemaVersion);
    }

    [Fact]
    public void TryRead_EmptyStringSchemaVersion_TreatedAsLegacy_Succeeds()
    {
        // #638 item 1: an empty schemaVersion is "not declared" — must fall back to legacy v0
        // (like null), not throw StepResultSchemaException.
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, """
        {
          "version": 1,
          "schemaVersion": "",
          "status": "success",
          "step": "Step 3",
          "namespace": "storage",
          "outputFileCount": 3,
          "warnings": [],
          "errors": [],
          "duration": "00:01:00.000"
        }
        """);

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(StepResultStatus.Success, result!.Status);
        Assert.Equal(string.Empty, result.SchemaVersion);
    }

    [Fact]
    public void TryRead_WhitespaceSchemaVersion_TreatedAsLegacy_Succeeds()
    {
        // Whitespace-only schemaVersion is equally "not declared" — no exception.
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, """
        {
          "version": 1,
          "schemaVersion": "   ",
          "status": "success",
          "step": "Step 4",
          "namespace": "keyvault",
          "outputFileCount": 1,
          "warnings": [],
          "errors": [],
          "duration": "00:00:10.000"
        }
        """);

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(StepResultStatus.Success, result!.Status);
    }

    [Fact]
    public async Task ReadAsync_EmptyStringSchemaVersion_TreatedAsLegacy_Succeeds()
    {
        var stepName = "step-empty-schema";
        var stepDir = Path.Combine(_testDir, stepName);
        Directory.CreateDirectory(stepDir);
        File.WriteAllText(Path.Combine(stepDir, "step-result.json"), """
        {
          "version": 1,
          "schemaVersion": "",
          "status": "success",
          "step": "Step 6",
          "namespace": "cosmos",
          "outputFileCount": 2,
          "warnings": [],
          "errors": [],
          "duration": "00:00:20.000"
        }
        """);

        var result = await StepResultReader.ReadAsync(stepName, _testDir);

        Assert.NotNull(result);
        Assert.Equal(StepResultStatus.Success, result.Status);
    }

    [Fact]
    public void TryRead_UnrecognizedSchemaVersion_ThrowsStepResultSchemaException()
    {
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, """
        {
          "version": 1,
          "schemaVersion": "99.0",
          "status": "success",
          "step": "Step 3",
          "namespace": "sql",
          "outputFileCount": 0,
          "warnings": [],
          "errors": [],
          "duration": "00:00:00"
        }
        """);

        var ex = Assert.Throws<StepResultSchemaException>(
            () => StepResultReader.TryRead(_testDir, out _));

        Assert.Equal("99.0", ex.ActualVersion);
        Assert.Contains("99.0", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_ReadsFileSuccessfully()
    {
        var stepName = "step-3";
        var stepDir = Path.Combine(_testDir, stepName);
        Directory.CreateDirectory(stepDir);
        File.WriteAllText(Path.Combine(stepDir, "step-result.json"), """
        {
          "version": 1,
          "schemaVersion": "1.0",
          "status": "success",
          "step": "Step 3 - Tool Generation",
          "namespace": "aks",
          "outputFileCount": 4,
          "warnings": [],
          "errors": [],
          "duration": "00:01:30.000",
          "durationMs": 90000,
          "timestamp": "2026-05-29T09:00:00Z"
        }
        """);

        var result = await StepResultReader.ReadAsync(stepName, _testDir);

        Assert.NotNull(result);
        Assert.Equal(StepResultStatus.Success, result.Status);
        Assert.Equal("1.0", result.SchemaVersion);
        Assert.Equal(90000L, result.DurationMs);
        Assert.Equal("2026-05-29T09:00:00Z", result.Timestamp);
    }

    [Fact]
    public async Task ReadAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => StepResultReader.ReadAsync("missing-step", _testDir));
    }

    [Fact]
    public async Task ReadAsync_UnrecognizedSchemaVersion_ThrowsStepResultSchemaException()
    {
        var stepName = "step-bad";
        var stepDir = Path.Combine(_testDir, stepName);
        Directory.CreateDirectory(stepDir);
        File.WriteAllText(Path.Combine(stepDir, "step-result.json"), """
        {
          "version": 1,
          "schemaVersion": "future-version",
          "status": "success",
          "step": "Step X",
          "namespace": "cosmos",
          "outputFileCount": 0,
          "warnings": [],
          "errors": [],
          "duration": "00:00:00"
        }
        """);

        var ex = await Assert.ThrowsAsync<StepResultSchemaException>(
            () => StepResultReader.ReadAsync(stepName, _testDir));

        Assert.Equal("future-version", ex.ActualVersion);
    }

    [Fact]
    public void TryRead_WithAllNewEnvelopeFields_ReadsCorrectly()
    {
        var filePath = Path.Combine(_testDir, "step-result.json");
        File.WriteAllText(filePath, """
        {
          "version": 1,
          "schemaVersion": "1.0",
          "status": "success",
          "step": "Step 3",
          "stepName": "step-3-tool-generation",
          "namespace": "speech",
          "outputFileCount": 6,
          "warnings": [],
          "errors": [],
          "duration": "00:03:00.000",
          "inputArtifacts": [{ "path": "input/speech.json", "sha256": "aabb" }],
          "outputArtifacts": [{ "path": "output/speech-recognize.md", "sha256": "ccdd" }],
          "validationStatus": "passed",
          "tokenUsageEnvelope": { "promptTokens": 1200, "completionTokens": 600 },
          "promptArchivePath": "archives/step3.zip",
          "durationMs": 180000,
          "timestamp": "2026-05-29T09:35:00Z"
        }
        """);

        var found = StepResultReader.TryRead(_testDir, out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("step-3-tool-generation", result!.StepName);
        Assert.Single(result.InputArtifacts!);
        Assert.Equal("input/speech.json", result.InputArtifacts![0].Path);
        Assert.Equal("aabb", result.InputArtifacts[0].Sha256);
        Assert.Single(result.OutputArtifacts!);
        Assert.Equal(ValidationStatus.Passed, result.ValidationStatus);
        Assert.Equal(1200, result.TokenUsageEnvelope!.PromptTokens);
        Assert.Equal(600, result.TokenUsageEnvelope.CompletionTokens);
        Assert.Equal("archives/step3.zip", result.PromptArchivePath);
        Assert.Equal(180000L, result.DurationMs);
        Assert.Equal("2026-05-29T09:35:00Z", result.Timestamp);
    }
}
