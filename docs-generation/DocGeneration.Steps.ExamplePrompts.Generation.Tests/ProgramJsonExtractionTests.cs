using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public class ProgramJsonExtractionTests
{
    // ─────────────────────────────────────────────────
    // Strategy 1: ```json code block
    // ─────────────────────────────────────────────────

    [Fact]
    public void ExtractsJson_FromJsonCodeBlock()
    {
        var response = """
            Some text.
            ```json
            {"tool": ["prompt"]}
            ```
            More text.
            """;

        var result = Program.ExtractJsonFromResponse(response);

        Assert.Equal("""{"tool": ["prompt"]}""", result);
    }

    // ─────────────────────────────────────────────────
    // Strategy 2: Last code block
    // ─────────────────────────────────────────────────

    [Fact]
    public void ExtractsJson_FromLastCodeBlock()
    {
        var response = """
            ```
            some other code
            ```
            Text between blocks.
            ```
            {"aks cluster list": ["List clusters"]}
            ```
            """;

        var result = Program.ExtractJsonFromResponse(response);

        Assert.Contains("aks cluster list", result);
    }

    // ─────────────────────────────────────────────────
    // Strategy 3: Brace matching
    // ─────────────────────────────────────────────────

    [Fact]
    public void ExtractsJson_UsingBraceMatching()
    {
        var response = """
            Preamble text here.
            {"speech recognizer list": ["List recognizers"]}
            """;

        var result = Program.ExtractJsonFromResponse(response);

        Assert.Contains("speech recognizer list", result);
        Assert.StartsWith("{", result);
        Assert.EndsWith("}", result);
    }

    // ─────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────

    [Fact]
    public void ReturnsEmpty_ForNullInput()
    {
        var result = Program.ExtractJsonFromResponse(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReturnsEmpty_ForEmptyInput()
    {
        var result = Program.ExtractJsonFromResponse("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReturnsPureJson_WhenInputIsPureJson()
    {
        var json = """{"tool": ["prompt 1", "prompt 2"]}""";
        var result = Program.ExtractJsonFromResponse(json);
        Assert.Equal(json, result);
    }

    [Fact]
    public void ReturnsInputAsIs_WhenNoJsonFound()
    {
        var plainText = "No JSON here at all";
        var result = Program.ExtractJsonFromResponse(plainText);
        Assert.Equal(plainText, result);
    }
}
