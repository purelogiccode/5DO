# Installation

## Download

Download the bundle for your system and architecture from
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.3.0):

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.3.0-win-x64.zip` |
| Windows arm64 | `5do-1.3.0-win-arm64.zip` |
| Linux x64 | `5do-1.3.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.3.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.3.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.3.0-osx-arm64.tar.gz` |

Each bundle contains both the desktop app and the CLI. The bundles are
framework-dependent: install the **.NET 10 Runtime** from
<https://dotnet.microsoft.com/download/dotnet/10.0> before running them. No .NET
runtime is included in the bundle.

Extract the bundle and run the executables.

## Requirements

- The **.NET 10 Runtime** installed on the host. The .NET 10 SDK is only needed for
  source builds.
- A legally obtained 3DO BIOS (1 MB; optional second 1 MB ROM for BIOS2/Kanji).
- A legally obtained disc image: ISO, BIN, CUE+BIN, or CHD.
- Windows 10+, a current Linux desktop, or macOS 12+.
- OpenGL 3.3 or GLES 3.0/3.1 for GPU presentation; a CPU fallback exists.
- No system OpenAL install is required; OpenAL Soft 1.23.1 is bundled.

## Run the downloaded bundle

Windows:

```powershell
.\FiveDo.Frontend.Avalonia.exe
```

Linux/macOS:

```sh
mkdir 5do-linux-x64
tar -xzf 5do-1.3.0-linux-x64.tar.gz -C 5do-linux-x64
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
