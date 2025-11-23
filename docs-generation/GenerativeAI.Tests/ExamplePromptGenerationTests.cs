using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace GenerativeAI.Tests;

public class ExamplePromptGenerationTests
{
    private readonly ITestOutputHelper _output;

    public ExamplePromptGenerationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GenerateExamplePrompts_WithStorageAccountTool_Returns5Prompts()
    {
        // Arrange
        var systemPromptPath = Path.Combine("..", "..", "..", "..", "prompts", "system-prompt-example-prompt.txt");
        var userPromptTemplatePath = Path.Combine("..", "..", "..", "..", "prompts", "user-prompt-example-prompt.txt");

        var systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
        var userPromptTemplate = await File.ReadAllTextAsync(userPromptTemplatePath);

        // Fill in the template with fake tool data
        var userPrompt = userPromptTemplate
            .Replace("{TOOL_NAME}", "Azure Storage Account Creator")
            .Replace("{TOOL_COMMAND}", "azure storage account create")
            .Replace("{ACTION_VERB}", "create")
            .Replace("{RESOURCE_TYPE}", "storage account")
            .Replace("{{#each PARAMETERS}}\n- {{name}} ({{#if required}}Required{{else}}Optional{{/if}}): {{description}}\n{{/each}}", 
                @"- account-name (Required): The name of the storage account (3-24 lowercase letters and numbers)
- resource-group (Required): Name of the resource group
- location (Required): Azure region (e.g., eastus, westus2, centralus)
- sku (Optional): Storage account SKU (Standard_LRS, Standard_GRS, Premium_LRS)
- kind (Optional): Storage account kind (StorageV2, BlobStorage, FileStorage)");

        var client = new GenerativeAI.GenerativeAIClient();

        _output.WriteLine("========================================");
        _output.WriteLine("SYSTEM PROMPT:");
        _output.WriteLine("========================================");
        _output.WriteLine(systemPrompt);
        _output.WriteLine("");
        _output.WriteLine("========================================");
        _output.WriteLine("USER PROMPT:");
        _output.WriteLine("========================================");
        _output.WriteLine(userPrompt);
        _output.WriteLine("");

        // Act
        var response = await client.GetChatCompletionAsync(systemPrompt, userPrompt);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response);

        _output.WriteLine("========================================");
        _output.WriteLine("AI RESPONSE:");
        _output.WriteLine("========================================");
        _output.WriteLine(response);
        _output.WriteLine("");

        // Verify output has 5 prompts
        var lines = response.Split('\n').Where(l => l.Trim().StartsWith("- **")).ToArray();
        Assert.Equal(5, lines.Length);
    }
}
