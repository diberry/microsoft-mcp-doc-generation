### [MCP Server](#tab/mcp-server)

This tool executes `monitor instrumentation get-learning-resource` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

List all available learning resources for Azure Monitor instrumentation or get the content of a specific resource by path. Returns all resource paths by default, or retrieves the full content when a path is specified. Note: For instrumenting an application, use orchestrator-start instead.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor instrumentation get-learning-resource
```

With parameters:

```azurecli
azmcp monitor instrumentation get-learning-resource --path <path>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--path` | string | Learning resource path. |

---
