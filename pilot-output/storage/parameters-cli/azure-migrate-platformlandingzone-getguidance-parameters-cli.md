---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
| Parameter | Type | Description |
|-----------|------|-------------|
| `--tenant` | string | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--scenario` | string | The modification scenario key. Valid values: resource-names, management-groups, ddos, bastion, dns, gateways, regions, ip-addresses, policy-enforcement, policy-assignment, ama, amba, defender, zero-trust, slz. |
| `--policy-name` | string | The policy assignment name to look up (e.g., 'Enable-DDoS-VNET'). Used with policy-enforcement or policy-assignment scenarios. |
| `--list-policies` | string | Set to true to list all available policies organized by archetype. Useful for finding the exact policy name. |
