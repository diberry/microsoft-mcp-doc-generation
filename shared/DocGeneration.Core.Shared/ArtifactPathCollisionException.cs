namespace Shared;

/// <summary>
/// Thrown by StepResultWriter when two output artifacts in the same envelope share a path.
/// </summary>
public sealed class ArtifactPathCollisionException : InvalidOperationException
{
    public string DuplicatePath { get; }

    public ArtifactPathCollisionException(string duplicatePath)
        : base($"Duplicate output artifact path detected: '{duplicatePath}'. Each artifact must have a unique path within a step envelope.")
    {
        DuplicatePath = duplicatePath;
    }
}
