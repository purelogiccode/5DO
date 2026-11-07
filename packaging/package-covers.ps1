param(
    [string] $SourceDirectory,
    [string] $OutputDirectory,
    [string] $PackVersion = (Get-Date -AsUTC -Format 'yyyy.MM.dd'),
    [int] $Quality = 85,
    [switch] $NoZip,
    [switch] $Clean,
    [switch] $Verbose
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $repoRoot 'References/Screenshots'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts/covers/v1'
}

if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
    throw "Source image directory not found: $SourceDirectory"
}

$arguments = @(
    'run',
    '--project', (Join-Path $repoRoot 'tools/FiveDo.CoverPackager/FiveDo.CoverPackager.csproj'),
    '-c', 'Release',
    '--',
    '--source', $SourceDirectory,
    '--output', $OutputDirectory,
    '--pack-version', $PackVersion,
    '--quality', $Quality,
    '--overrides', (Join-Path $repoRoot 'packaging/cover-overrides.json')
)
if ($NoZip) {
    $arguments += '--no-zip'
}
if ($Clean) {
    $arguments += '--clean'
}
if ($Verbose) {
    $arguments += '--verbose'
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Cover packager failed with exit code $LASTEXITCODE."
}

Write-Host ''
Write-Host 'Cover pack built:' $OutputDirectory
Write-Host 'Upload with packaging/publish-covers.ps1, or mirror the v1 directory to your R2 bucket.'
Write-Host 'Catalog URL: https://assets.purelogiccode.com/5do/v1/catalog.json'
