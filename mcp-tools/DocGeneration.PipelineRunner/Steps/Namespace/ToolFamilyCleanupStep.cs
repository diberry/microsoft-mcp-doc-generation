using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using Shared;

namespace PipelineRunner.Steps;

public sealed class ToolFamilyCleanupStep : NamespaceStepBase
{
    public ToolFamilyCleanupStep()
        : base(
            4,
            "Generate tool-family article",
            FailurePolicy.Fatal,
            dependsOn: [3],
            postValidators: [new ToolFamilyPostAssemblyValidator()],
            requiresAiConfiguration: true,
            usesIsolatedWorkspace: true,
            expectedOutputs: ["tool-family-metadata", "tool-family-related", "tool-family", "reports"],
            maxRetries: 2)
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (currentNamespace, _, _, matchingTools) = ResolveTarget(context);
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var artifactFailures = new List<ArtifactFailure>();
        var familyName = ResolveFamilyName(currentNamespace, matchingTools);
        var outputFileName = await ResolveFamilyOutputFileNameFromContextAsync(familyName, context);
        context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = familyName;
        context.Items[ToolFamilyPostAssemblyValidator.OutputFileNameContextKey] = outputFileName;

        var toolsInputDirectory = Path.Combine(context.OutputPath, "tools");
        if (!Directory.Exists(toolsInputDirectory))
        {
            warnings.Add($"Tools directory not found: '{toolsInputDirectory}'. Run Step 3 first.");
            artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        var matchingToolFiles = await ResolveFamilyToolFilesAsync(toolsInputDirectory, familyName, cancellationToken);
        if (matchingToolFiles.Count == 0)
        {
            warnings.Add($"No tool files found for family '{familyName}' in '{toolsInputDirectory}'.");
            artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
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

            var cliVersionPath = Path.Combine(context.OutputPath, "cli", "cli-version.json");
            if (File.Exists(cliVersionPath))
            {
                File.Copy(cliVersionPath, Path.Combine(tempCliDirectory, "cli-version.json"), overwrite: true);
            }
            else
            {
                warnings.Add($"CLI version file not found at '{cliVersionPath}'. Tool-family cleanup will use 'unknown'.");
            }

            var brandMappingSource = Path.Combine(context.DocsGenerationRoot, "data", "brand-to-server-mapping.json");
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
            var h2HeadingsSource = Path.Combine(context.OutputPath, "h2-headings");
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
            if (!cleanupResult.Succeeded)
            {
                AddProcessIssue(cleanupResult, warnings, "Tool-family cleanup failed");
                artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
            }

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
                    warnings.Add("Subprocess exited 0 but produced no output files. Check AI credentials and rate limits.");
                }

                warnings.AddRange(copyBackIssues);
                artifactFailures.Add(CreateFamilyFailure(context, familyName, outputFileName, warnings));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
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

    private static string ResolveFamilyName(string currentNamespace, IReadOnlyList<CliTool> matchingTools)
    {
        var firstCommand = matchingTools.Count > 0 ? matchingTools[0].Command : currentNamespace;
        var tokens = firstCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("Unable to infer a tool-family name for Step 4.");
        }

        return tokens[0].ToLowerInvariant();
    }

    private static async Task<IReadOnlyList<string>> ResolveFamilyToolFilesAsync(string toolsInputDirectory, string familyName, CancellationToken cancellationToken)
    {
        var toolFiles = Directory.EnumerateFiles(toolsInputDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Step 1: Try content matching (annotation-based) on ALL files
        var contentMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in toolFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var namespaceFromFile = await GetToolNamespaceFromFileAsync(filePath, cancellationToken);
            if (string.Equals(namespaceFromFile, familyName, StringComparison.OrdinalIgnoreCase))
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

    private static async Task<string?> GetToolNamespaceFromFileAsync(string filePath, CancellationToken cancellationToken)
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
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return null;
        }

        var tokens = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 0 ? null : tokens[0].ToLowerInvariant();
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
}
