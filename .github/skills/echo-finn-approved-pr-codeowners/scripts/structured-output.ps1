# Structured output helper for echo-finn-approved-pr-codeowners producers.
function New-StructuredOutputEnvelope {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [object]$Result,

        [ValidateSet('success', 'partial', 'failed')]
        [string]$Status = 'success',

        [object[]]$Errors = @(),

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Producer,

        [ValidateNotNullOrEmpty()]
        [string]$Schema = 'approved-pr-codeowners@1.0.0',

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$CorrelationId
    )

    $metadata = [ordered]@{
        producer        = $Producer
        contractVersion = '1.0.0'
        format          = 'json'
        generatedAt     = (Get-Date).ToUniversalTime().ToString('o')
        schema          = $Schema
        correlationId   = $CorrelationId
    }

    [ordered]@{
        status   = $Status
        result   = $Result
        errors   = @($Errors)
        metadata = $metadata
    }
}
