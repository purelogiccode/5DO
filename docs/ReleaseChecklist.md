# Release Checklist

**Adopted:** 2026-10-26
**Release timing:** **v1.13.2 is the current release**; five bundles (Windows/Linux
x64+arm64 and macOS arm64) are built from the same gates used for v1.0.0, v1.1.0, and
v1.2.x. macOS is published as a DMG. v1.13.2 is a bug-fix release for movie verification
and partial-extraction diagnostics; v1.13.0 added the native Metal surface, the
Phoenix/4DO presentation options, and GPG-signed Linux artifacts.
**Release definition:** v1.0 is a **documented-compatibility release**, not a
full-compatibility one. v1.1.0 was a maintenance update; v1.2.0 added features,
accuracy telemetry, and broader disc support; v1.2.1 added arm64 bundles; v1.2.2 added
interpreter/high-res/rewind performance, the opt-in managed JIT, library health and
database work, memory capacity options, and eight PBUS ports; v1.3.0 adds
480i/240p detection, deinterlacing, and field blending; v1.4.0 adds online cover art,
latency controls, unsigned-disc support, and the exact NTSC field clock; v1.5.0 adds
GPU HQx, the 24-bit VDL background, reference field-boundary timing, and CD parity.
v1.6.0 completes the 378-title compatibility matrix, tightens CD poll/speed/session
behavior, and hardens settings/NVRAM persistence. v1.7.0 adds CD subcode/Q, disc
conversion, focus-loss pausing, void patterns, pointer input scripts, macOS HID gamepads,
native macOS/arm64 runtime smoke, and DMG distribution for macOS. v1.7.1 removes the CHD
startup prefetch stall and audio underruns and refreshes the desktop/CLI/test
dependencies. v1.8.0 adds MMPX 2x and Catmull-Rom bicubic filtering, 5:4/16:9/16:10/21:9
aspect presets, and the .NET 11 / C# 15 toolchain.
v1.9.0 adds English/German localization, external metadata and media, physical
optical-drive boot, Windows DirectInput/HID gamepads, the controller preview, auto-crop,
display-sleep inhibition, Lua input scripting, the input timeline, and best-effort 4DO
`.4dosav` import/export. v1.9.1 adds seven UI languages, reversible locale switching, and
the two-step Escape pause/quit shortcut. v1.10.0 adds RetroAchievements with a managed
rcheevos engine, 3DO hashing, hardcore rules, a durable offline unlock queue,
hardcore-only leaderboard submission, rich presence, OSD notifications, and an
achievement window. v1.10.1 adds RetroAchievements save-state progress persistence, renames
the desktop apphost to `FiveDo`, and cuts time to the first window with deferred startup
work and ReadyToRun release bundles. It also adds the fullscreen pause/Escape flow, a localized loading overlay, and absolute release-note links. v1.10.2 adds CDI/NRG disc images and fixes the Windows bundle's OpenAL runtime loader name.
Ship the platforms and titles that are validated, publish an honest per-title matrix
and known issues, and move everything else to the post-1.0 backlog.

This checklist is the release gate. A checked box must have evidence (a command, CI
run, screenshot, or recorded hash), not just an intention. Update `docs/Status.md`,
`docs/Compatibility.md`, `docs/KnownIssues.md`, and `CHANGELOG.md` as items complete.

---

## 1.13.2 bug-fix gate

- [x] Bump `FiveDoVersion.Version`, `Directory.Build.props`, README, docs status,
      MkDocs navigation, AppStream `<release>`, changelog, and release notes to `1.13.2`.
- [x] `SYS-26`: movie verification uses the recorded emulated frame span, resolves the
      recorded disc and BIOS, and both a 600-frame disc movie and a 300-frame
      `--state-in` branch verify with exit 0.
- [x] `UX-36`: a partial extraction shows the "5DO cannot start" message with the missing
      file names and exits without a fatal crash or bug report; a complete folder still
      passes the smoke test.
- [x] CHDSharp 1.7.1 with every lock file refreshed; `dotnet restore --locked-mode`
      passes.
- [x] Full suite green (**2,449 tests**), `dotnet format`, `jb inspectcode`, and
      `mkdocs build --strict`.
- [x] Tagged release publishes the five bundles and the signed Linux assets; the
      download verification in [LinuxDistribution.md](LinuxDistribution.md) §3 passes
      from a clean download.

## 1.13.0 native Metal and signed Linux gate

- [x] Bump `FiveDoVersion.Version`, `Directory.Build.props`, README, docs status,
      MkDocs navigation, AppStream `<release>`, changelog, and release notes to `1.13.0`.
- [x] macOS: native `CAMetalLayer` presentation, windowed/resize/fullscreen, 320x240
      capture parity, and 3840x2160 offscreen output recorded in
      [Performance.md](Performance.md) §8.8 and [TestCorpus.md](TestCorpus.md) §1.1ct.
- [ ] macOS Retina 2x: unvalidated, blocked by the 1x-only NoMac display
      ([ImplementationPlan.md](ImplementationPlan.md) §4).
- [x] Presentation options: audio channel swap, named void patterns, GPU void color, and
      the emulation-health badge, each with settings round-trip tests.
- [x] Release key configured (`LINUX_GPG_PRIVATE_KEY`/`LINUX_GPG_KEY_ID`), fingerprint
      recorded in [LinuxDistribution.md](LinuxDistribution.md) §4.
- [x] CI signs every push, installs the `.deb`/`.rpm`, runs the sandboxed Flatpak
      `selftest`, and verifies the signed APT/RPM mirror trees.
- [x] Full suite green (2,439 tests), `dotnet format`, `jb inspectcode`, and
      `mkdocs build --strict`.
- [x] Tagged release publishes the bundles and the signed Linux assets, and the download
      verification in [LinuxDistribution.md](LinuxDistribution.md) §3 passes from a clean
      machine (v1.13.0: keyring imported to `E69F…4075`, good signatures on the manifest
      and the `.deb`, manifest hash matched).

## 1.12.0 macOS GPU gate

- [x] Bump `FiveDoVersion.Version`, `Directory.Build.props`, README, docs index, wiki,
      MkDocs navigation, changelog, and release notes to `1.12.0`.
- [x] Build with only the .NET 11 SDK on Windows (no `DOTNET_ROLL_FORWARD` environment
      override); the shader target rolls the net8.0 tool forward automatically.
- [x] macOS arm64: full suite **2,394/2,394**, CLI `selftest`, frontend `--smoke-test`,
      all eight Vulkan filter probes, and a 600-frame Wolfenstein 3D boot.
- [x] macOS arm64: packaged `.dmg`/`.pkg`/`.tar.gz`; CLI and frontend smoke pass from the
      tarball and the mounted DMG; SHA-256 checksums verify.
- [x] macOS evidence and limits recorded in `Compatibility.md` §10.4, `KnownIssues.md`,
      `Video.md`, `Vulkan.md`, and `ImplementationPlanArchive.md` §12.
- [x] Release apphosts switched to **self-contained** single files
      (`--self-contained true -p:PublishSingleFile=true`); the .NET 11 runtime is
      embedded and no runtime folder is staged.
- [x] Release workflow builds and publishes all five bundles and `SHA256SUMS.txt`;
      every checksum verified after download. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/44141799807>; CI run
      <https://github.com/purelogiccode/5DO/actions/runs/44144837632> and platform
      runtime run <https://github.com/purelogiccode/5DO/actions/runs/44141793058> passed.
- [x] Record the release run links and the current suite count in `Status.md`/`README.md`.

---

## Next release: Linux distribution gate

The Linux packaging and signing scripts are in `packaging/linux/` and run from
`.github/workflows/linux-packages.yml`. This gate turns them into release evidence; the
runbook and verification commands are in
[LinuxDistribution.md](LinuxDistribution.md). The unsigned build/install path was
validated by CI run
<https://github.com/purelogiccode/5DO/actions/runs/44193278546> (2026-11-17); the boxes
below still apply to the signed release.

- [ ] `LINUX_GPG_PRIVATE_KEY` and `LINUX_GPG_KEY_ID` are configured; only the signing
      subkey is stored, and its fingerprint matches `LinuxDistribution.md` §4.
- [ ] `linux-packages.yml` builds `.deb`, `.rpm`, AppImage, and Flatpak for x64 and
      arm64 from the release bundles.
- [ ] The Linux bundle contains the packager files under `packaging/` and the
      `5do.desktop` fullscreen action validates.
- [ ] `5do-cli doctor --strict` and `5do-cli bios discover --json` behave on the target host
      (the first-run guide offers the discovered BIOS when one exists).
- [ ] `desktop-file-validate`, `appstreamcli validate --no-net`, and
      `update-mime-database` pass on the shipped desktop/MIME/metainfo files.
- [ ] The AppStream `<release version="...">` entry matches `Directory.Build.props`
      and the screenshots resolve on the published docs site.
- [ ] `dpkg-deb -f` shows no `dotnet-runtime` dependency and the expected
      X11/fontconfig/freetype/GL dependency list.
- [ ] `sudo apt install ./5do_<version>_amd64.deb` succeeds on a stock Debian/Ubuntu
      host (the dependency list resolves), `5do --version` runs, and `sudo apt remove 5do`
      removes the symlinks.
- [ ] `gpg --verify` passes for `SHA256SUMS-linux.txt.asc` and every artifact `.asc`;
      `sha256sum -c SHA256SUMS-linux.txt` passes from a clean download.
- [ ] `rpm --checksig` passes for any header-signed package, and
      `sudo dnf install ./5do-<version>-1.x86_64.rpm` succeeds on Fedora.
- [ ] `flatpak install --user ./5do-<arch>.flatpak` succeeds and
      `flatpak run --command=5do-cli io.github.purelogiccode.FiveDO selftest --json`
      passes inside the sandbox on a matching host.
- [ ] The release page lists the `.deb`/`.rpm` files, both AppImages (+ `.zsync`), both
      Flatpak bundles, the keyring, and both checksum manifests; no repository is hosted
      or required.
- [ ] The key fingerprint, run links, and install commands are recorded in
      [Installers.md](Installers.md), [TestCorpus.md](TestCorpus.md), and the release
      notes; any unsigned run is labeled "unsigned".

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

## 1.7.0 platform and media-polish gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.7.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.7.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, `docs/Installation.md`, and
      `docs/Installers.md`.
- [x] Full suite **2,124/2,124** passes in Debug;
      `dotnet format --verify-no-changes` is clean;
      `jb inspectcode --severity=WARNING` reports 0 results.
- [x] Verify CD subcode/Q, the 2449-byte wire size, CD-ROM state v6 with v5
      compatibility, `disc convert`, focus-loss pausing, void patterns, VDLP pixel
      formats, pointer input scripts, and the macOS HID mapper are covered by tests.
- [x] Verify the extraction bug fixes: CHD conversion preserves a same-stem BIN and a CUE
      output cannot overwrite the source BIN.
- [x] Build all six framework-dependent bundles and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release` without deleting older artifacts.
      Local copies: `5do-1.7.0-{win-x64,win-arm64}.zip`,
      `5do-1.7.0-{linux-x64,linux-arm64}.tar.gz`, and
      `5do-1.7.0-{osx-x64,osx-arm64}.dmg`. CI run
      <https://github.com/purelogiccode/5DO/actions/runs/42877150967> passed every
      required job, including both native macOS DMG builds.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the 1.7.0 release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files. CI and the local PE/ELF checks passed.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`
      (`FRONTEND SMOKE OK 1.7.0`); CI verifies the arm64/macOS apphost machine types and
      publishes the macOS DMG. Native runtime smoke run
      <https://github.com/purelogiccode/5DO/actions/runs/42876332881> passed macOS
      x64/arm64, Linux arm64, and Windows arm64.
- [x] Create and push tag `v1.7.0`; the release workflow published six versioned bundles
      (macOS as DMG) and `SHA256SUMS.txt`:
      <https://github.com/purelogiccode/5DO/releases/tag/v1.7.0>. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/42878260384> passed all six
      bundle jobs and the publish job. All six published checksums verify locally, and
      the release assets were copied into
      `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for the
      1.7.0 release page, What's New, release notes, and wiki page. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/42878398641> passed build,
      Pages deploy, and wiki publish.

---

## 1.7.1 desktop-startup and dependency gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.7.1`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.7.1.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, and `docs/Installation.md`.
- [x] Include the dependency updates: Avalonia 12.1.6, System.CommandLine 3.0.0, and
      Microsoft.NET.Test.Sdk 18.11.1.
- [x] Route CHD readers directly to CHDSharp read-ahead and keep the sector prefetcher
      below normal priority with a yield between sectors.
- [x] Full suite **2,125/2,125** passes in Debug; `dotnet format --verify-no-changes`
      is clean; `jb inspectcode --severity=WARNING` reports 0 results.
- [x] Verify desktop Road Rash, Lemmings, and Space Ace runs report no sustained audio
      underruns and no skipped frames after startup.
- [x] Build all six framework-dependent bundles from the `v1.7.1` tag and keep local
      copies in `src/FiveDo.Frontend.Avalonia/bin/Release` without deleting older
      artifacts.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the 1.7.1 release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files.
- [x] Create and push tag `v1.7.1`; the release workflow publishes six versioned bundles
      and `SHA256SUMS.txt`. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/42898378598> passed all six
      bundle jobs and the publish job at
      <https://github.com/purelogiccode/5DO/releases/tag/v1.7.1>; all six checksums
      verify and the assets were copied into
      `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for the
      1.7.1 release. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/42897688864> passed build,
      Pages deploy, and wiki publish; `WhatsNew.html`, `ReleaseNotes-1.7.1.html`, and
      `wiki/Release-1.7.1` return HTTP 200.

## 1.8.0 filters and toolchain gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.8.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.8.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, `docs/Installation.md`,
      `docs/ImplementationPlan.md`, `docs/KnownIssues.md`, `docs/Roadmap.md`, and the wiki.
- [x] Move the SDK pin to `11.0.100`, target `net11.0`, use C# 15, and update all
      workflows, tools, and build paths from `net10.0` to `net11.0`.
- [x] Add MMPX 2x and Catmull-Rom bicubic filtering in OpenGL/GLES, Direct3D 11, Vulkan,
      and the CPU fallback; add 5:4/16:9/16:10/21:9 aspect presets.
- [x] Fix the invalid `global.json` version, the MMPX luma tie-break/triangle-tip rules,
      and the OpenGL MMPX finishing sampler.
- [x] Full suite **2,158/2,158** passes in Debug; `dotnet format --verify-no-changes`
      is clean; `jb inspectcode --severity=WARNING` reports 0 results.
- [x] Local win-x64 framework-dependent bundle verified: CLI `--version`/`selftest`,
      frontend `FRONTEND SMOKE OK 1.8.0`, `whatsnew.md`/release notes present, and no
      runtime, PDB, or XML files.
- [x] Build all six framework-dependent bundles from the `v1.8.0` tag and keep local
      copies in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the 1.8.0 release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files.
- [x] Create and push tag `v1.8.0`; the release workflow published six versioned bundles
      and `SHA256SUMS.txt` at
      <https://github.com/purelogiccode/5DO/releases/tag/v1.8.0>. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/42959751665> passed all six
      bundle jobs and the publish job; all six checksums verify and the assets are kept
      in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] CI run <https://github.com/purelogiccode/5DO/actions/runs/42958451196> passed
      every required job: build/test on Windows/Linux/macOS, trimmed CLI/frontend,
      both NativeAOT apphosts, the six bundle jobs, and the aggregate gate. Platform
      runtime run
      <https://github.com/purelogiccode/5DO/actions/runs/42958451118> passed macOS
      x64/arm64, Linux arm64, and Windows arm64.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for the
      1.8.0 release, including `WhatsNew.html`, `ReleaseNotes-1.8.0.html`, and
      `wiki/Release-1.8.0`. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/42957782832> passed build,
      Pages deploy, and wiki publish.

---

## 1.9.0 feature and interoperability gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.9.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.9.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, `docs/Installation.md`,
      `docs/ImplementationPlan.md`, `docs/KnownIssues.md`, `docs/Roadmap.md`, the MkDocs
      navigation, and the wiki.
- [x] Add English/German localization, `--locale`/`--list-locales`, Settings > General >
      UI language, and the translated smoke marker.
- [x] Add external metadata lookup, the deterministic cache, and manual media
      attachments in the CLI and Game Metadata window.
- [x] Add physical optical-drive boot, the Windows DirectInput/HID gamepad backend,
      controller preview, auto-crop, and display-sleep inhibition.
- [x] Add Lua input scripting, the input timeline, and best-effort 4DO `.4dosav`
      import/export.
- [x] Fix the invalid FILETIME scan abort, the OpenGL FBO fallback, and the metadata,
      optical-handle, and DirectInput lifetime defects found in review.
- [x] Full suite **2,252/2,252** passes in Debug; `dotnet restore --locked-mode`,
      `dotnet build`, and `dotnet format --verify-no-changes` are clean; a clean-cache
      `jb inspectcode --severity=WARNING` reports 0 results.
- [x] Local win-x64 framework-dependent bundle verified: CLI `--version`/`selftest`,
      frontend `FRONTEND SMOKE OK 1.9.0`, `whatsnew.md`/release notes present, and no
      runtime, PDB, or XML files.
- [x] Build all six framework-dependent bundles from the `v1.9.0` tag and keep local
      copies in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] Verify each bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
      `whatsnew.md`, the 1.9.0 release notes, and the legal notices, and contains no
      `runtime/`, `*.pdb`, or `*.xml` files.
- [x] Create and push tag `v1.9.0`; release run
      <https://github.com/purelogiccode/5DO/actions/runs/43558324543> built and
      published six versioned bundles and `SHA256SUMS.txt` at
      <https://github.com/purelogiccode/5DO/releases/tag/v1.9.0>. All six checksums
      verify and the assets are kept in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] CI run <https://github.com/purelogiccode/5DO/actions/runs/43558322127> passed
      every required job: build/test on Windows/Linux/macOS, the RID-agnostic lock
      check, format, benchmark gates, trimmed CLI/frontend, both NativeAOT apphosts,
      the six bundle jobs, and the aggregate gate.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for the
      1.9.0 release. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/43557896567> passed;
      `WhatsNew.html`, `ReleaseNotes-1.9.0.html`, and `wiki/Release-1.9.0` return
      HTTP 200.

---

## 1.9.1 localization and quality-of-life gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.9.1`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.9.1.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, `docs/Installation.md`, the
      MkDocs navigation, and the curated wiki pages.
- [x] Add Brazilian Portuguese, French, Spanish, Italian, Japanese, Korean, and
      Simplified Chinese catalogs; every translated catalog covers every non-tooltip
      key and the core shell tooltips (591 keys each).
- [x] Fix the language-switch regression so selecting English after German restores the
      labels in the same session, including when `--locale` was used for the launch.
- [x] Implement the two-step Escape shortcut: first press pauses, second press closes
      5DO; fullscreen, quick-menu, library, text entry, resume/stop, and focus changes
      keep priority. `EscapeQuitPolicyTests` cover the sequence.
- [x] Full suite **2,259/2,259** passes in Debug and Release;
      `dotnet restore --locked-mode`, `dotnet build`,
      `dotnet format --verify-no-changes`, and `jb inspectcode --severity=WARNING`
      report no warnings or errors.
- [x] Build all six framework-dependent bundles from the `v1.9.1` tag, verify contents
      and apphost architectures, and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release`. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/43566963957> built and
      published them.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`
      (`FRONTEND SMOKE OK 1.9.1`) and translated smoke markers
      (`locale=fr`, `firstMenuHeader=Fichier`; `locale=ja`,
      `firstMenuHeader=ファイル`).
- [x] Create and push tag `v1.9.1`; the release workflow published six versioned bundles
      and `SHA256SUMS.txt` at
      <https://github.com/purelogiccode/5DO/releases/tag/v1.9.1>. All six checksums
      verify locally.
- [x] CI run <https://github.com/purelogiccode/5DO/actions/runs/43565870407> passed
      every required job after one Windows ILLink rerun: Windows/Linux/macOS build-test,
      the RID-agnostic lock check, format, benchmark gates, trimmed CLI/frontend, both
      NativeAOT apphosts, the six bundle jobs, and the aggregate gate.
- [x] Confirm the docs workflow deploys the Pages site and publishes the wiki for
      `WhatsNew.html`, `ReleaseNotes-1.9.1.html`, and `wiki/Release-1.9.1`. Docs run
      <https://github.com/purelogiccode/5DO/actions/runs/43565870395> passed.

---

## 1.10.2 CDI/NRG and Windows OpenAL patch gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.10.2`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.10.2.md`,
      `README.md`, `docs/index.md`, `docs/Installation.md`, `docs/FAQ.md`,
      `docs/Roadmap.md`, `docs/Status.md`, `docs/KnownIssues.md`,
      `docs/Installers.md`, `docs/TestCorpus.md`, `docs/ImplementationPlan.md`, and the
      MkDocs navigation.
- [x] Full suite **2,365/2,365**; build, locked restore, format, and InspectCode clean.
- [x] Publish win-x64 CLI and frontend; verify `OpenAL32.dll` and `soft_oal.dll` are
      beside `FiveDo.Cli.exe` and `FiveDo.exe`.
- [x] Run the published CLI with `ALSOFT_LOGLEVEL=3`; the log reports OpenAL Soft
      `1.23.1`, and `audio devices --json` lists the `OpenAL Soft on ...` endpoints.
- [x] Verify `package-bundle.ps1` rejects a publish tree without `OpenAL32.dll` and
      includes both DLLs in the local `5do-1.10.2-win-x64.zip`.
- [x] Run the published frontend `--smoke-test`; the marker reports
      `FRONTEND SMOKE OK 1.10.2`.
- [x] Push tag `v1.10.2`; release run
      <https://github.com/purelogiccode/5DO/actions/runs/43776600683> built and
      published the six bundles and `SHA256SUMS.txt`. All six published hashes verify,
      and the assets are copied into `src/FiveDo.Frontend.Avalonia/bin/Release`.
- [x] CI run <https://github.com/purelogiccode/5DO/actions/runs/43776600895> passed
      after the transient win-x64 artifact-upload retry.
- [x] Docs run <https://github.com/purelogiccode/5DO/actions/runs/43778248201>
      deployed the Pages site and wiki. `WhatsNew.html`,
      `ReleaseNotes-1.10.2.html`, and `wiki/Release-1.10.2` return HTTP 200.

## 1.10.1 RetroAchievements continuity and startup gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.10.1`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.10.1.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, `docs/Installation.md`,
      `docs/KnownIssues.md`, `docs/BuildAndTest.md`, `docs/FAQ.md`,
      `docs/ImplementationPlan.md`, `docs/Roadmap.md`,
      `docs/RetroAchievementsCompliance.md`, and the MkDocs navigation.
- [x] Persist achievement hit counts, trigger state, measured values, delta/prior memref
      values, and leaderboard attempts in a dedicated `RetroAchievements` `.tdos`
      section; validate the game hash, IDs, and definition hashes before applying it.
- [x] Wire slot saves, quick saves, and slot loads through
      `ExportProgress`/`ImportProgress`, and reset the runtime when the section is
      missing or incompatible.
- [x] Rename the frontend apphost to `FiveDo`, and update the Windows installer, Linux
      desktop entry/DEB/RPM scripts, macOS app bundle, packaging, CI smoke commands,
      probe tests, and documentation.
- [x] Defer audio initialization, BIOS/disc loading, and metadata/disc-health caches;
      background the application-stats request.
- [x] Enable ReadyToRun for single-file, untrimmed frontend publishes and verify
      win-x64, win-arm64, and linux-x64 builds.
- [x] Make Escape leave fullscreen and pause a running game in one step; the next
      Escape closes 5DO. Keep F11 as the independent fullscreen toggle.
- [x] Show a localized **Loading game... / Please wait...** overlay from a fresh game
      start until the first visible frame is presented, with a 30-second safety timeout.
- [x] Convert every release-note Markdown link to an absolute GitHub URL and update the live v1.10.0 release body.
- [x] Full suite **2,344/2,344** passes in Debug; `dotnet build`,
      `dotnet format --verify-no-changes`, and
      `jb inspectcode --severity=WARNING` report no warnings or errors.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`
      (`FRONTEND SMOKE OK 1.10.1`) from the built bundle, and confirm the renamed
      `FiveDo.exe` apphost.
- [x] Build all six framework-dependent bundles from the `v1.10.1` tag, verify contents
      and apphost architectures, and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release`. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/43760492909> built and
      published them.
- [x] Create and push tag `v1.10.1`; the release workflow published six versioned
      bundles and `SHA256SUMS.txt` at
      <https://github.com/purelogiccode/5DO/releases/tag/v1.10.1>. All six checksums
      verify locally.
- [x] CI run <https://github.com/purelogiccode/5DO/actions/runs/43760004443> passed
      every required job: Windows/Linux/macOS build-test, the RID-agnostic lock check,
      format, benchmark gates, trimmed CLI/frontend, both NativeAOT apphosts, the six
      bundle jobs, and the aggregate gate. Platform runtime validation run
      <https://github.com/purelogiccode/5DO/actions/runs/43760004576> passed.
- [x] Docs run <https://github.com/purelogiccode/5DO/actions/runs/43760004392> deployed
      the Pages site and published the wiki. `WhatsNew.html`,
      `ReleaseNotes-1.10.1.html`, and `wiki/Release-1.10.1` return HTTP 200.
- [ ] Submit the compliance package to RetroAchievements and complete their review.
- [ ] After approval, make `RetroAchievementsCompliance.IsHardcoreApproved()` return
      `true`, remove the pending wording, and run a live hardcore award check.

---

## 1.10.0 RetroAchievements gate

- [x] Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.10.0`.
- [x] Update `CHANGELOG.md`, `docs/WhatsNew.md`, `docs/ReleaseNotes-1.10.0.md`,
      `README.md`, `docs/Status.md`, `docs/index.md`, `docs/Installation.md`,
      `docs/KnownIssues.md`, `docs/Privacy.md`, `docs/Configuration.md`,
      `docs/RetroAchievements.md`, `docs/RetroAchievementsCompliance.md`, and the
      MkDocs navigation.
- [x] Implement the managed rcheevos engine, 3DO `rc_hash_3do` hashing, Web API client,
      per-frame evaluation, OSD notifications, rich presence, leaderboards, hardcore
      rules, and the durable offline unlock queue.
- [x] Verify the 3DO hash byte-for-byte against RAHasher 1.8.3 on real CHD images.
- [x] Enforce hardcore rules: block save-state loading, rewind, frame advance, cheats,
      TAS/movie playback and recording, input recording, the movie editor, and the input
      timeline; allow fast-forward and creating save states; reset the game before a
      casual-to-hardcore session starts.
- [x] Submit leaderboard entries only from hardcore sessions and keep casual-only
      achievement unlocks active in hardcore.
- [x] Isolate achievement, notification, and frame-callback failures from the emulation
      loop.
- [x] Full suite **2,338/2,338** passes in Debug; `dotnet build`,
      `dotnet format --verify-no-changes`, and
      `jb inspectcode --severity=WARNING` report no warnings or errors.
- [x] Verify the x64 CLI `selftest` and desktop `--smoke-test`
      (`FRONTEND SMOKE OK 1.10.0`).
- [x] Build all six framework-dependent bundles from the `v1.10.0` tag, verify contents
      and apphost architectures, and keep local copies in
      `src/FiveDo.Frontend.Avalonia/bin/Release`. Release run
      <https://github.com/purelogiccode/5DO/actions/runs/43733549470> and packaging
      refresh run <https://github.com/purelogiccode/5DO/actions/runs/43735694120> built
      and published them.
- [x] Create and push tag `v1.10.0`; the release workflow published six versioned bundles
      and `SHA256SUMS.txt` at
      <https://github.com/purelogiccode/5DO/releases/tag/v1.10.0>. All six checksums
      verify locally.
- [x] CI run <https://github.com/purelogiccode/5DO/actions/runs/43735222579> passed
      every required job: Windows/Linux/macOS build-test, the RID-agnostic lock check,
      format, benchmark gates, trimmed CLI/frontend, both NativeAOT apphosts, the six
      bundle jobs, and the aggregate gate. The final docs-only commit also passed CI run
      <https://github.com/purelogiccode/5DO/actions/runs/43736567771>.
- [x] Docs run <https://github.com/purelogiccode/5DO/actions/runs/43736567863> deployed
      the Pages site and published the wiki. `WhatsNew.html`, `ReleaseNotes-1.10.0.html`,
      and `wiki/Release-1.10.0` return HTTP 200.
- [ ] Submit the compliance package to RetroAchievements and complete their review.
- [ ] After approval, make `RetroAchievementsCompliance.IsHardcoreApproved()` return
      `true`, remove the pending wording, and run a live hardcore award check.

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
| Secondary desktop builds | **Linux x64** (full suite and CLI/frontend smoke passed; GL preview); macOS x64/arm64 native runtime smoke passed | Linux GPU presentation is unvalidated under llvmpipe; Windows GL remains the validated renderer |
| Desktop arm64 | **Native runtime smoke passed** on macOS arm64, Linux arm64, and Windows arm64 | GLES 3.1, Tier B timing, and DPI are unvalidated; Raspberry Pi is out of scope |
| Compatibility set | **414 unique titles / 378 game-test-set titles**; 375 playable under the binary beyond-title rule | Per-title status must stay honest and evidenced |
| Disc formats | ISO, BIN, CUE+BIN, CHD | Already implemented and tested |
| Renderer | OpenGL default, CPU fallback | Filter family is complete |
| Packages | One bundle per system and architecture | `5do-<version>-win-x64.zip`, `5do-<version>-win-arm64.zip`, `5do-<version>-linux-x64.tar.gz`, `5do-<version>-linux-arm64.tar.gz`, `5do-<version>-osx-x64.dmg`, `5do-<version>-osx-arm64.dmg`; each contains the CLI, desktop app, README/license/notices/whatsnew. The .NET 11 runtime is a host prerequisite and is not bundled |
| Distribution model | **Donationware** | Non-commercial, source-available; voluntary donations only, no sales |
| Version | **`v1.10.1`** | Bump `FiveDoVersion.Version` and `Directory.Build.props`; v1.0.0 through v1.10.0 are historical |
| Release timing | **Released 2026-11-15** | v1.10.1 bundles (macOS as DMG) and checksums are attached to the GitHub release |

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
- [x] Native platform runtime smoke passes on macOS x64/arm64, Linux arm64, and
      Windows arm64 (2026-11-10, run 42770783778): CLI `selftest` and frontend
      `--smoke-test` on each runner.

Suggested commands:

```powershell
dotnet build 5DO.sln -c Release
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release
dotnet format 5DO.sln --verify-no-changes
jb inspectcode 5DO.sln --severity=WARNING --no-build
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- --frames 600 --gate
gh workflow run platform-runtime.yml
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
- [x] True 640x480 prototype is validated or rejected with evidence (post-1.0).
  VID-19 delivered the opt-in `--true-2x` CEL path on 2026-11-16; see
  [`TestCorpus.md`](TestCorpus.md) 1.1cl.
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
- [x] Do **not** copy the .NET runtime into the bundle. Users must install the .NET 11
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
      the .NET 11 runtime requirement, launch commands, and the no-BIOS/no-disc notice.
- [x] No runtime folder, PDBs, XML docs, or copyrighted content in release bundles.
- [x] Extract each bundle to a clean folder and run the CLI/frontend smoke test.
- [x] SHA-256 checksums generated for every bundle.
- [x] CI builds the Windows/Linux bundles on their native runners, cross-publishes the
      macOS x64 bundle on Linux, verifies that no runtime or XML files are present, and
      publishes them with the checksums; the docs workflow updates GitHub Pages and the
      wiki.
- [x] Local copies are written to `src/FiveDo.Frontend.Avalonia/bin/Release`.

### 5.1 Single-file decision (changed in 1.12.0)

The v1.0.0 distribution model was framework-dependent: users installed the .NET 11
runtime separately. **As of 1.12.0 the release uses self-contained single-file
apphosts.** Each app embeds the .NET 11 runtime (roughly 70-100 MB before compression),
so a download runs without a separate runtime install. The bundle still contains no
`runtime/` folder because the runtime is inside the single-file executable. This is the
required distribution model for 1.12.0 and later.

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
