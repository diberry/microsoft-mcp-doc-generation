#!/usr/bin/env bash
# resolve-aliases.sh — Step 3 of echo-finn-approved-pr-codeowners.
# OSPO lookup is intentionally an LLM step, not done here. This script only reads/writes the offline alias-cache and reports unresolved handles; the orchestrating agent invokes the OSPO sub-skill on misses, warms the cache, then re-runs this script.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="$SCRIPT_DIR/../config/approved-pr-codeowners.config.json"
DATA_DIR=""
RUN_ID=""
FINDINGS=""
CACHE=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --data-dir) DATA_DIR="$2"; shift 2 ;;
    --config) CONFIG="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; shift 2 ;;
    --findings) FINDINGS="$2"; shift 2 ;;
    --cache) CACHE="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
command -v jq >/dev/null 2>&1 || { echo "jq is required" >&2; exit 3; }
[ -f "$CONFIG" ] || { echo "Config not found: $CONFIG" >&2; exit 4; }
[ -n "$DATA_DIR" ] || DATA_DIR="$(jq -r '.data_dir' "$CONFIG")"
RUNS_SUBDIR="$(jq -r '.runs_subdir' "$CONFIG")"
resolve_run_dir() { local base="$DATA_DIR/$RUNS_SUBDIR"; if [ -n "$RUN_ID" ]; then echo "$base/$RUN_ID"; else ls -1d "$base"/* 2>/dev/null | sort -r | head -n1; fi; }
[ -n "$FINDINGS" ] || FINDINGS="$(resolve_run_dir)/findings.json"
[ -f "$FINDINGS" ] || { echo "findings.json not found: $FINDINGS" >&2; exit 4; }
[ -n "$CACHE" ] || CACHE="$(jq -r '.data_dir' "$CONFIG")/alias-cache.json"
UPN_DOMAIN="$(jq -r '.upn_domain' "$CONFIG")"
RETRY="$(jq -r '.unresolved_retry_days' "$CONFIG")"
NOW="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
[ -f "$CACHE" ] && CACHE_JSON="$(cat "$CACHE")" || CACHE_JSON='{"version":1,"entries":{}}'

mapfile -t HANDLES < <(jq -r '(if has("result") then .result else . end).approved_prs[].owners[] | select(.is_team|not) | .gh_handle | ltrimstr("@")' "$FINDINGS" | sort -fu)
for h in "${HANDLES[@]}"; do
  [ -n "$h" ] || continue
  key="$(printf '%s' "$h" | tr '[:upper:]' '[:lower:]')"
  CACHE_JSON="$(jq --arg k "$key" --arg gh "$h" --arg now "$NOW" --argjson retry "$RETRY" '
    if (.entries | has($k) | not) then .entries[$k]={gh_alias:$gh, ms_alias:null, upn:null, full_name:null, resolved_at:$now, source:"pending", status:"unresolved"}
    elif .entries[$k].status == "unresolved" then
      ($retry*86400) as $ttl | ((try (.entries[$k].resolved_at|fromdateiso8601) catch 0)) as $t |
      if ((now - $t) >= $ttl) then .entries[$k].resolved_at=$now | .entries[$k].source="pending-retry" else . end
    else . end' <<<"$CACHE_JSON")"
done

jq --argjson cache "$CACHE_JSON" --arg domain "$UPN_DOMAIN" --arg gen "$NOW" '
  def clean: ltrimstr("@");
  def unwrap: if has("result") then .result else . end;
  def res($o):
    if $o.is_team then $o + {status:"team", ms_alias:null, upn:null, mention:$o.gh_handle, note:"team-owner-not-resolved-via-ospo"}
    else ($o.gh_handle|clean|ascii_downcase) as $k | ($cache.entries[$k] // null) as $e |
      if ($e != null and $e.status == "resolved" and $e.ms_alias != null) then $o + {status:"resolved", ms_alias:$e.ms_alias, upn:($e.upn // ($e.ms_alias + "@" + $domain)), mention:("@" + $e.ms_alias)}
      else $o + {status:"unresolved", ms_alias:null, upn:null, mention:("@" + ($o.gh_handle|clean) + " (unresolved)")} end
    end;
  (unwrap
  | .stage = "alias-resolution"
  | .approved_prs |= map(.owners |= map(res(.)))
  | .unresolved_aliases = ([.approved_prs[].owners[] | select(.status=="unresolved") | (.gh_handle|clean)] | unique | map({gh_handle:., lookup:("https://repos.opensource.microsoft.com/people?q=" + .)}))) as $result
  | ($result.unresolved_aliases | length) as $unresolved
  | {status:(if $unresolved > 0 then "partial" else "success" end), result:$result, errors:(if $unresolved > 0 then [{code:"UNRESOLVED_ALIASES", message:($unresolved|tostring) + " GitHub owner handle(s) require OSPO lookup before Teams mentions are complete.", severity:"warning", target:"unresolved_aliases"}] else [] end), metadata:{producer:"echo-finn-approved-pr-codeowners.resolve-aliases", contractVersion:"1.0.0", format:"json", generatedAt:$gen, schema:"approved-pr-codeowners@1.0.0", correlationId:("approved-pr-codeowners-" + ($result.run_id|tostring))}}
' "$FINDINGS" > "$FINDINGS.next"
mv "$FINDINGS.next" "$FINDINGS"
mkdir -p "$(dirname "$CACHE")"
printf '%s' "$CACHE_JSON" | jq '.' > "$CACHE"
UNRES="$(jq '.result.unresolved_aliases | length' "$FINDINGS")"
echo "Resolved aliases. Unresolved individual handles: $UNRES"
[ "$UNRES" -eq 0 ] || jq -r '.result.unresolved_aliases[] | "Unresolved: " + .gh_handle + " -> " + .lookup' "$FINDINGS" >&2
echo "$FINDINGS"
