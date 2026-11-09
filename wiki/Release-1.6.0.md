# 5DO 1.6.0

**Released:** 2026-11-08

5DO 1.6.0 is the **compatibility-completion update**. It closes the unique-title
compatibility plan with **375 of 378** game-test-set titles playable under the documented
binary beyond-title rule, publishes the authoritative 5DO/4DO/Phoenix comparison, tightens
the remaining CD-ROM poll/speed/session behavior against Opera, and hardens settings and
NVRAM persistence against locked or cloud-synced data folders.

## Highlights

- **Compatibility completion.** The local collection covers **1,371 images, 414 unique
  titles, and a 378-title game/homebrew test set**. **375 of 378** are Playable, the
  comparison has **0 `Behind` and 0 `Regression` rows**, and the playable set is a strict
  superset of the **252-title 4DO/Phoenix peer union**. The two Title-only rows and one
  No-video row are non-retail/add-on images.
- **Authoritative 3-way matrix.** The committed comparison and beyond-title ledger record
  the canonical disc, BIOS, disc hash, capture frame, screenshot, and failure reason for
  every title.
- **CD-ROM parity.** `POLREMASK` is a one-shot reset; only the three interrupt enables
  persist, and `POLRE` is write-one-to-clear. MODE SELECT page 3 stores Opera's
  `MEI_CDROM_DOUBLE_SPEED` byte (`0x80`) and updates the XBUS 2x status bit; a configured
  multi-speed drive caps at 1x when the guest selects single speed. SESSION INFO, TOC
  Q-position, and the volume-header readable-disc limit now match the reference. The
  CD-ROM save-state section is version 5.
- **Persistence hardening.** Settings and NVRAM writes fall back to a direct copy when a
  cloud-sync placeholder, read-only file, or delete-locked file rejects the atomic
  replace. The shell keeps running when a write still fails, guards startup media loading,
  preserves a newer build's settings as `settings.json.v<version>.bak`, and normalizes
  null/short `Players` arrays.
- **Diagnostics and polish.** FIQ source tracing, differential-panel audio gating and
  scripted input, nightly baseline comparison, complete tooltip coverage, and logged
  fallback paths.

## Validation

- Full suite **2,061/2,061** passes in Debug.
- Build: 0 warnings, 0 errors. `dotnet format --verify-no-changes` and
  `jb inspectcode --severity=WARNING` are clean.
- 378-row ledger verification passes: **375 Playable**, 2 Title-only, 1 No-video,
  0 Not-loadable, 0 `Behind`/`Regression`, strict peer superset.
- The comparison generator `--check` reports the committed outputs are current.
- Road Rash boots for 600 frames in both compatible and accuracy profiles; the 50-title
  differential panel remains **50/50 frame hashes** with **0 errors**.
- Windows x64 is the validated target. Linux x64 full-suite and CLI/frontend smoke
  validation pass; GPU presentation under llvmpipe/SSH remains preview/best-effort.
  macOS and arm64 bundles are cross-published and architecture-validated.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.6.0-win-x64.zip` |
| Windows arm64 | `5do-1.6.0-win-arm64.zip` |
| Linux x64 | `5do-1.6.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.6.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.6.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.6.0-osx-arm64.tar.gz` |

Each bundle contains the framework-dependent single-file CLI and desktop app, the
README/license/notices, `whatsnew.md`, and the release/legal docs. Install the **.NET 10
Runtime** first; the bundles do **not** include the runtime.

See the [full release notes](https://purelogiccode.github.io/5DO/ReleaseNotes-1.6.0.html)
and [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.6.0) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough or a
  claim for every regional revision.
- CD command cadence is the remaining behavioral differential on the 50-title panel; the
  3,000-frame showcase still diverges on Road Rash.
- VRR depends on the GPU, driver, monitor, and desktop; 5DO only avoids the blocking swap
  interval.
