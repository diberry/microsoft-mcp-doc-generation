# Using the Horizontal Service Template

This guide explains how to use the new `horizontal-service-template.hbs` template with the HorizontalArticleGenerator.

## Overview

There are two templates available:
1. **horizontal-article-template.hbs** - Original template (AI-driven content)
2. **horizontal-service-template.hbs** - NEW template with enhanced structure (this guide)

## Quick Start

### Option 1: Temporary Replacement (for testing)

The simplest way to test the new template:

```bash
cd docs-generation/HorizontalArticleGenerator/templates

# Backup original
cp horizontal-article-template.hbs horizontal-article-template.hbs.backup

# Use new template
cp horizontal-service-template.hbs horizontal-article-template.hbs

# Run generator
cd ../..
pwsh ./Generate-HorizontalArticles.ps1

# Restore original
cd HorizontalArticleGenerator/templates
mv horizontal-article-template.hbs.backup horizontal-article-template.hbs
```

### Option 2: Modify Generator Code

To make the template selectable via command line:

1. **Edit HorizontalArticleGenerator.cs** (in the class constants section near the top):

```csharp
// Before:
private const string TEMPLATE_PATH = "./HorizontalArticleGenerator/templates/horizontal-article-template.hbs";

// After:
private readonly string _templatePath;

// Update constructor:
public HorizontalArticleGenerator(GenerativeAIOptions options, bool useTextTransformation = false, bool generateAllArticles = false, string templateName = "horizontal-article")
{
    // ... existing validation ...
    _templatePath = $"./HorizontalArticleGenerator/templates/{templateName}-template.hbs";
    _aiClient = new GenerativeAIClient(options);
    _useTextTransformation = useTextTransformation;
    _generateAllArticles = generateAllArticles;
}

// Update RenderAndSaveArticle method:
private async Task RenderAndSaveArticle(HorizontalArticleTemplateData templateData)
{
    var templatePath = Path.GetFullPath(_templatePath); // Use _templatePath instead of TEMPLATE_PATH
    // ... rest of method unchanged ...
}
```

2. **Edit HorizontalArticleProgram.cs** to add CLI argument:

```csharp
// Add new argument parsing
string templateName = "horizontal-article"; // default
if (args.Contains("--template"))
{
    var templateIndex = Array.IndexOf(args, "--template");
    if (templateIndex + 1 < args.Length)
    {
        templateName = args[templateIndex + 1];
    }
}

// Pass to generator constructor
var generator = new HorizontalArticleGenerator(options, useTextTransformation, !singleArticle, templateName);
```

3. **Usage**:

```bash
# Use original template (default)
dotnet run --project HorizontalArticleGenerator

# Use new service template
dotnet run --project HorizontalArticleGenerator -- --template horizontal-service

# With other options
dotnet run --project HorizontalArticleGenerator -- --single --template horizontal-service
```

### Option 3: Environment Variable

Add environment variable support:

```csharp
// In HorizontalArticleGenerator constructor:
var templateName = Environment.GetEnvironmentVariable("HORIZONTAL_TEMPLATE") ?? "horizontal-article";
_templatePath = $"./HorizontalArticleGenerator/templates/{templateName}-template.hbs";
```

Usage:
```bash
export HORIZONTAL_TEMPLATE=horizontal-service
pwsh ./Generate-HorizontalArticles.ps1
```

## Data Requirements

The new template requires additional variables. You'll need to update the AI prompts to generate these fields:

### Required Updates to System Prompt

Add to `horizontal-article-system-prompt.txt`:

```text
## Additional Fields for Enhanced Template

When using the horizontal-service template, also generate:

1. "genai-primaryInteraction": "string - Primary way users interact (verb phrase, e.g., 'manage secrets and keys securely')"

2. "genai-serviceFullDescription": "string - Comprehensive 2-4 sentence service explanation"

3. "genai-coreCapabilities": [
     {
       "name": "Capability name",
       "description": "What it provides"
     }
   ]

4. "genai-integrationBenefit": "string - One sentence about MCP integration value"

5. "genai-workflowSteps": ["array of 4-6 strings describing the request flow"]

6. "genai-setupSteps": [
     {
       "title": "Step title",
       "content": "Step instructions",
       "example": "Code or command example (optional)",
       "exampleLanguage": "Language for syntax highlighting (optional)"
     }
   ]

7. "genai-operationCategories": [
     {
       "category": "Category name",
       "description": "Category description (optional)",
       "tools": [/* tool objects with command, moreInfoLink, genai-shortDescription */]
     }
   ]

8. "genai-commonTasks": [  // Replaces genai-scenarios
     {
       "title": "Task name",
       "description": "Use case context",
       "examples": ["array of natural language prompts"],
       "whatHappens": "Process explanation (optional)",
       "result": "Expected outcome (optional)"
     }
   ]

9. "genai-permissionsTable": [
     {
       "operationType": "What operation",
       "role": "Required RBAC role",
       "scope": "Permission scope"
     }
   ]

10. "genai-authenticationMethods": [
      {
        "method": "Method name",
        "description": "When to use"
      }
    ]

11. "genai-bestPracticesStructured": {
      "security": ["array of security practices"],
      "performance": ["array of performance practices"],
      "costOptimization": ["array of cost practices"],
      "reliability": ["array of reliability practices"],
      "operational": ["array of operational practices"]
    }

12. "genai-troubleshootingIssues": [
      {
        "title": "Issue name",
        "symptoms": "What users see (optional)",
        "cause": "Why it happens (optional)",
        "resolution": "How to fix"
      }
    ]

13. "genai-supportLinks": [
      {
        "title": "Link text",
        "url": "URL"
      }
    ]

14. "genai-nextSteps": [
      {
        "title": "Link text",
        "url": "URL",
        "description": "Additional context (optional)"
      }
    ]

15. "genai-serviceLinks": [
      {
        "title": "Link text",
        "url": "URL"
      }
    ]

16. "genai-aiAutomationLinks": [
      {
        "title": "Link text",
        "url": "URL"
      }
    ]
```

### Backward Compatibility

The template includes fallbacks for most fields, so it can work with data from the original template:

- Missing `genai-workflowSteps` → Uses default workflow
- Missing `genai-operationCategories` → Uses flat tools list
- Missing `genai-permissionsTable` → Falls back to `genai-requiredRoles`
- Missing `genai-bestPracticesStructured` → Falls back to `genai-bestPractices`
- Missing `genai-troubleshootingIssues` → Falls back to `genai-commonIssues`

## Testing

1. **Test with sample data**:

```bash
cd docs-generation/HorizontalArticleGenerator

# Install Handlebars.Net if not already installed
dotnet restore

# Run a simple template test
dotnet run --project . -- --single --template horizontal-service
```

2. **Validate sample data**:

The `sample-data-keyvault.json` file contains a complete example of all required fields. Use it to verify your template renders correctly:

```bash
# Create a simple test harness (create test.csharp):
using System;
using System.IO;
using System.Text.Json;
using HandlebarsDotNet;

var sampleData = File.ReadAllText("templates/sample-data-keyvault.json");
var data = JsonSerializer.Deserialize<Dictionary<string, object>>(sampleData);

var template = File.ReadAllText("templates/horizontal-service-template.hbs");
var handlebars = Handlebars.Create();
var compiled = handlebars.Compile(template);
var result = compiled(data);

Console.WriteLine(result);
```

## Comparison: Original vs New Template

| Feature | Original Template | New Service Template |
|---------|------------------|---------------------|
| Service overview | Brief description | Dedicated "What is?" section with capabilities |
| How it works | Not included | Detailed workflow explanation |
| Setup steps | Not included | Step-by-step with code examples |
| Tools organization | Flat list | Categorized by operation type |
| Scenarios | Simple examples | Task-oriented with process details |
| Permissions | List or fallback | Structured table format |
| Authentication | Combined section | Separate methods + notes |
| Best practices | Flat list or fallback | Organized by WAF pillars |
| Troubleshooting | Simple list or fallback | Symptoms/Cause/Resolution format |
| Navigation | Related content | Next steps + categorized resources |

## Recommended Use Cases

### Use Original Template When:
- Quick article generation with minimal structure
- AI-generated content is the primary focus
- Simple service with few tools
- Rapid prototyping

### Use New Service Template When:
- Creating comprehensive service documentation
- Service has multiple tool categories
- Need structured best practices by pillar
- Want detailed setup instructions
- Following Microsoft Learn published article format

## Output Differences

### Original Template Output (~200 lines)
```markdown
# Use Azure MCP Server with [Service]

[Service] [brief description]...

## Prerequisites
- Standard list

## Available MCP tools
- Tool list

## Common scenarios
### Scenario 1: [Name]
[Description]
**Example commands:**
- "Example 1"

## Authentication and permissions
- Role list

## Best practices
- Practice 1

## Related content
- Links
```

### New Service Template Output (~400 lines)
```markdown
# Use Azure MCP Server with [Service]

[Service] [brief description]...

## What is [Service]?
[Full description with capabilities]

## Prerequisites
[Enhanced list with service-specific items]

## How Azure MCP Server works with [Service]
[Step-by-step workflow]

## Set up Azure MCP Server for [Service]
### Step 1: [Setup]
[Detailed instructions with examples]

## Available operations
### [Category 1]
- Tools in category

## Common tasks
### Task 1: [Name]
[Context and use case]
**Example prompts:**
```text
[Examples]
```
**What happens:** [Process]
**Expected result:** [Outcome]

## Authentication and permissions
### Required permissions
| Operation | Role | Scope |
|-----------|------|-------|

### Authentication methods
- Method list

## Best practices
### Security
- Practices

### Performance
- Practices

## Troubleshooting
### Issue: [Name]
**Symptoms:** [What you see]
**Cause:** [Why]
**Resolution:** [Fix]

## Next steps
- Forward-looking resources

## Related content
### Azure MCP Server
- Links

### [Service]
- Links
```

## Support

For issues or questions about the new template:
1. Check the [TEMPLATE-README.md](TEMPLATE-README.md) for variable documentation
2. Review the [sample-data-keyvault.json](sample-data-keyvault.json) example
3. Refer to the [horizontal-service-reference-example.md](horizontal-service-reference-example.md) for structure details

## Migration Path

To migrate from original to new template:

1. **Phase 1**: Update AI prompts to generate new fields
2. **Phase 2**: Test with sample data  
3. **Phase 3**: Generate subset of articles (e.g., Key Vault, Storage, Functions)
4. **Phase 4**: Review and refine based on output
5. **Phase 5**: Roll out to all services

## Future Enhancements

Potential improvements to consider:

1. **Template validation** - JSON schema for template data
2. **Partial templates** - Reusable components for sections
3. **Multiple templates** - Service-specific variations (data plane vs management plane)
4. **Template selection** - Auto-select based on service characteristics
5. **Preview mode** - Generate both templates for comparison
