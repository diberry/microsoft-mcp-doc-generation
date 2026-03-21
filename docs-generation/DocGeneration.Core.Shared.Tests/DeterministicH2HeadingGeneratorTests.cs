// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Shared;
using Xunit;

namespace Shared.Tests;

public class DeterministicH2HeadingGeneratorTests
{
    // ─── Verb Mapping Tests ───────────────────────────────────────────

    [Theory]
    [InlineData("compute vm list", "List virtual machines", "Get virtual machines")]
    [InlineData("compute vm get", "Get a virtual machine", "Get virtual machine")]
    [InlineData("compute vm create", "Create a new VM", "Create virtual machine")]
    [InlineData("compute vm delete", "Delete a virtual machine", "Delete virtual machine")]
    [InlineData("compute vm update", "Update a virtual machine", "Update virtual machine")]
    [InlineData("search index query", "Query documents in an index", "Query search index")]
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
}
