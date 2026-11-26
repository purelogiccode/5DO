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

    [string] $Version,

    # Optional folder containing the app-local MSVC runtime DLLs for the target
    # architecture. When omitted, the script locates the Visual Studio redistributable
    # files, or the same-architecture system copies on a matching host.
    [string] $VcRuntimeDirectory
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

function Get-PeMachine {
    param(
        [Parameter(Mandatory = $true)]
        [string] $LiteralPath
    )

    $bytes = [System.IO.File]::ReadAllBytes($LiteralPath)
    if ($bytes.Length -lt 0x40) {
        throw "'$LiteralPath' is too small to be a PE file."
    }

    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    if ($peOffset -lt 0 -or ($peOffset + 6) -gt $bytes.Length) {
        throw "'$LiteralPath' has an invalid PE header offset."
    }

    return [BitConverter]::ToUInt16($bytes, $peOffset + 4)
}

function Resolve-VcRuntimeDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Rid,

        [string] $Override
    )

    $architecture = if ($Rid -eq 'win-arm64') { 'arm64' } else { 'x64' }
    $expectedMachine = if ($Rid -eq 'win-arm64') { 0xAA64 } else { 0x8664 }
    # ARM64 C++ exception handling lives in vcruntime140.dll; vcruntime140_1.dll is
    # the x64 helper and the ARM64 redistributable does not ship an ARM64 copy.
    $required = if ($Rid -eq 'win-arm64') {
        @('vcruntime140.dll', 'msvcp140.dll')
    }
    else {
        @('vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll')
    }

    function Assert-VcRuntimeDirectory {
        param(
            [Parameter(Mandatory = $true)]
            [string] $Directory
        )

        $resolved = (Resolve-Path -LiteralPath $Directory).Path
        $missing = @($required | Where-Object {
            -not (Test-Path -LiteralPath (Join-Path $resolved $_))
        })
        if ($missing.Count -gt 0) {
            throw ("The VC++ runtime directory '$resolved' is missing " +
                "$($missing -join ', ') for $Rid. Pass -VcRuntimeDirectory with the " +
                "$architecture Microsoft Visual C++ redistributable files.")
        }

        foreach ($name in $required) {
            $machine = Get-PeMachine (Join-Path $resolved $name)
            if ($machine -ne $expectedMachine) {
                throw ("The VC++ runtime '$name' in '$resolved' has PE machine " +
                    "0x$($machine.ToString('X4')); $Rid requires 0x$($expectedMachine.ToString('X4')). " +
                    "Pass -VcRuntimeDirectory with the $architecture redistributable files.")
            }
        }

        return $resolved
    }

    if (-not [string]::IsNullOrWhiteSpace($Override)) {
        return Assert-VcRuntimeDirectory -Directory $Override
    }

    $candidates = [System.Collections.Generic.List[string]]::new()

    # GitHub windows-latest images install Visual Studio (2022 or newer), including the
    # ARM64 redistributable when the ARM64 C++ tools are present. vswhere is the stable
    # way to find the installation regardless of drive, edition, or version folder.
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $installations = @(& $vswhere -all -products * -property installationPath 2>$null)
        foreach ($installation in $installations) {
            if ([string]::IsNullOrWhiteSpace($installation)) {
                continue
            }

            $redistRoot = Join-Path $installation 'VC\Redist\MSVC'
            if (-not (Test-Path -LiteralPath $redistRoot)) {
                continue
            }

            foreach ($versionDirectory in Get-ChildItem -LiteralPath $redistRoot -Directory |
                Sort-Object Name -Descending) {
                $architectureRoot = Join-Path $versionDirectory.FullName $architecture
                if (-not (Test-Path -LiteralPath $architectureRoot)) {
                    continue
                }

                foreach ($crtDirectory in Get-ChildItem -LiteralPath $architectureRoot -Directory -Filter 'Microsoft.VC*.CRT') {
                    $candidates.Add($crtDirectory.FullName)
                }
            }
        }
    }

    # A host of the same architecture can supply the system copies for local x64
    # packaging. Cross-architecture packaging must use the redistributable folder,
    # because the PE machine is validated above.
    $processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    if (($architecture -eq 'x64' -and $processArchitecture -eq 'X64') -or
        ($architecture -eq 'arm64' -and $processArchitecture -eq 'Arm64')) {
        $candidates.Add((Join-Path $env:SystemRoot 'System32'))
    }

    foreach ($candidate in $candidates) {
        try {
            return Assert-VcRuntimeDirectory -Directory $candidate
        }
        catch {
            # Try the next installation or redistributable version.
        }
    }

    throw ("Could not find the $architecture Microsoft Visual C++ redistributable " +
        "($($required -join ', ')). Install the Visual Studio C++ ARM64/x64 build tools, " +
        "or pass -VcRuntimeDirectory pointing at a folder that contains them.")
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

    # OpenAL Soft imports the MSVC runtime. The .NET runtime is embedded, but the VC++
    # runtime is a separate deployment dependency on a clean Windows image. Keep the
    # target-architecture DLLs app-local so the bundle works without vc_redist.
    if ($Rid -like 'win-*') {
        $vcRuntimeSource = Resolve-VcRuntimeDirectory -Rid $Rid -Override $VcRuntimeDirectory
        $vcRuntimeFiles = if ($Rid -eq 'win-arm64') {
            @('vcruntime140.dll', 'msvcp140.dll')
        }
        else {
            @('vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll')
        }
        $expectedMachine = if ($Rid -eq 'win-arm64') { 0xAA64 } else { 0x8664 }
        foreach ($name in $vcRuntimeFiles) {
            $destination = Join-Path $staging $name
            Copy-Item -LiteralPath (Join-Path $vcRuntimeSource $name) -Destination $destination -Force
            $machine = Get-PeMachine $destination
            if ($machine -ne $expectedMachine) {
                throw ("The bundled VC++ runtime '$name' has PE machine " +
                    "0x$($machine.ToString('X4')); $Rid requires 0x$($expectedMachine.ToString('X4')).")
            }
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

    $gplLicense = Join-Path $repoRoot 'docs/GPL-2.0.txt'
    if (Test-Path -LiteralPath $gplLicense) {
        New-Item -ItemType Directory -Path $docsStaging -Force | Out-Null
        Copy-Item -LiteralPath $gplLicense -Destination $docsStaging -Force
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

        # Desktop-integration files for third-party packagers (AUR, Flatpak, distro
        # packages). They are reference copies only; the apphosts do not read them at
        # runtime, and the .deb/.rpm/AppImage install their own copies from packaging/.
        $packagerDirectory = Join-Path $staging 'packaging'
        New-Item -ItemType Directory -Path $packagerDirectory -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/5do.desktop') `
            -Destination (Join-Path $packagerDirectory '5do.desktop') -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/linux/5do.xml') `
            -Destination (Join-Path $packagerDirectory '5do.xml') -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/linux/io.github.purelogiccode.FiveDO.metainfo.xml') `
            -Destination (Join-Path $packagerDirectory 'io.github.purelogiccode.FiveDO.metainfo.xml') -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/5do.svg') `
            -Destination (Join-Path $packagerDirectory '5do.svg') -Force
        Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging/linux/5do-256.png') `
            -Destination (Join-Path $packagerDirectory '5do-256.png') -Force
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

    $runtimeNotice = 'runtime: .NET 11 embedded; no separate runtime install required'
    if ($Rid -like 'win-*') {
        $runtimeNotice += if ($Rid -eq 'win-arm64') {
            '; VC++ runtime (vcruntime140, msvcp140) is app-local'
        }
        else {
            '; VC++ runtime (vcruntime140, vcruntime140_1, msvcp140) is app-local'
        }
    }
    $manifest = @(
        '5DO portable bundle'
        'component: bundle'
        "rid: $Rid"
        "version: $Version"
        "commit: $commit"
        "built: $([DateTimeOffset]::UtcNow.ToString('u'))"
        $runtimeNotice
        'apps: FiveDo.Cli and FiveDo (self-contained single-file apphosts)'
        "launch: $launch"
        ''
        'BIOS ROMs and game images are not included.'
        'See README.md, whatsnew.md, LICENSE, THIRD-PARTY-NOTICES.md, docs/Licensing.md, and docs/OpenALSoft.md.'
        'The bundled OpenAL Soft runtime is LGPL-2.0-or-later; the license text is in docs/OpenAL-Soft-COPYING.txt.'
        'The 4DO game database is GPL-2.0; the complete text is in docs/GPL-2.0.txt.'
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
