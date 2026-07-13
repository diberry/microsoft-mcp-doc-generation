// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Generators;
using GenerativeAI;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

[Trait("Category", "Keyless")]
public sealed class ValidateAIOptionsKeylessTests
{
    [Fact]
    public void Keyless_DefaultCredential_DoesNotRequireApiKey()
    {
        var options = CreateKeylessOptions();

        var missing = ExamplePromptGeneratorStandalone.Program.ValidateAIOptions(options);

        Assert.DoesNotContain("FOUNDRY_API_KEY", missing);
        Assert.Empty(missing);
    }

    [Fact]
    public void Keyless_DefaultCredential_InitializesGeneratorWithoutApiKey()
    {
        var options = CreateKeylessOptions();
        var promptsDir = Path.Combine(FindRepoRoot(), "mcp-tools", "DocGeneration.Steps.ExamplePrompts.Generation", "prompts");

        var generator = new ExamplePromptGenerator(options, promptsDir);

        Assert.True(generator.IsInitialized);
    }

    [Fact]
    public void NonKeyless_MissingApiKey_RequiresApiKey()
    {
        var options = CreateKeylessOptions();
        options.UseDefaultCredential = false;

        var missing = ExamplePromptGeneratorStandalone.Program.ValidateAIOptions(options);

        Assert.Contains("FOUNDRY_API_KEY", missing);
    }

    private static GenerativeAIOptions CreateKeylessOptions()
        => new()
        {
            UseDefaultCredential = true,
            ApiKey = "",
            Endpoint = "https://oai-example-prompts-keyless.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
            ApiVersion = "2024-10-01-preview",
        };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "mcp-doc-generation.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Cannot find repository root.");
    }
}
