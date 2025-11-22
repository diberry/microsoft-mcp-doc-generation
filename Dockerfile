# Multi-stage Dockerfile for Azure MCP Documentation Generator
# This container packages both the MCP server and the documentation generation tools

# Stage 1: Build MCP Server
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS mcp-builder

# Install git and wget for cloning and downloading .NET 10 preview
RUN apt-get update && \
    apt-get install -y git wget && \
    rm -rf /var/lib/apt/lists/*

# Install .NET 10.0 preview SDK (required by MCP global.json)
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 --quality preview --install-dir /usr/share/dotnet && \
    rm dotnet-install.sh

# Clone Microsoft/MCP repository
WORKDIR /build/mcp
ARG MCP_BRANCH=main
RUN git clone --depth 1 --branch ${MCP_BRANCH} https://github.com/Microsoft/MCP.git .

# Build the Azure MCP Server
WORKDIR /build/mcp/servers/Azure.Mcp.Server/src
RUN dotnet build --configuration Release

# Stage 2: Build Documentation Generator
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS docs-builder

# Copy docs-generation source
WORKDIR /build/docs-generation
COPY docs-generation/ .

# Build the documentation generation solution
RUN dotnet restore docs-generation.sln && \
    dotnet build docs-generation.sln --configuration Release --no-restore

# Stage 3: Runtime Environment
FROM mcr.microsoft.com/dotnet/sdk:9.0

# Install PowerShell and wget for .NET 10 preview
# Using direct PowerShell installation method that works on Debian
RUN apt-get update && \
    apt-get install -y wget apt-transport-https software-properties-common && \
    # Download and install PowerShell for Debian 12
    wget https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/powershell_7.4.6-1.deb_amd64.deb && \
    dpkg -i powershell_7.4.6-1.deb_amd64.deb || apt-get install -f -y && \
    rm powershell_7.4.6-1.deb_amd64.deb && \
    rm -rf /var/lib/apt/lists/*

# Install .NET 10.0 preview SDK (required by MCP)
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 --quality preview --install-dir /usr/share/dotnet && \
    rm dotnet-install.sh

# Copy built MCP server from builder stage
COPY --from=mcp-builder /build/mcp /mcp

# Copy built docs-generation from builder stage
COPY --from=docs-builder /build/docs-generation /docs-generation

# Set working directory
WORKDIR /docs-generation

# Set environment variables
ENV DOTNET_ROLL_FORWARD=Major
ENV MCP_SERVER_PATH=/mcp/servers/Azure.Mcp.Server/src

# Create output directory
RUN mkdir -p /output

# Volume for generated documentation
VOLUME ["/output"]

# Default command: Generate documentation and copy to /output
CMD ["pwsh", "-Command", "\
    Write-Host '=== Azure MCP Documentation Generator ===' -ForegroundColor Cyan; \
    Write-Host \"MCP Server Path: $env:MCP_SERVER_PATH\" -ForegroundColor Yellow; \
    Write-Host 'Output Path: /output' -ForegroundColor Yellow; \
    Write-Host ''; \
    Write-Host 'Starting documentation generation...' -ForegroundColor Green; \
    Push-Location \"$env:MCP_SERVER_PATH\"; \
    dotnet build --configuration Release --nologo --verbosity quiet; \
    Pop-Location; \
    ./Generate-MultiPageDocs.ps1 -ExamplePrompts 1; \
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
