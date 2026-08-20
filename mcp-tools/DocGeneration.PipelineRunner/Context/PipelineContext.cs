using DocGeneration.Core.Tracing;
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

    public required WorkspaceManager Workspaces { get; init; }

    public required ICliMetadataLoader CliMetadataLoader { get; init; }

    public required TargetMatcher TargetMatcher { get; init; }

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

    public CliMetadataSnapshot? SourceCliOutput { get; set; }

    public bool AiConfigured { get; set; }

    /// <summary>
    /// True once the run has entered the "partial_explicit" offline continuation mode: the early
    /// bootstrap live Azure OpenAI probe failed and an interactive operator explicitly chose to
    /// continue with deterministic/verbatim-only output. When true, AI-dependent namespace steps
    /// (2, 3, 4, 6) must not attempt further AI calls and must mark AI-required artifacts/steps
    /// incomplete rather than reporting them as fully successful.
    /// </summary>
    public bool AiOffline { get; set; }

    /// <summary>
    /// Optional override for the interactive continue/abort prompt used by Bootstrap when the
    /// live probe fails. Null in production (a real <see cref="ConsolePipelineUserPrompt"/> is
    /// used); tests inject a fake to avoid depending on console I/O.
    /// </summary>
    public IPipelineUserPrompt? PipelineUserPrompt { get; init; }

    public IReadOnlyList<IPipelineStep> PlannedSteps { get; set; } = Array.Empty<IPipelineStep>();

    public IReadOnlyList<string> SelectedNamespaces { get; set; } = Array.Empty<string>();

    public IPipelineTracer Tracer { get; set; } = NullTracer.Instance;

    public Dictionary<string, object> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
}
