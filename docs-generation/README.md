# Documentation Generation

This project automatically generates multi-page documentation for Azure MCP (Model Context Protocol) tools using a C# generator with Handlebars templates.

## Overview

The documentation generation system consists of:

- **PowerShell Orchestrator** (`Generate-MultiPageDocs.ps1`) - Main entry point that coordinates the generation process
- **C# Generator** (`CSharpGenerator/`) - .NET 9.0 console application that processes CLI output and generates documentation using Handlebars templates
- **Handlebars Templates** (`templates/`) - Template files that define the structure and format of generated documentation

## Architecture

```
docs-generation/
├── Generate-MultiPageDocs.ps1     # Main orchestration script
├── stop-words.json                # Stop words removed from include filenames
├── compound-words.json            # Compound word mappings for include filenames
├── brand-to-server-mapping.json   # Brand name to filename mappings
├── CSharpGenerator/               # C# console application
│   ├── CSharpGenerator.csproj    # Project file with Handlebars.Net dependency
│   └── Program.cs                # Main generator logic
├── ToolMetadataExtractor/        # Tool for extracting ToolMetadata information
│   ├── Models/                   # Data models for tool metadata
│   ├── Services/                 # Services for metadata extraction
│   └── Program.cs                # Command-line interface
└── templates/                    # Handlebars template files
    ├── area-template.hbs         # Template for area-specific documentation
    └── common-tools.hbs          # Template for common tools documentation
```

## Configuration Files

### stop-words.json

Contains a list of common stop words that are removed from **include filenames only** during generation. This helps create cleaner, more concise filenames for the include files (annotations, parameters, and param-annotation files).

**Note**: Stop words are **NOT** removed from main area tool filenames (e.g., `acr.md`, `storage.md`).

Example:
```json
["a", "or", "and", "the", "in"]
```

**Usage**: Words like "a", "or", "and" are filtered out from include filenames such as:
- `annotations/azure-storage-account-get-annotations.md`
- `parameters/azure-storage-account-get-parameters.md`
- `param-and-annotation/azure-storage-account-get-param-annotation.md`

### compound-words.json

Contains mappings of compound words (smashed words) to their hyphenated equivalents for **include filenames only**. This ensures that compound words are properly separated with hyphens to pass filename validation rules.

**Note**: Compound word mappings are **NOT** applied to main area tool filenames (e.g., `acr.md`, `storage.md`).

Example:
```json
{
  "eventhub": "event-hub",
  "consumergroup": "consumer-group",
  "nodepool": "node-pool",
  "bestpractices": "best-practices",
  "activitylog": "activity-log"
}
```

**Usage**: Transforms include filenames to meet documentation standards:
- Before: `azure-event-hubs-eventhub-consumergroup-get-annotations.md`
- After: `azure-event-hubs-event-hub-consumer-group-get-annotations.md`

### Why Only Include Files?

The filename cleaning process (stop word removal and compound word separation) is applied exclusively to include files to:

1. **Pass validation rules**: Include files are validated against strict filename standards that prohibit stop words and require proper word separation
2. **Maintain stability**: Main area tool filenames (like `acr.md`, `storage.md`) remain unchanged to preserve existing documentation structure and links
3. **Improve readability**: Include filenames become more descriptive and easier to understand when cleaned

**Include file types affected**:
- `generated/multi-page/annotations/*.md` - Tool annotation files
- `generated/multi-page/parameters/*.md` - Tool parameter files  
- `generated/multi-page/param-and-annotation/*.md` - Combined parameter and annotation files

**Main tool files NOT affected**:
- `generated/multi-page/*.md` - Area-specific tool documentation (e.g., `acr.md`, `storage.md`, `keyvault.md`)

### brand-to-server-mapping.json

Contains mappings of MCP server area names to their preferred brand-based filenames. This is the **primary** method for determining include filenames and takes precedence over other naming strategies.

Example:
```json
[
  {
    "mcpServerName": "acr",
    "brandName": "Azure Container Registry",
    "fileName": "azure-container-registry"
  },
  {
    "mcpServerName": "storage",
    "brandName": "Azure Storage",
    "fileName": "azure-storage"
  }
]
```

**Usage**: Controls the base filename for all include files in that area:
- `azure-container-registry-registry-list-annotations.md`
- `azure-storage-account-get-parameters.md`

## Filename Generation Logic

The documentation generator uses a **three-tier resolution system** to determine filenames for include files (annotations, parameters, and param-annotation files). This ensures consistent, readable, and validation-compliant filenames.

### Three-Tier Filename Resolution

When generating include filenames, the system follows this priority order:

```
1. Brand Mapping (brand-to-server-mapping.json)
   ↓ if not found
2. Compound Words (compound-words.json)
   ↓ if not found
3. Original Area Name (lowercase)
```

### How It Works

For a tool command like `azureaibestpractices get`:

**Step 1: Check Brand Mapping**
```
Area: "azureaibestpractices"
Check: brand-to-server-mapping.json
Result: No mapping found → Continue to Step 2
```

**Step 2: Check Compound Words**
```
Area: "azureaibestpractices"
Check: compound-words.json
Match: "azureaibestpractices" → "azure-ai-best-practices"
Result: Use "azure-ai-best-practices" as base filename
```

**Step 3: Fallback to Original**
```
If no match in Steps 1 or 2:
Result: Use lowercase area name (e.g., "azureaibestpractices")
```

### Example Filename Generation

Given tool command: `azureaibestpractices get`

1. **Parse command**: Extract area ("azureaibestpractices") and operation ("get")
2. **Apply three-tier resolution**: Find "azure-ai-best-practices" in compound-words.json
3. **Clean operation parts**: Remove stop words, separate compound words
4. **Build filename**: `azure-ai-best-practices-get-annotations.md`

### Complete Filename Structure

Include filenames follow this pattern:
```
{base-filename}-{operation-parts}-{file-type}.md
```

Where:
- `{base-filename}`: Result from three-tier resolution (brand → compound → original)
- `{operation-parts}`: Remaining command parts after area, cleaned (stop words removed, compound words separated)
- `{file-type}`: One of `annotations`, `parameters`, or `param-annotation`

### Real-World Examples

| Tool Command | Area | Resolution Method | Generated Filename |
|-------------|------|-------------------|-------------------|
| `acr registry list` | acr | Brand mapping | `azure-container-registry-registry-list-annotations.md` |
| `storage account get` | storage | Brand mapping | `azure-storage-account-get-parameters.md` |
| `azureaibestpractices get` | azureaibestpractices | Compound words | `azure-ai-best-practices-get-annotations.md` |
| `eventhub consumergroup list` | eventhub | Compound words | `event-hub-consumer-group-list-param-annotation.md` |
| `customarea operation` | customarea | Original (fallback) | `customarea-operation-annotations.md` |

### When to Update Configuration Files

**Update brand-to-server-mapping.json when:**
- Adding a new service area that should use a specific brand name (e.g., "Azure Container Registry" for "acr")
- Changing the preferred filename for an existing service area
- Brand names should reflect official Microsoft product names

**Update compound-words.json when:**
- Discovering smashed/compound words in area names (e.g., "azureaibestpractices")
- New service areas use concatenated words without hyphens
- Filenames fail validation due to unrecognized compound words

**Priority matters:** Brand mappings always win over compound words. If both exist for the same area, the brand mapping filename will be used.

### Debugging Filename Generation

The generator outputs console messages during filename resolution:

```
Applied compound word transformation for 'azureaibestpractices': 'azureaibestpractices' -> 'azure-ai-best-practices'
```

```
Warning: No brand mapping or compound word found for area 'customarea', using 'customarea'
```

Use these messages to verify which resolution method was applied for each area.

### Impact of Filename Changes

When updating `brand-to-server-mapping.json` or `compound-words.json`:

1. **Old files remain**: Existing include files are not automatically deleted
2. **New files created**: Regeneration creates files with new names
3. **Area pages update**: Main area pages reference new filenames
4. **Broken links**: Old filenames no longer referenced may cause 404s

**Recommended workflow:**
1. Delete old include files before regeneration:
   ```powershell
   rm -rf generated/multi-page/annotations/*
   rm -rf generated/multi-page/parameters/*
   rm -rf generated/multi-page/param-and-annotation/*
   ```
2. Regenerate documentation:
   ```powershell
   pwsh ./Generate-MultiPageDocs.ps1
   ```
3. Verify new filenames match expected patterns
4. Commit changes with descriptive message explaining filename updates

## Process Flow

1. **Data Extraction**: The PowerShell script calls the Azure MCP CLI (`dotnet run -- tools list`) to extract tool information
2. **Metadata Extraction**: The ToolMetadataExtractor can be used to extract ToolMetadata properties from tool source files
3. **Data Processing**: CLI output and metadata are saved as JSON and passed to the C# generator
4. **Template Processing**: The C# generator uses Handlebars.Net to process templates with the extracted data
5. **Documentation Generation**: Multi-page Markdown documentation is generated in the `generated/multi-page/` directory

## Dependencies

### Global Dependencies (Central Package Management)

This project uses Central Package Management (CPM) as configured in the solution's `Directory.Packages.props`. The following dependency must be defined globally:

- **Handlebars.Net** (currently version 2.1.6) - Required for template processing in the C# generator

**Important**: When using CPM, package versions must be defined in `Directory.Packages.props`, not in individual project files. The `CSharpGenerator.csproj` contains only the package reference without a version number:

```xml
<PackageReference Include="Handlebars.Net" />
```

The version is centrally managed in `Directory.Packages.props`:

```xml
<PackageVersion Include="Handlebars.Net" Version="2.1.6" />
```

### Local Dependencies

- **.NET 9.0 SDK** - Required to build and run the C# generator
- **PowerShell** - Required to run the orchestration script
- **Azure MCP CLI** - Must be built and available at `../core/src/AzureMcp.Cli`

## Usage

### Basic Generation

Run in a PowerShell terminal.

```powershell
pwsh ./Generate-MultiPageDocs.ps1
```

### Advanced Options

```powershell
# Generate only JSON format (YAML not yet implemented)
./Generate-MultiPageDocs.ps1 -Format json

# Skip index page generation
./Generate-MultiPageDocs.ps1 -CreateIndex $false

# Skip common tools page generation
./Generate-MultiPageDocs.ps1 -CreateCommon $false
```

## Generated Output

The script generates documentation in the `generated/` directory:

```
generated/
├── cli/
│   ├── cli-output.json          # Raw CLI output data
│   └── cli-namespace.json       # Namespace data
└── multi-page/                 # Generated Markdown documentation
    ├── index.md                # Main index page (if enabled)
    ├── common-tools.md          # Common tools documentation (if enabled)
    └── [area-name].md           # Area-specific documentation pages
```

## Templates

### area-template.hbs

Generates documentation for each Azure service area (e.g., storage, compute, etc.). Includes:
- Area description and metadata
- Quick navigation links
- Detailed tool documentation

### common-tools.hbs

Generates documentation for common tools that span multiple service areas.

## Development

### Building the C# Generator

```bash
cd CSharpGenerator
dotnet build --configuration Release
```

### Adding New Templates

1. Create a new `.hbs` file in the `templates/` directory
2. Update the C# generator logic in `Program.cs` to use the new template
3. Test the generation process

### Troubleshooting

**Error: NU1008 - Projects that use central package version management should not define the version on the PackageReference**

This error occurs when a package version is defined in the project file instead of `Directory.Packages.props`. Ensure all package versions are centrally managed.

**Build Failures**

Ensure the Azure MCP CLI is built and available:
```bash
cd ../core/src/AzureMcp.Cli
dotnet build
```

## Integration

This documentation generation system is designed to be integrated into CI/CD pipelines. The generated documentation can be:
- Committed to the repository
- Published to documentation sites
- Used as input for further processing

## Contributing

When modifying this system:
1. Follow the coding guidelines in `.github/copilot-instructions.md`
2. Ensure all tests pass with `dotnet build`
3. Update templates and generator logic together
4. Test with representative Azure MCP CLI data

### Temp Order of operations

This process reads existing published 1P documentation to get existing natural parameter names, builds a map to original parameter names, then is used during content generation to provide consisten natural language parameter names.

Its important to read the output of Generate_MultiPageDocs to look for errors about missing parameter names, which need need to be added to nl-parameters.json

## 1. Run term extraction from live docs

```
dotnet run --project CSharpTermRefinement/CSharpTermRefinement.csproj
```
## 2. Run map parameter name

```
dotnet run --project CSharpMapParameterName/CSharpMapParameterName.csproj
```

## 3. Generate docs

```
pwsh ./Generate-MultiPageDocs.ps1
```

## 4. Search for `TBD`

If the process can't create a value, it inserts the `TBD` placeholder. Look for those in the generated markdown and provide better values based on content. 

## Tool Metadata Extractor

The ToolMetadataExtractor is a command-line utility that extracts metadata from MCP tool source files. It helps identify and extract `ToolMetadata` properties that provide important information about each tool's capabilities and behavior.

### Usage

```bash
# Extract metadata from a list of tools
dotnet run --project ToolMetadataExtractor/ToolMetadataExtractor.csproj -- --tools "storage account list" "keyvault secret create" --output metadata.json

# Extract metadata from a file containing tool names
dotnet run --project ToolMetadataExtractor/ToolMetadataExtractor.csproj -- --tools-file tool-list.txt --output metadata.json

# Use the provided scripts
./extract-metadata.sh    # Bash script
./Extract-Metadata.ps1   # PowerShell script
```

### Extracted Metadata

The tool extracts the following metadata for each tool:

- **ToolPath**: The full path of the tool (e.g., "storage account list")
- **SourceFile**: The path to the source file where the tool is defined
- **Metadata**: Dictionary of metadata properties and their boolean values:
  - **Destructive**: Whether the tool performs destructive operations
  - **Idempotent**: Whether calling the tool repeatedly with the same arguments has no additional effect
  - **OpenWorld**: Whether the tool interacts with an unpredictable set of entities
  - **ReadOnly**: Whether the tool only reads data without modifying state
  - **Secret**: Whether the tool handles sensitive information
  - **LocalRequired**: Whether the tool requires local execution
- **Title**: The tool's title
- **Description**: The tool's description

### Sample Output

```json
[
  {
    "ToolPath": "storage account list",
    "SourceFile": "/workspaces/new-mcp/tools/Azure.Mcp.Tools.Storage/src/Commands/Account/StorageAccountListCommand.cs",
    "Metadata": {
      "Destructive": false,
      "Idempotent": true,
      "OpenWorld": false,
      "ReadOnly": true,
      "LocalRequired": false,
      "Secret": false
    },
    "Title": "List Storage Accounts",
    "Description": "Lists storage accounts in a subscription or resource group..."
  }
]
```

## VS Code Debugging

The project includes debugging support for VS Code to help you debug the documentation generation process. You can use the `Debug-MultiPageDocs.ps1` script to prepare the environment and then attach the VS Code debugger.

### Steps to Debug in VS Code

1. **Prepare the VS Code launch configuration**:

   Ensure you have the following configuration in your `.vscode/launch.json` file:
   
   ```json
   {
       "name": "Debug Generate Docs",
       "type": "coreclr",
       "request": "launch",
       "preLaunchTask": "build",
       "program": "${workspaceFolder}/docs-generation/CSharpGenerator/bin/Debug/net9.0/CSharpGenerator.dll",
       "args": [
           "generate-docs",
           "../generated/cli/cli-output.json",
           "../generated/multi-page",
           "--index",
           "--common",
           "--commands"
       ],
       "cwd": "${workspaceFolder}/docs-generation/CSharpGenerator",
       "console": "integratedTerminal",
       "stopAtEntry": false,
       "env": {
           "DOTNET_ENVIRONMENT": "Development"
       }
   },
   {
       "name": "Attach to .NET Process",
       "type": "coreclr",
       "request": "attach",
       "processId": "${command:pickProcess}"
   },
   {
       "name": "PowerShell Interactive Session",
       "type": "PowerShell",
       "request": "launch",
       "cwd": "${cwd}"
   }
   ```

2. **Set breakpoints**:
   
   Set breakpoints in the CSharpGenerator code (e.g., `Program.cs`) where you want to pause execution.

3. **Run the debug script**:

   ```bash
   cd docs-generation
   pwsh ./Debug-MultiPageDocs.ps1
   ```

   This script will:
   - Clean up previous output
   - Generate or use existing CLI output
   - Build the C# generator in debug mode
   - Pause and provide instructions for attaching the debugger

4. **Start the debugger**:

   When the script pauses with "READY FOR DEBUGGING", switch to VS Code and:
   
   - Select the "Debug Generate Docs" launch configuration from the Run panel
   - Start debugging (F5)

5. **Follow the execution**:

   - The debugger will stop at your breakpoints
   - You can examine variables, step through code, and debug as needed

If you prefer not to use the debug script, you can manually start debugging by:

1. Building the CSharpGenerator in Debug mode
2. Ensuring CLI output exists at `generated/cli/cli-output.json`
3. Starting the "Debug Generate Docs" launch configuration in VS Code
