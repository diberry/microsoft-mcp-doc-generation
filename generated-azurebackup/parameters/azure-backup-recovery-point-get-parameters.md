---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:41 UTC
# [!INCLUDE [azurebackup recoverypoint get](../includes/tools/parameters/azure-backup-recovery-point-get-parameters.md)]
# azmcp azurebackup recoverypoint get
---

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Protected item** |  Required | The name of the protected item or backup instance. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Recovery point** |  Optional | The recovery point ID. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
