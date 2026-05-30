using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using Shared;

namespace PipelineRunner.Steps;

public sealed class ToolGenerationStep : NamespaceStepBase
{
    private const int DefaultMaxTokens = 8000;
    private static readonly ReducerRegistry Reducers = new();
    private static readonly UpstreamArtifactResolver UpstreamArtifacts = new();

    private static readonly Regex ComposedFailureRegex = new(
        @"Error processing (?<file>[^:]+\.md):",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ImprovedFailureRegex = new(
        @"Processing\s+(?<file>[^\s]+\.md)\.\.\.\s*✗",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ToolGenerationStep()
        : base(
            3,
            "Compose and improve tool files",
            FailurePolicy.Fatal,
            dependsOn: [1, 2],
            requiresAiConfiguration: true,
            createsFilteredCliView: true,
            expectedOutputs: ["tools-composed", "tools"],
            postValidators: [new ToolGenerationOutputValidator(), new ToolGenerationValidator()])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (_, cliOutput, _, matchingTools) = ResolveTarget(context);
        // P8: ReducerRegistry scaffold — when a reducer is registered, use it exclusively
        if (Reducers.HasReducer(Id))
        {
            throw new NotImplementedException($"Reducer registered for step {Id} but execution path not yet implemented.");
        }

        _ = await CreateFilteredCliFileAsync(context, cliOutput, matchingTools, "pipeline-runner-step3", cancellationToken);
        var nameContext = await FileNameContext.CreateAsync();
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var artifactFailures = new List<ArtifactFailure>();
        var step1Envelope = UpstreamArtifacts.TryReadUpstream(context.OutputPath, 1, "generate-annotations-parameters-and-raw-tools");
        var step2Envelope = UpstreamArtifacts.TryReadUpstream(context.OutputPath, 2, "generate-example-prompts");
        var rawToolsDirectory = ResolveUpstreamDirectory(
            context.OutputPath,
            step1Envelope,
            "tools-raw",
            Path.Combine(context.OutputPath, "tools-raw"),
            "step 1");
        var composedToolsDirectory = Path.Combine(context.OutputPath, "tools-composed");
        var improvedToolsDirectory = Path.Combine(context.OutputPath, "tools");
        var annotationsDirectory = ResolveUpstreamDirectory(
            context.OutputPath,
            step1Envelope,
            "annotations",
            Path.Combine(context.OutputPath, "annotations"),
            "step 1");
        var parametersDirectory = ResolveUpstreamDirectory(
            context.OutputPath,
            step1Envelope,
            "parameters",
            Path.Combine(context.OutputPath, "parameters"),
            "step 1");
        var examplePromptsDirectory = ResolveUpstreamDirectory(
            context.OutputPath,
            step2Envelope,
            "example-prompts",
            Path.Combine(context.OutputPath, "example-prompts"),
            "step 2");
        var toolArtifacts = matchingTools
            .Select(tool => ToolArtifacts.Create(
                tool.Command,
                rawToolsDirectory,
                annotationsDirectory,
                parametersDirectory,
                examplePromptsDirectory,
                composedToolsDirectory,
                improvedToolsDirectory,
                nameContext))
            .ToArray();

        var prerequisiteIssues = GetPrerequisiteIssues(toolArtifacts);
        if (prerequisiteIssues.Count > 0)
        {
            warnings.AddRange(prerequisiteIssues.SelectMany(static issue => issue.Value));
            artifactFailures.AddRange(toolArtifacts
                .Where(artifact => prerequisiteIssues.ContainsKey(artifact.Command))
                .Select(artifact => CreateArtifactFailure(
                    "tool",
                    artifact.Command,
                    "Tool generation prerequisites are incomplete for this tool.",
                    prerequisiteIssues[artifact.Command],
                    artifact.PrerequisitePaths)));

            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        var composedResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "DocGeneration.Steps.ToolGeneration.Composition"),
            [
                rawToolsDirectory,
                composedToolsDirectory,
                annotationsDirectory,
                parametersDirectory,
                examplePromptsDirectory,
            ],
            context.Request.SkipBuild,
            context.McpToolsRoot,
            cancellationToken);
        processResults.Add(composedResult);

        var composedIssues = GetComposedOutputIssues(toolArtifacts);
        if (!composedResult.Succeeded || composedIssues.Count > 0)
        {
            if (!composedResult.Succeeded)
            {
                AddProcessIssue(composedResult, warnings, "Composed tool generation failed");
            }

            warnings.AddRange(composedIssues.SelectMany(static issue => issue.Value));
            var failedComposedFiles = ParseFailingFiles(composedResult.StandardError, ComposedFailureRegex);
            if (!composedResult.Succeeded && failedComposedFiles.Count == 0)
            {
                artifactFailures.Add(CreateStepLevelFailure(
                    "DocGeneration.Steps.ToolGeneration.Composition",
                    "Tool composition failed before specific tools could be identified.",
                    warnings,
                    [rawToolsDirectory, composedToolsDirectory, annotationsDirectory, parametersDirectory, examplePromptsDirectory]));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
            }

            artifactFailures.AddRange(toolArtifacts
                .Where(artifact => failedComposedFiles.Contains(artifact.ToolFileName) || composedIssues.ContainsKey(artifact.Command))
                .Select(artifact => CreateArtifactFailure(
                    "tool",
                    artifact.Command,
                    "Tool composition failed for this tool.",
                    warnings.Concat(composedIssues.TryGetValue(artifact.Command, out var issueDetails) ? issueDetails : Array.Empty<string>()),
                    artifact.GenerationPaths)));

            if (artifactFailures.Count == 0)
            {
                artifactFailures.Add(!composedResult.Succeeded
                    ? CreateStepLevelFailure(
                        "DocGeneration.Steps.ToolGeneration.Composition",
                        "Tool composition failed before specific tools could be identified.",
                        warnings,
                        [rawToolsDirectory, composedToolsDirectory, annotationsDirectory, parametersDirectory, examplePromptsDirectory])
                    : CreateArtifactFailure(
                        "tool",
                        GetCurrentNamespace(context),
                        "Tool composition failed before all composed files were produced.",
                        warnings,
                        [rawToolsDirectory, composedToolsDirectory, annotationsDirectory, parametersDirectory, examplePromptsDirectory]));
            }

            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        var improvedResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "DocGeneration.Steps.ToolGeneration.Improvements"),
            [composedToolsDirectory, improvedToolsDirectory, DefaultMaxTokens.ToString()],
            context.Request.SkipBuild,
            context.McpToolsRoot,
            cancellationToken);
        processResults.Add(improvedResult);

        var improvedIssues = GetImprovedOutputIssues(toolArtifacts);
        if (!improvedResult.Succeeded || improvedIssues.Count > 0)
        {
            if (!improvedResult.Succeeded)
            {
                AddProcessIssue(improvedResult, warnings, "AI-improved tool generation failed");
            }

            warnings.AddRange(improvedIssues.SelectMany(static issue => issue.Value));
            var failedImprovedFiles = ParseFailingFiles(
                string.Join(Environment.NewLine, [improvedResult.StandardOutput, improvedResult.StandardError]),
                ImprovedFailureRegex);
            if (!improvedResult.Succeeded && failedImprovedFiles.Count == 0)
            {
                artifactFailures.Add(CreateStepLevelFailure(
                    "DocGeneration.Steps.ToolGeneration.Improvements",
                    "Tool improvement failed before specific tools could be identified.",
                    warnings,
                    [composedToolsDirectory, improvedToolsDirectory, annotationsDirectory, parametersDirectory, examplePromptsDirectory]));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
            }

            artifactFailures.AddRange(toolArtifacts
                .Where(artifact => failedImprovedFiles.Contains(artifact.ToolFileName) || improvedIssues.ContainsKey(artifact.Command))
                .Select(artifact => CreateArtifactFailure(
                    "tool",
                    artifact.Command,
                    "AI tool improvement failed for this tool.",
                    warnings.Concat(improvedIssues.TryGetValue(artifact.Command, out var issueDetails) ? issueDetails : Array.Empty<string>()),
                    artifact.GenerationPaths)));

            if (artifactFailures.Count == 0)
            {
                artifactFailures.Add(!improvedResult.Succeeded
                    ? CreateStepLevelFailure(
                        "DocGeneration.Steps.ToolGeneration.Improvements",
                        "Tool improvement failed before specific tools could be identified.",
                        warnings,
                        [composedToolsDirectory, improvedToolsDirectory, annotationsDirectory, parametersDirectory, examplePromptsDirectory])
                    : CreateArtifactFailure(
                        "tool",
                        GetCurrentNamespace(context),
                        "Tool improvement failed before all final tool files were written.",
                        warnings,
                        [composedToolsDirectory, improvedToolsDirectory, annotationsDirectory, parametersDirectory, examplePromptsDirectory]));
            }

            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        if (!context.Request.SkipValidation)
        {
            var outputIssues = new List<string>();
            EnsurePathHasContent(composedToolsDirectory, "composed tool output", outputIssues);
            EnsurePathHasContent(improvedToolsDirectory, "improved tool output", outputIssues);
            warnings.AddRange(outputIssues);
            if (outputIssues.Count > 0)
            {
                artifactFailures.AddRange(toolArtifacts
                    .Select(artifact => CreateArtifactFailure(
                        "tool",
                        artifact.Command,
                        "Tool generation validation found incomplete output directories.",
                        outputIssues,
                        artifact.GenerationPaths)));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
            }
        }

        return BuildResult(context, processResults, true, warnings, artifactFailures: artifactFailures);
    }

    private static Dictionary<string, List<string>> GetPrerequisiteIssues(IEnumerable<ToolArtifacts> toolArtifacts)
    {
        var issues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in toolArtifacts)
        {
            var missing = new List<string>();
            if (!File.Exists(artifact.RawToolPath))
            {
                missing.Add($"Missing raw tool prerequisite: '{artifact.RawToolPath}'.");
            }

            if (!File.Exists(artifact.AnnotationPath))
            {
                missing.Add($"Missing annotation prerequisite: '{artifact.AnnotationPath}'.");
            }

            if (!File.Exists(artifact.ParameterPath))
            {
                missing.Add($"Missing parameter prerequisite: '{artifact.ParameterPath}'.");
            }

            if (!File.Exists(artifact.ExamplePromptsPath))
            {
                missing.Add($"Missing example prompt prerequisite: '{artifact.ExamplePromptsPath}'.");
            }

            if (missing.Count > 0)
            {
                issues[artifact.Command] = missing;
            }
        }

        return issues;
    }

    private static Dictionary<string, List<string>> GetComposedOutputIssues(IEnumerable<ToolArtifacts> toolArtifacts)
    {
        var issues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in toolArtifacts)
        {
            if (File.Exists(artifact.ComposedToolPath))
            {
                continue;
            }

            issues[artifact.Command] = [$"Missing composed tool output: '{artifact.ComposedToolPath}'."];
        }

        return issues;
    }

    private static Dictionary<string, List<string>> GetImprovedOutputIssues(IEnumerable<ToolArtifacts> toolArtifacts)
    {
        var issues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in toolArtifacts)
        {
            if (File.Exists(artifact.ImprovedToolPath))
            {
                continue;
            }

            issues[artifact.Command] = [$"Missing improved tool output: '{artifact.ImprovedToolPath}'."];
        }

        return issues;
    }

    private static HashSet<string> ParseFailingFiles(string output, Regex regex)
        => regex.Matches(output)
            .Select(match => match.Groups["file"].Value.Trim())
            .Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static ArtifactFailure CreateStepLevelFailure(
        string artifactName,
        string summary,
        IEnumerable<string> details,
        IEnumerable<string> relatedPaths)
        => ArtifactFailure.Create("pipeline step", artifactName, summary, details, relatedPaths);

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

    private sealed record ToolArtifacts(
        string Command,
        string ToolFileName,
        string RawToolPath,
        string AnnotationPath,
        string ParameterPath,
        string ExamplePromptsPath,
        string ComposedToolPath,
        string ImprovedToolPath)
    {
        public string[] PrerequisitePaths => [RawToolPath, AnnotationPath, ParameterPath, ExamplePromptsPath];

        public string[] GenerationPaths => [RawToolPath, ComposedToolPath, ImprovedToolPath, AnnotationPath, ParameterPath, ExamplePromptsPath];

        public static ToolArtifacts Create(
            string command,
            string rawToolsDirectory,
            string annotationsDirectory,
            string parametersDirectory,
            string examplePromptsDirectory,
            string composedToolsDirectory,
            string improvedToolsDirectory,
            FileNameContext nameContext)
        {
            var toolFileName = ToolFileNameBuilder.BuildToolFileName(command, nameContext);
            return new ToolArtifacts(
                command,
                toolFileName,
                Path.Combine(rawToolsDirectory, toolFileName),
                Path.Combine(annotationsDirectory, ToolFileNameBuilder.BuildAnnotationFileName(command, nameContext)),
                Path.Combine(parametersDirectory, ToolFileNameBuilder.BuildParameterFileName(command, nameContext)),
                Path.Combine(examplePromptsDirectory, ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext)),
                Path.Combine(composedToolsDirectory, toolFileName),
                Path.Combine(improvedToolsDirectory, toolFileName));
        }
    }
}
