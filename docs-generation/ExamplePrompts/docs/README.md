# Example Prompts Automation

This directory contains automation tools for generating consistent, high-quality example prompts for Azure MCP Server tool documentation.

## Overview

When documenting Azure MCP Server tools, each tool requires a section of example prompts that demonstrate how users can interact with the tool using natural language. These automation files help maintain consistency and quality across all tool documentation.

## Files

### 1. `master-prompt-generator.md`

This file contains the master prompt template used to generate example prompts. It includes:

- **Instructions** for how to use the prompt
- **Input variables** that need to be provided
- **Prompt template** with detailed requirements
- **Example usages** demonstrating different scenarios
- **Notes** on best practices and conventions

**When to use**: Use this prompt with an AI assistant (like GitHub Copilot or ChatGPT) to generate example prompts for a new tool or to regenerate prompts for an updated tool.

### 2. `example-prompts-template.hbs`

This is a Handlebars template that formats the example prompts section in the markdown documentation. It includes:

- Template for rendering example prompts as a bulleted list
- Conditional rendering of parameters table
- Embedded usage examples and documentation
- Proper markdown formatting

**When to use**: Use this template when programmatically generating documentation or when you need a consistent structure for the example prompts section.

## Usage Workflow

### For Creating New Tool Documentation

1. **Gather tool information:**
   - Tool name (friendly name, e.g., "List subscriptions")
   - Tool command (HTML comment, e.g., `<!-- subscription list -->`)
   - Action verb (e.g., "list", "create", "get")
   - Resource type (e.g., "subscriptions", "storage account")
   - Parameters with required/optional status and descriptions

2. **Generate example prompts:**
   - Open `master-prompt-generator.md`
   - Replace the placeholders in the prompt template with your tool's information
   - Submit the completed prompt to an AI assistant
   - Review and refine the generated example prompts

3. **Format the output:**
   - Use the generated prompts directly in your markdown file, OR
   - Use `example-prompts-template.hbs` with a Handlebars processor for automated generation

4. **Add to your tool documentation:**
   - Insert the example prompts after the tool description
   - Follow with the parameters table if parameters exist
   - Add tool annotation hints include statement

### For Updating Existing Tool Documentation

1. **Review current parameters:**
   - Check if parameters have changed
   - Update parameter descriptions if needed

2. **Regenerate prompts if needed:**
   - Use the master prompt generator with updated information
   - Replace old example prompts with new ones

3. **Maintain consistency:**
   - Ensure example values follow Azure naming conventions
   - Verify all required parameters are included in examples

## Best Practices

### Example Prompt Guidelines

1. **Variety**: Create 5 diverse prompts with different:
   - Phrasing and sentence structures
   - Formality levels (formal vs. casual)
   - Question types (commands, questions, requests)

2. **Realism**: Use realistic Azure values:
   - Storage accounts: lowercase, no hyphens (e.g., `mystorageaccount`)
   - Resource groups: kebab-case (e.g., `my-resource-group`)
   - Locations: official region names (e.g., `eastus`, `westus2`)
   - Container/vault names: lowercase (e.g., `documents`, `production-vault`)

3. **Required Parameters**: 
   - Always include all required parameters in every example
   - Use quoted strings for concrete values
   - Make parameter values obvious and realistic

4. **Optional Parameters**:
   - Include in some examples to show flexibility
   - Demonstrate different combinations
   - Show how optional parameters enhance functionality

5. **Natural Language**:
   - Write as developers would naturally ask
   - Avoid overly formal or API-like syntax
   - Mix imperative commands with questions

### Category Labels

Use appropriate category labels that describe what the prompt does:

**List/Query operations:**
- List [resource], Show [resource], Find [resource], Query [resource], Check [resource]

**Create operations:**
- Create [resource], New [resource], Set up [resource], Add [resource], Make [resource]

**Get/Retrieve operations:**
- Get [resource] details, View [resource] info, Retrieve [resource], Check [resource]

**Update operations:**
- Update [resource], Modify [resource], Change [resource], Configure [resource]

**Delete operations:**
- Delete [resource], Remove [resource], Clean up [resource]

**Upload/Download:**
- Upload [file/data], Download [file/data], Transfer [resource]

## Example: Complete Workflow

Here's a complete example for creating documentation for a new "List Key Vault Keys" tool:

### Step 1: Gather Information

```
Tool Name: "List keys"
Tool Command: <!-- keyvault key list -->
Action Verb: "list"
Resource Type: "keys"
Parameters:
  - Vault (Required): "The name of the Key Vault"
  - Include managed (Required): "Whether to include managed keys in results"
```

### Step 2: Use Master Prompt

Copy the prompt template from `master-prompt-generator.md` and fill in:

```
Generate 5 diverse, natural language example prompts for an Azure MCP Server tool with the following characteristics:

Tool Name: List keys
Tool Command: <!-- keyvault key list -->
Action: list
Resource: keys

Parameters:
- Vault (Required): The name of the Key Vault
- Include managed (Required): Whether to include managed keys in results

[... rest of requirements from master prompt ...]
```

### Step 3: Review Generated Output

The AI generates:

```
- **List all keys**: "Show me all keys in my 'mykeyvault' Key Vault."
- **View keys**: "What keys do I have in Key Vault 'security-kv'?"
- **Find keys**: "List keys in my Key Vault 'central-keys'"
- **Query keys**: "Show all keys including managed keys in my Key Vault"
- **Check keys**: "What keys are available in my 'encryption-vault'?"
```

### Step 4: Add to Documentation

Insert into your markdown file:

```markdown
## Keys: List keys

<!-- keyvault key list -->

The Azure MCP Server can list all keys in an Azure Key Vault.

Example prompts include:

- **List all keys**: "Show me all keys in my 'mykeyvault' Key Vault."
- **View keys**: "What keys do I have in Key Vault 'security-kv'?"
- **Find keys**: "List keys in my Key Vault 'central-keys'"
- **Query keys**: "Show all keys including managed keys in my Key Vault"
- **Check keys**: "What keys are available in my 'encryption-vault'?"

| Parameter | Required or optional | Description |
|-----------|-------------|-------------|
| **Vault** | Required | The name of the Key Vault. |
| **Include managed** | Required | Whether or not to include managed keys in results. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

[!INCLUDE [keyvault key list](../includes/tools/annotations/azure-key-vault-key-list-annotations.md)]
```

## Quality Checklist

Before finalizing your generated example prompts, verify:

- [ ] All 5 prompts use different phrasing and structures
- [ ] Required parameters appear in all examples with realistic values
- [ ] Values follow Azure naming conventions
- [ ] Mix of formal and informal language
- [ ] Category labels are appropriate and varied
- [ ] Prompts sound natural and conversational
- [ ] Parameters table includes all parameters with correct required/optional status
- [ ] Tool annotation hints include statement is present

## Maintenance

When updating these automation files:

1. **Review existing documentation** to identify patterns
2. **Test changes** by generating example prompts for existing tools
3. **Update examples** in the master prompt generator
4. **Document changes** in this README

## Related Documentation

- [Azure MCP Server Tools Documentation](../index.md)
- [Get Started Guide](../../get-started.md)
- [Tool Annotations Guide](../index.md#tool-annotations-for-azure-mcp-server)
