using PipelineRunner.Services;

namespace PipelineRunner.Services;

/// <summary>
/// Typed request for invoking a validation PowerShell script.
/// All fields are explicit; wrappers must not infer paths from the current working directory.
/// </summary>
public sealed record ValidationScriptRequest(
    string ScriptPath,
    string RunId,
    string Namespace,
    string RepoRoot,
    string OutputRoot,
    string OutputJsonPath,
    IReadOnlyList<string> ArticlePaths,
    IReadOnlyDictionary<string, string> AdditionalArguments);

/// <summary>
/// Result of a validation script invocation, capturing all observable execution data.
/// </summary>
public sealed record ValidationScriptResult(
    int ExitCode,
    string OutputJsonPath,
    string StdOut,
    string StdErr,
    bool JsonArtifactExists,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public bool Succeeded => ExitCode == 0;
}

public interface IValidationScriptRunner
{
    Task<ValidationScriptResult> RunAsync(
        ValidationScriptRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Invokes a validation PowerShell script through <see cref="IProcessRunner"/> and
/// records execution metadata needed for artifact verification.
/// </summary>
public sealed class ValidationScriptRunner(IProcessRunner processRunner) : IValidationScriptRunner
{
    public async Task<ValidationScriptResult> RunAsync(
        ValidationScriptRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var arguments = BuildArguments(request);

        var processResult = await processRunner.RunPowerShellScriptAsync(
            request.ScriptPath,
            arguments,
            request.RepoRoot,
            cancellationToken);

        var completedAt = DateTimeOffset.UtcNow;

        return new ValidationScriptResult(
            ExitCode: processResult.ExitCode,
            OutputJsonPath: request.OutputJsonPath,
            StdOut: processResult.StandardOutput,
            StdErr: processResult.StandardError,
            JsonArtifactExists: File.Exists(request.OutputJsonPath),
            StartedAt: startedAt,
            CompletedAt: completedAt);
    }

    private static IReadOnlyList<string> BuildArguments(ValidationScriptRequest request)
    {
        if (request.ArticlePaths.Count == 0)
            throw new ArgumentException("ArticlePaths cannot be empty.", nameof(request));

        var args = new List<string>();

        if (request.ArticlePaths.Count == 1)
        {
            args.AddRange(["-ArticlePath", request.ArticlePaths[0]]);
        }
        else if (request.ArticlePaths.Count > 1)
        {
            // Find the common directory for multi-file validation
            var directory = Path.GetDirectoryName(request.ArticlePaths[0]) ?? request.OutputRoot;
            args.AddRange(["-ArticlesDir", directory]);
        }

        args.AddRange(["-RunId", request.RunId]);
        args.AddRange(["-Namespace", request.Namespace]);
        args.AddRange(["-OutputJson", request.OutputJsonPath]);

        foreach (var (key, value) in request.AdditionalArguments)
        {
            args.Add(key);
            if (!string.IsNullOrEmpty(value))
            {
                args.Add(value);
            }
        }

        return args;
    }
}
