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

// Index by command
function buildToolIndex(results) {
  const index = {};
  results.forEach(tool => {
    index[tool.command] = tool;
  });
  return index;
}

const toolIdx39 = buildToolIndex(beta39.results);
const toolIdx3 = buildToolIndex(beta3.results);

// Check for detailed parameter/description changes
const priorityNamespaces = ['functions', 'storage', 'servicebus', 'compute', 'monitor', 'kusto'];
const allCommands = new Set([...Object.keys(toolIdx39), ...Object.keys(toolIdx3)]);

console.log('=== DETAILED PARAMETER & DESCRIPTION CHANGES ===\n');

const changes = {
  parameterAdded: [],
  parameterRemoved: [],
  parameterRenamed: [],
  descriptionChanged: [],
  annotationChanged: [],
  sameTool: []
};

allCommands.forEach(cmd => {
  const tool39 = toolIdx39[cmd];
  const tool3 = toolIdx3[cmd];

  if (!tool39 || !tool3) {
    return; // Skip tools that don't exist in both versions
  }

  // Compare descriptions
  const desc39 = (tool39.description || '').trim();
  const desc3 = (tool3.description || '').trim();
  if (desc39 !== desc3) {
    changes.descriptionChanged.push({
      command: cmd,
      desc39: desc39.substring(0, 100),
      desc3: desc3.substring(0, 100)
    });
  }

  // Compare parameters
  const params39 = (tool39.option || []).map(o => o.name).sort();
  const params3 = (tool3.option || []).map(o => o.name).sort();

  const added = params3.filter(p => !params39.includes(p));
  const removed = params39.filter(p => !params3.includes(p));

  if (added.length > 0) {
    changes.parameterAdded.push({
      command: cmd,
      added
    });
  }

  if (removed.length > 0) {
    changes.parameterRemoved.push({
      command: cmd,
      removed
    });
  }

  // Check if parameters exist but descriptions changed
  if (params39.length === params3.length && JSON.stringify(params39) === JSON.stringify(params3)) {
    const paramDiffs = [];
    params39.forEach(pname => {
      const p39 = tool39.option.find(o => o.name === pname);
      const p3 = tool3.option.find(o => o.name === pname);
      if (p39 && p3 && (p39.description || '') !== (p3.description || '')) {
        paramDiffs.push({
          name: pname,
          desc39: (p39.description || '').substring(0, 60),
          desc3: (p3.description || '').substring(0, 60)
        });
      }
    });
    if (paramDiffs.length > 0) {
      changes.annotationChanged.push({
        command: cmd,
        paramDiffs
      });
    }
  }

  if (added.length === 0 && removed.length === 0 && desc39 === desc3) {
    changes.sameTool.push(cmd);
  }
});

console.log(`Total tools: ${allCommands.size}`);
console.log(`Identical tools: ${changes.sameTool.length}`);
console.log(`Tools with description changes: ${changes.descriptionChanged.length}`);
console.log(`Tools with parameter additions: ${changes.parameterAdded.length}`);
console.log(`Tools with parameter removals: ${changes.parameterRemoved.length}`);
console.log(`Tools with parameter description changes: ${changes.annotationChanged.length}`);

if (changes.descriptionChanged.length > 0) {
  console.log('\n=== DESCRIPTION CHANGES ===');
  changes.descriptionChanged.slice(0, 10).forEach(item => {
    console.log(`\n${item.command}:`);
    console.log(`  2.x: ${item.desc39}...`);
    console.log(`  3.x: ${item.desc3}...`);
  });
  if (changes.descriptionChanged.length > 10) {
    console.log(`\n... and ${changes.descriptionChanged.length - 10} more description changes`);
  }
}

if (changes.parameterAdded.length > 0) {
  console.log('\n=== PARAMETER ADDITIONS ===');
  changes.parameterAdded.slice(0, 10).forEach(item => {
    console.log(`${item.command}: added [${item.added.join(', ')}]`);
  });
  if (changes.parameterAdded.length > 10) {
    console.log(`... and ${changes.parameterAdded.length - 10} more`);
  }
}

if (changes.parameterRemoved.length > 0) {
  console.log('\n=== PARAMETER REMOVALS ===');
  changes.parameterRemoved.slice(0, 10).forEach(item => {
    console.log(`${item.command}: removed [${item.removed.join(', ')}]`);
  });
  if (changes.parameterRemoved.length > 10) {
    console.log(`... and ${changes.parameterRemoved.length - 10} more`);
  }
}

if (changes.annotationChanged.length > 0) {
  console.log('\n=== PARAMETER ANNOTATION CHANGES ===');
  changes.annotationChanged.slice(0, 10).forEach(item => {
    console.log(`\n${item.command}:`);
    item.paramDiffs.forEach(pd => {
      console.log(`  ${pd.name}:`);
      console.log(`    2.x: ${pd.desc39}...`);
      console.log(`    3.x: ${pd.desc3}...`);
    });
  });
  if (changes.annotationChanged.length > 10) {
    console.log(`\n... and ${changes.annotationChanged.length - 10} more parameter annotation changes`);
  }
}

// Save full report
fs.writeFileSync('detailed-diff.json', JSON.stringify(changes, null, 2));
console.log('\n\nFull report saved to: detailed-diff.json');
