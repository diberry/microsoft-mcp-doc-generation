using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class NamespaceStepTests
{
    [Fact]
    public async Task Step1_AnnotationsParametersRaw_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            SeedFile(Path.Combine(context.OutputPath, "annotations", "compute-list-annotations.md"));
            SeedFile(Path.Combine(context.OutputPath, "parameters", "compute-list-parameters.md"));
            SeedFile(Path.Combine(context.OutputPath, "tools-raw", "compute-list.md"));

            var step = new AnnotationsParametersRawStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(3, runner.Invocations.Count);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "generate-docs");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--annotations");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--no-build");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("cli-output-single-tool.json", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "--parameters");
            Assert.Contains(runner.Invocations[2].Arguments, argument => argument.EndsWith("ToolGeneration_Raw.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[2].Arguments, argument => argument == Path.Combine(context.OutputPath, "tools-raw"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_UsesGeneratorAndValidatorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";

            SeedFile(Path.Combine(context.OutputPath, "example-prompts", "compute-list-example-prompts.md"));
            SeedFile(Path.Combine(context.OutputPath, "example-prompts-prompts", "compute-list-input-prompt.md"));
            SeedFile(Path.Combine(context.OutputPath, "example-prompts-raw-output", "compute-list-raw-output.txt"));
            SeedFile(Path.Combine(context.OutputPath, "e2e-test-prompts", "parsed.json"), "{}");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("ExamplePromptGeneratorStandalone.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--e2e-prompts");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--param-manifests");
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument.EndsWith("ExamplePromptValidator.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "--tool-command");
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "compute list");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_ToolGeneration_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            SeedFile(Path.Combine(context.OutputPath, "tools-raw", "compute-list.md"));
            SeedFile(Path.Combine(context.OutputPath, "annotations", "compute-list-annotations.md"));
            SeedFile(Path.Combine(context.OutputPath, "parameters", "compute-list-parameters.md"));
            SeedFile(Path.Combine(context.OutputPath, "example-prompts", "compute-list-example-prompts.md"));
            SeedFile(Path.Combine(context.OutputPath, "tools-composed", "compute-list.md"));
            SeedFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"));

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("ToolGeneration_Composed.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == Path.Combine(context.OutputPath, "tools-raw"));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == Path.Combine(context.OutputPath, "example-prompts"));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument.EndsWith("ToolGeneration_Improved.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "8000");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step5_SkillsRelevance_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            var skillsOutputDirectory = Path.Combine(context.OutputPath, "skills-relevance");
            SeedFile(Path.Combine(skillsOutputDirectory, "compute-skills-relevance.md"));

            var step = new SkillsRelevanceStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(runner.Invocations);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("SkillsRelevance.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "compute");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--output-path");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == skillsOutputDirectory);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--min-score");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "0.1");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step5_SkillsRelevance_FailureReturnsWarningsWithoutThrowing()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(7, "GitHub API rate limited");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            var step = new SkillsRelevanceStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Single(runner.Invocations);
            Assert.Contains(result.Warnings, warning => warning.Contains("Skills relevance generation failed (exit code 7).", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("GitHub API rate limited", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_HorizontalArticles_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");
            SeedFile(Path.Combine(context.OutputPath, "horizontal-articles", "horizontal-article-compute.md"));

            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(runner.Invocations);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("HorizontalArticleGenerator.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--single-service");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "compute");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--output-path");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == context.OutputPath);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--transform");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static PipelineContext CreateContext(string testRoot, IProcessRunner processRunner, bool skipValidation, IReadOnlyList<string> toolCommands)
    {
        var docsGenerationRoot = Path.Combine(testRoot, "docs-generation");
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(docsGenerationRoot);
        Directory.CreateDirectory(outputPath);

        return new PipelineContext
        {
            Request = new PipelineRequest("compute", [1], outputPath, SkipBuild: true, SkipValidation: skipValidation, DryRun: false),
            RepoRoot = testRoot,
            DocsGenerationRoot = docsGenerationRoot,
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
        var root = Path.Combine(Path.GetTempPath(), $"pipeline-runner-step-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void SeedFile(string path, string content = "content")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void DeleteTestRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class FailingProcessRunner : IProcessRunner
    {
        private readonly int _exitCode;
        private readonly string _standardError;

        public FailingProcessRunner(int exitCode, string standardError)
        {
            _exitCode = exitCode;
            _standardError = standardError;
        }

        public List<ProcessSpec> Invocations { get; } = new();

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            Invocations.Add(spec);
            return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, _exitCode, string.Empty, _standardError, TimeSpan.Zero));
        }

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
            => RunAsync(
                new ProcessSpec(
                    "dotnet",
                    ["build", solutionPath, "--configuration", "Release", "--verbosity", "quiet"],
                    Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory),
                cancellationToken);

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
    }
}
