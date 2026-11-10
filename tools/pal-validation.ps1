param(
    [string[]] $Roots = @($env:FIVEDO_GAMES, 'J:\Panasonic 3DO'),
    [string] $BiosFolder = 'bios',
    [string] $OutputDirectory = 'artifacts/pal-validation',
    [string] $Cli = 'src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$cliPath = Join-Path $repoRoot $Cli
if (-not $NoBuild -and -not (Test-Path -LiteralPath $cliPath)) {
    dotnet build (Join-Path $repoRoot 'src/FiveDo.Cli/FiveDo.Cli.csproj') -c Release | Out-Host
}
if (-not (Test-Path -LiteralPath $cliPath)) {
    throw "CLI not found: $cliPath. Build Release first or pass -NoBuild with an existing path."
}

$biosPath = Join-Path $repoRoot (Join-Path $BiosFolder 'panafz10e-anvil.bin')
if (-not (Test-Path -LiteralPath $biosPath)) {
    throw "PAL BIOS not found: $biosPath"
}

$roots = @($Roots | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) } | Select-Object -Unique)
if ($roots.Count -eq 0) {
    throw 'No game roots exist. Set FIVEDO_GAMES or pass -Roots.'
}

# Europe-only/PAL-sensitive matrix titles. EU-US titles are validated in the normal
# NTSC sweep because their US releases are the canonical matrix row.
$titles = @(
    @{ Slug = 'battle-chess-europe'; File = 'Battle Chess (Europe).chd'; Frames = 36000; Captures = '600,3000,9000,18000,30000,36000'; Extra = @() },
    @{ Slug = 'defcon-5'; File = 'Defcon 5 (1995)(GoldStar)(EU)[!].chd'; Frames = 12000; Captures = '3000,6000,9000,12000'; Extra = @() },
    @{ Slug = 'pal-sampler'; File = 'Panasonic REAL 3DO Interactive Multiplayer - Sampler CD for PAL System (1994)(Panasonic)(EU)[!].chd'; Frames = 12000; Captures = '3000,6000,9000,12000'; Extra = @() },
    @{ Slug = 'powers-kingdom'; File = 'Powers Kingdom (1994)(Panasonic)(EU)[!].chd'; Frames = 12000; Captures = '3000,6000,9000,12000'; Extra = @() },
    @{ Slug = 'remute-3d'; File = 'Remute 3D (Europe) (En,De) (Unl).chd'; Frames = 12000; Captures = '3000,6000,9000,12000'; Extra = @('--allow-unsigned-discs') },
    @{ Slug = 'striker-world-cup-special'; File = 'Striker - World Cup Special (1995)(Panasonic)(EU)[!].chd'; Frames = 12000; Captures = '3000,6000,9000,12000'; Extra = @() },
    @{ Slug = 'true-golf-waialae'; File = 'True Golf Classics - Waialae Country Club (Germany).chd'; Frames = 12000; Captures = '3000,6000,9000,12000'; Extra = @() },
    @{ Slug = 'zhadnost'; File = 'Zhadnost - The Peoples Party (1995)(Studio 3DO)(EU)[!].chd'; Frames = 12000; Captures = '3000,6000,9000,12000'; Extra = @() }
)

$inputScript = Join-Path $repoRoot 'benchmarks/diff/scripts/tierb-10min.txt'
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$rows = [System.Collections.Generic.List[object]]::new()
foreach ($title in $titles) {
    $discPath = $null
    foreach ($root in $roots) {
        $candidate = Join-Path $root $title.File
        if (Test-Path -LiteralPath $candidate) {
            $discPath = (Resolve-Path -LiteralPath $candidate).Path
            break
        }
    }

    if ($null -eq $discPath) {
        Write-Warning "Missing PAL title: $($title.File)"
        $rows.Add([pscustomobject]@{
            title = $title.Slug
            file = $title.File
            status = 'Missing'
            frames = 0
            fieldRate = 0
            source = ''
            frameHash = ''
            bios = ''
            discHash = ''
            capture = ''
            notes = 'Disc image not found under the configured roots.'
        })
        continue
    }

    $directory = Join-Path $outputRoot $title.Slug
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $captureDirectory = Join-Path $directory 'captures'
    $resultPath = Join-Path $directory 'result.json'
    $stdoutPath = Join-Path $directory 'stdout.json'
    $stderrPath = Join-Path $directory 'stderr.txt'
    $nvramPath = Join-Path $directory 'nvram.ram'

    $arguments = @(
        $cliPath, 'run', $discPath,
        '--bios', $biosPath,
        '--region', 'pal1',
        '--frames', $title.Frames,
        '--audio', 'null',
        '--nvram', $nvramPath,
        '--capture-dir', $captureDirectory,
        '--capture-frames', $title.Captures,
        '--json'
    )
    if (Test-Path -LiteralPath $inputScript) {
        $arguments += @('--input-script', $inputScript)
    }
    $arguments += $title.Extra

    Write-Host "PAL $($title.Slug): $($title.Frames) frames"
    & dotnet @arguments > $stdoutPath 2> $stderrPath
    $exitCode = $LASTEXITCODE
    if (Test-Path -LiteralPath $stdoutPath) {
        Copy-Item -LiteralPath $stdoutPath -Destination $resultPath -Force
    }

    $result = $null
    try {
        $result = Get-Content -LiteralPath $stdoutPath -Raw | ConvertFrom-Json
    }
    catch {
        Write-Warning "PAL $($title.Slug) produced no parseable JSON; see $stdoutPath and $stderrPath."
    }

    $status = if ($exitCode -eq 0 -and $null -ne $result -and $result.ok) { 'Pass' } else { 'Failed' }
    $frames = if ($null -ne $result -and $result.PSObject.Properties['frames']) {
        [int]$result.frames
    }
    else {
        0
    }

    $capture = ''
    if ($frames -gt 0) {
        $capture = "captures/f$('{0:D6}' -f $frames).png"
    }
    $notes = if ($status -eq 'Pass') {
        "region=$($result.region); fieldRate=$($result.fieldRate); source=$($result.sourceWidth)x$($result.sourceHeight); capture at frame $frames"
    }
    else {
        (Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue) -replace '\s+', ' '
    }

    $rows.Add([pscustomobject]@{
        title = $title.Slug
        file = $title.File
        status = $status
        frames = $frames
        fieldRate = if ($null -ne $result) { $result.fieldRate } else { 0 }
        source = if ($null -ne $result) { "$($result.sourceWidth)x$($result.sourceHeight)" } else { '' }
        frameHash = if ($null -ne $result) { ('0x{0:X8}' -f [uint32]$result.frameHash) } else { '' }
        bios = if ($null -ne $result) { $result.biosName } else { '' }
        discHash = if ($null -ne $result) { $result.discHash } else { '' }
        capture = $capture
        notes = $notes
    })
}

$summaryPath = Join-Path $outputRoot 'summary.csv'
$rows | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding utf8NoBOM
$rows | Format-Table -AutoSize | Out-Host

$failed = @($rows | Where-Object { $_.status -ne 'Pass' })
if ($failed.Count -gt 0) {
    Write-Error "$($failed.Count) PAL title(s) failed; see $summaryPath."
    exit 1
}

Write-Host "PAL validation passed: $($rows.Count) title(s). Summary: $summaryPath"
