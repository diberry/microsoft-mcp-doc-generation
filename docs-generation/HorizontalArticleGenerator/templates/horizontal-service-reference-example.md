# Example Reference: Expected Published Article Structure

This document shows the expected structure of published horizontal articles based on typical Microsoft Learn service integration patterns.

## Article Structure (Inferred from MS Learn Standards)

### Front Matter
```yaml
---
title: Use Azure MCP Server with [Service Name]
description: Learn how to manage and interact with [Service] using Azure Model Context Protocol (MCP) Server through natural language commands.
ms.date: [Date]
ms.topic: how-to
ms.service: [service-identifier]
author: [author]
ms.author: [ms-author]
---
```

### Main Sections

#### 1. Title and Overview
```markdown
# Use Azure MCP Server with [Service Name]

[Service Name] [brief service description]. Azure Model Context Protocol (MCP) Server enables you to [primary interaction methods] using natural language commands through AI assistants like Claude, ChatGPT, or GitHub Copilot.
```

#### 2. What is [Service]? (NEW SECTION)
```markdown
## What is [Service Name]?

[Service Name] is [detailed service explanation]. It provides [core capabilities]:

- [Key feature 1]
- [Key feature 2]  
- [Key feature 3]

When integrated with Azure MCP Server, you can [benefit statement].
```

#### 3. Prerequisites (ENHANCED)
```markdown
## Prerequisites

To use Azure MCP Server with [Service], you need:

- **Azure subscription** - An active Azure subscription
- **Azure MCP Server** - Installed and configured ([setup guide link])
- **[Service-specific requirement 1]** - [why needed]
- **[Service-specific requirement 2]** - [why needed]
- **Authentication** - Azure credentials with appropriate permissions
```

#### 4. How it works (NEW SECTION)
```markdown
## How Azure MCP Server works with [Service]

Azure MCP Server provides a bridge between AI assistants and Azure [Service]. When you make a request:

1. [Step 1 of the process]
2. [Step 2 of the process]
3. [Step 3 of the process]
4. [Step 4 of the process]

This enables you to [outcome].
```

#### 5. Setup and Configuration (NEW SECTION)
```markdown
## Set up Azure MCP Server for [Service]

### Step 1: [Setup action 1]

[Detailed instructions]

### Step 2: [Setup action 2]

[Detailed instructions]

### Step 3: [Setup action 3]

[Detailed instructions]
```

#### 6. Available Operations/Tools (ENHANCED)
```markdown
## Available operations

Azure MCP Server provides the following operations for [Service]:

### [Operation Category 1]

- **[tool-name]** - [description]
- **[tool-name]** - [description]

### [Operation Category 2]

- **[tool-name]** - [description]
- **[tool-name]** - [description]

For complete parameter details, see the [tool reference documentation](link).
```

#### 7. Common Tasks and Examples (EXPANDED)
```markdown
## Common tasks

### Task 1: [Task name]

[Task description and context]

**Example prompts:**

```text
[Example natural language command 1]
[Example natural language command 2]
[Example natural language command 3]
```

**What happens:**

[Explanation of result]

### Task 2: [Task name]

[Repeat structure]
```

#### 8. Authentication and Permissions (DETAILED)
```markdown
## Authentication and permissions

### Required permissions

To perform [Service] operations, your Azure identity needs:

| Operation Type | Required Role | Scope |
|---------------|---------------|--------|
| [Operation 1] | [Role name] | [Resource/RG/Subscription] |
| [Operation 2] | [Role name] | [Resource/RG/Subscription] |

### Authentication methods

Azure MCP Server supports:

- [Auth method 1] - [when to use]
- [Auth method 2] - [when to use]
```

#### 9. Best Practices (STRUCTURED)
```markdown
## Best practices

### Security
- [Security practice 1]
- [Security practice 2]

### Performance
- [Performance practice 1]
- [Performance practice 2]

### Cost optimization
- [Cost practice 1]
- [Cost practice 2]

### Reliability
- [Reliability practice 1]
- [Reliability practice 2]
```

#### 10. Troubleshooting (EXPANDED)
```markdown
## Troubleshooting

### Issue: [Common issue 1]

**Symptoms:** [What you see]

**Cause:** [Why it happens]

**Resolution:** [How to fix]

### Issue: [Common issue 2]

[Repeat structure]

### Get support

If you need additional help:
- [Support option 1]
- [Support option 2]
```

#### 11. Next Steps (NEW SECTION)
```markdown
## Next steps

Now that you've learned to use Azure MCP Server with [Service], explore these resources:

- [Link 1 with description]
- [Link 2 with description]
- [Link 3 with description]

Learn more about:
- [Related topic 1]
- [Related topic 2]
```

#### 12. Related Content (EXPANDED)
```markdown
## Related content

### Azure MCP Server
- [Azure MCP Server overview](link)
- [Azure MCP Server installation](link)
- [Azure MCP Server configuration](link)

### [Service Name]
- [Service overview](link)
- [Service quickstart](link)
- [Service best practices](link)

### AI and automation
- [Using AI assistants with Azure](link)
- [Automation patterns](link)
```

### Key Differences from Current Template

1. **"What is [Service]?" section** - Dedicated service explanation
2. **"How it works" section** - Process flow explanation
3. **"Setup and Configuration" section** - Step-by-step setup instructions
4. **Categorized operations** - Tools grouped by category
5. **Expanded task examples** - More detailed "what happens" explanations
6. **Permissions table** - Structured table format for RBAC
7. **Authentication methods section** - Separate from permissions
8. **Structured best practices** - Organized by category (security, performance, cost, reliability)
9. **Enhanced troubleshooting** - Symptoms/Cause/Resolution format
10. **"Next steps" section** - Forward-looking guidance
11. **Categorized related content** - Organized by topic area
