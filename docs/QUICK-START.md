# Quick Start

Generate Azure MCP documentation in 5 minutes.

## Prerequisites

- **Node.js + npm** — for CLI metadata extraction
- **.NET 9 SDK** — for the generation pipeline
- **Azure OpenAI credentials** — for AI-enhanced steps (2, 3, 4, 6)

## Setup

1. **Clone and install:**
   ```bash
   git clone https://github.com/diberry/microsoft-mcp-doc-generation.git
   cd microsoft-mcp-doc-generation
   ```

2. **Configure AI credentials** (create `mcp-tools/.env`):
   ```env
   FOUNDRY_API_KEY=your-key-here
   FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com/
   FOUNDRY_MODEL_NAME=gpt-4.1-mini
   TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME=gpt-4o
   ```

3. **Generate for one namespace:**
   ```bash
   ./start.sh advisor
   ```

4. **Check output** in `./generated-advisor/`:
   ```
   generated-advisor/
   ├── tool-family/advisor.md          ← Main article
   ├── horizontal-articles/advisor.md  ← Overview article
   ├── annotations/                    ← Include files
   ├── parameters/                     ← Include files
   ├── example-prompts/                ← Include files
   └── reports/                        ← Validation reports
   ```

## Without AI (Steps 1 only)

To generate just the deterministic content (annotations, parameters, raw tools) without Azure OpenAI:

```bash
./start.sh advisor 1 --skip-env-validation
```

This produces `annotations/`, `parameters/`, and `tools-raw/` without any AI calls.

## Generate All Namespaces

```bash
./start.sh    # All 52 namespaces, all steps (~22-26 hours for full run)
```

## Next Steps

- **[ARCHITECTURE.md](ARCHITECTURE.md)** — How the pipeline works
- **[START-SCRIPTS.md](START-SCRIPTS.md)** — All start.sh options and workflows
- **[GENERATION-SCRIPTS.md](GENERATION-SCRIPTS.md)** — Script execution order details
- **[PROJECT-GUIDE.md](PROJECT-GUIDE.md)** — Full developer guide (extending, testing, troubleshooting)
