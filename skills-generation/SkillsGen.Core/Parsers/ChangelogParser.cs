using System.Text.RegularExpressions;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Parsers;

public static class ChangelogParser
{
    private static readonly Regex VersionHeaderRegex = new(@"^##\s+\[([^\]]+)\]\s+-\s+(\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);
    private static readonly Regex SkillNameRegex = new(@"`([a-z0-9\-]+)`", RegexOptions.Compiled);

    public static List<ChangelogEntry> Parse(string changelogContent)
    {
        var entries = new List<ChangelogEntry>();
        var lines = changelogContent.Split('\n');
        
        ChangelogEntry? currentEntry = null;
        string? currentSection = null;
        var skillsAdded = new List<string>();
        var skillsRemoved = new List<string>();
        var skillsChanged = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Check for version header
            var match = VersionHeaderRegex.Match(line);
            if (match.Success)
            {
                // Save previous entry if exists
                if (currentEntry != null)
                {
                    entries.Add(currentEntry);
                }

                // Start new entry
                var version = match.Groups[1].Value;
                var dateStr = match.Groups[2].Value;
                var date = DateOnly.Parse(dateStr);
                
                skillsAdded = new List<string>();
                skillsRemoved = new List<string>();
                skillsChanged = new List<string>();
                currentEntry = new ChangelogEntry(version, date, skillsAdded, skillsRemoved, skillsChanged);
                currentSection = null;
                continue;
            }

            // Check for section headers
            if (line.StartsWith("### Added"))
            {
                currentSection = "added";
                continue;
            }
            if (line.StartsWith("### Removed"))
            {
                currentSection = "removed";
                continue;
            }
            if (line.StartsWith("### Changed"))
            {
                currentSection = "changed";
                continue;
            }

            // Extract skill names from bullet points
            if (currentSection != null && line.StartsWith("-"))
            {
                var skillMatches = SkillNameRegex.Matches(line);
                foreach (Match skillMatch in skillMatches)
                {
                    var skillName = skillMatch.Groups[1].Value;
                    
                    // Skip non-skill entries (like version numbers, other references)
                    if (!IsSkillName(skillName))
                        continue;

                    switch (currentSection)
                    {
                        case "added":
                            if (!skillsAdded.Contains(skillName))
                                skillsAdded.Add(skillName);
                            break;
                        case "removed":
                            if (!skillsRemoved.Contains(skillName))
                                skillsRemoved.Add(skillName);
                            break;
                        case "changed":
                            if (!skillsChanged.Contains(skillName))
                                skillsChanged.Add(skillName);
                            break;
                    }
                }
            }
        }

        // Add last entry
        if (currentEntry != null)
        {
            entries.Add(currentEntry);
        }

        return entries;
    }

    private static bool IsSkillName(string name)
    {
        // Skills typically start with 'azure-', 'microsoft-', 'appinsights-', 'entra-', or 'airunway-'
        return name.StartsWith("azure-") || 
               name.StartsWith("microsoft-") || 
               name.StartsWith("appinsights-") || 
               name.StartsWith("entra-") ||
               name.StartsWith("airunway-");
    }
}
