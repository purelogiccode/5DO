param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
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

function Set-ContentWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $LiteralPath,

        [Parameter(Mandatory = $true)]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Encoding
    )

    for ($attempt = 0; ; $attempt++) {
        try {
            Set-Content -LiteralPath $LiteralPath -Value $Value -Encoding $Encoding
            return
        }
        catch [System.IO.IOException] {
            if ($attempt -ge 4) {
                throw
            }

            # Cloud-sync clients and virus scanners briefly hold the checksum
            # file open while the previous bundle is being written.
            Start-Sleep -Milliseconds (200 * ($attempt + 1))
        }
    }
}

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
    # into the bundle; users install the .NET 11 runtime on their machine.
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
        # The portable bundle has no /usr/bin/5do symlink, so point the entry at the
        # apphost directly. The .deb/.rpm installers replace this with the package
        # entry that launches the /usr/bin/5do wrapper.
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/5do-portable.desktop') `
            -Destination (Join-Path $staging '5do.desktop') -Force
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

    # Normalize a leading tag-style v and keep the version safe for a file name.
    $Version = $Version.Trim() -replace '^[vV]', ''
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = '0.0.0'
    }

    $safeVersion = $Version -replace '[^0-9A-Za-z._+-]', '_'

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
        'runtime: none bundled; requires the .NET 11 runtime installed on the host'
        'runtime-download: https://dotnet.microsoft.com/download/dotnet/11.0'
        'apps: FiveDo.Cli and FiveDo.Frontend.Avalonia (framework-dependent single-file apphosts)'
        "launch: $launch"
        ''
        'BIOS ROMs and game images are not included.'
        'See README.md, whatsnew.md, LICENSE, THIRD-PARTY-NOTICES.md, docs/Licensing.md, and docs/OpenALSoft.md.'
        'The bundled OpenAL Soft runtime is LGPL-2.0-or-later; the license text is in docs/OpenAL-Soft-COPYING.txt.'
    )
    Set-Content -LiteralPath (Join-Path $staging 'PACKAGE.txt') -Value $manifest -Encoding utf8NoBOM

    $name = "5do-$safeVersion-$Rid"
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

        $python = Get-Command -Name 'python' -ErrorAction SilentlyContinue
        if ($null -eq $python) {
            $python = Get-Command -Name 'python3' -ErrorAction SilentlyContinue
        }

        if ($null -eq $python) {
            throw 'Python 3 is required to create the tar.gz bundle.'
        }

        & $python.Source (Join-Path $PSScriptRoot 'make-tar.py') $staging $archive
        if ($LASTEXITCODE -ne 0) {
            throw "make-tar.py failed with exit code $LASTEXITCODE."
        }
    }

    # macOS is distributed as a drag-to-Applications DMG. The tar.gz remains the
    # deterministic intermediate used by the smoke-test extraction, while the
    # release workflow uploads the DMG.
    $extraArtifacts = @()
    $onMacOS = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::OSX)
    if ($Rid -like 'osx-*' -and $onMacOS) {
        $bash = Get-Command -Name 'bash' -ErrorAction SilentlyContinue
        if ($null -eq $bash) {
            throw 'bash is required to build the macOS DMG.'
        }

        $macPackage = Join-Path $repoRoot 'packaging/macos/build-macos-package.sh'
        & $bash.Source $macPackage `
            --bundle $staging --rid $Rid --version $Version --output $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "build-macos-package.sh failed with exit code $LASTEXITCODE."
        }

        $dmg = Get-ChildItem -LiteralPath $outputPath -File -Filter "*$Rid.dmg" |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -eq $dmg) {
            throw "No macOS DMG matching *$Rid.dmg was produced in $outputPath."
        }

        $extraArtifacts += $dmg.FullName
        Write-Output $dmg.FullName
    }

    $checksumFile = Join-Path $outputPath 'SHA256SUMS.txt'
    $artifacts = @($archive) + $extraArtifacts
    $artifactNames = @($artifacts | ForEach-Object { [System.IO.Path]::GetFileName($_) })
    $lines = @()
    if (Test-Path -LiteralPath $checksumFile) {
        $lines = @(Get-Content -LiteralPath $checksumFile | Where-Object {
            $line = $_
            -not ($artifactNames | Where-Object {
                $line -match "\s$([regex]::Escape($_))$"
            })
        })
    }

    foreach ($artifact in $artifacts) {
        $artifactName = [System.IO.Path]::GetFileName($artifact)
        $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines += "$hash  $artifactName"
    }

    Set-ContentWithRetry -LiteralPath $checksumFile -Value ($lines | Sort-Object) -Encoding ascii

    Write-Output $archive
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
