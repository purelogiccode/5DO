# Changelog

All notable changes are recorded here. The project is pre-1.0; formats and behavior may
still change.

## [Unreleased] - 2026-10-26

### Added

- Lemmings Fun 4 passcode `HNLHCIOECW` is now typed entirely with the D-pad/A
  (`docs/scripts/lem-fun4-dpad.txt`). The game shows the Fun 4 briefing at 22,600
  and starts gameplay at 23,000 (`PC 0x00083FD8`, hash `0xB7846C2D`); the state
  roundtrips (`0xEDEB1B9D` twice).
- CRT bloom in the OpenGL presenter: a 3x3 native-resolution glow around bright
  areas, controlled by `RendererSettings.Bloom`/`BloomThreshold` and a Settings
  slider.
- HQx 2x filter (`VideoFilter.Hqx`): a port of Maxim Stepin/Cameron Zemek's hq2x
  running on the CPU-expanded frame, with a C++ reference hash test.
- HQx 3x/4x filters (`VideoFilter.Hqx3x`/`Hqx4x`): generated from the 4DO hq3x/hq4x
  sources and verified against the C++ reference hashes. They are opt-in and cost
  more CPU time than HQx 2x.
- Benchmark budget gate: `ThreeDo.Benchmarks --gate` and the CI job enforce
  0 bytes/frame, core p99 <= 8 ms, total mean <= 8 ms, and total p99 <= 16.667 ms.
- Optional native CPU-frame screenshot (`-native.png`) alongside the filtered GL
  screenshot, controlled from Settings > Video.
- Game-library cover art: a sibling image with the same base name as a disc
  (`game.png`/`.jpg`/`.jpeg`/`.webp` beside `game.iso`) is previewed in the
  Library window.
- Per-game filter overrides in `AppSettings.FilterOverrides`, edited on the Settings
  dialog's Emulation page and applied when a disc opens.
- Shader-source and settings tests for the CRT bloom uniforms.
- Trim-safe JSON: `AppSettings`, `InputProfile`, save-slot metadata, and every CLI
  `--json` response now use source-generated `JsonSerializerContext` metadata.
  The CLI result types are named records (`CliJsonModels.cs`) and no longer use
  anonymous-type reflection.
- Trimmed CLI publishes: `ThreeDo.Cli` uses `PublishTrimmed=true` (`TrimMode=full`)
  for self-contained single-file builds, with a Windows CI smoke test. The win-x64
  audit covers `--version`, `selftest`, `bench`, `info`, `state inspect`,
  `state slots`, `fs list`, CHD boot/run, and a save-state roundtrip. See
  [`docs/Trimming.md`](docs/Trimming.md).
- `DiagnosticMethodInfo`-based RAM-write watchpoint descriptions (replaces
  `StackFrame.GetMethod`, which is trim-unsafe).
- `BenchmarkComparison` and benchmark `--baseline`/`--baseline-threshold` options:
  a same-machine relative check across core p99, total mean, and total p99 (default
  10%, exit code 4). Improvements pass; missing baseline metrics are ignored.
- Portable release archives: `packaging/package.ps1` builds
  `3doemulator-<component>-<rid>.zip` for Windows and `.tar.gz` for Linux/macOS,
  including README/notices/licensing, a generated `PACKAGE.txt`, and the Linux
  desktop entry. CI publishes both raw folders and archives for all six RIDs.

### Changed

- The CLI is published trimmed for all six RIDs; the Avalonia frontend remains
  untrimmed pending its XAML/`ViewLocator` audit.
- CI runs a trimmed-CLI smoke test on Windows in addition to the benchmark gate.
- The Settings video page now exposes a CRT bloom slider and lists the HQx
  2x/3x/4x filter modes.
- CI runs the benchmark budget gate instead of only checking allocations.
- Docs record the tenth-pass Lemmings evidence, the HQx 3x/4x hashes, and the
  remaining work.

## [Unreleased] - 2026-10-25

### Added

- `run --json` now reports `cdStatus` and `cdPoll`, making the static
  READ_ID-only wait state visible without a CD trace.
- `run --script-base state` shifts an input script by the loaded save state's
  frame count, so scripts can be written from frame 0 after `--state-in`.
- Per-title save-state, PNG, and WAV evidence for the five-title corpus, recorded
  in `docs/TestCorpus.md` 1.5a. Zero-frame loads, 120-frame continuations, and
  roundtrips are deterministic for every title.
- Space Ace interactive path: `X` returns to the title/menu and `A` starts the
  scene; the 15,000-frame state roundtrips and a gameplay WAV was captured.
- Alone in the Dark Begin/Resume menu and intro path; the intro is a static
  READ_ID-only wait.
- A `ScriptedPbusSource` test covering `B+Left` as two button bytes.
- `--autopilot road` pulses A/releases throttle through long-static,
  low-coverage, CD-load, and 180-frame restart-stuck states and reports
  `autopilotMenuAdvance` in `run --json`. This gets through `GAME LOADING`/`Get
  ready`, starts a new race at 80k, and cycles race/wreck/menu to 100k.
- `CdromDevice.LastReadFrame` for host load probes.

- Desktop gamepad backend: Windows XInput and Linux evdev, with hot-plugging,
  six-player mapping, analog-stick D-pad fallback, and trigger-to-shoulder fallback.
- Gamepad binding tab in the input editor.
- In-app diagnostic log viewer with level, category, search, auto-refresh, and clear.
- First-run welcome guide and error dialogs for BIOS, disc, and emulation failures.
- Save-state PNG thumbnails, shown in the Slots window.
- xBR-lv2 edge-directed 4x filter (opt-in), with sharp-bilinear finishing.
- Desktop GL GPU timer query and CPU-present timing in the FPS overlay.
- Shader source tests for the palette, scale, and xBR programs.

### Fixed

- **CD media-access wait.** Road Rash's old ~42,000 and ~92,000/100,000 stops were a
  static READ_ID-only wait, not wall pins. The CD device now raises the Portfolio
  media-access event (`0x40`) after two consecutive READ_ID polls with an idle gap;
  the game advances to `BUSTED`, race select, and a new race. The five 3,000-frame
  corpus hashes are unchanged.
- **GPU palette expansion used one CLUT index for all three channels.** Real-content
  frames appeared grayscale because the shader fetched `(R,G,B)` from the low five bits
  only. The shader now uses the red, green, and blue indices independently, matching
  `FrameAssembler` and `GpuPalette`.
- **GL screenshot readback was black** after the palette shader error; the scale pass
  also had a `vec2` uniform type mismatch that produced `GL_INVALID_OPERATION`.
- xBR now uses a CPU-expanded native-resolution source texture. This avoids a driver
  readback issue with the FBO-attached palette texture and keeps palette expansion
  pixel-identical to the CPU oracle in xBR mode.

### Changed

- Settings expose the xBR filter again.
- `AppSettings` records whether the first-run guide has been shown.
- The About dialog reports connected gamepads.
- Corrected and fixed the Road Rash diagnosis: the ~42,000 and ~92,000 stops were
  a static READ_ID-only game-side wait, not wall/rider pins. The CD media-access
  pulse advances them to `BUSTED`, race select, and a new race.
- The deferred CD read-status experiments were reverted because they changed the
  golden boot hashes; the media-access pulse keeps all five 3,000-frame hashes
  unchanged.
- Alone in the Dark's intro no longer stays static forever: A every 300 frames
  advances the man-at-desk cutscene to the room/mansion/title scenes. The media
  pulse alone does not advance the intro.
- Wolfenstein's long fresh-game load advances with A every 300 frames through
  character select to FLOOR 1-1 at 64,000 frames.
- Alone in the Dark has a scripted room movement loop with a save/load roundtrip
  (`0x44C7576B` -> two 120-frame continuations at `PC 0x0008BF00`,
  `0xE490C8C7`).

## Earlier work

See [`docs/ImplementationPlan.md`](docs/ImplementationPlan.md) for the milestone history
and [`docs/Status.md`](docs/Status.md) for the current compatibility and performance
snapshot.
