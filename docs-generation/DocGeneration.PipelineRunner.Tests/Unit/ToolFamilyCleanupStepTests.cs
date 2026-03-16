using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ToolFamilyCleanupStepTests
{
    [Fact]
    public async Task Step4_UsesIsolatedWorkspaceAndCopiesOutputsBackOnSuccess()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";

            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute show");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            processRunner.OnRun = spec =>
            {
                Assert.NotEqual(context.DocsGenerationRoot, spec.WorkingDirectory);
                Assert.Contains("pipeline-runner-step4", spec.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
                Assert.False(File.Exists(Path.Combine(context.OutputPath, "tool-family", "compute.md")));

                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", "compute-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", "compute-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", "compute.md"), "final article");

                return CallbackProcessRunner.Success(spec);
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(processRunner.Invocations);
            Assert.Equal("metadata", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family-metadata", "compute-metadata.md")));
            Assert.Equal("related", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family-related", "compute-related.md")));
            Assert.Equal("final article", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family", "compute.md")));
            Assert.False(Directory.Exists(processRunner.Invocations[0].WorkingDirectory));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_GeneratorFailureDoesNotCopyOutputsBack()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner
            {
                OnRun = spec =>
                {
                    var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                    Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                    File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", "compute.md"), "corrupted");
                    return CallbackProcessRunner.Failure(spec, 1, "cleanup failed");
                },
            };

            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, warning => warning.Contains("Tool-family cleanup failed", StringComparison.Ordinal));
            Assert.False(File.Exists(Path.Combine(context.OutputPath, "tool-family", "compute.md")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_MergesAnnotationAndPrefixMatches_WhenToolsHaveMixedAnnotations()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateCosmosContext(testRoot, processRunner);

            // Create tools with MIXED annotations:
            // 1. Tool WITH @mcpcli annotation → should match by content
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "azure-cosmos-db-list.md"), "cosmos list");
            
            // 2. Tool WITHOUT @mcpcli annotation → should match by prefix (via brand-to-server-mapping.json)
            var pathNoAnnotation = Path.Combine(context.OutputPath, "tools", "azure-cosmos-db-database-container-item-query.md");
            Directory.CreateDirectory(Path.GetDirectoryName(pathNoAnnotation)!);
            File.WriteAllText(pathNoAnnotation, "---\n---\n# Database Container Item Query\n\nNo annotation here\n");
            
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            int toolFileCount = 0;
            processRunner.OnRun = spec =>
            {
                // Count tool files that were copied to the isolated workspace
                var tempToolsDir = Path.Combine(Path.GetDirectoryName(spec.WorkingDirectory!)!, "generated", "tools");
                if (Directory.Exists(tempToolsDir))
                {
                    toolFileCount = Directory.GetFiles(tempToolsDir, "*.md").Length;
                }

                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", "cosmos-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", "cosmos-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", "cosmos.md"), "final article");

                return CallbackProcessRunner.Success(spec);
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            
            // CRITICAL ASSERTION: Both tools should be included (1 via annotation match, 1 via prefix match)
            Assert.Equal(2, toolFileCount);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static PipelineContext CreateCosmosContext(string testRoot, IProcessRunner processRunner)
    {
        var docsGenerationRoot = Path.Combine(testRoot, "docs-generation");
        var outputPath = Path.Combine(testRoot, "generated-cosmos");
        Directory.CreateDirectory(Path.Combine(docsGenerationRoot, "data"));
        Directory.CreateDirectory(outputPath);
        
        // Seed brand mappings with cosmos entry for testing
        var brandMappings = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { brandName = "Azure Cosmos DB", mcpServerName = "cosmos", shortName = "Cosmos DB", fileName = "azure-cosmos-db" }
        });
        File.WriteAllText(Path.Combine(docsGenerationRoot, "data", "brand-to-server-mapping.json"), brandMappings);

        var context = new PipelineContext
        {
            Request = new PipelineRequest("cosmos", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = testRoot,
            DocsGenerationRoot = docsGenerationRoot,
            OutputPath = outputPath,
            ProcessRunner = processRunner,
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = new StubCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new StubFilteredCliWriter(),
            BuildCoordinator = new StubBuildCoordinator(),
            AiCapabilityProbe = new StubAiCapabilityProbe(),
            Reports = new BufferedReportWriter(),
            CliVersion = "1.2.3",
            CliOutput = CreateSnapshot(["cosmos list", "cosmos database container item query"]),
            SelectedNamespaces = ["cosmos"],
        };
        
        // Set the namespace in the Items dictionary (required by NamespaceStepBase)
        context.Items["Namespace"] = "cosmos";
        
        return context;
    }

    private static PipelineContext CreateContext(string testRoot, IProcessRunner processRunner)
    {
        var docsGenerationRoot = Path.Combine(testRoot, "docs-generation");
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(Path.Combine(docsGenerationRoot, "data"));
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(docsGenerationRoot, "data", "brand-to-server-mapping.json"), "[]");

        return new PipelineContext
        {
            Request = new PipelineRequest("compute", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = testRoot,
            DocsGenerationRoot = docsGenerationRoot,
            OutputPath = outputPath,
            ProcessRunner = processRunner,
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = new StubCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new StubFilteredCliWriter(),
            BuildCoordinator = new StubBuildCoordinator(),
            AiCapabilityProbe = new StubAiCapabilityProbe(),
            Reports = new BufferedReportWriter(),
            CliVersion = "1.2.3",
            CliOutput = CreateSnapshot(["compute list", "compute show"]),
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

    private static void SeedToolFile(string path, string command)
        => SeedFile(path, $"---\n---\n# Sample\n\n<!-- @mcpcli {command} -->\nbody\n");

    private static void SeedFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pipeline-runner-step4-tests-{Guid.NewGuid():N}");
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

        public static ProcessExecutionResult Failure(ProcessSpec spec, int exitCode, string standardError)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, exitCode, string.Empty, standardError, TimeSpan.Zero);
    }
}
