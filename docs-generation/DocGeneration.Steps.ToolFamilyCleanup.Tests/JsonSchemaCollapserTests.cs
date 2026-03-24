// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for JsonSchemaCollapser — deterministic post-processor that collapses
/// inline JSON schema blocks in parameter table cells into concise prose.
/// The Deploy article's 151-line schema (314 &amp;quot; entities) was the primary
/// cause of its 61/100 Acrolinx score.
/// </summary>
public class JsonSchemaCollapserTests
{
    private const string ReplacementText = "JSON object that defines the input structure for this tool.";

    // ── Real-world Deploy schema collapse ───────────────────────────

    [Fact]
    public void Collapse_RealWorldDeploySchema_CollapsedToProseDescription()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Raw mcp tool input** |  Required | {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;workspaceFolder&quot;: {
                        &quot;type&quot;: &quot;string&quot;,
                        &quot;description&quot;: &quot;The full path of the workspace folder.&quot;
                    },
                    &quot;services&quot;: {
                        &quot;type&quot;: &quot;array&quot;,
                        &quot;items&quot;: {
                            &quot;type&quot;: &quot;object&quot;,
                            &quot;properties&quot;: {
                                &quot;name&quot;: {
                                    &quot;type&quot;: &quot;string&quot;
                                }
                            }
                        }
                    }
                },
                &quot;required&quot;: [
                    &quot;workspaceFolder&quot;,
                    &quot;services&quot;
                ]
            }. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Contains(ReplacementText, result);
        Assert.DoesNotContain("&quot;properties&quot;", result);
        Assert.DoesNotContain("&quot;type&quot;: &quot;object&quot;", result);
    }

    // ── Clean parameter table without schema — no change ────────────

    [Fact]
    public void Collapse_TableWithoutSchema_NoChange()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Azd env name** |  Required | The name of the environment created by `azd`. |
            | **Workspace folder** |  Required | The full path of the workspace folder. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Equal(input, result);
    }

    // ── Multiple tools, only one with schema ────────────────────────

    [Fact]
    public void Collapse_MultipleTools_OnlySchemaToolCollapsed()
    {
        var input = """
            ## Tool A

            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Input** |  Required | {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;name&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            }. |

            ## Tool B

            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Name** |  Required | The resource name. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        // Tool A's schema should be collapsed
        Assert.Contains(ReplacementText, result);
        Assert.DoesNotContain("&quot;properties&quot;", result);

        // Tool B's table should be intact
        Assert.Contains("The resource name.", result);
        Assert.Contains("## Tool B", result);
    }

    // ── Schema inside code block — NOT collapsed ────────────────────

    [Fact]
    public void Collapse_SchemaInsideCodeBlock_NotCollapsed()
    {
        var input = """
            Here is an example:

            ```json
            {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;name&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            }
            ```
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Equal(input, result);
    }

    // ── Already-collapsed text — idempotent ─────────────────────────

    [Fact]
    public void Collapse_AlreadyCollapsed_Idempotent()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Raw mcp tool input** |  Required | JSON object that defines the input structure for this tool. |
            """;

        var first = JsonSchemaCollapser.Collapse(input);
        var second = JsonSchemaCollapser.Collapse(first);

        Assert.Equal(first, second);
        Assert.Contains(ReplacementText, second);
    }

    // ── Empty/null input ────────────────────────────────────────────

    [Fact]
    public void Collapse_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", JsonSchemaCollapser.Collapse(""));
    }

    [Fact]
    public void Collapse_NullInput_ReturnsEmpty()
    {
        Assert.Equal("", JsonSchemaCollapser.Collapse(null!));
    }

    // ── Schema with varying indentation ─────────────────────────────

    [Fact]
    public void Collapse_SchemaWithNoIndentation_Collapsed()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Input** |  Required | {
            &quot;type&quot;: &quot;object&quot;,
            &quot;properties&quot;: {
            &quot;name&quot;: {
            &quot;type&quot;: &quot;string&quot;
            }
            }
            }. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Contains(ReplacementText, result);
        Assert.DoesNotContain("&quot;properties&quot;", result);
    }

    [Fact]
    public void Collapse_SchemaWithDeepIndentation_Collapsed()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Input** |  Required | {
                        &quot;type&quot;: &quot;object&quot;,
                        &quot;properties&quot;: {
                            &quot;id&quot;: {
                                &quot;type&quot;: &quot;string&quot;,
                                &quot;description&quot;: &quot;The resource ID.&quot;
                            }
                        }
                    }. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Contains(ReplacementText, result);
        Assert.DoesNotContain("&quot;properties&quot;", result);
    }

    // ── Table cell ending with }. | pattern ─────────────────────────

    [Fact]
    public void Collapse_TableCellEndsWithDotPipe_PreservesTableStructure()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Raw mcp tool input** |  Required | {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;folder&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            }. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        // Table structure preserved — row ends with " |"
        Assert.Contains($"| **Raw mcp tool input** |  Required | {ReplacementText} |", result);
        // Header and separator rows intact
        Assert.Contains("| Parameter |", result);
        Assert.Contains("|----", result);
    }

    // ── Replacement text is grammatically correct prose ──────────────

    [Fact]
    public void Collapse_ReplacementText_IsGrammaticallyCorrectProse()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Input** |  Required | {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;x&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            }. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        // Must be a proper sentence — starts with capital, ends with period
        Assert.Contains("JSON object that defines the input structure for this tool.", result);
        // Must NOT be just empty deletion
        Assert.NotEqual(input.Replace("{", "").Replace("}", ""), result);
    }

    // ── RunTwice — second pass produces identical output ─────────────

    [Fact]
    public void Collapse_RunTwice_ProducesIdenticalOutput()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Raw mcp tool input** |  Required | {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;workspaceFolder&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            }. |
            """;

        var first = JsonSchemaCollapser.Collapse(input);
        var second = JsonSchemaCollapser.Collapse(first);
        Assert.Equal(first, second);
    }

    // ── Schema without trailing dot before pipe ─────────────────────

    [Fact]
    public void Collapse_SchemaEndingWithBraceSpacePipe_Collapsed()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Input** |  Required | {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;name&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            } |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Contains(ReplacementText, result);
        Assert.DoesNotContain("&quot;properties&quot;", result);
    }

    // ── Mixed content: schema in table + schema in code block ────────

    [Fact]
    public void Collapse_SchemaInTableAndCodeBlock_OnlyTableCollapsed()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Input** |  Required | {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;name&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            }. |

            Example:

            ```json
            {
                &quot;type&quot;: &quot;object&quot;,
                &quot;properties&quot;: {
                    &quot;name&quot;: { &quot;type&quot;: &quot;string&quot; }
                }
            }
            ```
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        // Table schema collapsed
        Assert.Contains(ReplacementText, result);
        // Code block schema preserved
        Assert.Contains("```json", result);
        // The code block still has the schema content
        var afterCodeFence = result[(result.IndexOf("```json") + 7)..];
        Assert.Contains("&quot;properties&quot;", afterCodeFence);
    }

    // ── Multiline table cell without JSON schema — not collapsed ────

    [Fact]
    public void Collapse_MultilineTableCellWithoutSchema_NotCollapsed()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Config** |  Required | This is a long description
            that spans multiple lines
            but does not contain JSON schema. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Equal(input, result);
    }

    // ── Table cell with { but no schema markers — not collapsed ─────

    [Fact]
    public void Collapse_TableCellWithBraceButNoSchemaMarkers_NotCollapsed()
    {
        var input = """
            | Parameter |  Required or optional | Description |
            |-----------------------|----------------------|-------------|
            | **Filter** |  Required | A filter expression like {name: value}. |
            """;

        var result = JsonSchemaCollapser.Collapse(input);

        Assert.Equal(input, result);
    }
}
