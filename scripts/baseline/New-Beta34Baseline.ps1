#requires -Version 7.0
<#
.SYNOPSIS
    Deterministically freezes the Azure MCP beta.34 critical-failure baseline (issue #813, Step 1).

.DESCRIPTION
    Reads the 34 catalog critical-failure records from a completed generation run, sanitizes
    each record's text (deterministically + idempotently), writes them as immutable test
    fixtures keyed by a stable ID (from the classification file), and emits a provenance
    manifest (AD-028 schema). A -VerifyOnly pass regenerates into a temp dir and compares
    the deterministic outputs (fixture bytes + manifest record hashes) against the committed
    artifacts to prove determinism — failing nonzero on any drift.

    SANITIZATION STRATEGY (per AD-028 sanitization contract):
      PURE STRING REPLACEMENT on the raw file text (NOT re-serialization). The record's JSON
      structure is preserved byte-for-byte apart from the redactions below, so escaped forms
      (\u0027, \u0022, \u002B) and field ordering are retained exactly. Only line endings are
      normalized to LF and the BOM is stripped on write.

    Rules are applied in this order (case-insensitive; each rule matches BOTH the \\-escaped
    JSON form and the raw/forward-slash form via a shared separator sub-pattern):
      1. Repo root absolute path                          -> <REPO>
      2. C:\Users\<name>\AppData\Local\Temp               -> <TEMP>
         other C:\Users\<name>                            -> <USER_HOME>
      3. literal username 'diberry'                       -> <USER>
      4. host / $env:COMPUTERNAME                          -> <HOST>
      5. pipeline-runner-step<N>-<32hex>                  -> pipeline-runner-step<N>-<GUID>
      6. generated-<ns>-YYYY-MM-DD-HH-MM-SS               -> generated-<ns>-<RUNSTAMP>
      7. any remaining drive-letter absolute path X:\...  -> <PATH>   (safety net)

    RETAINED (semantic, never redacted): recordedAtUtc, azureMcpBuild version+SHA, namespace,
    stepId, stepName, artifactType, artifactName, summary, details, validatorResults,
    failurePolicy, stepWarnings, relatedPaths (paths within them are redacted but the array
    is preserved), processInvocations.

    DETERMINISM CONTRACT (-VerifyOnly): the manifest's volatile provenance fields
    (captureTimestampUtc, toolVersions) legitimately change per run and are EXCLUDED from the
    determinism comparison. VerifyOnly compares (a) every fixture file byte-for-byte and
    (b) every manifest record's sourceSha256 + sanitizedSha256. Any drift => nonzero exit.

.NOTES
    Owner: Quinn (DevOps/Scripts). Issue #813 Step 1. Branch: squad/813-step1-beta34-baseline.
    Never edits anything under generated-*/ (read-only source). Never reads mcp-tools/.env.
    AD-027 compliance: no param name case-insensitively collides with any local variable.
#>
[CmdletBinding()]
param(
    [string]$SourceRunPath = 'generated-20260813T162453',
    [string]$OutputRoot = 'mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures',
    [string]$ClassificationPath = 'scripts/baseline/beta34-classification.json',
    [switch]$VerifyOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---- Constants -------------------------------------------------------------
$AzureMcpBuild   = '3.0.0-beta.34+eec7acccddab1e16be852a3c3b9503cc9adf7538'
$SanitizerVer    = '1.0.0'
$ManifestSchema  = 1
$ExpectedRecords = 34

# ---- Small utilities -------------------------------------------------------
function Write-Fatal {
    param([string]$Message, [int]$Code = 1)
    Write-Host "FATAL: $Message" -ForegroundColor Red
    exit $Code
}

function ConvertTo-Lf {
    param([string]$Value)
    return ($Value -replace "`r`n", "`n") -replace "`r", "`n"
}

function Get-Sha256HexUpper {
    param([byte[]]$Bytes)
    $digest = [System.Security.Cryptography.SHA256]::HashData($Bytes)
    return ([System.BitConverter]::ToString($digest)).Replace('-', '')
}

function Get-FileSha256HexUpper {
    param([string]$FilePath)
    return Get-Sha256HexUpper -Bytes ([System.IO.File]::ReadAllBytes($FilePath))
}

function Write-Utf8NoBomLf {
    param([string]$FilePath, [string]$Content)
    $dir = Split-Path -Parent $FilePath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $lf = ConvertTo-Lf -Value $Content
    [System.IO.File]::WriteAllText($FilePath, $lf, ([System.Text.UTF8Encoding]::new($false)))
}

# ---- Sanitization ----------------------------------------------------------
# $Ctx: @{ RepoRoot = 'C:\...'; HostNames = @('CPC-...','...') }
function Get-SanitizedText {
    param([string]$Raw, [hashtable]$Ctx)

    $ic  = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    # Path separator as it appears in the in-memory text: 1-2 literal backslashes OR a slash.
    $sep = '(?:\\{1,2}|/)'
    $t   = $Raw

    # 1. Repo root (derived dynamically from the actual repo root, not hardcoded).
    $repoSegs = $Ctx.RepoRoot -split '[\\/]+' | Where-Object { $_ -ne '' }
    $reRepo   = ($repoSegs | ForEach-Object { [regex]::Escape($_) }) -join $sep
    $t = [regex]::Replace($t, $reRepo, '<REPO>', $ic)

    # 2. Temp dir first (most specific), then remaining user home.
    $reTemp = 'C:' + $sep + 'Users' + $sep + '[^\\/"]+' + $sep + 'AppData' + $sep + 'Local' + $sep + 'Temp'
    $t = [regex]::Replace($t, $reTemp, '<TEMP>', $ic)
    $reHome = 'C:' + $sep + 'Users' + $sep + '[^\\/"]+'
    $t = [regex]::Replace($t, $reHome, '<USER_HOME>', $ic)

    # 3. Literal username token.
    $t = [regex]::Replace($t, [regex]::Escape('diberry'), '<USER>', $ic)

    # 4. Host / machine name(s).
    foreach ($h in $Ctx.HostNames) {
        if (-not [string]::IsNullOrWhiteSpace($h)) {
            $t = [regex]::Replace($t, [regex]::Escape($h), '<HOST>', $ic)
        }
    }

    # 5. Pipeline temp GUID dirs -> collapse the 32-hex guid, retain the step number.
    $t = [regex]::Replace($t, 'pipeline-runner-step(\d+)-[0-9a-fA-F]{32}', 'pipeline-runner-step$1-<GUID>', $ic)

    # 6. Per-run output dir timestamp suffix.
    $t = [regex]::Replace($t, 'generated-([A-Za-z0-9_]+)-(\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2})', 'generated-$1-<RUNSTAMP>', $ic)

    # 7. Safety net: any remaining drive-letter absolute path token.
    #    Negative lookbehind on a letter keeps 'https://' etc. intact (only real X:\ drive roots).
    $t = [regex]::Replace($t, '(?<![A-Za-z])[A-Za-z]:' + $sep + '[^"\s]*', '<PATH>', $ic)

    return $t
}

# ---- Logical identity helpers ---------------------------------------------
function Get-RecordIdentity {
    # 4-tuple logical identity used to pair the catalog + namespace physical copies.
    param($Obj)
    return ('{0}|{1}|{2}|{3}' -f $Obj.namespace, $Obj.stepId, $Obj.artifactName, $Obj.recordedAtUtc)
}
function Get-ClassKey {
    # 3-tuple used to look a record up in the classification file.
    param([string]$Namespace, [int]$StepId, [string]$ArtifactName)
    return ('{0}|{1}|{2}' -f $Namespace, $StepId, ($ArtifactName.Trim().ToLowerInvariant()))
}

function Get-PropAlias {
    param($Obj, [string[]]$Names)
    foreach ($n in $Names) {
        $p = $Obj.PSObject.Properties[$n]
        if ($null -ne $p -and $null -ne $p.Value) { return $p.Value }
    }
    return $null
}

# ---- Classification loader -------------------------------------------------
# Supports TWO schemas (documented in scripts/baseline/README.md), matched robustly so
# ordering never matters:
#   (A) Parker's map-by-filename object: { "<catalogFileName>.json": { stableId,
#       classification, errorClass, hasUpstreamStep2, rationale }, ... }  (PRIMARY)
#   (B) Array/records schema: [{ namespace, stepId, artifactName, stableId, ... }]  (fallback)
# Returns @{ ByFile = @{fileName->entry}; ByClassKey = @{key->entry}; Entries = @(entry...) }
# where each entry is a shared-reference [ordered] hashtable carrying a 'used' flag.
function New-ClassEntry {
    param($Stable, $Role, $EClass, $Up, $Rat)
    return [ordered]@{
        stableId         = [string]$Stable
        role             = if ($Role)   { [string]$Role }   else { $null }
        errorClass       = if ($EClass) { [string]$EClass } else { $null }
        hasUpstreamStep2 = if ($null -ne $Up) { [bool]$Up } else { $null }
        rationale        = if ($Rat)    { [string]$Rat }    else { $null }
        used             = $false
    }
}

function Import-Classification {
    param([string]$Path)
    if (-not (Test-Path $Path)) { Write-Fatal "Classification file not found: $Path" 3 }
    try { $json = Get-Content $Path -Raw | ConvertFrom-Json -ErrorAction Stop }
    catch { Write-Fatal "Classification file is not valid JSON ($Path): $($_.Exception.Message)" 3 }

    $byFile     = @{}
    $byClassKey = @{}
    $entries    = New-Object System.Collections.Generic.List[object]

    # Detect an explicit array/records wrapper (schema B).
    $arr = $null
    if ($json -is [System.Array]) { $arr = $json }
    elseif ($json -is [System.Collections.IEnumerable] -and $json -isnot [string] -and
            $json.PSObject.Properties.Count -eq 0) { $arr = $json }
    else {
        foreach ($k in 'records', 'classifications', 'classification', 'items') {
            $p = $json.PSObject.Properties[$k]
            if ($null -ne $p -and $p.Value) { $arr = $p.Value; break }
        }
    }

    if ($null -ne $arr) {
        # Schema B: array of entries.
        foreach ($e in $arr) {
            $ns  = Get-PropAlias $e @('namespace', 'ns')
            $sid = Get-PropAlias $e @('stepId', 'step', 'stepid')
            $art = Get-PropAlias $e @('artifactName', 'artifact', 'tool', 'toolCommand')
            $stable = Get-PropAlias $e @('stableId', 'stableID', 'id', 'logicalId')
            if (-not $stable) { Write-Fatal "Classification entry missing stableId: $($e | ConvertTo-Json -Compress)" 3 }
            $entry = New-ClassEntry -Stable $stable `
                -Role (Get-PropAlias $e @('role', 'classification', 'class')) `
                -EClass (Get-PropAlias $e @('errorClass', 'error_class', 'defectClass')) `
                -Up (Get-PropAlias $e @('hasUpstreamStep2', 'hasUpstream', 'upstreamStep2')) `
                -Rat (Get-PropAlias $e @('rationale', 'reason', 'notes'))
            $entries.Add($entry)
            $srcFile = Get-PropAlias $e @('sourceFile', 'file', 'fileName', 'catalogFile')
            if ($srcFile) { $byFile[[string]$srcFile] = $entry }
            if ($ns -and $null -ne $sid -and $art) {
                $byClassKey[(Get-ClassKey -Namespace $ns -StepId ([int]$sid) -ArtifactName $art)] = $entry
            }
        }
    }
    else {
        # Schema A: object keyed by catalog filename, each value is a classification entry.
        foreach ($prop in $json.PSObject.Properties) {
            $e = $prop.Value
            $stable = Get-PropAlias $e @('stableId', 'stableID', 'id', 'logicalId')
            if (-not $stable) { Write-Fatal "Classification value for '$($prop.Name)' missing stableId." 3 }
            $entry = New-ClassEntry -Stable $stable `
                -Role (Get-PropAlias $e @('classification', 'role', 'class')) `
                -EClass (Get-PropAlias $e @('errorClass', 'error_class', 'defectClass')) `
                -Up (Get-PropAlias $e @('hasUpstreamStep2', 'hasUpstream', 'upstreamStep2')) `
                -Rat (Get-PropAlias $e @('rationale', 'reason', 'notes'))
            $entries.Add($entry)
            $byFile[[string]$prop.Name] = $entry
        }
    }

    if ($entries.Count -eq 0) { Write-Fatal "Classification file contained no usable entries." 3 }
    return @{ ByFile = $byFile; ByClassKey = $byClassKey; Entries = $entries }
}

# ---- Tool version capture (non-deterministic provenance, excluded from verify) ----
function Get-ToolVersions {
    $dotnet = try { (& dotnet --version) 2>$null | Select-Object -First 1 } catch { $null }
    $git    = try { (& git --version) 2>$null | Select-Object -First 1 } catch { $null }
    return [ordered]@{
        dotnet = if ($dotnet) { "$dotnet".Trim() } else { $null }
        pwsh   = $PSVersionTable.PSVersion.ToString()
        git    = if ($git) { "$git".Trim() } else { $null }
        os     = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription.Trim()
    }
}

# ---- Config / prompt hashing ----------------------------------------------
function Get-HashList {
    param([string]$RepoRoot, [string[]]$RelPaths)
    $out = [ordered]@{}
    foreach ($rel in ($RelPaths | Sort-Object)) {
        $full = Join-Path $RepoRoot ($rel -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        if (Test-Path $full) { $out[$rel] = Get-FileSha256HexUpper -FilePath $full }
        else { $out[$rel] = $null }
    }
    return $out
}

# ---- Secret scan -----------------------------------------------------------
function Invoke-SecretScan {
    param([string[]]$Files, [hashtable]$Ctx)
    $literal = @('diberry', 'C:\Users', $Ctx.RepoRoot)
    foreach ($h in $Ctx.HostNames) { if (-not [string]::IsNullOrWhiteSpace($h)) { $literal += $h } }
    $literal = $literal | Where-Object { $_ } | Select-Object -Unique
    $regexes = @(
        'password', 'apikey', 'api-key', 'secret', 'bearer ',
        'AccountKey=', 'SharedAccessSignature', 'eyJ[A-Za-z0-9_-]{6,}'
    )
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($f in $Files) {
        $text = Get-Content $f -Raw
        foreach ($lit in $literal) {
            if ($text.IndexOf($lit, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $hits.Add("$f :: literal '$lit'")
            }
        }
        foreach ($rx in $regexes) {
            $m = [regex]::Match($text, $rx, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($m.Success) { $hits.Add("$f :: pattern '$rx' -> '$($m.Value)'") }
        }
    }
    return $hits
}

# ---- Core build ------------------------------------------------------------
# Produces sanitized fixtures + manifest into $DestRoot. Returns an object carrying the
# deterministic subset (per-fixture bytes/hash + manifest records) for the verify pass.
function Build-Artifacts {
    param(
        [string]$DestRoot,
        [array]$CatalogFiles,
        [hashtable]$PhysicalByIdentity,
        [hashtable]$ClassMap,
        [hashtable]$Ctx,
        [string]$RunDirName,
        [string]$RepoRoot,
        [bool]$Quiet
    )

    $fixturesDir = Join-Path $DestRoot 'critical-failures'
    if (Test-Path $fixturesDir) { Remove-Item $fixturesDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $fixturesDir | Out-Null

    $records = New-Object System.Collections.Generic.List[object]
    $producedFiles = New-Object System.Collections.Generic.List[string]

    foreach ($cf in $CatalogFiles) {
        $rawBytes = [System.IO.File]::ReadAllBytes($cf.FullName)
        $rawText  = [System.IO.File]::ReadAllText($cf.FullName)  # includes BOM if present
        $obj      = $rawText | ConvertFrom-Json -ErrorAction Stop

        $ns  = [string]$obj.namespace
        $sid = [int]$obj.stepId
        $art = [string]$obj.artifactName
        # Preserve recordedAtUtc verbatim from raw text (ConvertFrom-Json coerces it to a
        # culture-dependent DateTime string, which would break deterministic diffs).
        $recMatch = [regex]::Match($rawText, '"recordedAtUtc"\s*:\s*"([^"]+)"')
        $rec = if ($recMatch.Success) { $recMatch.Groups[1].Value } else { [string]$obj.recordedAtUtc }

        # --- classification lookup: by catalog filename first, then logical class key ---
        $cls = $null
        if ($ClassMap.ByFile.ContainsKey($cf.Name)) { $cls = $ClassMap.ByFile[$cf.Name] }
        else {
            $ckey = Get-ClassKey -Namespace $ns -StepId $sid -ArtifactName $art
            if ($ClassMap.ByClassKey.ContainsKey($ckey)) { $cls = $ClassMap.ByClassKey[$ckey] }
        }
        if ($null -eq $cls) {
            Write-Fatal "No classification entry for catalog record '$($cf.Name)' ('$ns' step $sid '$art'). Classification must cover all $ExpectedRecords records." 4
        }
        $cls.used = $true
        $stableId = $cls.stableId

        # --- duplicate-copy accounting ---
        $ident = Get-RecordIdentity -Obj $obj
        if (-not $PhysicalByIdentity.ContainsKey($ident)) {
            Write-Fatal "No physical copies indexed for logical identity '$ident'." 5
        }
        $copies = $PhysicalByIdentity[$ident]
        if ($copies.Count -ne 2) {
            Write-Fatal "Record '$ident' has $($copies.Count) physical copies (expected exactly 2)." 5
        }
        $catalogCopy   = $copies | Where-Object { $_.IsCatalog } | Select-Object -First 1
        $namespaceCopy = $copies | Where-Object { -not $_.IsCatalog } | Select-Object -First 1
        if (-not $catalogCopy -or -not $namespaceCopy) {
            Write-Fatal "Record '$ident' does not have exactly one catalog + one namespace copy (catalog=$([bool]$catalogCopy), namespace=$([bool]$namespaceCopy))." 5
        }

        # --- sanitize (pure string replacement) + idempotency self-check ---
        $san1 = Get-SanitizedText -Raw $rawText -Ctx $Ctx
        $san2 = Get-SanitizedText -Raw $san1 -Ctx $Ctx
        $san1n = ConvertTo-Lf -Value $san1
        $san2n = ConvertTo-Lf -Value $san2
        if ($san1n -ne $san2n) {
            Write-Fatal "Sanitization is NOT idempotent for '$stableId' (second pass differs)." 6
        }

        # --- write fixture (LF, UTF-8 no BOM) ---
        $fixturePath = Join-Path $fixturesDir "$stableId.json"
        Write-Utf8NoBomLf -FilePath $fixturePath -Content $san1n
        $producedFiles.Add($fixturePath)

        $sourceSha    = Get-Sha256HexUpper -Bytes $rawBytes
        $sanitizedSha = Get-FileSha256HexUpper -FilePath $fixturePath

        $sourceRel = ($catalogCopy.Rel)
        $records.Add([ordered]@{
            stableId             = $stableId
            namespace            = $ns
            stepId               = $sid
            stepName             = [string]$obj.stepName
            artifactType         = [string]$obj.artifactType
            artifactName         = $art
            recordedAtUtc        = $rec
            classification       = $cls.role
            errorClass           = $cls.errorClass
            hasUpstreamStep2     = $cls.hasUpstreamStep2
            rationale            = $cls.rationale
            sourceRelativePath   = $sourceRel
            sourceSha256         = $sourceSha
            sanitizedRelativePath = "critical-failures/$stableId.json"
            sanitizedSha256      = $sanitizedSha
            physicalCopies       = @($catalogCopy.Rel, $namespaceCopy.Rel)
        })

        if (-not $Quiet) { Write-Host ("  [OK] {0,-40} <- {1}" -f $stableId, $catalogCopy.Rel) }
    }

    # Sort records by stableId for stable diffs.
    $sorted = $records | Sort-Object { $_.stableId }

    # --- provenance ---
    $repoCommit = try { (& git -C $RepoRoot rev-parse HEAD) 2>$null | Select-Object -First 1 } catch { $null }
    $promptFiles = @(
        'mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/prompts/system-prompt-example-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/prompts/user-prompt-example-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ExamplePrompts.Validation/prompts/system-prompt-example-prompt-validation.txt',
        'mcp-tools/DocGeneration.Steps.ExamplePrompts.Validation/prompts/user-prompt-example-prompt-validation.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/tool-family-cleanup-system-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/tool-family-cleanup-user-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/family-metadata-system-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/family-metadata-user-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/service-description-system-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/service-description-user-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/h2-heading-system-prompt.txt',
        'mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/h2-heading-user-prompt.txt'
    )
    $configFiles = @(
        'mcp-tools/data/common-parameters.json',
        'mcp-tools/data/brand-to-server-mapping.json',
        'mcp-tools/data/compound-words.json',
        'mcp-tools/data/stop-words.json',
        'mcp-tools/data/nl-parameters.json',
        'mcp-tools/data/nl-parameter-identifiers.json',
        'mcp-tools/data/static-text-replacement.json',
        'mcp-tools/data/service-doc-links.json',
        'mcp-tools/data/transformation-config.json',
        'mcp-tools/data/validation-gate-config.json'
    )

    $manifest = [ordered]@{
        schemaVersion = $ManifestSchema
        provenance = [ordered]@{
            repoCommitSha  = $repoCommit
            sourceRunDir   = $RunDirName
            azureMcpBuild  = $AzureMcpBuild
            sanitizerVersion = $SanitizerVer
            ai = [ordered]@{
                note = 'Model/deployment/apiVersion sourced from the public template mcp-tools/sample.env. temperature/seed are not set anywhere in code (SDK defaults apply) and are therefore null. Credential values in mcp-tools/.env were never accessed.'
                step2ExamplePrompts = [ordered]@{
                    model = 'gpt-4.1-mini'; deployment = 'gpt-4.1-mini'
                    apiVersion = '2025-01-01-preview'; temperature = $null; seed = $null
                }
                step4ToolFamilyCleanup = [ordered]@{
                    model = 'gpt-4o'; deployment = 'gpt-4o'
                    apiVersion = '2025-01-01-preview'; temperature = $null; seed = $null
                }
            }
            promptHashes = (Get-HashList -RepoRoot $RepoRoot -RelPaths $promptFiles)
            configHashes = (Get-HashList -RepoRoot $RepoRoot -RelPaths $configFiles)
            captureTimestampUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
            toolVersions = (Get-ToolVersions)
        }
        records = @($sorted)
    }

    $manifestPath = Join-Path $DestRoot 'beta34-baseline-manifest.json'
    $manifestJson = $manifest | ConvertTo-Json -Depth 32
    Write-Utf8NoBomLf -FilePath $manifestPath -Content $manifestJson
    $producedFiles.Add($manifestPath)

    return [pscustomobject]@{
        Records       = $sorted
        FixturesDir   = $fixturesDir
        ManifestPath  = $manifestPath
        ProducedFiles = $producedFiles
    }
}

# ============================================================================
# MAIN
# ============================================================================
$repoRoot = try { (& git rev-parse --show-toplevel) 2>$null | Select-Object -First 1 } catch { $null }
if (-not $repoRoot) { $repoRoot = (Get-Location).Path }
$repoRoot = (Resolve-Path $repoRoot).Path

function Resolve-InputPath {
    param([string]$P)
    if ([System.IO.Path]::IsPathRooted($P)) { return $P }
    return (Join-Path $repoRoot ($P -replace '/', [System.IO.Path]::DirectorySeparatorChar))
}

$runDir    = Resolve-InputPath $SourceRunPath
$outRoot   = Resolve-InputPath $OutputRoot
$classFile = Resolve-InputPath $ClassificationPath
$runDirName = Split-Path -Leaf ($runDir.TrimEnd('\', '/'))

if (-not (Test-Path $runDir)) { Write-Fatal "Source run path not found: $runDir" 2 }

# Build sanitization context (host names from current machine; not present in data but redacted defensively).
$hostName2 = $null
try { $hostName2 = (& hostname) 2>$null | Select-Object -First 1 } catch { $hostName2 = $null }
$hostNames = @($env:COMPUTERNAME, $hostName2) | Where-Object { $_ } | Select-Object -Unique
$ctx = @{
    RepoRoot  = $repoRoot
    HostNames = $hostNames
}

# ---- Catalog discovery + count gate ---------------------------------------
$catalogDir = Join-Path $runDir 'critical-failures'
if (-not (Test-Path $catalogDir)) { Write-Fatal "Catalog dir not found: $catalogDir" 2 }
$catalogFiles = @(Get-ChildItem $catalogDir -File -Filter *.json | Sort-Object Name)
if ($catalogFiles.Count -ne $ExpectedRecords) {
    Write-Fatal "Expected exactly $ExpectedRecords catalog records in '$catalogDir' but found $($catalogFiles.Count)." 2
}

# ---- Physical copy index (catalog + per-namespace), grouped by logical identity ----
$allPhysical = @(Get-ChildItem $runDir -Recurse -File -Filter *.json |
    Where-Object { $_.DirectoryName -match '[\\/]critical-failures$' })
$physicalByIdentity = @{}
foreach ($pf in $allPhysical) {
    try { $po = Get-Content $pf.FullName -Raw | ConvertFrom-Json -ErrorAction Stop } catch { continue }
    if ($null -eq $po.PSObject.Properties['namespace']) { continue }
    $ident = Get-RecordIdentity -Obj $po
    $isCatalog = ((Split-Path -Parent $pf.DirectoryName) -eq $runDir.TrimEnd('\', '/'))
    $rel = $pf.FullName.Substring($runDir.TrimEnd('\', '/').Length + 1) -replace '\\', '/'
    if (-not $physicalByIdentity.ContainsKey($ident)) {
        $physicalByIdentity[$ident] = New-Object System.Collections.Generic.List[object]
    }
    $physicalByIdentity[$ident].Add([pscustomobject]@{ Rel = $rel; IsCatalog = $isCatalog; Full = $pf.FullName })
}

# Fail closed on any record without exactly 2 physical copies (1 catalog + 1 namespace).
$badCopies = @()
foreach ($k in $physicalByIdentity.Keys) {
    $g = $physicalByIdentity[$k]
    $cat = @($g | Where-Object { $_.IsCatalog }).Count
    $nsc = @($g | Where-Object { -not $_.IsCatalog }).Count
    if ($g.Count -ne 2 -or $cat -ne 1 -or $nsc -ne 1) {
        $badCopies += "  $k => total=$($g.Count) catalog=$cat namespace=$nsc"
    }
}
if ($badCopies.Count -gt 0) {
    Write-Fatal ("Duplicate-copy accounting failed (each logical record needs exactly 1 catalog + 1 namespace copy):`n" + ($badCopies -join "`n")) 5
}

# ---- Classification ---------------------------------------------------------
$classMap = Import-Classification -Path $classFile
if ($classMap.Entries.Count -ne $ExpectedRecords) {
    Write-Fatal "Classification file has $($classMap.Entries.Count) entries but expected exactly $ExpectedRecords." 3
}

# ============================================================================
if ($VerifyOnly) {
    Write-Host "== VerifyOnly: regenerating into temp and comparing deterministic outputs ==" -ForegroundColor Cyan
    if (-not (Test-Path (Join-Path $outRoot 'beta34-baseline-manifest.json'))) {
        Write-Fatal "Committed baseline not found under '$outRoot' — run without -VerifyOnly first." 7
    }
    $tempRoot = Join-Path $repoRoot ("scripts/baseline/.verify-tmp-" + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    try {
        $fresh = Build-Artifacts -DestRoot $tempRoot -CatalogFiles $catalogFiles `
            -PhysicalByIdentity $physicalByIdentity -ClassMap $classMap -Ctx $ctx `
            -RunDirName $runDirName -RepoRoot $repoRoot -Quiet $true

        $scanHits = @(Invoke-SecretScan -Files @($fresh.ProducedFiles) -Ctx $ctx)
        if ($scanHits.Count -gt 0) { Write-Fatal ("Secret scan FAILED on regenerated files:`n" + ($scanHits -join "`n")) 8 }

        $drift = New-Object System.Collections.Generic.List[string]

        # (a) Fixture bytes must be byte-identical to committed.
        foreach ($r in $fresh.Records) {
            $freshFix     = Join-Path $tempRoot "critical-failures/$($r.stableId).json"
            $committedFix = Join-Path $outRoot  "critical-failures/$($r.stableId).json"
            if (-not (Test-Path $committedFix)) { $drift.Add("MISSING committed fixture: $($r.stableId).json"); continue }
            $fh = Get-FileSha256HexUpper -FilePath $freshFix
            $ch = Get-FileSha256HexUpper -FilePath $committedFix
            if ($fh -ne $ch) { $drift.Add("FIXTURE DRIFT $($r.stableId).json  fresh=$fh committed=$ch") }
        }

        # (b) Manifest record hashes (deterministic subset) must match committed.
        $committedManifest = Get-Content (Join-Path $outRoot 'beta34-baseline-manifest.json') -Raw | ConvertFrom-Json
        $committedById = @{}
        foreach ($cr in $committedManifest.records) { $committedById[$cr.stableId] = $cr }
        foreach ($r in $fresh.Records) {
            if (-not $committedById.ContainsKey($r.stableId)) { $drift.Add("MANIFEST: committed manifest missing record $($r.stableId)"); continue }
            $c = $committedById[$r.stableId]
            if ($c.sourceSha256 -ne $r.sourceSha256)       { $drift.Add("MANIFEST sourceSha256 drift $($r.stableId)") }
            if ($c.sanitizedSha256 -ne $r.sanitizedSha256) { $drift.Add("MANIFEST sanitizedSha256 drift $($r.stableId)") }
        }
        $committedCount = @($committedManifest.records).Count
        if ($committedCount -ne $ExpectedRecords) { $drift.Add("MANIFEST: committed record count $committedCount != $ExpectedRecords") }

        if ($drift.Count -gt 0) {
            Write-Fatal ("DETERMINISM DRIFT DETECTED (" + $drift.Count + " issue(s)):`n" + ($drift -join "`n")) 9
        }
        Write-Host "DETERMINISM VERIFIED: $($fresh.Records.Count)/$ExpectedRecords fixtures byte-identical; manifest record hashes match. Secret scan clean." -ForegroundColor Green
        exit 0
    }
    finally {
        if (Test-Path $tempRoot) { Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# ---- Normal generation ------------------------------------------------------
Write-Host "== Generating beta.34 baseline fixtures ==" -ForegroundColor Cyan
Write-Host "   source run : $runDirName"
Write-Host "   output root: $outRoot"
Write-Host "   classify   : $classFile"

$result = Build-Artifacts -DestRoot $outRoot -CatalogFiles $catalogFiles `
    -PhysicalByIdentity $physicalByIdentity -ClassMap $classMap -Ctx $ctx `
    -RunDirName $runDirName -RepoRoot $repoRoot -Quiet $false

# Orphan classification check (all entries must be consumed).
$orphans = @()
foreach ($entry in $classMap.Entries) { if (-not $entry.used) { $orphans += "  $($entry.stableId)" } }
if ($orphans.Count -gt 0) {
    Write-Fatal ("Classification has unused (orphan) entries not matched to any source record:`n" + ($orphans -join "`n")) 4
}

# Secret scan on every produced file.
$scan = @(Invoke-SecretScan -Files @($result.ProducedFiles) -Ctx $ctx)
if ($scan.Count -gt 0) {
    Write-Fatal ("Secret scan FAILED — produced files contain forbidden content:`n" + ($scan -join "`n")) 8
}

Write-Host ""
Write-Host "SUCCESS: $($result.Records.Count)/$ExpectedRecords fixtures written -> $($result.FixturesDir)" -ForegroundColor Green
Write-Host "Manifest: $($result.ManifestPath)"
Write-Host "Secret scan: CLEAN ($($result.ProducedFiles.Count) files scanned)."
exit 0
