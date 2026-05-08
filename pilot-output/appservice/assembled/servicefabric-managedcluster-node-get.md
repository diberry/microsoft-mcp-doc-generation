Get nodes for a Service Fabric managed cluster. Returns all nodes by default or a single node when a node name is specified. Includes name, node type, status, IP address, fault domain, upgrade domain, health state, and seed node status.

### Example CLI commands

Basic usage:

```azurecli
azmcp servicefabric managedcluster node get
```

With parameters:

```azurecli
azmcp servicefabric managedcluster node get --resource-group <resource-group> --cluster <cluster> --node <node>
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
| `--cluster` | string | Service Fabric managed cluster name. |
| `--node` | string | The node name. When specified, returns a single node instead of all nodes. |

