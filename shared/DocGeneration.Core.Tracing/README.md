# DocGeneration.Core.Tracing

Shared tracing infrastructure for pipeline observability across the MCP and Skills pipelines.

## Problem statement

Long-running documentation pipelines were difficult to diagnose when a step failed, was cancelled, or produced unexpected AI output. This package captures a single run in memory and flushes a small set of trace artifacts at the end so developers can inspect step timing, AI prompts/responses, and a human-readable summary without re-running the pipeline blindly.

## Output files

| File | Description | Typical use |
|------|-------------|-------------|
| `pipeline-trace.json` | Structured run metadata, including ordered step events, status, duration, and targets | Programmatic analysis, tooling, diffing runs |
| `ai-interactions.json` | Ordered AI prompt/response records with model, retry count, duration, and token usage when available | Prompt debugging, cost/perf analysis |
| `summary.md` | Lightweight markdown summary of the run, steps, AI totals, and errors | Fast human review in PRs or local runs |

## Key types

- `IPipelineTracer` / `IStepHandle`
- `PipelineTracer`
- `NullTracer`
- `TraceWriter`
- `Models/TraceEvent`, `Models/AiInteraction`, `Models/PipelineTrace`

## Complete usage example

```csharp
IPipelineTracer tracer = tracingEnabled
    ? new PipelineTracer("mcp-pipeline")
    : NullTracer.Instance;

IStepHandle? step = null;

try
{
    step = tracer.StartStep("rewrite", StepClassification.AI, targetName: namespaceName);

    var response = await client.GetChatCompletionAsync(systemPrompt, userPrompt, ct: cancellationToken);

    tracer.RecordAiCall(new AiInteractionRecord
    {
        SkillOrToolName = namespaceName,
        Operation = "rewrite",
        SystemPrompt = systemPrompt,
        UserPrompt = userPrompt,
        ResponseContent = response,
        Model = "gpt-4.1-mini",
        DurationMs = 250,
        RetryCount = 0
    });

    step.Complete("rewrite finished");
}
catch (Exception ex)
{
    step?.Fail(ex.Message);
    throw;
}
finally
{
    step?.Dispose();
    await tracer.FlushAsync(outputDirectory, CancellationToken.None);
}
```

## Integration guide

### Use `NullTracer` when tracing is optional

`NullTracer.Instance` implements the same interface and makes call sites simple. Consumers do not need `if (tracingEnabled)` branches around every tracing call.

### Always flush from `finally`

Call `FlushAsync` from a `finally` block so traces survive success, failure, and cancellation paths. In the MCP pipeline, the final helper intentionally uses `CancellationToken.None` during flush so trace files are still written after cancellation is requested.

### Prefer one tracer per process/run

Create a tracer per pipeline run or subprocess. Sequence numbers are local to that tracer instance, which keeps ordering stable within a single process.

## Known limitations

- **Process boundary:** A tracer cannot cross subprocess boundaries. In the MCP pipeline, the `PipelineRunner` records step-level timing for standalone programs, but those programs must emit their own trace files if you need per-AI-call detail.
- **Final flush is non-cancellable:** The last flush deliberately ignores cancellation to preserve observability artifacts. Trace files stay small, so this final write completes quickly.

## Testing

Run `dotnet test shared\DocGeneration.Core.Tracing.Tests\DocGeneration.Core.Tracing.Tests.csproj`.
