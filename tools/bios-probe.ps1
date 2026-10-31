<#
.SYNOPSIS
Probes BIOS-sensitive discs from a compatibility sweep with one or more BIOS images.

.DESCRIPTION
Reads a compat-sweep summary.csv, selects the rows whose status is in -Status, and
runs each disc with every BIOS in -Bios until one produces a rendered frame. Writes
per-attempt JSON/PNG evidence plus summary.csv and summary.md. The winning row keeps
the BIOS that produced the best result, so the next compatibility pass can record it.

.PARAMETER Csv
Sweep summary.csv to read. Defaults to artifacts/compat-full/summary.csv.

.PARAMETER Status
Statuses to retry. Defaults to Loadable/No video and Not Loadable. When invoking with
-File, pass a comma-separated list.

.PARAMETER Bios
BIOS images to try, in order. Defaults to panafz1, panafz10-patched, and the Anvil
BIOS. When invoking with -File, pass a comma-separated list.

.PARAMETER Frames
Frames per attempt. Defaults to 12000, enough for the long black transitions.

.PARAMETER Limit
Probe only the first N selected rows. Defaults to all.

.PARAMETER MinNonBlackPercent
Minimum share of sampled pixels that must be non-black for a result to count as
rendered. Defaults to 3, which rejects the 3DO boot starfield.

.PARAMETER MinColors
Minimum distinct colors for a rendered result. Defaults to 8.

.PARAMETER AllBios
Try every BIOS even after one renders. Useful for a complete comparison table.

.EXAMPLE
pwsh -File tools/bios-probe.ps1 -Frames 12000 -OutDir artifacts/bios-probe
#>
[CmdletBinding()]
param(
    [string]$Csv = 'artifacts/compat-full/summary.csv',
    [string[]]$Status = @('Loadable/No video', 'Not Loadable'),
    [string[]]$Bios = @(
        'bios/panafz1.bin',
        'bios/panafz10-patched.bin',
        'bios/panafz10e-anvil.bin'
    ),
    [int]$Frames = 12000,
    [int]$TimeoutSeconds = 300,
    [string]$OutDir = 'artifacts/bios-probe',
    [string]$Cli = '',
    [int]$Limit = 0,
    [int]$MinNonBlackPercent = 3,
    [int]$MinColors = 8,
    [switch]$AllBios,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

$repoRoot = Split-Path -Parent $PSScriptRoot
$outPath = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $repoRoot $OutDir }
$csvPath = if ([System.IO.Path]::IsPathRooted($Csv)) { $Csv } else { Join-Path $repoRoot $Csv }
$biosItems = @($Bios |
    ForEach-Object { $_ -split ',' } |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ })
$biosPaths = @($biosItems | ForEach-Object {
    if ([System.IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $repoRoot $_ }
})

function Write-Step([string]$Message) {
    Write-Host "[bios-probe] $Message"
}

function Get-CliCommand {
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path -LiteralPath $Explicit)) { throw "CLI path not found: $Explicit" }
        return @{ File = $Explicit; Prefix = @() }
    }

    $exe = Join-Path $repoRoot 'src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.exe'
    if (Test-Path -LiteralPath $exe) { return @{ File = $exe; Prefix = @() } }

    $dll = Join-Path $repoRoot 'src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll'
    if (Test-Path -LiteralPath $dll) { return @{ File = 'dotnet'; Prefix = @($dll) } }

    throw 'CLI not found. Build it first with: dotnet build src/FiveDo.Cli -c Release, or pass -Build.'
}

function ConvertTo-SafeName([string]$Name, [string]$FullPath) {
    $safe = [System.IO.Path]::GetFileNameWithoutExtension($Name)
    $safe = $safe -replace '[^A-Za-z0-9._-]+', '_'
    if ($safe.Length -gt 90) { $safe = $safe.Substring(0, 90) }
    $safe = $safe.Trim('_')
    $hash = [System.Security.Cryptography.SHA256]::HashData(
        [System.Text.Encoding]::UTF8.GetBytes($FullPath.ToLowerInvariant()))
    $suffix = [System.Convert]::ToHexString($hash).Substring(0, 8).ToLowerInvariant()
    return "$safe-$suffix"
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
                    if (($color.R + $color.G + $color.B) -gt 24) { $nonBlack++ }
                    [void]$colors.Add($color.ToArgb())
                }
            }
            return @{ NonBlackPixels = $nonBlack; DistinctColors = $colors.Count; TotalSamples = $samples; AnalysisError = '' }
        }
        finally { $bitmap.Dispose() }
    }
    catch {
        return @{ NonBlackPixels = $null; DistinctColors = $null; TotalSamples = 0; AnalysisError = $_.Exception.Message }
    }
}

function Invoke-OneAttempt {
    param(
        [string]$Disc,
        [string]$SafeName,
        [string]$BiosPath,
        [hashtable]$CliCommand
    )

    $biosSafe = [System.IO.Path]::GetFileNameWithoutExtension($BiosPath) -replace '[^A-Za-z0-9._-]+', '_'
    $gameDir = Join-Path (Join-Path $outPath $SafeName) $biosSafe
    New-Item -ItemType Directory -Force -Path $gameDir | Out-Null
    $jsonPath = Join-Path $gameDir 'result.json'
    $stderrPath = Join-Path $gameDir 'stderr.txt'
    $pngPath = Join-Path $gameDir 'frame.png'

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($prefix in $CliCommand.Prefix) { $arguments.Add($prefix) }
    $arguments.Add('run')
    $arguments.Add($Disc)
    $arguments.Add('--bios')
    $arguments.Add($BiosPath)
    $arguments.Add('--frames')
    $arguments.Add([string]$Frames)
    $arguments.Add('--json')
    $arguments.Add('--audio')
    $arguments.Add('null')
    $arguments.Add('--prefetch')
    $arguments.Add('--frame-out')
    $arguments.Add($pngPath)

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
    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        try { $result = $stdout | ConvertFrom-Json } catch { }
    }
    $metrics = Read-FrameMetrics $pngPath
    $exitCode = if ($process.HasExited) { $process.ExitCode } else { $null }
    $framesRun = if ($null -ne $result -and $null -ne $result.PSObject.Properties['frames']) { [int]$result.frames } else { 0 }
    $status = 'Not Loadable'
    $reason = ''

    if ($timedOut) {
        $reason = "timeout after $TimeoutSeconds s"
    }
    elseif ($exitCode -ne 0) {
        $reason = "exit code $exitCode"
    }
    elseif ($null -eq $result) {
        $reason = 'invalid or missing JSON'
    }
    elseif ($framesRun -lt $Frames) {
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
        Path = $Disc
        File = [System.IO.Path]::GetFileName($Disc)
        Bios = [System.IO.Path]::GetFileName($BiosPath)
        Status = $status
        Reason = $reason
        Frames = $framesRun
        ElapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
        Pc = if ($null -ne $result) { $result.pc } else { $null }
        FrameHash = if ($null -ne $result) { $result.frameHash } else { $null }
        NonBlackPixels = $metrics.NonBlackPixels
        DistinctColors = $metrics.DistinctColors
        AnalysisError = $metrics.AnalysisError
        PngPath = $pngPath
        JsonPath = $jsonPath
    }
}

if ($Build) {
    Write-Step 'building CLI'
    & dotnet build (Join-Path $repoRoot 'src/FiveDo.Cli/FiveDo.Cli.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
}

if (-not (Test-Path -LiteralPath $csvPath)) { throw "summary CSV not found: $csvPath" }
foreach ($biosPath in $biosPaths) {
    if (-not (Test-Path -LiteralPath $biosPath)) { throw "BIOS not found: $biosPath" }
}
if ($biosPaths.Count -eq 0) { throw 'At least one BIOS is required.' }

$cliCommand = Get-CliCommand $Cli
$statusItems = @($Status |
    ForEach-Object { $_ -split ',' } |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ })
$selected = @(Import-Csv -LiteralPath $csvPath | Where-Object { $statusItems -contains $_.Status })
if ($Limit -gt 0) { $selected = @($selected | Select-Object -First $Limit) }
if ($selected.Count -eq 0) { throw "No rows matched status: $($statusItems -join ', ')" }

New-Item -ItemType Directory -Force -Path $outPath | Out-Null
$rows = [System.Collections.Generic.List[object]]::new()
$index = 0

Write-Step "probing $($selected.Count) disc(s) with $($biosPaths.Count) BIOS image(s), $Frames frames each"
foreach ($row in $selected) {
    $index++
    $disc = $row.Path
    $safeName = if ($row.PSObject.Properties['SafeName'] -and $row.SafeName) {
        $row.SafeName
    }
    else {
        ConvertTo-SafeName ([System.IO.Path]::GetFileName($disc)) $disc
    }
    Write-Step "[$index/$($selected.Count)] $([System.IO.Path]::GetFileName($disc))"

    $best = $null
    $attempts = 0
    foreach ($biosPath in $biosPaths) {
        $attempts++
        $attempt = Invoke-OneAttempt -Disc $disc -SafeName $safeName -BiosPath $biosPath -CliCommand $cliCommand
        Write-Host ("           {0}: {1} ({2:0.0}s)" -f $attempt.Bios, $attempt.Status, $attempt.ElapsedSeconds)

        if ($null -eq $best) {
            $best = $attempt
        }
        else {
            $bestRendered = $best.Status -eq 'Loadable/Rendered'
            $attemptRendered = $attempt.Status -eq 'Loadable/Rendered'
            if ($attemptRendered -and -not $bestRendered) {
                $best = $attempt
            }
            elseif ($attemptRendered -eq $bestRendered) {
                $bestPixels = if ($null -ne $best.NonBlackPixels) { [int]$best.NonBlackPixels } else { 0 }
                $attemptPixels = if ($null -ne $attempt.NonBlackPixels) { [int]$attempt.NonBlackPixels } else { 0 }
                $bestColors = if ($null -ne $best.DistinctColors) { [int]$best.DistinctColors } else { 0 }
                $attemptColors = if ($null -ne $attempt.DistinctColors) { [int]$attempt.DistinctColors } else { 0 }
                if ($attemptPixels -gt $bestPixels -or ($attemptPixels -eq $bestPixels -and $attemptColors -gt $bestColors)) {
                    $best = $attempt
                }
            }
        }

        if (-not $AllBios -and $attempt.Status -eq 'Loadable/Rendered') { break }
    }

    $best | Add-Member -NotePropertyName Attempts -NotePropertyValue $attempts
    $rows.Add($best)
    $rows | Export-Csv -LiteralPath (Join-Path $outPath 'summary.csv') -NoTypeInformation -Encoding utf8
}

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add('# BIOS Probe')
$markdown.Add('')
$markdown.Add("Frames per attempt: $Frames; timeout: $TimeoutSeconds s; all BIOSes: $($AllBios.IsPresent)")
$markdown.Add('')
$markdown.Add('| Status | Disc | BIOS | Attempts | Frames | Elapsed (s) | PC | Frame hash | Non-black | Colors | Reason | Evidence |')
$markdown.Add('|---|---|---|---:|---:|---:|---|---:|---:|---|---|')
foreach ($row in $rows) {
    $evidence = try {
        ([System.IO.Path]::GetRelativePath($repoRoot, $row.PngPath)).Replace('\', '/')
    }
    catch { $row.PngPath }
    $line = "| $($row.Status) | $($row.File) | $($row.Bios) | $($row.Attempts) | $($row.Frames) | $($row.ElapsedSeconds) | "
    $line += "$($row.Pc) | $($row.FrameHash) | $($row.NonBlackPixels) | $($row.DistinctColors) | $($row.Reason) | $evidence |"
    $markdown.Add($line)
}
$markdown | Set-Content -LiteralPath (Join-Path $outPath 'summary.md') -Encoding utf8
Write-Step "wrote $(Join-Path $outPath 'summary.csv')"
Write-Step "wrote $(Join-Path $outPath 'summary.md')"
