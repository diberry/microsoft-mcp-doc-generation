// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public sealed class CliVersionReaderTests : IDisposable
{
    private readonly string _testRoot;

    public CliVersionReaderTests()
    {
        _testRoot = Path.Combine(AppContext.BaseDirectory, "cli-version-reader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_testRoot, "cli"));
    }

    [Fact]
    public async Task ReadCliVersionAsync_WithRawControlCharInJson_ParsesVersion()
    {
        // Raw (unescaped) 0x1A written as file bytes — reproduces the upstream
        // control-char crash that JsonDocument.Parse would otherwise throw on.
        var rawJson = "{\"version\":\"3.0.0-beta.23\",\"note\":\"x\u001Ay\"}";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "cli", "cli-version.json"), rawJson);

        var version = await CliVersionReader.ReadCliVersionAsync(_testRoot);

        Assert.Equal("3.0.0-beta.23", version);
    }

    [Fact]
    public async Task ReadCliVersionAsync_WithCleanJson_ParsesVersion()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_testRoot, "cli", "cli-version.json"),
            "{\"version\":\"1.2.3\"}");

        var version = await CliVersionReader.ReadCliVersionAsync(_testRoot);

        Assert.Equal("1.2.3", version);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
