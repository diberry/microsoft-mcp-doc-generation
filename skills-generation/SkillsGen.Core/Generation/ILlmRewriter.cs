using SkillsGen.Core.Models;

namespace SkillsGen.Core.Generation;

public interface ILlmRewriter
{
    Task<string> RewriteIntroAsync(string skillName, string rawDescription, CancellationToken ct = default);
    Task<string> GenerateKnowledgeOverviewAsync(string skillName, string rawBody, CancellationToken ct = default);
    Task<string?> SynthesizeWhatItProvidesAsync(string skillName, SkillData skillData, CancellationToken ct = default);
    Task<string?> SynthesizeWhenToUseSummaryAsync(string skillName, SkillData skillData, CancellationToken ct = default);
    Task<List<string>> TranslateWorkflowStepsAsync(string skillName, List<string> rawSteps, List<McpToolEntry> tools, CancellationToken ct = default);
}
