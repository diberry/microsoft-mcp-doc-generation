// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public class DeterministicExamplePromptGeneratorTests
{
    // ── ClassifyVerb ────────────────────────────────────────────────

    [Theory]
    [InlineData("storage account list", "list")]
    [InlineData("keyvault secret create", "create")]
    [InlineData("cosmos container get", "get")]
    [InlineData("sql database delete", "delete")]
    [InlineData("appservice webapp update", "update")]
    [InlineData("storage blob list", "list")]
    [InlineData("redis create", "create")]
    public void ClassifyVerb_StandardVerbs_ReturnsCorrectVerb(string command, string expected)
    {
        var result = DeterministicExamplePromptGenerator.ClassifyVerb(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("monitor query")]
    [InlineData("kusto execute")]
    [InlineData("speech recognize")]
    [InlineData("deploy generate_plan")]
    public void ClassifyVerb_NonStandardVerbs_ReturnsNull(string command)
    {
        var result = DeterministicExamplePromptGenerator.ClassifyVerb(command);
        Assert.Null(result);
    }

    [Fact]
    public void ClassifyVerb_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(DeterministicExamplePromptGenerator.ClassifyVerb(""));
        Assert.Null(DeterministicExamplePromptGenerator.ClassifyVerb(null!));
    }

    // ── IsEligible ──────────────────────────────────────────────────

    [Fact]
    public void IsEligible_StandardVerbNoE2e_ReturnsTrue()
    {
        var tool = MakeTool("storage account list", "List storage accounts");
        Assert.True(DeterministicExamplePromptGenerator.IsEligible(tool, hasE2ePrompts: false));
    }

    [Fact]
    public void IsEligible_StandardVerbWithE2e_ReturnsFalse()
    {
        var tool = MakeTool("storage account list", "List storage accounts");
        Assert.False(DeterministicExamplePromptGenerator.IsEligible(tool, hasE2ePrompts: true));
    }

    [Fact]
    public void IsEligible_NonStandardVerb_ReturnsFalse()
    {
        var tool = MakeTool("monitor query", "Execute a query");
        Assert.False(DeterministicExamplePromptGenerator.IsEligible(tool, hasE2ePrompts: false));
    }

    // ── ExtractResource ─────────────────────────────────────────────

    [Theory]
    [InlineData("storage account list", "account")]
    [InlineData("keyvault secret create", "secret")]
    [InlineData("cosmos container get", "container")]
    [InlineData("storage blob list", "blob")]
    [InlineData("redis list", "redis")]
    public void ExtractResource_ReturnsMiddleSegments(string command, string expected)
    {
        var result = DeterministicExamplePromptGenerator.ExtractResource(command);
        Assert.Equal(expected, result);
    }

    // ── Generate: structure ─────────────────────────────────────────

    [Fact]
    public void Generate_ReturnsExactlyFivePrompts()
    {
        var tool = MakeTool("storage account list", "List storage accounts",
            new Option { Name = "subscription", Required = true, Description = "Azure subscription" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        Assert.NotNull(response);
        Assert.Equal(5, response.Prompts.Count);
    }

    [Fact]
    public void Generate_SetsToolName()
    {
        var tool = MakeTool("storage account list", "List storage accounts");
        tool.Name = "storage_account_list";

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        Assert.Equal("storage_account_list", response.ToolName);
    }

    // ── Generate: required params in every prompt ───────────────────

    [Fact]
    public void Generate_AllPromptsContainRequiredParamValues()
    {
        var tool = MakeTool("storage blob list", "List blobs",
            new Option { Name = "account", Required = true, Description = "Storage account name" },
            new Option { Name = "container-name", Required = true, Description = "Container name" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        foreach (var prompt in response.Prompts)
        {
            // Each prompt must contain single-quoted values for both required params
            Assert.Matches(@"'[^']+'", prompt); // at least one quoted value
            // Count quoted values — should be at least 2 (one per required param)
            var quoteCount = System.Text.RegularExpressions.Regex.Matches(prompt, @"'[^']+'").Count;
            Assert.True(quoteCount >= 2,
                $"Prompt should contain values for all 2 required params but has {quoteCount} quoted values: {prompt}");
        }
    }

    [Fact]
    public void Generate_NoRequiredParams_StillGeneratesFivePrompts()
    {
        var tool = MakeTool("advisor list", "List recommendations");

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        Assert.Equal(5, response.Prompts.Count);
    }

    // ── Generate: prompt quality ────────────────────────────────────

    [Fact]
    public void Generate_AllPromptsEndWithPunctuation()
    {
        var tool = MakeTool("keyvault secret create", "Create a secret",
            new Option { Name = "vault", Required = true, Description = "Key vault name" },
            new Option { Name = "secret-name", Required = true, Description = "Secret name" },
            new Option { Name = "value", Required = true, Description = "Secret value" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        foreach (var prompt in response.Prompts)
        {
            Assert.True(
                prompt.EndsWith(".") || prompt.EndsWith("?"),
                $"Prompt should end with . or ? but got: {prompt}");
        }
    }

    [Fact]
    public void Generate_PromptsUseSingleQuotesForValues()
    {
        var tool = MakeTool("storage account get", "Get storage account",
            new Option { Name = "account", Required = true, Description = "Storage account name" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        foreach (var prompt in response.Prompts)
        {
            Assert.Contains("'", prompt);
            Assert.DoesNotContain("<", prompt); // no angle brackets
            Assert.DoesNotContain("`", prompt); // no backticks
        }
    }

    [Fact]
    public void Generate_ListVerb_UsesListStyleLanguage()
    {
        var tool = MakeTool("storage table list", "List tables",
            new Option { Name = "account", Required = true, Description = "Storage account" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        // At least one prompt should use list-style language
        var hasListLanguage = response.Prompts.Any(p =>
            p.Contains("List", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Show", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Display", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("What", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasListLanguage, "List verb should produce list-style language");
    }

    [Fact]
    public void Generate_CreateVerb_UsesCreateStyleLanguage()
    {
        var tool = MakeTool("storage account create", "Create storage account",
            new Option { Name = "account", Required = true, Description = "Account name" },
            new Option { Name = "location", Required = true, Description = "Location" },
            new Option { Name = "resource-group", Required = true, Description = "Resource group" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        var hasCreateLanguage = response.Prompts.Any(p =>
            p.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Set up", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Add", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasCreateLanguage, "Create verb should produce create-style language");
    }

    // ── Generate: idempotent ────────────────────────────────────────

    [Fact]
    public void Generate_SameInput_SameOutput()
    {
        var tool1 = MakeTool("storage account list", "List accounts",
            new Option { Name = "subscription", Required = true, Description = "Subscription" });
        var tool2 = MakeTool("storage account list", "List accounts",
            new Option { Name = "subscription", Required = true, Description = "Subscription" });
        tool1.Name = "storage_account_list";
        tool2.Name = "storage_account_list";

        var r1 = DeterministicExamplePromptGenerator.Generate(tool1);
        var r2 = DeterministicExamplePromptGenerator.Generate(tool2);

        Assert.Equal(r1.Prompts, r2.Prompts);
    }

    // ── Generate: varied prompts ────────────────────────────────────

    [Fact]
    public void Generate_FivePromptsAreNotAllIdentical()
    {
        var tool = MakeTool("keyvault secret list", "List secrets",
            new Option { Name = "vault", Required = true, Description = "Key vault name" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        var distinct = response.Prompts.Distinct().Count();
        Assert.True(distinct >= 3, $"Expected at least 3 distinct prompts but got {distinct}");
    }

    [Fact]
    public void Generate_DifferentValuePerPrompt()
    {
        var tool = MakeTool("storage account list", "List accounts",
            new Option { Name = "subscription", Required = true, Description = "Subscription" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        // Extract quoted values from each prompt
        var values = response.Prompts.Select(p =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(p, @"'([^']+)'");
            return match.Success ? match.Groups[1].Value : "";
        }).ToList();

        var distinct = values.Distinct().Count();
        Assert.True(distinct >= 3, $"Expected at least 3 distinct values but got {distinct}: {string.Join(", ", values)}");
    }

    // ── Generate: various verb types ────────────────────────────────

    [Theory]
    [InlineData("storage account list")]
    [InlineData("keyvault secret get")]
    [InlineData("cosmos container create")]
    [InlineData("sql database delete")]
    [InlineData("appservice webapp update")]
    public void Generate_AllVerbTypes_ProduceFivePrompts(string command)
    {
        var tool = MakeTool(command, "Test operation",
            new Option { Name = "name", Required = true, Description = "Resource name" });

        var response = DeterministicExamplePromptGenerator.Generate(tool);

        Assert.Equal(5, response.Prompts.Count);
        Assert.All(response.Prompts, p =>
            Assert.True(p.EndsWith(".") || p.EndsWith("?"), $"Bad punctuation: {p}"));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static Tool MakeTool(string command, string description, params Option[] requiredOptions)
    {
        return new Tool
        {
            Name = command.Replace(" ", "_"),
            Command = command,
            Description = description,
            Option = requiredOptions.Length > 0
                ? requiredOptions.ToList()
                : new List<Option>()
        };
    }
}
