List available Azure Functions templates or generate function code. Shows triggers (HTTP, Timer, Blob, EventHub, Durable, MCP triggers, and more), bindings, and serverless function options. Create durable functions, orchestrations, activity functions, or MCP server functions. Supports azd infrastructure with Bicep, Terraform, ARM templates. Without --template, lists all templates. With --template, generates code files. Select one trigger (required) and zero or more bindings.

### Example CLI commands

Basic usage:

```azurecli
azmcp functions template get
```

With parameters:

```azurecli
azmcp functions template get --language <language> --template <template> --runtime-version <runtime-version> --output <output>
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--language` | string | - | Programming language for the Azure Functions project. Valid values: python, typescript, javascript, java, csharp, powershell. |
| `--template` | string | - | Name of the function template to retrieve (e.g., HttpTrigger, BlobTrigger). Omit to list all available templates for the specified language. |
| `--runtime-version` | string | - | Optional runtime version for Java or TypeScript/JavaScript. When provided, template placeholders like {{javaVersion}} or {{nodeVersion}} are replaced automatically. See 'functions language list' for supported versions. |
| `--output` | string | - | Output format. 'New' (default) returns all files in a single 'files' list for creating complete projects. 'Add' separates files into 'functionFiles' and 'projectFiles' with merge instructions for adding to existing projects. |

