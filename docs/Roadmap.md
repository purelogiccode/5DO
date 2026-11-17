# Roadmap

This is the short public roadmap. The authoritative active plan is
[ImplementationPlan.md](ImplementationPlan.md), and the current position is
[Status.md](Status.md).

## Current phase

The project is at the **v1.10.2 Windows audio and disc-container patch release**
(published on GitHub). The compatibility plan is complete:

- **378 game-test-set titles** are classified beyond the title screen with dated evidence.
- **375 are playable**; the other three are non-retail/add-on images with documented root
  causes. The comparison has **0 `Behind` and 0 `Regression` rows**.
- 5DO is a strict superset of the **252-title peer union**, ahead of 4DO's **167** and
  Phoenix's **233** playable titles.
- The six framework-dependent bundles and `SHA256SUMS.txt` are published; macOS ships as
  a DMG. Windows x64 is the validated desktop; Linux x64 build/test and smoke pass, and
  macOS/arm64 native runtime smoke passes. GPU presentation on non-Windows hosts remains
  best-effort.
- v1.10.2 adds DiscJuggler `.cdi` and Nero `.nrg` disc images and fixes the Windows
  bundle's loader-named OpenAL runtime. The v1.10.1 feature set remains:
  RetroAchievements save-state hit persistence, the `FiveDo` desktop apphost, and
  deferred/ReadyToRun startup. The v1.10.0 feature set remains: managed rcheevos
  evaluation, 3DO disc identification, measured progress, a durable offline unlock
  queue, hardcore rules, rich presence, hardcore-only leaderboard submission, OSD
  notifications, and an achievement window.
  The v1.9.1 feature set remains: nine UI catalogs, reversible locale switching, the
  two-step Escape pause/quit shortcut, external metadata and media, physical
  optical-drive boot, Windows DirectInput/HID gamepads, the controller preview, auto-crop,
  display-sleep inhibition, Lua input scripting, the input timeline, and 4DO state interop.

## Post-1.10.2

**Playability depth (in progress):**

- Tier C gameplay-depth pass: the first increment promoted eight titles to `Good`
  (Olympic Soccer, John Madden Football, Crime Patrol, Mad Dog II, Shootout at Old
  Tucson, Guardian War, Hell - A Cyberpunk Thriller, and Slayer). The second increment
  added pointer-device input scripts and promoted four more: **Carrier - Fortress at
  Sea**, **Family Feud**, **Insect War**, and **N. O. B.**. The P0 pass promoted
  **BladeForce**, **Off-world Interceptor**, and **PGA Tour 96**, and opened the
  Jurassic Park Security Systems terminal. The ledger is **91 `Good`, 10 `Playable`**;
  the remaining game rows are DeathKeep, Jurassic Park, and Kingdom, alongside seven
  media/reference/add-on discs.
- Full-race Road Rash evidence and a longer Alone inventory/exploration sequence.
  Alone now reaches the room and the Actions menu; Road Rash still cycles through
  wrecks under the autopilot.
- The opt-in **true 2× CEL** path (`--true-2x`) renders independent 480-line CEL fields;
  it is validated on Jurassic Park Interactive's Raptor Maze and Family Feud and leaves
  bitmap/SPORT content line-doubled. The host gates it to those two titles; other games,
  including Road Rash, fall back to native 320x240. The legacy `--high-res` line-doubled
  mode remains available. See [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

**Accuracy residuals (not release blockers):**

- Road Rash's long-run frame divergence from the reference core.
- Jurassic Park Interactive's default FreeDO CEL model runs the Raptor Maze ahead of the
  reference; at 65,000 the reference is still on the 63,000 corridor frame. The
  Opera-busy model matches the reference frame hash exactly. Set the per-game CEL timing
  to **Opera busy** for
  reference pacing. The game now reaches in-park gameplay; the driving/Spitter minigames
  are not yet scripted.
- CD command-cadence residual and audio-only BIOS CD-player transport (the TOC now
  lists; playback remains a documented BIOS limit).
- Crash 'n Burn (Japan) BIOS/dump boot limitation.

**Platform and product:**

- **Linux GPU validation and Linux-specific features.** A bare-metal Linux Mint 22.3
  host (Intel HD 4000, Mesa 25.2.8, hardware OpenGL 4.2, Vulkan 1.2 `hasvk`, 1920x1080
  X11, HDA audio) replaces the llvmpipe-only VM for GPU evidence. Hardware GL
  presentation and Vulkan X11 (probe and desktop shell child surface) are validated
  (2026-11-16); Wayland is documented as unsupported and display-sleep inhibition uses
  the screen-saver D-Bus API. Remaining: real-device audio soak, evdev gamepad hardware,
  a full real-machine acceptance pass, and the desktop exit segfault tracked as
  `PLAT-13`. `hasvk` is the legacy/integrated validation target.
- GLES 3.1 and Tier B timing validation. Native CLI/frontend runtime smoke now passes on
  macOS x64/arm64, Linux arm64, and Windows arm64 (2026-11-10); desktop arm64 is no
  longer an open platform item.
- M14 high-resolution CEL research.
- Signed/notarized installers. Unsigned Windows/Linux/macOS packaging and file
  associations are in place (2026-11-10); macOS packaging needs a macOS host, and
  signing remains conditional on certificates.
- Additional metadata providers and media in the library.
- More TAS tooling: multi-player Lua authoring and multi-player timeline editing
  (single-player Lua scripting and the input timeline shipped in UX-29).
- Best-effort 4DO `.4dosav` import/export (UX-30); native 4DO state compatibility is
  not a goal.
- CD subcode is complete (2026-11-10); full PAL library validation was completed on
  2026-11-10.

Out of scope for v1: netplay, achievements, a full debugger UI, and 4K *internal* CEL
rendering. 4K output upscaling is supported.

## How to help

Compatibility reports with exact BIOS/disc hashes, frame counts, and privacy-scrubbed
diagnostics are the most useful contribution right now. See
[Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md) and
[Compatibility](Compatibility.md).
