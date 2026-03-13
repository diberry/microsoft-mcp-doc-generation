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
