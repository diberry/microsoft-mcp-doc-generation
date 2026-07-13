using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public sealed class VersionVerificationGateTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Directory.GetCurrentDirectory(),
        "TestArtifacts",
        $"version-verification-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenConfiguredVersionDoesNotMatchCliVersion()
    {
        var context = CreateContext(configuredVersion: "3.0.0-beta.14", cliVersion: "2.0.2");
        SeedSourceMetadata("3.0.0-beta.14+abcdef", """{"version":"3.0.0-beta.14","results":[]}""");

        var result = await SourceVersionVerificationGate.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("configured target version '3.0.0-beta.14'", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("CLI version file '2.0.2'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenSourceSnapshotFolderDoesNotMatchConfiguredVersion()
    {
        var context = CreateContext(
            configuredVersion: "3.0.0-beta.14",
            cliVersion: "3.0.0-beta.14",
            cliOutputPath: Path.Combine(_root, "mcp-cli-metadata", "2.0.2+abcdef", "tools-list.json"));

        var result = await SourceVersionVerificationGate.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("source metadata folder version '2.0.2'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenGeneratedCliOutputMatchesButConfiguredSourceFolderIsWrong()
    {
        var context = CreateContext(
            configuredVersion: "3.0.0-beta.14",
            cliVersion: "3.0.0-beta.14");
        SeedSourceMetadata("2.0.2+abcdef", """{"version":"3.0.0-beta.14","results":[]}""");

        var result = await SourceVersionVerificationGate.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("source metadata folder version '2.0.2'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenConfiguredSourceFolderIsUnavailable()
    {
        var context = CreateContext(
            configuredVersion: "3.0.0-beta.14",
            cliVersion: "3.0.0-beta.14");
        Directory.CreateDirectory(Path.Combine(_root, "mcp-cli-metadata"));

        var result = await SourceVersionVerificationGate.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("source metadata folder for configured target version '3.0.0-beta.14' was not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenConfiguredSourceJsonIsEmpty()
    {
        var context = CreateContext(
            configuredVersion: "3.0.0-beta.14",
            cliVersion: "3.0.0-beta.14");
        SeedSourceMetadata("3.0.0-beta.14+abcdef", "");

        var result = await SourceVersionVerificationGate.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("source CLI JSON is empty", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenConfiguredSourceJsonIsMissing()
    {
        var context = CreateContext(
            configuredVersion: "3.0.0-beta.14",
            cliVersion: "3.0.0-beta.14");
        Directory.CreateDirectory(Path.Combine(_root, "mcp-cli-metadata", "3.0.0-beta.14+abcdef"));

        var result = await SourceVersionVerificationGate.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("source CLI JSON was not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Passes_WhenConfiguredCliAndSourceFolderVersionsMatch()
    {
        var context = CreateContext(
            configuredVersion: "3.0.0-beta.14",
            cliVersion: "3.0.0-beta.14",
            cliOutputPath: Path.Combine(_root, "mcp-cli-metadata", "3.0.0-beta.14+abcdef", "tools-list.json"));
        SeedSourceMetadata("3.0.0-beta.14+abcdef", """{"version":"3.0.0-beta.14","results":[]}""");

        var result = await SourceVersionVerificationGate.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Warnings));
    }

    private PipelineContext CreateContext(string configuredVersion, string cliVersion, string? cliOutputPath = null)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "mcp-tool-version.txt"), configuredVersion);

        var outputPath = Path.Combine(_root, "generated-storage");
        Directory.CreateDirectory(outputPath);
        var snapshot = CreateSnapshot(cliOutputPath ?? Path.Combine(outputPath, "cli", "cli-output.json"));

        return new PipelineContext
        {
            Request = new PipelineRequest("storage", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = _root,
            McpToolsRoot = Path.Combine(_root, "mcp-tools"),
            OutputPath = outputPath,
            ProcessRunner = new RecordingProcessRunner(),
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = new StubCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new StubFilteredCliWriter(),
            BuildCoordinator = new StubBuildCoordinator(),
            AiCapabilityProbe = new StubAiCapabilityProbe(),
            Reports = new BufferedReportWriter(),
            CliVersion = cliVersion,
            CliOutput = snapshot,
            SelectedNamespaces = ["storage"],
        };
    }

    private void SeedSourceMetadata(string folderName, string json)
    {
        var directory = Path.Combine(_root, "mcp-cli-metadata", folderName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "cli-output.json"), json);
    }

    private static CliMetadataSnapshot CreateSnapshot(string filePath)
    {
        using var document = JsonDocument.Parse("""{"version":"3.0.0-beta.14","results":[]}""");
        return new CliMetadataSnapshot(filePath, document.RootElement.Clone(), []);
    }
}
