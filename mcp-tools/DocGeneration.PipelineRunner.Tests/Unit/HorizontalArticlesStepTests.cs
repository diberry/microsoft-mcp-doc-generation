using System.Text.Json;
using HorizontalArticleGenerator.Models;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using ToolGeneration_Improved.Models;
using ToolGeneration_Improved.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public sealed class HorizontalArticlesStepTests
{
    [Fact]
    public async Task Step6_ReducerPath_UsesArticleOutlineOverride_AndSkipsSubprocess()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, processRunner, skipValidation: false, toolCommands: ["compute vm create", "compute vm list"]);
            context.Items["Namespace"] = "compute";

            Directory.CreateDirectory(Path.Combine(context.OutputPath, "cli"));
            await File.WriteAllTextAsync(
                Path.Combine(context.OutputPath, "cli", "cli-version.json"),
                "{\"version\":\"1.2.3\"}");
            await File.WriteAllTextAsync(
                Path.Combine(context.OutputPath, "cli", "cli-output.json"),
                JsonSerializer.Serialize(new
                {
                    results = new object[]
                    {
                        new
                        {
                            command = "compute vm create",
                            name = "compute vm create",
                            description = "Create virtual machines.",
                            option = new object[] { new { name = "name" } }
                        },
                        new
                        {
                            command = "compute vm list",
                            name = "compute vm list",
                            description = "List virtual machines.",
                            option = Array.Empty<object>()
                        }
                    }
                }));

            ArticleOutlineContext? capturedOutline = null;
            context.Items[HorizontalArticlesStep.ArticleOutlineOverrideKey] =
                (Func<ArticleOutlineContext, CancellationToken, Task<string>>)((outline, _) =>
                {
                    capturedOutline = outline;
                    return Task.FromResult("# Override article");
                });

            var step = new HorizontalArticlesStep();

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success, string.Join(" | ", result.Warnings));
            Assert.Empty(processRunner.Invocations);
            Assert.NotNull(capturedOutline);
            Assert.Equal("compute", capturedOutline!.ServiceIdentifier);
            Assert.NotEmpty(capturedOutline.Sections);
            Assert.Equal(
                "# Override article",
                await File.ReadAllTextAsync(Path.Combine(context.OutputPath, "horizontal-articles", "horizontal-article-compute.md")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string CreateTestRoot()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "horizontal-articles-step-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        return testRoot;
    }

    private static void DeleteTestRoot(string testRoot)
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static PipelineContext CreateContext(string testRoot, IProcessRunner processRunner, bool skipValidation, IReadOnlyList<string> toolCommands)
    {
        var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(mcpToolsRoot);
        Directory.CreateDirectory(outputPath);

        var context = new PipelineContext
        {
            Request = new PipelineRequest("compute", [1], outputPath, SkipBuild: true, SkipValidation: skipValidation, DryRun: false),
            RepoRoot = testRoot,
            McpToolsRoot = mcpToolsRoot,
            OutputPath = outputPath,
            ProcessRunner = processRunner,
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = new StubCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new FilteredCliWriter(new WorkspaceManager()),
            BuildCoordinator = new StubBuildCoordinator(),
            AiCapabilityProbe = new StubAiCapabilityProbe(),
            Reports = new BufferedReportWriter(),
            CliVersion = "1.2.3",
            CliOutput = CreateSnapshot(toolCommands),
            SelectedNamespaces = ["compute"],
        };

        context.Items[ToolGenerationStep.ToolImproverOverrideKey] =
            static (ToolGenerationContext toolContext, CancellationToken _) => Task.FromResult(new ImprovedToolData
            {
                FileName = toolContext.ToolName,
                OriginalContent = toolContext.ComposedContent,
                ImprovedContent = toolContext.ComposedContent,
                WasImproved = false
            });

        return context;
    }

    private static CliMetadataSnapshot CreateSnapshot(IReadOnlyList<string> toolCommands)
    {
        var json = JsonSerializer.Serialize(new
        {
            version = "1.2.3",
            results = toolCommands.Select(command => new
            {
                command,
                name = command,
                description = $"Description for {command}",
            }),
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        var tools = root.GetProperty("results")
            .EnumerateArray()
            .Select(tool => new CliTool(
                tool.GetProperty("command").GetString() ?? string.Empty,
                tool.GetProperty("name").GetString() ?? string.Empty,
                tool.GetProperty("description").GetString(),
                tool.Clone()))
            .ToArray();

        return new CliMetadataSnapshot(Path.Combine(Path.GetTempPath(), $"cli-output-{Guid.NewGuid():N}.json"), root, tools);
    }
}
