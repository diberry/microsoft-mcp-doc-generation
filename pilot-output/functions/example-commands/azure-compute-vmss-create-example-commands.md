---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp compute vmss create
```

With parameters:

```azurecli
azmcp compute vmss create --resource-group <resource-group> --vmss-name <vmss-name> --location <location> --admin-username <admin-username> --admin-password <admin-password> --ssh-public-key <ssh-public-key> --vm-size <vm-size> --image <image> --os-type <os-type> --instance-count <instance-count> --upgrade-policy <upgrade-policy> --virtual-network <virtual-network> --subnet <subnet> --zone <zone> --os-disk-size-gb <os-disk-size-gb> --os-disk-type <os-disk-type>
```
