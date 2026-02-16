# Horizontal Article Generation Improvement Plan

**Date**: February 8, 2026  
**Status**: Analysis & Recommendations (Priority 1 Completed)

---

## Executive Summary

The Horizontal Article Generator is currently a standalone system that generates how-to articles for Azure services using AI-generated content. While functional, it operates independently from the proven tool family generation pipeline (Steps 1-4) and lacks several enhancements that could improve content quality, consistency, and maintainability.

This document outlines:
1. **Current State**: How horizontal articles are generated today
2. **Dependency Gaps**: What the horizontal generator lacks compared to tool family generation
3. **Integration Opportunities**: How to leverage existing infrastructure
4. **Proposed Improvements**: Specific changes to enhance content quality and consistency
5. **Implementation Sequencing**: Recommended order of improvements

---

## Part 1: Current State Analysis

### Current Architecture

```
HorizontalArticleGenerator/
├── HorizontalArticleProgram.cs      [Entry point]
├── Generators/
│   └── HorizontalArticleGenerator.cs [Core logic]
├── Models/
│   ├── StaticArticleData.cs         [CLI-extracted data]
│   ├── AIGeneratedArticleData.cs    [AI response structure]
│   └── HorizontalArticleTemplateData.cs [Merged data for template]
├── prompts/
│   ├── horizontal-article-system-prompt.txt
│   └── horizontal-article-user-prompt.txt
└── templates/
    └── horizontal-article-template.hbs
```

### Current Data Flow

```
1. Load CLI output (cli-output.json)
   ↓
2. Extract static data per service
   - Service name from brand-to-server-mapping.json
   - Tool list with descriptions
   - Parameter counts
   ↓
3. Generate AI content via Azure OpenAI
   - System prompt: Defines JSON output structure
   - User prompt: Provides tool context
   - Response: Parsed JSON with AI-generated content
   ↓
4. Merge static + AI data
   ↓
5. Render with Handlebars template
   ↓
6. Save to: generated/horizontal-articles/
```

### Current Features

✅ **Working Well**:
- Extracts service metadata from CLI output
- Uses brand-to-server-mapping.json for proper service names
- Generates realistic scenarios with natural language examples
- AI-powered content creation (overview, capabilities, best practices)
- Error logging for failed API calls
- Supports single-article test mode (`--single` flag)
- Handles missing prerequisites gracefully

⚠️ **Limitations**:
- **Text transformation integration** - ✅ Completed (AI output now normalized via `TransformationEngine`)
- **No brand mapping integration** - Regenerates same lookups as tool family generator
- **No common parameter filtering** - Lists all parameters without filtering shared ones
- **No parameter improvements** - Uses raw CLI parameter counts
- **No example prompts reuse** - Doesn't leverage existing generated example prompts
- **No annotations leverage** - Tool annotations exist but aren't used
- **No composed tool descriptions** - Doesn't use improved tool descriptions from step 3
- **No AI improvement pass** - No secondary LLM pass for style guide conformance
- **No metadata extraction** - Doesn't generate tool metadata like tool family does
- **Limited configuration** - Hardcoded paths, not fully configurable

### Generated Output Structure

```
horizontal-articles/
├── horizontal-article-{service}.md     # Generated how-to article
├── error-{service}.txt                 # Error log if generation failed
└── error-{service}-airesponse.txt      # Raw AI response if parsing failed
```

### Template Structure

The `horizontal-article-template.hbs` generates:
- Frontmatter (title, description, date, ms.service)
- Overview section with service capabilities
- Prerequisites section (generic + service-specific)
- Available MCP tools (listing + links)
- Common scenarios (3-5 scenarios with examples)
- AI-specific use cases (if detected as AI service)
- Authentication & permissions (RBAC roles)
- Service documentation links

---

## Part 2: Dependency Gaps & Missed Opportunities

### Gap 1: Missing Text Transformation Integration

**Current**: AI output used as-is without cleanup

**Available**: 
- `TextTransformation` library with:
  - `TransformationEngine` for text normalization
  - `TextNormalizer` for static replacements (VMSS → "Virtual machine scale set")
  - Service brand name lookups
  - Acronym handling

**Impact**: 
- Inconsistent capitalization (e.g., "vmss" vs "VMSS" vs "Virtual Machine Scale Set")
- Service names not normalized to brand names
- Static text replacements (VMSS, VM) not applied

**Example Gap**:
```
Without transformation:
"This vmss tool helps you manage virtual machine scale sets"

With transformation:
"This Virtual Machine Scale Set tool helps you manage Azure Virtual Machine Scale Sets"
```

---

### Gap 2: Unused Dependency Data

**Available but unused**:

| Resource | Location | Use Case |
|----------|----------|----------|
| Example Prompts | `generated/example-prompts/` | AI scenario examples already generated |
| Tool Annotations | `generated/annotations/` | Tool usage hints and best practices |
| Composed Tools | `generated/tools-composed/` | AI-improved tool descriptions |
| Tool Metadata | `generated/cli/cli-output.json` | Tool characteristics (destructive, secret-requiring) |
| Common Parameters | `docs-generation/common-parameters.json` | Shared parameters to filter |

**Why It Matters**:
- Example prompts are real, tested natural language commands (better than AI generating new ones)
- Composed tools already went through AI improvement for consistency
- Metadata identifies dangerous operations (should warn users)
- Annotations contain practical usage hints
- Common parameters should be filtered from tool lists to avoid repetition

---

### Gap 3: No AI Improvement Pass

**Current**: Direct template rendering from AI response  
**Available**: `ToolGeneration_Improved` with:
- System prompt enforcing Microsoft style guide
- Backtick formatting rules
- Product name capitalization
- Parameter table constraints
- Voice and tone standards

**Impact**:
- Horizontal articles lack Microsoft style guide conformance
- Inconsistent tone and formatting
- Potential capitalization issues (e.g., "azure storage" vs "Azure Storage")

---

### Gap 4: Brand Mapping Duplication

**Current**: Horizontal generator loads brand-to-server-mapping.json internally  
**Available**: `TransformationEngine.GetServiceDisplayName()` does this

**Why It Matters**:
- Duplicated logic
- If brand mapping changes, horizontal generator must be updated separately
- Two sources of truth for service names

---

### Gap 5: Parameter Table Issues

**Current Issues**:
- Lists ALL tool parameters
- Includes common parameters (tenant, subscription, auth-method, retry-*)
- Doesn't indicate which parameters are destructive/require secrets
- Doesn't show parameter count after filtering common ones

**Available Data**:
- `common-parameters.json` - List of 9 shared parameters
- CLI metadata - Shows destructive, read-only, secret-requiring flags
- `parameters/` files - Full parameter documentation already generated

---

### Gap 6: Missing Metadata Generation

**Comparison to Tool Family Generation**:

Tool Family Generator (Step 4):
```
For each service:
  1. Generates metadata (tool count, primary operation type, etc.)
  2. Generates related content (similar services, next steps)
  3. Assembles complete file with all sections
```

Horizontal Generator:
```
For each service:
  1. Generates single markdown file
  2. No metadata extraction
  3. No related content identification
```

**Missing Outputs**:
- Service categorization (data plane vs management plane primary focus)
- Tool operation type breakdown (e.g., "80% data plane, 20% management plane")
- Related services (services that work together)
- Common workflow chains

---

## Part 3: Integration Opportunities

### Opportunity 1: Leverage Existing Generated Files

**Current**: Regenerate everything from scratch each time  
**Better**: Reference already-generated files as includes

**Implementation**:
```
Horizontal Article Template Enhancement:
├── Service Overview (AI-generated)
├── Capabilities List (AI-generated)
├── Prerequisites (AI-generated + service-specific)
├── Available Tools Section
│   ├── Tool list with descriptions
│   └── [!INCLUDE] links to tool/parameters/examples if available
├── Scenarios (AI-generated)
├── Best Practices (AI-generated)
└── Authentication & Links (AI-generated)

Include Strategy:
- If example-prompts/{tool}-example-prompts.md exists → reference it
- If annotations/{tool} exists → reference it
- If tool is in tools-composed/ → use improved description
```

**Benefits**:
- Single source of truth for tool descriptions
- Updates to tool descriptions automatically flow to articles
- Reduces AI token usage (shorter prompts)
- Consistent with multi-page documentation approach

---

### Opportunity 2: Apply Text Transformation

**Current**: Raw AI output  
**Better**: Run through TransformationEngine

**Implementation**:
```csharp
// After parsing AI response
var transformationEngine = new TransformationEngine(transformationConfig);

// Normalize all AI-generated text
aiData.ServiceShortDescription = 
    transformationEngine.TransformDescription(aiData.ServiceShortDescription);
aiData.ServiceOverview = 
    transformationEngine.TransformDescription(aiData.ServiceOverview);

// For each capability/scenario/best practice...
foreach (var capability in aiData.Capabilities)
{
    capability = transformationEngine.TransformDescription(capability);
}
```

**Benefits**:
- Consistent VMSS/VM capitalization
- Proper Azure service name capitalization
- Unified text transformation rules
- Easier to maintain single rule set

---

### Opportunity 3: Add AI Improvement Pass

**Current**: Direct render from initial AI response  
**Better**: Add style guide conformance pass

**Implementation**:
```
Horizontal Generation Pipeline:
├─ Phase 1: Extract static data
├─ Phase 2: Generate AI content (JSON)
├─ Phase 3: Merge + render template → markdown
├─ Phase 4: Parse and improve via ToolGeneration_Improved pass
└─ Phase 5: Save final article
```

**New Step**:
```
Take rendered horizontal article markdown through:
1. Load into ToolGeneration_Improved system
2. Apply Microsoft style guide standards
3. Validate backtick usage
4. Check product name capitalization
5. Return cleaned markdown
```

**Expected Improvements**:
- Consistent formatting
- Proper code/parameter styling
- Microsoft voice and tone
- Professional appearance matching tool family pages

---

### Opportunity 4: Filter Common Parameters

**Current**: 
```
Available MCP tools for {Service}:
- tool-one --param1 --param2 --tenant --subscription
- tool-two --param3 --auth-method --resource-group
```

**Better**:
```
Available MCP tools for {Service}:
- tool-one --param1 --param2 (+ common params)
- tool-two --param3 (+ common params)

Note: All tools also accept common parameters like --tenant, --subscription, etc.
```

**Implementation**:
1. Load `common-parameters.json`
2. Filter tool parameters when generating AI content
3. Add note about common parameters being available everywhere

**Benefits**:
- Cleaner tool listings
- Reduce clutter in prompts
- Educate users about common parameters

---

### Opportunity 5: Add Service-Level Metadata

**Current**: Just generates markdown article  
**Better**: Also generate structured metadata

**New Output**:
```
tool-family-metadata/
└── {service}-metadata.json
    {
      "serviceName": "Azure Storage",
      "serviceIdentifier": "storage",
      "toolCount": 45,
      "toolsByPlane": {
        "dataPlane": 35,
        "managementPlane": 10
      },
      "destructiveToolCount": 5,
      "secretsRequiringTools": 8,
      "primaryUseCases": [...],
      "relatedServices": ["...]
    }
```

**Use Cases**:
- Service discovery and filtering
- Risk assessment (how many destructive operations)
- Workflow planning (what services to combine)

---

## Part 4: Proposed Improvements (Priority-Ordered)

### Priority 1: Text Transformation Integration ⭐⭐⭐⭐⭐ — ✅ Completed

**Impact**: High (fixes VMSS, capitalization, consistency)  
**Complexity**: Low (integrate existing library)  
**Effort**: 2-4 hours

**Changes (Implemented)**:
1. Load `TransformationConfig` in `HorizontalArticleProgram`
2. Create `TransformationEngine` instance
3. Apply to all AI-generated text fields:
   - serviceShortDescription
   - serviceOverview
   - Each capability
   - Each scenario (title + description)
   - Each best practice
   - Authentication notes
4. Test with VMSS-containing services

**Files Modified**:
- `HorizontalArticleProgram.cs` - Load and pass transformation engine
- `HorizontalArticleGenerator.cs` - Apply transformations after parsing AI response

**Testing**:
```
Run: ./Generate-HorizontalArticles.ps1 -ServiceArea "compute"
Verify: VMSS appears as "Virtual Machine Scale Set (VMSS)"
```

---

### Priority 2: Add AI Improvement Pass ⭐⭐⭐⭐

**Impact**: High (Microsoft style conformance)  
**Complexity**: Medium (integrate ToolGeneration_Improved)  
**Effort**: 4-6 hours

**Changes**:
1. After rendering template to markdown (Phase 3)
2. Save rendered markdown to temp file
3. Call `ToolGeneration_Improved` to clean it
4. Parse cleaned output
5. Save final version

**New Phase 4**:
```csharp
// After template rendering
var renderedMarkdown = HBSEngine.Render(template, mergedData);

// Improve via style guide
var improvedMarkdown = await ImproveWithAI(renderedMarkdown, serviceIdentifier);

// Save final version
await File.WriteAllTextAsync(finalPath, improvedMarkdown);
```

**Files to Create**:
- `HorizontalArticleGenerator/Services/AIImprovementService.cs` - Calls ToolGeneration_Improved

**Benefits**:
- Consistent with tool family generation approach
- Ensures Microsoft style guide compliance
- Fixes backtick usage, capitalization
- Professional appearance

---

### Priority 3: Reference Generated Files (Includes) ⭐⭐⭐⭐

**Impact**: Medium (better integration, single source of truth)  
**Complexity**: High (template redesign)  
**Effort**: 6-8 hours

**Changes**:
1. Enhance AI prompts to generate links instead of inline content
2. Modify template to [!INCLUDE] example prompts and annotations
3. Update tool listing format:
   ```
   - **[tool-name](link)** - description
     See also: [examples](examples-link) | [annotations](annotation-link)
   ```

**Key Enhancement to User Prompt**:
```
## Important: Link to Existing Generated Content

If the service has tools with existing generated example prompts or annotations:
- Reference them with links instead of duplicating
- Example: "See [example prompts for storage account create](../example-prompts/storage-account-create-example-prompts.md)"
- This ensures users get consistent, validated examples
```

**Benefits**:
- Single source of truth
- Reduced AI token usage
- Automatic updates when underlying files change
- Consistent examples across documentation

---

### Priority 4: Parameter Table Improvements ⭐⭐⭐

**Impact**: Medium (cleaner, more accurate)  
**Complexity**: Medium (filtering logic)  
**Effort**: 3-4 hours

**Changes**:
1. Load `common-parameters.json`
2. Filter common parameters from tool listings
3. Add note: "All tools also support common parameters like --tenant, --subscription..."

**Modified Tool Listing**:
```
**Example Commands:**
- storage account create --account-name ... --sku ...
  (Also supports: --tenant, --subscription, --auth-method, etc.)
```

**Files to Modify**:
- `HorizontalArticleGenerator.cs` - Add parameter filtering in data extraction
- `horizontal-article-user-prompt.txt` - Instruct AI to exclude common params

---

### Priority 5: Add Service-Level Metadata Generation ⭐⭐⭐

**Impact**: Medium (enables future features)  
**Complexity**: Medium (new output format)  
**Effort**: 4-6 hours

**New Outputs**:
```
tool-family-metadata/
└── {service}-metadata.json
    {
      "serviceName": "Azure Storage",
      "dataPlaneToolCount": 35,
      "managementPlaneToolCount": 10,
      "generatedAt": "2025-02-08T...",
      "articleLink": "horizontal-article-storage.md"
    }
```

**Implementation**:
1. Analyze tool list (data vs management plane)
2. Count destructive operations
3. Count secret-requiring operations
4. Generate JSON metadata
5. Save alongside HTML article

**Benefits**:
- Service discovery filtering
- Risk assessment dashboards
- Workflow planning tools
- Consistency with tool family metadata

---

### Priority 6: Parameter Metadata Integration ⭐⭐

**Impact**: Low-Medium (informational)  
**Complexity**: Medium (data structure changes)  
**Effort**: 3-4 hours

**Changes**:
1. Include metadata indicators in tool listings:
   ```
   - **storage account create** - Creates a storage account
     ⚠️ **Destructive** | Requires Contributor role
   ```

2. Pass tool metadata to template:
   ```
   Tools array with:
   - command
   - description
   - destructive: boolean
   - readOnly: boolean
   - requiresSecrets: boolean
   ```

**Benefits**:
- Warns users about dangerous operations
- Identifies which tools need secrets
- Read-only operations clearly marked

---

## Part 5: Non-Recommended Changes

These could be done but have limited benefit:

### ❌ Move Horizontal Generation to Tool Family Pipeline

**Why Not**:
- Horizontal articles serve different purpose (how-to guides)
- Tool family pages are reference documentation
- Different audiences and use cases
- Maintenance burden of tight coupling

**Better**: Keep separate but with shared utilities (transformation, improvements)

### ❌ Replace AI Generation with Static Templates

**Why Not**:
- AI-generated content provides significant value
- Scenarios and best practices need service context
- Static templates would be generic and less helpful
- Current prompts are well-structured

**Better**: Keep AI but add improvement passes

### ❌ Generate Horizontal Articles During Generate.ps1

**Why Not**:
- Adds significant time to main generation pipeline
- Can be run independently
- Different cadence and requirements
- Separate Azure OpenAI quota usage tracking

**Better**: Keep as independent Step 5 in Generate-ToolFamily.ps1

---

## Part 6: Implementation Roadmap

### Phase 1: Foundation Improvements (Week 1)

1. **Text Transformation Integration** (Priority 1)
   - Add transformation engine to HorizontalArticleGenerator
   - Test with compute/storage services
   - Verify VMSS, capitalization fixes

2. **Update Documentation**
   - Update README.md with transformation info
   - Document new text normalization behavior

### Phase 2: Quality Enhancements (Week 2)

3. **AI Improvement Pass** (Priority 2)
   - Create AIImprovementService
   - Add Phase 4 to generation pipeline
   - Test style guide compliance

4. **Parameter Filtering** (Priority 4)
   - Implement common parameter filtering
   - Update prompts
   - Test with parameter-heavy services

### Phase 3: Integration Features (Week 3)

5. **Reference Generated Files** (Priority 3)
   - Enhance prompts to reference includes
   - Update template with [!INCLUDE] links
   - Validate link generation

6. **Metadata Generation** (Priority 5)
   - Add metadata JSON output
   - Create metadata schema
   - Document usage

### Phase 4: Polish (Week 4)

7. **Parameter Metadata** (Priority 6)
   - Add destructive/secret indicators
   - Enhance template
   - Test comprehensive scenarios

8. **Testing & Documentation**
   - Comprehensive testing of all services
   - Update all documentation
   - Performance benchmarking

---

## Part 7: Success Criteria

### After Priority 1 (Text Transformation)
- [ ] All VMSS references are properly capitalized
- [ ] All Azure service names use brand names (e.g., "Azure Storage" not "storage")
- [ ] Text transformation rules from transformation-config.json applied consistently

### After Priority 2 (AI Improvement)
- [ ] Horizontal articles use Microsoft style guide standards
- [ ] Backticks used correctly (inline code, not parameter names)
- [ ] Product names capitalized properly throughout

### After Priority 3 (Includes)
- [ ] Tool example prompts referenced from existing generated files
- [ ] Tool annotations linked when available
- [ ] Single source of truth for tool descriptions

### After Priority 4 (Parameter Filtering)
- [ ] Common parameters not listed in tool parameter tables
- [ ] Clear note that common parameters available everywhere
- [ ] Reduced clutter in tool listings

### After Priority 5 (Metadata)
- [ ] Service metadata JSON files generated alongside articles
- [ ] Data plane/management plane split identified
- [ ] Metadata usable for service discovery

### After Priority 6 (Parameter Metadata)
- [ ] Destructive operations clearly marked with warnings
- [ ] Secret-requiring tools identified
- [ ] Read-only operations marked for clarity

---

## Part 8: Questions for Review

Before proceeding with implementation, consider:

1. **Include Strategy**: Should horizontal articles include content from other generated files, or maintain independence?
   - Pro-include: Single source of truth, automatic updates
   - Pro-independent: Standalone, self-contained articles

2. **AI Improvement Scope**: Should AI improvement apply to all AI-generated text, or only specific sections?
   - Current: None
   - Proposed: All sections

3. **Metadata Usage**: What will service metadata be used for?
   - Discovery dashboard?
   - Workflow planning?
   - Risk assessment?

4. **Timeline**: What's the priority of these improvements?
   - Immediate (needed for users)
   - Soon (nice to have)
   - Later (nice to have)

5. **Testing Coverage**: How many services should be tested for each priority?
   - Just a couple (storage, compute)?
   - All 30+?

---

## Part 9: Dependency Checklist

All improvements rely on existing infrastructure:

- [x] `TextTransformation` library (exists, working)
- [x] `ToolGeneration_Improved` prompts (exists, has improvement logic)
- [x] `common-parameters.json` (exists, 9 parameters defined)
- [x] `brand-to-server-mapping.json` (exists, service names defined)
- [x] `transformation-config.json` (exists, text rules defined)
- [x] Example prompts generation (exists in `generated/example-prompts/`)
- [x] Tool annotations (exists in `generated/annotations/`)
- [x] Composed tools (exists in `generated/tools-composed/`)

**No new dependencies required** - all improvements use existing components.

---

## Conclusion

The Horizontal Article Generator is functional but operates as a separate system. By integrating it with the proven tool family generation pipeline infrastructure (text transformation, AI improvement, parameter filtering), we can:

1. **Improve Quality**: Microsoft style guide compliance, consistent formatting
2. **Reduce Redundancy**: Reuse existing generated content, avoid duplication
3. **Increase Consistency**: Apply same rules as tool family generation
4. **Better Integration**: Connect horizontal articles with tool family pages
5. **Easier Maintenance**: Single source of truth for rules and content

The recommended approach is **Priority 1 + 2 first** (transformation + improvement pass) as they provide immediate, high-impact improvements with relatively low complexity. The other improvements can be phased in based on user feedback and available resources.

---

**Document Status**: Analysis Complete  
**Next Steps**: Await review and approval to proceed with implementation
