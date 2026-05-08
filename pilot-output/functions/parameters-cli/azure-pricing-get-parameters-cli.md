---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--tenant` | string | - | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | - | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | - | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | - | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | - | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | - | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | - | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--currency` | string | - | Currency code for pricing (e.g., USD, EUR). Default is USD. |
| `--sku` | string | - | ARM SKU name (e.g., Standard_D4s_v5, Standard_E64-16ds_v4) |
| `--service` | string | - | Azure service name (e.g., Virtual Machines, Storage, SQL Database) |
| `--region` | string | - | Azure region (e.g., eastus, westeurope, westus2) |
| `--service-family` | string | - | Service family (e.g., Compute, Storage, Databases, Networking) |
| `--price-type` | string | - | Price type filter (Consumption, Reservation, DevTestConsumption) |
| `--include-savings-plan` | string | - | Include savings plan pricing information (uses preview API version) |
| `--filter` | string | - | Raw OData filter expression for advanced queries (e.g., "meterId eq 'abc-123'") |
