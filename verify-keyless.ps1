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
    dotnet test .\skills-generation\skills-generation.slnx --filter "Category=Keyless" @buildFlag
}
finally {
    Pop-Location
}
