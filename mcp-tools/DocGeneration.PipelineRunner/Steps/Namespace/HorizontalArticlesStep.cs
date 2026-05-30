using Azure.Mcp.TextTransformation.Services;
using Azure.Mcp.TextTransformation.Models;
using GenerativeAI;
using HorizontalArticleGenerator.Builders;
using HorizontalArticleGenerator.Models;
using HorizontalArticleGenerator.Validation;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using Shared;
using Shared.Validation;
using HorizontalArticleGeneratorClass = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace PipelineRunner.Steps;

public sealed class HorizontalArticlesStep : NamespaceStepBase
{
    public const string ArticleOutlineOverrideKey = "HorizontalArticlesStep.ArticleOutlineOverride";
    private const string HorizontalArticlesStageName = "horizontal-articles";

    private static readonly ReducerRegistry Reducers = new();
    private static readonly PreAiValidatorRegistry ValidatorRegistry = new();
    private static readonly UpstreamArtifactResolver UpstreamArtifacts = new();

    static HorizontalArticlesStep()
    {
        Reducers.Register(6, static async (ctx, ct) =>
        {
            if (ctx is not ArticleOutlineReducerInput input)
            {
                throw new InvalidOperationException($"Reducer input for step 6 must be {nameof(ArticleOutlineReducerInput)}.");
            }

            var builder = new ArticleOutlineBuilder();
            return await builder.BuildAsync(input.OutputPath, input.ServiceNamespace, ct);
        });

        ValidatorRegistry.Register(HorizontalArticlesStageName, new ArticleOutlineContextValidator());
        ValidatorRegistry.Register(HorizontalArticlesStageName, new ArticleOutlineBudgetValidator());
    }

    public HorizontalArticlesStep()
        : base(
            6,
            "Generate horizontal article",
            FailurePolicy.Fatal,
            dependsOn: [0],
            requiresAiConfiguration: true,
            createsFilteredCliView: true,
            expectedOutputs: ["horizontal-articles"],
            postValidators: [new HorizontalArticleOutputValidator()])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (currentNamespace, cliOutput, _, matchingTools) = ResolveTarget(context);
        var useReducerPath = Reducers.HasReducer(Id);
        var hasOverride = context.Items.ContainsKey(ArticleOutlineOverrideKey);

        _ = await CreateFilteredCliFileAsync(context, cliOutput, matchingTools, "pipeline-runner-step6", cancellationToken);

        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var artifactFailures = new List<ArtifactFailure>();
        var bootstrapEnvelope = UpstreamArtifacts.TryReadUpstream(context.OutputPath, 0, "bootstrap-pipeline");
        var cliVersionPath = ResolveUpstreamFile(
            context.OutputPath,
            bootstrapEnvelope,
            Path.Combine("cli", "cli-version.json"),
            Path.Combine(context.OutputPath, "cli", "cli-version.json"));
        if (!context.Request.SkipValidation && !File.Exists(cliVersionPath))
        {
            warnings.Add($"CLI version file not found at '{cliVersionPath}'.");
            artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        if (useReducerPath)
        {
            try
            {
                var reducer = Reducers.GetReducer(6);
                if (reducer is null)
                {
                    throw new InvalidOperationException("Reducer path was selected for step 6, but no reducer is registered.");
                }

                var outline = (ArticleOutlineContext)await reducer(
                    new ArticleOutlineReducerInput(context.OutputPath, currentNamespace),
                    cancellationToken);

                var validationResult = await ValidatorRegistry.ValidateAsync(HorizontalArticlesStageName, outline, cancellationToken);
                if (!validationResult.IsValid)
                {
                    var errorMessages = validationResult.Errors.Select(static e => e.Message).ToArray();
                    warnings.AddRange(errorMessages);
                    artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
                    return BuildResult(
                        context,
                        processResults,
                        false,
                        warnings,
                        [new ValidatorResult("pre-ai-validation", false, errorMessages)],
                        artifactFailures);
                }

                var renderArticleAsync = await ResolveArticleRendererAsync(context, cancellationToken);
                var articleContent = await renderArticleAsync(outline, cancellationToken);
                var reducerArticleDirectory = Path.Combine(context.OutputPath, "horizontal-articles");
                Directory.CreateDirectory(reducerArticleDirectory);
                File.WriteAllText(
                    Path.Combine(reducerArticleDirectory, $"horizontal-article-{currentNamespace}.md"),
                    articleContent);
                RemoveStaleFile(Path.Combine(reducerArticleDirectory, $"error-{currentNamespace}.txt"));
                RemoveStaleFile(Path.Combine(reducerArticleDirectory, $"error-{currentNamespace}-airesponse.txt"));

                return BuildResult(context, processResults, true, warnings, artifactFailures: artifactFailures);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (hasOverride)
                {
                    warnings.Add($"Horizontal article generation failed: {ex.Message}");
                    artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
                    return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
                }

                Console.WriteLine($"Reducer path failed for namespace '{currentNamespace}', falling back to subprocess: {ex.Message}");
            }
        }

        var processResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "DocGeneration.Steps.HorizontalArticles"),
            ["--single-service", currentNamespace, "--output-path", context.OutputPath, "--transform"],
            context.Request.SkipBuild,
            context.McpToolsRoot,
            cancellationToken);
        processResults.Add(processResult);
        if (!processResult.Succeeded)
        {
            AddProcessIssue(processResult, warnings, "Horizontal article generation failed");
            artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        var articleDirectory = Path.Combine(context.OutputPath, "horizontal-articles");
        var articlePath = Path.Combine(articleDirectory, $"horizontal-article-{currentNamespace}.md");
        var errorPath = Path.Combine(articleDirectory, $"error-{currentNamespace}.txt");
        var aiErrorPath = Path.Combine(articleDirectory, $"error-{currentNamespace}-airesponse.txt");

        var success = true;
        if (File.Exists(errorPath) || File.Exists(aiErrorPath))
        {
            success = false;
            warnings.Add($"Horizontal article generation produced an error artifact for '{currentNamespace}'.");
        }

        if (!File.Exists(articlePath))
        {
            warnings.Add($"Expected horizontal article output at '{articlePath}'.");
            success = false;
        }

        if (!success)
        {
            artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
        }

        return BuildResult(context, processResults, success, warnings, artifactFailures: artifactFailures);
    }

    private static async Task<Func<ArticleOutlineContext, CancellationToken, Task<string>>> ResolveArticleRendererAsync(
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Items.TryGetValue(ArticleOutlineOverrideKey, out var overrideValue))
        {
            if (overrideValue is Func<ArticleOutlineContext, CancellationToken, Task<string>> articleOverride)
            {
                return articleOverride;
            }

            throw new InvalidOperationException(
                $"Context item '{ArticleOutlineOverrideKey}' must be {typeof(Func<ArticleOutlineContext, CancellationToken, Task<string>>).FullName}.");
        }

        var transformConfigPath = Path.Combine(context.McpToolsRoot, "data", "transformation-config.json");
        TransformationEngine? transformationEngine = null;
        if (File.Exists(transformConfigPath))
        {
            var loader = new ConfigLoader(transformConfigPath);
            transformationEngine = new TransformationEngine(await loader.LoadAsync());
        }

        var generator = new HorizontalArticleGeneratorClass(
            GenerativeAIOptions.LoadFromEnvironmentOrDotEnv(),
            useTextTransformation: transformationEngine is not null,
            generateAllArticles: false,
            transformationEngine,
            outputBasePath: context.OutputPath);
        return generator.GenerateArticleMarkdownAsync;
    }

    private static ArtifactFailure CreateHorizontalFailure(PipelineContext context, string currentNamespace, IEnumerable<string> details)
    {
        var articleDirectory = Path.Combine(context.OutputPath, "horizontal-articles");
        return CreateArtifactFailure(
            "horizontal article",
            $"horizontal-article-{currentNamespace}.md",
            "Horizontal article generation failed for this namespace.",
            details,
            [
                Path.Combine(articleDirectory, $"horizontal-article-{currentNamespace}.md"),
                Path.Combine(articleDirectory, $"error-{currentNamespace}.txt"),
                Path.Combine(articleDirectory, $"error-{currentNamespace}-airesponse.txt"),
            ]);
    }

    private static string ResolveUpstreamFile(
        string outputPath,
        StepResultFile? envelope,
        string relativeFilePath,
        string fallbackPath)
    {
        if (UpstreamArtifacts.TryResolveOutputFile(outputPath, envelope, relativeFilePath, out var resolvedPath))
        {
            Console.WriteLine(
                $"INFO: Using bootstrap envelope-based resolution for '{relativeFilePath}' at '{resolvedPath}'.");
            return resolvedPath;
        }

        return fallbackPath;
    }

    private static void RemoveStaleFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private sealed record ArticleOutlineReducerInput(string OutputPath, string ServiceNamespace);
}
