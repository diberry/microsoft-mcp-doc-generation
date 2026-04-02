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
}
