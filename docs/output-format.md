# Tool family output format

Tool sections in generated family articles follow this structure:

## H2 heading

Each tool starts with a `##` heading for the tool name.

## Description paragraph

Place one plain-language description paragraph immediately below the H2 heading. Do not duplicate that paragraph inside either tab.

## Tab block

Emit the tabs in this order:

1. `#### [Azure MCP CLI](#tab/azure-mcp-cli)`
2. `#### [MCP Server](#tab/mcp-server)`

The CLI tab is the deterministic command view derived from tools JSON. The MCP tab is the AI-improved server view with the leading description removed.

## Annotation block

Close the tab group with `---`, then place any tool annotation block after that separator so annotations appear once per tool and remain outside both tabs.
