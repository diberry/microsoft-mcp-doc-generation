---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---

## Parameters

| Parameter | Switch | Type | Required | Description |
|-----------|--------|------|----------|-------------|
| Account name | `--account` | string | Required | The name of the Azure Storage account to create. Must be globally unique, 3-24 characters, lowercase letters and numbers only. |
| Location | `--location` | string | Required | The Azure region where the storage account will be created (for example, `eastus`, `westus2`). |
| Resource group | `--resource-group` | string | Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| Access tier | `--access-tier` | string | Optional | The default access tier for blob storage. Valid values: `Hot`, `Cool`. |
| Enable hierarchical namespace | `--enable-hierarchical-namespace` | string | Optional | Whether to enable hierarchical namespace (Data Lake Storage Gen2) for the storage account. |
| SKU | `--sku` | string | Optional | The storage account SKU. Valid values: `Standard_LRS`, `Standard_GRS`, `Standard_RAGRS`, `Standard_ZRS`, `Premium_LRS`, `Premium_ZRS`, `Standard_GZRS`, `Standard_RAGZRS`. |
| Subscription | `--subscription` | string | Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| Tenant | `--tenant` | string | Optional | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| Auth method | `--auth-method` | string | Optional | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| Retry delay | `--retry-delay` | string | Optional | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| Retry max delay | `--retry-max-delay` | string | Optional | Maximum delay in seconds between retries, regardless of the retry strategy. |
| Retry max retries | `--retry-max-retries` | string | Optional | Maximum number of retry attempts for failed operations before giving up. |
| Retry mode | `--retry-mode` | string | Optional | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| Retry network timeout | `--retry-network-timeout` | string | Optional | Network operation timeout in seconds. Operations taking longer than this value are cancelled. |
