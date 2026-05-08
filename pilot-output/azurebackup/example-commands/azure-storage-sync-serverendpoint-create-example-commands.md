---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp storagesync serverendpoint create
```

With parameters:

```azurecli
azmcp storagesync serverendpoint create --resource-group <resource-group> --name <name> --sync-group-name <sync-group-name> --server-endpoint-name <server-endpoint-name> --server-resource-id <server-resource-id> --server-local-path <server-local-path> --cloud-tiering <cloud-tiering> --volume-free-space-percent <volume-free-space-percent> --tier-files-older-than-days <tier-files-older-than-days> --local-cache-mode <local-cache-mode>
```
