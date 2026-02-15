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
    
    [Fact]
    public void LoadFromEnvironmentOrDotEnv_OutputsMinimalConsoleMessage()
    {
        // Arrange - capture console output
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        
        try
        {
            // Act
            var opts = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
            
            // Assert - console output should be minimal
            var consoleOutput = stringWriter.ToString();
            
            // Should NOT contain verbose debug headers
            Assert.DoesNotContain("=== C# Environment Variable Loading ===", consoleOutput);
            Assert.DoesNotContain("Current Directory:", consoleOutput);
            Assert.DoesNotContain("AppContext.BaseDirectory:", consoleOutput);
            
            // Should only contain success message if credentials loaded
            // (This may or may not be present depending on environment)
            if (consoleOutput.Trim().Length > 0)
            {
                Assert.Contains("✓", consoleOutput);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
