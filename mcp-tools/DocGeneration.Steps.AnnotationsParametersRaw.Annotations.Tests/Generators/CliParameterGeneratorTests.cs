// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Generators;
using Shared;
using Xunit;

namespace DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests.Generators;

public class CliParameterGeneratorTests: IDisposable
{
    private readonly string _outputDir;
    private readonly string _templateFile;
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

    public CliParameterGeneratorTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "cli-param-gen-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
        _ctx = new FileNameContext(BrandMappings, CompoundWords, StopWords);

        var projectRoot = FindProjectRoot();
        _templateFile = Path.Combine(projectRoot, "mcp-tools", "templates", "cli-parameter-template.hbs");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    private static string FindProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "mcp-doc-generation.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not find project root");
    }

    private static Dictionary<string, CliToolInfo> MakeSingleTool(string command, params CliSwitch[] switches)
    {
        return new Dictionary<string, CliToolInfo>
        {
            [command] = new CliToolInfo(command, "Test tool", switches.ToList())
        };
    }

    [Fact]
    public async Task GenerateParameterCliFiles_SingleTool_WritesFile()
    {
        var tools = MakeSingleTool("storage account list",
            new CliSwitch("--resource-group", "The resource group name"));

        await CliParameterGenerator
            .GenerateParameterCliFilesAsync(tools, _templateFile, _outputDir, _ctx, "1.0.0", DateTime.UtcNow);

        var files = Directory.GetFiles(_outputDir, "*.md");
        Assert.Single(files);
    }

    [Fact]
    public async Task GenerateParameterCliFiles_RendersParameterTable()
    {
        var tools = MakeSingleTool("storage account list",
            new CliSwitch("--resource-group", "The resource group name", "string"));

        await CliParameterGenerator
            .GenerateParameterCliFilesAsync(tools, _templateFile, _outputDir, _ctx, "1.0.0", DateTime.UtcNow);

        var file = Directory.GetFiles(_outputDir, "*.md").Single();
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains("| Parameter |", content);
        Assert.Contains("`--resource-group`", content);
    }

    [Fact]
    public async Task GenerateParameterCliFiles_NoParameters_WritesNoParamsMessage()
    {
        var tools = MakeSingleTool("storage account list");

        await CliParameterGenerator
            .GenerateParameterCliFilesAsync(tools, _templateFile, _outputDir, _ctx, "1.0.0", DateTime.UtcNow);

        var file = Directory.GetFiles(_outputDir, "*.md").Single();
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains("This tool has no CLI parameters.", content);
    }

    [Fact]
    public async Task GenerateParameterCliFiles_MultipleSwitches_AllRendered()
    {
        var tools = MakeSingleTool("storage account list",
            new CliSwitch("--resource-group", "The resource group", "string"),
            new CliSwitch("--subscription", "The subscription ID", "string"),
            new CliSwitch("--output", "Output format", "string", Default: "json"));

        await CliParameterGenerator
            .GenerateParameterCliFilesAsync(tools, _templateFile, _outputDir, _ctx, "1.0.0", DateTime.UtcNow);

        var file = Directory.GetFiles(_outputDir, "*.md").Single();
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains("`--resource-group`", content);
        Assert.Contains("`--subscription`", content);
        Assert.Contains("`--output`", content);
    }

    [Fact]
    public async Task GenerateParameterCliFiles_IncludesFrontmatter()
    {
        var genDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var tools = MakeSingleTool("storage account list",
            new CliSwitch("--rg", "Resource group"));

        await CliParameterGenerator
            .GenerateParameterCliFilesAsync(tools, _templateFile, _outputDir, _ctx, "2.0.0", genDate);

        var file = Directory.GetFiles(_outputDir, "*.md").Single();
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains("ms.topic: include", content);
        Assert.Contains("ms.date: 06/15/2025", content);
        Assert.Contains("mcp-cli.version: 2.0.0", content);
    }

    [Fact]
    public async Task GenerateParameterCliFiles_UsesCorrectFileName()
    {
        var tools = MakeSingleTool("aks nodepool get",
            new CliSwitch("--name", "Name"));

        await CliParameterGenerator
            .GenerateParameterCliFilesAsync(tools, _templateFile, _outputDir, _ctx, "1.0.0", DateTime.UtcNow);

        var file = Directory.GetFiles(_outputDir, "*.md").Single();
        Assert.Equal("azure-kubernetes-service-node-pool-get-parameters-cli.md", Path.GetFileName(file));
    }
}
