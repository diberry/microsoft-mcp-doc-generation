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

        if (parseResult.Errors.Count > 0)
        {
            return new PipelineParseResult(null, parseResult.Errors.Select(error => error.Message).ToArray(), HelpRequested: false);
        }

        var namespaceValue = parseResult.GetValueForOption(options.Namespace);
        var outputValue = parseResult.GetValueForOption(options.Output) ?? PipelineRequest.GetDefaultOutputPath(namespaceValue);
        var stepsCsv = parseResult.GetValueForOption(options.Steps) ?? string.Join(',', PipelineRequest.DefaultSteps);

        if (!PipelineRequest.TryParseSteps(stepsCsv, out var steps, out var stepError))
        {
            return new PipelineParseResult(null, [stepError ?? "Invalid step list."], HelpRequested: false);
        }

        var request = new PipelineRequest(
            namespaceValue,
            steps,
            outputValue,
            parseResult.GetValueForOption(options.SkipBuild),
            parseResult.GetValueForOption(options.SkipValidation),
            parseResult.GetValueForOption(options.DryRun),
            parseResult.GetValueForOption(options.SkipEnvValidation),
            parseResult.GetValueForOption(options.SkipDeps));

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
        return rootCommand;
    }

    private static CliOptions BuildOptions()
        => new(
            new Option<string?>("--namespace", "Namespace/service area to process. Omit to process all namespaces."),
            new Option<string>("--steps", () => string.Join(',', PipelineRequest.DefaultSteps), "Comma-separated list of step identifiers to run."),
            new Option<string?>("--output", "Output directory. Defaults to .\\generated or .\\generated-<namespace>."),
            new Option<bool>("--skip-build", "Skip build work and require existing Release outputs."),
            new Option<bool>("--skip-validation", "Skip validation checks executed by the typed runner."),
            new Option<bool>("--skip-env-validation", "Skip Azure OpenAI environment validation during bootstrap."),
            new Option<bool>("--skip-deps", "Skip step dependency validation. Allows running a step without its prerequisites."),
            new Option<bool>("--dry-run", "Print the resolved execution plan without running bootstrap or steps."));

    private sealed record CliOptions(
        Option<string?> Namespace,
        Option<string> Steps,
        Option<string?> Output,
        Option<bool> SkipBuild,
        Option<bool> SkipValidation,
        Option<bool> SkipEnvValidation,
        Option<bool> SkipDeps,
        Option<bool> DryRun);
}
