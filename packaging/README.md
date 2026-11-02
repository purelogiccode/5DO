# Desktop packaging

The release artifacts are **one bundle per system**, not one archive per component.
Each bundle contains both the desktop app and the headless CLI:

| System | Bundle |
|---|---|
| Windows x64 | `5do-win-x64.zip` |
| Linux x64 | `5do-linux-x64.tar.gz` |
| macOS x64 | `5do-osx-x64.tar.gz` |

## Layout

The bundle is designed so each app is a single executable without embedding a
separate copy of the .NET runtime in each one:

```text
FiveDo.Cli[.exe]              single-file apphost
FiveDo.Frontend.Avalonia[.exe] single-file apphost
OpenAL32.dll / soft_oal.dll   native OpenAL Soft (Windows)
libopenal.so / libopenal.dylib
libSkiaSharp.*, av_libglesv2.dll, ...
runtime/                      one private .NET 10 runtime
  host/fxr/<version>/
  shared/Microsoft.NETCore.App/<version>/
README.md
whatsnew.md
LICENSE
THIRD-PARTY-NOTICES.md
docs/...
PACKAGE.txt
```

The apphosts are published with:

```text
--self-contained false
-p:PublishSingleFile=true
-p:PublishTrimmed=false
-p:AppHostRelativeDotNet=runtime
```

`AppHostRelativeDotNet` makes the apphost load the private runtime from the bundle's
`runtime/` directory, so users do not need a system .NET install. The runtime is
staged once and shared by both apps.

### Why not self-contained single-file?

A self-contained single-file publish embeds a complete .NET runtime in **each**
executable (roughly 70 MB per app before compression). That duplicates the runtime
and contradicts the requirement that the .NET runtime not be embedded in each binary.
A framework-dependent single file without a private runtime is smaller, but requires a
system .NET 10 install. The bundle therefore uses framework-dependent single-file
apphosts plus one shared private runtime.

### Why native libraries stay outside the single file

OpenAL Soft is LGPL-2.0-or-later and must remain replaceable. Skia and ANGLE also ship
as native assets. Keeping them beside the apphosts preserves relinking/replacement
rights and avoids extraction to a temporary directory.

## Build a bundle

```powershell
$rid = 'win-x64'

dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r $rid `
  --self-contained false -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -p:AppHostRelativeDotNet=runtime -m:1 -o "artifacts/bundle/$rid/cli"

dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r $rid --self-contained false -p:PublishSingleFile=true `
  -p:PublishTrimmed=false -p:AppHostRelativeDotNet=runtime -m:1 `
  -o "artifacts/bundle/$rid/frontend"

pwsh -NoProfile -File packaging/package-bundle.ps1 `
  -Rid $rid `
  -CliPublishDirectory "artifacts/bundle/$rid/cli" `
  -FrontendPublishDirectory "artifacts/bundle/$rid/frontend" `
  -OutputDirectory "artifacts/bundles"
```

The script merges the two publish outputs, verifies that shared files are identical,
stages the private runtime, copies the README/license/notices/whatsnew/release notes,
and writes `PACKAGE.txt` and `SHA256SUMS.txt`.

For local release copies, use:

```powershell
-OutputDirectory src/FiveDo.Frontend.Avalonia/bin/Release
```

## Linux file association

1. Extract the bundle and put `FiveDo.Frontend.Avalonia` on `PATH`.
2. Install the desktop entry:

   ```sh
   install -Dm644 5do.desktop ~/.local/share/applications/5do.desktop
   update-desktop-database ~/.local/share/applications
   ```

3. Optionally install an icon named `5do` in
   `~/.local/share/icons/hicolor/256x256/apps/`; convert
   `src/FiveDo.Frontend.Avalonia/Assets/fivedo.ico` to a 256x256 PNG first.

The application accepts a positional disc path or `--disc <path>`; opening a
`.iso`, `.bin`, `.cue`, or `.chd` file with the desktop entry loads it after the
configured BIOS.

## CI

`.github/workflows/build-bundle.yml` builds the three bundles on matching native
runners and runs the CLI/frontend smoke tests. `.github/workflows/release.yml`
publishes the bundles and `SHA256SUMS.txt` to the GitHub release. The docs workflow
publishes the documentation site and the wiki.

The archives are portable bundles, not installers. MSIX/dmg/AppImage packaging,
signing, and notarization remain post-1.0 work.
