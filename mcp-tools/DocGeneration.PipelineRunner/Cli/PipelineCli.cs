using System.CommandLine;
using System.CommandLine.Parsing;

namespace PipelineRunner.Cli;

public sealed record PipelineParseResult(PipelineRequest? Request, IReadOnlyList<string> Errors, bool HelpRequested);

public static class PipelineCli
{
    public static RootCommand CreateCommand()
    {
        var options = BuildOptions();
        var rootCommand = new RootCommand("Runs the documentation generation pipeline through the typed PipelineRunner host.");
        rootCommand.AddOption(options.Namespace);
        rootCommand.AddOption(options.Steps);
        rootCommand.AddOption(options.Output);
        rootCommand.AddOption(options.SkipBuild);
        rootCommand.AddOption(options.SkipValidation);
        rootCommand.AddOption(options.SkipEnvValidation);
        rootCommand.AddOption(options.SkipDeps);
        rootCommand.AddOption(options.DryRun);
        rootCommand.AddOption(options.McpBranch);
        rootCommand.AddOption(options.SkipChangelogGate);
        rootCommand.AddOption(options.RunFingerprintGate);
        rootCommand.AddOption(options.RunPromptRegressionGate);
        rootCommand.AddOption(options.SkipNpmUpdate);
        rootCommand.AddOption(options.Replay);
        rootCommand.AddOption(options.From);
        rootCommand.AddOption(options.StepName);
        return rootCommand;
    }

    public static PipelineParseResult Parse(string[] args)
    {
        if (args.Any(IsHelpToken))
        {
            return new PipelineParseResult(null, Array.Empty<string>(), HelpRequested: true);
        }

        var options = BuildOptions();
        var rootCommand = CreateCommandWith(options);
        var parseResult = rootCommand.Parse(args);
        var replay = parseResult.GetValueForOption(options.Replay);

        if (parseResult.Errors.Count > 0)
        {
            return new PipelineParseResult(null, parseResult.Errors.Select(error => error.Message).ToArray(), HelpRequested: false);
        }

        var namespaceValue = parseResult.GetValueForOption(options.Namespace);
        var outputValue = parseResult.GetValueForOption(options.Output) ?? PipelineRequest.GetDefaultOutputPath(namespaceValue);
        IReadOnlyList<int> steps = Array.Empty<int>();
        if (!replay)
        {
            var stepsCsv = parseResult.GetValueForOption(options.Steps) ?? string.Join(',', PipelineRequest.DefaultSteps);
            if (!PipelineRequest.TryParseSteps(stepsCsv, out steps, out var stepError))
            {
                return new PipelineParseResult(null, [stepError ?? "Invalid step list."], HelpRequested: false);
            }
        }

        var request = new PipelineRequest(
            namespaceValue,
            steps,
            outputValue,
            parseResult.GetValueForOption(options.SkipBuild),
            parseResult.GetValueForOption(options.SkipValidation),
            parseResult.GetValueForOption(options.DryRun),
            parseResult.GetValueForOption(options.SkipEnvValidation),
            parseResult.GetValueForOption(options.SkipDeps),
            parseResult.GetValueForOption(options.McpBranch),
            parseResult.GetValueForOption(options.SkipChangelogGate),
            parseResult.GetValueForOption(options.RunFingerprintGate),
            parseResult.GetValueForOption(options.RunPromptRegressionGate),
            parseResult.GetValueForOption(options.SkipNpmUpdate),
            replay,
            parseResult.GetValueForOption(options.From),
            parseResult.GetValueForOption(options.StepName));

        var validationErrors = request.Validate();
        return validationErrors.Count > 0
            ? new PipelineParseResult(null, validationErrors, HelpRequested: false)
            : new PipelineParseResult(request, Array.Empty<string>(), HelpRequested: false);
    }

    public static async Task<int> InvokeAsync(
        string[] args,
        Func<PipelineRequest, CancellationToken, Task<int>> handler,
        TextWriter? outputWriter = null,
        TextWriter? errorWriter = null,
        CancellationToken cancellationToken = default)
    {
        var rootCommand = CreateCommand();
        var parseResult = Parse(args);

        if (parseResult.HelpRequested)
        {
            return await rootCommand.InvokeAsync(args);
        }

        if (parseResult.Request is null)
        {
            var writer = errorWriter ?? Console.Error;
            foreach (var error in parseResult.Errors)
            {
                await writer.WriteLineAsync(error);
            }

            return global::PipelineRunner.PipelineRunner.InvalidArgumentsExitCode;
        }

        return await handler(parseResult.Request, cancellationToken);
    }

    private static bool IsHelpToken(string token)
        => string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "/?", StringComparison.OrdinalIgnoreCase);

    private static RootCommand CreateCommandWith(CliOptions options)
    {
        var rootCommand = new RootCommand("Runs the documentation generation pipeline through the typed PipelineRunner host.");
        rootCommand.AddOption(options.Namespace);
        rootCommand.AddOption(options.Steps);
        rootCommand.AddOption(options.Output);
        rootCommand.AddOption(options.SkipBuild);
        rootCommand.AddOption(options.SkipValidation);
        rootCommand.AddOption(options.SkipEnvValidation);
        rootCommand.AddOption(options.SkipDeps);
        rootCommand.AddOption(options.DryRun);
        rootCommand.AddOption(options.McpBranch);
        rootCommand.AddOption(options.SkipChangelogGate);
        rootCommand.AddOption(options.RunFingerprintGate);
        rootCommand.AddOption(options.RunPromptRegressionGate);
        rootCommand.AddOption(options.SkipNpmUpdate);
        rootCommand.AddOption(options.Replay);
        rootCommand.AddOption(options.From);
        rootCommand.AddOption(options.StepName);
        return rootCommand;
    }

    private static CliOptions BuildOptions()
        => new(
            new Option<string?>("--namespace", "Namespace/service area to process. Omit to process all namespaces."),
            new Option<string>("--steps", () => string.Join(',', PipelineRequest.DefaultSteps), "Comma-separated list of step identifiers to run."),
            new Option<string?>("--output", "Output directory. Defaults to .\\generated-<timestamp> or .\\generated-<namespace>-<timestamp>."),
            new Option<bool>("--skip-build", "Skip build work and require existing Release outputs."),
            new Option<bool>("--skip-validation", "Skip validation checks executed by the typed runner."),
            new Option<bool>("--skip-env-validation", "Skip Azure OpenAI environment validation during bootstrap."),
            new Option<bool>("--skip-deps", "Skip step dependency validation. Allows running a step without its prerequisites."),
            new Option<bool>("--dry-run", "Print the resolved execution plan without running bootstrap or steps."),
            new Option<string?>("--mcp-branch", "Branch of microsoft/mcp to fetch upstream files from. Overrides MCP_BRANCH env var. Default: main."),
            new Option<bool>("--skip-changelog-gate", "Skip the CHANGELOG gate check and process all namespaces regardless of CHANGELOG entries."),
            new Option<bool>("--run-fingerprint-gate", "After pipeline steps, compare output fingerprints against fingerprint-baseline.json. Fails on quality regressions."),
            new Option<bool>("--run-prompt-regression-gate", "After pipeline steps, run the DocGeneration.PromptRegression.Tests suite to verify prompt templates are unaffected."),
            new Option<bool>("--skip-npm-update", "Skip updating @azure/mcp to latest before installing. Use for offline runs or reproducible builds."),
            new Option<bool>("--replay", "Replay a single step against frozen upstream outputs from a prior run."),
            new Option<string?>("--from", "Run ID to replay from. Expected under .\\runs\\<run-id>\\."),
            new Option<string?>(["--step-name", "--step"], "Step slug to replay (for example: tool-generation or horizontal-articles)."));

    private sealed record CliOptions(
        Option<string?> Namespace,
        Option<string> Steps,
        Option<string?> Output,
        Option<bool> SkipBuild,
        Option<bool> SkipValidation,
        Option<bool> SkipEnvValidation,
        Option<bool> SkipDeps,
        Option<bool> DryRun,
        Option<string?> McpBranch,
        Option<bool> SkipChangelogGate,
        Option<bool> RunFingerprintGate,
        Option<bool> RunPromptRegressionGate,
        Option<bool> SkipNpmUpdate,
        Option<bool> Replay,
        Option<string?> From,
        Option<string?> StepName);
}
