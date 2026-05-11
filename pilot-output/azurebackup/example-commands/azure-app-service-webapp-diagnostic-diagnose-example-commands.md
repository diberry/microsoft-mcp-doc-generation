---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp appservice webapp diagnostic diagnose
```

With parameters:

```azurecli
azmcp appservice webapp diagnostic diagnose --resource-group <resource-group> --app <app> --detector-id <detector-id> --start-time <start-time> --end-time <end-time> --interval <interval>
```
