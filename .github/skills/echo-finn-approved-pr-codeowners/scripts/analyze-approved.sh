#!/usr/bin/env bash
# analyze-approved.sh — Step 2 of echo-finn-approved-pr-codeowners.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="$SCRIPT_DIR/../config/approved-pr-codeowners.config.json"
DATA_DIR=""
RUN_ID=""
RAW=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --data-dir) DATA_DIR="$2"; shift 2 ;;
    --config) CONFIG="$2"; shift 2 ;;
    --run-id) RUN_ID="$2"; shift 2 ;;
    --raw-prs) RAW="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
command -v jq >/dev/null 2>&1 || { echo "jq is required" >&2; exit 3; }
PYTHON_BIN="${PYTHON:-}"
if [ -z "$PYTHON_BIN" ]; then
  if command -v python3 >/dev/null 2>&1; then PYTHON_BIN="python3"
  elif command -v python >/dev/null 2>&1; then PYTHON_BIN="python"
  else echo "python3 or python is required" >&2; exit 3
  fi
fi
[ -f "$CONFIG" ] || { echo "Config not found: $CONFIG" >&2; exit 4; }
[ -n "$DATA_DIR" ] || DATA_DIR="$(jq -r '.data_dir' "$CONFIG")"
RUNS_SUBDIR="$(jq -r '.runs_subdir' "$CONFIG")"
resolve_run_dir() { local base="$DATA_DIR/$RUNS_SUBDIR"; if [ -n "$RUN_ID" ]; then echo "$base/$RUN_ID"; else ls -1d "$base"/* 2>/dev/null | sort -r | head -n1; fi; }
[ -n "$RAW" ] || RAW="$(resolve_run_dir)/raw-prs.json"
[ -f "$RAW" ] || { echo "raw-prs.json not found: $RAW" >&2; exit 4; }
"$PYTHON_BIN" - "$CONFIG" "$RAW" <<'PY'
import json, re, sys, pathlib, datetime
cfg=json.load(open(sys.argv[1], encoding='utf-8'))
raw_path=pathlib.Path(sys.argv[2])
raw_envelope=json.load(open(raw_path, encoding='utf-8'))
raw=raw_envelope.get('result', raw_envelope)
approval=re.compile(cfg['approval_comment_regex'], re.I)
def strip_code(s):
    s=s or ''
    s=re.sub(r'(?s)```.*?```',' ',s)
    s=re.sub(r'(?s)~~~.*?~~~',' ',s)
    return re.sub(r'`[^`]*`',' ',s)
def rx_from_pat(pat):
    p=pat.replace('\\ ', ' ').strip()
    if not p or p.startswith('#') or p.startswith('!'): return None
    anchored=p.startswith('/')
    if anchored: p=p[1:]
    dironly=p.endswith('/')
    if dironly: p=p.rstrip('/')
    out=[]; i=0
    while i < len(p):
        c=p[i]
        if c=='*':
            if i+1 < len(p) and p[i+1]=='*': out.append('.*'); i+=2; continue
            out.append('[^/]*')
        elif c=='?': out.append('[^/]')
        else: out.append(re.escape(c))
        i+=1
    body=''.join(out)
    if dironly: body += r'(?:/.*)?'
    has_slash='/' in p
    # GitHub CODEOWNERS follows gitignore anchoring: leading or middle slash anchors to repo root; slash-less patterns match at any depth.
    return re.compile(('^' if (anchored or has_slash) else r'(^|.*/)')+body+r'$', re.I)
def parse_codeowners(text):
    rules=[]
    for n,line in enumerate((text or '').splitlines(),1):
        t=line.strip()
        if not t or t.startswith('#'): continue
        parts=t.split()
        if len(parts)<2: continue
        owners=[x for x in parts[1:] if x.startswith('@')]
        r=rx_from_pat(parts[0])
        if owners and r: rules.append({'line':n,'pattern':parts[0],'rx':r,'owners':owners})
    return rules
rules=parse_codeowners(raw.get('codeowners',{}).get('content',''))
approved=[]; skipped=[]; me=(raw.get('me') or '').lower()
for pr in raw.get('prs',[]):
    if pr.get('state') != 'OPEN': skipped.append({'pr_number':pr.get('pr_number'),'reason':'not-open'}); continue
    if (pr.get('author') or '').lower()==me: skipped.append({'pr_number':pr.get('pr_number'),'reason':'authored-by-dina'}); continue
    formal=any(r.get('state')=='APPROVED' for r in pr.get('dina_reviews',[]))
    comment=any(approval.search(strip_code(c.get('body'))) for c in (pr.get('dina_comments',[])+pr.get('dina_review_comments',[])))
    if not (formal or comment): skipped.append({'pr_number':pr.get('pr_number'),'reason':'no-dina-approval'}); continue
    bykey={}
    for f in pr.get('changed_files',[]):
        path=(f or '').replace('\\','/')
        match=None
        for rule in rules:
            if rule['rx'].match(path): match=rule
        if not match: continue
        for owner in match['owners']:
            if owner.lstrip('@').lower()==me: continue
            bykey.setdefault(owner.lower(), {'gh_handle':owner,'is_team':'/' in owner})
    approved.append({'repo':pr.get('repo'),'pr_number':pr.get('pr_number'),'pr_title':pr.get('pr_title'),'pr_url':pr.get('pr_url'),'approval_source':'review' if formal else 'comment','changed_files':pr.get('changed_files',[]),'owners':list(bykey.values())})
out={'stage':'findings','version':1,'run_id':raw.get('run_id'),'generated_at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'repo':raw.get('repo'),'dina_login':raw.get('me'),'codeowners_path':raw.get('codeowners',{}).get('path'),'approved_prs':approved,'skipped':skipped}
out_path=raw_path.parent/'findings.json'
envelope={'status':'success','result':out,'errors':[],'metadata':{'producer':'echo-finn-approved-pr-codeowners.analyze-approved','contractVersion':'1.0.0','format':'json','generatedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'schema':'approved-pr-codeowners@1.0.0','correlationId':'approved-pr-codeowners-' + str(raw.get('run_id'))}}
out_path.write_text(json.dumps(envelope, indent=2), encoding='utf-8')
print(f'Wrote {out_path} (approved={len(approved)} skipped={len(skipped)}).')
print(out_path)
PY
