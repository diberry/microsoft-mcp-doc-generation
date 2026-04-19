namespace SkillsGen.Core.Models;

public enum DriftSeverity { Info, Warning, Error }
public enum DriftCategory { GenerationBug, SourceDataGap, ContentPrStale }

public record DriftItem(
    string SkillName,
    string Section,
    DriftSeverity Severity,
    DriftCategory Category,
    string Description,
    string? SuggestedFix);

public record DriftReport(
    string SkillName,
    string GeneratedPath,
    string PublishedUrl,
    List<DriftItem> Items,
    DateTime DetectedAtUtc);
