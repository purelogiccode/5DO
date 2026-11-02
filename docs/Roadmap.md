# Roadmap

This is the short public roadmap. The authoritative maintainer checklist is
[ImplementationPlan.md](ImplementationPlan.md), and the current position is
[Status.md](Status.md).

## Current phase

The project is at the **v1.0.0 documented-compatibility release** (published on GitHub).

**M13 — compatibility sweep and status merge**

- Loadability is complete: 594 images tested, **593 render**, 1 known no-video case
  (`Crash 'n Burn` Japan), and zero not-loadable rows.
- The in-game/visual review (M13.5) and high-resolution pass (M13.6) are post-1.0.
- Linux x64 full suite passed; GPU presentation is preview/best-effort.
- The remaining v1.0 work is the tag and artifact attachment.

**M14 — high-resolution CEL research (post-1.0)**

- Measure the current opt-in 640×480 mode.
- Prototype true internal high-resolution and 2× supersampling approaches.
- Decide whether the feature ships, remains opt-in, or moves to the post-1.0 backlog.

## Release status

v1.0.0 is published on
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0) as one
bundle per system (Windows/Linux/macOS x64) with the CLI and desktop app. The .NET 10
runtime is required on the host and is not bundled.

Full compatibility and M14 research are **not** v1.0 gates. CI is green, the soak is
complete, and the legal/attribution sign-off is recorded.

## Post-1.0

- Full-race and long-playthrough evidence for the remaining scripted titles.
- Raspberry Pi 4/5 and GLES 3.1 validation; arm64 hardware coverage.
- Tier B performance timing, DPI validation, and presentation polish.
- Installers, signing, and file associations.
- Optional managed IL JIT for guest ARM blocks.
- Additional metadata providers and media in the library.
- More TAS tooling, Lua scripting, and an input timeline.
- Full PAL library validation and CD subcode work.

Out of scope for v1: netplay, achievements, a full debugger UI, and 4K *internal* CEL
rendering. 4K output upscaling is supported.

## How to help

Compatibility reports with exact BIOS/disc hashes, frame counts, and privacy-scrubbed
diagnostics are the most useful contribution right now. See
[Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md) and
[Compatibility](Compatibility.md).
