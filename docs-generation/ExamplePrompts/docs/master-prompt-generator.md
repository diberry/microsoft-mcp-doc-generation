# Master Prompt for Generating Example Prompts

This prompt template is used to generate example prompts for Azure MCP Server tool documentation.

## Instructions

Use this prompt to generate natural, conversational example prompts for tool documentation. Replace the placeholders with actual values from your tool documentation.

## Input Variables

- **TOOL_NAME**: The friendly name of the tool (e.g., "List subscriptions", "Create storage account")
- **TOOL_COMMAND**: The HTML comment command (e.g., `<!-- subscription list -->`, `<!-- storage account create -->`)
- **ACTION_VERB**: The primary action (e.g., "list", "create", "get", "query", "upload", "delete")
- **RESOURCE_TYPE**: The Azure resource being operated on (e.g., "subscriptions", "storage account", "blob", "secret")
- **PARAMETERS**: Array of parameter objects with:
  - `name`: Parameter name
  - `required`: Boolean (true/false)
  - `description`: Parameter description

## Prompt Template

```
Generate 5 diverse, natural language example prompts for an Azure MCP Server tool with the following characteristics:

Tool Name: {TOOL_NAME}
Tool Command: {TOOL_COMMAND}
Action: {ACTION_VERB}
Resource: {RESOURCE_TYPE}

Parameters:
{{#each PARAMETERS}}
- {{name}} ({{#if required}}Required{{else}}Optional{{/if}}): {{description}}
{{/each}}

Requirements:
1. Create exactly 5 example prompts
2. Each prompt should be conversational and natural, as a developer would ask
3. Vary the phrasing significantly between prompts (use different sentence structures, synonyms, and approaches)
4. For required parameters, use realistic example values in quotes (e.g., 'mystorageaccount', 'my-resource-group', 'eastus')
5. Mix formal and informal language styles
6. Include variations like:
   - Direct commands ("Show me...", "List...", "Get...")
   - Question format ("What...?", "How many...?")
   - Request format ("I need to...", "Can you...")
   - Imperative format ("Create...", "Add...", "Upload...")
7. When multiple required parameters exist, demonstrate different ordering and natural ways to express them
8. Make values realistic to Azure naming conventions (lowercase for storage accounts, kebab-case for resource groups, etc.)
9. Each prompt should be a complete, standalone instruction that includes all required parameters
10. Format as a bulleted list with a bolded category label followed by a colon, then the prompt in quotes

Example format:
- **Category label**: "The example prompt text"

Categories to use (choose the appropriate category based on the tool's action and vary labels naturally within that category):
- For list/query operations: "List [resource]", "Show [resource]", "Find [resource]", "Query [resource]", "Check [resource]"
- For create operations: "Create [resource]", "New [resource]", "Set up [resource]", "Add [resource]", "Make [resource]"
- For get/retrieve operations: "Get [resource] details", "View [resource] info", "Retrieve [resource]", "Check [resource]", "Find [resource]"
- For update operations: "Update [resource]", "Modify [resource]", "Change [resource]", "Edit [resource]", "Configure [resource]"
- For delete operations: "Delete [resource]", "Remove [resource]", "Clean up [resource]"
- For upload/download: "Upload [file/data]", "Download [file/data]", "Transfer [resource]"
- For query/search: "Query [resource]", "Search [resource]", "Find [items]"

Note: Use only the category that matches the tool's ACTION_VERB. All 5 prompts should use varied labels from that same category.

Generate the prompts now:
```

## Example Usage

### Example 1: List Subscriptions (No Required Parameters)

**Input:**
- TOOL_NAME: "List subscriptions"
- TOOL_COMMAND: `<!-- subscription list -->`
- ACTION_VERB: "list"
- RESOURCE_TYPE: "subscriptions"
- PARAMETERS: [] (empty array, no parameters)

**Generated Output:**
```
- **List subscriptions**: "Show me all of my subscriptions."
- **Find subscriptions**: "List all subscriptions starting with `northeast`."
- **View subscriptions**: "What subscriptions do I have access to?"
- **Query subscriptions**: "I need to see all available subscriptions"
- **Check subscriptions**: "Show my Azure subscriptions"
```

### Example 2: Create Storage Account (Multiple Required Parameters)

**Input:**
- TOOL_NAME: "Create storage account"
- TOOL_COMMAND: `<!-- storage account create -->`
- ACTION_VERB: "create"
- RESOURCE_TYPE: "storage account"
- PARAMETERS:
  - {name: "Resource group", required: true, description: "The name of the Azure resource group"}
  - {name: "Account", required: true, description: "The name of the storage account to create"}
  - {name: "Location", required: true, description: "The Azure region where the account will be created"}
  - {name: "SKU", required: false, description: "The storage account SKU"}

**Generated Output:**
```
- **Create account**: "Create a storage account named 'mystorageaccount' in resource group 'my-resource-group' in location 'eastus'."
- **New storage**: "Set up a new storage account called 'datastore2025' in resource group 'my-resource-group' in region 'westus2'."
- **Specify SKU and kind**: "Create a storage account 'mydata' in resource group 'my-resource-group' in location 'eastus' with Standard_GRS."
- **Secure storage**: "Create a storage account 'securestore' in resource group 'my-resource-group' in location 'centralus' with HTTPS only."
- **Enable Data Lake**: "Set up a storage account 'datalakeacct' in resource group 'my-resource-group' in location 'westeurope' with hierarchical namespace enabled."
```

### Example 3: Query Cosmos DB Container (Mix of Required and Optional)

**Input:**
- TOOL_NAME: "Query container items"
- TOOL_COMMAND: `<!-- cosmos database container item query -->`
- ACTION_VERB: "query"
- RESOURCE_TYPE: "container items"
- PARAMETERS:
  - {name: "Account name", required: true, description: "The name of the Cosmos DB account"}
  - {name: "Database name", required: true, description: "The name of the database"}
  - {name: "Container name", required: true, description: "The name of the container"}
  - {name: "Query", required: false, description: "SQL query to execute"}

**Generated Output:**
```
- **Simple query**: "Query all orders placed after January 1, 2025 from the 'orders' container in database 'sales'"
- **Filter query**: "Find all products with price less than $50 in the 'products' container"
- **Complex query**: "Query items where category is 'electronics' and stock is greater than 10"
- **Join query**: "Show me orders with their related customer information"
- **Aggregation query**: "Count how many orders we have by status in the 'orders' container"
```

## Notes

- **Required parameters** must always appear in the example prompts with realistic values
- **Optional parameters** can be included in some examples but not all
- Use **realistic Azure naming conventions**:
  - Storage accounts: lowercase, no hyphens (e.g., 'mystorageaccount')
  - Resource groups: kebab-case (e.g., 'my-resource-group')
  - Locations: lowercase region names (e.g., 'eastus', 'westus2')
  - Container names: lowercase (e.g., 'documents', 'images')
- **Vary complexity**: Some prompts should be simple and direct, others should be more complex with multiple parameters or conditions
- **Natural language**: Prompts should sound like real developers asking questions, not formal API documentation
