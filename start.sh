# Clean up last run
rm -rf generated/

# Create output directories
mkdir -p generated/
mkdir -p generated/cli

# # Parse command line arguments
# VALIDATE_PROMPTS=""
# while [[ $# -gt 0 ]]; do
#     case $1 in
#         --validate)
#             VALIDATE_PROMPTS="--validate-prompts"
#             echo "✓ Example prompt validation enabled (will run after content generation)"
#             shift
#             ;;
#         *)
#             echo "Unknown option: $1"
#             echo "Usage: $0 [--validate]"
#             exit 1
#             ;;
#     esac
# done

# Generate tool metadata from MCP CLI
cd test-npm-azure-mcp
npm install
npm run --silent get:version > ../generated/cli/cli-version.json
npm run --silent get:tools-json > ../generated/cli/cli-output.json
npm run --silent get:tools-namespace > ../generated/cli/cli-namespace.json
cd ..

mkdir -p generated/common-general
mkdir -p generated/param-and-annotation
mkdir -p generated/tools
mkdir -p generated/example-prompts
mkdir -p generated/annotations
mkdir -p generated/logs

## Generate docs using C# generator
cd docs-generation 
dotnet restore docs-generation.sln
dotnet build docs-generation.sln --configuration Release --no-restore
dotnet run --project CSharpGenerator/CSharpGenerator.csproj --configuration Release -- \
    generate-docs \
    ../generated/cli/cli-output.json \
    ../generated/tools \
    --annotations \
    --example-prompts \
    --param-and-annotation \
    --common-general \
    --complete-tools \
    --validate --validate-prompts
cd ..

# # Summarize generated docs
# cd summary-generator
# npm install
# node generate-summary.js
# cd ..
