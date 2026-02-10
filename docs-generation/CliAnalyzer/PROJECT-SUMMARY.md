# CLI Analyzer - Project Summary

## Overview

The **CliAnalyzer** is a .NET 9.0 console application that provides comprehensive visual analysis of the Azure MCP CLI JSON output. It extracts and analyzes tool metadata, including namespaces, tools, parameters (both required and optional), and generates both console reports and beautiful HTML visualizations.

## Project Structure

```
CliAnalyzer/
├── CliAnalyzer.csproj              # Project file (.NET 9.0)
├── Program.cs                      # Entry point with System.CommandLine integration
├── README.md                       # User-facing documentation
├── Models/
│   ├── CliData.cs                 # JSON deserialization models (Tool, Parameter, etc.)
│   └── AnalysisResults.cs         # Analysis data structures with computed statistics
├── Services/
│   └── CliJsonLoader.cs           # Loads CLI JSON from file or URL
├── Analyzers/
│   └── CliAnalyzer.cs             # Core analysis logic (namespace grouping)
└── Reports/
    ├── ConsoleReporter.cs         # Console output formatting with tables
    └── HtmlReporter.cs            # HTML report generation
```

## Key Features

### 1. **Overall Statistics**
- Total namespaces and tools
- Parameter counts (required vs optional)
- Average parameters per tool
- Average tools per namespace

### 2. **Namespace Analysis**
Groups tools by Azure service namespace with detailed breakdowns:
- Tool count per namespace
- Parameter distribution (required/optional)
- Statistical averages

### 3. **Top Tools Report**
Identifies tools with the highest parameter counts to spot complex operations.

### 4. **Interactive Querying**
Query specific namespaces and drill down to individual tool details including:
- Full command syntax
- Parameter descriptions
- Type information
- Required vs optional distinction

### 5. **Dual Output Formats**
- **Console Output**: Color-coded tables with statistics
- **HTML Report**: Responsive, mobile-friendly dashboard with:
  - Statistics cards
  - Interactive data tables
  - Top tools ranking
  - Professional styling

## Usage Examples

### Basic Analysis
```bash
dotnet run --project docs-generation/CliAnalyzer
```
Displays full summary and generates `cli-analysis-report.html`

### Namespace Details
```bash
dotnet run --project docs-generation/CliAnalyzer -- --namespace foundry
```
Shows all tools in the Foundry namespace with their parameter counts

### Tool Details
```bash
dotnet run --project docs-generation/CliAnalyzer -- \
  --namespace foundry --tool "chat-completions-create"
```
Displays complete information about a specific tool, including all parameters

### Custom Paths
```bash
dotnet run --project docs-generation/CliAnalyzer -- \
  --file ../generated/cli/cli-output.json \
  --output ./my-report.html
```

### HTML Report Only
```bash
dotnet run --project docs-generation/CliAnalyzer -- --html-only
```

## Data Model

### Tool Structure
```csharp
public class Tool
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Command { get; set; }
    public List<Parameter> Options { get; set; }
    public string Namespace { get; } // Derived from Command
    public int RequiredParameterCount { get; }
    public int OptionalParameterCount { get; }
    public int TotalParameterCount { get; }
}
```

### Parameter Structure
```csharp
public class Parameter
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public bool Required { get; set; }
}
```

### Analysis Results
```csharp
public class AnalysisResults
{
    public List<NamespaceAnalysis> Namespaces { get; set; }
    // ... computed statistics:
    // - TotalNamespaces, TotalTools, TotalParameters
    // - TotalRequiredParameters, TotalOptionalParameters
    // - Averages: Required/Tool, Optional/Tool, Total/Tool, Tools/Namespace
}
```

## Technical Architecture

### Deserialization (`Models/CliData.cs`)
Uses `System.Text.Json` with property name mapping for case-insensitive JSON deserialization.

### Data Loading (`Services/CliJsonLoader.cs`)
- File I/O with error handling
- Path resolution (supports relative and absolute paths)
- Graceful error messages for missing files

### Analysis (`Analyzers/CliAnalyzer.cs`)
- Groups tools by namespace (derived from command)
- Calculates statistics on-the-fly
- Sorts by namespace name for consistent output

### Reporting (`Reports/`)
- **ConsoleReporter**: 
  - Color-coded output (with try-catch for unsupported environments)
  - Formatted tables with aligned columns
  - Text wrapping for descriptions
  
- **HtmlReporter**: 
  - Gradient stat cards
  - Responsive grid layout
  - Sortable data tables
  - Mobile-friendly CSS

### Command-Line Interface (`Program.cs`)
Uses `System.CommandLine` for modern, composable option handling:
- `--file` / `-f`: Input file path
- `--output` / `-o`: Output file path
- `--namespace` / `-n`: Filter by namespace
- `--tool` / `-t`: Filter by tool name
- `--html-only`: Skip console output

## Performance

- **Fast JSON Processing**: < 1 second for 231 tools
- **Memory Efficient**: Minimal allocations, LINQ projection
- **Streaming Output**: No buffering, real-time console feedback

## Error Handling

- Missing file detection with helpful messages
- JSON deserialization error reporting with details
- Console color support detection (graceful degradation)
- Invalid namespace/tool lookups with suggestions

## Building & Testing

### Build
```bash
cd docs-generation/CliAnalyzer
dotnet build
```

### Test Individual Features
```bash
# Full report
dotnet run

# Namespace analysis
dotnet run -- --namespace sql

# Tool details
dotnet run -- --namespace mysql --tool "create"

# With custom file
dotnet run -- --file "/path/to/cli.json"
```

### Publish for Distribution
```bash
dotnet publish -c Release -o ./bin/publish
```

## Integration Points

This project integrates with:
1. **CLI JSON Source**: `../generated/cli/cli-output.json` from MCP CLI
2. **Documentation Generation**: Results can feed into doc generation pipelines
3. **Analysis Dashboards**: HTML output can be embedded in web apps
4. **CI/CD Pipelines**: Programmatic access via command-line options

## Future Enhancements

- JSON export of analysis results for programmatic consumption
- CSV export for spreadsheet analysis
- Parameter usage frequency analysis across tools
- Trend comparison between CLI versions
- Interactive web dashboard (Blazor)
- Parameter type statistics and categorization
- Command pattern detection and reporting
- API endpoint for analysis results

## Dependencies

- **.NET 9.0 SDK**: Runtime and tooling
- **System.Text.Json**: JSON deserialization (built-in)
- **System.CommandLine**: Command-line parsing (v2.0.0-beta)
- **Central Package Management**: Via `Directory.Packages.props`

## Code Quality

- **Fully Optimized**: Efficient LINQ, minimal allocations
- **Error Resilient**: Try-catch for platform-specific features
- **Well-Documented**: Comprehensive README and inline comments
- **Modular Design**: Separation of concerns across layers
- **Testable**: Dependency injection ready, loosely coupled services

## Output Examples

### Console Summary
```
================================================================================
CLI ANALYSIS SUMMARY
================================================================================

OVERALL STATISTICS
------------------
  Total Namespaces                            50
  Total Tools                                231
  Total Parameters                          2626
  Total Required Parameters                  477
  Total Optional Parameters                 2149

  Avg Tools per Namespace                   4.62
  Avg Required Params per Tool              2.06
  Avg Optional Params per Tool              9.30
  Avg Total Params per Tool                11.37
```

### HTML Report
Generated HTML includes:
- 6 responsive statistics cards (gradient background)
- Namespace breakdown table with sorting
- Top 20 tools by parameter count
- Mobile-friendly responsive design
- Professional color scheme

## Running from Repository Root

```bash
# From the repo root directory
dotnet run --project docs-generation/CliAnalyzer [options]
```

The project automatically resolves paths relative to the current working directory, supporting both repo root and docs-generation subdirectory execution.
