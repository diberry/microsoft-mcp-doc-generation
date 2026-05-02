// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using Microsoft.Extensions.AI;
using ToolGeneration_Improved.Services;

namespace ToolGeneration_Improved.Tests;

/// <summary>
/// Tests for per-tool timeout, cancellation, and AI-failure fallback behavior.
/// </summary>
public class AiTimeoutFallbackTests : IDisposable
{
    private readonly string _inputDir;
    private readonly string _outputDir;

    public AiTimeoutFallbackTests()
    {
        _inputDir = Path.Combine(Path.GetTempPath(), $"timeout-test-in-{Guid.NewGuid():N}");
        _outputDir = Path.Combine(Path.GetTempPath(), $"timeout-test-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_inputDir);
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_inputDir)) Directory.Delete(_inputDir, true);
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, true);
    }

    private static string CreateToolContent(string toolName = "test-tool")
    {
        return $"""
            ---
            title: {toolName}
            ---
            # {toolName}

            This tool does something.

            Example prompts include:
            - "Do the thing"

            Required parameters:
            | Name | Description |
            |------|-------------|
            | --name | The name |
            """;
    }

    // ── Per-tool timeout falls back to original ──

    [Fact]
    public async Task TimedOutTool_SavesOriginalContent()
    {
        var originalContent = CreateToolContent("slow-tool");
        await File.WriteAllTextAsync(Path.Combine(_inputDir, "slow-tool.md"), originalContent);

        // AI client that hangs forever
        var hangingClient = new GenerativeAIClient(new HangingChatClient());
        var service = new ImprovedToolGeneratorService(hangingClient, "system", "{0}");

        var result = await service.GenerateImprovedToolFilesAsync(
            _inputDir, _outputDir, maxTokens: 1000,
            perToolTimeout: TimeSpan.FromMilliseconds(200));

        // Should have saved the original content as fallback
        var outputPath = Path.Combine(_outputDir, "slow-tool.md");
        Assert.True(File.Exists(outputPath), "Output file should exist even when AI times out");

        var savedContent = await File.ReadAllTextAsync(outputPath);
        Assert.Equal(originalContent, savedContent);
    }

    [Fact]
    public async Task TimedOutTool_ContinuesProcessingRemainingTools()
    {
        // First tool: hangs; Second tool: succeeds
        await File.WriteAllTextAsync(Path.Combine(_inputDir, "a-hanging-tool.md"), CreateToolContent("a-hanging"));
        await File.WriteAllTextAsync(Path.Combine(_inputDir, "b-fast-tool.md"), CreateToolContent("b-fast"));

        var selectiveClient = new GenerativeAIClient(new SelectiveHangChatClient("a-hanging"));
        var service = new ImprovedToolGeneratorService(selectiveClient, "system", "{0}");

        var result = await service.GenerateImprovedToolFilesAsync(
            _inputDir, _outputDir, maxTokens: 1000,
            perToolTimeout: TimeSpan.FromMilliseconds(200));

        // Both files should exist in output
        Assert.True(File.Exists(Path.Combine(_outputDir, "a-hanging-tool.md")), "Timed-out tool should still produce output");
        Assert.True(File.Exists(Path.Combine(_outputDir, "b-fast-tool.md")), "Fast tool should produce output");
    }

    // ── External cancellation propagates ──

    [Fact]
    public async Task ExternalCancellation_PropagatesImmediately()
    {
        await File.WriteAllTextAsync(Path.Combine(_inputDir, "tool.md"), CreateToolContent());

        var hangingClient = new GenerativeAIClient(new HangingChatClient());
        var service = new ImprovedToolGeneratorService(hangingClient, "system", "{0}");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.GenerateImprovedToolFilesAsync(
                _inputDir, _outputDir, maxTokens: 1000,
                perToolTimeout: TimeSpan.FromMinutes(5),
                pipelineCancellationToken: cts.Token));
    }

    // ── AI exception falls back to original ──

    [Fact]
    public async Task AiException_SavesOriginalContent()
    {
        var originalContent = CreateToolContent("error-tool");
        await File.WriteAllTextAsync(Path.Combine(_inputDir, "error-tool.md"), originalContent);

        var throwingClient = new GenerativeAIClient(new ThrowingChatClient());
        var service = new ImprovedToolGeneratorService(throwingClient, "system", "{0}");

        var result = await service.GenerateImprovedToolFilesAsync(
            _inputDir, _outputDir, maxTokens: 1000);

        var outputPath = Path.Combine(_outputDir, "error-tool.md");
        Assert.True(File.Exists(outputPath), "Output file should exist even when AI throws");

        var savedContent = await File.ReadAllTextAsync(outputPath);
        Assert.Equal(originalContent, savedContent);
    }

    [Fact]
    public async Task AiException_ReturnsZero_NotError()
    {
        // AI failures with fallback should not be counted as hard errors
        await File.WriteAllTextAsync(Path.Combine(_inputDir, "error-tool.md"), CreateToolContent());

        var throwingClient = new GenerativeAIClient(new ThrowingChatClient());
        var service = new ImprovedToolGeneratorService(throwingClient, "system", "{0}");

        var result = await service.GenerateImprovedToolFilesAsync(
            _inputDir, _outputDir, maxTokens: 1000);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DefaultPerToolTimeout_IsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), ImprovedToolGeneratorService.DefaultPerToolTimeout);
    }

    // ── Stub chat clients ──

    /// <summary>Chat client that never returns (simulates Azure OpenAI hang).</summary>
    private sealed class HangingChatClient : IChatClient
    {
        public ChatClientMetadata Metadata => new("test-hanging");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.Delay(Timeout.Infinite, cancellationToken)
                .ContinueWith<ChatResponse>(_ => throw new OperationCanceledException(), cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>Chat client that hangs for tools whose content contains a trigger string, succeeds for others.</summary>
    private sealed class SelectiveHangChatClient : IChatClient
    {
        private readonly string _hangTrigger;
        public SelectiveHangChatClient(string hangTrigger) => _hangTrigger = hangTrigger;
        public ChatClientMetadata Metadata => new("test-selective");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var userMsg = chatMessages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
            if (userMsg.Contains(_hangTrigger))
            {
                return Task.Delay(Timeout.Infinite, cancellationToken)
                    .ContinueWith<ChatResponse>(_ => throw new OperationCanceledException(), cancellationToken);
            }

            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, userMsg))
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

    /// <summary>Chat client that throws an HttpRequestException (simulates server error).</summary>
    private sealed class ThrowingChatClient : IChatClient
    {
        public ChatClientMetadata Metadata => new("test-throwing");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("502 Bad Gateway");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
