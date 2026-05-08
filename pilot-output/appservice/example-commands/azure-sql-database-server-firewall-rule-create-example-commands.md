---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp sql server firewall-rule create
```

With parameters:

```azurecli
azmcp sql server firewall-rule create --resource-group <resource-group> --server <server> --firewall-rule-name <firewall-rule-name> --start-ip-address <start-ip-address> --end-ip-address <end-ip-address>
```
