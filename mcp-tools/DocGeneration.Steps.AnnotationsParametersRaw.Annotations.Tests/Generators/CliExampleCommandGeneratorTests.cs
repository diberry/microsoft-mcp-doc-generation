// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Generators;
using Shared;
using Xunit;

namespace DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests.Generators;

public class CliExampleCommandGeneratorTests : IDisposable
{
    private readonly string _outputDir;
    private readonly FileNameContext _ctx;

    private static readonly Dictionary<string, BrandMapping> BrandMappings = new()
    {
        ["aks"] = new BrandMapping { FileName = "azure-kubernetes-service" },
        ["storage"] = new BrandMapping { FileName = "azure-storage" },
    };

    private static readonly Dictionary<string, string> CompoundWords = new()
    {
        ["nodepool"] = "node-pool",
    };

    private static readonly HashSet<string> StopWords = new() { "azure" };

    public CliExampleCommandGeneratorTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "cli-example-cmd-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
        _ctx = new FileNameContext(BrandMappings, CompoundWords, StopWords);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    private static CliToolInfo MakeTool(string command, params CliSwitch[] switches)
        => new(command, "Test tool", switches.ToList());

    // ── BuildExampleCommandContent tests ───────────────────────────

    [Fact]
    public void BuildExampleCommandContent_BasicUsage_IncludesAzmcpPrefix()
    {
        var tool = MakeTool("storage account list");
        var content = CliExampleCommandGenerator.BuildExampleCommandContent("storage account list", tool);

        Assert.Contains("azmcp storage account list", content);
        Assert.Contains("### Example CLI commands", content);
        Assert.Contains("```azurecli", content);
    }

    [Fact]
    public void BuildExampleCommandContent_WithToolSpecificSwitches_ShowsFullExample()
    {
        var tool = MakeTool("storage account list",
            new CliSwitch("--resource-group", "RG name", "string"),
            new CliSwitch("--account-name", "Storage account name", "string"));

        var content = CliExampleCommandGenerator.BuildExampleCommandContent("storage account list", tool);

        Assert.Contains("With parameters:", content);
        Assert.Contains("--resource-group", content);
        Assert.Contains("--account-name", content);
    }

    [Fact]
    public void BuildExampleCommandContent_OnlyGlobalSwitches_NoFullExample()
    {
        var tool = MakeTool("storage account list",
            new CliSwitch("--tenant", "Tenant ID", "string"),
            new CliSwitch("--auth-method", "Auth method", "string"),
            new CliSwitch("--subscription", "Subscription ID", "string"),
            new CliSwitch("--retry-mode", "Retry mode", "string"));

        var content = CliExampleCommandGenerator.BuildExampleCommandContent("storage account list", tool);

        Assert.DoesNotContain("With parameters:", content);
        Assert.DoesNotContain("--subscription", content);
    }

    [Fact]
    public void BuildExampleCommandContent_SwitchWithValuePlaceholder_UsesPlaceholder()
    {
        var tool = MakeTool("storage account list",
            new CliSwitch("--resource-group", "RG", "string", ValuePlaceholder: "<my-rg>"));

        var content = CliExampleCommandGenerator.BuildExampleCommandContent("storage account list", tool);

        Assert.Contains("--resource-group <my-rg>", content);
    }

    [Fact]
    public void BuildExampleCommandContent_SwitchWithoutPlaceholder_GeneratesFromName()
    {
        var tool = MakeTool("storage account list",
            new CliSwitch("--resource-group", "RG", "string"));

        var content = CliExampleCommandGenerator.BuildExampleCommandContent("storage account list", tool);

        Assert.Contains("--resource-group <resource-group>", content);
    }

    // ── GenerateExampleCommandFilesAsync tests ─────────────────────

    [Fact]
    public async Task GenerateExampleCommandFiles_WritesFiles()
    {
        var tools = new Dictionary<string, CliToolInfo>
        {
            ["storage account list"] = MakeTool("storage account list",
                new CliSwitch("--rg", "Resource group"))
        };

        await CliExampleCommandGenerator.GenerateExampleCommandFilesAsync(
            tools, _outputDir, _ctx, "1.0.0", DateTime.UtcNow);

        var files = Directory.GetFiles(_outputDir, "*.md");
        Assert.Single(files);
    }

    [Fact]
    public async Task GenerateExampleCommandFiles_IncludesFrontmatter()
    {
        var genDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var tools = new Dictionary<string, CliToolInfo>
        {
            ["storage account list"] = MakeTool("storage account list")
        };

        await CliExampleCommandGenerator.GenerateExampleCommandFilesAsync(
            tools, _outputDir, _ctx, "2.0.0", genDate);

        var file = Directory.GetFiles(_outputDir, "*.md").Single();
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains("ms.topic: include", content);
        Assert.Contains("ms.date: 06/15/2025", content);
        Assert.Contains("mcp-cli.version: 2.0.0", content);
    }

    [Fact]
    public async Task GenerateExampleCommandFiles_UsesCorrectFileName()
    {
        var tools = new Dictionary<string, CliToolInfo>
        {
            ["aks nodepool get"] = MakeTool("aks nodepool get")
        };

        await CliExampleCommandGenerator.GenerateExampleCommandFilesAsync(
            tools, _outputDir, _ctx, "1.0.0", DateTime.UtcNow);

        var file = Directory.GetFiles(_outputDir, "*.md").Single();
        Assert.Equal("azure-kubernetes-service-node-pool-get-example-commands.md", Path.GetFileName(file));
    }
}
