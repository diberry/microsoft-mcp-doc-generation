// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using HorizontalArticleGenerator.Models;
using Microsoft.Extensions.AI;
using Xunit;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace HorizontalArticleGenerator.Tests;

/// <summary>
/// Tests for #661 — transient-failure retry/backoff around AI calls in the horizontal-article
/// generator. Covers both the pure <c>WithRetry</c> helper and the end-to-end wiring through
/// <c>GenerateAIContentForTool</c> (retry recovers the AI result; exhaustion falls back to static).
/// </summary>
public class AiRetryTests : IDisposable
{
    private readonly string _outputBasePath;
    private readonly string _mcpToolsRoot;

    private const string ToolSystemPromptFile = "horizontal-article-tool-system-prompt.txt";
    private const string ToolUserPromptFile = "horizontal-article-tool-user-prompt.txt";
    private const string ProjectSubdir = "DocGeneration.Steps.HorizontalArticles";

    // Zero-delay backoff so retry tests run instantly.
    private static readonly Func<int, TimeSpan> NoDelay = _ => TimeSpan.Zero;

    public AiRetryTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "ai-retry-tests", Guid.NewGuid().ToString("N"));
        _outputBasePath = Path.Combine(root, "out");
        _mcpToolsRoot = Path.Combine(root, "mcp-tools");
        Directory.CreateDirectory(_outputBasePath);

        var promptDir = Path.Combine(_mcpToolsRoot, ProjectSubdir, "prompts");
        Directory.CreateDirectory(promptDir);
        File.WriteAllText(Path.Combine(promptDir, ToolSystemPromptFile), "You are a documentation assistant.");
        File.WriteAllText(Path.Combine(promptDir, ToolUserPromptFile), "Describe {{tool.command}} for {{serviceBrandName}}.");
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_outputBasePath)?.FullName;
        if (root != null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ─── WithRetry helper (pure) ──────────────────────────────────────────────

    [Fact]
    public async Task WithRetry_SucceedsOnFirstAttempt_InvokesOperationOnce()
    {
        var calls = 0;

        var result = await ArticleGenerator.WithRetry(
            () => { calls++; return Task.FromResult("ok"); },
            maxAttempts: 3,
            delay: NoDelay);

        Assert.Equal("ok", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task WithRetry_FailsThenSucceeds_RetriesAndReturnsResult()
    {
        var calls = 0;

        var result = await ArticleGenerator.WithRetry(
            () =>
            {
                calls++;
                if (calls < 2)
                    throw new HttpRequestException("503 transient failure");
                return Task.FromResult("recovered");
            },
            maxAttempts: 3,
            delay: NoDelay);

        Assert.Equal("recovered", result);
        Assert.Equal(2, calls); // failed once, succeeded on the second attempt
    }

    [Fact]
    public async Task WithRetry_AllAttemptsFail_ThrowsAfterMaxAttempts()
    {
        var calls = 0;

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await ArticleGenerator.WithRetry<string>(
                () =>
                {
                    calls++;
                    throw new HttpRequestException($"502 attempt {calls}");
                },
                maxAttempts: 3,
                delay: NoDelay));

        Assert.Equal(3, calls); // exactly maxAttempts invocations before giving up
        Assert.Contains("attempt 3", ex.Message);
    }

    // ─── End-to-end wiring through GenerateAIContentForTool ────────────────────

    [Fact]
    public async Task GenerateAIContentForTool_TransientFailureThenSuccess_RetriesAndUsesAIResult()
    {
        // Fake AI: throw once (transient), then return a valid per-tool JSON payload.
        const string successJson = "{\"genai-shortDescription\":\"Creates a Cosmos DB account.\",\"genai-capability\":\"Provision databases\"}";
        var chatClient = new ScriptedChatClient(failCount: 1, successText: successJson);
        var generator = CreateGenerator(new GenerativeAIClient(chatClient));

        var tool = new HorizontalToolSummary
        {
            Command = "cosmos account create",
            Description = "STATIC FALLBACK DESCRIPTION",
            ParameterCount = 2,
            Metadata = new()
        };

        var result = await generator.GenerateAIContentForTool(tool, "Azure Cosmos DB", "cosmos", toolIndex: 0);

        Assert.Equal("Creates a Cosmos DB account.", result.ShortDescription); // AI result, not the static fallback
        Assert.Equal("cosmos account create", result.Command);
        Assert.Equal(2, chatClient.CallCount); // proves one retry occurred
    }

    [Fact]
    public async Task GenerateAIContentForTool_AllAttemptsFail_FallsBackToStaticDescription()
    {
        // Fake AI: always throw a transient error — retry is exhausted, caller falls back.
        var chatClient = new ScriptedChatClient(failCount: int.MaxValue, successText: "");
        var generator = CreateGenerator(new GenerativeAIClient(chatClient));

        var tool = new HorizontalToolSummary
        {
            Command = "keyvault secret get",
            Description = "Gets a secret from a key vault.",
            ParameterCount = 1,
            Metadata = new()
        };

        var result = await generator.GenerateAIContentForTool(tool, "Azure Key Vault", "keyvault", toolIndex: 0);

        Assert.Equal("Gets a secret from a key vault.", result.ShortDescription); // static fallback
        Assert.Equal("keyvault secret get", result.Command);
        Assert.Equal(3, chatClient.CallCount); // exactly maxAttempts before falling back
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private ArticleGenerator CreateGenerator(GenerativeAIClient aiClient) =>
        new(aiClient, outputBasePath: _outputBasePath, mcpToolsRoot: _mcpToolsRoot, aiMaxAttempts: 3, aiRetryDelay: NoDelay);

    /// <summary>
    /// Fake <see cref="IChatClient"/> that throws a transient error for the first
    /// <c>failCount</c> calls, then returns <c>successText</c> as the assistant message.
    /// </summary>
    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly int _failCount;
        private readonly string _successText;

        public int CallCount { get; private set; }

        public ScriptedChatClient(int failCount, string successText)
        {
            _failCount = failCount;
            _successText = successText;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount <= _failCount)
                throw new HttpRequestException($"503 transient failure (call {CallCount})");

            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _successText))
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
