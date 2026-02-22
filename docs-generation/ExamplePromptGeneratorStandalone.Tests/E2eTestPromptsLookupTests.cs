using Xunit;
using ExamplePromptGeneratorStandalone.Models;

namespace ExamplePromptGeneratorStandalone.Tests;

public class E2eTestPromptsLookupTests
{
    [Fact]
    public void BuildLookup_ConvertsUnderscoresToSpaces()
    {
        var data = CreateData("advisor_recommendation_list", "List my recommendations");

        var lookup = E2eTestPromptsLookup.BuildLookup(data);

        Assert.True(lookup.ContainsKey("advisor recommendation list"));
        Assert.False(lookup.ContainsKey("advisor_recommendation_list"));
    }

    [Fact]
    public void BuildLookup_PreservesPrompts()
    {
        var data = CreateData("storage_account_list",
            "List storage accounts", "Show all storage accounts in my subscription");

        var lookup = E2eTestPromptsLookup.BuildLookup(data);

        Assert.Equal(2, lookup["storage account list"].Count);
        Assert.Equal("List storage accounts", lookup["storage account list"][0]);
        Assert.Equal("Show all storage accounts in my subscription", lookup["storage account list"][1]);
    }

    [Fact]
    public void BuildLookup_IsCaseInsensitive()
    {
        var data = CreateData("KeyVault_Secret_List", "List secrets");

        var lookup = E2eTestPromptsLookup.BuildLookup(data);

        Assert.True(lookup.ContainsKey("keyvault secret list"));
        Assert.True(lookup.ContainsKey("KEYVAULT SECRET LIST"));
    }

    [Fact]
    public void BuildLookup_HandlesMultipleSections()
    {
        var data = new E2eTestPromptsData
        {
            Sections = new List<E2eSection>
            {
                new()
                {
                    Heading = "Advisor",
                    Tools = new List<E2eToolEntry>
                    {
                        new() { ToolName = "advisor_recommendation_list", TestPrompts = new() { "List recs" } }
                    }
                },
                new()
                {
                    Heading = "Storage",
                    Tools = new List<E2eToolEntry>
                    {
                        new() { ToolName = "storage_account_list", TestPrompts = new() { "List accounts" } }
                    }
                }
            }
        };

        var lookup = E2eTestPromptsLookup.BuildLookup(data);

        Assert.Equal(2, lookup.Count);
        Assert.True(lookup.ContainsKey("advisor recommendation list"));
        Assert.True(lookup.ContainsKey("storage account list"));
    }

    [Fact]
    public void BuildLookup_SkipsDuplicateTools_KeepsFirst()
    {
        var data = new E2eTestPromptsData
        {
            Sections = new List<E2eSection>
            {
                new()
                {
                    Tools = new List<E2eToolEntry>
                    {
                        new() { ToolName = "monitor_alert_list", TestPrompts = new() { "First entry" } },
                        new() { ToolName = "monitor_alert_list", TestPrompts = new() { "Duplicate entry" } }
                    }
                }
            }
        };

        var lookup = E2eTestPromptsLookup.BuildLookup(data);

        Assert.Single(lookup);
        Assert.Equal("First entry", lookup["monitor alert list"][0]);
    }

    [Fact]
    public void BuildLookup_HandlesEmptySections()
    {
        var data = new E2eTestPromptsData { Sections = new List<E2eSection>() };

        var lookup = E2eTestPromptsLookup.BuildLookup(data);

        Assert.Empty(lookup);
    }

    [Fact]
    public void BuildLookup_HandlesToolWithEmptyPrompts()
    {
        var data = CreateData("sql_database_list");

        var lookup = E2eTestPromptsLookup.BuildLookup(data);

        Assert.True(lookup.ContainsKey("sql database list"));
        Assert.Empty(lookup["sql database list"]);
    }

    // ─────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────

    private static E2eTestPromptsData CreateData(string toolName, params string[] prompts)
    {
        return new E2eTestPromptsData
        {
            Sections = new List<E2eSection>
            {
                new()
                {
                    Tools = new List<E2eToolEntry>
                    {
                        new()
                        {
                            ToolName = toolName,
                            TestPrompts = prompts.ToList()
                        }
                    }
                }
            }
        };
    }
}
