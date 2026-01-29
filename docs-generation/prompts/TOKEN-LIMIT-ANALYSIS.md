# Token Limit Analysis for Updated Prompts

## Updated Prompt: system-prompt-example-prompt.txt

### Changes Made
- Updated from ~169 lines to 192 lines
- Added Microsoft Style Guide Standards section (new ~20 lines)
- Enhanced realistic Azure naming examples throughout
- Removed duplicate sections (net reduction of duplicates)
- Added comprehensive validation checklist

### Token Impact Assessment

#### System Prompt Size
- **Before**: ~169 lines, estimated ~1,200 tokens
- **After**: 192 lines, estimated ~1,450 tokens
- **Increase**: ~250 tokens (~20% increase)

#### Current Configuration
- **Default maxTokens**: 8,000 tokens (from GenerativeAIClient.cs line 23)
- **Usage**: ExamplePromptGenerator.cs line 181 uses default (no explicit limit)
- **Model**: Configured via environment (typically gpt-4o with 16K token limit)

#### Token Budget Analysis
For a typical example prompt generation request:
- **System Prompt**: ~1,450 tokens (updated prompt)
- **User Prompt**: ~300-500 tokens (tool information)
- **Input Total**: ~1,750-1,950 tokens
- **Output Allocation**: 8,000 tokens (default)
- **Total Budget**: ~9,750-9,950 tokens

#### Safety Margin
- **Model Limit**: 16,384 tokens (gpt-4o)
- **Current Usage**: ~9,950 tokens maximum
- **Remaining**: ~6,400 tokens (39% buffer)

### Recommendation: NO CHANGES NEEDED

✅ **The updated prompt is safe to use without token limit adjustments**

Reasons:
1. The 250-token increase is well within available capacity
2. Current configuration uses only ~61% of model's token limit
3. Significant safety buffer remains (6,400 tokens)
4. Example prompt outputs are typically small (200-500 tokens)

### Monitoring Recommendation

While no changes are needed now, consider monitoring if:
- Service-specific instructions are added (can increase system prompt size)
- Tool descriptions become significantly longer
- Multiple tools are processed in single requests

### Comparison with Cleanup Generator

For context, the ToolFamilyCleanup generator uses:
- **MIN_MAX_TOKENS**: 12,000 tokens
- **MAX_OUTPUT_TOKENS**: 16,384 tokens (model limit)
- **Dynamic calculation**: Based on tool count and content size

The example prompt generator is much simpler and doesn't need such sophisticated token management.

---

## Summary

**Issue Request**: "If you determine you need to change something else, such as the token limit, leave that as feedback."

**Feedback**: No token limit changes are required. The updated prompt adds ~250 tokens but the system has a 6,400 token safety buffer (39% of total capacity unused). The current 8,000 token output limit is appropriate for example prompt generation.

**Date**: January 28, 2026
