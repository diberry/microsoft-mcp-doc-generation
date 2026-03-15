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
        var familyName = ResolveFamilyName(currentNamespace, matchingTools);
        context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = familyName;

        var toolsInputDirectory = Path.Combine(context.OutputPath, "tools");
        if (!Directory.Exists(toolsInputDirectory))
        {
            return BuildResult(context, processResults, false, [$"Tools directory not found: '{toolsInputDirectory}'. Run Step 3 first."]);
        }

        var matchingToolFiles = await ResolveFamilyToolFilesAsync(toolsInputDirectory, familyName, cancellationToken);
        if (matchingToolFiles.Count == 0)
        {
            return BuildResult(context, processResults, false, [$"No tool files found for family '{familyName}' in '{toolsInputDirectory}'."]);
        }

        var tempRoot = context.Workspaces.CreateTemporaryDirectory("pipeline-runner-step4");
        try
        {
            var tempDocsDirectory = Path.Combine(tempRoot, "docs-generation");
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

            var cleanupResult = await context.ProcessRunner.RunDotNetProjectAsync(
                GetProjectPath(context, "ToolFamilyCleanup"),
                ["--multi-phase"],
                context.Request.SkipBuild,
                tempDocsDirectory,
                cancellationToken);
            processResults.Add(cleanupResult);
            if (!cleanupResult.Succeeded)
            {
                AddProcessIssue(cleanupResult, warnings, "Tool-family cleanup failed");
                return BuildResult(context, processResults, false, warnings);
            }

            var tempMetadataDirectory = Path.Combine(tempGeneratedDirectory, "tool-family-metadata");
            var tempRelatedDirectory = Path.Combine(tempGeneratedDirectory, "tool-family-related");
            var tempFinalDirectory = Path.Combine(tempGeneratedDirectory, "tool-family");
            var expectedMetadataFile = Path.Combine(tempMetadataDirectory, $"{familyName}-metadata.md");
            var expectedRelatedFile = Path.Combine(tempRelatedDirectory, $"{familyName}-related.md");
            var expectedFinalFile = Path.Combine(tempFinalDirectory, $"{familyName}.md");

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
                warnings.AddRange(copyBackIssues);
                return BuildResult(context, processResults, false, warnings);
            }

            CopyMarkdownFiles(tempMetadataDirectory, Path.Combine(context.OutputPath, "tool-family-metadata"));
            CopyMarkdownFiles(tempRelatedDirectory, Path.Combine(context.OutputPath, "tool-family-related"));
            CopyMarkdownFiles(tempFinalDirectory, Path.Combine(context.OutputPath, "tool-family"));

            return BuildResult(context, processResults, true, warnings);
        }
        finally
        {
            context.Workspaces.Delete(tempRoot);
        }
    }

    private static void CopyMarkdownFiles(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*.md", SearchOption.TopDirectoryOnly))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }
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

        var contentMatches = new List<string>();
        foreach (var filePath in toolFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var namespaceFromFile = await GetToolNamespaceFromFileAsync(filePath, cancellationToken);
            if (string.Equals(namespaceFromFile, familyName, StringComparison.OrdinalIgnoreCase))
            {
                contentMatches.Add(filePath);
            }
        }

        if (contentMatches.Count > 0)
        {
            return contentMatches;
        }

        var prefixes = await GetPrefixesAsync(familyName, cancellationToken);
        return toolFiles
            .Where(path => prefixes.Any(prefix =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                return string.Equals(fileName, prefix, StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith($"{prefix}-", StringComparison.OrdinalIgnoreCase);
            }))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
