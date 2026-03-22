#!/usr/bin/env node
// validate-cli-output.js — Validates that CLI metadata extraction produces well-formed JSON
// Usage: node validate-cli-output.js <path-to-cli-output.json>
//
// Checks:
// 1. File exists and is valid JSON
// 2. Has "results" array
// 3. Each tool has required fields: command, name, description
// 4. Each tool has option[] array and metadata object
// 5. Reports tool count and namespace breakdown

const fs = require('fs');
const path = require('path');

const filePath = process.argv[2];
if (!filePath) {
    console.error('Usage: node validate-cli-output.js <cli-output.json>');
    process.exit(1);
}

if (!fs.existsSync(filePath)) {
    console.error(`FAIL: File not found: ${filePath}`);
    process.exit(1);
}

let data;
try {
    data = JSON.parse(fs.readFileSync(filePath, 'utf8'));
} catch (e) {
    console.error(`FAIL: Invalid JSON: ${e.message}`);
    process.exit(1);
}

if (!Array.isArray(data.results)) {
    console.error('FAIL: Missing "results" array');
    process.exit(1);
}

const errors = [];
const namespaces = new Set();

for (let i = 0; i < data.results.length; i++) {
    const tool = data.results[i];
    const id = tool.command || `[index ${i}]`;

    if (!tool.command) errors.push(`${id}: missing "command"`);
    if (!tool.name) errors.push(`${id}: missing "name"`);
    if (!tool.description) errors.push(`${id}: missing "description"`);
    if (!Array.isArray(tool.option)) errors.push(`${id}: missing "option" array`);
    if (!tool.metadata) errors.push(`${id}: missing "metadata" object`);

    if (tool.command) {
        const ns = tool.command.split(' ')[0];
        namespaces.add(ns);
    }
}

if (errors.length > 0) {
    console.error(`FAIL: ${errors.length} validation error(s):`);
    errors.forEach(e => console.error(`  - ${e}`));
    process.exit(1);
}

console.log(`OK: ${data.results.length} tools across ${namespaces.size} namespaces`);
console.log(`  Namespaces: ${[...namespaces].sort().join(', ')}`);
process.exit(0);
