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
}
