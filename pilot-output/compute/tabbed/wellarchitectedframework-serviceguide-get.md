### [MCP Server](#tab/mcp-server)

This tool executes `wellarchitectedframework serviceguide get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Get Azure Well-Architected Framework guidance for a specific Azure service, or list all supported services when no service is specified. When a service is provided, returns architectural best practices, design patterns, and recommendations based on the five pillars: reliability, security, cost optimization, operational excellence, and performance efficiency. Optional: --service: A single Azure service name. Service name format: case-insensitive; hyphens, underscores, spaces, and name variations allowed; use double quotes (not single quotes) for names with spaces. e.g., cosmos-db, Cosmos_DB, "Cosmos DB", cosmosdb, cosmos-database, cosmosdatabase

### Example CLI commands

Basic usage:

```azurecli
azmcp wellarchitectedframework serviceguide get
```

With parameters:

```azurecli
azmcp wellarchitectedframework serviceguide get --service <service>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--service` | string | A single Azure service name. Service name format: case-insensitive; hyphens, underscores, spaces, and name variations allowed; use double quotes (not single quotes) for names with spaces. e.g., cosmos-db, Cosmos_DB, "Cosmos DB", cosmosdb, cosmos-database, cosmosdatabase |

---
