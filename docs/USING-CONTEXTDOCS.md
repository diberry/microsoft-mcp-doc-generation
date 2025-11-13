# Using .contextdocs with LLMs

This guide explains how to use the `.contextdocs` file to help Large Language Models (LLMs) better understand this codebase.

## What Are Context Files?

This repository provides multiple context files optimized for different AI tools:

### 1. `.contextdocs` (Root directory)
Comprehensive context file for general LLM use (ChatGPT, Claude, etc.). Contains:

- **Architecture overview** - How the system is structured
- **Component details** - What each part does
- **Workflow explanations** - How data flows through the system
- **Configuration guides** - How to configure and customize
- **Troubleshooting** - Common issues and solutions
- **Code patterns** - How to add features or fix bugs

### 2. `.github/copilot-instructions.md`
Official GitHub Copilot instruction file. **Automatically loaded** by GitHub Copilot when using `@workspace`.

### 3. `.cursorrules` (Root directory)
Cursor IDE context file. **Automatically loaded** by Cursor AI assistant.

### 4. `docs/ARCHITECTURE.md`
Visual architecture guide for both humans and AI assistants.

## Why Use It?

LLMs work better when they have comprehensive context about:
1. The overall project structure
2. Design decisions and rationale
3. Common patterns and conventions
4. Known issues and solutions
5. File locations and relationships

**Without .contextdocs**: LLM sees individual files in isolation  
**With .contextdocs**: LLM understands the full system architecture

## How to Use with Different LLMs

### GitHub Copilot Chat (VS Code)

**Automatic Loading** (Recommended):
- GitHub Copilot automatically loads `.github/copilot-instructions.md`
- Just use `@workspace` in your prompts
- No need to manually reference the file

```
@workspace How does filename resolution work?
@workspace Show me how to add a new service area
```

**Manual Reference** (Alternative):
```
Based on .contextdocs, explain how filename resolution works
```

**Method 2: Use in slash commands**
```
/explain How does the PowerShell orchestrator detect container environment?
```

### ChatGPT / Claude

**Method 1: Upload the file**
1. Open `.contextdocs` in your editor
2. Copy the entire content
3. Paste into chat with your question:
```
[Paste .contextdocs content]

Based on this context, explain the three-tier filename resolution system.
```

**Method 2: Reference with code**
```
I'm working on the Azure MCP Documentation Generator. 
See .contextdocs for full context.

How do I modify the Handlebars template processing?
```

### Cursor / Aider

**Cursor (Automatic)**:
- Cursor automatically loads `.cursorrules` on startup
- Just ask questions naturally:
```
How do I add a new service area?
Explain the Docker multi-stage build
```

**Aider (Manual)**:
```
/add .contextdocs

Now explain how Docker containerization works in this project
```

**Method 2: Reference directly**
```
Using .contextdocs as reference, help me debug the PowerShell script
```

## Example Prompts

### Understanding Architecture

**❌ Generic prompt** (less effective):
```
How does this documentation generator work?
```

**✅ With .contextdocs** (more effective):
```
Based on .contextdocs, explain the three-layer architecture 
(Orchestration, Generation, Template) and how data flows through it.
```

### Troubleshooting

**❌ Generic prompt**:
```
I'm getting a .NET SDK error
```

**✅ With .contextdocs**:
```
According to .contextdocs section "Common Issues & Solutions", 
I'm getting ".NET 10 SDK Not Found" error. What's the solution?
```

### Adding Features

**❌ Generic prompt**:
```
How do I add a new Azure service to the docs?
```

**✅ With .contextdocs**:
```
Based on .contextdocs "Adding New Service Area" pattern, 
walk me through adding Azure Cosmos DB with brand name 
"Azure Cosmos DB" and filename "azure-cosmos-db".
```

### Debugging Code

**❌ Generic prompt**:
```
This PowerShell script isn't working
```

**✅ With .contextdocs**:
```
Using .contextdocs as reference, the PowerShell orchestrator 
is failing at the environment detection step. What should I check?
```

### Modifying Templates

**❌ Generic prompt**:
```
How do I change the documentation format?
```

**✅ With .contextdocs**:
```
According to .contextdocs Template Layer section, which Handlebars 
template should I modify to change the main service documentation 
format? Show me the file location and what to edit.
```

## Best Practices

### 1. Always Reference .contextdocs First

Start your prompt with context:
```
Based on .contextdocs, [your question]
```

### 2. Be Specific About Sections

Reference specific sections:
```
According to .contextdocs "Docker Architecture" section, 
how does the multi-stage build work?
```

### 3. Combine with File References

```
Looking at .contextdocs and the file Generate-MultiPageDocs.ps1, 
explain how environment detection works.
```

### 4. Use for Code Review

```
Review this change to DocumentationGenerator.cs against 
the patterns described in .contextdocs.
```

### 5. Ask for Implementation Guidance

```
Based on .contextdocs "Key Patterns" section, show me how to 
implement [feature] following the established conventions.
```

## What .contextdocs Covers

### ✅ Architecture & Design
- Three-tier architecture (Orchestration → Generation → Templates)
- Multi-stage Docker build
- Data flow diagrams
- Component relationships

### ✅ Code Structure
- File locations and organization
- Key classes and their responsibilities
- Template types and usage
- Configuration file purposes

### ✅ Workflows
- Local development process
- Docker containerization
- GitHub Actions CI/CD
- Debugging procedures

### ✅ Configuration
- Three-tier filename resolution
- Brand mappings vs compound words
- Stop words and text replacements
- When to update each config file

### ✅ Troubleshooting
- Common build issues
- Environment-specific problems
- Permission errors
- Path resolution issues

### ✅ Patterns & Conventions
- Adding new service areas
- Modifying templates
- Updating configuration
- Development workflows

## When to Update .contextdocs

Update `.contextdocs` when you:

1. **Change Architecture** - New components, different flow
2. **Add Major Features** - New template types, configuration options
3. **Solve Complex Issues** - Document solutions for future reference
4. **Change Workflows** - New development or deployment processes
5. **Update Dependencies** - New version requirements

## Quick Reference Commands

### View the file
```bash
cat .contextdocs
```

### Search for specific topics
```bash
grep -i "docker" .contextdocs
grep -i "filename resolution" .contextdocs
```

### Count sections
```bash
grep "^##" .contextdocs | wc -l
```

### Show table of contents
```bash
grep "^##" .contextdocs
```

## Integration with Development Tools

### VS Code Settings

Add to `.vscode/settings.json`:
```json
{
  "github.copilot.chat.codeGeneration.instructions": [
    {
      "file": ".contextdocs"
    }
  ]
}
```

### Git Hooks

Add pre-commit hook to remind about updates:
```bash
#!/bin/bash
# .git/hooks/pre-commit

if git diff --cached --name-only | grep -q "Dockerfile\|docker-compose.yml\|docs-generation/"; then
  echo "⚠️  You modified core files. Consider updating .contextdocs"
fi
```

## Example Workflow

### Scenario: Adding a New Service

**Step 1**: Check .contextdocs
```
Look at .contextdocs section "Adding New Service Area"
```

**Step 2**: Ask LLM with context
```
Based on .contextdocs, I need to add Azure App Service.
Guide me through:
1. Adding brand mapping
2. Updating configuration
3. Testing the generation
```

**Step 3**: Implement changes
```
Using .contextdocs as reference, review my changes to 
brand-to-server-mapping.json:
[paste your changes]

Do they follow the established pattern?
```

**Step 4**: Verify
```
According to .contextdocs, what should I check after 
regenerating docs for a new service?
```

## Tips for Maximum Effectiveness

### ✅ DO:
- Reference .contextdocs in your first prompt
- Be specific about which section you're referencing
- Combine with actual file content when needed
- Use it for understanding before making changes
- Reference it in code reviews

### ❌ DON'T:
- Ask generic questions without context
- Ignore the architecture when making changes
- Skip reading relevant sections
- Make assumptions about how things work
- Forget to update it when architecture changes

## Getting Help

If you're stuck:

1. **Search .contextdocs** for keywords related to your issue
2. **Reference specific sections** in your LLM prompt
3. **Combine with error messages** for troubleshooting
4. **Ask for examples** based on patterns in .contextdocs
5. **Request step-by-step** guidance referencing the workflows

## Updating This Guide

This guide should be updated when:
- New LLM tools become popular
- Better prompting patterns are discovered
- Common usage questions arise
- Integration methods change

---

**Remember**: .contextdocs is your LLM's "onboarding document" for this project. Use it to help AI assistants provide more accurate, contextual, and useful responses!
