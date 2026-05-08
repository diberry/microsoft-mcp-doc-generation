---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp azurebackup policy create
```

With parameters:

```azurecli
azmcp azurebackup policy create --resource-group <resource-group> --vault <vault> --vault-type <vault-type> --policy <policy> --workload-type <workload-type> --schedule-time <schedule-time> --daily-retention-days <daily-retention-days>
```
