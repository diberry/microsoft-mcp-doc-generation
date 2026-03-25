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
        "## Capabilities\n\n" +
        "- List all storage accounts in a subscription or resource group\n" +
        "- Create and manage blob containers with access policies\n" +
        "- Upload and download files to blob storage\n" +
        "- Configure storage account settings and access tiers\n" +
        "- Manage table storage entities and queries\n\n" +
        "## Prerequisites\n\n" +
        "- An active Azure subscription\n" +
        "- Azure CLI installed and authenticated\n" +
        "- Appropriate RBAC role assignments on target storage accounts\n\n" +
        "## Permissions\n\n" +
        "| Role | Description |\n|------|-------------|\n" +
        "| Storage Blob Data Reader | Read access to blob data |\n" +
        "| Storage Blob Data Contributor | Read, write, and delete access to blob data |\n" +
        "| Storage Account Contributor | Full access to manage storage accounts |\n\n" +
        "## Best practices\n\n" +
        "- Use managed identity for authentication instead of storage account keys\n" +
        "- Enable soft delete for blob containers to protect against accidental deletion\n" +
        "- Use Azure Private Link endpoints for secure access to storage accounts\n" +
        "- Apply lifecycle management policies to optimize storage costs automatically\n\n" +
        "## Resources\n\n" +
        "- [Azure Storage documentation](/azure/storage/)\n" +
        "- [Azure Blob Storage quickstart](/azure/storage/blobs/storage-quickstart-blobs-portal)\n";

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
            $"# No Frontmatter\n\n{content}\n\n## Capabilities\n\n- Cap1\n\n## Prerequisites\n\n- Pre1\n\n## Permissions\n\n- Perm1");

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
        // Article with frontmatter but missing Permissions section
        var content =
            "---\ntitle: Azure MCP Server tools for Azure Monitor\nms.topic: concept-article\nms.date: 03/25/2026\n---\n\n" +
            "# Azure MCP Server tools for Azure Monitor\n\n" +
            "Azure Monitor provides full-stack monitoring for applications and infrastructure running on Azure and on-premises. " +
            "Use MCP tools to query logs, list metrics, and create alerts with natural language prompts. " +
            "These tools integrate with Log Analytics workspaces and Application Insights resources for comprehensive observability.\n\n" +
            "## Capabilities\n\n" +
            "- Query Azure Monitor logs using Kusto Query Language (KQL)\n" +
            "- List and analyze metrics from Azure resources\n" +
            "- Create and manage alert rules for proactive monitoring\n" +
            "- Configure diagnostic settings for resource telemetry collection\n\n" +
            "## Prerequisites\n\n" +
            "- An active Azure subscription with monitoring resources\n" +
            "- A Log Analytics workspace configured for your environment\n" +
            "- Azure CLI installed and authenticated with appropriate permissions\n";
        await File.WriteAllTextAsync(
            Path.Combine(articlesDir, "horizontal-article-monitor.md"), content);

        var context = CreateContext(_testRoot, "monitor");
        var result = await _validator.ValidateAsync(context, null!, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Missing required section: Permissions"));
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
            "## Capabilities\n\n" +
            "- List and manage Azure SQL databases and servers\n" +
            "- Execute SQL queries against Azure SQL databases\n" +
            "- Configure server firewall rules and security settings\n" +
            "- Manage elastic pools for cost-effective database hosting\n\n" +
            "## Prerequisites\n\n" +
            "- An active Azure subscription with SQL resources provisioned\n" +
            "- Azure CLI installed and authenticated\n\n" +
            "## Permissions\n\n" +
            "- SQL DB Contributor role for database management operations\n" +
            "- SQL Server Contributor role for server-level configuration\n";
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

        if (ns != null)
            ctx.Items["Namespace"] = ns;

        return ctx;
    }
}
