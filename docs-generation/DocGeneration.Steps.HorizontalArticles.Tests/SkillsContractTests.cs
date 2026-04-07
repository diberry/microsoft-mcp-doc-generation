// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using HorizontalArticleGenerator.Services;
using SkillsRelevance.Models;
using SkillsRelevance.Output;
using Xunit;

namespace HorizontalArticleGenerator.Tests;

/// <summary>
/// Cross-step contract test: validates that Step 5 JSON output can be consumed by Step 6 reader.
/// This catches writer/reader drift since they use separate models.
/// </summary>
public class SkillsContractTests : IDisposable
{
    private readonly string _outputBasePath;

    public SkillsContractTests()
    {
        _outputBasePath = Path.Combine(Path.GetTempPath(), "skills-contract-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputBasePath))
            Directory.Delete(_outputBasePath, recursive: true);
    }

    [Fact]
    public async Task Step5Writer_Step6Reader_Roundtrips()
    {
        var skillsDir = Path.Combine(_outputBasePath, "skills-relevance");

        var skills = new List<SkillInfo>
        {
            new()
            {
                Name = "Copilot for Azure",
                SourceRepository = "microsoft/GitHub-Copilot-for-Azure",
                SourceUrl = "https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/azure.md",
                RelevanceScore = 0.85,
                Description = "Azure resource management and troubleshooting",
                Tags = new List<string> { "azure", "cloud", "management" }
            },
            new()
            {
                Name = "Storage Explorer Skill",
                SourceRepository = "test/storage-skills",
                SourceUrl = "https://github.com/test/storage-skills/blob/main/explorer.md",
                RelevanceScore = 0.55,
                Description = "Navigate and manage Azure Storage accounts",
                Tags = new List<string> { "storage", "blob" }
            },
            new()
            {
                Name = "Low Relevance Skill",
                SourceRepository = "test/low",
                SourceUrl = "https://github.com/test/low/blob/main/skill.md",
                RelevanceScore = 0.3,
                Description = "Not very relevant",
                Tags = new List<string> { "misc" }
            }
        };

        // Step 5 writes JSON
        await SkillsMarkdownWriter.WriteServiceJsonAsync(skillsDir, "storage", skills);

        // Step 6 reads JSON
        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "storage");

        // Verify: high + medium skills returned, low skill filtered
        Assert.Equal(2, result.Count);

        Assert.Equal("Copilot for Azure", result[0].Name);
        Assert.Equal(0.85, result[0].RelevanceScore);
        Assert.Equal("Azure resource management and troubleshooting", result[0].Description);
        Assert.Equal("https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/azure.md", result[0].SourceUrl);

        Assert.Equal("Storage Explorer Skill", result[1].Name);
        Assert.Equal(0.55, result[1].RelevanceScore);
    }

    [Fact]
    public async Task Step5Writer_Step6Reader_EmptySkills_Roundtrips()
    {
        var skillsDir = Path.Combine(_outputBasePath, "skills-relevance");
        await SkillsMarkdownWriter.WriteServiceJsonAsync(skillsDir, "keyvault", new List<SkillInfo>());

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "keyvault");

        Assert.Empty(result);
    }
}
