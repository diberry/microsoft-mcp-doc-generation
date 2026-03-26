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
}
