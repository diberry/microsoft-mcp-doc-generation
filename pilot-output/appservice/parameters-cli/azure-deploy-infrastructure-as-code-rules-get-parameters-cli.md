---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--deployment-tool` | string | - | The deployment tool to use. Valid values: AzCli, AZD |
| `--iac-type` | string | - | The type of IaC file used for deployment. Valid values: bicep, terraform. Leave empty ONLY if user wants to use AzCli command script and no IaC file. |
| `--resource-types` | string | - | List of Azure resource types to generate rules for. Get the value from context and use the same resources defined in plan. Valid value: 'appservice','containerapp','function','aks','azuredatabaseforpostgresql','azuredatabaseformysql','azuresqldatabase','azurecosmosdb','azurestorageaccount','azurekeyvault' |
