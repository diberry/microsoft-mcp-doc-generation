using System.Text.Json;
using Xunit;
using Shared;

namespace Shared.Tests;

public class TokenUsageRecordTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var record = new TokenUsageRecord();

        Assert.Equal(0, record.PromptTokens);
        Assert.Equal(0, record.CompletionTokens);
        Assert.Equal(0, record.TotalTokens);
        Assert.Equal("", record.Model);
        Assert.Equal("", record.ToolName);
    }

    [Fact]
    public void Serialization_RoundTrips_AllFields()
    {
        var original = new TokenUsageRecord
        {
            PromptTokens = 150,
            CompletionTokens = 80,
            TotalTokens = 230,
            Model = "gpt-4o-mini",
            ToolName = "deploy_get"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TokenUsageRecord>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.PromptTokens, deserialized!.PromptTokens);
        Assert.Equal(original.CompletionTokens, deserialized.CompletionTokens);
        Assert.Equal(original.TotalTokens, deserialized.TotalTokens);
        Assert.Equal(original.Model, deserialized.Model);
        Assert.Equal(original.ToolName, deserialized.ToolName);
    }

    [Fact]
    public void Serialization_UsesExpectedPropertyNames()
    {
        var record = new TokenUsageRecord
        {
            PromptTokens = 10,
            CompletionTokens = 20,
            TotalTokens = 30,
            Model = "gpt-4o",
            ToolName = "storage_list"
        };

        var json = JsonSerializer.Serialize(record);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("promptTokens", out _));
        Assert.True(root.TryGetProperty("completionTokens", out _));
        Assert.True(root.TryGetProperty("totalTokens", out _));
        Assert.True(root.TryGetProperty("model", out _));
        Assert.True(root.TryGetProperty("toolName", out _));
    }
}

public class TokenUsageSummaryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var summary = new TokenUsageSummary();

        Assert.Equal(0, summary.TotalPromptTokens);
        Assert.Equal(0, summary.TotalCompletionTokens);
        Assert.Equal(0, summary.TotalTokens);
        Assert.Equal(0, summary.CallCount);
        Assert.NotNull(summary.Calls);
        Assert.Empty(summary.Calls);
    }

    [Fact]
    public void AddCall_SingleCall_AccumulatesTotals()
    {
        var summary = new TokenUsageSummary();
        var record = new TokenUsageRecord
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150,
            Model = "gpt-4o-mini",
            ToolName = "cosmos_query"
        };

        summary.AddCall(record);

        Assert.Equal(100, summary.TotalPromptTokens);
        Assert.Equal(50, summary.TotalCompletionTokens);
        Assert.Equal(150, summary.TotalTokens);
        Assert.Equal(1, summary.CallCount);
        Assert.Single(summary.Calls);
        Assert.Same(record, summary.Calls[0]);
    }

    [Fact]
    public void AddCall_MultipleCalls_AccumulatesTotals()
    {
        var summary = new TokenUsageSummary();

        summary.AddCall(new TokenUsageRecord
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150,
            Model = "gpt-4o-mini",
            ToolName = "storage_list"
        });

        summary.AddCall(new TokenUsageRecord
        {
            PromptTokens = 200,
            CompletionTokens = 120,
            TotalTokens = 320,
            Model = "gpt-4o-mini",
            ToolName = "keyvault_get"
        });

        summary.AddCall(new TokenUsageRecord
        {
            PromptTokens = 80,
            CompletionTokens = 40,
            TotalTokens = 120,
            Model = "gpt-4o",
            ToolName = "monitor_query"
        });

        Assert.Equal(380, summary.TotalPromptTokens);   // 100 + 200 + 80
        Assert.Equal(210, summary.TotalCompletionTokens); // 50 + 120 + 40
        Assert.Equal(590, summary.TotalTokens);           // 150 + 320 + 120
        Assert.Equal(3, summary.CallCount);
        Assert.Equal(3, summary.Calls.Count);
    }

    [Fact]
    public void Serialization_EmptySummary_RoundTripsCorrectly()
    {
        var original = new TokenUsageSummary();

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TokenUsageSummary>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(0, deserialized!.TotalPromptTokens);
        Assert.Equal(0, deserialized.TotalCompletionTokens);
        Assert.Equal(0, deserialized.TotalTokens);
        Assert.Equal(0, deserialized.CallCount);
        Assert.Empty(deserialized.Calls);
    }

    [Fact]
    public void Serialization_WithMultipleCalls_RoundTripsCorrectly()
    {
        var original = new TokenUsageSummary();
        original.AddCall(new TokenUsageRecord
        {
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150,
            Model = "gpt-4o-mini",
            ToolName = "deploy_create"
        });
        original.AddCall(new TokenUsageRecord
        {
            PromptTokens = 200,
            CompletionTokens = 80,
            TotalTokens = 280,
            Model = "gpt-4o",
            ToolName = "speech_recognize"
        });

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TokenUsageSummary>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.TotalPromptTokens, deserialized!.TotalPromptTokens);
        Assert.Equal(original.TotalCompletionTokens, deserialized.TotalCompletionTokens);
        Assert.Equal(original.TotalTokens, deserialized.TotalTokens);
        Assert.Equal(original.CallCount, deserialized.CallCount);
        Assert.Equal(2, deserialized.Calls.Count);

        // Verify individual call records survived round-trip
        Assert.Equal("deploy_create", deserialized.Calls[0].ToolName);
        Assert.Equal(100, deserialized.Calls[0].PromptTokens);
        Assert.Equal("speech_recognize", deserialized.Calls[1].ToolName);
        Assert.Equal(200, deserialized.Calls[1].PromptTokens);
    }

    [Fact]
    public void Serialization_UsesExpectedPropertyNames()
    {
        var summary = new TokenUsageSummary();
        summary.AddCall(new TokenUsageRecord
        {
            PromptTokens = 10,
            CompletionTokens = 5,
            TotalTokens = 15,
            Model = "gpt-4o",
            ToolName = "test"
        });

        var json = JsonSerializer.Serialize(summary);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("totalPromptTokens", out _));
        Assert.True(root.TryGetProperty("totalCompletionTokens", out _));
        Assert.True(root.TryGetProperty("totalTokens", out _));
        Assert.True(root.TryGetProperty("callCount", out _));
        Assert.True(root.TryGetProperty("calls", out _));
    }
}

public class StepResultFileTokenUsageTests
{
    [Fact]
    public void StepResultFile_WithTokenUsage_RoundTripsCorrectly()
    {
        var original = new StepResultFile
        {
            Version = 3,
            Status = StepResultStatus.Success,
            Step = "Step 3 - Tool Generation",
            Namespace = "deploy",
            OutputFileCount = 5,
            Duration = "00:02:15.123",
            TokenUsage = new TokenUsageSummary()
        };

        original.TokenUsage.AddCall(new TokenUsageRecord
        {
            PromptTokens = 500,
            CompletionTokens = 200,
            TotalTokens = 700,
            Model = "gpt-4o-mini",
            ToolName = "deploy_create"
        });

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.TokenUsage);
        Assert.Equal(500, deserialized.TokenUsage!.TotalPromptTokens);
        Assert.Equal(200, deserialized.TokenUsage.TotalCompletionTokens);
        Assert.Equal(700, deserialized.TokenUsage.TotalTokens);
        Assert.Equal(1, deserialized.TokenUsage.CallCount);
        Assert.Equal("deploy_create", deserialized.TokenUsage.Calls[0].ToolName);
    }

    [Fact]
    public void StepResultFile_WithoutTokenUsage_BackwardCompatible()
    {
        // v1/v2 JSON without tokenUsage field — must deserialize cleanly
        var json = """
        {
          "version": 2,
          "status": "success",
          "step": "Step 3 - Tool Generation",
          "namespace": "deploy",
          "outputFileCount": 5,
          "warnings": [],
          "errors": [],
          "duration": "00:02:15.123",
          "promptSnapshots": [
            { "fileName": "system-prompt.txt", "contentHash": "abc123", "sizeBytes": 1024 }
          ]
        }
        """;

        var result = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(result);
        Assert.Null(result!.TokenUsage);
        Assert.Equal(2, result.Version);
        Assert.Equal(StepResultStatus.Success, result.Status);
        Assert.NotNull(result.PromptSnapshots);
        Assert.Single(result.PromptSnapshots!);
    }

    [Fact]
    public void StepResultFile_V1_WithoutTokenUsage_StillWorks()
    {
        // Bare v1 JSON — no promptSnapshots, no tokenUsage
        var json = """
        {
          "version": 1,
          "status": "failure",
          "step": "Step 2 - Example Prompts",
          "namespace": "storage",
          "outputFileCount": 0,
          "warnings": ["warning 1"],
          "errors": ["error 1"],
          "duration": "00:00:30.000"
        }
        """;

        var result = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(result);
        Assert.Null(result!.TokenUsage);
        Assert.Null(result.PromptSnapshots);
        Assert.Equal(1, result.Version);
        Assert.Equal(StepResultStatus.Failure, result.Status);
    }

    [Fact]
    public void StepResultFile_TokenUsage_AppearsInJsonSchema()
    {
        var result = new StepResultFile
        {
            Version = 3,
            Status = StepResultStatus.Success,
            Step = "Step 6 - Horizontal Articles",
            Namespace = "advisor",
            OutputFileCount = 1,
            Duration = "00:01:00.000",
            TokenUsage = new TokenUsageSummary()
        };

        result.TokenUsage.AddCall(new TokenUsageRecord
        {
            PromptTokens = 300,
            CompletionTokens = 150,
            TotalTokens = 450,
            Model = "gpt-4o",
            ToolName = "advisor_get"
        });

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("tokenUsage", out var tokenUsageElement));
        Assert.True(tokenUsageElement.TryGetProperty("totalPromptTokens", out _));
        Assert.True(tokenUsageElement.TryGetProperty("totalCompletionTokens", out _));
        Assert.True(tokenUsageElement.TryGetProperty("totalTokens", out _));
        Assert.True(tokenUsageElement.TryGetProperty("callCount", out _));
        Assert.True(tokenUsageElement.TryGetProperty("calls", out var callsElement));
        Assert.Equal(JsonValueKind.Array, callsElement.ValueKind);
        Assert.Equal(1, callsElement.GetArrayLength());
    }

    [Fact]
    public void StepResultFile_NullTokenUsage_OmittedOrNullInJson()
    {
        var result = new StepResultFile
        {
            Version = 2,
            Status = StepResultStatus.Success,
            Step = "Step 1 - Annotations",
            Namespace = "compute",
            OutputFileCount = 10,
            Duration = "00:00:05.000",
            TokenUsage = null
        };

        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<StepResultFile>(json);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.TokenUsage);
    }
}
