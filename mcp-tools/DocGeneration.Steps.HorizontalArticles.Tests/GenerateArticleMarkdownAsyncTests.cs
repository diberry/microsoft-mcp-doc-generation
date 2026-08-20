// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using HorizontalArticleGenerator.Models;
using Microsoft.Extensions.AI;
using Xunit;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace HorizontalArticleGenerator.Tests;

/// <summary>
/// Behavioral tests for <see cref="ArticleGenerator.GenerateArticleMarkdownAsync"/> — the
/// PipelineRunner reducer entry point for Step 6. Confirms it uses the current per-tool +
/// namespace-fragment AI generation path (one per-tool call, then four small namespace-fragment
/// calls — overview/access/best-practices/links — instead of one broad namespace-summary call)
/// and never falls back to the obsolete monolithic <c>GenerateAIContent</c> single-call method.
/// </summary>
public sealed class GenerateArticleMarkdownAsyncTests : IDisposable
{
    private const string ProjectSubdir = "DocGeneration.Steps.HorizontalArticles";
    private const string ToolSystemPromptFile = "horizontal-article-tool-system-prompt.txt";
    private const string ToolUserPromptFile = "horizontal-article-tool-user-prompt.txt";
    private const string OverviewSystemPromptFile = "horizontal-article-namespace-overview-system-prompt.txt";
    private const string OverviewUserPromptFile = "horizontal-article-namespace-overview-user-prompt.txt";
    private const string AccessSystemPromptFile = "horizontal-article-namespace-access-system-prompt.txt";
    private const string AccessUserPromptFile = "horizontal-article-namespace-access-user-prompt.txt";
    private const string BestPracticesSystemPromptFile = "horizontal-article-namespace-best-practices-system-prompt.txt";
    private const string BestPracticesUserPromptFile = "horizontal-article-namespace-best-practices-user-prompt.txt";
    private const string LinksSystemPromptFile = "horizontal-article-namespace-links-system-prompt.txt";
    private const string LinksUserPromptFile = "horizontal-article-namespace-links-user-prompt.txt";
    private const string TemplateFile = "horizontal-article-template.hbs";

    private static readonly Func<int, TimeSpan> NoDelay = _ => TimeSpan.Zero;

    private readonly string _outputBasePath;
    private readonly string _mcpToolsRoot;
    private readonly string _promptDir;

    public GenerateArticleMarkdownAsyncTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "generate-article-markdown-tests", Guid.NewGuid().ToString("N"));
        _outputBasePath = Path.Combine(root, "out");
        _mcpToolsRoot = Path.Combine(root, "mcp-tools");
        Directory.CreateDirectory(_outputBasePath);
        Directory.CreateDirectory(Path.Combine(_outputBasePath, "cli"));
        File.WriteAllText(Path.Combine(_outputBasePath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

        _promptDir = Path.Combine(_mcpToolsRoot, ProjectSubdir, "prompts");
        Directory.CreateDirectory(_promptDir);
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_outputBasePath)?.Parent?.FullName;
        if (root != null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateArticleMarkdownAsync_PerToolPromptFilesMissing_ThrowsWithoutAnyAiCall()
    {
        // No prompt files written at all — the reducer path must refuse to fall back to the
        // obsolete monolithic GenerateAIContent call, and must not attempt any AI call.
        var chatClient = new CountingChatClient();
        var generator = CreateGenerator(new GenerativeAIClient(chatClient));
        var outline = CreateOutline();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateArticleMarkdownAsync(outline, CancellationToken.None));

        Assert.Equal(0, chatClient.CallCount);
        Assert.Contains("per-tool", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateArticleMarkdownAsync_FragmentSystemPromptMissing_ThrowsWithoutAnyAiCall()
    {
        WriteAllPromptFiles();
        File.Delete(Path.Combine(_promptDir, AccessSystemPromptFile));

        var chatClient = new CountingChatClient();
        var generator = CreateGenerator(new GenerativeAIClient(chatClient));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateArticleMarkdownAsync(CreateOutline(), CancellationToken.None));

        Assert.Equal(0, chatClient.CallCount);
        Assert.Contains("namespace-fragment", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateArticleMarkdownAsync_PromptFilesPresent_UsesPerToolAndFourNamespaceFragmentAiCalls()
    {
        WriteAllPromptFiles();
        WriteMinimalTemplate();

        // One tool => exactly 5 AI calls: 1 per-tool call + 4 namespace-fragment calls
        // (overview, access, best practices, links). This is the surgical replacement for the
        // single broad namespace-summary call (which used to be 1 per-tool + 1 namespace = 2 calls).
        const string perToolJson = """{"genai-shortDescription":"Creates a widget.","genai-capability":"Provisioning"}""";
        const string overviewJson = """{"genai-serviceShortDescription":"Manage widgets.","genai-serviceOverview":"Widgets overview."}""";
        const string accessJson = """{"genai-serviceSpecificPrerequisites":[],"genai-requiredRoles":[]}""";
        const string bestPracticesJson = """{"genai-bestPractices":[]}""";
        const string linksJson = """{"genai-serviceDocLink":"","genai-additionalLinks":[]}""";
        var chatClient = new SequencedChatClient([perToolJson, overviewJson, accessJson, bestPracticesJson, linksJson]);
        var generator = CreateGenerator(new GenerativeAIClient(chatClient));
        var outline = CreateOutline();

        var markdown = await generator.GenerateArticleMarkdownAsync(outline, CancellationToken.None);

        Assert.Equal(5, chatClient.CallCount);
        Assert.Contains("Widgets overview.", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateArticleMarkdownAsync_OverviewFragmentReturnsEmptyRequiredFields_ThrowsIncompleteNotSuccess()
    {
        WriteAllPromptFiles();
        WriteMinimalTemplate();

        // Per-tool call succeeds, but the overview fragment AI call returns an empty JSON object
        // (matches Step 2's documented "raw output {} by design" / an AI-required field missing).
        const string perToolJson = """{"genai-shortDescription":"Creates a widget.","genai-capability":"Provisioning"}""";
        const string emptyOverviewJson = "{}";
        const string accessJson = """{"genai-serviceSpecificPrerequisites":[],"genai-requiredRoles":[]}""";
        const string bestPracticesJson = """{"genai-bestPractices":[]}""";
        const string linksJson = """{"genai-serviceDocLink":"","genai-additionalLinks":[]}""";
        var chatClient = new SequencedChatClient([perToolJson, emptyOverviewJson, accessJson, bestPracticesJson, linksJson]);
        var generator = CreateGenerator(new GenerativeAIClient(chatClient));
        var outline = CreateOutline();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateArticleMarkdownAsync(outline, CancellationToken.None));

        // Never silently "succeeds" with placeholder/empty content — must surface as a failure.
        Assert.Contains("empty required fields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateArticleMarkdownAsync_OnlyNonOverviewFragmentEmpty_StillSucceeds()
    {
        // The overview fragment is the only fatal-if-absent one. If access/best-practices/links
        // come back empty (e.g. a component-specific failure survives retry) the article still
        // generates — partial success is preserved rather than failing the whole run.
        WriteAllPromptFiles();
        WriteMinimalTemplate();

        const string perToolJson = """{"genai-shortDescription":"Creates a widget.","genai-capability":"Provisioning"}""";
        const string overviewJson = """{"genai-serviceShortDescription":"Manage widgets.","genai-serviceOverview":"Widgets overview."}""";
        const string emptyAccessJson = "{}";
        const string emptyBestPracticesJson = "{}";
        const string emptyLinksJson = "{}";
        var chatClient = new SequencedChatClient([perToolJson, overviewJson, emptyAccessJson, emptyBestPracticesJson, emptyLinksJson]);
        var generator = CreateGenerator(new GenerativeAIClient(chatClient));
        var outline = CreateOutline();

        var markdown = await generator.GenerateArticleMarkdownAsync(outline, CancellationToken.None);

        Assert.Equal(5, chatClient.CallCount);
        Assert.Contains("Widgets overview.", markdown, StringComparison.Ordinal);
    }

    private ArticleGenerator CreateGenerator(GenerativeAIClient aiClient) =>
        new(aiClient, outputBasePath: _outputBasePath, mcpToolsRoot: _mcpToolsRoot, aiMaxAttempts: 1, aiRetryDelay: NoDelay);

    private static ArticleOutlineContext CreateOutline()
    {
        var toolEvidence = """{"kind":"tool","command":"widget create","description":"Creates a widget.","parameterCount":1,"moreInfoLink":"../parameters/widget-create-parameters.md"}""";
        return new ArticleOutlineContext(
            "Azure Widgets",
            [
                new ArticleOutlineSection("Prerequisites", "prerequisites", ["Have an Azure subscription."]),
                new ArticleOutlineSection("Tool overview", "tools", [toolEvidence]),
            ],
            "widgets",
            "1.0");
    }

    private void WriteAllPromptFiles()
    {
        File.WriteAllText(Path.Combine(_promptDir, ToolSystemPromptFile), "You are a documentation assistant.");
        File.WriteAllText(Path.Combine(_promptDir, ToolUserPromptFile), "Describe {{tool.command}} for {{serviceBrandName}}.");
        File.WriteAllText(Path.Combine(_promptDir, OverviewSystemPromptFile), "You are a documentation assistant.");
        File.WriteAllText(Path.Combine(_promptDir, OverviewUserPromptFile), "Summarize {{serviceBrandName}}.");
        File.WriteAllText(Path.Combine(_promptDir, AccessSystemPromptFile), "You are a documentation assistant.");
        File.WriteAllText(Path.Combine(_promptDir, AccessUserPromptFile), "Access for {{serviceBrandName}}.");
        File.WriteAllText(Path.Combine(_promptDir, BestPracticesSystemPromptFile), "You are a documentation assistant.");
        File.WriteAllText(Path.Combine(_promptDir, BestPracticesUserPromptFile), "Best practices for {{serviceBrandName}}.");
        File.WriteAllText(Path.Combine(_promptDir, LinksSystemPromptFile), "You are a documentation assistant.");
        File.WriteAllText(Path.Combine(_promptDir, LinksUserPromptFile), "Links for {{serviceBrandName}}.");
    }

    private void WriteMinimalTemplate()
    {
        var templateDir = Path.Combine(_mcpToolsRoot, ProjectSubdir, "templates");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(
            Path.Combine(templateDir, TemplateFile),
            "# {{serviceBrandName}}\n\n{{genai-serviceOverview}}\n");
    }

    /// <summary>Fake <see cref="IChatClient"/> that always throws — proves zero AI calls occur.</summary>
    private sealed class CountingChatClient : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("No AI call should have been attempted.");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>Fake <see cref="IChatClient"/> that returns canned responses in call order.</summary>
    private sealed class SequencedChatClient(IReadOnlyList<string> responses) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var text = responses[Math.Min(CallCount, responses.Count - 1)];
            CallCount++;
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                FinishReason = ChatFinishReason.Stop
            };
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
