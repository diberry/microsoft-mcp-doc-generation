using SkillsGen.Core.Models;

namespace SkillsGen.Core.Assessment;

public interface ITierAssessor
{
    TierAssessment Assess(SkillData skillData, TriggerData triggerData);
}
