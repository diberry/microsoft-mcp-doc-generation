### [MCP Server](#tab/mcp-server)

This tool executes `eventhubs namespace update` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create or Update a Namespace. This tool will either create a Namespace resource or 
update a pre-existing Namespace resource within the specified resource group, depending on 
whether or not the specified Namespace already exists. This tool may modify existing 
configurations, and is considered to be destructive. This is a potentially long-running operation.

When updating an existing namespace, you only need to provide the properties you want to change.
Unspecified properties will retain their existing values. At least one update property must be provided.

Common update scenarios:
- Scale up/down by changing SKU tier or capacity
- Enable/disable auto-inflate and set maximum throughput units
- Enable/disable Kafka support
- Modify tags for resource management
- Enable/disable zone redundancy (Premium SKU only)

### Example CLI commands

Basic usage:

```azurecli
azmcp eventhubs namespace update
```

With parameters:

```azurecli
azmcp eventhubs namespace update --resource-group <resource-group> --namespace <namespace> --location <location> --sku-name <sku-name> --sku-tier <sku-tier> --sku-capacity <sku-capacity> --is-auto-inflate-enabled <is-auto-inflate-enabled> --maximum-throughput-units <maximum-throughput-units> --kafka-enabled <kafka-enabled> --zone-redundant <zone-redundant> --tags <tags>
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
| `--namespace` | string | The name of the Event Hubs namespace to retrieve. Must be used with --resource-group option. |
| `--location` | string | The Azure region where the namespace is located (e.g., 'eastus', 'westus2'). |
| `--sku-name` | string | The SKU name for the namespace. Valid values: 'Basic', 'Standard', 'Premium'. |
| `--sku-tier` | string | The SKU tier for the namespace. Valid values: 'Basic', 'Standard', 'Premium'. |
| `--sku-capacity` | string | The SKU capacity (throughput units) for the namespace. Valid range depends on the SKU. |
| `--is-auto-inflate-enabled` | string | Enable or disable auto-inflate for the namespace. |
| `--maximum-throughput-units` | string | The maximum throughput units when auto-inflate is enabled. |
| `--kafka-enabled` | string | Enable or disable Kafka for the namespace. |
| `--zone-redundant` | string | Enable or disable zone redundancy for the namespace. |
| `--tags` | string | Tags for the namespace in JSON format (e.g., '{"key1":"value1","key2":"value2"}'). |

---
