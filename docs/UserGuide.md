# 5DO User Guide

This guide covers the desktop application and the headless CLI. The emulator does not
include a BIOS ROM or any game images; you must supply legally obtained files.

## Quick start

1. Install the .NET 10 runtime (or use a self-contained build).
2. Start the desktop app:

   ```powershell
   dotnet run -c Release --project src/FiveDo.Frontend.Avalonia
   # or open a disc directly (also used by file associations):
   dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- "game.chd"
   ```

3. On first run, choose **Open BIOS** and select a 3DO BIOS such as `panafz1.bin`.
4. Choose **Open Disc** and select an ISO, BIN, CUE+BIN, or CHD image.
5. Press **Play**. The status line shows the frame count, PC, cycles, and FPS.

The first-run guide is shown only once. **About** always shows the current BIOS, disc,
renderer, machine state, and paths.

## Controls

Default player 1 keyboard bindings:

| 3DO button | Key |
|---|---|
| D-pad | Arrow keys |
| A | Z |
| B | X |
| C | C |
| L | A |
| R | S |
| P (play/pause) | Enter |
| X (stop) | Backspace |

**Input** opens the binding editor. Keyboard bindings can be captured for all six
players. The **Gamepad** tab maps physical controls to 3DO buttons; left-stick
directions fall back to the D-pad, and analog triggers fall back to the matching
shoulder button when no explicit binding exists.

Supported gamepad backends:

- Windows: XInput (`xinput1_4`, `xinput1_3`, or `xinput9_1_0`).
- Linux: evdev (`/dev/input/event*`; the user normally needs the `input` group).
- macOS: no platform backend yet; use a keyboard.

Windows/Linux backends hot-plug controllers. The **About** dialog reports the number
of connected controllers.

## Display

The desktop renderer expands the 3DO palette at native resolution and scales on the
GPU. Available filters:

- **Accurate**: nearest-neighbor, pixel-perfect; the reference for the CPU assembler.
- **Nearest**: nearest-neighbor with arbitrary scale/letterboxing.
- **SharpBilinear**: the default enhanced mode.
- **CRT**: scanlines, aperture mask, curvature, vignette, and a configurable bloom
  glow (Settings -> Video -> CRT bloom).
- **Xbr**: Hyllian's xBR-lv2 edge-directed 4x pass, followed by a sharp-bilinear finish.
  This is an opt-in filter and costs more GPU time at 4K.
- **Hqx / Hqx3x / Hqx4x**: HQx 2x/3x/4x pixel-art smoothing on the CPU-expanded
  frame, followed by a sharp-bilinear finish. They are opt-in and use CPU time per
  frame; 3x/4x cost roughly twice/three times the 2x filter.

The Settings dialog's **Emulation** page can override the filter for the current
game. **Automatic** uses the global filter; an explicit choice is stored by game ID
in `settings.json`.

Aspect, integer scaling, vsync preference (applied through WGL/GLX/EGL where the
platform exposes a swap-interval extension), fullscreen (F11/Escape; the cursor is
hidden while fullscreen), and the FPS/status overlay are in **Settings**. The renderer
falls back to the CPU `WriteableBitmap` presenter when OpenGL initialization or context
recovery fails; the status line and **Logs** report the reason.

On Windows, the frontend accepts `--gl-backend auto|wgl|angle|software` to force a
renderer backend for troubleshooting or validation. `auto` is the default (desktop
WGL, then ANGLE/GLES, then software). The developer `--gl-probe` tool uses the same
option and can capture a 1:1 Accurate frame for CPU/GPU parity; see
[`Performance.md`](Performance.md).

The opt-in **Hi-Res** mode renders 640x480 CELs and restarts the machine when toggled.
It is off by default because 320x240 titles must not be upscaled.

## Save states and screenshots

The shell exposes ten numbered slots. **Save State** and **Load State** use the
selected slot; **<** and **>** change slots. **Slots** lists, saves, loads, and deletes
states. Each slot writes `slot-NN.tdos`, a JSON sidecar, and a PNG thumbnail. Writes are
atomic.

**Screenshot** writes a PNG named `<game>-<timestamp>-f<frames>.png` from the OpenGL
output (or the CPU frame when GL is unavailable). Enable **Also save the native CPU
frame with screenshots** in **Settings > Video** to write a second `-native.png`
image from the CPU frame assembler. The default directory is your Pictures folder;
change it in **Settings > Paths**.

## Library and settings

**Library** scans the folders configured in **Settings > Paths** for ISO/BIN/CUE/CHD
files and resolves titles through the embedded 4DO game database. Select a title to
open it. A sibling image with the same base name (for example `game.png` beside
`game.chd`) is shown as cover art.

Settings persist under the user local application data folder:

- Windows: `%LOCALAPPDATA%\5DO\settings.json`
- Linux/macOS: the equivalent local application data directory.

Per-game fix-mode overrides, automatic 4DO fixes, audio enable/volume, high-resolution
mode, save/screenshot paths, input bindings, and the current slot are stored there.
NVRAM is stored as `nvram.ram` in the same directory.

## Audio

The desktop app ships the OpenAL Soft 1.23.1 runtime, so live stereo output works
without a system OpenAL installation. Use Settings > Audio to enable/disable audio
and set the volume. If no playback device is available, the app logs a warning and
continues silently; the emulated DSP still runs so games do not stall.

The core produces exactly 44,100 stereo samples per emulated second, and the
dedicated core thread paces frames against that clock (falling back to 60 Hz when
no samples are available). The optional FPS overlay shows measured/target fps.

The CLI can write WAV files with `--audio wav --wav-out <path>` or play live with
`--audio openal --volume 0.5`. Add `--realtime` to pace a CLI run at the audio
clock; without it, the CLI runs as fast as possible and live audio is a short
burst.

To inspect a capture:

```powershell
dotnet run --project src/FiveDo.Cli -- audio analyze capture.wav --json
```

The report includes sample rate, channels, duration, peak, RMS, clipped/silent
frames, and DC offset. `run --json` includes the same statistics for WAV captures
and OpenAL dropped/underrun/queued/buffered counters plus estimated latency.

## Logs

**Logs** shows the in-memory diagnostic log with level, category, search, auto-refresh,
and clear controls. The log records BIOS/disc loads, renderer initialization and
fallback, save/load, screenshots, input changes, and emulation faults.

## CLI

Common commands:

```powershell
dotnet run -c Release --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin --frames 600 --frame-out frame.png
dotnet run -c Release --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin --frames 600 --prefetch --high-res
dotnet run -c Release --project src/FiveDo.Cli -- info game.chd --json
dotnet run -c Release --project src/FiveDo.Cli -- bios info panafz1.bin
dotnet run -c Release --project src/FiveDo.Cli -- state inspect quick.tdos
dotnet run -c Release --project src/FiveDo.Cli -- bench --frames 600
dotnet run -c Release --project src/FiveDo.Cli -- selftest
```

Audio backends: `--audio null|wav|openal`, `--wav-out`, `--volume`, `--realtime`.
Diagnostics: `--watch-lowmem`, `--dump-ram`, `--dump-dsp`, `--trace-frames`, `--poke`,
`--capture-dir`, `--capture-every`, `--input-script`, and `--autopilot road`. See
[`TestCorpus.md`](TestCorpus.md) for the full command set and script format.

Input scripts use **absolute emulated frame numbers** by default. When a run starts
from `--state-in`, the first executed frame is the state's saved frame count, so a
script that starts at `0` applies its last entry immediately. Add
`--script-base state` to shift the whole script by the loaded state's frame count;
for example, `0 A` then applies on the first frame after the state loads. `run
--json` reports `cdStatus` and `cdPoll`; a static READ_ID-only wait shows
`cdStatus=0xE1` and `cdPoll=0x0C`.

## Troubleshooting

- **"Load a BIOS first"**: open a BIOS before pressing Play.
- **Disc error**: verify the image is a 3DO disc and that the BIOS is compatible. CHD
  must be a CD image, not a DVD/HDD/raw image.
- **OpenGL unavailable**: update the GPU driver. The CPU fallback still runs; check
  **Logs** for the exact initialization error.
- **No gamepad input**: check that the controller is connected before starting the
  emulator, and review the **Gamepad** tab in **Input**. Linux users may need the
  `input` group.
- **Save state rejected**: states are tied to a BIOS/disc hash; warnings are reported
  in the status line and log, but the state is still loaded when possible.
- **xBR is slow**: use SharpBilinear or Accurate at 4K, or lower the output resolution.

## Legal

FreeDO/4DO are not standard OSI-licensed projects. Read
[`../THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md) and
[`Licensing.md`](Licensing.md) before distributing builds. "3DO" and "Panasonic" are
trademarks of their respective owners; this project is not affiliated with or endorsed
by them.
