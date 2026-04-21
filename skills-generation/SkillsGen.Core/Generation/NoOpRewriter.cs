using SkillsGen.Core.Models;

namespace SkillsGen.Core.Generation;

public class NoOpRewriter : ILlmRewriter
{
    public Task<string> RewriteIntroAsync(string skillName, string rawDescription, CancellationToken ct = default)
    {
        return Task.FromResult(rawDescription);
    }

    public Task<string> GenerateKnowledgeOverviewAsync(string skillName, string rawBody, CancellationToken ct = default)
    {
        return Task.FromResult(rawBody);
    }

    public Task<string?> SynthesizeWhatItProvidesAsync(string skillName, SkillData skillData, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(SkillPageGenerator.BuildWhatItProvides(skillData));
    }

    public Task<List<string>> TranslateWorkflowStepsAsync(string skillName, List<string> rawSteps, List<McpToolEntry> tools, CancellationToken ct = default)
    {
        return Task.FromResult(rawSteps);
    }
}
