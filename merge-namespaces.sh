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

    // Load articles from generated-{ns}/tool-family/{ns}.md
    const articles = {};
    let missing = false;
    for (const m of members) {
        const articlePath = path.join(rootDir, 'generated-' + m.ns, 'tool-family', m.ns + '.md');
        if (fs.existsSync(articlePath)) {
            articles[m.ns] = fs.readFileSync(articlePath, 'utf8');
        } else {
            console.log('  Skipping group ' + groupName + ': ' + m.ns + '.md not found');
            missing = true;
            break;
        }
    }
    if (missing) continue;

    // Merge: primary header + all tools (in order) + primary related content
    const primaryParsed = parseArticle(articles[primary.ns]);
    let allTools = [...primaryParsed.tools];
    for (const m of members) {
        if (m.role === 'primary') continue;
        const parsed = parseArticle(articles[m.ns]);
        allTools = allTools.concat(parsed.tools);
    }

    const totalTools = allTools.length;
    const updatedHeader = primaryParsed.header.replace(/tool_count:\s*\d+/, 'tool_count: ' + totalTools);
    const merged = updatedHeader + '\n' + allTools.join('\n\n') + '\n\n## Related content\n\n' + primaryParsed.related + '\n';

    const outputPath = path.join(rootDir, 'generated-' + primary.ns, 'tool-family', primary.ns + '.md');
    const toolCounts = members.map(m => m.ns + ':' + parseArticle(articles[m.ns]).tools.length).join(' + ');

    if (dryRun) {
        console.log('  DRY RUN: Would merge ' + members.map(m => m.ns).join(' + ') + ' -> ' + primary.ns + '.md');
        console.log('           ' + totalTools + ' tools (' + toolCounts + ')');
    } else {
        fs.writeFileSync(outputPath, merged);
        console.log('  Merged: ' + members.map(m => m.ns).join(' + ') + ' -> ' + primary.ns + '.md');
        console.log('          ' + totalTools + ' tools (' + toolCounts + ')');
    }
}
console.log('');
"
