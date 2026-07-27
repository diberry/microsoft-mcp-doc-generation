<#
.SYNOPSIS
    Summarizes metadata generation status for newly detected Azure MCP releases.

.DESCRIPTION
    This script reads the Step 1 release-detection artifact or an explicit
    version list, checks the metadata repo for generated folders, and records
    which versions are ready, pending, or already merged for downstream review.

.INPUTS
    Step 1 JSON artifact and metadata repo contents.

.OUTPUTS
    JSON artifact: echo-metadata-generation-{timestamp}.json
    Report: echo-metadata-generation-{timestamp}.md
#>
#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$MetadataRepoPath,
    [ValidateNotNullOrEmpty()]
    [string]$MetadataGenerationScript,
    [ValidateNotNullOrEmpty()]
    [string[]]$VersionList,
    [ValidateNotNullOrEmpty()]
    [string]$OutputDir,
    [ValidateNotNullOrEmpty()]
    [string]$Step1OutputPath,
    [ValidateNotNullOrEmpty()]
    [string]$SkillsRoot,
    [ValidateNotNullOrEmpty()]
    [string]$SkillsCatalogScript,
    [ValidateNotNullOrEmpty()]
    [string]$SkillsReadmeScript,
    [switch]$PlanOnly,
    [switch]$GenerateMetadata,
    [switch]$ChainedFromOrchestrator
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SkillDir = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $SkillDir))
. (Join-Path $RepoRoot '.github\skills\echo-release-detection\scripts\shared-helpers.ps1')

$METADATA_REPO_PATH = 'repos/public-diberry-microsoft-mcp-doc-generation'
$METADATA_GENERATION_SCRIPT = './start.sh'
$BRANCH_PREFIX = 'squad/azure-mcp-'
$OUTPUT_DIR = 'projects/azure-ai-tools/status'
$SKILLS_ROOT = '.github/skills'
$SKILLS_CATALOG_SCRIPT = '.github/skills/generate-skills-catalog.ps1'
$SKILLS_README_SCRIPT = '.github/skills/_scripts/Generate-SkillsReadme.ps1'
$REPORT_TEMPLATE_PATH = Join-Path $SkillDir 'templates\report-template.md'

<#
.SYNOPSIS
    Resolves the Step 1 JSON artifact.
.PARAMETER ExplicitPath
    Optional explicit Step 1 JSON path.
.PARAMETER SearchDirectory
    Output directory used for latest-artifact lookup.
#>
function Resolve-Step1Artifact {
    [CmdletBinding()]
    param(
        [string]$ExplicitPath,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$SearchDirectory
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolvedPath = Resolve-ConfiguredPath -Value $ExplicitPath -RepoRoot $RepoRoot
        Assert-FileExists -Path $resolvedPath -Description 'Step 1 JSON artifact'
        return Get-Item -Path $resolvedPath
    }

    return Get-LatestJsonArtifact -Directory $SearchDirectory -Pattern 'echo-release-detection-*.json'
}

<#
.SYNOPSIS
    Invokes the external metadata generation script for missing versions.
.DESCRIPTION
    Contract: the script receives the version list as a comma-delimited value.
    PowerShell scripts receive `-VersionList`, while shell or executable entry
    points receive the version list as the first argument. The same value is
    also exposed through the `ECHO_VERSION_LIST` environment variable.
.PARAMETER ScriptPath
    Resolved metadata generation entry point.
.PARAMETER Versions
    Versions that still need metadata folders.
.PARAMETER WorkingDirectory
    Working directory for the external script.
#>
function Invoke-MetadataGenerationScript {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$ScriptPath,

        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [string[]]$Versions,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$WorkingDirectory
    )

    Assert-FileExists -Path $ScriptPath -Description 'Metadata generation script'

    $versionArgument = ($Versions -join ',')
    $previousVersionList = $env:ECHO_VERSION_LIST

    try {
        $env:ECHO_VERSION_LIST = $versionArgument

        Push-Location $WorkingDirectory
        try {
            $commandOutput = switch ([System.IO.Path]::GetExtension($ScriptPath).ToLowerInvariant()) {
                '.ps1' { & pwsh -NoProfile -File $ScriptPath -VersionList $versionArgument 2>&1 }
                '.sh' { 
                    # For .sh scripts that wrap dotnet CLI, call the underlying .NET project directly
                    # This avoids bash/WSL PATH issues on Windows
                    $runnerProject = Join-Path $WorkingDirectory "mcp-tools\DocGeneration.PipelineRunner\DocGeneration.PipelineRunner.csproj"
                    & dotnet run --project $runnerProject -- --steps "1,2,3,4,5,6" 2>&1
                }
                default { & $ScriptPath $versionArgument 2>&1 }
            }
        }
        finally {
            Pop-Location
        }

        if ($LASTEXITCODE -ne 0) {
            $message = if ($commandOutput) { ($commandOutput | Out-String).Trim() } else { 'No output captured.' }
            throw "The metadata generation script exited with code $LASTEXITCODE. $message"
        }
    }
    finally {
        if ($null -eq $previousVersionList) {
            Remove-Item Env:\ECHO_VERSION_LIST -ErrorAction SilentlyContinue
        }
        else {
            $env:ECHO_VERSION_LIST = $previousVersionList
        }
    }
}

function Get-EchoAssetInventory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Root
    )

    $echoSkillNames = @('echo-release-detection', 'echo-metadata-generation', 'echo-content-impact')
    $assets = foreach ($skillName in $echoSkillNames) {
        $skillPath = Join-Path $Root $skillName
        if (-not (Test-Path $skillPath -PathType Container)) {
            continue
        }

        foreach ($file in Get-ChildItem -Path $skillPath -Recurse -File) {
            $extension = $file.Extension.ToLowerInvariant()
            $category = switch ($extension) {
                '.md' { 'markdown' }
                '.ps1' { 'script' }
                '.sh' { 'script' }
                default { 'other' }
            }

            [pscustomobject]@{
                skill    = $skillName
                category = $category
                path     = $file.FullName.Substring($RepoRoot.Length + 1) -replace '\\', '/'
            }
        }
    }

    return @($assets | Sort-Object skill, category, path)
}

function Test-VersionIsReleased {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Version
    )

    # Version is "released" if it does NOT contain beta, preview, rc, alpha, or similar pre-release markers
    # Examples:
    #   3.0.0 → released
    #   3.0.0-beta.20 → NOT released (contains -beta)
    #   3.0.0-rc.1 → NOT released (contains -rc)
    #   3.1.0 → released
    
    $preReleasePatterns = @('-beta', '-preview', '-rc', '-alpha', '-pre', '-dev')
    
    foreach ($pattern in $preReleasePatterns) {
        if ($Version -like "*$pattern*") {
            return $false
        }
    }
    
    return $true
}

function Get-CatalogSkillIds {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$CatalogPath
    )

    if (-not (Test-Path $CatalogPath -PathType Leaf)) {
        return @()
    }

    $catalog = Get-Content -Path $CatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
    return @($catalog.skills | ForEach-Object { $_.id } | Sort-Object -Unique)
}

function Invoke-CatalogRefresh {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$CatalogScriptPath,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$ReadmeScriptPath,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$WorkingDirectory
    )

    Assert-FileExists -Path $CatalogScriptPath -Description 'skills catalog generator'
    Assert-FileExists -Path $ReadmeScriptPath -Description 'skills README generator'

    Push-Location $WorkingDirectory
    try {
        $null = & pwsh -NoProfile -File $CatalogScriptPath 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Catalog generator exited with code $LASTEXITCODE."
        }

        $null = & pwsh -NoProfile -File $ReadmeScriptPath 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "README generator exited with code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

$resolvedMetadataRepoPath = Resolve-ConfiguredPath -Value $(if ($MetadataRepoPath) { $MetadataRepoPath } else { $METADATA_REPO_PATH }) -RepoRoot $RepoRoot
$resolvedOutputDir = Resolve-ConfiguredPath -Value $(if ($OutputDir) { $OutputDir } else { $OUTPUT_DIR }) -RepoRoot $RepoRoot
$resolvedGenerationScript = Resolve-ConfiguredPath -Value $(if ($MetadataGenerationScript) { $MetadataGenerationScript } else { $METADATA_GENERATION_SCRIPT }) -RepoRoot $RepoRoot -BasePath $resolvedMetadataRepoPath
$resolvedSkillsRoot = Resolve-ConfiguredPath -Value $(if ($SkillsRoot) { $SkillsRoot } else { $SKILLS_ROOT }) -RepoRoot $RepoRoot
$resolvedCatalogScript = Resolve-ConfiguredPath -Value $(if ($SkillsCatalogScript) { $SkillsCatalogScript } else { $SKILLS_CATALOG_SCRIPT }) -RepoRoot $RepoRoot
$resolvedReadmeScript = Resolve-ConfiguredPath -Value $(if ($SkillsReadmeScript) { $SkillsReadmeScript } else { $SKILLS_README_SCRIPT }) -RepoRoot $RepoRoot
$catalogPath = Join-Path $resolvedSkillsRoot 'skills.json'
$readmePath = Join-Path $resolvedSkillsRoot 'README.md'
$metadataRoot = Join-Path $resolvedMetadataRepoPath 'mcp-cli-metadata'

Assert-DirectoryExists -Path $resolvedMetadataRepoPath -Description 'Metadata repository'
# Metadata folder is created by -MetadataGenerationScript if missing (for new releases).
Ensure-Directory -Path $resolvedOutputDir -Description 'output directory'

$versions = Normalize-VersionList -Versions $VersionList
$incomingCorrelationId = $null
if ($versions.Count -eq 0) {
    $step1Artifact = Resolve-Step1Artifact -ExplicitPath $Step1OutputPath -SearchDirectory $resolvedOutputDir

    if ($step1Artifact) {
        $step1Envelope = Get-Content -Path $step1Artifact.FullName -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
        $incomingCorrelationId = if ($step1Envelope.metadata -and $step1Envelope.metadata.correlationId) { $step1Envelope.metadata.correlationId } else { $null }
        $step1 = if ($step1Envelope.ContainsKey('result') -and $null -ne $step1Envelope.result) { $step1Envelope.result } else { $step1Envelope }
        $versions = Normalize-VersionList -Versions $step1.NEW_VERSIONS
        if ($versions.Count -eq 0 -and $step1.CONTEXT_VERSIONS) {
            $versions = Normalize-VersionList -Versions $step1.CONTEXT_VERSIONS
        }
    }
}

$timestampUtc = (Get-Date).ToUniversalTime()
$timestampIso = $timestampUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
$fileTimestamp = $timestampUtc.ToString('yyyy-MM-ddTHH-mm-ss-fffZ')
$reportPath = Join-Path $resolvedOutputDir ("echo-metadata-generation-$fileTimestamp.md")
$jsonPath = Join-Path $resolvedOutputDir ("echo-metadata-generation-$fileTimestamp.json")

$missingVersions = @(
    foreach ($version in $versions) {
        if (-not (Get-VersionFolder -MetadataRoot $metadataRoot -Version $version)) {
            $version
        }
    }
)

# Separate missing versions into released and pre-release
# NOTE: Azure MCP Server ships beta/pre-release builds twice weekly and this team documents
# those betas as first-class releases — we do NOT wait for GA. All missing versions (including
# beta/preview/rc) are candidates for metadata generation. Test-VersionIsReleased is retained
# only for the informational release_status label (GA vs Pre-release), not as a generation gate.
$missingReleasedVersions = $missingVersions
$missingPreReleaseVersions = @()

# Step 2 Decision Gate: Report what needs generation and ask user
if ($missingVersions.Count -gt 0) {
    Write-Host ""
    Write-Host "📋 Metadata Status Check" -ForegroundColor Cyan
    Write-Host "========================================"
    
    # Show pre-release versions first (waiting status)
    if ($missingPreReleaseVersions.Count -gt 0) {
        Write-Host "⏳ Pre-release versions (waiting for GA release):" -ForegroundColor Yellow
        foreach ($version in $missingPreReleaseVersions) {
            Write-Host "  • $version (beta/preview — not yet released, will skip generation)" -ForegroundColor DarkYellow
        }
        Write-Host ""
    }
    
    # Show released versions that need generation
    if ($missingReleasedVersions.Count -gt 0) {
        Write-Host "✓ Versions ready for metadata generation (includes beta/pre-release):" -ForegroundColor Green
        foreach ($version in $missingReleasedVersions) {
            Write-Host "  • $version" -ForegroundColor Green
        }
        Write-Host ""
        
        if (-not $PlanOnly) {
            Write-Host "⚠️  Generation requires running the full documentation pipeline:" -ForegroundColor Yellow
            Write-Host "    ./start.sh $($missingReleasedVersions -join ',')"
            Write-Host ""
            Write-Host "This will take 15-30 minutes with Azure OpenAI calls." -ForegroundColor Yellow
            Write-Host ""
            Write-Host "To generate metadata, run:" -ForegroundColor Green
            Write-Host "  pwsh .github/skills/echo-metadata-generation/scripts/echo-metadata-generation.ps1 -VersionList '$($missingReleasedVersions -join ',')' -GenerateMetadata"
            Write-Host ""
            Write-Host "⏸️  PAUSING for your decision. Report shows current status below." -ForegroundColor Magenta
        }
    } else {
        Write-Host "ℹ️  All missing versions are pre-release. Skipping generation." -ForegroundColor Cyan
        Write-Host ""
    }
}

# Only generate if explicitly requested via -GenerateMetadata switch
# AND only for released versions
if (($missingReleasedVersions.Count -gt 0) -and $GenerateMetadata -and -not $PlanOnly) {
    Write-Host ""
    Write-Host "🔄 Generating metadata for: $($missingReleasedVersions -join ', ')" -ForegroundColor Cyan
    Invoke-MetadataGenerationScript -ScriptPath $resolvedGenerationScript -Versions $missingReleasedVersions -WorkingDirectory $resolvedMetadataRepoPath
    Write-Host "✅ Metadata generation complete." -ForegroundColor Green
    Write-Host ""
}

$prDetails = @()
foreach ($version in $versions) {
    $folder = Get-VersionFolder -MetadataRoot $metadataRoot -Version $version
    $isReleased = Test-VersionIsReleased -Version $version
    
    if ($folder) {
        $prDetails += [pscustomobject]@{
            version         = $version
            pr_number       = $null
            pr_url          = $null
            status          = $(if ($missingVersions -contains $version) { 'generated' } else { 'merged' })
            release_status  = if ($isReleased) { 'GA' } else { 'Pre-release' }
            branch          = "$BRANCH_PREFIX$version"
            metadata_folder = $folder.Name
        }
        continue
    }

    if ($PlanOnly) {
        $statusDisplay = if ($missingReleasedVersions -contains $version) { 'ready-for-generation' } else { 'planned' }

        $prDetails += [pscustomobject]@{
            version         = $version
            pr_number       = $null
            pr_url          = $null
            status          = $statusDisplay
            release_status  = if ($isReleased) { 'GA' } else { 'Pre-release' }
            branch          = "$BRANCH_PREFIX$version"
            metadata_folder = $null
        }
        continue
    }

    # No metadata folder after a generation attempt — contract violation (applies to beta too).
    throw "Metadata generation did not produce a folder for $version. Script contract: '$resolvedGenerationScript' must create mcp-cli-metadata\$version* for each version passed through -VersionList or the first positional argument."
}

$mergedCount = @($prDetails | Where-Object status -eq 'merged').Count
$pendingCount = @($prDetails | Where-Object status -ne 'merged').Count
$generatedCount = @($prDetails | Where-Object status -eq 'generated').Count
$assetInventory = Get-EchoAssetInventory -Root $resolvedSkillsRoot
$markdownAssets = @($assetInventory | Where-Object category -eq 'markdown')
$scriptAssets = @($assetInventory | Where-Object category -eq 'script')
$catalogIdsBefore = Get-CatalogSkillIds -CatalogPath $catalogPath
Invoke-CatalogRefresh -CatalogScriptPath $resolvedCatalogScript -ReadmeScriptPath $resolvedReadmeScript -WorkingDirectory $RepoRoot
$catalogIdsAfter = Get-CatalogSkillIds -CatalogPath $catalogPath
$trackedEchoSkillIds = @('echo-release-detection', 'echo-metadata-generation', 'echo-content-impact')
$catalogAdded = @($trackedEchoSkillIds | Where-Object { $catalogIdsBefore -notcontains $_ -and $catalogIdsAfter -contains $_ })
$catalogPresent = @($trackedEchoSkillIds | Where-Object { $catalogIdsAfter -contains $_ })
$catalogStatus = if ($catalogAdded.Count -gt 0) {
    "Added to skills catalog: $($catalogAdded -join ', ')"
}
elseif ($catalogPresent.Count -eq $trackedEchoSkillIds.Count) {
    'Echo skills were already present in the skills catalog; catalog artifacts were refreshed.'
}
else {
    "Catalog refresh completed, but missing entries remain: $((@($trackedEchoSkillIds | Where-Object { $catalogIdsAfter -notcontains $_ }) -join ', '))"
}

$tableRows = if ($prDetails.Count -eq 0) {
    '| None | n/a | n/a | No work detected | n/a |'
}
else {
    $prDetails | ForEach-Object {
        $prLink = if ($_.pr_url) { "[#$($_.pr_number)]($($_.pr_url))" } else { 'n/a' }
        "| $($_.version) | $($_.release_status) | $($_.status) | $(if ($_.metadata_folder) { $_.metadata_folder } else { 'not-generated' }) |"
    }
}

$gateMessage = if ($pendingCount -gt 0) {
    'User merge gate is still active for versions that are planned, generated locally, or have open PRs.'
}
else {
    'No merge gate is blocking Step 3 because all requested versions already exist in main.'
}

$assetRows = if ($assetInventory.Count -eq 0) {
    '| none | none | none |'
}
else {
    $assetInventory | ForEach-Object { "| $($_.skill) | $($_.category) | $($_.path) |" }
}

$reportValues = @{
    REPORT_TITLE     = 'Echo — Metadata Generation Report'
    TIMESTAMP        = $timestampIso
    INPUT_SOURCES    = "- Step 1 JSON: $(if ($Step1OutputPath) { $Step1OutputPath } else { 'latest artifact lookup' })`n- Metadata repo: $resolvedMetadataRepoPath`n- Generation entry point: $resolvedGenerationScript"
    SUMMARY          = "Versions processed: $($versions.Count)`n- Merged/already present: $mergedCount`n- Pending/planned: $pendingCount`n- Generated this run: $generatedCount"
    PR_TABLE         = ($tableRows -join "`n")
    ASSET_SUMMARY    = "Markdown assets: $($markdownAssets.Count)`nScripts: $($scriptAssets.Count)`nCatalog status: $catalogStatus"
    ASSET_TABLE      = ($assetRows -join "`n")
    USER_MERGE_GATE  = $gateMessage
    NEXT_STEPS       = $(if ($versions.Count -gt 0) { 'Proceed to Step 3 with the same version list.' } else { 'No Step 3 work is required until Step 1 finds a new version.' })
    CROSS_REFERENCES = '- Step 1 report pattern: `echo-release-detection-{timestamp}.md`' + "`n" + '- Step 3 report pattern: `echo-content-impact-{timestamp}.md`'
}

$reportContent = Render-Template -TemplatePath $REPORT_TEMPLATE_PATH -Values $reportValues
Set-Content -Path $reportPath -Value $reportContent -Encoding UTF8

$resultPayload = [ordered]@{
    TIMESTAMP         = $timestampIso
    FILE_TIMESTAMP    = $fileTimestamp
    VERSION_LIST      = $versions
    PR_DETAILS        = $prDetails
    MERGED_COUNT      = $mergedCount
    PENDING_COUNT     = $pendingCount
    GENERATED_COUNT   = $generatedCount
    ASSET_INVENTORY   = $assetInventory
    MARKDOWN_ASSET_COUNT = $markdownAssets.Count
    SCRIPT_ASSET_COUNT = $scriptAssets.Count
    CATALOG_STATUS    = $catalogStatus
    CATALOG_SKILLS_PRESENT = $catalogPresent
    CATALOG_SKILLS_ADDED = $catalogAdded
    README_PATH       = $readmePath
    CATALOG_PATH      = $catalogPath
    GENERATION_SCRIPT = $resolvedGenerationScript
    REPORT_PATH       = $reportPath
    JSON_PATH         = $jsonPath
    nextStep          = if ($versions.Count -gt 0 -and $pendingCount -eq 0) { 'echo-content-impact' } elseif ($pendingCount -gt 0) { 'user-merge-gate' } else { $null }
}
$correlationVersion = if ($versions.Count -gt 0) { $versions[0] } else { 'no-version' }
$correlationId = if ($incomingCorrelationId) { $incomingCorrelationId } else { "echo-azure-mcp-$correlationVersion" }
$output = New-StructuredOutputEnvelope -Result $resultPayload -Producer 'echo-metadata-generation' -Schema 'echo-metadata-generation@1.0.0' -CorrelationId $correlationId

$outputJson = $output | ConvertTo-Json -Depth 100
Set-Content -Path $jsonPath -Value $outputJson -Encoding UTF8
$outputJson

# ── Chain to Step 3 ──────────────────────────────────────────────────────────
if (-not $ChainedFromOrchestrator) {
    Write-Host '[INFO] 🔗 Chaining to Step 3: echo-content-impact...'
    $step3Script = Join-Path $ScriptDir '..\..\echo-content-impact\scripts\echo-content-impact.ps1'
    & pwsh -NoProfile -File $step3Script -ChainedFromOrchestrator
}
