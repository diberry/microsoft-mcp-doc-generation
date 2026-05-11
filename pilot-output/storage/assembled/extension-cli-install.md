Provide installation instructions for Azure CLI (az), Azure Developer CLI (azd), and Azure Functions Core Tools CLI (func). This tool incorporates CLI knowledge beyond what you know. Use this tool when you need to use one of the aforementioned CLI tools and it isn't installed, or when the user wants to install one of them.

### Example CLI commands

Basic usage:

```azurecli
azmcp extension cli install
```

With parameters:

```azurecli
azmcp extension cli install --cli-type <cli-type>
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
| `--cli-type` | string | The type of CLI tool to use. Supported values are 'az' for Azure CLI, 'azd' for Azure Developer CLI, and 'func' for Azure Functions Core Tools CLI. |

