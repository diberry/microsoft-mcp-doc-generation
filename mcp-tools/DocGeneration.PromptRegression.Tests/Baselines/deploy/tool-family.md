---

title: Azure MCP Server tools for Azure Deploy
description: Use Azure MCP Server tools to manage deployments and deployment pipelines for Azure applications and infrastructure with natural language prompts from your IDE.
ms.date: 03/25/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 5
mcp-cli.version: 2.0.0-beta.31+ed24dd9783f26645fd2b7218b4d52221b446354f
author: diberry
ms.author: diberry
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Deploy

The Azure MCP Server lets you manage deployments and CI/CD pipelines, including generating architecture diagrams, collecting application logs, producing deploy plans, and validating infrastructure-as-code rules, with natural language prompts.

Azure Deploy is a set of tools and workflows for orchestrating deployments and pipelines across Azure services. For more information, see [Azure Deploy documentation](/azure/devops/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Generate architecture diagram

<!-- @mcpcli deploy architecture diagram generate -->

Generates an Azure service architecture diagram that shows recommended Azure services and their connections for an application. This tool renders the diagram from an application topology (`AppTopology`) that you provide as input.

The tool builds the topology by scanning the workspace to detect services, frameworks, and environment variables that contain connection strings, and, for .NET Aspire applications, by checking `aspireManifest.json`. The output includes a visual diagram and a machine-readable topology that maps each service to a recommended Azure resource, such as Azure App Service, Azure Container Apps, Azure Functions, Azure Static Web Apps, Azure Cosmos DB, Azure Key Vault, and Azure Kubernetes Service (AKS).

The tool detects containerized services and suggests container hosting based on Docker artifacts. If a service doesn't expose a port, the tool defaults to `80`. This tool isn't intended for detailed network topology or security design.

<!-- Required parameters: 1 - 'Raw mcp tool input' -->

Example prompts include:

- "Generate the azure architecture diagram for this application with Raw mcp tool input '{"workspaceFolder":"/home/dev/inventory-app","projectName":"inventory-app","services":[{"name":"api","path":"src/api","language":"node","port":"8080","azureComputeHost":"appservice","dependencies":[],"settings":["DATABASE_URL"]}]}'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Raw mcp tool input** |  Required | JSON object that defines the input structure for this tool. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get app logs

<!-- @mcpcli deploy app logs get -->

This tool, part of the Model Context Protocol (MCP) server, shows application logs from the Log Analytics workspace that is associated with applications you deploy with the Azure Developer CLI (`azd`). It discovers the correct workspace and related resources from your azd environment configuration. You can view recent logs for Azure Container Apps, Azure App Service, and Function App resources to check deployment status and troubleshoot post-deployment issues.

<!-- Required parameters: 2 - 'Azd env name', 'Workspace folder' -->

Example prompts include:

- "Show me the log of the application deployed by azd for Azd env name 'dev' and workspace folder '/home/azure/myapp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Azd env name** |  Required | The name of the environment created by azd (AZURE_ENV_NAME) during `azd init` or `azd up`. If not, provided in context, the tool tries to find it in the `.azure` directory in the workspace or you can list environments with `azd env list`. |
| **Workspace folder** |  Required | The full path of the workspace folder that contains the azd project. |
| **Limit** |  Optional | The maximum number of log rows to retrieve. Use this to limit returned logs or to avoid reaching token limits. Default is 200. |
| **Subscription** |  Optional | The Azure subscription to use. Accepts a subscription ID (GUID) or display name. If not, specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get iac rules

<!-- @mcpcli deploy iac rules get -->

This tool retrieves rules and best practices for creating Bicep and Terraform Infrastructure as Code (IaC) files that deploy Azure applications. This tool, part of the Model Context Protocol (MCP) server, returns actionable guidance on Azure resource configuration standards, compatibility with Azure Developer CLI (azd) and Azure CLI, and general IaC quality requirements.

You specify the `Deployment tool` parameter to indicate the target deployment tooling. You can optionally scope results by setting the `Iac type` parameter or by providing specific resource types with the `Resource types` parameter. The output lists naming and parameterization patterns, security and role-based access control (RBAC) recommendations, idempotency guidance, and compatibility notes for Azure Developer tools.

Common resource types include `Azure App Service`, `Azure Container Apps`, and `Azure Functions`. You can also scope results to `Azure Kubernetes Service (AKS)`, `Azure Database for PostgreSQL`, `Azure Database for MySQL`, `Azure SQL Database`, `Azure Cosmos DB`, `Azure Storage` accounts, and `Azure Key Vault`.

Example prompt:
"Get IaC rules for 'appservice' and 'azuresqldatabase' using 'AzCli'"

<!-- Required parameters: 1 - 'Deployment tool' -->

Example prompts include:

- "Show me the rules and best practices for writing Bicep and Terraform IaC for Azure with deployment tool 'AzCli'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Deployment tool** |  Required | The deployment tool to use. Valid values: AzCli, AZD. |
| **Iac type** |  Optional | The type of IaC file used for deployment. Valid values: bicep, terraform. Leave empty ONLY if user wants to use AzCli command script and no IaC file. |
| **Resource types** |  Optional | List of Azure resource types to generate rules for. Get the value from context and use the same resources defined in plan. Valid value: 'appservice','containerapp','function','aks','azuredatabaseforpostgresql','azuredatabaseformysql','azuresqldatabase','azurecosmosdb','azurestorageaccount','azurekeyvault'. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get pipeline guidance

<!-- @mcpcli deploy pipeline guidance get -->

Generates CI/CD pipeline configuration and step-by-step guidance for deploying an application to Azure with GitHub Actions or Azure DevOps pipelines. This tool supports Azure Developer CLI (azd) and Azure CLI deployments, and it can generate pipelines that provision infrastructure and deploy application code. You specify the pipeline platform and whether the project uses the Azure Developer CLI (azd). The output includes pipeline YAML files and guidance for authentication, provisioning, and deployment steps.

This tool is part of the Azure Model Context Protocol (MCP) tools.

<!-- Required parameters: 3 - 'Deploy option', 'Is azd project', 'Pipeline platform' -->

Example prompts include:

- "How do I set up a CI/CD pipeline with GitHub Actions to deploy my app to Azure using Deploy option 'deploy-only', Is azd project 'false', Pipeline platform 'github-actions'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Deploy option** |  Required | Valid values: `deploy-only`, `provision-and-deploy`. Default to `deploy-only`. Set to `provision-and-deploy` only when the user explicitly wants an infrastructure provisioning pipeline that uses local provisioning scripts. |
| **Is azd project** |  Required | Whether to use the Azure Developer CLI (`azd`) in the deployment pipeline. Set to `true` only if `azure.yaml` is provided or the project context indicates use of azd tools. |
| **Pipeline platform** |  Required | The platform for the deployment pipeline. Valid values: `github-actions`, `azure-devops`. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

Example: "Generate a GitHub Actions pipeline that provisions infrastructure and deploys a Node.js app to Azure App Service. Set Deploy option to 'provision-and-deploy', Pipeline platform to 'github-actions', and Is azd project to 'false'."

## Get deploy plan

<!-- @mcpcli deploy plan get -->

The Model Context Protocol (MCP) get tool creates a formatted, step-by-step deployment plan for deploying an application to Azure from the options you provide. The plan includes suggested Azure resources, infrastructure as code (IaC) templates, and deployment instructions. You specify a target Azure hosting service, provisioning tool, IaC type, and deployment option to shape the plan. The tool doesn't scan your workspace or infer services automatically. Provide project details or existing Azure resource information so the plan reflects your environment.

The tool returns a deployment plan and example IaC templates or command scripts that match the selected provisioning tool and IaC type. Provide the `Workspace folder` path and either the `Project name` or explicit resource details when you generate a plan from project files or from existing Azure resources.

<!-- Required parameters: 6 - 'Deploy option', 'Project name', 'Provisioning tool', 'Source type', 'Target app service', 'Workspace folder' -->

Example prompts include:

- "How do I create a step-by-step deployment plan for project 'order-api' with deploy option 'provision-and-deploy', provisioning tool 'AZD', source type 'from-project', target app service 'ContainerApp', workspace folder '/home/dev/projects/order-api', and Iac options 'bicep'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Deploy option** |  Required | Set the value based on the project and user input. Valid values: `provision-and-deploy`, `deploy-only`, `provision-only`. Choose `deploy-only` if you deploy to existing Azure resources or if IaC files already exist in the project. Choose `provision-only` if you only provision Azure resources. Choose `provision-and-deploy` if you need both provisioning and deployment. |
| **Project name** |  Required | The name of the project to generate the deployment plan for. If not, provided, the tool infers the project name from the workspace. |
| **Provisioning tool** |  Required | The tool to use for provisioning Azure resources. Valid values: `AzCli`, `AZD`. `AzCli` maps to Azure CLI, and `AZD` maps to Azure Developer CLI (azd). |
| **Source type** |  Required | The source of the plan. Valid values: `from-project`, `from-azure`, `from-context`. Use `from-project` to generate a plan from project files in the workspace. Use `from-azure` to generate a plan based on existing Azure resources; provide resource details. Use `from-context` when you describe expected Azure resources and want the plan to reflect that context. |
| **Target app service** |  Required | The Azure hosting service to deploy the application to. Valid values: ContainerApp, WebApp, FunctionApp, AKS. `AKS` maps to Azure Kubernetes Service. Recommend one option based on the application architecture. |
| **Workspace folder** |  Required | The full path of the workspace folder that contains the project files. |
| **Iac options** |  Optional | The Infrastructure as Code option. Valid values: `bicep`, `terraform`. Leave empty to use Azure CLI command scripts. |
| **Resource group** |  Optional | The name of the Azure resource group. This resource group is a logical container for Azure resources. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure deployment documentation](/azure/azure-resource-manager/templates/)