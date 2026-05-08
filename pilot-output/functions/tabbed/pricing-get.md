### [MCP Server](#tab/mcp-server)

This tool executes `pricing get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Get Azure retail pricing information. CRITICAL/MANDATORY: Do NOT call this tool if the user only specifies a broad service name (e.g., 'Virtual Machines', 'Storage', 'SQL Database') without a specific SKU. Instead, FIRST ask the user which specific SKU or tier they want pricing for. If the user asks to compare pricing across regions or SKUs without specifying exact ARM SKU names, ask them which specific SKUs they want to compare. Do NOT assume or pick default SKUs. Only call this tool AFTER the user provides a specific SKU (--sku) or confirms they want all pricing for that service. Requires at least one filter: --sku, --service, --region, --service-family, or --filter. SAVINGS PLAN: 'SavingsPlan' is NOT a valid --price-type. Use --include-savings-plan flag instead. Valid --price-type values: Consumption, Reservation, DevTestConsumption. When --include-savings-plan is true, Consumption items include nested 'savingsPlan' array with 1-year/3-year pricing (mainly Linux VMs). FOR BICEP/ARM COST ESTIMATION: When user asks to estimate costs from a Bicep or ARM template file, read the file, extract each resource's type and SKU, call this tool for each resource and aggregate the monthly costs (hourly price * 730 hours/month).

### Example CLI commands

Basic usage:

```azurecli
azmcp pricing get
```

With parameters:

```azurecli
azmcp pricing get --currency <currency> --sku <sku> --service <service> --region <region> --service-family <service-family> --price-type <price-type> --include-savings-plan <include-savings-plan> --filter <filter>
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
| `--currency` | string | - | Currency code for pricing (e.g., USD, EUR). Default is USD. |
| `--sku` | string | - | ARM SKU name (e.g., Standard_D4s_v5, Standard_E64-16ds_v4) |
| `--service` | string | - | Azure service name (e.g., Virtual Machines, Storage, SQL Database) |
| `--region` | string | - | Azure region (e.g., eastus, westeurope, westus2) |
| `--service-family` | string | - | Service family (e.g., Compute, Storage, Databases, Networking) |
| `--price-type` | string | - | Price type filter (Consumption, Reservation, DevTestConsumption) |
| `--include-savings-plan` | string | - | Include savings plan pricing information (uses preview API version) |
| `--filter` | string | - | Raw OData filter expression for advanced queries (e.g., "meterId eq 'abc-123'") |

---
