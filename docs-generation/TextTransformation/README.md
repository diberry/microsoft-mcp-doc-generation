# TextTransformation Library

A .NET 9.0 library for managing text transformations, filename generation, and parameter normalization in the Azure MCP Documentation Generator.

## Overview

The TextTransformation library consolidates all text transformation logic previously scattered across multiple configuration files and code files. It provides a unified, lexicon-based approach to managing:

- Acronyms and their canonical forms
- Compound word transformations
- Stop words for different contexts
- Service brand name mappings
- Parameter name normalization
- Context-specific transformation rules

## Architecture

### Components

1. **Models** - Data structures for configuration
   - `TransformationConfig` - Root configuration object
   - `Lexicon` - Central dictionary of terms
   - `ServiceMapping` - Service name mappings
   - `ContextRules` - Context-specific rules

2. **Services** - Transformation logic
   - `TransformationEngine` - Main orchestration
   - `FilenameGenerator` - Three-tier filename resolution
   - `TextNormalizer` - Text cleaning and normalization

3. **ConfigLoader** - Configuration loading and reference resolution

## Configuration Schema

The library uses a lexicon-based JSON configuration format that eliminates duplication through `$ref`-style references.

### Example Configuration

```json
{
  "lexicon": {
    "acronyms": {
      "aks": {
        "canonical": "AKS",
        "expansion": "Azure Kubernetes Service",
        "preserveInTitleCase": true
      },
      "id": {
        "canonical": "ID",
        "plural": "IDs"
      }
    },
    "compoundWords": {
      "nodepool": {
        "components": ["node", "pool"],
        "joinStrategy": "hyphenate"
      }
    },
    "stopWords": ["a", "the", "or", "and", "in"],
    "abbreviations": {
      "eg": {
        "canonical": "e.g.",
        "expansion": "for example"
      }
    },
    "azureTerms": {
      "rg": {
        "display": "resource group",
        "description": "A logical container for Azure resources"
      }
    }
  },
  "services": {
    "mappings": [
      {
        "mcpName": "aks",
        "shortName": "$lexicon.acronyms.aks",
        "brandName": "Azure Kubernetes Service",
        "filename": "azure-kubernetes-service"
      }
    ]
  },
  "contexts": {
    "filename": {
      "rules": {
        "stopWords": "remove"
      }
    },
    "titleCase": {
      "rules": {
        "stopWords": "lowercase-unless-first"
      }
    }
  },
  "categoryDefaults": {
    "acronym": {
      "filenameTransform": "to-lowercase",
      "preserveInTitleCase": true
    }
  },
  "parameters": {
    "mappings": [
      {
        "parameter": "subscriptionId",
        "display": "subscription ID"
      }
    ]
  }
}
```

### Key Features

#### 1. Lexicon-Based Design
All terms are defined once in the lexicon and referenced elsewhere:
```json
"shortName": "$lexicon.acronyms.aks"
```

#### 2. Context-Specific Rules
Different transformation rules for different contexts:
- `filename` - For generating file names
- `titleCase` - For title case conversions
- `display` - For display text
- `description` - For descriptions

#### 3. Category Defaults
Apply consistent rules to categories of terms:
```json
"categoryDefaults": {
  "acronym": {
    "filenameTransform": "to-lowercase"
  }
}
```

## Usage

### Basic Setup

```csharp
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;

// Load configuration
var loader = new ConfigLoader("transformation-config.json");
var config = await loader.LoadAsync();

// Create transformation engine
var engine = new TransformationEngine(config);
```

### Service Name Transformations

```csharp
// Get display name
var displayName = engine.GetServiceDisplayName("aks");
// Returns: "Azure Kubernetes Service"

// Get short name
var shortName = engine.GetServiceShortName("aks");
// Returns: "AKS"
```

### Filename Generation

The library uses a three-tier resolution strategy:

1. **Tier 1** - Brand name from service mappings (highest priority)
2. **Tier 2** - Compound word transformations (medium priority)
3. **Tier 3** - Original area name (fallback)

```csharp
var filenameGen = engine.FilenameGenerator;

// Generate include filename
var filename = filenameGen.GenerateFilename("aks", "get-cluster", "annotations");
// Returns: "azure-kubernetes-service-get-cluster-annotations.md"

// Generate main service filename
var mainFile = filenameGen.GenerateMainServiceFilename("aks");
// Returns: "azure-kubernetes-service.md"

// Clean a filename (removes stop words)
var cleaned = filenameGen.CleanFilename("get-a-list-of-the-items");
// Returns: "get-list-items"
```

### Parameter Name Normalization

```csharp
var normalizer = engine.TextNormalizer;

// Normalize parameter name
var normalized = normalizer.NormalizeParameter("subscriptionId");
// Returns: "subscription ID"

// Split camelCase
var split = normalizer.SplitAndTransformProgrammaticName("resourceGroupName");
// Returns: "resource group name"
```

### Text Transformations

```csharp
var normalizer = engine.TextNormalizer;

// Convert to title case (preserves acronyms)
var title = normalizer.ToTitleCase("get vm id");
// Returns: "Get VM ID"

// Replace abbreviations
var replaced = normalizer.ReplaceStaticText("Use eg for examples");
// Returns: "Use e.g. for examples"

// Ensure ends with period
var withPeriod = normalizer.EnsureEndsPeriod("This is a test");
// Returns: "This is a test."
```

### Description Transformations

```csharp
// Transform description (replacements + period)
var description = engine.TransformDescription("Use eg for examples");
// Returns: "Use e.g. for examples."
```

## Migration from Legacy Configuration

### Old System (5 separate files)
- `stop-words.json` - 5 entries
- `compound-words.json` - 21 entries
- `brand-to-server-mapping.json` - 44 entries
- `nl-parameters.json` - 4 entries
- `static-text-replacement.json` - 7 entries
- Hard-coded acronyms in `TextCleanup.cs` - 31 acronyms

### New System (1 unified file)
All consolidated into `transformation-config.json` with:
- **Lexicon** section for all terms
- **Services** section for mappings
- **Contexts** for context-specific rules
- **$ref** references to eliminate duplication

### Benefits
1. **Single Source of Truth** - All transformations in one place
2. **No Duplication** - Terms defined once, referenced everywhere
3. **Type Safety** - Strongly-typed C# models
4. **Extensibility** - Easy to add new contexts and rules
5. **Maintainability** - Clear relationships between terms
6. **Testability** - Comprehensive unit test coverage

## Three-Tier Filename Resolution

The filename generator implements a prioritized resolution strategy:

```
Input: "aks"
  ↓
Tier 1: Check Services.Mappings
  → Found: filename="azure-kubernetes-service"
  → Use: "azure-kubernetes-service"
  
Input: "nodepool"
  ↓
Tier 1: Check Services.Mappings
  → Not Found
  ↓
Tier 2: Check Lexicon.CompoundWords
  → Found: components=["node", "pool"]
  → Use: "node-pool"
  
Input: "unknown"
  ↓
Tier 1: Check Services.Mappings
  → Not Found
  ↓
Tier 2: Check Lexicon.CompoundWords
  → Not Found
  ↓
Tier 3: Use Original
  → Use: "unknown"
```

## Testing

The library includes comprehensive unit tests:

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ConfigLoaderTests"
dotnet test --filter "FullyQualifiedName~FilenameGeneratorTests"
dotnet test --filter "FullyQualifiedName~TransformationEngineTests"
```

### Test Coverage
- **ConfigLoaderTests** - Configuration loading and reference resolution
- **FilenameGeneratorTests** - Three-tier resolution and filename cleaning
- **TransformationEngineTests** - End-to-end transformation scenarios

## Design Patterns

### 1. Lexicon Pattern
Central dictionary eliminates duplication:
```json
"acronyms": { "aks": { "canonical": "AKS" } }
"services": { "shortName": "$lexicon.acronyms.aks" }
```

### 2. Context Pattern
Different rules for different uses:
```json
"contexts": {
  "filename": { "rules": { "stopWords": "remove" } },
  "titleCase": { "rules": { "stopWords": "lowercase-unless-first" } }
}
```

### 3. Category Defaults Pattern
Consistent behavior for term categories:
```json
"categoryDefaults": {
  "acronym": { "filenameTransform": "to-lowercase" }
}
```

## API Reference

### TransformationEngine
- `GetServiceDisplayName(string mcpName)` - Get brand name
- `GetServiceShortName(string mcpName)` - Get short name
- `TransformDescription(string description)` - Full description transformation
- `TextNormalizer` - Access to text normalization
- `FilenameGenerator` - Access to filename generation

### FilenameGenerator
- `GenerateFilename(string area, string operation, string type)` - Generate include filename
- `GenerateMainServiceFilename(string area)` - Generate main service filename
- `CleanFilename(string filename)` - Clean and normalize filename

### TextNormalizer
- `NormalizeParameter(string param)` - Parameter to natural language
- `SplitAndTransformProgrammaticName(string name)` - Split camelCase
- `ToTitleCase(string text, string context)` - Title case with acronym preservation
- `ReplaceStaticText(string text)` - Apply abbreviation replacements
- `EnsureEndsPeriod(string text)` - Ensure text ends with period

### ConfigLoader
- `LoadAsync()` - Load and parse configuration file
- Automatically resolves `$ref` references

## Examples

### Example 1: Service Documentation
```csharp
var engine = new TransformationEngine(config);
var mcpName = "aks";

var displayName = engine.GetServiceDisplayName(mcpName);
// "Azure Kubernetes Service"

var filename = engine.FilenameGenerator.GenerateMainServiceFilename(mcpName);
// "azure-kubernetes-service.md"
```

### Example 2: Parameter Documentation
```csharp
var normalizer = engine.TextNormalizer;
var paramName = "subscriptionId";

var display = normalizer.NormalizeParameter(paramName);
// "subscription ID"

var description = $"The {display} to use for the operation";
// "The subscription ID to use for the operation"
```

### Example 3: Include File Generation
```csharp
var filenameGen = engine.FilenameGenerator;

var annotationFile = filenameGen.GenerateFilename("aks", "get-cluster", "annotations");
// "azure-kubernetes-service-get-cluster-annotations.md"

var parameterFile = filenameGen.GenerateFilename("storage", "list-accounts", "parameters");
// "storage-list-accounts-parameters.md"
```

## Troubleshooting

### Issue: Reference not resolved
**Symptom**: `$lexicon.acronyms.aks` appears in output instead of "AKS"

**Solution**: Ensure the key exists in the lexicon:
```json
"lexicon": {
  "acronyms": {
    "aks": { "canonical": "AKS" }
  }
}
```

### Issue: Stop words not removed
**Symptom**: Filename contains "a", "the", etc.

**Solution**: Check context rules:
```json
"contexts": {
  "filename": {
    "rules": { "stopWords": "remove" }
  }
}
```

### Issue: Acronyms not preserved in title case
**Symptom**: "ID" becomes "Id" in titles

**Solution**: Set preserveInTitleCase:
```json
"acronyms": {
  "id": {
    "canonical": "ID",
    "preserveInTitleCase": true
  }
}
```

## Version History

- **1.0.0** - Initial release
  - Lexicon-based configuration
  - Three-tier filename resolution
  - Context-specific transformation rules
  - Comprehensive test coverage

## License

Part of the Azure MCP Documentation Generator project.
