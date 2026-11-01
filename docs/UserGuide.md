# 5DO User Guide

This guide covers the desktop application and the headless CLI. The emulator does not
include a BIOS ROM or any game images; you must supply legally obtained files.

For the complete documentation map, see [`README.md`](README.md). For every CLI option,
see [`CliReference.md`](CliReference.md); for settings and paths, see
[`Configuration.md`](Configuration.md); for save-state details, see
[`SaveStates.md`](SaveStates.md); for symptom-driven fixes, see
[`Troubleshooting.md`](Troubleshooting.md).

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

### BIOS manager and automatic selection

Settings > Paths now edits a BIOS folder, a primary BIOS, an optional BIOS2/Kanji font,
and the **Auto-select BIOS** option. **Scan BIOS folder** validates every `.bin`/`.rom`
file and reports primary/secondary/error counts. The scanner recognizes the common
Panasonic FZ-1/FZ-10/FZ-10E/FZ-10JA, GoldStar, Sanyo, patched arcade, Kanji font, and
arcade support ROM hashes; unknown images are accepted with file-name-derived metadata.

When a BIOS folder is set and auto-selection is enabled, 5DO chooses a BIOS from the
disc's game database region, known title requirements, and the image's Kanji/Anvil/patch
metadata. The configured primary BIOS remains the fallback. Use **File > Assign BIOS to
Current Game...** to pin a specific image to the current game, or **Clear Game BIOS
Assignment** to return it to automatic selection. The status line and log show which BIOS
was selected and why.

From the CLI, `--bios-auto <folder>` performs the same selection for `run`/`trace`, and
`bios list`, `bios scan`, and `bios select` expose the catalog directly. `--bios2` loads
an explicit secondary ROM.

## Command-line launch (frontends and file associations)

The desktop executable accepts the following arguments. A positional disc path or
`--disc` opens and auto-starts the game, so no extra Play action is needed. This is the
form used by file associations and external emulator frontends:

```powershell
FiveDo.Frontend.Avalonia.exe "D:\Games\Road Rash (USA).chd" `
  --bios "D:\BIOS\panafz1.bin" --fullscreen
```

| Argument | Meaning |
|---|---|
| `"<disc>"` | Positional disc path (ISO, BIN, CUE+BIN, or CHD); opens and auto-starts it. |
| `--disc <path>` | Explicit disc path; same behavior as the positional form. |
| `--bios <path>` | BIOS image for this launch; overrides the saved BIOS path. A missing file prints an error and exits with code 1. |
| `-f`, `--fullscreen` | Start in fullscreen. Works with a disc or with the library view. Escape/F11 leaves fullscreen. |
| `--gl-backend <mode>` | `auto` (default), `wgl`, `angle`, `software`, or `vulkan`. |
| `--gl-probe-backend <mode>` | Alias for `--gl-backend` retained for the GPU probe. |
| `--version` | Print the version and exit. |
| `-h`, `--help` | Print frontend usage and exit. |

Launcher command template:

```text
FiveDo.Frontend.Avalonia.exe "{rom}" --bios "{bios}" --fullscreen
```

Developer/CI options already supported by the frontend:

| Argument | Meaning |
|---|---|
| `--gl-probe <json>` | OpenGL presentation probe with `--gl-probe-size`, `--gl-probe-frames`, `--gl-probe-warmup`, `--gl-probe-filter`, `--gl-probe-vsync`, `--gl-probe-offscreen`, `--gl-probe-capture`, and `--gl-probe-timeout`. |
| `--vk-probe <json>` | Headless Vulkan probe with `--vk-probe-size`, `--vk-probe-frames`, `--vk-probe-warmup`, `--vk-probe-filter`, `--vk-probe-capture`, and `--vk-probe-timeout`. |
| `--vk-window-probe <json>` | Native Win32 Vulkan swapchain probe with the matching `--vk-window-probe-*` options. |
| `--smoke-test [--smoke-output <path>]` | Startup smoke test used by CI; exits after the first UI pass. |

See [`Performance.md`](Performance.md), [`Vulkan.md`](Vulkan.md), and
[`BuildAndTest.md`](BuildAndTest.md) for the probe reports and commands.

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

### Keyboard shortcuts

- **F5** saves the current state to the selected slot.
- **F8** loads the selected slot.
- **F6** / **F7** select the previous / next save-state slot.
- **F9** advances the paused machine by one frame.
- **Shift+F9** rewinds one frame.
- **R** rewinds one second.
- **Tab** holds fast-forward; release it to return to normal speed.
- **F10** resets the machine.
- **F11** toggles fullscreen.
- **F12** captures a screenshot.
- **Escape** pauses emulation; in fullscreen it exits fullscreen first.
- **Save State**, **Load State**, **Screenshot**, **Reset**, slot changes, and
  fullscreen are also available from the toolbar and the **Emulation** menu.

The hotkey map is part of the input profile. Custom key assignments are resolved
case-insensitively; an unbound key falls through to the emulated pad.

### Rewind and fast-forward

Rewind is on by default. 5DO keeps compressed keyframes and the exact per-frame
controller packet for the configured window (60 seconds by default, 30-300 seconds in
**Settings > Emulation**). Press **R** to step back one second or **Shift+F9** to step
back one frame. The **Emulation** menu also exposes **Rewind 1 Second** and **Rewind 1
Frame**. Rewinding restores the full machine state and replays the recorded input, so
resuming is deterministic. The history is cleared when a state is loaded, the machine is
reset, or a different disc starts.

Hold **Tab** to fast-forward. The default multiplier is 4x and is configurable from 2x
to 10x in **Settings > Emulation**. The **Emulation > Fast-Forward** menu item toggles
the same mode for pointer-only use. Intermediate frames skip presentation and audio is
muted while fast-forwarding; emulation and input are unchanged. Releasing **Tab**,
pausing, stopping, or resetting returns to normal speed.

The default memory cap for compressed keyframes is 256 MB. Older keyframes are dropped
first, and at least one keyframe is always retained. Keyframes are compressed on the
emulation thread, so a very slow machine may see a brief hitch when one is captured
(every 120 frames by default).

### Cheats

**Emulation > Cheats...** opens the per-game cheat manager. Each game has a JSON file
named after its 4DO ID in the cheat directory (**Settings > Paths > Cheats**). The
manager lists every entry with an enable checkbox; select one to edit its name,
description, or code, or use **Add**, **Update**, and **Delete**. Changes are saved
immediately, invalid code is shown in red and is not applied, and the running game picks
up the new set without a restart.

Cheats are applied before every emulated frame, so they survive save-state loads, rewind,
and fast-forward. The engine supports raw byte writes, typed `write8`/`write16`/`write32`
writes, memory conditions, button conditions, and `and`/`or`/`not` combinations. Numbers
are hexadecimal by default. See [`Cheats.md`](Cheats.md) for the complete syntax and
CLI commands.

### Recording input

**Emulation > Record Input** starts capturing player-1 controller input while the
game runs. Selecting it again stops recording and writes
`<game>-<timestamp>-input.txt` to the configured save-state directory (or the default
5DO directory). Unchanged frames are coalesced into one line per button change, and
the file uses the same format as `--input-script`:

```text
# frame buttons
0 None
120 A
180 A,Down
240 None
```

Replay a recording headlessly with:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 1000 --input-script "path/to/game-20261029-120000-input.txt"
```

If the recording started after a save state was loaded, add `--script-base state` so
the script's frame numbers are shifted by the loaded frame count.

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
- **FSR**: AMD FidelityFX Super Resolution 1.0 (EASU upscale + RCAS sharpening) at the
  output resolution. It is opt-in and has a **FSR sharpness** slider in
  Settings > Video (0 = soft, 1 = maximum). It costs two GPU passes and is not
  recommended at 4K on weak GPUs.

The Settings dialog's **Emulation** page can override the filter for the current
game. **Automatic** uses the global filter; an explicit choice is stored by game ID
in `settings.json`.

Aspect, custom aspect ratio, integer scaling, vsync preference (applied through
WGL/GLX/EGL where the platform exposes a swap-interval extension), CRT bloom, FSR
sharpness, fullscreen
(F11/Escape; the cursor is hidden while fullscreen), and the FPS/status overlay are in
**Settings > Video**. **Frame skip** controls presentation catch-up: **Auto** is the
default and only drops presentation frames when the host misses its deadline; fixed
modes present one of every 2/3/4/5 frames. Emulation, input, audio, and save states still
run at full speed in every mode, so this is not fast-forward. Use Auto for normal play;
a fixed mode is useful on a slow GPU or when recording. The status overlay shows both
the emulated and presented rates. The **Graphics API** list selects **OpenGL**,
**Vulkan**, or **Software (CPU)**; changing it recreates the presenter without restarting
emulation. **Output resolution** selects the final presentation size: match the window,
native 320x240, 2x/3x/4x, 720p, 1080p, 1440p, or 4K. This is output upscaling, not
internal CEL rendering. The renderer falls back to the CPU `WriteableBitmap` presenter
when OpenGL initialization or context recovery fails; the status line and **Logs** report
the reason.

The fresh-install defaults are tuned for 60 fps without lowering quality: OpenGL,
match-window output, **SharpBilinear**, vsync on, integer scaling, and **Auto** frame
skip. If a title still misses 60 fps, use `Nearest`, a lower output resolution, or turn
off expensive filters first. The emulation core is also optimized in Debug builds, so
starting the app from the IDE should stay near Release speed.

On Windows, the frontend accepts `--gl-backend auto|wgl|angle|software|vulkan` to force
a renderer backend for troubleshooting or validation. `auto` is the default (desktop
WGL, then ANGLE/GLES, then software). The developer `--gl-probe` tool uses the same
option and can capture a 1:1 Accurate frame for CPU/GPU parity; see
[`Performance.md`](Performance.md).

The opt-in **Hi-Res** mode renders 640x480 CELs and restarts the machine when toggled.
It is off by default because 320x240 titles must not be upscaled.

## Save states and screenshots

The shell exposes ten numbered slots. **Save State** and **Load State** use the
selected slot; **<** and **>** change slots. **Slots** lists, saves, loads, and deletes
states. Each slot writes `slot-NN.tdos`, a JSON sidecar, and a PNG thumbnail. Writes are
atomic. Format version 2 compresses large sections with Brotli, so a state is typically
0.6-2.0 MB on disk (the uncompressed payload is ~25 MB). Version-1 states still load;
see [`SaveStates.md`](SaveStates.md) for the format and limits.

**Screenshot** writes a PNG named `<game>-<timestamp>-f<frames>.png` from the OpenGL
output (or the CPU frame when GL is unavailable). Enable **Also save the native CPU
frame with screenshots** in **Settings > Video** to write a second `-native.png`
image from the CPU frame assembler. The default directory is your Pictures folder;
change it in **Settings > Paths**.

## Library and settings

**Library** scans the folders configured in **Settings > Paths** for ISO/BIN/CUE/CHD
files and resolves titles through the embedded 4DO game database. Select a title to
open it. A sibling image with the same base name (for example `game.png` beside
`game.chd`) is shown as cover art. Scanning is recursive to depth 4. Discs are listed
immediately; game IDs and database titles are resolved in the background. Later scans
reuse `library-cache.json` and only reopen files whose size or timestamp changed.

Settings persist under the user local application data folder:

- Windows: `%LOCALAPPDATA%\5DO\settings.json`
- Linux/macOS: the equivalent local application data directory.
- Library metadata cache: `%LOCALAPPDATA%\5DO\library-cache.json`.

Per-game fix-mode overrides, automatic 4DO fixes, audio enable/volume, high-resolution
mode, the timing profile, ARM/CLIO/CEL/CD timing, CD speed, NVRAM storage/version,
save/screenshot paths, input bindings, and the current slot are stored there.
NVRAM defaults to `nvram.ram` in the same directory. Choose **Settings > Emulation >
NVRAM storage > Per game** to keep each title's save data in
`nvram/per-game/<gameId>.<version>.ram`, or pick a version 0-9 for independent save
slots. The first per-game save copies the shared image so existing data is not lost.
**File > Export NVRAM...**, **Import NVRAM...**, and **Reset NVRAM** manage the current
image from the desktop shell; the CLI can list, export, import, and reset images with
`nvram path|list|export|import|reset`.
The full `settings.json` schema, per-game override keys, input profile format, and
precedence rules are in [`Configuration.md`](Configuration.md).

The game library also shows a compact donation banner. **Donate** opens
[the 5DO donation page](https://www.purelogiccode.com/donate?app=5do), and
**I already donated** asks for the email used on the website and checks it through the
donation API. The email is stored only in the local settings file and is sent only when
checking a donation. A confirmed donation hides the banner; the close button hides it
until the next launch. The same actions are available under **Help > Donation Status...**
and **Help > Support 5DO...**.

## Audio

The desktop app ships the OpenAL Soft 1.23.1 runtime, so live stereo output works
without a system OpenAL installation. Use Settings > Audio to enable/disable audio,
set the volume, and play a 1 s 440 Hz test tone; the test reports the OpenAL error
when no playback device is available. If no device is present, the app logs a
warning and continues silently; the emulated DSP still runs so games do not stall.

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

### Audio diagnostics tap

Set `FIVEDO_AUDIO_TAP` to a path prefix before launching the CLI with `--audio openal`
or the desktop app (which uses OpenAL when audio is enabled) to record the exact PCM
handed to OpenAL and a per-buffer timing log:

```powershell
$env:FIVEDO_AUDIO_TAP = "$PWD\artifacts\tap"
dotnet run --project src/FiveDo.Frontend.Avalonia
```

This writes `tap.wav` (the signal the emulator queued, before the driver and any
Bluetooth link) and `tap.csv` (elapsed time, buffer size, ring/queue depth,
underrun flag, peak, and OpenAL error per buffer). Comparing `tap.wav` with a
loopback recording of the output device separates emulator defects from driver,
Bluetooth, and speaker problems. The status overlay also shows a live peak meter
and the ring/queue/underrun/drop counters.

## Logs

**Logs** shows the in-memory Serilog log with level, category, search, auto-refresh,
and clear controls. The log records BIOS/disc loads, renderer initialization and
fallback, save/load, screenshots, input changes, and emulation faults.

Both apps also write rolling daily files (seven retained):

- Windows: `%LOCALAPPDATA%\5DO\logs\5do-YYYYMMDD.log`
- macOS: `~/Library/Logs/5DO/`
- Linux: `$XDG_STATE_HOME/5DO/logs` or `~/.local/state/5DO/logs`

Set `FIVEDO_LOG_DIR` to redirect the files. Warning-or-higher events are also sent
asynchronously to the bug-report service with environment, error, and exception
details; submissions are deduplicated for five minutes and capped at ten per minute.
Set `FIVEDO_BUG_REPORTS=0` to disable submission. CI and test runs disable it
automatically, and the CLI keeps JSON on stdout with concise errors on stderr.

### Usage statistics

On launch, 5DO sends the application id (`5do`) and version to the
ApplicationStats service so the project can track usage. No user, machine, or game
data is sent. Set `FIVEDO_STATS=0` to opt out; CI and test runs are disabled
automatically. Override the endpoint, key, or id with `FIVEDO_STATS_ENDPOINT`,
`FIVEDO_STATS_API_KEY`, and `FIVEDO_STATS_APPLICATION_ID`.

## CLI

Common commands:

```powershell
dotnet run -c Release --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin --frames 600 --frame-out frame.png
dotnet run -c Release --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin --frames 600 --prefetch --high-res
dotnet run -c Release --project src/FiveDo.Cli -- info game.chd --json
dotnet run -c Release --project src/FiveDo.Cli -- bios info panafz1.bin
dotnet run -c Release --project src/FiveDo.Cli -- bios scan bios --json
dotnet run -c Release --project src/FiveDo.Cli -- run game.chd --bios-auto bios --frames 600
dotnet run -c Release --project src/FiveDo.Cli -- state inspect quick.tdos
dotnet run -c Release --project src/FiveDo.Cli -- bench --frames 600
dotnet run -c Release --project src/FiveDo.Cli -- selftest
```

Audio backends: `--audio null|wav|openal`, `--wav-out`, `--volume`, `--realtime`.
Diagnostics: `--watch-lowmem`, `--dump-ram`, `--dump-dsp`, `--trace-frames`, `--poke`,
`--capture-dir`, `--capture-every`, `--input-script`, and `--autopilot road`. See
[`CliReference.md`](CliReference.md) for the full command/option/JSON reference and
[`Diagnostics.md`](Diagnostics.md) for trace formats; [`TestCorpus.md`](TestCorpus.md)
has the local corpus commands and script format.

Input scripts use **absolute emulated frame numbers** by default. When a run starts
from `--state-in`, the first executed frame is the state's saved frame count, so a
script that starts at `0` applies its last entry immediately. Add
`--script-base state` to shift the whole script by the loaded state's frame count;
for example, `0 A` then applies on the first frame after the state loads. `run
--json` reports `cdStatus` and `cdPoll`; a static READ_ID-only wait shows
`cdStatus=0xE1` and `cdPoll=0x0C`.

## Troubleshooting

- **"Load a BIOS first"**: open a BIOS before pressing Play, or set a BIOS folder with
  auto-selection enabled. Check the scan result if the folder contains renamed dumps.
- **Disc error**: verify the image is a 3DO disc and that the BIOS is compatible. CHD
  must be a CD image, not a DVD/HDD/raw image.
- **OpenGL unavailable**: update the GPU driver. The CPU fallback still runs; check
  **Logs** for the exact initialization error.
- **No gamepad input**: check that the controller is connected before starting the
  emulator, and review the **Gamepad** tab in **Input**. Linux users may need the
  `input` group.
- **Save state rejected**: states are tied to a BIOS/disc hash; the loader warns on a
  BIOS or emulator-version mismatch but still loads when possible. A different disc is
  recorded in the header but not rejected, so verify the slot metadata. See
  [`SaveStates.md`](SaveStates.md).
- **xBR/HQx/FSR is slow**: use SharpBilinear or Accurate at 4K, or lower the output
  resolution. HQx 3x/4x and FSR are the most expensive opt-in modes.

For a full symptom-by-symptom guide, see [`Troubleshooting.md`](Troubleshooting.md).
For traces, dumps, and probes, see [`Diagnostics.md`](Diagnostics.md).

## Legal

5DO is distributed under the FreeDO modified GNU LGPL; see [`../LICENSE`](../LICENSE).
FreeDO/4DO are not standard OSI-licensed projects. Read
[`../THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md) and
[`Licensing.md`](Licensing.md) before distributing builds. "3DO" and "Panasonic" are
trademarks of their respective owners; this project is not affiliated with or endorsed
by them.
