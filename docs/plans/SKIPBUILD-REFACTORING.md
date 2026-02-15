# SkipBuild Refactoring Summary

## Objective
Eliminate redundant `dotnet build` calls throughout the generation pipeline. Only `preflight.ps1` should build the solution once.

## Changes Required

### 1. Core Pipeline Scripts (Add SkipBuild Parameter)

These scripts are called from `Generate-ToolFamily.ps1` and need `SkipBuild` parameter:

#### `1-Generate-AnnotationsParametersRaw-One.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Wrap `dotnet build` at line ~184 in `if (-not $SkipBuild)`
- Pass `--no-build` to all `dotnet run` commands when `SkipBuild` is true

#### `2-Generate-ExamplePrompts-One.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Wrap `dotnet build` at line ~147 in `if (-not $SkipBuild)`
- Pass `--no-build` to `dotnet run` at line ~162

#### `3-Generate-ToolGenerationAndAIImprovements-One.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Wrap `dotnet build` at line ~171 in `if (-not $SkipBuild)`
- Pass `--no-build` to all `dotnet run` commands

#### `4-Generate-ToolFamilyCleanup-One.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Wrap `dotnet build` at lines ~156 and ~197 in `if (-not $SkipBuild)`
- Pass `--no-build` to `dotnet run` commands

#### `5-Generate-HorizontalArticles-One.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Wrap `dotnet build` at line ~137 in `if (-not $SkipBuild)`
- Pass `--no-build` to `dotnet run` at line ~157

### 2. Generate-ToolFamily.ps1 Updates

Update calls to step scripts to pass `-SkipBuild $SkipBuild`:

```powershell
# Line ~291
& $step1Script -ToolCommand $ToolFamily -OutputPath $OutputPath -SkipValidation:$SkipValidation -SkipBuild $SkipBuild

# Line ~318
& $step2Script -ToolCommand $ToolFamily -OutputPath $OutputPath -SkipValidation:$SkipValidation -SkipBuild $SkipBuild

# Line ~346
& $step3Script -ToolCommand $ToolFamily -OutputPath $OutputPath -SkipAIImprovements:$SkipAIImprovements -SkipValidation:$SkipValidation -SkipBuild $SkipBuild

# Line ~393
& $step4Script -ToolFamily $ToolFamily -OutputPath $OutputPath -SkipValidation:$SkipValidation -SkipBuild $SkipBuild

# Line ~436
& $step5Script -ServiceArea $ToolFamily -OutputPath $OutputPath -UseTextTransformation:$UseTextTransformation -SkipBuild $SkipBuild
```

### 3. Validation Scripts (Add SkipBuild Parameter)

#### `0-Validate-BrandMappings.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Wrap `dotnet build` at line ~91 in `if (-not $SkipBuild)`
- Pass `--no-build` to `dotnet run` at line ~110 when SkipBuild is true

**Update preflight.ps1 call** (line ~119):
```powershell
& $validationScript -OutputPath $OutputPath -SkipBuild $true
```

### 4. Helper Scripts Called from Step Scripts

These scripts are called by the main step scripts and need updates:

#### `scripts/Generate-Annotations.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Pass `--no-build` to `dotnet run` at line ~115 when SkipBuild is true

#### `scripts/Generate-Parameters.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Pass `--no-build` to `dotnet run` at line ~115 when SkipBuild is true

#### `scripts/Generate-RawTools.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Pass `--no-build` to `dotnet run` at line ~105 when SkipBuild is true

#### `scripts/Generate-ExamplePromptsAI.ps1`
- Add parameter: `[bool]$SkipBuild = $false`
- Pass `--no-build` to `dotnet run` at line ~98 when SkipBuild is true

### 5. Update Calls in Step Scripts

Each step script that calls helper scripts needs to pass `-SkipBuild $SkipBuild`:

**In 1-Generate-AnnotationsParametersRaw-One.ps1:**
- Pass to Generate-Annotations.ps1
- Pass to Generate-Parameters.ps1
- Pass to Generate-RawTools.ps1

**In 2-Generate-ExamplePrompts-One.ps1:**
- Pass to Generate-ExamplePromptsAI.ps1

## Testing

After implementing:
1. Run `bash start.sh advisor 1` - should see build ONLY during preflight
2. No "Building..." messages should appear during step execution
3. Verify all generation still works correctly

## Pattern for Implementation

For each script:
1. Add parameter to param block
2. Wrap existing `dotnet build` in conditional
3. Add `--no-build` flag to `dotnet run` when SkipBuild is true:
   ```powershell
   if ($SkipBuild) {
       & dotnet run --project $project --configuration Release --no-build -- $args
   } else {
       & dotnet run --project $project --configuration Release -- $args
   }
   ```
4. Update calling scripts to pass `-SkipBuild $SkipBuild`

## Files to Update (Summary)

**Main pipeline (5 files):**
- 1-Generate-AnnotationsParametersRaw-One.ps1
- 2-Generate-ExamplePrompts-One.ps1
- 3-Generate-ToolGenerationAndAIImprovements-One.ps1
- 4-Generate-ToolFamilyCleanup-One.ps1
- 5-Generate-HorizontalArticles-One.ps1

**Orchestrator (1 file):**
- Generate-ToolFamily.ps1

**Validation (1 file):**
- 0-Validate-BrandMappings.ps1

**Helper scripts (4 files):**
- scripts/Generate-Annotations.ps1
- scripts/Generate-Parameters.ps1
- scripts/Generate-RawTools.ps1
- scripts/Generate-ExamplePromptsAI.ps1

**Already updated:**
- ✅ preflight.ps1
- ✅ Generate-ToolFamily.ps1 (has SkipBuild parameter)
- ✅ generate-tool-family.sh (passes -SkipBuild $true)
- ✅ scripts/Invoke-CliAnalyzer.ps1

**Total: 11 files need updates**
