# Namespace-Specific Output Directory Feature - Implementation Summary

## Overview

This feature modifies the documentation generation pipeline to use namespace-specific output directories when generating documentation for a single namespace.

## Behavior

| Usage Pattern | Command | Output Directory |
|--------------|---------|-----------------|
| All namespaces | `./start.sh` | `./generated/` |
| All namespaces (specific steps) | `./start.sh 1,2,3` | `./generated/` |
| Single namespace | `./start.sh advisor` | `./generated-advisor/` |
| Single namespace (specific steps) | `./start.sh advisor 1,2,3` | `./generated-advisor/` |

## Implementation Details

### Files Modified

1. **start.sh** (Root orchestrator)
   - Added `OUTPUT_DIR` variable that switches between `generated` and `generated-<namespace>`
   - Passes `OUTPUT_DIR` to preflight.ps1 and start-only.sh
   - Updated output messages to show dynamic directory
   - Updated header documentation to explain the feature

2. **docs-generation/scripts/start-only.sh** (Worker script)
   - Added third parameter `OUTPUT_DIR` (defaults to `$ROOT_DIR/generated`)
   - Uses `OUTPUT_DIR` for all CLI metadata checks
   - Creates output subdirectories under `OUTPUT_DIR`
   - Passes `OUTPUT_DIR` to generate-tool-family.sh

3. **docs-generation/scripts/generate-tool-family.sh** (Bash wrapper)
   - Added third parameter `OUTPUT_DIR` (optional)
   - Conditionally passes `-OutputPath` to PowerShell script when `OUTPUT_DIR` is set
   - Maintains backward compatibility when no output directory is specified

4. **README.md**
   - Added note about namespace-specific output directories in Quick Start section
   - Updated Critical Outputs section to mention both output locations
   - Added examples showing the output directory in command comments

### Files Added

1. **test-output-directory.sh**
   - Integration test suite with 4 test cases
   - Verifies OUTPUT_DIR logic in all modified scripts
   - Checks parameter passing through the chain
   - Validates output messages use dynamic paths

2. **demo-output-directory-feature.sh**
   - Demonstration script showing feature usage and benefits
   - Examples of different command patterns
   - Explains the value proposition

## Testing

All integration tests pass:

```bash
$ bash test-output-directory.sh
✓ PASS: start.sh contains OUTPUT_DIR logic with namespace suffix
✓ PASS: start-only.sh accepts OUTPUT_DIR as third parameter
✓ PASS: generate-tool-family.sh accepts and uses OUTPUT_DIR
✓ PASS: start.sh output message uses dynamic OUTPUT_DIR
```

## Benefits

1. **Isolation**: Single-namespace runs don't interfere with full catalog generation
2. **Clarity**: Output directory name clearly indicates which namespace was generated
3. **Safety**: Prevents accidental overwrites when working on specific services
4. **Workflow**: Enables parallel development on different namespaces
5. **Backward Compatible**: Default behavior unchanged when no namespace specified

## Example Usage

### Generate advisor namespace only (Step 1 - fast test):
```bash
./start.sh advisor 1
# Output: ./generated-advisor/tool-family/advisor.md
```

### Generate storage namespace with full pipeline:
```bash
./start.sh storage
# Output: ./generated-storage/tool-family/storage.md
#         ./generated-storage/horizontal-articles/horizontal-article-storage.md
```

### Generate all namespaces (unchanged behavior):
```bash
./start.sh
# Output: ./generated/tool-family/*.md (all 52 namespaces)
```

## Migration Notes

No breaking changes - existing scripts and workflows continue to work as before. The feature only activates when a specific namespace parameter is provided.

## Future Enhancements

Potential future improvements:
- Add cleanup script to remove namespace-specific directories
- Support for generating multiple specific namespaces in one run
- Configuration option to override the output directory naming pattern
