using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class BootstrapStepTests
{
    [Fact]
    public void BootstrapStep_IsGlobalScope()
        => Assert.Equal(StepScope.Global, new BootstrapStep().Scope);

    [Fact]
    public async Task ExecuteAsync_AiEnvironmentConfigured_Succeeds()
    {
        using var harness = CreateHarness(requiresAiConfiguration: true, aiConfigured: true);

        var result = await harness.Step.ExecuteAsync(harness.Context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(harness.Context.AiConfigured);
    }

    [Fact]
    public async Task ExecuteAsync_AiEnvironmentMissing_Fails()
    {
        using var harness = CreateHarness(requiresAiConfiguration: true, aiConfigured: false);

        var result = await harness.Step.ExecuteAsync(harness.Context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("Missing required AI configuration", StringComparison.Ordinal));
        Assert.Empty(harness.ProcessRunner.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_CliMetadataValidationPasses()
    {
        using var harness = CreateHarness();

        var result = await harness.Step.ExecuteAsync(harness.Context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("1.2.3", harness.Context.CliVersion);
        Assert.NotNull(harness.Context.CliOutput);
        Assert.Single(harness.Context.CliOutput!.Tools);
    }

    [Fact]
    public async Task ExecuteAsync_CliVersionMetadataMissing_Fails()
    {
        using var harness = CreateHarness(cliMetadataLoader: new DelegatingCliMetadataLoader(versionExists: false));

        var result = await harness.Step.ExecuteAsync(harness.Context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("CLI version metadata file was not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_RespectsSkipBuildFlag()
    {
        using var harness = CreateHarness(skipBuild: true);

        var result = await harness.Step.ExecuteAsync(harness.Context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(harness.BuildCoordinator.SkipBuildObserved);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsSkipValidationFlag()
    {
        using var harness = CreateHarness(skipValidation: true);

        var result = await harness.Step.ExecuteAsync(harness.Context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(
            harness.ProcessRunner.Invocations,
            invocation => invocation.Arguments.Any(argument => argument.EndsWith("DocGeneration.Steps.Bootstrap.BrandMappings.csproj", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ExecuteAsync_RespectsSkipEnvValidationFlag()
    {
        using var harness = CreateHarness(requiresAiConfiguration: true, skipEnvValidation: true, aiConfigured: false);

        var result = await harness.Step.ExecuteAsync(harness.Context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, harness.AiCapabilityProbe.ProbeCalls);
    }

    private static TestHarness CreateHarness(
        bool skipBuild = false,
        bool skipValidation = false,
        bool skipEnvValidation = false,
        bool requiresAiConfiguration = false,
        bool aiConfigured = true,
        ICliMetadataLoader? cliMetadataLoader = null)
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-bootstrap-tests-{Guid.NewGuid():N}");
        var docsGenerationRoot = Path.Combine(repoRoot, "docs-generation");
        Directory.CreateDirectory(Path.Combine(docsGenerationRoot, "data"));
        Directory.CreateDirectory(Path.Combine(docsGenerationRoot, "azure-mcp"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "test-npm-azure-mcp"));
        File.WriteAllText(Path.Combine(repoRoot, "docs-generation.sln"), string.Empty);
        File.WriteAllText(Path.Combine(docsGenerationRoot, "data", "brand-to-server-mapping.json"), "[]");
        File.WriteAllText(Path.Combine(docsGenerationRoot, "azure-mcp", "azmcp-commands.md"), "# Commands");

        var processRunner = new ScriptedProcessRunner();
        var buildCoordinator = new RecordingBuildCoordinator();
        var aiCapabilityProbe = new RecordingAiCapabilityProbe(aiConfigured);
        var step = new BootstrapStep();
        var plannedSteps = requiresAiConfiguration
            ? new IPipelineStep[] { step, new ExamplePromptsStep() }
            : new IPipelineStep[] { step, new AnnotationsParametersRawStep() };

        var context = new PipelineContext
        {
            Request = new PipelineRequest(
                "compute",
                [1],
                ".\\generated-compute",
                SkipBuild: skipBuild,
                SkipValidation: skipValidation,
                DryRun: false,
                SkipEnvValidation: skipEnvValidation),
            RepoRoot = repoRoot,
            DocsGenerationRoot = docsGenerationRoot,
            OutputPath = Path.Combine(repoRoot, "generated-compute"),
            ProcessRunner = processRunner,
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = cliMetadataLoader ?? new DelegatingCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new StubFilteredCliWriter(),
            BuildCoordinator = buildCoordinator,
            AiCapabilityProbe = aiCapabilityProbe,
            Reports = new BufferedReportWriter(),
            PlannedSteps = plannedSteps,
        };

        return new TestHarness(repoRoot, step, context, processRunner, buildCoordinator, aiCapabilityProbe);
    }

    private sealed class TestHarness : IDisposable
    {
        public TestHarness(string rootPath, BootstrapStep step, PipelineContext context, ScriptedProcessRunner processRunner, RecordingBuildCoordinator buildCoordinator, RecordingAiCapabilityProbe aiCapabilityProbe)
        {
            RootPath = rootPath;
            Step = step;
            Context = context;
            ProcessRunner = processRunner;
            BuildCoordinator = buildCoordinator;
            AiCapabilityProbe = aiCapabilityProbe;
        }

        public string RootPath { get; }

        public BootstrapStep Step { get; }

        public PipelineContext Context { get; }

        public ScriptedProcessRunner ProcessRunner { get; }

        public RecordingBuildCoordinator BuildCoordinator { get; }

        public RecordingAiCapabilityProbe AiCapabilityProbe { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class RecordingBuildCoordinator : IBuildCoordinator
    {
        public bool SkipBuildObserved { get; private set; }

        public ValueTask EnsureReadyAsync(string solutionPath, bool skipBuild, IReadOnlyList<string> requiredArtifacts, CancellationToken cancellationToken)
        {
            SkipBuildObserved = skipBuild;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingAiCapabilityProbe(bool isConfigured) : IAiCapabilityProbe
    {
        public int ProbeCalls { get; private set; }

        public ValueTask<AiCapabilityResult> ProbeAsync(string docsGenerationRoot, CancellationToken cancellationToken)
        {
            ProbeCalls++;
            return ValueTask.FromResult(new AiCapabilityResult(isConfigured, isConfigured ? Array.Empty<string>() : ["FOUNDRY_API_KEY"]));
        }
    }

    private sealed class DelegatingCliMetadataLoader : ICliMetadataLoader
    {
        private readonly CliMetadataLoader _inner = new();
        private readonly bool _outputExists;
        private readonly bool _versionExists;
        private readonly bool _namespaceExists;

        public DelegatingCliMetadataLoader(bool outputExists = true, bool versionExists = true, bool namespaceExists = true)
        {
            _outputExists = outputExists;
            _versionExists = versionExists;
            _namespaceExists = namespaceExists;
        }

        public bool CliOutputExists(string outputPath)
            => _outputExists && _inner.CliOutputExists(outputPath);

        public bool CliVersionExists(string outputPath)
            => _versionExists && _inner.CliVersionExists(outputPath);

        public bool NamespaceMetadataExists(string outputPath)
            => _namespaceExists && _inner.NamespaceMetadataExists(outputPath);

        public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
            => _inner.LoadCliOutputAsync(outputPath, cancellationToken);

        public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
            => _inner.LoadCliVersionAsync(outputPath, cancellationToken);

        public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
            => _inner.LoadNamespacesAsync(outputPath, cancellationToken);
    }

    private sealed class ScriptedProcessRunner : IProcessRunner
    {
        private static readonly string CliOutputJson = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    command = "compute list",
                    name = "compute list",
                    description = "List compute resources",
                },
            },
        });

        private static readonly string NamespaceJson = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    name = "compute",
                },
            },
        });

        public List<ProcessSpec> Invocations { get; } = new();

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            Invocations.Add(spec);

            if (spec.Arguments.SequenceEqual(["install", "--silent"]))
            {
                return ValueTask.FromResult(Success(spec));
            }

            var scriptName = spec.Arguments.LastOrDefault();
            var standardOutput = scriptName switch
            {
                "get:version" => "1.2.3",
                "get:tools-json" => CliOutputJson,
                "get:tools-namespace" => NamespaceJson,
                _ => string.Empty,
            };

            return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, standardOutput, string.Empty, TimeSpan.Zero));
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

        private static ProcessExecutionResult Success(ProcessSpec spec)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero);
    }
}
