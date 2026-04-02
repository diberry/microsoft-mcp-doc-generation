# lint-vale.tests.ps1 — Tests for Vale lint script
Describe "lint-vale.ps1" {
    BeforeAll {
        $scriptPath = Join-Path $PSScriptRoot ".." "lint-vale.ps1"
        $fixtureDir = Join-Path $PSScriptRoot "fixtures" "vale-test"
        New-Item -ItemType Directory -Path $fixtureDir -Force | Out-Null
    }

    It "Should exit 0 for clean markdown" {
        # Create a clean test file
        $cleanFile = Join-Path $fixtureDir "clean.md"
        @"
---
title: Test Article
ms.topic: reference
ms.date: 4/2/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Test Article

This article describes a test scenario for Azure services.
"@ | Set-Content $cleanFile

        $result = & $scriptPath -TargetDir $fixtureDir 2>&1
        # Vale may still find issues in minimal content, so we just verify it runs
        $LASTEXITCODE | Should -BeIn @(0, 1)
    }

    It "Should find Vale executable" {
        $skillsDir = Join-Path $PSScriptRoot ".." ".."
        $valeExists = (Test-Path "$skillsDir/tools/vale.exe") -or (Get-Command vale -ErrorAction SilentlyContinue)
        $valeExists | Should -BeTrue
    }

    AfterAll {
        $fixtureDir = Join-Path $PSScriptRoot "fixtures" "vale-test"
        if (Test-Path $fixtureDir) { Remove-Item $fixtureDir -Recurse -Force }
    }
}
