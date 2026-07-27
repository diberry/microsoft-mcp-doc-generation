#!/usr/bin/env bash
# render-outputs.sh — Step 4 of echo-finn-approved-pr-codeowners.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="$SCRIPT_DIR/../config/approved-pr-codeowners.config.json"
TPL_DIR="$SCRIPT_DIR/../templates"
DATA_DIR=""
RUN_ID=""
FINDINGS=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --data-dir) DATA_DIR="$2"; shift 2 ;;
    --config) CONFIG="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; shift 2 ;;
    --findings) FINDINGS="$2"; shift 2 ;;
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
RUNDIR="$(cd "$(dirname "$FINDINGS")" && pwd)"
RUNID="$(jq -r '(if has("result") then .result else . end).run_id' "$FINDINGS")"
GEN="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
safe() { printf '%s' "$1" | tr '\\' '/' | tr '\n\r' '  ' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'; }

ROWS='[]'
N="$(jq '(if has("result") then .result else . end).approved_prs | length' "$FINDINGS")"
i=0
while [ "$i" -lt "$N" ]; do
  E="$(jq -c "(if has(\"result\") then .result else . end).approved_prs[$i]" "$FINDINGS")"
  i=$((i+1))
  PRN="$(jq -r '.pr_number' <<<"$E")"
  TITLE="$(safe "$(jq -r '.pr_title' <<<"$E")")"
  URL="$(safe "$(jq -r '.pr_url' <<<"$E")")"
  OWNERS="$(jq -r '[.owners[].gh_handle] | if length==0 then "(none)" else join(" ") end' <<<"$E")"
  MENTIONS="$(jq -r '[.owners[].mention] | if length==0 then "(no codeowners)" else join(" ") end' <<<"$E")"
  UNRES="$(jq -r '[.owners[] | select(.status=="unresolved") | .gh_handle] | join(" ")' <<<"$E")"
  ROWS="$(jq --arg pr "$PRN" --arg t "$TITLE" --arg u "$URL" --arg o "$OWNERS" --arg m "$MENTIONS" --arg un "$UNRES" '. + [{PR_NUMBER:$pr, PR_TITLE:$t, PR_URL:$u, OWNERS:$o, MENTIONS:$m, UNRESOLVED:$un}]' <<<"$ROWS")"
done

render() {
  local tpl="$1" out="$2" content pre rest inner post rows_out row count j key val
  content="$(cat "$tpl")"
  pre="${content%%'{{#ROWS}}'*}"
  rest="${content#*'{{#ROWS}}'}"
  inner="${rest%%'{{/ROWS}}'*}"
  post="${rest#*'{{/ROWS}}'}"
  rows_out=""
  count="$(jq 'length' <<<"$ROWS")"
  j=0
  while [ "$j" -lt "$count" ]; do
    row="$inner"
    for key in PR_NUMBER PR_TITLE PR_URL OWNERS MENTIONS UNRESOLVED; do
      val="$(jq -r ".[$j].$key" <<<"$ROWS")"
      row="${row//"{{$key}}"/$val}"
    done
    rows_out+="$row"
    j=$((j+1))
  done
  content="$pre$rows_out$post"
  content="${content//'{{RUN_ID}}'/$RUNID}"
  content="${content//'{{GENERATED_AT}}'/$GEN}"
  printf '%s' "$content" > "$out"
}

TEAM_PATH="$RUNDIR/teams-paste.txt"
REPORT_PATH="$RUNDIR/report.md"
RENDER_JSON="$RUNDIR/render.json"
render "$TPL_DIR/teams-paste.txt.tmpl" "$TEAM_PATH"
render "$TPL_DIR/report.md.tmpl" "$REPORT_PATH"
jq --arg gen "$GEN" --arg run "$RUNID" --arg team "$TEAM_PATH" --arg report "$REPORT_PATH" '{status:"success", result:{stage:"render", run_id:$run, generated_at:$gen, approved_prs:(if has("result") then .result else . end).approved_prs, artifactPaths:{teamsPaste:$team, report:$report}}, errors:[], metadata:{producer:"echo-finn-approved-pr-codeowners.render-outputs", contractVersion:"1.0.0", format:"json", generatedAt:$gen, schema:"approved-pr-codeowners@1.0.0", correlationId:("approved-pr-codeowners-" + $run)}}' "$FINDINGS" > "$RENDER_JSON"
echo "Rendered outputs in: $RUNDIR"
echo "  teams-paste.txt"
echo "  report.md"
echo "  render.json"
echo "$RUNDIR"
