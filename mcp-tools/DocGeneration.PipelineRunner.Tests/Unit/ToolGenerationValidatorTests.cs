using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Tests for <see cref="ToolGenerationValidator"/> which detects pipeline template
/// token leakage (<<<TPL_LABEL_N>>>, <<<FROZEN_SECTION_N>>>), empty/truncated files,
/// and content loss between raw and improved tool files.
/// </summary>
public class ToolGenerationValidatorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ToolGenerationValidator _validator = new();

    public ToolGenerationValidatorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"tg-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    // ── Template token leakage ─────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_DetectsTemplateTokenLeakage_TplLabel()
    {
        var toolsDir = CreateToolsDir();
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-storage-list.md"),
            "# List storage accounts\n\n<<<TPL_LABEL_0>>> This tool lists storage accounts.\n\nMore content to fill out the file adequately for the minimum length check.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("<<<TPL_LABEL_0>>>"));
        Assert.Contains(result.Warnings, w => w.Contains("pipeline template token"));
    }

    [Fact]
    public async Task ValidateAsync_DetectsTemplateTokenLeakage_FrozenSection()
    {
        var toolsDir = CreateToolsDir();
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-keyvault-get.md"),
            "# Get key vault secret\n\n<<<FROZEN_SECTION_3>>> This tool retrieves secrets from Azure Key Vault.\n\nMore content to fill out the file for minimum length.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("<<<FROZEN_SECTION_3>>>"));
    }

    [Fact]
    public async Task ValidateAsync_DetectsOldFormatTemplateTokens()
    {
        var toolsDir = CreateToolsDir();
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-cosmos-list.md"),
            "# List Cosmos DB databases\n\n__TPL_LABEL_2__ This tool lists all Cosmos DB databases in your subscription.\n\nMore content padding to exceed the minimum.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("__TPL_LABEL_2__"));
    }

    [Fact]
    public async Task ValidateAsync_DetectsBoldFormatTemplateTokens()
    {
        var toolsDir = CreateToolsDir();
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-monitor-query.md"),
            "# Query Azure Monitor\n\n**TPL_LABEL_1** This tool queries Azure Monitor logs.\n\nMore content to reach the minimum character threshold.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("**TPL_LABEL_1**"));
    }

    [Fact]
    public async Task ValidateAsync_CleanContent_NoFalsePositives()
    {
        var toolsDir = CreateToolsDir();
        var rawDir = CreateRawDir();
        var rawContent = "# List storage accounts\n\nThis tool lists all Azure storage accounts in your subscription.\n\nExample prompts include:\n\n- \"List all storage accounts in resource group 'my-rg'.\"";
        var improvedContent = "# List storage accounts\n\nThis improved tool lists all Azure storage accounts in your subscription with enhanced descriptions.\n\nExample prompts include:\n\n- \"List all storage accounts in resource group 'my-rg'.\"";
        await File.WriteAllTextAsync(Path.Combine(rawDir, "azure-storage-list.md"), rawContent);
        await File.WriteAllTextAsync(Path.Combine(toolsDir, "azure-storage-list.md"), improvedContent);

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_ContentWithAngleBrackets_NoFalsePositive()
    {
        // Ensure legitimate angle brackets (e.g., in HTML or generic descriptions) don't trigger
        var toolsDir = CreateToolsDir();
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-sql-query.md"),
            "# Query Azure SQL\n\nUse `<connection-string>` for the database endpoint. Configure <<optional>> flags as needed.\n\nThis tool runs SQL queries against your Azure SQL databases and returns results.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
    }

    // ── Empty/truncated files ──────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_DetectsEmptyFile()
    {
        var toolsDir = CreateToolsDir();
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-speech-list.md"),
            string.Empty);

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("empty or truncated"));
    }

    [Fact]
    public async Task ValidateAsync_DetectsTruncatedFile()
    {
        var toolsDir = CreateToolsDir();
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-redis-get.md"),
            "# Short");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("empty or truncated"));
        Assert.Contains(result.Warnings, w => w.Contains("7 chars"));
    }

    [Fact]
    public async Task ValidateAsync_FileJustAboveThreshold_Passes()
    {
        var toolsDir = CreateToolsDir();
        // 51 chars = above 50 char threshold
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-aks-list.md"),
            "# List AKS clusters in the current subscription!!!");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
    }

    // ── Content loss detection ──────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_DetectsContentLoss()
    {
        var toolsDir = CreateToolsDir();
        var rawDir = CreateRawDir();

        // Raw file: 200 chars, improved file: 80 chars (60% shorter → triggers >50% warning)
        var rawContent = new string('A', 200);
        var improvedContent = new string('B', 80);

        await File.WriteAllTextAsync(Path.Combine(rawDir, "azure-compute-list.md"), rawContent);
        await File.WriteAllTextAsync(Path.Combine(toolsDir, "azure-compute-list.md"), improvedContent);

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("content loss") && w.Contains("azure-compute-list.md"));
    }

    [Fact]
    public async Task ValidateAsync_NoContentLoss_WhenImprovedLonger()
    {
        var toolsDir = CreateToolsDir();
        var rawDir = CreateRawDir();

        var rawContent = "# Short raw content for testing the validator logic.";
        var improvedContent = "# This improved content is actually longer than the raw input, which is expected behavior for the AI improvement step.\n\nMore detail added here.";

        await File.WriteAllTextAsync(Path.Combine(rawDir, "azure-storage-list.md"), rawContent);
        await File.WriteAllTextAsync(Path.Combine(toolsDir, "azure-storage-list.md"), improvedContent);

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_NoContentLoss_WhenRawFileMissing()
    {
        var toolsDir = CreateToolsDir();
        // No raw directory — content loss check should be skipped gracefully
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "azure-sql-list.md"),
            "# List SQL databases\n\nThis tool lists Azure SQL databases. No raw file to compare against here.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_ContentLoss_AtExactThreshold_NoWarning()
    {
        var toolsDir = CreateToolsDir();
        var rawDir = CreateRawDir();

        // Raw: 100 chars, Improved: 50 chars = exactly 50% — should NOT warn (>50% threshold)
        var rawContent = new string('A', 100);
        var improvedContent = new string('B', 50);

        await File.WriteAllTextAsync(Path.Combine(rawDir, "azure-search-query.md"), rawContent);
        await File.WriteAllTextAsync(Path.Combine(toolsDir, "azure-search-query.md"), improvedContent);

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
    }

    // ── Edge cases ─────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_NoToolsDirectory_ReturnsSuccess()
    {
        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_EmptyToolsDirectory_ReturnsSuccess()
    {
        CreateToolsDir(); // empty directory
        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_MultipleIssues_ReportsAll()
    {
        var toolsDir = CreateToolsDir();
        var rawDir = CreateRawDir();

        // File 1: leaked token
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "leaked.md"),
            "# Leaked\n\n<<<TPL_LABEL_5>>> This tool has a leaked pipeline template token in the final output file.");
        // File 2: truncated
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "short.md"),
            "# Short");
        // File 3: content loss
        await File.WriteAllTextAsync(Path.Combine(rawDir, "shrunk.md"), new string('X', 300));
        await File.WriteAllTextAsync(Path.Combine(toolsDir, "shrunk.md"), new string('Y', 100));
        // File 4: clean
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "clean.md"),
            "# Clean tool\n\nThis tool works perfectly with no issues at all, providing Azure storage management capabilities.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        // Should have a summary warning + individual warnings for leaked, short, and shrunk
        Assert.Contains(result.Warnings, w => w.Contains("leaked.md"));
        Assert.Contains(result.Warnings, w => w.Contains("short.md"));
        Assert.Contains(result.Warnings, w => w.Contains("shrunk.md"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("clean.md"));
    }

    [Fact]
    public async Task ValidateAsync_SummaryCountsDistinctFiles()
    {
        var toolsDir = CreateToolsDir();
        // File has BOTH a leaked token AND is a content-loss candidate
        var rawDir = CreateRawDir();
        await File.WriteAllTextAsync(Path.Combine(rawDir, "dual-issue.md"), new string('X', 400));
        await File.WriteAllTextAsync(
            Path.Combine(toolsDir, "dual-issue.md"),
            "<<<TPL_LABEL_0>>> Some short remaining content after bad AI rewrite.");

        var context = CreateContext(_testRoot);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        // Summary should count the file once even though it has multiple issues
        Assert.Contains(result.Warnings, w => w.Contains("1 of 1 tool file(s)"));
    }

    // ── Integration: validator registered for Step 3 ───────────────────

    [Fact]
    public void Step3_HasToolGenerationValidator_Registered()
    {
        var registry = StepRegistry.CreateDefault(scriptsRoot: ".");
        var step3 = registry.GetStep(3);

        Assert.Contains(step3.PostValidators, v => v is ToolGenerationValidator);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private string CreateToolsDir()
    {
        var toolsDir = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDir);
        return toolsDir;
    }

    private string CreateRawDir()
    {
        var rawDir = Path.Combine(_testRoot, "tools-raw");
        Directory.CreateDirectory(rawDir);
        return rawDir;
    }

    private static PipelineContext CreateContext(string outputPath) => new()
    {
        Request = new PipelineRequest("test", [3], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
        RepoRoot = outputPath,
        McpToolsRoot = outputPath,
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
