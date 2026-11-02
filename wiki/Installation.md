# Installation

## Download

Download the bundle for your system from
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0):

| System | Bundle |
|---|---|
| Windows x64 | `5do-win-x64.zip` |
| Linux x64 | `5do-linux-x64.tar.gz` |
| macOS x64 | `5do-osx-x64.tar.gz` |

Each bundle contains both the desktop app and the CLI, plus a private .NET 10 runtime
under `runtime/`. No separate .NET install is required.

Extract the whole bundle and keep `runtime/` next to the executables.

## Requirements

- A legally obtained 3DO BIOS (1 MB; optional second 1 MB ROM for BIOS2/Kanji).
- A legally obtained disc image: ISO, BIN, CUE+BIN, or CHD.
- Windows 10+, a current Linux desktop, or macOS 12+.
- OpenGL 3.3 or GLES 3.0/3.1 for GPU presentation; a CPU fallback exists.
- No system OpenAL install is required; OpenAL Soft 1.23.1 is bundled.
- The .NET 10 SDK is only needed for source builds.

## Run the downloaded bundle

Windows:

```powershell
.\FiveDo.Frontend.Avalonia.exe
```

Linux/macOS:

```sh
tar -xzf 5do-linux-x64.tar.gz      # or 5do-osx-x64.tar.gz
cd 5do-linux-x64
./FiveDo.Frontend.Avalonia
```

If the extracted executable is not executable on Linux/macOS:

```sh
chmod +x FiveDo.Cli FiveDo.Frontend.Avalonia
```

Open the desktop app, choose **Open BIOS**, select a BIOS such as `panafz1.bin`, choose
**Open Disc**, and press **Play**.

Open a disc directly:

```sh
./FiveDo.Frontend.Avalonia "Road Rash (USA).chd" --bios panafz1.bin --fullscreen
```

## Headless CLI

```sh
./FiveDo.Cli run game.chd --bios panafz1.bin --frames 600 --frame-out frame.png
```

## Build and run from source

```powershell
git clone https://github.com/purelogiccode/5DO.git
cd 5DO
dotnet build 5DO.sln -c Release
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia
```

## Data folders

The app stores settings, NVRAM, saves, cheats, library state, and logs under the .NET
local application data folder:

```text
Windows: %LOCALAPPDATA%\5DO\
Linux:   ~/.local/share/5DO/
macOS:   the .NET LocalApplicationData location
```

Use **Settings > Paths** or **About** for the exact paths. Portable mode uses
`portable.flag`, `--portable`, or `FIVEDO_DATA_DIR`.

## Full guide

See [Installation](https://purelogiccode.github.io/5DO/Installation.html) and the
[User guide](https://purelogiccode.github.io/5DO/UserGuide.html).
