using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public class PromptParameterSelectionTests
{
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
        var manifest = new List<ParameterManifestParameter>
        {
            new()
            {
                Name = "--vault-name",
                DisplayName = "Vault name",
                Required = false,
                RequiredText = "Required*",
                Description = "Provide vault name."
            },
            new()
            {
                Name = "--secret-name",
                DisplayName = "Secret name",
                Required = false,
                RequiredText = "Optional*",
                Description = "Provide secret name."
            }
        };

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

        var parameters = ExamplePromptGenerator.GetPromptParameters(tool, new List<ParameterManifestParameter>());

        Assert.Empty(parameters);
    }

    [Fact]
    public void GetPromptParameters_ManifestParameters_AreRequiredFirstAndStableWithinGroups_Bug743()
    {
        var tool = new Tool { Command = "storage account update" };
        var manifest = new List<ParameterManifestParameter>
        {
            new()
            {
                Name = "--storage-optional-first",
                DisplayName = "storage optional first",
                RequiredText = "Optional",
                Description = "Storage optional parameter."
            },
            new()
            {
                Name = "--key-vault-required-first",
                DisplayName = "key vault required first",
                RequiredText = "Required",
                Description = "Key Vault required parameter."
            },
            new()
            {
                Name = "--cosmos-required-second",
                DisplayName = "cosmos required second",
                RequiredText = "Required",
                Description = "Cosmos DB required parameter."
            },
            new()
            {
                Name = "--monitor-optional-second",
                DisplayName = "monitor optional second",
                RequiredText = "Optional",
                Description = "Monitor optional parameter."
            }
        };

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
    public void GetPromptParameters_UsesSharedParameterSortingHelper_Bug743()
    {
        var sourcePath = FindRepositoryFile(
            "mcp-tools",
            "DocGeneration.Steps.ExamplePrompts.Generation",
            "Generators",
            "ExamplePromptGenerator.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("ParameterSorting.SortByRequiredThenName", source);
        Assert.DoesNotContain(".OrderByDescending(p => p.IsRequired)", source);
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

    private static string FindRepositoryFile(params string[] relativePathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePathParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{Path.Combine(relativePathParts)}'.");
    }
}
