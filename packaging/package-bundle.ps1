param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64')]
    [string] $Rid,

    [Parameter(Mandatory = $true)]
    [string] $CliPublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $FrontendPublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$cliPath = (Resolve-Path -LiteralPath $CliPublishDirectory).Path
$frontendPath = (Resolve-Path -LiteralPath $FrontendPublishDirectory).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("5do-bundle-" + [guid]::NewGuid().ToString('N'))

function Copy-PublishTree {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    foreach ($file in Get-ChildItem -LiteralPath $Source -File -Recurse -Force) {
        $relative = [System.IO.Path]::GetRelativePath($Source, $file.FullName)
        $target = Join-Path $Destination $relative
        $targetDirectory = Split-Path -Parent $target
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null

        if (Test-Path -LiteralPath $target) {
            $existingHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
            $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            if ($existingHash -ne $sourceHash) {
                throw ("The CLI and frontend publish outputs conflict on '$relative'. " +
                    'Publish both apps with PublishSingleFile=true and PublishTrimmed=false so shared files are identical.')
            }

            continue
        }

        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
}

try {
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    # Both apps are framework-dependent single files. No .NET runtime is copied
    # into the bundle; users install the .NET 10 runtime on their machine.
    Copy-PublishTree -Source $cliPath -Destination $staging
    Copy-PublishTree -Source $frontendPath -Destination $staging

    # PDBs and XML documentation are build outputs, not runtime dependencies.
    Get-ChildItem -LiteralPath $staging -Recurse -File -Force -Filter '*.pdb' |
        Remove-Item -Force
    Get-ChildItem -LiteralPath $staging -Recurse -File -Force -Filter '*.xml' |
        Remove-Item -Force

    foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
        $source = Join-Path $repoRoot $file
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $staging -Force
        }
    }

    $whatsNew = Join-Path $repoRoot 'docs/WhatsNew.md'
    if (Test-Path -LiteralPath $whatsNew) {
        Copy-Item -LiteralPath $whatsNew -Destination (Join-Path $staging 'whatsnew.md') -Force
    }

    $docsStaging = Join-Path $staging 'docs'
    foreach ($docName in @('Licensing.md', 'ReleaseLegalReview.md', 'OpenALSoft.md')) {
        $docSource = Join-Path $repoRoot "docs/$docName"
        if (Test-Path -LiteralPath $docSource) {
            New-Item -ItemType Directory -Path $docsStaging -Force | Out-Null
            Copy-Item -LiteralPath $docSource -Destination $docsStaging -Force
        }
    }

    $openAlLicense = Join-Path $repoRoot 'docs/OpenAL-Soft-COPYING.txt'
    if (Test-Path -LiteralPath $openAlLicense) {
        New-Item -ItemType Directory -Path $docsStaging -Force | Out-Null
        Copy-Item -LiteralPath $openAlLicense -Destination $docsStaging -Force
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

    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = '0.0.0'
        $props = Join-Path $repoRoot 'Directory.Build.props'
        if (Test-Path -LiteralPath $props) {
            $match = Select-String -LiteralPath $props -Pattern '<Version>([^<]+)</Version>'
            if ($match -and $match.Matches.Count -gt 0) {
                $Version = $match.Matches[0].Groups[1].Value
            }
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

    $launch = if ($Rid -like 'win-*') {
        'FiveDo.Cli.exe --help  |  FiveDo.Frontend.Avalonia.exe'
    }
    else {
        './FiveDo.Cli --help  |  ./FiveDo.Frontend.Avalonia'
    }

    $manifest = @(
        '5DO portable bundle'
        'component: bundle'
        "rid: $Rid"
        "version: $Version"
        "commit: $commit"
        "built: $([DateTimeOffset]::UtcNow.ToString('u'))"
        'runtime: none bundled; requires the .NET 10 runtime installed on the host'
        'runtime-download: https://dotnet.microsoft.com/download/dotnet/10.0'
        'apps: FiveDo.Cli and FiveDo.Frontend.Avalonia (framework-dependent single-file apphosts)'
        "launch: $launch"
        ''
        'BIOS ROMs and game images are not included.'
        'See README.md, whatsnew.md, LICENSE, THIRD-PARTY-NOTICES.md, docs/Licensing.md, and docs/OpenALSoft.md.'
        'The bundled OpenAL Soft runtime is LGPL-2.0-or-later; the license text is in docs/OpenAL-Soft-COPYING.txt.'
    )
    Set-Content -LiteralPath (Join-Path $staging 'PACKAGE.txt') -Value $manifest -Encoding utf8NoBOM

    $name = "5do-$Rid"
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
        if (Test-Path -LiteralPath $archive) {
            Remove-Item -LiteralPath $archive -Force
        }

        & python (Join-Path $PSScriptRoot 'make-tar.py') $staging $archive
        if ($LASTEXITCODE -ne 0) {
            throw "make-tar.py failed with exit code $LASTEXITCODE."
        }
    }

    $checksumFile = Join-Path $outputPath 'SHA256SUMS.txt'
    $archiveName = [System.IO.Path]::GetFileName($archive)
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines = @()
    if (Test-Path -LiteralPath $checksumFile) {
        $lines = @(Get-Content -LiteralPath $checksumFile |
            Where-Object { $_ -notmatch "\s$([regex]::Escape($archiveName))$" })
    }

    $lines += "$hash  $archiveName"
    $lines | Sort-Object | Set-Content -LiteralPath $checksumFile -Encoding ascii

    Write-Output $archive
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
