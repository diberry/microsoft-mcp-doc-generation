using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class PipelineContextFactoryTests
{
    [Fact]
    public async Task CreateAsync_WithExplicitNamespace_ResolvesAbsolutePaths()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            var factory = CreateFactory(repoRoot, new StubCliMetadataLoader());
            var relativePath = Path.Combine(".", "custom-output");
            var request = new PipelineRequest("storage_account", new[] { 1 }, relativePath, false, false, false);

            var context = await factory.CreateAsync(request, CancellationToken.None);

            Assert.Equal(repoRoot, context.RepoRoot);
            Assert.Equal(Path.Combine(repoRoot, "mcp-tools"), context.McpToolsRoot);
            Assert.Equal(Path.GetFullPath(Path.Combine(repoRoot, relativePath)), context.OutputPath);
            Assert.Equal(new[] { "storage account" }, context.SelectedNamespaces);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenMetadataExists_LoadsNamespacesAndCliVersion()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            var outputPath = Path.Combine(repoRoot, "generated");
            var cliDirectory = Path.Combine(outputPath, "cli");
            Directory.CreateDirectory(cliDirectory);
            await File.WriteAllTextAsync(Path.Combine(cliDirectory, "cli-version.json"), "{\"version\":\"1.2.3\"}");
            await File.WriteAllTextAsync(Path.Combine(cliDirectory, "cli-namespace.json"), "{\"results\":[{\"name\":\"compute\"},{\"name\":\"storage\"}]}");
            await File.WriteAllTextAsync(Path.Combine(cliDirectory, "cli-output.json"), "{\"version\":\"1.2.3\",\"results\":[{\"command\":\"compute list\",\"name\":\"compute list\",\"description\":\"List compute resources\"}]}");

            var factory = CreateFactory(repoRoot, new CliMetadataLoader());
            var request = new PipelineRequest(null, new[] { 1 }, Path.Combine(".", "generated"), false, false, false);

            var context = await factory.CreateAsync(request, CancellationToken.None);

            Assert.Equal(new[] { "compute", "storage" }, context.SelectedNamespaces);
            Assert.Equal("1.2.3", context.CliVersion);
            Assert.NotNull(context.CliOutput);
            Assert.Single(context.CliOutput!.Tools);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private static PipelineContextFactory CreateFactory(string repoRoot, ICliMetadataLoader cliMetadataLoader)
        => new(
            new RecordingProcessRunner(),
            new WorkspaceManager(),
            cliMetadataLoader,
            new TargetMatcher(),
            new StubFilteredCliWriter(),
            new StubBuildCoordinator(),
            new StubAiCapabilityProbe(),
            new BufferedReportWriter(),
            repoRoot);

    private static string CreateTempRepo()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);
        return repoRoot;
    }
}
