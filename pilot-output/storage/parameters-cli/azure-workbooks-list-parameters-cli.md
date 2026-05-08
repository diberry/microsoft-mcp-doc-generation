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
| `--kind` | string | - | Filter workbooks by kind (e.g., 'shared', 'user'). If not specified, all kinds will be returned. |
| `--category` | string | - | Filter workbooks by category (e.g., 'workbook', 'sentinel', 'TSG'). If not specified, all categories will be returned. |
| `--source-id` | string | - | Filter workbooks by source resource ID (e.g., Application Insights resource, Log Analytics workspace). If not specified, all workbooks will be returned. |
| `--name-contains` | string | - | Filter workbooks where display name contains this text (case-insensitive). |
| `--modified-after` | string | - | Filter workbooks modified after this date (ISO 8601 format, e.g., '2024-01-15'). |
| `--output-format` | string | - | Output format: 'summary' (id+name only, minimal tokens), 'standard' (metadata without content, default), 'full' (includes serializedData). |
| `--max-results` | string | - | Maximum number of results to return (default: 50, max: 1000). |
| `--include-total-count` | string | - | Include total count of all matching workbooks in the response (default: true). |
