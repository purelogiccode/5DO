# Changelog

All notable changes are recorded here. The project is pre-1.0; formats and behavior may
still change.

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
- `--autopilot road` pulses A through long-static, low-coverage, frozen-line, or
  CD-load states and reports `autopilotMenuAdvance` in `run --json`. This gets
  through the `BUSTED`/race-select flow to a new race.
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

## Earlier work

See [`docs/ImplementationPlan.md`](docs/ImplementationPlan.md) for the milestone history
and [`docs/Status.md`](docs/Status.md) for the current compatibility and performance
snapshot.
