using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Fetchers;

public class LocalSkillFetcher : ISkillSourceFetcher
{
    private readonly string _basePath;
    private readonly ILogger<LocalSkillFetcher> _logger;

    public LocalSkillFetcher(string basePath, ILogger<LocalSkillFetcher> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    public Task<SkillSourceFiles?> FetchAsync(string skillName, CancellationToken ct = default)
    {
        var skillDir = Path.Combine(_basePath, skillName);
        var skillMdPath = Path.Combine(skillDir, "SKILL.md");

        if (!File.Exists(skillMdPath))
        {
            _logger.LogWarning("SKILL.md not found at {Path}", skillMdPath);
            return Task.FromResult<SkillSourceFiles?>(null);
        }

        try
        {
            var skillMd = File.ReadAllText(skillMdPath);

            string? triggers = null;
            var triggersPath = Path.Combine(skillDir, "triggers.test.ts");
            if (File.Exists(triggersPath))
            {
                triggers = File.ReadAllText(triggersPath);
            }

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
