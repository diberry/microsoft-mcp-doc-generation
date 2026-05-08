### [MCP Server](#tab/mcp-server)

This tool executes `appservice webapp deployment get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieves detailed information about Azure App Service web app deployments, including deployment name,
if deployment is actively happening, when the deployment started and ended, who authored and deployed the
deployment, and the type of deployment. If a specific deployment ID is not provided, the command will return
details for all deployments in the web app. You can specify a deployment ID to get details for a specific
deployment.

### Example CLI commands

Basic usage:

```azurecli
azmcp appservice webapp deployment get
```

With parameters:

```azurecli
azmcp appservice webapp deployment get --resource-group <resource-group> --app <app> --deployment-id <deployment-id>
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
| `--app` | string | - | The name of the Azure App Service (e.g., my-webapp). |
| `--deployment-id` | string | - | The ID of the deployment. |

---
