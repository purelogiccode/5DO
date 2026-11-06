# 5DO 1.3.0

**Release date:** 2026-11-06
**Status:** Presentation feature update on the 1.2.2 performance/library release.

## Highlights

- **480i/240p field handling.** Interlaced titles can now be presented with correct
  field handling instead of showing one field as a full frame. Settings > Video exposes
  four modes:
  - **Auto** detects complementary fields and weaves them (desktop default).
  - **Bob** presents each field unchanged (the historical behavior and CLI default).
  - **Weave** always combines two fields into a 480-line (PAL 576-line) frame.
  - **Blend** averages consecutive fields into one flicker-free 240-line frame.
- **Content-based detection.** `Auto` compares same-index rows in adjacent fields with
  the vertical difference inside the current field. Progressive 240p content repeats the
  same row in both fields, so its temporal difference is near zero; interlaced content
  puts an adjacent source row in each field. A verdict must persist for 12 fields before
  weaving starts and 30 progressive fields before it stops. Explicit modes and per-game
  profiles override detection when a title still combs or flickers.
- **Correct presentation geometry.** Woven frames carry a half-height pixel aspect, so
  the OpenGL, Vulkan, Direct3D 11, and CPU presenters keep the source display geometry
  and apply integer scaling in display space. PAL 288-line fields weave to 576 lines.
- **Screenshots, GIFs, and automation.** Screenshots and GIF recording capture the
  presented frame. GIFs rescale a mid-recording 240↔480 change instead of dropping
  frames. `--deinterlace auto|bob|weave|blend` selects the mode in `run` and `trace`, and
  `run --json` reports `deinterlace`, `interlaced`, and `interlaceDetected`.
- **No 240p regression.** The CLI and emulation core default to bob. On 2026-11-06,
  3,000-frame bob-vs-auto runs of Road Rash, Space Ace, and Alone in the Dark produced
  identical hashes, as did 300-frame runs of Wolfenstein 3D and Lemmings.

## Changed

- GIF recording now keeps its initial canvas size and rescales frames whose dimensions
  change, rather than skipping every frame after a field-mode switch.
- Per-game profiles include the field-handling mode; older profiles leave the global
  setting unchanged.

## Requirements

- The **.NET 10 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.3.0-win-x64.zip` |
| Windows arm64 | `5do-1.3.0-win-arm64.zip` |
| Linux x64 | `5do-1.3.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.3.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.3.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.3.0-osx-arm64.tar.gz` |

Each bundle contains the framework-dependent single-file CLI and desktop app, the
README/license/notices, `whatsnew.md`, and the legal/release docs. `SHA256SUMS.txt` is
attached to the GitHub release.

## Known limits

- `Auto` is a content heuristic, not a hardware mode bit. No local title is confirmed
  480i, so the synthetic complementary-field tests are the current 480i evidence.
  Explicit `weave`/`blend` modes and per-game profiles are available.
- A woven capture is stored at 320x480 (PAL 320x576) with half-height pixels. Viewers
  that assume square pixels will show it taller than 4:3; the presenters apply
  `VdlFrame.PixelAspect` correctly.
- The opt-in 640x480 high-resolution mode is already progressive line-doubled output and
  ignores weave/blend.
- Emulation accuracy, compatibility, and save-state behavior are unchanged from 1.2.2.

## Validation

- Full suite: **1,833/1,833** passing in Debug and Release.
- `dotnet format --verify-no-changes` clean.
- `jb inspectcode --severity=WARNING`: no warnings in changed files; the remaining
  warnings are pre-existing in `Arm60.Jit.cs` and `StateWriter.cs`.
- Frontend `--smoke-test` passed.
- Forced weave produced a 320x480 frame on Road Rash; forced blend stayed at 320x240;
  high-resolution Lemmings stayed at 640x480 under both modes.

See [`Video.md`](Video.md) section 1 for the field-handling model and
[`ImplementationPlan.md`](ImplementationPlan.md) for the backlog status.
