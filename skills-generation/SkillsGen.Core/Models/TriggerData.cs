namespace SkillsGen.Core.Models;

public record TriggerData(
    List<string> ShouldTrigger,
    List<string> ShouldNotTrigger,
    string? SourceFile);
