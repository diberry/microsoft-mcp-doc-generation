---
ms.topic: include
ms.date: 05/11/2026
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
---
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--datasource-id` | string | Yes | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (e.g., '/subscriptions/.../virtualMachines/myvm'). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (e.g., 'SAPHanaDatabase;instance;dbname'). |
| `--location` | string | Yes | The Azure region (e.g., 'eastus', 'westus2'). |
