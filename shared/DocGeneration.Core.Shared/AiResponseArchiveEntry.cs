// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;

namespace Shared;

/// <summary>
/// Represents a single archived AI response for audit trail purposes.
/// Captures the raw response, prompt hash, token usage, and metadata
/// so that AI interactions can be reviewed and debugged after generation.
/// </summary>
public sealed class AiResponseArchiveEntry
{
    /// <summary>Pipeline step that produced this response (e.g., "step2", "step3").</summary>
    [JsonPropertyName("step")]
    public string Step { get; set; } = "";

    /// <summary>Tool name the AI call was made for (e.g., "storage_blob_list").</summary>
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = "";

    /// <summary>SHA256 hash of the prompt sent to the AI model.</summary>
    [JsonPropertyName("promptHash")]
    public string PromptHash { get; set; } = "";

    /// <summary>Raw response text from the AI model, before any parsing or transformation.</summary>
    [JsonPropertyName("rawResponse")]
    public string RawResponse { get; set; } = "";

    /// <summary>AI model deployment name (e.g., "gpt-4o-mini").</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>UTC timestamp when the AI response was received.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Number of tokens in the prompt sent to the model.</summary>
    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; set; }

    /// <summary>Number of tokens in the model's completion response.</summary>
    [JsonPropertyName("completionTokens")]
    public int CompletionTokens { get; set; }
}
