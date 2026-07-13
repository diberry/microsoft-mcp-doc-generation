param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

Push-Location $repoRoot
try {
    $buildFlag = @()
    if ($SkipBuild) {
        $buildFlag = @("--no-build")
    }

    dotnet test .\mcp-doc-generation.sln --filter "Category=Keyless" @buildFlag
    $mcpExitCode = $LASTEXITCODE
    if ($mcpExitCode -eq 0) {
        Write-Host "MCP keyless suite: PASSED"
    }
    else {
        Write-Host "MCP keyless suite: FAILED"
    }

    dotnet test .\skills-generation\skills-generation.slnx --filter "Category=Keyless" @buildFlag
    $skillsExitCode = $LASTEXITCODE
    if ($skillsExitCode -eq 0) {
        Write-Host "Skills keyless suite: PASSED"
    }
    else {
        Write-Host "Skills keyless suite: FAILED"
    }

    if (($mcpExitCode -eq 0) -and ($skillsExitCode -eq 0)) {
        Write-Host "Keyless verification overall: PASSED"
        exit 0
    }

    Write-Host "Keyless verification overall: FAILED"
    exit 1
}
finally {
    Pop-Location
}
