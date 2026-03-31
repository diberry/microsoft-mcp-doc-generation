# Token Usage Tracking

Track Azure OpenAI token consumption per pipeline step for cost observability and optimization.

## Overview

AI-enhanced pipeline steps (2, 3, 4, 6) call Azure OpenAI for each tool they process. Token usage tracking captures prompt tokens, completion tokens, and model information for every API call, then aggregates them per step in `step-result.json`.

## Data Model

### TokenUsageRecord

Captures a single Azure OpenAI API call:

```json
{
  "promptTokens": 500,
  "completionTokens": 200,
  "totalTokens": 700,
  "model": "gpt-4o-mini",
  "toolName": "deploy_create"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `promptTokens` | int | Tokens in the input prompt |
| `completionTokens` | int | Tokens in the AI response |
| `totalTokens` | int | Sum of prompt + completion |
| `model` | string | Model deployment name |
| `toolName` | string | Tool/namespace the call was for |

### TokenUsageSummary

Aggregates all AI calls within a step:

```json
{
  "totalPromptTokens": 1500,
  "totalCompletionTokens": 800,
  "totalTokens": 2300,
  "callCount": 5,
  "calls": [ ... ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `totalPromptTokens` | int | Sum of prompt tokens across all calls |
| `totalCompletionTokens` | int | Sum of completion tokens across all calls |
| `totalTokens` | int | Sum of total tokens across all calls |
| `callCount` | int | Number of AI API calls made |
| `calls` | array | Individual `TokenUsageRecord` entries |

## StepResultFile v3 Schema

`StepResultFile` gained a nullable `TokenUsage` property in schema version 3.

```json
{
  "version": 3,
  "status": "success",
  "step": "Step 3 - Tool Generation",
  "namespace": "deploy",
  "outputFileCount": 5,
  "warnings": [],
  "errors": [],
  "duration": "00:02:15.123",
  "promptSnapshots": [ ... ],
  "tokenUsage": {
    "totalPromptTokens": 1500,
    "totalCompletionTokens": 800,
    "totalTokens": 2300,
    "callCount": 5,
    "calls": [
      {
        "promptTokens": 500,
        "completionTokens": 200,
        "totalTokens": 700,
        "model": "gpt-4o-mini",
        "toolName": "deploy_create"
      }
    ]
  }
}
```

### Backward Compatibility

- **v1/v2 → v3 reading**: The `TokenUsage` property is `TokenUsageSummary?` (nullable). Files without `tokenUsage` deserialize cleanly — the property stays `null`.
- **v3 → v1/v2 reading**: Consumers that don't know about `tokenUsage` ignore the unknown JSON property (default `System.Text.Json` behavior).
- Non-AI steps (Step 0, Step 1, Step 5) won't populate `tokenUsage` — it remains `null`.

## Usage

```csharp
// Create a summary and record calls
var usage = new TokenUsageSummary();

usage.AddCall(new TokenUsageRecord
{
    PromptTokens = 500,
    CompletionTokens = 200,
    TotalTokens = 700,
    Model = "gpt-4o-mini",
    ToolName = "deploy_create"
});

// Attach to step result
var result = new StepResultFile
{
    Version = 3,
    Status = StepResultStatus.Success,
    Step = "Step 3 - Tool Generation",
    Namespace = "deploy",
    OutputFileCount = 5,
    TokenUsage = usage
};
```

## Future: AI Client Integration

The token usage infrastructure is in place but **AI clients don't yet record usage at runtime**. Wiring `GenerativeAIClient` to capture token counts from Azure OpenAI SDK responses is follow-up work. When implemented:

- Each AI step (2, 3, 4, 6) will record token usage in `step-result.json`.
- The pipeline runner can aggregate usage across all steps for a full run summary.
- Teams can monitor cost trends per namespace and per step.

## Related

- [Prompt versioning](prompt-versioning.md) — Prompt hash tracking (v2 schema)
- [ARCHITECTURE.md](ARCHITECTURE.md) — Pipeline step details and data flow
- Source: `docs-generation/DocGeneration.Core.Shared/TokenUsage.cs`
- Source: `docs-generation/DocGeneration.Core.Shared/StepResultFile.cs`
