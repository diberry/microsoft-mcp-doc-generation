#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pester tests for adaptive H2/H3 tool counting in Validate-ToolFamily-PostAssembly.ps1
#>

BeforeAll {
    # Dot-source the shared functions used by the validator
    $scriptRoot = $PSScriptRoot
    . "$scriptRoot\Shared-Functions.ps1"

    # Extract functions from the validator script for unit testing
    $validatorScript = Get-Content "$scriptRoot\Validate-ToolFamily-PostAssembly.ps1" -Raw
    $functionsToExtract = @(
        'Remove-Markup',
        'Convert-ToSlug',
        'Convert-CommandToToolKey',
        'Get-McpCliCommands',
        'Get-FrontmatterValue',
        'Get-SectionParameterRows',
        'Get-ArticleSections'
    )
    foreach ($funcName in $functionsToExtract) {
        $pattern = "(?ms)^function $funcName \{.*?^\}"
        $match = [regex]::Match($validatorScript, $pattern)
        if ($match.Success) {
            Invoke-Expression $match.Value
        }
    }
}

Describe 'Get-ArticleSections - Adaptive H2/H3 Tool Detection' {

    Context 'Multi-resource namespace (azurebackup pattern)' {
        It 'Should count 16 H3 tools across 9 H2 resource groups' {
            # Simulate azurebackup pattern: H2 groups with H3 tool sub-sections
            $article = @"
---
title: Azure Backup tools
tool_count: 16
---

## Vault Management

### list vaults

<!-- @mcpcli azurebackup list-vaults -->

Example prompts include:
- List all recovery services vaults

### create vault

<!-- @mcpcli azurebackup create-vault -->

Example prompts include:
- Create a new backup vault

## Protected Items

### list protected items

<!-- @mcpcli azurebackup list-protected-items -->

Example prompts include:
- Show all protected items

### backup now

<!-- @mcpcli azurebackup backup-now -->

Example prompts include:
- Run a backup now for my VM

## Policy Management

### list policies

<!-- @mcpcli azurebackup list-policies -->

Example prompts include:
- List backup policies

### create policy

<!-- @mcpcli azurebackup create-policy -->

Example prompts include:
- Create a new backup policy

## Job Monitoring

### list jobs

<!-- @mcpcli azurebackup list-jobs -->

Example prompts include:
- Show recent backup jobs

### get job

<!-- @mcpcli azurebackup get-job -->

Example prompts include:
- Get details of a backup job

## Recovery Points

### list recovery points

<!-- @mcpcli azurebackup list-recovery-points -->

Example prompts include:
- List recovery points for my VM

### restore

<!-- @mcpcli azurebackup restore -->

Example prompts include:
- Restore a VM from backup

## Disaster Recovery

### enable replication

<!-- @mcpcli azurebackup enable-replication -->

Example prompts include:
- Enable disaster recovery replication

### failover

<!-- @mcpcli azurebackup failover -->

Example prompts include:
- Initiate a failover

## Governance

### list compliance

<!-- @mcpcli azurebackup list-compliance -->

Example prompts include:
- Show backup compliance status

### configure alerts

<!-- @mcpcli azurebackup configure-alerts -->

Example prompts include:
- Configure backup alerts

## Resource Groups

### list resource groups

<!-- @mcpcli azurebackup list-resource-groups -->

Example prompts include:
- List resource groups with backup resources

## Reports

### generate report

<!-- @mcpcli azurebackup generate-report -->

Example prompts include:
- Generate a backup report

## Related content

See also Azure Backup documentation.
"@

            $result = Get-ArticleSections -ArticleContent $article -NamespaceName 'azurebackup'
            $result.Sections.Count | Should -Be 16
            $result.DetectionMode | Should -Be 'multi-resource'
        }
    }

    Context 'Single-resource namespace (flat H2 tools)' {
        It 'Should count H2 sections as tools with no regression' {
            $article = @"
---
title: Azure SQL tools
tool_count: 3
---

## list databases

<!-- @mcpcli sql list-databases -->

Example prompts include:
- List all SQL databases

## get database

<!-- @mcpcli sql get-database -->

Example prompts include:
- Get details of my SQL database

## create database

<!-- @mcpcli sql create-database -->

Example prompts include:
- Create a new SQL database

## Related content

See also Azure SQL documentation.
"@

            $result = Get-ArticleSections -ArticleContent $article -NamespaceName 'sql'
            $result.Sections.Count | Should -Be 3
            $result.DetectionMode | Should -Be 'single-resource'
        }

        It 'Should preserve tool keys for single-resource tools' {
            $article = @"
---
title: Azure Compute tools
tool_count: 2
---

## list vms

<!-- @mcpcli compute list-vms -->

Example prompts include:
- List all virtual machines

## create vm

<!-- @mcpcli compute create-vm -->

Example prompts include:
- Create a new virtual machine
"@

            $result = Get-ArticleSections -ArticleContent $article -NamespaceName 'compute'
            $result.Sections[0].ToolKey | Should -Be 'list-vms'
            $result.Sections[1].ToolKey | Should -Be 'create-vm'
        }
    }

    Context 'Mixed content - H3s that are NOT tools should be excluded' {
        It 'Should exclude Overview, Prerequisites, Parameters, Examples, Remarks, Related H3s' {
            $article = @"
---
title: Azure Storage tools
tool_count: 2
---

## Blob Operations

### Overview

This section covers blob operations.

### upload blob

<!-- @mcpcli storage upload-blob -->

Example prompts include:
- Upload a file to blob storage

### Parameters

Common parameters for blob operations.

### download blob

<!-- @mcpcli storage download-blob -->

Example prompts include:
- Download a blob

### Examples

Here are additional examples.

### Remarks

Note about usage.

### Prerequisites

You need a storage account.

### Related

See blob documentation.
"@

            $result = Get-ArticleSections -ArticleContent $article -NamespaceName 'storage'
            $result.Sections.Count | Should -Be 2
            $result.Sections[0].Heading | Should -Be 'upload blob'
            $result.Sections[1].Heading | Should -Be 'download blob'
        }
    }

    Context 'Edge case - Empty H2 section with no H3s' {
        It 'Should count empty H2 as a tool (single-resource behavior)' {
            $article = @"
---
title: Azure Redis tools
tool_count: 2
---

## list caches

<!-- @mcpcli redis list-caches -->

Example prompts include:
- List Redis caches

## flush cache

"@

            $result = Get-ArticleSections -ArticleContent $article -NamespaceName 'redis'
            $result.Sections.Count | Should -Be 2
            $result.Sections[1].Heading | Should -Be 'flush cache'
        }
    }

    Context 'Edge case - Mixed H2 behavior (some with H3, some without)' {
        It 'Should handle H2s with H3 tools alongside H2s without H3s' {
            $article = @"
---
title: Azure Mixed tools
tool_count: 3
---

## Resource Group

### list items

<!-- @mcpcli mixed list-items -->

Example prompts include:
- List items

### delete item

<!-- @mcpcli mixed delete-item -->

Example prompts include:
- Delete an item

## standalone tool

<!-- @mcpcli mixed standalone-tool -->

Example prompts include:
- Run the standalone tool
"@

            $result = Get-ArticleSections -ArticleContent $article -NamespaceName 'mixed'
            $result.Sections.Count | Should -Be 3
            $result.DetectionMode | Should -Be 'multi-resource'
        }
    }
}
