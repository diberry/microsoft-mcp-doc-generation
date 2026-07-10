// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Regression tests proving <see cref="CliMetadataLoader"/> tolerates raw
/// (unescaped) control characters (e.g. 0x1A SUB) that upstream Azure MCP CLI
/// metadata can emit inside JSON string values. Written as raw file bytes so
/// the JSON parser would otherwise throw '0x1A is invalid within a JSON string'.
/// </summary>
public sealed class CliMetadataLoaderControlCharTests : IDisposable
{
    private readonly string _outputPath;

    public CliMetadataLoaderControlCharTests()
    {
        _outputPath = Path.Combine(AppContext.BaseDirectory, "cli-metadata-loader-ctrl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_outputPath, "cli"));
    }

    [Fact]
    public async Task LoadCliOutputAsync_WithRawControlChar_DoesNotThrow()
    {
        var rawJson =
            "{\"results\":[{\"command\":\"keyvault secret get\",\"name\":\"get\"," +
            "\"description\":\"Get a\u001A secret.\"}]}";
        await File.WriteAllTextAsync(Path.Combine(_outputPath, "cli", "cli-output.json"), rawJson);

        var loader = new CliMetadataLoader();
        var snapshot = await loader.LoadCliOutputAsync(_outputPath, CancellationToken.None);

        Assert.Single(snapshot.Tools);
        Assert.Equal("keyvault secret get", snapshot.Tools[0].Command);
    }

    [Fact]
    public async Task LoadNamespacesAsync_WithRawControlChar_DoesNotThrow()
    {
        var rawJson =
            "{\"results\":[{\"name\":\"cosmos\",\"description\":\"Cosmos\u001A DB.\"}," +
            "{\"name\":\"storage\",\"description\":\"Storage.\"}]}";
        await File.WriteAllTextAsync(Path.Combine(_outputPath, "cli", "cli-namespace.json"), rawJson);

        var loader = new CliMetadataLoader();
        var namespaces = await loader.LoadNamespacesAsync(_outputPath, CancellationToken.None);

        Assert.Equal(["cosmos", "storage"], namespaces);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }
    }
}
