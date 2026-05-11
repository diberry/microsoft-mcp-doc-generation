---
ms.topic: include
ms.date: 2026-05-11 14:41:34 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
---

<!-- Required parameters: 4 - 'Policy', 'Resource group', 'Vault name', 'Workload type' -->

Example prompts include:

- "Create backup policy 'daily-vm-policy' in resource group 'rg-backup-prod' with vault name 'rsv-prod-vault' for workload type 'VM' and schedule time '02:00' with daily retention days '30'."
- "Set up policy 'sql-weekly-retention' for workload type 'SQL' in resource group 'rg-sql-prod' using vault name 'rsv-sql-vault' with daily retention days '7'."
- "Create policy 'aks-backup-policy' in resource group 'rg-aks' on vault name 'dpp-aks-vault' for workload type 'AKS' with vault type 'dpp'."
- "Can you create backup policy 'azureblob-monthly' in resource group 'rg-storage' using vault name 'rsv-storage-vault' for workload type 'AzureBlob'?"
- "Create backup policy 'saphana-daily' with workload type 'SAPHANA' in resource group 'rg-sap' and vault name 'rsv-sap-vault'."
