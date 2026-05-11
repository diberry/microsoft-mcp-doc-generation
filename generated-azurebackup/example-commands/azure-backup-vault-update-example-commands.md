---
ms.topic: include
ms.date: 05/11/2026
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
---
**Example CLI command**

```azurecli
azmcp azurebackup vault update \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>] \
  [--redundancy <redundancy>] \
  [--soft-delete <soft-delete>] \
  [--soft-delete-retention-days <soft-delete-retention-days>] \
  [--immutability-state <immutability-state>] \
  [--identity-type <identity-type>] \
  [--tags <tags>]
```
