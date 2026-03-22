# test-npm-azure-mcp

Azure MCP CLI metadata extractor — the single integration point between the npm `@azure/mcp` package and the C# documentation generation pipeline.

## Purpose

This package installs the Azure MCP CLI and extracts structured tool metadata (JSON) that feeds every step of the documentation pipeline. **All 52 namespace documentation generations depend on this package.**

## Setup

1. Copy `samples.env` to `.env` and fill in your Azure credentials:
   ```bash
   cp samples.env .env
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Verify the CLI works:
   ```bash
   npm run get:version
   ```

## Scripts

| Script | Purpose | Used by Pipeline |
|--------|---------|:---:|
| `get:version` | Get MCP CLI version string | ✅ Bootstrap Step 0 |
| `get:tools-json` | Extract all tools as JSON | ✅ Bootstrap Step 0 |
| `get:tools-namespace` | Extract tools by namespace | ✅ Bootstrap Step 0 |
| `start` | Azure CLI login | Manual |
| `start:info` | MCP server info | Manual |
| `start:help` | CLI help text | Manual |
| `start:list` | List tools (human readable) | Manual |
| `get:subscriptions` | List Azure subscriptions | Manual |
| `get:chat-completion` | Test Azure OpenAI via MCP | Manual |

## Pipeline Integration

```
start.sh → PipelineRunner → BootstrapStep (Step 0)
  ├─ npm install (this package)
  ├─ npm run get:version      → cli/cli-version.json
  ├─ npm run get:tools-json   → cli/cli-output.json
  └─ npm run get:tools-namespace → cli/cli-namespace.json
      ↓
  Steps 1-6 consume cli-output.json for all generation
```

## Version Snapshots

Each `@azure/mcp` version has a snapshot directory (e.g., `2.0.0-beta.31+.../tools-list.json`) preserving the tool metadata at that version. The GitHub workflow `update-azure-mcp.yml` automatically creates these on version updates.

## Environment Variables (.env)

| Variable | Required | Description |
|----------|:---:|-------------|
| `AZURE_SUBSCRIPTION_ID` | ✅ | Azure subscription GUID |
| `AZURE_TENANT_ID` | ✅ | Azure AD tenant GUID |
| `FOUNDRY_KEY` | For chat | Azure OpenAI API key |
| `AZURE_RESOURCE_GROUP` | For chat | Resource group name |
| `AZURE_FOUNDRY_NAME` | For chat | Foundry resource name |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | For chat | Model deployment name |
| `AUTH_METHOD` | For chat | Authentication method (`key`) |
