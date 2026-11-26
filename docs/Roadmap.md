# Roadmap

This is the short public roadmap. The authoritative active plan is
[ImplementationPlan.md](ImplementationPlan.md), and the current position is
[Status.md](Status.md).

## Current phase

The project is at **v1.17.1** (published on GitHub). Windows x64 and Linux x64 are the
validated desktop targets; macOS arm64 renders through Vulkan/MoltenVK; Linux arm64 and
Windows arm64 pass native CLI/frontend runtime smoke. The compatibility program is
complete on the current corpus:

- **378 game-test-set titles** are classified beyond the title screen with dated evidence.
- **375 are playable**; the other three are non-retail/add-on images with documented root
  causes. The comparison has **0 `Behind` and 0 `Regression` rows**.
- 5DO is a strict superset of the **252-title peer union**, ahead of 4DO's **167** and
  Phoenix's **233** playable titles.
- Five self-contained bundles are published (Windows/Linux x64+arm64 and macOS arm64);
  Linux also has `.deb`, `.rpm`, AppImage, and Flatpak assets. The .NET 11 runtime is
  embedded, so no separate runtime is required. Intel macOS is unsupported.
- The current release retains rolling autosave/auto-resume, ZIP/7z ISO/BIN/CUE+BIN
  containers, physical CD-DA and read-error recovery, the Steam Deck first-run layout,
  Arcade CRT, RetroAchievements, TAS movies, rewind/run-ahead, cheats, and the library.

## Next (post-1.17.1)

The full ordered backlog is in [ImplementationPlan.md](ImplementationPlan.md). The
short version:

**Compliance and evidence**

- The 2026-11-25 `REL-13` decision retains the FreeDO modified GNU LGPL as
  non-commercial donationware; the 4DO game database is a separate GPL-2.0 data
  component. `REL-12` documentation/claims consistency is complete. Official
  Debian/Ubuntu/Fedora/openSUSE archives and Flathub are not eligible under those terms.
- `REL-14` RetroAchievements hardcore client approval (request submitted; awaiting
  external review).
- `TEST-09` full-library soak: all 1,371 images at 10,000 frames and all 414 unique
  titles at 30,000 frames, with per-title hashes, audio, exit status, and captures.

**Platform and distribution**

- `PLAT-06` GLES 3.1/DPI/Tier B validation; `PLAT-17` Retina 2x and `PLAT-05` macOS
  gamepad hardware evidence. `PLAT-19` native Wayland Vulkan is blocked until Avalonia
  officially supports Wayland.
- `REL-08` AUR/AppImageHub/itch.io listings and `REL-10` EmuDeck/RetroDeck integration;
  `REL-09` Flatpak sandbox/repository work once the licence position is settled.
- `UX-16` signed Windows/macOS artifacts when the certificate and Apple account are
  available.

**Product and rendering**

- `UX-33` GPU letterbox/pillarbox void patterns, one presenter at a time.
- `UX-38` video recording with synchronized audio.
- `SYS-25` live CPU-clock probe; `NET-01` netplay loopback probe (commit only if the
  probe is clean).
- `SYS-29` representative real 3DO MDS/MDF and CloneCD CCD/IMG/SUB validation; the
  readers and synthetic data/audio tests are complete.
- `VID-20` true-2x CEL production completeness; `VID-21` GPU CEL rasterization as a
  bounded probe. `VID-22` high-refresh frame interpolation shipped in 1.17.x and is
  documented in [Performance.md](Performance.md) §8.9.

**Accuracy residuals (not release blockers)**

- Road Rash's long-run frame divergence from the reference core.
- Jurassic Park Interactive's default FreeDO CEL model runs the Raptor Maze ahead of the
  reference; the Opera-busy model matches the reference frame hash. Set the per-game CEL
  timing to **Opera busy** for reference pacing.
- CD command-cadence residual and audio-only BIOS CD-player transport (the TOC lists;
  playback remains a documented BIOS limitation).
- Crash 'n Burn (Japan) BIOS/dump boot limitation.

## Out of scope for 1.x

Netplay is a candidate (`NET-01`), not a committed feature. The following are explicitly
out of scope unless the product scope changes:

- a full debugger UI;
- official Debian/Ubuntu/Fedora/openSUSE archives and Flathub under the retained
  non-commercial FreeDO terms;
- BIOS-less/embedded boot ROM or Opera-style HLE boot (5DO requires a real BIOS);
- a custom shader pipeline;
- Discord Rich Presence;
- cloud saves;
- Android, iOS, WebAssembly, a libretro core, or Raspberry Pi;
- 4K *internal* CEL rendering (4K output upscaling is supported).

## How to help

Compatibility reports with exact BIOS/disc hashes, frame counts, and privacy-scrubbed
diagnostics are the most useful contribution right now. See
[Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md) and
[Compatibility](Compatibility.md).
