# Archived Scripts

These scripts have been consolidated into the single `generate.sh` script at the repository root.

## Migration Guide

### Old: `start.sh`
**New:** `./generate.sh all`

Generates full documentation for all tool families.

### Old: `start-only.sh <family>`
**New:** `./generate.sh family <family>`

Generates documentation for a single tool family/namespace.

Example:
```bash
# Old
./start-only.sh advisor

# New
./generate.sh family advisor
```

### Old: `start-only.sh <family> <steps>`
**New:** `./generate.sh family <family> --steps <steps>`

Generates documentation for a single tool family with specific steps.

Example:
```bash
# Old
./start-only.sh advisor 1,2,3

# New
./generate.sh family advisor --steps 1,2,3
```

### Old: `start-horizontal.sh`
**New:** Functionality integrated into `generate.sh family` (Step 5)

Horizontal articles are now generated as part of the tool family generation pipeline.

## New Functionality

The consolidated `generate.sh` also adds:

- `./generate.sh reports` - Generate only CLI analysis and common files
- Better help text: `./generate.sh help`
- Consistent command structure
- Color-coded output

## Why the Change?

The consolidation provides:
1. Single entry point for all documentation generation
2. Clear, consistent CLI interface
3. Easier to maintain
4. Better help documentation
5. Reduced confusion about which script to use

See the main repository README for full documentation.
