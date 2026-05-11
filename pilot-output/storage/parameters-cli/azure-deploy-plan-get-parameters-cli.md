---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
| Parameter | Type | Description |
|-----------|------|-------------|
| `--workspace-folder` | string | The full path of the workspace folder. |
| `--project-name` | string | The name of the project to generate the deployment plan for. If not provided, will be inferred from the workspace. |
| `--target-app-service` | string | The Azure service to deploy the application. Valid values: ContainerApp, WebApp, FunctionApp, AKS. Recommend one based on user application. |
| `--provisioning-tool` | string | The tool to use for provisioning Azure resources. Valid values: AzCli, AZD. |
| `--iac-options` | string | The Infrastructure as Code option. Valid values: bicep, terraform. Leave empty if user wants to use azcli command script. |
| `--source-type` | string | The source of the plan to generate from. Valid values: 'from-project', 'from-azure', 'from-context'. If user doesn't have existing resources, set 'from-project' and generating deploy plan based on the project files in the workspace. If user mentions Azure resources exist, set 'from-azure' and ask for existing Azure resources details to generate plan. If the user have no existing resource but declare the expected Azure resources, use 'from-context' and the deploy plan should be based on the user's input. |
| `--deploy-option` | string | Set the value based on project and user's input. Valid values: 'provision-and-deploy', 'deploy-only', 'provision-only'. Use 'deploy-only' if user mentions they want to deploy to existing Azure resources or Iac files already exist in project, get Azure resource group from project files or from user. Use 'provision-only' if user only wants to provision Azure resource. Use 'provision-and-deploy' if user wants to deploy application and doesn't have existing infrastructure resources, or are starting from an empty resource group. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
