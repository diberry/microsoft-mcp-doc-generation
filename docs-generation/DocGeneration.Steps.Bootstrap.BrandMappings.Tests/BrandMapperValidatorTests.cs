// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BrandMapperValidator.Tests;

public class BrandMapperValidatorTests
{
    [Fact]
    public async Task ValidatorExits_WithSuccess_WhenAllNamespacesAreMapped()
    {
        // Arrange
        using var tempDir = TestHelpers.CreateTempDir();
        var cliOutputPath = TestHelpers.GetTestDataFile("cli-output-test.json");
        var brandMappingPath = TestHelpers.GetTestDataFile("brand-mapping-test.json");
        var outputPath = Path.Combine(tempDir.Path, "results.json");

        // Create CLI output with only mapped namespaces
        var cliOutput = new
        {
            status = 0,
            results = new[]
            {
                new { name = "advisor-list", command = "advisor list", description = "List advisors" },
                new { name = "storage-list", command = "storage list", description = "List storage" }
            }
        };
        var tempCliPath = Path.Combine(tempDir.Path, "cli-mapped.json");
        await File.WriteAllTextAsync(tempCliPath, JsonSerializer.Serialize(cliOutput));

        // Act
        var exitCode = await RunValidator(tempCliPath, brandMappingPath, outputPath);

        // Assert
        Assert.Equal(0, exitCode); // Success - all namespaces mapped
        Assert.True(File.Exists(outputPath), "Output file should be created");

        var output = await File.ReadAllTextAsync(outputPath);
        var result = JsonSerializer.Deserialize<JsonElement>(output);
        Assert.Equal(0, result.GetProperty("newMappingsNeeded").GetInt32());
    }

    [Fact]
    public async Task ValidatorExits_WithCode2_WhenNewNamespacesAreFound()
    {
        // Arrange
        using var tempDir = TestHelpers.CreateTempDir();
        var cliOutputPath = TestHelpers.GetTestDataFile("cli-output-test.json");
        var brandMappingPath = TestHelpers.GetTestDataFile("brand-mapping-test.json");
        var outputPath = Path.Combine(tempDir.Path, "results.json");

        // Act - using test data which includes "newservice" namespace
        var exitCode = await RunValidator(cliOutputPath, brandMappingPath, outputPath);

        // Assert
        Assert.Equal(2, exitCode); // Exit code 2 = new mappings need review
        Assert.True(File.Exists(outputPath), "Output file should be created");

        var output = await File.ReadAllTextAsync(outputPath);
        var result = JsonSerializer.Deserialize<JsonElement>(output);
        
        // Should have suggestions for "newservice"
        var newMappingsNeeded = result.GetProperty("newMappingsNeeded").GetInt32();
        Assert.True(newMappingsNeeded > 0, "Should have at least one new mapping suggestion");
        
        var suggestions = result.GetProperty("suggestions").EnumerateArray().ToList();
        Assert.NotEmpty(suggestions);
        
        // Check that newservice is in suggestions
        var hasNewService = suggestions.Any(s => 
            s.GetProperty("mcpServerName").GetString() == "newservice");
        Assert.True(hasNewService, "Should have suggestion for 'newservice' namespace");
    }

    [Fact]
    public async Task ValidatorExits_WithError_WhenCliFileNotFound()
    {
        // Arrange
        using var tempDir = TestHelpers.CreateTempDir();
        var cliOutputPath = Path.Combine(tempDir.Path, "nonexistent.json");
        var brandMappingPath = TestHelpers.GetTestDataFile("brand-mapping-test.json");
        var outputPath = Path.Combine(tempDir.Path, "results.json");

        // Act
        var exitCode = await RunValidator(cliOutputPath, brandMappingPath, outputPath);

        // Assert
        Assert.Equal(1, exitCode); // Error exit code
    }

    [Fact]
    public async Task ValidatorExits_WithError_WhenCliOutputIsEmpty()
    {
        // Arrange
        using var tempDir = TestHelpers.CreateTempDir();
        var cliOutputPath = Path.Combine(tempDir.Path, "empty-cli.json");
        var brandMappingPath = TestHelpers.GetTestDataFile("brand-mapping-test.json");
        var outputPath = Path.Combine(tempDir.Path, "results.json");

        // Create empty CLI output
        var emptyOutput = new { status = 0, results = new object[0] };
        await File.WriteAllTextAsync(cliOutputPath, JsonSerializer.Serialize(emptyOutput));

        // Act
        var exitCode = await RunValidator(cliOutputPath, brandMappingPath, outputPath);

        // Assert
        Assert.Equal(1, exitCode); // Error - no tools
    }

    [Fact]
    public async Task Validator_CreatesOutputDirectory_WhenNotExists()
    {
        // Arrange
        using var tempDir = TestHelpers.CreateTempDir();
        var cliOutputPath = TestHelpers.GetTestDataFile("cli-output-test.json");
        var brandMappingPath = TestHelpers.GetTestDataFile("brand-mapping-test.json");
        var outputPath = Path.Combine(tempDir.Path, "nested", "dir", "results.json");

        // Act
        var exitCode = await RunValidator(cliOutputPath, brandMappingPath, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath), "Output file should be created even in nested directory");
    }

    [Fact]
    public async Task Validator_ExtractsNamespaces_FromCommandPrefix()
    {
        // Arrange
        using var tempDir = TestHelpers.CreateTempDir();
        var brandMappingPath = TestHelpers.GetTestDataFile("brand-mapping-test.json");
        var outputPath = Path.Combine(tempDir.Path, "results.json");

        // Create CLI output with various command formats
        var cliOutput = new
        {
            status = 0,
            results = new[]
            {
                new { name = "test1", command = "namespace1 action", description = "Test" },
                new { name = "test2", command = "namespace1 other action", description = "Test" },
                new { name = "test3", command = "namespace2 action", description = "Test" }
            }
        };
        var cliPath = Path.Combine(tempDir.Path, "cli.json");
        await File.WriteAllTextAsync(cliPath, JsonSerializer.Serialize(cliOutput));

        // Act
        var exitCode = await RunValidator(cliPath, brandMappingPath, outputPath);

        // Assert
        Assert.Equal(2, exitCode); // Should find unmapped namespaces
        
        var output = await File.ReadAllTextAsync(outputPath);
        var result = JsonSerializer.Deserialize<JsonElement>(output);
        Assert.Equal(2, result.GetProperty("totalNamespaces").GetInt32()); // namespace1, namespace2
    }

    /// <summary>
    /// Runs the BrandMapperValidator with specified arguments.
    /// </summary>
    private static async Task<int> RunValidator(string cliOutputPath, string brandMappingPath, string outputPath)
    {
        // Since we can't directly call Program.Main from another assembly (it's internal),
        // we need to run the validator as a separate process
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "BrandMapperValidator.dll");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{assemblyPath}\" --cli-output \"{cliOutputPath}\" --brand-mapping \"{brandMappingPath}\" --output \"{outputPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start BrandMapperValidator process");
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
