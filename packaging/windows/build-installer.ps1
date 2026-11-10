param(
    [Parameter(Mandatory = $true)]
    [string] $BundleDirectory,

    [string] $Version,

    [ValidateSet('x64', 'arm64')]
    [string] $Architecture = 'x64',

    [string] $OutputDirectory = 'artifacts/installers',

    # Optional Inno Setup sign command, for example:
    #   'signtool.exe sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a $f'
    [string] $SignCommand
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bundle = (Resolve-Path -LiteralPath $BundleDirectory).Path
foreach ($executable in @('FiveDo.Frontend.Avalonia.exe', 'FiveDo.Cli.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bundle $executable))) {
        throw "Bundle is missing $executable`: $bundle"
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $props = Join-Path $repoRoot 'Directory.Build.props'
    $Version = '0.0.0'
    if (Test-Path -LiteralPath $props) {
        $match = Select-String -LiteralPath $props -Pattern '<Version>([^<]+)</Version>'
        if ($match -and $match.Matches.Count -gt 0) {
            $Version = $match.Matches[0].Groups[1].Value
        }
    }
}

$Version = ($Version.Trim() -replace '^[vV]', '')
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = '0.0.0'
}

$iscc = Get-Command 'iscc.exe' -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $candidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'))
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates.Add((Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'))
    }

    $existing = @($candidates | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -gt 0) {
        $isccPath = $existing[0]
    }
    else {
        throw 'Inno Setup 6 (ISCC.exe) is required. Install it from https://jrsoftware.org/isinfo.php.'
    }
}
else {
    $isccPath = $iscc.Source
}

$output = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
New-Item -ItemType Directory -Path $output -Force | Out-Null

$architecturesAllowed = if ($Architecture -eq 'arm64') { 'arm64' } else { 'x64compatible' }
$arguments = @(
    "/DBundleDir=$bundle",
    "/DAppVersion=$Version",
    "/DArchitecture=$Architecture",
    "/DArchitecturesAllowed=$architecturesAllowed",
    "/DArchitecturesInstallIn64BitMode=$architecturesAllowed",
    "/O$output"
)
if (-not [string]::IsNullOrWhiteSpace($SignCommand)) {
    $arguments += @('/S5doSign=' + $SignCommand, '/DSignToolName=5doSign')
}

$script = Join-Path $PSScriptRoot '5do.iss'
& $isccPath @arguments $script
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

$installer = Get-ChildItem -LiteralPath $output -File -Filter "*win-$Architecture-setup.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $installer) {
    throw "No installer matching *win-$Architecture-setup.exe was produced in $output."
}

$hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = "$($installer.FullName).sha256"
Set-Content -LiteralPath $checksumPath -Value "$hash  $($installer.Name)" -Encoding utf8NoBOM

Write-Host "Built $($installer.FullName)"
Write-Host "SHA256 $hash"
