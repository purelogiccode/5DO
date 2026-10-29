param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('cli', 'frontend')]
    [string] $Component,

    [Parameter(Mandatory = $true)]
    [string] $Rid,

    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("5do-package-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Get-ChildItem -LiteralPath $publishPath -Force |
        Copy-Item -Destination $staging -Recurse -Force
    Get-ChildItem -LiteralPath $staging -Recurse -Force -Filter '*.pdb' |
        Remove-Item -Force

    foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
        $source = Join-Path $repoRoot $file
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $staging -Force
        }
    }

    $docsStaging = Join-Path $staging 'docs'
    foreach ($docName in @('Licensing.md', 'ReleaseLegalReview.md', 'OpenALSoft.md')) {
        $docSource = Join-Path $repoRoot "docs/$docName"
        if (Test-Path -LiteralPath $docSource) {
            New-Item -ItemType Directory -Path $docsStaging -Force | Out-Null
            Copy-Item -LiteralPath $docSource -Destination $docsStaging -Force
        }
    }

    $licensesStaging = Join-Path $docsStaging 'licenses'
    $openAlLicense = Join-Path $repoRoot 'docs/licenses/OpenAL-Soft-COPYING.txt'
    if (Test-Path -LiteralPath $openAlLicense) {
        New-Item -ItemType Directory -Path $licensesStaging -Force | Out-Null
        Copy-Item -LiteralPath $openAlLicense -Destination $licensesStaging -Force
    }

    $releaseNotes = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs') `
        -Filter 'ReleaseNotes-*.md' -File -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if ($releaseNotes) {
        New-Item -ItemType Directory -Path $docsStaging -Force | Out-Null
        Copy-Item -LiteralPath $releaseNotes.FullName -Destination $docsStaging -Force
    }

    if ($Rid -like 'linux-*') {
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/5do.desktop') `
            -Destination $staging -Force
    }

    $version = '0.0.0'
    $props = Join-Path $repoRoot 'Directory.Build.props'
    if (Test-Path -LiteralPath $props) {
        $match = Select-String -LiteralPath $props -Pattern '<Version>([^<]+)</Version>'
        if ($match -and $match.Matches.Count -gt 0) {
            $version = $match.Matches[0].Groups[1].Value
        }
    }

    $commit = 'unknown'
    try {
        $commit = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
            $commit = 'unknown'
        }
    }
    catch {
        $commit = 'unknown'
    }

    $manifest = @(
        '5DO portable package'
        "component: $Component"
        "rid: $Rid"
        "version: $version"
        "commit: $commit"
        "built: $([DateTimeOffset]::UtcNow.ToString('u'))"
        ''
        'BIOS ROMs and game images are not included.'
        'See README.md, LICENSE, THIRD-PARTY-NOTICES.md, docs/Licensing.md, and docs/OpenALSoft.md.'
        'The bundled OpenAL Soft runtime is LGPL-2.0-or-later; the license text is in docs/licenses/OpenAL-Soft-COPYING.txt.'
    )
    Set-Content -LiteralPath (Join-Path $staging 'PACKAGE.txt') -Value $manifest -Encoding utf8NoBOM

    $name = "5do-$Component-$Rid"
    if ($Rid -like 'win-*') {
        $archive = Join-Path $outputPath "$name.zip"
        if (Test-Path -LiteralPath $archive) {
            Remove-Item -LiteralPath $archive -Force
        }

        $items = Get-ChildItem -LiteralPath $staging -Force | Select-Object -ExpandProperty FullName
        Compress-Archive -LiteralPath $items -DestinationPath $archive -Force
    }
    else {
        $archive = Join-Path $outputPath "$name.tar.gz"
        & tar -czf $archive -C $staging .
        if ($LASTEXITCODE -ne 0) {
            throw "tar failed with exit code $LASTEXITCODE."
        }
    }

    Write-Output $archive
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
