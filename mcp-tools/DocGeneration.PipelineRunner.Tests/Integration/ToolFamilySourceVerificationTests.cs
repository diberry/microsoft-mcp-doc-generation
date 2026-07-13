using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Integration;

public sealed class ToolFamilySourceVerificationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Directory.GetCurrentDirectory(),
        "TestArtifacts",
        $"tool-family-source-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenArticleContainsToolNotInSourceJson()
    {
        var context = CreateContext(
            sourceTools:
            [
                SourceTool("storage account list", "--account")
            ],
            articleTools:
            [
                ("List storage accounts", "storage account list", ["account"]),
                ("Delete storage account", "storage account delete", ["account"]),
            ],
            frontmatterVersion: "3.0.0-beta.14");

        var result = await ValidateAsync(context);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("article marker(s) are not present in source CLI JSON", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("storage account delete", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenDocumentedParameterIsNotInSourceJson()
    {
        var context = CreateContext(
            sourceTools:
            [
                SourceTool("keyvault secret get", "--resource")
            ],
            articleTools:
            [
                ("Get secret", "keyvault secret get", ["resource-name"]),
            ],
            frontmatterVersion: "3.0.0-beta.14",
            namespaceName: "keyvault");

        var result = await ValidateAsync(context);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("parameter(s) documented but not present in source CLI JSON", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("resource-name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenFrontmatterVersionDoesNotMatchSourceVersion()
    {
        var context = CreateContext(
            sourceTools:
            [
                SourceTool("cosmos database list", "--account")
            ],
            articleTools:
            [
                ("List databases", "cosmos database list", ["account"]),
            ],
            frontmatterVersion: "2.0.2",
            namespaceName: "cosmos");

        var result = await ValidateAsync(context);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("frontmatter mcp-cli.version: 2.0.2, source version: 3.0.0-beta.14", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_Passes_WhenArticleMatchesSourceJsonAndVersion()
    {
        var context = CreateContext(
            sourceTools:
            [
                SourceTool("search index list", "--service", "--index")
            ],
            articleTools:
            [
                ("List indexes", "search index list", ["service", "index"]),
            ],
            frontmatterVersion: "3.0.0-beta.14",
            namespaceName: "search");

        var result = await ValidateAsync(context);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Warnings));
    }

    private async Task<ValidatorResult> ValidateAsync(PipelineContext context)
    {
        var validator = new ToolFamilyPostAssemblyValidator();
        return await validator.ValidateAsync(context, new SourceVerificationStep(), CancellationToken.None);
    }

    private PipelineContext CreateContext(
        IReadOnlyList<JsonElement> sourceTools,
        IReadOnlyList<(string Heading, string Command, IReadOnlyList<string> Parameters)> articleTools,
        string frontmatterVersion,
        string namespaceName = "storage")
    {
        var outputPath = Path.Combine(_root, $"generated-{namespaceName}");
        Directory.CreateDirectory(Path.Combine(outputPath, "tools"));
        Directory.CreateDirectory(Path.Combine(outputPath, "tool-family"));

        var cliTools = sourceTools
            .Select(tool => new CliTool(
                tool.GetProperty("command").GetString()!,
                tool.GetProperty("name").GetString()!,
                tool.GetProperty("description").GetString(),
                tool))
            .ToArray();

        using var document = JsonDocument.Parse($$"""{"version":"3.0.0-beta.14","results":[{{string.Join(",", sourceTools.Select(t => t.GetRawText()))}}]}""");
        var cliOutput = new CliMetadataSnapshot(
            Path.Combine(_root, "mcp-cli-metadata", "3.0.0-beta.14+abcdef", "tools-list.json"),
            document.RootElement.Clone(),
            cliTools);

        foreach (var articleTool in articleTools)
        {
            var fileName = $"{articleTool.Command.Replace(' ', '-')}.md";
            File.WriteAllText(
                Path.Combine(outputPath, "tools", fileName),
                $"---\n---\n# {articleTool.Heading}\n\n<!-- @mcpcli {articleTool.Command} -->\n");
        }

        File.WriteAllText(
            Path.Combine(outputPath, "tool-family", $"{namespaceName}.md"),
            BuildArticle(namespaceName, frontmatterVersion, articleTools));

        var context = new PipelineContext
        {
            Request = new PipelineRequest(namespaceName, [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
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
            CliVersion = "3.0.0-beta.14",
            CliOutput = cliOutput,
            SelectedNamespaces = [namespaceName],
        };

        context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = namespaceName;
        return context;
    }

    private static JsonElement SourceTool(string command, params string[] parameters)
    {
        var options = string.Join(
            ",",
            parameters.Select(parameter => $$"""{"name":"{{parameter}}","description":"Test parameter","type":"string"}"""));
        using var document = JsonDocument.Parse($$"""{"name":"{{command.Split(' ').Last()}}","description":"Test tool","command":"{{command}}","option":[{{options}}]}""");
        return document.RootElement.Clone();
    }

    private static string BuildArticle(
        string namespaceName,
        string frontmatterVersion,
        IReadOnlyList<(string Heading, string Command, IReadOnlyList<string> Parameters)> tools)
    {
        var sections = string.Join(
            "\n\n",
            tools.Select(tool => string.Join(
                "\n",
                [
                    $"## {tool.Heading}",
                    $"<!-- @mcpcli {tool.Command} -->",
                    "Example prompts include:",
                    $"- Run {tool.Command} with realistic values",
                    "| Parameter | Required |",
                    "| --- | --- |",
                    .. tool.Parameters.Select(parameter => $"| {parameter} | No |")
                ])));

        return $$"""
        ---
        title: {{namespaceName}} tools
        mcp-cli.version: {{frontmatterVersion}}
        tool_count: {{tools.Count}}
        ---
        # {{namespaceName}} tools

        {{sections}}

        ## See also
        - Link
        """;
    }

    private sealed class SourceVerificationStep : IPipelineStep
    {
        public int Id => 4;
        public string Name => "Generate tool-family article";
        public StepScope Scope => StepScope.Namespace;
        public FailurePolicy FailurePolicy => FailurePolicy.Fatal;
        public IReadOnlyList<int> DependsOn => [];
        public IReadOnlyList<IPostValidator> PostValidators => [];
        public int MaxRetries => 0;

        public ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(StepResult.DryRun([]));
    }
}
