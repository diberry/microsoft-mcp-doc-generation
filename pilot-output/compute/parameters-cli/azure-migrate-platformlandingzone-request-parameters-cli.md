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
| `--subscription` | string | - | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--resource-group` | string | - | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--action` | string | - | The action to perform: 'update' (set parameters), 'check' (check existing platform landing zone), 'generate' (generate platform landing zone), 'download' (get download instructions), 'status' (view parameter status). |
| `--region-type` | string | - | The region type for the Platform Landing Zone. Valid values: 'single', 'multi'. |
| `--firewall-type` | string | - | The firewall type for the Platform Landing Zone. Valid values: 'azurefirewall', 'nva'. |
| `--network-architecture` | string | - | The network architecture for the Platform Landing Zone. Valid values: 'hubspoke', 'vwan'. |
| `--identity-subscription-id` | string | - | The Azure subscription ID for the identity management group in Platform Landing Zone (GUID format). |
| `--management-subscription-id` | string | - | The Azure subscription ID for the management group in Platform Landing Zone (GUID format). |
| `--connectivity-subscription-id` | string | - | The Azure subscription ID for the connectivity group in Platform Landing Zone (GUID format). |
| `--security-subscription-id` | string | - | The Azure subscription ID for security resources in Platform Landing Zone (GUID format). |
| `--regions` | string | - | Comma-separated list of Azure regions for Platform Landing Zone (e.g., 'eastus,westus2'). |
| `--environment-name` | string | - | The environment name for the Platform Landing Zone. |
| `--version-control-system` | string | - | The version control system for the Platform Landing Zone. Valid values: 'local', 'github', 'azuredevops'. |
| `--organization-name` | string | - | The organization name for the Platform Landing Zone. |
| `--migrate-project-name` | string | - | The Azure Migrate project name for Platform Landing Zone generation context. |
| `--migrate-project-resource-id` | string | - | The full resource ID of the Azure Migrate project for Platform Landing Zone (alternative to subscription/resourceGroup/migrateProjectName). |
| `--location` | string | - | The Azure region location for creating new resources (e.g., 'eastus', 'westus2'). Required for 'createmigrateproject' action. |
