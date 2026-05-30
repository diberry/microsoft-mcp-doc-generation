using System.Text;
using System.Text.RegularExpressions;
using GenerativeAI;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using Shared;
using ToolGeneration_Improved.Models;
using ToolGeneration_Improved.Services;

namespace PipelineRunner.Steps;

public sealed class ToolGenerationStep : NamespaceStepBase
{
    private const int DefaultMaxTokens = 8000;
    internal const string ToolImproverOverrideKey = "ToolGenerationStep.ToolImproverOverride";

    private static readonly ReducerRegistry Reducers = new();
    private static readonly UpstreamArtifactResolver UpstreamArtifacts = new();
    private static readonly TimeSpan ReducerImprovementTimeout = TimeSpan.FromMinutes(5);

    private static readonly Regex ComposedFailureRegex = new(
        @"Error processing (?<file>[^:]+\.md):",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ImprovedFailureRegex = new(
        @"Processing\s+(?<file>[^\s]+\.md)\.\.\.\s*✗",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static ToolGenerationStep()
    {
        Reducers.Register(3, static async (ctx, ct) =>
        {
            if (ctx is not ToolGenerationReducerInput input)
            {
                throw new InvalidOperationException($"Reducer input for step 3 must be {nameof(ToolGenerationReducerInput)}.");
            }

            var reducer = new ToolGenerationReducer();
            return await reducer.ReduceAsync(input.ComposedToolsDirectory, input.ToolFileName, input.MaxTokens, ct);
        });
    }

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
        var useReducerPath = Reducers.HasReducer(Id);

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

        if (useReducerPath)
        {
            try
            {
                await ImproveToolsWithReducerAsync(
                    context,
                    toolArtifacts,
                    composedToolsDirectory,
                    improvedToolsDirectory,
                    warnings,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                warnings.Add($"AI-improved tool generation failed: {ex.Message}");
                artifactFailures.Add(CreateStepLevelFailure(
                    "DocGeneration.Steps.ToolGeneration.Improvements",
                    "Tool improvement failed before specific tools could be identified.",
                    warnings,
                    [composedToolsDirectory, improvedToolsDirectory, annotationsDirectory, parametersDirectory, examplePromptsDirectory]));
                return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
            }
        }
        else
        {
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
        }

        var reducerImprovedIssues = GetImprovedOutputIssues(toolArtifacts);
        if (reducerImprovedIssues.Count > 0)
        {
            warnings.AddRange(reducerImprovedIssues.SelectMany(static issue => issue.Value));
            artifactFailures.AddRange(toolArtifacts
                .Where(artifact => reducerImprovedIssues.ContainsKey(artifact.Command))
                .Select(artifact => CreateArtifactFailure(
                    "tool",
                    artifact.Command,
                    "AI tool improvement failed for this tool.",
                    warnings.Concat(reducerImprovedIssues.TryGetValue(artifact.Command, out var issueDetails) ? issueDetails : Array.Empty<string>()),
                    artifact.GenerationPaths)));

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

    private static async Task ImproveToolsWithReducerAsync(
        PipelineContext context,
        IReadOnlyList<ToolArtifacts> toolArtifacts,
        string composedToolsDirectory,
        string improvedToolsDirectory,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(improvedToolsDirectory);

        var reducer = Reducers.GetReducer(3);
        if (reducer is null)
        {
            throw new InvalidOperationException("Reducer path was selected for step 3, but no reducer is registered.");
        }

        var improveToolAsync = await ResolveToolImproverAsync(context, cancellationToken);

        foreach (var artifact in toolArtifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reduction = (ToolGenerationContext)await reducer(
                new ToolGenerationReducerInput(composedToolsDirectory, artifact.ToolFileName, DefaultMaxTokens),
                cancellationToken);

            try
            {
                using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                toolCts.CancelAfter(ReducerImprovementTimeout);

                var improvedTool = await improveToolAsync(reduction, toolCts.Token);
                await File.WriteAllTextAsync(artifact.ImprovedToolPath, improvedTool.ImprovedContent, Encoding.UTF8, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                await File.WriteAllTextAsync(artifact.ImprovedToolPath, reduction.ComposedContent, Encoding.UTF8, cancellationToken);
            }
            catch (ToolImprovementAiException)
            {
                await File.WriteAllTextAsync(artifact.ImprovedToolPath, reduction.ComposedContent, Encoding.UTF8, cancellationToken);
            }
            catch (Exception ex)
            {
                warnings.Add($"AI-improved tool generation failed for '{artifact.ToolFileName}': {ex.Message}");
            }
        }
    }

    private static async Task<Func<ToolGenerationContext, CancellationToken, Task<ImprovedToolData>>> ResolveToolImproverAsync(
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        if (context.Items.TryGetValue(ToolImproverOverrideKey, out var overrideValue))
        {
            if (overrideValue is Func<ToolGenerationContext, CancellationToken, Task<ImprovedToolData>> improver)
            {
                return improver;
            }

            throw new InvalidOperationException(
                $"Context item '{ToolImproverOverrideKey}' must be {typeof(Func<ToolGenerationContext, CancellationToken, Task<ImprovedToolData>>).FullName}.");
        }

        var prompts = await LoadImprovementPromptsAsync(context.McpToolsRoot, cancellationToken);
        var service = new ImprovedToolGeneratorService(new GenerativeAIClient(), prompts.SystemPrompt, prompts.UserPromptTemplate);
        return (toolContext, ct) => service.ImproveToolAsync(toolContext, prompts.SystemPrompt, prompts.UserPromptTemplate, ct);
    }

    private static async Task<ImprovementPrompts> LoadImprovementPromptsAsync(string mcpToolsRoot, CancellationToken cancellationToken)
    {
        var projectRoot = Path.Combine(mcpToolsRoot, "DocGeneration.Steps.ToolGeneration.Improvements");
        var promptsDirectory = Path.Combine(projectRoot, "prompts");
        var dataDirectory = Path.Combine(mcpToolsRoot, "data");

        var systemPromptPath = Path.Combine(promptsDirectory, "system-prompt.txt");
        var userPromptTemplatePath = Path.Combine(promptsDirectory, "user-prompt-template.txt");

        var systemPrompt = PromptTokenResolver.Resolve(
            await File.ReadAllTextAsync(systemPromptPath, cancellationToken),
            dataDirectory);
        var userPromptTemplate = PromptTokenResolver.Resolve(
            await File.ReadAllTextAsync(userPromptTemplatePath, cancellationToken),
            dataDirectory);

        return new ImprovementPrompts(systemPrompt, userPromptTemplate);
    }

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

    private sealed record ToolGenerationReducerInput(string ComposedToolsDirectory, string ToolFileName, int MaxTokens);

    private sealed record ImprovementPrompts(string SystemPrompt, string UserPromptTemplate);
}
