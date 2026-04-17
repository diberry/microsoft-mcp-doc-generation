// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using DocGeneration.TestInfrastructure;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Tests for ProjectRootFinder — Phase 1.5 path hardening.
/// Validates .git sentinel (primary) and mcp-doc-generation.sln fallback.
/// These tests run from within the repo, so real sentinels are available.
/// </summary>
public class ProjectRootFinderTests
{
    [Fact]
    public void FindSolutionRoot_ReturnsNonNullDirectory()
    {
        var root = ProjectRootFinder.FindSolutionRoot();
        Assert.NotNull(root);
        Assert.True(Directory.Exists(root), $"Returned root does not exist: {root}");
    }

    [Fact]
    public void FindSolutionRoot_ContainsGitSentinel()
    {
        // Primary sentinel: .git directory or file (worktree)
        var root = ProjectRootFinder.FindSolutionRoot();
        var gitPath = Path.Combine(root, ".git");
        Assert.True(
            Directory.Exists(gitPath) || File.Exists(gitPath),
            $"Expected .git sentinel at {gitPath}");
    }

    [Fact]
    public void FindSolutionRoot_ContainsSolutionFile()
    {
        // Fallback sentinel should also be present in this repo
        var root = ProjectRootFinder.FindSolutionRoot();
        var slnPath = Path.Combine(root, "mcp-doc-generation.sln");
        Assert.True(File.Exists(slnPath), $"Expected solution file at {slnPath}");
    }

    [Fact]
    public void FindSolutionRoot_IsConsistentAcrossMultipleCalls()
    {
        var root1 = ProjectRootFinder.FindSolutionRoot();
        var root2 = ProjectRootFinder.FindSolutionRoot();
        Assert.Equal(root1, root2);
    }

    [Fact]
    public void FindMcpToolsRoot_ReturnsMcpToolsSubdirectory()
    {
        var mcpRoot = ProjectRootFinder.FindMcpToolsRoot();
        Assert.NotNull(mcpRoot);
        Assert.EndsWith("mcp-tools", mcpRoot.Replace('\\', '/').TrimEnd('/'));
    }

    [Fact]
    public void FindMcpToolsRoot_IsChildOfSolutionRoot()
    {
        var solutionRoot = ProjectRootFinder.FindSolutionRoot();
        var mcpToolsRoot = ProjectRootFinder.FindMcpToolsRoot();
        var expected = Path.Combine(solutionRoot, "mcp-tools");
        Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(mcpToolsRoot));
    }

    [Fact]
    public void FindSolutionRoot_DoesNotReturnBinOrObjDirectory()
    {
        var root = ProjectRootFinder.FindSolutionRoot();
        var dirName = Path.GetFileName(root);
        Assert.NotEqual("bin", dirName);
        Assert.NotEqual("obj", dirName);
        Assert.NotEqual("Debug", dirName);
        Assert.NotEqual("Release", dirName);
    }
}
