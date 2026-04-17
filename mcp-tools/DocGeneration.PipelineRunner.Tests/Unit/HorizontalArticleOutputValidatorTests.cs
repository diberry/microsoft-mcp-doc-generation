using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class HorizontalArticleOutputValidatorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly HorizontalArticleOutputValidator _validator = new();

    public HorizontalArticleOutputValidatorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"horiz-validator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    private const string ValidArticle =
        "---\ntitle: Azure MCP Server tools for Azure Storage\nms.topic: concept-article\nms.date: 03/25/2026\n---\n\n" +
        "# Azure MCP Server tools for Azure Storage\n\n" +
        "Azure Storage provides scalable, durable cloud storage for blobs, files, queues, and tables. " +
        "Use Azure MCP Server tools to manage storage accounts, containers, and blobs with natural language prompts from your IDE. " +
        "These tools help you create, list, and configure storage resources without leaving your development environment.\n\n" +
        "## What is the Azure MCP Server?\n\n" +
        "For Azure Storage users, this means you can:\n\n" +
        "- List all storage accounts in a subscription or resource group\n" +
        "- Create and manage blob containers with access policies\n" +
        "- Upload and download files to blob storage\n\n" +
        "## Prerequisites\n\n" +
        "- An active Azure subscription\n" +
        "- Azure CLI installed and authenticated\n" +
        "- Appropriate RBAC role assignments on target storage accounts\n\n" +
        "## Where can you use Azure MCP Server?\n\n" +
        "Available in VS Code, Visual Studio, and other MCP-compatible tools.\n\n" +
        "## Available tools for Azure Storage\n\n" +
        "| Tool | Description |\n|------|-------------|\n" +
        "| `storage account list` | List storage accounts in a subscription |\n" +
        "| `storage blob list` | List blobs in a container |\n\n" +
        "## Get started\n\n" +
        "1. Set up your environment\n" +
        "2. Start exploring\n\n" +
        "## Best practices\n\n" +
        "- **Use managed identity**: Prefer managed identity for authentication instead of storage account keys\n" +
        "- **Enable soft delete**: Enable soft delete for blob containers to protect against accidental deletion\n\n" +
        "## Related content\n\n" +
        "* [Azure MCP Server overview](../overview.md)\n" +
        "* [Azure Storage documentation](/azure/storage/)\n";

    [Fact]
    public async Task ValidateAsync_ValidArticle_ReturnsSuccess()
    {
        var articlesDir = Path.Combine(_testRoot, "horizontal-articles");
        Directory.CreateDirectory(articlesDir);
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "horizontal-article-storage.md"), ValidArticle);

        var context = CreateContext(_testRoot, "storage");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_MissingArticle_ReturnsFail()
    {
        var articlesDir = Path.Combine(_testRoot, "horizontal-articles");
        Directory.CreateDirectory(articlesDir);
        // No article file created

        var context = CreateContext(_testRoot, "storage");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("not found"));
    }

    [Fact]
    public async Task ValidateAsync_ErrorArtifactPresent_ReturnsFail()
    {
        var articlesDir = Path.Combine(_testRoot, "horizontal-articles");
        Directory.CreateDirectory(articlesDir);
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "horizontal-article-storage.md"), ValidArticle);
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "error-storage.txt"), "AI generation failed: rate limit");

        var context = CreateContext(_testRoot, "storage");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("error artifacts"));
    }

    [Fact]
    public async Task ValidateAsync_TruncatedContent_ReturnsFail()
    {
        var articlesDir = Path.Combine(_testRoot, "horizontal-articles");
        Directory.CreateDirectory(articlesDir);
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "horizontal-article-cosmos.md"),
            "---\ntitle: Test\n---\n\n# Short\n\nTruncated.");

        var context = CreateContext(_testRoot, "cosmos");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("truncated"));
    }

    [Fact]
    public async Task ValidateAsync_MissingFrontmatter_ReturnsFail()
    {
        var content = new string('x', 2000); // Long enough but no frontmatter
        var articlesDir = Path.Combine(_testRoot, "horizontal-articles");
        Directory.CreateDirectory(articlesDir);
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "horizontal-article-keyvault.md"),
            $"# No Frontmatter\n\n{content}\n\n## Prerequisites\n\n- Pre1\n\n## Best practices\n\n- Practice1\n\n## Related content\n\n- Link1");

        var context = CreateContext(_testRoot, "keyvault");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Missing or invalid frontmatter"));
    }

    [Fact]
    public async Task ValidateAsync_MissingRequiredSections_ReturnsFail()
    {
        var articlesDir = Path.Combine(_testRoot, "horizontal-articles");
        Directory.CreateDirectory(articlesDir);
        // Article with frontmatter but missing Best practices and Related content sections
        var content =
            "---\ntitle: Azure MCP Server tools for Azure Monitor\nms.topic: concept-article\nms.date: 03/25/2026\n---\n\n" +
            "# Azure MCP Server tools for Azure Monitor\n\n" +
            "Azure Monitor provides full-stack monitoring for applications and infrastructure running on Azure and on-premises. " +
            "Use MCP tools to query logs, list metrics, and create alerts with natural language prompts. " +
            "These tools integrate with Log Analytics workspaces and Application Insights resources for comprehensive observability.\n\n" +
            "## Prerequisites\n\n" +
            "- An active Azure subscription with monitoring resources\n" +
            "- A Log Analytics workspace configured for your environment\n" +
            "- Azure CLI installed and authenticated with appropriate permissions\n";
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "horizontal-article-monitor.md"), content);

        var context = CreateContext(_testRoot, "monitor");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Missing required section: Best practices"));
    }

    [Fact]
    public async Task ValidateAsync_MissingFrontmatterField_WarnsButDetails()
    {
        var articlesDir = Path.Combine(_testRoot, "horizontal-articles");
        Directory.CreateDirectory(articlesDir);
        // Frontmatter missing ms.date
        var content =
            "---\ntitle: Azure MCP Server tools for Azure SQL\nms.topic: concept-article\n---\n\n" +
            "# Azure MCP Server tools for Azure SQL\n\n" +
            "Azure SQL provides a family of managed database services built on SQL Server. " +
            "Use MCP tools to manage databases, configure server settings, and execute queries with natural language prompts. " +
            "These tools support Azure SQL Database, Azure SQL Managed Instance, and elastic pool management.\n\n" +
            "## Prerequisites\n\n" +
            "- An active Azure subscription with SQL resources provisioned\n" +
            "- Azure CLI installed and authenticated\n\n" +
            "## Best practices\n\n" +
            "- **Use managed identity**: Prefer managed identity for SQL authentication\n" +
            "- **Enable auditing**: Configure database auditing for compliance\n\n" +
            "## Related content\n\n" +
            "* [Azure SQL documentation](/azure/azure-sql/)\n";
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "horizontal-article-sql.md"), content);

        var context = CreateContext(_testRoot, "sql");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Frontmatter missing ms.date"));
    }

    [Fact]
    public async Task ValidateAsync_NoNamespaceInContext_ReturnsSuccess()
    {
        // Context without Namespace set — validator should skip gracefully
        var context = CreateContext(_testRoot, null);
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.True(result.Success);
    }

    private static PipelineContext CreateContext(string outputPath, string? ns)
    {
        var ctx = new PipelineContext
        {
            Request = new PipelineRequest(ns, [6], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
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

        if (ns != null)
            ctx.Items["Namespace"] = ns;

        return ctx;
    }
}
