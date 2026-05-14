// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace TemplateEngine.Tests;

public class CliHelpersTests
{
    [Fact]
    public void TabHeader_RendersCorrectMarkdown()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{tabHeader label id}}}",
            new Dictionary<string, object> { ["label"] = "MCP Server", ["id"] = "mcp-server" });
        Assert.Equal("#### [MCP Server](#tab/mcp-server)", result);
    }

    [Fact]
    public void TabHeader_MissingArgs_ReturnsEmpty()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{tabHeader}}}",
            new Dictionary<string, object>());
        Assert.Equal("", result);
    }

    [Fact]
    public void CliCommand_PrependsAzmcp()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{cliCommand command}}}",
            new Dictionary<string, object> { ["command"] = "storage account list" });
        Assert.Equal("azmcp storage account list", result);
    }

    [Fact]
    public void CliCommand_EmptyCommand_ReturnsAzmcpOnly()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{cliCommand command}}}",
            new Dictionary<string, object> { ["command"] = "" });
        Assert.Equal("azmcp ", result);
    }

    [Fact]
    public void EscapeTableCell_NoPipes_Unchanged()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{escapeTableCell val}}}",
            new Dictionary<string, object> { ["val"] = "hello world" });
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void EscapeTableCell_WithPipes_Escaped()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{escapeTableCell val}}}",
            new Dictionary<string, object> { ["val"] = "json | table | tsv" });
        Assert.Equal(@"json \| table \| tsv", result);
    }

    [Fact]
    public void EscapeTableCell_Empty_ReturnsEmpty()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{escapeTableCell val}}}",
            new Dictionary<string, object> { ["val"] = "" });
        Assert.Equal("", result);
    }

    [Fact]
    public void EscapeTableCell_Missing_ReturnsEmpty()
    {
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{{escapeTableCell missing}}}",
            new Dictionary<string, object>());
        Assert.Equal("", result);
    }
}
