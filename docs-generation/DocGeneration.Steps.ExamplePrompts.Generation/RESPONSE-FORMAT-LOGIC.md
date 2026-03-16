# Response Format Logic - Isolated View

This document shows the two key units of logic for handling LLM responses:
1. **What we tell the LLM to return** (prompt instructions)
2. **How we extract the JSON from the response** (processing function)

---

## UNIT 1: Prompt Instructions (What LLM Should Return)

**Location**: `prompts/system-prompt-example-prompt.txt` (lines 150-235)

### Key Requirements Told to LLM:

```
═══════════════════════════════════════════════════════════════
CRITICAL: RESPONSE FORMAT REQUIREMENTS - READ THIS SECTION CAREFULLY
═══════════════════════════════════════════════════════════════

YOUR RESPONSE MUST CONTAIN:

1. ONLY THE JSON OBJECT - Nothing else
2. NO PREAMBLE - Do not include "Here is the response:" or "Step 1:", etc.
3. NO VERIFICATION TEXT - Do not include your checklist, verification steps, or reasoning
4. NO CODE FENCES - Do not wrap the JSON in ```json or ``` markers
5. NO EXTRA TEXT - Do not add explanations before or after the JSON

RESPONSE STRUCTURE (EXACT FORMAT):

{
  "tool-command-name": [
    "example prompt 1",
    "example prompt 2",
    "example prompt 3",
    "example prompt 4",
    "example prompt 5"
  ]
}
```

### ✅ Correct Example:
```json
{
  "storage account list": [
    "Show me all storage accounts in resource group 'rg-prod'",
    "List the storage accounts in 'rg-dev'"
  ]
}
```

### ❌ Incorrect Examples (What NOT to do):

**With preamble:**
```
Here are the 5 prompts:
{
  "storage account list": [...]
}
```

**With verification:**
```
Required Parameters:
☐ --resource-group

{
  "storage account list": [...]
}
```

**With code fences:**
```
```json
{
  "storage account list": [...]
}
```
```

---

## UNIT 2: Response Processing (How We Extract JSON)

**Location**: `Generators/ExamplePromptGenerator.cs` (lines 148-230)

### Function: `ExtractJsonFromLLMResponse(string response)`

**Purpose**: LLMs often return responses with preamble text, reasoning steps, verification checklists, and other content BEFORE the actual JSON object. This function isolates ONLY the JSON object.

### Processing Strategies (in priority order):

```csharp
// ═══════════════════════════════════════════════════════════════
// STRATEGY 1: Look for ```json code block (most explicit)
// ═══════════════════════════════════════════════════════════════
if (text.Contains("```json"))
{
    var start = text.IndexOf("```json") + 7;
    var end = text.IndexOf("```", start);
    if (end > start)
    {
        return text.Substring(start, end - start).Trim();
    }
}

// ═══════════════════════════════════════════════════════════════
// STRATEGY 2: Find the LAST ``` code block
// (LLM often puts final answer at the end)
// ═══════════════════════════════════════════════════════════════
if (text.Contains("```"))
{
    var lastClosingTick = text.LastIndexOf("```");
    var lastOpeningTick = text.LastIndexOf("```", lastClosingTick - 1);
    if (lastOpeningTick >= 0)
    {
        var blockContent = text.Substring(
            lastOpeningTick + 3, 
            lastClosingTick - lastOpeningTick - 3
        ).Trim();
        
        // Verify it starts with { (looks like JSON)
        if (blockContent.StartsWith("{"))
        {
            return blockContent;
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// STRATEGY 3: Find last complete JSON object
// Use brace matching to find the outermost { ... }
// ═══════════════════════════════════════════════════════════════
var lastClosingBrace = text.LastIndexOf('}');
if (lastClosingBrace >= 0)
{
    // Search backwards to find the matching opening brace
    int braceCount = 1;
    for (int i = lastClosingBrace - 1; i >= 0; i--)
    {
        if (text[i] == '}') braceCount++;
        else if (text[i] == '{') braceCount--;
        
        if (braceCount == 0)
        {
            return text.Substring(i, lastClosingBrace - i + 1).Trim();
        }
    }
}
```

### Example Processing:

**Input (LLM Response with preamble):**
```
Required Parameters:
☐ --vault
☐ --secret
☐ --value

Prompt 1: "Create a secret named 'db-password'..."
✓ --vault present? YES

```
{
  "keyvault secret create": [
    "Create a secret named 'db-password'..."
  ]
}
```

**Output (Extracted JSON):**
```json
{
  "keyvault secret create": [
    "Create a secret named 'db-password'..."
  ]
}
```

---

## Problem Analysis: Why LLM Ignores Instructions

### The Issue:
Despite clear instructions to return ONLY JSON, the LLM sometimes returns:
- Verification checklists ("Required Parameters: ☐ --vault")
- Reasoning steps ("STEP 1:", "STEP 2:")
- Code fence wrappers (``` around the JSON)

### Why This Happens:
1. **System prompt emphasizes verification**: The prompt strongly encourages creating checklists and verification matrices to ensure all required parameters are included
2. **LLM "thinks out loud"**: The model shows its work before giving the final answer
3. **Conflicting instructions**: "Verify all parameters" vs "Return ONLY JSON"

### Current Solution:
- **Accept the behavior**: LLMs will sometimes include reasoning/verification
- **Robust extraction**: Use 3-tier strategy to find the JSON regardless of preamble
- **Log what we find**: Console messages show which extraction strategy succeeded

### Diagnostic Output:
When extraction succeeds, you'll see one of:
- `📝 JSON extracted from ```json block`
- `📝 JSON extracted from last ``` block`
- `📝 JSON extracted using brace matching`

When extraction fails:
- `⚠️ No JSON structure found in response`

---

## Testing the Extraction

To verify the extraction logic works on a specific response:

1. **Check the raw output file**: `generated/example-prompts-raw-output/{tool}-raw-output.txt`
2. **Look for the console message**: Should show which strategy was used
3. **Verify clean JSON**: Raw output should contain ONLY the JSON object

**Example good raw output:**
```json
{
  "acr registry list": [
    "List all container registries in the subscription 'MySubscription'.",
    "Show me all available ACR registries under the resource group 'rg-production'."
  ]
}
```

**Example problematic raw output (OLD behavior):**
```
Required Parameters:
☐ --resource-group

STEP 1: Generate prompts...
STEP 2: Verify...

```
{
  "acr registry list": [...]
}
```

---

## Next Steps for Improvement

If the LLM continues to ignore instructions:

1. **Simplify verification requirement**: Remove the detailed checklist requirement from system prompt
2. **Add negative examples**: Show more examples of what NOT to do
3. **Use function calling**: Force structured output via OpenAI function/tool calling
4. **Accept and extract**: Current approach - let LLM verify, extract JSON automatically

**Current Approach**: Option 4 - Robust extraction handles any format the LLM returns.
