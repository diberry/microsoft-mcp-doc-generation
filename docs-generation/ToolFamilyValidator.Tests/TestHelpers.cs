// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace ToolFamilyValidator.Tests;

public static class TestHelpers
{
    public static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");

    public static TempDir CreateTempDir() => new();

    public static string GetFixturePath(string fixtureName) => Path.Combine(TestDataPath, fixtureName);

    public static void CopyFixtureToTemp(string fixtureName, string destinationPath)
    {
        var sourcePath = GetFixturePath(fixtureName);
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Fixture directory not found: {sourcePath}");
        }

        CopyDirectory(sourcePath, destinationPath);
    }

    public static async Task<ValidatorRunResult> RunValidatorAsync(string namespaceName, string outputPath)
    {
        var scriptPath = GetValidatorScriptPath();
        var reportPath = Path.Combine(outputPath, "reports", $"tool-family-validation-{namespaceName.ToLowerInvariant()}.txt");

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-Namespace");
        startInfo.ArgumentList.Add(namespaceName);
        startInfo.ArgumentList.Add("-OutputPath");
        startInfo.ArgumentList.Add(outputPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start tool family validator process");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(standardOutputTask, standardErrorTask, process.WaitForExitAsync());

        var reportContent = File.Exists(reportPath)
            ? await File.ReadAllTextAsync(reportPath)
            : null;

        return new ValidatorRunResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask,
            reportPath,
            reportContent);
    }

    private static string GetValidatorScriptPath()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "scripts",
            "Validate-ToolFamily-PostAssembly.ps1"));

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Validator script not found.", scriptPath);
        }

        return scriptPath;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var filePath in Directory.GetFiles(sourceDir))
        {
            var destinationFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            File.Copy(filePath, destinationFilePath, overwrite: true);
        }

        foreach (var directoryPath in Directory.GetDirectories(sourceDir))
        {
            var destinationSubdirectory = Path.Combine(destinationDir, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, destinationSubdirectory);
        }
    }
}

public sealed record ValidatorRunResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string ReportPath,
    string? ReportContent)
{
    public string CombinedOutput => string.Join(
        Environment.NewLine,
        new[] { StandardOutput, StandardError, ReportContent ?? string.Empty });
}

public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ToolFamilyValidatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
