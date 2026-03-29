# Prompt Versioning

Track which prompt content was used for each pipeline step execution, enabling reproducibility and regression detection when prompts change.

## Overview

AI-enhanced pipeline steps (2, 3, 4, 6) depend on prompt files that guide Azure OpenAI output quality and structure. When a prompt changes, the generated documentation changes too. Prompt versioning answers two questions:

1. **What prompt content produced this output?** — Each step result records SHA256 hashes of the prompts used.
2. **Did a prompt actually change between runs?** — Comparing hashes across runs detects content drift, even when file timestamps or formatting change.

Hashing happens **after** token resolution, so the hash reflects the actual content sent to the AI — including expanded shared includes like Acrolinx rules.

## How It Works

```
prompt file on disk
       │
       ▼
PromptTokenResolver.Resolve()   ← expands {{ACROLINX_RULES}} etc.
       │
       ▼
PromptHasher.ComputeHash()      ← SHA256 of resolved content
       │
       ▼
PromptSnapshot record            ← fileName, contentHash, sizeBytes, lastModified
       │
       ▼
StepResultFile.PromptSnapshots   ← attached to step-result.json (v2 schema)
```

## Key Components

All types live in `DocGeneration.Core.Shared` (namespace `Shared`).

### PromptHasher

Static utility that computes SHA256 hashes for prompt content and files.

```csharp
public static class PromptHasher
{
    // Returns a lowercase 64-character hex string (SHA256).
    public static string ComputeHash(string content);

    // Reads a file, optionally resolves tokens via PromptTokenResolver,
    // then returns a PromptSnapshot with the content hash and file metadata.
    // Pass dataDir to enable token resolution; null hashes raw content.
    public static async Task<PromptSnapshot> HashFileAsync(
        string filePath, string? dataDir = null);
}
```

- **`ComputeHash(string content)`** — Pure function. Encodes the string as UTF-8, computes SHA256, returns the hash as a lowercase hex string (64 characters).
- **`HashFileAsync(string filePath, string? dataDir)`** — Reads the file, optionally runs `PromptTokenResolver.Resolve()` when `dataDir` is provided, hashes the resolved content, and returns a `PromptSnapshot` with file metadata.

### PromptSnapshot

Immutable record capturing a prompt file's identity at a point in time.

```csharp
public sealed record PromptSnapshot(
    string FileName,        // e.g. "system-prompt.txt"
    string ContentHash,     // SHA256 hex string (64 chars)
    long SizeBytes,         // Raw file size on disk
    DateTimeOffset LastModified);  // File modification time in UTC
```

### PromptTokenResolver

Expands shared tokens in prompt text before hashing. Currently supports one token:

| Token | Source file | Purpose |
|-------|------------|---------|
| `{{ACROLINX_RULES}}` | `data/shared-acrolinx-rules.txt` | Canonical Acrolinx compliance rules shared across all AI steps |

```csharp
public static class PromptTokenResolver
{
    // Replaces {{ACROLINX_RULES}} with content from shared-acrolinx-rules.txt.
    // Caches the file content after first read (thread-safe).
    public static string Resolve(string prompt, string dataDir);
}
```

**Why resolve before hashing?** If the Acrolinx rules file changes, every prompt that includes `{{ACROLINX_RULES}}` gets a new hash — correctly signaling that the effective prompt content changed, even though the prompt file itself didn't.

## StepResultFile v2 Schema

`StepResultFile` gained a nullable `PromptSnapshots` property in schema version 2.

```json
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
    {
      "fileName": "system-prompt.txt",
      "contentHash": "a1b2c3d4...64 hex chars",
      "sizeBytes": 1024
    }
  ]
}
```

### Backward compatibility

- **v1 → v2 reading**: The `PromptSnapshots` property is `List<PromptSnapshotRecord>?` (nullable). Files written without `promptSnapshots` deserialize cleanly — the property stays `null`.
- **v2 → v1 reading**: Consumers that don't know about `promptSnapshots` simply ignore the unknown JSON property (default `System.Text.Json` behavior).
- The `Version` field defaults to `1`, so only results that explicitly attach snapshots carry `"version": 2`.

### PromptSnapshotRecord (serialization type)

`StepResultFile.PromptSnapshotRecord` is the JSON-serializable version of `PromptSnapshot`. It omits `LastModified` (not useful in persisted results).

```csharp
public sealed class PromptSnapshotRecord
{
    public string FileName { get; set; }      // "system-prompt.txt"
    public string ContentHash { get; set; }   // SHA256 hex
    public long SizeBytes { get; set; }       // raw file size
}
```

## StepResultWriter Helper

`StepResultWriter.AddPromptSnapshots()` converts `PromptSnapshot` records to `PromptSnapshotRecord` entries and attaches them to a `StepResultFile`, setting the version to 2:

```csharp
public static class StepResultWriter
{
    public const string FileName = "step-result.json";

    public static void Write(string directory, StepResultFile result);

    // Converts PromptSnapshot → PromptSnapshotRecord, attaches to result,
    // and bumps version to 2.
    public static void AddPromptSnapshots(
        StepResultFile result, IEnumerable<PromptSnapshot> snapshots);
}
```

## Usage in a Pipeline Step

A pipeline step that uses AI prompts would integrate prompt versioning like this:

```csharp
// 1. Hash each prompt file (with token resolution)
var systemSnapshot = await PromptHasher.HashFileAsync(
    Path.Combine(promptsDir, "system-prompt.txt"),
    dataDir: dataDirectory);

var userSnapshot = await PromptHasher.HashFileAsync(
    Path.Combine(promptsDir, "user-prompt.txt"),
    dataDir: dataDirectory);

// 2. Attach snapshots to the step result
var result = new StepResultFile
{
    Status = StepResultStatus.Success,
    Step = "Step 3 - Tool Generation",
    Namespace = namespaceName,
    OutputFileCount = generatedFiles.Count
};

StepResultWriter.AddPromptSnapshots(result, [systemSnapshot, userSnapshot]);

// 3. Write the result file (now includes promptSnapshots at version 2)
StepResultWriter.Write(outputDirectory, result);
```

## Future: Pipeline Integration

The prompt versioning infrastructure is in place but **steps don't yet call `PromptHasher` at runtime**. Wiring each step's prompt loading to also hash and record snapshots is future work. When implemented:

- Every AI step (2, 3, 4, 6) will record its prompt hashes in `step-result.json`.
- The pipeline runner can compare hashes across runs to detect prompt drift.
- CI can flag builds where prompts changed but baselines weren't updated, complementing the [prompt regression testing framework](../docs-generation/DocGeneration.PromptRegression.Tests/README.md).

## Related

- [ARCHITECTURE.md](ARCHITECTURE.md) — Pipeline step details and data flow
- [Prompt regression testing](../docs-generation/DocGeneration.PromptRegression.Tests/README.md) — Baseline comparison for output quality
- [FINGERPRINTING.md](FINGERPRINTING.md) — Snapshot and diff generated output for regression detection
- Source: `docs-generation/DocGeneration.Core.Shared/PromptHasher.cs`
- Source: `docs-generation/DocGeneration.Core.Shared/StepResultFile.cs`
- Source: `docs-generation/DocGeneration.Core.Shared/StepResultWriter.cs`
- Source: `docs-generation/DocGeneration.Core.Shared/PromptTokenResolver.cs`
