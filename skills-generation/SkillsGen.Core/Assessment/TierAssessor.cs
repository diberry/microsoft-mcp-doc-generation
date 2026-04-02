using SkillsGen.Core.Models;

namespace SkillsGen.Core.Assessment;

public class TierAssessor : ITierAssessor
{
    private const int Tier1Threshold = 4;

    public TierAssessment Assess(SkillData skillData, TriggerData triggerData)
    {
        var questions = new List<QuestionResult>();
        int score = 0;

        // Q1: Services.Count >= 3 → +2
        var q1Answer = skillData.Services.Count >= 3;
        var q1Score = q1Answer ? 2 : 0;
        score += q1Score;
        questions.Add(new QuestionResult(
            "Q1", "Does the skill reference 3 or more Azure services?",
            q1Answer, $"Services count: {skillData.Services.Count}"));

        // Q2: UseFor.Count >= 5 → +2
        var q2Answer = skillData.UseFor.Count >= 5;
        var q2Score = q2Answer ? 2 : 0;
        score += q2Score;
        questions.Add(new QuestionResult(
            "Q2", "Does the skill have 5 or more use-for scenarios?",
            q2Answer, $"UseFor count: {skillData.UseFor.Count}"));

        // Q3: ShouldTrigger.Count >= 3 → +1
        var q3Answer = triggerData.ShouldTrigger.Count >= 3;
        var q3Score = q3Answer ? 1 : 0;
        score += q3Score;
        questions.Add(new QuestionResult(
            "Q3", "Does the skill have 3 or more trigger prompts?",
            q3Answer, $"ShouldTrigger count: {triggerData.ShouldTrigger.Count}"));

        // Q4: Description.Length >= 200 → +1
        var q4Answer = (skillData.Description?.Length ?? 0) >= 200;
        var q4Score = q4Answer ? 1 : 0;
        score += q4Score;
        questions.Add(new QuestionResult(
            "Q4", "Is the description 200+ characters?",
            q4Answer, $"Description length: {skillData.Description?.Length ?? 0}"));

        // Q5: Services or McpTools reference Azure → +1
        var q5Answer = skillData.Services.Any(s => s.Name.Contains("Azure", StringComparison.OrdinalIgnoreCase)) ||
                       skillData.McpTools.Any(t => t.Command.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
                                                    t.ToolName.Contains("azure", StringComparison.OrdinalIgnoreCase)) ||
                       skillData.Services.Count > 0 || skillData.McpTools.Count > 0;
        var q5Score = q5Answer ? 1 : 0;
        score += q5Score;
        questions.Add(new QuestionResult(
            "Q5", "Do services or MCP tools reference Azure?",
            q5Answer, $"Azure references found: {q5Answer}"));

        var tier = score >= Tier1Threshold ? 1 : 2;
        var rationale = $"Score {score}/{Tier1Threshold + 3}: {(tier == 1 ? "Tier 1 (comprehensive)" : "Tier 2 (essential)")}";

        return new TierAssessment(
            Tier: tier,
            Questions: questions,
            Rationale: rationale,
            ShowToolsSection: skillData.McpTools.Count > 0,
            ShowTriggersSection: triggerData.ShouldTrigger.Count > 0,
            ShowDecisionGuidance: tier == 1 && skillData.DecisionGuidance.Count > 0,
            ShowWorkflow: tier == 1 && skillData.WorkflowSteps.Count > 0,
            ShowDetailedPrompts: tier == 1);
    }
}
