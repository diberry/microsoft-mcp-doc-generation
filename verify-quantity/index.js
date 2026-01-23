#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '..');
const GENERATED = path.join(ROOT, 'generated');

// Load configuration files
const brandMap = JSON.parse(
  fs.readFileSync(path.join(ROOT, 'docs-generation', 'brand-to-server-mapping.json'), 'utf8')
).reduce((map, item) => {
  map[item.mcpServerName] = item.fileName;
  return map;
}, {});

const compound = JSON.parse(
  fs.readFileSync(path.join(ROOT, 'docs-generation', 'compound-words.json'), 'utf8')
);

// Helper to convert tokens using compound word mappings
function slugToken(token) {
  return compound[token] || token;
}

// Generate expected filename base
function getExpectedFilenameBase(service, tokens) {
  const prefix = brandMap[service] || `azure-${service}`;
  if (!tokens || tokens.length === 0) {
    return prefix;
  }
  return `${prefix}-${tokens.map(slugToken).join('-')}`;
}

// Parse cli-output.json
function parseCliOutput() {
  const cliOutputPath = path.join(GENERATED, 'cli', 'cli-output.json');
  
  if (!fs.existsSync(cliOutputPath)) {
    console.error('❌ ERROR: cli-output.json not found at', cliOutputPath);
    process.exit(1);
  }

  const cliData = JSON.parse(fs.readFileSync(cliOutputPath, 'utf8'));
  const tools = [];

  // The structure is { status, message, results: [...] }
  if (!cliData.results || !Array.isArray(cliData.results)) {
    console.error('❌ ERROR: Invalid cli-output.json structure');
    process.exit(1);
  }

  for (const tool of cliData.results) {
    // Each tool has a "command" field like "acr registry list"
    const fullName = tool.command;
    const parts = fullName.split(/\s+/);
    const service = parts[0];
    const tokens = parts.slice(1);
    const filenameBase = getExpectedFilenameBase(service, tokens);

    tools.push({
      service,
      tokens,
      fullName,
      filenameBase,
      id: tool.id,
      name: tool.name,
      description: tool.description,
    });
  }

  return tools;
}

// Check file existence for all output types
function verifyFiles(tools) {
  const results = {
    total: tools.length,
    annotations: { found: 0, missing: [] },
    parameters: { found: 0, missing: [] },
    examplePrompts: { found: 0, missing: [] },
    paramAndAnnotation: { found: 0, missing: [] },
    completeTools: { found: 0, missing: [] },
  };

  const dirs = {
    annotations: path.join(GENERATED, 'annotations'),
    parameters: path.join(GENERATED, 'parameters'),
    examplePrompts: path.join(GENERATED, 'example-prompts'),
    paramAndAnnotation: path.join(GENERATED, 'param-and-annotation'),
    completeTools: path.join(GENERATED, 'tools'),
  };

  // Check directory existence
  for (const [key, dir] of Object.entries(dirs)) {
    if (!fs.existsSync(dir)) {
      console.warn(`⚠️  Directory not found: ${dir}`);
    }
  }

  for (const tool of tools) {
    const { filenameBase, fullName } = tool;

    // Check annotations
    const annotationFile = path.join(dirs.annotations, `${filenameBase}-annotations.md`);
    if (fs.existsSync(annotationFile)) {
      results.annotations.found++;
    } else {
      results.annotations.missing.push(fullName);
    }

    // Check parameters
    const parameterFile = path.join(dirs.parameters, `${filenameBase}-parameters.md`);
    if (fs.existsSync(parameterFile)) {
      results.parameters.found++;
    } else {
      results.parameters.missing.push(fullName);
    }

    // Check example prompts
    const examplePromptFile = path.join(dirs.examplePrompts, `${filenameBase}-example-prompts.md`);
    if (fs.existsSync(examplePromptFile)) {
      results.examplePrompts.found++;
    } else {
      results.examplePrompts.missing.push(fullName);
    }

    // Check param-and-annotation
    const paramAndAnnotationFile = path.join(dirs.paramAndAnnotation, `${filenameBase}-param-and-annotation.md`);
    if (fs.existsSync(paramAndAnnotationFile)) {
      results.paramAndAnnotation.found++;
    } else {
      results.paramAndAnnotation.missing.push(fullName);
    }

    // Check complete tools
    const completeToolFile = path.join(dirs.completeTools, `${filenameBase}.complete.md`);
    if (fs.existsSync(completeToolFile)) {
      results.completeTools.found++;
    } else {
      results.completeTools.missing.push(fullName);
    }
  }

  return results;
}

// Visualize results with bar charts
function visualizeResults(results) {
  console.log('\n' + '='.repeat(80));
  console.log('  AZURE MCP DOCUMENTATION GENERATION VERIFICATION REPORT');
  console.log('='.repeat(80));
  console.log(`\n📊 Total Tools: ${results.total}\n`);

  const categories = [
    { key: 'annotations', label: 'Annotations', icon: '📝' },
    { key: 'parameters', label: 'Parameters', icon: '⚙️' },
    { key: 'examplePrompts', label: 'Example Prompts', icon: '💡' },
    { key: 'paramAndAnnotation', label: 'Param+Annotation', icon: '📋' },
    { key: 'completeTools', label: 'Complete Tools', icon: '✅' },
  ];

  // Calculate percentages and create bars
  const maxLabelLength = Math.max(...categories.map(c => c.label.length));
  
  console.log('┌' + '─'.repeat(78) + '┐');
  
  for (const category of categories) {
    const data = results[category.key];
    const percentage = ((data.found / results.total) * 100).toFixed(1);
    const missing = data.missing.length;
    
    const label = `${category.icon} ${category.label}`.padEnd(maxLabelLength + 3);
    const stats = `${data.found}/${results.total}`.padStart(10);
    const pct = `${percentage}%`.padStart(7);
    
    // Progress bar (40 chars wide)
    const barWidth = 40;
    const filled = Math.round((data.found / results.total) * barWidth);
    const empty = barWidth - filled;
    const bar = '█'.repeat(filled) + '░'.repeat(empty);
    
    // Color coding
    const statusIcon = missing === 0 ? '✅' : missing < 10 ? '⚠️ ' : '❌';
    
    console.log(`│ ${label} │ ${bar} │ ${stats} ${pct} ${statusIcon} │`);
  }
  
  console.log('└' + '─'.repeat(78) + '┘');

  // Summary statistics
  console.log('\n📈 SUMMARY:');
  console.log('─'.repeat(80));
  
  const completeness = categories.map(c => ({
    name: c.label,
    missing: results[c.key].missing.length,
  }));

  completeness.sort((a, b) => b.missing - a.missing);

  for (const item of completeness) {
    const status = item.missing === 0 ? '✅ COMPLETE' : `❌ Missing ${item.missing}`;
    console.log(`  ${item.name.padEnd(20)}: ${status}`);
  }

  // Identify tools missing from ALL categories
  console.log('\n🔍 ANALYSIS:');
  console.log('─'.repeat(80));

  const allMissing = new Set();
  for (const category of categories) {
    for (const tool of results[category.key].missing) {
      allMissing.add(tool);
    }
  }

  const missingFromAll = [];
  for (const tool of allMissing) {
    const missingCount = categories.filter(c => results[c.key].missing.includes(tool)).length;
    if (missingCount === categories.length) {
      missingFromAll.push(tool);
    }
  }

  if (missingFromAll.length > 0) {
    console.log(`\n⚠️  Tools missing from ALL output types (${missingFromAll.length}):`);
    missingFromAll.forEach(tool => console.log(`     • ${tool}`));
  }

  // Find tools missing from specific categories only
  console.log('\n📊 BREAKDOWN BY MISSING TYPE:');
  for (const category of categories) {
    if (results[category.key].missing.length > 0) {
      console.log(`\n  ${category.icon} ${category.label} - Missing ${results[category.key].missing.length}:`);
      
      // Group by service
      const byService = {};
      for (const tool of results[category.key].missing) {
        const service = tool.split(' ')[0];
        if (!byService[service]) byService[service] = [];
        byService[service].push(tool);
      }

      for (const [service, tools] of Object.entries(byService).sort()) {
        console.log(`     ${service} (${tools.length}): ${tools.join(', ')}`);
      }
    }
  }

  console.log('\n' + '='.repeat(80) + '\n');
}

// Generate detailed report file
function generateReport(results, tools) {
  const reportPath = path.join(ROOT, './generated/missing-tools.md');
  
  let report = `# Azure MCP Documentation Generation - Missing Files Report\n\n`;
  report += `Generated: ${new Date().toISOString()}\n\n`;
  report += `## Overview\n\n`;
  report += `- **Total Tools**: ${results.total}\n`;
  report += `- **Annotations Found**: ${results.annotations.found} (${results.annotations.missing.length} missing)\n`;
  report += `- **Parameters Found**: ${results.parameters.found} (${results.parameters.missing.length} missing)\n`;
  report += `- **Example Prompts Found**: ${results.examplePrompts.found} (${results.examplePrompts.missing.length} missing)\n`;
  report += `- **Param+Annotation Found**: ${results.paramAndAnnotation.found} (${results.paramAndAnnotation.missing.length} missing)\n`;
  report += `- **Complete Tools Found**: ${results.completeTools.found} (${results.completeTools.missing.length} missing)\n\n`;

  const categories = [
    { key: 'annotations', label: 'Annotations' },
    { key: 'parameters', label: 'Parameters' },
    { key: 'examplePrompts', label: 'Example Prompts' },
    { key: 'paramAndAnnotation', label: 'Param+Annotation' },
    { key: 'completeTools', label: 'Complete Tools' },
  ];

  for (const category of categories) {
    if (results[category.key].missing.length > 0) {
      report += `## Missing ${category.label}\n\n`;
      
      // Group by service
      const byService = {};
      for (const tool of results[category.key].missing) {
        const service = tool.split(' ')[0];
        if (!byService[service]) byService[service] = [];
        byService[service].push(tool);
      }

      for (const [service, tools] of Object.entries(byService).sort()) {
        report += `### ${service} (${tools.length} tools)\n\n`;
        tools.forEach(tool => {
          const toolData = results.tools?.find(t => t.fullName === tool);
          const expected = toolData ? toolData.filenameBase : 'unknown';
          report += `- \`${tool}\` → Expected filename: \`${expected}-${category.key === 'completeTools' ? 'complete' : category.key}.md\`\n`;
        });
        report += '\n';
      }
    }
  }

  // Tools with ALL files present
  const completeTools = tools.filter(tool => 
    !results.annotations.missing.includes(tool.fullName) &&
    !results.parameters.missing.includes(tool.fullName) &&
    !results.examplePrompts.missing.includes(tool.fullName) &&
    !results.paramAndAnnotation.missing.includes(tool.fullName) &&
    !results.completeTools.missing.includes(tool.fullName)
  );

  report += `## Complete Tools (${completeTools.length})\n\n`;
  report += `These tools have all file types generated successfully:\n\n`;
  
  const completeByService = {};
  for (const tool of completeTools) {
    if (!completeByService[tool.service]) completeByService[tool.service] = [];
    completeByService[tool.service].push(tool.fullName);
  }

  for (const [service, tools] of Object.entries(completeByService).sort()) {
    report += `- **${service}** (${tools.length}): ${tools.join(', ')}\n`;
  }

  fs.writeFileSync(reportPath, report);
  console.log(`\n📄 Detailed report written to: ${path.relative(process.cwd(), reportPath)}`);
}

// Main execution
function main() {
  console.log('🔍 Parsing cli-output.json...');
  const tools = parseCliOutput();
  console.log(`✅ Found ${tools.length} tools\n`);

  console.log('🔎 Verifying generated files...');
  const results = verifyFiles(tools);
  results.tools = tools; // Store for report generation

  visualizeResults(results);
  generateReport(results, tools);
}

main();
