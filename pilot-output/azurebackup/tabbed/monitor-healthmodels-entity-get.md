### [MCP Server](#tab/mcp-server)

This tool executes `monitor healthmodels entity get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieve the health status of an entity for a given Azure Monitor Health Model. Use this tool ONLY when the user mentions a specific health model name and asks for health status, health events. This provides application-level health monitoring with custom health models, not basic Azure resource availability.
For basic Azure resource availability status, use Resource Health tool instead `azmcp_resourcehealth_availability-status_get`.  
For querying logs from a Log Analystics workspace, use `azmcp_monitor_workspace_log_query`.  
For querying logs of a specific Azure resource, use `azmcp_monitor_resource_log_query`. 
Required arguments:
    - --entity: The entity to get health for
    - --health-model: The health model name

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor healthmodels entity get
```

With parameters:

```azurecli
azmcp monitor healthmodels entity get --resource-group <resource-group> --entity <entity> --health-model <health-model>
```

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
| `--entity` | string | - | The entity to get health for. |
| `--health-model` | string | - | The name of the health model for which to get the health. |

---
