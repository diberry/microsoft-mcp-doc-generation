---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp fileshares fileshare update
```

With parameters:

```azurecli
azmcp fileshares fileshare update --resource-group <resource-group> --name <name> --provisioned-storage-in-gib <provisioned-storage-in-gib> --provisioned-io-per-sec <provisioned-io-per-sec> --provisioned-throughput-mib-per-sec <provisioned-throughput-mib-per-sec> --public-network-access <public-network-access> --nfs-root-squash <nfs-root-squash> --allowed-subnets <allowed-subnets> --tags <tags>
```
