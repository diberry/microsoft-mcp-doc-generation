using ToolGeneration_Composed.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ComposedToolGeneratorServiceTests
{
    [Fact]
    public async Task GenerateComposedToolFilesAsync_StripsEmbeddedExamplePromptMarkers()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"composed-tool-generator-tests-{Guid.NewGuid():N}");
        var rawToolsDir = Path.Combine(testRoot, "raw-tools");
        var outputDir = Path.Combine(testRoot, "output");
        var annotationsDir = Path.Combine(testRoot, "annotations");
        var parametersDir = Path.Combine(testRoot, "parameters");
        var examplePromptsDir = Path.Combine(testRoot, "example-prompts");

        Directory.CreateDirectory(rawToolsDir);
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(annotationsDir);
        Directory.CreateDirectory(parametersDir);
        Directory.CreateDirectory(examplePromptsDir);

        try
        {
            var rawToolPath = Path.Combine(rawToolsDir, "azure-compute-list.md");
            await File.WriteAllTextAsync(rawToolPath, "---\n---\n# list\n\n<!-- @mcpcli compute list -->\n\nLists compute resources.\n\n{{EXAMPLE_PROMPTS_CONTENT}}\n\n{{PARAMETERS_CONTENT}}\n\n{{ANNOTATIONS_CONTENT}}\n");

            await File.WriteAllTextAsync(
                Path.Combine(examplePromptsDir, "azure-compute-list-example-prompts.md"),
                "---\n---\n<!-- @mcpcli compute list -->\n<!-- Required parameters: 1 - 'Resource group' -->\n\nExample prompts include:\n\n- \"List compute resources in 'rg-app'\"\n");

            await File.WriteAllTextAsync(
                Path.Combine(parametersDir, "azure-compute-list-parameters.md"),
                "---\n---\n| Parameter | Required or optional | Description |\n| --- | --- | --- |\n| **Resource group** | Required | Resource group name. |\n");

            await File.WriteAllTextAsync(
                Path.Combine(annotationsDir, "azure-compute-list-annotations.md"),
                "---\n---\nAnnotation content.\n");

            var service = new ComposedToolGeneratorService();
            var exitCode = await service.GenerateComposedToolFilesAsync(rawToolsDir, outputDir, annotationsDir, parametersDir, examplePromptsDir);

            Assert.Equal(0, exitCode);

            var outputPath = Path.Combine(outputDir, "azure-compute-list.md");
            var output = await File.ReadAllTextAsync(outputPath);
            var markerMatches = System.Text.RegularExpressions.Regex.Matches(output, System.Text.RegularExpressions.Regex.Escape("<!-- @mcpcli compute list -->"));

            Assert.Single(markerMatches.Cast<System.Text.RegularExpressions.Match>());
            Assert.Contains("Example prompts include:", output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
