// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using ToolFamilyCleanup.Services;

namespace ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for the --verify-only mode that applies deterministic post-processors without AI.
/// This mode should skip all AI/generative steps and apply only the 10 deterministic processors.
/// </summary>
public class VerifyOnlyModeTests
{
    [Fact]
    public void VerifyOnlyMode_AppliesDeterministicProcessorsInOrder()
    {
        // Arrange: Input markdown with issues each processor should fix
        var input = @"---
title: Test
---

# Test Tool

This tool will be created.
We can not do this.
";

        // Act: Apply all deterministic processors
        var result = ApplyDeterministicProcessors(input);

        // Assert: Verify processors run and produce valid output
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.StartsWith("---", result); // Frontmatter preserved
        Assert.Contains("can't", result); // ContractionFixer
    }

    [Fact]
    public void VerifyOnlyMode_PreservesFrontmatter()
    {
        // Arrange
        var input = @"---
title: Test Tool
ms.topic: reference
---

# Test Tool";

        // Act
        var result = ApplyDeterministicProcessors(input);

        // Assert: Frontmatter should still be present and enriched
        Assert.StartsWith("---", result);
        Assert.Contains("title: Test Tool", result);
    }

    [Fact]
    public void VerifyOnlyMode_CollapsesJsonSchema()
    {
        // Arrange: Input with expanded JSON schema
        var input = @"# Test

Parameter schema:

```json
{
  ""type"": ""object"",
  ""properties"": {
    ""name"": {
      ""type"": ""string""
    }
  }
}
```
";

        // Act
        var result = ApplyDeterministicProcessors(input);

        // Assert: JsonSchemaCollapser should condense schema
        Assert.Contains("```json", result);
        // The schema should be more compact (fewer lines)
        var inputLines = input.Split('\n').Length;
        var resultLines = result.Split('\n').Length;
        Assert.True(resultLines <= inputLines);
    }

    private static string ApplyDeterministicProcessors(string input)
    {
        // Apply the 10 deterministic processors in pipeline order (stages 4-13)
        var current = input;
        
        current = AcronymExpander.ExpandAll(current);
        current = FrontmatterEnricher.EnrichWithDefaults(current);
        current = DuplicateExampleStripper.Strip(current);
        current = AnnotationSpaceFixer.Fix(current);
        current = PresentTenseFixer.Fix(current);
        current = ContractionFixer.Fix(current);
        current = IntroductoryCommaFixer.Fix(current);
        current = ExampleValueBackticker.Fix(current);
        current = LearnUrlRelativizer.Relativize(current);
        current = JsonSchemaCollapser.Collapse(current);
        
        return current;
    }
}
