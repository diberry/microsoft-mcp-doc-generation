---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# list

<!-- @mcpcli azurebackup protectableitem list -->

Lists items that can be backed up (protectable items) in a Recovery Services vault,
such as SQL databases and SAP HANA databases discovered on registered VMs.
Use this to find databases and workloads available for backup protection.
Only supported for RSV vaults; DPP datasources are protected by ARM resource ID directly.
Filter results by --workload-type (e.g., SQL, SAPHana) or --container.

{{EXAMPLE_PROMPTS_CONTENT}}

{{PARAMETERS_CONTENT}}

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

{{ANNOTATIONS_CONTENT}}

