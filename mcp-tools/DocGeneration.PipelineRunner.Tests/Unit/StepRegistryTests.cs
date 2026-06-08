using System.Text;
using PipelineRunner.Cli;
using PipelineRunner.Contracts;
using PipelineRunner.Context;
using PipelineRunner.Registry;
using PipelineRunner.Steps;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class StepRegistryTests
{
    [Fact]
    public void Constructor_DuplicateIds_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new StepRegistry([
            new FakeStep(1, "first"),
            new FakeStep(1, "duplicate"),
        ]));
    }

    [Fact]
    public void GetOrderedSteps_PrependsGlobalStepsBeforeRequestedNamespaceSteps()
    {
        var registry = new StepRegistry([
            new FakeStep(4, "fourth"),
            new FakeStep(0, "bootstrap", StepScope.Global),
            new FakeStep(2, "second"),
            new FakeStep(1, "first"),
        ]);

        var steps = registry.GetOrderedSteps([4, 1, 2]);

        Assert.Equal(new[] { 0, 1, 2, 4 }, steps.Select(step => step.Id));
    }

    [Fact]
    public void GetStep_KnownId_ReturnsRegisteredStep()
    {
        var registry = new StepRegistry([
            new FakeStep(2, "second"),
        ]);

        var step = registry.GetStep(2);

        Assert.Equal("second", step.Name);
    }

    [Fact]
    public void TryGetBySlug_StepNameSlug_ReturnsRegisteredStep()
    {
        var registry = new StepRegistry([
            new FakeStep(3, "Compose and improve tool files"),
        ]);

        var found = registry.TryGetBySlug("compose-and-improve-tool-files", out var step);

        Assert.True(found);
        Assert.NotNull(step);
        Assert.Equal(3, step!.Id);
    }

    [Fact]
    public void TryGetBySlug_TypeAlias_ReturnsRegisteredStep()
    {
        var registry = new StepRegistry([
            new ToolGenerationStep(),
        ]);

        var found = registry.TryGetBySlug("tool-generation", out var step);

        Assert.True(found);
        Assert.NotNull(step);
        Assert.Equal(3, step!.Id);
    }

    [Fact]
    public void CreateDefault_RegistersAllStandardStepsAsTypedImplementations()
    {
        var scriptsRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-scripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scriptsRoot);

        try
        {
            var registry = StepRegistry.CreateDefault(scriptsRoot);

            Assert.IsType<BootstrapStep>(registry.GetStep(0));
            Assert.IsType<AnnotationsParametersRawStep>(registry.GetStep(1));
            Assert.IsType<ExamplePromptsStep>(registry.GetStep(2));
            Assert.IsType<global::PipelineRunner.Steps.ToolGenerationStep>(registry.GetStep(3));
            Assert.IsType<ToolFamilyCleanupStep>(registry.GetStep(4));
            Assert.IsType<SkillsRelevanceStep>(registry.GetStep(5));
            Assert.IsType<HorizontalArticlesStep>(registry.GetStep(6));
            Assert.IsType<ArticleHealthValidatorStep>(registry.GetStep(7));
        }
        finally
        {
            Directory.Delete(scriptsRoot, recursive: true);
        }
    }

    [Fact]
    public void AllValidSteps_MatchesRegisteredSteps()
    {
        var scriptsRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-scripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scriptsRoot);

        try
        {
            var registry = StepRegistry.CreateDefault(scriptsRoot);
            var registeredSteps = registry.GetAllSteps().Select(step => step.Id).ToArray();

            Assert.Equal(registeredSteps, PipelineRequest.AllValidSteps);
        }
        finally
        {
            Directory.Delete(scriptsRoot, recursive: true);
        }
    }

    [Fact]
    public void ValidateAgainstConfig_ConfigMatchesRegistry_NoWarningEmitted()
    {
        var registry = new StepRegistry([
            new FakeStep(0, "bootstrap"),
            new FakeStep(1, "step-one"),
        ]);
        var configJson = """[{"id":0,"name":"bootstrap"},{"id":1,"name":"step-one"}]""";
        var configPath = WriteTempConfig(configJson);
        var writer = new StringWriter();

        StepRegistry.ValidateAgainstConfig(registry, configPath, writer);

        Assert.Empty(writer.ToString());
    }

    [Fact]
    public void ValidateAgainstConfig_ConfigHasExtraStepId_EmitsWarningForExtraId()
    {
        var registry = new StepRegistry([new FakeStep(1, "step-one")]);
        var configJson = """[{"id":1,"name":"step-one"},{"id":99,"name":"phantom"}]""";
        var configPath = WriteTempConfig(configJson);
        var writer = new StringWriter();

        StepRegistry.ValidateAgainstConfig(registry, configPath, writer);

        var output = writer.ToString();
        Assert.Contains("[WARN]", output);
        Assert.Contains("99", output);
        Assert.Contains("pipeline.config.json but not in registry", output);
    }

    [Fact]
    public void ValidateAgainstConfig_RegistryHasExtraStepId_EmitsWarningForExtraId()
    {
        var registry = new StepRegistry([
            new FakeStep(1, "step-one"),
            new FakeStep(99, "unlisted"),
        ]);
        var configJson = """[{"id":1,"name":"step-one"}]""";
        var configPath = WriteTempConfig(configJson);
        var writer = new StringWriter();

        StepRegistry.ValidateAgainstConfig(registry, configPath, writer);

        var output = writer.ToString();
        Assert.Contains("[WARN]", output);
        Assert.Contains("99", output);
        Assert.Contains("registry but not in pipeline.config.json", output);
    }

    [Fact]
    public void ValidateAgainstConfig_ConfigFileNotFound_EmitsFileNotFoundWarning()
    {
        var registry = new StepRegistry([new FakeStep(1, "step-one")]);
        var missingPath = Path.Combine(Path.GetTempPath(), $"no-such-config-{Guid.NewGuid():N}.json");
        var writer = new StringWriter();

        StepRegistry.ValidateAgainstConfig(registry, missingPath, writer);

        var output = writer.ToString();
        Assert.Contains("[WARN]", output);
        Assert.Contains("pipeline.config.json not found", output);
    }

    [Fact]
    public void ValidateAgainstConfig_InvalidJson_EmitsParseFailureWarning()
    {
        var registry = new StepRegistry([new FakeStep(1, "step-one")]);
        var configPath = WriteTempConfig("this is not json {{{{");
        var writer = new StringWriter();

        StepRegistry.ValidateAgainstConfig(registry, configPath, writer);

        var output = writer.ToString();
        Assert.Contains("[WARN]", output);
        Assert.Contains("Failed to parse pipeline.config.json", output);
    }

    [Fact]
    public void ValidateAgainstConfig_NoMismatch_DoesNotThrow()
    {
        var registry = new StepRegistry([new FakeStep(0, "bootstrap")]);
        var configJson = """[{"id":0,"name":"bootstrap"}]""";
        var configPath = WriteTempConfig(configJson);

        // Phase 1 must never throw on divergence — even for extra/missing steps
        var exception = Record.Exception(() =>
            StepRegistry.ValidateAgainstConfig(registry, configPath, TextWriter.Null));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateAgainstConfig_Divergence_DoesNotThrow()
    {
        var registry = new StepRegistry([new FakeStep(1, "step-one")]);
        var configJson = """[{"id":99,"name":"unregistered"}]""";
        var configPath = WriteTempConfig(configJson);

        // Phase 1: divergence must emit a warning, not throw
        var exception = Record.Exception(() =>
            StepRegistry.ValidateAgainstConfig(registry, configPath, TextWriter.Null));

        Assert.Null(exception);
    }

    private static string WriteTempConfig(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pipeline-config-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json, Encoding.UTF8);
        return path;
    }

    private class FakeStep : IPipelineStep
    {
        public FakeStep(int id, string name, StepScope scope = StepScope.Namespace)
        {
            Id = id;
            Name = name;
            Scope = scope;
        }

        public int Id { get; }

        public string Name { get; }

        public StepScope Scope { get; }

        public FailurePolicy FailurePolicy => FailurePolicy.Fatal;

        public IReadOnlyList<int> DependsOn => Array.Empty<int>();

        public IReadOnlyList<IPostValidator> PostValidators => Array.Empty<IPostValidator>();

        public int MaxRetries => 0;

        public ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
    }

    private sealed class ToolGenerationStep()
        : FakeStep(3, "Compose and improve tool files");
}
