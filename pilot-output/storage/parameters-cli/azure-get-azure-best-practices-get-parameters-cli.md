---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--resource` | string | - | The Azure resource type for which to get best practices. Options: 'general' (general Azure), 'azurefunctions' (Azure Functions), 'static-web-app' (Azure Static Web Apps), 'coding-agent' (Coding Agent). |
| `--action` | string | - | The action type for the best practices. Options: 'all', 'code-generation', 'deployment'. Note: 'static-web-app' and 'coding-agent' resources only supports 'all'. |
