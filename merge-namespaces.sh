#!/bin/bash
# merge-namespaces.sh — Post-assembly merge for multi-namespace tool articles (AD-011)
#
# Reads brand-to-server-mapping.json merge groups, finds tool-family articles
# from generated-{namespace}/ directories, and merges them.
#
# Usage:
#   ./merge-namespaces.sh                    # Merge all configured groups
#   ./merge-namespaces.sh --dry-run          # Show what would be merged without writing
#
# Called automatically by start.sh after all namespace processing completes.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY_RUN="${1:-false}"
if [[ "$DRY_RUN" == "--dry-run" ]]; then DRY_RUN="true"; fi

cd "$ROOT_DIR"
DRY_RUN_FLAG="$DRY_RUN" node -e "
const fs = require('fs');
const path = require('path');

const rootDir = process.cwd();
const dryRun = process.env.DRY_RUN_FLAG === 'true';
const brandPath = path.join(rootDir, 'mcp-tools', 'data', 'brand-to-server-mapping.json');
const mappings = JSON.parse(fs.readFileSync(brandPath, 'utf8'));

// Find merge groups
const grouped = {};
for (const m of mappings) {
    if (!m.mergeGroup) continue;
    if (!grouped[m.mergeGroup]) grouped[m.mergeGroup] = [];
    grouped[m.mergeGroup].push({
        ns: m.mcpServerName,
        order: m.mergeOrder || 99,
        role: m.mergeRole || 'secondary'
    });
}

const groups = Object.entries(grouped);
if (groups.length === 0) { process.exit(0); }

console.log('');
console.log('===================================================================');
console.log('Post-Assembly: Namespace Merge (AD-011)');
console.log('===================================================================');

function resolveGeneratedDir(namespaceName) {
    const prefix = 'generated-' + namespaceName + '-';
    const candidates = fs.readdirSync(rootDir, { withFileTypes: true })
        .filter(entry => entry.isDirectory())
        .map(entry => entry.name)
        .filter(name => name === 'generated-' + namespaceName || (name.startsWith(prefix) && /^\d{8}T\d{9}Z$/i.test(name.slice(prefix.length))))
        .map(name => ({
            name,
            path: path.join(rootDir, name),
            mtimeMs: fs.statSync(path.join(rootDir, name)).mtimeMs
        }))
        .sort((a, b) => b.mtimeMs - a.mtimeMs || b.name.localeCompare(a.name));

    return candidates.length > 0 ? candidates[0].path : null;
}

function parseArticle(md) {
    const lines = md.split('\n');
    let header = [], tools = [], related = [], currentTool = [];
    let inHeader = true, inRelated = false, foundH2 = false;
    for (const line of lines) {
        if (line.startsWith('## ')) {
            if (inHeader) inHeader = false;
            if (line.trim().toLowerCase() === '## related content') {
                if (foundH2 && currentTool.length) tools.push(currentTool.join('\n').trimEnd());
                currentTool = [];
                inRelated = true;
                continue;
            }
            if (foundH2 && currentTool.length) tools.push(currentTool.join('\n').trimEnd());
            currentTool = [line];
            foundH2 = true;
        } else if (inRelated) {
            related.push(line);
        } else if (inHeader) {
            header.push(line);
        } else {
            currentTool.push(line);
        }
    }
    if (!inRelated && foundH2 && currentTool.length) tools.push(currentTool.join('\n').trimEnd());
    return { header: header.join('\n'), tools, related: related.join('\n').trimEnd() };
}

for (const [groupName, members] of groups) {
    members.sort((a, b) => a.order - b.order);
    const primary = members.find(m => m.role === 'primary');
    if (!primary) {
        console.log('  WARNING: Group ' + groupName + ' has no primary, skipping.');
        continue;
    }

    // Loads {ns}{suffix}.md for every member. Returns {missing, missingNs, articles, generatedDirs}.
    function loadVariant(suffix) {
        const articles = {};
        const generatedDirs = {};
        for (const m of members) {
            const generatedDir = resolveGeneratedDir(m.ns);
            const articlePath = generatedDir ? path.join(generatedDir, 'tool-family', m.ns + suffix + '.md') : null;
            if (articlePath && fs.existsSync(articlePath)) {
                generatedDirs[m.ns] = generatedDir;
                articles[m.ns] = fs.readFileSync(articlePath, 'utf8');
            } else {
                return { missing: true, missingNs: m.ns };
            }
        }
        return { missing: false, articles, generatedDirs };
    }

    // Merge: primary header + all tools (in order) + primary related content.
    // Identical rules for the plain and -cli variants.
    function buildMerged(articles) {
        const primaryParsed = parseArticle(articles[primary.ns]);
        let allTools = [...primaryParsed.tools];
        for (const m of members) {
            if (m.role === 'primary') continue;
            allTools = allTools.concat(parseArticle(articles[m.ns]).tools);
        }
        const totalTools = allTools.length;
        const updatedHeader = primaryParsed.header.replace(/tool_count:\s*\d+/, 'tool_count: ' + totalTools);
        const merged = updatedHeader + '\n' + allTools.join('\n\n') + '\n\n## Related content\n\n' + primaryParsed.related + '\n';
        const toolCounts = members.map(m => m.ns + ':' + parseArticle(articles[m.ns]).tools.length).join(' + ');
        return { merged, totalTools, toolCounts };
    }

    // Merges one variant and writes {primary.ns}{suffix}.md. suffix '' = canonical, '-cli' = CLI-tab variant.
    // required=true (canonical): a write error propagates and aborts (fail-fast). required=false (-cli):
    // best-effort — a write error is caught and logged so it never blocks the canonical merge.
    function mergeVariant(suffix, required) {
        const loaded = loadVariant(suffix);
        if (loaded.missing) {
            const label = suffix === '' ? '' : ' ' + suffix.replace(/^-/, '') + ' variant for';
            console.log('  Skipping' + label + ' group ' + groupName + ': ' + loaded.missingNs + suffix + '.md not found');
            return required ? false : true;
        }
        const { merged, totalTools, toolCounts } = buildMerged(loaded.articles);
        const outputPath = path.join(loaded.generatedDirs[primary.ns], 'tool-family', primary.ns + suffix + '.md');
        const memberLabel = members.map(m => m.ns + suffix).join(' + ');
        if (dryRun) {
            console.log('  DRY RUN: Would merge ' + memberLabel + ' -> ' + primary.ns + suffix + '.md');
            console.log('           ' + totalTools + ' tools (' + toolCounts + ')');
        } else {
            try {
                fs.writeFileSync(outputPath, merged);
            } catch (err) {
                if (required) throw err;
                console.log('  WARNING: Skipping ' + suffix.replace(/^-/, '') + ' variant for group ' + groupName + ': write failed (' + err.message + ')');
                return true;
            }
            console.log('  Merged: ' + memberLabel + ' -> ' + primary.ns + suffix + '.md');
            console.log('          ' + totalTools + ' tools (' + toolCounts + ')');
        }
        return true;
    }

    // Canonical (plain, no CLI tabs) — required. If missing, skip the whole group.
    if (!mergeVariant('', true)) continue;

    // CLI-tab variant — must follow the same merge rules. Best-effort: skip with a
    // notice if any member's -cli.md is absent (never blocks the canonical merge).
    mergeVariant('-cli', false);
}
console.log('');
"
