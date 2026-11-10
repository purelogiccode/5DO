# Release Checklist

**Adopted:** 2026-10-26
**Release timing:** **v1.6.0 is the current release**; unified Windows/Linux/macOS x64/arm64
bundles are built from the same gates used for v1.0.0, v1.1.0, and v1.2.x.
**Release definition:** v1.0 is a **documented-compatibility release**, not a
full-compatibility one. v1.1.0 was a maintenance update; v1.2.0 added features,
accuracy telemetry, and broader disc support; v1.2.1 added arm64 bundles; v1.2.2 added
interpreter/high-res/rewind performance, the opt-in managed JIT, library health and
database work, memory capacity options, and eight PBUS ports; v1.3.0 adds
480i/240p detection, deinterlacing, and field blending; v1.4.0 adds online cover art,
latency controls, unsigned-disc support, and the exact NTSC field clock; v1.5.0 adds
GPU HQx, the 24-bit VDL background, reference field-boundary timing, and CD parity.
v1.6.0 completes the 378-title compatibility matrix, tightens CD poll/speed/session
behavior, and hardens settings/NVRAM persistence.
Ship the platforms and titles that are validated, publish an honest per-title matrix
and known issues, and move everything else to the post-1.0 backlog.

This checklist is the release gate. A checked box must have evidence (a command, CI
run, screenshot, or recorded hash), not just an intention. Update `docs/Status.md`,
`docs/Compatibility.md`, `docs/KnownIssues.md`, and `CHANGELOG.md` as items complete.

---

## 1.1.0 maintenance gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.1.0`.
- [x] Move the OpenAL Soft license text to `docs/OpenAL-Soft-COPYING.txt` and remove
      the nested `docs/licenses/` folder.
- [x] Every bundle copies the license to `docs/OpenAL-Soft-COPYING.txt`.
- [x] Game library deduplicates copies of the same game by 3DO game ID.
- [x] Desktop status bar no longer shows the `frame ... PC ... cyc` counter.
- [x] CLI and desktop logging route through Serilog; warning-and-higher events reach
      the bug-report API with environment, error, and exception blocks.
- [x] CLI and desktop launch statistics call the ApplicationStats endpoint.
- [x] Full suite **1,475/1,475**, `dotnet format --verify-no-changes`, and
      `jb inspectcode --severity=WARNING` are clean.
- [x] CI cross-publishes the macOS x64 bundle on Linux and validates its Mach-O
      architecture, avoiding the unavailable hosted macOS runner.
- [x] Build the three bundles. Windows and Linux bundles passed CLI `selftest` and
      desktop `--smoke-test`; the macOS x64 bundle is cross-published on Linux and
      validated as Mach-O x86_64, matching the v1.0.0 runtime-validation scope.
- [x] Publish `v1.1.0` and verify `SHA256SUMS.txt`.

---

## 1.3.0 field-handling gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.3.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.3.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, and `docs/Installation.md`.
- [x] Update the MkDocs navigation and the curated wiki sidebar with the 1.3.0 pages.
- [x] Full suite **1,833/1,833** passes in Debug and Release;
      `dotnet format --verify-no-changes` is clean.
- [x] Verify VID-04: bob/weave/blend, auto detection, PAL 288→576, high-res fallback,
      aspect-aware viewports, per-game/CLI selection, and screenshot/GIF behavior.
- [x] Verify 240p hashes are unchanged: 3,000-frame bob-vs-auto runs of Road Rash,
      Space Ace, and Alone in the Dark, plus 300-frame Wolfenstein 3D and Lemmings.
- [x] Build all six framework-dependent single-file bundles and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release` without deleting older artifacts.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`; verify arm64 apphost
      machine types.
- [x] Create and push tag `v1.3.0`; the release workflow published six versioned
      bundles and `SHA256SUMS.txt`:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.3.0>. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/42235476257> passed all six
      bundle jobs and the publish job; all six checksums verify. Local bundles are in
      `src/FiveDo.Frontend.Avalonia/bin/Release`; every historical release asset is
      archived under `github-releases/` there.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/42235456021> passed; the
      1.3.0 release page, What's New, release notes, and wiki page return HTTP 200.

---

## 1.4.0 feature and accuracy gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.4.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.4.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, and `docs/Installation.md`.
- [x] Update the MkDocs navigation and the curated wiki sidebar with the 1.4.0 pages.
- [x] Full suite **1,905/1,905** passes in Debug and Release;
      `dotnet format --verify-no-changes` is clean;
      `jb inspectcode --severity=WARNING` reports 0 results.
- [x] Verify UX-26 cover catalog, SYS-06 unsigned-disc patching, VID-12 latency controls,
      the exact NTSC clock, VDLP blanking/background, and the OpenAL switching/test-tone
      fixes are documented and covered by tests.
- [x] Build all six framework-dependent single-file bundles and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release` without deleting older artifacts.
      Local bundles: `5do-1.4.0-{win-x64,win-arm64}.zip` and
      `5do-1.4.0-{linux-x64,linux-arm64,osx-x64,osx-arm64}.tar.gz`.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the 1.4.0 release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test` (`FRONTEND SMOKE OK 1.4.0`);
      CI verifies the arm64 apphost machine types.
- [x] Create and push tag `v1.4.0`; the release workflow published six versioned bundles
      and `SHA256SUMS.txt`:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.4.0>. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/42399055155> passed all six
      bundle jobs and the publish job; all six checksums verify locally. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/42398646569> passed every
      required job. Local bundles are in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for the
      1.4.0 release page, What's New, release notes, and wiki page. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/42399572702> passed build,
      Pages deploy, and wiki publish; the 1.4.0 What's New and wiki release page are
      live.

---

## 1.5.0 accuracy and GPU-HQx gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.5.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.5.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, and `docs/Installation.md`.
- [x] Update the MkDocs navigation and the curated wiki sidebar with the 1.5.0 pages.
- [x] Full suite **1,939/1,939** passes in Debug;
      `dotnet format --verify-no-changes` is clean;
      `jb inspectcode --severity=WARNING` reports 0 results.
- [x] Verify GPU HQx 2x/3x/4x rule-table parity in Direct3D 11 and Vulkan, plus the
      WGL/ANGLE GL probe path.
- [x] Verify the 24-bit VDL background, zero-entry stop, field-boundary `RunFrame`,
      CD poll/FIQ/READ_DATA parity, and the differential harness reports are documented
      and covered by tests.
- [x] Build all six framework-dependent single-file bundles and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release` without deleting older artifacts.
      Local bundles: `5do-1.5.0-{win-x64,win-arm64}.zip` and
      `5do-1.5.0-{linux-x64,linux-arm64,osx-x64,osx-arm64}.tar.gz`; all six SHA-256
      checksums verify.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the 1.5.0 release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files. CI verifies every bundle tree; local
      extraction of `5do-1.5.0-win-x64.zip` confirms the same contents.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`
      (`FRONTEND SMOKE OK 1.5.0`, `windowState=Normal` and `windowState=FullScreen`);
      CI verifies the arm64 apphost machine types.
- [x] Create and push tag `v1.5.0`; the release workflow published six versioned
      bundles and `SHA256SUMS.txt`:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.5.0>. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/42445302517> passed all six
      bundle jobs and the publish job. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/42445297515> passed every
      required job.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for the
      1.5.0 release page, What's New, release notes, and wiki page. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/42445297539> passed build,
      Pages deploy, and wiki publish; `WhatsNew.html`, `ReleaseNotes-1.5.0.html`, and
      `wiki/Release-1.5.0` return HTTP 200.

---

## 1.6.0 compatibility-completion gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.6.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.6.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, and `docs/Installation.md`.
- [x] Update the MkDocs navigation and the curated wiki sidebar with the 1.6.0 pages.
- [x] Full suite **2,061/2,061** passes in Debug;
      `dotnet format --verify-no-changes` is clean;
      `jb inspectcode --severity=WARNING` reports 0 results.
- [x] Verify the CD poll reset, guest drive speed, SESSION INFO, TOC Q-position,
      volume-header readable-disc limit, FIQ source tracing, and CD-ROM state v5 are
      documented and covered by tests.
- [x] Verify the 378-title ledger and comparison outputs:
      `tools/verify-beyond-title-ledger.py` passes with 375 Playable, 0 Behind/Regression,
      and a strict peer superset; `tools/generate-compat-comparison.py --check` reports
      the committed outputs are current.
- [x] Verify settings/NVRAM persistence fallback, newer-schema backup, and input
      normalization with unit tests and the locked-settings desktop reproduction.
- [x] Build all six framework-dependent single-file bundles and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release` without deleting older artifacts.
      Local bundles: `5do-1.6.0-{win-x64,win-arm64}.zip` and
      `5do-1.6.0-{linux-x64,linux-arm64,osx-x64,osx-arm64}.tar.gz`; all six
      `SHA256SUMS-1.6.0.txt` entries verify.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the 1.6.0 release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`
      (`FRONTEND SMOKE OK 1.6.0`); CI verifies the arm64/macOS apphost machine types.
- [x] Create and push tag `v1.6.0`; the release workflow published six versioned
      bundles and `SHA256SUMS.txt`:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.6.0>. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/42552318341> passed all six
      bundle jobs and the publish job. All six published `SHA256SUMS.txt` entries verify
      locally. Local bundles are in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for the
      1.6.0 release page, What's New, release notes, and wiki page. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/42552303805> passed build,
      Pages deploy, and wiki publish; `ReleaseNotes-1.6.0.html`, `WhatsNew.html`, and
      `wiki/Release-1.6.0` return HTTP 200.

---

## Cover-art hosting gate

- [ ] Run `packaging/package-covers.ps1`; review `artifacts/covers/v1/report.json`.
- [ ] Confirm every catalog image reloads and hashes correctly, and every game ID is
      mapped at most once (the packager verifies this automatically).
- [ ] Upload `artifacts/covers/v1` with `packaging/publish-covers.ps1`; verify
      `https://assets.purelogiccode.com/5do/v1/catalog.json`
      returns the expected `packVersion` and cover count.
- [ ] Test a clean cache on Windows and Linux: catalog refresh, on-demand download,
      bulk-pack download, `covers verify`, and offline fallback to cached/local covers.
- [ ] Confirm the catalog and images are **not** inside any release bundle.
- [ ] Confirm the rights/takedown notice is present in `docs/CoverArt.md` and linked from
      Settings > Covers.

---

## 1.2.2 performance and library gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.2.2`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.2.2.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, and `docs/Installation.md`.
- [x] Update the MkDocs navigation and the curated wiki sidebar with the 1.2.2 pages.
- [x] Full suite **1,815/1,815** passes; `dotnet format --verify-no-changes` is clean.
- [x] Benchmark gate passes with 0 bytes/frame and p99 within budget.
- [x] Build all six framework-dependent single-file bundles and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release` without deleting older artifacts.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`; verify arm64 apphost
      machine types. CI ran the x64 runtime smoke tests; the local and CI bundle checks
      verified PE x64/ARM64, ELF x86-64/aarch64, and Mach-O x86_64/arm64.
- [x] Audit every commit after `v1.2.1` and fix the eight concrete bugs found:
      JIT register-shift PC delta; stale ROM-bank blocks; stale soft-reset blocks;
      FIQ entry telemetry; prefetch track-sector length; legacy PBUS slots 7-8;
      pointer-exit port bound; ARM clock range.
- [x] Create and push tag `v1.2.2`; the release workflow published six versioned bundles
      and `SHA256SUMS.txt`:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.2.2>. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/42169040644> passed all six
      bundle jobs and the publish job; all six checksums verify.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/42169037958> passed; the
      1.2.2 release page and wiki page return HTTP 200.

---

## 1.2.1 arm64 packaging gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.2.1`.
- [x] Add `win-arm64`, `linux-arm64`, and `osx-arm64` to the bundle script and CI matrix.
- [x] Verify PE ARM64, ELF aarch64, and Mach-O arm64 apphosts.
- [x] Full suite **1,597/1,597** passes; format and inspectcode are clean.
- [x] Build all six bundles and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Create and push tag `v1.2.1`; the release workflow published six versioned
      bundles and checksums:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.2.1>. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/41583317988> passed all six
      bundle jobs and the publish job; all six `SHA256SUMS.txt` entries verify.

---

## 1.2.0 feature gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.2.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.2.0.md`,
      `README.md`, `docs/Status.md`, and `docs/index.md`.
- [x] Verify the Direct3D 11 backend, startup release checks, fuzz harness,
      per-edge overscan/void border, screenshot gallery, refresh-rate setting, flat
      CHD/raw ISO support, and FIQ/DMA/DSP audit changes are documented.
- [x] Full suite **1,597/1,597** passes; `dotnet format --verify-no-changes` is clean.
- [x] `jb inspectcode --severity=WARNING` reports 0 results before producing bundles.
- [x] Build the Windows, Linux, and macOS x64 bundles.
- [x] Windows/Linux bundle CLI `selftest` and desktop `--smoke-test` pass; the macOS
      bundle is validated as Mach-O x86_64. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/41581528432> passed all three
      bundle jobs and the publish job.
- [x] Keep local bundle copies under `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Create and push tag `v1.2.0`; the release workflow published the bundles,
      checksums, and release notes:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.2.0>. All three
      `SHA256SUMS.txt` entries verify.

---

## 0. Recommended v1 scope

These are recommendations; the release owner confirms or changes them in section 1.
The important rule is: **do not claim a platform or a playable title that has not
been validated.**

| Item | Recommended v1 scope | Notes |
|---|---|---|
| Validated desktop platform | **Windows x64** | WGL and ANGLE/GLES validated; CPU fallback works |
| Secondary desktop builds | **Linux x64** (full suite and CLI/frontend smoke passed; GL preview); macOS x64/arm64 built and architecture-validated | Linux GPU presentation is unvalidated under llvmpipe; Windows GL remains the validated renderer |
| Desktop arm64 | **Built and architecture-validated**; runtime validation post-1.0 | GLES 3.1, Tier B timing, and DPI are unvalidated; Raspberry Pi is out of scope |
| Compatibility set | **414 unique titles / 378 game-test-set titles**; 375 playable under the binary beyond-title rule | Per-title status must stay honest and evidenced |
| Disc formats | ISO, BIN, CUE+BIN, CHD | Already implemented and tested |
| Renderer | OpenGL default, CPU fallback | Filter family is complete |
| Packages | One bundle per system and architecture | `5do-<version>-win-x64.zip`, `5do-<version>-win-arm64.zip`, `5do-<version>-linux-x64.tar.gz`, `5do-<version>-linux-arm64.tar.gz`, `5do-<version>-osx-x64.tar.gz`, `5do-<version>-osx-arm64.tar.gz`; each contains the CLI, desktop app, README/license/notices/whatsnew. The .NET 10 runtime is a host prerequisite and is not bundled |
| Distribution model | **Donationware** | Non-commercial, source-available; voluntary donations only, no sales |
| Version | **`v1.6.0`** | Bump `FiveDoVersion.Version` and `Directory.Build.props`; v1.0.0, v1.1.0, v1.2.0, v1.2.1, v1.2.2, v1.3.0, v1.4.0, v1.5.0, and rc.1 are historical |
| Release timing | **Prepared 2026-11-08** | v1.6.0 bundles and checksums are attached to the GitHub release |

---

## 1. Scope and decisions

- [x] Confirm supported RIDs and which one is the **validated** target:
      **Windows x64** is the validated target for 1.0.0; Linux x64 full suite and CLI/frontend smoke
      passed and GPU presentation is preview/best-effort. The other RIDs are build
      artifacts only unless separately smoke-tested.
- [x] Confirm the supported title list and each title's status:
      **Playable**, **Boots/menus**, or **Known issues** (12 documented titles; five
      showcase titles with scripted gameplay evidence).
- [x] Confirm the release version: **1.0.0**; legal/distribution sign-off is recorded.
- [x] Confirm the distribution model and license in `docs/Licensing.md`:
      FreeDO modified GNU LGPL, **donationware** (non-commercial/source-available;
      voluntary donations, no sales), recorded in the root `LICENSE`.
- [x] Confirm installers/signing/notarization are **out of scope** for 1.0.0.
- [x] Record the decisions in this file and in `docs/Status.md`.

Recommended title statuses:

| Title | v1 status to document |
|---|---|
| Wolfenstein 3D | Playable: fresh game, movement/fire, save/load; known long load |
| Lemmings | Playable: passcode path, gameplay start, save/load |
| Road Rash | Playable race, not full-race completion; document the known wreck/end state |
| Alone in the Dark | Playable room/menu/intro; inventory not scripted |
| Space Ace | Playable interactive scene start; longer decisions unvalidated |
| Gex | Boots/attract scene; input not scripted |
| Need for Speed | Boots/menus; input not scripted |
| Super Street Fighter II Turbo | Boots/attract match; input not scripted |
| Total Eclipse | Boots/attract gameplay; input not scripted |
| Killing Time | Boots/title menu; input not scripted |
| Theme Park | Boots/title menu; captured menu audio is silent |
| Return Fire | Boots/menu; input not scripted |

---

## 2. Legal and attribution (hard gate)

- [x] Distribution/license decision recorded in `docs/Licensing.md` and the root
      `LICENSE`.
- [x] `THIRD-PARTY-NOTICES.md` covers FreeDO/4DO, Opera/libopera, CHDSharp, Avalonia,
      Silk.NET, Serilog and its console/file sinks, the bundled OpenAL Soft 1.23.1
      runtime, and other NuGet dependencies. See
      [`ReleaseLegalReview.md`](ReleaseLegalReview.md).
- [x] OpenAL Soft LGPL-2.0-or-later obligations (notice, corresponding source
      offer, relinking rights) are included in the release decision. The runtime is
      bundled in the Windows/Linux/macOS x64 bundles. Evidence: `docs/OpenALSoft.md`
      records the notice, the exact upstream 1.23.1 source, the three-year written
      source offer, and relinking instructions; `docs/OpenAL-Soft-COPYING.txt`
      contains the complete LGPL-2.0 text; `packaging/package-bundle.ps1` copies both
      into every bundle, and the win-x64 bundle was inspected after packaging.
- [x] About dialog and CLI `--version`/license output are current.
- [x] No BIOS ROM, disc image, extracted asset, WAV capture, or copyrighted game
      content is tracked in the repository; packaging uses only publish output plus
      README/notices/licensing.
- [x] Release owner/legal sign-off recorded in section 6: project owner approved
      distribution under the FreeDO modified GNU LGPL as free, open-source
      donationware (non-commercial; voluntary donations only), 2026-11-02.

This is the only item that can block the release independently of engineering. Start
it first. The engineering audit and the remaining owner/legal decisions are recorded
in [`ReleaseLegalReview.md`](ReleaseLegalReview.md).

---

## 3. Build, test, and CI

- [x] `dotnet build 5DO.sln -c Release` passes with 0 warnings and 0 errors
      (1.0.0).
- [x] `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release` passes:
      **1,815/1,815** with the current tree.
- [x] `dotnet format 5DO.sln --verify-no-changes` passes.
- [x] `jb inspectcode 5DO.sln --severity=WARNING` reports 0 warnings and 0 errors
      (2026-11-02).
- [x] CI is green on the supported runners (2026-11-02): Windows, Linux, macOS,
      NativeAOT CLI/frontend, unified bundle builds, CodeQL, docs, and dependency review.
- [x] Synthetic benchmark `--gate` passes: 2.28 ms/frame core mean, 4.03 ms core
      p99, 4.66 ms total p99, 0 bytes/frame.
- [x] Bundle CLI smoke test passes: `--version`, `selftest --json`,
      `bench --frames 2 --json`, `info`, and a 300-frame disc run.
- [x] Bundle frontend smoke test passes (`--smoke-test`).
- [x] Linux x64 smoke test passes (2026-11-02): the full test suite **1,473/1,473**,
      CLI `selftest`, `bench`, `info`, `bios info`, a 3,000-frame Wolfenstein boot,
      non-silent audio, and the frontend `--smoke-test` on Linux Mint 22.3. The GL probe
      timed out under llvmpipe, so Linux GPU presentation is preview/best-effort; see
      [`Compatibility.md`](Compatibility.md) §10.3.
- [x] NativeAOT CLI/frontend smoke tests are not shipped in 1.0.0; the
      framework-dependent single-file bundles are the release artifacts.

Suggested commands:

```powershell
dotnet build 5DO.sln -c Release
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release
dotnet format 5DO.sln --verify-no-changes
jb inspectcode 5DO.sln --sEverity=WARNING --no-build
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- --frames 600 --gate
```

---

## 4. Stability and compatibility

Run a **10-minute soak** (36,000 frames at 60 Hz) on each showcase title, and at least
a boot/menu soak on the remaining supported titles. Record final PC and frame hash;
compare against `docs/TestCorpus.md` where a baseline exists.

Use `tools/release-soak.ps1` for the per-machine run. The corpus differs between the
two development machines, so a `Missing on this machine` row is expected and is not a
failure. The script uses archived 12k states for absent discs when available and
writes `summary.md`/`summary.json` for the release evidence.

### 4.1 Full-collection compatibility sweep (M13)

The v1.0 release uses the full local collection's recorded loadability status; the
post-1.0 visual review continues from it. See [`CompatibilitySweep.md`](CompatibilitySweep.md)
and [`GameCompatibilityList.md`](GameCompatibilityList.md).

- [x] Run the 3,000-frame loadability sweep over `J:\Panasonic 3DO` (589 CHDs) and
      `G:\MAME\MAME Software List CHDs\3do` (5 CHDs): 594 tested, 593 render,
      1 known boot failure (`Crash 'n Burn` Japan), 0 not loadable.
- [x] Every image has a loadability status and an evidence path.
- [x] Every `Not Loadable` result has a triage note and reproduction command (none remain).
- [ ] Rendered titles have a screenshot and a short input smoke test (post-1.0 visual review).
- [ ] The high-resolution pass has a per-title result (post-1.0).
- [x] Linux x64 build/test/smoke results are recorded (2026-11-02): the full test
      suite **1,473/1,473**, CLI `selftest`/`bench`/`info`, a 3,000-frame Wolfenstein
      boot, non-silent audio, and the frontend `--smoke-test` passed on Linux Mint
      22.3. The GL probe timed out under llvmpipe, so GPU presentation is
      preview/best-effort.

M13.5/M13.6 (in-game/visual and high-resolution review) are **post-1.0** and do not
block v1.0. Linux x64 full suite and CLI/frontend smoke passed; the unified bundles are
built and published by CI. Linux GPU presentation remains preview/best-effort.

### 4.2 High-resolution CEL decision (M14 — post-1.0 research)

M14 is **not a v1.0 gate**. The current `--high-res` mode remains opt-in; the default
stays 320x240 + output filtering. Track the research in
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

- [ ] Current `--high-res` mode is measured per tested title (post-1.0).
- [ ] True 640x480 prototype is validated or rejected with evidence (post-1.0).
- [ ] 2x supersampling prototype has frame-time/allocations data (post-1.0).
- [ ] GPU CEL feasibility note has a parity/performance estimate (post-1.0).
- [ ] The default remains compatibility-safe 320x240 + output filtering unless a title
      is explicitly validated.

### 4.3 Existing soak evidence (12-title set)

- [x] Wolfenstein 3D soak: 36,000 frames, exit 0; `0x0814F039`.
- [x] Lemmings soak: 36,000 frames, exit 0; `0x9939D345`.
- [x] Road Rash soak: 36,000 frames, exit 0; `0xBDC2DBEA`.
- [x] Alone in the Dark soak: 36,000 frames, exit 0; `0xBA7F2C8C`.
- [x] Space Ace soak: 36,000 frames, exit 0; `0xFD0F5B72`.
- [x] Gex, Need for Speed, SSF2 Turbo, Total Eclipse, Killing Time, Theme Park,
      and Return Fire: 36,000-frame state soak from the archived 12k states on
      1.0.0 candidate; all exit 0. The source discs for these seven are not present on
      this machine, so this is a continuation soak, not a fresh boot.
- [x] Save/load roundtrip on each showcase title: zero-frame hash matches and two
      120-frame continuations are identical (see `Compatibility.md` §10.2).
- [x] Audio is non-silent 44.1 kHz stereo on the showcase titles and uses the exact
      44,100-sample clock (10.000 s per 600 frames). Road Rash's race mix clips
      (2,036 frames in a 92k-state capture), the 36k Lemmings gameplay state happened
      to be silent while its 23k state is non-silent, and Theme Park's menu is silent;
      all are documented in `TestCorpus.md` 1.5a2. Live OpenAL output, audio-driven
      pacing, WAV analysis, and AOT CLI audio are validated. The 2026-10-28 pass
      fixed the remaining live-output crackle (buffer ownership) and validated it
      with the PCM tap: 0 OpenAL errors, 0 underruns, and 0 discontinuities across
      2,345 buffer boundaries.
- [x] Keyboard input works on the showcase titles through the scripted runs; gamepad
      mapping and merge behavior are covered by unit tests, with hardware validation
      still platform-specific.
- [x] Screenshots and save slots are covered by unit tests and the frontend smoke
      path; a manual click-through was not repeated in this session.
- [x] OpenGL path works (WGL and ANGLE/GLES Accurate parity, 0 different pixels);
      CPU fallback is covered by the existing tests and smoke path.
- [x] `docs/Compatibility.md` updated with the release matrix and §10.2 soak evidence.
- [x] `docs/KnownIssues.md` updated with the Space Ace long no-input wait and the
      remaining platform/gameplay limitations.

Suggested soak command:

```powershell
$env:FIVEDO_GAMES = "J:\Panasonic 3DO"
$env:FIVEDO_DISC  = "$env:FIVEDO_GAMES\Wolfenstein 3D (USA).chd"

dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_DISC" --bios bios/panafz1.bin --frames 36000 `
  --nvram artifacts/soak-w3d.ram --frame-out artifacts/soak-w3d.png --json
```

A known hang at a documented frame is acceptable for v1.0 if `KnownIssues.md` says so.
A new crash, silent corruption, or save-state regression is not.

---

## 5. Packaging and artifacts

**Hard gate:** a release must **produce and publish runnable bundles/binaries**. A
source-only tag or GitHub release is not a complete release.

- [x] Publish the CLI and desktop app as framework-dependent single-file apphosts
      (`PublishSingleFile=true`, `PublishTrimmed=false`).
- [x] Do **not** copy the .NET runtime into the bundle. Users must install the .NET 10
      runtime on their machine; `PACKAGE.txt` and the README record the requirement.
- [x] Build one bundle per system with `packaging/package-bundle.ps1`:
      `5do-<version>-win-x64.zip`, `5do-<version>-linux-x64.tar.gz`,
      `5do-<version>-osx-x64.tar.gz`.
- [x] Each bundle contains both executables, `README.md`, `LICENSE`,
      `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, `docs/Licensing.md`,
      `docs/ReleaseLegalReview.md`, `docs/OpenALSoft.md`,
      `docs/OpenAL-Soft-COPYING.txt`, the release notes, and `PACKAGE.txt`.
- [x] Remove PDB and XML documentation files; they are build outputs, not runtime
      dependencies.
- [x] Keep the native OpenAL Soft/Skia/ANGLE libraries beside the apphosts so the
      LGPL replacement rights remain practical.
- [x] `PACKAGE.txt` records component `bundle`, RID, version, commit, UTC build time,
      the .NET 10 runtime requirement, launch commands, and the no-BIOS/no-disc notice.
- [x] No runtime folder, PDBs, XML docs, or copyrighted content in release bundles.
- [x] Extract each bundle to a clean folder and run the CLI/frontend smoke test.
- [x] SHA-256 checksums generated for every bundle.
- [x] CI builds the Windows/Linux bundles on their native runners, cross-publishes the
      macOS x64 bundle on Linux, verifies that no runtime or XML files are present, and
      publishes them with the checksums; the docs workflow updates GitHub Pages and the
      wiki.
- [x] Local copies are written to `src/FiveDo.Frontend.Avalonia/bin/Release`.

### 5.1 Single-file decision

A self-contained single-file publish embeds a complete .NET runtime in **each**
executable (roughly 70 MB per app before compression) and duplicates it. The release
therefore uses **framework-dependent single-file apphosts**: each app is one executable,
the .NET runtime is not embedded, and the bundle contains no runtime folder. Users must
install the .NET 10 runtime on their machine.

This is the required distribution model for v1.0.0: no .NET runtime in the binaries or
inside the bundle.

---

## 6. Release mechanics and sign-off

- [x] Bump `FiveDoVersion.Version` in `src/FiveDo.Core/FiveDoVersion.cs` to
      `1.0.0` (and `Directory.Build.props`).
- [x] Move the `CHANGELOG.md` `[Unreleased]` section to `1.0.0` and date it.
- [x] Write release notes: feature summary, supported platforms, supported titles,
      known issues, BIOS/disc requirements, license.
- [x] Tag the release (`v1.0.0`).
- [x] Attach the unified bundles and checksums to the release (published on GitHub:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.0.0>).
- [x] CI release workflow builds the bundles, writes `SHA256SUMS.txt`, updates the
      release, and removes obsolete per-component assets.
- [x] Update README, `docs/Status.md`, `docs/Compatibility.md`,
      `docs/KnownIssues.md`, `docs/WhatsNew.md`, and the release docs.
- [x] Record the post-1.0 backlog in `docs/ImplementationPlan.md`.

| Sign-off | Name | Date |
|---|---|---|
| Release owner | Project owner (purelogiccode) | 2026-11-02 |
| Legal / distribution | Approved: FreeDO modified GNU LGPL, donationware | 2026-11-02 |
| Compatibility / QA | Documented-compatibility scope: 593/594 render, 12-title matrix; Linux x64 full suite | 2026-11-02 |
| Packaging / CI | Unified framework-dependent bundles, no bundled runtime, release workflow | 2026-11-02 |

---

## 7. Post-1.0 backlog (explicitly not release gates)

- Finish a full Road Rash race and script Alone's inventory/exploration.
- Add input/gameplay scripts for Gex, Need for Speed, SSF2 Turbo, Total Eclipse,
  Killing Time, Theme Park, and Return Fire.
- M13.5 in-game/visual review and M13.6 high-resolution pass.
- M14 high-resolution CEL research (true 640x480, supersampling, GPU CEL feasibility).
- Continue CD/CLIO/MADAM/ARM timing triage with the Opera differential harness.
- GLES 3.1, desktop arm64 hardware, 100/150/200% DPI, and Linux/macOS
  launch validation if not claimed for v1. Raspberry Pi, Android/iOS, and WebAssembly
  are out of scope.
- Tier B 1080p60 timing, 3D CEL profiling, hot-path optimization, and the
  same-machine relative benchmark gate on stable CI hardware.
- Signed/notarized installers remain conditional on certificates/accounts. Unsigned
  Windows (Inno Setup), Linux (`.deb`/`.rpm` + desktop/MIME), and macOS
  (`.app`/`.dmg`/`.pkg`) packaging scaffolding is in `packaging/`; the Windows
  installer and Linux package metadata were validated on 2026-11-10. See
  [`Installers.md`](Installers.md). Frontend NativeAOT audio is covered by the
  2026-11-10 soak; the CLI AOT path remains validated.
- ViewModel tests and UI polish.
- Optional M12 managed ARM JIT (profile first; not needed for v1).

---

## 8. Definition of done

v1.0 is done when sections 1-6 are complete and signed off, the supported set has been
soaked, the compatibility matrix and known issues are honest, and the release artifacts
can be extracted and started on every claimed platform. Full compatibility is **not**
part of this definition. M13.5/M13.6 visual/high-resolution review and M14 research are
post-1.0.
