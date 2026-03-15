// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Xunit;

namespace ToolFamilyValidator.Tests;

public class ToolFamilyValidatorTests
{
    private const string NamespaceName = "sample";

    [Fact]
    public async Task ValidArticle_WithMatchingTools_ExitsZero()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.ReportContent);
        Assert.Contains("RESULT: PASS (clean)", result.CombinedOutput);
        Assert.Contains("✅ Tool count integrity: PASS", result.ReportContent);
    }

    [Fact]
    public async Task MissingToolSection_ExitsNonZero()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        var articlePath = GetArticlePath(tempDir);
        await RemoveSectionAsync(articlePath, "Gamma list");

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing from article: 1", result.CombinedOutput);
        Assert.Contains("sample-gamma-list.md", result.CombinedOutput);
        Assert.Contains("RESULT: FAIL", result.CombinedOutput);
    }

    [Fact]
    public async Task ExtraArticleSection_ExitsNonZero()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        var articlePath = GetArticlePath(tempDir);
        await ReplaceInFileAsync(articlePath, "tool_count: 3", "tool_count: 4");
        await AppendSectionBeforeRelatedContentAsync(articlePath, DeltaInspectSection);

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Missing from files: 1", result.CombinedOutput);
        Assert.Contains("Delta inspect", result.CombinedOutput);
        Assert.Contains("RESULT: FAIL", result.CombinedOutput);
    }

    [Fact]
    public async Task WrongToolCount_ExitsNonZero()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        await ReplaceInFileAsync(GetArticlePath(tempDir), "tool_count: 3", "tool_count: 9");

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("❌ Tool count integrity: FAIL", result.CombinedOutput);
        Assert.Contains("Frontmatter tool_count: 9", result.CombinedOutput);
    }

    [Fact]
    public async Task DuplicateToolKeys_ExitsNonZero()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        File.Copy(
            GetToolPath(tempDir, "sample-alpha-get.md"),
            GetToolPath(tempDir, "sample-alpha-get-copy.md"),
            overwrite: true);

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Duplicate tool file mapping for 'alpha-get'", result.CombinedOutput);
    }

    [Fact]
    public async Task MissingYamlFrontmatter_ExitsNonZero()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        await UpdateArticleAsync(GetArticlePath(tempDir), content =>
            Regex.Replace(content, "(?s)^---\n.*?\n---\n", string.Empty));

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Tool family validation failed: Tool-family article is missing YAML frontmatter.", result.CombinedOutput);
    }

    [Fact]
    public async Task MissingRequiredParamInPrompt_ExitsZeroWithWarning()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        await ReplaceInFileAsync(
            GetArticlePath(tempDir),
            "- Get the alpha resource named 'alpha-one' in resource group 'rg-app'.",
            "- Get the alpha resource in resource group 'rg-app'.");

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Required params in prompts:", result.CombinedOutput);
        Assert.Contains("⚠️ alpha-get: missing 'Resource Name' in example prompt", result.CombinedOutput);
    }

    [Fact]
    public async Task NonStandardExampleHeader_ExitsZeroWithWarning()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        await ReplaceSectionHeaderAsync(GetArticlePath(tempDir), "Gamma list", "Usage examples:");

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Example headers:", result.CombinedOutput);
        Assert.Contains("⚠️ gamma-list: example prompt header is Usage examples:", result.CombinedOutput);
    }

    [Fact]
    public async Task WrongMarkerCount_ExitsZeroWithWarning()
    {
        using var tempDir = TestHelpers.CreateTempDir();
        PrepareValidFixture(tempDir);

        await AddExtraMarkerAsync(GetArticlePath(tempDir), "sample gamma list");

        var result = await TestHelpers.RunValidatorAsync(NamespaceName, tempDir.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Annotation markers: 7 found (expected 6) ⚠️", result.CombinedOutput);
        Assert.Contains("⚠️ gamma-list: expected 2 annotation markers, found 3", result.CombinedOutput);
    }

    private static void PrepareValidFixture(TempDir tempDir) =>
        TestHelpers.CopyFixtureToTemp("ValidSetup", tempDir.Path);

    private static string GetArticlePath(TempDir tempDir) =>
        Path.Combine(tempDir.Path, "tool-family", $"{NamespaceName}.md");

    private static string GetToolPath(TempDir tempDir, string fileName) =>
        Path.Combine(tempDir.Path, "tools", fileName);

    private static async Task ReplaceInFileAsync(string filePath, string oldValue, string newValue)
    {
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains(oldValue, content);
        await File.WriteAllTextAsync(filePath, content.Replace(oldValue, newValue));
    }

    private static async Task RemoveSectionAsync(string articlePath, string heading)
    {
        await UpdateArticleAsync(articlePath, content =>
        {
            var pattern = $@"(?ms)^## {Regex.Escape(heading)}\n.*?(?=^## |\z)";
            var updated = Regex.Replace(content, pattern, string.Empty);
            return updated.TrimEnd() + "\n";
        });
    }

    private static async Task ReplaceSectionHeaderAsync(string articlePath, string heading, string newHeader)
    {
        await UpdateArticleAsync(articlePath, content =>
        {
            var pattern = $@"(^## {Regex.Escape(heading)}\n.*?)(Example prompts include:)";
            var updated = new Regex(pattern, RegexOptions.Multiline | RegexOptions.Singleline)
                .Replace(content, match => $"{match.Groups[1].Value}{newHeader}", 1);
            Assert.NotEqual(content, updated);
            return updated;
        });
    }

    private static async Task AppendSectionBeforeRelatedContentAsync(string articlePath, string sectionText)
    {
        await UpdateArticleAsync(articlePath, content =>
        {
            const string anchor = "\n## Related content\n";
            Assert.Contains(anchor, content);
            return content.Replace(anchor, $"\n{sectionText.Trim()}\n\n## Related content\n");
        });
    }

    private static async Task AddExtraMarkerAsync(string articlePath, string commandText)
    {
        await UpdateArticleAsync(articlePath, content =>
        {
            var marker = $"<!-- @mcpcli {commandText} -->";
            var updated = new Regex(Regex.Escape(marker)).Replace(content, $"{marker}\n{marker}", 1);
            Assert.NotEqual(content, updated);
            return updated;
        });
    }

    private static async Task UpdateArticleAsync(string articlePath, Func<string, string> update)
    {
        var normalized = (await File.ReadAllTextAsync(articlePath)).Replace("\r\n", "\n");
        var updated = update(normalized);
        await File.WriteAllTextAsync(articlePath, updated.Replace("\n", Environment.NewLine));
    }

    private const string DeltaInspectSection = """
## Delta inspect
<!-- @mcpcli sample delta inspect -->
<!-- @mcpcli sample delta inspect -->
Example prompts include:
- Inspect delta item 'delta-01'.
| Parameter | Required | Description |
| --- | --- | --- |
| `Inspect Mode` | No | Optional inspection mode. |
""";
}
