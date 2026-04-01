// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using SkillsRelevance.Services;

namespace SkillsRelevance.Tests;

public class SkillContentParserTests
{
    // ── Markdown with YAML frontmatter ────────────────────────────────────

    [Fact]
    public void Parse_MarkdownWithFrontmatter_ExtractsName()
    {
        var content = """
            ---
            name: Azure Storage Helper
            description: Helps with Azure Blob Storage operations
            ---
            # Azure Storage Helper
            Some body text.
            """;

        var skill = SkillContentParser.Parse("storage-helper.md", content, "https://github.com/org/repo/storage-helper.md", "https://raw.githubusercontent.com/org/repo/main/storage-helper.md", "Test Source");

        Assert.Equal("Azure Storage Helper", skill.Name);
    }

    [Fact]
    public void Parse_MarkdownWithFrontmatter_ExtractsDescription()
    {
        var content = """
            ---
            description: Manages Azure Key Vault secrets
            ---
            Some body text.
            """;

        var skill = SkillContentParser.Parse("kv.md", content, "https://example.com/kv.md", "https://raw.example.com/kv.md", "Test Source");

        Assert.Equal("Manages Azure Key Vault secrets", skill.Description);
    }

    [Fact]
    public void Parse_MarkdownWithFrontmatter_ExtractsTags()
    {
        var content = """
            ---
            tags:
              - azure
              - storage
              - blob
            ---
            Body content.
            """;

        var skill = SkillContentParser.Parse("tags-test.md", content, "https://example.com/tags-test.md", "https://raw.example.com/tags-test.md", "Test Source");

        Assert.Contains("azure", skill.Tags);
        Assert.Contains("storage", skill.Tags);
        Assert.Contains("blob", skill.Tags);
    }

    [Fact]
    public void Parse_MarkdownWithFrontmatter_ExtractsAuthorAndVersion()
    {
        var content = """
            ---
            author: Jane Doe
            version: 1.2.0
            ---
            Content.
            """;

        var skill = SkillContentParser.Parse("versioned.md", content, "https://example.com/versioned.md", "https://raw.example.com/versioned.md", "Test Source");

        Assert.Equal("Jane Doe", skill.Author);
        Assert.Equal("1.2.0", skill.Version);
    }

    [Fact]
    public void Parse_MarkdownWithFrontmatter_ExtractsLastUpdated()
    {
        var content = """
            ---
            date: 2024-03-15
            ---
            Content.
            """;

        var skill = SkillContentParser.Parse("dated.md", content, "https://example.com/dated.md", "https://raw.example.com/dated.md", "Test Source");

        Assert.NotNull(skill.LastUpdated);
        Assert.Equal(2024, skill.LastUpdated!.Value.Year);
        Assert.Equal(3, skill.LastUpdated.Value.Month);
    }

    [Fact]
    public void Parse_MarkdownNoFrontmatter_FallsBackToH1ForName()
    {
        var content = """
            # My Skill Title

            This is the description paragraph.
            """;

        var skill = SkillContentParser.Parse("my-skill.md", content, "https://example.com/my-skill.md", "https://raw.example.com/my-skill.md", "Test Source");

        Assert.Equal("My Skill Title", skill.Name);
    }

    [Fact]
    public void Parse_MarkdownNoFrontmatterNoH1_DeriveNameFromFilename()
    {
        var content = "Just some plain text with no headings.";

        var skill = SkillContentParser.Parse("my-skill-name.md", content, "https://example.com/my-skill-name.md", "https://raw.example.com/my-skill-name.md", "Test Source");

        Assert.Equal("my skill name", skill.Name);
    }

    [Fact]
    public void Parse_SetsRawContent()
    {
        var content = "# Title\nSome content here.";
        var skill = SkillContentParser.Parse("skill.md", content, "https://example.com/skill.md", "https://raw.example.com/skill.md", "Source");
        Assert.Equal(content, skill.RawContent);
    }

    [Fact]
    public void Parse_SetsSourceFields()
    {
        var skill = SkillContentParser.Parse(
            "skill.md",
            "content",
            "https://example.com/html",
            "https://raw.example.com/raw",
            "My Source");

        Assert.Equal("skill.md", skill.FileName);
        Assert.Equal("https://example.com/html", skill.SourceUrl);
        Assert.Equal("https://raw.example.com/raw", skill.RawContentUrl);
        Assert.Equal("My Source", skill.SourceRepository);
    }

    // ── Plain YAML ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_YamlFile_ExtractsFields()
    {
        var content = """
            name: Cosmos DB Skill
            description: Helps with Azure Cosmos DB NoSQL queries
            category: database
            """;

        var skill = SkillContentParser.Parse("cosmos.yml", content, "https://example.com/cosmos.yml", "https://raw.example.com/cosmos.yml", "Source");

        Assert.Equal("Cosmos DB Skill", skill.Name);
        Assert.Equal("Helps with Azure Cosmos DB NoSQL queries", skill.Description);
        Assert.Equal("database", skill.Category);
    }

    [Fact]
    public void Parse_YamlFile_ExtractsServicesList()
    {
        var content = """
            name: Multi-Service Skill
            services:
              - Azure Monitor
              - Azure Log Analytics
            """;

        var skill = SkillContentParser.Parse("multi.yaml", content, "https://example.com/multi.yaml", "https://raw.example.com/multi.yaml", "Source");

        Assert.Contains("Azure Monitor", skill.AzureServices);
        Assert.Contains("Azure Log Analytics", skill.AzureServices);
    }

    // ── JSON ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_JsonFile_ExtractsFields()
    {
        var content = """
            {
              "name": "Key Vault JSON Skill",
              "description": "Manages secrets in Azure Key Vault",
              "author": "DevOps Team",
              "version": "2.0",
              "category": "security"
            }
            """;

        var skill = SkillContentParser.Parse("kv.json", content, "https://example.com/kv.json", "https://raw.example.com/kv.json", "Source");

        Assert.Equal("Key Vault JSON Skill", skill.Name);
        Assert.Equal("Manages secrets in Azure Key Vault", skill.Description);
        Assert.Equal("DevOps Team", skill.Author);
        Assert.Equal("2.0", skill.Version);
        Assert.Equal("security", skill.Category);
    }

    [Fact]
    public void Parse_JsonFile_ExtractsTags()
    {
        var content = """
            {
              "name": "Tagged Skill",
              "tags": ["azure", "devops", "pipelines"]
            }
            """;

        var skill = SkillContentParser.Parse("tagged.json", content, "https://example.com/tagged.json", "https://raw.example.com/tagged.json", "Source");

        Assert.Contains("azure", skill.Tags);
        Assert.Contains("devops", skill.Tags);
        Assert.Contains("pipelines", skill.Tags);
    }

    [Fact]
    public void Parse_InvalidJson_FallsBackToPlainText()
    {
        var content = "not valid json {{ }}";
        var skill = SkillContentParser.Parse("bad.json", content, "https://example.com/bad.json", "https://raw.example.com/bad.json", "Source");
        // Should not throw; description should be populated with some content
        Assert.NotEmpty(skill.Description);
    }

    // ── ExtractAzureServices ──────────────────────────────────────────────

    [Fact]
    public void ExtractAzureServices_FindsKnownServices()
    {
        var content = "This skill helps you use Azure Key Vault and Azure Monitor for observability.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.Contains("Azure Key Vault", services);
        Assert.Contains("Azure Monitor", services);
    }

    [Fact]
    public void ExtractAzureServices_IsCaseInsensitive()
    {
        var content = "Connect to azure storage and AZURE SQL databases.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.Contains("Azure Storage", services);
        Assert.Contains("Azure SQL", services);
    }

    [Fact]
    public void ExtractAzureServices_NoDuplicates()
    {
        var content = "Azure Storage is great. Use Azure Storage for blobs. Azure Storage is fast.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.Equal(1, services.Count(s => s.Equals("Azure Storage", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ExtractAzureServices_EmptyContent_ReturnsEmptyList()
    {
        var services = SkillContentParser.ExtractAzureServices(string.Empty);
        Assert.Empty(services);
    }

    [Fact]
    public void ExtractAzureServices_NoKnownServices_ReturnsEmptyList()
    {
        var content = "This is about JavaScript and Node.js with no Azure services.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.Empty(services);
    }

    // ── ExtractSection ────────────────────────────────────────────────────

    [Fact]
    public void ExtractSection_ReturnsContentUnderMatchingHeading()
    {
        var content = """
            # Overview
            Some overview text.

            ## Best Practices
            Always use managed identities.
            Prefer RBAC over access keys.

            ## Troubleshooting
            Check logs first.
            """;

        var section = SkillContentParser.ExtractSection(content, "best practice");
        Assert.Contains("managed identities", section);
        Assert.Contains("RBAC", section);
    }

    [Fact]
    public void ExtractSection_StopsAtNextHeading()
    {
        var content = """
            ## Best Practices
            Use this approach.

            ## Other Section
            This should not be included.
            """;

        var section = SkillContentParser.ExtractSection(content, "best practice");
        Assert.DoesNotContain("should not be included", section);
    }

    [Fact]
    public void ExtractSection_HeadingNotFound_ReturnsEmpty()
    {
        var content = """
            # Overview
            Some text.
            """;

        var section = SkillContentParser.ExtractSection(content, "troubleshoot");
        Assert.Empty(section);
    }

    [Fact]
    public void ExtractSection_IsCaseInsensitive()
    {
        var content = """
            ## TROUBLESHOOTING
            Check the Azure Monitor logs.
            """;

        var section = SkillContentParser.ExtractSection(content, "troubleshoot");
        Assert.Contains("Azure Monitor", section);
    }
}
