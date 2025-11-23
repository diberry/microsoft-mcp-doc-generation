# Multi-stage Dockerfile for Azure MCP Documentation Generator
# This container only builds the documentation generation tools
# It expects CLI output files to be provided via volume mount

# Stage 1: Build Documentation Generator
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS docs-builder

# Copy docs-generation source
WORKDIR /build/docs-generation
COPY docs-generation/ .

# Build the documentation generation solution
RUN dotnet restore docs-generation.sln && \
    dotnet build docs-generation.sln --configuration Release --no-restore

# Stage 2: Runtime Environment
FROM mcr.microsoft.com/dotnet/sdk:9.0

# Install PowerShell
# Using direct PowerShell installation method that works on Debian
RUN apt-get update && \
    apt-get install -y wget apt-transport-https software-properties-common && \
    # Download and install PowerShell for Debian 12
    wget https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/powershell_7.4.6-1.deb_amd64.deb && \
    dpkg -i powershell_7.4.6-1.deb_amd64.deb || apt-get install -f -y && \
    rm powershell_7.4.6-1.deb_amd64.deb && \
    rm -rf /var/lib/apt/lists/*

# Copy built docs-generation from builder stage
COPY --from=docs-builder /build/docs-generation /docs-generation

# Copy root-level scripts
COPY run-generative-ai-output.sh /run-generative-ai-output.sh
RUN chmod +x /run-generative-ai-output.sh

# Set working directory
WORKDIR /docs-generation

# Set environment variables
ENV DOTNET_ROLL_FORWARD=Major

# Create output directory
RUN mkdir -p /output

# Volume for generated documentation (expects CLI files in /output/cli)
VOLUME ["/output"]

# Default command: Generate documentation from existing CLI output
CMD ["pwsh", "-Command", \
    "Write-Host '=== Azure MCP Documentation Generator ===' -ForegroundColor Cyan; \
    Write-Host 'Output Path: /output' -ForegroundColor Yellow; \
    Write-Host ''; \
    if (-not (Test-Path '/output/cli/cli-output.json')) { \
        Write-Host '❌ CLI output files not found in /output/cli/' -ForegroundColor Red; \
        Write-Host 'Please mount CLI output files generated from Dockerfile.mcp-cli-output' -ForegroundColor Yellow; \
        exit 1; \
    }; \
    Write-Host 'Starting documentation generation...' -ForegroundColor Green; \
    ./Generate-MultiPageDocs.ps1; \
    Write-Host ''; \
    Write-Host 'Copying generated documentation to /output...' -ForegroundColor Green; \
    if (Test-Path 'generated') { \
        Copy-Item -Path 'generated/*' -Destination '/output/' -Recurse -Force; \
        Write-Host '✅ Documentation generated successfully!' -ForegroundColor Green; \
        Write-Host 'Files available in mounted volume at /output' -ForegroundColor Cyan; \
    } else { \
        Write-Host '❌ Generation failed - no output directory found' -ForegroundColor Red; \
        exit 1; \
    }"]

# Labels for container metadata
LABEL maintainer="Azure MCP Documentation Team"
LABEL description="Automated documentation generator for Azure MCP tools"
LABEL version="1.0"
LABEL org.opencontainers.image.source="https://github.com/diberry/microsoft-mcp-doc-generation"
