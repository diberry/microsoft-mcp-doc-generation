namespace SkillsGen.Core.Generation;

public interface ILlmRewriter
{
    Task<string> RewriteIntroAsync(string skillName, string rawDescription, CancellationToken ct = default);
    Task<string> GenerateKnowledgeOverviewAsync(string skillName, string rawBody, CancellationToken ct = default);
}
