# 5DO — Missing Features

**Snapshot:** 2026-11-21
**Release:** v1.16.1
**Status:** the compatibility program is complete. This file tracks **missing work only**;
completed items, their evidence, and the full stable-ID history are in
[ImplementationPlanArchive.md](ImplementationPlanArchive.md) Part II.
**Open items:** 14 - 3 platform, 3 release/packaging, 5 product, 2 rendering, 1 awaiting hardware.

> **Filter rule.** An item belongs in §3 only when it can be completed and evidenced with
> the current 378-title corpus, the current codebase, and obtainable hardware or CI. Items
> blocked by licensing, missing hardware, unavailable reference captures, or open-ended
> research are listed in §4 and are **not** checkboxes.
>
> **Evidence rule.** A task is complete only when its **Done when** line is satisfied and
> the capture, hash, frame count, BIOS/disc identity, test, or release artifact is
> committed. Completed rows move to the archive; they are not kept here.

---

## 1. Completed baseline

Nothing in §3 is closed. The 375/378 playability result, the 101-row quality ledger, the
50/50 accuracy panel, the v1.9.1–v1.14.2 release history, the 39 completed
`QUAL`/`AUD`/`PLAT`/`ACC`/`SYS`/`UX`/`VID` rows (including the post-1.13.1 `SYS-26`
movie-verification and `UX-36` startup-preflight fixes), the `REL-07` AppImage update
closure, the `REL-11` Windows MSVC runtime closure, and every completed stable ID are
recorded in
[ImplementationPlanArchive.md](ImplementationPlanArchive.md) §10–§16. Do not
reopen a completed ID without new measurement.

---

## 2. How the remaining backlog works

- **Priority:** **P0** player-visible quality/evidence, **P1** release/platform hardening,
  **P2** accuracy features, **P3** product features.
- **Effort:** **S** under 3 developer-days, **M** 3–10, **L** 10–30, **XL** 30–90+.
- **Status:** **Open** (not started or in progress), **Partial** (a usable subset ships),
  **Candidate** (probe first, commit only on evidence), **Awaiting hardware** (code and
  tests are complete; only the named device evidence is missing).
- **One feature per change.** Do not mix accuracy, rendering, and UX work in one change.
- **One root cause per fix.** Every fix starts from a failing title, test, or panel row.
- **No speculative work.** If a task cannot change a status, a gate, or a release artifact,
  it does not belong in P0/P1.
- **No duplicate entries.** Reference an ID instead of restating the same work.
- **Blocked items stay blocked.** Moving an item from §4 to §3 requires the named blocking
  condition to be resolved first.

---

## 3. Missing features — achievable backlog

### 3.1 P1 — macOS platform

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **PLAT-17** | Partial | S | **macOS Retina 2x row**: windowed, live resize, fullscreen, the 320x240 capture, and 4K offscreen output are validated on the NoMac host (2026-11-18). Only 2x Retina is missing: the paravirtual display advertises 11 modes that are all 1:1, so `backingScaleFactor` stays 1.0 even for a `CGVirtualDisplay` created with `hiDPI`. | `PixelSizeTests` and the `MacMetalSurface` drawable math already cover points × backing scale; a HiDPI display would exercise the same path end to end. | A run on a HiDPI display records `scale 2.0`, a drawable at twice the points, and an unstretched 4:3 viewport in [Performance.md](Performance.md) §8.8. |
| **PLAT-05** | Awaiting hardware | S | **macOS gamepad backend**: physical detection for the IOKit HID source. `MacOsGamepadSource`, `MacOsHidGamepadMapper`, and 21 mapping/profile tests are complete. | Only the physical check needs a macOS host with a connected controller; the mapping is already unit-tested. | A macOS gamepad is detected and mapped through the existing eight-player profile. |

`PLAT-14` (native Metal surface), `PLAT-15` (bundled MoltenVK/loader), `PLAT-16`
(OpenAL live-switch), and `PLAT-18` (NoMac release gate) are closed; see the archive.
`PLAT-17` validated the whole 1x matrix on 2026-11-18 and keeps only its Retina 2x row
open (§4).

### 3.2 P1 — Presentation and performance validation

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **PLAT-06** | Partial | M | **GLES 3.1, DPI 100/150/200%, and Tier B timing validation**: finish the presentation/performance matrix on real hardware. DPI math (100/125/150/200%), GLES 3.0/ANGLE, and 4K WGL are already validated. | The renderer and benchmark harness exist; only real GLES 3.1 hardware and a real Tier B device are missing. | Results are recorded in [Performance.md](Performance.md) §8.8, with every residual labeled `validated` or `best-effort`. |

### 3.3 P1 — Release and packaging

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **REL-08** | Open | M | **Third-party channel listings**: publish the AUR `5do-bin` package (PKGBUILD ships in `packaging/linux/aur/`), an AppImageHub entry, and an itch.io project page. The packager-facing files (desktop entry, MIME XML, metainfo, both icons) already ship in the Linux bundle and are verified by `build-bundle.yml`. | These channels accept a repackaged upstream release and need no licence change; the catalog entries are small, reversible files. | `yay -S 5do-bin` installs and launches on Arch, the AppImageHub entry appears in the catalog, and the itch.io page serves the current AppImage. |
| **REL-09** | Partial | L | **Flatpak distribution**: the published `5do-1.13.1-linux-aarch64.flatpak` installed with its 26.08 runtime on Linux arm64 hardware on 2026-11-19 and ran the CLI `selftest`, the frontend smoke, and Theme Park at 59.9 fps. Remaining: verify gamepad, audio, and display-sleep inhibit inside the sandbox, then add a self-hosted signed repository and a Flathub submission once the licence position is settled. `build-flatpak.sh` and `io.github.purelogiccode.FiveDO.yml` exist, and CI installs and runs the bundle. | Flatpak is the channel GNOME Software, KDE Discover, and the Steam Deck Discover store read; the runtime supplies GL/audio and the manifest only repackages the self-contained apphosts. | `flatpak install --user ./5do-<arch>.flatpak` then `flatpak run io.github.purelogiccode.FiveDO` launches 5DO with gamepad, audio, and display-sleep inhibit working, and CI runs `5do-cli selftest` inside the sandbox; Flathub review has an explicit licence decision recorded in [ReleaseLegalReview.md](ReleaseLegalReview.md). |
| **REL-10** | Partial | S | **Steam Deck and EmuDeck integration**: the Deck-sized first-run BIOS offer and non-Steam-game setup are complete (2026-11-21). The welcome guide detects `SteamDeck`/`SteamOS`/`SteamGamepadUI`/gamescope, uses a 960x680 layout with large buttons, shows the BIOS folder, and offers **Scan** and **Open Folder**; [Installation.md](Installation.md) documents Desktop Mode, the BIOS folder, adding the AppImage/Flatpak to Steam, `--fullscreen`, and a trackpad controller layout. Remaining: the upstream EmuDeck/RetroDeck 3DO entry, which needs their repository. | evdev gamepads and display-sleep inhibit are validated on Linux hardware; the rest is presentation and documentation. | A Deck user installs from the AppImage or EmuDeck, boots a game with a controller, and reaches gameplay without a keyboard; the setup steps are in [Installation.md](Installation.md). |

The unsigned Windows installer, the Linux `.deb`/`.rpm` (now signed), the desktop entry,
MIME associations, and the macOS `.app`/`.dmg`/`.pkg` scaffolding are complete and built
on native CI runners; only Windows code signing and macOS notarization are still blocked
(§4). The packages ship as GitHub release assets and install with
`apt install ./…deb` / `dnf install ./…rpm` / `flatpak install ./…flatpak`; no repository
is hosted. Evidence: [Installers.md](Installers.md),
[LinuxDistribution.md](LinuxDistribution.md).

### 3.4 P3 — Product features

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **UX-33** | Partial | S | **Minor Phoenix/4DO presentation options**: audio channel swap, named void patterns 4DO/Bumps/Metal, an emulation-health badge, and the GPU scanout fill that paints the configured void color in the letterbox/pillarbox (all shipped 2026-11-18). Remaining: render the selected 4DO/Bumps/Metal/checker/grid/scanline pattern inside the GPU letterbox/pillarbox bars on OpenGL/GLES, Vulkan, and Direct3D 11; the pattern is currently shell-composited, so the GPU bars show only the solid void color. Land each option as its own change. | 4DO and Phoenix expose these settings; 5DO already owns audio mixing, void brushes, overscan, and FPS/audio diagnostics, so each option is presentation-only and does not touch emulation state. | Each option has a settings round-trip test; audio swap changes left/right output without changing frame/state/audio hashes; the border and solid void color render across all presenters; patterns render in the shell border, behind the software presenter, and inside the GPU bars on all three backends; the health badge reflects existing counters and is documented in the UserGuide. |
| **SYS-25** | Candidate | M | **Live CPU-clock changes**: evaluate applying the configured CPU clock without recreating the machine. Preserve save-state/timing determinism and expose the change only if the probe is clean. | FreeDO and 4DO apply the clock live, and 5DO already has a validated 0.5×–4× clock matrix and a single-threaded machine loop. This is a candidate, not a committed feature. | A live change preserves stock-speed run/state hashes, does not corrupt timing or save states, and is covered by a test; otherwise the restart-required behavior stays and the candidate is closed with evidence. |
| **UX-37** | Open | M | **Autosave and auto-resume**: add a dedicated rolling autosave slot written on pause, clean exit, and a configurable interval; on the next launch, reopen the last disc and offer or automatically load the newest valid autosave, matching 4DO's `AutoOpenGameFile`/`AutoLoadLastSave` and Phoenix's autosave/top-slot behavior. Keep manual slots untouched, validate the disc/BIOS hash before restore, and expose separate enable/disable settings. | 5DO already reopens the last disc on startup and has versioned, checksummed save states with sidecar metadata; the work is a policy layer around the existing `SaveStateSlotManager` and `AppSettings`, not a new state format. | A clean exit writes an autosave; relaunch reopens the last disc and restores the newest valid autosave without touching manual slots; a disabled setting suppresses both; a mismatched disc/BIOS refuses with an actionable message; tests cover clean exit, crash recovery, disabled mode, and settings round-trip; [UserGuide.md](UserGuide.md) and [Configuration.md](Configuration.md) document the behavior. |
| **SYS-27** | Open | M | **ZIP/7z disc containers**: open a 3DO image or a CUE+BIN set directly from a ZIP or 7z archive through a virtual file layer, without extracting to a temporary directory. Wire the archive path through `DiscImageFactory`, the game library, drag/drop, recent discs, and the CLI (`run`, `info`, `disc`, `fs`), and report encrypted, multi-volume, or unsupported archives clearly. | The existing `IDiscSource` abstraction already separates sector reads from the host path, and .NET supplies ZIP support; a managed 7z decoder is the only new dependency. The 378-title corpus can compare archive and extracted reads. | A zipped ISO and a 7z CUE+BIN set boot and match the extracted image's disc and 600-frame hashes; the library, desktop open path, and CLI accept both; encrypted/multi-volume archives fail with an actionable message; parser fuzz tests and archive round-trip tests pass; [Compatibility.md](Compatibility.md) and [UserGuide.md](UserGuide.md) list the formats. |
| **NET-01** | Candidate | XL | **Netplay**: add a two-player deterministic network mode. Choose delay-based lockstep or rollback after a loopback probe, synchronize the initial BIOS/disc/state, exchange per-frame PBUS packets, detect and recover from desync, and expose host/join, port, input delay, and reconnect settings. Start with direct IP/LAN; a lobby and NAT traversal are out of scope for the first increment. | The machine is already single-threaded, fixed-timestep, and deterministic; TAS movie playback and `MachineStateSnapshot` provide the input/state plumbing. RetroArch's frontend netplay is the only peer reference, so this starts as a bounded probe rather than a committed release feature. | Two local instances complete a 10-minute two-player session with no desync and matching frame/audio hashes; reconnect restores from the last confirmed state; a deliberate packet loss/delay test recovers or reports a clear desync; latency and bandwidth are recorded; [UserGuide.md](UserGuide.md) documents host/join, ports, and the direct-IP-only scope. |

### 3.5 P2/P3 — Rendering completeness

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **VID-20** | Candidate | L | **True 2x CEL production completeness**: extend the `VID-19` `--true-2x` path beyond Jurassic Park Interactive and Family Feud. Add bitmap/SPORT high-resolution sampling, framebuffer-read semantics, per-title validation across the 378-title corpus, keep the game-ID gate removed while per-title results are recorded, and fix the Road Rash bottom-row/rainbow artifact during scene transitions. | `VID-19` already writes independent 480-line CEL fields and matches the reference on a 3D and a 2D title; [HighResolutionCelResearch.md](HighResolutionCelResearch.md) scopes the remaining bitmap/SPORT work, and the existing frame-hash harness can gate every step. This is a bounded production slice of `VID-01`, not open-ended 4K research. | The 50-title accuracy panel and 378-title corpus run with true 2x where validated, native frame/state/audio hashes stay unchanged, Road Rash transitions show no stale rows, the hard-coded gate is removed or replaced by a documented per-title list, and the results are recorded in [HighResolutionCelResearch.md](HighResolutionCelResearch.md) and [Compatibility.md](Compatibility.md). |
| **VID-21** | Candidate | XL | **GPU 3D CEL rasterization**: move packed, scaled, and arbitrary CEL rasterization to the GPU with palette/CLUT-correct output, texture sampling/filtering, SPORT fill/copy interaction, and a CPU fallback. Keep the CPU rasterizer as the accuracy oracle and compare every stage against it. | 5DO already owns the CEL command parser, CLUT/VRAM layout, and GL/Vulkan/D3D11 presenters, and Phoenix demonstrates that GPU CEL rendering is feasible; the 50-title panel and true-2x captures provide the parity oracle. This is a large, high-risk rendering project, so it starts as a probe. | A GPU path matches the CPU reference on a defined CEL-heavy subset (at least the 50-title panel at frame 300) with zero pixel differences, the default path is unchanged, all three backends render the same frame, the CPU fallback remains, and the measured frame time improves on a named title; otherwise the probe is closed with evidence. |

### 3.6 P3 — Hardware evidence awaited

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **SYS-21** | Done | M | **Physical CD-ROM drive support**: boot from a host optical drive. `PhysicalDiscSource`, **File > Boot Optical Drive...**, and the CLI drive-root path read a burned CD-R through the cooked block interface. Windows discovery probes the optical device with `IOCTL_STORAGE_CHECK_VERIFY2` and falls back to `\\.\CdRomN`, because a 3DO disc's custom filesystem is not mountable and may have no drive letter. The drive TOC is exposed through `ITrackedDiscSource`, and raw CDDA frames use `IOCTL_CDROM_RAW_READ` (Windows) or `CDROMREADAUDIO` (Linux). Host read failures are reported as MEI receive-ECC and do not stop the core. | The disc abstraction already supports multiple image formats; a host CD source is additive. | A physical 3DO disc boots and reaches beyond-title; the device/format path is documented. Issue #8 validated burned CD-R boot on a Pioneer BD-RW. Physical CD-DA is implemented; a real mixed-mode audio track is the remaining hardware check. |
| **SYS-23** | Awaiting hardware | M | **Windows DirectInput/generic HID gamepad backend**: `DirectInputGamepadSource`/`WindowsGamepadSource` enumerate devices, filter duplicate XInput pads, calibrate trigger rest positions, and keep XInput as fallback; axis/POV/trigger tests pass. Only a physical DirectInput-only pad check is missing. | `IRawGamepadSource`, `PlatformRawGamepadSource`, and `GamepadMapper` already abstract and test the mapping; a Windows source is additive. | A DirectInput-only pad is detected and mapped on Windows; axis/POV/trigger tests pass; XInput remains the default fallback; the backend and device selection are documented in [Configuration.md](Configuration.md). |

---

## 4. Blocked, residual, or not achievable (do not add checkboxes)

These items stay out of §3 until the named condition changes. “Residual” means the item
was measured and closed with a documented limitation, not that it is unachievable.

| Item | Why it is not in the achievable backlog | Revisit if |
|---|---|---|
| **SYS-05 embedded boot ROM** | GPL-2.0-only licensing risk; no distributable ROM exists. Retail titles still need a user BIOS. | Legal sign-off and a distributable ROM exist. |
| **UX-16 signing/notarization** | Requires valid Windows code-signing certificates and an Apple developer account; the unsigned bundles and installer/package scaffolding are the current release artifacts. | The certificates and accounts are available. |
| **Official Linux archives (Debian, Ubuntu, Fedora, openSUSE)** | The FreeDO notice forbids commercial use without the rights holders' approval, which fails the Debian DFSG and Fedora legal tests; the archive builders also run without network access, so the NuGet graph must be vendored. Direct downloads plus the optional mirror tooling (`packaging/linux/build-*-repo.sh`) and Fedora COPR/Ubuntu PPAs are the interim paths. | The FreeDO rights holders grant written commercial-use permission (or clarify the licence) **and** a network-free, vendored source build exists. |
| **Intel macOS** | **Dropped in 1.12.0.** The rented NoMac host is arm64 and there is no Intel Mac with a usable Metal device; the project supports Apple Silicon macOS only. | Product scope changes and an Intel Mac becomes available for validation. |
| **PLAT-17 Retina 2x row** | The rented NoMac host's paravirtual display advertises 11 modes that are all 1:1, and a private `CGVirtualDisplay` created with `hiDPI` is also handed 1x modes, so `backingScaleFactor` stays 1.0 and the shell never sees scale 2. Every other PLAT-17 row is validated (2026-11-18). | A macOS host with a HiDPI display, or a virtual display that advertises 2x modes, is available. |
| **SYS-16 six-player/multi-tap** | No matrix title requires it and the hardware is unavailable. | A matrix title requires six players and hardware is available. |
| **SYS-01 audio-only CD playback** | The FZ-10 BIOS CD player lists the 14-track TOC after the 2352+96 subcode fix, but its transport controls remain inert; the 4DO reference documents the same BIOS limitation. A guest CD-player application or a reference capture is needed. | A 3DO CD-player application (or a hardware/Opera capture) starts playback from an audio-only disc. |
| **CORE-01 ARM60 cycle-class** | Closed: the panel proved frame-boundary phase, not a cycle-model gap; no game-visible effect. | A title changes status with the ARM timing model and no other cause explains it. |
| **CORE-05 threaded DSP** | Closed: DSP is ~14% of frame time; handoff/validation cost exceeds the work. | A title spends >30% of frame time in DSP. |
| **PERF-08 managed JIT** | Closed: the interpreter remains faster on real titles and is the accuracy oracle. | A measured 1.5× end-to-end win on a real title. |
| **VID-01 true 640×480 / 4K internal CEL** | Open-ended 4K research. `VID-19` delivered the bounded opt-in CEL path (`--true-2x`) and `VID-20` now tracks the remaining 2x production slice; true 4K internal CEL remains unproven. | A hardware/reference capture proves a 4K internal path is bounded, or `VID-20` produces a compatibility/visual win that justifies the next step. |
| **Full 594/1,371-image differential** | Replaced by the 378-title unique corpus; regional copies add noise, not signal. | A region-specific defect is suspected. |
| **Top-100 audit** | Replaced by Tier A (378) + Tier B (~100) + Tier C (50). | The matrix shows a blind spot outside Tier B. |
| **Hardware test ROM suite** | Dropped; the differential oracle, 2,000+ tests, and fuzz targets already catch the regressions a ROM suite would. | A hardware-only defect appears that the oracle cannot reproduce. |
| **WebAssembly/browser, Android/iOS, Raspberry Pi, libretro core** | Explicitly out of scope for the desktop product. Steam Deck is covered by the Linux packaging path (`REL-10`), not a native port. | Product scope changes. |
| **Full debugger UI** | Out of scope for the 1.x desktop product. Netplay is now tracked as `NET-01`; a full debugger remains out of scope. (RetroAchievements shipped in v1.10.0.) | A post-compatibility product roadmap opens and a debugger is prioritized. |
| **QUAL-05B Road Rash full race** | A fresh 150,000-frame `--autopilot road` run still ends with the rider off the bike on the sidewalk; the remaining work is driving/start-line and traffic avoidance, not emulation. Evidence: `artifacts/p0-rr-auto-boot`. | A scripted driver completes the 5.5-mile race without a wreck, or a new autopilot strategy proves it can. |
| **ACC-01 Road Rash long-run divergence** | Documented residual: first behavioral divergence is frame 600 (guest read-request ordering); no game-visible impact has been shown. | A Road Rash gameplay scene (not intro/attract) diverges, or a matrix title changes status. |
| **ACC-04 Jurassic Park default CEL pacing** | Documented residual: the default FreeDO CEL model advances the Raptor Maze ahead of the reference; at 65,000 the reference is still on the 63,000 corridor frame, while `--cel-timing opera` matches the OperaCore frame hash `0xD5D6A993` exactly. Reachability and rendering are fixed. | A per-game Opera-busy profile is adopted, the default CEL model is retuned against a CEL-heavy panel, or a player-visible pacing defect appears outside the Raptor Maze. |
| **VID-19 true 2x presentation residual** | Documented blocker, tracked by `VID-20`: unvalidated titles may misrender bitmap/SPORT or framebuffer-read content while the CPU frame is clean. The OpenGL 480-line line-parameter upload bug that caused the Road Rash bottom rainbow was fixed on 2026-11-19, and the host gate was removed so every title can be re-tested; Jurassic Park Interactive and Family Feud remain the validated references. | `VID-20` corrects the remaining bitmap/SPORT paths and passes a frame-by-frame Road Rash comparison; otherwise the mode is retired. |
| **CORE-04 CD command-cadence residual** | Documented residual: 11/50 panel titles still differ in CD command cadence at frame 300; video/audio match. | A title shows a player-visible CD/audio effect, or a matrix title changes status. |
| **Crash 'n Burn (Japan)** | The Japan dump/BIOS pair is the limit; every tested BIOS stays on the Anvil boot screen. USA/Korea revisions work. | A verified Japan dump/BIOS pair renders the game. |
| **DeathKeep / Kingdom gameplay** | Both match the reference's black/title screen; no known input reaches gameplay. | A reference gameplay capture proves a reachable scene. |
| **480i real-title validation** | No local title is confirmed 480i; only synthetic processor tests exist. | A confirmed 480i title or hardware capture is available. |
| **Exact peer frame parity after beyond-title** | Not required by the playable definition. | A player-visible regression is traced to a peer divergence. |
| **Deep playthrough of all 378 titles** | The compatibility claim is beyond-title, not full-game completion. | A title's status or quality grade depends on deeper play. |

---

## 5. Execution order

1. **Packaging:** `REL-07` and `REL-11` are closed. The next packaging work is the
   release channels in item 3 (`REL-08`, `REL-10`, then `REL-09`).
2. **macOS platform:** `PLAT-17` now has only its Retina 2x row, which needs a HiDPI
   display (§4), and `PLAT-05` needs a physical controller, so nothing on macOS is
   runnable until one of those devices is attached. The native Metal surface (`PLAT-14`)
   is complete, so the macOS build no longer reads back every frame.
3. **Release channels:** publish the channel listings (`REL-08`, `REL-10`), then finish
   with the hosted Flatpak repository and Flathub review (`REL-09`). The mirror trees and
   the software-center metadata are already verified
   ([LinuxDistribution.md](LinuxDistribution.md) §5.1–§5.2).
4. **Product:** `UX-33` tiled patterns as one change per presenter, `UX-37`
   autosave/auto-resume, the `SYS-25` live-clock probe, then `SYS-27` archive containers.
   Close `SYS-25` with evidence if the probe is not clean; do not keep it open as a wish.
5. **Network:** `NET-01` starts with a two-instance loopback probe; commit to a netplay
   implementation only if the probe is clean.
6. **Rendering:** `VID-20` true-2x completeness first, then `VID-21` GPU CEL rasterization
   as a bounded probe.
7. **Hardware evidence:** `SYS-23` as soon as a DirectInput-only pad is available;
   `SYS-21` is closed by the CD-R boot in issue #8.
8. **Presentation matrix:** `PLAT-06` when real GLES 3.1 and Tier B hardware is reachable.

**Stop rule:** do not start P3 product work while a P1 platform or packaging smoke is
failing. §4 items stay blocked until their revisit condition is met.

---

## 6. Verification commands

```powershell
# Build, test, format, static analysis
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore
jb inspectcode 5DO.sln --severity=WARNING --no-updates --no-build --output=inspectcode.sarif

# Frontend smoke and host diagnostics
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --smoke-test
dotnet run --project src/FiveDo.Cli -c Debug -- doctor --strict
dotnet run --project src/FiveDo.Cli -c Debug -- bios discover --json

# Native platform runtime smoke (macOS arm64, Linux arm64, Windows arm64)
gh workflow run platform-runtime.yml

# Release bundles
pwsh -NoProfile -File packaging/package-bundle.ps1
```

```bash
# Linux packages, signatures, AppImage, and Flatpak (Linux or WSL)
bash packaging/linux/build-deb.sh --bundle artifacts/bundle/linux-x64 --rid linux-x64
bash packaging/linux/build-rpm.sh --bundle artifacts/bundle/linux-x64 --rid linux-x64
bash packaging/linux/build-appimage.sh --bundle artifacts/bundle/linux-x64 --name 5do-x86_64.AppImage
bash packaging/linux/build-flatpak.sh --bundle artifacts/bundle/linux-x64 --rid linux-x64
bash packaging/linux/sign-artifacts.sh --key-id "$LINUX_GPG_KEY_ID" \
  --keyring-out artifacts/signing/5do-archive-keyring.gpg artifacts/installers
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
| `References/Docs` | Hardware/SDK documentation | ARM60, 3DO, CD-ROM, VDLP, and Portfolio details |
| `References/RMG-master`, `References/rpcs3-0.0.43` | UX benchmarks only | Library, controller profiles, packaging, per-game config |

**Rules:** port behavior, not code; one reference per change; tests first; preserve
determinism; keep the core single-threaded by default; do not anchor to Opera bugs; do not
ship copyrighted BIOS or game content; document intentional divergence in
[PortingNotes.md](PortingNotes.md).

---

## 8. Evidence and history

- [Status](Status.md) — current position and release state.
- [Compatibility](Compatibility.md) — supported set and platform evidence.
- [5DO vs 4DO vs Phoenix](CompatibilityComparison.md) — unique-title matrix.
- [BeyondTitleEvidence.md](BeyondTitleEvidence.md) — per-title captures and failure reasons.
- [KnownIssues.md](KnownIssues.md) — current limitations.
- [DifferentialHarness.md](DifferentialHarness.md) — Opera comparison and panel evidence.
- [TestCorpus.md](TestCorpus.md) — test inventory and dated batches.
- [ReleaseChecklist.md](ReleaseChecklist.md) — release gate.
- [ImplementationPlanArchive.md](ImplementationPlanArchive.md) — completed baseline,
  results, completed backlog rows, stable IDs, retired aliases, and appendices.

The archive is retained for traceability only. New work belongs in this file; an item
leaves this file when its **Done when** line is satisfied, not when it is implemented.
