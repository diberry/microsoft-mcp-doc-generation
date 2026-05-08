Appends a tamper-proof entry to a Confidential Ledger instance and returns the transaction identifier.

### Example CLI commands

Basic usage:

```azurecli
azmcp confidentialledger entries append
```

With parameters:

```azurecli
azmcp confidentialledger entries append --ledger <ledger> --content <content> --collection-id <collection-id>
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
| `--ledger` | string | - | The name of the Confidential Ledger instance (e.g., 'myledger'). |
| `--content` | string | - | The JSON or text payload to append as a tamper-proof ledger entry. |
| `--collection-id` | string | - | Optional ledger collection identifier. If omitted the default collection is used. |

