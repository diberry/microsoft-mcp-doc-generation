using Microsoft.Extensions.AI;
using Xunit;

namespace GenerativeAI.Tests;

/// <summary>
/// Behavioral tests for the universal offline gate in <see cref="GenerativeAIClient"/>. This is
/// the single, service-agnostic choke point every AI-dependent pipeline step (2, 3, 4, 6) calls
/// through, so gating here — rather than in each step — disables ALL further Azure OpenAI calls
/// for the remainder of a run without threading a flag through every caller.
/// </summary>
[Collection("OfflineEnvironmentVariable")]
public sealed class AiEndpointOfflineGateTests : IDisposable
{
    public AiEndpointOfflineGateTests()
        => Environment.SetEnvironmentVariable(GenerativeAIClient.OfflineEnvironmentVariable, null);

    public void Dispose()
        => Environment.SetEnvironmentVariable(GenerativeAIClient.OfflineEnvironmentVariable, null);

    [Fact]
    public async Task GetChatCompletionAsync_OfflineModeActive_ThrowsWithoutCallingChatClient()
    {
        Environment.SetEnvironmentVariable(GenerativeAIClient.OfflineEnvironmentVariable, "true");
        var spyChatClient = new SpyChatClient("should never be returned");
        var client = new GenerativeAIClient(spyChatClient);

        var exception = await Assert.ThrowsAsync<AiEndpointOfflineException>(
            () => client.GetChatCompletionAsync("system prompt", "user prompt", toolOrNamespace: "storage", operation: "GenerateTool"));

        Assert.Equal(0, spyChatClient.CallCount);
        Assert.Contains(GenerativeAIClient.OfflineEnvironmentVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains("storage", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("on")]
    public void IsOfflineModeActive_TruthyVariants_ReturnsTrue(string value)
    {
        Environment.SetEnvironmentVariable(GenerativeAIClient.OfflineEnvironmentVariable, value);
        Assert.True(GenerativeAIClient.IsOfflineModeActive());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("0")]
    public void IsOfflineModeActive_FalsyOrUnset_ReturnsFalse(string? value)
    {
        Environment.SetEnvironmentVariable(GenerativeAIClient.OfflineEnvironmentVariable, value);
        Assert.False(GenerativeAIClient.IsOfflineModeActive());
    }

    [Fact]
    public async Task GetChatCompletionAsync_OfflineModeNotSet_CallsChatClientNormally()
    {
        var spyChatClient = new SpyChatClient("normal response");
        var client = new GenerativeAIClient(spyChatClient);

        var response = await client.GetChatCompletionAsync("system prompt", "user prompt");

        Assert.Equal("normal response", response);
        Assert.Equal(1, spyChatClient.CallCount);
    }

    private sealed class SpyChatClient(string responseText) : IChatClient
    {
        public int CallCount { get; private set; }

        public ChatClientMetadata Metadata => new("spy");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                FinishReason = ChatFinishReason.Stop
            };
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
