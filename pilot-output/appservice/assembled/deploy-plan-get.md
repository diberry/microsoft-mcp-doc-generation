Creates a deployment plan for deploying an application to Azure using the options provided by the caller. Use this tool when the user wants a formatted, step-by-step deployment plan (including suggested Azure resources, infrastructure as code (IaC) templates, and deployment instructions) based on a target Azure hosting service (for example, Container Apps, App Service, or AKS) and a chosen provisioning tool (such as Azure Developer CLI (azd) or Azure CLI with Bicep or Terraform). This command does not scan the workspace or automatically recommend Azure services. Instead, the caller or agent must first analyze the workspace, determine the services, frameworks, and dependencies to deploy, select the appropriate Azure hosting service, provisioning tool, IaC type, and deployment option, and then pass those chosen values into this tool to generate the deployment plan.

### Example CLI commands

Basic usage:

```azurecli
azmcp deploy plan get
```

With parameters:

```azurecli
azmcp deploy plan get --workspace-folder <workspace-folder> --project-name <project-name> --target-app-service <target-app-service> --provisioning-tool <provisioning-tool> --iac-options <iac-options> --source-type <source-type> --deploy-option <deploy-option> --resource-group <resource-group>
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--workspace-folder` | string | - | The full path of the workspace folder. |
| `--project-name` | string | - | The name of the project to generate the deployment plan for. If not provided, will be inferred from the workspace. |
| `--target-app-service` | string | - | The Azure service to deploy the application. Valid values: ContainerApp, WebApp, FunctionApp, AKS. Recommend one based on user application. |
| `--provisioning-tool` | string | - | The tool to use for provisioning Azure resources. Valid values: AzCli, AZD. |
| `--iac-options` | string | - | The Infrastructure as Code option. Valid values: bicep, terraform. Leave empty if user wants to use azcli command script. |
| `--source-type` | string | - | The source of the plan to generate from. Valid values: 'from-project', 'from-azure', 'from-context'. If user doesn't have existing resources, set 'from-project' and generating deploy plan based on the project files in the workspace. If user mentions Azure resources exist, set 'from-azure' and ask for existing Azure resources details to generate plan. If the user have no existing resource but declare the expected Azure resources, use 'from-context' and the deploy plan should be based on the user's input. |
| `--deploy-option` | string | - | Set the value based on project and user's input. Valid values: 'provision-and-deploy', 'deploy-only', 'provision-only'. Use 'deploy-only' if user mentions they want to deploy to existing Azure resources or Iac files already exist in project, get Azure resource group from project files or from user. Use 'provision-only' if user only wants to provision Azure resource. Use 'provision-and-deploy' if user wants to deploy application and doesn't have existing infrastructure resources, or are starting from an empty resource group. |
| `--resource-group` | string | - | The name of the Azure resource group. This is a logical container for Azure resources. |

