// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests Config.Load — validates config file loading, path resolution, and error handling.
/// Priority: P1 — Config.Load must succeed before any generation runs.
/// </summary>
[Collection("StaticState")]
public class ConfigTests
{
    [Fact]
    public void Load_ValidConfig_ReturnsTrue()
    {
        var configPath = TestHelpers.TestDataPath("config.json");
        var result = Config.Load(configPath);

        Assert.True(result);
    }

    [Fact]
    public void Load_ValidConfig_SetsNLParametersPath()
    {
        var configPath = TestHelpers.TestDataPath("config.json");
        Config.Load(configPath);

        Assert.NotNull(Config.NLParametersPath);
        Assert.Contains("nl-parameters.json", Config.NLParametersPath);
    }

    [Fact]
    public void Load_ValidConfig_SetsTextReplacerPath()
    {
        var configPath = TestHelpers.TestDataPath("config.json");
        Config.Load(configPath);

        Assert.NotNull(Config.TextReplacerParametersPath);
        Assert.Contains("static-text-replacement.json", Config.TextReplacerParametersPath);
    }

    [Fact]
    public void Load_MissingConfigFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(
            () => Config.Load("/nonexistent/config.json"));
    }

    [Fact]
    public void Load_EmptyConfig_ThrowsInvalidDataException()
    {
        using var tmpDir = TestHelpers.CreateTempDir();
        var configPath = Path.Combine(tmpDir.Path, "config.json");
        File.WriteAllText(configPath, "{}");

        Assert.Throws<InvalidDataException>(() => Config.Load(configPath));
    }

    [Fact]
    public void Load_MissingRequiredFile_ThrowsFileNotFoundException()
    {
        using var tmpDir = TestHelpers.CreateTempDir();
        var configPath = Path.Combine(tmpDir.Path, "config.json");
        File.WriteAllText(configPath, """{"RequiredFiles": ["nonexistent.json"]}""");

        Assert.Throws<FileNotFoundException>(() => Config.Load(configPath));
    }

    [Fact]
    public void Load_ResolvesPaths_AsAbsolute()
    {
        var configPath = TestHelpers.TestDataPath("config.json");
        Config.Load(configPath);

        Assert.True(Path.IsPathRooted(Config.NLParametersPath!));
        Assert.True(Path.IsPathRooted(Config.TextReplacerParametersPath!));
    }
}
