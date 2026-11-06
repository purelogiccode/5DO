# What's new in 5DO 1.3.0

**Released:** 2026-11-06

5DO 1.3.0 is a presentation update on the 1.2.2 performance/library release. It adds
**480i/240p field handling** while keeping the six-bundle packaging model: Windows,
Linux, and macOS on x64 and arm64, with the CLI and desktop app in one framework-dependent
bundle.

## Changes in 1.3.0

### 480i/240p field handling

- **Auto detection and weave (desktop default).** `InterlaceProcessor` detects
  complementary fields and combines them into a 480-line (PAL 576-line) frame. A verdict
  must persist for 12 fields before weaving starts and 30 progressive fields before it
  stops, so 240p output stays byte-identical to bob.
- **Bob, weave, and blend.** Settings > Video selects **Auto**, **Bob** (present every
  field, the historical behavior and CLI default), **Weave** (always combine two
  fields), or **Blend** (average two fields into one flicker-free frame).
- **Per-game and CLI control.** Per-game profiles store the mode, and
  `--deinterlace auto|bob|weave|blend` selects it in `run`/`trace`. `run --json` adds
  `deinterlace`, `interlaced`, and `interlaceDetected`.
- **Correct aspect and integer scaling.** Woven frames carry
  `VdlFrame.PixelAspect = 0.5`; OpenGL, Vulkan, Direct3D 11, and the CPU presenter keep
  the source display geometry. PAL 288-line fields weave to 576 lines.
- **Screenshots and GIFs.** Captures use the presented frame. GIFs rescale a
  mid-recording 240↔480 change instead of dropping frames.
- **High-resolution safe.** The opt-in 640x480 line-doubled mode is progressive and
  ignores weave/blend, so its output is unchanged.

### Validation and compatibility

- **240p unchanged.** 3,000-frame bob-vs-auto hashes match on Road Rash, Space Ace, and
  Alone in the Dark; 300-frame hashes match on Wolfenstein 3D and Lemmings.
- **1,833 tests** pass in Debug and Release. `dotnet format --verify-no-changes` is
  clean, the frontend `--smoke-test` passes, and inspectcode reports no warnings in the
  changed files.
- **Presentation-only.** The mode changes neither emulated state, input, audio, nor save
  states. The CLI defaults to bob for reference-compatible diagnostic hashes.

### Documentation

- [`Video.md`](https://purelogiccode.github.io/5DO/Video.html) documents the field model,
  detection heuristic, screenshot/GIF behavior, and PAL/high-res limits.
- [`CliReference.md`](https://purelogiccode.github.io/5DO/CliReference.html),
  [`Configuration.md`](https://purelogiccode.github.io/5DO/Configuration.html), and
  [`UserGuide.md`](https://purelogiccode.github.io/5DO/UserGuide.html) document the new
  option.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.3.0-win-x64.zip` |
| Windows arm64 | `5do-1.3.0-win-arm64.zip` |
| Linux x64 | `5do-1.3.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.3.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.3.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.3.0-osx-arm64.tar.gz` |

Each bundle contains both the desktop app and the CLI. The **.NET 10 Runtime** must be
installed on the host; the bundles are framework-dependent and do **not** contain the
runtime. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.3.0) for
`SHA256SUMS.txt`.

## Known limits

- `Auto` is a content heuristic, not a hardware mode bit. No local title is confirmed
  480i, so synthetic complementary-field tests are the current 480i evidence; explicit
  modes and per-game profiles remain available.
- Woven captures are 320x480 (PAL 320x576) with half-height pixels; square-pixel viewers
  will show them taller than 4:3.
