Create a new key in an Azure Key Vault. This command creates a key with the specified name and type in the given vault. Supports types: RSA, RSA-HSM, EC, EC-HSM, oct, oct-HSM. Required: --vault <vault>, --key <key> --key-type <key-type> --subscription <subscription>. Optional: --tenant <tenant>. Returns: Returns: name, id, keyId, keyType, enabled, notBefore, expiresOn, createdOn, updatedOn. Creates a new key version if it already exists.

### Example CLI commands

Basic usage:

```azurecli
azmcp keyvault key create
```

With parameters:

```azurecli
azmcp keyvault key create --vault <vault> --key <key> --key-type <key-type>
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
| `--vault` | string | - | The name of the Key Vault. |
| `--key` | string | - | The name of the key to retrieve/modify from the Key Vault. |
| `--key-type` | string | - | The type of key to create (RSA, EC). |

