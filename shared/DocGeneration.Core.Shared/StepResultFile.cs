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
/// Validation outcome for a step's output artifacts.
/// Serializes as lowercase strings in JSON (e.g., "passed", "failed", "skipped").
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ValidationStatus>))]
public enum ValidationStatus
{
    /// <summary>All validation checks passed.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>One or more validation checks failed.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Validation was not applicable or was skipped.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped
}

/// <summary>
/// A reference to an artifact with its file path and content hash.
/// Used for input/output artifact tracking in the step envelope.
/// </summary>
public sealed class ArtifactReference
{
    /// <summary>Relative or absolute path to the artifact file.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    /// <summary>SHA-256 hex digest of the artifact content.</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
}

/// <summary>
/// Lightweight token usage summary for a single step execution.
/// Distinct from <see cref="TokenUsageSummary"/> which aggregates multiple per-tool calls.
/// Null for deterministic (non-AI) steps.
/// </summary>
public sealed class TokenUsageEnvelope
{
    /// <summary>Total prompt tokens consumed across all AI calls in this step.</summary>
    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; set; }

    /// <summary>Total completion tokens produced across all AI calls in this step.</summary>
    [JsonPropertyName("completionTokens")]
    public int CompletionTokens { get; set; }
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
///
/// Envelope extension (Phase 1 Point 3) adds: schemaVersion, stepName, inputArtifacts,
/// outputArtifacts, validationStatus, tokenUsageEnvelope, promptArchivePath, durationMs, timestamp.
/// All new fields are nullable/optional for full backward compatibility with legacy v0 files.
/// </summary>
public class StepResultFile
{
    // Two version fields intentionally coexist (see #638 item 6):
    //   • Version (int)        — legacy CONTENT-shape revision of this file, incremented as
    //                            generator output grew (v1 base, v2 added promptSnapshots,
    //                            v3 added tokenUsage). Predates the typed envelope.
    //   • SchemaVersion (string) — semantic ENVELOPE schema version ("1.0") introduced in the
    //                            Phase 1 Point 3 envelope extension and validated by
    //                            StepResultReader. Null/empty means "legacy, no envelope".
    // They are not redundant: Version tracks the historical field set; SchemaVersion gates the
    // envelope contract. Keep both until the legacy integer scheme is fully retired.

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

    // ── Phase 1 Point 3: Envelope extension fields ────────────────────────────

    /// <summary>
    /// Semantic schema version string (e.g., "1.0"). Separate from the integer <see cref="Version"/> field.
    /// Absent (null) in legacy v0 files — treated as legacy without error.
    /// Present and unrecognized → <see cref="StepResultSchemaException"/> is thrown on read.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }

    /// <summary>Canonical step name for cross-step lookups (e.g., "step-3-tool-generation").</summary>
    [JsonPropertyName("stepName")]
    public string? StepName { get; set; }

    /// <summary>
    /// Input artifacts consumed by this step, each with a path and SHA-256 hash.
    /// Null when not tracked (legacy steps or deterministic steps without artifact tracking).
    /// </summary>
    [JsonPropertyName("inputArtifacts")]
    public List<ArtifactReference>? InputArtifacts { get; set; }

    /// <summary>
    /// Output artifacts produced by this step, each with a path and SHA-256 hash.
    /// Null when not tracked.
    /// </summary>
    [JsonPropertyName("outputArtifacts")]
    public List<ArtifactReference>? OutputArtifacts { get; set; }

    /// <summary>
    /// Result of post-step validation. Null when validation was not run.
    /// </summary>
    [JsonPropertyName("validationStatus")]
    public ValidationStatus? ValidationStatus { get; set; }

    /// <summary>
    /// Lightweight token usage envelope for this step. Null for deterministic (non-AI) steps.
    /// Distinct from <see cref="TokenUsage"/> which carries per-tool call detail.
    /// </summary>
    [JsonPropertyName("tokenUsageEnvelope")]
    public TokenUsageEnvelope? TokenUsageEnvelope { get; set; }

    /// <summary>Path to the archived prompt package for this step. Null for deterministic steps.</summary>
    [JsonPropertyName("promptArchivePath")]
    public string? PromptArchivePath { get; set; }

    /// <summary>Wall-clock duration in milliseconds. Complements the <see cref="Duration"/> string field.</summary>
    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    /// <summary>Step completion timestamp in ISO 8601 format (e.g., "2026-05-29T09:35:22Z").</summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

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