const fs = require('fs');

function extractJSON(content) {
  const startIdx = content.indexOf('{');
  const endIdx = content.lastIndexOf('}');
  if (startIdx === -1 || endIdx === -1) return null;
  return JSON.parse(content.substring(startIdx, endIdx + 1));
}

// Load versions
const beta1 = extractJSON(fs.readFileSync('cli-beta1.json', 'utf8'));
const beta40 = extractJSON(fs.readFileSync('cli-beta40.json', 'utf8'));
const beta3v1 = JSON.parse(fs.readFileSync('../generated/cli/cli-output.json', 'utf8'));

console.log('=== 2.x vs 3.x REAL COMPARISON ===\n');
console.log(`2.0.0-beta.1 tools: ${beta1.results?.length || 0}`);
console.log(`2.0.0-beta.40 tools: ${beta40.results?.length || 0}`);
console.log(`3.0.0-beta.1 tools: ${beta3v1.results?.length || 0}`);
console.log('\n');

// Check JSON structure differences
console.log('JSON Structure (2.x beta.1):');
console.log(`  Has "status": ${beta1.status ? 'yes' : 'no'}`);
console.log(`  Has "results": ${Array.isArray(beta1.results) ? 'yes' : 'no'}`);
console.log(`  Has "namespaces": ${Array.isArray(beta1.namespaces) ? 'yes' : 'no'}`);

console.log('\nJSON Structure (3.x beta.1):');
console.log(`  Has "status": ${beta3v1.status ? 'yes' : 'no'}`);
console.log(`  Has "results": ${Array.isArray(beta3v1.results) ? 'yes' : 'no'}`);
console.log(`  Has "namespaces": ${Array.isArray(beta3v1.namespaces) ? 'yes' : 'no'}`);

// Index by namespace and command
function buildIndex(results) {
  const index = {};
  if (!results) return index;
  results.forEach(tool => {
    const parts = tool.command.split(' ');
    const ns = parts[0] || 'unknown';
    if (!index[ns]) index[ns] = {};
    index[ns][tool.command] = tool;
  });
  return index;
}

const idx1 = buildIndex(beta1.results);
const idx40 = buildIndex(beta40.results);
const idx3v1 = buildIndex(beta3v1.results);

// Get all unique namespaces
const allNS = new Set([...Object.keys(idx40), ...Object.keys(idx3v1)]);
const namespaces = Array.from(allNS).sort();

console.log('\n=== NAMESPACE INVENTORY ===');
console.log(`2.x (beta.40) namespaces: ${Object.keys(idx40).length}`);
console.log(`3.x (beta.1) namespaces: ${Object.keys(idx3v1).length}`);

// Compare namespace-level changes
const changes = {
  new: [],
  removed: [],
  toolCountChange: []
};

namespaces.forEach(ns => {
  const tools40 = idx40[ns] ? Object.keys(idx40[ns]) : [];
  const tools3 = idx3v1[ns] ? Object.keys(idx3v1[ns]) : [];
  
  if (tools40.length === 0 && tools3.length > 0) {
    changes.new.push({ ns, count: tools3.length });
  } else if (tools40.length > 0 && tools3.length === 0) {
    changes.removed.push({ ns, count: tools40.length });
  } else if (tools40.length !== tools3.length) {
    changes.toolCountChange.push({ ns, from: tools40.length, to: tools3.length });
  }
});

console.log(`\n=== NAMESPACE LEVEL CHANGES ===`);
console.log(`New namespaces: ${changes.new.length}`);
changes.new.forEach(item => console.log(`  + ${item.ns} (${item.count} tools)`));

console.log(`\nRemoved namespaces: ${changes.removed.length}`);
changes.removed.forEach(item => console.log(`  - ${item.ns} (was ${item.count} tools)`));

console.log(`\nNamespaces with changed tool counts: ${changes.toolCountChange.length}`);
changes.toolCountChange.forEach(item => {
  console.log(`  ${item.ns}: ${item.from} → ${item.to} (${item.to - item.from > 0 ? '+' : ''}${item.to - item.from})`);
});

// Sample some parameter differences
console.log(`\n=== PARAMETER CHANGES (Priority Namespaces) ===`);
const priority = ['functions', 'storage', 'servicebus', 'compute', 'monitor', 'kusto'];

priority.forEach(ns => {
  const cmds40 = idx40[ns] ? Object.keys(idx40[ns]) : [];
  const cmds3 = idx3v1[ns] ? Object.keys(idx3v1[ns]) : [];
  
  console.log(`\n${ns}:`);
  console.log(`  Tools: ${cmds40.length} → ${cmds3.length}`);
  
  // Check for parameter changes in common commands
  const commonCmds = cmds40.filter(cmd => cmds3.includes(cmd));
  let paramChanges = 0;
  commonCmds.forEach(cmd => {
    const tool40 = idx40[ns][cmd];
    const tool3 = idx3v1[ns][cmd];
    const params40 = (tool40.option || []).map(o => o.name).sort();
    const params3 = (tool3.option || []).map(o => o.name).sort();
    
    if (JSON.stringify(params40) !== JSON.stringify(params3)) {
      paramChanges++;
    }
  });
  
  if (paramChanges > 0) {
    console.log(`  Commands with parameter changes: ${paramChanges}/${commonCmds.length}`);
  }
});

// Save summary report
const summary = {
  version2: '2.0.0-beta.40',
  version3: '3.0.0-beta.1',
  toolCount: { v2: beta40.results?.length || 0, v3: beta3v1.results?.length || 0 },
  namespaceCount: { v2: Object.keys(idx40).length, v3: Object.keys(idx3v1).length },
  changes
};

fs.writeFileSync('2x-vs-3x-summary.json', JSON.stringify(summary, null, 2));
console.log('\n\nFull summary saved to: 2x-vs-3x-summary.json');
