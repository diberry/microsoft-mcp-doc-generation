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
    
.PARAMETER Branch
    Specify which Microsoft/MCP branch to use (default: main)
    
.PARAMETER Interactive
    Start an interactive shell in the container for debugging
    
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
    ./run-docker.ps1 -Branch feature-branch
    Use a specific MCP branch
    
.EXAMPLE
    ./run-docker.ps1 -Interactive
    Start an interactive debug shell
#>

param(
    [switch]$BuildOnly,
    [switch]$NoCache,
    [string]$Branch = "main",
    [switch]$Interactive
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
Write-Warning "MCP Branch: $Branch"
Write-Host ""

$buildArgs = @("build", "--build-arg", "MCP_BRANCH=$Branch", "-t", "azure-mcp-docgen:latest")
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

# Create output directory
$null = New-Item -ItemType Directory -Path "generated" -Force

# Interactive mode
if ($Interactive) {
    Write-Host ""
    Write-Info "🔧 Starting interactive debug shell..."
    Write-Warning "Run inside container: pwsh ./Generate-MultiPageDocs.ps1"
    Write-Warning "Exit with: exit"
    Write-Host ""
    
    docker run --rm -it `
        -v "${PWD}/generated:/output" `
        --entrypoint /bin/bash `
        azure-mcp-docgen:latest
    exit 0
}

# Run the documentation generator
Write-Host ""
Write-Info "📝 Running documentation generator..."
Write-Warning "Output directory: $PWD/generated"
Write-Host ""

docker run --rm `
    -v "${PWD}/generated:/output" `
    azure-mcp-docgen:latest | Out-Null

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
        Get-ChildItem "generated/multi-page" -Filter "*.md" -File | 
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
        Write-Warning "⚠️  No multi-page directory found"
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
