#!/usr/bin/env node
// generate-report.test.js — TDD tests for generation report
// Uses Node.js built-in test runner (node:test + node:assert)

const { describe, it, before, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

const FIXTURES = path.join(__dirname, 'fixtures');

// Import will fail until generate-report.js exists — that's TDD
const {
  loadCommonParams,
  extractNamespaces,
  computeToolStats,
  computeNamespaceSummary,
  generateReport
} = require('../generate-report.js');

// Load fixture data once
const cliOutput = JSON.parse(fs.readFileSync(path.join(FIXTURES, 'cli-output.json'), 'utf8'));
const cliNamespace = JSON.parse(fs.readFileSync(path.join(FIXTURES, 'cli-namespace.json'), 'utf8'));
const cliVersion = JSON.parse(fs.readFileSync(path.join(FIXTURES, 'cli-version.json'), 'utf8'));
const commonParams = JSON.parse(fs.readFileSync(path.join(FIXTURES, 'common-parameters.json'), 'utf8'));

// ─── loadCommonParams ────────────────────────────────────────────

describe('loadCommonParams', () => {
  it('returns a Set of parameter names', () => {
    const result = loadCommonParams(commonParams);
    assert.ok(result instanceof Set);
    assert.equal(result.size, 7);
    assert.ok(result.has('--tenant'));
    assert.ok(result.has('--retry-network-timeout'));
  });
});

// ─── extractNamespaces ───────────────────────────────────────────

describe('extractNamespaces', () => {
  it('groups tools by namespace (first word of command)', () => {
    const ns = extractNamespaces(cliOutput.results);
    assert.equal(Object.keys(ns).length, 2, 'should find 2 namespaces');
    assert.ok(ns.acr, 'should have acr namespace');
    assert.ok(ns.cosmos, 'should have cosmos namespace');
  });

  it('assigns correct tools per namespace', () => {
    const ns = extractNamespaces(cliOutput.results);
    assert.equal(ns.acr.length, 2, 'acr should have 2 tools');
    assert.equal(ns.cosmos.length, 2, 'cosmos should have 2 tools');
  });
});

// ─── computeToolStats ────────────────────────────────────────────

describe('computeToolStats', () => {
  const commonSet = new Set(commonParams.map(p => p.name));

  it('counts non-global params correctly for tool with only common params', () => {
    // acr registry list: 9 total options, 7 common = 2 non-global (subscription, resource-group)
    const acrList = cliOutput.results[0];
    const stats = computeToolStats(acrList, commonSet);
    assert.equal(stats.totalParams, 9);
    assert.equal(stats.nonGlobalParams, 2);
    assert.equal(stats.requiredParams, 0);
    assert.equal(stats.optionalParams, 2);
  });

  it('counts required params correctly', () => {
    // cosmos query: 12 total, 7 common = 5 non-global, 3 required, 2 optional
    const cosmosQuery = cliOutput.results[2];
    const stats = computeToolStats(cosmosQuery, commonSet);
    assert.equal(stats.totalParams, 12);
    assert.equal(stats.nonGlobalParams, 5);
    assert.equal(stats.requiredParams, 3);
    assert.equal(stats.optionalParams, 2);
  });

  it('counts zero non-global when all params are common + subscription', () => {
    // cosmos account list: 8 total, 7 common = 1 non-global (subscription)
    const cosmosList = cliOutput.results[3];
    const stats = computeToolStats(cosmosList, commonSet);
    assert.equal(stats.nonGlobalParams, 1);
    assert.equal(stats.requiredParams, 0);
    assert.equal(stats.optionalParams, 1);
  });
});

// ─── computeNamespaceSummary ─────────────────────────────────────

describe('computeNamespaceSummary', () => {
  const commonSet = new Set(commonParams.map(p => p.name));

  it('aggregates tool counts and params per namespace', () => {
    const ns = extractNamespaces(cliOutput.results);
    const summary = computeNamespaceSummary(ns, commonSet);

    assert.equal(summary.acr.toolCount, 2);
    // acr: tool1 has 9 params (2 non-global), tool2 has 9 params (2 non-global)
    assert.equal(summary.acr.totalParams, 18);
    assert.equal(summary.acr.nonGlobalParams, 4);

    assert.equal(summary.cosmos.toolCount, 2);
    // cosmos: tool1 has 12 params (5 non-global), tool2 has 8 params (1 non-global)
    assert.equal(summary.cosmos.totalParams, 20);
    assert.equal(summary.cosmos.nonGlobalParams, 6);
  });
});

// ─── generateReport ──────────────────────────────────────────────

describe('generateReport', () => {
  let report;

  before(() => {
    report = generateReport({
      cliOutput,
      cliNamespace,
      cliVersion,
      commonParams
    });
  });

  it('returns a non-empty string', () => {
    assert.ok(typeof report === 'string');
    assert.ok(report.length > 0);
  });

  // ── Header ──

  it('starts with H1 Generation Report', () => {
    assert.ok(report.startsWith('# Generation Report'));
  });

  it('includes version from cli-version.json', () => {
    assert.ok(report.includes('2.0.0-beta.31'));
  });

  it('includes generation date in YYYY-MM-DD format', () => {
    const dateMatch = report.match(/Generation date:\*\* (\d{4}-\d{2}-\d{2})/);
    assert.ok(dateMatch, 'should contain generation date');
  });

  it('includes total namespaces count', () => {
    assert.ok(report.includes('**Total namespaces:** 2'));
  });

  it('includes total tools count', () => {
    assert.ok(report.includes('**Total tools:** 4'));
  });

  // ── Namespace Summary Table ──

  it('has a Namespace Summary section', () => {
    assert.ok(report.includes('## Namespace Summary'));
  });

  it('has a markdown table with correct headers', () => {
    assert.ok(report.includes('| Namespace | Tools | Non-Global Params |'));
  });

  it('has a separator row after headers', () => {
    assert.ok(report.includes('| --- | --- | --- |'));
  });

  it('includes acr row with correct counts', () => {
    // acr: 2 tools, 4 non-global
    const acrRow = report.match(/\| acr \| 2 \| 4 \|/);
    assert.ok(acrRow, 'acr row should have correct values');
  });

  it('includes cosmos row with correct counts', () => {
    // cosmos: 2 tools, 6 non-global
    const cosmosRow = report.match(/\| cosmos \| 2 \| 6 \|/);
    assert.ok(cosmosRow, 'cosmos row should have correct values');
  });

  // ── Per-Namespace Tool Detail ──

  it('has Tool Detail section', () => {
    assert.ok(report.includes('## Tool Detail'));
  });

  it('has per-namespace H3 with tool count', () => {
    assert.ok(report.includes('### acr (2 tools)'));
    assert.ok(report.includes('### cosmos (2 tools)'));
  });

  it('has tool detail table headers', () => {
    const headerPattern = '| Tool | Command | Required | Optional | Total Non-Global |';
    // Should appear at least twice (once per namespace)
    const matches = report.split(headerPattern).length - 1;
    assert.ok(matches >= 2, `expected at least 2 tool detail tables, found ${matches}`);
  });

  it('has cosmos query tool row with correct params', () => {
    // cosmos query: command "cosmos database container item query", 3 required, 2 optional, 5 total non-global
    assert.ok(report.includes('cosmos database container item query'));
    const queryRow = report.match(/\| query \| cosmos database container item query \| 3 \| 2 \| 5 \|/);
    assert.ok(queryRow, 'cosmos query row should have correct param counts');
  });

  it('has acr list tool row with correct params', () => {
    // acr list: 0 required, 2 optional, 2 non-global
    const listRow = report.match(/\| list \| acr registry list \| 0 \| 2 \| 2 \|/);
    assert.ok(listRow, 'acr list row should have correct param counts');
  });

  // ── Diff-friendly ──

  it('is deterministic (same input = same output modulo date)', () => {
    const report2 = generateReport({ cliOutput, cliNamespace, cliVersion, commonParams });
    // Replace date lines for comparison
    const normalize = s => s.replace(/Generation date: \d{4}-\d{2}-\d{2}/, 'Generation date: DATE');
    assert.equal(normalize(report), normalize(report2));
  });

  it('namespaces are sorted alphabetically', () => {
    const nsLines = report.match(/^### .+ \(\d+ tools\)$/gm);
    assert.ok(nsLines);
    const names = nsLines.map(l => l.match(/### (.+) \(/)[1]);
    const sorted = [...names].sort();
    assert.deepEqual(names, sorted);
  });

  it('tools within namespace are sorted alphabetically by name', () => {
    // Extract tool rows from cosmos section
    const cosmosSection = report.split('### cosmos')[1].split('###')[0];
    const toolNames = [...cosmosSection.matchAll(/^\| (\w+) \|/gm)].map(m => m[1]).filter(n => n !== 'Tool');
    const sorted = [...toolNames].sort();
    assert.deepEqual(toolNames, sorted);
  });
});

// ─── Edge cases ──────────────────────────────────────────────────

describe('edge cases', () => {
  it('handles empty results array', () => {
    const report = generateReport({
      cliOutput: { status: 200, results: [] },
      cliNamespace: { status: 200, results: [] },
      cliVersion: { version: '0.0.0' },
      commonParams: []
    });
    assert.ok(report.includes('**Total namespaces:** 0'));
    assert.ok(report.includes('**Total tools:** 0'));
  });

  it('handles tool with no options array', () => {
    const report = generateReport({
      cliOutput: {
        status: 200,
        results: [{ id: '1', name: 'test', description: 'Test', command: 'ns cmd' }]
      },
      cliNamespace: { status: 200, results: [{ name: 'ns', command: 'ns' }] },
      cliVersion: { version: '1.0.0' },
      commonParams: []
    });
    assert.ok(report.includes('**Total tools:** 1'));
  });
});
