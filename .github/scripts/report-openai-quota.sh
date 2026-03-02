#!/bin/bash
# Report Azure OpenAI token quota availability across all regions and models
# Usage: ./report-openai-quota.sh [--all] [--filter PATTERN]
#
# Options:
#   --all        Show all models including those with 0 limit (default: only available quota)
#   --filter     Filter model names by pattern (e.g., "gpt-4o" or "embedding")
#   --csv        Output in CSV format
#
# Requires: Azure CLI (az) authenticated with an active subscription

set -euo pipefail

SHOW_ALL=false
FILTER=""
CSV_OUTPUT=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --all) SHOW_ALL=true; shift ;;
    --filter) FILTER="$2"; shift 2 ;;
    --csv) CSV_OUTPUT=true; shift ;;
    -h|--help)
      echo "Usage: $0 [--all] [--filter PATTERN] [--csv]"
      echo ""
      echo "Options:"
      echo "  --all        Show all models including those with 0 limit"
      echo "  --filter     Filter model names by pattern (e.g., 'gpt-4o' or 'embedding')"
      echo "  --csv        Output in CSV format"
      exit 0
      ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

# Verify authentication
if ! az account show &>/dev/null; then
  echo "❌ Error: Not authenticated. Run 'az login' first."
  exit 1
fi

SUB_NAME=$(az account show --query name -o tsv)
SUB_ID=$(az account show --query id -o tsv)

echo "=========================================="
echo "Azure OpenAI Quota Report"
echo "=========================================="
echo "Subscription: $SUB_NAME ($SUB_ID)"
echo "Date: $(date -u '+%Y-%m-%d %H:%M UTC')"
if [[ -n "$FILTER" ]]; then
  echo "Filter: $FILTER"
fi
if [[ "$SHOW_ALL" == "true" ]]; then
  echo "Showing: All models (including 0 limit)"
else
  echo "Showing: Models with available quota only"
fi
echo ""

# Azure OpenAI supported regions
# https://learn.microsoft.com/azure/ai-services/openai/concepts/models
REGIONS=(
  australiaeast
  brazilsouth
  canadacentral
  canadaeast
  eastus
  eastus2
  francecentral
  germanywestcentral
  japaneast
  koreacentral
  northcentralus
  norwayeast
  polandcentral
  southafricanorth
  southcentralus
  southindia
  swedencentral
  switzerlandnorth
  uksouth
  westeurope
  westus
  westus3
)

API_VERSION="2023-05-01"

# Collect data
declare -A REGION_DATA
ERRORS=()

for region in "${REGIONS[@]}"; do
  printf "\r  Scanning: %-25s" "$region" >&2
  
  RESULT=$(az rest \
    --method GET \
    --uri "https://management.azure.com/subscriptions/${SUB_ID}/providers/Microsoft.CognitiveServices/locations/${region}/usages?api-version=${API_VERSION}" \
    2>/dev/null) || {
    ERRORS+=("$region")
    continue
  }

  # Build JMESPath query based on options
  if [[ "$SHOW_ALL" == "true" ]]; then
    JQ_FILTER="."
  else
    JQ_FILTER='[.[] | select(.limit > 0)]'
  fi

  if [[ -n "$FILTER" ]]; then
    JQ_FILTER="[.[] | select(.name.value | test(\"${FILTER}\"; \"i\"))]"
    if [[ "$SHOW_ALL" != "true" ]]; then
      JQ_FILTER="[.[] | select(.limit > 0 and (.name.value | test(\"${FILTER}\"; \"i\")))]"
    fi
  fi

  FILTERED=$(echo "$RESULT" | jq -r ".value | ${JQ_FILTER} | .[] | [\"${region}\", .name.value, (.currentValue|tostring), (.limit|tostring), (if .limit > 0 then ((.limit - .currentValue)|tostring) else \"0\" end)] | @tsv" 2>/dev/null)

  if [[ -n "$FILTERED" ]]; then
    REGION_DATA["$region"]="$FILTERED"
  fi
done

printf "\r%-40s\n" "" >&2

# Output results
if [[ "$CSV_OUTPUT" == "true" ]]; then
  echo "Region,Model,Used,Limit,Available"
  for region in "${REGIONS[@]}"; do
    if [[ -n "${REGION_DATA[$region]:-}" ]]; then
      echo "${REGION_DATA[$region]}" | while IFS=$'\t' read -r r name used limit avail; do
        echo "$r,$name,$used,$limit,$avail"
      done
    fi
  done
else
  # Table output
  printf "%-22s %-50s %8s %8s %10s\n" "Region" "Model" "Used" "Limit" "Available"
  printf "%-22s %-50s %8s %8s %10s\n" "$(printf '%0.s-' {1..22})" "$(printf '%0.s-' {1..50})" "--------" "--------" "----------"

  TOTAL_MODELS=0
  for region in "${REGIONS[@]}"; do
    if [[ -n "${REGION_DATA[$region]:-}" ]]; then
      echo "${REGION_DATA[$region]}" | while IFS=$'\t' read -r r name used limit avail; do
        printf "%-22s %-50s %8s %8s %10s\n" "$r" "$name" "$used" "$limit" "$avail"
      done
      TOTAL_MODELS=$((TOTAL_MODELS + $(echo "${REGION_DATA[$region]}" | wc -l)))
    fi
  done

  echo ""
  echo "Total: $TOTAL_MODELS model-region combinations"
fi

if [[ ${#ERRORS[@]} -gt 0 ]]; then
  echo ""
  echo "⚠️  Failed to query regions: ${ERRORS[*]}"
fi

echo ""
echo "Done."
