# CLI Version Capture Feature

## Overview

The documentation generator now captures the Azure MCP CLI version and includes it in the generated documentation frontmatter.

## Implementation

### 1. PowerShell Script (`Generate-MultiPageDocs.ps1`)

The script captures the version before generating documentation:

```powershell
Write-Progress "Capturing CLI version..."
$versionOutput = & dotnet run --no-build -- --version 2>&1
if ($LASTEXITCODE -ne 0) { 
    Write-Warning "Failed to capture CLI version, continuing without it"
    $cliVersion = "unknown"
} else {
    # Filter out launch settings and build messages, get just the version number
    $cliVersion = ($versionOutput | Where-Object { $_ -match '^\d+\.\d+\.\d+' } | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($cliVersion)) {
        $cliVersion = "unknown"
    }
    Write-Info "CLI Version: $cliVersion"
}
```

The version is then passed to the C# generator:

```powershell
if ($cliVersion -and $cliVersion -ne "unknown") { 
    $generatorArgs += "--version"
    $generatorArgs += $cliVersion
}
```

### 2. C# Generator (`Program.cs`)

The generator accepts the version parameter:

```csharp
// Extract version if provided
string? cliVersion = null;
var versionIndex = Array.IndexOf(args, "--version");
if (versionIndex >= 0 && versionIndex + 1 < args.Length)
{
    cliVersion = args[versionIndex + 1];
}

return await DocumentationGenerator.GenerateAsync(
    cliOutputFile,
    outputDir,
    generateIndex,
    generateCommon,
    generateCommands,
    generateServiceOptions,
    generateAnnotations,
    cliVersion);
```

### 3. Documentation Generator (`DocumentationGenerator.cs`)

The version is set in the transformed data:

```csharp
// Set version if provided
if (!string.IsNullOrWhiteSpace(cliVersion))
{
    transformedData.Version = cliVersion;
    Console.WriteLine($"Using CLI version: {cliVersion}");
}
```

### 4. Handlebars Templates

All templates now include the version in their YAML frontmatter:

**Area Template (`area-template.hbs`):**
```handlebars
---
ms.topic: include
ms.date: {{formatDateShort generatedAt}}
ms.version: {{version}}
---

# {{areaName}} Tools

**Area:** {{areaName}}  
**Description:** {{areaData.description}}  
**Tool Count:** {{areaData.toolCount}}  
**Generated:** {{formatDate generatedAt}}  
**Version:** {{version}}
```

**Commands Template (`commands-template.hbs`):**
```handlebars
---
ms.topic: include
ms.date: {{formatDateShort generatedAt}}
ms.version: {{version}}
---

# Azure MCP CLI Commands

**Generated:** {{formatDate generatedAt}}  
**Version:** {{version}}
```

**Annotation Template (`annotation-template.hbs`):**
```handlebars
---
ms.topic: include
ms.date: {{formatDateShort generatedAt}}
ms.version: {{version}}
# [!INCLUDE [{{command}}](../includes/tools/annotations/{{annotationFileName}})]
# azmcp {{command}}
---
```

**Parameter Template (`parameter-template.hbs`):**
```handlebars
---
ms.topic: include
ms.date: {{formatDateShort generatedAt}}
ms.version: {{version}}
# [!INCLUDE [{{command}}](../includes/tools/parameters/{{parameterFileName}})]
<!-- azmcp {{command}} -->
---
```

**Other Templates:**
- `param-annotation-template.hbs` - ✅ Version in frontmatter
- `common-tools.hbs` - ✅ Version in frontmatter
- `service-start-option.hbs` - ✅ Version in frontmatter
- `tool-annotations-template.hbs` - ✅ Version in frontmatter

### Version Data Flow

The version flows through the system as follows:

1. **PowerShell** captures version → `$cliVersion`
2. **C# Generator** receives via `--version` → `cliVersion` parameter
3. **DocumentationGenerator** stores in → `transformedData.Version`
4. **Individual File Generators** pass to templates via context:
   - `GenerateAnnotationFilesAsync()` - Adds `["version"] = data.Version`
   - `GenerateParameterFilesAsync()` - Adds `["version"] = data.Version`
   - `GenerateParamAnnotationFilesAsync()` - Adds `["version"] = data.Version`
   - `GenerateAreaPageAsync()` - Uses `data.Version` from transformedData
   - Other generators also use `data.Version`
5. **Handlebars Templates** render → `{{version}}` in frontmatter and body

## Version Format

The captured version follows the format:
```
2.0.0-beta.4+12ef1fb57a0107622e25739243f59086a4900983
```

This includes:
- **Major.Minor.Patch**: `2.0.0`
- **Pre-release tag**: `beta.4`
- **Build metadata**: Git commit hash (`12ef1fb5...`)

## Example Output

When the documentation is generated, each file will include the version in its YAML frontmatter **and** in the document body:

```markdown
---
ms.topic: include
ms.date: 2025-11-13
ms.version: 2.0.0-beta.4+12ef1fb57a0107622e25739243f59086a4900983
---

# ACR Tools

**Area:** acr  
**Description:** Azure Container Registry operations  
**Tool Count:** 2  
**Generated:** November 13, 2025 10:30:00 AM UTC  
**Version:** 2.0.0-beta.4+12ef1fb57a0107622e25739243f59086a4900983
```

### Frontmatter Usage

The `ms.version` field in the frontmatter can be used by:
- **Microsoft Learn**: Version-specific documentation rendering
- **Documentation tools**: Automated version tracking and indexing
- **Search engines**: Better SEO with version metadata
- **Build pipelines**: Version validation and compatibility checks

## Fallback Behavior

If the version cannot be captured:
1. PowerShell script sets `$cliVersion = "unknown"`
2. C# generator uses the hardcoded default: `"1.0.0"`
3. Documentation is generated with fallback version

## Testing

### Test Version Capture

```bash
# From Docker container
./run-mcp-cli.sh --version

# Output:
# Using launch settings from /mcp/servers/Azure.Mcp.Server/src/Properties/launchSettings.json...
# Building...
# 2.0.0-beta.4+12ef1fb57a0107622e25739243f59086a4900983
```

### Test Documentation Generation

```bash
# Full documentation generation
cd docs-generation
pwsh ./Generate-MultiPageDocs.ps1

# Check version in generated files
grep "Version:" generated/multi-page/acr.md
```

### Test C# Generator Directly

```bash
cd docs-generation/CSharpGenerator
dotnet run --configuration Release -- generate-docs \
    ../generated/cli-output.json \
    ../generated/multi-page \
    --index --common --commands --annotations \
    --version "2.0.0-beta.4+test"
```

## Benefits

1. **Traceability**: Know which CLI version generated the documentation
2. **Version Tracking**: Track documentation across CLI versions
3. **Debugging**: Easier to identify version-specific issues
4. **Compliance**: Meet documentation versioning requirements

## Future Enhancements

- Add version comparison in generated documentation
- Generate version changelog
- Include version in filename or directory structure
- Add version-specific documentation sections

## Related Files

- `docs-generation/Generate-MultiPageDocs.ps1` - Main orchestration script
- `docs-generation/CSharpGenerator/Program.cs` - CLI argument parsing
- `docs-generation/CSharpGenerator/DocumentationGenerator.cs` - Version handling
- `docs-generation/templates/area-template.hbs` - Area page template
- `docs-generation/templates/commands-template.hbs` - Commands page template

## See Also

- [Main README](../README.md)
- [Generator README](../docs-generation/README.md)
- [Architecture Guide](./ARCHITECTURE.md)
