# Integration Plan: Separated Tool Generation

## Overview

This document outlines how to integrate the separated tool generation packages (RawToolGenerator, ComposedToolGenerator, ImprovedToolGenerator) into the main documentation generation pipeline.

## Current Architecture

The current documentation generation flow in `Generate-MultiPageDocs.ps1`:

```
1. Extract CLI data → cli-output.json
2. Run CSharpGenerator with multiple generators:
   - PageGenerator → multi-page/*.md
   - AnnotationGenerator → multi-page/annotations/*.md
   - ParameterGenerator → multi-page/parameters/*.md
   - ParamAnnotationGenerator → multi-page/param-and-annotation/*.md
   - ExamplePromptGenerator → multi-page/example-prompts/*.md
   - CompleteToolGenerator → tools/*.complete.md (optional)
3. Generate reports and summaries
```

## Proposed Integration Architecture

### Option 1: Replace CompleteToolGenerator (Recommended)

Replace the existing `CompleteToolGenerator` with the separated tool generation pipeline:

```
1. Extract CLI data → cli-output.json
2. Run CSharpGenerator with generators:
   - PageGenerator → multi-page/*.md
   - AnnotationGenerator → multi-page/annotations/*.md
   - ParameterGenerator → multi-page/parameters/*.md
   - ParamAnnotationGenerator → multi-page/param-and-annotation/*.md
   - ExamplePromptGenerator → multi-page/example-prompts/*.md
3. Run Separated Tool Generation:
   - RawToolGenerator → tools-raw/*.md
   - ComposedToolGenerator → tools-composed/*.md
   - ImprovedToolGenerator → tools-ai-improved/*.md (optional)
4. Generate reports and summaries
```

### Option 2: Run in Parallel (Alternative)

Run both existing and new pipelines in parallel for comparison:

```
1. Extract CLI data → cli-output.json
2. Run CSharpGenerator (existing pipeline)
   - All existing generators including CompleteToolGenerator
3. Run Separated Tool Generation (new pipeline)
   - RawToolGenerator
   - ComposedToolGenerator
   - ImprovedToolGenerator (optional)
4. Compare outputs for quality assessment
```

## Implementation Steps

### Step 1: Update Generate-MultiPageDocs.ps1

Add new parameters to control separated tool generation:

```powershell
param(
    # ... existing parameters ...
    [bool]$UseSeparatedToolGeneration = $false,
    [bool]$ApplyAIImprovements = $false,
    [int]$MaxTokens = 8000
)
```

### Step 2: Conditional Execution

Add logic to choose between existing and new pipeline:

```powershell
if ($UseSeparatedToolGeneration) {
    Write-Info "Using separated tool generation pipeline..."
    
    # Generate annotations, parameters, example prompts (as before)
    # ...
    
    # Run separated tool generation
    $separateToolsScript = Join-Path $PSScriptRoot "Generate-SeparateTools.ps1"
    & $separateToolsScript `
        -OutputPath $outputDir `
        -SkipImproved:(!$ApplyAIImprovements) `
        -MaxTokens $MaxTokens
}
else {
    Write-Info "Using traditional tool generation..."
    
    # Run CompleteToolGenerator (existing)
    # ...
}
```

### Step 3: Update Docker Integration

Modify the Dockerfile to include the new generators:

```dockerfile
# Build separated tool generators
WORKDIR /docs-generation
RUN dotnet build RawToolGenerator/RawToolGenerator.csproj && \
    dotnet build ComposedToolGenerator/ComposedToolGenerator.csproj && \
    dotnet build ImprovedToolGenerator/ImprovedToolGenerator.csproj
```

### Step 4: Update GitHub Actions Workflow

Add environment variables and parameters to the workflow:

```yaml
- name: Generate Documentation
  env:
    FOUNDRY_API_KEY: ${{ secrets.FOUNDRY_API_KEY }}
    FOUNDRY_ENDPOINT: ${{ secrets.FOUNDRY_ENDPOINT }}
    FOUNDRY_MODEL_NAME: ${{ secrets.FOUNDRY_MODEL_NAME }}
  run: |
    pwsh ./docs-generation/Generate-MultiPageDocs.ps1 \
      -UseSeparatedToolGeneration $true \
      -ApplyAIImprovements $true
```

## Configuration Changes

### New Configuration Files

No new configuration files needed. The separated generators use:
- Existing `brand-to-server-mapping.json`
- Existing `.env` for Azure OpenAI credentials

### Environment Variables

For AI improvements, require:
- `FOUNDRY_API_KEY` - Azure OpenAI API key
- `FOUNDRY_ENDPOINT` - Azure OpenAI endpoint
- `FOUNDRY_MODEL_NAME` - Deployment/model name

## Testing Strategy

### Phase 1: Validation Testing

1. Run both pipelines side-by-side
2. Compare outputs:
   - File counts
   - Content structure
   - Content quality
3. Identify any regressions or issues

### Phase 2: Quality Assessment

1. Review AI-improved files for quality
2. Measure improvements:
   - Clarity of descriptions
   - Realism of example prompts
   - Adherence to Microsoft style guide
3. Collect feedback from documentation reviewers

### Phase 3: Performance Testing

1. Measure execution time for each phase
2. Identify bottlenecks
3. Optimize if needed (parallel processing, caching, etc.)

## Migration Path

### Week 1-2: Testing Phase

- Enable separated tool generation with feature flag
- Run in parallel with existing pipeline
- Compare and validate outputs
- No changes to production output

### Week 3-4: Feedback Phase

- Share AI-improved files with documentation team
- Collect feedback on quality improvements
- Make adjustments to prompts if needed
- Iterate on improvement logic

### Week 5: Cutover

- Switch default to separated tool generation
- Keep existing pipeline as fallback
- Monitor for issues
- Be ready to rollback if problems occur

### Week 6+: Cleanup

- Remove old CompleteToolGenerator if successful
- Update documentation
- Remove feature flags
- Consolidate code

## Rollback Plan

If issues are discovered after integration:

1. **Immediate Rollback**: Set `UseSeparatedToolGeneration = $false`
2. **Revert to Previous Output**: Use existing tool files
3. **Investigate Issue**: Review logs and error messages
4. **Fix and Retest**: Address the issue in the new generators
5. **Retry Integration**: Attempt cutover again after fixes

## Success Criteria

The integration is considered successful when:

1. ✅ All 208 tool files are generated without errors
2. ✅ File structure matches expected format
3. ✅ Content quality meets or exceeds current output
4. ✅ AI improvements provide measurable value
5. ✅ Performance is acceptable (< 20 minutes total)
6. ✅ No regressions in existing functionality
7. ✅ Documentation team approves quality

## Benefits of Integration

### Improved Modularity

- Each generator has a single, clear responsibility
- Easier to test and debug individual stages
- Can skip stages if not needed (e.g., skip AI improvements)

### Better Quality Control

- Can review intermediate outputs at each stage
- AI improvements are optional and can be toggled
- Easier to identify where issues occur

### Enhanced Customization

- Can customize prompts for AI improvements
- Can adjust which content is embedded
- Can modify placeholder format easily

### Future Extensibility

- Easy to add new stages (e.g., validation, testing)
- Can integrate with other AI models
- Can add custom post-processing

## Maintenance Considerations

### Prompt Management

- Prompts are stored in `ImprovedToolGenerator/Prompts/`
- Version control prompts alongside code
- Document prompt changes and their effects

### Error Handling

- Each generator handles errors independently
- Orchestration script reports failures clearly
- Logs are separated by generator

### Performance Optimization

If performance becomes an issue:
- Consider parallel processing for AI improvements
- Cache AI responses to avoid re-processing
- Implement incremental generation (only changed files)

## Long-term Vision

Eventually, the separated tool generation could replace the entire tool documentation generation:

1. **Phase 1**: Replace CompleteToolGenerator (current plan)
2. **Phase 2**: Extract other generators (annotations, parameters) into separate packages
3. **Phase 3**: Create a unified orchestration system
4. **Phase 4**: Add advanced features (validation, testing, quality metrics)

This would result in a fully modular, maintainable documentation generation system.
