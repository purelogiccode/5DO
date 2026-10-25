# Changelog

All notable changes are recorded here. The project is pre-1.0; formats and behavior may
still change.

## [Unreleased] - 2026-10-25

### Added

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

## Earlier work

See [`docs/ImplementationPlan.md`](docs/ImplementationPlan.md) for the milestone history
and [`docs/Status.md`](docs/Status.md) for the current compatibility and performance
snapshot.
