---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---

### [MCP Server](#tab/mcp-server)

| Parameter | Required | Description |
|-----------|----------|-------------|
| **Account name** | Required | The name of the Azure Storage account to create. Must be globally unique, 3-24 characters, lowercase letters and numbers only. |
| **Location** | Required | The Azure region where the storage account will be created (for example, `eastus`, `westus2`). |
| **Resource group** | Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Access tier** | Optional | The default access tier for blob storage. Valid values: `Hot`, `Cool`. |
| **Enable hierarchical namespace** | Optional | Whether to enable hierarchical namespace (Data Lake Storage Gen2) for the storage account. |
| **SKU** | Optional | The storage account SKU. Valid values: `Standard_LRS`, `Standard_GRS`, `Standard_RAGRS`, `Standard_ZRS`, `Premium_LRS`, `Premium_ZRS`, `Standard_GZRS`, `Standard_RAGZRS`. |
| **Subscription** | Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used instead. |
| **Tenant** | Optional | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| **Auth method** | Optional | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| **Retry delay** | Optional | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| **Retry max delay** | Optional | Maximum delay in seconds between retries, regardless of the retry strategy. |
| **Retry max retries** | Optional | Maximum number of retry attempts for failed operations before giving up. |
| **Retry mode** | Optional | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| **Retry network timeout** | Optional | Network operation timeout in seconds. Operations taking longer than this value are cancelled. |

### [CLI](#tab/cli)

| Parameter | Type | Description |
|-----------|------|-------------|
| `--account` | string | The name of the Azure Storage account to create. Must be globally unique, 3-24 characters, lowercase letters and numbers only. |
| `--location` | string | The Azure region where the storage account will be created (for example, `eastus`, `westus2`). |
| `--resource-group` | string | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| `--access-tier` | string | The default access tier for blob storage. Valid values: `Hot`, `Cool`. |
| `--enable-hierarchical-namespace` | string | Whether to enable hierarchical namespace (Data Lake Storage Gen2) for the storage account. |
| `--sku` | string | The storage account SKU. Valid values: `Standard_LRS`, `Standard_GRS`, `Standard_RAGRS`, `Standard_ZRS`, `Premium_LRS`, `Premium_ZRS`, `Standard_GZRS`, `Standard_RAGZRS`. |
| `--subscription` | string | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the `AZURE_SUBSCRIPTION_ID` environment variable will be used instead. |
| `--tenant` | string | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | Network operation timeout in seconds. Operations taking longer than this value are cancelled. |

---
