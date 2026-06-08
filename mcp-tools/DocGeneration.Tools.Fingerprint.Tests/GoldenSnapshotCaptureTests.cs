namespace DocGeneration.Tools.Fingerprint.Tests;

public sealed class GoldenSnapshotCaptureTests
{
    [Fact]
    public void CaptureFromOutputDirectory_ClassifiesFilesAndExtractsMetadata()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            WriteFile(outputDirectory, "generation-summary.md", "# Summary");
            WriteFile(outputDirectory, Path.Combine("annotations", "advisor.md"), "deterministic annotations");
            WriteFile(outputDirectory, Path.Combine("common-general", "overview.md"), "deterministic common");
            WriteFile(outputDirectory, Path.Combine("tools", "advisor-tool.md"), """
                ---
                title: Advisor tool
                ms.topic: how-to
                description: Generated tool article.
                ---

                # Advisor tool

                ## Usage

                Details.

                ## Examples

                More details.
                """);
            WriteFile(outputDirectory, Path.Combine("e2e-test-prompts", "advisor-tool.e2e.json"), """
                {
                  "title": "Advisor tool prompts",
                  "totalSections": 2,
                  "sections": [
                    { "name": "first" },
                    { "name": "second" }
                  ]
                }
                """);
            WriteFile(outputDirectory, Path.Combine("trace", "ignored.txt"), "ignore me");

            var capture = new GoldenSnapshotCapture(repoRoot);

            var manifest = capture.CaptureFromOutputDirectory(outputDirectory, "advisor");

            Assert.Equal("advisor", manifest.Namespace);
            Assert.Contains("generation-summary.md", manifest.DeterministicFiles.Keys);
            Assert.Contains("annotations/advisor.md", manifest.DeterministicFiles.Keys);
            Assert.Contains("common-general/overview.md", manifest.DeterministicFiles.Keys);
            Assert.DoesNotContain("trace/ignored.txt", manifest.DeterministicFiles.Keys);
            Assert.Contains("tools/advisor-tool.md", manifest.AiFiles.Keys);
            Assert.Contains("e2e-test-prompts/advisor-tool.e2e.json", manifest.AiFiles.Keys);

            var markdownEntry = manifest.AiFiles["tools/advisor-tool.md"];
            Assert.Equal(2, markdownEntry.SectionCount);
            Assert.Equal(["description", "ms.topic", "title"], markdownEntry.RequiredKeys);

            var jsonEntry = manifest.AiFiles["e2e-test-prompts/advisor-tool.e2e.json"];
            Assert.Equal(2, jsonEntry.SectionCount);
            Assert.Equal(["sections", "title", "totalSections"], jsonEntry.RequiredKeys);
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    private static string CreateScratchDirectory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestScratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteScratchDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }
}
