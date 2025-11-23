# Tool Description Analyzer Prompt

This prompt helps identify which parts of a tool description are meant for human users versus MCP host/client systems.

## Purpose

When documenting MCP tools, descriptions contain two types of information:
1. **Human-facing content**: Explains what the tool does in accessible language for documentation readers
2. **MCP host/client-facing content**: Technical details that the MCP system uses to understand tool behavior, constraints, and invocation patterns

This prompt analyzes tool descriptions to separate these concerns.

## The Prompt

```
Analyze the following Azure MCP Server tool description and separate it into two distinct parts:

1. **HUMAN-FACING CONTENT** - Information meant for documentation readers (developers using the tool)
2. **MCP HOST/CLIENT-FACING CONTENT** - Technical details for the MCP system to understand tool behavior

Tool Information:
---
Tool Name: {TOOL_NAME}
Tool Command: {TOOL_COMMAND}
Tool Description: {TOOL_DESCRIPTION}
Parameters: {PARAMETERS_TABLE}
---

Classification Guidelines:

HUMAN-FACING CONTENT includes:
- What the tool does in plain language
- Why a user would want to use this tool
- Business/functional purpose
- User benefits and capabilities
- Examples of use cases
- Context about the Azure service
- Helpful explanations of what gets returned

MCP HOST/CLIENT-FACING CONTENT includes:
- Explicit negative instructions (e.g., "Not for listing...", "Do not use this tool to...")
- Required vs optional parameters and their exact format
- Technical constraints and limitations
- Return value specifications (exact field names, data types)
- Command syntax and flags
- Conditional behavior (e.g., "If no X specified, then Y")
- Tool selection hints (when to use this tool vs another)
- Idempotency, destructiveness, or state-change information
- Authentication requirements
- Data format specifications (JSON, base64, etc.)
- Version handling (e.g., "creates new version if exists")

Output Format:
--------------

### HUMAN-FACING CONTENT
[One or more sentences describing what the tool does for the user, written naturally]

### MCP HOST/CLIENT-FACING CONTENT
[Technical specifications, constraints, and system behavior details as bullet points]
- Point 1
- Point 2
- ...

### REASONING
[Brief explanation of why you classified certain phrases one way or another]
```

## Example 1: Simple Tool (Cosmos DB Account List)

**Input:**
```
Tool Name: Account List
Tool Command: <!-- cosmos account list -->
Tool Description: List all Cosmos DB accounts in a subscription. This command retrieves and displays all Cosmos DB accounts available in the specified subscription. Results include account names and are returned as a JSON array.
Parameters: None
```

**Expected Output:**
```
### HUMAN-FACING CONTENT
List all Cosmos DB accounts in a subscription. This provides a quick overview of your Cosmos DB resources.

### MCP HOST/CLIENT-FACING CONTENT
- Retrieves and displays all Cosmos DB accounts available in the specified subscription
- Results include account names
- Returns data as a JSON array
- No parameters required

### REASONING
The phrase "List all Cosmos DB accounts" is user-focused and explains the action. The phrase "This provides a quick overview" adds user value context. The technical details about JSON array return format and what fields are included are for the MCP system to understand data structure.
```

## Example 2: Complex Tool (Key Vault Secret Get)

**Input:**
```
Tool Name: Secret Get
Tool Command: <!-- keyvault secret get -->
Tool Description: Get/retrieve/show details for a single secret in an Azure Key Vault (latest version). Not for listing multiple secrets. Required: --vault <vault>, --secret <secret> --subscription <subscription>. Optional: --tenant <tenant>. Returns: name, value, ID, contentType, enabled, notBefore, expiresOn, createdOn, updatedOn, tags.
Parameters:
- Vault (Required): The name of the Key Vault
- Secret (Required): The name of the secret
```

**Expected Output:**
```
### HUMAN-FACING CONTENT
Retrieve a specific secret from a Key Vault. This is useful for accessing sensitive configuration values, API keys, connection strings, and other secrets stored securely in Azure Key Vault.

### MCP HOST/CLIENT-FACING CONTENT
- Gets the latest version of the secret only
- NOT for listing multiple secrets (use Secret List tool instead)
- Required parameters: vault name, secret name, subscription
- Optional parameter: tenant ID
- Returns the following fields: name, value, ID, contentType, enabled, notBefore, expiresOn, createdOn, updatedOn, tags
- Command syntax: --vault <vault>, --secret <secret>, --subscription <subscription>, --tenant <tenant> (optional)

### REASONING
The "Get/retrieve/show details" and usage description are human-focused. The explicit negative instruction "Not for listing multiple secrets" is an MCP system hint to prevent tool misuse. The detailed parameter requirements with exact flag names and the comprehensive list of return fields are technical specifications for the MCP host.
```

## Example 3: Tool with Constraints (Storage Blob Upload)

**Input:**
```
Tool Name: Blob Upload
Tool Command: <!-- storage blob upload -->
Tool Description: Uploads a local file to an Azure Storage blob, only if the blob does not exist, returning the last modified time, ETag, and content hash of the uploaded blob.
Parameters:
- Account (Required): The name of the Azure Storage account
- Container (Required): The name of the container
- Blob (Required): The name of the blob (full path within container)
- Local file path (Required): The local file path to read content from
```

**Expected Output:**
```
### HUMAN-FACING CONTENT
Upload a local file to Azure Storage blob storage. This allows you to store files in the cloud for scalable, durable storage.

### MCP HOST/CLIENT-FACING CONTENT
- Only uploads if the blob does NOT already exist (prevents overwrites)
- Requires a local file as input source
- Returns: last modified time, ETag, content hash
- All parameters are required: account name, container name, blob path, local file path
- Blob path should be the full path within the container (e.g., 'file.txt' or 'folder/file.txt')
- Local file path must be accessible on the local system

### REASONING
"Upload a local file" explains the action for users. The critical constraint "only if the blob does not exist" is essential for the MCP system to understand idempotency behavior. The return value specification and path format requirements are technical details for proper tool invocation.
```

## Example 4: Tool with Disambiguation (Storage Blob Container Get)

**Input:**
```
Tool Name: Blob Container Get
Tool Command: <!-- storage blob container get -->
Tool Description: Show/list containers in a storage account. Use this tool to list all blob containers in the storage account or show details for a specific Storage container. Displays container properties including access policies, lease status, and metadata. If no container specified, shows all containers in the storage account. Required: account <account>, subscription <subscription>. Optional: container <container>, tenant <tenant>. Returns: container name, lastModified, leaseStatus, publicAccessLevel, metadata, and container properties. Do not use this tool to list blobs in a container.
Parameters:
- Account (Required): The name of the Azure Storage account
- Container (Optional): The name of the container to access
```

**Expected Output:**
```
### HUMAN-FACING CONTENT
View blob containers in a storage account. You can list all containers or get details about a specific container, including its properties, access policies, lease status, and metadata.

### MCP HOST/CLIENT-FACING CONTENT
- Dual purpose: lists all containers OR shows specific container details
- Conditional behavior: If no container parameter provided, shows ALL containers in the account
- Required parameters: account name, subscription
- Optional parameters: container name, tenant ID
- Returns: container name, lastModified, leaseStatus, publicAccessLevel, metadata, container properties
- Do NOT use this tool to list blobs within a container (use Blob Get tool instead)
- Command syntax: account <account>, subscription <subscription>, container <container> (optional), tenant <tenant> (optional)

### REASONING
The "View blob containers" describes the user action. The explicit negative instruction "Do not use this tool to list blobs in a container" is crucial for the MCP system to select the correct tool. The conditional behavior based on whether container is specified helps the MCP understand when to invoke this tool. The detailed return fields and parameter requirements are technical specifications.
```

## Usage Instructions

1. **Extract the raw tool description** from the generated documentation
2. **Identify all parameters** from the parameters table
3. **Copy the prompt template** above
4. **Fill in the placeholders** with your tool information
5. **Submit to an AI assistant** for analysis
6. **Review the output** to ensure proper separation
7. **Use the human-facing content** for the main documentation description
8. **Use the MCP host/client-facing content** in system prompts, tool metadata, or technical specifications

## Common Patterns

### Human-Facing Patterns
- "List/Get/Create/Update/Delete [resource]"
- "This helps you..."
- "This allows you to..."
- "Useful for..."
- "Use this when..."
- Business value statements
- Plain language explanations

### MCP Host/Client-Facing Patterns
- "Required: --flag <value>"
- "Optional: --flag <value>"
- "Returns: field1, field2, field3"
- "Not for [action]..."
- "Do not use this tool to..."
- "If no X specified, then Y"
- "Creates new version if exists"
- Technical constraints and exact formats
- Command syntax with flags
- Conditional logic descriptions
- Tool disambiguation hints

## Quality Checklist

After analyzing a tool description, verify:

- [ ] Human-facing content is written in natural, accessible language
- [ ] Human-facing content explains the "what" and "why" for users
- [ ] MCP host/client-facing content includes all technical constraints
- [ ] Negative instructions ("Not for...", "Do not use...") are in MCP section
- [ ] Parameter requirements and formats are in MCP section
- [ ] Return value specifications are in MCP section
- [ ] Conditional behaviors are explicitly stated in MCP section
- [ ] Tool selection hints are in MCP section
- [ ] No technical jargon unnecessarily complicates human-facing content
- [ ] Reasoning section explains any ambiguous classifications

## Related Files

- [master-prompt-generator.md](master-prompt-generator.md) - For generating example prompts
- [example-prompts-template.hbs](example-prompts-template.hbs) - Template for formatting examples
- [README.md](README.md) - Overview of the automation system
