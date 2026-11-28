# 5DO — Missing Features

**Snapshot:** 2026-11-28
**Release:** v1.20.0
**Status:** the compatibility program is complete. This file tracks **remaining work
only**; completed items, their evidence, the full stable-ID history, and the completed
portions of partially open IDs are in
[ImplementationPlanArchive.md](ImplementationPlanArchive.md) Part II and §17.
**Open items:** 12 - 2 P0 compliance/evidence, 3 platform/presentation, 3 release/packaging,
2 rendering, 2 hardware/media.

> **Filter rule.** An item belongs in §3 only when it can be completed and evidenced with
> the current 378-title corpus, the current codebase, and obtainable hardware or CI. Items
> blocked by licensing, missing hardware, unavailable reference captures, or open-ended
> research are listed in §4 and are **not** checkboxes.
>
> **Evidence rule.** A task is complete only when its **Done when** line is satisfied and
> the capture, hash, frame count, BIOS/disc identity, test, or release artifact is
> committed. Completed rows and shipped sub-features move to the archive; they are not
> kept here.

---

## 1. Completed baseline

No remaining §3 item is fully implemented; every open ID still has a remaining `Done when`
condition. The completed `REL-13`/`REL-12`/`SYS-28`/`VID-22`/`UX-33`/`SYS-25`/`UX-38` rows are in
[ImplementationPlanArchive.md](ImplementationPlanArchive.md) §12, completed sub-features of
the still-partial IDs are in §17, and the completed baseline, results, stable-ID history,
and release history are in §10–§16. Do not reopen a completed ID without new measurement.

---

## 2. How the remaining backlog works

- **Priority:** **P0** compliance/player-visible quality/evidence, **P1** release/platform
  hardening, **P2** accuracy features, **P3** product features.
- **Effort:** **S** under 3 developer-days, **M** 3–10, **L** 10–30, **XL** 30–90+.
- **Status:** **Open** (not started or in progress), **Partial** (a usable subset ships),
  **Candidate** (probe first, commit only on evidence), **Awaiting hardware** (code and
  tests are complete; only the named device evidence is missing), **Awaiting external
  review** (a third party must approve), **Awaiting certificate** (release signing is
  blocked on a purchased certificate/account).
- **One feature per change.** Do not mix accuracy, rendering, and UX work in one change.
- **One root cause per fix.** Every fix starts from a failing title, test, or panel row.
- **No speculative work.** If a task cannot change a status, a gate, or a release artifact,
  it does not belong in P0/P1.
- **No duplicate entries.** Reference an ID instead of restating the same work.
- **Partial rows list remaining work only.** Shipped sub-features are recorded in the
  archive §17; do not re-narrate them as missing work.
- **Blocked items stay blocked.** Moving an item from §4 to §3 requires the named blocking
  condition to be resolved first.

---

## 3. Missing features — achievable backlog

### 3.0 P0/P1 — Compliance and long-run evidence

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **REL-14** | Awaiting external review | S | **RetroAchievements hardcore client approval**: obtain RetroAchievements' approval, flip the switch, remove the pending wording, and record a live hardcore award. The client code path and rules are in archive §17. | The remaining step is RetroAchievements' external review, not 5DO code. The approval switch and live test are already scoped. | The approval is recorded; `RetroAchievementsCompliance.IsHardcoreApproved()` returns `true`; the pending wording is removed from KnownIssues/FAQ/README; a live hardcore award is recorded. |
| **TEST-09** | Open | L | **Full-library extended soak**: run the whole library, not just the beyond-title checkpoint. Use every one of the **1,371 images** for a minimum of **10,000 frames** each, and every **414 unique title** (the 378-title game/homebrew set plus 36 secondary media) for a minimum of **30,000 frames**, with 60,000 frames for known-slow boots and scripted input where one exists. Record frame hash, PC, audio RMS/peak, exit status, warnings, and a final-frame capture; compare against a committed baseline and investigate every difference. This is longer coverage, not a claim of full-game completion. | The headless runner, frame/audio hashes, input scripts, and the 378-title evidence ledger already exist. At roughly 230 fps headless, 10,000 frames is about 43 seconds per image and 30,000 frames is about 130 seconds per unique title, so the two-tier pass is roughly one to two days on one machine and shards cleanly across CI workers. | A committed `docs/data/full-library-soak.csv` covers **1,371/1,371 images** and **414/414 unique titles**; 0 crashes and 0 loadability/status regressions; every hash/audio difference from the baseline is explained or fixed; the pass is wired into a weekly or release gate; results are summarized in [TestCorpus.md](TestCorpus.md) and [Compatibility.md](Compatibility.md). |

### 3.1 P1 — macOS platform

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **PLAT-17** | Partial | S | **macOS Retina 2x row**: validate the shell on a HiDPI display; the paravirtual display advertises 11 modes that are all 1:1, so `backingScaleFactor` stays 1.0 even for a `CGVirtualDisplay` created with `hiDPI`. The 1x matrix is in archive §17. | `PixelSizeTests` and the `MacMetalSurface` drawable math already cover points × backing scale; a HiDPI display would exercise the same path end to end. | A run on a HiDPI display records `scale 2.0`, a drawable at twice the points, and an unstretched 4:3 viewport in [Performance.md](Performance.md) §8.8. |
| **PLAT-05** | Awaiting hardware | S | **macOS gamepad physical detection**: detect and map a physical controller through the existing eight-player profile. The IOKit HID source and 21 mapping/profile tests are in archive §17; only the host is missing. | Only the physical check needs a macOS host with a connected controller; the mapping is already unit-tested. | A macOS gamepad is detected and mapped through the existing eight-player profile. |

`PLAT-17` keeps only its Retina 2x row open; the other macOS presentation IDs are in the
archive.

### 3.2 P1 — Presentation and performance validation

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **PLAT-06** | Partial | M | **GLES 3.1 and Tier B timing validation**: run real GLES 3.1 hardware and a real Tier B percentile run. The earlier DPI/ANGLE/4K matrix is in archive §17. | The renderer and benchmark harness exist; only the named hardware is missing. | Results are recorded in [Performance.md](Performance.md) §8.8, with every residual labeled `validated` or `best-effort`. |

### 3.3 P1 - Release and packaging

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **REL-08** | Open | M | **Third-party channel listings**: publish the AUR package, AppImageHub entry, and itch.io project page under the retained non-commercial terms. The PKGBUILD and packager-facing files are in archive §17. | These channels accept a repackaged upstream release and need no code change; the catalog entries are small, reversible files. | `yay -S 5do-bin` installs and launches on Arch, the AppImageHub entry appears in the catalog, and the itch.io page serves the current AppImage with the FreeDO notice and GPL-2.0 database text. |
| **REL-09** | Partial | L | **Flatpak sandbox and self-hosted distribution**: verify gamepad, audio, and display-sleep inhibit inside the sandbox, then add a self-hosted repository (operator-owned key). Flathub is **not eligible** under the retained non-commercial terms. The bundle install, arm64 runtime test, and CI sandboxed selftest are in archive §17. | Flatpak is the channel GNOME Software, KDE Discover, and the Steam Deck Discover store read; the runtime supplies GL/audio and the manifest only repackages the self-contained apphosts. | `flatpak install --user ./5do-<arch>.flatpak` then `flatpak run io.github.purelogiccode.FiveDO` launches 5DO with gamepad, audio, and display-sleep inhibit working, and CI runs `5do-cli selftest` inside the sandbox; the self-hosted repository is documented and the Flathub-ineligible decision is recorded in [ReleaseLegalReview.md](ReleaseLegalReview.md). |
| **REL-10** | Partial | S | **Steam Deck and EmuDeck integration**: add the upstream EmuDeck/RetroDeck 3DO entry, which needs their repository. The Deck first-run layout, gamescope detection, and setup documentation are in archive §17. | evdev gamepads and display-sleep inhibit are validated on Linux hardware; the rest is presentation and documentation. | A Deck user installs from the AppImage or EmuDeck, boots a game with a controller, and reaches gameplay without a keyboard; the setup steps are in [Installation.md](Installation.md). |

Release signing and notarization remain blocked (§4). Evidence for the current bundles
and channel files: [Installers.md](Installers.md),
[LinuxDistribution.md](LinuxDistribution.md).

### 3.4 P3 — Product features

The former P3 product item, **direct-IP netplay (`NET-01`)**, is complete and moved to
archive §17. An optional session token/authentication layer and a two-window physical
controller smoke are post-release hardening. **NET-02** now tracks the lobby/NAT
prototype; more than two players and pointer devices remain out of scope.

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **NET-02** | Partial | M | **VPS lobby and NAT traversal prototype**: emulator-agnostic ASP.NET Core 11 coordinator, room/join codes, WebSocket signaling, TCP control relay, UDP punch coordination, UDP relay fallback, 5DO CLI `--lobby`, `FiveDo.LobbyProbe`, HTTP/UDP rate limits, per-IP room quotas, relay caps, and health monitoring. Remaining: XGrid client integration, persistence, and fully automated origin-certificate renewal. | The control plane, relays, 5DO transport, and headless probe are implemented; IIS/Cloudflare deployment is validated on the Windows VPS; direct NAT between the Azure host and a home network passed; a 600-frame 5DO relay session matched the reference hash; the public service was validated end to end on 2026-11-27 (room lifecycle, WebSocket signaling, TCP relay, UDP relay, cache bypass, origin lock); the suite is 2,688/2,688; the protocol and hardening are documented in [Lobby.md](Lobby.md). | Two 5DO peers complete a lobby-hosted session from different NATs with direct and relay evidence; the probe and lobby tests pass; an XGrid headless client joins the same room contract. |

### 3.5 P2/P3 - Rendering completeness

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **VID-20** | Candidate | L | **True 2x production completeness**: add bitmap/SPORT high-resolution sampling and framebuffer-read semantics, validate per title across the 378-title corpus. The `--true-2x` base path, gate removal, and OpenGL upload fix are in archive §17. | The existing path already writes independent 480-line fields and matches the reference on a 3D and a 2D title; [HighResolutionCelResearch.md](HighResolutionCelResearch.md) scopes the remaining bitmap/SPORT work, and the existing frame-hash harness can gate every step. This is a bounded production slice of `VID-01`, not open-ended 4K research. | The 50-title accuracy panel and 378-title corpus run with true 2x where validated, native frame/state/audio hashes stay unchanged, Road Rash transitions show no stale rows, the mode is available to every title or documented per title, and the results are recorded in [HighResolutionCelResearch.md](HighResolutionCelResearch.md) and [Compatibility.md](Compatibility.md). |
| **VID-21** | Candidate | XL | **GPU 3D CEL rasterization**: move packed, scaled, and arbitrary CEL rasterization to the GPU with palette/CLUT-correct output, texture sampling/filtering, SPORT fill/copy interaction, and a CPU fallback. Keep the CPU rasterizer as the accuracy oracle and compare every stage against it. | 5DO already owns the CEL command parser, CLUT/VRAM layout, and GL/Vulkan/D3D11 presenters, and Phoenix demonstrates that GPU CEL rendering is feasible; the 50-title panel and true-2x captures provide the parity oracle. This is a large, high-risk rendering project, so it starts as a probe. | A GPU path matches the CPU reference on a defined CEL-heavy subset (at least the 50-title panel at frame 300) with zero pixel differences, the default path is unchanged, all three backends render the same frame, the CPU fallback remains, and the measured frame time improves on a named title; otherwise the probe is closed with evidence. |

### 3.6 P3 - Hardware evidence and disc formats

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **SYS-23** | Awaiting hardware | M | **Windows DirectInput/generic HID gamepad physical check**: detect and map a physical DirectInput-only pad. The backend, duplicate filtering, trigger calibration, XInput fallback, and axis/POV/trigger tests are in archive §17. | `IRawGamepadSource`, `PlatformRawGamepadSource`, and `GamepadMapper` already abstract and test the mapping; a Windows source is additive. | A DirectInput-only pad is detected and mapped on Windows; axis/POV/trigger tests pass; XInput remains the default fallback; the backend and device selection are documented in [Configuration.md](Configuration.md). |
| **SYS-29** | Partial | M | **Representative real-image validation**: the read-only Alcohol MDS/MDX and CloneCD CCD/IMG/SUB readers, factory/scanner/file-picker wiring, and synthetic/malformed tests are complete (archive §17). Remaining: open representative real 3DO MDS/MDF and CCD/IMG/SUB images, list tracks, verify sectors, and compare the 600-frame hash/PC with the equivalent BIN/CUE. | The `DiscImageFactory`, sector normalization, track table, and `disc info`/`tracks`/`verify` commands already exist, and MDSSharp/CCDSharp supply the container parsers. The new readers are additive and do not touch emulation state. | Representative MDS/MDF and CCD/IMG/SUB images open, list tracks, verify sectors, and boot to the same 600-frame hash/PC as the equivalent BIN/CUE; malformed inputs fail with typed errors; tests and `CliReference.md`/`KnownIssues.md` document the new formats. |

---

## 4. Blocked, residual, or not achievable (do not add checkboxes)

These items stay out of §3 until the named condition changes. “Residual” means the item
was measured and closed with a documented limitation, not that it is unachievable.

| Item | Why it is not in the achievable backlog | Revisit if |
|---|---|---|
| **SYS-05 embedded boot ROM** | GPL-2.0-only licensing risk; no distributable ROM exists. Retail titles still need a user BIOS. | Legal sign-off and a distributable ROM exist. |
| **BIOS-less/HLE boot** | Explicitly out of scope for 1.x. 5DO uses the real BIOS boot path; the Opera-style BIOS-less setup is not adopted, and adding an HLE boot would need a separate compatibility matrix and licence review. | Product scope changes and an HLE boot can be validated against the real-BIOS oracle. |
| **UX-16 signing/notarization** | A Windows code-signing certificate purchase is planned; an Apple developer account is still required for macOS notarization. The current release artifacts are unsigned ZIP/DMG/tar.gz bundles, and the Windows Inno Setup installer is built but not published. | The Windows certificate and Apple account are available, and the signed artifacts pass the release gate. |
| **Official Linux archives (Debian, Ubuntu, Fedora, openSUSE)** | The FreeDO notice forbids commercial use without the rights holders' approval, which fails the Debian DFSG and Fedora legal tests; the archive builders also run without network access, so the NuGet graph must be vendored. The 2026-11-25 `REL-13` decision retained those terms, so the archives and Flathub remain closed; direct downloads plus the optional self-hosted mirror tooling (`packaging/linux/build-*-repo.sh`) are the supported paths. | The FreeDO rights holders grant a licence compatible with the archives **or** the restricted core is replaced, **and** a network-free, vendored source build exists. |
| **Intel macOS** | **Dropped in 1.12.0.** The rented NoMac host is arm64 and there is no Intel Mac with a usable Metal device; the project supports Apple Silicon macOS only. | Product scope changes and an Intel Mac becomes available for validation. |
| **PLAT-17 Retina 2x row** | The rented NoMac host's paravirtual display advertises 11 modes that are all 1:1, and a private `CGVirtualDisplay` created with `hiDPI` is also handed 1x modes, so `backingScaleFactor` stays 1.0 and the shell never sees scale 2. Every other PLAT-17 row is validated (2026-11-18). | A macOS host with a HiDPI display, or a virtual display that advertises 2x modes, is available. |
| **PLAT-19 native Wayland Vulkan surface** | Blocked by upstream: Avalonia 12.2 has no officially supported native Wayland backend. `Avalonia.Wayland` is an experimental opt-in package that `UsePlatformDetect()` does not select, and Wayland has no native child-window model for the existing embedded presenters. The `VK_KHR_wayland_surface` work would need a stable native window/subsurface handoff from Avalonia first. | Avalonia officially supports Wayland in the default desktop backend, including a stable native window or subsurface handle for the embedded video presenter, and a Wayland-only compositor host is available. |
| **SYS-16 six-player/multi-tap** | No matrix title requires it and the hardware is unavailable. | A matrix title requires six players and hardware is available. |
| **SYS-01 audio-only CD playback** | The FZ-10 BIOS CD player lists the 14-track TOC after the 2352+96 subcode fix, but its transport controls remain inert; the 4DO reference documents the same BIOS limitation. A guest CD-player application or a reference capture is needed. | A 3DO CD-player application (or a hardware/Opera capture) starts playback from an audio-only disc. |
| **CORE-01 ARM60 cycle-class** | Closed: the panel proved frame-boundary phase, not a cycle-model gap; no game-visible effect. | A title changes status with the ARM timing model and no other cause explains it. |
| **CORE-05 threaded DSP** | Closed: DSP is ~14% of frame time; handoff/validation cost exceeds the work. | A title spends >30% of frame time in DSP. |
| **PERF-08 managed JIT** | Closed: the interpreter remains faster on real titles and is the accuracy oracle. | A measured 1.5× end-to-end win on a real title. |
| **VID-01 true 640×480 / 4K internal CEL** | Open-ended 4K research. `VID-19` delivered the bounded opt-in CEL path (`--true-2x`) and `VID-20` now tracks the remaining 2x production slice; true 4K internal CEL remains unproven. | A hardware/reference capture proves a 4K internal path is bounded, or `VID-20` produces a compatibility/visual win that justifies the next step. |
| **Full 594/1,371-image differential** | Replaced by the unique-title corpus; `TEST-09` now runs a two-tier soak across all 1,371 images and 414 unique titles, and regional copies add noise rather than signal at the panel level. | A region-specific defect is suspected. |
| **Top-100 audit** | Replaced by Tier A (378) + Tier B (~100) + Tier C (50). | The matrix shows a blind spot outside Tier B. |
| **Hardware test ROM suite** | Dropped; the differential oracle, 2,600+ tests, and fuzz targets already catch the regressions a ROM suite would. | A hardware-only defect appears that the oracle cannot reproduce. |
| **WebAssembly/browser, Android/iOS, Raspberry Pi, libretro core** | Explicitly out of scope for the desktop product. Steam Deck is covered by the Linux packaging path (`REL-10`), not a native port. | Product scope changes. |
| **Full debugger UI** | Out of scope for the 1.x desktop product. Netplay is now tracked as `NET-01`; a full debugger remains out of scope. (RetroAchievements shipped in v1.10.0.) | A post-compatibility product roadmap opens and a debugger is prioritized. |
| **Custom shader pipeline, Discord Rich Presence, cloud saves** | Explicitly not planned for 1.x. The built-in filter set covers presentation; there is no third-party shader runtime, Discord IPC, or cloud-account integration. | Product scope changes and a concrete integration requirement is accepted. |
| **ACC-01 Road Rash long-run divergence** | Documented residual: first behavioral divergence is frame 600 (guest read-request ordering); no game-visible impact has been shown. | A Road Rash gameplay scene (not intro/attract) diverges, or a matrix title changes status. |
| **ACC-04 Jurassic Park default CEL pacing** | Documented residual: the default FreeDO CEL model advances the Raptor Maze ahead of the reference; at 65,000 the reference is still on the 63,000 corridor frame, while `--cel-timing opera` matches the OperaCore frame hash `0xD5D6A993` exactly. Reachability and rendering are fixed. | A per-game Opera-busy profile is adopted, the default CEL model is retuned against a CEL-heavy panel, or a player-visible pacing defect appears outside the Raptor Maze. |
| **VID-19 true 2x presentation residual** | Documented blocker, tracked by `VID-20`: unvalidated titles may misrender bitmap/SPORT or framebuffer-read content while the CPU frame is clean. The OpenGL 480-line line-parameter upload bug that caused the Road Rash bottom rainbow was fixed on 2026-11-19, and the host gate was removed so every title can be re-tested; Jurassic Park Interactive and Family Feud remain the validated references. | `VID-20` corrects the remaining bitmap/SPORT paths and passes a frame-by-frame Road Rash comparison; otherwise the mode is retired. |
| **CORE-04 CD command-cadence residual** | Documented residual: 11/50 panel titles still differ in CD command cadence at frame 300; video/audio match. | A title shows a player-visible CD/audio effect, or a matrix title changes status. |
| **Crash 'n Burn (Japan)** | The Japan dump/BIOS pair is the limit; every tested BIOS stays on the Anvil boot screen. USA/Korea revisions work. | A verified Japan dump/BIOS pair renders the game. |
| **DeathKeep / Kingdom gameplay** | Both match the reference's black/title screen; no known input reaches gameplay. | A reference gameplay capture proves a reachable scene. |
| **480i real-title validation** | No local title is confirmed 480i; only synthetic processor tests exist. | A confirmed 480i title or hardware capture is available. |
| **Exact peer frame parity after beyond-title** | Not required by the playable definition. | A player-visible regression is traced to a peer divergence. |
| **Deep playthrough of all 378 titles** | The compatibility claim is beyond-title, not full-game completion. `TEST-09` adds a 10,000-frame all-image / 30,000-frame unique-title soak, but it is still not a scripted full-game completion. | A title's status or quality grade depends on deeper play. |

---

## 5. Execution order

1. **Compliance and evidence:** `REL-13` and `REL-12` are complete (archive §12);
   the retained FreeDO modified GNU LGPL and the separate GPL-2.0 database are recorded
   in [ReleaseLegalReview.md](ReleaseLegalReview.md). `REL-14` runs in parallel as an
   external review. Establish the `TEST-09` full-library baseline before changing
   emulation behavior, so later regressions are measurable.
2. **Packaging:** publish the channel listings (`REL-08`, `REL-10`) under the retained
   terms and finish the self-hosted Flatpak repository/sandbox checks (`REL-09`).
   Flathub remains closed by the `REL-13` decision. Start `UX-16` signing when the
   Windows certificate and Apple account are available.
3. **macOS platform:** `PLAT-17` keeps only its Retina 2x row, which needs a HiDPI
   display (§4), and `PLAT-05` needs a physical controller. Nothing on macOS is
   runnable until one of those devices is attached.
4. **Product/network:** `NET-01` is complete (archive §17): direct-IP host/join, TCP state
   sync, UDP lockstep, reconnect, CLI, and desktop dialog, with the 36,000-frame run and
   probe matching the reference. Optional session-token and two-window physical-controller
   hardening are post-release. `NET-02` carries the lobby/NAT prototype through Windows VPS
   validation and XGrid client integration; the desktop lobby UI is implemented; >2
   players remain out of scope.
5. **Rendering:** `VID-20` true-2x completeness first, then `VID-21` GPU CEL rasterization
   as a bounded probe. `VID-22` high-refresh interpolation is complete (archive §12); it
   keeps the emulated machine at its native 59.94/50 Hz and only raises the presentation
   rate.
6. **Hardware/media evidence:** `SYS-23` as soon as a DirectInput-only pad is available;
   `SYS-29` when representative real MDS/MDF and CCD/IMG/SUB images are available for
   boot-hash parity.
7. **Presentation matrix:** `PLAT-06` when real GLES 3.1 and Tier B hardware is reachable.
   `PLAT-19` is blocked on official Avalonia Wayland support (§4).

**Stop rule:** do not start P3 product work while a P0 compliance item, P1 platform smoke,
or packaging gate is failing. §4 items stay blocked until their revisit condition is met.

---

## 6. Verification commands

```powershell
# Build, test, format, static analysis
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore
jb inspectcode 5DO.sln --severity=WARNING --no-updates --no-build --output=inspectcode.sarif

# Documentation and public-claim consistency (REL-12)
mkdocs build --strict

# Frontend smoke and host diagnostics
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --smoke-test
dotnet run --project src/FiveDo.Cli -c Debug -- doctor --strict
dotnet run --project src/FiveDo.Cli -c Debug -- bios discover --json

# Native platform runtime smoke (macOS arm64, Linux arm64, Windows arm64)
gh workflow run platform-runtime.yml

# Release bundles
pwsh -NoProfile -File packaging/package-bundle.ps1

# Full-library extended soak (TEST-09); harness to be added with the item
# pwsh -NoProfile -File tools/run-full-library-soak.ps1 -ImageFrames 10000 -TitleFrames 30000 -Parallel 4
```

```bash
# Linux packages and checksums, AppImage, and Flatpak (Linux or WSL)
bash packaging/linux/build-deb.sh --bundle artifacts/bundle/linux-x64 --rid linux-x64
bash packaging/linux/build-rpm.sh --bundle artifacts/bundle/linux-x64 --rid linux-x64
bash packaging/linux/build-appimage.sh --bundle artifacts/bundle/linux-x64 --name 5do-x86_64.AppImage
bash packaging/linux/build-flatpak.sh --bundle artifacts/bundle/linux-x64 --rid linux-x64
( cd artifacts/installers && find . -maxdepth 1 -type f \
    \( -name '*.deb' -o -name '*.rpm' -o -name '*.AppImage' -o -name '*.flatpak' \) \
    -printf '%f\n' | sort | xargs -r sha256sum > SHA256SUMS-linux.txt )
```

The macOS runbook is [BuildAndTest.md](BuildAndTest.md) §7.2. Compatibility and quality
regression gates stay enforced by `compat-baseline.yml` and
`tools/verify-playability-grades.py`; the full release gate is
[ReleaseChecklist.md](ReleaseChecklist.md). Keep a local copy of each bundle in
`src/FiveDo.Frontend.Avalonia/bin/Release`, remove PDB/XML files, and publish
`SHA256SUMS.txt`.

---

## 7. Reference strategy

| Reference | Role | Use it for |
|---|---|---|
| `References/opera-libretro-master` | Timing/behavior oracle | CD-ROM, DSP, peripherals, SWI HLE, boot ROM, accuracy fixes |
| `References/4DO-master` | Compatibility oracle | Game database, fix flags, known-bad titles, UI behavior |
| `References/fourdo-code-r389-trunk` | Historical 4DO snapshot | Older behavior and regression comparisons |
| `References/FourDO Ported` | Porting notes/code | 4DO-to-C# mapping and conventions |
| `References/freedo-master` | Original core reference | FreeDO quirks, ARM/CLIO/MADAM/DSP baseline |
| `References/CPP_SimpleGui` | Opera test harness | Differential runs and reference captures |
| `References/Docs`, `References/Docs2`, `References/Docs3` | Hardware/SDK documentation | ARM60, 3DO, CD-ROM, VDLP, and Portfolio details |
| `References/Aaru` | Disc-image format reference | MDS/MDF, CCD/IMG/SUB, CDI/NRG edge cases, sector normalization (`SYS-29`) |
| `References/cdirip`, `References/libcdio` | Optical-format references | CDI layout, track/sector parsing, and physical-drive behavior |
| `References/RMG-master`, `References/rpcs3-0.0.43` | UX benchmarks only | Library, controller profiles, packaging, per-game config, video recording, high-refresh presentation |

**Rules:** port behavior, not code; one reference per change; tests first; preserve
determinism; keep the core single-threaded by default; do not anchor to Opera bugs; do not
ship copyrighted BIOS or game content; document intentional divergence in
[PortingNotes.md](PortingNotes.md). Aaru/cdirip/libcdio are **format references only**:
implement the parsers in-tree and cover them with representative images and malformed-input
tests; do not copy their code or ship their binaries.

---

## 8. Evidence and history

- [Status](Status.md) — current position and release state.
- [Compatibility](Compatibility.md) — supported set and platform evidence.
- [5DO vs 4DO vs Phoenix](CompatibilityComparison.md) — unique-title matrix.
- [BeyondTitleEvidence.md](BeyondTitleEvidence.md) — per-title captures and failure reasons.
- [KnownIssues.md](KnownIssues.md) — current limitations.
- [DifferentialHarness.md](DifferentialHarness.md) — Opera comparison and panel evidence.
- [TestCorpus.md](TestCorpus.md) — test inventory and dated batches.
- [ReleaseLegalReview.md](ReleaseLegalReview.md) - `REL-13` licence decision, 4DO
  database provenance, and channel eligibility.
- [GPL-2.0.txt](GPL-2.0.txt) - complete GPL-2.0 text for the 4DO game database.
- [ReleaseChecklist.md](ReleaseChecklist.md) - release gate.
- [ImplementationPlanArchive.md](ImplementationPlanArchive.md) - completed baseline,
  results, completed backlog rows (including `REL-13`/`REL-12`), partial deliveries
  (§17), stable IDs, retired aliases, and appendices.

The archive is retained for traceability only. New work belongs in this file; a whole
item leaves this file when its **Done when** line is satisfied, and a shipped sub-feature
of a partial item moves to archive §17.
