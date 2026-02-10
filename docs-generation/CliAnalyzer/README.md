# CLI Analyzer

A .NET 9.0 console application that provides comprehensive visual analysis of the Azure MCP CLI JSON output, displaying namespaces, tools, and parameter statistics.

## Features

- **Overall Statistics**: Total namespaces, tools, parameters, and their breakdown
- **Namespace Analysis**: Detailed breakdown of tools and parameters per namespace
- **Parameter Tracking**: Counts required vs optional parameters for each tool
- **Top Tools Report**: Lists tools with the highest parameter counts
- **Markdown Report Generation**: Creates a clean Markdown report for sharing and viewing
- **Interactive Analysis**: Query specific namespaces and tools
- **Console Output**: Color-coded, formatted tables with statistics

## Prerequisites

- .NET 9.0 SDK or later
- CLI JSON file from Azure MCP server (`generated/cli/cli-output.json`)

## Installation

1. Navigate to the CliAnalyzer directory:
   ```bash
   cd docs-generation/CliAnalyzer
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

## Usage

### Basic Analysis (Full Summary + Markdown Report)

```bash
dotnet run
```

This will:
1. Load the default CLI JSON file (`../generated/cli/cli-output.json`)
2. Display a console summary with statistics and tables
3. Generate a Markdown report (`cli-analysis-report.md`)

### Custom CLI File

```bash
dotnet run -- --file path/to/cli-output.json
```

### Custom Output Path

```bash
dotnet run -- --output path/to/report.md
```

### Namespace Analysis

View detailed statistics for a specific namespace:

```bash
dotnet run -- --namespace acr
```

This displays:
- Tools in the namespace
- Parameter breakdown (required vs optional)
- Average parameters per tool

### Tool Analysis

View detailed information for a specific tool:

```bash
dotnet run -- --namespace acr --tool list
```

This shows:
- Full tool description
- Complete command
- Detailed parameter list with types and descriptions
- Required vs optional parameters

### Markdown Report Only

Generate Markdown report without console output:

```bash
dotnet run -- --html-only
```

### Combined Options

```bash
dotnet run -- --file ../generated/cli/cli-output.json --output ./analysis.md --html-only
```

## Output

### Console Output

The console displays:
- **Overall Statistics**: Aggregated counts and averages
- **Namespace Breakdown Table**: Tools and parameters per namespace
- **Top Tools**: 10 tools with the most parameters

Color coding:
- 🟦 Cyan: Column headers
- 🟨 Yellow: Section titles
- 🟩 Green: Overall header
- 🟥 Red: Required parameters

### Markdown Report

The Markdown report includes:
- Overall statistics table
- Namespace breakdown table
- Top 20 tools by parameter count table

## Architecture

### Models (`Models/`)
- **CliData.cs**: Deserialization models for CLI JSON structure
- **AnalysisResults.cs**: Analysis data structures with computed statistics

### Services (`Services/`)
- **CliJsonLoader.cs**: Loads CLI JSON from file or URL

### Analyzers (`Analyzers/`)
- **CliAnalyzer.cs**: Core analysis logic that groups tools by namespace

### Reports (`Reports/`)
- **ConsoleReporter.cs**: Formats and displays console output
- **MarkdownReporter.cs**: Generates Markdown reports

## Example Output

```
================================================================================
CLI ANALYSIS SUMMARY
================================================================================

OVERALL STATISTICS
-----------
  Total Namespaces                    30
  Total Tools                        208
  Total Parameters                 1,245
  Total Required Parameters          312
  Total Optional Parameters          933

  Avg Tools per Namespace            6.93
  Avg Required Params per Tool        1.50
  Avg Optional Params per Tool        4.48
  Avg Total Params per Tool           5.98

NAMESPACE BREAKDOWN
-----------
Namespace            Tools     Params   Required  Optional   Avg/Tool
-----------
acr                     4        24        8         16        6.00
aks                     6        45        12        33        7.50
...
```

## Building & Testing

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

### Publish for Distribution
```bash
dotnet publish -c Release -o ./bin/publish
```

## Performance

- Fast JSON deserialization using System.Text.Json
- Efficient grouping and sorting with LINQ
- Minimal memory footprint
- Process time: < 1 second for typical CLI JSON (200+ tools)

## Future Enhancements

- JSON export of analysis results
- CSV export for spreadsheet import
- Parameter usage statistics across tools
- Trend analysis for multiple CLI versions
- Interactive web dashboard

## License

Part of the Azure MCP Documentation Generator project.
