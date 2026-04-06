# Configuration Registry

> Inventory, load order, and schema coverage for all JSON configuration files in `docs-generation/data/`.

## Configuration files inventory

| File | Purpose | Loader class(es) | Used by step(s) | Entry count | Has schema? |
|------|---------|-------------------|------------------|-------------|-------------|
| `brand-to-server-mapping.json` | Maps MCP namespace → Azure brand name, short name, filename, merge config | `DataFileLoader`, `ToolFileNameBuilder`, `CleanupGenerator`, `BootstrapStep` | 0 (Bootstrap), 1, 3, 4, 6 | ~52 entries | Yes |
| `common-parameters.json` | Shared CLI parameters filtered from per-tool tables (auth, retry, subscription) | `DataFileLoader`, `DocumentationGenerator` | 1 (Annotations) | 9 entries | Yes |
| `compound-words.json` | Concatenated CLI tokens → hyphenated forms for filename generation | `DataFileLoader`, `ToolFileNameBuilder` | 0, 1 | ~23 entries | Yes |
| `stop-words.json` | Words removed from generated include filenames | `DataFileLoader`, `ToolFileNameBuilder` | 0, 1 | 5 entries | Yes |
| `nl-parameters.json` | CLI parameter names → human-readable text | `TextCleanup`, `Config` | 1 (Annotations) | 4 entries | Yes |
| `nl-parameter-identifiers.json` | Single-word identifiers → "X name" expansions | `TextCleanup` (auto-discovered) | 1 (Annotations) | ~19 entries | No |
| `static-text-replacement.json` | Find/replace pairs for terminology standardization | `TextCleanup`, `Config`, `ArticleContentProcessor` | 1, 4, 6 | ~31 entries | Yes |
| `acronym-definitions.json` | Acronym → expansion mappings with optional context patterns | `AcronymExpander` | 4 (Tool Family Cleanup) | ~10 entries | No |
| `service-doc-links.json` | Namespace → service documentation URL, title, SEO description | `CleanupGenerator` | 4 (Tool Family Cleanup) | ~52 entries | No |
| `transformation-config.json` | Declares required data files for horizontal article generator | `ConfigLoader` | 6 (Horizontal Articles) | 2 entries | No |
| `config.json` | Declares required data files for annotation generator | `Config` | 1 (Annotations) | 2 entries | No |

## Load order

Configuration files are loaded at different pipeline stages. The table below shows the order.

### Step 0 — Bootstrap (global, runs once)

1. **`brand-to-server-mapping.json`** — loaded by `BootstrapStep` to validate all CLI namespaces have brand mappings and to copy the file to temp directories for downstream use.

### Step 1 — Annotations, Parameters, Raw Tools (per namespace)

1. **`config.json`** — loaded by `Config.Load()` to discover paths of required data files.
2. **`brand-to-server-mapping.json`** — loaded by `DataFileLoader.LoadBrandMappingsAsync()`.
3. **`common-parameters.json`** — loaded by `DataFileLoader.LoadCommonParametersAsync()`.
4. **`compound-words.json`** — loaded by `DataFileLoader.LoadCompoundWordsAsync()`.
5. **`stop-words.json`** — loaded by `DataFileLoader.LoadStopWordsAsync()`.
6. **`nl-parameters.json`** — loaded by `TextCleanup.LoadFiles()`.
7. **`nl-parameter-identifiers.json`** — auto-discovered from same directory as `nl-parameters.json`.
8. **`static-text-replacement.json`** — loaded by `TextCleanup.LoadFiles()`.

### Step 4 — Tool Family Cleanup (per namespace)

1. **`brand-to-server-mapping.json`** — loaded by `CleanupGenerator.Initialize()`.
2. **`service-doc-links.json`** — loaded by `CleanupGenerator.Initialize()`.
3. **`acronym-definitions.json`** — loaded by `AcronymExpander.LoadDefinitions()` (with 3-path fallback).

### Step 6 — Horizontal Articles (per namespace)

1. **`transformation-config.json`** — loaded by `ConfigLoader.LoadAsync()`.
2. **`brand-to-server-mapping.json`** — loaded for brand resolution.
3. **`static-text-replacement.json`** — loaded for `ArticleContentProcessor` transformations.

## Duplicate data analysis

Several files contain overlapping data or serve similar purposes under different structures.

| Data overlap | Files involved | Details |
|-------------|---------------|---------|
| Brand name mapping | `brand-to-server-mapping.json`, `service-doc-links.json` | Both are keyed on MCP namespace. `service-doc-links.json` duplicates the namespace→brand relationship and adds URL/SEO fields. Both files must be updated when a new namespace is added. |
| Static text replacements | `static-text-replacement.json`, `nl-parameters.json` | Both use `{ "Parameter": "...", "NaturalLanguage": "..." }` schema. `static-text-replacement` handles broad text corrections; `nl-parameters` handles parameter-name-to-display mappings. Loaded by the same `TextCleanup` class. |
| Identifier expansions | `nl-parameter-identifiers.json`, `nl-parameters.json` | Both map short tokens to human-readable text. `nl-parameter-identifiers` adds the suffix "name" (e.g. `account` → `Account name`). Same schema and same loader. |
| Required-files declarations | `config.json`, `transformation-config.json` | Both declare `RequiredFiles` arrays pointing to other data files. `config.json` serves Step 1; `transformation-config.json` serves Step 6. Identical structure with different consumers. |
| VMSS expansion | `static-text-replacement.json`, `acronym-definitions.json` | Both define VMSS → "virtual machine scale set" mappings, though in different formats and for different processing stages. |

## Schema coverage

| File | Schema | Status |
|------|--------|--------|
| `brand-to-server-mapping.json` | `schemas/brand-to-server-mapping.schema.json` | ✅ Covered |
| `common-parameters.json` | `schemas/common-parameters.schema.json` | ✅ Covered |
| `compound-words.json` | `schemas/compound-words.schema.json` | ✅ Covered |
| `stop-words.json` | `schemas/stop-words.schema.json` | ✅ Covered |
| `nl-parameters.json` | `schemas/nl-parameters.schema.json` | ✅ Covered |
| `static-text-replacement.json` | `schemas/static-text-replacement.schema.json` | ✅ Covered |
| `nl-parameter-identifiers.json` | — | ❌ Needs schema (same structure as `nl-parameters.json`; can `$ref` it) |
| `acronym-definitions.json` | — | ❌ Needs schema |
| `service-doc-links.json` | — | ❌ Needs schema |
| `transformation-config.json` | — | ❌ Needs schema |
| `config.json` | — | ❌ Needs schema |

## Recommendations

### 1. Merge `service-doc-links.json` into `brand-to-server-mapping.json`

`service-doc-links.json` is keyed by namespace and adds `title`, `url`, and `seoDescription`. These fields could be added directly to each entry in `brand-to-server-mapping.json`, eliminating a 52-entry duplicate file and the need to keep both in sync when namespaces are added or renamed.

### 2. Merge `nl-parameter-identifiers.json` into `nl-parameters.json`

Both files share the same `{ "Parameter", "NaturalLanguage" }` schema and are loaded by `TextCleanup`. They could be a single array (or two sections within one file) to reduce the number of files and simplify discovery.

### 3. Unify `config.json` and `transformation-config.json`

Both files have the same `{ "RequiredFiles": [...] }` structure. A single file with step-keyed sections (e.g. `"step1": { ... }, "step6": { ... }`) would be easier to maintain.

### 4. Use `$ref` for shared schemas

`nl-parameters.json`, `nl-parameter-identifiers.json`, and `static-text-replacement.json` all use the same `{ "Parameter", "NaturalLanguage" }` item schema. Define a shared `parameter-nl-pair.schema.json` and use `$ref` from all three schemas to avoid structural drift.

### 5. Add schemas for remaining files

Priority order for the five uncovered files:
1. `service-doc-links.json` — large file (52 entries), frequently edited
2. `acronym-definitions.json` — has optional fields (`ContextPattern`, `ExpandedForm`) that are easy to misconfigure
3. `nl-parameter-identifiers.json` — can reuse `nl-parameters.schema.json` via `$ref`
4. `config.json` / `transformation-config.json` — small files, low risk
