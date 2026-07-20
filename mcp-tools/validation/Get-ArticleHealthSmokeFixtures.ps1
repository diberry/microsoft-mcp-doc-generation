param(
    [string]$FixturesDir = (Join-Path $PSScriptRoot "tests\fixtures")
)

$ErrorActionPreference = "Stop"

$smokeFixtures = @(
    "valid-article.md"
)

foreach ($fixture in $smokeFixtures) {
    $path = Join-Path $FixturesDir $fixture
    if (-not (Test-Path $path)) {
        Write-Error "Article Health smoke fixture not found: $path"
        exit 1
    }

    (Get-Item $path).FullName
}
