# 5DO 1.4.0

**Released:** 2026-11-07

5DO 1.4.0 is a feature and accuracy update on the 1.3.0 field-handling release. It adds
online cover art, latency controls, unsigned-disc support, and the exact 59.94 Hz NTSC
clock used by the reference core.

## Highlights

- **Online cover art.** An optional content-addressed WebP catalog at
  `https://assets.purelogiccode.com/5do/v1` provides 473 unique covers and maps 645 of
  653 4DO game IDs. 5DO downloads only the covers it needs, verifies every image by
  SHA-256, and supports an optional ~27 MB bulk pack for offline use. Settings > Covers
  and `covers update/download/match/verify/cache` expose the feature.
- **Latency controls.** Settings > Video adds 0-2 frame run-ahead, a 0-8 frame
  presentation delay, and VRR/adaptive-sync presentation. Settings > Audio and
  `--audio-sync -250..250` add a host A/V offset. Run-ahead uses a reusable uncompressed
  machine snapshot and auto-disables after repeated frame-budget overruns.
- **Unsigned discs.** Settings > Paths and `--allow-unsigned-discs` disable the BIOS media
  RSA check in memory. A known No-RSA BIOS is preferred; otherwise a compatible retail
  1 MB image is patched with the documented two-site bypass. The BIOS file is never
  modified.
- **Exact NTSC clock.** NTSC now runs at 60000/1001 (59.94 Hz, 208,541 cycles per field at
  12.5 MHz) instead of 60.00 Hz. The rational VDL accumulator removes the per-batch 8.24
  rounding that ran the scanline clock about 0.09% fast.
- **Fixes.** Disabled bitmap DMA now renders a black VDLP line, the background word uses
  the correct 8-bit-to-5-bit RGB conversion, live OpenAL device switching keeps each
  output's context, and the Settings test tone no longer crackles from sleep-granularity
  gaps.

## Validation

- Full suite **1,905/1,905** passes in Debug.
- Build: 0 warnings, 0 errors. `dotnet format --verify-no-changes` and
  `jb inspectcode --severity=WARNING` are clean.
- 50-title differential panel: 50/50 captured, 32/50 match every frame hash at
  60/120/300, every sample delta within 9. Space Ace, Wolfenstein 3D, Alone in the Dark,
  and Lemmings match Opera at 3,000 frames; Road Rash still diverges.
- Windows x64 is the validated target. Linux x64 full-suite and CLI/frontend smoke pass;
  GPU presentation remains preview/best-effort under llvmpipe/SSH. macOS and arm64 bundles
  are cross-published and architecture-validated.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.4.0-win-x64.zip` |
| Windows arm64 | `5do-1.4.0-win-arm64.zip` |
| Linux x64 | `5do-1.4.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.4.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.4.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.4.0-osx-arm64.tar.gz` |

Each bundle contains the framework-dependent single-file CLI and desktop app, the
README/license/notices, `whatsnew.md`, and the release/legal docs. Install the **.NET 10
Runtime** first; the bundles do **not** include the runtime.

See the [full release notes](https://purelogiccode.github.io/5DO/ReleaseNotes-1.4.0.html)
and [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.4.0) for
`SHA256SUMS.txt`.

## Known limits

- Run-ahead is CPU-bound and incompatible with rewind history and deterministic movie
  recording/playback; the frontend disables those features while it is active.
- VRR depends on the GPU, driver, monitor, and desktop; 5DO only avoids the blocking swap
  interval.
- The online cover catalog is optional and is not included in any release bundle.
- The differential panel still has unresolved PC/CPSR phase and CD-cadence differences;
  frame-hash matches are evidence, not a full accuracy claim.
