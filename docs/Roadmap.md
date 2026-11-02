# Roadmap

This is the short public roadmap. The authoritative maintainer checklist is
[ImplementationPlan.md](ImplementationPlan.md), and the current position is
[Status.md](Status.md).

## Current phase

The project is pre-1.0 and the v1.0 release is deliberately deferred.

**M13 — compatibility sweep and status merge**

- The first full-collection loadability sweep is complete: 594 images tested.
- After triage, 593 are loadable/rendered, with one known no-video case
  (`Crash 'n Burn` Japan) and zero not-loadable rows.
- Remaining work: BIOS-aware reruns, high-resolution sweep evidence, Linux VM
  validation, and a reviewed per-title status column in the compatibility list.

**M14 — high-resolution CEL research**

- Measure the current opt-in 640×480 mode.
- Prototype true internal high-resolution and 2× supersampling approaches.
- Decide whether the feature ships, remains opt-in, or moves to the post-1.0 backlog.

## Release blockers

A stable release is not cut until:

1. The compatibility sweep and high-resolution decision are complete.
2. The legal/attribution review and distribution model are signed off.
3. CI is green on the supported matrix and the release artifacts pass the soak.
4. Known issues and supported-platform claims are accurate.

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
