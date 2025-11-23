#!/bin/bash

# Default parameters
PROJECT_PATH="/workspaces/new-mcp"
OUTPUT_FILE="tool-metadata.json"
SKIP_EXTRACTION=false
SKIP_TOOL_LIST=false
TOOL_LIST_FILE="tool-list.txt"

# Parse command-line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --project-path)
      PROJECT_PATH="$2"
      shift 2
      ;;
    --output-file)
      OUTPUT_FILE="$2"
      shift 2
      ;;
    --skip-extraction)
      SKIP_EXTRACTION=true
      shift
      ;;
    --skip-tool-list)
      SKIP_TOOL_LIST=true
      shift
      ;;
    --tool-list-file)
      TOOL_LIST_FILE="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

# Set working directory to the project root
cd "$PROJECT_PATH" || exit 1

# Configure paths
SERVER_PROJECT="servers/Azure.Mcp.Server/src/Azure.Mcp.Server.csproj"
EXTRACTOR_PROJECT="docs-generation/ToolMetadataExtractor/ToolMetadataExtractor.csproj"
TOOL_LIST_PATH="$PROJECT_PATH/$TOOL_LIST_FILE"

# Step 1: Get tool list from MCP CLI if not skipping
if [ "$SKIP_TOOL_LIST" = false ]; then
  echo "Getting tool list from MCP CLI..."
  
  # Run the tools list command and save to a temporary file
  TEMP_JSON=$(mktemp)
  dotnet run --project "$SERVER_PROJECT" -- tools list > "$TEMP_JSON"
  
  if [ $? -ne 0 ]; then
    echo "Failed to get tool list from MCP CLI"
    rm "$TEMP_JSON"
    exit 1
  fi
  
  # Process the JSON and extract command paths
  # Remove "azmcp " prefix from each command
  jq -r '.results[] | select(.command != null) | .command | select(startswith("azmcp ")) | sub("azmcp ";"")' "$TEMP_JSON" > "$TOOL_LIST_PATH"
  
  TOOL_COUNT=$(wc -l < "$TOOL_LIST_PATH")
  echo "Found $TOOL_COUNT tools, saved to $TOOL_LIST_PATH"
  rm "$TEMP_JSON"
else
  echo "Skipping tool list extraction, using existing file: $TOOL_LIST_PATH"
  if [ ! -f "$TOOL_LIST_PATH" ]; then
    echo "Tool list file does not exist: $TOOL_LIST_PATH"
    exit 1
  fi
fi

# Step 2: Run the extractor if not skipping
if [ "$SKIP_EXTRACTION" = false ]; then
  echo "Running metadata extractor..."
  
  # Build the project first
  dotnet build "$EXTRACTOR_PROJECT"
  if [ $? -ne 0 ]; then
    echo "Failed to build the extractor project"
    exit 1
  fi
  
  # Run the extractor with the tool list file
  OUTPUT_PATH="$PROJECT_PATH/$OUTPUT_FILE"
  dotnet run --project "$EXTRACTOR_PROJECT" --no-build -- --tools-file "$TOOL_LIST_PATH" --output "$OUTPUT_PATH"
  
  if [ $? -ne 0 ]; then
    echo "Metadata extraction failed"
    exit 1
  fi
  
  echo "Metadata extraction complete, results saved to $OUTPUT_PATH"
else
  echo "Skipping metadata extraction"
fi

echo "Script execution complete!"