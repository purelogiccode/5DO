param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-arm64')]
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

    # Both apps are self-contained single files: the .NET runtime is embedded in the
    # executable, so users do not install a separate runtime.
    Copy-PublishTree -Source $cliPath -Destination $staging
    Copy-PublishTree -Source $frontendPath -Destination $staging

    # Silk.NET loads the loader name (OpenAL32.dll on Windows, libopenal.so on
    # Linux, libopenal.dylib on macOS), not the NuGet package's soft_oal.dll. A
    # bundle that ships only the package name silently falls back to a system
    # OpenAL, which may exist but fail to make a context current.
    $openAlRuntimes = if ($Rid -like 'win-*') {
        @('OpenAL32.dll', 'soft_oal.dll')
    }
    elseif ($Rid -like 'linux-*') {
        @('libopenal.so')
    }
    elseif ($Rid -like 'osx-*') {
        @('libopenal.dylib')
    }
    else {
        throw "Unsupported RID '$Rid' for the OpenAL runtime check."
    }

    foreach ($runtimeName in $openAlRuntimes) {
        $runtimePath = Join-Path $staging $runtimeName
        if (-not (Test-Path -LiteralPath $runtimePath)) {
            throw ("The bundle is missing the OpenAL Soft runtime '$runtimeName'. " +
                'Publish must copy the loader-named native library beside the apphosts.')
        }
    }

    if ($Rid -eq 'osx-arm64') {
        # Ship the Vulkan loader and MoltenVK ICD so a release DMG does not require
        # Homebrew. The app loader prefers these files over /opt/homebrew/lib.
        $brew = Get-Command -Name 'brew' -ErrorAction SilentlyContinue
        if ($null -eq $brew) {
            throw 'Homebrew is required to stage the macOS MoltenVK runtime.'
        }

        $moltenPrefix = (& $brew.Source --prefix molten-vk 2>$null).Trim()
        $loaderPrefix = (& $brew.Source --prefix vulkan-loader 2>$null).Trim()
        $loaderLibrary = Join-Path $loaderPrefix 'lib/libvulkan.1.dylib'
        $moltenLibrary = Join-Path $moltenPrefix 'lib/libMoltenVK.dylib'
        if (-not (Test-Path -LiteralPath $loaderLibrary) -or
            -not (Test-Path -LiteralPath $moltenLibrary)) {
            throw ('Install the macOS Vulkan runtime before packaging: ' +
                'brew install molten-vk vulkan-loader.')
        }

        Copy-Item -LiteralPath $loaderLibrary -Destination (Join-Path $staging 'libvulkan.1.dylib') -Force
        Copy-Item -LiteralPath $moltenLibrary -Destination (Join-Path $staging 'libMoltenVK.dylib') -Force
        $icd = [ordered]@{
            file_format_version = '1.0.0'
            ICD = [ordered]@{
                library_path = './libMoltenVK.dylib'
                api_version = '1.4.0'
                is_portability_driver = $true
            }
        } | ConvertTo-Json -Depth 4
        Set-Content -LiteralPath (Join-Path $staging 'MoltenVK_icd.json') -Value $icd -Encoding utf8NoBOM

        foreach ($licenseName in @('MoltenVK-LICENSE.txt', 'Vulkan-Loader-LICENSE.txt')) {
            $licensePath = Join-Path $repoRoot "docs/$licenseName"
            if (Test-Path -LiteralPath $licensePath) {
                $docsStaging = Join-Path $staging 'docs'
                New-Item -ItemType Directory -Path $docsStaging -Force | Out-Null
                Copy-Item -LiteralPath $licensePath -Destination $docsStaging -Force
            }
        }
    }

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

    $releaseNotes = Join-Path $repoRoot "docs/ReleaseNotes-$Version.md"
    if (Test-Path -LiteralPath $releaseNotes) {
        New-Item -ItemType Directory -Path $docsStaging -Force | Out-Null
        Copy-Item -LiteralPath $releaseNotes -Destination $docsStaging -Force
    }
    else {
        Write-Warning "No release notes found for version $Version ($releaseNotes)."
    }

    if ($Rid -like 'linux-*') {
        # The portable bundle has no /usr/bin/5do symlink, so point the entry at the
        # apphost directly. The .deb/.rpm installers replace this with the package
        # entry that launches the /usr/bin/5do wrapper.
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/5do-portable.desktop') `
            -Destination (Join-Path $staging '5do.desktop') -Force
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
        'FiveDo.Cli.exe --help  |  FiveDo.exe'
    }
    else {
        './FiveDo.Cli --help  |  ./FiveDo'
    }

    $manifest = @(
        '5DO portable bundle'
        'component: bundle'
        "rid: $Rid"
        "version: $Version"
        "commit: $commit"
        "built: $([DateTimeOffset]::UtcNow.ToString('u'))"
        'runtime: .NET 11 embedded; no separate runtime install required'
        'apps: FiveDo.Cli and FiveDo (self-contained single-file apphosts)'
        "launch: $launch"
        ''
        'BIOS ROMs and game images are not included.'
        'See README.md, whatsnew.md, LICENSE, THIRD-PARTY-NOTICES.md, docs/Licensing.md, and docs/OpenALSoft.md.'
        'The bundled OpenAL Soft runtime is LGPL-2.0-or-later; the license text is in docs/OpenAL-Soft-COPYING.txt.'
        'macOS arm64 bundles include the Vulkan loader and MoltenVK (Apache-2.0); see docs/MoltenVK-LICENSE.txt and docs/Vulkan-Loader-LICENSE.txt.'
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
