// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RelatedSkillsGenerator.Parsers;
using Xunit;

namespace RelatedSkillsGenerator.Tests;

public class CatalogParserTests
{
    private const string SampleCatalog = """
        # 📚 Azure Agent Skills Catalog

        ## 📍 Quick Navigation

        - [🕸️ Web](#web)
        - [☁️ Compute](#compute)

        ## ☁️ Compute

        Skills for Compute.

        | Skill | Description |
        |-------|-------------|
        | [azure-functions](skills/azure-functions/) | Expert knowledge for Azure Functions development including troubleshooting. |
        | [azure-virtual-machines](skills/azure-virtual-machines/) | Expert knowledge for Azure Virtual Machines. |

        ---

        ## 🕸️ Web

        Skills for Web.

        | Skill | Description |
        |-------|-------------|
        | [azure-app-service](skills/azure-app-service/) | Expert knowledge for Azure App Service development. |
        | [azure-static-web-apps](skills/azure-static-web-apps/) | Expert knowledge for Azure Static Web Apps. |

        ---

        ## 📊 Summary

        **Total Skills:** 4
        """;

    [Fact]
    public void ParseContent_ExtractsSkillsFromMultipleCategories()
    {
        var skills = CatalogParser.ParseContent(SampleCatalog);

        Assert.Equal(4, skills.Count);
    }

    [Fact]
    public void ParseContent_AssignsCorrectCategory()
    {
        var skills = CatalogParser.ParseContent(SampleCatalog);

        var functions = skills.First(s => s.Name == "azure-functions");
        Assert.Equal("Compute", functions.Category);

        var appService = skills.First(s => s.Name == "azure-app-service");
        Assert.Equal("Web", appService.Category);
    }

    [Fact]
    public void ParseContent_ExtractsDescription()
    {
        var skills = CatalogParser.ParseContent(SampleCatalog);

        var functions = skills.First(s => s.Name == "azure-functions");
        Assert.Contains("Azure Functions development", functions.Description);
    }

    [Fact]
    public void ParseContent_GeneratesCorrectUrl()
    {
        var skills = CatalogParser.ParseContent(SampleCatalog);

        var appService = skills.First(s => s.Name == "azure-app-service");
        Assert.Equal("https://github.com/MicrosoftDocs/Agent-Skills/tree/main/skills/azure-app-service", appService.SkillUrl);
    }

    [Fact]
    public void ParseContent_SkipsSummarySection()
    {
        var skills = CatalogParser.ParseContent(SampleCatalog);

        Assert.DoesNotContain(skills, s => s.Category == "Summary");
    }

    [Fact]
    public void ParseContent_EmptyContent_ReturnsEmpty()
    {
        var skills = CatalogParser.ParseContent("");
        Assert.Empty(skills);
    }
}

public class BundlesParserTests
{
    private const string SampleBundles = """
        # 🎁 Curated Skill Bundles

        ## 📍 Quick Navigation

        - [🚀 Quick Start Bundle](#quick-start-bundle)
        - [⭐ Popular Bundle](#popular-bundle)

        ## 🚀 Quick Start Bundle

        **Start here.** Absolute essentials for any Azure developer.

        | Skill | Description |
        |-------|-------------|
        | [azure-app-service](../skills/azure-app-service/) | Expert knowledge for App Service. |
        | [azure-functions](../skills/azure-functions/) | Expert knowledge for Functions. |
        | [azure-key-vault](../skills/azure-key-vault/) | Expert knowledge for Key Vault. |

        ---

        ## ⭐ Popular Bundle

        **Popular Azure services.** The services that power most workloads.

        | Skill | Description |
        |-------|-------------|
        | [azure-app-service](../skills/azure-app-service/) | Expert knowledge for App Service. |
        | [azure-cosmos-db](../skills/azure-cosmos-db/) | Expert knowledge for Cosmos DB. |

        ---
        """;

    [Fact]
    public void ParseContent_ExtractsBundleNames()
    {
        var bundles = BundlesParser.ParseContent(SampleBundles);

        Assert.Equal(2, bundles.Count);
        Assert.Equal("Quick Start Bundle", bundles[0].Name);
        Assert.Equal("Popular Bundle", bundles[1].Name);
    }

    [Fact]
    public void ParseContent_ExtractsBundleDescription()
    {
        var bundles = BundlesParser.ParseContent(SampleBundles);

        Assert.Equal("Start here", bundles[0].Description);
        Assert.Equal("Popular Azure services", bundles[1].Description);
    }

    [Fact]
    public void ParseContent_ExtractsSkillNames()
    {
        var bundles = BundlesParser.ParseContent(SampleBundles);

        var quickStart = bundles[0];
        Assert.Equal(3, quickStart.SkillNames.Count);
        Assert.Contains("azure-app-service", quickStart.SkillNames);
        Assert.Contains("azure-functions", quickStart.SkillNames);
        Assert.Contains("azure-key-vault", quickStart.SkillNames);
    }

    [Fact]
    public void ParseContent_GeneratesAnchorId()
    {
        var bundles = BundlesParser.ParseContent(SampleBundles);

        Assert.Contains("quick-start-bundle", bundles[0].AnchorId);
    }

    [Fact]
    public void GetBundleUrl_ReturnsCorrectUrl()
    {
        var url = BundlesParser.GetBundleUrl("quick-start-bundle");
        Assert.Contains("BUNDLES.md#quick-start-bundle", url);
    }

    [Fact]
    public void ParseContent_SkipsNavigationSection()
    {
        var bundles = BundlesParser.ParseContent(SampleBundles);
        Assert.DoesNotContain(bundles, b => b.Name == "Quick Navigation");
    }

    [Fact]
    public void ParseContent_EmptyContent_ReturnsEmpty()
    {
        var bundles = BundlesParser.ParseContent("");
        Assert.Empty(bundles);
    }
}
