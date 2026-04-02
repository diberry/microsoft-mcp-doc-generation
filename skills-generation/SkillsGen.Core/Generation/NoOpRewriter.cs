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
}
