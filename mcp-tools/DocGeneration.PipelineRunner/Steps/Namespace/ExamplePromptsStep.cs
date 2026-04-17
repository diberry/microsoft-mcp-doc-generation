using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using Shared;

namespace PipelineRunner.Steps;

public sealed class ExamplePromptsStep : NamespaceStepBase
{
    private const int MaxValidationRetries = 2;

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
            .ToDictionary(artifact => artifact.Command, StringComparer.OrdinalIgnoreCase);

        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var validatorResults = new List<ValidatorResult>();
        var artifactFailures = new List<ArtifactFailure>();
        var generatorProject = GetProjectPath(context, "DocGeneration.Steps.ExamplePrompts.Generation");

        var generatorArguments = BuildGeneratorArguments(filteredCli.FilePath, context.OutputPath, cliVersion);
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
                    "DocGeneration.Steps.ExamplePrompts.Generation",
                    "Example prompt generation failed before specific tools could be identified.",
                    warnings,
                    [
                        Path.Combine(context.OutputPath, "example-prompts"),
                        Path.Combine(context.OutputPath, "example-prompts-prompts"),
                        Path.Combine(context.OutputPath, "example-prompts-raw-output"),
                    ]));

                return BuildResult(context, processResults, false, warnings, validatorResults, artifactFailures);
            }

            artifactFailures.AddRange(toolArtifacts.Values
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
            var outputIssues = GetPerToolOutputIssues(toolArtifacts.Values);
            if (outputIssues.Count > 0)
            {
                warnings.AddRange(outputIssues.SelectMany(static issue => issue.Value));
                artifactFailures.AddRange(toolArtifacts.Values
                    .Where(artifact => outputIssues.ContainsKey(artifact.Command))
                    .Select(artifact => CreateArtifactFailure(
                        "tool",
                        artifact.Command,
                        "Example prompt outputs are incomplete for this tool.",
                        outputIssues[artifact.Command],
                        artifact.AllPaths)));

                return BuildResult(context, processResults, false, warnings, validatorResults, artifactFailures);
            }

            var validatorProject = GetProjectPath(context, "DocGeneration.Steps.ExamplePrompts.Validation");
            var validationOutcome = await ValidateWithRetriesAsync(
                context,
                matchingTools,
                toolArtifacts,
                generatorProject,
                generatorArguments,
                validatorProject,
                processResults,
                cancellationToken);

            warnings.AddRange(validationOutcome.Warnings);
            validatorResults.Add(validationOutcome.ValidatorResult);
            artifactFailures.AddRange(validationOutcome.ArtifactFailures);
        }

        return BuildResult(context, processResults, true, warnings, validatorResults, artifactFailures);
    }

    private async Task<ValidationOutcome> ValidateWithRetriesAsync(
        PipelineContext context,
        IReadOnlyList<CliTool> matchingTools,
        IReadOnlyDictionary<string, ToolArtifacts> toolArtifacts,
        string generatorProject,
        IReadOnlyList<string> generatorArguments,
        string validatorProject,
        ICollection<ProcessExecutionResult> processResults,
        CancellationToken cancellationToken)
    {
        var validationWarnings = new List<string>();
        var initialToolCommand = matchingTools.Count == 1 ? matchingTools[0].Command : null;
        var initialValidation = await RunValidatorAsync(context, validatorProject, matchingTools, context.OutputPath, initialToolCommand, cancellationToken);
        processResults.Add(initialValidation.ProcessResult);
        validationWarnings.AddRange(initialValidation.Warnings);

        var unresolvedCommands = new HashSet<string>(initialValidation.InvalidCommands, StringComparer.OrdinalIgnoreCase);
        if (unresolvedCommands.Count > 0)
        {
            var retryOutcome = await RetryInvalidToolsAsync(
                context,
                matchingTools,
                toolArtifacts,
                generatorProject,
                generatorArguments,
                validatorProject,
                unresolvedCommands,
                processResults,
                cancellationToken);

            validationWarnings.AddRange(retryOutcome.Warnings);
            unresolvedCommands = retryOutcome.UnresolvedCommands;

            return new ValidationOutcome(
                new ValidatorResult("Validate-ExamplePrompts-RequiredParams", unresolvedCommands.Count == 0, validationWarnings),
                validationWarnings,
                retryOutcome.ArtifactFailures);
        }

        return new ValidationOutcome(
            new ValidatorResult("Validate-ExamplePrompts-RequiredParams", true, validationWarnings),
            validationWarnings,
            Array.Empty<ArtifactFailure>());
    }

    private async Task<RetryOutcome> RetryInvalidToolsAsync(
        PipelineContext context,
        IReadOnlyList<CliTool> matchingTools,
        IReadOnlyDictionary<string, ToolArtifacts> toolArtifacts,
        string generatorProject,
        IReadOnlyList<string> generatorArguments,
        string validatorProject,
        HashSet<string> invalidCommands,
        ICollection<ProcessExecutionResult> processResults,
        CancellationToken cancellationToken)
    {
        var retryWarnings = new List<string>();
        var unresolvedCommands = new HashSet<string>(invalidCommands, StringComparer.OrdinalIgnoreCase);
        var perToolWarnings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in invalidCommands)
        {
            if (!toolArtifacts.TryGetValue(command, out var artifact))
            {
                continue;
            }

            for (var attempt = 1; attempt <= MaxValidationRetries && unresolvedCommands.Contains(command); attempt++)
            {
                var preservedArtifacts = PreserveAttemptArtifacts(artifact, attempt);
                var reason = SummarizeValidationReport(preservedArtifacts.ValidationPath);
                var retryMessage = $"Retrying example prompts for '{command}' (attempt {attempt}/{MaxValidationRetries}) because {reason}";
                context.Reports.Warning($"    {retryMessage}");
                retryWarnings.Add(retryMessage);
                AddToolWarnings(perToolWarnings, command, [retryMessage]);

                var retryGeneratorResult = await context.ProcessRunner.RunDotNetProjectAsync(
                    generatorProject,
                    BuildRetryGeneratorArguments(generatorArguments, command, preservedArtifacts.ValidationPath),
                    context.Request.SkipBuild,
                    context.DocsGenerationRoot,
                    cancellationToken);
                processResults.Add(retryGeneratorResult);
                if (!retryGeneratorResult.Succeeded)
                {
                    var generationWarnings = new List<string>();
                    AddProcessIssue(retryGeneratorResult, generationWarnings, $"Example prompt regeneration failed for '{command}'");
                    retryWarnings.AddRange(generationWarnings);
                    AddToolWarnings(perToolWarnings, command, generationWarnings);
                    continue;
                }

                var outputIssues = GetMissingGenerationOutputs(artifact);
                if (outputIssues.Count > 0)
                {
                    retryWarnings.AddRange(outputIssues);
                    AddToolWarnings(perToolWarnings, command, outputIssues);
                    continue;
                }

                var retryValidation = await RunValidatorAsync(context, validatorProject, matchingTools, context.OutputPath, command, cancellationToken);
                processResults.Add(retryValidation.ProcessResult);
                retryWarnings.AddRange(retryValidation.Warnings);
                AddToolWarnings(perToolWarnings, command, retryValidation.Warnings);

                if (!retryValidation.InvalidCommands.Contains(command))
                {
                    unresolvedCommands.Remove(command);
                }
            }
        }

        var artifactFailures = unresolvedCommands
            .Where(toolArtifacts.ContainsKey)
            .Select(command =>
            {
                var artifact = toolArtifacts[command];
                perToolWarnings.TryGetValue(command, out var toolWarnings);
                return CreateArtifactFailure(
                    "tool",
                    command,
                    "Example prompt validation failed for this tool after automatic retries.",
                    BuildValidationFailureDetails(artifact, toolWarnings),
                    [artifact.ValidationPath, artifact.ExamplePromptPath, artifact.RawOutputPath]);
            })
            .ToArray();

        return new RetryOutcome(unresolvedCommands, retryWarnings, artifactFailures);
    }

    private static List<string> BuildGeneratorArguments(string filteredCliPath, string outputPath, string cliVersion)
        =>
        [
            filteredCliPath,
            outputPath,
            cliVersion,
        ];

    private static List<string> BuildRetryGeneratorArguments(IReadOnlyList<string> baseArguments, string command, string validationPath)
    {
        var retryArguments = new List<string>(baseArguments)
        {
            "--tool-command",
            command,
            "--validation-feedback-file",
            validationPath,
        };

        return retryArguments;
    }

    private static List<string> BuildValidatorArguments(string outputPath, string? toolCommand)
    {
        var validatorArguments = new List<string>
        {
            "--generated", outputPath,
            "--example-prompts-dir", Path.Combine(outputPath, "example-prompts"),
        };

        if (!string.IsNullOrWhiteSpace(toolCommand))
        {
            validatorArguments.Add("--tool-command");
            validatorArguments.Add(toolCommand);
        }

        return validatorArguments;
    }

    private static async Task<ValidationRun> RunValidatorAsync(
        PipelineContext context,
        string validatorProject,
        IReadOnlyList<CliTool> matchingTools,
        string outputPath,
        string? toolCommand,
        CancellationToken cancellationToken)
    {
        var validatorWarnings = new List<string>();
        var validatorResult = await context.ProcessRunner.RunDotNetProjectAsync(
            validatorProject,
            BuildValidatorArguments(outputPath, toolCommand),
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);

        var invalidCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!validatorResult.Succeeded)
        {
            var summary = string.IsNullOrWhiteSpace(toolCommand)
                ? "Example prompt validation completed with issues"
                : $"Example prompt validation completed with issues for '{toolCommand}'";
            AddProcessIssue(validatorResult, validatorWarnings, summary);

            invalidCommands = ParseInvalidTools(validatorResult.StandardOutput, matchingTools);
            if (invalidCommands.Count == 0)
            {
                invalidCommands = string.IsNullOrWhiteSpace(toolCommand)
                    ? matchingTools.Select(tool => tool.Command).ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : [toolCommand];
            }
        }

        return new ValidationRun(validatorResult, invalidCommands, validatorWarnings);
    }

    private static void AddToolWarnings(Dictionary<string, List<string>> perToolWarnings, string command, IEnumerable<string> warnings)
    {
        if (!perToolWarnings.TryGetValue(command, out var toolWarnings))
        {
            toolWarnings = new List<string>();
            perToolWarnings[command] = toolWarnings;
        }

        toolWarnings.AddRange(warnings.Where(static warning => !string.IsNullOrWhiteSpace(warning)));
    }

    private static ToolArtifacts PreserveAttemptArtifacts(ToolArtifacts artifact, int attempt)
    {
        var preservedArtifacts = artifact.CreateRetryAttempt(attempt);
        CopyArtifactIfExists(artifact.ExamplePromptPath, preservedArtifacts.ExamplePromptPath);
        CopyArtifactIfExists(artifact.InputPromptPath, preservedArtifacts.InputPromptPath);
        CopyArtifactIfExists(artifact.RawOutputPath, preservedArtifacts.RawOutputPath);
        CopyArtifactIfExists(artifact.ValidationPath, preservedArtifacts.ValidationPath);
        return preservedArtifacts;
    }

    private static void CopyArtifactIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static string SummarizeValidationReport(string validationPath)
    {
        if (!File.Exists(validationPath))
        {
            return "validation reported issues";
        }

        var details = ReadValidationReportDetails(validationPath);
        return details.Count > 0
            ? details[0]
            : "validation reported issues";
    }

    private static IReadOnlyList<string> BuildValidationFailureDetails(ToolArtifacts artifact, IReadOnlyList<string>? retryWarnings)
    {
        var details = new List<string>();
        if (retryWarnings != null)
        {
            details.AddRange(retryWarnings);
        }

        details.AddRange(ReadValidationReportDetails(artifact.ValidationPath));
        if (details.Count == 0)
        {
            details.Add("Validation remained invalid after automatic retries.");
        }

        return details.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static List<string> ReadValidationReportDetails(string validationPath)
    {
        if (!File.Exists(validationPath))
        {
            return new List<string>();
        }

        return File.ReadLines(validationPath)
            .Select(static line => line.Trim())
            .Where(static line =>
                line.StartsWith("**Summary:**", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("- Missing params:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("- Issue:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("- ", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();
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

        public ToolArtifacts CreateRetryAttempt(int attempt)
        {
            var attemptDirectory = $"attempt-{attempt}";
            return new ToolArtifacts(
                Command,
                BuildAttemptPath(ExamplePromptPath, attemptDirectory),
                BuildAttemptPath(InputPromptPath, attemptDirectory),
                BuildAttemptPath(RawOutputPath, attemptDirectory),
                BuildAttemptPath(ValidationPath, attemptDirectory));
        }

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

        private static string BuildAttemptPath(string path, string attemptDirectory)
            => Path.Combine(Path.GetDirectoryName(path)!, attemptDirectory, Path.GetFileName(path));
    }

    private sealed record ValidationRun(
        ProcessExecutionResult ProcessResult,
        HashSet<string> InvalidCommands,
        IReadOnlyList<string> Warnings);

    private sealed record RetryOutcome(
        HashSet<string> UnresolvedCommands,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<ArtifactFailure> ArtifactFailures);

    private sealed record ValidationOutcome(
        ValidatorResult ValidatorResult,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<ArtifactFailure> ArtifactFailures);
}
