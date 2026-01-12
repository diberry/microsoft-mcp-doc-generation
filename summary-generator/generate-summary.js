#!/usr/bin/env node

/**
 * Azure MCP Documentation Generation Summary Generator
 * 
 * This JavaScript port reproduces the summary output from Generate-MultiPageDocs.ps1
 * Reads CLI output files and generated documentation to produce statistics and summaries.
 */

const fs = require('fs');
const path = require('path');

// Configuration
const config = {
    cliOutputPath: process.env.CLI_OUTPUT_PATH || '../generated/cli/cli-output.json',
    outputDir: process.env.OUTPUT_DIR || '../generated/tools',
    parentOutputDir: process.env.PARENT_OUTPUT_DIR || '../generated',
    summaryFileName: 'generation-summary.md'
};

// Helper functions for colored output
const colors = {
    reset: '\x1b[0m',
    cyan: '\x1b[36m',
    green: '\x1b[32m',
    yellow: '\x1b[33m',
    red: '\x1b[31m',
    magenta: '\x1b[35m'
};

function writeInfo(message) {
    console.log(`${colors.cyan}INFO: ${message}${colors.reset}`);
}

function writeSuccess(message) {
    console.log(`${colors.green}SUCCESS: ${message}${colors.reset}`);
}

function writeWarning(message) {
    console.log(`${colors.yellow}WARNING: ${message}${colors.reset}`);
}

function writeError(message) {
    console.log(`${colors.red}ERROR: ${message}${colors.reset}`);
}

function writeProgress(message) {
    console.log(`${colors.magenta}PROGRESS: ${message}${colors.reset}`);
}

// Read JSON file safely
function readJsonFile(filePath) {
    try {
        const fullPath = path.resolve(__dirname, filePath);
        if (!fs.existsSync(fullPath)) {
            throw new Error(`File not found: ${fullPath}`);
        }
        const content = fs.readFileSync(fullPath, 'utf8');
        return JSON.parse(content);
    } catch (error) {
        throw new Error(`Failed to read ${filePath}: ${error.message}`);
    }
}

// Get file size in KB
function getFileSizeKB(filePath) {
    try {
        const stats = fs.statSync(filePath);
        return (stats.size / 1024).toFixed(1);
    } catch {
        return '0.0';
    }
}

// List markdown files in directory
function listMarkdownFiles(dirPath) {
    try {
        const fullPath = path.resolve(__dirname, dirPath);
        if (!fs.existsSync(fullPath)) {
            return [];
        }
        return fs.readdirSync(fullPath)
            .filter(file => file.endsWith('.md'))
            .sort();
    } catch {
        return [];
    }
}

// Parse tool counts by service area from CLI output
function parseToolCountsByArea(cliData) {
    const toolCountsByArea = {};
    
    if (!cliData.results) {
        return { toolCountsByArea, totalTools: 0, totalAreas: 0 };
    }
    
    cliData.results.forEach(tool => {
        // Extract service area from command (first part before space)
        const commandParts = tool.command.split(' ');
        const area = commandParts[0] || 'unknown';
        
        if (!toolCountsByArea[area]) {
            toolCountsByArea[area] = 0;
        }
        toolCountsByArea[area]++;
    });
    
    const totalTools = cliData.results.length;
    const totalAreas = Object.keys(toolCountsByArea).length;
    
    return { toolCountsByArea, totalTools, totalAreas };
}

// Build tool list by service area
function buildToolListByArea(cliData) {
    const toolsByArea = {};
    
    if (!cliData.results) {
        return toolsByArea;
    }
    
    cliData.results.forEach(tool => {
        const commandParts = tool.command.split(' ');
        const area = commandParts[0] || 'unknown';
        
        if (!toolsByArea[area]) {
            toolsByArea[area] = [];
        }
        
        const paramCount = tool.parameters ? Object.keys(tool.parameters).length : 0;
        toolsByArea[area].push({
            command: tool.command,
            description: tool.description || 'No description',
            paramCount: paramCount
        });
    });
    
    // Sort tools within each area
    Object.keys(toolsByArea).forEach(area => {
        toolsByArea[area].sort((a, b) => a.command.localeCompare(b.command));
    });
    
    return toolsByArea;
}

// Generate summary markdown content
function generateSummaryMarkdown(summaryData) {
    const lines = [];
    
    lines.push('# Azure MCP Documentation Generation Summary');
    lines.push('');
    lines.push(`**Generated:** ${new Date().toISOString().replace('T', ' ').substring(0, 19)} UTC`);
    lines.push('**Generation Method:** C# generator with Handlebars templates');
    lines.push(`**Total Pages Created:** ${summaryData.totalPages}`);
    lines.push('');
    
    // Generated files section
    lines.push('## Generated Documentation Files');
    lines.push('');
    summaryData.files.forEach(file => {
        lines.push(`- 📄 ${file.name} (${file.sizeKB}KB)`);
    });
    
    // Data files section
    lines.push('');
    lines.push('## Data Files');
    lines.push('');
    summaryData.dataFiles.forEach(file => {
        if (file.exists) {
            lines.push(`- 📄 ${file.path} (${file.sizeKB}KB) - ${file.description}`);
        }
    });
    
    // Tool statistics
    if (summaryData.totalTools > 0) {
        lines.push('');
        lines.push('## Tool Statistics');
        lines.push('');
        lines.push(`- **Total tools:** ${summaryData.totalTools}`);
        lines.push(`- **Total service areas:** ${summaryData.totalAreas}`);
        
        if (Object.keys(summaryData.toolCountsByArea).length > 0) {
            lines.push('');
            lines.push('### Tools by Service Area');
            lines.push('');
            Object.keys(summaryData.toolCountsByArea).sort().forEach(area => {
                const count = summaryData.toolCountsByArea[area];
                lines.push(`- **${area}:** ${count} tools`);
            });
        }
        
        // Complete tool list
        if (summaryData.toolsByArea && Object.keys(summaryData.toolsByArea).length > 0) {
            lines.push('');
            lines.push('## Complete Tool List');
            lines.push('');
            
            const sortedAreas = Object.keys(summaryData.toolsByArea).sort();
            sortedAreas.forEach((area, index) => {
                if (index > 0) {
                    lines.push('');
                    lines.push('');
                }
                
                const tools = summaryData.toolsByArea[area];
                lines.push(`### ${area} (${tools.length} tools)`);
                lines.push('');
                
                tools.forEach(tool => {
                    lines.push(`- ${tool.command} (${tool.paramCount} params) - ${tool.description}`);
                });
            });
        }
    }
    
    return lines.join('\n');
}

// Main execution
async function main() {
    try {
        writeProgress('Starting Azure MCP Documentation Generation Summary...');
        
        // Step 1: Load CLI output
        writeProgress('Step 1: Loading CLI output data...');
        const cliData = readJsonFile(config.cliOutputPath);
        writeSuccess(`Loaded CLI output with ${cliData.results?.length || 0} tools`);
        
        // Step 2: Parse tool statistics
        writeProgress('Step 2: Parsing tool statistics...');
        const { toolCountsByArea, totalTools, totalAreas } = parseToolCountsByArea(cliData);
        const toolsByArea = buildToolListByArea(cliData);
        
        writeInfo('');
        writeInfo('Tool Statistics:');
        writeInfo(`  📊 Total tools: ${totalTools}`);
        writeInfo(`  📊 Total service areas: ${totalAreas}`);
        writeInfo('  📊 Tools by service area:');
        Object.keys(toolCountsByArea).sort().forEach(area => {
            writeInfo(`     • ${area}: ${toolCountsByArea[area]} tools`);
        });
        
        // Step 3: List generated files
        writeProgress('Step 3: Scanning generated documentation files...');
        const outputDir = path.resolve(__dirname, config.outputDir);
        const files = listMarkdownFiles(config.outputDir).map(file => ({
            name: file,
            sizeKB: getFileSizeKB(path.join(outputDir, file))
        }));
        
        writeInfo('');
        writeInfo('Generated files:');
        files.forEach(file => {
            writeInfo(`  📄 ${file.name} (${file.sizeKB}KB)`);
        });
        
        // Step 4: Collect data file information
        writeProgress('Step 4: Collecting data file information...');
        const parentDir = path.resolve(__dirname, config.parentOutputDir);
        const dataFiles = [
            {
                path: path.join(parentDir, 'cli/cli-output.json'),
                description: 'CLI output'
            },
            {
                path: path.join(parentDir, 'cli/cli-namespace.json'),
                description: 'CLI namespace output'
            },
            {
                path: path.join(parentDir, 'namespaces.csv'),
                description: 'Alphabetically sorted namespaces CSV'
            },
            {
                path: path.join(parentDir, 'tool-count-comparison.json'),
                description: 'Tool count comparison report'
            },
            {
                path: path.join(parentDir, 'ToolDescriptionEvaluator.json'),
                description: 'ToolDescriptionEvaluator tools.json (local copy)'
            }
        ].map(file => ({
            ...file,
            exists: fs.existsSync(file.path),
            sizeKB: fs.existsSync(file.path) ? getFileSizeKB(file.path) : '0.0'
        }));
        
        writeInfo('');
        writeInfo('Data files:');
        dataFiles.forEach(file => {
            if (file.exists) {
                writeInfo(`  📄 ${file.path} (${file.sizeKB}KB) - ${file.description}`);
            }
        });
        
        // Step 5: Generate summary markdown
        writeProgress('Step 5: Generating summary markdown file...');
        const summaryData = {
            totalPages: files.length,
            files: files,
            dataFiles: dataFiles,
            totalTools: totalTools,
            totalAreas: totalAreas,
            toolCountsByArea: toolCountsByArea,
            toolsByArea: toolsByArea
        };
        
        const summaryMarkdown = generateSummaryMarkdown(summaryData);
        const summaryFilePath = path.join(parentDir, config.summaryFileName);
        fs.writeFileSync(summaryFilePath, summaryMarkdown, 'utf8');
        
        const summaryFileSize = getFileSizeKB(summaryFilePath);
        writeSuccess(`Summary saved to: ${summaryFilePath} (${summaryFileSize}KB)`);
        
        // Step 6: Display complete tool list
        writeInfo('');
        writeInfo('Complete Tool List:');
        Object.keys(toolsByArea).sort().forEach(area => {
            const tools = toolsByArea[area];
            writeInfo('');
            writeInfo(`${area} (${tools.length} tools):`);
            writeInfo('─'.repeat(60));
            tools.forEach(tool => {
                writeInfo(`  • ${tool.command} (${tool.paramCount} params) - ${tool.description}`);
            });
        });
        
        writeSuccess('');
        writeSuccess('========================================');
        writeSuccess(`✅ Summary generation complete!`);
        writeSuccess(`📄 ${files.length} documentation pages`);
        writeSuccess(`🔧 ${totalTools} tools across ${totalAreas} service areas`);
        writeSuccess('========================================');
        
    } catch (error) {
        writeError(`Summary generation failed: ${error.message}`);
        writeError(`Stack trace: ${error.stack}`);
        process.exit(1);
    }
}

// Run main function
if (require.main === module) {
    main();
}

module.exports = { main, parseToolCountsByArea, buildToolListByArea, generateSummaryMarkdown };
