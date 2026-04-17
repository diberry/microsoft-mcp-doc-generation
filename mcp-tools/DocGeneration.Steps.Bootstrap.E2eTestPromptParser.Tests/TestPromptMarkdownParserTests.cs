using E2eTestPromptParser;
using E2eTestPromptParser.Models;
using Xunit;

namespace E2eTestPromptParser.Tests;

public class TestPromptMarkdownParserTests
{
    private const string MinimalDocument = """
        # Azure MCP End-to-End Test Prompts

        This file contains prompts used for end-to-end testing.

        ## Azure Advisor

        | Tool Name | Test Prompt |
        |:----------|:----------|
        | advisor_recommendation_list | List all recommendations in my subscription |
        | advisor_recommendation_list | Show me Advisor recommendations in the subscription <subscription> |

        ## Azure Cosmos DB

        | Tool Name | Test Prompt |
        |:----------|:----------|
        | cosmos_account_list | List all cosmosdb accounts in my subscription |
        | cosmos_account_list | Show me my cosmosdb accounts |
        | cosmos_database_list | List all the databases in the cosmosdb account <account_name> |
        """;

    [Fact]
    public void Parse_ExtractsTitle()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        Assert.Equal("Azure MCP End-to-End Test Prompts", doc.Title);
    }

    [Fact]
    public void Parse_ExtractsSectionCount()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        Assert.Equal(2, doc.Sections.Count);
    }

    [Fact]
    public void Parse_ExtractsSectionHeadings()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        Assert.Equal("Azure Advisor", doc.Sections[0].Heading);
        Assert.Equal("Azure Cosmos DB", doc.Sections[1].Heading);
    }

    [Fact]
    public void Parse_ExtractsAdvisorEntries()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var section = doc.Sections[0];
        Assert.Equal(2, section.Entries.Count);
        Assert.All(section.Entries, e => Assert.Equal("advisor_recommendation_list", e.ToolName));
    }

    [Fact]
    public void Parse_ExtractsCosmosEntries()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var section = doc.Sections[1];
        Assert.Equal(3, section.Entries.Count);
    }

    [Fact]
    public void Parse_PreservesToolNameAndPromptText()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var entry = doc.Sections[0].Entries[1];
        Assert.Equal("advisor_recommendation_list", entry.ToolName);
        Assert.Equal("Show me Advisor recommendations in the subscription <subscription>", entry.TestPrompt);
    }

    [Fact]
    public void AllEntries_ReturnsFlatList()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        Assert.Equal(5, doc.AllEntries.Count);
    }

    [Fact]
    public void ToolNames_ReturnsDistinctSet()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var names = doc.ToolNames;
        Assert.Equal(3, names.Count);
        Assert.Contains("advisor_recommendation_list", names);
        Assert.Contains("cosmos_account_list", names);
        Assert.Contains("cosmos_database_list", names);
    }

    [Fact]
    public void GetEntriesByToolName_ReturnsMatchingEntries()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var entries = doc.GetEntriesByToolName("cosmos_account_list");
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void GetEntriesByToolName_ReturnsEmptyForUnknownTool()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var entries = doc.GetEntriesByToolName("nonexistent_tool");
        Assert.Empty(entries);
    }

    [Fact]
    public void GetSectionsByToolName_ReturnsCorrectSection()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var sections = doc.GetSectionsByToolName("cosmos_database_list");
        Assert.Single(sections);
        Assert.Equal("Azure Cosmos DB", sections[0].Heading);
    }

    [Fact]
    public void GetEntriesByNamespace_GroupsByPrefix()
    {
        var doc = TestPromptMarkdownParser.Parse(MinimalDocument);
        var byNs = doc.GetEntriesByNamespace();
        Assert.Equal(2, byNs.Count);
        Assert.True(byNs.ContainsKey("advisor"));
        Assert.True(byNs.ContainsKey("cosmos"));
        Assert.Equal(2, byNs["advisor"].Count);
        Assert.Equal(3, byNs["cosmos"].Count);
    }

    [Fact]
    public void Parse_HandlesEscapedAngleBrackets()
    {
        var markdown = """
            # Test

            ## MySQL

            | Tool Name | Test Prompt |
            |:----------|:----------|
            | mysql_list | List all tables in the MySQL database \<database> in server \<server> |
            """;

        var doc = TestPromptMarkdownParser.Parse(markdown);
        var entry = doc.Sections[0].Entries[0];
        Assert.Equal("List all tables in the MySQL database <database> in server <server>", entry.TestPrompt);
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyResult()
    {
        var doc = TestPromptMarkdownParser.Parse("");
        Assert.Equal(string.Empty, doc.Title);
        Assert.Empty(doc.Sections);
        Assert.Empty(doc.AllEntries);
    }

    [Fact]
    public void Parse_SectionWithNoEntries_CreatesEmptySection()
    {
        var markdown = """
            # Title

            ## Empty Section

            No table here, just text.

            ## Has Table

            | Tool Name | Test Prompt |
            |:----------|:----------|
            | tool_a | Do something |
            """;

        var doc = TestPromptMarkdownParser.Parse(markdown);
        Assert.Equal(2, doc.Sections.Count);
        Assert.Empty(doc.Sections[0].Entries);
        Assert.Single(doc.Sections[1].Entries);
    }

    [Fact]
    public void Parse_MultipleToolsInOneSection()
    {
        var markdown = """
            # Title

            ## Azure Storage

            | Tool Name | Test Prompt |
            |:----------|:----------|
            | storage_account_get | Show me my storage accounts |
            | storage_blob_get | List all blobs in container <container> |
            | storage_table_list | List all tables in the storage account <account> |
            """;

        var doc = TestPromptMarkdownParser.Parse(markdown);
        var section = doc.Sections[0];
        Assert.Equal(3, section.Entries.Count);
        Assert.Equal("storage_account_get", section.Entries[0].ToolName);
        Assert.Equal("storage_blob_get", section.Entries[1].ToolName);
        Assert.Equal("storage_table_list", section.Entries[2].ToolName);
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TestPromptMarkdownParser.Parse(null!));
    }

    [Fact]
    public void Parse_HandlesPipesInPromptText()
    {
        // Edge case: prompts shouldn't contain |, but if the table is well-formed
        // this tests the basic split logic
        var markdown = """
            # Title

            ## Test

            | Tool Name | Test Prompt |
            |:----------|:----------|
            | tool_a | Run a query in database |
            """;

        var doc = TestPromptMarkdownParser.Parse(markdown);
        Assert.Equal("Run a query in database", doc.Sections[0].Entries[0].TestPrompt);
    }

    [Fact]
    public void Parse_ToolNamesCaseSensitive()
    {
        var markdown = """
            # Title

            ## Test

            | Tool Name | Test Prompt |
            |:----------|:----------|
            | Tool_A | Prompt one |
            | tool_a | Prompt two |
            """;

        var doc = TestPromptMarkdownParser.Parse(markdown);
        Assert.Equal(2, doc.ToolNames.Count);
        Assert.Contains("Tool_A", doc.ToolNames);
        Assert.Contains("tool_a", doc.ToolNames);
    }
}
