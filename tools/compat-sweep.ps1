<#
.SYNOPSIS
Runs the 5DO CLI over a local 3DO disc collection and records loadability evidence.

.DESCRIPTION
The sweep is deliberately a black-box test: each disc is booted in a fresh CLI
process, run for a fixed number of frames, and classified from the process exit code,
the CLI's --json result, and an optional screenshot. It does not modify the collection
and does not use a shared NVRAM file, so one game cannot contaminate the next.

Results are written under artifacts/compat by default:
  summary.csv          one row per disc
  summary.md           human-readable table
  <game>/result.json   raw CLI --json output
  <game>/frame.png     final CPU-assembled frame (unless -NoFrameCapture)
  <game>/stderr.txt    CLI stderr

The status values use the 4DO compatibility scale:
  Not Loadable, Unplayable, Playable, Good, No data.
For an automated sweep, "Loadable" means the CLI completed the requested frames; the
image metrics tell you whether the final frame actually rendered something.

.PARAMETER Roots
Collection roots. Defaults to the two local collections.

.PARAMETER Bios
Primary BIOS path. Defaults to bios/panafz1.bin.

.PARAMETER Bios2
Optional BIOS2/Kanji font path. Japanese font ROMs must be paired with their
primary BIOS; the sweep passes this through to the CLI.

.PARAMETER Frames
Frames per disc. Defaults to 3000 (50 seconds at 60 Hz), enough for most boot/menu
screens. Use 12000 for a deeper boot check.

.PARAMETER TimeoutSeconds
Per-process timeout. Defaults to 180 seconds.

.PARAMETER HighRes
Pass --high-res to the CLI.

.PARAMETER Resume
Skip discs whose result.json already exists and parses.

.PARAMETER Limit
Test at most N discs (useful for a smoke run).

.PARAMETER Filter
Only test files whose name matches this wildcard.

.PARAMETER Files
Explicit disc paths to test instead of scanning the roots. Takes precedence over Roots.

.PARAMETER RetryFrames
When a disc completes but its final frame is blank, rerun it once with this frame count.
Defaults to 0 (no retry). Slow loaders such as Myst need around 20,000 frames.

.PARAMETER NoFrameCapture
Do not write frame.png. This makes the sweep faster but disables image metrics.

.PARAMETER NoPrefetch
Disable the CLI --prefetch cache.

.EXAMPLE
pwsh -File tools/compat-sweep.ps1 -Frames 3000

.EXAMPLE
pwsh -File tools/compat-sweep.ps1 -Limit 10 -Frames 1200 -Resume
#>

[CmdletBinding()]
param(
    [string[]]$Roots = @(
        'J:\Panasonic 3DO',
        'G:\MAME\MAME Software List CHDs\3do'
    ),
    [string]$Bios = 'bios/panafz1.bin',
    [string]$Bios2 = '',
    [int]$Frames = 3000,
    [int]$TimeoutSeconds = 180,
    [string]$OutDir = 'artifacts/compat',
    [string]$Cli = '',
    [switch]$HighRes,
    [switch]$Resume,
    [int]$Limit = 0,
    [string]$Filter = '*.chd',
    [string[]]$Files = @(),
    [int]$RetryFrames = 0,
    [switch]$NoFrameCapture,
    [switch]$NoPrefetch,
    [int]$MinNonBlackPercent = 3,
    [int]$MinColors = 8,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Keep summary.csv machine-readable regardless of the host locale (decimal point,
# not decimal comma).
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

$repoRoot = Split-Path -Parent $PSScriptRoot
$outPath = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $repoRoot $OutDir }
$biosPath = if ([System.IO.Path]::IsPathRooted($Bios)) { $Bios } else { Join-Path $repoRoot $Bios }
$bios2Path = if ([string]::IsNullOrWhiteSpace($Bios2)) { '' } elseif ([System.IO.Path]::IsPathRooted($Bios2)) { $Bios2 } else { Join-Path $repoRoot $Bios2 }

function Write-Step([string]$Message) {
    Write-Host "[compat] $Message"
}

function Get-CliCommand {
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path -LiteralPath $Explicit)) {
            throw "CLI path not found: $Explicit"
        }
        return @{ File = $Explicit; Prefix = @() }
    }

    $exe = Join-Path $repoRoot 'src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.exe'
    if (Test-Path -LiteralPath $exe) {
        return @{ File = $exe; Prefix = @() }
    }

    $dll = Join-Path $repoRoot 'src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll'
    if (Test-Path -LiteralPath $dll) {
        return @{ File = 'dotnet'; Prefix = @($dll) }
    }

    throw 'CLI not found. Build it first with: dotnet build src/FiveDo.Cli -c Release, or pass -Build.'
}

function ConvertTo-SafeName([string]$Name, [string]$FullPath) {
    $safe = [System.IO.Path]::GetFileNameWithoutExtension($Name)
    $safe = $safe -replace '[^A-Za-z0-9._-]+', '_'
    if ($safe.Length -gt 90) {
        $safe = $safe.Substring(0, 90)
    }
    $safe = $safe.Trim('_')
    $hash = [System.Security.Cryptography.SHA256]::HashData(
        [System.Text.Encoding]::UTF8.GetBytes($FullPath.ToLowerInvariant()))
    $suffix = [System.Convert]::ToHexString($hash).Substring(0, 8).ToLowerInvariant()
    return "$safe-$suffix"
}

function Get-Prop {
    param($Object, [string]$Name, $Default = $null)
    if ($null -eq $Object) { return $Default }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $Default }
    return $property.Value
}

function ConvertTo-MarkdownTable {
    param([object[]]$Rows, [string[]]$Columns)
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('| ' + ($Columns -join ' | ') + ' |')
    $lines.Add('|' + (($Columns | ForEach-Object { '---' }) -join '|') + '|')
    foreach ($row in $Rows) {
        $cells = foreach ($column in $Columns) {
            $value = Get-Prop $row $column ''
            if ($null -eq $value) { '' } else { ([string]$value).Replace('|', '\|') }
        }
        $lines.Add('| ' + ($cells -join ' | ') + ' |')
    }
    return $lines
}

function Read-FrameMetrics([string]$PngPath) {
    if (-not (Test-Path -LiteralPath $PngPath)) {
        return @{ NonBlackPixels = $null; DistinctColors = $null; TotalSamples = 0; AnalysisError = 'frame.png missing' }
    }

    try {
        Add-Type -AssemblyName System.Drawing.Common -ErrorAction Stop
        $bitmap = [System.Drawing.Bitmap]::new($PngPath)
        try {
            $nonBlack = 0
            $samples = 0
            $colors = [System.Collections.Generic.HashSet[int]]::new()
            $step = [Math]::Max(1, [Math]::Floor([Math]::Min($bitmap.Width, $bitmap.Height) / 120))
            for ($y = 0; $y -lt $bitmap.Height; $y += $step) {
                for ($x = 0; $x -lt $bitmap.Width; $x += $step) {
                    $color = $bitmap.GetPixel($x, $y)
                    $samples++
                    if (($color.R + $color.G + $color.B) -gt 24) {
                        $nonBlack++
                    }
                    [void]$colors.Add($color.ToArgb())
                }
            }
            return @{
                NonBlackPixels = $nonBlack
                DistinctColors = $colors.Count
                TotalSamples = $samples
                AnalysisError = ''
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
    catch {
        return @{
            NonBlackPixels = $null
            DistinctColors = $null
            TotalSamples = 0
            AnalysisError = $_.Exception.Message
        }
    }
}

function Invoke-OneDisc {
    param(
        [string]$Disc,
        [string]$SafeName,
        [hashtable]$CliCommand,
        [int]$Frames
    )

    $gameDir = Join-Path $outPath $SafeName
    New-Item -ItemType Directory -Force -Path $gameDir | Out-Null
    $jsonPath = Join-Path $gameDir 'result.json'
    $stderrPath = Join-Path $gameDir 'stderr.txt'
    $pngPath = Join-Path $gameDir 'frame.png'

    if ($Resume -and (Test-Path -LiteralPath $jsonPath) -and $existingResults.ContainsKey($Disc)) {
        try {
            $existing = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
            if ($existing.ok) {
                Write-Step "skip (resume): $([System.IO.Path]::GetFileName($Disc))"
                return $existingResults[$Disc]
            }
        }
        catch {
            # Fall through and rerun a corrupt/partial result.
        }
    }

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($prefix in $CliCommand.Prefix) { $arguments.Add($prefix) }
    $arguments.Add('run')
    $arguments.Add($Disc)
    $arguments.Add('--bios')
    $arguments.Add($biosPath)
    if ($bios2Path) {
        $arguments.Add('--bios2')
        $arguments.Add($bios2Path)
    }
    $arguments.Add('--frames')
    $arguments.Add([string]$Frames)
    $arguments.Add('--json')
    $arguments.Add('--audio')
    $arguments.Add('null')
    if (-not $NoPrefetch) { $arguments.Add('--prefetch') }
    if ($HighRes) { $arguments.Add('--high-res') }
    if (-not $NoFrameCapture) {
        $arguments.Add('--frame-out')
        $arguments.Add($pngPath)
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $CliCommand.File
    foreach ($argument in $arguments) { $startInfo.ArgumentList.Add($argument) }
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $completed = $process.WaitForExit($TimeoutSeconds * 1000)
    $timedOut = $false
    if (-not $completed) {
        $timedOut = $true
        try { $process.Kill($true) } catch { }
        $process.WaitForExit(5000) | Out-Null
    }
    $stopwatch.Stop()

    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    Set-Content -LiteralPath $jsonPath -Value $stdout -Encoding utf8
    Set-Content -LiteralPath $stderrPath -Value $stderr -Encoding utf8

    $result = $null
    $parseError = ''
    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        try {
            $result = $stdout | ConvertFrom-Json
        }
        catch {
            $parseError = $_.Exception.Message
        }
    }

    $metrics = if ($NoFrameCapture) {
        @{ NonBlackPixels = $null; DistinctColors = $null; TotalSamples = 0; AnalysisError = 'capture disabled' }
    }
    else {
        Read-FrameMetrics $pngPath
    }

    $exitCode = if ($process.HasExited) { $process.ExitCode } else { $null }
    $framesRun = if ($null -ne (Get-Prop $result 'frames')) { [int](Get-Prop $result 'frames') } else { 0 }
    $status = 'Not Loadable'
    $reason = ''

    if ($timedOut) {
        $status = 'Not Loadable'
        $reason = "timeout after $TimeoutSeconds s"
    }
    elseif ($exitCode -ne 0) {
        $status = 'Not Loadable'
        $reason = "exit code $exitCode"
    }
    elseif ($null -eq $result) {
        $status = 'Not Loadable'
        $reason = if ($parseError) { "invalid JSON: $parseError" } else { 'no JSON result' }
    }
    elseif ($framesRun -lt $Frames) {
        $status = 'Not Loadable'
        $reason = "ran $framesRun of $Frames frames"
    }
    else {
        $rendered = $false
        if ($metrics.NonBlackPixels -ne $null -and $metrics.TotalSamples -gt 0) {
            $nonBlackPercent = 100.0 * $metrics.NonBlackPixels / $metrics.TotalSamples
            $rendered = ($nonBlackPercent -ge $MinNonBlackPercent) -and ($metrics.DistinctColors -ge $MinColors)
        }
        $status = if ($rendered) { 'Loadable/Rendered' } else { 'Loadable/No video' }
        $reason = if ($rendered) { 'completed and final frame is non-black' } else { 'completed but final frame is black/blank' }
    }

    return [pscustomobject]@{
        Root = Split-Path -Qualifier $Disc
        File = [System.IO.Path]::GetFileName($Disc)
        Path = $Disc
        SafeName = $SafeName
        Bios = [System.IO.Path]::GetFileName($biosPath)
        Bios2 = if ($bios2Path) { [System.IO.Path]::GetFileName($bios2Path) } else { '' }
        SizeMB = [Math]::Round((Get-Item -LiteralPath $Disc).Length / 1MB, 2)
        Status = $status
        Reason = $reason
        ExitCode = $exitCode
        TimedOut = $timedOut
        Frames = $framesRun
        RequestedFrames = $Frames
        ElapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
        Pc = Get-Prop $result 'pc'
        FrameHash = Get-Prop $result 'frameHash'
        SourceWidth = Get-Prop $result 'sourceWidth'
        SourceHeight = Get-Prop $result 'sourceHeight'
        NonBlackPixels = $metrics.NonBlackPixels
        DistinctColors = $metrics.DistinctColors
        AnalysisSamples = $metrics.TotalSamples
        AnalysisError = $metrics.AnalysisError
        DiscHash = Get-Prop $result 'discHash'
        DspRunning = Get-Prop $result 'dspRunning'
        CdStatus = Get-Prop $result 'cdStatus'
        CdPoll = Get-Prop $result 'cdPoll'
        JsonPath = $jsonPath
        PngPath = if ($NoFrameCapture) { '' } else { $pngPath }
    }
}

if ($Build) {
    Write-Step 'building CLI'
    & dotnet build (Join-Path $repoRoot 'src/FiveDo.Cli/FiveDo.Cli.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
}

if (-not (Test-Path -LiteralPath $biosPath)) {
    throw "BIOS not found: $biosPath"
}
if ($bios2Path -and -not (Test-Path -LiteralPath $bios2Path)) {
    throw "BIOS2 not found: $bios2Path"
}

$cliCommand = Get-CliCommand $Cli
$discs = [System.Collections.Generic.List[string]]::new()
if ($Files.Count -gt 0) {
    foreach ($file in $Files) {
        $resolved = if (Test-Path -LiteralPath $file) { (Resolve-Path -LiteralPath $file).Path } else { '' }
        if ($resolved) {
            $discs.Add($resolved)
        }
        else {
            Write-Warning "Disc not found: $file"
        }
    }
}
else {
    foreach ($root in $Roots) {
        if (-not (Test-Path -LiteralPath $root)) {
            Write-Warning "Collection root not found: $root"
            continue
        }
        foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Filter $Filter -ErrorAction SilentlyContinue) {
            $discs.Add($file.FullName)
        }
    }
}
$discs = @($discs | Sort-Object)
if ($Limit -gt 0) {
    $discs = @($discs | Select-Object -First $Limit)
}
if ($discs.Count -eq 0) {
    throw 'No disc images matched the sweep.'
}

New-Item -ItemType Directory -Force -Path $outPath | Out-Null
$summaryCsv = Join-Path $outPath 'summary.csv'
$summaryMd = Join-Path $outPath 'summary.md'
$results = [System.Collections.Generic.List[object]]::new()
$existingResults = @{}
if ($Resume -and (Test-Path -LiteralPath $summaryCsv)) {
    Import-Csv -LiteralPath $summaryCsv | ForEach-Object {
        if ($_.Path) {
            $existingResults[$_.Path] = $_
        }
    }
}
$index = 0

Write-Step "testing $($discs.Count) disc(s), $Frames frames each, timeout $TimeoutSeconds s"
foreach ($disc in $discs) {
    $index++
    $safeName = ConvertTo-SafeName ([System.IO.Path]::GetFileName($disc)) $disc
    $label = "[$index/$($discs.Count)] $([System.IO.Path]::GetFileName($disc))"
    Write-Step $label

    $row = Invoke-OneDisc -Disc $disc -SafeName $safeName -CliCommand $cliCommand -Frames $Frames
    if ($null -ne $row -and $RetryFrames -gt $Frames -and $row.Status -eq 'Loadable/No video') {
        Write-Step "         retrying with $RetryFrames frames"
        $retry = Invoke-OneDisc -Disc $disc -SafeName $safeName -CliCommand $cliCommand -Frames $RetryFrames
        if ($null -ne $retry -and $retry.Status -eq 'Loadable/Rendered') {
            $row = $retry
        }
    }

    if ($null -ne $row) {
        $results.Add($row)
        Write-Host ("         {0}: {1} ({2:0.0}s)" -f $row.Status, $row.Reason, $row.ElapsedSeconds)
        if (($index % 10) -eq 0) {
            $results | Export-Csv -LiteralPath $summaryCsv -NoTypeInformation -Encoding utf8
        }
    }
}

if ($results.Count -gt 0) {
    $results | Export-Csv -LiteralPath $summaryCsv -NoTypeInformation -Encoding utf8
    $markdown = [System.Collections.Generic.List[string]]::new()
    $markdown.Add('# Compatibility Sweep')
    $markdown.Add('')
    $markdown.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $markdown.Add("Frames per disc: $Frames; timeout: $TimeoutSeconds s; high-res: $($HighRes.IsPresent)")
    $markdown.Add('')
    $markdown.AddRange([string[]](ConvertTo-MarkdownTable -Rows $results -Columns @(
        'File', 'Status', 'Reason', 'Frames', 'RequestedFrames', 'ElapsedSeconds', 'Pc',
        'FrameHash', 'SourceWidth', 'SourceHeight', 'NonBlackPixels', 'DistinctColors',
        'DiscHash', 'DspRunning', 'CdStatus', 'CdPoll', 'JsonPath', 'PngPath'
    )))
    $markdown | Set-Content -LiteralPath $summaryMd -Encoding utf8
    Write-Step "wrote $summaryCsv"
    Write-Step "wrote $summaryMd"
}

$summary = $results | Group-Object Status | Sort-Object Name | ForEach-Object {
    '{0}: {1}' -f $_.Name, $_.Count
}
Write-Step ('results: ' + ($summary -join ', '))
