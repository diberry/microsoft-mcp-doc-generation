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
| `--subscription` | string | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--server` | string | The Azure SQL Server name. |
| `--database` | string | The Azure SQL Database name. |
| `--sku-name` | string | The SKU name for the database (e.g., Basic, S0, P1, GP_Gen5_2). |
| `--sku-tier` | string | The SKU tier for the database (e.g., Basic, Standard, Premium, GeneralPurpose). |
| `--sku-capacity` | string | The SKU capacity (DTU or vCore count) for the database. |
| `--collation` | string | The collation for the database (e.g., SQL_Latin1_General_CP1_CI_AS). |
| `--max-size-bytes` | string | The maximum size of the database in bytes. |
| `--elastic-pool-name` | string | The name of the elastic pool to assign the database to. |
| `--zone-redundant` | string | Whether the database should be zone redundant. |
| `--read-scale` | string | Read scale option for the database (Enabled or Disabled). |
