List policy assignments in a subscription or scope. This command retrieves all Azure Policy
assignments along with their complete policy definition details (rules, effects, parameters schema),
enforcement modes, assignment parameters, and metadata. This enables agents to understand policy
requirements and design compliant cloud services. You can optionally filter by scope to list
assignments at a specific resource group, resource, or management group level.

### Example CLI commands

Basic usage:

```azurecli
azmcp policy assignment list
```

With parameters:

```azurecli
azmcp policy assignment list --scope <scope>
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
| `--scope` | string | The scope of the policy assignment (e.g., /subscriptions/{subscriptionId}, /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}). |

