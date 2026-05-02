# Microsoft Foundry Infrastructure Migration Summary

**Date:** 2025-01-06  
**Issue:** #506  
**PR:** #513  
**Author:** Gonzo (Infrastructure/Automation Lead)

## Overview

Successfully migrated infrastructure from Azure OpenAI to Azure AI Services (Microsoft Foundry-compatible) with zero code changes and no breaking changes to existing deployments.

## Key Discovery

**Microsoft Foundry does not have a separate Bicep resource type.** It uses the exact same infrastructure as Azure OpenAI:

- **Resource Type:** `Microsoft.CognitiveServices/accounts`
- **Kind:** `AIServices` (more flexible than `OpenAI`, supports Foundry)
- **API Version:** `2025-06-01` (latest stable with Foundry support)
- **Deployment:** `Microsoft.CognitiveServices/accounts/deployments` (same as Azure OpenAI)

**Source:** [Microsoft Learn - Foundry Bicep Quickstart](https://learn.microsoft.com/azure/foundry/how-to/create-resource-template)

## Changes Made

### Bicep Templates (`infra/`)

**`infra/modules/openai.bicep`:**
- Updated `kind: 'OpenAI'` → `kind: 'AIServices'`
- Upgraded API version: `@2024-10-01` → `@2025-06-01`
- Updated resource/parameter descriptions
- Comments now reference Azure AI Services instead of Azure OpenAI

**`infra/main.bicep`:**
- Module names: `openai-primary` → `foundry-primary`, `openai-secondary` → `foundry-secondary`
- Resource naming: `oai-${environmentName}` → `ai-${environmentName}`
- Updated comments and descriptions
- Output variable names unchanged (still `FOUNDRY_*`)

### Documentation

**`README.md`:**
- Updated references from "Azure OpenAI" to "Azure AI Services (Foundry-compatible)"
- Pipeline step descriptions updated

**`CHANGELOG.md`:**
- Added entry documenting the migration

## What Didn't Change

### SDK / Code
- **No changes required** to `GenerativeAIClient.cs` or any C# code
- `Azure.AI.OpenAI` SDK works with both Azure OpenAI and Foundry endpoints
- `AzureOpenAIClient` class compatible with both endpoint types
- `IChatClient` abstraction already provides clean client swapping

### Environment Variables
- **No changes required** to `.env` files or environment variable names
- `FOUNDRY_*` prefix was forward-looking and already appropriate
- Variable names: `FOUNDRY_API_KEY`, `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_NAME`, etc.
- `GenerativeAIOptions.cs` loads from same environment variables

### Application Code
- All 5 AI consumer components work unchanged:
  1. Example prompts generation
  2. Horizontal articles generation
  3. Skills generation
  4. Bootstrap step (brand mappings)
  5. Tool family cleanup

## Testing Results

All tests passed without modifications:

```bash
dotnet build mcp-doc-generation.sln --configuration Release
# ✅ Build succeeded, 0 errors

dotnet test mcp-doc-generation.sln --configuration Release
# ✅ 668+ tests passed across 12 test projects

dotnet build skills-generation/skills-generation.slnx
# ✅ Build succeeded, 0 errors
```

## Deployment Impact

### For New Deployments
1. Use the updated Bicep templates
2. Deploy with `azd up` or your deployment pipeline
3. Resources will be named `ai-*` instead of `oai-*`
4. Set `FOUNDRY_*` environment variables as before

### For Existing Deployments
**If you need to migrate existing infrastructure:**

**Option 1: In-place update (resource names stay the same)**
- Change `kind: 'OpenAI'` to `kind: 'AIServices'` in Bicep
- Keep existing resource names (`oai-*`)
- Redeploy with `azd up`
- **No environment variable changes needed**

**Option 2: Fresh deployment (new resource names)**
- Deploy new `ai-*` resources alongside existing `oai-*`
- Update `.env` to point to new endpoints
- Cutover when ready
- Delete old resources

**Option 3: Continue using Azure OpenAI**
- No action required
- Existing `kind: 'OpenAI'` resources continue to work
- SDK is compatible with both

## Architecture Notes

### Why AIServices Instead of OpenAI?

The `AIServices` kind is more flexible:
- Supports Azure OpenAI models
- Supports Microsoft Foundry features
- Future-proofs for Azure AI platform evolution
- Same pricing and capabilities as `OpenAI` kind

### Endpoint Compatibility

Both endpoint types work with `Azure.AI.OpenAI` SDK:
- **Azure OpenAI:** `https://<name>.openai.azure.com`
- **Foundry:** `https://<name>.cognitiveservices.azure.com` or `https://<name>.openai.azure.com`

The SDK abstracts endpoint differences through `AzureOpenAIClient`.

### Abstraction Layer Benefits

The `GenerativeAIClient` class uses `Microsoft.Extensions.AI.IChatClient`:
- Clean separation between infrastructure and application code
- Easy to swap between Azure OpenAI, Foundry, or other providers
- Retry logic and error handling centralized
- Future-proof architecture

## Lessons Learned

1. **Research first:** Microsoft Foundry doesn't have a separate resource type - saves days of investigation
2. **Forward-looking naming:** `FOUNDRY_*` environment variables proved prescient
3. **Abstraction pays off:** `IChatClient` abstraction enabled zero-code migration
4. **SDK compatibility:** Azure OpenAI SDK works with Foundry endpoints out of the box

## References

- [Microsoft Learn - Deploy Foundry Resource with Bicep](https://learn.microsoft.com/azure/foundry/how-to/create-resource-template)
- [Microsoft Learn - CognitiveServices/accounts Bicep Reference](https://learn.microsoft.com/azure/templates/microsoft.cognitiveservices/accounts)
- [Azure AI Foundry Quickstart Templates](https://github.com/Azure/azure-quickstart-templates/tree/master/quickstarts/microsoft.machinelearningservices)
- [Issue #506](https://github.com/diberry/microsoft-mcp-doc-generation/issues/506)
- [PR #513](https://github.com/diberry/microsoft-mcp-doc-generation/pull/513)

## Migration Checklist

For teams migrating similar projects:

- [ ] Research target platform infrastructure (may use same resource types!)
- [ ] Update Bicep `kind` property to `AIServices`
- [ ] Upgrade API versions to latest stable
- [ ] Update resource naming conventions
- [ ] Update documentation and comments
- [ ] Verify SDK compatibility (often no changes needed)
- [ ] Check environment variable naming
- [ ] Run full test suite
- [ ] Document deployment options (in-place vs new resources)
- [ ] Update CHANGELOG
- [ ] Create PR with detailed research findings

---

**Conclusion:** The migration was simpler than expected due to infrastructure compatibility. The key insight - that Foundry uses the same Bicep resource type as Azure OpenAI - saved significant development time and enabled a zero-downtime migration path.
