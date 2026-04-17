using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

/// <summary>
/// Status of a pipeline step execution.
/// Serializes as lowercase strings in JSON (e.g., "success", "failure", "partial").
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepResultStatus>))]
public enum StepResultStatus
{
    /// <summary>All outputs generated without errors.</summary>
    [JsonStringEnumMemberName("success")]
    Success,

    /// <summary>Step failed completely.</summary>
    [JsonStringEnumMemberName("failure")]
    Failure,

    /// <summary>Some outputs generated, but with errors or missing files.</summary>
    [JsonStringEnumMemberName("partial")]
    Partial
}

/// <summary>
/// Structured result written by generator processes as step-result.json.
/// Replaces regex-based subprocess error detection with a typed contract.
///
/// Schema v3 (added tokenUsage):
/// {
///   "version": 3,
///   "status": "success|failure|partial",
///   "step": "Step 3 - Tool Generation",
///   "namespace": "deploy",
///   "outputFileCount": 5,
///   "warnings": ["warning 1"],
///   "errors": ["error 1"],
///   "duration": "00:02:15.123",
///   "promptSnapshots": [{ "fileName": "...", "contentHash": "...", "sizeBytes": 1024 }],
///   "tokenUsage": { "totalPromptTokens": 500, "totalCompletionTokens": 200, ... }
/// }
/// </summary>
public class StepResultFile
{
    /// <summary>Schema version for forward compatibility. v2: added promptSnapshots. v3: added tokenUsage.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>Overall status: success, failure, or partial.</summary>
    [JsonPropertyName("status")]
    public StepResultStatus Status { get; set; } = StepResultStatus.Failure;

    /// <summary>Human-readable step name (e.g., "Step 3 - Tool Generation").</summary>
    [JsonPropertyName("step")]
    public string Step { get; set; } = "";

    /// <summary>MCP namespace this step ran for (e.g., "deploy", "storage").</summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "";

    /// <summary>Number of output files successfully generated.</summary>
    [JsonPropertyName("outputFileCount")]
    public int OutputFileCount { get; set; }

    /// <summary>Non-fatal warnings encountered during execution.</summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>Fatal errors encountered during execution.</summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    /// <summary>Wall-clock duration as a TimeSpan string (e.g., "00:02:15.123").</summary>
    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "";

    /// <summary>
    /// v2: SHA256 snapshots of prompt files used during step execution.
    /// Nullable for backward compatibility - v1 result files omit this field.
    /// </summary>
    [JsonPropertyName("promptSnapshots")]
    public List<PromptSnapshotRecord>? PromptSnapshots { get; set; }

    /// <summary>
    /// v3: Aggregated token usage from Azure OpenAI calls during step execution.
    /// Nullable for backward compatibility - non-AI steps and v1/v2 results omit this field.
    /// </summary>
    [JsonPropertyName("tokenUsage")]
    public TokenUsageSummary? TokenUsage { get; set; }

    /// <summary>
    /// Serialization-friendly record for prompt file metadata.
    /// Maps from <see cref="PromptSnapshot"/> but omits LastModified (not needed in JSON).
    /// </summary>
    public sealed class PromptSnapshotRecord
    {
        /// <summary>Prompt filename (e.g., "system-prompt.txt").</summary>
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        /// <summary>SHA256 hash of post-token-resolution content.</summary>
        [JsonPropertyName("contentHash")]
        public string ContentHash { get; set; } = "";

        /// <summary>Raw file size in bytes.</summary>
        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; set; }
    }
}