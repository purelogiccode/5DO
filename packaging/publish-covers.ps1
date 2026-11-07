param(
    [string] $SourceDirectory,
    [string] $Remote = 'r2:simplelauncher-thumbnails/5do/v1',
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $repoRoot 'artifacts/covers/v1'
}

$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $source 'catalog.json'))) {
    throw "catalog.json not found in $source. Run packaging/package-covers.ps1 first."
}

if (-not (Get-Command rclone -ErrorAction SilentlyContinue)) {
    throw 'rclone is required. Configure an R2 remote, then rerun this script.'
}

$remote = $Remote.TrimEnd('/')
$common = @('--checksum')
if ($DryRun) {
    $common += '--dry-run'
}

$imageHeaders = @(
    '--header-upload', 'Cache-Control: public, max-age=31536000, immutable',
    '--header-upload', 'Content-Type: image/webp'
)
$catalogHeaders = @(
    '--header-upload', 'Cache-Control: public, max-age=300',
    '--header-upload', 'Content-Type: application/json'
)
$packHeaders = @(
    '--header-upload', 'Cache-Control: public, max-age=31536000, immutable',
    '--header-upload', 'Content-Type: application/zip'
)
$textHeaders = @(
    '--header-upload', 'Cache-Control: public, max-age=300',
    '--header-upload', 'Content-Type: text/plain; charset=utf-8'
)
$jsonHeaders = @(
    '--header-upload', 'Cache-Control: public, max-age=300',
    '--header-upload', 'Content-Type: application/json'
)

& rclone copy (Join-Path $source 'images') "$remote/images" @common @imageHeaders
if ($LASTEXITCODE -ne 0) { throw "rclone image upload failed with exit code $LASTEXITCODE." }

& rclone copyto (Join-Path $source 'catalog.json') "$remote/catalog.json" @common @catalogHeaders
if ($LASTEXITCODE -ne 0) { throw "rclone catalog upload failed with exit code $LASTEXITCODE." }

$packs = Join-Path $source 'packs'
if (Test-Path -LiteralPath $packs) {
    & rclone copy $packs "$remote/packs" @common @packHeaders
    if ($LASTEXITCODE -ne 0) { throw "rclone pack upload failed with exit code $LASTEXITCODE." }
}

& rclone copyto (Join-Path $source 'SHA256SUMS.txt') "$remote/SHA256SUMS.txt" @common @textHeaders
if ($LASTEXITCODE -ne 0) { throw "rclone checksum upload failed with exit code $LASTEXITCODE." }

$report = Join-Path $source 'report.json'
if (Test-Path -LiteralPath $report) {
    & rclone copyto $report "$remote/report.json" @common @jsonHeaders
    if ($LASTEXITCODE -ne 0) { throw "rclone report upload failed with exit code $LASTEXITCODE." }
}

Write-Host "Published $source to $remote."
Write-Host 'Catalog: https://assets.purelogiccode.com/5do/v1/catalog.json'
