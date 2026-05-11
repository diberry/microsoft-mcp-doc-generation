Submit the user's enhancement selection after orchestrator-start returned status 'enhancement_available'.
Present the enhancement options to the user first, then call this tool with their chosen option key(s).
Multiple enhancements can be selected by passing a comma-separated list (e.g. 'redis,processors').
After this call succeeds, continue with orchestrator-next as usual.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor instrumentation send-enhancement-select
```

With parameters:

```azurecli
azmcp monitor instrumentation send-enhancement-select --session-id <session-id> --enhancement-keys <enhancement-keys>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--session-id` | string | The workspace path returned as sessionId from orchestrator-start. |
| `--enhancement-keys` | string | One or more enhancement keys, comma-separated (e.g. 'redis', 'redis,processors', 'entityframework,otlp'). |

