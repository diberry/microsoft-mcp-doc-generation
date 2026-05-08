### [MCP Server](#tab/mcp-server)

This tool executes `marketplace product list` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieves and lists all marketplace products (offers) available to a subscription in the Azure Marketplace. Use this tool to search, select, browse, or filter marketplace offers by product name, publisher, pricing, or metadata. Returns information for each product, including display name, publisher details, category, pricing data, and available plans.

### Example CLI commands

Basic usage:

```azurecli
azmcp marketplace product list
```

With parameters:

```azurecli
azmcp marketplace product list --language <language> --search <search> --filter <filter> --orderby <orderby> --select <select> --next-cursor <next-cursor> --expand <expand>
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
| `--language` | string | - | Product language code (e.g., 'en' for English, 'fr' for French). |
| `--search` | string | - | Search for products using a short general term (up to 25 characters) |
| `--filter` | string | - | OData filter expression to filter results based on ProductSummary properties (e.g., "displayName eq 'Azure'"). |
| `--orderby` | string | - | OData orderby expression to sort results by ProductSummary fields (e.g., "displayName asc" or "popularity desc"). |
| `--select` | string | - | OData select expression to choose specific ProductSummary fields to return (e.g., "displayName,publisherDisplayName,uniqueProductId"). |
| `--next-cursor` | string | - | Pagination cursor to retrieve the next page of results. Use the NextPageLink value from a previous response. |
| `--expand` | string | - | OData expand expression to include related data in the response (e.g., "plans" to include plan details). |

---
