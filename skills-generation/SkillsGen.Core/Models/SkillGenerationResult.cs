namespace SkillsGen.Core.Models;

public record SkillValidationResult(bool IsValid, List<string> Errors, List<string> Warnings, int WordCount, int SectionCount);

public record SkillGenerationResult(
    string SkillName,
    int Tier,
    SkillValidationResult Validation,
    string? OutputPath,
    long DurationMs);

public record SkillGenerationReport(
    List<SkillGenerationResult> Results,
    long TotalDurationMs,
    string GeneratorVersion,
    string TemplateVersion,
    DateTime GeneratedAtUtc);
