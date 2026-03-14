using System.Diagnostics;
using System.Text;

namespace PipelineRunner.Services;

public sealed class ProcessRunner : IProcessRunner
{
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
        var commandArguments = new List<string>
        {
            "run",
            "--project",
            projectPath,
            "--configuration",
            "Release",
        };

        if (noBuild)
        {
            commandArguments.Add("--no-build");
        }

        commandArguments.Add("--");
        commandArguments.AddRange(arguments);

        return RunAsync(new ProcessSpec("dotnet", commandArguments, workingDirectory), cancellationToken);
    }

    public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(
        string scriptPath,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var commandArguments = new List<string> { "-File", scriptPath };
        commandArguments.AddRange(arguments);
        return RunAsync(new ProcessSpec("pwsh", commandArguments, workingDirectory), cancellationToken);
    }

    public async ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            WorkingDirectory = spec.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = spec.StandardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        process.Start();

        if (spec.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(spec.StandardInput.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        stopwatch.Stop();

        return new ProcessExecutionResult(
            spec.FileName,
            spec.Arguments,
            spec.WorkingDirectory,
            process.ExitCode,
            await outputTask,
            await errorTask,
            stopwatch.Elapsed);
    }
}
