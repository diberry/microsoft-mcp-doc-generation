using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.PostProcessing;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Generation;

public class AcrolinxPostProcessorTests
{
    private readonly ILogger<AcrolinxPostProcessor> _logger = Substitute.For<ILogger<AcrolinxPostProcessor>>();

    private static readonly string SampleReplacementsJson = """
    [
        { "Parameter": "utilize", "NaturalLanguage": "use" },
        { "Parameter": "leverage", "NaturalLanguage": "use" },
        { "Parameter": "Azure Active Directory", "NaturalLanguage": "Microsoft Entra ID" },
        { "Parameter": "via", "NaturalLanguage": "through" },
        { "Parameter": "e.g.", "NaturalLanguage": "for example" }
    ]
    """;

    private static readonly string SampleAcronymsJson = """
    [
        { "Acronym": "RBAC", "Expansion": "role-based access control" },
        { "Acronym": "MCP", "Expansion": "Model Context Protocol" }
    ]
    """;

    [Fact]
    public void Process_AppliesStaticReplacements()
    {
        var processor = new AcrolinxPostProcessor(SampleReplacementsJson, null, _logger);
        var result = processor.Process("You can utilize this tool.");

        result.Should().Contain("use this tool");
        result.Should().NotContain("utilize");
    }

    [Fact]
    public void Process_AppliesBrandReplacements()
    {
        var processor = new AcrolinxPostProcessor(SampleReplacementsJson, null, _logger);
        var result = processor.Process("Configure Azure Active Directory for access.");

        result.Should().Contain("Microsoft Entra ID");
        result.Should().NotContain("Azure Active Directory");
    }

    [Fact]
    public void Process_ExpandsAcronymsOnFirstUse()
    {
        var processor = new AcrolinxPostProcessor(null, SampleAcronymsJson, _logger);
        var result = processor.Process("Configure RBAC roles for the service.");

        result.Should().Contain("role-based access control (RBAC)");
    }

    [Fact]
    public void Process_AppliesContractions()
    {
        var processor = new AcrolinxPostProcessor(null, null, _logger);
        var result = processor.Process("This tool does not support that feature.");

        result.Should().Contain("doesn't");
        result.Should().NotContain("does not");
    }

    [Fact]
    public void Process_NormalizesUrls()
    {
        var processor = new AcrolinxPostProcessor(null, null, _logger);
        var result = processor.Process("See [docs](https://learn.microsoft.com/en-us/azure/storage) for details.");

        result.Should().Contain("/azure/storage");
        result.Should().NotContain("https://learn.microsoft.com");
    }

    [Fact]
    public void Process_EmptyContent_ReturnsEmpty()
    {
        var processor = new AcrolinxPostProcessor(null, null, _logger);
        var result = processor.Process("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Process_InvalidJson_HandlesGracefully()
    {
        var processor = new AcrolinxPostProcessor("not json", "not json", _logger);
        var result = processor.Process("Some content.");

        // Should still apply contractions and URL normalization
        result.Should().NotBeEmpty();
    }

    // --- Introductory comma tests ---

    [Theory]
    [InlineData("However the service is unavailable.", "However, the service is unavailable.")]
    [InlineData("Therefore you should retry.", "Therefore, you should retry.")]
    [InlineData("Additionally the tool supports querying.", "Additionally, the tool supports querying.")]
    [InlineData("Furthermore this skill provides context.", "Furthermore, this skill provides context.")]
    public void AddIntroductoryCommas_SingleWord_InsertsComma(string input, string expected)
    {
        var result = AcrolinxPostProcessor.AddIntroductoryCommas(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("For example you can create a storage account.", "For example, you can create a storage account.")]
    [InlineData("By default the tool uses managed identity.", "By default, the tool uses managed identity.")]
    [InlineData("In addition the skill supports tagging.", "In addition, the skill supports tagging.")]
    public void AddIntroductoryCommas_Phrase_InsertsComma(string input, string expected)
    {
        var result = AcrolinxPostProcessor.AddIntroductoryCommas(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void AddIntroductoryCommas_AlreadyHasComma_NoChange()
    {
        var input = "However, the service is available.";
        var result = AcrolinxPostProcessor.AddIntroductoryCommas(input);
        result.Should().Be(input);
    }

    [Fact]
    public void AddIntroductoryCommas_MidSentence_NoChange()
    {
        var input = "The service is however unavailable.";
        var result = AcrolinxPostProcessor.AddIntroductoryCommas(input);
        // "however" mid-sentence shouldn't get a comma
        result.Should().Be(input);
    }

    // --- Bare skill name wrapping tests ---

    [Fact]
    public void WrapBareSkillNames_WrapsBareName()
    {
        var input = "The azure-storage skill manages resources.";
        var result = AcrolinxPostProcessor.WrapBareSkillNames(input);
        result.Should().Contain("`azure-storage`");
    }

    [Fact]
    public void WrapBareSkillNames_DoesNotDoubleWrap()
    {
        var input = "Use the `azure-storage` skill.";
        var result = AcrolinxPostProcessor.WrapBareSkillNames(input);
        result.Should().Be("Use the `azure-storage` skill.");
        result.Should().NotContain("``azure-storage``");
    }

    [Fact]
    public void WrapBareSkillNames_WrapsMultiSegmentNames()
    {
        var input = "Both azure-cosmos-db and azure-key-vault are supported.";
        var result = AcrolinxPostProcessor.WrapBareSkillNames(input);
        result.Should().Contain("`azure-cosmos-db`");
        result.Should().Contain("`azure-key-vault`");
    }

    [Fact]
    public void WrapBareSkillNames_IgnoresNonSkillHyphenatedWords()
    {
        var input = "This is a well-known approach.";
        var result = AcrolinxPostProcessor.WrapBareSkillNames(input);
        result.Should().Be(input);
    }

    [Fact]
    public void WrapBareSkillNames_PreservesUrls()
    {
        var input = "**Skill:** `azure-storage` | [Source code](https://github.com/microsoft/azure-skills/tree/main/skills/azure-storage)";
        var result = AcrolinxPostProcessor.WrapBareSkillNames(input);
        // The URL should not have backticks injected
        result.Should().Contain("skills/azure-storage)");
        result.Should().NotContain("skills/`azure-storage`");
    }

    [Fact]
    public void WrapBareSkillNames_SkipsHeadings()
    {
        var input = "# azure-storage\n\nThe azure-storage skill works.";
        var result = AcrolinxPostProcessor.WrapBareSkillNames(input);
        // Heading line should be preserved, body line should be wrapped
        result.Should().StartWith("# azure-storage");
        result.Should().Contain("`azure-storage` skill");
    }

    // --- Long sentence splitting tests ---

    [Fact]
    public void SplitLongSentences_ShortSentence_Unchanged()
    {
        var input = "This skill manages Azure Storage accounts.";
        var result = AcrolinxPostProcessor.SplitLongSentences(input);
        result.Should().Be(input);
    }

    [Fact]
    public void SplitLongSentences_LongSentenceWithConjunction_Splits()
    {
        // Build a sentence > 35 words with "and" after word 20
        var input = "This skill provides comprehensive knowledge about Azure Storage account management including blob containers file shares queue storage table storage networking configuration for virtual network integration private endpoint setup and advanced data protection features for enterprise environments.";
        var result = AcrolinxPostProcessor.SplitLongSentences(input);
        // Should be split at a conjunction — the original had only a trailing period
        var periods = result.Count(c => c == '.');
        periods.Should().BeGreaterThan(1);
    }

    [Fact]
    public void SplitLongSentences_SkipsHeadings()
    {
        var input = "## This is a long heading with many words about Azure Storage accounts and blob containers and file shares and queue storage and table storage and networking";
        var result = AcrolinxPostProcessor.SplitLongSentences(input);
        result.Should().Be(input);
    }

    [Fact]
    public void SplitLongSentences_SkipsListItems()
    {
        var input = "- This is a long list item with many words about Azure Storage accounts and blob containers and file shares and queue storage and table storage and networking";
        var result = AcrolinxPostProcessor.SplitLongSentences(input);
        result.Should().Be(input);
    }

    [Fact]
    public void SplitLongSentences_SkipsTableRows()
    {
        var input = "| This is a long table cell with many words about Azure Storage accounts and blob containers and file shares and queue storage and table storage and networking |";
        var result = AcrolinxPostProcessor.SplitLongSentences(input);
        result.Should().Be(input);
    }

    // --- Integration: Process applies all new features ---

    [Fact]
    public void Process_AppliesIntroductoryCommas()
    {
        var processor = new AcrolinxPostProcessor(null, null, _logger);
        var result = processor.Process("However the tool works well.");
        result.Should().Contain("However, the tool works well.");
    }

    [Fact]
    public void Process_WrapsBarSkillNames()
    {
        var processor = new AcrolinxPostProcessor(null, null, _logger);
        var result = processor.Process("The azure-monitor skill tracks metrics.");
        result.Should().Contain("`azure-monitor`");
    }

    [Fact]
    public void Process_DoesNotWrapSkillNamesInFrontmatter()
    {
        var processor = new AcrolinxPostProcessor(null, null, _logger);
        var input = "---\ntitle: azure-storage skill\nms.service: azure-mcp-server\n---\n\nThe azure-storage skill works.";
        var result = processor.Process(input);
        // Frontmatter should be untouched
        result.Should().Contain("ms.service: azure-mcp-server");
        result.Should().NotContain("ms.service: `azure-mcp-server`");
        // Body should be wrapped
        result.Should().Contain("`azure-storage` skill works");
    }

    [Fact]
    public void SplitFrontmatter_SeparatesCorrectly()
    {
        var input = "---\ntitle: Test\n---\n\nBody content.";
        var (fm, body) = AcrolinxPostProcessor.SplitFrontmatter(input);
        fm.Should().Contain("title: Test");
        body.Should().Contain("Body content.");
    }

    [Fact]
    public void SplitFrontmatter_NoFrontmatter_ReturnsEmptyPrefix()
    {
        var input = "No frontmatter here.";
        var (fm, body) = AcrolinxPostProcessor.SplitFrontmatter(input);
        fm.Should().BeEmpty();
        body.Should().Be(input);
    }

    // --- Goal-before-action rewriting tests ---

    [Theory]
    [InlineData(
        "Run the command to list resources.",
        "To list resources, run the command.")]
    [InlineData(
        "Execute the query to retrieve metrics.",
        "To retrieve metrics, execute the query.")]
    [InlineData(
        "Use the skill to manage storage accounts.",
        "To manage storage accounts, use the skill.")]
    public void RewriteGoalBeforeAction_RewritesRunToPattern(string input, string expected)
    {
        var result = AcrolinxPostProcessor.RewriteGoalBeforeAction(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void RewriteGoalBeforeAction_SkipsHeadings()
    {
        var input = "## Run the command to list resources";
        var result = AcrolinxPostProcessor.RewriteGoalBeforeAction(input);
        result.Should().Be(input);
    }

    [Fact]
    public void RewriteGoalBeforeAction_SkipsListItems()
    {
        var input = "- Run the command to list resources.";
        var result = AcrolinxPostProcessor.RewriteGoalBeforeAction(input);
        result.Should().Be(input);
    }

    [Fact]
    public void RewriteGoalBeforeAction_NoMatch_Unchanged()
    {
        var input = "To list resources, run the command.";
        var result = AcrolinxPostProcessor.RewriteGoalBeforeAction(input);
        result.Should().Be(input);
    }

    [Fact]
    public void RewriteGoalBeforeAction_CaseInsensitive()
    {
        var input = "run the tool to check quotas.";
        var result = AcrolinxPostProcessor.RewriteGoalBeforeAction(input);
        result.Should().Be("To check quotas, run the tool.");
    }

    // --- Colon removal tests ---

    [Theory]
    [InlineData("**Azure CLI:** Install version 2.60+", "**Azure CLI** Install version 2.60+")]
    [InlineData("**GitHub Copilot:** Required for all skills", "**GitHub Copilot** Required for all skills")]
    [InlineData("**Node.js:** Version 18 or later", "**Node.js** Version 18 or later")]
    public void RemoveBoldLabelColons_RemovesColon(string input, string expected)
    {
        var result = AcrolinxPostProcessor.RemoveBoldLabelColons(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void RemoveBoldLabelColons_NoBoldLabel_Unchanged()
    {
        var input = "Install Azure CLI version 2.60+";
        var result = AcrolinxPostProcessor.RemoveBoldLabelColons(input);
        result.Should().Be(input);
    }

    [Fact]
    public void RemoveBoldLabelColons_BoldWithoutColon_Unchanged()
    {
        var input = "**Azure CLI** Install version 2.60+";
        var result = AcrolinxPostProcessor.RemoveBoldLabelColons(input);
        result.Should().Be(input);
    }

    // --- Consecutive duplicate sentence detection tests ---

    [Fact]
    public void RemoveConsecutiveDuplicateSentences_RemovesDuplicates()
    {
        var input = "The skill manages storage accounts. This skill manages Azure storage accounts.";
        var result = AcrolinxPostProcessor.RemoveConsecutiveDuplicateSentences(input);
        result.Split(". ").Length.Should().BeLessThan(input.Split(". ").Length);
    }

    [Fact]
    public void RemoveConsecutiveDuplicateSentences_KeepsDifferentSentences()
    {
        var input = "The skill manages storage accounts. It also provides diagnostic tools.";
        var result = AcrolinxPostProcessor.RemoveConsecutiveDuplicateSentences(input);
        result.Should().Contain("storage accounts");
        result.Should().Contain("diagnostic tools");
    }

    [Fact]
    public void RemoveConsecutiveDuplicateSentences_SkipsHeadings()
    {
        var input = "## Manage storage accounts";
        var result = AcrolinxPostProcessor.RemoveConsecutiveDuplicateSentences(input);
        result.Should().Be(input);
    }

    [Fact]
    public void AreSentencesDuplicate_HighOverlap_ReturnsTrue()
    {
        var a = "The skill manages Azure storage accounts effectively.";
        var b = "This skill manages Azure storage accounts.";
        AcrolinxPostProcessor.AreSentencesDuplicate(a, b).Should().BeTrue();
    }

    [Fact]
    public void AreSentencesDuplicate_LowOverlap_ReturnsFalse()
    {
        var a = "The skill manages Azure storage accounts.";
        var b = "Configure RBAC roles for the subscription.";
        AcrolinxPostProcessor.AreSentencesDuplicate(a, b).Should().BeFalse();
    }

    [Fact]
    public void AreSentencesDuplicate_EmptySentence_ReturnsFalse()
    {
        AcrolinxPostProcessor.AreSentencesDuplicate("", "Some text.").Should().BeFalse();
    }

    // --- Acronym expansion verification ---

    [Fact]
    public void Process_ExpandsNewAcronyms_IDE()
    {
        var acronymsJson = """[{ "Acronym": "IDE", "Expansion": "integrated development environment" }]""";
        var processor = new AcrolinxPostProcessor(null, acronymsJson, _logger);
        var result = processor.Process("Open your IDE to start coding.");
        result.Should().Contain("integrated development environment (IDE)");
    }

    [Fact]
    public void Process_ExpandsNewAcronyms_CLI()
    {
        var acronymsJson = """[{ "Acronym": "CLI", "Expansion": "command-line interface" }]""";
        var processor = new AcrolinxPostProcessor(null, acronymsJson, _logger);
        var result = processor.Process("Install the CLI tool first.");
        result.Should().Contain("command-line interface (CLI)");
    }

    [Fact]
    public void Process_ExpandsNewAcronyms_ARM()
    {
        var acronymsJson = """[{ "Acronym": "ARM", "Expansion": "Azure Resource Manager" }]""";
        var processor = new AcrolinxPostProcessor(null, acronymsJson, _logger);
        var result = processor.Process("Deploy ARM templates to provision resources.");
        result.Should().Contain("Azure Resource Manager (ARM)");
    }
}
