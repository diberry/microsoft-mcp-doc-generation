using GenerativeAI;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using Shared;
using Shared.Validation;
using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using ToolFamilyCleanup.Validation;

namespace PipelineRunner.Steps;

public sealed class ToolFamilyCleanupStep : NamespaceStepBase
{
    public const string FamilyCleanupOverrideKey = "ToolFamilyCleanupStep.FamilyCleanupOverride";

    private static readonly ReducerRegistry Reducers = new();
    private static readonly UpstreamArtifactResolver UpstreamArtifacts = new();

    static ToolFamilyCleanupStep()
    {
        Reducers.Register(4, static async (ctx, ct) =>
        {
            if (ctx is not FamilyStructureReducerInput input)
            {
                throw new InvalidOperationException($"Reducer input for step 4 must be {nameof(FamilyStructureReducerInput)}.");
            }

            var builder = new FamilyStructureBuilder();
            return await builder.BuildAsync(input.ToolsDirectory, input.FamilyName, input.H2HeadingsDirectory, ct);
        });

        Reducers.RegisterValidator(new FamilyStructureContextValidator());
    }

    public ToolFamilyCleanupStep()
        : base(
            4,
            "Generate tool-family article",
            FailurePolicy.Fatal,
            dependsOn: [3],
            postValidators: [new ToolFamilyPostAssemblyValidator(), new CompositionOutputValidator()],
            requiresAiConfiguration: true,
            usesIsolatedWorkspace: true,
            expectedOutputs: ["tool-family-metadata", "tool-family-related", "tool-family", "reports"],
            maxRetries: 2)
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (currentNamespace, _, _, matchingTools) = ResolveTarget(context);
        var useReducerPath = Reducers.HasReducer(Id);
        var hasCleanupOverride = context.Items.ContainsKey(FamilyCleanupOverrideKey);
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var artifactFailures = new List<ArtifactFailure>();
        var brandMappings = await DataFileLoader.LoadBrandMappingsAsync();
        var familyName = ResolveFamilyName(currentNamespace, matchingTools, brandMappings);
        var outputFileName = await ResolveFamilyOutputFileNameFromContextAsync(familyName, context);
        context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = familyName;
        context.Items[ToolFamilyPostAssemblyValidator.OutputFileNameContextKey] = outputFileName;

        var bootstrapEnvelope = UpstreamArtifacts.TryReadUpstream(context.OutputPath, 0, "bootstrap-pipeline");
        var step1Envelope = UpstreamArtifacts.TryReadUpstream(context.OutputPath, 1, "generate-annotations-parameters-and-raw-tools");
        var step3Envelope = UpstreamArtifacts.TryReadUpstream(context.OutputPath, 3, "compose-and-improve-tool-files");

        var toolsInputDirectory = ResolveUpstreamDirectory(
            context.OutputPath,
            step3Envelope,
            "tools",
            Path.Combine(context.OutputPath, "tools"),
            "step 3");
        var toolsRawDirectory = ResolveUpstreamDirectory(
            context.OutputPath,
            step3Envelope,
            "tools-raw",
            Path.Combine(context.OutputPath, "tools-raw"),
            "step 3");
        var cliVersionPath = ResolveUpstreamFile(
            context.OutputPath,
            bootstrapEnvelope,
            Path.Combine("cli", "cli-version.json"),
            Path.Combine(context.OutputPath, "cli", "cli-version.json"),
            "bootstrap");
        var h2HeadingsSource = ResolveUpstreamDirectory(
            context.OutputPath,
            bootstrapEnvelope,
            "h2-headings",
            Path.Combine(context.OutputPath, "h2-headings"),
            "bootstrap");
        var cliTabConfigPath = ResolveUpstreamFile(
            context.OutputPath,
            bootstrapEnvelope,
            "cli-tab-config.json",
            Path.Combine(context.OutputPath, "cli-tab-config.json"),
            "bootstrap");
        var cliOutputPath = ResolveUpstreamFile(
            context.OutputPath,
            bootstrapEnvelope,
            Path.Combine("cli", "cli-output.json"),
            Path.Combine(context.OutputPath, "cli", "cli-output.json"),
            "bootstrap");
        var parameterCliDir = ResolveUpstreamDirectory(
            context.OutputPath,
            step1Envelope,
            "parameter-cli",
            Path.Combine(context.OutputPath, "parameter-cli"),
            "step 1");
        var exampleCommandsDir = ResolveUpstreamDirectory(
            context.OutputPath,
            step1Envelope,
            "example-commands",
            Path.Combine(context.OutputPath, "example-commands"),
            "step 1");

        // Fallback: if tools/ is empty or doesn't exist, try tools-raw/ (#602)
        if (!Directory.Exists(toolsInputDirectory)
            || !Directory.EnumerateFiles(toolsInputDirectory, "*.md", SearchOption.TopDirectoryOnly).Any())
        {
            if (Directory.Exists(toolsRawDirectory)
                && Directory.EnumerateFiles(toolsRawDirectory, "*.md", SearchOption.TopDirectoryOnly).Any())
            {
                Console.WriteLine("INFO: Using tools-raw/ as fallback (tools/ not available).");
                toolsInputDirectory = toolsRawDirectory;
            }
            else if (!Directory.Exists(toolsInputDirectory))
            {
                warnings.Add($"Tools directory not found: '{toolsInputDirectory}'. Run Step 3 first.");
                artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
            }
        }

        var matchingToolFiles = await ResolveFamilyToolFilesAsync(toolsInputDirectory, familyName, cancellationToken);
        if (matchingToolFiles.Count == 0)
        {
            warnings.Add($"No tool files found for family '{familyName}' in '{toolsInputDirectory}'.");
            artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        if (useReducerPath)
        {
            try
            {
                var (artifacts, preAiErrors) = await GenerateFamilyWithReducerAsync(
                    context,
                    toolsInputDirectory,
                    familyName,
                    h2HeadingsSource,
                    cliVersionPath,
                    brandMappings,
                    warnings,
                    cancellationToken);

                if (preAiErrors.Count > 0)
                {
                    var errorMessages = preAiErrors.Select(static e => e.Message).ToArray();
                    warnings.AddRange(errorMessages);
                    artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
                    return BuildResult(
                        context,
                        processResults,
                        true,
                        warnings,
                        [new ValidatorResult("pre-ai-validation", false, errorMessages)],
                        artifactFailures);                }

                WriteFamilyArtifacts(context.OutputPath, outputFileName, artifacts!);
                RemoveStaleFamilyFiles(context.OutputPath, familyName, outputFileName);
                await ApplyCliTabWrappingAsync(
                    context,
                    currentNamespace,
                    cliTabConfigPath,
                    cliOutputPath,
                    parameterCliDir,
                    exampleCommandsDir,
                    toolsRawDirectory,
                    Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md"),
                    warnings,
                    cancellationToken);

                return BuildResult(context, processResults, true, warnings, artifactFailures: artifactFailures);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (hasCleanupOverride)
                {
                    warnings.Add($"Tool-family cleanup failed: {ex.Message}");
                    artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
                    return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
                }

                Console.WriteLine($"Reducer path failed for family '{familyName}', falling back to subprocess: {ex.Message}");
            }
        }

        var tempRoot = context.Workspaces.CreateTemporaryDirectory("pipeline-runner-step4");
        try
        {
            var tempDocsDirectory = Path.Combine(tempRoot, "mcp-tools");
            var tempGeneratedDirectory = Path.Combine(tempRoot, "generated");
            var tempToolsDirectory = Path.Combine(tempGeneratedDirectory, "tools");
            var tempCliDirectory = Path.Combine(tempGeneratedDirectory, "cli");
            var tempDataDirectory = Path.Combine(tempDocsDirectory, "data");

            Directory.CreateDirectory(tempDocsDirectory);
            Directory.CreateDirectory(tempToolsDirectory);
            Directory.CreateDirectory(tempCliDirectory);
            Directory.CreateDirectory(tempDataDirectory);

            if (File.Exists(cliVersionPath))
            {
                File.Copy(cliVersionPath, Path.Combine(tempCliDirectory, "cli-version.json"), overwrite: true);
            }
            else
            {
                warnings.Add($"CLI version file not found at '{cliVersionPath}'. Tool-family cleanup will use 'unknown'.");
            }

            var brandMappingSource = Path.Combine(context.McpToolsRoot, "data", "brand-to-server-mapping.json");
            if (File.Exists(brandMappingSource))
            {
                File.Copy(brandMappingSource, Path.Combine(tempDocsDirectory, "brand-to-server-mapping.json"), overwrite: true);
                File.Copy(brandMappingSource, Path.Combine(tempDataDirectory, "brand-to-server-mapping.json"), overwrite: true);
            }

            foreach (var filePath in matchingToolFiles)
            {
                var destinationPath = Path.Combine(tempToolsDirectory, Path.GetFileName(filePath));
                File.Copy(filePath, destinationPath, overwrite: true);
            }

            // Copy h2-headings JSON for deterministic heading lookup in Phase 1.5
            if (Directory.Exists(h2HeadingsSource))
            {
                var tempH2Directory = Path.Combine(tempGeneratedDirectory, "h2-headings");
                Directory.CreateDirectory(tempH2Directory);
                foreach (var filePath in Directory.EnumerateFiles(h2HeadingsSource, "*.json"))
                {
                    File.Copy(filePath, Path.Combine(tempH2Directory, Path.GetFileName(filePath)), overwrite: true);
                }
            }

            var cleanupResult = await context.ProcessRunner.RunDotNetProjectAsync(
                GetProjectPath(context, "DocGeneration.Steps.ToolFamilyCleanup"),
                ["--multi-phase"],
                context.Request.SkipBuild,
                tempDocsDirectory,
                cancellationToken);
            processResults.Add(cleanupResult);

            // FIX #478: Check output files BEFORE failing on exit code
            var tempMetadataDirectory = Path.Combine(tempGeneratedDirectory, "tool-family-metadata");
            var tempRelatedDirectory = Path.Combine(tempGeneratedDirectory, "tool-family-related");
            var tempFinalDirectory = Path.Combine(tempGeneratedDirectory, "tool-family");
            var expectedMetadataFile = Path.Combine(tempMetadataDirectory, $"{outputFileName}-metadata.md");
            var expectedRelatedFile = Path.Combine(tempRelatedDirectory, $"{outputFileName}-related.md");
            var expectedFinalFile = Path.Combine(tempFinalDirectory, $"{outputFileName}.md");

            var copyBackIssues = new List<string>();
            if (!File.Exists(expectedMetadataFile))
            {
                copyBackIssues.Add($"Expected isolated metadata output at '{expectedMetadataFile}'.");
            }
            if (!File.Exists(expectedRelatedFile))
            {
                copyBackIssues.Add($"Expected isolated related-content output at '{expectedRelatedFile}'.");
            }
            if (!File.Exists(expectedFinalFile))
            {
                copyBackIssues.Add($"Expected isolated tool-family output at '{expectedFinalFile}'.");
            }

            if (copyBackIssues.Count > 0)
            {
                // Output files are missing - NOW check exit code and fail
                if (!cleanupResult.Succeeded)
                {
                    AddProcessIssue(cleanupResult, warnings, "Tool-family cleanup failed");
                }

                // Surface subprocess stdout so the "✗ Failed to process" message is visible (#160)
                if (!string.IsNullOrWhiteSpace(cleanupResult.StandardOutput))
                {
                    var relevantLines = cleanupResult.StandardOutput
                        .Split('\n')
                        .Where(line => line.Contains("✗") || line.Contains("Failed") || line.Contains("Error"))
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrEmpty(line))
                        .ToArray();
                    if (relevantLines.Length > 0)
                    {
                        warnings.Add($"Subprocess output: {string.Join(" | ", relevantLines)}");
                    }
                }

                // Always add diagnostic hint when no subprocess error lines were surfaced
                if (!warnings.Any(w => w.Contains("Subprocess output:")))
                {
                    var diagnosticMessage = cleanupResult.Succeeded
                        ? "Subprocess exited 0 but produced no output files. Check AI credentials and rate limits."
                        : $"Subprocess exited {cleanupResult.ExitCode} and produced no output files.";
                    warnings.Add(diagnosticMessage);
                }

                warnings.AddRange(copyBackIssues);
                artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
            }

            // FIX #478: Output files exist - treat non-zero exit code as warning, not failure
            if (!cleanupResult.Succeeded)
            {
                warnings.Add($"Subprocess exited with code {cleanupResult.ExitCode} but all output files were generated successfully. Treating as warning.");
            }

            CopyMarkdownFiles(tempMetadataDirectory, Path.Combine(context.OutputPath, "tool-family-metadata"));
            CopyMarkdownFiles(tempRelatedDirectory, Path.Combine(context.OutputPath, "tool-family-related"));
            CopyMarkdownFiles(tempFinalDirectory, Path.Combine(context.OutputPath, "tool-family"));

            // Clean stale files with old naming convention (#267)
            if (!string.Equals(familyName, outputFileName, StringComparison.OrdinalIgnoreCase))
            {
                RemoveStaleFile(Path.Combine(context.OutputPath, "tool-family", $"{familyName}.md"));
                RemoveStaleFile(Path.Combine(context.OutputPath, "tool-family-metadata", $"{familyName}-metadata.md"));
                RemoveStaleFile(Path.Combine(context.OutputPath, "tool-family-related", $"{familyName}-related.md"));
            }

            // Apply CLI tab wrapping to the generated tool-family article
            var familyArticlePath = Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md");
            if (File.Exists(familyArticlePath))
            {
                // Check if CLI tab generation is allowed for this namespace
                var cliTabConfig = CliTabConfig.LoadFromFile(cliTabConfigPath);
                if (!cliTabConfig.IsNamespaceAllowed(currentNamespace))
                {
                    Console.WriteLine($"  ⊘ CLI tab generation disabled for namespace '{currentNamespace}'");
                    return BuildResult(context, processResults, true, warnings, artifactFailures: artifactFailures);
                }

                try
                {
                    if (File.Exists(cliOutputPath) && Directory.Exists(parameterCliDir) && Directory.Exists(exampleCommandsDir))
                    {
                        var cliJson = await File.ReadAllTextAsync(cliOutputPath, cancellationToken);
                        var allCliTools = CliJsonMapper.MapFromCliOutput(cliJson);
                        var nameContext = await FileNameContext.CreateAsync();

                        // Filter CLI tools to only those in the current namespace
                        var cliTools = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (key, tool) in allCliTools)
                        {
                            if (key.StartsWith(currentNamespace + " ", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals(currentNamespace, StringComparison.OrdinalIgnoreCase))
                                cliTools[key] = tool;
                        }

                        // Extract NLP descriptions from tools-raw files (source of truth)
                        var nlpDescriptions = await NlpDescriptionExtractor.ExtractNlpDescriptionsAsync(
                            toolsRawDirectory, nameContext, cliTools.Keys);

                        // Align CLI descriptions with NLP descriptions (deterministic, no AI)
                        if (nlpDescriptions.Count > 0)
                        {
                            var improver = new CliProseImprover();
                            var improved = await improver.ImproveProseAsync(cliTools, nlpDescriptions, cancellationToken: cancellationToken);
                            cliTools = new Dictionary<string, CliToolInfo>(improved, StringComparer.OrdinalIgnoreCase);
                            Console.WriteLine($"  ✓ Aligned {cliTools.Count} CLI descriptions with NLP (deterministic)");
                        }

                        var assembledContent = await CliContentAssembler.AssembleAllCliContentAsync(
                            cliTools, parameterCliDir, exampleCommandsDir, nameContext);

                        // Reconciliation gate: validate MCP↔CLI description alignment
                        if (nlpDescriptions.Count > 0)
                        {
                            var cliDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var (key, tool) in cliTools)
                                cliDescriptions[key] = tool.Description;

                            var alignmentResult = DescriptionAlignmentValidator
                                .Validate(nlpDescriptions, cliDescriptions);

                            foreach (var warning in alignmentResult.Warnings)
                            {
                                Console.WriteLine($"  ⚠ Alignment: {warning}");
                                warnings.Add($"Description alignment warning: {warning}");
                            }
                            foreach (var error in alignmentResult.Errors)
                            {
                                Console.WriteLine($"  ✗ Alignment: {error}");
                                warnings.Add($"Description alignment error: {error}");
                            }
                        }

                        if (assembledContent.Count > 0)
                        {
                            var familyMarkdown = await File.ReadAllTextAsync(familyArticlePath, cancellationToken);
                            var tabbedMarkdown = CliTabWrapper.ApplyTabsToFamilyArticle(familyMarkdown, assembledContent);
                            await File.WriteAllTextAsync(familyArticlePath, tabbedMarkdown, cancellationToken);
                        }
                    }
                    else
                    {
                        warnings.Add("CLI tab wrapping skipped: missing cli-output.json, parameter-cli, or example-commands directories.");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"CLI tab wrapping failed (non-fatal): {ex.Message}");
                }
            }

            return BuildResult(context, processResults, true, warnings, artifactFailures: artifactFailures);
        }
        finally
        {
            context.Workspaces.Delete(tempRoot);
        }
    }

    private static ArtifactFailure CreateFamilyFailure(PipelineContext context, string familyName, string outputFileName, IEnumerable<string> details)
        => CreateArtifactFailure(
            "tool family",
            familyName,
            "Tool-family generation failed for this family.",
            details,
            [
                Path.Combine(context.OutputPath, "tool-family", $"{outputFileName}.md"),
                Path.Combine(context.OutputPath, "tool-family-metadata", $"{outputFileName}-metadata.md"),
                Path.Combine(context.OutputPath, "tool-family-related", $"{outputFileName}-related.md"),
                Path.Combine(context.OutputPath, "reports", $"tool-family-validation-{familyName}.txt"),
            ]);

    private static void CopyMarkdownFiles(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*.md", SearchOption.TopDirectoryOnly))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }
    }

    private static void RemoveStaleFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static async Task<string> ResolveFamilyOutputFileNameFromContextAsync(string familyName, PipelineContext _)
    {
        return await ToolFileNameBuilder.ResolveFamilyFileNameAsync(familyName);
    }

    public sealed record FamilyCleanupArtifacts(string Metadata, string RelatedContent, string FinalContent);

    private sealed record FamilyStructureReducerInput(string ToolsDirectory, string FamilyName, string? H2HeadingsDirectory);

    private static string ResolveFamilyName(string currentNamespace, IReadOnlyList<CliTool> matchingTools,
        IReadOnlyDictionary<string, BrandMapping> brandMappings)
    {
        // Direct lookup (works when namespace has no underscores, e.g. "monitor", "functionapp").
        if (brandMappings.ContainsKey(currentNamespace))
        {
            return currentNamespace.ToLowerInvariant();
        }

        // GetCurrentNamespace normalizes underscores to spaces ("extension_azqr" → "extension azqr").
        // Re-introduce underscores for the brand mapping key lookup so that decomposed namespaces
        // like "extension_azqr", "extension_cli_generate" resolve to their configured fileName (#603).
        var underscoredNamespace = currentNamespace.Replace(' ', '_');
        if (underscoredNamespace != currentNamespace && brandMappings.ContainsKey(underscoredNamespace))
        {
            return underscoredNamespace.ToLowerInvariant();
        }

        var firstCommand = matchingTools.Count > 0 ? matchingTools[0].Command : currentNamespace;
        var tokens = firstCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("Unable to infer a tool-family name for Step 4.");
        }

        return tokens[0].ToLowerInvariant();
    }

    private static string ResolveUpstreamDirectory(
        string outputPath,
        StepResultFile? envelope,
        string relativeDirectory,
        string fallbackPath,
        string upstreamStep)
    {
        if (UpstreamArtifacts.TryResolveOutputDirectory(outputPath, envelope, relativeDirectory, out var resolvedPath))
        {
            Console.WriteLine(
                $"INFO: Using {upstreamStep} envelope-based resolution for '{relativeDirectory}' at '{resolvedPath}'.");
            return resolvedPath;
        }

        return fallbackPath;
    }

    private static string ResolveUpstreamFile(
        string outputPath,
        StepResultFile? envelope,
        string relativeFilePath,
        string fallbackPath,
        string upstreamStep)
    {
        if (UpstreamArtifacts.TryResolveOutputFile(outputPath, envelope, relativeFilePath, out var resolvedPath))
        {
            Console.WriteLine(
                $"INFO: Using {upstreamStep} envelope-based resolution for '{relativeFilePath}' at '{resolvedPath}'.");
            return resolvedPath;
        }

        return fallbackPath;
    }

    private static async Task<IReadOnlyList<string>> ResolveFamilyToolFilesAsync(string toolsInputDirectory, string familyName, CancellationToken cancellationToken)
    {
        var toolFiles = Directory.EnumerateFiles(toolsInputDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Step 1: Try content matching (annotation-based) on ALL files.
        // Normalize familyName for comparison: underscores become spaces (e.g., "extension_azqr" → "extension azqr")
        // so it matches the multi-token CLI commands recorded in @mcpcli annotations.
        var normalizedFamilyName = familyName.Replace('_', ' ');
        var contentMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in toolFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var commandFromFile = await GetToolCommandFromFileAsync(filePath, cancellationToken);
            if (commandFromFile != null &&
                (commandFromFile.Equals(normalizedFamilyName, StringComparison.OrdinalIgnoreCase) ||
                 commandFromFile.StartsWith(normalizedFamilyName + " ", StringComparison.OrdinalIgnoreCase)))
            {
                contentMatches.Add(filePath);
            }
        }

        // Step 2: For files NOT matched by content, try prefix matching
        var prefixes = await GetPrefixesAsync(familyName, cancellationToken);
        var prefixMatches = toolFiles
            .Where(path => !contentMatches.Contains(path)) // Only match files not already matched by content
            .Where(path => prefixes.Any(prefix =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                return string.Equals(fileName, prefix, StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith($"{prefix}-", StringComparison.OrdinalIgnoreCase);
            }))
            .ToArray();

        // Step 3: Return union of both strategies (deduplicated)
        return contentMatches.Concat(prefixMatches)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<string?> GetToolCommandFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var markerIndex = content.IndexOf("@mcpcli", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var lineEnd = content.IndexOfAny(['\r', '\n'], markerIndex);
        var markerText = lineEnd >= 0 ? content[markerIndex..lineEnd] : content[markerIndex..];
        var commandText = markerText.Replace("@mcpcli", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<!--", string.Empty, StringComparison.Ordinal)
            .Replace("-->", string.Empty, StringComparison.Ordinal)
            .Trim();
        return string.IsNullOrWhiteSpace(commandText) ? null : commandText.ToLowerInvariant();
    }

    private static async Task<IReadOnlyList<string>> GetPrefixesAsync(string familyName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefixes = new List<string> { familyName };
        var brandMappings = await DataFileLoader.LoadBrandMappingsAsync();
        if (brandMappings.TryGetValue(familyName, out var mapping) && !string.IsNullOrWhiteSpace(mapping.FileName))
        {
            var mappedPrefix = mapping.FileName.ToLowerInvariant();
            prefixes.Add(mappedPrefix);
            if (!mappedPrefix.StartsWith("azure-", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add($"azure-{mappedPrefix}");
            }
        }

        prefixes.Add($"ai-{familyName}");
        prefixes.Add($"azure-{familyName}");
        return prefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(prefix => prefix.Length)
            .ToArray();
    }

    private static async Task<(FamilyCleanupArtifacts? Artifacts, IReadOnlyList<ValidationError> ValidationErrors)> GenerateFamilyWithReducerAsync(
        PipelineContext context,
        string toolsDirectory,
        string familyName,
        string? h2HeadingsDirectory,
        string cliVersionPath,
        IReadOnlyDictionary<string, BrandMapping> brandMappings,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var reducer = Reducers.GetReducer(4);
        if (reducer is null)
        {
            throw new InvalidOperationException("Reducer path was selected for step 4, but no reducer is registered.");
        }

        var structure = (FamilyStructureContext)await reducer(
            new FamilyStructureReducerInput(toolsDirectory, familyName, h2HeadingsDirectory),
            cancellationToken);

        var validationResult = await ReducerRegistry.AggregateAsync(Reducers.GetValidators<FamilyStructureContext>(), structure, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (null, validationResult.Errors);
        }

        var cleanupAsync = await ResolveFamilyCleanupAsync(context, familyName, cliVersionPath, brandMappings, warnings, cancellationToken);
        return (await cleanupAsync(structure, cancellationToken), System.Array.Empty<ValidationError>());
    }

    private static async Task<Func<FamilyStructureContext, CancellationToken, Task<FamilyCleanupArtifacts>>> ResolveFamilyCleanupAsync(
        PipelineContext context,
        string familyName,
        string cliVersionPath,
        IReadOnlyDictionary<string, BrandMapping> brandMappings,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (context.Items.TryGetValue(FamilyCleanupOverrideKey, out var overrideValue))
        {
            if (overrideValue is Func<FamilyStructureContext, CancellationToken, Task<FamilyCleanupArtifacts>> cleanupOverride)
            {
                return cleanupOverride;
            }

            throw new InvalidOperationException(
                $"Context item '{FamilyCleanupOverrideKey}' must be {typeof(Func<FamilyStructureContext, CancellationToken, Task<FamilyCleanupArtifacts>>).FullName}.");
        }

        var displayName = brandMappings.TryGetValue(familyName, out var mapping) && !string.IsNullOrWhiteSpace(mapping.BrandName)
            ? mapping.BrandName!
            : familyName;

        var cliVersion = "unknown";
        if (File.Exists(cliVersionPath))
        {
            var cliRoot = Path.GetDirectoryName(Path.GetDirectoryName(cliVersionPath)!)!;
            cliVersion = await CliVersionReader.ReadCliVersionAsync(cliRoot);
        }
        else
        {
            warnings.Add($"CLI version file not found at '{cliVersionPath}'. Tool-family cleanup will use 'unknown'.");
        }

        var cleanupGenerator = new CleanupGenerator(ResolveGenerativeAIOptions(context.McpToolsRoot), new CleanupConfiguration());
        return async (structure, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var artifacts = await cleanupGenerator.GenerateFromStructureAsync(structure, displayName, cliVersion, ct);
            return new FamilyCleanupArtifacts(artifacts.Metadata, artifacts.RelatedContent, artifacts.FinalContent);
        };
    }

    /// <summary>
    /// Resolves the generative AI options for the tool-family reducer, anchored to the
    /// mcp-tools root so keyless (DefaultAzureCredential) configuration in <c>.env</c> is
    /// loaded regardless of the current working directory. The reducer runs with an isolated
    /// working directory, so an empty <see cref="GenerativeAIOptions"/> would leave the endpoint
    /// and deployment unset and break the keyless path. Keyless is the intended, supported design.
    /// </summary>
    internal static GenerativeAIOptions ResolveGenerativeAIOptions(string mcpToolsRoot)
        => GenerativeAIOptions.LoadFromEnvironmentOrDotEnv(mcpToolsRoot);

    private static void WriteFamilyArtifacts(string outputPath, string outputFileName, FamilyCleanupArtifacts artifacts)
    {
        var metadataDirectory = Path.Combine(outputPath, "tool-family-metadata");
        var relatedDirectory = Path.Combine(outputPath, "tool-family-related");
        var finalDirectory = Path.Combine(outputPath, "tool-family");
        Directory.CreateDirectory(metadataDirectory);
        Directory.CreateDirectory(relatedDirectory);
        Directory.CreateDirectory(finalDirectory);

        File.WriteAllText(Path.Combine(metadataDirectory, $"{outputFileName}-metadata.md"), artifacts.Metadata);
        File.WriteAllText(Path.Combine(relatedDirectory, $"{outputFileName}-related.md"), artifacts.RelatedContent);
        File.WriteAllText(Path.Combine(finalDirectory, $"{outputFileName}.md"), artifacts.FinalContent);
    }

    private static void RemoveStaleFamilyFiles(string outputPath, string familyName, string outputFileName)
    {
        if (string.Equals(familyName, outputFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RemoveStaleFile(Path.Combine(outputPath, "tool-family", $"{familyName}.md"));
        RemoveStaleFile(Path.Combine(outputPath, "tool-family-metadata", $"{familyName}-metadata.md"));
        RemoveStaleFile(Path.Combine(outputPath, "tool-family-related", $"{familyName}-related.md"));
    }

    private static async Task ApplyCliTabWrappingAsync(
        PipelineContext context,
        string currentNamespace,
        string cliTabConfigPath,
        string cliOutputPath,
        string parameterCliDir,
        string exampleCommandsDir,
        string toolsRawDirectory,
        string familyArticlePath,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(familyArticlePath))
        {
            return;
        }

        var cliTabConfig = CliTabConfig.LoadFromFile(cliTabConfigPath);
        if (!cliTabConfig.IsNamespaceAllowed(currentNamespace))
        {
            Console.WriteLine($"  ⊘ CLI tab generation disabled for namespace '{currentNamespace}'");
            return;
        }

        try
        {
            if (File.Exists(cliOutputPath) && Directory.Exists(parameterCliDir) && Directory.Exists(exampleCommandsDir))
            {
                var cliJson = await File.ReadAllTextAsync(cliOutputPath, cancellationToken);
                var allCliTools = CliJsonMapper.MapFromCliOutput(cliJson);
                var nameContext = await FileNameContext.CreateAsync();

                var cliTools = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, tool) in allCliTools)
                {
                    if (key.StartsWith(currentNamespace + " ", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals(currentNamespace, StringComparison.OrdinalIgnoreCase))
                    {
                        cliTools[key] = tool;
                    }
                }

                var nlpDescriptions = await NlpDescriptionExtractor.ExtractNlpDescriptionsAsync(
                    toolsRawDirectory, nameContext, cliTools.Keys);

                if (nlpDescriptions.Count > 0)
                {
                    var improver = new CliProseImprover();
                    var improved = await improver.ImproveProseAsync(cliTools, nlpDescriptions, cancellationToken: cancellationToken);
                    cliTools = new Dictionary<string, CliToolInfo>(improved, StringComparer.OrdinalIgnoreCase);
                    Console.WriteLine($"  ✓ Aligned {cliTools.Count} CLI descriptions with NLP (deterministic)");
                }

                var assembledContent = await CliContentAssembler.AssembleAllCliContentAsync(
                    cliTools, parameterCliDir, exampleCommandsDir, nameContext);

                if (nlpDescriptions.Count > 0)
                {
                    var cliDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, tool) in cliTools)
                    {
                        cliDescriptions[key] = tool.Description;
                    }

                    var alignmentResult = DescriptionAlignmentValidator.Validate(nlpDescriptions, cliDescriptions);
                    foreach (var warning in alignmentResult.Warnings)
                    {
                        Console.WriteLine($"  ⚠ Alignment: {warning}");
                        warnings.Add($"Description alignment warning: {warning}");
                    }

                    foreach (var error in alignmentResult.Errors)
                    {
                        Console.WriteLine($"  ✗ Alignment: {error}");
                        warnings.Add($"Description alignment error: {error}");
                    }
                }

                if (assembledContent.Count > 0)
                {
                    var familyMarkdown = await File.ReadAllTextAsync(familyArticlePath, cancellationToken);
                    var tabbedMarkdown = CliTabWrapper.ApplyTabsToFamilyArticle(familyMarkdown, assembledContent);
                    await File.WriteAllTextAsync(familyArticlePath, tabbedMarkdown, cancellationToken);
                }
            }
            else
            {
                warnings.Add("CLI tab wrapping skipped: missing cli-output.json, parameter-cli, or example-commands directories.");
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"CLI tab wrapping failed (non-fatal): {ex.Message}");
        }
    }
}
