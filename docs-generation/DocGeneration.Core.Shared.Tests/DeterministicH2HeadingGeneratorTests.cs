// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Shared;
using Xunit;

namespace Shared.Tests;

public class DeterministicH2HeadingGeneratorTests
{
    // ─── Bug #390: Source-aware pluralization ──────────────────────────

    [Fact]
    public void GenerateHeading_DescriptionResource_SkipsPluralizeOnList()
    {
        // "storage list" → verb="list" (from VerbMap), resource extracted from description
        // Description already has "accounts" (plural) → should NOT re-pluralize
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "storage list", "List storage accounts");
        Assert.Contains("accounts", heading, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accountses", heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateHeading_CommandResource_PluralizesOnList()
    {
        // "compute vm list" → verb="list", resource from command segment ["vm"]
        // Command-sourced resource SHOULD be pluralized
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "compute vm list", "List virtual machines");
        Assert.Contains("machines", heading, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Bug #390: Pluralize safety net (belt-and-suspenders) ────────

    [Theory]
    [InlineData("databases", "databases")]       // Already plural — must NOT become "databaseses"
    [InlineData("resources", "resources")]        // Already plural
    [InlineData("servers", "servers")]            // Already plural
    [InlineData("indexes", "indexes")]            // Already plural (ends in "xes")
    [InlineData("addresses", "addresses")]        // Already plural (ends in "ses")
    [InlineData("disk", "disks")]                 // Singular — should pluralize
    [InlineData("machine", "machines")]           // Singular — should pluralize
    [InlineData("cluster", "clusters")]           // Singular — should pluralize
    [InlineData("index", "indexes")]              // Singular ending in x — should add es
    [InlineData("cache", "caches")]               // Singular ending in che — should add s
    public void Pluralize_HandlesAlreadyPluralWords(string input, string expected)
    {
        var result = DeterministicH2HeadingGenerator.Pluralize(input);
        Assert.Equal(expected, result);
    }

    // ─── Bug #383 Issue #2: Compound words ──────────────────────────

    [Fact]
    public void GenerateHeading_ExpandsCompoundWords()
    {
        var compoundWords = new Dictionary<string, string>
        {
            ["webtests"] = "web-tests",
            ["loganalytics"] = "log-analytics",
        };
        // "monitor webtests list" → verb=list, resource=["webtests"]
        // With compound words: "webtests" → "web tests" → pluralized → "web tests"
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "monitor webtests list", "List web tests", compoundWords);
        Assert.Contains("web tests", heading, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("webtests", heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateHeading_ExpandsLogAnalytics()
    {
        var compoundWords = new Dictionary<string, string>
        {
            ["loganalytics"] = "log-analytics",
        };
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "monitor loganalytics query", "Query log analytics data", compoundWords);
        Assert.Contains("log analytics", heading, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("loganalytics", heading, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Bug #383 Issue #3: No namespace prefix for single-word resources ─

    [Fact]
    public void GenerateHeading_NoNamespacePrefix()
    {
        // "compute disk create" → should be "Create disk" NOT "Create compute disk"
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "compute disk create", "Create a new Azure managed disk");
        Assert.Equal("Create disk", heading);
    }

    [Fact]
    public void GenerateHeadings_ComputeNamespace_NoNamespacePrefix_StillUnique()
    {
        // After removing namespace prefix, disambiguation should still produce unique headings
        var tools = new[]
        {
            ("compute disk create", "Create a new Azure managed disk"),
            ("compute disk delete", "Delete an Azure managed disk"),
            ("compute disk get", "Get details of a managed disk"),
            ("compute vm create", "Create a new virtual machine"),
            ("compute vm delete", "Delete a virtual machine"),
            ("compute vm get", "Get details of a virtual machine"),
        };

        var headings = DeterministicH2HeadingGenerator.GenerateHeadings(
            tools.Select(t => (t.Item1, (string?)t.Item2)));

        Assert.Equal(6, headings.Count);
        Assert.Equal(headings.Values.Distinct().Count(), headings.Count);

        // "disk" headings should NOT contain "compute" prefix
        var diskCreate = headings["compute disk create"];
        Assert.DoesNotContain("compute", diskCreate, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Verb Mapping Tests ───────────────────────────────────────────

    [Theory]
    [InlineData("compute vm list", "List virtual machines", "Get virtual machines")]
    [InlineData("compute vm get", "Get a virtual machine", "Get virtual machine")]
    [InlineData("compute vm create", "Create a new VM", "Create virtual machine")]
    [InlineData("compute vm delete", "Delete a virtual machine", "Delete virtual machine")]
    [InlineData("compute vm update", "Update a virtual machine", "Update virtual machine")]
    [InlineData("search index query", "Query documents in an index", "Query index")]
    public void GenerateHeading_VerbMapping(string command, string description, string expected)
    {
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(command, description);
        Assert.Equal(expected, heading);
    }

    // ─── Abbreviation Expansion Tests ─────────────────────────────────

    [Theory]
    [InlineData("compute vm create", "Create a new VM", "Create virtual machine")]
    [InlineData("compute vmss get", "Get VMSS details", "Get virtual machine scale set")]
    public void GenerateHeading_ExpandsAbbreviations(string command, string description, string expected)
    {
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(command, description);
        Assert.Equal(expected, heading);
    }

    // ─── Uniqueness: Fileshares Namespace (14 tools, many verb collisions) ──

    [Fact]
    public void GenerateHeadings_FilesharesNamespace_AllUnique()
    {
        var tools = new[]
        {
            ("fileshares fileshare check-name-availability", "Check if a file share name is available"),
            ("fileshares fileshare create", "Create a new Azure managed file share resource"),
            ("fileshares fileshare delete", "Delete a file share"),
            ("fileshares fileshare get", "Get details of a specific file share or list all file shares"),
            ("fileshares fileshare peconnection get", "Get details of a private endpoint connection"),
            ("fileshares fileshare peconnection update", "Update the state of a private endpoint connection"),
            ("fileshares fileshare snapshot create", "Create a snapshot of an Azure managed file share"),
            ("fileshares fileshare snapshot delete", "Delete a file share snapshot permanently"),
            ("fileshares fileshare snapshot get", "Get details of a file share snapshot or list all snapshots"),
            ("fileshares fileshare snapshot update", "Update properties of a file share snapshot"),
            ("fileshares fileshare update", "Update an existing Azure managed file share resource"),
            ("fileshares limits", "Get file share limits for a subscription and location"),
            ("fileshares rec", "Get provisioning parameter recommendations for a file share"),
            ("fileshares usage", "Get file share usage data for a subscription and location"),
        };

        var headings = DeterministicH2HeadingGenerator.GenerateHeadings(
            tools.Select(t => (t.Item1, (string?)t.Item2)));

        // ALL 14 headings must be unique
        Assert.Equal(14, headings.Count);
        Assert.Equal(headings.Values.Distinct().Count(), headings.Count);

        // No bare verbs allowed
        foreach (var heading in headings.Values)
        {
            Assert.False(heading.Split(' ').Length < 2,
                $"Heading '{heading}' is a bare verb — must be at least 2 words");
        }
    }

    // ─── Uniqueness: Search Namespace (6 tools, 3x "get" collisions) ──

    [Fact]
    public void GenerateHeadings_SearchNamespace_AllUnique()
    {
        var tools = new[]
        {
            ("search index get", "List/get Azure AI Search indexes"),
            ("search index query", "Query documents in an Azure AI Search index"),
            ("search knowledge base get", "Get details of Azure AI Search knowledge bases"),
            ("search knowledge base retrieve", "Execute a retrieval operation using a knowledge base"),
            ("search knowledge source get", "Get details of Azure AI Search knowledge sources"),
            ("search service list", "List Azure AI Search services in a subscription"),
        };

        var headings = DeterministicH2HeadingGenerator.GenerateHeadings(
            tools.Select(t => (t.Item1, (string?)t.Item2)));

        Assert.Equal(6, headings.Count);
        Assert.Equal(headings.Values.Distinct().Count(), headings.Count);
    }

    // ─── Uniqueness: Compute Namespace (12 tools, 3x each verb) ───────

    [Fact]
    public void GenerateHeadings_ComputeNamespace_AllUnique()
    {
        var tools = new[]
        {
            ("compute disk create", "Create a new Azure managed disk"),
            ("compute disk delete", "Delete an Azure managed disk"),
            ("compute disk get", "Get details of a managed disk"),
            ("compute disk update", "Update a managed disk"),
            ("compute vm create", "Create a new virtual machine"),
            ("compute vm delete", "Delete a virtual machine"),
            ("compute vm get", "Get details of a virtual machine"),
            ("compute vm update", "Update a virtual machine"),
            ("compute vmss create", "Create a virtual machine scale set"),
            ("compute vmss delete", "Delete a virtual machine scale set"),
            ("compute vmss get", "Get details of a virtual machine scale set"),
            ("compute vmss update", "Update a virtual machine scale set"),
        };

        var headings = DeterministicH2HeadingGenerator.GenerateHeadings(
            tools.Select(t => (t.Item1, (string?)t.Item2)));

        Assert.Equal(12, headings.Count);
        Assert.Equal(headings.Values.Distinct().Count(), headings.Count);
    }

    // ─── Heading Format Constraints ───────────────────────────────────

    [Fact]
    public void GenerateHeading_StartsWithVerb_TitleCase()
    {
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "compute vm create", "Create a new virtual machine");
        Assert.Matches(@"^[A-Z][a-z]+ ", heading); // Starts with capital verb + space
    }

    [Fact]
    public void GenerateHeading_MaxSevenWords()
    {
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "fileshares fileshare check-name-availability",
            "Check if a file share name is available");
        Assert.True(heading.Split(' ').Length <= 7, $"Heading '{heading}' exceeds 7 words");
    }

    [Fact]
    public void GenerateHeading_NullDescription_StillWorks()
    {
        var heading = DeterministicH2HeadingGenerator.GenerateHeading("compute vm get", null);
        Assert.NotEmpty(heading);
        Assert.DoesNotContain("##", heading); // Raw text, no markdown
    }

    [Fact]
    public void GenerateHeading_SingleSegmentCommand()
    {
        // Commands like "fileshares limits" with no resource segment
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "fileshares limits", "Get file share limits");
        Assert.True(heading.Split(' ').Length >= 2);
    }

    // ─── Sentence Case: ToSentenceCase Direct Tests ───────────────────

    [Theory]
    [InlineData("Get All Resources", "Get all resources")]
    [InlineData("List SQL Databases", "List SQL databases")]
    [InlineData("Create File Share", "Create file share")]
    [InlineData("Delete Azure VM", "Delete Azure VM")]
    [InlineData("Get Resource Health Events", "Get resource health events")]
    [InlineData("Get all resources", "Get all resources")] // already sentence case
    [InlineData("Get", "Get")] // single word
    [InlineData("Create Cosmos DB Account", "Create Cosmos DB account")]
    [InlineData("Update Kubernetes Cluster", "Update Kubernetes cluster")]
    [InlineData("Query PostgreSQL Databases", "Query PostgreSQL databases")]
    [InlineData("Get Redis Cache", "Get Redis cache")]
    [InlineData("Delete Entra ID Configuration", "Delete Entra ID configuration")]
    [InlineData("List SignalR Services", "List SignalR services")]
    [InlineData("", "")] // empty string
    public void ToSentenceCase_ConvertsCorrectly(string input, string expected)
    {
        var result = DeterministicH2HeadingGenerator.ToSentenceCase(input);
        Assert.Equal(expected, result);
    }

    // ─── Sentence Case: GenerateHeading Integration Tests ─────────────

    [Fact]
    public void GenerateHeading_ProducesSentenceCase()
    {
        // Heading for known verbs should be sentence case
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "compute vm create", "Create a new virtual machine");
        // "Create virtual machine" — first word capitalized, rest lowercase
        Assert.Equal("Create virtual machine", heading);
        Assert.Matches(@"^[A-Z][a-z]+ [a-z]", heading);
    }

    [Fact]
    public void GenerateHeading_PreservesProperNounsInSentenceCase()
    {
        // "compute vmss get" expands vmss to "virtual machine scale set"
        // Sentence case should lowercase non-proper-noun words
        var heading = DeterministicH2HeadingGenerator.GenerateHeading(
            "compute vmss get", "Get details of a virtual machine scale set");
        Assert.StartsWith("Get", heading);
        // "virtual" should be lowercase (not a proper noun)
        Assert.Contains("virtual", heading);
    }

    [Fact]
    public void GenerateHeadings_AllOutputsSentenceCase()
    {
        var tools = new[]
        {
            ("compute disk create", "Create a new Azure managed disk"),
            ("compute disk delete", "Delete an Azure managed disk"),
            ("compute vm create", "Create a new virtual machine"),
            ("compute vm get", "Get details of a virtual machine"),
        };

        var headings = DeterministicH2HeadingGenerator.GenerateHeadings(
            tools.Select(t => (t.Item1, (string?)t.Item2)));

        foreach (var heading in headings.Values)
        {
            // Every heading: first word capitalized, subsequent non-proper-noun words lowercase
            var words = heading.Split(' ');
            Assert.True(char.IsUpper(words[0][0]),
                $"Heading '{heading}' — first word must start with uppercase");

            for (int i = 1; i < words.Length; i++)
            {
                var word = words[i];
                // Either it's a recognized proper noun (could be any case) or it's lowercase
                bool isProperNoun = DeterministicH2HeadingGenerator.ProperNouns.Contains(word);
                if (!isProperNoun)
                {
                    Assert.True(char.IsLower(word[0]),
                        $"Heading '{heading}' — word '{word}' at position {i} should be lowercase (not a proper noun)");
                }
            }
        }
    }
}
