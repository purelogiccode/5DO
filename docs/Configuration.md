# Configuration Reference

This document describes the desktop settings file, per-game overrides, input profiles,
NVRAM, paths, and environment variables. The CLI does **not** read `settings.json`; its
behavior is controlled entirely by command-line options (see
[`CliReference.md`](CliReference.md)).

---

## 1. Where settings live

The desktop app uses .NET's local application data folder:

```text
<LocalApplicationData>/5DO/
```

`AppSettingsStore.DefaultDirectory` and `DefaultPath` implement this. On Windows:

```text
%LOCALAPPDATA%\5DO\settings.json
%LOCALAPPDATA%\5DO\nvram.ram
%LOCALAPPDATA%\5DO\states\slot-00.tdos
```

On Linux, .NET maps `LocalApplicationData` to the XDG data directory, typically
`~/.local/share/5DO`. On macOS it is the .NET `LocalApplicationData` location for the
current user; use the **About** dialog or the path shown in **Settings > Paths** to see
the exact path rather than hard-coding one.

| File / directory | Purpose |
|---|---|
| `settings.json` | All persistent desktop settings, including the input profile. |
| `nvram.ram` | 32 KB 3DO NVRAM image, debounced and written atomically. |
| `nvram.ram.bad` | A corrupt NVRAM image moved aside on load; the app starts fresh. |
| `states/` | Default save-state directory (`slot-NN.tdos`, sidecar JSON, PNG thumbnail). |
| `settings.json.tmp` | Temporary file used during atomic settings writes. |

The screenshot directory defaults to the user's **Pictures** folder when
`ScreenshotDirectory` is empty. The save directory defaults to
`<LocalApplicationData>/5DO/states` when empty.

**Portable mode is not implemented.** A `portable.flag` file has no effect in the
current code, despite an older architecture note. Do not rely on it.

Settings are written atomically (temporary file + replace). A malformed
`settings.json` is ignored and defaults are used; the bad file is not renamed. Back up
or delete the file to reset the desktop configuration.

---

## 2. `settings.json` schema

The file is indented JSON with PascalCase property names and string enum values.
Unknown properties are ignored by `System.Text.Json`. Fresh settings use the optimized
defaults: OpenGL presentation, match-window output, sharp-bilinear filtering, vsync on,
integer scaling, and automatic presentation frame skipping. The following table lists
every serialized property in `AppSettings`.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `FirstRunCompleted` | bool | `false` | Whether the first-run guide has been shown. |
| `BiosPath` | string | `""` | Last BIOS path. |
| `DonationEmail` | string | `""` | Optional email used to recognize a 5DO donation. |
| `HasDonated` | bool | `false` | Cached confirmation for `DonationEmail`. |
| `DonationLastCheckedUtc` | string? | `null` | UTC timestamp of the last successful donation check. |
| `DiscPath` | string | `""` | Last disc path. |
| `RecentDiscs` | string[] | `[]` | Recent disc paths, newest first, capped at 10. |
| `SaveDirectory` | string | `""` | Save-state directory; empty means `<LocalApplicationData>/5DO/states`. |
| `ScreenshotDirectory` | string | `""` | Screenshot directory; empty means the user's Pictures folder. |
| `SaveSlot` | int | `0` | Selected zero-based slot. Clamped to the slot count on load. |
| `SaveSlotCount` | int | `10` | Number of slots. Clamped to 1..100 on load. |
| `LibraryFolders` | string[] | `[]` | Folders scanned by the game library. |
| `AutoGameFixes` | bool | `true` | Apply the 4DO per-game fix-mode mapping automatically. |
| `FixModeOverrides` | object | `{}` | Per-game fix-mode overrides, keyed by game ID. |
| `FilterOverrides` | object | `{}` | Per-game filter overrides, keyed by game ID. |
| `Filter` | enum string | `"SharpBilinear"` | Global presentation filter. |
| `FrameSkip` | enum string | `"Auto"` | Presentation frame-skip policy: `Off`, `Auto`, `OneInTwo`, `OneInThree`, `OneInFour`, or `OneInFive`. Only presentation is skipped; emulation and audio always run every frame. |
| `Aspect` | enum string | `"FourThree"` | Aspect policy. |
| `CustomAspect` | double | `1.3333` | Width/height ratio used when `Aspect` is `Custom`. |
| `Vsync` | bool | `true` | Request platform vsync (where supported). |
| `IntegerScale` | bool | `true` | Prefer integer scaling for 4:3/integer modes. |
| `Renderer` | enum string | `"OpenGl"` | Presentation backend: `OpenGl`, `Vulkan`, or `Software`. |
| `OutputResolution` | enum string | `"Window"` | Final output size: `Window`, `Native`, `Scale2X`, `Scale3X`, `Scale4X`, `Hd720`, `FullHd1080`, `Qhd1440`, or `Uhd2160`. |
| `ShowOverlay` | bool | `false` | Show the FPS/status overlay. |
| `SaveNativeScreenshot` | bool | `false` | Also write a `-native.png` CPU frame with each screenshot. |
| `Bloom` | float | `0.35` | CRT bloom strength, 0..1. |
| `FsrSharpness` | float | `0.8` | FSR 1.0 RCAS sharpness, 0 (soft) .. 1 (maximum). |
| `HighResolution` | bool | `false` | Opt-in 640x480 CEL/VDLP mode. |
| `ArmTiming` | enum string | `"FreeDoCompatible"` | ARM60 N-cycle class: `FreeDoCompatible` (N=4) or `Arm60Datasheet` (N=2, accuracy). |
| `CpuClockMultiplier` | double | `1.0` | ARM clock multiplier, 0.5..4.0 (6.25-50 MHz). 1.0 is the stock 12.5 MHz. |
| `RandomSeed` | uint? | `null` | Optional initial DSP PRNG seed; `null` uses FreeDO's `0xa5a5a5a5`. |
| `Volume` | float | `1.0` | OpenAL gain, 0..1. |
| `AudioEnabled` | bool | `true` | Enable live OpenAL output. |
| `Input` | object | defaults | Six-player keyboard/gamepad bindings plus hotkeys. |

The legacy `PreferOpenGl` boolean is still read when loading older files; `false` maps to
`Renderer: "Software"`. New files write `Renderer` instead.

### Enum values

`Filter` (string): `Accurate`, `Nearest`, `SharpBilinear`, `Crt`, `Xbr`, `Hqx`,
`Hqx3x`, `Hqx4x`, `Fsr`.

`FrameSkip` (string): `Off`, `Auto`, `OneInTwo`, `OneInThree`, `OneInFour`,
`OneInFive`. `Auto` skips a presentation frame only after three presented frames exceed
the audio-derived frame budget; it recovers after 120 good frames. The fixed modes
present one of every 2/3/4/5 emulated frames. In every mode the ARM CPU, CEL engine,
DSP, timers, and CD-ROM still execute all frames, so game speed, input, audio, and save
states are identical.

`Aspect` (string): `Integer`, `FourThree`, `Stretch`, `Custom`.

`Renderer` (string): `OpenGl`, `Vulkan`, `Software`.

`OutputResolution` (string): `Window`, `Native`, `Scale2X`, `Scale3X`, `Scale4X`,
`Hd720`, `FullHd1080`, `Qhd1440`, `Uhd2160`.

`FixModeOverrides` values (string flags): `None`, `BitTiming1`, `BitTiming2`,
`BitTiming3`, `BitTiming4`, `BitTiming5`, `BitTiming6`, `BitTiming7`,
`GraphicsStepY`. Flags may be comma-separated when combined. The Settings dialog edits
the raw hexadecimal mask instead.

`FilterOverrides` values use the same filter names as `Filter`.

### Per-game override keys

Game IDs are normalized to the first eight hexadecimal characters, uppercased. A full
32-character MD5 is accepted and truncated. Example:

```json
"FixModeOverrides": {
  "0511D3D2": "BitTiming6"
},
"FilterOverrides": {
  "0511D3D2": "Fsr"
}
```

The lookup is case-insensitive and tolerates a full MD5 key.

### Example (abridged)

The real file contains six player profiles; this example shows the shape and the
player-1 defaults. See section 3 for the full input structure.

```json
{
  "FirstRunCompleted": true,
  "BiosPath": "C:\\3DO\\panafz1.bin",
  "DonationEmail": "",
  "HasDonated": false,
  "DonationLastCheckedUtc": null,
  "DiscPath": "C:\\3DO\\Road Rash (USA).chd",
  "RecentDiscs": [
    "C:\\3DO\\Road Rash (USA).chd"
  ],
  "SaveDirectory": "C:\\Users\\me\\AppData\\Local\\5DO\\states",
  "ScreenshotDirectory": "",
  "SaveSlot": 0,
  "SaveSlotCount": 10,
  "LibraryFolders": [
    "C:\\3DO"
  ],
  "AutoGameFixes": true,
  "FixModeOverrides": {},
  "FilterOverrides": {},
  "Filter": "SharpBilinear",
  "Aspect": "FourThree",
  "CustomAspect": 1.3333,
  "Vsync": true,
  "IntegerScale": true,
  "Renderer": "OpenGl",
  "OutputResolution": "Window",
  "ShowOverlay": false,
  "SaveNativeScreenshot": false,
  "Bloom": 0.35,
  "FsrSharpness": 0.8,
  "HighResolution": false,
  "Volume": 1.0,
  "AudioEnabled": true,
  "Input": {
    "Players": [
      {
        "Name": "Player 1",
        "Keyboard": {
          "Up": "Up",
          "Down": "Down",
          "Left": "Left",
          "Right": "Right",
          "Z": "A",
          "X": "B",
          "C": "C",
          "A": "L",
          "S": "R",
          "Enter": "P",
          "Backspace": "X"
        },
        "Gamepad": {
          "South": "A",
          "West": "B",
          "North": "C",
          "LeftShoulder": "L",
          "RightShoulder": "R",
          "LeftTrigger": "L",
          "RightTrigger": "R",
          "DpadUp": "Up",
          "DpadDown": "Down",
          "DpadLeft": "Left",
          "DpadRight": "Right",
          "LeftStickUp": "Up",
          "LeftStickDown": "Down",
          "LeftStickLeft": "Left",
          "LeftStickRight": "Right",
          "Start": "P",
          "Select": "X"
        }
      }
    ],
    "Hotkeys": {
      "SaveState": "F5",
      "LoadState": "F8",
      "Screenshot": "F12",
      "Fullscreen": "F11",
      "Pause": "Escape",
      "Reset": "F10",
      "SlotPrevious": "F6",
      "SlotNext": "F7",
      "FrameAdvance": "F9"
    }
  }
}
```

In a real file `Players` has six entries. Players 2-6 have no keyboard bindings by
default but do have the default gamepad mapping.

---

## 3. Input profile

`AppSettings.Input` is an `InputProfile`:

- `Players`: array of six `PlayerProfile` objects.
- `Hotkeys`: action-name to key-name map.

Each `PlayerProfile`:

| Property | Type | Meaning |
|---|---|---|
| `Name` | string | Display name, e.g. `Player 1`. |
| `Keyboard` | object | Key name (Avalonia `Key.ToString()`) to `ControllerButton`. |
| `Gamepad` | object | Physical control name to `ControllerButton`. |

`ControllerButton` values: `None`, `A`, `B`, `C`, `P`, `X`, `L`, `R`, `Up`, `Down`,
`Left`, `Right`.

Gamepad binding names understood by `GamepadMapper`:

`South`, `East`, `West`, `North`, `LeftShoulder`, `RightShoulder`, `LeftTrigger`,
`RightTrigger`, `DpadUp`, `DpadDown`, `DpadLeft`, `DpadRight`, `LeftStick`,
`RightStick`, `LeftStickUp`, `LeftStickDown`, `LeftStickLeft`, `LeftStickRight`,
`RightStickUp`, `RightStickDown`, `RightStickLeft`, `RightStickRight`.

Default player-1 keyboard:

| 3DO | Key |
|---|---|
| Up/Down/Left/Right | Arrow keys |
| A | Z |
| B | X |
| C | C |
| L | A |
| R | S |
| P | Enter |
| X | Backspace |

Default gamepad mapping:

| Physical | 3DO |
|---|---|
| South (XInput A / evdev BTN_SOUTH) | A |
| West (XInput X / evdev BTN_WEST) | B |
| North (XInput Y / evdev BTN_NORTH) | C |
| LeftShoulder | L |
| RightShoulder | R |
| LeftTrigger | L (fallback) |
| RightTrigger | R (fallback) |
| D-pad directions | matching D-pad |
| Left stick directions | matching D-pad (fallback) |
| Start | P |
| Select | X |

Explicit stick/trigger bindings win over the fallback. The default radial dead zone is
0.5; the default trigger threshold is 0.5.

**Hotkeys:** the profile stores default F5 (save), F8 (load), F6/F7 (previous/next
slot), F9 (advance one frame while paused), F12 (screenshot), F11 (fullscreen),
Escape (pause), and F10 (reset). The shell dispatches these bindings before
forwarding a key to the emulated pad; F11 also works from the library, and Escape
leaves fullscreen before it reaches the pause action. `HotkeyRouter` resolves
the persisted action names case-insensitively, so a custom key assignment in the
profile is honored. Unknown action names and unbound keys are ignored.

The binding editor reports conflicts when one key/control is bound to multiple buttons
or one hotkey is bound to multiple actions.

`InputProfileStore` can load/save a standalone input JSON file, but the desktop shell
uses the embedded `AppSettings.Input` object. There is no separate `input.json` by
default.

---

## 4. Per-game fixes and filters

Resolution order in the desktop shell:

1. Explicit per-game `FixModeOverrides` entry.
2. If `AutoGameFixes` is true, `GameCompatibility.ForGameId(gameId)` (4DO database
   mapping).
3. `FixMode.None`.

For the CLI:

1. `--fix-mode 0xMASK`.
2. `--no-game-fixes`.
3. `GameCompatibility.ForGameId(gameId)`.
4. `FixMode.None`.

Filter resolution:

1. Per-game `FilterOverrides` entry.
2. Global `Filter`.

`GameId` is the lowercase MD5 of the first two 2048-byte sectors. The 4DO fix mapping
matches the first eight uppercase characters; see
[`Compatibility.md`](Compatibility.md) section 5 for the known titles.

The Settings dialog's **Emulation** page edits:

- `AutoGameFixes` checkbox.
- `HighResolution` checkbox (restarts the machine when changed).
- `ArmTiming` combo: FreeDO-compatible N=4 or ARM60 datasheet N=2 (restarts the machine when changed).
- `CpuClockMultiplier` field: 0.5..4.0 (restarts the machine when changed).
- `RandomSeed` field: blank for the FreeDO default, or a 32-bit hexadecimal value.
- Current game display and fix-mode override (`0x...` hex mask, blank = automatic).
- Per-game filter override (`Automatic` or an explicit filter).

Changing high-resolution mode or ARM timing recreates the machine and reopens the
current BIOS/disc; the state is not preserved. Save states record the ARM timing mode,
and loading a state written with another mode reports a warning instead of rejecting it.

---

## 5. NVRAM

`NvramStore` manages a 32 KB image at `<LocalApplicationData>/5DO/nvram.ram`.

- A missing file produces `NvramImage.CreateFresh()`, which initializes the volume
  header and initial directory words.
- The machine writes NVRAM snapshots through `MarkChanged`; the host flushes at most
  once per 250 ms (`NvramStore.FlushInterval`) and forces a flush on pause/stop/dispose.
- Writes use a temporary file plus atomic replace.
- If the file is not exactly 32 KB, it is moved to `nvram.ram.bad` and a fresh image is
  used. This preserves the bad image for diagnosis.
- A save state also contains NVRAM; loading a state restores the state's NVRAM and the
  host later persists it through the normal path.

The CLI uses `--nvram <path>` instead: load if the file exists, otherwise fresh; write
back after the run. With `--state-in`, NVRAM comes from the state unless `--nvram` is
also used for the post-run save.

---

## 6. Paths and library behavior

| Setting | Behavior |
|---|---|
| `BiosPath` | Last BIOS; used by **About** and optional startup auto-open. |
| `DiscPath` | Last disc; used by optional startup auto-open. |
| `RecentDiscs` | Newest first, deduplicated case-insensitively, capped at 10. |
| `SaveDirectory` | Default `.../5DO/states`; used by `SaveStateSlotManager`. |
| `ScreenshotDirectory` | Empty means Pictures; screenshots are named `<game>-<yyyyMMdd-HHmmss>-f<frame>.png`. |
| `CoverDirectory` | Optional shared folder for cover art named after the disc files. |
| `LibraryFolders` | Scanned recursively to depth 4 for `.iso`, `.bin`, `.cue`, `.chd`. |

Library entries are sorted by title then path. Cover art is matched by file name: a
sibling image with the same base name and an extension of `.png`, `.jpg`, `.jpeg`, or
`.webp` (for example `game.png` beside `game.chd`) takes precedence. If no sibling
exists, `CoverDirectory` is searched for `<disc base name>.<ext>`; the folder is set
under **Configuration > Settings > Paths > Game covers**. Unknown discs use the file
name as the title and show `Unknown`; known discs use the embedded 4DO database
metadata. Covers are decoded once per file at a bounded height and shared by path, so
duplicate discs in different library folders reuse the same bitmap.

Discs are listed immediately from the folder enumeration. Game IDs and sector counts are
then computed in the background with bounded parallelism, and the list updates as titles
resolve. Results are cached in `<LocalApplicationData>/5DO/library-cache.json`; later
scans reuse the cache for files whose length and last-write time are unchanged. The
status bar reports metadata-scan progress.

The **Library** view supports grid/list modes, search by title/path/game ID/format, and
an icon-size slider. Double-click or Enter boots the selected disc.

---

## 7. Screenshots and save slots

Screenshots:

- Default directory: Pictures.
- Name: `<sanitized game name>-<yyyyMMdd-HHmmss>-f<frames:D6>.png`.
- With `SaveNativeScreenshot`, a second
  `<name>-<stamp>-f<frames:D6>-native.png` is written from the CPU assembler.
- The filtered screenshot comes from the OpenGL readback when available; otherwise the
  CPU frame is used.

Save slots:

- `SaveSlotCount` defaults to 10 and is clamped to 1..100 on load.
- Files: `slot-NN.tdos`, `slot-NN.json`, `slot-NN.png` (zero-based `NN`).
- The state header is authoritative for game ID and frame count; the sidecar is
  metadata only and may be missing.
- Writes are atomic; a failed load does not change the current slot index.
- See [`SaveStates.md`](SaveStates.md) for the format.

---

## 8. Audio settings

| Setting | Effect |
|---|---|
| `AudioEnabled` | Opens OpenAL Soft at startup. If no device is present, the app logs a warning and runs silently. |
| `Volume` | OpenAL gain, clamped 0..1. |
| `Bloom` | CRT bloom only; 0 disables. |
| `FsrSharpness` | FSR RCAS sharpness; 0 soft, 1 maximum. |

The Settings > Audio page also has a **Play 440 Hz test tone** button. It opens a
separate short-lived OpenAL output, plays one second, and reports the OpenAL error if no
device is available.

See [`Audio.md`](Audio.md) for the full pipeline and diagnostics.

---

## 9. Environment variables

| Variable | Used by | Meaning |
|---|---|---|
| `FIVEDO_AUDIO_TAP` | OpenAL output (CLI `--audio openal`/desktop) | Path prefix for `<prefix>.wav` (exact queued PCM) and `<prefix>.csv` (per-buffer timing). |
| `ALSOFT_DRIVERS` | OpenAL Soft | Set to `null` to use OpenAL's null backend for headless tests. |
| `FIVEDO_BIOS` | Tests | Path to a 1 MB BIOS for real-BIOS tests. |
| `FIVEDO_FRAME_DUMP` | Tests | Directory for boot-frame PNGs. |
| `FIVEDO_GPU_TESTS` | Tests | Set to `1` to run the real WGL/ANGLE probe test. |
| `FIVEDO_AUDIO_TESTS` | Tests | Set to `1` to open a real OpenAL device. |
| `FIVEDO_VULKAN_TESTS` | Tests | Set to `1` to run the opt-in Vulkan renderer and window-probe integration tests. |
| `FIVEDO_BUG_REPORTS` | Desktop/CLI | Set to `0` to disable bug-report submissions. Automated smoke/probe runs disable them automatically. |
| `FIVEDO_GAMES` | `tools/release-soak.ps1` | Disc corpus root. |

The CLI does not read any of these except `FIVEDO_AUDIO_TAP` (through the OpenAL
backend) and `ALSOFT_DRIVERS` (through OpenAL Soft).

---

## 10. Precedence summary

```text
frontend launch arguments (--disc, --bios, --fullscreen, --gl-backend)
  > CLI flags
  > per-game fix/filter overrides (desktop)
  > explicit --fix-mode / --no-game-fixes (CLI)
  > automatic 4DO database fixes
  > global settings
  > built-in defaults
```

For the desktop shell, changes made in **Settings** are applied when the dialog is
accepted; `settings.json` is written immediately. Changes to high-resolution mode
restart the machine. Changes to audio settings recreate the OpenAL output if needed.
Changing `Renderer` recreates the active presenter without restarting emulation.
Changes to `OutputResolution`, filters, aspect, and vsync apply to the current renderer
without restarting the machine. Frame-skip changes apply immediately to the running core
loop.

### Performance defaults

Fresh settings target 60 fps without lowering image quality:

- **OpenGL** presentation at **match-window** output resolution. The emulated frame stays
  at its native resolution and the GPU performs the final filtered upscale.
- **Sharp-bilinear** filtering (smooth at non-integer scale factors with far less CPU/GPU
  cost than xBR/HQx).
- **VSync** requested and **integer scaling** enabled.
- **Auto** frame skip. This is presentation-only and stays off when the core meets its
  deadline; if the host cannot present every frame, it drops presentation frames instead
  of slowing the game down.
- High-resolution CEL mode disabled. Use it only for titles validated at 640x480.

If the machine is CPU-bound rather than presentation-bound, choose a faster filter
(`Nearest`), reduce `OutputResolution`, or run a Release build. Auto frame skip cannot make
the emulated CPU, CEL, or DSP faster; it only removes presentation work from the frame
budget.

---

## 11. Resetting configuration

To reset the desktop app to defaults:

1. Close the app.
2. Back up and delete `settings.json` (and optionally `nvram.ram` and `states/`).
3. Start the app; it creates a fresh settings file and NVRAM image on first save.

Deleting `nvram.ram` resets the emulated console's saved settings/volume, not the
game's save data on the disc. Save states are independent files; deleting
`settings.json` does not delete them.

If the app fails to start because of settings, rename `settings.json` instead of
editing it; malformed JSON is silently replaced with defaults on the next load.
