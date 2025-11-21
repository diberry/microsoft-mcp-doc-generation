# Azure MCP CLI Container Helper Script (PowerShell)
# Provides easy access to Azure MCP Server CLI via Docker

[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$NoCache,
    [string]$Branch = "main",
    [switch]$Shell,
    [switch]$Help,
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$CommandArgs
)

$ErrorActionPreference = "Stop"

$IMAGE_NAME = "azure-mcp-cli:latest"
$MCP_BRANCH = if ($env:MCP_BRANCH) { $env:MCP_BRANCH } else { $Branch }

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Show-Usage {
    Write-Host ""
    Write-ColorOutput "═══════════════════════════════════════════════════════════" -Color Cyan
    Write-ColorOutput "  Azure MCP CLI Container Helper - WRAPPER SCRIPT HELP" -Color Cyan
    Write-ColorOutput "═══════════════════════════════════════════════════════════" -Color Cyan
    Write-Host ""
    
    Write-ColorOutput "Usage:" -Color Yellow
    Write-Host "  .\run-mcp-cli.ps1 [WRAPPER_OPTIONS] [--] [MCP_COMMAND] [MCP_ARGS...]`n"
    
    Write-ColorOutput "Wrapper Options (for this script only):" -Color Yellow
    Write-Host "  -Build              Build the Docker image first"
    Write-Host "  -NoCache            Build without using cache"
    Write-Host "  -Branch <name>      Use specific MCP branch (default: main)"
    Write-Host "  -Shell              Open interactive shell in container"
    Write-Host "  -Help               Show this wrapper script help`n"
    
    Write-ColorOutput "Separator:" -Color Yellow
    Write-Host "  --                  Pass all remaining arguments directly to MCP CLI`n"
    
    Write-ColorOutput "Examples:" -Color Yellow
    Write-ColorOutput "  # Show this wrapper script help (wrapper only)" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 -Help`n"
    
    Write-ColorOutput "  # Show MCP CLI help (calls MCP CLI in Docker)" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 -- --help`n"
    
    Write-ColorOutput "  # List all MCP tools (calls: azmcp tools list)" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 tools list`n"
    
    Write-ColorOutput "  # List just tool names (calls: azmcp tools list --name-only)" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 tools list --name-only`n"
    
    Write-ColorOutput "  # List tool namespaces (calls: azmcp tools list --namespace-mode)" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 tools list --namespace-mode`n"
    
    Write-ColorOutput "  # Get MCP CLI version (calls: azmcp --version)" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 -- --version`n"
    
    Write-ColorOutput "  # Wrapper option + MCP command: build image, then list tools" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 -Build -- tools list`n"
    
    Write-ColorOutput "  # Wrapper option: use different MCP git branch" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 -Branch feature-branch -- tools list`n"
    
    Write-ColorOutput "  # Wrapper option: open shell inside container (no MCP command)" -Color Green
    Write-Host "  .\run-mcp-cli.ps1 -Shell`n"
    
    Write-ColorOutput "Common MCP CLI Commands (all run inside Docker):" -Color Yellow
    Write-Host "  tools list                      List all MCP tools (full JSON)"
    Write-Host "  tools list --name-only          List just tool names (concise)"
    Write-Host "  tools list --namespace-mode     List service namespaces"
    Write-Host "  --help                          Show MCP CLI help"
    Write-Host "  --version                       Show MCP CLI version`n"
    
    Write-ColorOutput "What Runs Where?" -Color Yellow
    Write-Host "  Wrapper only:     -Help, -Build, -NoCache, -Branch, -Shell"
    Write-Host "  Inside Docker:    Everything else (passed to: azmcp [command])`n"
    
    Write-ColorOutput "Tip:" -Color Yellow
    Write-Host "  Use -- to clearly separate wrapper options from MCP CLI arguments`n"
}

function Build-Image {
    param([switch]$UseNoCache)
    
    $noCacheFlag = if ($UseNoCache) { "--no-cache" } else { "" }
    
    Write-ColorOutput "Building Azure MCP CLI container..." -Color Cyan
    
    $buildArgs = @(
        "build"
        $noCacheFlag
        "--build-arg", "MCP_BRANCH=$MCP_BRANCH"
        "-t", $IMAGE_NAME
        "-f", "Dockerfile.cli"
        "."
    ) | Where-Object { $_ -ne "" }
    
    & docker $buildArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "✅ Docker image built successfully" -Color Green
        docker images $IMAGE_NAME
    } else {
        Write-ColorOutput "❌ Docker build failed" -Color Red
        exit 1
    }
}

function Start-Shell {
    Write-ColorOutput "Opening interactive shell in Azure MCP CLI container..." -Color Cyan
    Write-ColorOutput "MCP Server is located at: /mcp/servers/Azure.Mcp.Server/src" -Color Yellow
    Write-ColorOutput "Run 'dotnet run -- [command]' to execute MCP CLI`n" -Color Yellow
    
    docker run `
        --rm `
        -it `
        --entrypoint /bin/bash `
        $IMAGE_NAME
}

function Invoke-Command {
    param([string[]]$Arguments)
    
    # Check if image exists
    $null = docker image inspect $IMAGE_NAME 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "Image not found. Building..." -Color Yellow
        Build-Image
    }
    
    # Show header for MCP CLI output
    Write-ColorOutput "═══════════════════════════════════════════════════════════" -Color Cyan
    Write-ColorOutput "  MCP CLI OUTPUT" -Color Cyan
    Write-ColorOutput "═══════════════════════════════════════════════════════════`n" -Color Cyan
    
    # Run the MCP CLI with provided arguments
    docker run `
        --rm `
        $IMAGE_NAME `
        $Arguments
}

# Main execution
try {
    # Handle help flag separately
    if ($Help -or ($CommandArgs -and $CommandArgs[0] -eq "-h")) {
        Show-Usage
        exit 0
    }
    
    # Build if requested
    if ($Build) {
        Build-Image -UseNoCache:$NoCache
    }
    
    # Open shell if requested
    if ($Shell) {
        Start-Shell
        exit 0
    }
    
    # Run command if arguments provided
    if ($CommandArgs -and $CommandArgs.Count -gt 0) {
        Invoke-Command -Arguments $CommandArgs
    } else {
        # No arguments, show help
        Show-Usage
    }
}
catch {
    Write-ColorOutput "❌ Error: $_" -Color Red
    exit 1
}
