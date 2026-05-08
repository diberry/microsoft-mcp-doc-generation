### [MCP Server](#tab/mcp-server)

This tool executes `monitor instrumentation orchestrator-start` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

START HERE for Azure Monitor instrumentation. Analyzes workspace and returns the first action to execute. After executing the action, call orchestrator-next to continue. DO NOT improvise. Execute EXACTLY what the 'instruction' field tells you.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor instrumentation orchestrator-start
```

With parameters:

```azurecli
azmcp monitor instrumentation orchestrator-start --workspace-path <workspace-path>
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--workspace-path` | string | - | Absolute path to the workspace folder. |

---
