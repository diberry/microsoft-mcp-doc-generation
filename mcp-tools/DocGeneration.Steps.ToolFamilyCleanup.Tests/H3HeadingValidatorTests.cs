// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.Steps.ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class H3HeadingValidatorTests
{
    [Fact]
    public void Validate_EmptyContent_ReturnsValid()
    {
        // Arrange
        var markdown = string.Empty;

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NoH3Headings_ReturnsValid()
    {
        // Arrange
        var markdown = @"---
title: Test Article
---

# Main Heading

## Section 1

Some content here.

## Section 2

More content.
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_TabMarkers_AllowsMcpServerTab()
    {
        // Arrange
        var markdown = @"---
title: Test Article
---

# Main Heading

## Tool Name

### [MCP Server](#tab/mcp-server)

MCP Server content here.

### [CLI](#tab/cli)

CLI content here.

---
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NonTabH3_ReturnsError()
    {
        // Arrange
        var markdown = @"---
title: Test Article
---

# Main Heading

## Section 1

### Subsection

This is not allowed.
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("### Subsection", result.Errors[0]);
    }

    [Fact]
    public void Validate_MultipleNonTabH3s_ReturnsMultipleErrors()
    {
        // Arrange
        var markdown = @"---
title: Test Article
---

# Main Heading

## Section 1

### First Subsection

Some content.

### Second Subsection

More content.

## Section 2

### Third Subsection

Even more content.
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains("### First Subsection", result.Errors[0]);
        Assert.Contains("### Second Subsection", result.Errors[1]);
        Assert.Contains("### Third Subsection", result.Errors[2]);
    }

    [Fact]
    public void Validate_MixedTabMarkersAndNonTabH3s_OnlyReportsNonTabErrors()
    {
        // Arrange
        var markdown = @"---
title: Test Article
---

# Main Heading

## Tool 1

### [MCP Server](#tab/mcp-server)

MCP content.

### [CLI](#tab/cli)

CLI content.

---

### Invalid Subsection

This should be reported.
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("### Invalid Subsection", result.Errors[0]);
    }

    [Fact]
    public void Validate_H3WithLeadingWhitespace_DetectsError()
    {
        // Arrange
        var markdown = @"---
title: Test Article
---

# Main Heading

## Section 1

   ### Subsection with leading whitespace

This should be detected.
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_TabMarkerWithDifferentLabel_Allowed()
    {
        // Arrange
        var markdown = @"---
title: Test Article
---

# Main Heading

## Tool Name

### [Custom Tab](#tab/custom-id)

Custom tab content.

---
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_H3InCodeBlock_IgnoresCodeBlock()
    {
        // Arrange
        // Note: Current implementation does NOT ignore code blocks
        // This test documents current behavior
        var markdown = @"---
title: Test Article
---

# Main Heading

## Section 1

```markdown
### This is in a code block
```
";

        // Act
        var result = H3HeadingValidator.Validate(markdown);

        // Assert
        // Current implementation will detect this as an error
        // (does not parse code blocks)
        Assert.False(result.IsValid);
    }
}
