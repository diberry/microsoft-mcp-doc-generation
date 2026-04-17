using Xunit;
using ExamplePromptGeneratorStandalone.Generators;

namespace ExamplePromptGeneratorStandalone.Tests;

public class CleanAIGeneratedTextTests
{
    // ─────────────────────────────────────────────────
    // Smart quotes → straight quotes
    // ─────────────────────────────────────────────────

    [Fact]
    public void ReplacesLeftSingleSmartQuote()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("it\u2018s a test");
        Assert.Equal("it's a test", result);
    }

    [Fact]
    public void ReplacesRightSingleSmartQuote()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("it\u2019s a test");
        Assert.Equal("it's a test", result);
    }

    [Fact]
    public void ReplacesLeftDoubleSmartQuote()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("Use \u201Cmy-vault\u201D as name");
        Assert.Equal("Use \"my-vault\" as name", result);
    }

    [Fact]
    public void ReplacesRightDoubleSmartQuote()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("called \u201Ctest\u201D");
        Assert.Equal("called \"test\"", result);
    }

    // ─────────────────────────────────────────────────
    // HTML entities → plain text
    // ─────────────────────────────────────────────────

    [Fact]
    public void ReplacesHtmlQuotEntity()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("name &quot;test&quot;");
        Assert.Equal("name \"test\"", result);
    }

    [Fact]
    public void ReplacesHtmlNumericQuotEntity()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("name &#34;test&#34;");
        Assert.Equal("name \"test\"", result);
    }

    [Fact]
    public void ReplacesHtmlAposEntity()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("it&apos;s working");
        Assert.Equal("it's working", result);
    }

    [Fact]
    public void ReplacesHtmlNumericAposEntity()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("it&#39;s working");
        Assert.Equal("it's working", result);
    }

    [Fact]
    public void ReplacesHtmlAmpEntity()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("A &amp; B");
        Assert.Equal("A & B", result);
    }

    [Fact]
    public void ReplacesHtmlLtGtEntities()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("&lt;subscription&gt;");
        Assert.Equal("<subscription>", result);
    }

    // ─────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────

    [Fact]
    public void ReturnsNull_ForNullInput()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ReturnsEmpty_ForEmptyInput()
    {
        var result = ExamplePromptGenerator.CleanAIGeneratedText("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ReturnsUnchanged_WhenNoEntities()
    {
        var input = "List all storage accounts in my subscription";
        var result = ExamplePromptGenerator.CleanAIGeneratedText(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void HandlesMultipleReplacements()
    {
        var input = "List &quot;secrets&quot; in vault &apos;my-vault&apos; &amp; show &lt;details&gt;";
        var result = ExamplePromptGenerator.CleanAIGeneratedText(input);
        Assert.Equal("List \"secrets\" in vault 'my-vault' & show <details>", result);
    }
}
