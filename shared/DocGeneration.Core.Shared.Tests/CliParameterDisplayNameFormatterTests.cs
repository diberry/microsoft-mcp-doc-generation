// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class CliParameterDisplayNameFormatterTests
{
    [Theory]
    [InlineData("--resource-group", "resource-group")]
    [InlineData("-v", "v")]
    [InlineData("resource-group", "resource-group")]
    [InlineData("---resource-group", "-resource-group")]
    public void StripCliPrefix_StripsOneLeadingCliPrefix(string input, string expected)
    {
        Assert.Equal(expected, CliParameterDisplayNameFormatter.StripCliPrefix(input));
    }

    [Fact]
    public void StripCliPrefix_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CliParameterDisplayNameFormatter.StripCliPrefix(null));
        Assert.Equal(string.Empty, CliParameterDisplayNameFormatter.StripCliPrefix(string.Empty));
    }
}
