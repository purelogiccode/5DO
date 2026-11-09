# Roadmap

This is the short public roadmap. The authoritative active plan is
[ImplementationPlan.md](ImplementationPlan.md), and the current position is
[Status.md](Status.md).

## Current phase

The project is at the **v1.6.0 compatibility-completion release** (published on GitHub).
The compatibility plan is complete:

- **378 game-test-set titles** are classified beyond the title screen with dated evidence.
- **375 are playable**; the other three are non-retail/add-on images with documented root
  causes. The comparison has **0 `Behind` and 0 `Regression` rows**.
- 5DO is a strict superset of the **252-title peer union**, ahead of 4DO's **167** and
  Phoenix's **233** playable titles.
- The six framework-dependent bundles and `SHA256SUMS.txt` are published. Windows x64 is
  the validated desktop; Linux x64 build/test and smoke pass in CI, and macOS remains
  best-effort.

## Post-1.6

**Playability depth (next):**

- Tier C gameplay review: promote the remaining `Playable` titles to `Good` with
  title-specific scripts, reasons, and evidence.
- Full-race and long-playthrough evidence for Road Rash, Alone in the Dark, and the
  remaining scripted titles.

**Accuracy residuals (not release blockers):**

- Road Rash's long-run frame divergence from the reference core.
- CD command-cadence residual and CD-DA audio-only TOC/track-list evidence.
- Crash 'n Burn (Japan) BIOS/dump boot limitation.

**Platform and product:**

- GLES 3.1, desktop arm64, and Linux/macOS GPU validation.
- M14 high-resolution CEL research.
- Installers, signing, and file associations.
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
