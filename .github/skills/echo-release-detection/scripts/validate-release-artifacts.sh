#!/usr/bin/env bash
# Shared Echo+Finn release-artifact guard for Azure MCP and Azure Skills release sync.
set -euo pipefail

if repo_root="$(git rev-parse --show-toplevel 2>/dev/null)"; then
  cd "$repo_root"
elif [[ -f .git ]] && read -r gitdir_line < .git && [[ "$gitdir_line" == gitdir:* ]]; then
  gitdir="${gitdir_line#gitdir: }"
  if [[ "$gitdir" =~ ^([A-Za-z]):/(.*)$ ]]; then
    drive="${BASH_REMATCH[1],,}"
    export GIT_DIR="/mnt/$drive/${BASH_REMATCH[2]}"
    export GIT_WORK_TREE="$(pwd -P)"
    repo_root="$GIT_WORK_TREE"
  else
    export GIT_DIR="$gitdir"
    export GIT_WORK_TREE="$(pwd -P)"
    repo_root="$GIT_WORK_TREE"
  fi
else
  echo "validate-release-artifacts.sh must run from a git working tree." >&2
  exit 1
fi

metadata_names='^(cli-output\.json|cli-namespace\.json|cli-version\.json|namespace-mapping\.json)$'
probe_names='*echo-release-probe*|*release-probe*|*cli-extraction*|*mcp-cli-extract*|*azmcp-extract*|*azure-mcp-extract*'
offenses=()

is_excluded_path() {
  case "$1" in
    .git|.git/*|repos|repos/*|.worktrees|.worktrees/*) return 0 ;;
    *) return 1 ;;
  esac
}

# Marker: the upstream microsoft/azure-skills repo ships .github/plugins/azure-skills/CHANGELOG.md at its own root. Finding that path INSIDE this hub tree means someone cloned the upstream repo in-place instead of reading it via 'gh api' -- flag the clone root as a stray in-tree clone.
get_stray_azure_skills_clone_root() {
  local rel="$1"
  case "$rel" in
    .github/plugins/azure-skills|.github/plugins/azure-skills/*)
      printf '%s\n' '.'
      return 0
      ;;
    */.github/plugins/azure-skills|*/.github/plugins/azure-skills/*)
      printf '%s\n' "${rel%%/.github/plugins/azure-skills*}"
      return 0
      ;;
    *) return 1 ;;
  esac
}

is_baseline_archive() {
  git ls-tree -r --name-only HEAD | grep -E '\.(zip|nupkg)$' | grep -Fx -- "$1" >/dev/null 2>&1
}

while IFS= read -r -d '' file; do
  rel="${file#./}"
  if is_excluded_path "$rel"; then
    continue
  fi

  base="$(basename "$rel")"
  if [[ "$base" =~ $metadata_names ]]; then
    offenses+=("CLI metadata file outside repos/: $rel")
  fi

  case "$rel" in
    *.zip|*.nupkg)
      if ! is_baseline_archive "$rel"; then
        offenses+=("Release package/archive in hub: $rel")
      fi
      ;;
    */.github/plugins/azure-skills/CHANGELOG.md|.github/plugins/azure-skills/CHANGELOG.md)
      if clone_root="$(get_stray_azure_skills_clone_root "$rel")"; then
        offenses+=("Stray upstream azure-skills clone in hub: $clone_root")
      fi
      ;;
  esac
done < <(find . \( -path './.git' -o -path './repos' -o -path './.worktrees' \) -prune -o -type f -print0)

while IFS= read -r -d '' dir; do
  rel="${dir#./}"
  if is_excluded_path "$rel"; then
    continue
  fi

  if clone_root="$(get_stray_azure_skills_clone_root "$rel")"; then
    offenses+=("Stray upstream azure-skills clone in hub: $clone_root")
  fi
done < <(find . \( -path './.git' -o -path './repos' -o -path './.worktrees' \) -prune -o -type d -print0)

if [[ -d projects ]]; then
  while IFS= read -r -d '' dir; do
    rel="${dir#./}"
    name="$(basename "$rel")"
    case "$name" in
      *echo-release-probe*|*release-probe*|*cli-extraction*|*mcp-cli-extract*|*azmcp-extract*|*azure-mcp-extract*)
        offenses+=("Release probe/scratch directory under projects/: $rel")
        ;;
    esac
  done < <(find projects -type d -print0)
fi

while IFS= read -r staged; do
  [[ -z "$staged" ]] && continue
  rel="${staged//\\//}"
  if is_excluded_path "$rel"; then
    continue
  fi

  base="$(basename "$rel")"
  if [[ "$base" =~ $metadata_names ]]; then
    offenses+=("Staged CLI metadata file outside repos/: $rel")
  fi

  case "$rel" in
    *.zip|*.nupkg)
      if ! is_baseline_archive "$rel"; then
        offenses+=("Staged release package/archive in hub: $rel")
      fi
      ;;
  esac
done < <(git diff --cached --name-only --diff-filter=ACMR)

if (( ${#offenses[@]} > 0 )); then
  printf 'Release artifact validation failed:\n' >&2
  printf ' - %s\n' "${offenses[@]}" | sort -u >&2
  exit 1
fi

printf 'Release artifact validation passed: no new packages, probe/scratch directories, CLI metadata snapshots, or stray azure-skills clones found in the hub repo.\n'
