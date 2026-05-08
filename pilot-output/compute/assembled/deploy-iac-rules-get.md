Retrieves rules and best practices for creating Bicep and Terraform Infrastructure as Code (IaC) files to deploy Azure applications. Use this tool when the user asks for rules, guidelines, or best practices for writing Bicep scripts or Terraform templates for Azure resources. The rules cover Azure resource configuration standards, compatibility with Azure Developer CLI (azd) and Azure CLI, and general IaC quality requirements. Use when user asks: show me the rules and best practices for writing Bicep and Terraform IaC for Azure.

### Example CLI commands

Basic usage:

```azurecli
azmcp deploy iac rules get
```

With parameters:

```azurecli
azmcp deploy iac rules get --deployment-tool <deployment-tool> --iac-type <iac-type> --resource-types <resource-types>
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--deployment-tool` | string | - | The deployment tool to use. Valid values: AzCli, AZD |
| `--iac-type` | string | - | The type of IaC file used for deployment. Valid values: bicep, terraform. Leave empty ONLY if user wants to use AzCli command script and no IaC file. |
| `--resource-types` | string | - | List of Azure resource types to generate rules for. Get the value from context and use the same resources defined in plan. Valid value: 'appservice','containerapp','function','aks','azuredatabaseforpostgresql','azuredatabaseformysql','azuresqldatabase','azurecosmosdb','azurestorageaccount','azurekeyvault' |

