// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class JsonControlCharacterSanitizerTests
{
    [Fact]
    public void StripInvalidControlCharacters_RemovesRawControlCharInsideString()
    {
        // 0x1A (SUB) embedded raw inside a JSON string value.
        var json = "{\"description\":\"before\u001Aafter\"}";

        var sanitized = JsonControlCharacterSanitizer.StripInvalidControlCharacters(json);

        Assert.Equal("{\"description\":\"beforeafter\"}", sanitized);
        Assert.DoesNotContain('\u001A', sanitized);
    }

    [Fact]
    public void StripInvalidControlCharacters_PreservesEscapeSequences()
    {
        // Escaped newline and escaped quote must survive untouched.
        var json = "{\"description\":\"line1\\nline2 \\\"quoted\\\"\"}";

        var sanitized = JsonControlCharacterSanitizer.StripInvalidControlCharacters(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void StripInvalidControlCharacters_PreservesStructuralWhitespace()
    {
        // Newlines/tabs OUTSIDE string literals are structural and must be kept.
        var json = "{\n\t\"key\": \"value\"\n}";

        var sanitized = JsonControlCharacterSanitizer.StripInvalidControlCharacters(json);

        Assert.Equal(json, sanitized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StripInvalidControlCharacters_HandlesNullOrEmpty(string? input)
    {
        var sanitized = JsonControlCharacterSanitizer.StripInvalidControlCharacters(input!);

        Assert.Equal(input, sanitized);
    }

    [Fact]
    public void StripInvalidControlCharacters_RemovesMultipleControlCharsAcrossServices()
    {
        // Varied services, multiple raw control chars in different string values.
        var json =
            "{\"results\":[" +
            "{\"command\":\"keyvault secret get\",\"description\":\"Get\u0000 secret\"}," +
            "{\"command\":\"cosmos database list\",\"description\":\"List\u001F databases\"}" +
            "]}";

        var sanitized = JsonControlCharacterSanitizer.StripInvalidControlCharacters(json);

        Assert.DoesNotContain('\u0000', sanitized);
        Assert.DoesNotContain('\u001F', sanitized);
        Assert.Contains("Get secret", sanitized);
        Assert.Contains("List databases", sanitized);
    }
}
