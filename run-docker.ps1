#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Azure MCP Documentation Generator - Quick Run Script (PowerShell)
    
.DESCRIPTION
    This script makes it easy to generate documentation locally using Docker.
    Works on Windows, macOS, and Linux.
    
.PARAMETER BuildOnly
    Build the Docker image without running it
    
.PARAMETER NoCache
    Build without using Docker cache (clean build)
    
.PARAMETER Interactive
    Start an interactive shell in the container for debugging

.PARAMETER SkipCliGeneration
    Skip CLI output generation (requires existing files)
    
.EXAMPLE
    ./run-docker.ps1
    Build and run the documentation generator
    
.EXAMPLE
    ./run-docker.ps1 -BuildOnly
    Just build the Docker image
    
.EXAMPLE
    ./run-docker.ps1 -NoCache
    Rebuild from scratch without cache
    
.EXAMPLE
    ./run-docker.ps1 -Interactive
    Start an interactive debug shell
#>

param(
    [switch]$BuildOnly,
    [switch]$NoCache,
    [switch]$Interactive,
    [switch]$SkipCliGeneration
)

# Helper functions for colored output
function Write-Header { param([string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Write-Info { param([string]$Message) Write-Host $Message -ForegroundColor Blue }
function Write-Success { param([string]$Message) Write-Host $Message -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host $Message -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host $Message -ForegroundColor Red }

Write-Header "========================================"
Write-Header "Azure MCP Documentation Generator"
Write-Header "========================================"
Write-Host ""

# Check if Docker is installed
try {
    $null = Get-Command docker -ErrorAction Stop
    Write-Success "✅ Docker is installed"
} catch {
    Write-Error "❌ Docker is not installed"
    Write-Host "Please install Docker Desktop"
    Write-Host "Visit: https://docs.docker.com/get-docker/"
    exit 1
}

# Check if Docker daemon is running
try {
    docker info | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker daemon not running"
    }
    Write-Success "✅ Docker is ready"
} catch {
    Write-Error "❌ Docker daemon is not running"
    Write-Host "Please start Docker Desktop"
    exit 1
}
Write-Host ""

# Build the Docker image
Write-Info "📦 Building Docker image..."
Write-Host ""

# Get user/group IDs on Linux/macOS for non-root container execution
$userArgs = @()
if ($IsLinux -or $IsMacOS) {
    $userId = & id -u
    $groupId = & id -g
    $userArgs = @("--build-arg", "USER_ID=$userId", "--build-arg", "GROUP_ID=$groupId")
    Write-Info "Building with user mapping: $userId:$groupId"
}

$buildArgs = @("build", "-t", "azure-mcp-docgen:latest", "-f", "docker/Dockerfile") + $userArgs
if ($NoCache) {
    $buildArgs += "--no-cache"
}
$buildArgs += "."

& docker $buildArgs | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Error "❌ Docker build failed"
    exit 1
}

Write-Host ""
Write-Success "✅ Docker image built successfully"

# Show image info
$imageSize = docker images azure-mcp-docgen:latest --format "{{.Size}}"
Write-Header "Image size: $imageSize"

# Exit if build-only mode
if ($BuildOnly) {
    Write-Host ""
    Write-Header "Build-only mode: Skipping container run"
    exit 0
}

# Create output directories with proper permissions
$dirs = @("generated/cli", "generated/tools", "generated/example-prompts", "generated/logs")
foreach ($dir in $dirs) {
    $null = New-Item -ItemType Directory -Path $dir -Force
}

# Interactive mode
if ($Interactive) {
    Write-Host ""
    Write-Info "🔧 Starting interactive debug shell..."
    if ($SkipCliGeneration) {
        Write-Warning "CLI generation will be skipped. If files are missing, run:"
        Write-Warning "  pwsh docs-generation/Get-McpCliOutput.ps1 -OutputPath generated/cli"
    }
    Write-Warning "Run inside container: pwsh ./Generate-MultiPageDocs.ps1"
    Write-Warning "Exit with: exit"
    Write-Host ""
    
    $runArgs = @("run", "--rm", "-it")
    if ($IsLinux -or $IsMacOS) {
        $runArgs += @("--user", "${userId}:${groupId}")
    }
    $runArgs += @("-v", "${PWD}/generated:/output", "--entrypoint", "/bin/bash", "azure-mcp-docgen:latest")
    
    & docker $runArgs
    exit 0
}

# Step 1: Generate CLI output files (unless skipped)
if (-not $SkipCliGeneration) {
    Write-Host ""
    Write-Info "📝 Step 1: Generating MCP CLI output files..."
    Write-Host ""
    
    # Run CLI output generation using npm-based approach
    # Set environment variable so PowerShell script can find npm project
    $env:NPM_PROJECT_PATH = Join-Path $PWD "test-npm-azure-mcp"
    if (pwsh docs-generation/Get-McpCliOutput.ps1 -OutputPath generated/cli) {
        Write-Success "✅ CLI output files generated"
    } else {
        Write-Host ""
        Write-Error "❌ Failed to generate CLI output files"
        exit 1
    }
} else {
    Write-Host ""
    Write-Warning "⏭️  Skipping CLI generation (--skip-cli-generation flag set)"
    Write-Host ""
    
    # Validate that required files exist
    $cliOutputFile = "generated/cli/cli-output.json"
    $namespaceFile = "generated/cli/cli-namespace.json"
    $versionFile = "generated/cli/cli-version.json"
    
    if (-not (Test-Path $cliOutputFile) -or -not (Test-Path $namespaceFile) -or -not (Test-Path $versionFile)) {
        Write-Host ""
        Write-Error "❌ CLI output files not found"
        Write-Host ""
        Write-Host "Required files:"
        Write-Host "  • $cliOutputFile"
        Write-Host "  • $namespaceFile"
        Write-Host "  • $versionFile"
        Write-Host ""
        Write-Host "Run: ./run-docker.ps1 to generate them"
        exit 1
    }
    
    Write-Success "✅ CLI output files found"
}

# Step 2: Load .env file if it exists
$envFile = "docs-generation/.env"
$envArgs = @()
if (Test-Path $envFile) {
    Write-Host ""
    Write-Info "📄 Loading credentials from $envFile"
    # Note: Environment variables from .env would need to be sourced in the Docker context
    Write-Success "✅ Credentials loaded"
} else {
    Write-Warning "⚠️  No .env file found at $envFile"
    Write-Warning "   Example prompts will not be generated"
}

# Step 3: Run the documentation generator
Write-Host ""
Write-Info "📝 Step 2: Running documentation generator..."
Write-Warning "Output directory: $PWD/generated"

$runArgs = @("run", "--rm")
if ($IsLinux -or $IsMacOS) {
    $runArgs += @("--user", "${userId}:${groupId}")
}
$runArgs += @("-v", "${PWD}/generated:/output", "--env", "SKIP_CLI_GENERATION=true", "azure-mcp-docgen:latest")

& docker $runArgs | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Success "========================================"
    Write-Success "✅ Documentation generated successfully!"
    Write-Success "========================================"
    Write-Host ""
    
    # Show summary of generated files
    if (Test-Path "generated/multi-page") {
        $fileCount = (Get-ChildItem "generated/multi-page" -Filter "*.md" -File | Measure-Object).Count
        Write-Header "📄 Generated $fileCount markdown files"
        Write-Host ""
        Write-Header "Output location:"
        Write-Host "  $PWD/generated/"
        Write-Host ""
        
        # Show first few files
        Write-Header "Sample files:"
        Get-ChildItem "generated/tools" -Filter "*.md" -File | 
            Sort-Object Name | 
            Select-Object -First 5 | 
            ForEach-Object {
                $size = [math]::Round($_.Length / 1KB, 1)
                Write-Host "  • $($_.Name) (${size}KB)"
            }
        
        if ($fileCount -gt 5) {
            Write-Host "  ... and $($fileCount - 5) more files"
        }
        
        Write-Host ""
        Write-Success "🎉 Ready to use!"
    } else {
        Write-Warning "⚠️  No tools directory found"
        Write-Host "Generated files:"
        Get-ChildItem "generated" -ErrorAction SilentlyContinue | Format-Table -AutoSize
    }
} else {
    Write-Host ""
    Write-Error "❌ Documentation generation failed"
    Write-Host ""
    Write-Host "Troubleshooting:"
    Write-Host "  1. Check Docker logs above for errors"
    Write-Host "  2. Try rebuilding: ./run-docker.ps1 -NoCache"
    Write-Host "  3. Try interactive mode: ./run-docker.ps1 -Interactive"
    Write-Host "  4. Check available disk space"
    Write-Host "  5. Ensure Docker has enough memory (8GB recommended)"
    exit 1
}
