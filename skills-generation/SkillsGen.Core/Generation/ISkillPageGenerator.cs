namespace SkillsGen.Core.Generation;

using SkillsGen.Core.Models;

public interface ISkillPageGenerator
{
    string Generate(SkillData skillData, TriggerData triggerData, TierAssessment tierAssessment, SkillPrerequisites prerequisites, Func<string, string>? triggerProcessor = null);
}
