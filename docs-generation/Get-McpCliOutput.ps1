#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate MCP CLI output files for documentation generation
    
.DESCRIPTION
    This script uses the @azure/mcp CLI from test-npm-azure-mcp to generate
    three output files: cli-output.json, cli-namespace.json, and cli-version.json
    
    This script runs locally using npm to call the CLI commands.
    
.PARAMETER OutputPath
    Directory where CLI output files will be saved (default: generated/cli)
    
.PARAMETER SkipIfExists
    Skip generation if all three output files already exist and are valid
    
.EXAMPLE
    ./Get-McpCliOutput.ps1
    ./Get-McpCliOutput.ps1 -OutputPath /output/cli
    ./Get-McpCliOutput.ps1 -SkipIfExists
#>

param(
    [string]$OutputPath = "generated/cli",
    [switch]$SkipIfExists
)

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

function Test-ValidCliOutputFile {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $fileSize = (Get-Item $FilePath).Length
    if ($fileSize -eq 0) {
        Write-Warning "File is empty: $FilePath"
        return $false
    }
    
    try {
        $content = Get-Content $FilePath -Raw | ConvertFrom-Json
        if (-not $content.status) {
            Write-Warning "Missing 'status' field in: $FilePath"
            return $false
        }
        if (-not $content.results) {
            Write-Warning "Missing 'results' field in: $FilePath"
            return $false
        }
        return $true
    } catch {
        Write-Warning "Invalid JSON in: $FilePath - $($_.Exception.Message)"
        return $false
    }
}

function Test-ValidVersionFile {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $fileSize = (Get-Item $FilePath).Length
    if ($fileSize -eq 0) {
        Write-Warning "File is empty: $FilePath"
        return $false
    }
    
    try {
        $content = Get-Content $FilePath -Raw | ConvertFrom-Json
        if (-not $content.version) {
            Write-Warning "Missing 'version' field in: $FilePath"
            return $false
        }
        return $true
    } catch {
        Write-Warning "Invalid JSON in: $FilePath - $($_.Exception.Message)"
        return $false
    }
}

# Main execution
try {
    Write-Progress "Starting Azure MCP CLI Output Generation..."
    
    # Determine test-npm-azure-mcp path
    if ($env:NPM_PROJECT_PATH) { 
        $npmProjectPath = $env:NPM_PROJECT_PATH 
    } else { 
        # Resolve relative to script location, then go up to repo root
        $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
        $repoRoot = Split-Path -Parent $scriptDir
        $npmProjectPath = Join-Path $repoRoot "test-npm-azure-mcp"
    }
    
    # Resolve to absolute path
    $npmProjectPath = (Resolve-Path -LiteralPath $npmProjectPath -ErrorAction SilentlyContinue).ProviderPath
    
    $environment = "npm"
    Write-Info "Environment: $environment"
    Write-Info "NPM Project Path: $npmProjectPath"
    
    # Verify npm project path exists
    if (-not (Test-Path $npmProjectPath)) {
        Write-Error "NPM project path not found: $npmProjectPath"
        Write-Error ""
        Write-Error "Expected location: ../test-npm-azure-mcp"
        exit 1
    }
    
    # Verify package.json exists
    $packageJsonPath = Join-Path $npmProjectPath "package.json"
    if (-not (Test-Path $packageJsonPath)) {
        Write-Error "package.json not found at: $packageJsonPath"
        exit 1
    }
    
    # Resolve OutputPath to absolute path BEFORE changing directories
    $OutputPath = (Resolve-Path -LiteralPath $OutputPath -ErrorAction SilentlyContinue).ProviderPath
    if (-not $OutputPath) {
        # If path doesn't exist yet, resolve the parent directory
        $parentPath = Split-Path -Parent $OutputPath
        if (Test-Path $parentPath) {
            $OutputPath = Join-Path $parentPath (Split-Path -Leaf $OutputPath)
        } else {
            $OutputPath = (Get-Item $PWD).FullName + "/" + $OutputPath
        }
    }
    Write-Info "Resolved output path: $OutputPath"
    
    # Create output directory
    if (-not (Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
        Write-Info "Created output directory: $OutputPath"
    }
    
    # Test write permissions
    $testFile = Join-Path $OutputPath ".write-test"
    try {
        [System.IO.File]::WriteAllText($testFile, "test")
        Remove-Item $testFile -Force -ErrorAction SilentlyContinue
        Write-Info "✓ Write permissions verified"
    } catch {
        Write-Error "❌ Cannot write to output directory: $OutputPath"
        Write-Error "   Check permissions or Docker user mapping"
        exit 1
    }
    
    # Define output files
    $cliOutputFile = Join-Path $OutputPath "cli-output.json"
    $namespaceOutputFile = Join-Path $OutputPath "cli-namespace.json"
    $versionOutputFile = Join-Path $OutputPath "cli-version.json"
    
    # Check if files exist and are valid
    if ($SkipIfExists) {
        Write-Progress "Checking for existing CLI output files..."
        
        $allFilesValid = (Test-ValidCliOutputFile $cliOutputFile) -and `
                         (Test-ValidCliOutputFile $namespaceOutputFile) -and `
                         (Test-ValidVersionFile $versionOutputFile)
        
        if ($allFilesValid) {
            Write-Success "All CLI output files exist and are valid. Skipping generation."
            Write-Info "  ✓ $cliOutputFile"
            Write-Info "  ✓ $namespaceOutputFile"
            Write-Info "  ✓ $versionOutputFile"
            exit 0
        } else {
            Write-Warning "Some CLI output files are missing or invalid. Regenerating..."
        }
    }
    
    # Navigate to npm project directory
    Push-Location $npmProjectPath
    
    try {
        # Ensure dependencies are installed
        Write-Progress "Verifying npm dependencies..."
        if (-not (Test-Path "node_modules")) {
            Write-Progress "Installing npm dependencies..."
            & npm install --silent
            if ($LASTEXITCODE -ne 0) { 
                throw "Failed to install npm dependencies (exit code: $LASTEXITCODE)" 
            }
            Write-Success "Dependencies installed successfully"
        } else {
            Write-Success "Dependencies already installed"
        }
        
        # Capture CLI version
        Write-Progress "Capturing CLI version..."
        $versionOutput = & npm run get:version 2>&1 | Select-Object -First 1
        $cliVersion = "unknown"
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($versionOutput)) {
            $cliVersion = $versionOutput.Trim()
        }
        Write-Info "CLI Version: $cliVersion"
        
        # Get npm package version info
        $npmVersion = "unknown"
        try {
            $packageJson = Get-Content "package.json" -Raw | ConvertFrom-Json
            $npmVersion = $packageJson.dependencies.'@azure/mcp'
        } catch {
            Write-Warning "Could not determine @azure/mcp version: $($_.Exception.Message)"
        }
        
        # Run CLI tools list command (standard mode)
        Write-Progress "Running CLI tools list command..."
        $output = & npm run --silent get:tools-json 2>&1
        if ($LASTEXITCODE -ne 0) { 
            throw "Failed to generate CLI output (exit code: $LASTEXITCODE)" 
        }
        
        # Extract JSON from output (filter out npm warnings/notices)
        $jsonOutput = $output | Where-Object { $_ -and -not $_.StartsWith("npm") -and -not $_.StartsWith(">") } | Join-String -Separator "`n"
        
        # Save JSON output
        $jsonOutput | Out-File -FilePath $cliOutputFile -Encoding UTF8
        
        if (-not (Test-ValidCliOutputFile $cliOutputFile)) {
            throw "Generated cli-output.json is invalid"
        }
        
        $outputSize = [math]::Round((Get-Item $cliOutputFile).Length / 1KB, 1)
        Write-Success "Generated: $cliOutputFile (${outputSize}KB)"
        
        # Run CLI tools list command (namespace mode)
        Write-Progress "Running CLI tools list command (namespace mode)..."
        $output = & npm run --silent get:tools-namespace 2>&1
        if ($LASTEXITCODE -ne 0) { 
            throw "Failed to generate namespace data (exit code: $LASTEXITCODE)" 
        }
        
        # Extract JSON from output (filter out npm warnings/notices)
        $jsonNamespaceOutput = $output | Where-Object { $_ -and -not $_.StartsWith("npm") -and -not $_.StartsWith(">") } | Join-String -Separator "`n"
        
        # Save JSON output
        $jsonNamespaceOutput | Out-File -FilePath $namespaceOutputFile -Encoding UTF8
        
        if (-not (Test-ValidCliOutputFile $namespaceOutputFile)) {
            throw "Generated cli-namespace.json is invalid"
        }
        
        $namespaceSize = [math]::Round((Get-Item $namespaceOutputFile).Length / 1KB, 1)
        Write-Success "Generated: $namespaceOutputFile (${namespaceSize}KB)"
        
        # Create version file with comprehensive metadata
        Write-Progress "Creating version metadata file..."
        $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        
        $versionData = @{
            version = $cliVersion
            timestamp = $timestamp
            npmVersion = $npmVersion
            environment = $environment
            npmProjectPath = $npmProjectPath
        }
        
        $versionData | ConvertTo-Json -Depth 10 | Out-File -FilePath $versionOutputFile -Encoding UTF8
        
        if (-not (Test-ValidVersionFile $versionOutputFile)) {
            throw "Generated cli-version.json is invalid"
        }
        
        $versionSize = [math]::Round((Get-Item $versionOutputFile).Length / 1KB, 1)
        Write-Success "Generated: $versionOutputFile (${versionSize}KB)"
        
    } finally {
        Pop-Location
    }
    
    # Summary
    Write-Success ""
    Write-Success "CLI Output Generation Complete!"
    Write-Info ""
    Write-Info "Generated files:"
    Write-Info "  📄 $cliOutputFile (${outputSize}KB)"
    Write-Info "  📄 $namespaceOutputFile (${namespaceSize}KB)"
    Write-Info "  📄 $versionOutputFile (${versionSize}KB)"
    Write-Info ""
    Write-Info "CLI Version: $cliVersion"
    Write-Info "@azure/mcp Version: $npmVersion"
    Write-Info "Environment: $environment"
    
    exit 0
    
} catch {
    Write-Error "CLI output generation failed: $($_.Exception.Message)"
    Write-Error "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}
