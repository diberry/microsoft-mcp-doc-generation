### [MCP Server](#tab/mcp-server)

This tool executes `storage account create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Creates an Azure Storage account in the specified resource group and location and returns the created storage account
information including name, location, SKU, access settings, and configuration details.

### Example CLI commands

Basic usage:

```azurecli
azmcp storage account create
```

With parameters:

```azurecli
azmcp storage account create --resource-group <resource-group> --account <account> --location <location> --sku <sku> --access-tier <access-tier> --enable-hierarchical-namespace <enable-hierarchical-namespace>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--tenant` | string | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--subscription` | string | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--account` | string | The name of the Azure Storage account to create. Must be globally unique, 3-24 characters, lowercase letters and numbers only. |
| `--location` | string | The Azure region where the storage account will be created (e.g., 'eastus', 'westus2'). |
| `--sku` | string | The storage account SKU. Valid values: Standard_LRS, Standard_GRS, Standard_RAGRS, Standard_ZRS, Premium_LRS, Premium_ZRS, Standard_GZRS, Standard_RAGZRS. |
| `--access-tier` | string | The default access tier for blob storage. Valid values: Hot, Cool. |
| `--enable-hierarchical-namespace` | string | Whether to enable hierarchical namespace (Data Lake Storage Gen2) for the storage account. |

---
