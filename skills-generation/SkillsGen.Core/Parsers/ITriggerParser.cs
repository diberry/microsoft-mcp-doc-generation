namespace SkillsGen.Core.Parsers;
using SkillsGen.Core.Models;

public interface ITriggerParser
{
    TriggerData Parse(string? triggersContent);
}
