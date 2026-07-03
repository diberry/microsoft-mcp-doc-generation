using System.Text.Json;
using Xunit;
using Shared;

namespace Shared.Tests;

public class StepResultFileTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new StepResultFile();

        Assert.Equal(1, result.Version);
        Assert.Equal(StepResultStatus.Failure, result.Status);
        Assert.Equal("", result.Step);
        Assert.Equal("", result.Namespace);
        Assert.Equal(0, result.OutputFileCount);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Errors);
        Assert.Equal("", result.Duration);
    }

    [Fact]
    public void Serialization_RoundTrips_AllFields()
    {
        var original = new StepResultFile
        {
            Version = 1,
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy",
            OutputFileCount = 5,
            Warnings = new List<string> { "warning 1", "warning 2" },
            Errors = new List<string>(),
            Duration = "00:02:15.123"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Version, deserialized!.Version);
        Assert.Equal(original.Status, deserialized.Status);
        Assert.Equal(original.Step, deserialized.Step);
        Assert.Equal(original.Namespace, deserialized.Namespace);
        Assert.Equal(original.OutputFileCount, deserialized.OutputFileCount);
        Assert.Equal(original.Warnings, deserialized.Warnings);
        Assert.Equal(original.Errors, deserialized.Errors);
        Assert.Equal(original.Duration, deserialized.Duration);
    }

    [Theory]
    [InlineData(StepResultStatus.Success, "success")]
    [InlineData(StepResultStatus.Failure, "failure")]
    [InlineData(StepResultStatus.Partial, "partial")]
    public void Status_Serializes_AsLowerCaseString(StepResultStatus status, string expected)
    {
        var result = new StepResultFile { Status = status };
        var json = JsonSerializer.Serialize(result);

        Assert.Contains($"\"{expected}\"", json);
    }

    [Theory]
    [InlineData("success", StepResultStatus.Success)]
    [InlineData("failure", StepResultStatus.Failure)]
    [InlineData("partial", StepResultStatus.Partial)]
    public void Status_Deserializes_FromLowerCaseString(string jsonValue, StepResultStatus expected)
    {
        var json = $$"""{"version":1,"status":"{{jsonValue}}","step":"","namespace":"","outputFileCount":0,"warnings":[],"errors":[],"duration":""}""";
        var result = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(result);
        Assert.Equal(expected, result!.Status);
    }

    [Fact]
    public void Serialization_MatchesExpectedJsonSchema()
    {
        var result = new StepResultFile
        {
            Version = 1,
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy",
            OutputFileCount = 5,
            Warnings = new List<string> { "warning 1" },
            Errors = new List<string> { "error 1" },
            Duration = "00:02:15.123"
        };

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify all expected property names exist with camelCase naming
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("step", out _));
        Assert.True(root.TryGetProperty("namespace", out _));
        Assert.True(root.TryGetProperty("outputFileCount", out _));
        Assert.True(root.TryGetProperty("warnings", out _));
        Assert.True(root.TryGetProperty("errors", out _));
        Assert.True(root.TryGetProperty("duration", out _));
    }

    [Fact]
    public void Deserialization_FromExactSchemaJson()
    {
        // Matches the exact schema from issue #210
        var json = """
        {
          "version": 1,
          "status": "success",
          "step": "Step 3 - Tool Generation",
          "namespace": "deploy",
          "outputFileCount": 5,
          "warnings": ["warning 1"],
          "errors": ["error 1"],
          "duration": "00:02:15.123"
        }
        """;

        var result = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Version);
        Assert.Equal(StepResultStatus.Success, result.Status);
        Assert.Equal("Step 3 - Tool Generation", result.Step);
        Assert.Equal("deploy", result.Namespace);
        Assert.Equal(5, result.OutputFileCount);
        Assert.Single(result.Warnings);
        Assert.Equal("warning 1", result.Warnings[0]);
        Assert.Single(result.Errors);
        Assert.Equal("error 1", result.Errors[0]);
        Assert.Equal("00:02:15.123", result.Duration);
    }

    [Fact]
    public void Partial_Status_WithWarningsAndErrors()
    {
        var result = new StepResultFile
        {
            Status = StepResultStatus.Partial,
            Step = "Step 4 - Tool Family Cleanup",
            Namespace = "storage",
            OutputFileCount = 3,
            Warnings = new List<string> { "File X had issues" },
            Errors = new List<string> { "File Y failed completely" },
            Duration = "00:05:00.000"
        };

        var json = JsonSerializer.Serialize(result);
        var roundTripped = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(StepResultStatus.Partial, roundTripped!.Status);
        Assert.Single(roundTripped.Warnings);
        Assert.Single(roundTripped.Errors);
    }

    // ── Phase 1 Point 3: Envelope extension field tests ──────────────────────

    [Fact]
    public void NewEnvelopeFields_DefaultToNull()
    {
        var result = new StepResultFile();

        Assert.Null(result.SchemaVersion);
        Assert.Null(result.StepName);
        Assert.Null(result.InputArtifacts);
        Assert.Null(result.OutputArtifacts);
        Assert.Null(result.ValidationStatus);
        Assert.Null(result.TokenUsageEnvelope);
        Assert.Null(result.PromptArchivePath);
        Assert.Null(result.DurationMs);
        Assert.Null(result.Timestamp);
    }

    [Theory]
    [InlineData(ValidationStatus.Passed, "passed")]
    [InlineData(ValidationStatus.Failed, "failed")]
    [InlineData(ValidationStatus.Skipped, "skipped")]
    public void ValidationStatus_Serializes_AsLowerCaseString(ValidationStatus status, string expected)
    {
        var result = new StepResultFile { ValidationStatus = status };
        var json = JsonSerializer.Serialize(result);

        Assert.Contains($"\"{expected}\"", json);
    }

    [Theory]
    [InlineData("passed", ValidationStatus.Passed)]
    [InlineData("failed", ValidationStatus.Failed)]
    [InlineData("skipped", ValidationStatus.Skipped)]
    public void ValidationStatus_Deserializes_FromLowerCaseString(string jsonValue, ValidationStatus expected)
    {
        var json = $$"""{"version":1,"status":"success","step":"","namespace":"","outputFileCount":0,"warnings":[],"errors":[],"duration":"","validationStatus":"{{jsonValue}}"}""";
        var result = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(result);
        Assert.Equal(expected, result!.ValidationStatus);
    }

    [Fact]
    public void ArtifactReference_RoundTrips()
    {
        var artifact = new ArtifactReference
        {
            Path = "output/keyvault-list.md",
            Sha256 = "abc123def456"
        };

        var json = JsonSerializer.Serialize(artifact);
        var roundTripped = JsonSerializer.Deserialize<ArtifactReference>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal("output/keyvault-list.md", roundTripped!.Path);
        Assert.Equal("abc123def456", roundTripped.Sha256);
    }

    [Fact]
    public void ArtifactReference_SerializesWithExpectedPropertyNames()
    {
        var artifact = new ArtifactReference { Path = "foo.md", Sha256 = "aabbcc" };
        var json = JsonSerializer.Serialize(artifact);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("path", out _));
        Assert.True(doc.RootElement.TryGetProperty("sha256", out _));
    }

    [Fact]
    public void TokenUsageEnvelope_RoundTrips()
    {
        var envelope = new TokenUsageEnvelope { PromptTokens = 800, CompletionTokens = 400 };

        var json = JsonSerializer.Serialize(envelope);
        var roundTripped = JsonSerializer.Deserialize<TokenUsageEnvelope>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(800, roundTripped!.PromptTokens);
        Assert.Equal(400, roundTripped.CompletionTokens);
    }

    [Fact]
    public void AllNineNewFields_SerializeAndDeserialize_Correctly()
    {
        var original = new StepResultFile
        {
            SchemaVersion = "1.0",
            StepName = "step-3-tool-generation",
            InputArtifacts = new List<ArtifactReference>
            {
                new() { Path = "input/cosmos-data.json", Sha256 = "deadbeef" }
            },
            OutputArtifacts = new List<ArtifactReference>
            {
                new() { Path = "output/cosmos-create.md", Sha256 = "cafebabe" }
            },
            ValidationStatus = ValidationStatus.Passed,
            TokenUsageEnvelope = new TokenUsageEnvelope { PromptTokens = 1000, CompletionTokens = 500 },
            PromptArchivePath = "archives/step-3-prompts.zip",
            DurationMs = 135_000L,
            Timestamp = "2026-05-29T09:35:22Z"
        };

        var json = JsonSerializer.Serialize(original);
        var result = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(result);
        Assert.Equal("1.0", result!.SchemaVersion);
        Assert.Equal("step-3-tool-generation", result.StepName);
        Assert.NotNull(result.InputArtifacts);
        Assert.Single(result.InputArtifacts!);
        Assert.Equal("input/cosmos-data.json", result.InputArtifacts[0].Path);
        Assert.Equal("deadbeef", result.InputArtifacts[0].Sha256);
        Assert.NotNull(result.OutputArtifacts);
        Assert.Single(result.OutputArtifacts!);
        Assert.Equal("output/cosmos-create.md", result.OutputArtifacts[0].Path);
        Assert.Equal(ValidationStatus.Passed, result.ValidationStatus);
        Assert.NotNull(result.TokenUsageEnvelope);
        Assert.Equal(1000, result.TokenUsageEnvelope!.PromptTokens);
        Assert.Equal(500, result.TokenUsageEnvelope.CompletionTokens);
        Assert.Equal("archives/step-3-prompts.zip", result.PromptArchivePath);
        Assert.Equal(135_000L, result.DurationMs);
        Assert.Equal("2026-05-29T09:35:22Z", result.Timestamp);
    }

    [Fact]
    public void LegacyJson_WithoutNewEnvelopeFields_DeserializesSuccessfully()
    {
        // Simulates an existing step-result.json written before Phase 1 Point 3 changes
        var legacyJson = """
        {
          "version": 1,
          "status": "success",
          "step": "Step 1 - Annotations",
          "namespace": "storage",
          "outputFileCount": 3,
          "warnings": [],
          "errors": [],
          "duration": "00:01:00.000"
        }
        """;

        var result = JsonSerializer.Deserialize<StepResultFile>(legacyJson);

        Assert.NotNull(result);
        Assert.Equal(StepResultStatus.Success, result!.Status);
        // All new fields should be null — no exception, full backward compatibility
        Assert.Null(result.SchemaVersion);
        Assert.Null(result.StepName);
        Assert.Null(result.InputArtifacts);
        Assert.Null(result.OutputArtifacts);
        Assert.Null(result.ValidationStatus);
        Assert.Null(result.TokenUsageEnvelope);
        Assert.Null(result.PromptArchivePath);
        Assert.Null(result.DurationMs);
        Assert.Null(result.Timestamp);
    }

    [Fact]
    public void NullEnvelopeFields_SerializeAsJsonNull()
    {
        var result = new StepResultFile { Status = StepResultStatus.Success };
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(result, options);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // New nullable fields present as null, not absent
        Assert.True(root.TryGetProperty("schemaVersion", out var sv));
        Assert.Equal(JsonValueKind.Null, sv.ValueKind);
        Assert.True(root.TryGetProperty("durationMs", out var dm));
        Assert.Equal(JsonValueKind.Null, dm.ValueKind);
        Assert.True(root.TryGetProperty("timestamp", out var ts));
        Assert.Equal(JsonValueKind.Null, ts.ValueKind);
    }
}
