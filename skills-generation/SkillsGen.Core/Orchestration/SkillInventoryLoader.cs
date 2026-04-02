using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Orchestration;

public class SkillInventoryLoader
{
    private readonly ILogger<SkillInventoryLoader> _logger;

    public SkillInventoryLoader(ILogger<SkillInventoryLoader> logger)
    {
        _logger = logger;
    }

    public List<SkillInventoryEntry> Load(string inventoryPath)
    {
        if (!File.Exists(inventoryPath))
        {
            _logger.LogWarning("Skills inventory file not found: {Path}", inventoryPath);
            return [];
        }

        var json = File.ReadAllText(inventoryPath);
        return ParseJson(json);
    }

    public List<SkillInventoryEntry> ParseJson(string json)
    {
        try
        {
            var inventory = JsonSerializer.Deserialize<SkillsInventoryFile>(json, JsonOptions);
            if (inventory?.Skills == null)
            {
                _logger.LogWarning("Skills inventory file has no 'skills' array");
                return [];
            }

            return inventory.Skills
                .Select(s => new SkillInventoryEntry(s.Name, s.DisplayName, s.Category))
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse skills inventory JSON");
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private record SkillsInventoryFile(List<SkillEntry> Skills);
    private record SkillEntry(string Name, string DisplayName, string Category);
}
