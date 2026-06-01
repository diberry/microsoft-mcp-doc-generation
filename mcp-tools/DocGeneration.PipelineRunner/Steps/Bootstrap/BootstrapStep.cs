using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using Shared;
using System.Text;

namespace PipelineRunner.Steps;

public sealed class BootstrapStep : StepDefinition
{
    /// <summary>
    /// Base path within the microsoft/mcp repo where upstream doc files live.
    /// </summary>
    internal const string McpDocsPath = "servers/Azure.Mcp.Server/docs";

    /// <summary>
    /// Shared HttpClient for upstream file fetching. Static to avoid socket exhaustion.
    /// </summary>
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "AzureMcpDocGen/1.0" } },
    };

    /// <summary>
    /// Constructs a raw GitHub URL for a file in the microsoft/mcp repo on the given branch.
    /// </summary>
    internal static string BuildUpstreamUrl(string branch, string fileName)
        => $"https://raw.githubusercontent.com/microsoft/mcp/{branch}/{McpDocsPath}/{fileName}";

    private static readonly string[] BaseOutputDirectories =
    [
        "cli",
        "common-general",
        "tools",
        "example-prompts",
        "annotations",
        "logs",
        "tool-family",
    ];

    private readonly INamespaceMappingEmitter _namespaceMappingEmitter;

    public BootstrapStep(INamespaceMappingEmitter? namespaceMappingEmitter = null)
        : base(
            0,
            "Bootstrap pipeline",
            StepScope.Global,
            FailurePolicy.Fatal,
            requiresCliOutput: false,
            requiresCliVersion: false,
            expectedOutputs:
            [
                "cli",
                "e2e-test-prompts",
                "common-general",
                "tools",
                "example-prompts",
                "annotations",
                "logs",
                "tool-family",
                "namespace-mapping.json",
            ])
    {
        _namespaceMappingEmitter = namespaceMappingEmitter ?? new NamespaceMappingEmitter();
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();

        try
        {
            context.Reports.Info($"Upstream MCP branch: {context.McpBranch}");

            if (NeedsAiConfiguration(context))
            {
                if (context.Request.SkipEnvValidation)
                {
                    context.Reports.Info("Skipping AI environment validation.");
                }
                else
                {
                    var probeResult = await context.AiCapabilityProbe.ProbeAsync(context.McpToolsRoot, cancellationToken);
                    if (!probeResult.IsConfigured)
                    {
                        warnings.AddRange(probeResult.MissingKeys.Select(key => $"Missing required AI configuration: {key}"));
                        return BuildResult(context, processResults, success: false, warnings);
                    }

                    context.AiConfigured = true;
                }
            }

            if (IsFullPipelineRun(context))
            {
                ResetOutputDirectory(context.OutputPath);
            }
            else
            {
                // Incremental run — preserve existing output, just ensure directories exist
                Directory.CreateDirectory(context.OutputPath);
            }
            CreateDirectories(context.OutputPath, ["cli"]);

            await context.BuildCoordinator.EnsureReadyAsync(
                Path.Combine(context.RepoRoot, "mcp-doc-generation.sln"),
                context.Request.SkipBuild,
                GetRequiredArtifacts(context.McpToolsRoot, context.PlannedSteps),
                cancellationToken);

            var cliDirectory = Path.Combine(context.OutputPath, "cli");
            var mcpCliMetadataProject = Path.Combine(context.McpToolsRoot, "McpCliMetadata", "McpCliMetadata.csproj");
            if (!File.Exists(mcpCliMetadataProject))
            {
                warnings.Add($"CLI metadata project was not found: {mcpCliMetadataProject}");
                return BuildResult(context, processResults, success: false, warnings);
            }


            if (!context.Request.SkipNpmUpdate)
            {
                var pinnedVersion = await ReadMcpToolVersionAsync(context.RepoRoot, cancellationToken);
                if (pinnedVersion is not null)
                {
                    context.Reports.Info($"Installing azure.mcp@{pinnedVersion} from mcp-tool-version.txt...");
                    var versionedInstallResult = await context.ProcessRunner.RunAsync(
                        new ProcessSpec("dotnet", ["tool", "update", "azure.mcp", "--global", "--version", pinnedVersion], context.RepoRoot),
                        cancellationToken);
                    processResults.Add(versionedInstallResult);

                    var combinedOutput = versionedInstallResult.StandardOutput + versionedInstallResult.StandardError;
                    var alreadyAtVersion = !versionedInstallResult.Succeeded &&
                        combinedOutput.Contains("already", StringComparison.OrdinalIgnoreCase);

                    if (!versionedInstallResult.Succeeded && !alreadyAtVersion)
                    {
                        AddProcessIssue(versionedInstallResult, warnings, $"Failed to install azure.mcp@{pinnedVersion} — use --skip-tool-update for offline or reproducible builds");
                        return BuildResult(context, processResults, success: false, warnings);
                    }

                    context.Reports.Info($"azure.mcp@{pinnedVersion} is ready.");
                }
                else
                {
                    context.Reports.Warning("mcp-tool-version.txt not found — updating azure.mcp to latest version (add mcp-tool-version.txt for reproducible builds).");
                    var latestInstallResult = await context.ProcessRunner.RunAsync(
                        new ProcessSpec("dotnet", ["tool", "update", "azure.mcp", "--global"], context.RepoRoot),
                        cancellationToken);
                    processResults.Add(latestInstallResult);
                    if (!latestInstallResult.Succeeded)
                    {
                        AddProcessIssue(latestInstallResult, warnings, "Failed to update azure.mcp dotnet tool globally — use --skip-tool-update for offline or reproducible builds");
                        return BuildResult(context, processResults, success: false, warnings);
                    }

                    context.Reports.Info("azure.mcp dotnet tool updated to latest.");
                }
            }
            else
            {
                context.Reports.Info("Skipping azure.mcp dotnet tool update (--skip-npm-update).");
            }

            var metadataGenerationResult = await context.ProcessRunner.RunDotNetProjectAsync(
                mcpCliMetadataProject,
                [context.OutputPath],
                noBuild: true,
                context.RepoRoot,
                cancellationToken);
            processResults.Add(metadataGenerationResult);
            if (!metadataGenerationResult.Succeeded)
            {
                AddProcessIssue(metadataGenerationResult, warnings, "CLI metadata extraction failed");
                return BuildResult(context, processResults, success: false, warnings);
            }

            if (!context.CliMetadataLoader.CliOutputExists(context.OutputPath))
            {
                warnings.Add($"CLI output metadata file was not found under '{context.OutputPath}'.");
                return BuildResult(context, processResults, success: false, warnings);
            }

            if (!context.CliMetadataLoader.CliVersionExists(context.OutputPath))
            {
                warnings.Add($"CLI version metadata file was not found under '{context.OutputPath}'.");
                return BuildResult(context, processResults, success: false, warnings);
            }

            if (!context.CliMetadataLoader.NamespaceMetadataExists(context.OutputPath))
            {
                warnings.Add($"CLI namespace metadata file was not found under '{context.OutputPath}'.");
                return BuildResult(context, processResults, success: false, warnings);
            }

            context.CliOutput = await context.CliMetadataLoader.LoadCliOutputAsync(context.OutputPath, cancellationToken);
            context.CliVersion = await context.CliMetadataLoader.LoadCliVersionAsync(context.OutputPath, cancellationToken);
            await context.CliMetadataLoader.LoadNamespacesAsync(context.OutputPath, cancellationToken);

            // Generate deterministic H2 headings from CLI metadata (once, for all namespaces)
            await GenerateH2HeadingsAsync(context);

            if (!string.IsNullOrWhiteSpace(context.Request.Namespace))
            {
                try
                {
                    context.TargetMatcher.FindMatches(context.CliOutput.Tools, context.Request.Namespace!);
                }
                catch (Exception ex)
                {
                    warnings.Add(ex.Message);
                    return BuildResult(context, processResults, success: false, warnings);
                }
            }

            if (!context.Request.SkipValidation)
            {
                var brandMappingProject = Path.Combine(context.McpToolsRoot, "DocGeneration.Steps.Bootstrap.BrandMappings", "DocGeneration.Steps.Bootstrap.BrandMappings.csproj");
                var brandMappingFile = Path.Combine(context.McpToolsRoot, "data", "brand-to-server-mapping.json");
                var suggestionsFile = Path.Combine(context.OutputPath, "reports", "brand-mapping-suggestions.json");
                var brandValidationResult = await context.ProcessRunner.RunDotNetProjectAsync(
                    brandMappingProject,
                    [
                        "--cli-output", Path.Combine(cliDirectory, "cli-output.json"),
                        "--brand-mapping", brandMappingFile,
                        "--output", suggestionsFile,
                    ],
                    noBuild: true,
                    context.McpToolsRoot,
                    cancellationToken);
                processResults.Add(brandValidationResult);
                if (!brandValidationResult.Succeeded)
                {
                    AddProcessIssue(brandValidationResult, warnings, "Brand mapping validation failed");
                    return BuildResult(
                        context,
                        processResults,
                        success: false,
                        warnings,
                        exitCodeOverride: global::PipelineRunner.PipelineRunner.MapBootstrapExitCode(brandValidationResult.ExitCode));
                }
            }
            else
            {
                context.Reports.Info("Skipping bootstrap validation checks.");
            }

            var e2eOutputFile = Path.Combine(context.OutputPath, "e2e-test-prompts", "parsed.json");
            var e2eProject = Path.Combine(context.McpToolsRoot, "DocGeneration.Steps.Bootstrap.E2eTestPromptParser", "DocGeneration.Steps.Bootstrap.E2eTestPromptParser.csproj");

            // Centralized fetch: Bootstrap downloads e2eTestPrompts.md from the resolved branch,
            // then passes the local file to the parser (no branch/URL logic in the parser itself).
            var e2eLocalFallback = Path.Combine(context.McpToolsRoot, "azure-mcp", "e2eTestPrompts.md");
            var e2eRemoteUrl = BuildUpstreamUrl(context.McpBranch, "e2eTestPrompts.md");
            string? e2eFetchedFile = null;
            try
            {
                e2eFetchedFile = await FetchUpstreamFileAsync(
                    e2eRemoteUrl, e2eLocalFallback, "e2eTestPrompts.md", context, warnings, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                warnings.Add("e2eTestPrompts.md not available from upstream or locally (non-blocking).");
            }

            var e2eArgs = new List<string> { e2eOutputFile };
            if (e2eFetchedFile is not null)
            {
                e2eArgs.AddRange(["--file", e2eFetchedFile]);
            }

            var e2eResult = await context.ProcessRunner.RunDotNetProjectAsync(
                e2eProject,
                e2eArgs.ToArray(),
                noBuild: true,
                context.McpToolsRoot,
                cancellationToken);
            processResults.Add(e2eResult);
            if (!e2eResult.Succeeded)
            {
                AddProcessIssue(e2eResult, warnings, "E2E test prompt parsing failed (non-blocking)");
            }

            var azmcpLocalFallback = Path.Combine(context.McpToolsRoot, "azure-mcp", "azmcp-commands.md");
            var azmcpRemoteUrl = BuildUpstreamUrl(context.McpBranch, "azmcp-commands.md");
            var azmcpSourceFile = await FetchUpstreamFileAsync(
                azmcpRemoteUrl, azmcpLocalFallback, "azmcp-commands.md", context, warnings, cancellationToken);
            var azmcpOutputFile = Path.Combine(cliDirectory, "azmcp-commands.json");
            var azmcpProject = Path.Combine(context.McpToolsRoot, "DocGeneration.Steps.Bootstrap.CommandParser", "DocGeneration.Steps.Bootstrap.CommandParser.csproj");
            var azmcpResult = await context.ProcessRunner.RunDotNetProjectAsync(
                azmcpProject,
                ["--file", azmcpSourceFile, "--output", azmcpOutputFile],
                noBuild: true,
                context.McpToolsRoot,
                cancellationToken);
            processResults.Add(azmcpResult);
            if (!azmcpResult.Succeeded)
            {
                AddProcessIssue(azmcpResult, warnings, "azmcp-commands.md parsing failed (non-blocking)");
            }

            if (azmcpResult.Succeeded)
            {
                var enricherProject = Path.Combine(context.McpToolsRoot, "DocGeneration.Steps.Bootstrap.ToolMetadataEnricher", "DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.csproj");
                var enrichedOutputFile = Path.Combine(cliDirectory, "cli-output-enriched.json");
                var enricherResult = await context.ProcessRunner.RunDotNetProjectAsync(
                    enricherProject,
                    [
                        "--cli-output", Path.Combine(cliDirectory, "cli-output.json"),
                        "--azmcp-commands", azmcpOutputFile,
                        "--output", enrichedOutputFile,
                    ],
                    noBuild: true,
                    context.McpToolsRoot,
                    cancellationToken);
                processResults.Add(enricherResult);
                if (!enricherResult.Succeeded)
                {
                    AddProcessIssue(enricherResult, warnings, "Tool metadata enrichment failed (non-blocking)");
                }
            }

            var brandMappingPath = Path.Combine(context.McpToolsRoot, "data", "brand-to-server-mapping.json");

            // Load brand entries once for both the CLI tab config and the namespace mapping emitter.
            // TODO(F5): ResolveCliTabNamespacesAsync also reads this file independently; consolidate
            // to a single read to eliminate the inconsistency window if these diverge.
            var brandEntries = await LoadBrandMappingEntriesAsync(brandMappingPath, cancellationToken);

            // F3: warn visibly when brand-to-server-mapping.json produces no entries
            if (brandEntries.Count == 0)
            {
                context.Reports.Warning("brand-to-server-mapping.json is empty or missing — namespace-mapping.json will contain no namespaces.");
            }

            var namespacesForConfig = await ResolveCliTabNamespacesAsync(brandMappingPath, context.SelectedNamespaces, cancellationToken);
            var cliTabConfig = CliTabConfig.ForNamespaces([.. namespacesForConfig]);
            var cliTabConfigPath = Path.Combine(context.OutputPath, "cli-tab-config.json");
            await File.WriteAllTextAsync(
                cliTabConfigPath,
                JsonSerializer.Serialize(cliTabConfig),
                Encoding.UTF8,
                cancellationToken);
            context.Reports.Info($"Generated cli-tab-config.json with {cliTabConfig.AllowedNamespaces.Count} namespace(s).");

            var unmatchedTools = await _namespaceMappingEmitter.EmitAsync(
                brandEntries,
                context.CliOutput!,
                context.CliVersion!,
                context.OutputPath,
                cancellationToken);

            // F1: warn when any tool was not matched to a namespace prefix
            if (unmatchedTools.Count > 0)
            {
                context.Reports.Warning(
                    $"namespace-mapping.json: {unmatchedTools.Count} tool(s) were not matched to any brand mapping namespace: {string.Join(", ", unmatchedTools)}");
            }

            context.Reports.Info("Emitted namespace-mapping.json.");

            CreateDirectories(context.OutputPath, BaseOutputDirectories);
            return BuildResult(context, processResults, success: true, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add(ex.Message);
            return BuildResult(context, processResults, success: false, warnings);
        }
    }

    /// <summary>
    /// Reads the pinned azure.mcp tool version from <c>mcp-tool-version.txt</c> in the repo root.
    /// Returns <c>null</c> if the file does not exist, signalling a fallback to the latest version.
    /// </summary>
    internal static async ValueTask<string?> ReadMcpToolVersionAsync(string repoRoot, CancellationToken cancellationToken)
    {
        var versionFile = Path.Combine(repoRoot, "mcp-tool-version.txt");
        if (!File.Exists(versionFile))
            return null;
        var content = await File.ReadAllTextAsync(versionFile, cancellationToken);
        return content.Trim();
    }

    private static bool NeedsAiConfiguration(PipelineContext context)
        => context.PlannedSteps
            .OfType<StepDefinition>()
            .Any(step => step.RequiresAiConfiguration);

    /// <summary>
    /// Returns true when all six namespace steps (1–6) are planned, indicating a full pipeline run.
    /// Partial runs (e.g., --steps 4) preserve existing output from prior steps.
    /// </summary>
    private static bool IsFullPipelineRun(PipelineContext context)
    {
        var requestedSteps = context.Request.Steps;
        var fullRun = new HashSet<int> { 1, 2, 3, 4, 5, 6 };
        return fullRun.IsSubsetOf(requestedSteps);
    }

    private static void ResetOutputDirectory(string outputPath)
    {
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, recursive: true);
        }

        Directory.CreateDirectory(outputPath);
    }

    private static void CreateDirectories(string outputPath, IEnumerable<string> directories)
    {
        foreach (var directory in directories)
        {
            Directory.CreateDirectory(Path.Combine(outputPath, directory));
        }
    }

    /// <summary>
    /// Loads <c>brand-to-server-mapping.json</c> from the given path as a list of <see cref="BrandMappingEntry"/>.
    /// Returns an empty list if the file does not exist or contains malformed JSON.
    /// </summary>
    private static async Task<IReadOnlyList<BrandMappingEntry>> LoadBrandMappingEntriesAsync(
        string brandMappingPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(brandMappingPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(brandMappingPath, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<BrandMappingEntry[]>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> GetRequiredArtifacts(string mcpToolsRoot, IReadOnlyList<IPipelineStep> plannedSteps)
    {
        var requiredProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DocGeneration.Steps.Bootstrap.BrandMappings",
            "DocGeneration.Steps.Bootstrap.E2eTestPromptParser",
            "DocGeneration.Steps.Bootstrap.CommandParser",
            "DocGeneration.Steps.Bootstrap.ToolMetadataEnricher",
        };

        foreach (var step in plannedSteps.OfType<StepDefinition>())
        {
            foreach (var project in step.Id switch
            {
                1 => ["DocGeneration.Steps.AnnotationsParametersRaw.Annotations", "DocGeneration.Steps.AnnotationsParametersRaw.RawTools"],
                2 => ["DocGeneration.Steps.ExamplePrompts.Generation", "DocGeneration.Steps.ExamplePrompts.Validation"],
                3 => ["DocGeneration.Steps.ToolGeneration.Composition", "DocGeneration.Steps.ToolGeneration.Improvements"],
                4 => ["DocGeneration.Steps.ToolFamilyCleanup"],
                5 => ["DocGeneration.Steps.SkillsRelevance"],
                6 => ["DocGeneration.Steps.HorizontalArticles"],
                _ => Array.Empty<string>(),
            })
            {
                requiredProjects.Add(project);
            }
        }

        var targetFramework = Path.GetFileName(Path.GetDirectoryName(typeof(BootstrapStep).Assembly.Location)) ?? "net10.0";

        return requiredProjects
            .Select(project => Path.Combine(mcpToolsRoot, project, "bin", "Release", targetFramework, $"{project}.dll"))
            .ToArray();
    }

    /// <summary>
    /// Resolves the set of namespaces to include in cli-tab-config.json.
    /// Uses brand-to-server-mapping.json as the source of truth, expanding
    /// merge group peers when a single namespace is specified.
    /// Falls back to selectedNamespaces if the brand mapping is empty or unavailable.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> ResolveCliTabNamespacesAsync(
        string brandMappingPath,
        IReadOnlyList<string> selectedNamespaces,
        CancellationToken cancellationToken)
    {
        List<BrandMapping> brandMappings = [];
        if (File.Exists(brandMappingPath))
        {
            var json = await File.ReadAllTextAsync(brandMappingPath, cancellationToken);
            try
            {
                brandMappings = JsonSerializer.Deserialize<List<BrandMapping>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                }) ?? [];
            }
            catch (JsonException)
            {
                // Corrupt brand-mapping file -- fall back to selectedNamespaces
                return selectedNamespaces;
            }
        }

        var validMappings = brandMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.McpServerName))
            .ToList();

        // All-namespace run: include every namespace in the brand mapping
        if (selectedNamespaces.Count == 0)
        {
            var all = validMappings.Select(m => m.McpServerName!).ToArray();
            return all.Length > 0 ? all : selectedNamespaces;
        }

        // Single-namespace run: include the namespace + any merge-group peers
        var result = new HashSet<string>(selectedNamespaces, StringComparer.OrdinalIgnoreCase);
        foreach (var ns in selectedNamespaces)
        {
            var entry = validMappings.FirstOrDefault(m =>
                string.Equals(m.McpServerName, ns, StringComparison.OrdinalIgnoreCase));
            if (entry?.MergeGroup is { Length: > 0 } mergeGroup)
            {
                foreach (var peer in validMappings.Where(m =>
                    string.Equals(m.MergeGroup, mergeGroup, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(peer.McpServerName!);
                }
            }
        }

        return [.. result];
    }

    private static void AddProcessIssue(ProcessExecutionResult processResult, ICollection<string> warnings, string summary)
    {
        warnings.Add($"{summary} (exit code {processResult.ExitCode}).");

        if (!string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            warnings.Add(processResult.StandardError.Trim());
        }

        if (!string.IsNullOrWhiteSpace(processResult.StandardOutput))
        {
            warnings.Add(processResult.StandardOutput.Trim());
        }
    }

    /// <summary>
    /// Downloads a file from the upstream microsoft/mcp repo. Falls back to a local copy on failure.
    /// </summary>
    internal static async Task<string> FetchUpstreamFileAsync(
        string remoteUrl,
        string localFallback,
        string displayName,
        PipelineContext context,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"mcp-upstream-{Environment.ProcessId}-{displayName}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Reports.Info($"Fetching {displayName} from {remoteUrl}");
            var content = await SharedHttpClient.GetStringAsync(remoteUrl, cancellationToken);
            await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, cancellationToken);
            context.Reports.Info($"Fetched {displayName} ({content.Length:N0} bytes)");
            return tempPath;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Propagate user cancellation (Ctrl+C), don't fall back
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            warnings.Add($"Failed to fetch {displayName} from upstream: {ex.Message}. Using local fallback.");
            if (File.Exists(localFallback))
            {
                return localFallback;
            }

            throw new FileNotFoundException(
                $"Neither upstream ({remoteUrl}) nor local fallback ({localFallback}) for {displayName} is available.");
        }
    }

    private StepResult BuildResult(
        PipelineContext context,
        IReadOnlyCollection<ProcessExecutionResult> processResults,
        bool success,
        IEnumerable<string> warnings,
        int? exitCodeOverride = null)
    {
        var resolvedWarnings = warnings
            .Where(static warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var outputs = ExpectedOutputs
            .Select(relativePath => Path.Combine(context.OutputPath, relativePath))
            .ToArray();
        var commands = processResults.Select(result => result.DisplayCommand).ToArray();
        var duration = TimeSpan.FromTicks(processResults.Sum(result => result.Duration.Ticks));

        return new StepResult(success, resolvedWarnings, duration, outputs, commands, Array.Empty<ValidatorResult>(), Array.Empty<ArtifactFailure>(), exitCodeOverride);
    }

    /// <summary>
    /// Generates deterministic H2 headings from CLI metadata and writes h2-headings.json
    /// per namespace. Runs once in bootstrap so all downstream steps can consume the file.
    /// </summary>
    private static async Task GenerateH2HeadingsAsync(PipelineContext context)
    {
        if (context.CliOutput?.Tools == null || context.CliOutput.Tools.Count == 0) return;

        var tools = context.CliOutput.Tools;
        var compoundWords = await DataFileLoader.LoadCompoundWordsAsync();

        // Group tools by namespace (first segment of command)
        var byNamespace = tools
            .Where(t => !string.IsNullOrWhiteSpace(t.Command))
            .GroupBy(t => t.Command.Split(' ')[0].ToLowerInvariant());

        var totalHeadings = 0;
        foreach (var nsGroup in byNamespace)
        {
            var nsName = nsGroup.Key;
            var toolData = nsGroup
                .Select(t => (command: t.Command, description: t.Description))
                .ToList();

            var headings = DeterministicH2HeadingGenerator.GenerateHeadings(toolData!, compoundWords);

            // Write to the appropriate output directory
            var nsOutputDir = string.IsNullOrWhiteSpace(context.Request.Namespace)
                ? context.OutputPath  // all-namespace run → ./generated/
                : context.OutputPath; // single-namespace run → ./generated-{ns}/

            var h2Dir = Path.Combine(nsOutputDir, "h2-headings");
            Directory.CreateDirectory(h2Dir);

            var h2Path = Path.Combine(h2Dir, $"{nsName}.json");
            var json = JsonSerializer.Serialize(headings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(h2Path, json, Encoding.UTF8);
            totalHeadings += headings.Count;
        }

        context.Reports.Info($"Generated {totalHeadings} deterministic H2 headings for {byNamespace.Count()} namespace(s).");
    }
}
