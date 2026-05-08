---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp sql db create
```

With parameters:

```azurecli
azmcp sql db create --resource-group <resource-group> --server <server> --database <database> --sku-name <sku-name> --sku-tier <sku-tier> --sku-capacity <sku-capacity> --collation <collation> --max-size-bytes <max-size-bytes> --elastic-pool-name <elastic-pool-name> --zone-redundant <zone-redundant> --read-scale <read-scale>
```
