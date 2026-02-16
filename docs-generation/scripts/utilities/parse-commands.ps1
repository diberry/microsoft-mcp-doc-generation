#!/usr/bin/env pwsh

# Parse CLI output JSON and create structured CSV
param(
    [Parameter(Mandatory=$true)]
    [string]$InputJsonPath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputCsvPath
)

# Read and parse the JSON file
$jsonContent = Get-Content $InputJsonPath -Raw | ConvertFrom-Json

# Create array to hold the parsed commands
$commands = @()

# Process each result
foreach ($result in $jsonContent.results) {
    $command = $result.command
    
    # Split the command into parts
    $parts = $command -split '\s+'
    
    if ($parts.Length -ge 4) {
        # Four or more parts: namespace family tool operation
        $namespace = $parts[0]
        $family = $parts[1]
        $tool = $parts[2]
        $operation = $parts[3]
    } elseif ($parts.Length -eq 3) {
        # Three parts: namespace family operation
        $namespace = $parts[0]
        $family = $parts[1]
        $tool = $parts[2]
        $operation = ""
    } elseif ($parts.Length -eq 2) {
        # Two parts: namespace operation
        $namespace = $parts[0]
        $family = ""
        $tool = $parts[1]
        $operation = ""
    } else {
        # Single part: just namespace
        $namespace = $parts[0]
        $family = ""
        $tool = ""
        $operation = ""
    }
    
    $commands += [PSCustomObject]@{
        Namespace = $namespace
        Family = $family
        Tool = $tool
        Operation = $operation
    }
}

# Sort by namespace, then family, then tool, then operation
$commands = $commands | Sort-Object Namespace, Family, Tool, Operation

# Export to CSV
$commands | Export-Csv -Path $OutputCsvPath -NoTypeInformation

Write-Host "Generated $OutputCsvPath with $($commands.Count) commands"