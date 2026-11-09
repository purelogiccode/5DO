# What's new in 5DO 1.6.0

**Released:** 2026-11-08

5DO 1.6.0 is the **compatibility-completion update**. It closes the unique-title
compatibility plan with **375 of 378** game-test-set titles playable under the documented
binary beyond-title rule, publishes the authoritative 5DO/4DO/Phoenix comparison, tightens
the remaining CD-ROM poll/speed/session behavior against Opera, and hardens settings and
NVRAM persistence against locked or cloud-synced data folders.

## Highlights

### Compatibility completion

- The local collection now covers **1,371 images, 414 unique titles, and a 378-title
  game/homebrew test set**.
- **375 of 378** test-set titles are Playable: every title has a dated beyond-title
  capture, BIOS, disc hash, frame, and screenshot.
- The comparison has **0 `Behind` and 0 `Regression` rows** and is a strict superset of
  the **252-title peer union** (167 4DO + 233 Phoenix after the one evidenced peer
  invalidation).
- The two remaining Title-only rows and one No-video row are non-retail/add-on images:
  `Joypad Test v1.0`, the standalone *Return Fire - Maps O' Death* data disc, and the
  `LameNES Emulator`.
- [`CompatibilityComparison.md`](CompatibilityComparison.md) is the authoritative
  per-title 5DO/4DO/Phoenix matrix; [`BeyondTitleEvidence.md`](BeyondTitleEvidence.md)
  holds the captures and failure reasons, and `docs/data/beyond-title-results.csv` is the
  machine-readable ledger.

### CD-ROM parity

- **Poll reset is one-shot.** `POLREMASK` performs a local FIFO reset and does not persist
  in the poll register. Only the three interrupt enables remain set, and `POLRE` is
  write-one-to-clear. Commands, status, and data are no longer gated by a stale reset bit.
- **Guest drive speed.** MODE SELECT page 3 stores Opera's
  `MEI_CDROM_DOUBLE_SPEED` byte (`0x80`) and updates the XBUS 2x status bit. A configured
  multi-speed drive drops to 1x when the guest selects single speed; `Unbounded` remains a
  host override.
- **Readable-disc limit.** The drive reports the 3DO volume header's big-endian
  `volume_block_count` (sector 0, offset `0x50`) instead of the physical image size. This
  matches Opera's readable-disc limit on pressed discs whose padding differs from the
  declared volume.
- **TOC and SESSION INFO.** TOC entries carry the Q-position control bit, and SESSION INFO
  returns the single-session payload used by the reference.
- **Save-state section 5.** The CD-ROM state records the guest speed byte and resolved
  sector count; version 3/4 states recover the speed byte from the XBUS status, and older
  states still load.

### Persistence hardening

- Settings and NVRAM writes prefer an atomic temporary-file replace. When a cloud-sync
  placeholder, read-only file, or another process denies the replace, 5DO copies the
  completed temporary file over the destination instead of aborting the UI.
- If the destination still cannot be written, the shell logs the failure and keeps running;
  startup media loading is guarded so a configured disc, BIOS, or settings path can no
  longer crash `OnOpened`.
- A `settings.json` written by a newer build is preserved as
  `settings.json.v<version>.bak` before an older build loads it.
- Null or short `Players` arrays are normalized to eight non-null profiles with safe
  keyboard/gamepad dictionaries.

### Diagnostics and desktop polish

- `--fiq-trace` and the timer trace now record the XBUS FIQ source, enable/acknowledge
  writes, and level-held raise transitions (`xbusfiq`, `fiqgen`, `fiqwrite`, `xbussel`,
  `xbuspoll`).
- The differential harness can gate audio, replay scripted input, and compare a fresh run
  against `benchmarks/diff/baseline/showcase-report.json`; a capture error fails the job.
- Every interactive control in the Avalonia windows now has a tooltip, enforced by
  `FrontendToolTipTests`. Fallback and cleanup paths log their expected exceptions instead
  of swallowing them.

## Validation

- Full suite **2,061/2,061** passes in Debug.
- `dotnet build` reports **0 warnings and 0 errors**; `dotnet format
  --verify-no-changes` and `jb inspectcode --severity=WARNING` are clean.
- `tools/verify-beyond-title-ledger.py`: 378 ledger rows, **375 Playable**, 2 Title-only,
  1 No-video, 0 Not-loadable, **0 Behind/Regression**, strict peer superset.
- `tools/generate-compat-comparison.py --check` reports the committed comparison outputs
  are current.
- Road Rash boots for 600 frames in both the compatible and accuracy profiles after the
  CD/volume-header changes; the 50-title differential panel remains **50/50 frame
  hashes** at frames 60/120/300 with **0 errors**.
- The six framework-dependent bundles contain the CLI, desktop app, README, license,
  notices, `whatsnew.md`, and release/legal docs, with no runtime, PDB, or XML files;
  `SHA256SUMS.txt` verifies.

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
| Windows x64 | `5do-1.6.0-win-x64.zip` |
| Windows arm64 | `5do-1.6.0-win-arm64.zip` |
| Linux x64 | `5do-1.6.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.6.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.6.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.6.0-osx-arm64.tar.gz` |

Each bundle contains the framework-dependent single-file CLI and desktop app, `README.md`,
`LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.6.0) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule (a distinct screen beyond
  the title with input), not a full playthrough or a claim for every regional revision.
- CD command cadence remains the residual differential on the 50-title panel; the
  3,000-frame showcase still diverges on Road Rash.
- Windows x64 remains the validated target. Linux x64 full-suite and CLI/frontend smoke
  validation pass; GPU presentation under llvmpipe/SSH is preview/best-effort. macOS and
  arm64 bundles are cross-published and architecture-validated.
- VRR depends on the GPU, driver, monitor, and desktop; 5DO only avoids the blocking swap
  interval.
