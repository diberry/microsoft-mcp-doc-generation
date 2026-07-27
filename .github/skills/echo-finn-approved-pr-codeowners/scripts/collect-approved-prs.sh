#!/usr/bin/env bash
# collect-approved-prs.sh — Step 1 of echo-finn-approved-pr-codeowners.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="$SCRIPT_DIR/../config/approved-pr-codeowners.config.json"
DATA_DIR=""
RUN_ID=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --config) CONFIG="$2"; shift 2 ;;
    --data-dir) DATA_DIR="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
command -v gh >/dev/null 2>&1 || { echo "gh is required" >&2; exit 3; }
command -v jq >/dev/null 2>&1 || { echo "jq is required" >&2; exit 3; }
[ -f "$CONFIG" ] || { echo "Config not found: $CONFIG" >&2; exit 4; }
[ -n "$DATA_DIR" ] || DATA_DIR="$(jq -r '.data_dir' "$CONFIG")"
[ -n "$RUN_ID" ] || RUN_ID="$(date +%Y-%m-%d-%H%M)"
REPO="$(jq -r '.target_repo' "$CONFIG")"
SEARCH_LIMIT="$(jq -r '.search_limit' "$CONFIG")"
RUNS_SUBDIR="$(jq -r '.runs_subdir' "$CONFIG")"
RUN_DIR="$DATA_DIR/$RUNS_SUBDIR/$RUN_ID"
mkdir -p "$RUN_DIR"
ME="$(gh api user --jq .login | tr -d '[:space:]')"
[ -n "$ME" ] || { echo "Could not resolve current GitHub login" >&2; exit 5; }
echo "Authenticated GitHub login: $ME"

PAIRS="$RUN_DIR/candidates.tsv"
: > "$PAIRS"
append_search() {
  local qualifier="$1"
  local label="$2"
  local json count
  json="$(gh search prs --repo "$REPO" --state open "$qualifier" --json number --limit "$SEARCH_LIMIT" 2>/dev/null || echo '[]')"
  count="$(jq 'length' <<<"$json")"
  if [ "$count" -eq "$SEARCH_LIMIT" ]; then
    echo "WARNING: $label search hit the $SEARCH_LIMIT-result cap; some PRs may be missed." >&2
  fi
  jq -r '.[] | .number' <<<"$json" >> "$PAIRS"
}
append_search 'reviewed-by:@me' 'reviewed-by'
append_search 'commenter:@me' 'commenter'
sort -u "$PAIRS" > "$RUN_DIR/candidates.sorted.tsv"
mv "$RUN_DIR/candidates.sorted.tsv" "$PAIRS"
COUNT="$(wc -l < "$PAIRS" | tr -d '[:space:]')"
echo "Found $COUNT candidate open PR(s). Pulling details..."

OWNER="${REPO%%/*}"
NAME="${REPO##*/}"
CODE_PATH=""
CODE_CONTENT=""
while IFS= read -r p; do
  [ -n "$p" ] || continue
  raw="$(gh api "repos/$OWNER/$NAME/contents/$p" 2>/dev/null || true)"
  if [ -n "$raw" ]; then
    CODE_PATH="$p"
    CODE_CONTENT="$(printf '%s' "$raw" | jq -r '.content' | tr -d '\n' | base64 -d)"
    break
  fi
done < <(jq -r '.codeowners_paths[]' "$CONFIG")

PRS='[]'
while IFS= read -r N; do
  [ -n "$N" ] || continue
  VIEW="$(gh pr view "$N" --repo "$REPO" --json number,title,url,isDraft,state,author,files 2>/dev/null || true)"
  [ -n "$VIEW" ] || { echo "Skip ${REPO}#${N} (view failed)" >&2; continue; }
  IC="$(gh api --paginate "repos/$OWNER/$NAME/issues/$N/comments" --jq "[.[] | select(.user.login == \"$ME\") | {author: .user.login, body: .body, created_at: .created_at}]" 2>/dev/null || echo '[]')"
  RC="$(gh api --paginate "repos/$OWNER/$NAME/pulls/$N/comments" --jq "[.[] | select(.user.login == \"$ME\") | {author: .user.login, body: .body, created_at: .created_at}]" 2>/dev/null || echo '[]')"
  RV="$(gh api --paginate "repos/$OWNER/$NAME/pulls/$N/reviews" --jq "[.[] | select(.user.login == \"$ME\") | {author: .user.login, state: .state, body: .body, submitted_at: .submitted_at}]" 2>/dev/null || echo '[]')"
  ITEM="$(jq -n --arg repo "$REPO" --argjson v "$VIEW" --argjson ic "$IC" --argjson rc "$RC" --argjson rv "$RV" '{repo:$repo, pr_number:$v.number, pr_title:$v.title, pr_url:$v.url, is_draft:$v.isDraft, state:$v.state, author:$v.author.login, dina_reviews:$rv, dina_comments:$ic, dina_review_comments:$rc, changed_files:($v.files | map(.path // .filename))}')"
  PRS="$(jq --argjson it "$ITEM" '. + [$it]' <<<"$PRS")"
done < "$PAIRS"

OUT="$RUN_DIR/raw-prs.json"
GEN="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
RESULT="$(jq -n --arg stage "raw-collection" --arg run "$RUN_ID" --arg gen "$GEN" --arg me "$ME" --arg repo "$REPO" --arg cp "$CODE_PATH" --arg cc "$CODE_CONTENT" --argjson prs "$PRS" '{stage:$stage, version:1, run_id:$run, generated_at:$gen, me:$me, repo:$repo, codeowners:{path:$cp, content:$cc}, prs:$prs}')"
printf '%s' "$RESULT" | jq --arg gen "$GEN" --arg corr "approved-pr-codeowners-$RUN_ID" '{status:"success", result:., errors:[], metadata:{producer:"echo-finn-approved-pr-codeowners.collect-approved-prs", contractVersion:"1.0.0", format:"json", generatedAt:$gen, schema:"approved-pr-codeowners@1.0.0", correlationId:$corr}}' > "$OUT"
echo "Wrote $OUT"
echo "$OUT"
