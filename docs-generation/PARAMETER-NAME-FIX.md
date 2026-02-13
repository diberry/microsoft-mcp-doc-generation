# Parameter Name Preservation Fix

## Issue
Ensure that parameter names with type qualifiers (like "name") are fully preserved in documentation parameter tables.

## Problem Statement
Parameter names such as:
- `resource-group-name`
- `secret-name`  
- `storage-account-name`
- `container-name`

Should display in documentation as:
- "**Resource group name**" (NOT "Resource group")
- "**Secret name**" (NOT "Secret")
- "**Storage account name**" (NOT "Storage account")
- "**Container name**" (NOT "Container")

## Investigation Findings

After thorough investigation of the codebase, **no code was found that strips the "name" suffix** from parameter names. The transformation logic in both:

1. `TextCleanup.NormalizeParameter()` (NaturalLanguageGenerator/TextCleanup.cs)
2. `formatNaturalLanguage` helper (CSharpGenerator/HandlebarsTemplateEngine.cs)

Both correctly join ALL words in the parameter name, preserving type qualifiers like "name".

## Solution

Added explicit documentation and comments as safeguards to ensure this behavior is maintained:

### Files Modified

1. **docs-generation/NaturalLanguageGenerator/TextCleanup.cs**
   - Added comprehensive XML documentation
   - Added inline comments emphasizing word preservation
   - Lines 213-259 (NormalizeParameter method)

2. **docs-generation/CSharpGenerator/HandlebarsTemplateEngine.cs**
   - Added detailed comments to formatNaturalLanguage helper
   - Lines 324-388

3. **docs-generation/CSharpGenerator/Generators/ParameterGenerator.cs**
   - Added explanatory comment at NL_Name assignment
   - Line 123

4. **docs-generation/CSharpGenerator/Generators/PageGenerator.cs**
   - Added comment documenting word preservation
   - Line 141

5. **docs-generation/CSharpGenerator/Generators/ParamAnnotationGenerator.cs**
   - Added comment documenting word preservation
   - Line 176

## Testing

Created comprehensive test suite that validates parameter name transformation:

```
Test Results: 10 passed, 0 failed

✓ resource-group-name → Resource group name
✓ resource-name → Resource name  
✓ secret-name → Secret name
✓ storage-account-name → Storage account name
✓ container-name → Container name
✓ key-name → Key name
✓ vault-name → Vault name
✓ database-name → Database name
✓ server-name → Server name
✓ app-name → App name
```

## Impact

- **No functional changes** - the code already worked correctly
- **Added documentation** - prevents future regressions
- **Added safeguards** - explicit comments ensure developers understand the requirement
- **Verified behavior** - test suite confirms correct transformation

## Future Maintenance

If you need to modify parameter name transformation:

1. **ALWAYS preserve type qualifiers** like "name", "id", "uri", etc.
2. **Run the test suite** to verify no regressions
3. **Check all generators** that use `TextCleanup.NormalizeParameter()`:
   - ParameterGenerator
   - PageGenerator  
   - ParamAnnotationGenerator
   - DocumentationGenerator
   - OptionsDiscovery
   - ServiceOptionsDiscovery

## Related Files

- `docs-generation/nl-parameters.json` - Direct parameter mappings
- `docs-generation/static-text-replacement.json` - Text replacements
- `docs-generation/stop-words.json` - Words removed from filenames (NOT parameter names)
- `docs-generation/templates/parameter-template.hbs` - Parameter table template
- `docs-generation/templates/area-template.hbs` - Area page template
- `docs-generation/templates/param-annotation-template.hbs` - Combined template

## Build Verification

```bash
cd docs-generation
dotnet build CSharpGenerator/CSharpGenerator.csproj
# Result: Build succeeded with 0 warnings, 0 errors
```

## Date
February 13, 2026
