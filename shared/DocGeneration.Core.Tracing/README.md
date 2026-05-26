# DocGeneration.Core.Tracing

Shared tracing infrastructure for pipeline observability.

## Purpose
- Capture step execution metadata in memory for a single pipeline run
- Record AI prompt/response interactions with sequence numbers for stable ordering
- Flush trace artifacts to `pipeline-trace.json`, `ai-interactions.json`, and `summary.md`

## Key Types
- `IPipelineTracer` / `IStepHandle`
- `PipelineTracer`
- `NullTracer`
- `TraceWriter`
- `Models/TraceEvent`, `Models/AiInteraction`, `Models/PipelineTrace`

## Usage
Construct `PipelineTracer` per pipeline run, wrap each step in `StartStep(...)`, record AI calls with `RecordAiCall(...)`, and call `FlushAsync(...)` from a `finally` block.

## Testing
Run `dotnet test shared\DocGeneration.Core.Tracing.Tests\DocGeneration.Core.Tracing.Tests.csproj`.
