using System.Diagnostics;

namespace PipelineRunner.Services;

public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string? StandardInput = null)
{
    public string DisplayCommand => FormatCommand(FileName, Arguments);

    public static string FormatCommand(string fileName, IEnumerable<string> arguments)
        => string.Join(' ', [Quote(fileName), .. arguments.Select(Quote)]);

    private static string Quote(string value)
        => value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}

public sealed record ProcessExecutionResult(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration)
{
    public bool Succeeded => ExitCode == 0;

    public string DisplayCommand => ProcessSpec.FormatCommand(FileName, Arguments);
}

public interface IProcessRunner
{
    ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken);

    ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken);

    ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(
        string projectPath,
        IEnumerable<string> arguments,
        bool noBuild,
        string workingDirectory,
        CancellationToken cancellationToken);

    ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(
        string scriptPath,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken);
}
