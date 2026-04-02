using FluentAssertions;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

public class TriggerTestParserTests
{
    private readonly TriggerTestParser _parser = new();

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_ValidFile_ExtractsShouldTriggerPrompts()
    {
        var content = LoadFixture("sample-triggers.test.ts");
        var result = _parser.Parse(content);

        result.ShouldTrigger.Should().HaveCountGreaterOrEqualTo(5);
        result.ShouldTrigger.Should().Contain("How do I create a storage account?");
    }

    [Fact]
    public void Parse_ValidFile_ExtractsShouldNotTriggerPrompts()
    {
        var content = LoadFixture("sample-triggers.test.ts");
        var result = _parser.Parse(content);

        result.ShouldNotTrigger.Should().HaveCountGreaterOrEqualTo(3);
        result.ShouldNotTrigger.Should().Contain("How do I deploy a web app?");
    }

    [Fact]
    public void Parse_NullInput_ReturnsEmptyLists()
    {
        var result = _parser.Parse(null);

        result.ShouldTrigger.Should().BeEmpty();
        result.ShouldNotTrigger.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyLists()
    {
        var result = _parser.Parse("");

        result.ShouldTrigger.Should().BeEmpty();
        result.ShouldNotTrigger.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoArrays_ReturnsEmptyLists()
    {
        var content = "const something = 'hello';\nexport default {};";
        var result = _parser.Parse(content);

        result.ShouldTrigger.Should().BeEmpty();
        result.ShouldNotTrigger.Should().BeEmpty();
    }

    [Fact]
    public void Parse_DifferentQuoteStyles_ExtractsAll()
    {
        var content = LoadFixture("sample-triggers.test.ts");
        var result = _parser.Parse(content);

        // The fixture has single quotes, double quotes, and backticks
        result.ShouldTrigger.Should().Contain("How do I create a storage account?"); // single
        result.ShouldTrigger.Should().Contain("Upload a file to Azure Blob Storage"); // double
        result.ShouldTrigger.Should().Contain("Show me the storage metrics for my account"); // backtick
    }

    [Fact]
    public void Parse_MixedArrayDeclarations_ExtractsBoth()
    {
        var content = @"
const shouldTriggerPrompts = [
  'prompt one',
  'prompt two',
];

const shouldNotTriggerPrompts = [
  'negative one',
];
";
        var result = _parser.Parse(content);

        result.ShouldTrigger.Should().HaveCount(2);
        result.ShouldNotTrigger.Should().HaveCount(1);
    }
}
