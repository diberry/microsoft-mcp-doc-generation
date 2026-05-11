---
ms.topic: include
ms.date: 05/11/2026
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
---
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--soft-delete` | string | Yes | Soft delete state: 'AlwaysOn', 'On', or 'Off'. |
| `--soft-delete-retention-days` | string | No | Soft delete retention period (14-180 days). |
