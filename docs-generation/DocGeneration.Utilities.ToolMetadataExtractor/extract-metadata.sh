#!/bin/bash

# Build the project
dotnet build DocGeneration.Utilities.ToolMetadataExtractor/DocGeneration.Utilities.ToolMetadataExtractor.csproj

# Run the tool with sample tools
dotnet run --project DocGeneration.Utilities.ToolMetadataExtractor/DocGeneration.Utilities.ToolMetadataExtractor.csproj -- --tools-file DocGeneration.Utilities.ToolMetadataExtractor/sample-tools.txt --output extracted-metadata.json

# Show success message
echo "Metadata extracted and saved to extracted-metadata.json"