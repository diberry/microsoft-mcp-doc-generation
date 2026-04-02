# lint-vale.Tests.ps1 — Tests for Vale lint script (docs-generation)
Describe "lint-vale.ps1" {
    BeforeAll {
        $scriptPath = Join-Path $PSScriptRoot ".." "lint-vale.ps1"
        $fixtureDir = Join-Path $PSScriptRoot "fixtures" "vale-test"
        New-Item -ItemType Directory -Path $fixtureDir -Force | Out-Null
    }

    It "Script file should exist" {
        $scriptPath | Should -Exist
    }

    It "Should be valid PowerShell syntax" {
        $errors = $null
        [System.Management.Automation.PSParser]::Tokenize(
            (Get-Content $scriptPath -Raw), [ref]$errors
        )
        $errors.Count | Should -Be 0
    }

    It "Should have TargetDir parameter" {
        $content = Get-Content $scriptPath -Raw
        $content | Should -Match 'param\s*\('
        $content | Should -Match '\[string\]\s*\$TargetDir'
    }

    It "Should reference .vale.ini config" {
        $content = Get-Content $scriptPath -Raw
        $content | Should -Match '\.vale\.ini'
    }

    It "Should exit 0 for clean markdown" {
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
        $LASTEXITCODE | Should -Be 0
    }

    It "Should find Vale executable" {
        $docsGenDir = Join-Path $PSScriptRoot ".." ".."
        $valeExists = (Test-Path "$docsGenDir/tools/vale.exe") -or
                      (Get-Command vale -ErrorAction SilentlyContinue) -or
                      (Test-Path (Join-Path (Split-Path -Parent $docsGenDir) "vale_bin" "vale.exe"))
        $valeExists | Should -BeTrue
    }

    AfterAll {
        $fixtureDir = Join-Path $PSScriptRoot "fixtures" "vale-test"
        if (Test-Path $fixtureDir) { Remove-Item $fixtureDir -Recurse -Force }
    }
}
