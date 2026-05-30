// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PipelineRunner.Services;
using Shared;

namespace DocGeneration.PipelineRunner.SmokeTests;

/// <summary>
/// Local test-double implementations for use in smoke tests.
/// Duplicated from DocGeneration.PipelineRunner.Tests because that project is not
/// referenced by DocGeneration.PipelineRunner.SmokeTests.
/// </summary>

/// <summary>Isolated duplicate of <c>RecordingProcessRunner</c> from PipelineRunner.Tests — smoke tests must not take a test-project dependency.</summary>
internal sealed class RecordingProcessRunner : IProcessRunner
{
    public List<ProcessSpec> Invocations { get; } = new();

    public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        Invocations.Add(spec);
        return ValueTask.FromResult(new ProcessExecutionResult(
            spec.FileName, spec.Arguments, spec.WorkingDirectory,
            0, string.Empty, string.Empty, TimeSpan.Zero));
    }

    public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
        => RunAsync(
            new ProcessSpec(
                "dotnet",
                ["build", solutionPath, "--configuration", "Release", "--verbosity", "quiet"],
                Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory),
            cancellationToken);

    public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(
        string projectPath,
        IEnumerable<string> arguments,
        bool noBuild,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var invocation = new List<string> { "run", "--project", projectPath, "--configuration", "Release" };
        if (noBuild)
            invocation.Add("--no-build");
        invocation.Add("--");
        invocation.AddRange(arguments);
        return RunAsync(new ProcessSpec("dotnet", invocation, workingDirectory), cancellationToken);
    }

    public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(
        string scriptPath,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
        => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), cancellationToken);
}

/// <summary>Isolated duplicate of <c>SmokeStubBuildCoordinator</c> from PipelineRunner.Tests — smoke tests must not take a test-project dependency.</summary>
internal sealed class SmokeStubBuildCoordinator : IBuildCoordinator
{
    public ValueTask EnsureReadyAsync(
        string solutionPath,
        bool skipBuild,
        IReadOnlyList<string> requiredArtifacts,
        CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

/// <summary>Isolated duplicate of <c>SmokeStubAiCapabilityProbe</c> from PipelineRunner.Tests — smoke tests must not take a test-project dependency.</summary>
internal sealed class SmokeStubAiCapabilityProbe : IAiCapabilityProbe
{
    public ValueTask<AiCapabilityResult> ProbeAsync(string mcpToolsRoot, CancellationToken cancellationToken)
        => ValueTask.FromResult(new AiCapabilityResult(true, Array.Empty<string>()));
}

/// <summary>Isolated duplicate of <c>SmokeStubFilteredCliWriter</c> from PipelineRunner.Tests — smoke tests must not take a test-project dependency.</summary>
internal sealed class SmokeStubFilteredCliWriter : IFilteredCliWriter
{
    public ValueTask<FilteredCliFileHandle> WriteAsync(
        CliMetadataSnapshot cliOutput,
        IReadOnlyList<CliTool> matchingTools,
        string tempDirectoryName,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(new FilteredCliFileHandle(
            Path.GetTempPath(),
            Path.Combine(Path.GetTempPath(), "cli-output-single-tool.json")));
}

/// <summary>Isolated duplicate of <c>SmokeStubBrandMappingLoader</c> from PipelineRunner.Tests — smoke tests must not take a test-project dependency.</summary>
internal sealed class SmokeStubBrandMappingLoader : IBrandMappingLoader
{
    public Task<IReadOnlyList<BrandMappingEntry>> LoadAsync(string mcpToolsRoot, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<BrandMappingEntry>>([]);
}
