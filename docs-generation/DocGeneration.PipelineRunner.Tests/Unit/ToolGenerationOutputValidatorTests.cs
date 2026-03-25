using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ToolGenerationOutputValidatorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ToolGenerationOutputValidator _validator = new();

    public ToolGenerationOutputValidatorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Fact]
    public async Task ValidateAsync_CleanFiles_ReturnsSuccess()
    {
        var toolsDir = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDir);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-storage-list.md"),
            "# List storage accounts\n\nThis tool lists all storage accounts in your subscription.\n\nExample prompts include:\n\n- \"List all storage accounts in resource group 'my-rg'.\"");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_LeakedHandlebarsTokens_ReturnsFail()
    {
        var toolsDir = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDir);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-storage-list.md"),
            "# List storage accounts\n\nThis tool {{toolName}} lists accounts. The service is {{serviceBrandName}}.\n\nMore content to exceed minimum length threshold here.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Handlebars token"));
        Assert.Contains(result.Warnings, w => w.Contains("{{toolName}}"));
    }

    [Fact]
    public async Task ValidateAsync_LeakedTemplateVariables_ReturnsFail()
    {
        var toolsDir = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDir);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-compute-list.md"),
            "# List VMs\n\nThis tool has {REQUIRED_PARAM_COUNT} required parameters: {PARAM_NAMES}.\n\nMore content padding to meet the minimum character threshold requirement for the validator.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("template variable"));
    }

    [Fact]
    public async Task ValidateAsync_SuspiciouslyShortFile_ReturnsFail()
    {
        var toolsDir = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDir);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-keyvault-list.md"),
            "# List\n\nEmpty.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("suspiciously short"));
    }

    [Fact]
    public async Task ValidateAsync_NoToolsDirectory_ReturnsSuccess()
    {
        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_MixedCleanAndLeaked_FailsOnlyForLeaked()
    {
        var toolsDir = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDir);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "clean-tool.md"),
            "# Clean Tool\n\nThis tool works correctly and has no leaked tokens. It provides Azure storage management capabilities for listing accounts.");
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "leaked-tool.md"),
            "# Leaked Tool\n\nThis tool {{EXAMPLE_PROMPTS_CONTENT}} has a leaked placeholder that should not be in the final output content.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("1 of 2 tool file(s)"));
        Assert.Contains(result.Warnings, w => w.Contains("leaked-tool.md"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("clean-tool.md"));
    }

    private static PipelineContext CreateContext(string outputPath) => new()
    {
        Request = new PipelineRequest("test", [3], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
        RepoRoot = outputPath,
        DocsGenerationRoot = outputPath,
        OutputPath = outputPath,
        ProcessRunner = new RecordingProcessRunner(),
        Workspaces = new WorkspaceManager(),
        CliMetadataLoader = new StubCliMetadataLoader(),
        TargetMatcher = new TargetMatcher(),
        FilteredCliWriter = new StubFilteredCliWriter(),
        BuildCoordinator = new StubBuildCoordinator(),
        AiCapabilityProbe = new StubAiCapabilityProbe(),
        Reports = new BufferedReportWriter(),
    };
}
