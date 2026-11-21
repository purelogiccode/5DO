# 5DO Documentation

<p align="center">
  <img src="assets/logo.png" alt="5DO — 3DO Emulator" width="640">
</p>

<p align="center">
  <strong>A modern, source-available C# 15/.NET 11 emulator for the 3DO Interactive Multiplayer.</strong>
</p>

<p align="center">
  <a href="https://github.com/purelogiccode/5DO/actions/workflows/ci.yml"><img src="https://github.com/purelogiccode/5DO/actions/workflows/ci.yml/badge.svg?branch=master" alt="CI status"></a>
  <a href="https://github.com/purelogiccode/5DO/actions/workflows/docs.yml"><img src="https://github.com/purelogiccode/5DO/actions/workflows/docs.yml/badge.svg?branch=master" alt="Documentation build"></a>
  <a href="https://github.com/purelogiccode/5DO"><img src="https://img.shields.io/badge/.NET-11.0-512BD4?logo=dotnet&amp;logoColor=white" alt=".NET 11"></a>
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey" alt="Windows, Linux, and macOS">
  <a href="https://github.com/purelogiccode/5DO/blob/master/LICENSE"><img src="https://img.shields.io/badge/license-FreeDO%20modified%20LGPL-blue" alt="License"></a>
</p>

!!! important "v1.14.2 packaging and diagnostics bug-fix release"
    5DO is at v1.14.2. Windows bundles now include the app-local MSVC runtime OpenAL
    Soft needs, AppImage update metadata resolves the next release, cover-catalog HTTP
    failures fall back to the cached catalog, and expected user conditions no longer
    file bug reports. The 1.14.0 feature baseline is unchanged: `--high-res` remains a
    presentation-only 2x upscale, the OpenGL 480-line upload is fixed, and retail
    Gamegun titles keep a joypad on port 1. **P** pauses, **Space** toggles fast-forward,
    **F1** connects the Gamegun, and **Alt+Enter** toggles fullscreen.
    **Windows x64, Linux x64, and macOS arm64** are validated targets: macOS presents
    through a native `CAMetalLayer` surface at 60 fps, with all eight filters and a
    packaged DMG. GPG-signed Linux artifacts use the keyring fingerprint
    `E69FF266D1FB01D3411578775C4C1E8EAFD44075`. The macOS arm64 bundle includes MoltenVK
    and the Vulkan loader; the CPU presenter is the fallback. Linux arm64 and Windows
    arm64 pass the native runtime smoke; Intel macOS is unsupported. No BIOS ROMs, game
    images, or extracted game assets are distributed here; supply legally obtained files.

## Start here

<div class="grid cards" markdown>

-   :material-download: **Install and run**

    ---

    Platform requirements, BIOS and disc setup, first run, and data locations.

    [:octicons-arrow-right-24: Installation](Installation.md)

-   :material-gamepad-variant: **User guide**

    ---

    Controls, display filters, save states, rewind, library, recording, cheats, and
    the desktop shell.

    [:octicons-arrow-right-24: User guide](UserGuide.md)

-   :material-help-circle: **FAQ**

    ---

    BIOS, disc formats, releases, performance, platforms, saves, and licensing.

    [:octicons-arrow-right-24: Frequently asked questions](FAQ.md)

-   :material-check-decagram: **Compatibility**

    ---

    Browse the 594-disc loadability sweep, filter by status and region, and open
    per-disc evidence.

    [:octicons-arrow-right-24: Compatibility browser](CompatibilityBrowser.md)
    · [:octicons-arrow-right-24: Compatibility notes](Compatibility.md)

</div>

## Choose a path

| I want to... | Read |
|---|---|
| Play games on the desktop app | [Installation](Installation.md), [User guide](UserGuide.md), [FAQ](FAQ.md) |
| Use the headless emulator or automate tests | [CLI reference](CliReference.md), [Diagnostics](Diagnostics.md) |
| Configure settings, profiles, or controllers | [Configuration](Configuration.md), [User guide](UserGuide.md) |
| Understand save states, cheats, or TAS movies | [Save states](SaveStates.md), [Cheats](Cheats.md), [TAS movies](Tas.md) |
| Diagnose a problem | [Troubleshooting](Troubleshooting.md), [Known issues](KnownIssues.md) |
| Build, test, or publish 5DO | [Build and test](BuildAndTest.md), [Test strategy](TestStrategy.md) |
| Contribute code or documentation | [Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md), [Developer guide](DeveloperGuide.md), [Documentation guide](Documentation.md) |
| Check the current project position | [Status](Status.md), [Roadmap](Roadmap.md), [Release notes](ReleaseNotes-1.14.2.md) |
| Read the license and attribution rules | [Licensing](Licensing.md), [project license](https://github.com/purelogiccode/5DO/blob/master/LICENSE), [third-party notices](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md) |

## Current snapshot

**2026-11-21, v1.14.2 packaging and diagnostics bug-fix release (GitHub Releases).**

- **2,547 tests** are present in the current test assembly.
- **Windows bundles are self-contained for audio:** `vcruntime140.dll` and
  `msvcp140.dll` (plus the x64-only `vcruntime140_1.dll`) ship beside `OpenAL32.dll`,
  so a clean Windows image no longer needs the Visual C++ redistributable.
- **AppImage updates resolve the next release:** the embed uses
  `gh-releases-zsync`, the `.zsync` target points at the versioned AppImage, and the
  build validates both. The 1.14.1 AppImages predate the fix, so update those manually.
- **Diagnostics are quieter:** cover-catalog HTTP failures fall back to the cached
  catalog, and expected state/experimental/deployment warnings no longer file bug
  reports.
- **CHD Mode 2 sectors and crop dimensions are fixed:** native 2336-byte and 2324-byte
  track sectors start at frame offset zero for raw-sector consumers and DAT hashing, and
  clearing auto-crop or a manual crop returns pointer mapping and CPU presentation to the
  live frame size.
- **Persistence and shutdown are hardened:** state, cache, BIOS, NVRAM, screenshot, and
  movie writes use unique temporary files with cleanup; malformed settings and caches
  recover instead of crashing; and audio, RetroAchievements, Vulkan, Direct3D, and core
  workers are not disposed while they may still be running.
- **High-resolution presentation is safe for every title:** `--high-res` keeps the guest
  at native 320x240 and doubles the finished frame in `Vdlp`; a 300-frame Road Rash run
  matches a 2x nearest upscale of the native frame with 0 differing pixels. The OpenGL
  per-line parameter texture now uses `RG32UI`, eliminating the 480-line rainbow rows.
- **Gamegun defaults are complete:** retail American Laser Games titles keep a joypad on
  port 1 and the gun on port 2, Demolition Man starts with the pad and connects the gun
  on demand with F1, and database input defaults now reach the desktop shell.
- **Movie verification matches the recording:** the header records the emulated frame
  span, disc and BIOS are resolved automatically, and a 600-frame disc movie verifies
  with exit 0; a partial extraction now explains the missing native files instead of
  crashing inside Skia.
- **macOS arm64 presents through a native Metal surface** (`CAMetalLayer`,
  `VK_EXT_metal_surface`) with 0 renderer bytes/frame at 1080p FIFO, live resize and
  fullscreen, 320x240 capture parity, and 4K offscreen output. Retina 2x remains
  unvalidated because the validation host only advertises 1:1 display modes.
- **Presentation options:** audio channel swap, 4DO/Bumps/Metal void patterns, the void
  color on the GPU presenters, and an emulation-health badge.
- **Linux artifacts are signed:** keyring, per-artifact `.asc`, and a signed checksum
  manifest; CI builds and installs both `.deb`/`.rpm` sets and both Flatpak bundles.
- Disc containers: ISO, BIN, CUE+BIN, CHD (including parent/child), DiscJuggler CDI, and
  Nero NRG. The CDI and NRG readers accept cooked 2048-byte, 2336-byte Mode 2, raw
  2352-byte, and the 2353/2368/2448/2449-byte raw variants, and expose the same track
  table and game ID as the equivalent ISO/CHD.
- RetroAchievements: managed rcheevos evaluation, 3DO disc identification, measured
  progress, a durable offline unlock queue, hardcore rules, rich presence,
  hardcore-only leaderboard submission, OSD notifications, and a progress window.
  Achievement hit counts and leaderboard attempts now persist in save states through a
  dedicated, definition-validated `.tdos` section.
- The desktop apphost is **`FiveDo.exe`** on Windows and **`FiveDo`** on Linux/macOS;
  the CLI remains `FiveDo.Cli`. ReadyToRun release bundles and deferred startup work
  cut time to the first window on the validation host from about 1.09 s in Debug to
  about 0.62 s. A localized **Loading game...** overlay covers the first-frame wait,
  and the first Escape in a running fullscreen game exits fullscreen and pauses in one
  step.
- Localization (English, German, Brazilian Portuguese, French, Spanish, Italian,
  Japanese, Korean, and Simplified Chinese), external metadata and manual media, physical
  optical-drive boot, Windows DirectInput/HID gamepads, the controller preview,
  auto-crop, display-sleep inhibition, Lua input scripting, the input timeline,
  best-effort 4DO `.4dosav` import/export, and the two-step Escape pause/quit
  shortcut are available.
- The 50-title differential panel matches **50/50 frame hashes** at frames
  60/120/300 with **0 errors**. The frame-boundary phase is resolved: the harness
  samples Opera's ARM/CD state in the checkpoint's own field, and 5DO ends a field
  on the 263/312-line scanline boundary like the reference. The same-frame
  retired-instruction delta is **11-20** at frame 60 and **23-2,223** at frame 120.
  The first behavioral divergence at f=300 is now the remaining CD command cadence;
  the command-50 boot handoff fell from **+207,707 cycles / +86,511 instructions**
  to **-818 / -376**. See [`DifferentialHarness.md`](DifferentialHarness.md).
- HQx 2x/3x/4x now run as GPU rule-table passes in OpenGL/GLES, Direct3D 11, and
  Vulkan. At 4K on the RTX 4070 present is **0.14-0.34 ms** and the GPU pass is
  **0.09-0.17 ms**, down from 6.82/10.93 ms CPU-bound.
- The compatibility-completion corpus reports **1,371 images and 414 unique titles**.
  **375 of 378** game-test-set titles are playable under the binary beyond-title rule,
  with **0 `Behind` and 0 `Regression`** rows and a strict superset of the 252-title
  peer union.
- The earlier full-collection loadability sweep covered **594 disc images**: **576
  rendered**, **13 black/blank**, and **5 not loadable** on the first pass. After
  triage: **593 loadable/rendered**, **1 known no-video case** (`Crash 'n Burn` Japan),
  and **0 not-loadable rows**.
- MMPX 2x and Catmull-Rom bicubic filtering are available in OpenGL/GLES, Direct3D 11,
  Vulkan, and the CPU fallback. Aspect presets now include 5:4, 16:9, 16:10, and 21:9.
- **Windows x64, Linux x64, and macOS arm64 are validated GPU targets.** Linux passes
  hardware OpenGL 4.2, Vulkan 1.2 through X11 in the probe and desktop shell, a 600 s
  real-device audio soak with 0 drops/underruns, evdev gamepads, display sleep, and the
  full acceptance pass on bare-metal hardware (2026-11-17). macOS arm64 passes the
  Vulkan/MoltenVK presenter, all eight filters, the full suite, and a 60 fps packaged
  game (2026-11-17). Linux arm64 and Windows arm64 pass the native CLI/frontend runtime
  smoke (2026-11-10). Intel macOS is unsupported as of 1.12.0.
- v1.14.2 is published on [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.14.2)
  as one bundle per system and architecture (Windows/Linux, x64 and arm64; macOS arm64),
  each with the CLI, desktop app, and SHA-256 checksums; macOS is distributed as a DMG.
  The .NET 11 runtime is embedded in the self-contained apphosts. See
  [What's new](WhatsNew.md).

See [Status](Status.md) and [Known issues](KnownIssues.md) for the current position and
the remaining work.

## Documentation map

### Using 5DO

- [Installation](Installation.md) — source builds, CI artifacts, platform notes, data folders.
- [User guide](UserGuide.md) — desktop shell, controls, filters, saves, library, audio, logs.
- [FAQ](FAQ.md) — common questions and short answers.
- [Compatibility browser](CompatibilityBrowser.md) — filterable per-game sweep results with screenshots and evidence.
- [Compatibility notes](Compatibility.md) — BIOS, disc, input, and game-fix compatibility.
- [Game compatibility list](GameCompatibilityList.md) - 241-title 4DO reference and 5DO status.
- [5DO vs 4DO vs Phoenix](CompatibilityComparison.md) - unique-title comparison across all three emulators.
- [Game compatibility results](GameCompatibilityResults.md) - per-disc sweep evidence.
- [Known issues](KnownIssues.md) — current game, renderer, platform, and packaging limits.
- [Troubleshooting](Troubleshooting.md) — symptom-driven diagnosis.
- [Glossary](Glossary.md) — hardware, emulator, renderer, audio, and release terms.

### Reference

- [CLI reference](CliReference.md) — commands, options, exit codes, JSON, and file formats.
- [Configuration](Configuration.md) — `settings.json`, profiles, paths, input, NVRAM, environment.
- [Cover art](CoverArt.md) — online catalog, WebP pack, matching, R2 hosting, and takedown.
- [Save states](SaveStates.md) — `.tdos` v3 layout, validation, slots, and diffing.
- [Cheats](Cheats.md) — per-game code syntax, desktop manager, and CLI.
- [TAS movies](Tas.md) — `.5domovie` recording, replay, verification, branching, and editing.
- [Audio](Audio.md) — DSP clock, OpenAL output, WAV capture, tap, analysis, and pacing.
- [Video](Video.md) — VDLP, frame assembly, OpenGL/GLES pipeline, filters, and probes.
- [Vulkan](Vulkan.md) — optional Vulkan backend, SPIR-V pipeline, measurements, and limits.
- [Diagnostics](Diagnostics.md) — logs, traces, watchpoints, dumps, probes, and bundles.

### Development and maintenance

- [Architecture](Architecture.md) — dependency rules, runtime composition, and state ownership.
- [Developer guide](DeveloperGuide.md) — source map, invariants, extension points, and conventions.
- [Build and test](BuildAndTest.md) - prerequisites, build/test/format, benchmarks, publish, CI.
- [Installers and file associations](Installers.md) - Windows/Linux/macOS packages, signing, and MIME/registry hooks.
- [Test strategy](TestStrategy.md) — unit, integration, golden, differential, fuzz, and performance testing.
- [Porting notes](PortingNotes.md) — C++ to C# rules and reference quirks.
- [Reference strategy](ReferenceStrategy.md) — Opera/libopera hierarchy and differential procedure.
- [Differential harness](DifferentialHarness.md) — manifests, checkpoint hashes, reports, and allowlists.
- [Trimming](Trimming.md) — trim-safe JSON, trimmed publishes, and NativeAOT notes.
- [Documentation guide](Documentation.md) — build the site, preview locally, and publish the wiki.
- [Status](Status.md) — current position, completion estimate, and next steps.
- [Roadmap](Roadmap.md) — concise milestone summary.
- [Implementation plan](ImplementationPlan.md) — consolidated achievable backlog after compatibility completion.
- [Implementation plan archive](ImplementationPlanArchive.md) — completed log, stable IDs, evidence, and history.
- [Release checklist](ReleaseChecklist.md) — v1.1 scope and gates.
- [Release legal review](ReleaseLegalReview.md) — attribution and distribution decisions.
- [OpenAL Soft notice](OpenALSoft.md) — LGPL notice, source offer, and relinking rights.
- [Test corpus](TestCorpus.md) - machine-specific BIOS/disc evidence commands (not committed assets).
- [PAL library validation](PalValidation.md) - Europe-only matrix captures with the documented PAL BIOS.
- [Documentation source guide](DocumentationSource.md) — conventions for editing this documentation set.

## Project links

- [Repository](https://github.com/purelogiccode/5DO)
- [Wiki](https://github.com/purelogiccode/5DO/wiki)
- [Discussions](https://github.com/purelogiccode/5DO/discussions)
- [Issues](https://github.com/purelogiccode/5DO/issues)
- [Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md)
- [Code of conduct](https://github.com/purelogiccode/5DO/blob/master/CODE_OF_CONDUCT.md)
- [Security policy](https://github.com/purelogiccode/5DO/blob/master/SECURITY.md)
- [Changelog](https://github.com/purelogiccode/5DO/blob/master/CHANGELOG.md)

---

5DO is **donationware**: non-commercial, source-available software distributed under
the FreeDO modified GNU LGPL. "3DO" and "Panasonic" are trademarks of their respective
owners. 5DO is not affiliated with or endorsed by them.
