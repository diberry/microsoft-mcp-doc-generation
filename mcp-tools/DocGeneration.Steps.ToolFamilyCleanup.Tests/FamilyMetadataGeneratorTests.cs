// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using Microsoft.Extensions.AI;
using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class FamilyMetadataGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_UsesAtLeastTwoThousandMaxTokensForServiceDescription()
    {
        var chatClient = new CapturingChatClient("AI description.");
        var generator = new FamilyMetadataGenerator(
            new GenerativeAIClient(chatClient),
            "system prompt",
            "Describe {{FAMILY_DISPLAY_NAME}}.");

        await generator.GenerateAsync(CreateFamilyContent("Azure Storage"));

        Assert.NotNull(chatClient.LastOptions);
        Assert.True(chatClient.LastOptions!.MaxOutputTokens >= 2000,
            $"Expected max output tokens >= 2000 but was {chatClient.LastOptions!.MaxOutputTokens}.");
    }

    [Fact]
    public async Task GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription()
    {
        var generator = new FamilyMetadataGenerator(
            new GenerativeAIClient(new TruncatingChatClient()),
            "system prompt",
            "Describe {{FAMILY_DISPLAY_NAME}}.");

        var result = await generator.GenerateAsync(CreateFamilyContent("Azure Storage", familyName: "storage"));

        // Pre-existing test updated to match current fallback behavior: BuildFallbackServiceDescription
        // now always returns the literal "<TBD_Content>" placeholder (see
        // ToolFamilyCleanup/Services/FamilyMetadataGenerator.cs) rather than a synthesized
        // per-service sentence. This placeholder must never be reported as a fully successful
        // description (requirement 3 / architecture docs "Designed AI Behavior" table).
        Assert.Contains("<TBD_Content>", result);
    }

    [Fact]
    public async Task GenerateAsync_WhenAiEndpointOffline_UsesTbdContentFallback_WithoutAttemptingAnyAiCall()
    {
        Environment.SetEnvironmentVariable(GenerativeAIClient.OfflineEnvironmentVariable, "true");
        try
        {
            var chatClient = new CapturingChatClient("should never be used");
            var generator = new FamilyMetadataGenerator(
                new GenerativeAIClient(chatClient),
                "system prompt",
                "Describe {{FAMILY_DISPLAY_NAME}}.");

            var result = await generator.GenerateAsync(CreateFamilyContent("Azure Storage"));

            // Requirement 3: never a false success — the TBD placeholder marks this content
            // as incomplete, and no AI call is attempted while the endpoint is known offline.
            Assert.Contains("<TBD_Content>", result);
            Assert.Null(chatClient.LastOptions);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GenerativeAIClient.OfflineEnvironmentVariable, null);
        }
    }

    private static FamilyContent CreateFamilyContent(string displayName, string familyName = "storage") => new()
    {
        FamilyName = familyName,
        DisplayName = displayName,
        Metadata = string.Empty,
        RelatedContent = string.Empty,
        Tools =
        [
            new ToolContent
            {
                ToolName = "storage list",
                FileName = "storage-list.complete.md",
                FamilyName = familyName,
                Content = "## storage list",
                Command = "storage list"
            }
        ]
    };

    private sealed class CapturingChatClient(string responseText) : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public ChatClientMetadata Metadata => new("test");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                FinishReason = ChatFinishReason.Stop
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class TruncatingChatClient : IChatClient
    {
        public ChatClientMetadata Metadata => new("test");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "partial"))
            {
                FinishReason = ChatFinishReason.Length
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
