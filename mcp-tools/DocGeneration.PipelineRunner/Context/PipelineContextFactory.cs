using PipelineRunner.Cli;
using PipelineRunner.Services;

namespace PipelineRunner.Context;

public sealed class PipelineContextFactory
{
    private readonly IProcessRunner _processRunner;
    private readonly IWorkspaceManager _workspaceManager;
    private readonly ICliMetadataLoader _cliMetadataLoader;
    private readonly ITargetMatcher _targetMatcher;
    private readonly IFilteredCliWriter _filteredCliWriter;
    private readonly IBuildCoordinator _buildCoordinator;
    private readonly IAiCapabilityProbe _aiCapabilityProbe;
    private readonly IReportWriter _reportWriter;
    private readonly string? _repoRootOverride;

    public PipelineContextFactory(
        IProcessRunner processRunner,
        IWorkspaceManager workspaceManager,
        ICliMetadataLoader cliMetadataLoader,
        ITargetMatcher targetMatcher,
        IFilteredCliWriter filteredCliWriter,
        IBuildCoordinator buildCoordinator,
        IAiCapabilityProbe aiCapabilityProbe,
        IReportWriter reportWriter,
        string? repoRootOverride = null)
    {
        _processRunner = processRunner;
        _workspaceManager = workspaceManager;
        _cliMetadataLoader = cliMetadataLoader;
        _targetMatcher = targetMatcher;
        _filteredCliWriter = filteredCliWriter;
        _buildCoordinator = buildCoordinator;
        _aiCapabilityProbe = aiCapabilityProbe;
        _reportWriter = reportWriter;
        _repoRootOverride = repoRootOverride;
    }

    public async ValueTask<PipelineContext> CreateAsync(PipelineRequest request, CancellationToken cancellationToken)
    {
        var repoRoot = ResolveRepoRoot(_repoRootOverride);
        var McpToolsRoot = Path.Combine(repoRoot, "mcp-tools");
        var outputPath = Path.GetFullPath(
            Path.IsPathRooted(request.OutputPath)
                ? request.OutputPath
                : Path.Combine(repoRoot, request.OutputPath));

        var context = new PipelineContext
        {
            Request = request,
            RepoRoot = repoRoot,
            McpToolsRoot = McpToolsRoot,
            OutputPath = outputPath,
            ProcessRunner = _processRunner,
            Workspaces = _workspaceManager,
            CliMetadataLoader = _cliMetadataLoader,
            TargetMatcher = _targetMatcher,
            FilteredCliWriter = _filteredCliWriter,
            BuildCoordinator = _buildCoordinator,
            AiCapabilityProbe = _aiCapabilityProbe,
            Reports = _reportWriter,
            SelectedNamespaces = Array.Empty<string>(),
        };

        if (!string.IsNullOrWhiteSpace(request.Namespace))
        {
            context.SelectedNamespaces = [_targetMatcher.Normalize(request.Namespace!)];
        }
        else if (_cliMetadataLoader.NamespaceMetadataExists(outputPath))
        {
            context.SelectedNamespaces = await _cliMetadataLoader.LoadNamespacesAsync(outputPath, cancellationToken);
        }

        if (_cliMetadataLoader.CliOutputExists(outputPath))
        {
            context.CliOutput = await _cliMetadataLoader.LoadCliOutputAsync(outputPath, cancellationToken);
            context.CliVersion = await _cliMetadataLoader.LoadCliVersionAsync(outputPath, cancellationToken);
        }

        return context;
    }

    internal static string ResolveRepoRoot(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "mcp-doc-generation.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the current application base directory.");
    }
}
