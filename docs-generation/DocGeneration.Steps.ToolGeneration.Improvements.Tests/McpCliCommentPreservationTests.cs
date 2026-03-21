// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolGeneration_Improved.Services;

namespace DocGeneration.Steps.ToolGeneration.Improvements.Tests;

public class McpCliCommentPreservationTests
{
    [Fact]
    public void ExtractMcpCliComment_FindsComment()
    {
        var content = "---\nfrontmatter\n---\n# create\n\n<!-- @mcpcli fileshares fileshare create -->\n\nDescription.";
        var comment = ImprovedToolGeneratorService.ExtractMcpCliComment(content);
        Assert.Equal("<!-- @mcpcli fileshares fileshare create -->", comment);
    }

    [Fact]
    public void ExtractMcpCliComment_NoComment_ReturnsNull()
    {
        var content = "---\nfrontmatter\n---\n# create\n\nDescription with no comment.";
        var comment = ImprovedToolGeneratorService.ExtractMcpCliComment(content);
        Assert.Null(comment);
    }

    [Fact]
    public void ExtractMcpCliComment_MultiWordCommand()
    {
        var content = "# get\n\n<!-- @mcpcli fileshares fileshare snapshot create -->\n\nDesc.";
        var comment = ImprovedToolGeneratorService.ExtractMcpCliComment(content);
        Assert.Equal("<!-- @mcpcli fileshares fileshare snapshot create -->", comment);
    }

    [Fact]
    public void RestoreMcpCliComment_AlreadyPresent_NoChange()
    {
        var content = "# create\n\n<!-- @mcpcli fileshares fileshare create -->\n\nDescription.";
        var result = ImprovedToolGeneratorService.RestoreMcpCliComment(content, "<!-- @mcpcli fileshares fileshare create -->");
        Assert.Equal(content, result);
    }

    [Fact]
    public void RestoreMcpCliComment_Stripped_RestoresAfterH1()
    {
        var content = "# create\n\nDescription without comment.";
        var result = ImprovedToolGeneratorService.RestoreMcpCliComment(content, "<!-- @mcpcli fileshares fileshare create -->");
        Assert.Contains("<!-- @mcpcli fileshares fileshare create -->", result);
        // Comment should appear after H1
        var h1Index = result.IndexOf("# create");
        var commentIndex = result.IndexOf("<!-- @mcpcli");
        Assert.True(commentIndex > h1Index, "Comment should be after H1 heading");
    }

    [Fact]
    public void RestoreMcpCliComment_NullComment_NoChange()
    {
        var content = "# create\n\nDescription.";
        var result = ImprovedToolGeneratorService.RestoreMcpCliComment(content, null);
        Assert.Equal(content, result);
    }

    [Fact]
    public void RestoreMcpCliComment_EmptyContent_NoChange()
    {
        var result = ImprovedToolGeneratorService.RestoreMcpCliComment("", "<!-- @mcpcli test -->");
        Assert.Equal("", result);
    }

    [Fact]
    public void RoundTrip_ExtractThenRestore_PreservesComment()
    {
        var original = "---\nfrontmatter\n---\n# create\n\n<!-- @mcpcli fileshares fileshare create -->\n\nDescription text.";
        var comment = ImprovedToolGeneratorService.ExtractMcpCliComment(original);
        
        // Simulate AI stripping the comment
        var aiOutput = "---\nfrontmatter\n---\n# create\n\nImproved description text.";
        
        var restored = ImprovedToolGeneratorService.RestoreMcpCliComment(aiOutput, comment);
        Assert.Contains("<!-- @mcpcli fileshares fileshare create -->", restored);
    }
}
