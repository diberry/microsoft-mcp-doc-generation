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
| `--event-type` | string | - | Filter by event type (ServiceIssue, PlannedMaintenance, HealthAdvisory, Security). If not specified, all event types are included. |
| `--status` | string | - | Filter by status (Active, Resolved). If not specified, all statuses are included. |
| `--tracking-id` | string | - | Filter by tracking ID to get a specific service health event. |
| `--filter` | string | - | Additional OData filter expression to apply to the service health events query. |
| `--query-start-time` | string | - | Start time for the query in ISO 8601 format (e.g., 2024-01-01T00:00:00Z). Events from this time onwards will be included. |
| `--query-end-time` | string | - | End time for the query in ISO 8601 format (e.g., 2024-01-31T23:59:59Z). Events up to this time will be included. |
