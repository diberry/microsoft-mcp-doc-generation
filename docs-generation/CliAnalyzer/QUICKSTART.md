# CliAnalyzer - Quick Start Guide

## Installation

The project is located at: `docs-generation/CliAnalyzer/`

No external dependencies needed beyond .NET 9.0 SDK.

## Building

```bash
cd docs-generation/CliAnalyzer
dotnet build
```

## Running from Repository Root

All commands below assume you're in the repository root directory.

### 1. Generate Full Analysis Report

**Command:**
```bash
dotnet run --project docs-generation/CliAnalyzer
```

**Output:**
- Console table with 50 namespaces and 231 tools
- Breakdown of parameters (required vs optional)
- Top 10 tools by parameter count
- Markdown report: `cli-analysis-report.md`

**Sample Output:**
```
Loaded 231 tools

NAMESPACE BREAKDOWN
-------------------
Namespace           Tools   Params  Required  Optional  Avg/Tool
monitor             13      191     30        161       14.7
sql                 15      174     43        131       11.6
eventhubs           9       112     23        89        12.4
keyvault            11      111     21        90        10.1
foundry             19      207     46        161       10.9
```

---

### 2. Analyze a Specific Namespace

**Command:**
```bash
dotnet run --project docs-generation/CliAnalyzer -- --namespace foundry
```

**Output:**
Shows all 19 tools in the Foundry namespace with their parameters:

```
NAMESPACE: foundry

STATISTICS
----------
  Total Tools                                 19
  Total Parameters                           207
  Required Parameters                         46
  Optional Parameters                        161

TOOLS
-----
Tool Name              Command                      Req  Opt  Total
chat-completions-create  foundry openai chat-...   4    17   21
chat-completions-streaming  foundry openai chat-... 4    17   21
deploy                 foundry models deploy        5    14   19
```

---

### 3. Get Details About a Specific Tool

**Command:**
```bash
dotnet run --project docs-generation/CliAnalyzer -- --namespace sql --tool "create"
```

**Output:**
Complete tool information including description and all parameters:

```
TOOL: sql / create
Command: sql db create

DESCRIPTION
-----------
Create a new Azure SQL Database. This operation creates a new
database in the specified SQL server...

PARAMETERS
----------
  Required                                     3
  Optional                                    16
  Total                                       19

REQUIRED PARAMETERS:
  --resource-group
    Type: string
    Description: The name of the Azure resource group...

  --server
    Type: string
    Description: The name of the SQL server...

OPTIONAL PARAMETERS:
  --tenant
  --auth-method
  --retry-delay
  ...
```

---

### 4. List Top Tools by Complexity

**Command:**
```bash
dotnet run --project docs-generation/CliAnalyzer
```

**Shows:**
Top 10 tools with the most parameters:

```
TOP TOOLS BY PARAMETER COUNT
----------------------------
monitor      create   monitor webtests create       6    23    29
monitor      update   monitor webtests update       2    27    29
managedlustre create  managedlustre fs create       9    19    28
fileshares   create   fileshare creation            3    19    22
foundry      deploy   foundry models deploy         5    14    19
```

---

### 5. Generate Markdown Report Only (Skip Console)

**Command:**
```bash
dotnet run --project docs-generation/CliAnalyzer -- --html-only
```

**Output:**
- No console output
- Creates: `cli-analysis-report.md`
- Great for CI/CD pipelines

---

### 6. Use Custom Input/Output Paths

**Command:**
```bash
dotnet run --project docs-generation/CliAnalyzer -- \
  --file path/to/custom-cli.json \
  --output reports/analysis.md
```

---

## Common Scenarios

### Scenario 1: Check Parameter Requirements for a Tool Family

Find all SQL tools and their required parameters:

```bash
dotnet run --project docs-generation/CliAnalyzer -- --namespace sql
```

Look for the "Required" column to see which tools require parameters.

### Scenario 2: Identify Complex Commands

Run the full analysis and check the "TOP TOOLS" section to find commands with many parameters that might need special attention in documentation.

### Scenario 3: Compare Namespace Complexity

```bash
dotnet run --project docs-generation/CliAnalyzer
```

The namespace breakdown table shows:
- Which namespaces have the most tools
- Which have complex tools (high Avg/Tool)
- Parameter distribution (Required vs Optional)

### Scenario 4: Generate Documentation Insights

```bash
dotnet run --project docs-generation/CliAnalyzer -- --html-only
```

Share the Markdown report with documentation teams to understand:
- Which tools are most critical (based on complexity)
- Which need better parameter documentation
- Where standardization is needed

---

## Understanding the Output

### Console Tables

#### Namespace Breakdown
- **Namespace**: Azure service name
- **Tools**: Number of commands available
- **Params**: Total parameters across all tools
- **Required**: Parameters that must be provided
- **Optional**: Parameters with defaults
- **Avg/Tool**: Average parameters per command

#### Top Tools by Parameter Count
- **Namespace**: Service category
- **Tool**: Command name
- **Command**: Full CLI command
- **Required**: Required parameters for that tool
- **Optional**: Optional parameters
- **Total**: Sum of required + optional

### Markdown Report

The generated Markdown report includes:
- **Overall Statistics**: Tabular summary
- **Namespace Breakdown**: Tools and parameters per namespace
- **Tools Ranking**: Top 20 most complex tools

---

## Tips & Tricks

### 1. Piping to Other Tools

Get just the namespace names:
```bash
dotnet run --project docs-generation/CliAnalyzer | grep "^[a-z]" | head -20
```

### 2. Output to File

Save console output:
```bash
dotnet run --project docs-generation/CliAnalyzer > analysis.txt 2>&1
```

### 3. Find Tools with Most Required Parameters

The output table is sortable if you use the Markdown report in a viewer that supports table sorting. For console, look for high "Required" values in the top tools section.

### 4. Verify CLI JSON Loading

Add detailed path output by checking the "Loading CLI JSON from:" line to confirm the correct file was loaded.

---

## Troubleshooting

### "CLI JSON file not found"
- Run from repository root directory
- Or use `--file` to specify absolute path

### "Namespace 'xyz' not found"
- Check spelling (case-insensitive but must exist)
- Run without namespace filter to see all available ones

### No Markdown report generated
- Check current working directory permissions
- Try specifying full path with `--output`

### Console colors not showing
- Colors are handled gracefully on all platforms
- Content will still display correctly in black & white

---

## Integration Examples

### Export for Analysis
```bash
# Generate both console output and Markdown for review
dotnet run --project docs-generation/CliAnalyzer
```

### Automated Documentation
```bash
# Use Markdown report in automated pipelines
dotnet run --project docs-generation/CliAnalyzer -- --html-only
# Result: cli-analysis-report.md
```

### Parameter Discovery
```bash
# Find all parameters for a service
dotnet run --project docs-generation/CliAnalyzer -- --namespace keyvault
```

---

## Next Steps

1. **Explore Namespaces**: Run analysis and find services of interest
2. **Drill Down**: Use namespace and tool filters to examine specific commands
3. **Generate Reports**: Create Markdown reports for sharing with teams
4. **Identify Patterns**: Look for tools with similar parameter patterns
5. **Documentation Planning**: Use complexity metrics to prioritize documentation efforts

For detailed technical documentation, see: `docs-generation/CliAnalyzer/README.md`
