# Tool Example Usage Prompt Generator Template

Use this template to generate natural language example usage prompts for Azure MCP Server tools.

## Template

```
Generate 3-5 natural language example usage prompts for the following tool:

**Tool Name:** {{subToolFamily command}}: {{subOperation command}}

**Description:** {{description}}

**Parameters:**
{{#if option}}
{{#each option}}
- {{#if (eq NL_Name "Unknown")}}{{formatNaturalLanguage name}}{{else}}{{replace NL_Name "." " "}}{{/if}} ({{RequiredText}}): {{description}}
{{/each}}
{{else}}
No parameters
{{/if}}

**Requirements for Example Prompts:**
1. Use natural, conversational language that a user would actually type
2. Include both simple and complex scenarios
3. Show variations in how users might phrase the same request
4. Include prompts that demonstrate optional parameters when applicable
5. Make prompts specific enough to clearly map to this tool
6. Use realistic Azure resource names and scenarios
7. Vary the formality level (some casual, some more formal)

**Format the output as markdown:**
```markdown
Example prompts: 

- "Prompt 1 text here"
- "Prompt 2 text here"
- "Prompt 3 text here"
- "Prompt 4 text here" (if applicable)
- "Prompt 5 text here" (if applicable)
```

**Example for context:**
For a "storage account get" tool, the output would be:
```markdown
Example prompts: 

- "Show me details for my storage account called 'mystorageacct'"
- "Get information about all storage accounts in my subscription"
- "What are the settings for storage account 'companydata2024'?"
```


## Usage Instructions

1. **Replace placeholders** with actual tool information:
   - `{TOOL_H2_HEADING}`: The H2 heading from the documentation (e.g., "Get:", "List:", "Create:")
   - `{TOOL_DESCRIPTION}`: The tool's description paragraph
   - `{PARAMETER_LIST}`: List the parameters in a clear format

2. **Parameter List Format Example:**
   ```
   - account (optional): The name of the Azure Storage account
   - location (required): The Azure region where the resource is located
   - sku (optional): The storage account SKU type
   ```

3. **Submit to AI Assistant** and review the generated prompts for:
   - Natural language flow
   - Realistic scenarios
   - Parameter coverage
   - Variety in phrasing

## Examples of Good vs Poor Prompts

### ✅ Good Example Prompts
- "Show me all my storage accounts"
- "Get details for storage account 'mycompanydata'"
- "What storage accounts do I have in my subscription?"
- "List storage accounts with their locations and SKUs"

### ❌ Poor Example Prompts
- "Execute storage account list command" (too technical)
- "Run the get storage tool" (references tool directly)
- "Storage account information" (too vague)
- "Show storage" (too ambiguous)

## Notes for Documentation Teams

- Use this template consistently across all tool documentation
- Store generated prompts in appropriate documentation sections
- Review prompts periodically to ensure they remain natural and relevant
- Consider user personas when generating prompts (developers, admins, etc.)