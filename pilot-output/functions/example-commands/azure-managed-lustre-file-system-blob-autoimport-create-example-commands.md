---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp managedlustre fs blob autoimport create
```

With parameters:

```azurecli
azmcp managedlustre fs blob autoimport create --resource-group <resource-group> --filesystem-name <filesystem-name> --job-name <job-name> --conflict-resolution-mode <conflict-resolution-mode> --autoimport-prefixes <autoimport-prefixes> --admin-status <admin-status> --enable-deletions <enable-deletions> --maximum-errors <maximum-errors>
```
