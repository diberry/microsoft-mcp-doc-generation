namespace SkillsGen.Core.Models;

public record InventoryDriftReport(
    List<string> SkillsInUpstreamNotInInventory,
    List<string> SkillsInInventoryNotInUpstream,
    List<string> ChangelogNamingDiscrepancies,
    List<ChangelogEntry> ChangelogEntries,
    int UpstreamSkillCount,
    int InventorySkillCount)
{
    public bool HasDrift => 
        SkillsInUpstreamNotInInventory.Count > 0 || 
        SkillsInInventoryNotInUpstream.Count > 0;
}
