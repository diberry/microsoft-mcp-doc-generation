---
title: Azure skill for Azure App Onboard Prereq
description: Assess whether source code is ready to deploy to Azure — the check BEFORE infrastructure work. Evaluates build health, app completeness, dependencies and local services, stack compatibility, and deployment feasibility. Answers questions about what your app needs before it can be deployed — frameworks, dependencies, and configuration.
ms.topic: reference
ms.date: 8/3/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure App Onboard Prereq

Assess whether source code is ready to deploy to Azure — the check BEFORE infrastructure work. Evaluates build health, app completeness, dependencies and local services, stack compatibility, and deployment feasibility. Answers questions about what your app needs before it can be deployed — frameworks, dependencies, and configuration.

**Skill:** `azure-app-onboard-prereq` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-app-onboard-prereq/SKILL.md)

## What it provides

The Azure App Onboard Prereq skill evaluates a user's repository for build health, app completeness, and Azure deployment feasibility. It produces per-component verdicts (PASS/WARN/FAIL) that are consumed by downstream phases in the Azure App Onboard pipeline. The skill performs static-only verification and read-only evaluation without executing build or test commands.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **Azure CLI**—Install the latest version from [https://aka.ms/cli](https://aka.ms/cli).
- **Azure Developer CLI (azd)**—Install from [https://aka.ms/azd](https://aka.ms/azd).

## When to use this skill

Use the **Azure App Onboard Prereq** skill when you need to:

- Evaluate your repository for deployment readiness
- Check if your app is ready to deploy to Azure
- Determine what your app needs before deployment
- Identify deployment blockers and framework compatibility
- Verify build health and app completeness
- Check dependency compatibility
- Assess stack compatibility with Azure

### When not to use this skill

- **Validate infrastructure (Bicep/Terraform/azure.yaml)** — Use **azure-validate**
- **Generate infrastructure as code (IaC)** — Use **azure-prepare**
- **End-to-end idea-to-production deployment** — Use **azure-app-onboard**
- **Run `azd up` or deploy to Azure** — Use **azure-deploy**

## MCP tools

This skill uses the following Azure MCP tools:

| Tool | Description |
|------|-------------|
| `mcp_azure_mcp_get_azure_bestpractices` | Validate detected stack patterns against Azure best practices |
| `mcp_azure_mcp_extension_cli_install` | Check/install required CLI tools (az, azd, func) |

## Skill workflow

The Azure App Onboard Prereq skill executes an 8-step workflow:

### Step 1: Session Check
Verify or create a session context for the evaluation. If this is a direct entry, create a new session with a unique identifier and store session context in `context.json`.

### Step 2: Scan Workspace
Scan the repository for project files, infrastructure definitions, services, and dependencies. Detect components, classify Terraform providers, and identify stack conflicts. Gates on cloud SDK dependencies (AWS, Google Cloud, etc.).

### Step 3: Per-Component Evaluation
Perform three evaluation checks:
- **Build check** — Verify build configuration and health
- **Completeness check** — Confirm app has required components
- **Deployability check** — Assess Azure deployment feasibility
- **Component mapping** (conditional) — Handle monorepo structures

### Step 4: Write Artifacts
Write evaluation results to `prereq-output.json` and apply readiness gate logic to determine overall health status.

### Step 5: Present Findings
Display verdicts grouped by severity for user review before proceeding.

### Step 6: Remediation (Conditional)
If FAIL verdicts exist or recommended fixes are needed, guide user through remediation steps with static verification and re-evaluation.

### Step 7: Write Final State
Record the final scan commit SHA and mark the prereq phase as complete.

### Step 8: Route
Route to next phase based on readiness status and user intent (deploy to Azure, use existing infrastructure, or fix and re-run).

## Example prompts

Try these prompts to activate this skill:

- "Evaluate my repo"
- "Is my app ready to deploy?"
- "What does my app need to deploy?"
- "What do I need before deploying?"
- "Does my app need a Dockerfile?"
- "What's blocking my deployment?"
- "Are my dependencies compatible?"
- "Does Azure support my framework?"
- "Can I ship this to Azure?"
- "Scan my repo for issues"
- "Is this app deployable?"
- "Check if my app is ready for Azure"
- "Are there any blockers?"
- "What needs to change before deploying?"
- "Check my app configuration"

## Related skills

- [azure-app-onboard](/azure/copilot/skills/azure-app-onboard)
- [azure-validate](/azure/copilot/skills/azure-validate)
- [azure-prepare](/azure/copilot/skills/azure-prepare)
- [azure-deploy](/azure/copilot/skills/azure-deploy)
- [azure-cloud-migrate](/azure/copilot/skills/azure-cloud-migrate)

## Related content

- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-app-onboard-prereq/SKILL.md)
