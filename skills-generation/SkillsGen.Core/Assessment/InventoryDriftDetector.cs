using Microsoft.Extensions.Logging;
using SkillsGen.Core.Fetchers;
using SkillsGen.Core.Models;
using SkillsGen.Core.Parsers;

namespace SkillsGen.Core.Assessment;

public class InventoryDriftDetector
{
    private readonly ISkillDirectoryLister _skillFetcher;
    private readonly IChangelogFetcher _changelogFetcher;
    private readonly ILogger<InventoryDriftDetector> _logger;

    public InventoryDriftDetector(
        ISkillDirectoryLister skillFetcher,
        IChangelogFetcher changelogFetcher,
        ILogger<InventoryDriftDetector> logger)
    {
        _skillFetcher = skillFetcher;
        _changelogFetcher = changelogFetcher;
        _logger = logger;
    }

    public async Task<InventoryDriftReport> DetectDriftAsync(
        List<SkillInventoryEntry> inventory,
        CancellationToken ct = default)
    {
        // Fetch upstream skill directories (authoritative)
        _logger.LogInformation("Fetching upstream skill directories...");
        var upstreamSkills = await _skillFetcher.ListSubdirectoriesAsync("skills", ct);
        
        // Fetch and parse CHANGELOG
        _logger.LogInformation("Fetching CHANGELOG...");
        var changelogContent = await _changelogFetcher.FetchAsync(ct);
        var changelogEntries = changelogContent != null 
            ? ChangelogParser.Parse(changelogContent) 
            : new List<ChangelogEntry>();

        // Extract inventory skill names
        var inventorySkills = inventory.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var upstreamSet = upstreamSkills.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Find skills in upstream but not in inventory
        var missingInInventory = upstreamSet
            .Where(u => !inventorySkills.Contains(u))
            .OrderBy(s => s)
            .ToList();

        // Find skills in inventory but not in upstream (possible removals)
        var missingInUpstream = inventorySkills
            .Where(i => !upstreamSet.Contains(i))
            .OrderBy(s => s)
            .ToList();

        // Check for naming discrepancies between CHANGELOG and directories
        var namingDiscrepancies = new List<string>();
        foreach (var entry in changelogEntries)
        {
            var allChangelogSkills = entry.SkillsAdded
                .Concat(entry.SkillsRemoved)
                .Concat(entry.SkillsChanged)
                .Distinct();

            foreach (var changelogSkill in allChangelogSkills)
            {
                // Check if CHANGELOG mentions a skill that doesn't exist in upstream or inventory
                if (!upstreamSet.Contains(changelogSkill) && !inventorySkills.Contains(changelogSkill))
                {
                    // Look for similar names (e.g., azure-cost-optimization vs azure-cost)
                    var similarUpstream = upstreamSet
                        .FirstOrDefault(u => u.Contains(changelogSkill) || changelogSkill.Contains(u));
                    
                    if (similarUpstream != null)
                    {
                        namingDiscrepancies.Add($"CHANGELOG says '{changelogSkill}' but directory is '{similarUpstream}'");
                    }
                    else
                    {
                        namingDiscrepancies.Add($"CHANGELOG mentions '{changelogSkill}' but not found in upstream or inventory");
                    }
                }
            }
        }

        _logger.LogInformation(
            "Drift detection complete: {UpstreamCount} upstream, {InventoryCount} inventory, {MissingInInventory} missing, {MissingInUpstream} removed",
            upstreamSkills.Count, inventory.Count, missingInInventory.Count, missingInUpstream.Count);

        return new InventoryDriftReport(
            missingInInventory,
            missingInUpstream,
            namingDiscrepancies.Distinct().OrderBy(s => s).ToList(),
            changelogEntries,
            upstreamSkills.Count,
            inventory.Count);
    }
}
