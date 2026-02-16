#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates annotations, parameters, raw tool files, commands, and common pages

.DESCRIPTION
    Runs the base generation steps required for tool pipelines:
    1. Annotations
    2. Parameters
    3. Raw tool files (via scripts/Generate-RawTools.ps1)
    4. Commands page
    5. Common tools page

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from script location)

.PARAMETER CreateCommands
    Whether to create a commands page (default: true)

.PARAMETER CreateCommon
    Whether to create a common tools page (default: true)

.PARAMETER CreateServiceOptions
    Whether to create a service start options page (default: true)

.EXAMPLE
    ./1-Generate-AnnotationsParametersRaw.ps1
    ./1-Generate-AnnotationsParametersRaw.ps1 -OutputPath ../generated
    ./1-Generate-AnnotationsParametersRaw.ps1 -CreateCommands $false
#>

param(
    [string]$OutputPath = "../generated",
    [bool]$CreateCommands = $true,
    [bool]$CreateCommon = $true,
    [bool]$CreateServiceOptions = $true
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

function Normalize-Command {
    param([string]$Command)

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return $null
    }

    $normalized = ($Command -replace "\s+", " ").Trim().ToLowerInvariant()
    return $normalized
}

function Get-CommandFromFile {
    param([string]$FilePath)

    try {
        $lines = Get-Content -Path $FilePath -TotalCount 50 -ErrorAction Stop
    } catch {
        return $null
    }

    foreach ($line in $lines) {
        if ($line -match "^#\s*azmcp\s+(.+)$") {
            return $matches[1].Trim()
        }

        if ($line -match "^\s*<!--\s*@mcpcli\s+(.+?)\s*-->") {
            return $matches[1].Trim()
        }
    }

    return $null
}

function Build-CommandMap {
    param(
        [string]$DirectoryPath,
        [string]$FileLabel
    )

    $map = @{}
    $errors = @()
    $duplicates = @()

    if (-not (Test-Path $DirectoryPath)) {
        return [pscustomobject]@{
            Map = $map
            Errors = @("Directory not found: $DirectoryPath")
            Duplicates = $duplicates
            Count = 0
        }
    }

    $files = @(Get-ChildItem -Path $DirectoryPath -Filter "*.md" -File -ErrorAction SilentlyContinue)
    foreach ($file in $files) {
        $command = Get-CommandFromFile -FilePath $file.FullName
        if (-not $command) {
            $errors += "$FileLabel missing command: $($file.Name)"
            continue
        }

        $normalized = Normalize-Command -Command $command
        if (-not $normalized) {
            $errors += "$FileLabel invalid command: $($file.Name)"
            continue
        }

        if ($map.ContainsKey($normalized)) {
            $duplicates += "$FileLabel duplicate command '$normalized': $($file.Name)"
            continue
        }

        $map[$normalized] = $file.FullName
    }

    return [pscustomobject]@{
        Map = $map
        Errors = $errors
        Duplicates = $duplicates
        Count = $files.Count
    }
}

function Write-ValidationSummary {
    param(
        [string]$Title,
        [string[]]$Missing,
        [string[]]$Extra,
        [string[]]$Errors,
        [string[]]$Duplicates
    )

    Write-Info "$Title"

    if ($Missing.Count -eq 0 -and $Extra.Count -eq 0 -and $Errors.Count -eq 0 -and $Duplicates.Count -eq 0) {
        Write-Success "✓ Validation passed"
        return $true
    }

    if ($Missing.Count -gt 0) {
        Write-Warning "Missing tools ($($Missing.Count))"
        $Missing | ForEach-Object { Write-Warning "  - $_" }
    }

    if ($Extra.Count -gt 0) {
        Write-Warning "Extra files ($($Extra.Count))"
        $Extra | ForEach-Object { Write-Warning "  - $_" }
    }

    if ($Errors.Count -gt 0) {
        Write-Warning "Parse errors ($($Errors.Count))"
        $Errors | ForEach-Object { Write-Warning "  - $_" }
    }

    if ($Duplicates.Count -gt 0) {
        Write-Warning "Duplicate commands ($($Duplicates.Count))"
        $Duplicates | ForEach-Object { Write-Warning "  - $_" }
    }

    return $false
}

try {
    Write-Progress "Annotations + Parameters + Raw Tools"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""

    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path (Get-Location) $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }

    Write-Info "Output directory: $outputDir"

    $cliOutputFile = Join-Path $outputDir "cli/cli-output.json"
    $versionOutputFile = Join-Path $outputDir "cli/cli-version.json"

    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }

    if (-not (Test-Path $versionOutputFile)) {
        Write-Warning "CLI version file not found: $versionOutputFile"
    }

    $cliVersion = "unknown"
    if (Test-Path $versionOutputFile) {
        $versionContent = Get-Content $versionOutputFile -Raw
        if ($versionContent.Trim().StartsWith('{')) {
            $versionData = $versionContent | ConvertFrom-Json
            $cliVersion = $versionData.version ?? $versionData.Version ?? "unknown"
        } else {
            $cliVersion = $versionContent.Trim()
        }
    }

    Write-Info "CLI Version: $cliVersion"
    Write-Info ""

    Write-Progress "Step 1: Generating annotation include files..."
    & "$scriptDir\Generate-Annotations.ps1" -OutputPath $OutputPath -CliVersion $cliVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Annotations generation failed"
    }

    Write-Progress "Step 2: Generating parameter include files..."
    & "$scriptDir\Generate-Parameters.ps1" -OutputPath $OutputPath -CliVersion $cliVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Parameters generation failed"
    }

    Write-Progress "Step 3: Generating raw tool files..."
    & "$scriptDir\Generate-RawTools.ps1" -OutputPath $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Raw tool generation failed"
    }

    Write-Progress "Step 4: Generating commands page..."
    & "$scriptDir\Generate-Commands.ps1" -OutputPath $OutputPath -CliVersion $cliVersion -CreateServiceOptions $CreateServiceOptions
    if ($LASTEXITCODE -ne 0) {
        throw "Commands generation failed"
    }

    Write-Progress "Step 5: Generating common tools page..."
    & "$scriptDir\Generate-Common.ps1" -OutputPath $OutputPath -CliVersion $cliVersion -CreateServiceOptions $CreateServiceOptions
    if ($LASTEXITCODE -ne 0) {
        throw "Common tools generation failed"
    }

    Write-Progress "Step 6: Validating tool coverage..."
    $cliJson = Get-Content $cliOutputFile -Raw | ConvertFrom-Json
    $cliCommands = @($cliJson.results | ForEach-Object { $_.command } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $cliCommandSet = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($command in $cliCommands) {
        $normalized = Normalize-Command -Command $command
        if ($normalized) {
            [void]$cliCommandSet.Add($normalized)
        }
    }

    $annotationsDir = Join-Path $outputDir "annotations"
    $parametersDir = Join-Path $outputDir "parameters"
    $rawToolsDir = Join-Path $outputDir "tools-raw"

    $annotationsResult = Build-CommandMap -DirectoryPath $annotationsDir -FileLabel "Annotations"
    $parametersResult = Build-CommandMap -DirectoryPath $parametersDir -FileLabel "Parameters"
    $rawToolsResult = Build-CommandMap -DirectoryPath $rawToolsDir -FileLabel "RawTools"

    $missingAnnotations = @($cliCommandSet | Where-Object { -not $annotationsResult.Map.ContainsKey($_) })
    $missingParameters = @($cliCommandSet | Where-Object { -not $parametersResult.Map.ContainsKey($_) })
    $missingRawTools = @($cliCommandSet | Where-Object { -not $rawToolsResult.Map.ContainsKey($_) })

    $extraAnnotations = @($annotationsResult.Map.Keys | Where-Object { -not $cliCommandSet.Contains($_) })
    $extraParameters = @($parametersResult.Map.Keys | Where-Object { -not $cliCommandSet.Contains($_) })
    $extraRawTools = @($rawToolsResult.Map.Keys | Where-Object { -not $cliCommandSet.Contains($_) })

    Write-Info "CLI tools: $($cliCommandSet.Count)"
    Write-Info "Annotations files: $($annotationsResult.Count)"
    Write-Info "Parameters files: $($parametersResult.Count)"
    Write-Info "Raw tools files: $($rawToolsResult.Count)"

    Write-Info ""
    $annotationsOk = Write-ValidationSummary -Title "Annotations validation" -Missing $missingAnnotations -Extra $extraAnnotations -Errors $annotationsResult.Errors -Duplicates $annotationsResult.Duplicates
    $parametersOk = Write-ValidationSummary -Title "Parameters validation" -Missing $missingParameters -Extra $extraParameters -Errors $parametersResult.Errors -Duplicates $parametersResult.Duplicates
    $rawToolsOk = Write-ValidationSummary -Title "Raw tools validation" -Missing $missingRawTools -Extra $extraRawTools -Errors $rawToolsResult.Errors -Duplicates $rawToolsResult.Duplicates

    if (-not ($annotationsOk -and $parametersOk -and $rawToolsOk)) {
        throw "Validation failed: missing or extra tool files detected"
    }

    Write-Info ""
    Write-Info "Final counts"
    Write-Info "  Annotations: $($annotationsResult.Count)"
    Write-Info "  Parameters: $($parametersResult.Count)"
    Write-Info "  Raw tools:   $($rawToolsResult.Count)"

    Write-Success "Annotations, parameters, raw tools, commands, and common pages generated successfully"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} catch {
    Write-Error "Generation failed: $($_.Exception.Message)"
    exit 1
}
