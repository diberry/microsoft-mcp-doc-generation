using Shared;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public class ParameterManifestLoadingTests
{
    [Fact]
    public async Task LoadParameterManifestAsync_LoadsV2ManifestForToolCommand()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"param-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var context = new FileNameContext(new Dictionary<string, BrandMapping>(), new Dictionary<string, string>(), new HashSet<string>());
            var manifestFileName = ToolFileNameBuilder.BuildParameterManifestFileName("storage account list", context);
            var manifestPath = Path.Combine(tempDir, manifestFileName);

            // Write a valid v2 manifest
            await File.WriteAllTextAsync(manifestPath, """
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
            """);

            var manifest = await Program.LoadParameterManifestAsync("storage account list", tempDir, context);

            Assert.NotNull(manifest);
            Assert.IsType<CanonicalParameterManifest>(manifest);
            var parameter = Assert.Single(manifest!.Parameters);
            Assert.Equal("Subscription", parameter.DisplayName);
            Assert.Equal("Required", parameter.RequiredText);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadParameterManifestAsync_ReturnsNullWhenFileMissing()
    {
        var context = new FileNameContext(new Dictionary<string, BrandMapping>(), new Dictionary<string, string>(), new HashSet<string>());
        var tempDir = Path.Combine(Path.GetTempPath(), $"param-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifest = await Program.LoadParameterManifestAsync("storage account list", tempDir, context);

            Assert.Null(manifest);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadParameterManifestAsync_WithLegacyFormat_ThrowsParameterManifestException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"param-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var context = new FileNameContext(new Dictionary<string, BrandMapping>(), new Dictionary<string, string>(), new HashSet<string>());
            var manifestFileName = ToolFileNameBuilder.BuildParameterManifestFileName("storage account list", context);
            var manifestPath = Path.Combine(tempDir, manifestFileName);

            // Legacy bare-array format must be rejected (fail-closed)
            await File.WriteAllTextAsync(manifestPath, "[{\"name\":\"--subscription\",\"displayName\":\"Subscription\",\"required\":true,\"requiredText\":\"Required\",\"isConditionalRequired\":false,\"description\":\"Subscription ID.\"}]");

            var ex = await Assert.ThrowsAsync<ParameterManifestException>(
                () => Program.LoadParameterManifestAsync("storage account list", tempDir, context));

            Assert.Equal(ParameterManifestErrorCode.PARAM_MANIFEST_LEGACY_FORMAT, ex.ErrorCode);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
