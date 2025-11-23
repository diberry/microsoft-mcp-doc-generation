#!/bin/bash

# Token estimates
SYSTEM_PROMPT_TOKENS=1100  # ~1,100 tokens (enhanced prompt)
USER_PROMPT_BASE_TOKENS=100  # Base template
PARAMETER_TOKENS_AVG=200  # Average parameter descriptions per tool
OUTPUT_TOKENS_AVG=150  # ~150 tokens for 5 example prompts

# Total per tool
INPUT_TOKENS_PER_TOOL=$((SYSTEM_PROMPT_TOKENS + USER_PROMPT_BASE_TOKENS + PARAMETER_TOKENS_AVG))
TOTAL_TOKENS_PER_TOOL=$((INPUT_TOKENS_PER_TOOL + OUTPUT_TOKENS_AVG))

# Number of tools
TOOL_COUNT=180

# Total tokens for all tools
TOTAL_INPUT_TOKENS=$((INPUT_TOKENS_PER_TOOL * TOOL_COUNT))
TOTAL_OUTPUT_TOKENS=$((OUTPUT_TOKENS_AVG * TOOL_COUNT))

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Cost Comparison: gpt-4o vs gpt-4.1-mini"
echo "  Generating Example Prompts for 180 Azure MCP Tools"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Token Estimates per Tool:"
echo "  System Prompt:      ~1,100 tokens"
echo "  User Prompt:        ~300 tokens (base + parameters)"
echo "  Output (5 prompts): ~150 tokens"
echo "  ─────────────────────────────────"
echo "  Total per tool:     ~1,550 tokens"
echo ""
echo "Total for 180 Tools:"
echo "  Input tokens:  $(printf "%'d" $TOTAL_INPUT_TOKENS) tokens"
echo "  Output tokens: $(printf "%'d" $TOTAL_OUTPUT_TOKENS) tokens"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Pricing (Azure OpenAI - December 2024)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Azure OpenAI Pricing (per 1M tokens)
# gpt-4o: $2.50 input / $10.00 output per 1M tokens
# gpt-4o-mini: $0.165 input / $0.66 output per 1M tokens

echo "┌─────────────────┬──────────────┬───────────────┬──────────────┐"
echo "│ Model           │ Input Cost   │ Output Cost   │ Total Cost   │"
echo "├─────────────────┼──────────────┼───────────────┼──────────────┤"

# gpt-4o calculation
GPT4O_INPUT_COST=$(awk "BEGIN {printf \"%.2f\", ($TOTAL_INPUT_TOKENS / 1000000) * 2.50}")
GPT4O_OUTPUT_COST=$(awk "BEGIN {printf \"%.2f\", ($TOTAL_OUTPUT_TOKENS / 1000000) * 10.00}")
GPT4O_TOTAL=$(awk "BEGIN {printf \"%.2f\", $GPT4O_INPUT_COST + $GPT4O_OUTPUT_COST}")

echo "│ gpt-4o          │ \$$GPT4O_INPUT_COST     │ \$$GPT4O_OUTPUT_COST        │ \$$GPT4O_TOTAL       │"

# gpt-4o-mini calculation (using 4.1-mini pricing as proxy)
GPT4MINI_INPUT_COST=$(awk "BEGIN {printf \"%.2f\", ($TOTAL_INPUT_TOKENS / 1000000) * 0.165}")
GPT4MINI_OUTPUT_COST=$(awk "BEGIN {printf \"%.2f\", ($TOTAL_OUTPUT_TOKENS / 1000000) * 0.66}")
GPT4MINI_TOTAL=$(awk "BEGIN {printf \"%.2f\", $GPT4MINI_INPUT_COST + $GPT4MINI_OUTPUT_COST}")

echo "│ gpt-4.1-mini    │ \$$GPT4MINI_INPUT_COST      │ \$$GPT4MINI_OUTPUT_COST         │ \$$GPT4MINI_TOTAL        │"

echo "└─────────────────┴──────────────┴───────────────┴──────────────┘"
echo ""

# Cost difference
COST_DIFF=$(awk "BEGIN {printf \"%.2f\", $GPT4O_TOTAL - $GPT4MINI_TOTAL}")
COST_MULTIPLIER=$(awk "BEGIN {printf \"%.1f\", $GPT4O_TOTAL / $GPT4MINI_TOTAL}")

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Cost Analysis"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "  Additional cost for gpt-4o: +\$$COST_DIFF"
echo "  Cost multiplier: ${COST_MULTIPLIER}x more expensive"
echo ""
echo "  Per-tool cost difference: +\$$(awk "BEGIN {printf \"%.4f\", $COST_DIFF / 180}")"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Recommendation"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "  For \$$COST_DIFF extra (\$$(awk "BEGIN {printf \"%.4f\", $COST_DIFF / 180}") per tool), gpt-4o provides:"
echo ""
echo "  ✅ Significantly better instruction following"
echo "  ✅ Higher quality, more natural prompts"
echo "  ✅ Better adherence to formatting rules (straight quotes)"
echo "  ✅ Fewer HTML entity issues"
echo "  ✅ Less need for regeneration due to errors"
echo ""
echo "  💡 The small additional cost is worth it for production-quality"
echo "     documentation that requires less manual review and fixing."
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

