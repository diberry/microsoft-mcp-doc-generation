Retrieves details about a Service Bus topic. Returns runtime information and topic properties including number of subscriptions, max message size, max topic size, number of scheduled messages, etc.
Required arguments are namespace: The fully qualified Service Bus namespace host name (usually in the form <namespace>.servicebus.windows.net) and topic: Topic name to get information about.
Do not use this to get details on Service Bus subscription- instead use servicebus_topic_subscription_details.

### Example CLI commands

Basic usage:

```azurecli
azmcp servicebus topic details
```

With parameters:

```azurecli
azmcp servicebus topic details --namespace <namespace> --topic <topic>
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
| `--namespace` | string | The fully qualified Service Bus namespace host name. (This is usually in the form <namespace>.servicebus.windows.net) |
| `--topic` | string | The name of the topic containing the subscription. |

