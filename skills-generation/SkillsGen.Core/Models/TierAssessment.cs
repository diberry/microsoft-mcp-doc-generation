namespace SkillsGen.Core.Models;

public record QuestionResult(string Id, string Question, bool Answer, string Evidence);

public record TierAssessment(
    int Tier,
    List<QuestionResult> Questions,
    string Rationale,
    bool ShowToolsSection,
    bool ShowTriggersSection,
    bool ShowDecisionGuidance,
    bool ShowWorkflow,
    bool ShowDetailedPrompts);
