---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
| Parameter | Type | Description |
|-----------|------|-------------|
| `--session-id` | string | The workspace path returned as sessionId from orchestrator-start. |
| `--findings-json` | string | JSON object with brownfield analysis findings. Required properties:
- serviceOptions: Service options findings from analyzing AddApplicationInsightsTelemetry() call. Null if not found.
- initializers: Telemetry initializer findings from analyzing ITelemetryInitializer or IConfigureOptions<TelemetryConfiguration> implementations. Null if none found.
- processors: Telemetry processor findings from analyzing ITelemetryProcessor implementations. Null if none found.
- clientUsage: TelemetryClient usage findings from analyzing direct TelemetryClient usage. Null if not found.
- sampling: Custom sampling configuration findings. Null if no custom sampling.
- telemetryPipeline: Custom ITelemetryChannel or TelemetrySinks usage findings. Null if not found.
- logging: Explicit logger provider and filter findings. Null if not found.
For sections that do not exist in the codebase, pass an empty/default object (e.g. found: false, hasCustomSampling: false) rather than null. |
