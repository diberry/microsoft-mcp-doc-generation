### [MCP Server](#tab/mcp-server)

This tool executes `marketplace product get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieves detailed information about a specific Azure Marketplace product (offer) for a given subscription,
 including available plans, pricing, and product metadata.

### Example CLI commands

Basic usage:

```azurecli
azmcp marketplace product get
```

With parameters:

```azurecli
azmcp marketplace product get --product-id <product-id> --include-stop-sold-plans <include-stop-sold-plans> --language <language> --market <market> --lookup-offer-in-tenant-level <lookup-offer-in-tenant-level> --plan-id <plan-id> --sku-id <sku-id> --include-service-instruction-templates <include-service-instruction-templates> --pricing-audience <pricing-audience>
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
| `--product-id` | string | - | The ID of the marketplace product to retrieve. This is the unique identifier for the product in the Azure Marketplace. |
| `--include-stop-sold-plans` | string | - | Include stop-sold or hidden plans in the response. |
| `--language` | string | - | Product language code (e.g., 'en' for English, 'fr' for French). |
| `--market` | string | - | Product market code (e.g., 'US' for United States, 'UK' for United Kingdom). |
| `--lookup-offer-in-tenant-level` | string | - | Check against tenant private audience when retrieving the product. |
| `--plan-id` | string | - | Filter results by a specific plan ID. |
| `--sku-id` | string | - | Filter results by a specific SKU ID. |
| `--include-service-instruction-templates` | string | - | Include service instruction templates in the response. |
| `--pricing-audience` | string | - | Pricing audience for the request header. |

---
