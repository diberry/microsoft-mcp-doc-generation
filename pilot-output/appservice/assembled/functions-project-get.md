Get project scaffolding information for a new Azure Functions app. Use for getting project structure, setup instructions, and file list for initializing serverless projects. Returns project structure overview and setup instructions that agents use to create files. Use after functions language list and before functions template get.

### Example CLI commands

Basic usage:

```azurecli
azmcp functions project get
```

With parameters:

```azurecli
azmcp functions project get --language <language>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--language` | string | Programming language for the Azure Functions project. Valid values: python, typescript, javascript, java, csharp, powershell. |

