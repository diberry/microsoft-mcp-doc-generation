using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Fetchers;

public class LocalSkillFetcher : ISkillSourceFetcher
{
    private readonly string _skillsPath;
    private readonly string? _testsPath;
    private readonly ILogger<LocalSkillFetcher> _logger;

    public LocalSkillFetcher(string skillsPath, ILogger<LocalSkillFetcher> logger, string? testsPath = null)
    {
        _skillsPath = skillsPath;
        _testsPath = testsPath;
        _logger = logger;
    }

    public Task<SkillSourceFiles?> FetchAsync(string skillName, CancellationToken ct = default)
    {
        var skillDir = Path.Combine(_skillsPath, skillName);
        var skillMdPath = Path.Combine(skillDir, "SKILL.md");

        if (!File.Exists(skillMdPath))
        {
            _logger.LogWarning("SKILL.md not found at {Path}", skillMdPath);
            return Task.FromResult<SkillSourceFiles?>(null);
        }

        try
        {
            var skillMd = File.ReadAllText(skillMdPath);

            // Look for triggers in multiple locations:
            // 1. {testsPath}/{skillName}/triggers.test.ts (separate tests directory)
            // 2. {skillsPath}/{skillName}/triggers.test.ts (co-located)
            string? triggers = null;
            string? triggersSource = null;

            if (_testsPath != null)
            {
                var testsFilePath = Path.Combine(_testsPath, skillName, "triggers.test.ts");
                if (File.Exists(testsFilePath))
                {
                    triggers = File.ReadAllText(testsFilePath);
                    triggersSource = testsFilePath;
                }
            }

            if (triggers == null)
            {
                var colocatedPath = Path.Combine(skillDir, "triggers.test.ts");
                if (File.Exists(colocatedPath))
                {
                    triggers = File.ReadAllText(colocatedPath);
                    triggersSource = colocatedPath;
                }
            }

            if (triggers != null)
                _logger.LogDebug("Found triggers for {Skill} at {Path}", skillName, triggersSource);
            else
                _logger.LogWarning("No triggers.test.ts found for {Skill}", skillName);

            return Task.FromResult<SkillSourceFiles?>(
                new SkillSourceFiles(skillMd, triggers, skillDir, null));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read skill files from {Path}", skillDir);
            return Task.FromResult<SkillSourceFiles?>(null);
        }
    }
}
