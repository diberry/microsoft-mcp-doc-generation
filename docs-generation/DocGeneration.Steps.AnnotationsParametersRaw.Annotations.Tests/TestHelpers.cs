// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using CSharpGenerator.Models;

namespace CSharpGenerator.Tests;

/// <summary>
/// Shared helpers for loading test fixtures and building test objects.
/// </summary>
internal static class TestHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Resolves a path relative to the TestData directory.
    /// </summary>
    internal static string TestDataPath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);

    /// <summary>
    /// Loads and deserializes the test CLI output fixture.
    /// </summary>
    internal static CliOutput LoadCliOutput(string fileName = "cli-output-synthetic.json")
    {
        var json = File.ReadAllText(TestDataPath(fileName));
        return JsonSerializer.Deserialize<CliOutput>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize {fileName}");
    }

    /// <summary>
    /// Creates a minimal Tool for unit tests.
    /// </summary>
    internal static Tool CreateTool(
        string command,
        string? description = null,
        List<Option>? options = null,
        ToolMetadata? metadata = null)
    {
        return new Tool
        {
            Name = $"azmcp {command}",
            Command = command,
            Description = description ?? $"Test tool for {command}.",
            Option = options ?? new List<Option>(),
            Metadata = metadata
        };
    }

    /// <summary>
    /// Creates an Option (parameter) for unit tests.
    /// </summary>
    internal static Option CreateOption(
        string name,
        bool required = false,
        string type = "string",
        string? description = null)
    {
        return new Option
        {
            Name = name,
            Type = type,
            Required = required,
            Description = description ?? $"The {name.TrimStart('-')} value."
        };
    }

    /// <summary>
    /// Creates a CliOutput wrapping the given tools.
    /// </summary>
    internal static CliOutput CreateCliOutput(params Tool[] tools)
    {
        return new CliOutput { Results = tools.ToList() };
    }

    /// <summary>
    /// Creates a temporary directory that is cleaned up on dispose.
    /// </summary>
    internal static TempDir CreateTempDir() => new();

    internal sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "CSharpGeneratorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
