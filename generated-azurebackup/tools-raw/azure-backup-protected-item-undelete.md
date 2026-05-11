---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# undelete

<!-- @mcpcli azurebackup protecteditem undelete -->

Undeletes or restores a soft-deleted backup item to an active protection state.
Use this when a backup or protected item was accidentally deleted and needs to be recovered.
For RSV vaults: pass the datasource ARM resource ID as --datasource-id.
For DPP vaults: pass the datasource ARM resource ID as --datasource-id.
Optionally specify --container for RSV workload items (SQL/HANA).
The operation is asynchronous; use 'azurebackup job get' to monitor progress.

{{EXAMPLE_PROMPTS_CONTENT}}

{{PARAMETERS_CONTENT}}

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

{{ANNOTATIONS_CONTENT}}

