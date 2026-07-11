using System.Reflection;
using System.Text.Json;
using HorizontalArticleGenerator.Models;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Shared;
using Shared.Validation;
using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Validation;
using ToolGeneration_Improved.Models;
using ToolGeneration_Improved.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public sealed class PreAiGateRealStepTests
{
    [Fact]
    public async Task Step3_RealToolGenerationBudgetGate_RecordsValidatorResultAndSkipsToolImprover()
    {
        var testRoot = CreateTestRoot(nameof(Step3_RealToolGenerationBudgetGate_RecordsValidatorResultAndSkipsToolImprover));
        try
        {
            const string command = "storage account list";
            var processRunner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, "storage", 3, processRunner, [command]);
            var toolImproverInvoked = false;
            context.Items[ToolGenerationStep.ToolImproverOverrideKey] =
                (Func<ToolGenerationContext, CancellationToken, Task<ImprovedToolData>>)((toolContext, _) =>
                {
                    toolImproverInvoked = true;
                    return Task.FromResult(new ImprovedToolData
                    {
                        FileName = toolContext.ToolName,
                        OriginalContent = toolContext.ComposedContent,
                        ImprovedContent = toolContext.ComposedContent,
                        WasImproved = false
                    });
                });

            var fileNames = await ResolveToolFileNamesAsync(command);
            SeedStep3Prerequisites(context.OutputPath, command, fileNames);
            SeedFile(
                Path.Combine(context.OutputPath, "tools-composed", fileNames.ToolFileName),
                $"<!-- @mcpcli {command} -->{Environment.NewLine}## Storage account inventory{Environment.NewLine}{new string('x', 400_050)}");

            var result = await new ToolGenerationStep().ExecuteAsync(context, CancellationToken.None);

            Assert.Contains(result.Warnings, warning => warning.Contains("Pre-AI validation failed for", StringComparison.Ordinal));
            Assert.Contains(
                result.ValidatorResults,
                validator => validator.Name == "pre-ai-validation" && !validator.Success);
            Assert.False(toolImproverInvoked, "Tool improver override must not run when the real step-3 pre-AI gate fails.");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_RealArticleOutlineBudgetGate_RecordsValidatorResultAndSkipsRenderer()
    {
        var testRoot = CreateTestRoot(nameof(Step6_RealArticleOutlineBudgetGate_RecordsValidatorResultAndSkipsRenderer));
        try
        {
            var oversizedDescription = new string('m', 310_000);
            var commands = new[] { "monitor alert list", "monitor metrics list" };
            var processRunner = new RecordingProcessRunner();
            var context = CreateContext(
                testRoot,
                "monitor",
                6,
                processRunner,
                commands,
                command => $"{command} {oversizedDescription}");
            var rendererInvoked = false;
            context.Items[HorizontalArticlesStep.ArticleOutlineOverrideKey] =
                (Func<ArticleOutlineContext, CancellationToken, Task<string>>)((_, _) =>
                {
                    rendererInvoked = true;
                    return Task.FromResult("# Monitor article");
                });

            SeedCliVersion(context.OutputPath);
            SeedCliOutputFile(
                context.OutputPath,
                commands.Select(command => (Command: command, Description: $"{command} {oversizedDescription}")));

            var result = await new HorizontalArticlesStep().ExecuteAsync(context, CancellationToken.None);

            Assert.Contains(
                result.ValidatorResults,
                validator => validator.Name == "pre-ai-validation" && !validator.Success);
            Assert.False(rendererInvoked, "Article renderer override must not run when the real step-6 pre-AI gate fails.");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_RealFamilyCleanupGate_AggregatesInjectedValidatorAndSkipsRenderer()
    {
        var testRoot = CreateTestRoot(nameof(Step4_RealFamilyCleanupGate_AggregatesInjectedValidatorAndSkipsRenderer));
        try
        {
            const string injectedError = "Injected Key Vault family structure failure.";
            var processRunner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, "keyvault", 4, processRunner, ["keyvault secret list"]);
            var cleanupInvoked = false;
            context.Items[ToolFamilyCleanupStep.PreAiValidatorOverrideKey] =
                new AlwaysFailFamilyStructureValidator(injectedError);
            context.Items[ToolFamilyCleanupStep.FamilyCleanupOverrideKey] =
                (Func<FamilyStructureContext, CancellationToken, Task<ToolFamilyCleanupStep.FamilyCleanupArtifacts>>)((structure, _) =>
                {
                    cleanupInvoked = true;
                    return Task.FromResult(new ToolFamilyCleanupStep.FamilyCleanupArtifacts(
                        $"metadata for {structure.FamilyName}",
                        "related content",
                        "final content"));
                });

            var fileNames = await ResolveToolFileNamesAsync("keyvault secret list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", fileNames.ToolFileName), "keyvault secret list");
            SeedCliVersion(context.OutputPath);

            var result = await new ToolFamilyCleanupStep().ExecuteAsync(context, CancellationToken.None);

            Assert.Contains(
                result.ValidatorResults,
                validator => validator.Name == "pre-ai-validation"
                    && !validator.Success
                    && validator.Warnings.Any(message => message.Contains(injectedError, StringComparison.Ordinal)));
            Assert.False(cleanupInvoked, "Family cleanup override must not run when the real step-4 pre-AI gate fails.");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Step4_RegisteredPreAiValidators_IncludeFamilyStructureContextValidator()
    {
        var reducersField = typeof(ToolFamilyCleanupStep).GetField("Reducers", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(reducersField);
        var reducers = Assert.IsType<ReducerRegistry>(reducersField!.GetValue(null));

        Assert.Contains(
            reducers.GetValidators<FamilyStructureContext>(),
            validator => validator.GetType() == typeof(FamilyStructureContextValidator));
    }

    private static PipelineContext CreateContext(
        string testRoot,
        string serviceNamespace,
        int stepId,
        IProcessRunner processRunner,
        IReadOnlyList<string> toolCommands,
        Func<string, string>? describe = null)
    {
        var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, $"generated-{serviceNamespace}");
        Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "data"));
        Directory.CreateDirectory(outputPath);
        SeedFile(Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json"), "[]");

        var context = new PipelineContext
        {
            Request = new PipelineRequest(serviceNamespace, [stepId], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
            RepoRoot = testRoot,
            McpToolsRoot = mcpToolsRoot,
            OutputPath = outputPath,
            ProcessRunner = processRunner,
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = new StubCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new LocalFilteredCliWriter(testRoot),
            BuildCoordinator = new StubBuildCoordinator(),
            AiCapabilityProbe = new StubAiCapabilityProbe(),
            Reports = new BufferedReportWriter(),
            CliVersion = "1.2.3",
            CliOutput = CreateSnapshot(testRoot, toolCommands, describe),
            SelectedNamespaces = [serviceNamespace],
        };
        context.Items["Namespace"] = serviceNamespace;
        return context;
    }

    private static CliMetadataSnapshot CreateSnapshot(
        string testRoot,
        IReadOnlyList<string> toolCommands,
        Func<string, string>? describe)
    {
        var json = JsonSerializer.Serialize(new
        {
            version = "1.2.3",
            results = toolCommands.Select(command => new
            {
                command,
                name = command,
                description = describe?.Invoke(command) ?? $"Description for {command}",
                option = Array.Empty<object>()
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

        return new CliMetadataSnapshot(Path.Combine(testRoot, "cli-output-snapshot.json"), root, tools);
    }

    private static async Task<ToolFileNames> ResolveToolFileNamesAsync(string command)
    {
        var nameContext = await FileNameContext.CreateAsync();
        return new ToolFileNames(
            ToolFileNameBuilder.BuildToolFileName(command, nameContext),
            ToolFileNameBuilder.BuildAnnotationFileName(command, nameContext),
            ToolFileNameBuilder.BuildParameterFileName(command, nameContext),
            ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext));
    }

    private static void SeedStep3Prerequisites(string outputPath, string command, ToolFileNames fileNames)
    {
        SeedToolFile(Path.Combine(outputPath, "tools-raw", fileNames.ToolFileName), command);
        SeedFile(Path.Combine(outputPath, "annotations", fileNames.AnnotationFileName), $"<!-- @mcpcli {command} -->{Environment.NewLine}annotations");
        SeedFile(Path.Combine(outputPath, "parameters", fileNames.ParameterFileName), $"<!-- @mcpcli {command} -->{Environment.NewLine}parameters");
        SeedFile(Path.Combine(outputPath, "example-prompts", fileNames.ExamplePromptsFileName), $"<!-- @mcpcli {command} -->{Environment.NewLine}prompts");
    }

    private static void SeedCliVersion(string outputPath)
        => SeedFile(Path.Combine(outputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

    private static void SeedCliOutputFile(string outputPath, IEnumerable<(string Command, string Description)> tools)
        => SeedFile(
            Path.Combine(outputPath, "cli", "cli-output.json"),
            JsonSerializer.Serialize(new
            {
                results = tools.Select(tool => new
                {
                    command = tool.Command,
                    name = tool.Command,
                    description = tool.Description,
                    option = Array.Empty<object>()
                })
            }));

    private static void SeedToolFile(string path, string command)
        => SeedFile(path, $"---{Environment.NewLine}---{Environment.NewLine}# Sample{Environment.NewLine}{Environment.NewLine}<!-- @mcpcli {command} -->{Environment.NewLine}body{Environment.NewLine}");

    private static void SeedFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateTestRoot(string testName)
    {
        var root = Path.Combine(
            Directory.GetCurrentDirectory(),
            "test-artifacts",
            "pre-ai-gate-real-steps",
            $"{testName}-{Guid.NewGuid():N}");
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

    private sealed record ToolFileNames(
        string ToolFileName,
        string AnnotationFileName,
        string ParameterFileName,
        string ExamplePromptsFileName);

    private sealed class AlwaysFailFamilyStructureValidator(string message) : IPreAiValidator<FamilyStructureContext>
    {
        public Task<PreAiValidationResult> ValidateAsync(FamilyStructureContext context, CancellationToken cancellationToken)
            => Task.FromResult(PreAiValidationResult.Fail(new ValidationError("Injected", message, ValidationSeverity.Error)));

        public Task<PreAiValidationResult> ValidateAsync(object context, CancellationToken cancellationToken)
            => context is FamilyStructureContext familyContext
                ? ValidateAsync(familyContext, cancellationToken)
                : Task.FromResult(PreAiValidationResult.Pass());
    }

    private sealed class LocalFilteredCliWriter(string testRoot) : IFilteredCliWriter
    {
        public ValueTask<FilteredCliFileHandle> WriteAsync(
            CliMetadataSnapshot cliOutput,
            IReadOnlyList<CliTool> matchingTools,
            string tempDirectoryName,
            CancellationToken cancellationToken)
        {
            var directory = Path.Combine(testRoot, "filtered-cli", tempDirectoryName);
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, "cli-output.json");
            File.WriteAllText(filePath, cliOutput.RawRoot.GetRawText());
            return ValueTask.FromResult(new FilteredCliFileHandle(directory, filePath));
        }
    }
}
