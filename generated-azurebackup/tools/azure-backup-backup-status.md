---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# status

<!-- @mcpcli azurebackup backup status -->

This tool checks the backup status of an Azure resource and returns whether the resource is protected, along with the backup vault and policy details from Azure Backup. You can verify protection for a virtual machine, managed disk, storage account, or other data source. The tool requires the datasource ARM resource ID and the Azure region where the resource exists.

For example, check the backup status of a virtual machine by providing its resource ID '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/myResourceGroup/providers/Microsoft.Compute/virtualMachines/myVM' and Location 'eastus'.

<!-- Required parameters: 2 - 'Datasource ID', 'Location' -->

Example prompts include:

- "Check backup status for datasource ID '/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/webapp-prod' in location 'eastus'."
- "Is datasource ID '/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/rg-backup/providers/Microsoft.Compute/disks/db-disk' protected in location 'westus2'?"
- "Show backup protection details for datasource ID '/subscriptions/abcdefab-0000-1111-2222-333344445555/resourceGroups/rg-storage/providers/Microsoft.Storage/storageAccounts/mystorageacct' in location 'eastus2'."
- "Verify backup status for datasource ID 'SAPHanaDatabase;instance;sapprd' in location 'centralus'."
- "I need the backup status for datasource ID '/subscriptions/22222222-2222-3333-4444-555566667777/resourceGroups/prod-rg/providers/Microsoft.RecoveryServices/vaults/my-vault/backupFabrics/Azure/protectionContainers/iaasvmcontainer;iaasvmcontainerv2;prod-rg;app-server/protectableItems/app-server' in location 'eastus'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Datasource ID** |  Required | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`). |
| **Location** |  Required | The Azure region (for example, `'eastus'`, `'westus2'`). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌