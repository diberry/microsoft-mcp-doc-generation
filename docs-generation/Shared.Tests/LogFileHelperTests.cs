using Xunit;
using Shared;

namespace Shared.Tests;

public class LogFileHelperTests : IDisposable
{
    public LogFileHelperTests()
    {
        LogFileHelper.Reset();
    }

    public void Dispose()
    {
        LogFileHelper.Reset();
    }

    [Fact]
    public void BuildLogFilename_WithoutInitialize_ReturnsDebug()
    {
        Assert.Equal("debug.log", LogFileHelper.BuildLogFilename());
    }

    [Theory]
    [InlineData("example-prompts", "example-prompts.log")]
    [InlineData("annotations-generator", "annotations-generator.log")]
    [InlineData("parameters-generator", "parameters-generator.log")]
    [InlineData("raw-tool-generator", "raw-tool-generator.log")]
    [InlineData("composed-tool-generator", "composed-tool-generator.log")]
    [InlineData("horizontal-article-generator", "horizontal-article-generator.log")]
    [InlineData("brand-mapper-validator", "brand-mapper-validator.log")]
    public void BuildLogFilename_WithProcessName_ReturnsProcessLog(string processName, string expected)
    {
        LogFileHelper.Initialize(processName);
        Assert.Equal(expected, LogFileHelper.BuildLogFilename());
    }

    [Fact]
    public void Initialize_ResetsLogFilePath()
    {
        // Create a log file with default name
        LogFileHelper.WriteDebug("first message");
        var firstPath = LogFileHelper.GetCurrentLogFilePath();

        // Reinitialize with a process name
        LogFileHelper.Initialize("new-process");
        LogFileHelper.WriteDebug("second message");
        var secondPath = LogFileHelper.GetCurrentLogFilePath();

        Assert.NotNull(firstPath);
        Assert.NotNull(secondPath);
        Assert.NotEqual(firstPath, secondPath);
        Assert.Equal("new-process.log", Path.GetFileName(secondPath!));
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        LogFileHelper.Initialize("some-process");
        LogFileHelper.WriteDebug("test");
        Assert.NotNull(LogFileHelper.GetCurrentLogFilePath());

        LogFileHelper.Reset();

        Assert.Null(LogFileHelper.GetCurrentLogFilePath());
    }

    [Fact]
    public void WriteDebug_CreatesLogFileOnDisk()
    {
        LogFileHelper.Initialize("test-process");

        LogFileHelper.WriteDebug("hello world");
        var logPath = LogFileHelper.GetCurrentLogFilePath();

        Assert.NotNull(logPath);
        Assert.True(File.Exists(logPath), $"Log file should exist at: {logPath}");
        var content = File.ReadAllText(logPath);
        Assert.Contains("hello world", content);
    }

    [Fact]
    public void WriteDebug_IncludesTimestamp()
    {
        LogFileHelper.Initialize("ts-test");

        LogFileHelper.WriteDebug("timestamped entry");
        var logPath = LogFileHelper.GetCurrentLogFilePath();

        Assert.NotNull(logPath);
        var content = File.ReadAllText(logPath);
        // Timestamp format: [2026-02-22 12:00:00.000]
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]", content);
    }

    [Fact]
    public void WriteDebugLines_WritesAllMessages()
    {
        LogFileHelper.Initialize("batch-test");

        LogFileHelper.WriteDebugLines(new[] { "line1", "line2", "line3" });
        var logPath = LogFileHelper.GetCurrentLogFilePath();

        Assert.NotNull(logPath);
        var content = File.ReadAllText(logPath);
        Assert.Contains("line1", content);
        Assert.Contains("line2", content);
        Assert.Contains("line3", content);
    }

    [Fact]
    public void WriteDebug_StartsFreshOnNewProcess()
    {
        LogFileHelper.Initialize("fresh-test");
        LogFileHelper.WriteDebug("first run");
        var logPath = LogFileHelper.GetCurrentLogFilePath();

        Assert.NotNull(logPath);
        // Simulate a new process by reinitializing with same name
        LogFileHelper.Initialize("fresh-test");
        LogFileHelper.WriteDebug("second run");

        var content = File.ReadAllText(logPath!);
        Assert.Contains("second run", content);
        Assert.DoesNotContain("first run", content);
    }

    [Fact]
    public void LogFilePath_UsesProcessNameAsFilename()
    {
        LogFileHelper.Initialize("annotations-generator");
        LogFileHelper.WriteDebug("test");
        var logPath = LogFileHelper.GetCurrentLogFilePath();

        Assert.NotNull(logPath);
        Assert.Equal("annotations-generator.log", Path.GetFileName(logPath!));
    }
}
