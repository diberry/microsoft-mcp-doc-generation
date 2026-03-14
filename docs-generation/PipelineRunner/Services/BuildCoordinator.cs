namespace PipelineRunner.Services;

public sealed class BuildCoordinator : IBuildCoordinator
{
    private readonly IProcessRunner _processRunner;
    private readonly IReportWriter _reportWriter;
    private bool _buildCompleted;

    public BuildCoordinator(IProcessRunner processRunner, IReportWriter reportWriter)
    {
        _processRunner = processRunner;
        _reportWriter = reportWriter;
    }

    public async ValueTask EnsureReadyAsync(
        string solutionPath,
        bool skipBuild,
        IReadOnlyList<string> requiredArtifacts,
        CancellationToken cancellationToken)
    {
        if (skipBuild)
        {
            var missingArtifacts = requiredArtifacts.Where(path => !File.Exists(path)).ToArray();
            if (missingArtifacts.Length > 0)
            {
                throw new FileNotFoundException(
                    $"Build was skipped, but required Release artifacts were not found: {string.Join(", ", missingArtifacts)}");
            }

            _reportWriter.Info("Skipping build; existing Release artifacts were found.");
            return;
        }

        if (_buildCompleted)
        {
            _reportWriter.Info("Skipping duplicate build; the solution was already built for this run.");
            return;
        }

        var result = await _processRunner.RunDotNetBuildAsync(solutionPath, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"dotnet build failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}");
        }

        _buildCompleted = true;
        _reportWriter.Info("Solution build completed.");
    }
}
