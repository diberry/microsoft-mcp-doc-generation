using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Generation;

public class AzureOpenAiRewriterTests
{
    // The rewriter goes through Microsoft.Extensions.AI's IChatClient abstraction
    // (NOT the raw Azure.AI.OpenAI ChatClient). This keeps it unit-testable AND
    // avoids the Azure.AI.OpenAI/OpenAI binary-mismatch MissingMethodException.
    // Auth is keyless by policy — see CreateKeyless. No API keys anywhere.

    private static IChatClient StubChatClient(string responseText)
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))));
        return chatClient;
    }

    private static AzureOpenAiRewriter NewRewriter(IChatClient chatClient, string? acrolinxRules = null) =>
        new(
            chatClient,
            "gpt-5-mini",
            "System prompt. {{ACROLINX_RULES}}",
            "Write for {{skillName}}: {{description}}",
            acrolinxRules,
            Substitute.For<ILogger<AzureOpenAiRewriter>>());

    [Fact]
    public void Constructor_WithInjectedChatClient_DoesNotThrow()
    {
        var act = () => NewRewriter(StubChatClient("ok"), "Use contractions. Use active voice.");
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullChatClient_Throws()
    {
        var act = () => new AzureOpenAiRewriter(
            null!,
            "gpt-5-mini",
            "system",
            "user",
            null,
            Substitute.For<ILogger<AzureOpenAiRewriter>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RewriteIntroAsync_ReturnsChatClientContent()
    {
        var rewriter = NewRewriter(StubChatClient("A clear customer-facing intro."));

        var result = await rewriter.RewriteIntroAsync("python-appservice-deploy", "Deploy Python apps.");

        result.Should().Be("A clear customer-facing intro.");
    }

    [Fact]
    public async Task GenerateKnowledgeOverviewAsync_ReturnsChatClientContent()
    {
        var rewriter = NewRewriter(StubChatClient("Knowledge overview text."));

        var result = await rewriter.GenerateKnowledgeOverviewAsync("python-appservice-deploy", "body");

        result.Should().Be("Knowledge overview text.");
    }

    [Fact]
    public async Task SynthesizeWhatItProvidesAsync_ReturnsChatClientContent()
    {
        var rewriter = NewRewriter(StubChatClient("It provides streamlined deployment."));
        var skill = new SkillData
        {
            Name = "python-appservice-deploy",
            DisplayName = "Python App Service Deploy",
            Description = "Deploy Python apps to Azure App Service.",
            UseFor = ["Deploying Python web apps"],
            Services = [new ServiceEntry("Azure App Service", "when hosting web apps")]
        };

        var result = await rewriter.SynthesizeWhatItProvidesAsync("python-appservice-deploy", skill);

        result.Should().Be("It provides streamlined deployment.");
    }

    [Fact]
    public async Task TranslateWorkflowStepsAsync_ParsesJsonArrayResponse()
    {
        var rewriter = NewRewriter(StubChatClient("[\"You create the app.\", \"You deploy it.\"]"));

        var result = await rewriter.TranslateWorkflowStepsAsync(
            "python-appservice-deploy",
            ["create app", "deploy"],
            []);

        result.Should().Equal("You create the app.", "You deploy it.");
    }

    [Fact]
    public async Task TranslateWorkflowStepsAsync_EmptyInput_ReturnsEmpty()
    {
        var rewriter = NewRewriter(StubChatClient("ignored"));

        var result = await rewriter.TranslateWorkflowStepsAsync("skill", [], []);

        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateKeyless_BuildsRewriterWithoutApiKey()
    {
        // Policy: this repo NEVER uses API keys. CreateKeyless builds a
        // DefaultAzureCredential-backed rewriter from endpoint + model only.
        var rewriter = AzureOpenAiRewriter.CreateKeyless(
            "https://oai-test.cognitiveservices.azure.com/",
            "gpt-5-mini",
            "system {{ACROLINX_RULES}}",
            "Write for {{skillName}}: {{description}}",
            null,
            Substitute.For<ILogger<AzureOpenAiRewriter>>());

        rewriter.Should().NotBeNull();
    }
}
