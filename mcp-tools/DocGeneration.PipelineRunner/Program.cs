using HorizontalArticleGenerator.Builders;
using HorizontalArticleGenerator.Validation;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using ToolFamilyCleanup.Services;
using ToolFamilyCleanup.Validation;
using ToolGeneration_Improved.Validation;

var preAiRegistry = new ReducerRegistry();

preAiRegistry.Register(4, async (ctx, ct) =>
{
    var pc = (PipelineContext)ctx;
    var ns = pc.Items.TryGetValue("Namespace", out var v) ? v as string ?? string.Empty : string.Empty;
    var toolsDir = Path.Combine(pc.OutputPath, "tools");
    return (object)await new FamilyStructureBuilder().BuildAsync(toolsDir, ns, null, ct);
});

preAiRegistry.Register(6, async (ctx, ct) =>
{
    var pc = (PipelineContext)ctx;
    var ns = pc.Items.TryGetValue("Namespace", out var v) ? v as string ?? string.Empty : string.Empty;
    return (object)await new ArticleOutlineBuilder().BuildAsync(pc.OutputPath, ns, ct);
});

preAiRegistry.RegisterValidator(new FamilyStructureContextValidator());
preAiRegistry.RegisterValidator(new ArticleOutlineContextValidator());
preAiRegistry.RegisterValidator(new ArticleOutlineBudgetValidator());
preAiRegistry.RegisterValidator(new ToolGenerationContextValidator());
preAiRegistry.RegisterValidator(new ToolGenerationBudgetValidator());

return await PipelineCli.InvokeAsync(
    args,
    async (request, cancellationToken) =>
    {
        var runner = global::PipelineRunner.PipelineRunner.CreateDefault(preAiRegistry: preAiRegistry);
        return await runner.RunAsync(request, cancellationToken);
    });
