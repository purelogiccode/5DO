# 5DO 1.3.0

**Released:** 2026-11-06

5DO 1.3.0 is a presentation feature update on the 1.2.2 performance/library release. The
six-bundle packaging model is unchanged: Windows, Linux, and macOS on x64 and arm64, with
the CLI and desktop app in one framework-dependent bundle.

## Highlights

- **480i/240p field handling.** Settings > Video now selects **Auto** (detect 480i fields
  and weave them), **Bob** (present every field unchanged), **Weave** (always combine two
  fields), or **Blend** (average two fields into one flicker-free frame). The desktop
  defaults to Auto; the CLI and emulation core default to Bob.
- **Auto detection.** The detector compares same-index rows in adjacent fields with the
  vertical difference inside the current field. A verdict persists for 12 fields before
  weaving starts and 30 progressive fields before it stops. Per-game profiles and
  `--deinterlace` override detection.
- **Correct aspect and integer scaling.** Woven frames carry
  `VdlFrame.PixelAspect = 0.5`; OpenGL, Vulkan, Direct3D 11, and the CPU presenter keep
  the source display geometry. PAL 288-line fields weave to 576 lines.
- **Screenshots and GIFs.** Captures use the presented frame; sidecars record
  `deinterlace` and `interlaced`. GIFs rescale a mid-recording 240↔480 change instead of
  dropping frames.
- **High-resolution safe.** The opt-in 640x480 line-doubled mode is progressive and
  ignores weave/blend, so its output is unchanged.
- **No 240p regression.** Bob and auto hashes match over 3,000 frames on Road Rash, Space
  Ace, and Alone in the Dark, and 300 frames on Wolfenstein 3D and Lemmings.

## Bundles

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.3.0-win-x64.zip` |
| Windows arm64 | `5do-1.3.0-win-arm64.zip` |
| Linux x64 | `5do-1.3.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.3.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.3.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.3.0-osx-arm64.tar.gz` |

Each bundle contains the desktop app, the CLI, `README.md`, `LICENSE`,
`THIRD-PARTY-NOTICES.md`, `whatsnew.md`, the release notes, and the legal/OpenAL
notices. `SHA256SUMS.txt` is attached to the release.

The apps are framework-dependent single-file executables. Install the **.NET 10
Runtime** from <https://dotnet.microsoft.com/download/dotnet/10.0>; no runtime is
embedded in the bundle.

## Verification

- Full suite: **1,833 tests passing** in Debug and Release.
- `dotnet format --verify-no-changes` clean; frontend `--smoke-test` passed.
- Forced weave produced a 320x480 frame; forced blend stayed at 320x240; high-resolution
  Lemmings remained 640x480.
- Six bundles build; x64 runtime smoke tests pass; arm64 apphosts are
  architecture-verified.

## Known issues

- `Auto` is a content heuristic, not a hardware mode bit. No local title is confirmed
  480i, so synthetic complementary-field tests are the current 480i evidence.
- Woven captures are 320x480 (PAL 320x576) with half-height pixels; square-pixel viewers
  show them taller than 4:3.
- `Crash 'n Burn` (Japan) remains the single known no-video case in the 594-disc sweep.
- Linux GPU presentation is preview/best-effort under llvmpipe.
- Arm64 bundles are architecture-validated but not executed on arm64 hardware in CI.

See the [full release notes](https://purelogiccode.github.io/5DO/ReleaseNotes-1.3.0.html)
and [What's new](https://purelogiccode.github.io/5DO/WhatsNew.html).
