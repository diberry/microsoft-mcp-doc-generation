using PipelineRunner.Cli;
using PipelineRunner.Services;

namespace PipelineRunner.Context;

public sealed class PipelineContext
{
    public required PipelineRequest Request { get; init; }

    public required string RepoRoot { get; init; }

    public required string DocsGenerationRoot { get; init; }

    public required string OutputPath { get; init; }

    public required IProcessRunner ProcessRunner { get; init; }

    public required IWorkspaceManager Workspaces { get; init; }

    public required ICliMetadataLoader CliMetadataLoader { get; init; }

    public required ITargetMatcher TargetMatcher { get; init; }

    public required IFilteredCliWriter FilteredCliWriter { get; init; }

    public required IBuildCoordinator BuildCoordinator { get; init; }

    public required IAiCapabilityProbe AiCapabilityProbe { get; init; }

    public required IReportWriter Reports { get; init; }

    public string? CliVersion { get; set; }

    public CliMetadataSnapshot? CliOutput { get; set; }

    public bool AiConfigured { get; set; }

    public IReadOnlyList<string> SelectedNamespaces { get; set; } = Array.Empty<string>();

    public Dictionary<string, object> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
}
