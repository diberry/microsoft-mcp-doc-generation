using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using Shared;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public class PromptParameterSelectionTests
{
    // Helper: build a minimal CanonicalParameterManifest for testing.
    private static CanonicalParameterManifest BuildManifest(
        params (string canonical, string displayName, string requiredText, bool required, string description)[] entries)
    {
        var parameters = entries.Select(e => new CanonicalParameterEntry(
            e.canonical, e.displayName,
            new[] { e.displayName.ToLowerInvariant().Replace(' ', '-') },
            new[] { e.canonical },
            e.required, e.requiredText,
            false, e.description)).ToArray();

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
            foreach (var alias in p.PlaceholderAliases)
                index.TryAdd(alias, p.CanonicalName);

        return new CanonicalParameterManifest(
            "2.0", "test tool", "test",
            new ManifestSourceIdentity("1.0.0", "2026-01-01T00:00:00Z"),
            parameters, index);
    }

    [Fact]
    public void GetPromptParameters_UsesManifestDisplayNamesAndRequiredMarkers()
    {
        var tool = new Tool
        {
            Command = "keyvault secret get",
            Option =
            [
                new Option { Name = "--vault-name", Required = false, Description = "Raw fallback description" }
            ]
        };
        var manifest = BuildManifest(
            ("vault-name", "Vault name", "Required*", false, "Provide vault name."),
            ("secret-name", "Secret name", "Optional*", false, "Provide secret name."));

        var parameters = ExamplePromptGenerator.GetPromptParameters(tool, manifest);

        Assert.Collection(parameters,
            required =>
            {
                Assert.Equal("Vault name", required.Name);
                Assert.Equal("Required*", required.RequirementText);
                Assert.Equal("Provide vault name.", required.Description);
                Assert.True(required.IsRequired);
            },
            optional =>
            {
                Assert.Equal("Secret name", optional.Name);
                Assert.Equal("Optional*", optional.RequirementText);
                Assert.False(optional.IsRequired);
            });
    }

    [Fact]
    public void GetPromptParameters_FallsBackToCliOptions_WhenManifestMissing()
    {
        var tool = new Tool
        {
            Option =
            [
                new Option { Name = "--vault-name", Required = true, Description = "Vault description" },
                new Option { Name = "--subscription", Required = false, Description = "Subscription description" }
            ]
        };

        var parameters = ExamplePromptGenerator.GetPromptParameters(tool, null);

        Assert.Collection(parameters,
            required =>
            {
                Assert.Equal("--vault-name", required.Name);
                Assert.Equal("Required", required.RequirementText);
                Assert.True(required.IsRequired);
            },
            optional =>
            {
                Assert.Equal("--subscription", optional.Name);
                Assert.Equal("Optional", optional.RequirementText);
                Assert.False(optional.IsRequired);
            });
    }

    [Fact]
    public void GetPromptParameters_UsesEmptyManifestInsteadOfFallingBackToCliOptions()
    {
        var tool = new Tool
        {
            Option =
            [
                new Option { Name = "--vault-name", Required = true, Description = "Vault description" }
            ]
        };

        var parameters = ExamplePromptGenerator.GetPromptParameters(tool, BuildManifest());

        Assert.Empty(parameters);
    }

    [Fact]
    public void GetPromptParameters_ManifestParameters_AreRequiredFirstAndStableWithinGroups_Bug743()
    {
        var tool = new Tool { Command = "storage account update" };
        var manifest = BuildManifest(
            ("storage-optional-first", "storage optional first", "Optional", false, "Storage optional parameter."),
            ("key-vault-required-first", "key vault required first", "Required", true, "Key Vault required parameter."),
            ("cosmos-required-second", "cosmos required second", "Required", true, "Cosmos DB required parameter."),
            ("monitor-optional-second", "monitor optional second", "Optional", false, "Monitor optional parameter."));

        var parameters = ExamplePromptGenerator.GetPromptParameters(tool, manifest);

        Assert.Equal(
            [
                "key vault required first",
                "cosmos required second",
                "storage optional first",
                "monitor optional second"
            ],
            parameters.Select(p => p.Name));
    }

    [Fact]
    public void GetPromptParameters_ToolOptions_AreRequiredFirstAndStableWithinGroups_Bug743()
    {
        var tool = new Tool
        {
            Command = "cosmos database container create",
            Option =
            [
                new Option { Name = "--storage-optional-first", Required = false, Description = "Storage optional parameter." },
                new Option { Name = "--key-vault-required-first", Required = true, Description = "Key Vault required parameter." },
                new Option { Name = "--cosmos-required-second", Required = true, Description = "Cosmos DB required parameter." },
                new Option { Name = "--monitor-optional-second", Required = false, Description = "Monitor optional parameter." }
            ]
        };

        var parameters = ExamplePromptGenerator.GetPromptParameters(tool, null);

        Assert.Equal(
            [
                "--key-vault-required-first",
                "--cosmos-required-second",
                "--storage-optional-first",
                "--monitor-optional-second"
            ],
            parameters.Select(p => p.Name));
    }

    [Fact]
    public void BuildParametersSection_PreservesManifestRequirementText()
    {
        var parameters = new List<(string Name, string RequirementText, string Description, bool IsRequired)>
        {
            ("Vault name", "Required*", "Provide vault name.", true),
            ("Secret name", "Optional*", "Provide secret name.", false)
        };

        var section = ExamplePromptGenerator.BuildParametersSection(parameters);

        Assert.Contains("- Vault name (Required*): Provide vault name.", section);
        Assert.Contains("- Secret name (Optional*): Provide secret name.", section);
    }
}
