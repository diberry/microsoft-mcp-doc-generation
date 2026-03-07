// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NUnit.Framework;
using SkillsRelevance.Services;

namespace SkillsRelevance.Tests;

[TestFixture]
public class SkillContentParserTests
{
    // ── Markdown with YAML frontmatter ────────────────────────────────────

    [Test]
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

        Assert.That(skill.Name, Is.EqualTo("Azure Storage Helper"));
    }

    [Test]
    public void Parse_MarkdownWithFrontmatter_ExtractsDescription()
    {
        var content = """
            ---
            description: Manages Azure Key Vault secrets
            ---
            Some body text.
            """;

        var skill = SkillContentParser.Parse("kv.md", content, "https://example.com/kv.md", "https://raw.example.com/kv.md", "Test Source");

        Assert.That(skill.Description, Is.EqualTo("Manages Azure Key Vault secrets"));
    }

    [Test]
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

        Assert.That(skill.Tags, Contains.Item("azure"));
        Assert.That(skill.Tags, Contains.Item("storage"));
        Assert.That(skill.Tags, Contains.Item("blob"));
    }

    [Test]
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

        Assert.That(skill.Author, Is.EqualTo("Jane Doe"));
        Assert.That(skill.Version, Is.EqualTo("1.2.0"));
    }

    [Test]
    public void Parse_MarkdownWithFrontmatter_ExtractsLastUpdated()
    {
        var content = """
            ---
            date: 2024-03-15
            ---
            Content.
            """;

        var skill = SkillContentParser.Parse("dated.md", content, "https://example.com/dated.md", "https://raw.example.com/dated.md", "Test Source");

        Assert.That(skill.LastUpdated, Is.Not.Null);
        Assert.That(skill.LastUpdated!.Value.Year, Is.EqualTo(2024));
        Assert.That(skill.LastUpdated.Value.Month, Is.EqualTo(3));
    }

    [Test]
    public void Parse_MarkdownNoFrontmatter_FallsBackToH1ForName()
    {
        var content = """
            # My Skill Title

            This is the description paragraph.
            """;

        var skill = SkillContentParser.Parse("my-skill.md", content, "https://example.com/my-skill.md", "https://raw.example.com/my-skill.md", "Test Source");

        Assert.That(skill.Name, Is.EqualTo("My Skill Title"));
    }

    [Test]
    public void Parse_MarkdownNoFrontmatterNoH1_DeriveNameFromFilename()
    {
        var content = "Just some plain text with no headings.";

        var skill = SkillContentParser.Parse("my-skill-name.md", content, "https://example.com/my-skill-name.md", "https://raw.example.com/my-skill-name.md", "Test Source");

        Assert.That(skill.Name, Is.EqualTo("my skill name"));
    }

    [Test]
    public void Parse_SetsRawContent()
    {
        var content = "# Title\nSome content here.";
        var skill = SkillContentParser.Parse("skill.md", content, "https://example.com/skill.md", "https://raw.example.com/skill.md", "Source");
        Assert.That(skill.RawContent, Is.EqualTo(content));
    }

    [Test]
    public void Parse_SetsSourceFields()
    {
        var skill = SkillContentParser.Parse(
            "skill.md",
            "content",
            "https://example.com/html",
            "https://raw.example.com/raw",
            "My Source");

        Assert.That(skill.FileName, Is.EqualTo("skill.md"));
        Assert.That(skill.SourceUrl, Is.EqualTo("https://example.com/html"));
        Assert.That(skill.RawContentUrl, Is.EqualTo("https://raw.example.com/raw"));
        Assert.That(skill.SourceRepository, Is.EqualTo("My Source"));
    }

    // ── Plain YAML ────────────────────────────────────────────────────────

    [Test]
    public void Parse_YamlFile_ExtractsFields()
    {
        var content = """
            name: Cosmos DB Skill
            description: Helps with Azure Cosmos DB NoSQL queries
            category: database
            """;

        var skill = SkillContentParser.Parse("cosmos.yml", content, "https://example.com/cosmos.yml", "https://raw.example.com/cosmos.yml", "Source");

        Assert.That(skill.Name, Is.EqualTo("Cosmos DB Skill"));
        Assert.That(skill.Description, Is.EqualTo("Helps with Azure Cosmos DB NoSQL queries"));
        Assert.That(skill.Category, Is.EqualTo("database"));
    }

    [Test]
    public void Parse_YamlFile_ExtractsServicesList()
    {
        var content = """
            name: Multi-Service Skill
            services:
              - Azure Monitor
              - Azure Log Analytics
            """;

        var skill = SkillContentParser.Parse("multi.yaml", content, "https://example.com/multi.yaml", "https://raw.example.com/multi.yaml", "Source");

        Assert.That(skill.AzureServices, Contains.Item("Azure Monitor"));
        Assert.That(skill.AzureServices, Contains.Item("Azure Log Analytics"));
    }

    // ── JSON ─────────────────────────────────────────────────────────────

    [Test]
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

        Assert.That(skill.Name, Is.EqualTo("Key Vault JSON Skill"));
        Assert.That(skill.Description, Is.EqualTo("Manages secrets in Azure Key Vault"));
        Assert.That(skill.Author, Is.EqualTo("DevOps Team"));
        Assert.That(skill.Version, Is.EqualTo("2.0"));
        Assert.That(skill.Category, Is.EqualTo("security"));
    }

    [Test]
    public void Parse_JsonFile_ExtractsTags()
    {
        var content = """
            {
              "name": "Tagged Skill",
              "tags": ["azure", "devops", "pipelines"]
            }
            """;

        var skill = SkillContentParser.Parse("tagged.json", content, "https://example.com/tagged.json", "https://raw.example.com/tagged.json", "Source");

        Assert.That(skill.Tags, Contains.Item("azure"));
        Assert.That(skill.Tags, Contains.Item("devops"));
        Assert.That(skill.Tags, Contains.Item("pipelines"));
    }

    [Test]
    public void Parse_InvalidJson_FallsBackToPlainText()
    {
        var content = "not valid json {{ }}";
        var skill = SkillContentParser.Parse("bad.json", content, "https://example.com/bad.json", "https://raw.example.com/bad.json", "Source");
        // Should not throw; description should be populated with some content
        Assert.That(skill.Description, Is.Not.Empty);
    }

    // ── ExtractAzureServices ──────────────────────────────────────────────

    [Test]
    public void ExtractAzureServices_FindsKnownServices()
    {
        var content = "This skill helps you use Azure Key Vault and Azure Monitor for observability.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.That(services, Contains.Item("Azure Key Vault"));
        Assert.That(services, Contains.Item("Azure Monitor"));
    }

    [Test]
    public void ExtractAzureServices_IsCaseInsensitive()
    {
        var content = "Connect to azure storage and AZURE SQL databases.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.That(services, Contains.Item("Azure Storage"));
        Assert.That(services, Contains.Item("Azure SQL"));
    }

    [Test]
    public void ExtractAzureServices_NoDuplicates()
    {
        var content = "Azure Storage is great. Use Azure Storage for blobs. Azure Storage is fast.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.That(services.Count(s => s.Equals("Azure Storage", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
    }

    [Test]
    public void ExtractAzureServices_EmptyContent_ReturnsEmptyList()
    {
        var services = SkillContentParser.ExtractAzureServices(string.Empty);
        Assert.That(services, Is.Empty);
    }

    [Test]
    public void ExtractAzureServices_NoKnownServices_ReturnsEmptyList()
    {
        var content = "This is about JavaScript and Node.js with no Azure services.";
        var services = SkillContentParser.ExtractAzureServices(content);
        Assert.That(services, Is.Empty);
    }

    // ── ExtractSection ────────────────────────────────────────────────────

    [Test]
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
        Assert.That(section, Does.Contain("managed identities"));
        Assert.That(section, Does.Contain("RBAC"));
    }

    [Test]
    public void ExtractSection_StopsAtNextHeading()
    {
        var content = """
            ## Best Practices
            Use this approach.

            ## Other Section
            This should not be included.
            """;

        var section = SkillContentParser.ExtractSection(content, "best practice");
        Assert.That(section, Does.Not.Contain("should not be included"));
    }

    [Test]
    public void ExtractSection_HeadingNotFound_ReturnsEmpty()
    {
        var content = """
            # Overview
            Some text.
            """;

        var section = SkillContentParser.ExtractSection(content, "troubleshoot");
        Assert.That(section, Is.Empty);
    }

    [Test]
    public void ExtractSection_IsCaseInsensitive()
    {
        var content = """
            ## TROUBLESHOOTING
            Check the Azure Monitor logs.
            """;

        var section = SkillContentParser.ExtractSection(content, "troubleshoot");
        Assert.That(section, Does.Contain("Azure Monitor"));
    }
}
