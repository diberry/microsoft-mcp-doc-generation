namespace PipelineRunner.Services;

public interface IBuildCoordinator
{
    ValueTask EnsureReadyAsync(
        string solutionPath,
        bool skipBuild,
        IReadOnlyList<string> requiredArtifacts,
        CancellationToken cancellationToken);
}
