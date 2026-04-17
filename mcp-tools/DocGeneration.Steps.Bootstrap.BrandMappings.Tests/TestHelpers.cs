// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BrandMapperValidator.Tests;

public static class TestHelpers
{
    /// <summary>
    /// Gets the path to the TestData directory.
    /// </summary>
    public static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");

    /// <summary>
    /// Creates a temporary directory for test output.
    /// </summary>
    public static TempDir CreateTempDir() => new TempDir();

    /// <summary>
    /// Gets a test data file path.
    /// </summary>
    public static string GetTestDataFile(string filename) => Path.Combine(TestDataPath, filename);
}

/// <summary>
/// Creates a temporary directory that is automatically cleaned up when disposed.
/// </summary>
public class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BrandMapperTests_{Guid.NewGuid():N}");
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
