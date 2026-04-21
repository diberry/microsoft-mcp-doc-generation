const fs = require('fs');

function extractJSON(content) {
  const startIdx = content.indexOf('{');
  const endIdx = content.lastIndexOf('}');
  if (startIdx === -1 || endIdx === -1) return null;
  return JSON.parse(content.substring(startIdx, endIdx + 1));
}

// Load 2.x (beta.39) data
const beta39 = extractJSON(fs.readFileSync('39.json', 'utf8'));

// Load 3.x (beta.1 current) data
const beta3 = JSON.parse(fs.readFileSync('../generated/cli/cli-output.json', 'utf8'));

// Index by namespace and command
function buildIndex(results) {
  const index = {};
  results.forEach(tool => {
    const parts = tool.command.split(' ');
    const ns = parts[0] || 'unknown';
    if (!index[ns]) index[ns] = {};
    index[ns][tool.command] = tool;
  });
  return index;
}

const idx39 = buildIndex(beta39.results);
const idx3 = buildIndex(beta3.results);

// Get all unique namespaces
const allNS = new Set([...Object.keys(idx39), ...Object.keys(idx3)]);
const namespaces = Array.from(allNS).sort();

console.log('=== VERSION COMPARISON: 2.0.0-beta.39 vs 3.0.0-beta.1 ===\n');
console.log('Total namespaces in either version:', namespaces.length);
console.log('2.x (beta.39) namespaces:', Object.keys(idx39).length);
console.log('3.x (beta.1) namespaces:', Object.keys(idx3).length);
console.log('\n');

// Get high-priority namespaces from the task
const priority = ['functions', 'storage', 'servicebus', 'compute', 'monitor', 'kusto'];

console.log('=== PRIORITY NAMESPACES (from changelog) ===');
priority.forEach(ns => {
  const tools39 = idx39[ns] ? Object.keys(idx39[ns]).length : 0;
  const tools3 = idx3[ns] ? Object.keys(idx3[ns]).length : 0;
  console.log(`${ns}: 2.x=${tools39} tools, 3.x=${tools3} tools, change=${tools3-tools39}`);
});

console.log('\n');
console.log('=== ALL NAMESPACE CHANGES ===\n');

let changedCount = 0;
let newCount = 0;
let removedCount = 0;
const changes = {};

namespaces.forEach(ns => {
  const tools39 = idx39[ns] ? Object.keys(idx39[ns]) : [];
  const tools3 = idx3[ns] ? Object.keys(idx3[ns]) : [];
  
  if (tools39.length === 0 && tools3.length > 0) {
    console.log(`✨ NEW: ${ns} (${tools3.length} tools)`);
    newCount++;
    changes[ns] = { status: 'new', count39: 0, count3: tools3.length };
    return;
  }
  
  if (tools39.length > 0 && tools3.length === 0) {
    console.log(`❌ REMOVED: ${ns} (was ${tools39.length} tools)`);
    removedCount++;
    changes[ns] = { status: 'removed', count39: tools39.length, count3: 0 };
    return;
  }
  
  if (tools39.length !== tools3.length) {
    console.log(`📊 CHANGED: ${ns} (${tools39.length} → ${tools3.length} tools, diff=${tools3.length - tools39.length})`);
    changedCount++;
    changes[ns] = { status: 'changed', count39: tools39.length, count3: tools3.length };
  } else {
    changes[ns] = { status: 'same', count39: tools39.length, count3: tools3.length };
  }
});

console.log('\n=== SUMMARY ===');
console.log(`New: ${newCount} namespaces`);
console.log(`Removed: ${removedCount} namespaces`);
console.log(`Tool count changed: ${changedCount} namespaces`);

// Save for detailed analysis
fs.writeFileSync('diff-versions.json', JSON.stringify({
  version2: '2.0.0-beta.39',
  version3: '3.0.0-beta.1',
  summary: { newCount, removedCount, changedCount, totalNamespaces: namespaces.length },
  changes,
  namespaces: Array.from(allNS).sort()
}, null, 2));

console.log('\nDetailed diff saved to: diff-versions.json');
