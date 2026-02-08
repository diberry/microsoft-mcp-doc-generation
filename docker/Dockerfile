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
ENV DOTNET_ROLL_FORWARD=Major \
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFICATION_SCHEDULE=never \
    DOTNET_SKIP_WORKLOAD_VERIFICATION=1 \
    DOTNET_NOLOGO=1

# Preemptively update workloads to avoid verification notices
RUN dotnet --info && dotnet workload update || true

# Create output directory
RUN mkdir -p /output

# Add non-root user support
ARG USER_ID=1000
ARG GROUP_ID=1000
RUN groupadd -g ${GROUP_ID} vscode || true && \
    useradd -m -u ${USER_ID} -g ${GROUP_ID} vscode || true && \
    chown -R vscode:vscode /docs-generation /output

# Volume for generated documentation (expects CLI files in /output/cli)
VOLUME ["/output"]

# Switch to non-root user
USER vscode

# Default command: Generate documentation from existing CLI output
CMD ["pwsh", "-Command", \
    "Write-Host '=== Azure MCP Documentation Generator ===' -ForegroundColor Cyan; \
    Write-Host \"Running as user: $(id -u):$(id -g)\" -ForegroundColor Yellow; \
    Write-Host 'Output Path: /output' -ForegroundColor Yellow; \
    Write-Host ''; \
    if (-not (Test-Path '/output/cli/cli-output.json')) { \
        Write-Host '❌ CLI output files not found in /output/cli/' -ForegroundColor Red; \
        Write-Host 'Please mount CLI output files generated from Dockerfile.mcp-cli-output' -ForegroundColor Yellow; \
        exit 1; \
    }; \
    Write-Host 'Starting documentation generation...' -ForegroundColor Green; \
    ./Generate.ps1 -ExamplePrompts $true; \
    Write-Host ''; \
    if (Test-Path '/output/tools') { \
        Write-Host '✅ Documentation generated successfully!' -ForegroundColor Green; \
        Write-Host 'Files available in mounted volume at /output' -ForegroundColor Cyan; \
    } else { \
        Write-Host '❌ Generation failed - no output directory found at /output/tools' -ForegroundColor Red; \
        exit 1; \
    }"]

# Labels for container metadata
LABEL maintainer="Azure MCP Documentation Team"
LABEL description="Automated documentation generator for Azure MCP tools"
LABEL version="1.0"
LABEL org.opencontainers.image.source="https://github.com/diberry/microsoft-mcp-doc-generation"
