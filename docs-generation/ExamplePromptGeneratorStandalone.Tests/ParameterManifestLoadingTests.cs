using ExamplePromptGeneratorStandalone.Models;
using Shared;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public class ParameterManifestLoadingTests
{
    [Fact]
    public async Task LoadParameterManifestAsync_LoadsManifestForToolCommand()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"param-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var context = new FileNameContext(new Dictionary<string, BrandMapping>(), new Dictionary<string, string>(), new HashSet<string>());
            var manifestFileName = ToolFileNameBuilder.BuildParameterManifestFileName("storage account list", context);
            var manifestPath = Path.Combine(tempDir, manifestFileName);
            await File.WriteAllTextAsync(manifestPath, "[{\"name\":\"--subscription\",\"displayName\":\"Subscription\",\"required\":true,\"requiredText\":\"Required\",\"isConditionalRequired\":false,\"description\":\"Subscription ID.\"}]");

            var manifest = await Program.LoadParameterManifestAsync("storage account list", tempDir, context);

            var parameter = Assert.Single(manifest!);
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
}
