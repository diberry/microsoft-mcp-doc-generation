using System.Text.Json;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Tests.Fixtures;

internal sealed class RecordingProcessRunner : IProcessRunner
{
    public List<ProcessSpec> Invocations { get; } = new();

    public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        Invocations.Add(spec);
        return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero));
    }

    public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
        => RunAsync(new ProcessSpec("dotnet", ["build", solutionPath], Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory), cancellationToken);

    public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken cancellationToken)
        => RunAsync(new ProcessSpec("dotnet", ["run", projectPath, .. arguments], workingDirectory), cancellationToken);

    public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken cancellationToken)
        => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), cancellationToken);
}

internal sealed class BufferedReportWriter : IReportWriter
{
    public List<string> Messages { get; } = new();

    public void Info(string message) => Messages.Add(message);

    public void Warning(string message) => Messages.Add($"WARNING: {message}");

    public void Error(string message) => Messages.Add($"ERROR: {message}");
}

internal sealed class StubBuildCoordinator : IBuildCoordinator
{
    public ValueTask EnsureReadyAsync(string solutionPath, bool skipBuild, IReadOnlyList<string> requiredArtifacts, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

internal sealed class StubAiCapabilityProbe : IAiCapabilityProbe
{
    public ValueTask<AiCapabilityResult> ProbeAsync(string docsGenerationRoot, CancellationToken cancellationToken)
        => ValueTask.FromResult(new AiCapabilityResult(true, Array.Empty<string>()));
}

internal sealed class StubFilteredCliWriter : IFilteredCliWriter
{
    public ValueTask<FilteredCliFileHandle> WriteAsync(CliMetadataSnapshot cliOutput, IReadOnlyList<CliTool> matchingTools, string tempDirectoryName, CancellationToken cancellationToken)
        => ValueTask.FromResult(new FilteredCliFileHandle(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "cli-output-single-tool.json")));
}

internal sealed class StubCliMetadataLoader : ICliMetadataLoader
{
    public bool CliOutputExists(string outputPath) => false;

    public bool NamespaceMetadataExists(string outputPath) => false;

    public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
