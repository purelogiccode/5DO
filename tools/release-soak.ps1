<#
.SYNOPSIS
Runs the documented-compatibility release soak for the 3DO corpus on this machine.

.DESCRIPTION
The game corpus is machine-specific: the office and home machines do not contain
the same discs. This script discovers the supported titles that are present, runs a
36,000-frame soak for each, and records the result. Titles whose discs are absent are
listed as "missing on this machine" rather than failing the run. If an archived
12k state is available, the script continues it to the target frame count instead.

The default 36,000 frames is 10 minutes of emulated time at 60 Hz.

.PARAMETER GamesRoot
Disc root. Defaults to $env:FIVEDO_GAMES.

.PARAMETER Bios
BIOS path. Defaults to bios/panafz1.bin in the repository.

.PARAMETER CliPath
Built FiveDo.Cli.dll. Defaults to the Release build.

.PARAMETER StateDir
Directory containing archived 12k states for the titles whose discs are absent.

.PARAMETER OutputDir
Directory for the per-title JSON/PNG/state evidence and the summaries.

.PARAMETER Frames
Target emulated frame count. Defaults to 36000 (10 minutes at 60 Hz).

.PARAMETER Filter
Optional title filter, matched against the title key or label (for example `Gex` or
`Road Rash`). Useful for a targeted re-run.

.PARAMETER NoSavedStateFallback
Do not continue from archived states. Absent discs are reported as missing.

.EXAMPLE
pwsh -File tools/release-soak.ps1

.EXAMPLE
pwsh -File tools/release-soak.ps1 -GamesRoot 'D:\Samples\3DO' -Frames 300 -OutputDir artifacts/soak-smoke
#>

param(
    [string] $GamesRoot = $env:FIVEDO_GAMES,
    [string] $Bios = (Join-Path $PSScriptRoot '..\bios\panafz1.bin'),
    [string] $CliPath = (Join-Path $PSScriptRoot '..\src\FiveDo.Cli\bin\Release\net11.0\FiveDo.Cli.dll'),
    [string] $StateDir = (Join-Path $PSScriptRoot '..\artifacts\compat-new'),
    [string] $OutputDir = (Join-Path $PSScriptRoot '..\artifacts\release-soak'),
    [int] $Frames = 36000,
    [string] $Filter,
    [switch] $NoSavedStateFallback
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($GamesRoot)) {
    throw 'Set FIVEDO_GAMES or pass -GamesRoot <path>.'
}

$gamesRootPath = (Resolve-Path -LiteralPath $GamesRoot).Path
$biosPath = (Resolve-Path -LiteralPath $Bios).Path
$cliFullPath = (Resolve-Path -LiteralPath $CliPath).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$stateRoot = $null
if (-not $NoSavedStateFallback -and (Test-Path -LiteralPath $StateDir)) {
    $stateRoot = (Resolve-Path -LiteralPath $StateDir).Path
}

$titles = @(
    @{ Key = 'wolfenstein'; Label = 'Wolfenstein 3D'; DiscPattern = 'Wolfenstein'; StatePattern = 'Wolfenstein' },
    @{ Key = 'lemmings'; Label = 'Lemmings'; DiscPattern = 'Lemmings'; StatePattern = 'Lemmings' },
    @{ Key = 'roadrash'; Label = 'Road Rash'; DiscPattern = 'Road Rash'; StatePattern = 'Road-Rash' },
    @{ Key = 'spaceace'; Label = 'Space Ace'; DiscPattern = 'Space Ace'; StatePattern = 'Space-Ace' },
    @{ Key = 'alone'; Label = 'Alone in the Dark'; DiscPattern = 'Alone in the Dark'; StatePattern = 'Alone' },
    @{ Key = 'gex'; Label = 'Gex'; DiscPattern = 'Gex'; StatePattern = 'Gex' },
    @{ Key = 'nfs'; Label = 'Need for Speed'; DiscPattern = 'Need for Speed|Road & Track Presents'; StatePattern = 'Need-for-Speed|Road-Track' },
    @{ Key = 'ssf2t'; Label = 'Super Street Fighter II Turbo'; DiscPattern = 'Street Fighter II Turbo'; StatePattern = 'Super-Street-Fighter' },
    @{ Key = 'totaleclipse'; Label = 'Total Eclipse'; DiscPattern = 'Total Eclipse'; StatePattern = 'Total-Eclipse' },
    @{ Key = 'killingtime'; Label = 'Killing Time'; DiscPattern = 'Killing Time'; StatePattern = 'Killing-Time' },
    @{ Key = 'themepark'; Label = 'Theme Park'; DiscPattern = 'Theme Park'; StatePattern = 'Theme-Park' },
    @{ Key = 'returnfire'; Label = 'Return Fire'; DiscPattern = 'Return Fire'; StatePattern = 'Return-Fire' }
)

$discFiles = @(Get-ChildItem -LiteralPath $gamesRootPath -Recurse -File -Include '*.chd', '*.iso', '*.cue' -ErrorAction SilentlyContinue)
$stateFiles = @()
if ($stateRoot) {
    $stateFiles = @(Get-ChildItem -LiteralPath $stateRoot -Recurse -File -Filter '*.state' -ErrorAction SilentlyContinue)
}

function Get-ExtensionRank([string] $extension) {
    switch ($extension.ToLowerInvariant()) {
        '.chd' { return 0 }
        '.iso' { return 1 }
        '.cue' { return 2 }
        default { return 3 }
    }
}

$selectedTitles = $titles
if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $selectedTitles = @($titles | Where-Object { $_.Key -match $Filter -or $_.Label -match $Filter })
    if ($selectedTitles.Count -eq 0) {
        throw "No supported title matched filter '$Filter'."
    }
}

$results = @()
foreach ($title in $selectedTitles) {
    $disc = $discFiles |
        Where-Object { $_.Name -match $title.DiscPattern } |
        Sort-Object @{ Expression = { Get-ExtensionRank $_.Extension } }, Name |
        Select-Object -First 1

    $state = $null
    if (-not $disc -and $stateFiles.Count -gt 0) {
        $state = $stateFiles |
            Where-Object { $_.Name -match $title.StatePattern -and $_.Name -match '12k|12000' } |
            Sort-Object Name |
            Select-Object -First 1
        if (-not $state) {
            $state = $stateFiles |
                Where-Object { $_.Name -match $title.StatePattern } |
                Sort-Object Name |
                Select-Object -First 1
        }
    }

    if (-not $disc -and -not $state) {
        $results += [pscustomobject]@{
            title = $title.Label
            source = 'missing'
            file = $null
            status = 'Missing on this machine'
            frames = 0
            pc = $null
            hash = $null
            warnings = @()
            elapsedSeconds = 0
        }
        Write-Output "$($title.Label): missing on this machine"
        continue
    }

    $safeName = ($title.Label -replace '[^A-Za-z0-9]+', '-').Trim('-')
    $base = Join-Path $outputPath $safeName
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $runFrames = $Frames
    $source = 'disc'

    if ($disc) {
        & dotnet $cliFullPath run $disc.FullName --bios $biosPath --frames $runFrames `
            --frame-out "$base.png" --state-out "$base.state" --json > "$base.json" 2> "$base.err"
    }
    else {
        $source = 'state'
        $stateInfo = & dotnet $cliFullPath state inspect $state.FullName --json 2> "$base.inspect.err" | ConvertFrom-Json
        $stateFrames = [int64] $stateInfo.FramesExecuted
        $runFrames = [Math]::Max(0, $Frames - $stateFrames)
        & dotnet $cliFullPath run --state-in $state.FullName --bios $biosPath --frames $runFrames `
            --frame-out "$base.png" --state-out "$base.state" --json > "$base.json" 2> "$base.err"
    }

    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()
    $json = $null
    if ((Test-Path -LiteralPath "$base.json") -and (Get-Item -LiteralPath "$base.json").Length -gt 0) {
        try {
            $json = Get-Content -LiteralPath "$base.json" -Raw | ConvertFrom-Json
        }
        catch {
            $json = $null
        }
    }

    $ok = $exitCode -eq 0 -and $null -ne $json -and [bool] $json.ok
    $result = [pscustomobject]@{
        title = $title.Label
        source = $source
        file = if ($disc) { $disc.FullName } else { $state.FullName }
        status = if ($ok) { 'Pass' } else { 'Fail' }
        frames = if ($json) { $json.frames } else { 0 }
        pc = if ($json) { $json.pc } else { $null }
        hash = if ($json) { $json.frameHash } else { $null }
        warnings = if ($json) { @($json.warnings) } else { @() }
        elapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
    }
    $results += $result
    Write-Output ("{0}: {1} ({2}, {3} frames, {4}s)" -f $title.Label, $result.status, $source, $result.frames, $result.elapsedSeconds)
}

$summary = [pscustomobject]@{
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('u')
    gamesRoot = $gamesRootPath
    framesTarget = $Frames
    results = $results
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputPath 'summary.json') -Encoding utf8NoBOM

$markdown = @(
    '# Release soak summary'
    ''
    "Generated: $($summary.generatedUtc)"
    "Games root: ``$gamesRootPath``"
    "Target frames: $Frames"
    ''
    '| Title | Source | Status | Frames | PC | Hash | Warnings |'
    '|---|---|---|---:|---|---|---|'
)
foreach ($r in $results) {
    $pc = if ($null -ne $r.pc) { '0x{0:X8}' -f [int64] $r.pc } else { '' }
    $hash = if ($null -ne $r.hash) { '0x{0:X8}' -f [int64] $r.hash } else { '' }
    $warnings = ($r.warnings -join '; ') -replace '\|', '/'
    $markdown += "| $($r.title) | $($r.source) | $($r.status) | $($r.frames) | $pc | $hash | $warnings |"
}
$markdown += ''
$markdown += 'A `Missing on this machine` row is expected when the corpus differs between machines.'
$markdown | Set-Content -LiteralPath (Join-Path $outputPath 'summary.md') -Encoding utf8NoBOM

$failures = @($results | Where-Object { $_.status -eq 'Fail' })
if ($failures.Count -gt 0) {
    Write-Error ("$($failures.Count) release soak run(s) failed. See $outputPath.")
    exit 1
}

Write-Output "Release soak complete: $($results.Count) title(s) checked; evidence in $outputPath."
exit 0
