namespace SkillsGen.Core.Parsers;
using SkillsGen.Core.Models;

public interface ISkillParser
{
    SkillData Parse(string skillName, string markdownContent);
}
