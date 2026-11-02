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
| `nvram.ram` | Shared 32 KB 3DO NVRAM image (global version 0), written atomically. |
| `nvram.<version>.ram` | Global version 1-9 image. |
| `nvram/per-game/<gameId>.<version>.ram` | Per-game NVRAM image. |
| `nvram*.bad` | A corrupt NVRAM image moved aside on load; the app starts fresh. |
| `states/` | Default save-state directory (`slot-NN.tdos`, sidecar JSON, PNG thumbnail). |
| `settings.json.tmp` | Temporary file used during atomic settings writes. |

The screenshot directory defaults to the user's **Pictures** folder when
`ScreenshotDirectory` is empty. The save directory defaults to
`<data-directory>/states` when empty.

**Portable mode.** Place a `portable.flag` file next to the executable (or pass
`--portable`) to keep `settings.json`, NVRAM, `states/`, cheats, library state, and logs
beside the executable. `FIVEDO_DATA_DIR` overrides the data directory and is useful for
separate instances. Portable mode never writes to `%LOCALAPPDATA%`; the executable
directory must be writable.

**Named profiles.** `--config-profile <name>` (or `FIVEDO_PROFILE`) selects
`<data-directory>/profiles/<name>/` for settings, NVRAM, save states, cheats, library
state, and logs. Profiles are created on first launch and isolated from each other.
`5do settings profiles` lists existing profile directories; `FIVEDO_DATA_DIR` takes
precedence over a named profile. Settings > Paths selects a profile and writes
`<base>/default-profile.txt` for the next launch, creates a profile from the current
settings, opens the profiles folder, and imports/exports settings JSON. The desktop
acquires `<data>/5do.instance.lock` at startup; a second process using the same directory
shows the owner and exits. `FIVEDO_ALLOW_MULTIPLE=1` disables that guard. Different
profiles or data directories use different lock files and may run together.

Settings are written atomically (temporary file + replace) and carry
`"SettingsVersion": 2`. An unversioned or v1 file is migrated in place on the next load.
A malformed `settings.json` is copied to `settings.json.invalid-<timestamp>.bak`
before defaults are used, so the original bytes are never silently lost. Back up or
delete the file to reset the desktop configuration.

---

## 2. `settings.json` schema

The file is indented JSON with PascalCase property names and string enum values.
Unknown properties are ignored by `System.Text.Json`. Fresh settings use the optimized
defaults: OpenGL presentation, match-window output, sharp-bilinear filtering, vsync on,
integer scaling, and automatic presentation frame skipping. The following table lists
every serialized property in `AppSettings`.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `SettingsVersion` | int | `2` | Settings schema version. Unversioned files are migrated on load. |
| `FirstRunCompleted` | bool | `false` | Whether the first-run guide has been shown. |
| `BiosPath` | string | `""` | Last primary BIOS path; fallback when auto-selection is off or unavailable. |
| `Bios2Path` | string | `""` | Optional BIOS2/Kanji font path. |
| `BiosFolder` | string | `""` | Folder scanned for automatic region/BIOS selection. |
| `AutoBios` | bool | `true` | Select a region-appropriate BIOS when `BiosFolder` is set. |
| `BiosOverrides` | object | `{}` | Per-game BIOS MD5 overrides keyed by 4DO game ID. |
| `DonationEmail` | string | `""` | Optional email used to recognize a 5DO donation. |
| `HasDonated` | bool | `false` | Cached confirmation for `DonationEmail`. |
| `DonationLastCheckedUtc` | string? | `null` | UTC timestamp of the last successful donation check. |
| `DiscPath` | string | `""` | Last disc path. |
| `RecentDiscs` | string[] | `[]` | Recent disc paths, newest first, capped at 10. |
| `SaveDirectory` | string | `""` | Save-state directory; empty means `<LocalApplicationData>/5DO/states`. |
| `ScreenshotDirectory` | string | `""` | Screenshot directory; empty means the user's Pictures folder. |
| `CheatsDirectory` | string | `""` | Per-game cheat JSON directory; empty means `<LocalApplicationData>/5DO/cheats`. |
| `SaveSlot` | int | `0` | Selected zero-based slot. Clamped to the slot count on load. |
| `SaveSlotCount` | int | `10` | Number of slots. Clamped to 1..100 on load. |
| `LibraryFolders` | string[] | `[]` | Folders scanned by the game library. |
| `LibraryMetadataPath` | string | `""` | Optional offline metadata JSON; empty means `<LocalApplicationData>/5DO/library-metadata.json`. |
| `AutoGameFixes` | bool | `true` | Apply the 4DO per-game fix-mode mapping automatically. |
| `FixModeOverrides` | object | `{}` | Per-game fix-mode overrides, keyed by game ID. |
| `FilterOverrides` | object | `{}` | Per-game filter overrides, keyed by game ID. |
| `GameProfiles` | object | `{}` | Per-game timing and CD-DA profile snapshots, keyed by game ID. Each entry holds `Profile`, `Region`, `ArmTiming`, `ClioTimers`, `CelTiming`, `CdromTiming`, `CdSpeed`, `CpuClockMultiplier`, `CdAudioVolume`, and `CdAudioMuted`. |
| `Filter` | enum string | `"SharpBilinear"` | Global presentation filter. |
| `FrameSkip` | enum string | `"Auto"` | Presentation frame-skip policy: `Off`, `Auto`, `OneInTwo`, `OneInThree`, `OneInFour`, or `OneInFive`. Only presentation is skipped; emulation and audio always run every frame. |
| `Aspect` | enum string | `"FourThree"` | Aspect policy. |
| `CustomAspect` | double | `1.3333` | Width/height ratio used when `Aspect` is `Custom`. |
| `Vsync` | bool | `true` | Request platform vsync (where supported). |
| `IntegerScale` | bool | `true` | Prefer integer scaling for 4:3/integer modes. |
| `OverscanPixels` | int | `0` | Crop this many pixels from every edge before presentation and native screenshots (0-32). Presentation-only; frame hashes, input, and save states are unchanged. |
| `Renderer` | enum string | `"OpenGl"` | Presentation backend: `OpenGl`, `Vulkan`, or `Software`. |
| `OutputResolution` | enum string | `"Window"` | Final output size: `Window`, `Native`, `Scale2X`, `Scale3X`, `Scale4X`, `Hd720`, `FullHd1080`, `Qhd1440`, or `Uhd2160`. |
| `ShowOverlay` | bool | `false` | Show the FPS/status overlay. |
| `ShowInputDisplay` | bool | `false` | Show the frame/skip/lag counters and controller buttons over the game. |
| `InputDisplayPlayer` | int | `1` | Controller port (1-6) shown by the input display. |
| `OsdEnabled` | bool | `true` | Show transient on-screen notifications. |
| `OsdPosition` | enum string | `"TopRight"` | Notification anchor: `TopLeft`, `TopCenter`, `TopRight`, `BottomLeft`, `BottomCenter`, or `BottomRight`. |
| `OsdOpacity` | double | `0.9` | Notification opacity, 0.1..1. |
| `OsdDurationMilliseconds` | int | `2200` | Notification lifetime, 500..10000 ms. |
| `QuickMenuEnabled` | bool | `true` | Enable the F4/Select+Start in-game quick menu. |
| `SaveNativeScreenshot` | bool | `false` | Also write a `-native.png` CPU frame with each screenshot. |
| `Bloom` | float | `0.35` | CRT bloom strength, 0..1. |
| `FsrSharpness` | float | `0.8` | FSR 1.0 RCAS sharpness, 0 (soft) .. 1 (maximum). |
| `HighResolution` | bool | `false` | Opt-in 640x480 CEL/VDLP mode. |
| `Profile` | enum string | `"Compatible"` | Last selected timing profile: `Compatible`, `Accuracy`, `Performance`, or `Custom`. The individual timing fields remain authoritative. |
| `Region` | enum string | `"Ntsc"` | Video region used when `AutoRegion` is off: `Ntsc` (320x240, 60 Hz), `Pal1` (320x288, 50 Hz), or `Pal2` (384x288, 50 Hz). |
| `AutoRegion` | bool | `true` | Follow the resolved BIOS video standard: PAL BIOS selects PAL1, NTSC selects NTSC. Disable to force `Region`. |
| `ArmTiming` | enum string | `"FreeDoCompatible"` | ARM60 N-cycle class: `FreeDoCompatible` (N=4) or `Arm60Datasheet` (N=2, accuracy). |
| `ClioTimers` | enum string | `"FreeDoCompatible"` | CLIO timer scan: `FreeDoCompatible` (all sixteen timers per event) or `OperaScheduler` (one slot at a time with an explicit cascade carry). |
| `CelTiming` | enum string | `"FreeDoCompatible"` | MADAM CEL timing: `FreeDoCompatible` (synchronous draw) or `OperaBusy` (deferred draw with a pixel/texel busy deadline). |
| `CdromTiming` | enum string | `"FreeDoCompatible"` | CD-ROM data timing: `FreeDoCompatible` (immediate transfers) or `OperaCycleBased` (cycle-based sector readiness with delayed status/FIQ). |
| `CdSpeed` | enum string | `"X2"` | Effective read speed for `OperaCycleBased`: `Unbounded`, `X1`, `X2` (stock), `X4`, `X8`, `X16`, `X24`, or `X32`. Ignored by `FreeDoCompatible`. |
| `CpuClockMultiplier` | double | `1.0` | ARM clock multiplier, 0.5..4.0 (6.25-50 MHz). 1.0 is the stock 12.5 MHz. |
| `RandomSeed` | uint? | `null` | Optional initial DSP PRNG seed; `null` uses FreeDO's `0xa5a5a5a5`. |
| `NvramStorageMode` | enum string | `"Global"` | `Global` keeps one shared image; `PerGame` keeps a separate image per 4DO game ID. |
| `NvramVersion` | int | `0` | Independent NVRAM version slot, 0-9. |
| `NvramDirectory` | string | `""` | Directory that owns the NVRAM layout; empty means the resolved data directory (portable-aware). |
| `Volume` | float | `1.0` | OpenAL gain, 0..1. |
| `AudioEnabled` | bool | `true` | Enable live OpenAL output. |
| `CdAudioVolume` | float | `0.8` | CD-DA mix level, 0..1. Applies only while PLAY_AUDIO is active. |
| `CdAudioMuted` | bool | `false` | Keep CD-DA playback running but remove it from the mixed output. |
| `AudioDevice` | string | `""` | OpenAL playback device name; empty uses the system default. |
| `AudioBufferMilliseconds` | int | `100` | Target OpenAL buffer latency, 20-500 ms. |
| `PointerPort` | int | `0` | `0` sends host pointer input to every pointer device; `1`-`6` routes it to that controller port. |
| `PointerScale` | float | `1.0` | Pointer calibration scale (0.5-2.0). |
| `PointerOffsetX`, `PointerOffsetY` | int | `0` | Pointer calibration offset in native pixels. |
| `AutoSwapOnEject` | bool | `true` | Advance to the next playlist entry when the guest ejects the disc (ODE-style). Without a playlist, **F3** opens a disc picker. |
| `RewindEnabled` | bool | `true` | Record compressed keyframes and input for rewind. |
| `RewindSeconds` | int | `60` | Rewind history length in seconds, 30..300. |
| `FastForwardMultiplier` | int | `4` | Fast-forward speed, 2..10. Audio is muted while active. |
| `InputDevices` | string[] | six `"Joypad"` entries | PBUS device per port: `Joypad`, `Flightstick`, `Mouse`, `Lightgun`, `ArcadeLightgun`, `OrbatakTrackball`, or `None`. |
| `InputDeviceOverrides` | object | `{}` | Per-game six-device lists keyed by game ID. An override wins over `InputDevices`. |
| `AutoInputDevices` | bool | `true` | Seed a lightgun or Orbatak trackball override for known titles when the game has no override. |
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

`Profile` (string): `Compatible`, `Accuracy`, `Performance`, `Custom`. A profile is a
shortcut for the individual timing fields; it never changes fixes, high-resolution mode,
or the random seed. If the fields no longer match the selected profile, the dialog saves
`Custom`.

`Region` (string): `Ntsc`, `Pal1`, `Pal2`. NTSC runs 60 fields per second with 240
visible lines. PAL1/PAL2 run 50 fields per second with 288 visible lines; PAL2 is 384
pixels wide. PAL requires a European BIOS and changes the machine timing, so restart the
machine after changing it.

`ClioTimers` (string): `FreeDoCompatible`, `OperaScheduler`.

`CelTiming` (string): `FreeDoCompatible`, `OperaBusy`.

`CdromTiming` (string): `FreeDoCompatible`, `OperaCycleBased`.

`CdSpeed` (string): `Unbounded`, `X1`, `X2`, `X4`, `X8`, `X16`, `X24`, `X32`.

`NvramStorageMode` (string): `Global`, `PerGame`.

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
  "SettingsVersion": 2,
  "FirstRunCompleted": true,
  "BiosPath": "C:\\3DO\\panafz1.bin",
  "Bios2Path": "",
  "BiosFolder": "C:\\3DO\\bios",
  "AutoBios": true,
  "BiosOverrides": {
    "6B1A7FED": "51F2F43AE2F3508A14D9F56597E2D3CE"
  },
  "DonationEmail": "",
  "HasDonated": false,
  "DonationLastCheckedUtc": null,
  "DiscPath": "C:\\3DO\\Road Rash (USA).chd",
  "RecentDiscs": [
    "C:\\3DO\\Road Rash (USA).chd"
  ],
  "SaveDirectory": "C:\\Users\\me\\AppData\\Local\\5DO\\states",
  "ScreenshotDirectory": "",
  "CheatsDirectory": "",
  "SaveSlot": 0,
  "SaveSlotCount": 10,
  "LibraryFolders": [
    "C:\\3DO"
  ],
  "LibraryMetadataPath": "",
  "AutoGameFixes": true,
  "FixModeOverrides": {},
  "FilterOverrides": {},
  "GameProfiles": {},
  "Filter": "SharpBilinear",
  "Aspect": "FourThree",
  "CustomAspect": 1.3333,
  "Vsync": true,
  "IntegerScale": true,
  "OverscanPixels": 0,
  "Renderer": "OpenGl",
  "OutputResolution": "Window",
  "ShowOverlay": false,
  "ShowInputDisplay": false,
  "OsdEnabled": true,
  "OsdPosition": "TopRight",
  "OsdOpacity": 0.9,
  "OsdDurationMilliseconds": 2200,
  "QuickMenuEnabled": true,
  "SaveNativeScreenshot": false,
  "Bloom": 0.35,
  "FsrSharpness": 0.8,
  "HighResolution": false,
  "Region": "Ntsc",
  "AutoRegion": true,
  "Volume": 1.0,
  "AudioEnabled": true,
  "AudioDevice": "",
  "AudioBufferMilliseconds": 100,
  "InputDeviceOverrides": {},
  "AutoInputDevices": true,
  "CdAudioVolume": 0.8,
  "CdAudioMuted": false,
  "AutoSwapOnEject": true,
  "RewindEnabled": true,
  "RewindSeconds": 60,
  "FastForwardMultiplier": 4,
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
      "FrameAdvance": "F9",
      "Rewind": "R",
      "FastForward": "Tab"
    }
  }
}
```

In a real file `Players` has six entries. Players 2-6 have no keyboard bindings by
default but do have the default gamepad mapping.

### BIOS selection

`BiosFolder` is the source of automatic selection. `AutoBios` defaults to `true`, but
selection only runs when `BiosFolder` is set; an existing `BiosPath`-only configuration
continues to use that exact BIOS. Selection order is:

1. `BiosOverrides[gameId]` (MD5), if present and found in the folder.
2. A known title requirement (Slayer Europe/USA, Powers Kingdom Europe, Arcade CD-ROM).
3. Region matching from the game database (`US`, `JP`, `EU`, and combined strings),
   including Kanji and patched-image preferences.
4. `BiosPath`/`Bios2Path` as the configured fallback.

`bios list`/`bios scan` and the Settings > Paths scan button validate the folder.
Per-game overrides can be assigned in Settings > Emulation > BIOS override, with
**File > Assign BIOS to Current Game...**, or in **File > BIOS / NVRAM Manager...**.
The manager also imports validated images and sets BIOS1/BIOS2. Save states already
warn when the loaded BIOS hash differs from the state header.

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
slot), F9 (advance one frame while paused), Shift+F9 (rewind one frame), R (rewind one
second), Tab (hold to fast-forward), F4 (quick menu), F12 (screenshot), F11 (fullscreen),
Escape (pause), and F10 (reset). The shell dispatches these bindings before forwarding a
key to the emulated pad; F11 also works from the library, and Escape leaves fullscreen
before it reaches the pause action. `HotkeyRouter` resolves the persisted action names
case-insensitively, so a custom key assignment in the profile is honored. Unknown action
names and unbound keys are ignored.

The binding editor reports conflicts when one key/control is bound to multiple buttons
or one hotkey is bound to multiple actions.

`InputProfileStore` can load/save a standalone input JSON file, but the desktop shell
uses the embedded `AppSettings.Input` object. There is no separate `input.json` by
default.

### 3.1 PBUS devices

`AppSettings.InputDevices` stores one device name per PBUS port. Missing, blank, or
unknown entries fall back to `Joypad`, so older settings files keep working. The
desktop shell applies the list through `AppSettings.ApplyInputDevices`; the connected
port count only grows to the highest non-joypad port, preserving the historical
single-pad packet for default configurations.

Selecting a peripheral switches the packet to the Opera daisy-chain layout. The
legacy 16-byte 4DO layout remains active while every port is a joypad. The CLI mirrors
the setting with `--input-device 1=mouse,3=lightgun` and can force either layout with
`--pbus-format legacy|opera`.

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

`GameProfiles` stores a per-game timing/CD-DA snapshot keyed by game ID. `GetGameProfile`
and `SetGameProfile` normalize the key case-insensitively; `ApplyGameProfile(gameId)`
copies the snapshot into the live settings, and `CreateEffectiveForGame(gameId)` returns
a copy for machine creation without mutating live settings. Settings > Emulation edits,
saves, deletes, imports, and exports the current game's profile; `run/trace
--game-profile` applies one headlessly. `RegionOverrides` pins a disc to
NTSC/PAL1/PAL2 and wins over the automatic/global region choice. `settings validate [path]` reports syntax and schema version;
`settings migrate [path]` normalizes a file and rewrites it at the current version.

The Settings dialog's **Emulation** page edits:

- `AutoGameFixes` checkbox.
- `HighResolution` checkbox (restarts the machine when changed).
- `Region` combo: NTSC, PAL1, or PAL2. PAL runs at 50 Hz with 288 visible lines and
  needs a PAL BIOS. **Choose PAL/NTSC automatically from the BIOS** is enabled by
  default and switches the running machine between frames when a newly resolved BIOS
  changes the video standard. **Region override** pins the current game to NTSC, PAL1,
  or PAL2 regardless of the automatic choice.
- **Pointer port** and **Pointer calibration**: choose all pointer devices or a single
  controller port, then scale and offset the host pointer in native pixels.
- **Output device** and **Buffer latency**: select an OpenAL device and a 20-500 ms
  target; changes recreate only the host audio output.
- `Profile` combo: Compatible, Accuracy, Performance, or Custom. Selecting a profile
  fills the timing controls; manually changing one of them saves `Custom`.
- `ArmTiming` combo: FreeDO-compatible N=4 or ARM60 datasheet N=2 (restarts the machine when changed).
- `ClioTimers` combo: FreeDO-compatible all-timer scan or Opera per-slot scheduler (restarts the machine when changed).
- `CelTiming` combo: FreeDO-compatible synchronous draw or Opera pixel/texel busy timing (restarts the machine when changed).
- `CdromTiming` combo: FreeDO-compatible immediate transfers or Opera cycle-based sector readiness (restarts the machine when changed).
- `CdSpeed` combo: unbounded or 1x-32x for the cycle-based model; 2x is the stock drive speed (restarts the machine when changed).
- `CpuClockMultiplier` field: 0.5..4.0 (restarts the machine when changed).
- `RandomSeed` field: blank for the FreeDO default, or a 32-bit hexadecimal value.
- Current game display and fix-mode override (`0x...` hex mask, blank = automatic).
- Per-game filter override (`Automatic` or an explicit filter).

Changing high-resolution mode, ARM timing, the CLIO timer mode, the CEL timing mode, the
CD-ROM timing mode, or the CD-ROM speed recreates the machine and reopens the current
BIOS/disc; the state is not preserved. Save states record all four timing modes and the CD
speed, and loading a state written with another value reports a warning instead of
rejecting it.

---

## 5. NVRAM

`NvramStore` manages a 32 KB image. The default is the historical shared image at
`<LocalApplicationData>/5DO/nvram.ram`; `NvramStorageMode` and `NvramVersion` select the
layout:

| Mode | Version | Path |
|---|---:|---|
| `Global` | 0 | `<root>/nvram.ram` (legacy) |
| `Global` | 1-9 | `<root>/nvram.<version>.ram` |
| `PerGame` | 0-9 | `<root>/nvram/per-game/<gameId>.<version>.ram` |

`<root>` is `NvramDirectory`, or the settings directory when empty. Per-game IDs are
the lowercase 4DO game ID (the MD5 of the first two sectors); unsafe characters are
replaced with `_`. Versions are independent images, so switching versions never
overwrites another version's data.

- A missing file produces `NvramImage.CreateFresh()`, which initializes the volume
  header and initial directory words.
- When a per-game image does not exist yet, loading falls back to the per-game version 0
  image, then the shared version, then the legacy `nvram.ram`. The next save writes the
  per-game file, so switching from shared to per-game preserves the existing data once.
- The machine writes NVRAM snapshots through `MarkChanged`; the host flushes at most
  once per 250 ms (`NvramStore.FlushInterval`) and forces a flush on pause/stop/dispose.
- Writes use a temporary file plus atomic replace.
- If the primary file is not exactly 32 KB, it is moved to `<name>.bad` and a fresh image
  is used. A corrupt primary image does not silently fall back to an older image.
- A save state also contains NVRAM; loading a state restores the state's NVRAM and the
  host later persists it through the normal path.

The CLI supports both explicit and layout-based NVRAM:

- `--nvram <path>` loads that file if it exists and writes it back after the run.
- `--nvram-mode global|per-game`, `--nvram-version 0-9`, and `--nvram-dir <path>`
  resolve the same layout as the desktop settings. These options cannot be combined
  with `--nvram`.
- `nvram path|list|export|import|reset` resolves, lists, backs up, restores, or clears
  images without running the machine.

**File > BIOS / NVRAM Manager...** lists the shared and per-game images with validation
state and game metadata, and adds per-image backup, import, reset, delete, and a
layout-preserving **Backup All...** action. Reset, delete, and import-overwrite require
a second click on the same button.

With `--state-in`, NVRAM comes from the state; a layout or explicit NVRAM path is still
written after the run when supplied.

---

## 6. Paths and library behavior

| Setting | Behavior |
|---|---|
| `BiosPath` | Primary BIOS fallback; used by **About** and optional startup auto-open. |
| `Bios2Path` | Optional BIOS2/Kanji font loaded with the primary BIOS. |
| `BiosFolder` | Folder scanned for automatic selection; required for auto BIOS. |
| `AutoBios` | Enables region/known-title selection when `BiosFolder` is set. |
| `BiosOverrides` | Per-game MD5 pins, assigned in Settings or **File > Assign BIOS to Current Game...**. |
| `DiscPath` | Last disc; used by optional startup auto-open. |
| `RecentDiscs` | Newest first, deduplicated case-insensitively, capped at 10. |
| `SaveDirectory` | Default `.../5DO/states`; used by `SaveStateSlotManager`. |
| `ScreenshotDirectory` | Empty means Pictures; screenshots are named `<game>-<yyyyMMdd-HHmmss>-f<frame>.png`. |
| `CoverDirectory` | Optional shared folder for cover art named after the disc files. |
| `CheatsDirectory` | Per-game cheat JSON folder; empty means `.../5DO/cheats`. |
| `LibraryFolders` | Scanned recursively to depth 4 for `.iso`, `.bin`, `.cue`, `.chd`. |
| `LibraryMetadataPath` | Optional offline metadata overlay; empty means `.../5DO/library-metadata.json`. |

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

The **Library** view supports grid/list modes, search by title/path/game ID/format/
metadata, an icon-size slider, All/Favorites/Played/Unplayed filters, and
Title/Recently played/Most played/Release year/Publisher sorts. Double-click or Enter
boots the selected disc. Star buttons toggle favorites.

User library data is stored in `<LocalApplicationData>/5DO/library-state.json`:

```json
{
  "Version": 1,
  "Games": {
    "id:F3AF1B13": {
      "Favorite": true,
      "PlaytimeSeconds": 4500,
      "LastPlayedUtc": "2026-11-01T15:00:00Z",
      "LaunchCount": 3
    }
  }
}
```

Records are keyed by `id:<4DO game ID>` when known and `path:<full path>` for unknown
discs. A path-keyed record migrates to the game-ID key when the disc becomes known.
Play time is measured only while emulation is running and is flushed every 60 seconds
and on pause/stop/close.

The optional `<LocalApplicationData>/5DO/library-metadata.json` overlay (or the path in
`LibraryMetadataPath`) adds facts without a network provider:

```json
{
  "Version": 1,
  "Records": [
    {
      "GameId": "F3AF1B13",
      "Path": "",
      "Title": "Example Title",
      "Developer": "Example Developer",
      "Publisher": "Example Publisher",
      "ReleaseYear": "1995",
      "Genre": "Action",
      "Regions": "USA",
      "Players": 2,
      "Description": "Optional description."
    }
  ]
}
```

`GameId` and `Path` are both optional; the game ID is preferred when both match.
Non-empty overlay fields override the 4DO database and file name. The file is read on
library refresh and works fully offline.

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
| `CdAudioVolume` | CD-DA mix level, clamped 0..1. |
| `CdAudioMuted` | Removes CD-DA from the mix while playback continues. |
| `AutoSwapOnEject` | ODE-style automatic advance to the next playlist disc after a guest eject. |
| `Bloom` | CRT bloom only; 0 disables. |
| `FsrSharpness` | FSR RCAS sharpness; 0 soft, 1 maximum. |

The CLI matches the CD-DA controls with `run --cd-volume 0..1` and `--mute-cd`. The CD
save-state section records both values; a v3 state loads with the defaults.

The Settings > Audio page also has a **Play 440 Hz test tone** button. It opens a
separate short-lived OpenAL output, plays one second, and reports the OpenAL error if no
device is available.

See [`Audio.md`](Audio.md) for the full pipeline and diagnostics.

---

## 9. Environment variables

| Variable | Used by | Meaning |
|---|---|---|
| `FIVEDO_AUDIO_TAP` | OpenAL output (CLI `--audio openal`/desktop) | Path prefix for `<prefix>.wav` (exact queued PCM) and `<prefix>.csv` (per-buffer timing). |
| `FIVEDO_DATA_DIR` | Desktop/CLI | Override the data directory for settings, NVRAM, cheats, library state, and logs. Takes precedence over named profiles. |
| `FIVEDO_PROFILE` | Desktop/CLI | Select `<data-directory>/profiles/<name>/` as an isolated named configuration. |
| `FIVEDO_ALLOW_MULTIPLE` | Desktop | Set to `1` to skip the per-instance write lock (automation and test harnesses). |
| `FIVEDO_PORTABLE` | Desktop/CLI | Set to `1` to behave as if `portable.flag` is next to the executable. |
| `FIVEDO_LOG_DIR` | Desktop/CLI | Override the rolling log directory. |
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
  > per-game BIOS override (MD5)
  > known title BIOS requirement
  > region-based BIOS selection (when BiosFolder is set)
  > configured BiosPath/Bios2Path fallback
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
loop. OSD enable/position/opacity/duration and the quick-menu toggle apply immediately;
notification lifetime changes affect new messages.

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
