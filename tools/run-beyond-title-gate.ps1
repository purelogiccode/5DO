#Requires -Version 7.0
<#
.SYNOPSIS
    Run the full beyond-title playability gate and update the evidence ledger.

.DESCRIPTION
    Builds the Release CLI, runs the 378-game beyond-title sweep, regenerates the
    3-way comparison and the evidence page, then either updates the committed
    baseline or fails on a Playable regression. Intended for a nightly/weekly
    local scheduled task or a self-hosted runner with the game collection mounted.

.PARAMETER Scope
    peer-gap, unverified, or all. The gate default is all.

.PARAMETER SkipExisting
    Only test canonical discs without a status in the ledger yet (incremental).

.PARAMETER UpdateBaseline
    Rewrite docs/data/beyond-title-baseline.json instead of checking it.
#>
[CmdletBinding()]
param(
    [int]$Jobs = 10,
    [ValidateSet('peer-gap', 'unverified', 'all')]
    [string]$Scope = 'all',
    [string]$Out = 'artifacts/beyond-title',
    [string]$Ledger = 'docs/data/beyond-title-results.csv',
    [switch]$SkipExisting,
    [switch]$UpdateBaseline,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $NoBuild) {
        dotnet build src/FiveDo.Cli/FiveDo.Cli.csproj -c Release --nologo -v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
    }

    $arguments = @(
        'tools/beyond-title-sweep.py',
        '--scope', $Scope,
        '--jobs', $Jobs,
        '--out', $Out,
        '--ledger', $Ledger,
        '--deep-control'
    )
    if ($SkipExisting) { $arguments += '--skip-existing' }
    python @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Beyond-title sweep failed.' }

    python tools/generate-compat-comparison.py `
        --inventory artifacts/corpus/inventory.json `
        --sweep artifacts/compat-full/summary-merged.csv `
        --sweep artifacts/compat-unique/summary.csv `
        --beyond $Ledger
    if ($LASTEXITCODE -ne 0) { throw 'Comparison generation failed.' }

    python tools/beyond-title-report.py --ledger $Ledger
    if ($LASTEXITCODE -ne 0) { throw 'Evidence report generation failed.' }

    python tools/verify-beyond-title-ledger.py --ledger $Ledger
    if ($LASTEXITCODE -ne 0) { throw 'Beyond-title ledger verification failed.' }

    if ($UpdateBaseline) {
        python tools/beyond-title-baseline.py --ledger $Ledger --update
    }
    else {
        python tools/beyond-title-baseline.py --ledger $Ledger --check --require-count 378
    }
    if ($LASTEXITCODE -ne 0) { throw 'Beyond-title regression gate failed.' }
}
finally {
    Pop-Location
}
