# 5DO 1.6.0

**Release date:** 2026-11-08
**Status:** Compatibility-completion update on the 1.5.0 accuracy release.

## Highlights

- **The compatibility plan is complete.** The unique-title corpus covers **1,371 images,
  414 unique titles, and a 378-title game/homebrew test set**. **375 of 378** test-set
  titles are Playable under the documented binary beyond-title rule (a distinct screen
  beyond the title with input). The comparison has **0 `Behind` and 0 `Regression` rows**
  and is a strict superset of the **252-title peer union** (167 4DO + 233 Phoenix after
  one evidenced peer invalidation).
- **Authoritative 3-way matrix.** [`CompatibilityComparison.md`](CompatibilityComparison.md)
  and `docs/data/compatibility-comparison.json` compare 5DO, 4DO, and Phoenix on the same
  canonical discs. [`BeyondTitleEvidence.md`](BeyondTitleEvidence.md) records the BIOS,
  disc hash, capture frame, screenshot, and failure reason for every title.
- **CD-ROM parity.** `POLREMASK` is now a one-shot FIFO reset; only the three interrupt
  enables persist, and `POLRE` is write-one-to-clear. MODE SELECT page 3 stores Opera's
  `MEI_CDROM_DOUBLE_SPEED` byte (`0x80`) and updates the XBUS 2x status bit; a configured
  multi-speed drive caps at 1x when the guest selects single speed. SESSION INFO returns
  the single-session payload, TOC entries carry the Q-position bit, and the readable-disc
  limit comes from the volume header's big-endian `volume_block_count` at sector 0 offset
  `0x50`. The CD-ROM save-state section is version 5 and remains backward compatible.
- **Persistence hardening.** Settings and NVRAM writes prefer an atomic temporary-file
  replace and fall back to a direct copy when a cloud-sync placeholder, read-only file, or
  delete-locked file rejects the replace. The shell keeps running when the write still
  fails, guards startup media loading, and preserves a newer build's settings as
  `settings.json.v<version>.bak`. Null/short `Players` arrays are normalized to eight
  non-null profiles.
- **Diagnostics.** `--fiq-trace` and the timer trace record the XBUS FIQ source,
  enable/acknowledge writes, and level-held raise transitions. The differential harness
  can gate audio, replay input scripts, and compare a fresh run with the checked-in
  baseline; capture errors fail the job.
- **Desktop polish.** Every interactive Avalonia control has a tooltip, enforced by
  `FrontendToolTipTests`; fallback/cleanup paths log their expected exceptions.

## Changed

- The CD-ROM state section is version 5. Version 3/4 states recover the guest speed byte
  from the XBUS status; older states continue to load.
- `FiveDo.Cli` and `FiveDo.Frontend.Avalonia` generate XML documentation. Internal
  `Program` members and `AsyncEvent.Run` use internal visibility; the two production async
  methods without an `Async` suffix were renamed.
- The README, status snapshot, compatibility docs, and release checklist now report the
  414-title corpus, the 378-title test set, and the current test count.
- A weekly compatibility baseline workflow runs the full beyond-title gate on the
  self-hosted 3DO runner and uploads the evidence.

## Fixed

- Settings persistence no longer aborts the desktop shell when the data folder denies an
  atomic replace. The store copies the completed temporary file over the destination and
  logs a warning if that also fails instead of throwing from the UI thread.
- NVRAM writes use the same fallback and keep the image dirty for a later retry instead of
  throwing during pause or shutdown.
- A newer-schema settings file is backed up before an older build rewrites it; expected
  schema/invalid-file warnings no longer create bug reports.
- Input settings with a null or short `Players` array no longer fail input initialization.
- Unreadable disc/BIOS/media paths are shown in the UI without filing a bug report, and
  startup configured paths cannot abort `OnOpened`.

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
- Windows x64 bundle smoke passes (CLI `--version`/`selftest` and desktop
  `--smoke-test`); the release workflow validates the six bundle trees, checksums, and
  arm64/macOS apphost architectures.

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

- Playability follows the documented binary beyond-title rule, not a full playthrough or a
  claim for every regional revision.
- CD command cadence is the remaining behavioral differential on the 50-title panel; the
  3,000-frame showcase still diverges on Road Rash.
- Windows x64 remains the validated target. Linux x64 full-suite and CLI/frontend smoke
  validation pass; GPU presentation under llvmpipe/SSH is preview/best-effort. macOS and
  arm64 bundles are cross-published and architecture-validated.
- VRR depends on the GPU, driver, monitor, and desktop; 5DO only avoids the blocking swap
  interval.
