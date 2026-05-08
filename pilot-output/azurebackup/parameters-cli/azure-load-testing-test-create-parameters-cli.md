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
| `--test-resource-name` | string | The name of the load test resource for which you want to fetch the details. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--test-id` | string | The ID of the load test for which you want to fetch the details. |
| `--description` | string | The description for the load test run. This provides additional context about the test run. |
| `--display-name` | string | The display name for the load test run. This is a user-friendly name to identify the test run. |
| `--endpoint` | string | The endpoint URL to be tested. This is the URL of the HTTP endpoint that will be subjected to load testing. |
| `--virtual-users` | string | Virtual users is a measure of load that is simulated to test the HTTP endpoint. (Default - 50) |
| `--duration` | string | This is the duration for which the load is simulated against the endpoint. Enter decimals for fractional minutes (e.g., 1.5 for 1 minute and 30 seconds). Default is 20 mins |
| `--ramp-up-time` | string | The ramp-up time is the time it takes for the system to ramp-up to the total load specified. Enter decimals for fractional minutes (e.g., 1.5 for 1 minute and 30 seconds). Default is 1 min |
