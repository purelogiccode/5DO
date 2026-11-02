# Installation

## Requirements

- A legally obtained 3DO BIOS (1 MB; optional second 1 MB ROM for BIOS2/Kanji).
- A legally obtained disc image: ISO, BIN, CUE+BIN, or CHD.
- .NET SDK **10.0.100+** for source builds. Published self-contained builds do not
  need a separate runtime.
- Windows 10+, a current Linux desktop, or macOS 12+.
- OpenGL 3.3 or GLES 3.0/3.1 for GPU presentation; a CPU fallback exists.
- No system OpenAL install is required; OpenAL Soft 1.23.1 is bundled.

There is no stable binary release yet. Build from source or use the portable archives
from a successful [CI run](https://github.com/purelogiccode/5DO/actions/workflows/ci.yml).

## Build and run

```powershell
git clone https://github.com/purelogiccode/5DO.git
cd 5DO
dotnet build 5DO.sln -c Release
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia
```

Open the desktop app, choose **Open BIOS**, select a BIOS such as `panafz1.bin`, choose
**Open Disc**, and press **Play**.

Open a disc directly:

```powershell
FiveDo.Frontend.Avalonia.exe "D:\Games\Road Rash (USA).chd" `
  --bios "D:\BIOS\panafz1.bin" --fullscreen
```

## Headless

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 600 --frame-out frame.png
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
