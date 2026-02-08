# AI Example Prompts: Quote and HTML Entity Fixes

## Problem Identified

The AI-generated example prompts had two issues:

1. **Smart Quotes (Curly Quotes)**: The AI model was generating typographical quotes (`'` `'` `"` `"`) instead of straight quotes (`'` `"`)
2. **HTML Entities**: Some prompts contained HTML entity encoding (`&quot;`, `&lt;`, `&gt;`, `&#39;`) instead of plain characters

### Examples of Issues

**Smart Quotes:**
```markdown
- **Get cluster**: Show me the 'dev-cluster' AKS cluster
```
Should be:
```markdown
- **Get cluster**: Show me the 'dev-cluster' AKS cluster
```

**HTML Entities:**
```markdown
- **Add entries**: Append {&quot;action&quot;:&quot;create&quot;}
```
Should be:
```markdown
- **Add entries**: Append {"action":"create"}
```

## Root Cause

The Azure OpenAI model (gpt-4.1-mini) was generating these characters despite instructions. This is a **model output issue**, not a post-processing problem.

## Solution Implemented

### Two-Layer Defense Strategy

#### 1. Enhanced System Prompt (Prevention)
**File**: `docs-generation/prompts/system-prompt-example-prompt.txt`

Added explicit, detailed instructions:
- ✅ **Only use straight quotes** (`'` and `"`) - keyboard keys
- ❌ **Never use smart/curly quotes** (`'` `'` `"` `"`)
- ❌ **Never use HTML entities** (`&quot;`, `&apos;`, `&lt;`, `&gt;`, `&amp;`)
- Added character examples and validation checklist
- Added "before submitting" verification step

**Key Addition:**
```
**CRITICAL FORMATTING RULES - MUST FOLLOW EXACTLY:**

1. **ONLY STRAIGHT QUOTES**: Use ONLY straight quotes (') and (") - keyboard apostrophe and quote keys
   - ❌ NEVER use smart/curly quotes: ' ' " "
   - ❌ NEVER use typographical quotes (Unicode U+2018, U+2019, U+201C, U+201D)
   - ✅ ALWAYS use straight quotes: ' and "

2. **NO HTML ENTITIES**: Do NOT use HTML encoding of any kind
   - ❌ NEVER write: &quot; &apos; &#39; &#34; &amp; &lt; &gt;
   - ✅ ALWAYS write plain characters: " ' & < >

FINAL CHECK: Look at your output. If you see any of these characters, you made a mistake: ' ' " " &quot; &apos;
Correct format uses only: ' and "
```

#### 2. Post-Processing Cleanup (Safety Net)
**Files**: 
- `docs-generation/NaturalLanguageGenerator/TextCleanup.cs` (new method)
- `docs-generation/CSharpGenerator/Generators/ExamplePromptGenerator.cs` (integration)

Added `CleanAIGeneratedText()` method that automatically fixes:
- Smart single quotes (`'` `'`) → Straight apostrophe (`'`)
- Smart double quotes (`"` `"`) → Straight quote (`"`)
- HTML quote entities (`&quot;`, `&#34;`) → Quote (`"`)
- HTML apostrophe entities (`&apos;`, `&#39;`) → Apostrophe (`'`)
- HTML symbols (`&lt;`, `&gt;`, `&amp;`) → Plain characters (`<`, `>`, `&`)

**Implementation:**
```csharp
// Clean each prompt to fix smart quotes and HTML entities
Prompts = firstEntry.Value
    .Select(p => NaturalLanguageGenerator.TextCleanup.CleanAIGeneratedText(p))
    .ToList()
```

## Why Both Layers?

1. **System Prompt**: Ideally prevents issues at the source (AI model output)
2. **Post-Processing**: Guarantees clean output even if AI model ignores instructions

This defense-in-depth approach ensures:
- ✅ Future regenerations will be cleaner (better prompt)
- ✅ Any prompts that slip through are automatically fixed (cleanup function)
- ✅ No manual intervention required

## Testing

Build verified successful with all changes:
```bash
cd docs-generation
dotnet build
# Build succeeded in 15.7s
```

## Next Steps

To regenerate all example prompts with the fixes:

```bash
# From repository root
sudo bash ./run-generative-ai-output.sh
```

This will:
1. Use the enhanced system prompt (prevents issues)
2. Apply post-processing cleanup (fixes any remaining issues)
3. Generate clean example prompts with only straight quotes and plain characters

## Files Changed

1. **docs-generation/prompts/system-prompt-example-prompt.txt**
   - Enhanced with explicit character formatting rules
   - Added validation checklist and examples

2. **docs-generation/NaturalLanguageGenerator/TextCleanup.cs**
   - Added `CleanAIGeneratedText()` method
   - Handles Unicode smart quotes and HTML entities

3. **docs-generation/CSharpGenerator/Generators/ExamplePromptGenerator.cs**
   - Integrated cleanup function into prompt processing
   - Cleans all prompts after AI generation

## Impact

- **All 181 tools** will have clean example prompts
- **No HTML entities** in generated markdown
- **Consistent straight quotes** throughout
- **Better markdown rendering** in documentation
