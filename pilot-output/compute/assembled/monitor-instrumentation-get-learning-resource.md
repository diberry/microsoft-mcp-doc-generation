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

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--path` | string | - | Learning resource path. |

