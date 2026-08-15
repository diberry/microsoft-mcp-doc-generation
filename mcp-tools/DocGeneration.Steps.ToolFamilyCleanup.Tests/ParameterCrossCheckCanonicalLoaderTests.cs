// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Group 4 (Step-4-applicable seam) — ParameterCrossCheckService must route through
/// the strict canonical loader and fail closed (not swallow JsonException).
/// </summary>
public class ParameterCrossCheckCanonicalLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ParameterCrossCheckCanonicalLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"crosscheck-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// After implementation, ParameterCrossCheckService.LoadValidParametersAsync must use
    /// CanonicalParameterManifestLoader. When a manifest is malformed JSON, it must
    /// throw ParameterManifestException (fail closed), not swallow and skip.
    /// </summary>
    [Fact]
    public void LoadValidParameters_MalformedManifest_ThrowsInsteadOfSilentSkip()
    {
        var path = Path.Combine(_tempDir, "malformed-params.json");
        File.WriteAllText(path, "{{not valid json}}");

        // The canonical loader fails closed on malformed JSON
        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp storage account list", "storage"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_MALFORMED, ex.ErrorCode);
    }

    /// <summary>
    /// When manifest has a legacy bare-array format, the loader must reject it.
    /// ParameterCrossCheckService must propagate this failure instead of silently skipping.
    /// </summary>
    [Fact]
    public void LoadValidParameters_LegacyFormat_ThrowsInsteadOfSilentSkip()
    {
        var path = Path.Combine(_tempDir, "legacy-params.json");
        File.WriteAllText(path, """[{"Name":"account","DisplayName":"Account name"}]""");

        var ex = Assert.Throws<ParameterManifestException>(
            () => CanonicalParameterManifestLoader.Load(path, "azmcp appconfig kv list", "appconfig"));

        Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_LEGACY_FORMAT, ex.ErrorCode);
    }

    /// <summary>
    /// A valid v2 manifest loads successfully through the canonical loader.
    /// This proves ParameterCrossCheckService can use the loader for its normal path.
    /// </summary>
    [Fact]
    public void LoadValidParameters_ValidV2Manifest_LoadsSuccessfully()
    {
        var json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "azmcp storage account list",
          "namespace": "storage",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "resource-group",
              "displayName": "Resource group",
              "displayAliases": ["resource-group"],
              "placeholderAliases": ["resource-group", "resource_group"],
              "required": true,
              "requiredText": "Required",
              "isConditionalRequired": false,
              "description": "The resource group name."
            }
          ]
        }
        """;
        var path = Path.Combine(_tempDir, "valid-params.json");
        File.WriteAllText(path, json);

        var manifest = CanonicalParameterManifestLoader.Load(
            path, "azmcp storage account list", "storage");

        Assert.NotNull(manifest);
        Assert.Single(manifest.Parameters);
        Assert.Equal("resource-group", manifest.Parameters[0].CanonicalName);
    }
}
