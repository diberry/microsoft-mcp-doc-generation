using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Shared;
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

            // Resolve brand-mapped output filename the same way ToolFamilyCleanupStep does,
            // so the callback creates files the step will find (PR #316 changed expected filenames).
            var outputFileName = await ToolFileNameBuilder.ResolveFamilyFileNameAsync("compute");

            processRunner.OnRun = spec =>
            {
                Assert.NotEqual(context.McpToolsRoot, spec.WorkingDirectory);
                Assert.Contains("pipeline-runner-step4", spec.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
                Assert.False(File.Exists(Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md")));

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
            Assert.Single(processRunner.Invocations);
            Assert.Equal("metadata", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family-metadata", $"{outputFileName}-metadata.md")));
            Assert.Equal("related", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family-related", $"{outputFileName}-related.md")));
            Assert.Equal("final article", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md")));
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
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", "azure-cosmos-db-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", "azure-cosmos-db-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", "azure-cosmos-db.md"), "final article");

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

    [Fact]
    public async Task Step4_ExitZeroButNoOutputFiles_FailsWithSubprocessOutput()
    {
        // Reproduces #160: subprocess exits 0 (AI exception swallowed) but produces no output files.
        // Before fix: step reported "Expected isolated output" with no explanation.
        // After fix: step surfaces the "✗ Failed" lines from subprocess stdout.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner
            {
                OnRun = spec =>
                {
                    // Subprocess exits 0 but writes NO output files (AI generation failed silently)
                    var stdout = "[1/1] Processing family: search (6 tools)...\n" +
                                 "[1/1]   Phase 2: Generating metadata... \n" +
                                 "[1/1] ✗ Failed to process search: Rate limit exceeded after 5 retries\n" +
                                 "\n=== Summary ===\nFailed:           1\n";
                    return CallbackProcessRunner.SuccessWithOutput(spec, stdout);
                },
            };

            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            // Should surface the failure reason from subprocess stdout
            Assert.Contains(result.Warnings, w => w.Contains("✗") || w.Contains("Failed"));
            // Should NOT just say "Expected isolated output" without context
            Assert.Contains(result.Warnings, w => w.Contains("Subprocess output"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_ExitZeroNoOutputNoErrorLines_ShowsGenericMessage()
    {
        // Edge case: subprocess exits 0, no output files, stdout has no error indicators
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner
            {
                OnRun = spec =>
                {
                    // Subprocess exits 0 with clean-looking output but no files written
                    return CallbackProcessRunner.SuccessWithOutput(spec, "Azure MCP Tool Family Cleanup\n============================\n");
                },
            };

            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            // Should show a helpful generic message when no error lines found
            Assert.Contains(result.Warnings, w => w.Contains("produced no output files"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_ExitZeroNoOutputEmptyStdout_ShowsGenericMessage()
    {
        // Edge case from code review: subprocess stdout is completely empty.
        // Generic diagnostic must still appear even when there's nothing to parse.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner
            {
                OnRun = spec => CallbackProcessRunner.Success(spec), // Success returns empty stdout
            };

            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("produced no output files"));
            Assert.Contains(result.Warnings, w => w.Contains("Expected isolated"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }


    [Fact]
    public async Task Step4_UsesBrandMappedOutputFilename_WhenBrandMappingExists()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContextWithBrandMapping(testRoot, processRunner);
            context.Items["Namespace"] = "compute";

            SeedToolFile(Path.Combine(context.OutputPath, "tools", "azure-compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "azure-compute-show.md"), "compute show");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            processRunner.OnRun = spec =>
            {
                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", "azure-compute-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", "azure-compute-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", "azure-compute.md"), "final article");
                return CallbackProcessRunner.Success(spec);
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(context.OutputPath, "tool-family", "azure-compute.md")));
            Assert.Equal("final article", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family", "azure-compute.md")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_CleansStaleFilesWithOldNaming_WhenBrandMappedFileGenerated()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContextWithBrandMapping(testRoot, processRunner);
            context.Items["Namespace"] = "compute";

            SeedToolFile(Path.Combine(context.OutputPath, "tools", "azure-compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), "stale old content");

            processRunner.OnRun = spec =>
            {
                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", "azure-compute-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", "azure-compute-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", "azure-compute.md"), "new article");
                return CallbackProcessRunner.Success(spec);
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(context.OutputPath, "tool-family", "azure-compute.md")));
            Assert.False(File.Exists(Path.Combine(context.OutputPath, "tool-family", "compute.md")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static PipelineContext CreateContextWithBrandMapping(string testRoot, IProcessRunner processRunner)
    {
        var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "data"));
        Directory.CreateDirectory(outputPath);
        var brandMappings = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { brandName = "Azure Compute", mcpServerName = "compute", shortName = "Compute", fileName = "azure-compute" }
        });
        File.WriteAllText(Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json"), brandMappings);
        var context = new PipelineContext
        {
            Request = new PipelineRequest("compute", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = testRoot,
            McpToolsRoot = mcpToolsRoot,
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
        return context;
    }

    private static PipelineContext CreateCosmosContext(string testRoot, IProcessRunner processRunner)
    {
        var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, "generated-cosmos");
        Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "data"));
        Directory.CreateDirectory(outputPath);
        
        // Seed brand mappings with cosmos entry for testing
        var brandMappings = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { brandName = "Azure Cosmos DB", mcpServerName = "cosmos", shortName = "Cosmos DB", fileName = "azure-cosmos-db" }
        });
        File.WriteAllText(Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json"), brandMappings);

        var context = new PipelineContext
        {
            Request = new PipelineRequest("cosmos", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = testRoot,
            McpToolsRoot = mcpToolsRoot,
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
        var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "data"));
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json"), "[]");

        return new PipelineContext
        {
            Request = new PipelineRequest("compute", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = testRoot,
            McpToolsRoot = mcpToolsRoot,
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

    // ========================================
    // NEW TESTS FOR ISSUE #478: Exit Code 1 Handling
    // ========================================

    [Fact]
    public async Task Cleanup_Succeeds_When_OutputFiles_Exist_Despite_NonZero_ExitCode()
    {
        // Reproduces #478: appservice and virtualdesktop reported exit code 1 but files were generated successfully.
        // Expected: Step should succeed because output files are what matter, not exit code.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var outputFileName = await ToolFileNameBuilder.ResolveFamilyFileNameAsync("compute");
            
            processRunner.OnRun = spec =>
            {
                // Subprocess exits with code 1 BUT produces all required output files
                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", $"{outputFileName}-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", $"{outputFileName}-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", $"{outputFileName}.md"), "final article");
                
                // Return exit code 1 despite successful file generation
                return CallbackProcessRunner.Failure(spec, 1, "Warning: Some non-critical issue occurred");
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            // CRITICAL: Should succeed because all output files exist
            Assert.True(result.Success, "Step should succeed when output files exist despite exit code 1");
            Assert.Equal("metadata", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family-metadata", $"{outputFileName}-metadata.md")));
            Assert.Equal("related", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family-related", $"{outputFileName}-related.md")));
            Assert.Equal("final article", File.ReadAllText(Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md")));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Cleanup_Fails_When_No_OutputFiles_Generated()
    {
        // Reproduces #478: functions namespace - exit code 1 AND no tool-family file generated.
        // Expected: Step should fail with clear error message.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner
            {
                OnRun = spec =>
                {
                    // Subprocess exits 1 and produces NO output files
                    return CallbackProcessRunner.Failure(spec, 1, "AI generation failed completely");
                }
            };

            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success, "Step should fail when no output files generated");
            Assert.Contains(result.Warnings, w => w.Contains("Tool-family cleanup failed"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Cleanup_Fails_When_Partial_OutputFiles_Generated()
    {
        // Reproduces edge case: subprocess produces metadata and related but NOT the final tool-family file.
        // Expected: Step should fail, listing which specific file is missing.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var outputFileName = await ToolFileNameBuilder.ResolveFamilyFileNameAsync("compute");
            
            processRunner.OnRun = spec =>
            {
                // Subprocess exits 1 and produces ONLY metadata and related (missing final file)
                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", $"{outputFileName}-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", $"{outputFileName}-related.md"), "related");
                // MISSING: tool-family final file NOT created
                
                return CallbackProcessRunner.Failure(spec, 1, "Partial failure during generation");
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success, "Step should fail when final tool-family file is missing");
            // Check for the actual warning - it should detect missing files
            var hasFileWarning = result.Warnings.Any(w => w.Contains("Expected isolated") || w.Contains("tool-family"));
            Assert.True(hasFileWarning, $"Should report missing file. Actual warnings: {string.Join(", ", result.Warnings)}");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Cleanup_Reports_Missing_Files_Diagnostics()
    {
        // When files are missing, error should surface subprocess stdout/stderr for debugging.
        // Expected: Warning messages include relevant subprocess output.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner
            {
                OnRun = spec =>
                {
                    // Subprocess exits 1 with diagnostic output but no files
                    var stdout = "[1/1] Processing family: compute (3 tools)...\n" +
                                 "[1/1]   Phase 1: Generating metadata... ✓\n" +
                                 "[1/1]   Phase 2: Generating related content... ✗ Failed\n" +
                                 "[1/1] ✗ Failed to process compute: AI rate limit exceeded\n";
                    return new ProcessExecutionResult(
                        spec.FileName, 
                        spec.Arguments, 
                        spec.WorkingDirectory, 
                        1, 
                        stdout, 
                        "Error: rate limit", 
                        TimeSpan.Zero);
                }
            };

            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            // Should surface subprocess diagnostic output
            Assert.Contains(result.Warnings, w => w.Contains("Tool-family cleanup failed"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ========================================
    // END NEW TESTS FOR ISSUE #478
    // ========================================

    // ========================================
    // NEW TESTS FOR ISSUES #602 AND #603
    // ========================================

    [Fact]
    public async Task Step4_FallsBackToToolsRaw_WhenToolsDirectoryAbsent_Bug602()
    {
        // Reproduces #602: When Step 3 is skipped, tools/ doesn't exist.
        // Step 4 should fall back to tools-raw/ to enable structural validation without AI steps.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";

            // Only seed tools-raw/, NOT tools/
            SeedToolFile(Path.Combine(context.OutputPath, "tools-raw", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools-raw", "compute-show.md"), "compute show");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var outputFileName = await ToolFileNameBuilder.ResolveFamilyFileNameAsync("compute");

            processRunner.OnRun = spec =>
            {
                // Verify tools were staged from tools-raw/
                var tempToolsDir = Path.Combine(Path.GetDirectoryName(spec.WorkingDirectory!)!, "generated", "tools");
                Assert.True(Directory.Exists(tempToolsDir), "Tools dir should be created in isolated workspace");
                Assert.True(Directory.GetFiles(tempToolsDir, "*.md").Length > 0, "Tool files should be staged from tools-raw/");

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

            Assert.True(result.Success, $"Step should succeed using tools-raw/ fallback. Warnings: {string.Join(", ", result.Warnings)}");
            Assert.Single(processRunner.Invocations);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_FallsBackToToolsRaw_WhenToolsDirectoryEmpty_Bug602()
    {
        // When tools/ exists but is empty (Step 3 produced no output), fall back to tools-raw/.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";

            // Create empty tools/ directory
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "tools"));
            // Seed tools-raw/ with actual files
            SeedToolFile(Path.Combine(context.OutputPath, "tools-raw", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var outputFileName = await ToolFileNameBuilder.ResolveFamilyFileNameAsync("compute");

            processRunner.OnRun = spec =>
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

            Assert.True(result.Success, $"Step should succeed using tools-raw/ fallback when tools/ is empty. Warnings: {string.Join(", ", result.Warnings)}");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_Fails_WhenBothToolsAndToolsRawAbsent_Bug602()
    {
        // When neither tools/ nor tools-raw/ exist, fail with a clear error message.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            var context = CreateContext(testRoot, processRunner);
            context.Items["Namespace"] = "compute";

            // Neither tools/ nor tools-raw/ exist
            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Tools directory not found"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_UsesDecomposedNamespace_AsFamilyName_Bug603()
    {
        // Reproduces #603: for namespace "extension_azqr", ResolveFamilyName should return
        // "extension_azqr" (the raw namespace) rather than "extension" (tokens[0] of CLI command).
        // This test verifies the step finds files correctly when namespace contains an underscore.
        var testRoot = CreateTestRoot();
        try
        {
            var processRunner = new CallbackProcessRunner();
            // Use a context with namespace "extension_azqr" but CLI command prefix "extension"
            var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
            var outputPath = Path.Combine(testRoot, "generated-extension_azqr");
            Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "data"));
            Directory.CreateDirectory(outputPath);

            // Brand mapping: key is "extension_azqr"
            File.WriteAllText(Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json"),
                """[{"mcpServerName":"extension_azqr","brandName":"Azure Extension AZQR","shortName":"AZQR","fileName":"azure-extension-azqr"}]""");

            var context = new PipelineContext
            {
                Request = new PipelineRequest("extension_azqr", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
                RepoRoot = testRoot,
                McpToolsRoot = mcpToolsRoot,
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
                CliOutput = CreateSnapshot(["extension azqr scan", "extension azqr list"]),
                SelectedNamespaces = ["extension_azqr"],
            };
            context.Items["Namespace"] = "extension_azqr";

            // Seed tool files annotated with the CLI command prefix "extension" (what the CLI emits)
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "azure-extension-azqr-scan.md"), "extension azqr scan");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "azure-extension-azqr-list.md"), "extension azqr list");
            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            processRunner.OnRun = spec =>
            {
                var isolatedGeneratedRoot = Path.GetFullPath(Path.Combine(spec.WorkingDirectory, "..", "generated"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family-related"));
                Directory.CreateDirectory(Path.Combine(isolatedGeneratedRoot, "tool-family"));
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-metadata", "azure-extension-azqr-metadata.md"), "metadata");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family-related", "azure-extension-azqr-related.md"), "related");
                File.WriteAllText(Path.Combine(isolatedGeneratedRoot, "tool-family", "azure-extension-azqr.md"), "final article");

                return CallbackProcessRunner.Success(spec);
            };

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success, $"Step should succeed for decomposed namespace 'extension_azqr'. Warnings: {string.Join(", ", result.Warnings)}");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ========================================
    // END NEW TESTS FOR ISSUES #602 AND #603
    // ========================================

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

        public static ProcessExecutionResult SuccessWithOutput(ProcessSpec spec, string standardOutput)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, standardOutput, string.Empty, TimeSpan.Zero);

        public static ProcessExecutionResult Failure(ProcessSpec spec, int exitCode, string standardError)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, exitCode, string.Empty, standardError, TimeSpan.Zero);
    }
}
