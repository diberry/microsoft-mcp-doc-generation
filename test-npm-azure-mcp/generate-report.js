#!/usr/bin/env node

/**
 * generate-report.js — Deterministic markdown generation report
 *
 * Reads CLI metadata (cli-output.json, cli-namespace.json, cli-version.json)
 * and produces a scannable markdown report at generated/generation-report.md.
 *
 * No AI — pure code-based extraction. Diff-friendly, consistent every run.
 *
 * Usage:
 *   node generate-report.js [--cli-output <path>] [--cli-namespace <path>]
 *                           [--cli-version <path>] [--common-params <path>]
 *                           [--output <path>]
 */

const fs = require('fs');
const path = require('path');

// ─── Defaults ────────────────────────────────────────────────────

const DEFAULTS = {
  cliOutput: path.resolve(__dirname, '../generated/cli/cli-output.json'),
  cliNamespace: path.resolve(__dirname, '../generated/cli/cli-namespace.json'),
  cliVersion: path.resolve(__dirname, '../generated/cli/cli-version.json'),
  commonParams: path.resolve(__dirname, '../docs-generation/data/common-parameters.json'),
  output: path.resolve(__dirname, '../generated/generation-report.md')
};

// ─── Core functions (exported for testing) ───────────────────────

/**
 * Build a Set of common parameter names to exclude from non-global counts.
 * @param {Array} commonParamsArray - Array of {name, ...} objects
 * @returns {Set<string>}
 */
function loadCommonParams(commonParamsArray) {
  return new Set(commonParamsArray.map(p => p.name));
}

/**
 * Group tools by namespace (first word of the command string).
 * @param {Array} tools - Array of tool objects from cli-output.json results
 * @returns {Object} Map of namespace → tool[]
 */
function extractNamespaces(tools) {
  const namespaces = {};
  for (const tool of tools) {
    const ns = tool.command.split(' ')[0];
    if (!namespaces[ns]) namespaces[ns] = [];
    namespaces[ns].push(tool);
  }
  return namespaces;
}

/**
 * Compute parameter statistics for a single tool.
 * @param {Object} tool - Tool object with option[] array
 * @param {Set<string>} commonSet - Set of common param names to exclude
 * @returns {{ totalParams: number, nonGlobalParams: number, requiredParams: number, optionalParams: number }}
 */
function computeToolStats(tool, commonSet) {
  const options = tool.option || [];
  const totalParams = options.length;
  const nonGlobal = options.filter(o => !commonSet.has(o.name));
  const nonGlobalParams = nonGlobal.length;
  const requiredParams = nonGlobal.filter(o => o.required === true).length;
  const optionalParams = nonGlobalParams - requiredParams;
  return { totalParams, nonGlobalParams, requiredParams, optionalParams };
}

/**
 * Compute aggregate stats per namespace.
 * @param {Object} namespaceMap - Map of namespace → tool[]
 * @param {Set<string>} commonSet - Set of common param names
 * @returns {Object} Map of namespace → { toolCount, totalParams, nonGlobalParams }
 */
function computeNamespaceSummary(namespaceMap, commonSet) {
  const summary = {};
  for (const [ns, tools] of Object.entries(namespaceMap)) {
    let totalParams = 0;
    let nonGlobalParams = 0;
    for (const tool of tools) {
      const stats = computeToolStats(tool, commonSet);
      totalParams += stats.totalParams;
      nonGlobalParams += stats.nonGlobalParams;
    }
    summary[ns] = { toolCount: tools.length, totalParams, nonGlobalParams };
  }
  return summary;
}

/**
 * Generate the full markdown report string.
 * @param {{ cliOutput, cliNamespace, cliVersion, commonParams }} data
 * @returns {string} Markdown report
 */
function generateReport({ cliOutput, cliNamespace, cliVersion, commonParams }) {
  const commonSet = loadCommonParams(commonParams);
  const tools = cliOutput.results || [];
  const namespaceMap = extractNamespaces(tools);
  const nsSummary = computeNamespaceSummary(namespaceMap, commonSet);
  const sortedNamespaces = Object.keys(namespaceMap).sort();

  const version = cliVersion.version || 'unknown';
  const date = new Date().toISOString().slice(0, 10);
  const totalNamespaces = sortedNamespaces.length;
  const totalTools = tools.length;

  const lines = [];

  // ── Header ──
  lines.push('# Generation Report');
  lines.push('');
  lines.push(`- **@azure/mcp version:** ${version}`);
  lines.push(`- **Generation date:** ${date}`);
  lines.push(`- **Total namespaces:** ${totalNamespaces}`);
  lines.push(`- **Total tools:** ${totalTools}`);
  lines.push('');

  // ── Namespace Summary Table ──
  lines.push('## Namespace Summary');
  lines.push('');
  lines.push('| Namespace | Tools | Total Params | Non-Global Params |');
  lines.push('| --- | --- | --- | --- |');
  for (const ns of sortedNamespaces) {
    const s = nsSummary[ns];
    lines.push(`| ${ns} | ${s.toolCount} | ${s.totalParams} | ${s.nonGlobalParams} |`);
  }
  lines.push('');

  // ── Tool Detail ──
  lines.push('## Tool Detail');
  lines.push('');
  for (const ns of sortedNamespaces) {
    const nsTools = namespaceMap[ns].sort((a, b) => a.name.localeCompare(b.name));
    lines.push(`### ${ns} (${nsTools.length} tools)`);
    lines.push('');
    lines.push('| Tool | Command | Required | Optional | Total Non-Global |');
    lines.push('| --- | --- | --- | --- | --- |');
    for (const tool of nsTools) {
      const stats = computeToolStats(tool, commonSet);
      lines.push(`| ${tool.name} | ${tool.command} | ${stats.requiredParams} | ${stats.optionalParams} | ${stats.nonGlobalParams} |`);
    }
    lines.push('');
  }

  return lines.join('\n');
}

// ─── CLI entry point ─────────────────────────────────────────────

function parseArgs(argv) {
  const args = {};
  for (let i = 2; i < argv.length; i += 2) {
    const key = argv[i].replace(/^--/, '');
    args[key] = argv[i + 1];
  }
  return args;
}

function readJson(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  // Handle files that have non-JSON prefix (e.g., npm script output)
  const firstBrace = raw.indexOf('{');
  const firstBracket = raw.indexOf('[');
  let start = -1;
  if (firstBrace >= 0 && firstBracket >= 0) start = Math.min(firstBrace, firstBracket);
  else if (firstBrace >= 0) start = firstBrace;
  else if (firstBracket >= 0) start = firstBracket;
  if (start < 0) throw new Error(`No JSON found in ${filePath}`);
  return JSON.parse(raw.slice(start));
}

function main() {
  const args = parseArgs(process.argv);
  const paths = {
    cliOutput: args['cli-output'] || DEFAULTS.cliOutput,
    cliNamespace: args['cli-namespace'] || DEFAULTS.cliNamespace,
    cliVersion: args['cli-version'] || DEFAULTS.cliVersion,
    commonParams: args['common-params'] || DEFAULTS.commonParams,
    output: args['output'] || DEFAULTS.output
  };

  // Validate inputs exist
  for (const [label, p] of Object.entries(paths)) {
    if (label === 'output') continue;
    if (!fs.existsSync(p)) {
      console.error(`ERROR: ${label} not found: ${p}`);
      process.exit(1);
    }
  }

  const data = {
    cliOutput: readJson(paths.cliOutput),
    cliNamespace: readJson(paths.cliNamespace),
    cliVersion: readJson(paths.cliVersion),
    commonParams: readJson(paths.commonParams)
  };

  const report = generateReport(data);

  // Ensure output directory exists
  const outDir = path.dirname(paths.output);
  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  fs.writeFileSync(paths.output, report, 'utf8');
  console.log(`Report written to ${paths.output}`);
  console.log(`  Namespaces: ${Object.keys(extractNamespaces(data.cliOutput.results || [])).length}`);
  console.log(`  Tools: ${(data.cliOutput.results || []).length}`);
}

if (require.main === module) {
  main();
}

module.exports = {
  loadCommonParams,
  extractNamespaces,
  computeToolStats,
  computeNamespaceSummary,
  generateReport
};
