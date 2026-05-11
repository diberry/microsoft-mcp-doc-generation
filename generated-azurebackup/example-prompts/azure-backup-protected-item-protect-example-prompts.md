---
ms.topic: include
ms.date: 2026-05-11 14:42:50 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
---

<!-- Required parameters: 4 - 'Datasource ID', 'Policy', 'Resource group', 'Vault name' -->

Example prompts include:

- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/prod-rg/providers/Microsoft.Compute/virtualMachines/webapp-prod' with policy 'DailyBackup' in resource group 'prod-rg' and vault 'rsv-prod'."
- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/rg-prod/providers/Microsoft.Compute/disks/dataDisk1' with policy 'HourlyBackup' in resource group 'rg-prod' and vault 'dpp-backup', datasource type 'AzureDisk'."
- "Protect datasource ID 'SAPHanaDatabase;instance01;SalesDB' with policy 'SAPHanaPolicy' in resource group 'sap-rg' and vault 'rsv-sap', container 'saphana-cont'."
- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/files-rg/providers/Microsoft.Storage/storageAccounts/mystorage/fileServices/default/shares/backups' with policy 'FileSharePolicy' in resource group 'files-rg' and vault 'rsv-files', container 'files-container'."
- "Can you protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/db-staging' using policy 'WeeklyFull' in resource group 'test-rg' and vault 'backup-vault'?"
