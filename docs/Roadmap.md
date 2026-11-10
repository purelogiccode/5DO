# Roadmap

This is the short public roadmap. The authoritative active plan is
[ImplementationPlan.md](ImplementationPlan.md), and the current position is
[Status.md](Status.md).

## Current phase

The project is at the **v1.7.0 platform and media-polish release** (published on GitHub).
The compatibility plan is complete:

- **378 game-test-set titles** are classified beyond the title screen with dated evidence.
- **375 are playable**; the other three are non-retail/add-on images with documented root
  causes. The comparison has **0 `Behind` and 0 `Regression` rows**.
- 5DO is a strict superset of the **252-title peer union**, ahead of 4DO's **167** and
  Phoenix's **233** playable titles.
- The six framework-dependent bundles and `SHA256SUMS.txt` are published; macOS ships as
  a DMG. Windows x64 is the validated desktop; Linux x64 build/test and smoke pass, and
  macOS/arm64 native runtime smoke passes. GPU presentation on non-Windows hosts remains
  best-effort.

## Post-1.7

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

**Accuracy residuals (not release blockers):**

- Road Rash's long-run frame divergence from the reference core.
- CD command-cadence residual and audio-only BIOS CD-player transport (the TOC now
  lists; playback remains a documented BIOS limit).
- Crash 'n Burn (Japan) BIOS/dump boot limitation.

**Platform and product:**

- GLES 3.1, Linux/macOS GPU presentation, and Tier B timing validation. Native
  CLI/frontend runtime smoke now passes on macOS x64/arm64, Linux arm64, and Windows
  arm64 (2026-11-10); desktop arm64 is no longer an open platform item.
- M14 high-resolution CEL research.
- Signed/notarized installers. Unsigned Windows/Linux/macOS packaging and file
  associations are in place (2026-11-10); macOS packaging needs a macOS host, and
  signing remains conditional on certificates.
- Additional metadata providers and media in the library.
- More TAS tooling, Lua scripting, and an input timeline.
- CD subcode is complete (2026-11-10); full PAL library validation was completed on
  2026-11-10.

Out of scope for v1: netplay, achievements, a full debugger UI, and 4K *internal* CEL
rendering. 4K output upscaling is supported.

## How to help

Compatibility reports with exact BIOS/disc hashes, frame counts, and privacy-scrubbed
diagnostics are the most useful contribution right now. See
[Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md) and
[Compatibility](Compatibility.md).
