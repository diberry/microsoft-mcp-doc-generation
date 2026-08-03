using System.Text.RegularExpressions;
using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Unit tests for ExamplePromptCommentInjector — verifying HTML comment injection
/// into example prompt include files when required parameters are missing.
/// 
/// Acceptance criteria:
/// - Comment injected when params missing
/// - No comment when params complete
/// - Comment doesn't affect rendered output
/// - Multiple missing params handled correctly
/// </summary>
public class ExamplePromptCommentInjectorTests
{
    private readonly ExamplePromptCommentInjector _injector = new();

    [Fact]
    public void InjectComment_WithMissingParams_AddsWarningCommentAtTop()
    {
        // Arrange
        var examplePromptContent = """
<!-- @mcpcli kv-get -->
Example 1: `az keyvault secret show --name secret1`

Example 2: `az keyvault secret show --name secret2`
""";
        var missingParams = new[] { "account", "vault-name" };

        // Act
        var result = _injector.InjectComment(examplePromptContent, "kv-get", missingParams);

        // Assert
        Assert.StartsWith("<!-- ⚠️ PIPELINE WARNING:", result);
        Assert.Contains("AI-generated example prompts missing required parameter", result);
        Assert.Contains("account", result);
        Assert.Contains("vault-name", result);
        // Original content preserved
        Assert.Contains("Example 1:", result);
        Assert.Contains("@mcpcli kv-get", result);
    }

    [Fact]
    public void InjectComment_WithSingleMissingParam_FormatsCorrectly()
    {
        // Arrange
        var content = "<!-- @mcpcli compute-list -->\nExample: command";
        var missingParams = new[] { "resource-group" };

        // Act
        var result = _injector.InjectComment(content, "compute-list", missingParams);

        // Assert
        Assert.Contains("'resource-group'", result);
        Assert.DoesNotContain(" and ", result); // Single param, no "and"
    }

    [Fact]
    public void InjectComment_WithMultipleMissingParams_FormatsAsCommaList()
    {
        // Arrange
        var content = "<!-- @mcpcli storage-account-show -->\nExample: command";
        var missingParams = new[] { "account-name", "resource-group", "storage-key" };

        // Act
        var result = _injector.InjectComment(content, "storage-account-show", missingParams);

        // Assert
        // Verify comma-separated list in comment
        var commentMatch = Regex.Match(result, @"<!--.*?-->", RegexOptions.Singleline);
        var comment = commentMatch.Value;
        Assert.Contains("'account-name'", comment);
        Assert.Contains("'resource-group'", comment);
        Assert.Contains("'storage-key'", comment);
    }

    [Fact]
    public void InjectComment_WithNothingMissing_ReturnsUnchanged()
    {
        // Arrange
        var content = "<!-- @mcpcli database-create -->\nExample: az sql db create";
        var emptyMissingParams = Array.Empty<string>();

        // Act
        var result = _injector.InjectComment(content, "database-create", emptyMissingParams);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void InjectComment_CommentInvisibleInRenderedOutput()
    {
        // Arrange: Simulate markdown rendering (strip HTML comments)
        var content = "<!-- @mcpcli vm-create -->\nExample: `az vm create --name myvm`";
        var missingParams = new[] { "resource-group" };

        // Act
        var injected = _injector.InjectComment(content, "vm-create", missingParams);
        
        // Simulate markdown renderer stripping HTML comments
        var renderedMarkdown = Regex.Replace(injected, @"<!--.*?-->", "", RegexOptions.Singleline).Trim();

        // Assert: Rendered output unchanged from original non-comment content
        Assert.Equal("Example: `az vm create --name myvm`", renderedMarkdown);
    }

    [Fact]
    public void InjectComment_PreservesExistingComments()
    {
        // Arrange
        var content = """
<!-- @mcpcli storage-blob-upload -->
<!-- This is an existing engineering comment -->
Example: `az storage blob upload --account-name myaccount --container-name mycontainer`
""";
        var missingParams = new[] { "account-name" };

        // Act
        var result = _injector.InjectComment(content, "storage-blob-upload", missingParams);

        // Assert
        Assert.Contains("<!-- This is an existing engineering comment -->", result);
        Assert.Contains("⚠️ PIPELINE WARNING:", result);
        // Should preserve the @mcpcli marker, existing comment, and add new warning comment
        var commentCount = Regex.Matches(result, @"<!--").Count;
        Assert.Equal(3, commentCount); // @mcpcli, existing comment, new warning comment
    }

    [Fact]
    public void InjectComment_IdempotentWhenCalledTwice()
    {
        // Arrange
        var content = "<!-- @mcpcli network-vnet-create -->\nExample: command";
        var missingParams = new[] { "resource-group" };

        // Act
        var result1 = _injector.InjectComment(content, "network-vnet-create", missingParams);
        var result2 = _injector.InjectComment(result1, "network-vnet-create", missingParams);

        // Assert: Should not duplicate the comment
        Assert.Equal(result1, result2);
        var warningCount = Regex.Matches(result2, @"⚠️ PIPELINE WARNING:").Count;
        Assert.Equal(1, warningCount);
    }

    [Fact]
    public void InjectCommentToFile_CreatesBackupAndModifiesFile()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        try
        {
            var filePath = Path.Combine(testDir, "example-prompt.md");
            var originalContent = "<!-- @mcpcli kv-get -->\nExample: command";
            File.WriteAllText(filePath, originalContent);

            var missingParams = new[] { "vault-name" };

            // Act
            _injector.InjectCommentToFile(filePath, "kv-get", missingParams);

            // Assert: File modified with comment
            var modifiedContent = File.ReadAllText(filePath);
            Assert.Contains("⚠️ PIPELINE WARNING:", modifiedContent);
            Assert.Contains("'vault-name'", modifiedContent);
            
            // Original content still present
            Assert.Contains("Example: command", modifiedContent);
            
            // Backup created
            var backupPath = filePath + ".bak";
            Assert.True(File.Exists(backupPath));
            Assert.Equal(originalContent, File.ReadAllText(backupPath));
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public void InjectCommentToFile_WithNoMissingParams_DoesNotModifyFile()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        try
        {
            var filePath = Path.Combine(testDir, "example-prompt.md");
            var originalContent = "<!-- @mcpcli database-list -->\nExample: command";
            File.WriteAllText(filePath, originalContent);

            // Act
            _injector.InjectCommentToFile(filePath, "database-list", Array.Empty<string>());

            // Assert: File unchanged
            var modifiedContent = File.ReadAllText(filePath);
            Assert.Equal(originalContent, modifiedContent);
            
            // No backup created
            var backupPath = filePath + ".bak";
            Assert.False(File.Exists(backupPath));
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }
}
