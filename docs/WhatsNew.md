# What's new in 5DO 1.4.0

**Released:** 2026-11-07

5DO 1.4.0 is a feature and accuracy update on the 1.3.0 field-handling release. It adds
**online cover art**, **latency controls**, **unsigned-disc support**, and the **exact
59.94 Hz NTSC clock** used by the reference core. It keeps the six-bundle packaging model:
Windows, Linux, and macOS on x64 and arm64, with the CLI and desktop app in one
framework-dependent bundle.

## Changes in 1.4.0

### Online cover art

- **Optional catalog.** 5DO downloads a small catalog from
  `https://assets.purelogiccode.com/5do/v1` and only the covers it needs. The current
  pack has **473 unique WebP covers** and maps **645 of 653** 4DO game IDs.
- **Matching ladder.** Exact game IDs come first, followed by annotation stripping,
  token/no-space/subtitle aliases, and Jaro-Winkler fallback with sequel-number guards.
  The default threshold is **0.85**.
- **Cache and offline use.** Images are content-addressed and verified by SHA-256.
  An optional **~27 MB bulk pack** supports offline installs, and local covers still
  take precedence when they match.
- **Settings and CLI.** Settings > Covers exposes the catalog URL, fuzzy matching,
  cache actions, and the rights notice. The CLI adds
  `covers update/download/match/verify/cache`; `covers download --dry-run` previews
  matches before fetching.

### Latency controls

- **Run-ahead.** Settings > Video selects 0-2 speculative frames. `CoreRunner` saves and
  restores a reusable uncompressed `MachineStateSnapshot` each displayed frame, and
  auto-disables run-ahead after repeated frame-budget overruns.
- **Presentation delay and VRR.** A 0-8 frame delay holds the presented image behind the
  core. Adaptive-sync presentation disables the blocking swap interval and lets the
  compositor drive cadence.
- **Audio sync.** Settings > Audio and `--audio-sync -250..250` shift host audio relative
  to video without changing emulation speed or save states.
- **Telemetry.** The FPS overlay reports software input-poll-to-frame-submit latency, and
  `bench --latency` reports snapshot size, capture, and restore cost.

### Unsigned discs

- **In-memory RSA bypass.** Settings > Paths and `--allow-unsigned-discs` disable the
  BIOS media RSA check for unsigned, homebrew, and arcade discs. The BIOS file is never
  modified.
- **Safe selection.** A known No-RSA BIOS is preferred when available; otherwise a
  compatible retail 1 MB image is patched with the documented two-site bypass. Already
  patched images and known No-RSA file names are accepted, and an incompatible image is
  rejected with an explicit error.

### Accuracy and compatibility

- **Exact NTSC clock.** 5DO now runs NTSC at 60000/1001 (59.94 Hz, 208,541 cycles per
  field at 12.5 MHz) instead of 60.00 Hz. The rational VDL accumulator removes the
  per-batch 8.24 rounding that ran the scanline clock about 0.09% fast.
- **Reference evidence.** On the 50-title panel, every sample delta is within 9 samples at
  frame 300 and **32/50 titles match every frame hash** at frames 60/120/300. At 3,000
  frames Space Ace, Wolfenstein 3D, Alone in the Dark, and Lemmings match Opera's hash;
  Road Rash still diverges. The differential harness now compares CD command sequences
  and per-command cycle stamps.
- **VDLP blanking and background.** Disabled bitmap DMA now renders a black line instead
  of retaining the previous field, and the background word is converted from 8-bit RGB to
  the 5-bit format the renderers expect.
- **Audio reliability.** Live OpenAL device switching uses the thread-local context
  extension, and the Settings test tone queues the whole second up front so Windows sleep
  granularity cannot insert silent gaps.

### Documentation

- [`CoverArt.md`](CoverArt.md) documents the catalog schema, matching, cache, hosting,
  and takedown policy.
- [`CliReference.md`](CliReference.md), [`Configuration.md`](Configuration.md),
  [`UserGuide.md`](UserGuide.md), [`Audio.md`](Audio.md), [`Video.md`](Video.md), and
  [`KnownIssues.md`](KnownIssues.md) document the new options and limits.
- [`DifferentialHarness.md`](DifferentialHarness.md) and [`TestCorpus.md`](TestCorpus.md)
  record the exact-clock and CD-sequence evidence.

## Validation

- Full suite **1,905/1,905** passes in Debug and Release; `dotnet build` reports 0 warnings and
  0 errors; `dotnet format --verify-no-changes` is clean; `jb inspectcode
  --severity=WARNING` reports 0 results.
- The frontend `--smoke-test`, CLI `selftest`, and existing differential harness checks
  are part of the release gate.
- Windows x64 remains the validated target. Linux x64 full-suite and CLI/frontend smoke
  validation pass; GPU presentation remains preview/best-effort under llvmpipe/SSH.
  macOS and arm64 bundles are cross-published and architecture-validated.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.4.0-win-x64.zip` |
| Windows arm64 | `5do-1.4.0-win-arm64.zip` |
| Linux x64 | `5do-1.4.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.4.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.4.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.4.0-osx-arm64.tar.gz` |

Each bundle contains both the desktop app and the CLI. The **.NET 10 Runtime** must be
installed on the host; the bundles are framework-dependent and do **not** contain the
runtime. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.4.0) for
`SHA256SUMS.txt`.

## Known limits

- Run-ahead is CPU-bound and opt-in. It is incompatible with rewind history and
  deterministic movie recording/playback; the frontend disables those features while it
  is active.
- VRR is a display/driver feature. 5DO can avoid the blocking swap interval, but actual
  adaptive-sync activation depends on the GPU, driver, monitor, and desktop.
- The online cover catalog is optional. A wrong fuzzy match should be corrected with the
  threshold or a local cover; the catalog and images are not included in any release
  bundle.
- The 50-title differential panel still has unresolved PC/CPSR phase and CD-cadence
  differences; frame-hash matches are evidence, not a full accuracy claim.
