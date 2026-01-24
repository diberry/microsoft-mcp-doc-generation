# Customizing Tool Family Cleanup Prompts

This guide explains how to customize the LLM prompts for the Tool Family Cleanup tool to add Azure MCP-specific style requirements or modify Microsoft style guide enforcement.

## Overview

The Tool Family Cleanup tool uses two prompt files:
1. **System Prompt** - Defines the overall task, guidelines, and LLM behavior
2. **User Prompt Template** - Per-file prompt with placeholders for filename and content

## Prompt Files Location

```
docs-generation/
└── prompts/
    ├── tool-family-cleanup-system-prompt.txt  # System-level instructions
    └── tool-family-cleanup-user-prompt.txt    # Per-file prompt template
```

## System Prompt Customization

**File**: `docs-generation/prompts/tool-family-cleanup-system-prompt.txt`

This is where you define the overall behavior and standards the LLM should follow.

### Structure

The system prompt has these main sections:

1. **Your Responsibilities** - What the LLM should do
2. **Output Requirements** - Format and structure constraints
3. **Important Notes** - Key guidelines to remember

### Adding Azure MCP Style Requirements

To add Azure MCP-specific conventions, edit **Section 4: Azure MCP-Specific Standards**:

```text
4. **Azure MCP-Specific Standards**:
   - Ensure tool descriptions clearly explain what each tool does
   - Verify that prerequisites are clearly stated
   - Confirm that authentication requirements are mentioned
   - Ensure examples use natural language that users would actually say to an AI assistant
   - Verify that links and references are properly formatted
   - Ensure metadata (frontmatter) is complete and correct
   
   <!-- ADD YOUR CUSTOM REQUIREMENTS HERE -->
   - [Your requirement 1]
   - [Your requirement 2]
   - [Your requirement 3]
```

### Example Customizations

#### Example 1: Enforce Tool Naming Conventions

Add this to Section 4:

```text
   - Tool names must use lowercase with hyphens (e.g., 'storage account create')
   - Tool family headings must follow pattern: "Azure {Service} Tools"
   - Tool subheadings must match exact tool command names
```

#### Example 2: Require Consistent Example Format

Add this to Section 4:

```text
   - All example prompts must be in natural language (no CLI syntax)
   - Examples must start with action verbs ("Create", "List", "Delete")
   - Each example must show expected outcome
```

#### Example 3: Enforce Authentication Documentation

Add this to Section 4:

```text
   - Every tool family file must have an "Authentication" section
   - Authentication section must mention required RBAC roles
   - Must reference common authentication parameters
```

## User Prompt Template Customization

**File**: `docs-generation/prompts/tool-family-cleanup-user-prompt.txt`

This template is used for each individual file being processed.

### Placeholders

- `{{FILENAME}}` - Replaced with the actual filename being processed
- `{{CONTENT}}` - Replaced with the file content

### Modifying Instructions

You can add specific instructions for each file:

```text
**Your Task**:
1. Review the content for adherence to Microsoft style guide standards
2. Improve clarity, readability, and technical accuracy
3. Ensure consistent formatting and structure
4. Apply Azure MCP-specific documentation conventions
5. Output ONLY the cleaned markdown - no explanations or commentary

<!-- ADD CUSTOM INSTRUCTIONS -->
6. [Your custom instruction 1]
7. [Your custom instruction 2]
```

### Example Customizations

#### Example 1: Add Frontmatter Validation

```text
**Your Task**:
1. Review the content for adherence to Microsoft style guide standards
2. Verify frontmatter includes: title, description, ms.topic, ms.date
3. Ensure ms.date is in YYYY-MM-DD format
...
```

#### Example 2: Require Section Order

```text
**Your Task**:
1. Review the content for adherence to Microsoft style guide standards
2. Ensure sections appear in this order:
   - Title (H1)
   - Overview
   - Available Tools
   - Prerequisites
   - Authentication
   - Examples
   - Best Practices
   - Next Steps
...
```

## Testing Your Changes

After modifying prompts:

1. **Save the prompt files**
2. **Run the tool on a test file**:
   ```bash
   cd docs-generation
   pwsh ./Generate-ToolFamilyCleanup.ps1 -InputDir ./path/to/test/files
   ```
3. **Review the prompts** in `./generated/tool-family-cleanup-prompts/`
4. **Review the output** in `./generated/tool-family-cleanup/`
5. **Iterate** - Adjust prompts and re-run

## Advanced Customization

### Adjusting LLM Behavior Tone

In the system prompt, you can modify the opening statement:

**Current:**
```text
You are an expert technical writer and editor specializing in Microsoft documentation standards.
```

**More Strict:**
```text
You are a senior technical editor enforcing strict Microsoft style guide compliance.
Your role is to ensure every document meets publication standards.
```

**More Conversational:**
```text
You are a helpful technical writing assistant improving Azure documentation.
Your goal is to make content clear, accurate, and user-friendly.
```

### Adding Output Format Examples

In the system prompt "Output Requirements" section, you can add examples:

```text
## Output Requirements

- **Output ONLY valid markdown** - do not include explanations or commentary

Example of CORRECT output:
```markdown
---
title: Azure Storage Tools
---

# Azure Storage Tools

Azure Storage provides...
```

Example of INCORRECT output:
```markdown
I've reviewed the document and made the following changes:
1. Fixed the title
2. Improved clarity

Here's the cleaned version:
...
```
```

### Emphasizing Specific Guidelines

To make the LLM pay more attention to certain rules, add them to multiple sections:

1. Add to "Your Responsibilities"
2. Add to "Azure MCP-Specific Standards"
3. Add to "Important Notes"
4. Mention in the user prompt template

## Common Scenarios

### Scenario 1: Enforce Consistent Terminology

**Where**: System Prompt, Section 1, item 1

**Add**:
```text
   - Use consistent terminology (use approved terms list):
     * "Azure MCP Server" not "MCP Server" or "Azure MCP"
     * "tool family" not "service family" or "command group"
     * "natural language prompt" not "command" or "query"
```

### Scenario 2: Require Examples in Every Tool

**Where**: System Prompt, Section 4

**Add**:
```text
   - Every tool description must include at least 2 example prompts
   - Examples must demonstrate different use cases
   - Examples must be realistic and practical
```

### Scenario 3: Enforce Link Validation

**Where**: System Prompt, Section 2, item 4

**Add**:
```text
   - Verify that all links use correct Microsoft Learn URL format
   - Ensure internal links use relative paths
   - Remove or update any broken links (404 errors)
   - Add appropriate link text (no "click here" or bare URLs)
```

## Prompt Iteration Workflow

1. **Start Conservative** - Begin with minimal changes
2. **Test on One File** - Use a single file to verify behavior
3. **Review Output** - Check if LLM understood your instructions
4. **Adjust Specificity** - Make instructions more specific if needed
5. **Test on Multiple Files** - Verify consistency across different files
6. **Document Changes** - Note what works well for future reference

## Troubleshooting

### LLM Ignores Your Custom Rules

**Solution**: 
- Move the rule to the "Important Notes" section
- Rephrase as a requirement not a suggestion
- Add examples of correct vs incorrect application

### LLM Adds Unwanted Content

**Solution**:
- Add to "Output Requirements": "Do not add [specific content]"
- Emphasize "Output ONLY valid markdown"
- Add to "Important Notes": "Do not [specific action]"

### LLM Removes Important Content

**Solution**:
- Add to "Important Notes": "Preserve all [specific content]"
- Add to "Output Requirements": "Maintain all [specific elements]"
- Rephrase as "do not remove" rather than "keep"

## Best Practices

1. **Be Specific** - Vague instructions lead to inconsistent results
2. **Use Examples** - Show the LLM what you want
3. **Prioritize** - Put most important rules first
4. **Test Incrementally** - Add one rule at a time
5. **Document** - Keep notes on what works
6. **Version Control** - Commit prompt changes with descriptive messages

## Getting Help

- Review the README: `docs-generation/ToolFamilyCleanup/README.md`
- Check existing prompts: `docs-generation/prompts/`
- Examine output logs: `./generated/tool-family-cleanup-prompts/`
