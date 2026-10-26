# Desktop packaging

The CI publish jobs produce self-contained `ThreeDo.Frontend.Avalonia` folders for
the six supported RIDs. This directory holds the small desktop-integration files
that an installer or a Linux package would install alongside the executable.

## Linux file association

1. Publish or copy the frontend so `ThreeDo.Frontend.Avalonia` is on `PATH`.
2. Install the desktop entry:

   ```sh
   install -Dm644 packaging/3doemulator.desktop \
     ~/.local/share/applications/3doemulator.desktop
   update-desktop-database ~/.local/share/applications
   ```

3. Optionally install an icon named `3doemulator` in
   `~/.local/share/icons/hicolor/256x256/apps/`; convert
   `src/ThreeDo.Frontend.Avalonia/Assets/avalonia-logo.ico` to a 256x256 PNG first.

The application accepts a positional disc path or `--disc <path>`; opening a
`.iso`, `.bin`, `.cue`, or `.chd` file with the desktop entry loads it after the
configured BIOS.

Windows file associations are registered by the installer (M10.7), and macOS
associations come from the app bundle's `Info.plist`. Both remain release work.
