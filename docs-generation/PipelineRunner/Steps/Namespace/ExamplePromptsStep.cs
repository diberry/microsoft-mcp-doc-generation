using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using Shared;

namespace PipelineRunner.Steps;

public sealed class ExamplePromptsStep : NamespaceStepBase
{
    private static readonly Regex FailedToolRegex = new(
        @"^\s*❌\s+(?<command>.+?)(?:\s+\(.*\))?$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FailedToolTextRegex = new(
        @"^\s*\[(?:FAILED|ERROR)\]\s+(?<command>.+?)(?:\s+\(.*\))?$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ExamplePromptsStep()
        : base(
            2,
            "Generate example prompts",
            FailurePolicy.Fatal,
            dependsOn: [1],
            requiresAiConfiguration: true,
            createsFilteredCliView: true,
            expectedOutputs: ["example-prompts", "example-prompts-prompts", "example-prompts-raw-output"])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (_, cliOutput, cliVersion, matchingTools) = ResolveTarget(context);
        var filteredCli = await CreateFilteredCliFileAsync(context, cliOutput, matchingTools, "pipeline-runner-step2", cancellationToken);
        var nameContext = await FileNameContext.CreateAsync();
        var toolArtifacts = matchingTools
            .Select(tool => ToolArtifacts.Create(tool.Command, context.OutputPath, nameContext))
            .ToArray();

        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var validatorResults = new List<ValidatorResult>();
        var artifactFailures = new List<ArtifactFailure>();
        var generatorProject = GetProjectPath(context, "ExamplePromptGeneratorStandalone");

        var generatorArguments = new List<string>
        {
            filteredCli.FilePath,
            context.OutputPath,
            cliVersion,
        };

        var e2ePromptsPath = Path.Combine(context.OutputPath, "e2e-test-prompts", "parsed.json");
        if (File.Exists(e2ePromptsPath))
        {
            generatorArguments.Add("--e2e-prompts");
            generatorArguments.Add(e2ePromptsPath);
        }

        var parameterManifestDirectory = Path.Combine(context.OutputPath, "parameters");
        generatorArguments.Add("--param-manifests");
        generatorArguments.Add(parameterManifestDirectory);

        var generatorResult = await context.ProcessRunner.RunDotNetProjectAsync(
            generatorProject,
            generatorArguments,
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(generatorResult);
        if (!generatorResult.Succeeded)
        {
            AddProcessIssue(generatorResult, warnings, "Example prompt generation failed");
            var failedCommands = ParseFailedCommands(generatorResult.StandardOutput, matchingTools);
            if (failedCommands.Count == 0)
            {
                artifactFailures.Add(CreateArtifactFailure(
                    "pipeline step",
                    "ExamplePromptGeneratorStandalone",
                    "Example prompt generation failed before specific tools could be identified.",
                    warnings,
                    [
                        Path.Combine(context.OutputPath, "example-prompts"),
                        Path.Combine(context.OutputPath, "example-prompts-prompts"),
                        Path.Combine(context.OutputPath, "example-prompts-raw-output"),
                    ]));

                return BuildResult(context, processResults, false, warnings, validatorResults, artifactFailures);
            }

            artifactFailures.AddRange(toolArtifacts
                .Where(artifact => failedCommands.Contains(artifact.Command))
                .Select(artifact => CreateArtifactFailure(
                    "tool",
                    artifact.Command,
                    "Example prompt generation failed for this tool.",
                    warnings.Concat(GetMissingGenerationOutputs(artifact)),
                    artifact.AllPaths)));

            return BuildResult(context, processResults, false, warnings, validatorResults, artifactFailures);
        }

        if (!context.Request.SkipValidation)
        {
            var outputIssues = GetPerToolOutputIssues(toolArtifacts);
            if (outputIssues.Count > 0)
            {
                warnings.AddRange(outputIssues.SelectMany(static issue => issue.Value));
                artifactFailures.AddRange(toolArtifacts
                    .Where(artifact => outputIssues.ContainsKey(artifact.Command))
                    .Select(artifact => CreateArtifactFailure(
                        "tool",
                        artifact.Command,
                        "Example prompt outputs are incomplete for this tool.",
                        outputIssues[artifact.Command],
                        artifact.AllPaths)));

                return BuildResult(context, processResults, false, warnings, validatorResults, artifactFailures);
            }

            var validatorProject = GetProjectPath(context, "ExamplePromptValidator");
            var validatorWarnings = new List<string>();
            var validatorArguments = new List<string>
            {
                "--generated", context.OutputPath,
                "--example-prompts-dir", Path.Combine(context.OutputPath, "example-prompts"),
            };

            if (matchingTools.Count == 1)
            {
                validatorArguments.Add("--tool-command");
                validatorArguments.Add(matchingTools[0].Command);
            }

            var validatorResult = await context.ProcessRunner.RunDotNetProjectAsync(
                validatorProject,
                validatorArguments,
                context.Request.SkipBuild,
                context.DocsGenerationRoot,
                cancellationToken);

            processResults.Add(validatorResult);
            if (!validatorResult.Succeeded)
            {
                AddProcessIssue(validatorResult, validatorWarnings, "Example prompt validation completed with issues");
                warnings.AddRange(validatorWarnings);

                var invalidCommands = ParseInvalidTools(validatorResult.StandardOutput, matchingTools);
                if (invalidCommands.Count == 0)
                {
                    invalidCommands = toolArtifacts.Select(static artifact => artifact.Command).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }

                artifactFailures.AddRange(toolArtifacts
                    .Where(artifact => invalidCommands.Contains(artifact.Command))
                    .Select(artifact => CreateArtifactFailure(
                        "tool",
                        artifact.Command,
                        "Example prompt validation failed for this tool.",
                        validatorWarnings,
                        [artifact.ValidationPath, artifact.ExamplePromptPath, artifact.RawOutputPath])));
            }

            validatorResults.Add(new ValidatorResult(
                "Validate-ExamplePrompts-RequiredParams",
                validatorResult.Succeeded,
                validatorWarnings));
        }

        return BuildResult(context, processResults, true, warnings, validatorResults, artifactFailures);
    }

    private static Dictionary<string, List<string>> GetPerToolOutputIssues(IEnumerable<ToolArtifacts> toolArtifacts)
    {
        var issues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in toolArtifacts)
        {
            var missing = GetMissingGenerationOutputs(artifact);
            if (missing.Count > 0)
            {
                issues[artifact.Command] = missing;
            }
        }

        return issues;
    }

    private static List<string> GetMissingGenerationOutputs(ToolArtifacts artifact)
    {
        var missing = new List<string>();
        if (!File.Exists(artifact.ExamplePromptPath))
        {
            missing.Add($"Missing example prompts markdown: '{artifact.ExamplePromptPath}'.");
        }

        if (!File.Exists(artifact.InputPromptPath))
        {
            missing.Add($"Missing saved example prompt input: '{artifact.InputPromptPath}'.");
        }

        if (!File.Exists(artifact.RawOutputPath))
        {
            missing.Add($"Missing raw AI output: '{artifact.RawOutputPath}'.");
        }

        return missing;
    }

    private static HashSet<string> ParseFailedCommands(string output, IReadOnlyList<CliTool> matchingTools)
    {
        var knownCommands = matchingTools
            .Select(tool => tool.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var failedCommands = ParseFailedCommands(output, knownCommands, FailedToolRegex);
        return failedCommands.Count > 0
            ? failedCommands
            : ParseFailedCommands(output, knownCommands, FailedToolTextRegex);
    }

    private static HashSet<string> ParseFailedCommands(string output, HashSet<string> knownCommands, Regex regex)
    {
        var failedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in regex.Matches(output))
        {
            var command = match.Groups["command"].Value.Trim();
            var parenthesisIndex = command.IndexOf(" (", StringComparison.Ordinal);
            if (parenthesisIndex >= 0)
            {
                command = command[..parenthesisIndex].Trim();
            }

            if (knownCommands.Contains(command))
            {
                failedCommands.Add(command);
            }
        }

        return failedCommands;
    }

    private static HashSet<string> ParseInvalidTools(string output, IReadOnlyList<CliTool> matchingTools)
    {
        var knownCommands = matchingTools
            .Select(tool => tool.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalidTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var collectInvalidTools = false;

        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Equals("Invalid tools:", StringComparison.OrdinalIgnoreCase))
            {
                collectInvalidTools = true;
                continue;
            }

            if (!collectInvalidTools)
            {
                continue;
            }

            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                break;
            }

            var command = line[2..].Trim();
            if (knownCommands.Contains(command))
            {
                invalidTools.Add(command);
            }
        }

        return invalidTools;
    }

    private sealed record ToolArtifacts(
        string Command,
        string ExamplePromptPath,
        string InputPromptPath,
        string RawOutputPath,
        string ValidationPath)
    {
        public string[] AllPaths => [ExamplePromptPath, InputPromptPath, RawOutputPath, ValidationPath];

        public static ToolArtifacts Create(string command, string outputPath, FileNameContext nameContext)
        {
            var baseName = ToolFileNameBuilder.BuildBaseFileName(command, nameContext);
            return new ToolArtifacts(
                command,
                Path.Combine(outputPath, "example-prompts", ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext)),
                Path.Combine(outputPath, "example-prompts-prompts", ToolFileNameBuilder.BuildInputPromptFileName(command, nameContext)),
                Path.Combine(outputPath, "example-prompts-raw-output", ToolFileNameBuilder.BuildRawOutputFileName(command, nameContext)),
                Path.Combine(outputPath, "example-prompts-validation", $"{baseName}-validation.md"));
        }
    }
}
