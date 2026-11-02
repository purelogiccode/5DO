# Quick Start

## From the release bundle

1. Download the bundle for your system from
   [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0).
2. Extract the whole bundle. Keep `runtime/` next to the executables.
3. Start the desktop app:

   ```powershell
   .\FiveDo.Frontend.Avalonia.exe
   ```

   Linux/macOS:

   ```sh
   ./FiveDo.Frontend.Avalonia
   ```

4. Choose **Open BIOS** and select your legally obtained 3DO BIOS.
5. Choose **Open Disc** and select an ISO, BIN, CUE+BIN, or CHD.
6. Press **Play**.

## From source

1. Install the .NET 10 SDK.
2. Clone and build:

   ```powershell
   git clone https://github.com/purelogiccode/5DO.git
   cd 5DO
   dotnet build 5DO.sln -c Release
   ```

3. Start the desktop app:

   ```powershell
   dotnet run -c Release --project src/FiveDo.Frontend.Avalonia
   ```

Default player-1 controls: arrows for the D-pad, `Z` = A, `X` = B, `C` = C,
`A` = L, `S` = R, `Enter` = P, `Backspace` = X.

Useful shortcuts: `F5` save, `F8` load, `F11` fullscreen, `F12` screenshot,
`R` rewind, hold `Tab` fast-forward, `F4` quick menu.

Headless example:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 600 --audio wav --wav-out capture.wav
```

Next: [Installation](Installation), [FAQ](FAQ), and the
[User guide](https://purelogiccode.github.io/5DO/UserGuide.html).
