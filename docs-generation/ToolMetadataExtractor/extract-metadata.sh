#!/bin/bash

# Build the project
dotnet build ToolMetadataExtractor/ToolMetadataExtractor.csproj

# Run the tool with sample tools
dotnet run --project ToolMetadataExtractor/ToolMetadataExtractor.csproj -- --tools-file ToolMetadataExtractor/sample-tools.txt --output extracted-metadata.json

# Show success message
echo "Metadata extracted and saved to extracted-metadata.json"