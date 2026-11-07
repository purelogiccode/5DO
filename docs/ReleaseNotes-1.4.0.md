# 5DO 1.4.0

**Release date:** 2026-11-07
**Status:** Feature and accuracy update on the 1.3.0 field-handling release.

## Highlights

- **Online cover art (UX-26).** An optional content-addressed WebP catalog at
  `https://assets.purelogiccode.com/5do/v1` provides 473 unique covers and maps 645 of
  653 4DO game IDs. 5DO downloads the catalog and only the covers it needs, verifies every
  image by SHA-256, and supports an optional ~27 MB bulk pack for offline use. Matching
  uses exact game IDs, annotation stripping, token/no-space/subtitle aliases, and
  Jaro-Winkler fallback with sequel-number guards. Settings > Covers and the
  `covers update/download/match/verify/cache` CLI commands expose the feature.
- **Latency controls (VID-12).** Settings > Video adds 0-2 frame run-ahead, a 0-8 frame
  presentation delay, and VRR/adaptive-sync presentation. Settings > Audio and
  `--audio-sync -250..250` add a host A/V offset. Per-game profiles store all four values,
  the FPS overlay reports software input-poll-to-frame-submit telemetry, and run-ahead
  auto-disables after repeated frame-budget overruns. The run-ahead snapshot is a reusable
  uncompressed `MachineStateSnapshot`; machine-state section v2 stores only visible lines
  and copies pixels as blocks, and the RAM loader clears only the previously addressable
  tail.
- **Unsigned-disc support (SYS-06).** Settings > Paths and `--allow-unsigned-discs`
  disable the BIOS media RSA check in memory so unsigned, homebrew, and arcade discs can
  boot. A known No-RSA BIOS is preferred when available; otherwise a compatible retail
  1 MB image is patched with the documented two-site bypass. The BIOS file is never
  modified. Already patched images and known No-RSA file names are accepted, and an
  incompatible image is rejected with an explicit error.
- **Exact NTSC field clock and rational VDL.** NTSC now runs at 60000/1001 (59.94 Hz,
  208,541 cycles per field at 12.5 MHz) instead of 60.00 Hz. `Quartz` uses an exact
  rational VDL accumulator, removing the per-batch 8.24 rounding that ran the scanline
  clock about 0.09% fast. Quartz state is version 4; version 1-3 states convert their
  8.24 fraction on load.
- **VDLP and audio fixes.** Disabled bitmap DMA now renders a black line instead of
  retaining the previous field, and the background word is converted from 8-bit RGB to
  the 5-bit format the renderers expect. Live OpenAL device switching uses the
  thread-local context extension, and the Settings test tone queues the whole second up
  front so Windows sleep granularity cannot insert silent gaps.
- **Differential evidence.** The harness now compares CD command counts, opcode hashes,
  full command sequences, per-command cycle stamps, CPSR, instruction counts, and sample
  deltas. The 50-title panel captured 50/50 with 0 errors; **32/50 titles match every
  frame hash** at frames 60/120/300 and every sample delta is within 9. At 3,000 frames
  Space Ace, Wolfenstein 3D, Alone in the Dark, and Lemmings match Opera's hash; Road
  Rash still diverges.

## Changed

- `FiveDoMachine.FramesPerSecond` now returns the exact 59.94 Hz field rate for host
  pacing; the nominal `FieldRate` remains 60/50 for display and JSON compatibility.
- `AppSettings` schema is version 7. Version 5 added unsigned-disc support, version 6
  added latency options, and version 7 added the online cover options. Older files load
  with documented defaults.
- Machine save-state section v2 stores only visible VDL lines and preserves frame
  dimensions and pixel aspect across load/restore.
- `CoreRunner` invalidates the run-ahead snapshot after any external machine action
  (disc swap, state load, BIOS/NVRAM change) even when the frame counter does not move.
- `LatencyTelemetry.MaxMilliseconds` now describes the rolling window rather than the
  maximum ever observed.

## Requirements

- The **.NET 10 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Optional: network access for the online cover catalog. Local covers and the core
  emulator work offline.

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
README/license/notices, `whatsnew.md`, and the legal/release docs. `SHA256SUMS.txt` is
attached to the GitHub release.

## Known limits

- Run-ahead is CPU-bound and opt-in. It is incompatible with rewind history and
  deterministic movie recording/playback; the frontend disables those features while it
  is active.
- VRR is a display/driver feature. 5DO can avoid the blocking swap interval, but actual
  adaptive-sync activation depends on the GPU, driver, monitor, and desktop.
- The online cover catalog is optional and is not included in any release bundle. A wrong
  fuzzy match should be corrected with the threshold or a local cover.
- The 50-title differential panel still has unresolved PC/CPSR phase and CD-cadence
  differences. Frame-hash matches are evidence, not a full accuracy claim.
- Windows x64 is the validated target. Linux x64 full-suite and CLI/frontend smoke
  validation pass; GPU presentation remains preview/best-effort under llvmpipe/SSH.
  macOS and arm64 bundles are cross-published and architecture-validated.

## Validation

- Full suite: **1,905/1,905** passing in Debug and Release.
- `dotnet build 5DO.sln -c Debug`: 0 warnings, 0 errors.
- `dotnet format 5DO.sln --verify-no-changes --no-restore`: clean.
- `jb inspectcode 5DO.sln --severity=WARNING --no-updates`: 0 results.
- Frontend `--smoke-test`, CLI `selftest`, and the differential harness checks are part
  of the release gate.
