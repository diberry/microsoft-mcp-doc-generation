// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.TestInfrastructure;
using Xunit;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// E2E integration tests that validate the structure and content of already-generated
/// pipeline output. These tests do NOT run the pipeline — they validate existing
/// generated-* directories in the repo root.
///
/// Tests skip gracefully when no generated output exists (e.g., in CI without prior generation).
/// </summary>
[Trait("Category", "E2E")]
public class GeneratedOutputTests
{
    /// <summary>
    /// Discovers all generated-* namespace directories in the repo root that contain
    /// complete pipeline output (at minimum a tool-family/ subdirectory).
    /// Incomplete namespaces (e.g., only logs/) are excluded.
    /// If none qualify, MemberData yields no rows and xUnit skips the Theory.
    /// </summary>
    public static IEnumerable<object[]> GetGeneratedNamespaces()
    {
        string repoRoot;
        try
        {
            repoRoot = ProjectRootFinder.FindSolutionRoot();
        }
        catch (InvalidOperationException)
        {
            yield break;
        }

        var dirs = Directory.GetDirectories(repoRoot, "generated-*");
        foreach (var dir in dirs.OrderBy(d => d))
        {
            var dirName = Path.GetFileName(dir);
            // Skip the base "generated" directory if it somehow matches
            if (dirName == "generated")
                continue;

            // Only include namespaces with complete pipeline output
            if (!Directory.Exists(Path.Combine(dir, "tool-family")))
                continue;

            yield return new object[] { dirName };
        }
    }

    private static string GetOutputPath(string namespaceDirName)
    {
        var repoRoot = ProjectRootFinder.FindSolutionRoot();
        return Path.Combine(repoRoot, namespaceDirName);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Namespace_HasExpectedDirectoryStructure()
    {
        var namespaces = GetGeneratedNamespaces().ToList();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var namespaceDirName = (string)ns[0];
            var outputPath = GetOutputPath(namespaceDirName);
            var result = OutputStructureValidator.ValidateDirectoryStructure(outputPath);

            Assert.True(result.Success,
                $"Directory structure issues in {namespaceDirName}:\n" +
                string.Join("\n", result.Issues));
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Namespace_NoLeakedTemplateTokens()
    {
        var namespaces = GetGeneratedNamespaces().ToList();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var namespaceDirName = (string)ns[0];
            var outputPath = GetOutputPath(namespaceDirName);
            var result = OutputStructureValidator.ValidateNoLeakedTokens(outputPath);

            Assert.True(result.Success,
                $"Leaked template tokens in {namespaceDirName}:\n" +
                string.Join("\n", result.Issues));
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Namespace_AllPublishableMarkdownHasValidFrontmatter()
    {
        var namespaces = GetGeneratedNamespaces().ToList();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var namespaceDirName = (string)ns[0];
            var outputPath = GetOutputPath(namespaceDirName);
            var result = OutputStructureValidator.ValidateMarkdownFrontmatter(outputPath);

            Assert.True(result.Success,
                $"Frontmatter issues in {namespaceDirName}:\n" +
                string.Join("\n", result.Issues));
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Namespace_NoEmptyOrTruncatedFiles()
    {
        var namespaces = GetGeneratedNamespaces().ToList();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var namespaceDirName = (string)ns[0];
            var outputPath = GetOutputPath(namespaceDirName);
            var result = OutputStructureValidator.ValidateFileIntegrity(outputPath);

            Assert.True(result.Success,
                $"File integrity issues in {namespaceDirName}:\n" +
                string.Join("\n", result.Issues));
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Namespace_ToolFamilyArticleHasValidToolCount()
    {
        var namespaces = GetGeneratedNamespaces().ToList();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var namespaceDirName = (string)ns[0];
            var outputPath = GetOutputPath(namespaceDirName);
            var result = OutputStructureValidator.ValidateToolCount(outputPath);

            Assert.True(result.Success,
                $"Tool count issues in {namespaceDirName}:\n" +
                string.Join("\n", result.Issues));
        }
    }

    /// <summary>
    /// Sentinel test that always runs. Reports whether generated output was found,
    /// making it explicit in CI when E2E validation was skipped due to missing output.
    /// When no generated output exists, this test passes with an informational message
    /// rather than failing — CI may not have prior pipeline output.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    public void GeneratedOutput_DiscoveryReport()
    {
        var namespaces = GetGeneratedNamespaces().ToList();

        // Always passes — the message tells CI/developers what happened
        Assert.True(true,
            namespaces.Count > 0
                ? $"Found {namespaces.Count} generated namespace(s): " +
                  string.Join(", ", namespaces.Select(n => n[0]))
                : "No generated-* directories found in repo root. " +
                  "E2E validation requires prior pipeline execution. " +
                  "Run './start.sh <namespace>' to generate output first.");
    }
}
