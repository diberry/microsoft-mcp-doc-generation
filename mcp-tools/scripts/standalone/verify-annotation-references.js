const fs = require('fs');
const path = require('path');

// Directories
const toolsDir = path.join(__dirname, '..', '..', '..', 'articles', 'azure-mcp-server', 'tools');
const annotationsDir = path.join(__dirname, '..', '..', '..', 'articles', 'azure-mcp-server', 'includes', 'tools', 'annotations');

// Get all annotation files (exclude index.md if it exists)
const annotationFiles = fs.readdirSync(annotationsDir)
    .filter(file => file.endsWith('.md'))
    .sort();

// Get all tool files (exclude index.md and annotations directory)
const toolFiles = fs.readdirSync(toolsDir)
    .filter(file => file.endsWith('.md') && file !== 'index.md')
    .sort();

// Track references to each annotation file
const annotationReferences = {};
annotationFiles.forEach(file => {
    annotationReferences[file] = [];
});

// Scan each tool file for INCLUDE statements
for (const toolFile of toolFiles) {
    const toolFilePath = path.join(toolsDir, toolFile);
    const content = fs.readFileSync(toolFilePath, 'utf-8');
    
    // Find all INCLUDE statements with annotation references
    // Pattern: [!INCLUDE [text](../includes/tools/annotations/filename.md)]
    const includePattern = /\[!INCLUDE\s+\[[^\]]+\]\(\.\.\/includes\/tools\/annotations\/([^)]+)\)\]/g;
    
    let match;
    while ((match = includePattern.exec(content)) !== null) {
        const annotationFile = match[1];
        
        if (annotationReferences.hasOwnProperty(annotationFile)) {
            annotationReferences[annotationFile].push(toolFile);
        } else {
            // Reference to a non-existent annotation file
            if (!annotationReferences['_missing']) {
                annotationReferences['_missing'] = [];
            }
            annotationReferences['_missing'].push({
                toolFile: toolFile,
                annotationFile: annotationFile
            });
        }
    }
}

// Generate report
const report = {
    summary: {
        totalAnnotationFiles: annotationFiles.length,
        referencedOnce: 0,
        referencedMultipleTimes: 0,
        notReferenced: 0,
        missingFiles: 0
    },
    details: {
        correct: [],
        multipleReferences: [],
        noReferences: [],
        missingFiles: []
    }
};

// Analyze results
for (const annotationFile of annotationFiles) {
    const references = annotationReferences[annotationFile];
    
    if (references.length === 0) {
        report.summary.notReferenced++;
        report.details.noReferences.push(annotationFile);
    } else if (references.length === 1) {
        report.summary.referencedOnce++;
        report.details.correct.push({
            annotation: annotationFile,
            toolFile: references[0]
        });
    } else {
        report.summary.referencedMultipleTimes++;
        report.details.multipleReferences.push({
            annotation: annotationFile,
            toolFiles: references,
            count: references.length
        });
    }
}

// Check for references to missing files
if (annotationReferences['_missing']) {
    report.summary.missingFiles = annotationReferences['_missing'].length;
    report.details.missingFiles = annotationReferences['_missing'];
}

// Write report to markdown file
const reportLines = [];
reportLines.push('# Annotation Reference Verification Report');
reportLines.push('');
reportLines.push(`**Generated:** ${new Date().toISOString()}`);
reportLines.push('');
reportLines.push('## Summary');
reportLines.push('');
reportLines.push(`- **Total annotation files:** ${report.summary.totalAnnotationFiles}`);
reportLines.push(`- **✅ Referenced exactly once:** ${report.summary.referencedOnce}`);
reportLines.push(`- **⚠️ Referenced multiple times:** ${report.summary.referencedMultipleTimes}`);
reportLines.push(`- **❌ Not referenced (orphaned):** ${report.summary.notReferenced}`);
reportLines.push(`- **🔴 Missing files (referenced but don't exist):** ${report.summary.missingFiles}`);
reportLines.push('');

// Report issues first
let hasIssues = false;

if (report.details.multipleReferences.length > 0) {
    hasIssues = true;
    reportLines.push('## ⚠️ Files Referenced Multiple Times');
    reportLines.push('');
    reportLines.push('These annotation files are included in multiple tool files:');
    reportLines.push('');
    for (const item of report.details.multipleReferences) {
        reportLines.push(`### ${item.annotation}`);
        reportLines.push('');
        reportLines.push(`**Referenced ${item.count} times in:**`);
        for (const toolFile of item.toolFiles) {
            reportLines.push(`- ${toolFile}`);
        }
        reportLines.push('');
    }
}

if (report.details.noReferences.length > 0) {
    hasIssues = true;
    reportLines.push('## ❌ Orphaned Annotation Files');
    reportLines.push('');
    reportLines.push('These annotation files exist but are not referenced by any tool file:');
    reportLines.push('');
    for (const file of report.details.noReferences) {
        reportLines.push(`- \`${file}\``);
    }
    reportLines.push('');
}

if (report.details.missingFiles.length > 0) {
    hasIssues = true;
    reportLines.push('## 🔴 Missing Annotation Files');
    reportLines.push('');
    reportLines.push('These annotation files are referenced but do not exist:');
    reportLines.push('');
    for (const item of report.details.missingFiles) {
        reportLines.push(`- **${item.annotationFile}**`);
        reportLines.push(`  - Referenced in: \`${item.toolFile}\``);
    }
    reportLines.push('');
}

if (!hasIssues) {
    reportLines.push('## ✅ All Checks Passed!');
    reportLines.push('');
    reportLines.push('- All annotation files are referenced exactly once');
    reportLines.push('- No orphaned annotation files');
    reportLines.push('- No references to missing files');
    reportLines.push('');
}

// List correct references in a collapsible section
if (report.details.correct.length > 0) {
    reportLines.push('## ✅ Correct References');
    reportLines.push('');
    reportLines.push(`${report.details.correct.length} annotation files are correctly referenced exactly once.`);
    reportLines.push('');
    reportLines.push('<details>');
    reportLines.push('<summary>View all correct references</summary>');
    reportLines.push('');
    reportLines.push('| Annotation File | Tool File |');
    reportLines.push('|-----------------|-----------|');
    for (const item of report.details.correct) {
        reportLines.push(`| \`${item.annotation}\` | \`${item.toolFile}\` |`);
    }
    reportLines.push('');
    reportLines.push('</details>');
}

// Write report
const reportPath = path.join(__dirname, 'annotation-reference-report.md');
fs.writeFileSync(reportPath, reportLines.join('\n'), 'utf-8');

// Console output
console.log('✅ Annotation reference verification complete!');
console.log(`📊 Report saved to: ${reportPath}`);
console.log('');
console.log('Summary:');
console.log(`  Total annotation files: ${report.summary.totalAnnotationFiles}`);
console.log(`  ✅ Referenced once: ${report.summary.referencedOnce}`);
console.log(`  ⚠️  Multiple references: ${report.summary.referencedMultipleTimes}`);
console.log(`  ❌ Orphaned: ${report.summary.notReferenced}`);
console.log(`  🔴 Missing: ${report.summary.missingFiles}`);

if (hasIssues) {
    console.log('');
    console.log('⚠️  Issues found - see report for details');
    process.exit(1);
} else {
    console.log('');
    console.log('✅ All checks passed!');
    process.exit(0);
}
