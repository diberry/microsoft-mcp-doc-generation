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
}
