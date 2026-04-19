namespace SkillsGen.Core.Validation;

using SkillsGen.Core.Models;

public interface IDriftDetector
{
    DriftReport DetectDrift(string skillName, string generatedContent, string publishedContent, string generatedPath, string publishedUrl);
}
