using System.Text.Json;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using Shared;

namespace PipelineRunner.Steps;

public sealed class BootstrapStep : StepDefinition
{
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

    public BootstrapStep()
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
            ])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();

        try
        {
            if (NeedsAiConfiguration(context))
            {
                if (context.Request.SkipEnvValidation)
                {
                    context.Reports.Info("Skipping AI environment validation.");
                }
                else
                {
                    var probeResult = await context.AiCapabilityProbe.ProbeAsync(context.DocsGenerationRoot, cancellationToken);
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
                Path.Combine(context.RepoRoot, "docs-generation.sln"),
                context.Request.SkipBuild,
                GetRequiredArtifacts(context.DocsGenerationRoot, context.PlannedSteps),
                cancellationToken);

            var cliDirectory = Path.Combine(context.OutputPath, "cli");
            var testNpmDirectory = Path.Combine(context.RepoRoot, "test-npm-azure-mcp");
            if (!Directory.Exists(testNpmDirectory))
            {
                warnings.Add($"CLI metadata extractor directory was not found: {testNpmDirectory}");
                return BuildResult(context, processResults, success: false, warnings);
            }

            var npmExecutable = GetNpmExecutable();
            var npmInstallResult = await context.ProcessRunner.RunAsync(
                new ProcessSpec(npmExecutable, ["install", "--silent"], testNpmDirectory),
                cancellationToken);
            processResults.Add(npmInstallResult);
            if (!npmInstallResult.Succeeded)
            {
                AddProcessIssue(npmInstallResult, warnings, "CLI metadata dependency installation failed");
                return BuildResult(context, processResults, success: false, warnings);
            }

            var versionResult = await CaptureCommandOutputAsync(
                context,
                processResults,
                testNpmDirectory,
                cliDirectory,
                "get:version",
                "cli-version.json",
                cancellationToken);
            if (!versionResult.Succeeded)
            {
                AddProcessIssue(versionResult, warnings, "CLI version extraction failed");
                return BuildResult(context, processResults, success: false, warnings);
            }

            var cliOutputResult = await CaptureCommandOutputAsync(
                context,
                processResults,
                testNpmDirectory,
                cliDirectory,
                "get:tools-json",
                "cli-output.json",
                cancellationToken);
            if (!cliOutputResult.Succeeded)
            {
                AddProcessIssue(cliOutputResult, warnings, "CLI tool metadata extraction failed");
                return BuildResult(context, processResults, success: false, warnings);
            }

            var namespaceResult = await CaptureCommandOutputAsync(
                context,
                processResults,
                testNpmDirectory,
                cliDirectory,
                "get:tools-namespace",
                "cli-namespace.json",
                cancellationToken);
            if (!namespaceResult.Succeeded)
            {
                AddProcessIssue(namespaceResult, warnings, "CLI namespace metadata extraction failed");
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
                var brandMappingProject = Path.Combine(context.DocsGenerationRoot, "DocGeneration.Steps.Bootstrap.BrandMappings", "DocGeneration.Steps.Bootstrap.BrandMappings.csproj");
                var brandMappingFile = Path.Combine(context.DocsGenerationRoot, "data", "brand-to-server-mapping.json");
                var suggestionsFile = Path.Combine(context.OutputPath, "reports", "brand-mapping-suggestions.json");
                var brandValidationResult = await context.ProcessRunner.RunDotNetProjectAsync(
                    brandMappingProject,
                    [
                        "--cli-output", Path.Combine(cliDirectory, "cli-output.json"),
                        "--brand-mapping", brandMappingFile,
                        "--output", suggestionsFile,
                    ],
                    noBuild: true,
                    context.DocsGenerationRoot,
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
            var e2eProject = Path.Combine(context.DocsGenerationRoot, "DocGeneration.Steps.Bootstrap.E2eTestPromptParser", "DocGeneration.Steps.Bootstrap.E2eTestPromptParser.csproj");
            var e2eResult = await context.ProcessRunner.RunDotNetProjectAsync(
                e2eProject,
                [e2eOutputFile],
                noBuild: true,
                context.DocsGenerationRoot,
                cancellationToken);
            processResults.Add(e2eResult);
            if (!e2eResult.Succeeded)
            {
                AddProcessIssue(e2eResult, warnings, "E2E test prompt parsing failed (non-blocking)");
            }

            var azmcpSourceFile = Path.Combine(context.DocsGenerationRoot, "azure-mcp", "azmcp-commands.md");
            var azmcpOutputFile = Path.Combine(cliDirectory, "azmcp-commands.json");
            var azmcpProject = Path.Combine(context.DocsGenerationRoot, "DocGeneration.Steps.Bootstrap.CommandParser", "DocGeneration.Steps.Bootstrap.CommandParser.csproj");
            var azmcpResult = await context.ProcessRunner.RunDotNetProjectAsync(
                azmcpProject,
                ["--file", azmcpSourceFile, "--output", azmcpOutputFile],
                noBuild: true,
                context.DocsGenerationRoot,
                cancellationToken);
            processResults.Add(azmcpResult);
            if (!azmcpResult.Succeeded)
            {
                AddProcessIssue(azmcpResult, warnings, "azmcp-commands.md parsing failed (non-blocking)");
            }

            if (azmcpResult.Succeeded)
            {
                var enricherProject = Path.Combine(context.DocsGenerationRoot, "DocGeneration.Steps.Bootstrap.ToolMetadataEnricher", "DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.csproj");
                var enrichedOutputFile = Path.Combine(cliDirectory, "cli-output-enriched.json");
                var enricherResult = await context.ProcessRunner.RunDotNetProjectAsync(
                    enricherProject,
                    [
                        "--cli-output", Path.Combine(cliDirectory, "cli-output.json"),
                        "--azmcp-commands", azmcpOutputFile,
                        "--output", enrichedOutputFile,
                    ],
                    noBuild: true,
                    context.DocsGenerationRoot,
                    cancellationToken);
                processResults.Add(enricherResult);
                if (!enricherResult.Succeeded)
                {
                    AddProcessIssue(enricherResult, warnings, "Tool metadata enrichment failed (non-blocking)");
                }
            }

            CreateDirectories(context.OutputPath, BaseOutputDirectories);
            return BuildResult(context, processResults, success: true, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add(ex.Message);
            return BuildResult(context, processResults, success: false, warnings);
        }
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

    private static string GetNpmExecutable()
        => OperatingSystem.IsWindows() ? "npm.cmd" : "npm";

    private static IReadOnlyList<string> GetRequiredArtifacts(string docsGenerationRoot, IReadOnlyList<IPipelineStep> plannedSteps)
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

        return requiredProjects
            .Select(project => Path.Combine(docsGenerationRoot, project, "bin", "Release", "net9.0", $"{project}.dll"))
            .ToArray();
    }

    private static async ValueTask<ProcessExecutionResult> CaptureCommandOutputAsync(
        PipelineContext context,
        ICollection<ProcessExecutionResult> processResults,
        string workingDirectory,
        string outputDirectory,
        string scriptName,
        string outputFileName,
        CancellationToken cancellationToken)
    {
        var result = await context.ProcessRunner.RunAsync(
            new ProcessSpec(GetNpmExecutable(), ["run", "--silent", scriptName], workingDirectory),
            cancellationToken);
        processResults.Add(result);

        if (result.Succeeded)
        {
            var outputPath = Path.Combine(outputDirectory, outputFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, result.StandardOutput.Trim(), cancellationToken);
        }

        return result;
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

            var headings = DeterministicH2HeadingGenerator.GenerateHeadings(toolData!);

            // Write to the appropriate output directory
            var nsOutputDir = string.IsNullOrWhiteSpace(context.Request.Namespace)
                ? context.OutputPath  // all-namespace run → ./generated/
                : context.OutputPath; // single-namespace run → ./generated-{ns}/

            var h2Dir = Path.Combine(nsOutputDir, "h2-headings");
            Directory.CreateDirectory(h2Dir);

            var h2Path = Path.Combine(h2Dir, $"{nsName}.json");
            var json = JsonSerializer.Serialize(headings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(h2Path, json);
            totalHeadings += headings.Count;
        }

        context.Reports.Info($"Generated {totalHeadings} deterministic H2 headings for {byNamespace.Count()} namespace(s).");
    }
}
