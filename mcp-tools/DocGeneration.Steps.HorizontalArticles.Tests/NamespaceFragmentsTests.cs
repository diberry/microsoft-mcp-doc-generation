// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using GenerativeAI;
using HorizontalArticleGenerator.Models;
using Microsoft.Extensions.AI;
using Xunit;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;
using NamespaceFragment = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator.NamespaceFragment;

namespace HorizontalArticleGenerator.Tests;

/// <summary>
/// TDD regression coverage for the Step 6 surgical refactor that replaces the single broad
/// namespace-summary AI call (33 KB legacy prompt, seven fields, prone to truncation on
/// gpt-5-mini, then blindly retried three times) with four small, focused namespace-fragment AI
/// calls (overview/access/best-practices/links) and a deterministic in-process stitcher. These
/// tests fail if the refactor is reverted: they prove (1) the four fragments stitch correctly into
/// <see cref="NamespaceSummaryAIData"/>, (2) each fragment's output token budget matches the user's
/// specification exactly, (3) non-transient failures (token truncation, malformed JSON, HTTP
/// 400/401) are never retried while transient failures still are, and (4) the fragment AI calls use
/// their own compact prompt files - never the legacy 33 KB system prompt - and forward real
/// operation/target context so status logs never show <c>target=unknown</c>.
/// </summary>
public sealed class NamespaceFragmentsTests : IDisposable
{
    private const string ProjectSubdir = "DocGeneration.Steps.HorizontalArticles";
    private static readonly Func<int, TimeSpan> NoDelay = _ => TimeSpan.Zero;

    private readonly string _outputBasePath;
    private readonly string _mcpToolsRoot;
    private readonly string _promptDir;

    public NamespaceFragmentsTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "namespace-fragments-tests", Guid.NewGuid().ToString("N"));
        _outputBasePath = Path.Combine(root, "out");
        _mcpToolsRoot = Path.Combine(root, "mcp-tools");
        Directory.CreateDirectory(_outputBasePath);
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

    // --- StitchNamespaceSummary (pure, deterministic mapping) ---

    [Fact]
    public void StitchNamespaceSummary_MapsAllFourFragmentsIntoNamespaceSummaryAIData()
    {
        var overview = new NamespaceOverviewFragment
        {
            ServiceShortDescription = "Manage widgets.",
            ServiceOverview = "Widgets overview paragraph."
        };
        var access = new NamespaceAccessFragment
        {
            ServiceSpecificPrerequisites = [new Prerequisite { Title = "Subscription", Description = "An Azure subscription." }],
            RequiredRoles = [new RequiredRole { Name = "Widget Contributor", Purpose = "Create and manage widgets." }]
        };
        var bestPractices = new NamespaceBestPracticesFragment
        {
            BestPractices = [new BestPractice { Title = "Use managed identity", Description = "Avoid static credentials." }]
        };
        var links = new NamespaceLinksFragment
        {
            ServiceDocLink = "https://learn.microsoft.com/azure/widgets/",
            AdditionalLinks = [new AdditionalLink { Title = "Pricing", Url = "https://azure.microsoft.com/pricing/widgets/" }]
        };

        var stitched = ArticleGenerator.StitchNamespaceSummary(overview, access, bestPractices, links);

        Assert.Equal(overview.ServiceShortDescription, stitched.ServiceShortDescription);
        Assert.Equal(overview.ServiceOverview, stitched.ServiceOverview);
        Assert.Same(access.ServiceSpecificPrerequisites, stitched.ServiceSpecificPrerequisites);
        Assert.Same(access.RequiredRoles, stitched.RequiredRoles);
        Assert.Same(bestPractices.BestPractices, stitched.BestPractices);
        Assert.Equal(links.ServiceDocLink, stitched.ServiceDocLink);
        Assert.Same(links.AdditionalLinks, stitched.AdditionalLinks);
    }

    [Fact]
    public void StitchNamespaceSummary_AllFragmentsEmpty_ProducesEmptyNamespaceSummary()
    {
        var stitched = ArticleGenerator.StitchNamespaceSummary(
            new NamespaceOverviewFragment(), new NamespaceAccessFragment(), new NamespaceBestPracticesFragment(), new NamespaceLinksFragment());

        Assert.Equal(string.Empty, stitched.ServiceShortDescription);
        Assert.Equal(string.Empty, stitched.ServiceOverview);
        Assert.Empty(stitched.ServiceSpecificPrerequisites);
        Assert.Empty(stitched.RequiredRoles);
        Assert.Null(stitched.BestPractices);
        Assert.Null(stitched.ServiceDocLink);
        Assert.Empty(stitched.AdditionalLinks);
    }

    // --- Per-fragment output token budgets (user spec: 500/1500/1500/750) ---

    [Fact]
    public void CalculateMaxTokens_NamespaceFragment_Overview_Returns500()
    {
        // NamespaceFragment is internal, so the enum value is resolved inside the test body rather
        // than passed as a public [Theory] parameter (which would require a public parameter type).
        Assert.Equal(500, ArticleGenerator.CalculateMaxTokens(NamespaceFragment.Overview));
    }

    [Fact]
    public void CalculateMaxTokens_NamespaceFragment_Access_Returns1500()
    {
        Assert.Equal(1500, ArticleGenerator.CalculateMaxTokens(NamespaceFragment.Access));
    }

    [Fact]
    public void CalculateMaxTokens_NamespaceFragment_BestPractices_Returns1500()
    {
        Assert.Equal(1500, ArticleGenerator.CalculateMaxTokens(NamespaceFragment.BestPractices));
    }

    [Fact]
    public void CalculateMaxTokens_NamespaceFragment_Links_Returns750()
    {
        Assert.Equal(750, ArticleGenerator.CalculateMaxTokens(NamespaceFragment.Links));
    }

    // --- IsRetryableAiFailure classification ---

    [Theory]
    [InlineData("LLM response was truncated due to token limit. Used tokens: 500, Max output tokens: 500.")]
    [InlineData("Response TRUNCATED DUE TO TOKEN LIMIT (case-insensitive match).")]
    public void IsRetryableAiFailure_TokenTruncation_IsNotRetryable(string message)
    {
        var ex = new InvalidOperationException(message);
        Assert.False(ArticleGenerator.IsRetryableAiFailure(ex));
    }

    [Fact]
    public void IsRetryableAiFailure_MalformedJson_IsNotRetryable()
    {
        JsonException ex;
        try
        {
            JsonSerializer.Deserialize<NamespaceOverviewFragment>("{ not valid json");
            throw new InvalidOperationException("Expected a JsonException.");
        }
        catch (JsonException jsonEx)
        {
            ex = jsonEx;
        }

        Assert.False(ArticleGenerator.IsRetryableAiFailure(ex));
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.BadRequest)]
    [InlineData(System.Net.HttpStatusCode.Unauthorized)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    [InlineData(System.Net.HttpStatusCode.NotFound)]
    [InlineData(System.Net.HttpStatusCode.UnprocessableEntity)]
    public void IsRetryableAiFailure_HttpClientError_IsNotRetryable(System.Net.HttpStatusCode statusCode)
    {
        var ex = new HttpRequestException("client error", inner: null, statusCode: statusCode);
        Assert.False(ArticleGenerator.IsRetryableAiFailure(ex));
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.TooManyRequests)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError)]
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable)]
    [InlineData(System.Net.HttpStatusCode.GatewayTimeout)]
    public void IsRetryableAiFailure_TransientHttpFailure_IsRetryable(System.Net.HttpStatusCode statusCode)
    {
        var ex = new HttpRequestException("transient error", inner: null, statusCode: statusCode);
        Assert.True(ArticleGenerator.IsRetryableAiFailure(ex));
    }

    [Fact]
    public void IsRetryableAiFailure_HttpNetworkFailureWithoutStatus_IsRetryable()
    {
        Assert.True(ArticleGenerator.IsRetryableAiFailure(new HttpRequestException("DNS failure")));
    }

    [Fact]
    public void IsRetryableAiFailure_Timeout_IsRetryable()
    {
        Assert.True(ArticleGenerator.IsRetryableAiFailure(new TimeoutException("request timed out")));
    }

    [Fact]
    public void IsRetryableAiFailure_GenericException_IsNotRetryable()
    {
        Assert.False(ArticleGenerator.IsRetryableAiFailure(new InvalidOperationException("some other failure")));
    }

    [Fact]
    public void IsRetryableAiFailure_Cancellation_IsNotRetryable()
    {
        Assert.False(ArticleGenerator.IsRetryableAiFailure(new OperationCanceledException()));
    }

    // --- WithRetry + IsRetryableAiFailure wiring: truncation executes once, transient retries ---

    [Fact]
    public async Task WithRetry_WithIsRetryableAiFailure_TokenTruncation_ExecutesExactlyOnceAndThrows()
    {
        var calls = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ArticleGenerator.WithRetry<string>(
                () =>
                {
                    calls++;
                    throw new InvalidOperationException("LLM response was truncated due to token limit. Used tokens: 500, Max output tokens: 500.");
                },
                maxAttempts: 3,
                delay: NoDelay,
                shouldRetry: ArticleGenerator.IsRetryableAiFailure));

        Assert.Equal(1, calls); // no retry attempted -- truncation is deterministic, not transient
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithRetry_WithIsRetryableAiFailure_Http400_ExecutesExactlyOnceAndThrows()
    {
        var calls = 0;

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await ArticleGenerator.WithRetry<string>(
                () =>
                {
                    calls++;
                    throw new HttpRequestException("bad request", inner: null, statusCode: System.Net.HttpStatusCode.BadRequest);
                },
                maxAttempts: 3,
                delay: NoDelay,
                shouldRetry: ArticleGenerator.IsRetryableAiFailure));

        Assert.Equal(1, calls); // no retry attempted for a non-transient client error
    }

    [Fact]
    public async Task WithRetry_WithIsRetryableAiFailure_TransientHttpRequestException_RetriesAndRecovers()
    {
        var calls = 0;

        var result = await ArticleGenerator.WithRetry(
            () =>
            {
                calls++;
                if (calls < 2)
                {
                    throw new HttpRequestException("503 transient failure", inner: null, statusCode: System.Net.HttpStatusCode.ServiceUnavailable);
                }
                return Task.FromResult("recovered");
            },
            maxAttempts: 3,
            delay: NoDelay,
            shouldRetry: ArticleGenerator.IsRetryableAiFailure);

        Assert.Equal("recovered", result);
        Assert.Equal(2, calls); // transient failure -- retried once, then succeeded
    }

    // --- End-to-end fragment generation: compact prompts only, correct budgets, real context ---

    [Fact]
    public async Task GenerateNamespaceOverviewAIContent_UsesOnlyCompactOverviewPrompts_NotLegacyBroadPrompt()
    {
        // Deliberately write ONLY the overview fragment's own prompt pair -- never the 33 KB legacy
        // horizontal-article-system-prompt.txt or the legacy namespace-user-prompt.txt. If the
        // implementation ever regresses to reading the legacy broad prompt, this file simply won't
        // exist and the fragment call will degrade to an empty result instead of succeeding.
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-overview-system-prompt.txt"),
            "You are a Microsoft Learn documentation assistant. Return JSON only.");
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-overview-user-prompt.txt"),
            "Summarize {{serviceBrandName}} in one short description and one overview paragraph.");

        var chatClient = new RecordingChatClient("""{"genai-serviceShortDescription":"Manage widgets.","genai-serviceOverview":"Widgets overview."}""");
        var aiClient = new GenerativeAIClient(chatClient);
        var generator = new ArticleGenerator(aiClient, _outputBasePath, _mcpToolsRoot, aiMaxAttempts: 1, aiRetryDelay: NoDelay);
        var staticData = CreateStaticData();

        var fragment = await generator.GenerateNamespaceOverviewAIContent(staticData);

        Assert.Equal("Manage widgets.", fragment.ServiceShortDescription);
        Assert.Equal("Widgets overview.", fragment.ServiceOverview);
        Assert.Equal(1, chatClient.CallCount);
        // The compact overview system prompt was actually used (not some other/legacy content).
        Assert.Contains("Microsoft Learn documentation assistant", chatClient.LastSystemPrompt, StringComparison.Ordinal);
        // Output budget forwarded matches the user's overview spec (~500).
        Assert.Equal(500, chatClient.LastMaxOutputTokens);
        Assert.Equal(ReasoningEffort.Low, chatClient.LastReasoningEffort);
        Assert.Same(ChatResponseFormat.Json, chatClient.LastResponseFormat);
    }

    [Fact]
    public async Task GenerateNamespaceOverviewAIContent_TruncatedResponse_IsAppendedToPromptArtifact()
    {
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-overview-system-prompt.txt"),
            "Return JSON only.");
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-overview-user-prompt.txt"),
            "Summarize {{serviceBrandName}}.");

        const string partialResponse = """{"genai-serviceOverview":"partial""";
        var chatClient = new RecordingChatClient(partialResponse, ChatFinishReason.Length);
        var generator = new ArticleGenerator(
            new GenerativeAIClient(chatClient),
            _outputBasePath,
            _mcpToolsRoot,
            aiMaxAttempts: 1,
            aiRetryDelay: NoDelay);

        await generator.GenerateNamespaceOverviewAIContent(CreateStaticData());

        var artifactPath = Path.Combine(
            _outputBasePath,
            "horizontal-article-prompts",
            "horizontal-article-widgets-namespace-overview-prompt.md");
        var artifact = await File.ReadAllTextAsync(artifactPath);
        Assert.Contains("## AI Response (truncated)", artifact, StringComparison.Ordinal);
        Assert.Contains(partialResponse, artifact, StringComparison.Ordinal);
        Assert.Contains("## AI Error", artifact, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateNamespaceOverviewAIContent_NoResponseFailure_IsRecordedInPromptArtifact()
    {
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-overview-system-prompt.txt"),
            "Return JSON only.");
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-overview-user-prompt.txt"),
            "Summarize {{serviceBrandName}}.");

        var generator = new ArticleGenerator(
            new GenerativeAIClient(new ThrowingChatClient(new HttpRequestException("service unavailable"))),
            _outputBasePath,
            _mcpToolsRoot,
            aiMaxAttempts: 1,
            aiRetryDelay: NoDelay);

        await generator.GenerateNamespaceOverviewAIContent(CreateStaticData());

        var artifactPath = Path.Combine(
            _outputBasePath,
            "horizontal-article-prompts",
            "horizontal-article-widgets-namespace-overview-prompt.md");
        var artifact = await File.ReadAllTextAsync(artifactPath);
        Assert.Contains("## AI Response", artifact, StringComparison.Ordinal);
        Assert.Contains("_No response was received._", artifact, StringComparison.Ordinal);
        Assert.Contains("## AI Error", artifact, StringComparison.Ordinal);
        Assert.Contains("service unavailable", artifact, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateNamespaceAccessAIContent_MissingPromptFiles_DegradesToEmptyFragmentWithoutThrowing()
    {
        // No access prompt files written at all -- must gracefully degrade, not throw, and must not
        // call the AI (proves it never silently falls back to the legacy broad prompt either).
        var chatClient = new RecordingChatClient("{}");
        var aiClient = new GenerativeAIClient(chatClient);
        var generator = new ArticleGenerator(aiClient, _outputBasePath, _mcpToolsRoot, aiMaxAttempts: 1, aiRetryDelay: NoDelay);
        var staticData = CreateStaticData();

        var fragment = await generator.GenerateNamespaceAccessAIContent(staticData);

        Assert.Empty(fragment.ServiceSpecificPrerequisites);
        Assert.Empty(fragment.RequiredRoles);
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task GenerateNamespaceLinksAIContent_ForwardsRealOperationAndTargetContext_NoTargetUnknown()
    {
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-links-system-prompt.txt"),
            "You are a Microsoft Learn documentation assistant. Return JSON only.");
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-namespace-links-user-prompt.txt"),
            "Provide documentation links for {{serviceBrandName}}.");

        var logLines = new List<string>();
        var chatClient = new RecordingChatClient("""{"genai-serviceDocLink":"https://learn.microsoft.com/azure/widgets/","genai-additionalLinks":[]}""");
        var aiClient = new GenerativeAIClient(chatClient, statusLogger: logLines.Add);
        var generator = new ArticleGenerator(aiClient, _outputBasePath, _mcpToolsRoot, aiMaxAttempts: 1, aiRetryDelay: NoDelay);
        var staticData = CreateStaticData();

        await generator.GenerateNamespaceLinksAIContent(staticData);

        Assert.Single(logLines);
        Assert.DoesNotContain("target=unknown", logLines[0], StringComparison.Ordinal);
        Assert.Contains("target=widgets", logLines[0], StringComparison.Ordinal);
        Assert.Contains("operation=namespace-links", logLines[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAIContentForTool_ForwardsRealOperationAndTargetContext_NoTargetUnknown()
    {
        var toolPromptDir = _promptDir;
        File.WriteAllText(Path.Combine(toolPromptDir, "horizontal-article-tool-system-prompt.txt"), "You are a documentation assistant.");
        File.WriteAllText(Path.Combine(toolPromptDir, "horizontal-article-tool-user-prompt.txt"), "Describe {{tool.command}} for {{serviceBrandName}}.");

        var logLines = new List<string>();
        var chatClient = new RecordingChatClient("""{"genai-shortDescription":"Creates a widget.","genai-capability":"Provisioning"}""");
        var aiClient = new GenerativeAIClient(chatClient, statusLogger: logLines.Add);
        var generator = new ArticleGenerator(aiClient, _outputBasePath, _mcpToolsRoot, aiMaxAttempts: 1, aiRetryDelay: NoDelay);
        var tool = new HorizontalToolSummary { Command = "widget create", Description = "Creates a widget.", ParameterCount = 1, Metadata = new() };

        await generator.GenerateAIContentForTool(tool, "Azure Widgets", "widgets", toolIndex: 0);

        Assert.Single(logLines);
        Assert.DoesNotContain("target=unknown", logLines[0], StringComparison.Ordinal);
        Assert.Contains("target=widget create", logLines[0], StringComparison.Ordinal);
        Assert.Contains("operation=per-tool", logLines[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAIContentForTool_TruncatedResponse_IsAppendedToPromptArtifact()
    {
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-tool-system-prompt.txt"),
            "Return JSON only.");
        File.WriteAllText(
            Path.Combine(_promptDir, "horizontal-article-tool-user-prompt.txt"),
            "Describe {{tool.command}}.");

        const string partialResponse = """{"genai-shortDescription":"partial""";
        var generator = new ArticleGenerator(
            new GenerativeAIClient(new RecordingChatClient(partialResponse, ChatFinishReason.Length)),
            _outputBasePath,
            _mcpToolsRoot,
            aiMaxAttempts: 1,
            aiRetryDelay: NoDelay);
        var tool = new HorizontalToolSummary
        {
            Command = "widget create",
            Description = "Creates a widget.",
            ParameterCount = 1,
            Metadata = new()
        };

        await generator.GenerateAIContentForTool(tool, "Azure Widgets", "widgets", toolIndex: 0);

        var artifactPath = Path.Combine(
            _outputBasePath,
            "horizontal-article-prompts",
            "horizontal-article-widgets-tool-00-prompt.md");
        var artifact = await File.ReadAllTextAsync(artifactPath);
        Assert.Contains("## AI Response (truncated)", artifact, StringComparison.Ordinal);
        Assert.Contains(partialResponse, artifact, StringComparison.Ordinal);
        Assert.Contains("## AI Error", artifact, StringComparison.Ordinal);
    }

    // --- Helpers ---

    private static StaticArticleData CreateStaticData() => new()
    {
        ServiceBrandName = "Azure Widgets",
        ServiceIdentifier = "widgets",
        GeneratedAt = DateTime.UtcNow.ToString("o"),
        Version = "1.0.0",
        ToolsReferenceLink = "../tool-family/widgets.md",
        Tools = [new HorizontalToolSummary { Command = "widget create", Description = "Creates a widget.", ParameterCount = 1, Metadata = new() }]
    };

    /// <summary>Fake <see cref="IChatClient"/> that records the last request and returns a canned response.</summary>
    private sealed class RecordingChatClient(
        string responseText,
        ChatFinishReason? finishReason = null) : IChatClient
    {
        public int CallCount { get; private set; }
        public string LastSystemPrompt { get; private set; } = string.Empty;
        public int? LastMaxOutputTokens { get; private set; }
        public ReasoningEffort? LastReasoningEffort { get; private set; }
        public ChatResponseFormat? LastResponseFormat { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSystemPrompt = chatMessages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? string.Empty;
            LastMaxOutputTokens = options?.MaxOutputTokens;
            LastReasoningEffort = options?.Reasoning?.Effort;
            LastResponseFormat = options?.ResponseFormat;
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                FinishReason = finishReason ?? ChatFinishReason.Stop
            };
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class ThrowingChatClient(Exception exception) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ChatResponse>(exception);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
