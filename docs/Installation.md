# Installation

This page covers getting 5DO running on Windows, Linux, and macOS. 5DO does **not**
include a BIOS ROM or any game images. You must supply legally obtained files.

## Before you start

| Requirement | Details |
|---|---|
| 3DO BIOS | A 1 MB BIOS image such as `panafz1.bin`. An optional second 1 MB ROM can be used as BIOS2/Kanji. |
| Disc image | ISO (2048-byte sectors), BIN (2352-byte sectors), CUE+BIN, or CHD. |
| .NET | The **.NET 10 Runtime** must be installed on the host; the published bundles are framework-dependent and do **not** include the runtime. The .NET 10 SDK is only needed for source builds. |
| Desktop | Windows 10+, a current Linux desktop, or macOS 12+. |
| GPU | OpenGL 3.3 or OpenGL ES 3.0/3.1 for GPU presentation. A CPU fallback is available. |
| Audio | No system OpenAL installation is required; OpenAL Soft 1.23.1 is bundled. |

## Download

Download the bundle for your system and architecture from
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.2.1):

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.2.1-win-x64.zip` |
| Windows arm64 | `5do-1.2.1-win-arm64.zip` |
| Linux x64 | `5do-1.2.1-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.2.1-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.2.1-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.2.1-osx-arm64.tar.gz` |

Each bundle contains both the desktop app and the CLI. Install the .NET 10 Runtime
first from <https://dotnet.microsoft.com/download/dotnet/10.0>; the bundle does not
contain a .NET runtime. Extract the bundle and run the executables.

Bundles are versioned, for example `5do-1.2.1-win-arm64.zip`.

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
2. Choose **Open BIOS** and select a 3DO BIOS such as `panafz1.bin`.
3. Choose **Open Disc** and select an ISO, BIN, CUE+BIN, or CHD image.
4. Press **Play**.
5. Configure controllers in **Input** and video/audio in **Settings** if needed.

The first-run guide appears once. **About** always shows the current BIOS, disc,
renderer, machine state, and paths.

You can skip the open dialogs on later launches:

```powershell
FiveDo.Frontend.Avalonia.exe "D:\Games\Road Rash (USA).chd" `
  --bios "D:\BIOS\panafz1.bin" --fullscreen
```

The CLI accepts the same files directly:

```powershell
dotnet run --project src/FiveDo.Cli -- run "game.chd" --bios "panafz1.bin" `
  --frames 600 --frame-out frame.png
```

## BIOS management

The desktop **File > BIOS / NVRAM Manager...** dialog scans a folder, validates images,
shows region/Kanji/Anvil/patch metadata, imports without overwriting a different image,
and assigns BIOS1/BIOS2. With auto-selection enabled, 5DO chooses a BIOS from the disc's
game database region, known title requirements, or a per-game override, then falls back
to the configured primary BIOS.

The CLI exposes the same catalog:

```powershell
dotnet run --project src/FiveDo.Cli -- bios scan bios --json
dotnet run --project src/FiveDo.Cli -- bios select game.chd --folder bios --json
dotnet run --project src/FiveDo.Cli -- run game.chd --bios-auto bios --frames 600
```

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
- A Linux `.desktop` association file and portable packaging scripts are in
  [`packaging/`](https://github.com/purelogiccode/5DO/tree/master/packaging).

### Linux

- Use a current desktop with OpenGL 3.3 or GLES 3.0/3.1 drivers.
- evdev gamepads are supported; the user normally needs membership in the `input`
  group.
- The local validation target is a Linux Mint virtual machine. Full distribution
  coverage is still post-1.0 work.
- The framework-dependent `linux-x64` and `linux-arm64` bundles are produced by CI.

### macOS

- The framework-dependent `osx-x64` and `osx-arm64` bundles are produced by CI.
- There is no platform gamepad backend yet; use the keyboard.
- Runtime validation is post-1.0 work. Please report platform-specific findings with
  a [diagnostics bundle](Diagnostics.md).

### arm64

Windows, Linux, and macOS arm64 bundles are produced and architecture-verified by CI.
Runtime validation on arm64 hardware is post-1.0; report findings with a diagnostics
bundle. Raspberry Pi, Android/iOS, and WebAssembly are out of scope. See
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
