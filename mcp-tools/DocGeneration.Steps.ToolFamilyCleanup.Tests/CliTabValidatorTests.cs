// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.Steps.ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class CliTabValidatorTests
{
    [Fact]
    public void Validate_ValidTabStructure_ReturnsValid()
    {
        var markdown = """
            ## Tool A

            #### [MCP Server](#tab/mcp-server)

            MCP content here.

            #### [CLI](#tab/cli)

            CLI content here.

            ---

            """;

        var result = CliTabValidator.Validate(markdown);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MismatchedTabCounts_ReturnsError()
    {
        var markdown = """
            #### [MCP Server](#tab/mcp-server)

            MCP content.

            ---
            """;

        var result = CliTabValidator.Validate(markdown);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Mismatched tab counts"));
    }

    [Fact]
    public void Validate_UnterminatedTabGroup_ReturnsError()
    {
        var markdown = """
            #### [MCP Server](#tab/mcp-server)

            MCP content.

            #### [CLI](#tab/cli)

            CLI content.
            """;

        var result = CliTabValidator.Validate(markdown);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Unterminated tab group"));
    }

    [Fact]
    public void Validate_NestedTabGroup_ReturnsError()
    {
        var markdown = """
            #### [MCP Server](#tab/mcp-server)

            MCP content.

            #### [MCP Server](#tab/mcp-server)

            Nested MCP content.

            #### [CLI](#tab/cli)

            CLI content.

            ---
            """;

        var result = CliTabValidator.Validate(markdown);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Nested tab group"));
    }

    [Fact]
    public void Validate_CliTabWithoutMcpTab_ReturnsError()
    {
        var markdown = """
            #### [CLI](#tab/cli)

            CLI content without MCP tab.

            ---
            """;

        var result = CliTabValidator.Validate(markdown);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("CLI tab opened without preceding MCP Server tab"));
    }

    [Fact]
    public void Validate_NoTabs_ReturnsWarning()
    {
        var markdown = """
            ## Tool A

            Some regular content with no tabs.
            """;

        var result = CliTabValidator.Validate(markdown);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, w => w.Contains("No CLI tabs found"));
    }

    [Fact]
    public void Validate_MultipleValidTabGroups_AllValid()
    {
        var markdown = """
            ## Tool A

            #### [MCP Server](#tab/mcp-server)

            MCP A content.

            #### [CLI](#tab/cli)

            CLI A content.

            ---

            ## Tool B

            #### [MCP Server](#tab/mcp-server)

            MCP B content.

            #### [CLI](#tab/cli)

            CLI B content.

            ---
            """;

        var result = CliTabValidator.Validate(markdown);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NoContent_ReturnsValid()
    {
        var result = CliTabValidator.Validate("");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
