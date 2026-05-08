---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--language` | string | - | Programming language for the Azure Functions project. Valid values: python, typescript, javascript, java, csharp, powershell. |
| `--template` | string | - | Name of the function template to retrieve (e.g., HttpTrigger, BlobTrigger). Omit to list all available templates for the specified language. |
| `--runtime-version` | string | - | Optional runtime version for Java or TypeScript/JavaScript. When provided, template placeholders like {{javaVersion}} or {{nodeVersion}} are replaced automatically. See 'functions language list' for supported versions. |
| `--output` | string | - | Output format. 'New' (default) returns all files in a single 'files' list for creating complete projects. 'Add' separates files into 'functionFiles' and 'projectFiles' with merge instructions for adding to existing projects. |
