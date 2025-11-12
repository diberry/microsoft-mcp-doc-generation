const fs = require('fs');
const path = require('path');

// Directory containing tool files
const toolsDir = path.join(__dirname, '..', 'articles', 'azure-mcp-server', 'tools');

// Get all tool files (exclude index.md)
const toolFiles = fs.readdirSync(toolsDir)
    .filter(file => file.endsWith('.md') && file !== 'index.md')
    .sort();

// Pattern for annotation INCLUDE statements
const includePattern = /\[!INCLUDE\s+\[[^\]]+\]\(\.\.\/includes\/tools\/annotations\/[^)]+\)\]/;

// Pattern for tool annotation hints line (with flexible spacing)
const hintPattern = /\[Tool annotation hints\]\(index\.md#tool-annotation-hints\):/;

// Results tracking
const results = {
    totalFiles: 0,
    totalIncludes: 0,
    correct: [],
    missing: [],
    issues: []
};

// Process each file
for (const toolFile of toolFiles) {
    results.totalFiles++;
    const toolFilePath = path.join(toolsDir, toolFile);
    const content = fs.readFileSync(toolFilePath, 'utf-8');
    const lines = content.split('\n');
    
    // Find all annotation INCLUDE statements
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        
        if (includePattern.test(line)) {
            results.totalIncludes++;
            
            // Check if previous line is the tool annotation hints line
            let previousLineIndex = i - 1;
            let previousLine = '';
            
            // Skip empty lines to find the actual previous content line
            while (previousLineIndex >= 0 && lines[previousLineIndex].trim() === '') {
                previousLineIndex--;
            }
            
            if (previousLineIndex >= 0) {
                previousLine = lines[previousLineIndex].trim();
            }
            
            // Extract the INCLUDE file name for context
            const includeMatch = line.match(/annotations\/([^)]+)\)/);
            const includeName = includeMatch ? includeMatch[1] : 'unknown';
            
            if (hintPattern.test(previousLine)) {
                // Correct: hint line is present
                results.correct.push({
                    file: toolFile,
                    line: i + 1,
                    includeName: includeName
                });
            } else {
                // Missing or incorrect hint line
                results.missing.push({
                    file: toolFile,
                    line: i + 1,
                    includeName: includeName,
                    previousLine: previousLine || '(empty or start of file)',
                    actualLine: line.trim()
                });
            }
        }
    }
}

// Generate report
const reportLines = [];
reportLines.push('# Tool Annotation Hints Verification Report');
reportLines.push('');
reportLines.push(`**Generated:** ${new Date().toISOString()}`);
reportLines.push('');
reportLines.push('## Summary');
reportLines.push('');
reportLines.push(`- **Total tool files checked:** ${results.totalFiles}`);
reportLines.push(`- **Total annotation INCLUDE statements:** ${results.totalIncludes}`);
reportLines.push(`- **✅ Correct (hint line present):** ${results.correct.length}`);
reportLines.push(`- **❌ Missing hint line:** ${results.missing.length}`);
reportLines.push('');

// Report issues
if (results.missing.length > 0) {
    reportLines.push('## ❌ Missing Tool Annotation Hints');
    reportLines.push('');
    reportLines.push('These INCLUDE statements are missing the required "Tool annotation hints" line immediately before them:');
    reportLines.push('');
    
    for (const item of results.missing) {
        reportLines.push(`### ${item.file} (line ${item.line})`);
        reportLines.push('');
        reportLines.push(`**Annotation file:** \`${item.includeName}\``);
        reportLines.push('');
        reportLines.push('**Found before INCLUDE:**');
        reportLines.push('```');
        reportLines.push(item.previousLine);
        reportLines.push('```');
        reportLines.push('');
        reportLines.push('**Expected:**');
        reportLines.push('```markdown');
        reportLines.push('[Tool annotation hints](index.md#tool-annotation-hints):');
        reportLines.push('');
        reportLines.push(item.actualLine);
        reportLines.push('```');
        reportLines.push('');
    }
} else {
    reportLines.push('## ✅ All Checks Passed!');
    reportLines.push('');
    reportLines.push('All annotation INCLUDE statements have the required "Tool annotation hints" line immediately before them.');
    reportLines.push('');
}

// List correct entries in collapsible section
if (results.correct.length > 0) {
    reportLines.push('## ✅ Correct Annotations');
    reportLines.push('');
    reportLines.push(`${results.correct.length} annotation INCLUDE statements have the correct hint line.`);
    reportLines.push('');
    reportLines.push('<details>');
    reportLines.push('<summary>View all correct annotations</summary>');
    reportLines.push('');
    reportLines.push('| File | Line | Annotation File |');
    reportLines.push('|------|------|-----------------|');
    for (const item of results.correct) {
        reportLines.push(`| \`${item.file}\` | ${item.line} | \`${item.includeName}\` |`);
    }
    reportLines.push('');
    reportLines.push('</details>');
    reportLines.push('');
}

// Write report
const reportPath = path.join(__dirname, 'annotation-hints-report.md');
fs.writeFileSync(reportPath, reportLines.join('\n'), 'utf-8');

// Console output
console.log('✅ Tool annotation hints verification complete!');
console.log(`📊 Report saved to: ${reportPath}`);
console.log('');
console.log('Summary:');
console.log(`  Total files checked: ${results.totalFiles}`);
console.log(`  Total INCLUDE statements: ${results.totalIncludes}`);
console.log(`  ✅ Correct: ${results.correct.length}`);
console.log(`  ❌ Missing hint line: ${results.missing.length}`);

if (results.missing.length > 0) {
    console.log('');
    console.log('⚠️  Issues found - see report for details');
    process.exit(1);
} else {
    console.log('');
    console.log('✅ All checks passed!');
    process.exit(0);
}
