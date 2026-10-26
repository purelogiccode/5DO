# Desktop packaging

The CI publish jobs produce self-contained `FiveDo.Frontend.Avalonia` folders for
the six supported RIDs. This directory holds the small desktop-integration files
that an installer or a Linux package would install alongside the executable.

## Linux file association

1. Publish or copy the frontend so `FiveDo.Frontend.Avalonia` is on `PATH`.
2. Install the desktop entry:

   ```sh
   install -Dm644 packaging/5do.desktop \
     ~/.local/share/applications/5do.desktop
   update-desktop-database ~/.local/share/applications
   ```

3. Optionally install an icon named `5do` in
   `~/.local/share/icons/hicolor/256x256/apps/`; convert
   `src/FiveDo.Frontend.Avalonia/Assets/avalonia-logo.ico` to a 256x256 PNG first.

The application accepts a positional disc path or `--disc <path>`; opening a
`.iso`, `.bin`, `.cue`, or `.chd` file with the desktop entry loads it after the
configured BIOS.

## Portable archives

CI packages each published CLI and frontend folder with
`packaging/package.ps1`. The archives are named
`5do-<component>-<rid>.zip` for Windows and `.tar.gz` for Linux/macOS.
They contain the publish output, `README.md`, `THIRD-PARTY-NOTICES.md`,
`docs/Licensing.md`, and a generated `PACKAGE.txt` with the component, RID, version,
commit, and build time. Linux archives also include `5do.desktop`.

Build one locally:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o artifacts/cli/win-x64
pwsh -File packaging/package.ps1 -Component cli -Rid win-x64 `
  -PublishDirectory artifacts/cli/win-x64 -OutputDirectory artifacts/packages
```

The archives are portable folders, not installers. MSIX/dmg/AppImage packaging,
signing, and notarization remain release work.

Windows file associations are registered by the installer (M10.7), and macOS
associations come from the app bundle's `Info.plist`. Both remain release work.
