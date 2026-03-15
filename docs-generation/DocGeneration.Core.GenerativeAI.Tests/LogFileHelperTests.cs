using System;
using System.IO;
using Xunit;
using GenerativeAI;
using Shared;

namespace GenerativeAI.Tests;

public class LogFileHelperTests
{
    [Fact]
    public void LoadFromEnvironmentOrDotEnv_WritesDebugToLogFile()
    {
        // Arrange
        var beforeLogPath = LogFileHelper.GetCurrentLogFilePath();
        
        // Act
        var opts = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
        
        // Assert
        var afterLogPath = LogFileHelper.GetCurrentLogFilePath();
        
        // Verify a log file was created
        Assert.NotNull(afterLogPath);
        Assert.True(File.Exists(afterLogPath), $"Log file should exist at: {afterLogPath}");
        
        // Verify log contains expected content
        var logContent = File.ReadAllText(afterLogPath);
        Assert.Contains("C# Environment Variable Loading", logContent);
        Assert.Contains("Current Directory:", logContent);
        Assert.Contains("AppContext.BaseDirectory:", logContent);
    }
    
    // SKIPPED: Brittle test — depends on live resources (.env file with FOUNDRY_* credentials).
    // Fails when .env is missing or credentials are invalid.
    // [Fact]
    // public void LoadFromEnvironmentOrDotEnv_OutputsMinimalConsoleMessage()
    // {
    //     var originalOut = Console.Out;
    //     using var stringWriter = new StringWriter();
    //     Console.SetOut(stringWriter);
    //     try
    //     {
    //         var opts = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
    //         var consoleOutput = stringWriter.ToString();
    //         Assert.DoesNotContain("=== C# Environment Variable Loading ===", consoleOutput);
    //         Assert.DoesNotContain("Current Directory:", consoleOutput);
    //         Assert.DoesNotContain("AppContext.BaseDirectory:", consoleOutput);
    //         Assert.True(
    //             consoleOutput.Contains("✓") || consoleOutput.Contains("⚠"),
    //             "Console should show either success (✓) or warning (⚠) indicator");
    //     }
    //     finally
    //     {
    //         Console.SetOut(originalOut);
    //     }
    // }
}
