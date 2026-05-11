Generate and download platform landing zone configurations for Azure Migrate projects.
Updates parameters, check existing landing zones, and view parameters status.

**Actions:**
- createmigrateproject: Create a new Azure Migrate project if one doesn't exist (requires location parameter)
- check: Check if a platform landing zone already exists
- update: Update all parameters for generation (collect ALL params in one call)
- generate: Generate the platform landing zone
- download: Download generated files to local workspace
- status: View cached parameters

**Context (required for most actions):**
- subscription, resourceGroup, migrateProjectName

**Create Azure Migrate Parameters (for 'createmigrateproject' action):**
- subscription, resourceGroup, migrateProjectName, location

**Generation Parameters (for 'update' action - collect ALL at once from user):**
| Parameter | Options | Default |
|-----------|---------|----------|
| regionType | single, multi | single |
| firewallType | azurefirewall, nva | azurefirewall |
| networkArchitecture | hubspoke, vwan | hubspoke |
| versionControlSystem | local, github, azuredevops | local |
| regions | comma-separated (e.g., eastus,westus) | eastus |
| environmentName | any string | prod |
| organizationName | any string | contoso |
| identitySubscriptionId | GUID | (uses main subscription) |
| managementSubscriptionId | GUID | (uses main subscription) |
| connectivitySubscriptionId | GUID | (uses main subscription) |

**Workflow:**
1. Ask the user if they want to create a new Azure Migrate project or use an existing one. If creating, collect location parameter and create the project.
2. action='createmigrateproject' - Create a new Azure Migrate project only if the user doesn't have one already. Requires location parameter.
3. action='check' - See if one already exists
4. action='update' with ALL parameters - Ask user to confirm defaults or provide values
5. action='generate' - Create the landing zone
6. action='download' - Get the files
7. Extract zip to workspace root

**IMPORTANT:** When using 'update', collect ALL parameters from the user in ONE call.
Show them the defaults and ask which ones they want to change.

### Example CLI commands

Basic usage:

```azurecli
azmcp azuremigrate platformlandingzone request
```

With parameters:

```azurecli
azmcp azuremigrate platformlandingzone request --resource-group <resource-group> --action <action> --region-type <region-type> --firewall-type <firewall-type> --network-architecture <network-architecture> --identity-subscription-id <identity-subscription-id> --management-subscription-id <management-subscription-id> --connectivity-subscription-id <connectivity-subscription-id> --security-subscription-id <security-subscription-id> --regions <regions> --environment-name <environment-name> --version-control-system <version-control-system> --organization-name <organization-name> --migrate-project-name <migrate-project-name> --migrate-project-resource-id <migrate-project-resource-id> --location <location>
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
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--action` | string | The action to perform: 'update' (set parameters), 'check' (check existing platform landing zone), 'generate' (generate platform landing zone), 'download' (get download instructions), 'status' (view parameter status). |
| `--region-type` | string | The region type for the Platform Landing Zone. Valid values: 'single', 'multi'. |
| `--firewall-type` | string | The firewall type for the Platform Landing Zone. Valid values: 'azurefirewall', 'nva'. |
| `--network-architecture` | string | The network architecture for the Platform Landing Zone. Valid values: 'hubspoke', 'vwan'. |
| `--identity-subscription-id` | string | The Azure subscription ID for the identity management group in Platform Landing Zone (GUID format). |
| `--management-subscription-id` | string | The Azure subscription ID for the management group in Platform Landing Zone (GUID format). |
| `--connectivity-subscription-id` | string | The Azure subscription ID for the connectivity group in Platform Landing Zone (GUID format). |
| `--security-subscription-id` | string | The Azure subscription ID for security resources in Platform Landing Zone (GUID format). |
| `--regions` | string | Comma-separated list of Azure regions for Platform Landing Zone (e.g., 'eastus,westus2'). |
| `--environment-name` | string | The environment name for the Platform Landing Zone. |
| `--version-control-system` | string | The version control system for the Platform Landing Zone. Valid values: 'local', 'github', 'azuredevops'. |
| `--organization-name` | string | The organization name for the Platform Landing Zone. |
| `--migrate-project-name` | string | The Azure Migrate project name for Platform Landing Zone generation context. |
| `--migrate-project-resource-id` | string | The full resource ID of the Azure Migrate project for Platform Landing Zone (alternative to subscription/resourceGroup/migrateProjectName). |
| `--location` | string | The Azure region location for creating new resources (e.g., 'eastus', 'westus2'). Required for 'createmigrateproject' action. |

