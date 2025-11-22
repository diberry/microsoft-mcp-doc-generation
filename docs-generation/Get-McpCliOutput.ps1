#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate MCP CLI output files for documentation generation
    
.DESCRIPTION
    This script builds the Azure MCP Server and runs CLI commands to generate
    three output files: cli-output.json, cli-namespace.json, and cli-version.json
    
    This script is designed to work in both containerized and local environments.
    
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
    
    # Determine environment (container vs local)
    $mcpServerPath = if ($env:MCP_SERVER_PATH) { 
        $env:MCP_SERVER_PATH 
    } else { 
        "..\servers\Azure.Mcp.Server\src" 
    }
    
    $environment = if ($env:MCP_SERVER_PATH) { "docker" } else { "local" }
    Write-Info "Environment: $environment"
    Write-Info "MCP Server Path: $mcpServerPath"
    
    # Verify MCP server path exists
    if (-not (Test-Path $mcpServerPath)) {
        Write-Error "MCP server path not found: $mcpServerPath"
        Write-Error ""
        Write-Error "For local execution, please clone Microsoft/MCP repository:"
        Write-Error "  git clone https://github.com/Microsoft/MCP.git ../servers"
        Write-Error ""
        Write-Error "Expected location: ../servers/Azure.Mcp.Server/src"
        exit 1
    }
    
    # Create output directory
    if (-not (Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
        Write-Info "Created output directory: $OutputPath"
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
    
    # Navigate to MCP server directory
    Push-Location $mcpServerPath
    
    try {
        # Build the MCP Server
        Write-Progress "Building Azure MCP Server..."
        & dotnet build --configuration Release --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) { 
            throw "Failed to build Azure MCP Server (exit code: $LASTEXITCODE)" 
        }
        Write-Success "MCP Server built successfully"
        
        # Capture CLI version
        Write-Progress "Capturing CLI version..."
        $versionOutput = & dotnet run --no-build --configuration Release -- --version 2>&1
        $cliVersion = "unknown"
        if ($LASTEXITCODE -eq 0) {
            $cliVersion = ($versionOutput | Where-Object { $_ -match '^\d+\.\d+\.\d+' } | Select-Object -First 1).Trim()
            if ([string]::IsNullOrWhiteSpace($cliVersion)) {
                $cliVersion = "unknown"
            }
        }
        Write-Info "CLI Version: $cliVersion"
        
        # Get MCP branch from git (if available)
        $mcpBranch = "unknown"
        try {
            $branchOutput = & git rev-parse --abbrev-ref HEAD 2>&1
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($branchOutput)) {
                $mcpBranch = $branchOutput.Trim()
            }
        } catch {
            Write-Warning "Could not determine MCP branch: $($_.Exception.Message)"
        }
        
        # Run CLI tools list command (standard mode)
        Write-Progress "Running CLI tools list command..."
        $rawOutput = & dotnet run --no-build --configuration Release -- tools list
        if ($LASTEXITCODE -ne 0) { 
            throw "Failed to generate CLI output (exit code: $LASTEXITCODE)" 
        }
        
        # Filter out non-JSON content and save
        $jsonOutput = $rawOutput | Where-Object { $_ -match '^\s*[\{\[]' -or $_ -notmatch '^Using launch settings' }
        $jsonOutput | Out-File -FilePath $cliOutputFile -Encoding UTF8
        
        if (-not (Test-ValidCliOutputFile $cliOutputFile)) {
            throw "Generated cli-output.json is invalid"
        }
        
        $outputSize = [math]::Round((Get-Item $cliOutputFile).Length / 1KB, 1)
        Write-Success "Generated: $cliOutputFile (${outputSize}KB)"
        
        # Run CLI tools list command (namespace mode)
        Write-Progress "Running CLI tools list command (namespace mode)..."
        $namespaceRawOutput = & dotnet run --no-build --configuration Release -- tools list --namespace-mode
        if ($LASTEXITCODE -ne 0) { 
            throw "Failed to generate namespace data (exit code: $LASTEXITCODE)" 
        }
        
        # Filter out non-JSON content and save
        $jsonNamespaceOutput = $namespaceRawOutput | Where-Object { $_ -match '^\s*[\{\[]' -or $_ -notmatch '^Using launch settings' }
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
            mcpBranch = $mcpBranch
            environment = $environment
            mcpServerPath = $mcpServerPath
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
    Write-Info "MCP Branch: $mcpBranch"
    Write-Info "Environment: $environment"
    
    exit 0
    
} catch {
    Write-Error "CLI output generation failed: $($_.Exception.Message)"
    Write-Error "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}
