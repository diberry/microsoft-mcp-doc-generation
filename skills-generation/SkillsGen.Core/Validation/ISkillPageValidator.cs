namespace SkillsGen.Core.Validation;

using SkillsGen.Core.Models;

public interface ISkillPageValidator
{
    SkillValidationResult Validate(string renderedContent, int tier, SkillData skillData, TriggerData triggerData);
}
