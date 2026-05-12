#!/usr/bin/env node
// Generates CLI example commands from the most recent tools-list.json
// Output: alphabetical by namespace, then tool ΓÇö required params bare, optional in brackets

const fs = require('fs');
const path = require('path');

const GLOBAL_SWITCHES = new Set([
  '--subscription', '--tenant', '--tenant-id',
  '--auth-method', '--retry-delay', '--retry-max-delay',
  '--retry-max-retries', '--retry-mode', '--retry-network-timeout',
  '--learn'
]);

function findLatestToolsList(baseDir) {
  const dirs = fs.readdirSync(baseDir, { withFileTypes: true })
    .filter(d => d.isDirectory() && d.name.startsWith('3.0.0-beta.'))
    .map(d => d.name)
    .sort((a, b) => {
      const numA = parseInt(a.match(/beta\.(\d+)/)?.[1] || '0', 10);
      const numB = parseInt(b.match(/beta\.(\d+)/)?.[1] || '0', 10);
      return numB - numA;
    });

  if (dirs.length === 0) throw new Error('No 3.0.0-beta.* directories found');
  const toolsPath = path.join(baseDir, dirs[0], 'tools-list.json');
  if (!fs.existsSync(toolsPath)) throw new Error(`No tools-list.json in ${dirs[0]}`);
  console.log(`Using: ${dirs[0]}/tools-list.json\n`);
  return toolsPath;
}

function buildCliCommand(tool) {
  const cmd = `azmcp ${tool.command}`;
  const options = (tool.option || []).filter(o => !GLOBAL_SWITCHES.has(o.name));

  const required = options.filter(o => o.required === true);
  const optional = options.filter(o => o.required !== true);

  // Sort each group alphabetically
  required.sort((a, b) => a.name.localeCompare(b.name));
  optional.sort((a, b) => a.name.localeCompare(b.name));

  const parts = [cmd];

  for (const p of required) {
    const placeholder = p.name.replace(/^--/, '');
    parts.push(`  ${p.name} <${placeholder}>`);
  }

  for (const p of optional) {
    const placeholder = p.name.replace(/^--/, '');
    parts.push(`  [${p.name} <${placeholder}>]`);
  }

  return parts.join(' \\\n');
}

function getNamespace(command) {
  // namespace = first word(s) before the action verb (last word)
  const words = command.split(' ');
  return words.slice(0, -1).join(' ') || words[0];
}

// Main
const baseDir = __dirname;
const toolsPath = findLatestToolsList(baseDir);
const data = JSON.parse(fs.readFileSync(toolsPath, 'utf8'));
const tools = data.results || [];

// Sort by command (alphabetical by namespace then tool)
tools.sort((a, b) => a.command.localeCompare(b.command));

// Group by namespace
let currentNamespace = null;
const lines = [];

for (const tool of tools) {
  const ns = getNamespace(tool.command);
  if (ns !== currentNamespace) {
    currentNamespace = ns;
    if (lines.length > 0) lines.push('');
    lines.push(`## ${ns}`);
    lines.push('');
  }

  lines.push('```console');
  lines.push(buildCliCommand(tool));
  lines.push('```');
  lines.push('');
}

const output = lines.join('\n');
const outPath = path.join(baseDir, 'cli-examples.md');
fs.writeFileSync(outPath, output, 'utf8');
console.log(`Generated ${tools.length} CLI examples ΓåÆ cli-examples.md`);
