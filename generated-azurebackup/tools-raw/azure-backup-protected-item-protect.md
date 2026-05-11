---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# protect

<!-- @mcpcli azurebackup protecteditem protect -->

Enables or configures backup protection for an Azure resource by creating a
protected item or backup instance. Protects VMs, disks, file shares, SQL databases,
SAP HANA databases, and other supported datasources.
For VMs: pass the VM ARM resource ID as --datasource-id.
For workloads (SQL/HANA): pass the protectable item name from 'protectableitem list'
as --datasource-id (e.g., 'SAPHanaDatabase;instance;dbname'), and specify --container.
Requires a backup policy name via --policy. The operation is asynchronous;
use 'azurebackup job get' to monitor the protection job progress.

{{EXAMPLE_PROMPTS_CONTENT}}

{{PARAMETERS_CONTENT}}

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

{{ANNOTATIONS_CONTENT}}

