// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RelatedSkillsGenerator.Models;
using SkillList;

namespace SkillList.Tests;

public class SkillToProductsLookupTests
{
    [Fact]
    public void BuildLookup_SkipsOtherNamespace()
    {
        var mappings = new List<SkillMapping>
        {
            new SkillMapping("other", "", new List<SkillReference>
            {
                new SkillReference("some-skill", "standalone")
            })
        };

        var result = Program.BuildSkillToProductsLookup(mappings);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildLookup_MapsSingleProduct()
    {
        var mappings = new List<SkillMapping>
        {
            new SkillMapping("appservice", "Azure App Service", new List<SkillReference>
            {
                new SkillReference("azure-app-service", "primary")
            })
        };

        var result = Program.BuildSkillToProductsLookup(mappings);

        Assert.Single(result);
        Assert.Equal(new[] { "Azure App Service" }, result["azure-app-service"]);
    }

    [Fact]
    public void BuildLookup_MapsMultipleProducts()
    {
        var mappings = new List<SkillMapping>
        {
            new SkillMapping("foundry", "Microsoft Foundry", new List<SkillReference>
            {
                new SkillReference("azure-ai-foundry-local", "primary")
            }),
            new SkillMapping("search", "Azure AI Search", new List<SkillReference>
            {
                new SkillReference("azure-ai-foundry-local", "related")
            })
        };

        var result = Program.BuildSkillToProductsLookup(mappings);

        Assert.Single(result);
        Assert.Equal(2, result["azure-ai-foundry-local"].Count);
        Assert.Contains("Microsoft Foundry", result["azure-ai-foundry-local"]);
        Assert.Contains("Azure AI Search", result["azure-ai-foundry-local"]);
    }

    [Fact]
    public void BuildLookup_NoDuplicateProducts()
    {
        var mappings = new List<SkillMapping>
        {
            new SkillMapping("storage", "Azure Storage", new List<SkillReference>
            {
                new SkillReference("azure-blob-storage", "primary"),
                new SkillReference("azure-files", "primary")
            })
        };

        var result = Program.BuildSkillToProductsLookup(mappings);

        Assert.Equal(2, result.Count);
        Assert.Single(result["azure-blob-storage"]);
        Assert.Equal("Azure Storage", result["azure-blob-storage"][0]);
    }

    [Fact]
    public void BuildLookup_CaseInsensitiveLookup()
    {
        var mappings = new List<SkillMapping>
        {
            new SkillMapping("cosmos", "Azure Cosmos DB", new List<SkillReference>
            {
                new SkillReference("Azure-Cosmos-DB", "primary")
            })
        };

        var result = Program.BuildSkillToProductsLookup(mappings);

        Assert.True(result.ContainsKey("azure-cosmos-db"));
        Assert.True(result.ContainsKey("Azure-Cosmos-DB"));
    }

    [Fact]
    public void BuildLookup_EmptyMappings_ReturnsEmpty()
    {
        var result = Program.BuildSkillToProductsLookup(new List<SkillMapping>());
        Assert.Empty(result);
    }

    [Fact]
    public void BuildLookup_EmptySkillsList_ReturnsEmpty()
    {
        var mappings = new List<SkillMapping>
        {
            new SkillMapping("quota", "Azure Quota", new List<SkillReference>())
        };

        var result = Program.BuildSkillToProductsLookup(mappings);
        Assert.Empty(result);
    }
}
