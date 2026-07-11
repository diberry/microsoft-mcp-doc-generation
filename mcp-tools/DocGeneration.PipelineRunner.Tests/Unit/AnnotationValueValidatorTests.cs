using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Behavioral tests for <see cref="AnnotationValueValidator"/> (#695 Part 2).
///
/// The validator compares the annotation-table VALUES (✅/❌) rendered in an
/// assembled tool-family article against the source-of-truth boolean values from
/// the CLI `tools list` metadata (metadata.&lt;field&gt;.value). A mismatch means an
/// AI/post-processing step corrupted an emoji value; it must be caught at generation
/// time as a blocking issue.
///
/// Column order is fixed: Destructive, Idempotent, Open World, Read Only, Secret, Local Required.
/// Tests intentionally draw on varied Azure services (Storage, Key Vault, Cosmos DB, Monitor)
/// to prove the logic is service-agnostic.
/// </summary>
public class AnnotationValueValidatorTests
{
    // bool[] order: Destructive, Idempotent, Open World, Read Only, Secret, Local Required
    private static string Article(string command, string valueRow) =>
        $"""
        ---
        title: T
        ---
        # T

        ## Section
        <!-- @mcpcli {command} -->
        [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

        | Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
        |:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
        {valueRow}
        """;

    [Fact]
    public void GetValueMismatchIssues_AllValuesMatch_ReturnsNoIssues()
    {
        // Storage: article table exactly matches CLI metadata.
        var article = Article("storage account list", "| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |");
        var expected = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = [false, true, false, true, false, false],
        };

        var issues = AnnotationValueValidator.GetValueMismatchIssues(article, expected);

        Assert.Empty(issues);
    }

    [Fact]
    public void GetValueMismatchIssues_SingleFieldMismatch_ReturnsOneIssueNamingField()
    {
        // Key Vault: article shows Read Only ✅ but CLI metadata says ❌.
        var article = Article("keyvault secret create", "| ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |");
        var expected = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyvault secret create"] = [true, false, false, false, false, false],
        };

        var issues = AnnotationValueValidator.GetValueMismatchIssues(article, expected);

        var issue = Assert.Single(issues);
        Assert.Contains("keyvault secret create", issue, StringComparison.Ordinal);
        Assert.Contains("Read Only", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void GetValueMismatchIssues_MultipleFieldMismatch_ReturnsIssuePerField()
    {
        // Cosmos DB: Destructive and Secret both differ.
        var article = Article("cosmos account delete", "| ❌ | ✅ | ❌ | ❌ | ✅ | ❌ |");
        var expected = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["cosmos account delete"] = [true, true, false, false, false, false],
        };

        var issues = AnnotationValueValidator.GetValueMismatchIssues(article, expected);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Contains("Destructive", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Contains("Secret", StringComparison.Ordinal));
    }

    [Fact]
    public void GetValueMismatchIssues_MultipleTools_AssociatesTableWithNearestPrecedingCommand()
    {
        // Monitor: two tool sections; only the SECOND tool's table is wrong.
        // Proves the value row is matched to the @mcpcli command that precedes it.
        var article = """
        ---
        title: Monitor
        ---
        # Monitor

        ## Metric list
        <!-- @mcpcli monitor metric list -->
        [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

        | Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
        |:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
        | ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |

        ## Alert create
        <!-- @mcpcli monitor alert create -->
        [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

        | Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
        |:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
        | ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |
        """;
        var expected = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["monitor metric list"] = [false, true, false, true, false, false],   // matches
            ["monitor alert create"] = [true, false, false, false, false, false], // article contradicts
        };

        var issues = AnnotationValueValidator.GetValueMismatchIssues(article, expected);

        Assert.All(issues, i => Assert.Contains("monitor alert create", i, StringComparison.Ordinal));
        Assert.DoesNotContain(issues, i => i.Contains("monitor metric list", StringComparison.Ordinal));
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void GetValueMismatchIssues_CommandNotInMetadata_SkipsValueCheck()
    {
        // AKS: an annotation table whose command is not in the CLI metadata dict is
        // left to the format/cross-reference validators — no value mismatch emitted.
        var article = Article("aks cluster get", "| ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |");
        var expected = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = [false, true, false, true, false, false],
        };

        var issues = AnnotationValueValidator.GetValueMismatchIssues(article, expected);

        Assert.Empty(issues);
    }

    [Fact]
    public void GetValueMismatchIssues_NoAnnotationTable_ReturnsNoIssues()
    {
        var article = """
        ---
        title: T
        ---
        # T

        ## Section
        <!-- @mcpcli sql db list -->
        Some prose, no annotation table.
        """;
        var expected = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["sql db list"] = [false, true, false, true, false, false],
        };

        var issues = AnnotationValueValidator.GetValueMismatchIssues(article, expected);

        Assert.Empty(issues);
    }

    [Fact]
    public void GetValueMismatchIssues_CrlfArticle_StillDetectsMismatch()
    {
        // Speech: CRLF line endings must not defeat detection.
        var article = Article("speech stt recognize", "| ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |")
            .Replace("\n", "\r\n", StringComparison.Ordinal);
        var expected = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["speech stt recognize"] = [false, false, false, false, false, false],
        };

        var issues = AnnotationValueValidator.GetValueMismatchIssues(article, expected);

        var issue = Assert.Single(issues);
        Assert.Contains("Destructive", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseValueRow_ValidRow_ReturnsSixBooleans()
    {
        var result = AnnotationValueValidator.ParseValueRow("| ✅ | ❌ | ✅ | ❌ | ✅ | ❌ |");

        Assert.NotNull(result);
        Assert.Equal(new[] { true, false, true, false, true, false }, result);
    }

    [Fact]
    public void ParseValueRow_SeparatorRow_ReturnsNull()
    {
        var result = AnnotationValueValidator.ParseValueRow("|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|");

        Assert.Null(result);
    }

    [Fact]
    public void ParseValueRow_WrongCellCount_ReturnsNull()
    {
        var result = AnnotationValueValidator.ParseValueRow("| ✅ | ❌ | ✅ |");

        Assert.Null(result);
    }
}
