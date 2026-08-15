// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

/// <summary>
/// Group 2 — Program.cs canonical seam binding tests.
/// Proves that LoadParameterManifestAsync returns CanonicalParameterManifest? (not
/// List&lt;ParameterManifestParameter&gt;?) so downstream code uses the v2 type.
/// </summary>
public class CanonicalManifestBindingTests : IDisposable
{
    private readonly string _tempDir;

    public CanonicalManifestBindingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"canonical-binding-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LoadParameterManifestAsync_WithV2Json_ReturnsCanonicalManifest()
    {
        // Arrange: write a valid v2 manifest to disk
        var nameContext = new FileNameContext(
            new Dictionary<string, BrandMapping>(),
            new Dictionary<string, string>(),
            new HashSet<string>());

        var v2Json = """
        {
          "schemaVersion": "2.0",
          "toolCommand": "storage account list",
          "namespace": "storage",
          "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
          "parameters": [
            {
              "canonicalName": "subscription",
              "displayName": "Subscription",
              "displayAliases": ["subscription"],
              "placeholderAliases": ["subscription", "subscription_id"],
              "required": true,
              "requiredText": "Required",
              "isConditionalRequired": false,
              "description": "Subscription ID."
            }
          ],
          "placeholderAliasIndex": { "subscription": "subscription", "subscription_id": "subscription" }
        }
        """;

        var manifestFileName = ToolFileNameBuilder.BuildParameterManifestFileName("storage account list", nameContext);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, manifestFileName), v2Json);

        // Act: call the migrated loader
        var result = await Program.LoadParameterManifestAsync("storage account list", _tempDir, nameContext);

        // Assert: result is CanonicalParameterManifest (not legacy DTO)
        Assert.NotNull(result);
        Assert.IsType<CanonicalParameterManifest>(result);
        Assert.Equal("2.0", result!.SchemaVersion);
        Assert.Equal("storage account list", result.ToolCommand);
        Assert.Single(result.Parameters);
        Assert.Equal("subscription", result.Parameters[0].CanonicalName);
        Assert.Equal("Subscription", result.Parameters[0].DisplayName);
        Assert.True(result.Parameters[0].Required);
    }

    [Fact]
    public async Task LoadParameterManifestAsync_WhenFileMissing_ReturnsNull()
    {
        var nameContext = new FileNameContext(
            new Dictionary<string, BrandMapping>(),
            new Dictionary<string, string>(),
            new HashSet<string>());

        var result = await Program.LoadParameterManifestAsync("storage account list", _tempDir, nameContext);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadParameterManifestAsync_WithLegacyArrayJson_ThrowsParameterManifestException()
    {
        // After migration: if the file exists but is in legacy format, it must throw
        // (fail-closed), not silently return null.
        var nameContext = new FileNameContext(
            new Dictionary<string, BrandMapping>(),
            new Dictionary<string, string>(),
            new HashSet<string>());

        var legacyJson = """[{"name":"--subscription","displayName":"Subscription","required":true,"requiredText":"Required","isConditionalRequired":false,"description":"Subscription ID."}]""";
        var manifestFileName = ToolFileNameBuilder.BuildParameterManifestFileName("storage account list", nameContext);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, manifestFileName), legacyJson);

        await Assert.ThrowsAsync<ParameterManifestException>(
            () => Program.LoadParameterManifestAsync("storage account list", _tempDir, nameContext));
    }
}
