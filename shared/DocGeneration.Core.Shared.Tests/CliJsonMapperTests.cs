// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class CliJsonMapperTests
{
    // ── Shared JSON fixtures ────────────────────────────────────────

    private const string SingleToolJson = """
        {
          "status": 200,
          "message": "Success",
          "results": [
            {
              "command": "storage account list",
              "name": "list",
              "description": "List storage accounts in a subscription.",
              "option": [
                { "name": "--subscription", "description": "Azure subscription ID", "type": "string" },
                { "name": "--resource-group", "description": "Resource group name", "type": "string" }
              ],
              "metadata": {
                "destructive": { "value": false },
                "readOnly": { "value": true }
              }
            }
          ]
        }
        """;

    private const string MultipleToolsJson = """
        {
          "results": [
            {
              "command": "storage account list",
              "name": "list",
              "description": "List accounts.",
              "option": []
            },
            {
              "command": "storage blob upload",
              "name": "upload",
              "description": "Upload blob.",
              "option": []
            }
          ]
        }
        """;

    private const string EnrichedJson = """
        {
          "results": [
            {
              "command": "storage account list",
              "name": "list",
              "description": "List storage accounts in a subscription.",
              "option": [
                { "name": "--subscription", "description": "Azure subscription ID", "type": "string" }
              ],
              "enrichment": {
                "matched": true,
                "parameterEnhancements": {
                  "--subscription": {
                    "default": "AZURE_SUBSCRIPTION_ID env var",
                    "valuePlaceholder": "<subscription-id>",
                    "allowedValues": ["sub1", "sub2"]
                  }
                }
              }
            }
          ]
        }
        """;

    // ── MapFromCliOutput tests ──────────────────────────────────────

    [Fact]
    public void MapFromCliOutput_SingleTool_MapsCorrectly()
    {
        var result = CliJsonMapper.MapFromCliOutput(SingleToolJson);

        Assert.Single(result);
        Assert.True(result.ContainsKey("storage account list"));
        var tool = result["storage account list"];
        Assert.Equal("storage account list", tool.Command);
        Assert.Equal("List storage accounts in a subscription.", tool.Description);
    }

    [Fact]
    public void MapFromCliOutput_MultipleTools_AllMapped()
    {
        var result = CliJsonMapper.MapFromCliOutput(MultipleToolsJson);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("storage account list"));
        Assert.True(result.ContainsKey("storage blob upload"));
    }

    [Fact]
    public void MapFromCliOutput_OptionsMapToSwitches()
    {
        var result = CliJsonMapper.MapFromCliOutput(SingleToolJson);
        var tool = result["storage account list"];

        Assert.Equal(2, tool.Switches.Count);
        Assert.Equal("--subscription", tool.Switches[0].Name);
        Assert.Equal("Azure subscription ID", tool.Switches[0].Description);
        Assert.Equal("string", tool.Switches[0].Type);
        Assert.Equal("--resource-group", tool.Switches[1].Name);
    }

    [Fact]
    public void MapFromCliOutput_MissingOptionalFields_DefaultsGracefully()
    {
        var json = """
            {
              "results": [
                {
                  "command": "some tool"
                }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);
        var tool = result["some tool"];

        Assert.Equal("", tool.Description);
        Assert.Empty(tool.Switches);
    }

    [Fact]
    public void MapFromCliOutput_EmptyResults_ReturnsEmptyDictionary()
    {
        var json = """{ "results": [] }""";

        var result = CliJsonMapper.MapFromCliOutput(json);

        Assert.Empty(result);
    }

    [Fact]
    public void MapFromCliOutput_MalformedJson_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CliJsonMapper.MapFromCliOutput("not json"));
    }

    [Fact]
    public void MapFromCliOutput_MissingResultsProperty_ThrowsMeaningfully()
    {
        var json = """{ "status": 200 }""";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CliJsonMapper.MapFromCliOutput(json));

        Assert.Contains("results", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapFromCliOutput_NullResults_ThrowsMeaningfully()
    {
        var json = """{ "results": null }""";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CliJsonMapper.MapFromCliOutput(json));

        Assert.Contains("results", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapFromCliOutput_DuplicateCommands_LastWins()
    {
        var json = """
            {
              "results": [
                { "command": "storage list", "description": "first" },
                { "command": "storage list", "description": "second" }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);

        Assert.Single(result);
        Assert.Equal("second", result["storage list"].Description);
    }

    [Fact]
    public void MapFromCliOutput_CommandNormalized_CaseInsensitive()
    {
        var json = """
            {
              "results": [
                { "command": "Storage Account List", "description": "test" }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);

        Assert.True(result.ContainsKey("storage account list"));
        Assert.True(result.ContainsKey("STORAGE ACCOUNT LIST"));
    }

    [Fact]
    public void MapFromCliOutput_CommandNormalized_WhitespaceCollapsed()
    {
        var json = """
            {
              "results": [
                { "command": "  storage   account   list  ", "description": "test" }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);

        Assert.True(result.ContainsKey("storage account list"));
    }

    [Fact]
    public void MapFromCliOutput_MetadataDestructive_Mapped()
    {
        var json = """
            {
              "results": [
                {
                  "command": "storage delete",
                  "metadata": { "destructive": { "value": true } }
                }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);

        Assert.True(result["storage delete"].IsDestructive);
    }

    [Fact]
    public void MapFromCliOutput_MetadataReadOnly_Mapped()
    {
        var result = CliJsonMapper.MapFromCliOutput(SingleToolJson);

        Assert.True(result["storage account list"].IsReadOnly);
    }

    [Fact]
    public void MapFromCliOutput_MetadataMissing_DefaultsFalse()
    {
        var json = """
            {
              "results": [
                { "command": "simple tool" }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);
        var tool = result["simple tool"];

        Assert.False(tool.IsDestructive);
        Assert.False(tool.IsReadOnly);
    }

    [Fact]
    public void MapFromCliOutput_ToolMissingCommand_Skipped()
    {
        var json = """
            {
              "results": [
                { "description": "no command field" },
                { "command": "valid tool", "description": "has command" }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);

        Assert.Single(result);
        Assert.True(result.ContainsKey("valid tool"));
    }

    [Fact]
    public void MapFromCliOutput_OptionMissingName_Skipped()
    {
        var json = """
            {
              "results": [
                {
                  "command": "test tool",
                  "option": [
                    { "description": "no name" },
                    { "name": "--valid", "description": "has name" }
                  ]
                }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromCliOutput(json);
        var tool = result["test tool"];

        Assert.Single(tool.Switches);
        Assert.Equal("--valid", tool.Switches[0].Name);
    }

    // ── MapFromEnrichedCliOutput tests ──────────────────────────────

    [Fact]
    public void MapFromEnrichedCliOutput_MergesDefaults()
    {
        var result = CliJsonMapper.MapFromEnrichedCliOutput(EnrichedJson);
        var tool = result["storage account list"];
        var sub = tool.Switches.First(s => s.Name == "--subscription");

        Assert.Equal("AZURE_SUBSCRIPTION_ID env var", sub.Default);
    }

    [Fact]
    public void MapFromEnrichedCliOutput_MergesValuePlaceholder()
    {
        var result = CliJsonMapper.MapFromEnrichedCliOutput(EnrichedJson);
        var tool = result["storage account list"];
        var sub = tool.Switches.First(s => s.Name == "--subscription");

        Assert.Equal("<subscription-id>", sub.ValuePlaceholder);
    }

    [Fact]
    public void MapFromEnrichedCliOutput_MergesAllowedValues()
    {
        var result = CliJsonMapper.MapFromEnrichedCliOutput(EnrichedJson);
        var tool = result["storage account list"];
        var sub = tool.Switches.First(s => s.Name == "--subscription");

        Assert.NotNull(sub.AllowedValues);
        Assert.Equal(new[] { "sub1", "sub2" }, sub.AllowedValues);
    }

    [Fact]
    public void MapFromEnrichedCliOutput_EnrichmentMatched_Mapped()
    {
        var result = CliJsonMapper.MapFromEnrichedCliOutput(EnrichedJson);
        var tool = result["storage account list"];

        Assert.True(tool.EnrichmentMatched);
    }

    [Fact]
    public void MapFromEnrichedCliOutput_EnrichmentNotMatched_FlagFalse()
    {
        var json = """
            {
              "results": [
                {
                  "command": "test tool",
                  "option": [],
                  "enrichment": { "matched": false }
                }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromEnrichedCliOutput(json);

        Assert.False(result["test tool"].EnrichmentMatched);
    }

    [Fact]
    public void MapFromEnrichedCliOutput_NoEnrichment_FallsBackToBase()
    {
        var json = """
            {
              "results": [
                {
                  "command": "plain tool",
                  "description": "no enrichment",
                  "option": [
                    { "name": "--flag", "description": "a flag", "type": "bool" }
                  ]
                }
              ]
            }
            """;

        var result = CliJsonMapper.MapFromEnrichedCliOutput(json);
        var tool = result["plain tool"];

        Assert.Null(tool.EnrichmentMatched);
        Assert.Null(tool.Switches[0].Default);
        Assert.Null(tool.Switches[0].ValuePlaceholder);
        Assert.Null(tool.Switches[0].AllowedValues);
    }

    // ── NormalizeCommand tests ──────────────────────────────────────

    [Fact]
    public void NormalizeCommand_TrimsAndLowercases()
    {
        Assert.Equal("storage list", CliJsonMapper.NormalizeCommand("  Storage List  "));
    }

    [Fact]
    public void NormalizeCommand_CollapsesWhitespace()
    {
        Assert.Equal("storage account list", CliJsonMapper.NormalizeCommand("storage   account   list"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void NormalizeCommand_EmptyOrNull_ReturnsEmpty(string? input)
    {
        Assert.Equal("", CliJsonMapper.NormalizeCommand(input!));
    }
}
