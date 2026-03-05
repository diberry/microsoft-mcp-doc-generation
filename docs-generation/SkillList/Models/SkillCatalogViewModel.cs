namespace SkillList.Models;

/// <summary>
/// View model for a single skill entry in the catalog page.
/// </summary>
internal record SkillEntry(
    string Name,
    string Description,
    string SkillUrl,
    List<string> RelatedProducts,
    string Category
);

/// <summary>
/// View model for the entire skills catalog page.
/// </summary>
internal record SkillCatalogViewModel(
    string GeneratedAt,
    string CliVersion,
    int TotalSkills,
    int TotalWithMcpMapping,
    List<SkillEntry> Skills
);
