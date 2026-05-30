namespace PipelineRunner.Contracts;

/// <summary>
/// Defines the 5-file observability contract for every step workspace.
/// After step execution, PipelineRunner checks for these files and logs warnings for any missing.
/// </summary>
public sealed record StageOutputContract(
    string StepName,
    string WorkspaceDirectory,
    bool IsDeterministic)
{
    public const string SummaryFileName = "summary.md";
    public const string StepResultFileName = "step-result.json";
    public const string ValidationFileName = "validation.json";
    public const string PromptPreviewFileName = "prompt-preview.txt";
    public const string PromptPreviewNaFileName = "prompt-preview-na.txt";
    public const string MetricsFileName = "metrics.json";

    /// <summary>
    /// Returns the expected file paths based on whether the step is deterministic.
    /// </summary>
    public IReadOnlyList<string> GetExpectedFiles()
    {
        var promptFile = IsDeterministic ? PromptPreviewNaFileName : PromptPreviewFileName;
        return
        [
            Path.Combine(WorkspaceDirectory, SummaryFileName),
            Path.Combine(WorkspaceDirectory, StepResultFileName),
            Path.Combine(WorkspaceDirectory, ValidationFileName),
            Path.Combine(WorkspaceDirectory, promptFile),
            Path.Combine(WorkspaceDirectory, MetricsFileName),
        ];
    }
}
