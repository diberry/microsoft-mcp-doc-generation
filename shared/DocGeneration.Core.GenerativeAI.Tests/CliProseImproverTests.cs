// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Shared;
using Xunit;

namespace GenerativeAI.Tests;

/// <summary>
/// Tests for CliProseImprover — AI improvement of CLI prose fields with validation and fallback.
/// </summary>
public class CliProseImproverTests
{
    private static readonly string SystemPrompt = "You are a test system prompt.";

    // ── Helpers ──────────────────────────────────────────────────────

    private static CliToolInfo MakeTool(
        string command = "storage account list",
        string description = "List all storage accounts in a subscription.",
        params CliSwitch[] switches)
    {
        return new CliToolInfo(command, description, switches.Length > 0 ? switches : new[]
        {
            new CliSwitch("--subscription", "The Azure subscription ID."),
            new CliSwitch("--resource-group", "The name of the resource group.")
        });
    }

    private static IReadOnlyDictionary<string, CliToolInfo> MakeToolDict(params (string key, CliToolInfo tool)[] items)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, tool) in items)
            result[key] = tool;
        return result;
    }

    private static CliProseImprover CreateImprover(IChatClient chatClient)
        => new(new GenerativeAIClient(chatClient), SystemPrompt);

    // ── Stub chat clients ────────────────────────────────────────────

    /// <summary>Chat client that returns a fixed string response.</summary>
    private sealed class FixedResponseChatClient : IChatClient
    {
        private readonly string _response;
        public FixedResponseChatClient(string response) => _response = response;
        public ChatClientMetadata Metadata => new("test-fixed");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _response))
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

    /// <summary>Chat client that never returns (simulates timeout).</summary>
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

    /// <summary>Chat client that throws an exception.</summary>
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

    /// <summary>Chat client that captures the cancellation token and allows checking propagation.</summary>
    private sealed class TokenCapturingChatClient : IChatClient
    {
        public CancellationToken CapturedToken { get; private set; }
        public ChatClientMetadata Metadata => new("test-token-capture");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CapturedToken = cancellationToken;
            var json = """{"tool_description":"Improved.","switch_descriptions":{}}""";
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
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

    // ── Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task ImproveProseAsync_ImprovesToolDescription()
    {
        var aiResponse = """
        {
            "tool_description": "Lists all storage accounts in the specified subscription."
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var tools = MakeToolDict(("storage account list", MakeTool()));

        var result = await improver.ImproveProseAsync(tools);

        Assert.Single(result);
        var tool = result.Values.First();
        Assert.Equal("Lists all storage accounts in the specified subscription.", tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_SwitchDescriptionsPreservedAsIs()
    {
        // AI response only has tool_description — switches not sent to LLM per contract
        var aiResponse = """
        {
            "tool_description": "Lists all storage accounts."
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools);

        var tool = result.Values.First();
        // Switches should be unchanged — not sent to LLM
        Assert.Equal(rawTool.Switches[0].Description, tool.Switches[0].Description);
        Assert.Equal(rawTool.Switches[1].Description, tool.Switches[1].Description);
    }

    [Fact]
    public async Task ImproveProseAsync_SwitchNamesPreserved()
    {
        var aiResponse = """
        {
            "tool_description": "Lists storage accounts."
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var tool = MakeTool();
        var tools = MakeToolDict(("storage account list", tool));

        var result = await improver.ImproveProseAsync(tools);

        var improved = result.Values.First();
        Assert.Equal("--subscription", improved.Switches[0].Name);
        Assert.Equal("--resource-group", improved.Switches[1].Name);
    }

    [Fact]
    public async Task ImproveProseAsync_ValidationFailure_FallsBackToRaw()
    {
        // AI returns markdown formatting in description — should fallback
        var aiResponse = """
        {
            "tool_description": "Lists **all** storage accounts with `az` command."
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools);

        var tool = result.Values.First();
        // Tool description should fall back to raw because it contains markdown
        Assert.Equal(rawTool.Description, tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_EmptyDescription_FallsBackToRaw()
    {
        var aiResponse = """
        {
            "tool_description": ""
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools);

        var tool = result.Values.First();
        Assert.Equal(rawTool.Description, tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_LengthViolation_FallsBackToRaw()
    {
        // >300% length of original should trigger length violation
        var rawTool = MakeTool(description: "List accounts.");
        var longDesc = new string('A', rawTool.Description.Length * 4);
        var aiResponse = $$"""
        {
            "tool_description": "{{longDesc}}"
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools);

        var tool = result.Values.First();
        Assert.Equal(rawTool.Description, tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_Timeout_FallsBackToRaw()
    {
        var improver = CreateImprover(new HangingChatClient());
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        // Use a very short timeout to trigger fallback quickly
        var result = await improver.ImproveProseAsync(tools, perToolTimeout: TimeSpan.FromMilliseconds(50));

        var tool = result.Values.First();
        Assert.Equal(rawTool.Description, tool.Description);
        Assert.Equal(rawTool.Switches[0].Description, tool.Switches[0].Description);
    }

    [Fact]
    public async Task ImproveProseAsync_AiException_FallsBackToRaw()
    {
        var improver = CreateImprover(new ThrowingChatClient());
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools);

        var tool = result.Values.First();
        Assert.Equal(rawTool.Description, tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_MalformedJsonResponse_FallsBackToRaw()
    {
        var improver = CreateImprover(new FixedResponseChatClient("This is not JSON at all!!!"));
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools);

        var tool = result.Values.First();
        Assert.Equal(rawTool.Description, tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_EmptyToolDict_ReturnsEmpty()
    {
        var improver = CreateImprover(new FixedResponseChatClient("{}"));
        var tools = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);

        var result = await improver.ImproveProseAsync(tools);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ImproveProseAsync_MultipleTools_AllProcessed()
    {
        var aiResponse1 = """
        {
            "tool_description": "Lists storage accounts."
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse1));
        var tool1 = new CliToolInfo("storage account list", "List accounts.", new[]
        {
            new CliSwitch("--subscription", "The Azure subscription ID.")
        });
        var tool2 = new CliToolInfo("storage account show", "Show an account.", new[]
        {
            new CliSwitch("--subscription", "The Azure subscription ID.")
        });
        var tools = MakeToolDict(
            ("storage account list", tool1),
            ("storage account show", tool2));

        var result = await improver.ImproveProseAsync(tools);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ImproveProseAsync_CancellationToken_Propagated()
    {
        var capturingClient = new TokenCapturingChatClient();
        var improver = CreateImprover(capturingClient);
        var tool = new CliToolInfo("test cmd", "A test tool.", new[]
        {
            new CliSwitch("--flag", "A flag.")
        });
        var tools = MakeToolDict(("test cmd", tool));

        using var cts = new CancellationTokenSource();
        await improver.ImproveProseAsync(tools, cancellationToken: cts.Token);

        Assert.True(capturingClient.CapturedToken.CanBeCanceled);
    }

    [Fact]
    public async Task ImproveProseAsync_WithNlpDescription_AdaptsVoiceDeterministically()
    {
        // NLP description starts with "This tool retrieves..." — should be converted to "Retrieves..."
        var nlpDesc = "This tool retrieves detailed information about Azure Storage accounts, including account name and location.";
        // AI will receive the voice-adapted description and return it cleaned up
        var aiResponse = """
        {
            "tool_description": "Retrieves detailed information about Azure Storage accounts, including account name and location."
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var nlpDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = nlpDesc
        };
        var tools = MakeToolDict(("storage account list", MakeTool()));

        var result = await improver.ImproveProseAsync(tools, nlpDescriptions);

        Assert.Single(result);
        var tool = result.Values.First();
        Assert.Equal("Retrieves detailed information about Azure Storage accounts, including account name and location.", tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_WithoutNlpDescription_UsesCliDescription()
    {
        var aiResponse = """
        {
            "tool_description": "Lists all storage accounts in the specified subscription."
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var tools = MakeToolDict(("storage account list", MakeTool()));

        // No NLP descriptions provided — should fall back to improving CLI description
        var result = await improver.ImproveProseAsync(tools, nlpDescriptions: null);

        Assert.Single(result);
        var tool = result.Values.First();
        Assert.Equal("Lists all storage accounts in the specified subscription.", tool.Description);
    }

    // ── AdaptNlpToCliVoice tests ─────────────────────────────────────

    [Fact]
    public void AdaptNlpToCliVoice_RemovesThisToolPrefix()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice("This tool creates a storage account.");
        Assert.Equal("Creates a storage account.", result);
    }

    [Fact]
    public void AdaptNlpToCliVoice_RemovesMcpPreamble()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice(
            "Model Context Protocol (MCP) tools let you run tasks that manage Azure resources. Creates a storage account.");
        Assert.Equal("Creates a storage account.", result);
    }

    [Fact]
    public void AdaptNlpToCliVoice_ReplacesMcpServerReference()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice("Gets details from the MCP Server about accounts.");
        Assert.Equal("Gets details from the Azure MCP CLI about accounts.", result);
    }

    [Fact]
    public void AdaptNlpToCliVoice_NoChangeNeeded_ReturnsAsIs()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice("Creates a storage account in the specified region.");
        Assert.Equal("Creates a storage account in the specified region.", result);
    }
}
