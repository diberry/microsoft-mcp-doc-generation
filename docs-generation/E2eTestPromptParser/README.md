# E2eTestPromptParser

A .NET 9.0 standalone console app that downloads and parses the Azure MCP Server's `e2eTestPrompts.md` markdown file into structured JSON or summary output.

## Source File

The input file is maintained in the upstream repository:

**[microsoft/mcp → servers/Azure.Mcp.Server/docs/e2eTestPrompts.md](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/docs/e2eTestPrompts.md)**

The remote URL is configured in `config.json`:

```json
{
  "remoteUrl": "https://raw.githubusercontent.com/microsoft/mcp/main/servers/Azure.Mcp.Server/docs/e2eTestPrompts.md",
  "localFileName": "e2eTestPrompts.md"
}
```

The app downloads the file from `remoteUrl` on each run and saves it locally as `localFileName` before parsing.

## Data Model

```
E2eTestPromptDocument
├── Title                    # H1 heading ("Azure MCP End-to-End Test Prompts")
└── Sections[]               # One per H2 heading
    ├── Heading              # e.g. "Azure Cosmos DB"
    └── Entries[]            # Table rows
        ├── ToolName         # e.g. "cosmos_account_list"
        └── TestPrompt       # e.g. "List all cosmosdb accounts in my subscription"
```

### JSON output shape

The JSON output groups prompts by tool within each section:

```json
{
  "title": "Azure MCP End-to-End Test Prompts",
  "totalSections": 55,
  "totalTools": 225,
  "totalPrompts": 620,
  "sections": [
    {
      "heading": "Azure Advisor",
      "toolCount": 1,
      "promptCount": 3,
      "tools": [
        {
          "toolName": "advisor_recommendation_list",
          "testPrompts": [
            "List all recommendations in my subscription",
            "Show me Advisor recommendations in the subscription <subscription>",
            "List all Advisor recommendations in the subscription <subscription>"
          ]
        }
      ]
    }
  ]
}
```

## Usage

### Parse from string

```csharp
using E2eTestPromptParser;

var markdown = File.ReadAllText("e2eTestPrompts.md");
var doc = TestPromptMarkdownParser.Parse(markdown);
```

### Parse from file path

```csharp
var doc = TestPromptMarkdownParser.ParseFile("path/to/e2eTestPrompts.md");
```

### Parse from stream

```csharp
using var stream = File.OpenRead("e2eTestPrompts.md");
var doc = await TestPromptMarkdownParser.ParseStreamAsync(stream);
```

### Query the parsed data

```csharp
// All entries across all sections
var allEntries = doc.AllEntries;           // IReadOnlyList<TestPromptEntry>

// Distinct tool names
var toolNames = doc.ToolNames;             // IReadOnlySet<string>

// Entries for a specific tool
var entries = doc.GetEntriesByToolName("cosmos_account_list");

// Which section(s) contain a tool
var sections = doc.GetSectionsByToolName("cosmos_account_list");

// Group entries by namespace prefix (text before first underscore)
var byNamespace = doc.GetEntriesByNamespace();
// byNamespace["cosmos"] → all cosmos_* entries
// byNamespace["advisor"] → all advisor_* entries
```

## Parser Behavior

- Extracts H1 as document title, H2 headings as section boundaries
- Parses markdown table rows (`| tool_name | prompt text |`)
- Unescapes `\<value>` → `<value>` in prompt text
- Sections with no table rows produce empty entry lists
- Tool name matching is case-sensitive

## Running

```bash
# JSON output to stdout (default)
dotnet run --project docs-generation/E2eTestPromptParser

# JSON output to a file
dotnet run --project docs-generation/E2eTestPromptParser -- parsed.json

# Summary output to stdout
dotnet run --project docs-generation/E2eTestPromptParser -- --format summary

# Summary output to a file
dotnet run --project docs-generation/E2eTestPromptParser -- output.txt --format summary
```

### As a library reference

You can also reference this project from another .NET project:

```xml
<ProjectReference Include="../E2eTestPromptParser/E2eTestPromptParser.csproj" />
```

```csharp
var doc = TestPromptMarkdownParser.ParseFile("path/to/e2eTestPrompts.md");

foreach (var section in doc.Sections)
{
    Console.WriteLine($"## {section.Heading} ({section.Entries.Count} prompts)");
    foreach (var entry in section.Entries)
        Console.WriteLine($"  {entry.ToolName}: {entry.TestPrompt}");
}
```

## Tests

```bash
dotnet test docs-generation/E2eTestPromptParser.Tests
```

## Handling upstream format changes

The parser depends on the markdown structure of the upstream `e2eTestPrompts.md` file. If the upstream file changes shape, parsing may silently produce empty or incorrect results. Here's how to diagnose and fix.

### Signs the upstream format changed

- Section count, tool count, or prompt count drops to 0 or an unexpectedly low number
- Sections appear with empty `Entries` lists
- Tool names or prompts are missing or malformed

### What the parser expects

The parser relies on these structural conventions:

| Element | Expected format |
|---|---|
| Document title | `# Heading text` (H1) |
| Section heading | `## Heading text` (H2) |
| Table header | `\| Tool Name \| Test Prompt \|` |
| Table separator | `\|:----------\|:----------\|` |
| Table data row | `\| tool_name \| prompt text \|` |

### How to fix

1. **Download the new file and inspect it**:
   ```bash
   curl -o /tmp/e2eTestPrompts.md https://raw.githubusercontent.com/microsoft/mcp/main/servers/Azure.Mcp.Server/docs/e2eTestPrompts.md
   head -50 /tmp/e2eTestPrompts.md
   ```

2. **Identify what changed** — common possibilities:
   - H2 headings renamed or restructured (e.g. H3 subheadings added)
   - Table columns reordered or new columns added
   - Table format changed (e.g. no separator row, different delimiters)
   - Escaped characters changed (e.g. `\<` vs `<`)

3. **Update the parser** in `TestPromptMarkdownParser.cs`:
   - H1/H2 detection: lines 57-73 (`line.StartsWith("# ")` / `line.StartsWith("## ")`)
   - Table separator detection: `SeparatorRowRegex()` at the bottom of the file
   - Table row parsing: `ParseTableRow()` method — adjust cell extraction if columns change
   - Character unescaping: end of `ParseTableRow()` — add/remove replacements as needed

4. **Update the tests** in `E2eTestPromptParser.Tests/TestPromptMarkdownParserTests.cs`:
   - Update `MinimalDocument` constant to match the new format
   - Adjust expected counts and field values in assertions
   - Add new test cases for any new structural elements

5. **Update the models** in `Models/` if the data shape fundamentally changes:
   - `TestPromptEntry.cs` — if table columns change
   - `ServiceAreaSection.cs` — if section nesting changes
   - `E2eTestPromptDocument.cs` — if document-level structure changes

6. **Update the JSON output** in `Program.cs` → `FormatJson()` if new fields need to be serialized

7. **Verify**:
   ```bash
   dotnet test docs-generation/E2eTestPromptParser.Tests
   dotnet run --project docs-generation/E2eTestPromptParser -- --format summary
   ```
