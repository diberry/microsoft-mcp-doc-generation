---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
### Example CLI commands

Basic usage:

```azurecli
azmcp compute vmss update
```

With parameters:

```azurecli
azmcp compute vmss update --resource-group <resource-group> --vmss-name <vmss-name> --upgrade-policy <upgrade-policy> --capacity <capacity> --vm-size <vm-size> --overprovision <overprovision> --enable-auto-os-upgrade <enable-auto-os-upgrade> --scale-in-policy <scale-in-policy> --tags <tags>
```
