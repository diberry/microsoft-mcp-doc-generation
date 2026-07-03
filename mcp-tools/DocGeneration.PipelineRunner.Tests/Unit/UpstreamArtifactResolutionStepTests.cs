using System.Text.Json;
using HorizontalArticleGenerator.Models;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Shared;
using ToolGeneration_Improved.Models;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public sealed class UpstreamArtifactResolutionStepTests
{
    [Fact]
    public async Task Step3_UsesEnvelopeResolvedPrerequisites_WhenLegacyDirectoriesAreMissing()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["keyvault list"]);
            context.Items["Namespace"] = "keyvault";

            var nameContext = await FileNameContext.CreateAsync();
            var toolFileName = ToolFileNameBuilder.BuildToolFileName("keyvault list", nameContext);
            var annotationFileName = ToolFileNameBuilder.BuildAnnotationFileName("keyvault list", nameContext);
            var parameterFileName = ToolFileNameBuilder.BuildParameterFileName("keyvault list", nameContext);
            var examplePromptsFileName = ToolFileNameBuilder.BuildExamplePromptsFileName("keyvault list", nameContext);

            SeedFile(Path.Combine(context.OutputPath, "custom-upstream", "tools-raw", toolFileName));
            SeedFile(Path.Combine(context.OutputPath, "custom-upstream", "annotations", annotationFileName));
            SeedFile(Path.Combine(context.OutputPath, "custom-upstream", "parameters", parameterFileName));
            SeedFile(Path.Combine(context.OutputPath, "custom-upstream", "example-prompts", examplePromptsFileName));

            WriteEnvelope(
                context.OutputPath,
                1,
                "generate-annotations-parameters-and-raw-tools",
                "keyvault",
                [
                    "custom-upstream/tools-raw/" + toolFileName,
                    "custom-upstream/annotations/" + annotationFileName,
                    "custom-upstream/parameters/" + parameterFileName
                ]);

            WriteEnvelope(
                context.OutputPath,
                2,
                "generate-example-prompts",
                "keyvault",
                ["custom-upstream/example-prompts/" + examplePromptsFileName]);

            var invocationCount = 0;
            runner.OnRun = spec =>
            {
                invocationCount++;
                Assert.Contains(Path.Combine(context.OutputPath, "custom-upstream", "tools-raw"), spec.Arguments);
                Assert.Contains(Path.Combine(context.OutputPath, "custom-upstream", "annotations"), spec.Arguments);
                Assert.Contains(Path.Combine(context.OutputPath, "custom-upstream", "parameters"), spec.Arguments);
                Assert.Contains(Path.Combine(context.OutputPath, "custom-upstream", "example-prompts"), spec.Arguments);
                SeedFile(Path.Combine(context.OutputPath, "tools-composed", toolFileName));
                return CallbackProcessRunner.Success(spec);
            };

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(runner.Invocations);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_UsesEnvelopeResolvedToolDirectory_WhenLegacyDirectoriesAreMissing()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";

            var customToolPath = Path.Combine(context.OutputPath, "custom-step3", "tools", "compute-list.md");
            SeedToolFile(customToolPath, "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            WriteEnvelope(
                context.OutputPath,
                3,
                "compose-and-improve-tool-files",
                "compute",
                ["custom-step3/tools/compute-list.md"]);

            var outputFileName = await ToolFileNameBuilder.ResolveFamilyFileNameAsync("compute");
            runner.OnRun = spec =>
            {
                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", $"{outputFileName}-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", $"{outputFileName}-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", $"{outputFileName}.md"), "final article");
                return CallbackProcessRunner.Success(spec);
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(runner.Invocations);
            Assert.Equal("final article", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_UsesEnvelopeResolvedCliVersion_WhenLegacyPathIsMissing()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["advisor list"]);
            context.Items["Namespace"] = "advisor";

            SeedFile(Path.Combine(context.OutputPath, "custom-bootstrap", "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");
            SeedFile(
                Path.Combine(context.OutputPath, "cli", "cli-output.json"),
                JsonSerializer.Serialize(new
                {
                    results = new[]
                    {
                        new { command = "advisor list", name = "advisor list", description = "List advisor recommendations." }
                    }
                }));

            WriteEnvelope(
                context.OutputPath,
                0,
                "bootstrap-pipeline",
                "advisor",
                ["custom-bootstrap/cli/cli-version.json"]);

            context.Items[HorizontalArticlesStep.ArticleOutlineOverrideKey] =
                (Func<ArticleOutlineContext, CancellationToken, Task<string>>)((_, _) => Task.FromResult("article"));

            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Empty(runner.Invocations);
            Assert.Equal("article", File.ReadAllText(Path.Combine(context.OutputPath, "horizontal-articles", "horizontal-article-advisor.md")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static void WriteEnvelope(string outputPath, int stepId, string stepSlug, string currentNamespace, IReadOnlyList<string> artifactPaths)
    {
        var stepDir = Path.Combine(outputPath, $"step-{stepId}-{stepSlug}");
        StepResultWriter.Write(stepDir, new StepResultFile
        {
            SchemaVersion = "1.0",
            Status = StepResultStatus.Success,
            Step = $"Step {stepId}",
            StepName = $"step-{stepId}-{stepSlug}",
            Namespace = currentNamespace,
            OutputArtifacts = artifactPaths
                .Select(path => new ArtifactReference { Path = path.Replace('\\', '/'), Sha256 = "abc123" })
                .ToList()
        });
    }

    private static PipelineContext CreateContext(string testRoot, IProcessRunner processRunner, bool skipValidation, IReadOnlyList<string> toolCommands)
    {
        var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, "generated-output");
        Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "data"));
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json"), "[]");

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

    private static string CreateTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"upstream-resolution-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTestRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void SeedFile(string path, string content = "content")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void SeedToolFile(string path, string command)
        => SeedFile(path, $"---\n---\n# Sample\n\n<!-- @mcpcli {command} -->\nbody\n");

    private sealed class CallbackProcessRunner : IProcessRunner
    {
        public List<ProcessSpec> Invocations { get; } = new();

        public Func<ProcessSpec, ProcessExecutionResult>? OnRun { get; set; }

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            Invocations.Add(spec);
            return ValueTask.FromResult(OnRun?.Invoke(spec) ?? Success(spec));
        }

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
            => RunAsync(new ProcessSpec("dotnet", ["build", solutionPath], Path.GetDirectoryName(solutionPath) ?? string.Empty), cancellationToken);

        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken cancellationToken)
        {
            var invocation = new List<string>
            {
                "run",
                "--project",
                projectPath,
                "--configuration",
                "Release",
            };

            if (noBuild)
            {
                invocation.Add("--no-build");
            }

            invocation.Add("--");
            invocation.AddRange(arguments);
            return RunAsync(new ProcessSpec("dotnet", invocation, workingDirectory), cancellationToken);
        }

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken cancellationToken)
            => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), cancellationToken);

        public static ProcessExecutionResult Success(ProcessSpec spec)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero);
    }
}
