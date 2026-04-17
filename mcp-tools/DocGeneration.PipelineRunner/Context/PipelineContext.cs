using PipelineRunner.Cli;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Context;

public sealed class PipelineContext
{
    public required PipelineRequest Request { get; init; }

    public required string RepoRoot { get; init; }

    public required string McpToolsRoot { get; init; }

    public required string OutputPath { get; init; }

    public required IProcessRunner ProcessRunner { get; init; }

    public required IWorkspaceManager Workspaces { get; init; }

    public required ICliMetadataLoader CliMetadataLoader { get; init; }

    public required ITargetMatcher TargetMatcher { get; init; }

    public required IFilteredCliWriter FilteredCliWriter { get; init; }

    public required IBuildCoordinator BuildCoordinator { get; init; }

    public required IAiCapabilityProbe AiCapabilityProbe { get; init; }

    public required IReportWriter Reports { get; init; }

    /// <summary>
    /// The resolved upstream branch for fetching files from microsoft/mcp.
    /// Delegates to <see cref="PipelineRequest.ResolvedMcpBranch"/>.
    /// </summary>
    public string McpBranch => Request.ResolvedMcpBranch;

    public string? CliVersion { get; set; }

    public CliMetadataSnapshot? CliOutput { get; set; }

    public bool AiConfigured { get; set; }

    public IReadOnlyList<IPipelineStep> PlannedSteps { get; set; } = Array.Empty<IPipelineStep>();

    public IReadOnlyList<string> SelectedNamespaces { get; set; } = Array.Empty<string>();

    public Dictionary<string, object> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
}
