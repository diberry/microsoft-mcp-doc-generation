### [MCP Server](#tab/mcp-server)

This tool executes `get azure bestpractices get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

This tool returns a list of best practices for code generation, operations and deployment
        when working with Azure services. It should be called for any code generation, deployment or
        operations involving Azure, Azure Functions, Azure Kubernetes Service (AKS), Azure Container
        Apps (ACA), Bicep, Terraform, Azure Cache, Redis, CosmosDB, Entra, Azure Active Directory,
        Azure App Services, or any other Azure technology or programming language. Only call this function
        when you are confident the user is discussing Azure. If this tool needs to be categorized,
        it belongs to the Azure Best Practices category.

### Example CLI commands

Basic usage:

```azurecli
azmcp get azure bestpractices get
```

With parameters:

```azurecli
azmcp get azure bestpractices get --resource <resource> --action <action>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--resource` | string | The Azure resource type for which to get best practices. Options: 'general' (general Azure), 'azurefunctions' (Azure Functions), 'static-web-app' (Azure Static Web Apps), 'coding-agent' (Coding Agent). |
| `--action` | string | The action type for the best practices. Options: 'all', 'code-generation', 'deployment'. Note: 'static-web-app' and 'coding-agent' resources only supports 'all'. |

---
