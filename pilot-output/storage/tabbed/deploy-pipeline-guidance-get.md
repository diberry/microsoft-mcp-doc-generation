### [MCP Server](#tab/mcp-server)

This tool executes `deploy pipeline guidance get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Generates CI/CD pipeline configuration and step-by-step guidance for deploying an application to Azure using GitHub Actions or Azure DevOps pipelines. Use this tool when the user wants to create a CI/CD pipeline, set up automated deployment workflows, or configure pipeline files to deploy their application to Azure. Supports both Azure Developer CLI (azd) and Azure CLI based deployments, and can generate pipelines that provision infrastructure and deploy application code. Before calling this tool, confirm with the user whether they prefer GitHub Actions or Azure DevOps, and whether they have existing Azure resources for their deployment environments. Use when user asks: how do I set up a CI/CD pipeline with GitHub Actions or Azure DevOps to deploy my app to Azure?

### Example CLI commands

Basic usage:

```azurecli
azmcp deploy pipeline guidance get
```

With parameters:

```azurecli
azmcp deploy pipeline guidance get --is-azd-project <is-azd-project> --pipeline-platform <pipeline-platform> --deploy-option <deploy-option>
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
| `--is-azd-project` | string | Whether to use azd tool in the deployment pipeline. Set to true ONLY if azure.yaml is provided or the context suggests AZD tools. |
| `--pipeline-platform` | string | The platform for the deployment pipeline. Valid values: github-actions, azure-devops. |
| `--deploy-option` | string | Valid values: deploy-only, provision-and-deploy. Default to deploy-only. Set to 'provision-and-deploy' ONLY WHEN user explicitly wants infra provisioning pipeline using local provisioning scripts. |

---
