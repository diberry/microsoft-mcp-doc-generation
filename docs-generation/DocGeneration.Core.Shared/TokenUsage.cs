using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared;

/// <summary>
/// Records token usage from a single Azure OpenAI API call.
/// Captured per-tool so cost and consumption can be attributed.
/// </summary>
public sealed class TokenUsageRecord
{
    /// <summary>Number of tokens in the prompt (input).</summary>
    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; set; }

    /// <summary>Number of tokens in the completion (output).</summary>
    [JsonPropertyName("completionTokens")]
    public int CompletionTokens { get; set; }

    /// <summary>Total tokens consumed (prompt + completion).</summary>
    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }

    /// <summary>Model deployment name (e.g., "gpt-4o-mini").</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>Which tool or namespace this call was for (e.g., "deploy_create").</summary>
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = "";
}

/// <summary>
/// Aggregated token usage across all AI calls within a pipeline step.
/// Attached to <see cref="StepResultFile.TokenUsage"/> for observability.
/// </summary>
public sealed class TokenUsageSummary
{
    /// <summary>Sum of prompt tokens across all calls.</summary>
    [JsonPropertyName("totalPromptTokens")]
    public int TotalPromptTokens { get; set; }

    /// <summary>Sum of completion tokens across all calls.</summary>
    [JsonPropertyName("totalCompletionTokens")]
    public int TotalCompletionTokens { get; set; }

    /// <summary>Sum of total tokens across all calls.</summary>
    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }

    /// <summary>Number of AI API calls made.</summary>
    [JsonPropertyName("callCount")]
    public int CallCount { get; set; }

    /// <summary>Individual call records for per-tool attribution.</summary>
    [JsonPropertyName("calls")]
    public List<TokenUsageRecord> Calls { get; set; } = new();

    /// <summary>
    /// Records a single AI call and updates running totals.
    /// </summary>
    public void AddCall(TokenUsageRecord record)
    {
        Calls.Add(record);
        TotalPromptTokens += record.PromptTokens;
        TotalCompletionTokens += record.CompletionTokens;
        TotalTokens += record.TotalTokens;
        CallCount++;
    }
}
