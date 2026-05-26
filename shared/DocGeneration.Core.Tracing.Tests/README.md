# DocGeneration.Core.Tracing.Tests

Unit tests for the shared pipeline tracing library.

## Coverage
- `PipelineTracer` step lifecycle, AI capture, sequencing, flushing, and concurrency
- `TraceWriter` JSON, markdown, and atomic-write behavior
- `NullTracer` no-op behavior

## Run
Use `dotnet test shared\DocGeneration.Core.Tracing.Tests\DocGeneration.Core.Tracing.Tests.csproj`.
