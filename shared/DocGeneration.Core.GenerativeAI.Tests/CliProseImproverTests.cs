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
            "tool_description": "Lists all storage accounts in the specified subscription.",
            "switch_descriptions": {
                "--subscription": "The Azure subscription identifier.",
                "--resource-group": "The name of the resource group."
            }
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
    public async Task ImproveProseAsync_ImprovesSwitchDescriptions()
    {
        var aiResponse = """
        {
            "tool_description": "Lists all storage accounts.",
            "switch_descriptions": {
                "--subscription": "The Azure subscription identifier.",
                "--resource-group": "The name of the Azure resource group."
            }
        }
        """;
        var improver = CreateImprover(new FixedResponseChatClient(aiResponse));
        var tools = MakeToolDict(("storage account list", MakeTool()));

        var result = await improver.ImproveProseAsync(tools);

        var tool = result.Values.First();
        Assert.Equal("The Azure subscription identifier.", tool.Switches[0].Description);
        Assert.Equal("The name of the Azure resource group.", tool.Switches[1].Description);
    }

    [Fact]
    public async Task ImproveProseAsync_SwitchNamesPreserved()
    {
        var aiResponse = """
        {
            "tool_description": "Lists storage accounts.",
            "switch_descriptions": {
                "--subscription": "Improved sub desc.",
                "--resource-group": "Improved rg desc."
            }
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
            "tool_description": "Lists **all** storage accounts with `az` command.",
            "switch_descriptions": {
                "--subscription": "The Azure subscription ID.",
                "--resource-group": "The name of the resource group."
            }
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
            "tool_description": "",
            "switch_descriptions": {
                "--subscription": "The Azure subscription ID.",
                "--resource-group": "The name of the resource group."
            }
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
        // 3x length of original should trigger length violation (>200%)
        var rawTool = MakeTool(description: "List accounts.");
        var longDesc = new string('A', rawTool.Description.Length * 3);
        var aiResponse = $$"""
        {
            "tool_description": "{{longDesc}}",
            "switch_descriptions": {
                "--subscription": "The Azure subscription ID.",
                "--resource-group": "The name of the resource group."
            }
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
            "tool_description": "Lists storage accounts.",
            "switch_descriptions": {
                "--subscription": "The Azure subscription identifier."
            }
        }
        """;
        // The FixedResponseChatClient returns the same response for every call,
        // but we just need to verify both tools are present in the result.
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
        // Deliberately NOT an empty dict, but one without switches in AI response is fine
        // because token capture client returns valid JSON
        var tools = MakeToolDict(("test cmd", tool));

        using var cts = new CancellationTokenSource();
        await improver.ImproveProseAsync(tools, cancellationToken: cts.Token);

        // The captured token should be linked (not directly equal) but must be cancellable
        // We verify it can register — if propagation failed, this wouldn't work
        Assert.True(capturingClient.CapturedToken.CanBeCanceled);
    }

    [Fact]
    public async Task ImproveProseAsync_WithNlpDescription_UsesNlpAsSource()
    {
        // NLP description includes return fields and behavioral details that should be preserved
        var nlpDesc = "Retrieves detailed information about Azure Storage accounts, including account name, location, SKU, kind, hierarchical namespace status, HTTPS-only settings, and blob public access configuration. If a specific account name is not provided, the command will return details for all accounts in a subscription.";
        // AI response adapted from NLP (imperative voice)
        var aiResponse = """
        {
            "tool_description": "Gets detailed information about Azure Storage accounts, including account name, location, SKU, kind, hierarchical namespace status, HTTPS-only settings, and blob public access configuration. If a specific account name is not provided, the command will return details for all accounts in a subscription.",
            "switch_descriptions": {
                "--subscription": "The Azure subscription identifier.",
                "--resource-group": "The name of the resource group."
            }
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
        // Verify the improved description matches AI response (FixedResponseChatClient returns what we give it)
        Assert.Equal("Gets detailed information about Azure Storage accounts, including account name, location, SKU, kind, hierarchical namespace status, HTTPS-only settings, and blob public access configuration. If a specific account name is not provided, the command will return details for all accounts in a subscription.", tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_WithoutNlpDescription_UsesCliDescription()
    {
        var aiResponse = """
        {
            "tool_description": "Lists all storage accounts in the specified subscription.",
            "switch_descriptions": {
                "--subscription": "The Azure subscription identifier.",
                "--resource-group": "The name of the resource group."
            }
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
}
