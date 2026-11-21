# Installation

This page covers getting 5DO running on Windows, Linux, and macOS. 5DO does **not**
include a BIOS ROM or any game images. You must supply legally obtained files.

## Before you start

| Requirement | Details |
|---|---|
| 3DO BIOS | A 1 MB BIOS image such as `panafz1.bin`. An optional second 1 MB ROM can be used as BIOS2/Kanji. |
| Disc image | ISO (2048-byte sectors), BIN (2352-byte sectors), CUE+BIN, CHD, DiscJuggler CDI, or Nero NRG. |
| .NET | The published bundles are **self-contained**: the .NET 11 runtime is embedded, so no separate runtime install is required. The .NET 11 SDK is only needed for source builds. |
| Desktop | Windows 10+, a current Linux desktop, or macOS 12+. |
| GPU | OpenGL 3.3 or OpenGL ES 3.0/3.1 for GPU presentation. A CPU fallback is available. |
| Audio | No system OpenAL installation is required; OpenAL Soft 1.23.1 is bundled. The Windows bundles also include the app-local MSVC runtime it needs. |
| Linux display sleep | Display-sleep inhibition uses the freedesktop screen-saver D-Bus API through `python3-dbus` (normally installed with the desktop). Without it the feature is skipped silently. |

## Download

Download the bundle for your system and architecture from
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.14.2):

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.14.2-win-x64.zip` |
| Windows arm64 | `5do-1.14.2-win-arm64.zip` |
| Linux x64 | `5do-1.14.2-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.14.2-linux-arm64.tar.gz` |
| macOS arm64 | `5do-1.14.2-osx-arm64.dmg` |

Each bundle contains both the desktop app and the CLI. The .NET 11 runtime is embedded
in the self-contained apphosts, so no separate runtime install is required. On Windows
extract the ZIP and run
`FiveDo.exe`; on Linux extract the tarball and run
`./FiveDo`; on macOS open the DMG and drag `5DO.app` to Applications.

Bundles are versioned, for example `5do-1.14.2-win-arm64.zip` or
`5do-1.14.2-osx-arm64.dmg`.

### Installers and file associations

Optional unsigned installers can be built from the bundles; see
[Installers and file associations](Installers.md) and
[`packaging/README.md`](https://github.com/purelogiccode/5DO/blob/master/packaging/README.md).
The Windows installer registers `.iso`/`.cue`/`.chd`/`.bin`/`.cdi`/`.nrg` file
associations. The Linux
packages install a desktop entry, icon, and `application/x-3do-disc` MIME type. The
macOS `.app` declares its document UTI. Signing and notarization run only when the
appropriate certificate or Apple account is supplied.

## Build from source

```powershell
git clone https://github.com/purelogiccode/5DO.git
cd 5DO
dotnet build 5DO.sln -c Release
dotnet test 5DO.sln -c Release
```

Start the desktop app:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia
```

Start the CLI:

```powershell
dotnet run --project src/FiveDo.Cli -- --help
```

## First run

1. Start the desktop app.
2. Choose **Open BIOS** and select a 3DO BIOS such as `panafz1.bin`. If 5DO already
   finds a BIOS in one of the [default folders](#bios-management), the guide offers a
   one-click **Use this BIOS** button instead.
3. Choose **Open Disc** and select an ISO, BIN, CUE+BIN, CHD, CDI, or NRG image.
4. Press **Play**.
5. Configure controllers in **Input** and video/audio in **Settings** if needed.

The first-run guide appears once. **About** always shows the current BIOS, disc,
renderer, machine state, and paths.

You can skip the open dialogs on later launches:

```powershell
FiveDo.exe "D:\Games\Road Rash (USA).chd" `
  --bios "D:\BIOS\panafz1.bin" --fullscreen
```

The CLI accepts the same files directly:

```powershell
dotnet run --project src/FiveDo.Cli -- run "game.chd" --bios "panafz1.bin" `
  --frames 600 --frame-out frame.png
```

## Steam Deck and SteamOS

5DO runs as a non-Steam AppImage or Flatpak on SteamOS. The first-run guide detects a
Steam Deck/gamescope session and switches to a larger layout with bigger buttons, the
BIOS folder path, and **Scan** / **Open Folder** actions, so the initial BIOS step works
with the touchscreen or the Deck's trackpad.

1. Switch to **Desktop Mode**.
2. Download `5do-<version>-linux-x86_64.AppImage` from
   [GitHub Releases](https://github.com/purelogiccode/5DO/releases/latest) and make it
   executable (`chmod +x`). The Flatpak bundle works the same way if you prefer a sandbox.
3. Put a legally obtained 1 MB BIOS such as `panafz1.bin` in
   `~/.local/share/5DO/bios`. For the Flatpak, use the app's data folder shown in
   **Settings > Paths** (normally
   `~/.var/app/io.github.purelogiccode.FiveDO/data/5DO/bios`). You can also keep it in a
   `bios` folder beside the app or in `~/BIOS`.
4. In Steam, choose **Games > Add a Non-Steam Game...**, browse to the AppImage (or the
   Flatpak launcher), and add it as **5DO**.
5. Open the game's **Properties > Launch Options** and enter `--fullscreen`. In
   **Controller**, use a layout with a mouse trackpad (for example **Gamepad with Mouse
   Trackpad**); the Deck's touchscreen also works.
6. Return to **Gaming Mode** and launch 5DO. The first-run guide offers the discovered
   BIOS with **Use this BIOS**. If you add or move the ROM while the guide is open,
   press **Scan** to run discovery again.

5DO is not yet listed in EmuDeck/RetroDeck's 3DO emulator list; use the non-Steam method
above until the upstream entry lands. The AppImage and Flatpak both keep the BIOS
requirement visible because DRM-free BIOS files cannot be bundled.

## BIOS management

The desktop **File > BIOS / NVRAM Manager...** dialog scans a folder, validates images,
shows region/Kanji/Anvil/patch metadata, imports without overwriting a different image,
and assigns BIOS1/BIOS2. With auto-selection enabled, 5DO chooses a BIOS from the disc's
game database region, known title requirements, or a per-game override, then falls back
to the configured primary BIOS.

The CLI exposes the same catalog:

```powershell
dotnet run --project src/FiveDo.Cli -- bios scan bios --json
dotnet run --project src/FiveDo.Cli -- bios discover --json
dotnet run --project src/FiveDo.Cli -- bios select game.chd --folder bios --json
dotnet run --project src/FiveDo.Cli -- run game.chd --bios-auto bios --frames 600
```

`bios discover` searches these folders in order and reports the best image:

1. `FIVEDO_BIOS_DIR`, when set
2. `<data folder>/bios` (on Linux, `~/.local/share/5DO/bios`)
3. `bios` next to the executable
4. `~/BIOS`
5. `<Documents>/BIOS`

Discovery only reads files you already have; 5DO never downloads a BIOS. A missing
folder is reported as missing, not as an error.

## Data locations

The desktop app stores settings, NVRAM, save states, cheats, library state, and logs
under the .NET local application data folder:

```text
Windows: %LOCALAPPDATA%\5DO\
Linux:   ~/.local/share/5DO/   (XDG data directory)
macOS:   the .NET LocalApplicationData location for the current user
```

Use **Settings > Paths** or **About** to see the exact paths on your machine.

### Portable mode

Place an empty `portable.flag` next to the executable, or start with `--portable`, to
keep settings, NVRAM, save states, cheats, library state, and logs beside the
executable. `FIVEDO_DATA_DIR` selects an explicit data directory.

Named profiles live under `profiles/<name>/` and can be selected with
`--config-profile <name>` or `FIVEDO_PROFILE`. See
[Configuration](Configuration.md) for the full path and profile rules.

## Platform notes

### Windows

- Windows x64 is the validated target.
- XInput controllers are supported (`xinput1_4`, `xinput1_3`, or `xinput9_1_0`).
- The desktop uses WGL by default and can force ANGLE/GLES with
  `--gl-backend angle`.
- The optional Inno Setup installer in [`packaging/windows`](https://github.com/purelogiccode/5DO/tree/master/packaging/windows)
  installs both apphosts and registers `5DO.Disc`/`5DO.Playlist` associations.

### Linux

- Use a current desktop with OpenGL 3.3 or GLES 3.0/3.1 drivers.
- evdev gamepads are supported; the user normally needs membership in the `input`
  group. `5do-cli doctor` reports unreadable `/dev/input/event*` nodes and prints the
  `usermod` command.
- `packaging/linux/build-deb.sh`, `build-rpm.sh`, `build-appimage.sh`, and
  `build-flatpak.sh` produce the `.deb`, `.rpm`, portable AppImage, and single-file
  Flatpak; CI builds, signs, and publishes them as release assets for x64 and arm64.
  The asset names include the platform and version — `5do_<version>_amd64.deb`,
  `5do-<version>-1.linux.x86_64.rpm`, `5do-<version>-linux-x86_64.AppImage`, and
  `5do-<version>-linux-x86_64.flatpak` — so the release table can map each system to one
  file. Signing, direct installation, verification, and the optional mirror tooling are
  in [Linux distribution and signing](LinuxDistribution.md).
- `flatpak install --user ./5do-<version>-linux-<arch>.flatpak` installs the sandboxed
  build. It keeps its data under `~/.var/app/io.github.purelogiccode.FiveDO/`, while the
  file picker can still read BIOS and disc images from your home directory.
- X11 works directly. On a Wayland session the shell runs under XWayland; a
  Wayland-only session without `DISPLAY` falls back to the slower offscreen presenter.
  `5do-cli doctor` reports which case applies.
- The portable tarball includes a `packaging/` folder with the desktop entry, MIME
  definition, AppStream metainfo, and icons for third-party packagers.
- The self-contained `linux-x64` and `linux-arm64` bundles are produced by CI.

### macOS

- The self-contained macOS arm64 bundle is produced by CI. Intel macOS is
  unsupported as of 1.12.0.
- The gamepad backend uses IOKit HID (Generic Desktop Joystick/Gamepad/Multi-axis) and
  maps through the same eight-player profile as XInput/evdev. macOS may ask for Input
  Monitoring permission; without it the frontend falls back to the keyboard.
- The `.dmg`/`.pkg` script in [`packaging/macos`](https://github.com/purelogiccode/5DO/tree/master/packaging/macos)
  declares the disc UTI and document type. Building it requires a macOS host.
- The macOS arm64 bundle passes the native CLI `selftest`, frontend `--smoke-test`, and
  the Vulkan/MoltenVK GPU path (2026-11-17). Report platform-specific findings with a
  [diagnostics bundle](Diagnostics.md).

### arm64

Windows, Linux, and macOS arm64 bundles are produced and architecture-verified by CI.
All three pass the native CLI `selftest` and frontend `--smoke-test` on hosted arm64
runners (2026-11-10). Raspberry Pi, Android/iOS, and WebAssembly are out of scope. See
[Performance](Performance.md) for the current status.

## GPU and audio troubleshooting

- If OpenGL initialization fails, 5DO falls back to the CPU presenter automatically.
  Force a backend with `--gl-backend auto|wgl|angle|software|vulkan`.
- If audio is silent, check **Settings > Audio**, the selected OpenAL device, and the
  volume/mute state. `audio devices` lists playback devices.
- Use **Help > Save Diagnostics Bundle...** before reporting a rendering or audio
  problem. The bundle scrubs user paths and account names.

See [Troubleshooting](Troubleshooting.md) for symptom-specific fixes.

## Next steps

- [User guide](UserGuide.md)
- [Configuration](Configuration.md)
- [Compatibility](Compatibility.md)
- [FAQ](FAQ.md)
- [CLI reference](CliReference.md)
