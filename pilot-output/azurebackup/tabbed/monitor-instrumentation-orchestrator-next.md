### [MCP Server](#tab/mcp-server)

This tool executes `monitor instrumentation orchestrator-next` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Get the next instrumentation action after completing the current one.
Call this ONLY after you have executed the EXACT instruction from the previous response.
DO NOT skip steps. DO NOT improvise. DO NOT add extra code or commands.

Expected workflow:
1. You received an action from orchestrator-start or orchestrator-next
2. You executed EXACTLY what the 'instruction' field told you to do
3. Now call this tool to get the next action

Returns: The next action to execute, or 'complete' status when all steps are done.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor instrumentation orchestrator-next
```

With parameters:

```azurecli
azmcp monitor instrumentation orchestrator-next --session-id <session-id> --completion-note <completion-note>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--session-id` | string | The workspace path returned as sessionId from orchestrator-start. |
| `--completion-note` | string | One sentence describing what you executed, e.g., 'Ran dotnet add package command' or 'Added UseAzureMonitor() to Program.cs' |

---
