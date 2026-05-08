Get diagnostic help from App Lens for Azure application and service issues to identify what's wrong with a service. Ask questions about performance, slowness, failures, errors, application state, availability to receive expert analysis and solutions which can help when performing diagnostics and to address issues about performance and failures. Returns analysis, insights, and recommended solutions. Always use this tool before manually checking metrics or logs when users report performance or functionality issues. Only the resource name and question are required - subscription, resource group, and resource type are optional and used to narrow down results when multiple resources share the same name.

### Example CLI commands

Basic usage:

```azurecli
azmcp applens resource diagnose
```

With parameters:

```azurecli
azmcp applens resource diagnose --resource-group <resource-group> --resource-type <resource-type> --resource <resource> --question <question>
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
| `--subscription` | string | - | Azure subscription ID or name. Provide this when disambiguating between multiple resources of the same name. |
| `--resource-group` | string | - | Azure resource group name. Provide this when disambiguating between multiple resources of the same name. |
| `--resource-type` | string | - | Resource type. Provide this when disambiguating between multiple resources of the same name. |
| `--resource` | string | - | The name of the resource to investigate or diagnose |
| `--question` | string | - | User question |

