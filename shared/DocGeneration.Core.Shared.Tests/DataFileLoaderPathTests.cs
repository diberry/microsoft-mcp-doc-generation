// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Shared;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Tests for DataFileLoader.GetDataDirectoryPath() — Phase 1.5 path hardening.
/// Validates the walk-up algorithm that finds data/ using brand-to-server-mapping.json fingerprint.
/// Tests run from within the repo so real data files are available.
/// </summary>
public class DataFileLoaderPathTests
{
    [Fact]
    public void GetDataDirectoryPath_ReturnsExistingDirectory()
    {
        var dataDir = DataFileLoader.GetDataDirectoryPath();
        Assert.True(Directory.Exists(dataDir), $"Data directory does not exist: {dataDir}");
    }

    [Fact]
    public void GetDataDirectoryPath_ContainsBrandMappingFingerprint()
    {
        // The walk-up algorithm uses this file as a fingerprint
        var dataDir = DataFileLoader.GetDataDirectoryPath();
        var fingerprint = Path.Combine(dataDir, "brand-to-server-mapping.json");
        Assert.True(File.Exists(fingerprint), $"Fingerprint file not found at {fingerprint}");
    }

    [Fact]
    public void GetDataDirectoryPath_ContainsCompoundWordsFile()
    {
        // compound-words.json is loaded by DataFileLoader
        var dataDir = DataFileLoader.GetDataDirectoryPath();
        var filePath = Path.Combine(dataDir, "compound-words.json");
        Assert.True(File.Exists(filePath), $"compound-words.json not found at {filePath}");
    }

    [Fact]
    public void GetDataDirectoryPath_ContainsStopWordsFile()
    {
        var dataDir = DataFileLoader.GetDataDirectoryPath();
        var filePath = Path.Combine(dataDir, "stop-words.json");
        Assert.True(File.Exists(filePath), $"stop-words.json not found at {filePath}");
    }

    [Fact]
    public void GetDataDirectoryPath_ContainsCommonParametersFile()
    {
        var dataDir = DataFileLoader.GetDataDirectoryPath();
        var filePath = Path.Combine(dataDir, "common-parameters.json");
        Assert.True(File.Exists(filePath), $"common-parameters.json not found at {filePath}");
    }

    [Fact]
    public void GetDataDirectoryPath_ResolvesToDataOrMcpToolsData()
    {
        // The path should end with either "data" or "mcp-tools/data" (or "mcp-tools\data")
        var dataDir = DataFileLoader.GetDataDirectoryPath();
        var normalized = dataDir.Replace('\\', '/');
        Assert.True(
            normalized.EndsWith("/data") || normalized.EndsWith("/mcp-tools/data"),
            $"Data directory path should end with /data or /mcp-tools/data, got: {normalized}");
    }

    [Fact]
    public void GetDataDirectoryPath_IsConsistentAcrossMultipleCalls()
    {
        var path1 = DataFileLoader.GetDataDirectoryPath();
        var path2 = DataFileLoader.GetDataDirectoryPath();
        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetDataDirectoryPath_IsAbsolutePath()
    {
        var dataDir = DataFileLoader.GetDataDirectoryPath();
        Assert.True(Path.IsPathRooted(dataDir), $"Expected absolute path, got: {dataDir}");
    }

    [Fact]
    public async Task LoadBrandMappingsAsync_ReturnsNonEmptyDictionary()
    {
        // Validates end-to-end: path resolution → file load → parse
        // Brand mappings include services like Storage, Key Vault, Cosmos DB, etc.
        var mappings = await DataFileLoader.LoadBrandMappingsAsync();
        Assert.NotNull(mappings);
        Assert.True(mappings.Count > 0, "Brand mappings should contain at least one entry");
    }

    [Fact]
    public async Task LoadCompoundWordsAsync_ReturnsNonEmptyDictionary()
    {
        var words = await DataFileLoader.LoadCompoundWordsAsync();
        Assert.NotNull(words);
        Assert.True(words.Count > 0, "Compound words should contain at least one entry");
    }

    [Fact]
    public async Task LoadStopWordsAsync_ReturnsNonEmptySet()
    {
        var stopWords = await DataFileLoader.LoadStopWordsAsync();
        Assert.NotNull(stopWords);
        Assert.True(stopWords.Count > 0, "Stop words should contain at least one entry");
    }

    [Fact]
    public async Task LoadCommonParametersAsync_ReturnsNonEmptyList()
    {
        // Common parameters include tenant, subscription, auth-method, retry-* params
        var parameters = await DataFileLoader.LoadCommonParametersAsync();
        Assert.NotNull(parameters);
        Assert.True(parameters.Count > 0, "Common parameters should contain at least one entry");
    }
}
