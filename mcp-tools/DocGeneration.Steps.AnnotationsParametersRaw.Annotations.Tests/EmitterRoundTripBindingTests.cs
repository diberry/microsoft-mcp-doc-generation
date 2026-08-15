// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using CSharpGenerator.Generators;
using CSharpGenerator.Models;
using Shared;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Group 1 — Emitter round-trip binding tests.
/// Proves that the JSON written by GenerateParameterFilesAsync is v2 envelope
/// (loadable by CanonicalParameterManifestLoader), not a legacy bare array.
/// </summary>
[Collection("StaticState")]
public class EmitterRoundTripBindingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _templateFile;

    public EmitterRoundTripBindingTests(TransformationEngineFixture _)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"emitter-roundtrip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _templateFile = Path.Combine(_tempDir, "parameter-template.hbs");
        File.WriteAllText(_templateFile, "{{command}}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Integration: calls the REAL GenerateParameterFilesAsync production method,
    /// then loads the emitted -params.json with CanonicalParameterManifestLoader.
    /// FAILS if the emitter regresses to serializing List&lt;ParameterManifestEntry&gt;
    /// because the loader rejects bare arrays with PARAM_MANIFEST_LEGACY_FORMAT.
    /// </summary>
    [Fact]
    public async Task GenerateParameterFilesAsync_EmitsV2ManifestLoadableByCanonicalLoader()
    {
        var data = new TransformedData
        {
            Version = "1.0.0-test",
            GeneratedAt = DateTime.UtcNow,
            SourceDiscoveredCommonParams = new List<CommonParameter>(),
            Tools = new List<Tool>
            {
                new()
                {
                    Command = "azmcp storage account list",
                    Area = "storage",
                    Option = new List<Option>
                    {
                        new() { Name = "--resource-group", Required = true, Description = "The resource group name." },
                        new() { Name = "--subscription", Required = false, Description = "The subscription ID." }
                    }
                }
            }
        };

        var outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(outputDir);

        var generator = new ParameterGenerator();
        await generator.GenerateParameterFilesAsync(data, outputDir, _templateFile);

        var manifestFiles = Directory.GetFiles(outputDir, "*-params.json");
        Assert.Single(manifestFiles);
        var manifestPath = manifestFiles[0];

        // Load via strict canonical loader — fails if legacy bare array
        var loaded = CanonicalParameterManifestLoader.Load(
            manifestPath, "azmcp storage account list",
            expectedNamespace: "storage", currentAzureMcpBuild: "1.0.0-test");

        Assert.Equal("2.0", loaded.SchemaVersion);
        Assert.Equal("azmcp storage account list", loaded.ToolCommand);
        Assert.Equal("storage", loaded.Namespace);
        Assert.Equal("1.0.0-test", loaded.SourceIdentity.AzureMcpBuild);
        Assert.Equal(2, loaded.Parameters.Count);
        Assert.Contains(loaded.Parameters, p => p.CanonicalName == "resource-group" && p.Required);
        Assert.Contains(loaded.Parameters, p => p.CanonicalName == "subscription" && !p.Required);
    }

    /// <summary>
    /// Guard: legacy bare-array JSON is always rejected by the canonical loader.
    /// </summary>
    [Fact]
    public void LegacyArray_SerializedToFile_ThrowsLegacyFormatException()
    {
        var json = """[{"name":"--account","displayName":"Account name","required":true,"requiredText":"Required","isConditionalRequired":false,"description":"The account."}]""";
        var path = Path.Combine(_tempDir, "legacy-params.json");
        File.WriteAllText(path, json);

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp test tool"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_LEGACY_FORMAT, ex.ErrorCode);
    }

}
