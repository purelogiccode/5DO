<a id="master-dashboard"></a>
# 5DO — Master Implementation Plan, Feature Backlog, and World-Best Roadmap

**Snapshot:** 2026-11-04
**Repository:** <https://github.com/purelogiccode/5DO><br>
**Branch:** `master` (latest code commit on `master`)<br>
**Status:** **1,619 tests pass**; `dotnet build` reports **0 warnings / 0 errors**; `dotnet format --verify-no-changes` is clean; `jb inspectcode --severity=WARNING` reports **0 results**.<br>
**Purpose:** one source of truth for what is done, what is missing, what to work on next, and how each feature is specified and tested.

> **This file consolidates and supersedes:**
> - `docs/ImplementationPlan.md` — now **Part I** (detailed milestones and progress log)
> - `5DO-Feature-Spec.md` — now **Part II** (feature specifications and acceptance criteria)
> - `Make5doTheBestOfAll.md` — now **Part III** (gap analysis, strategy, Opera parity audit)
> - `docs/NextSession.md` — now **Part IV** (session handoff, evidence, commands, do-not-redo notes)
>
> The two root files were removed after the merge. `docs/NextSession.md` is retained as a short redirect to Part IV. All future plan updates go in this file.

---

## 0. Current status at a glance

| Area | Current state | Evidence / next gate |
|---|---|---|
| Core emulation | ARM60, CLIO, MADAM/CEL, DSP, SPORT, XBUS, CD-ROM, input, NVRAM, save states implemented | M0–M8 complete; accuracy parity work remains in CORE-01..CORE-09 |
| Frontend | Avalonia shell, library, settings, gamepad, logs, screenshots, rewind, fast-forward, cheats, TAS movies, disc browser, BIOS/NVRAM manager, OSD/quick menu | UX-01/02/04/11/12/19/23 done; UX-06/13 partial |
| Renderer | Two-stage OpenGL/GLES (default), optional Vulkan and Direct3D 11 (Windows), 4K output, nearest/accurate/sharp-bilinear/CRT/xBR/HQx/FSR | M9R/M9P core complete; D3D11 backend implemented (M9R.14/VID-15); GLES 3.1, Tier B, and GPU CI remain |
| Audio | Exact 44.1 kHz DSP clock, OpenAL Soft, WAV capture, tap, analysis, audio-driven pacing | AUD-01/02/04/05 remain for full settings/mixer/golden coverage |
| Disc support | ISO, BIN, CUE+BIN, CHD parent/child, multi-track CUE/CHD TOC, CD-DA sectors/mixing/volume, M3U multi-disc playlist and live swap, Opera file system, prefetch, disc browser | ODE guest injection, known-title CD-DA evidence, read-ahead/subcode, PAL remain (SYS-01/02/03) |
| Compatibility sweep | 594 discs: **594 load**, **593 render**; 1 known boot failure (Crash 'n Burn Japan); 0 not loadable | v1.0 ships with this documented evidence; 12,000-frame in-game/visual review and high-res pass are post-1.0; Linux x64 full suite passed (GL preview) |
| Differential harness | 5DO vs OperaCore harness, checkpoints, audio/state hashes, smoke + five-title showcase | Full 594-disc manifest and nightly dashboard remain (CORE-11/FND-05) |
| Tests | **1,619** passing | Target 2,000+ with coverage gate (ENG-09) |
| Performance | Road Rash 3D race **4.20 ms/frame**, p99 **7.40 ms**; 4K GPU probe measured | Tier B, all-filter 4K60 budgets remain |
| Platforms | Windows x64 validated; Linux x64 full suite **1,473/1,473** passed (GL preview); six unified x64/arm64 bundles (Windows/Linux/macOS) with framework-dependent single-file apphosts; .NET 10 runtime required (not bundled); opt-in NativeAOT | macOS/installers are post-1.0; Linux GPU presentation is unvalidated under llvmpipe. WebAssembly, Android/iOS, and Raspberry Pi are out of scope |
| Release | **v1.2.1 released (GitHub)** | Documented-compatibility scope; legal/attribution sign-off recorded; M13 loadability evidence complete; M14 is post-1.0 research; Linux x64 full suite passed; six x64/arm64 bundles published; tags `v1.0.0`, `v1.1.0`, `v1.2.0`, and `v1.2.1` |

**Current position:** the core and shell are strong enough to play and validate a large local collection, and the documented-compatibility v1.0 scope is ready to ship. Full compatibility and high-resolution CEL research are post-1.0; the remaining gap to “best in the world” is **accuracy parity, universal compatibility, and release trust**, not basic feature count.

**Scope decision (2026-11-02): Steam Deck support is dropped.** UX-08 is gamepad navigation and a big-picture desktop layout only; ECO-04 covers Linux/macOS validation, Flatpak/AppImage, the macOS gamepad backend, and Metal/MoltenVK. No Steam Deck controls, packaging, or validation are planned, and the platform is removed from the release gates and comparison tables.

**Scope decision (2026-11-02): WebAssembly, Android/iOS, and Raspberry Pi are dropped.**
ECO-02 (WebAssembly/browser), ECO-03 (Android/iOS frontend), and ECO-05
(Raspberry Pi/arm64/GLES optimization) are removed from the active feature checklist and
release gates. Their specifications remain in Part II for history. Desktop Windows,
Linux, and macOS (x64/arm64) remain supported; Pi-specific performance, packaging, and
validation are not planned. MOON-06 is narrowed to desktop platforms, and Pi 5 is removed
from the performance gates. Historical progress logs may still mention these platforms;
those entries are retained as history, not as current commitments.

**2026-11-02 hardening pass:** CORE-12's dedicated fuzz harness (six targets plus a
nightly workflow), VID-06's per-edge overscan and per-game crop profiles with a
configurable void border, and VID-14's gallery filters, side-by-side compare, export,
and settings sidecars are landed. **CORE-07** now measures FIQ request-to-vector
latency and traces requests/entries, **CORE-06** closes the output-FIFO reload,
DIPIR-protected XBUS DMA, MADAM MCTL persistence, and XBUS Opera poll/state gaps, and
**CORE-05** exposes DSP per-tick timing metrics. The suite is **1,619 tests**; build,
format, and inspectcode are clean.

---

## 1. How to use this document

1. **Start here.** Read the current status and the priority next actions.
2. **Pick one unchecked item.** Do not mix accuracy, rendering, and UX work in one change.
3. **Open the matching part** for the full specification, files, tests, and acceptance criteria.
4. **Mark it done** only when the feature-spec definition of done is satisfied (behavior + tests + docs + no regressions).
5. **Update this file** when an item lands: change `[ ]` to `[x]`, update the status text, add a progress-log entry, and refresh the evidence links.
6. **Keep the historical parts.** Parts I–IV preserve the detailed evidence and “do not redo” notes; do not delete them when updating the checklist.

### Legend

- `[x]` — **Done** and validated.
- `[ ]` — **Remaining**. The bold status says whether it is **Partial**, **Pending**, **In progress**, or **Blocked**.
- `**Partial**` — substantial implementation exists; the remaining work is listed after “remaining:”.
- `**Pending**` — not started, or only research/design exists.
- `**Blocked**` — depends on another item, legal review, or hardware.
- Priority: **P0** correctness/compatibility, **P1** expected best-in-class, **P2** ecosystem multiplier, **P3** long-term.
- Effort: **S** under 3 developer-days, **M** 3–10, **L** 10–30, **XL** 30–90+.

### Status source

The feature checklist below starts from the former `5DO-Feature-Spec.md` index (2026-10-31) and applies the 2026-11-01 session updates plus the 2026-11-02 Direct3D 11 plan and v1.0 release decisions. If you verify or land an item, update its checkbox and status here first, then update the detailed section in Part II and the evidence in Part IV.

---

## 2. Document map

| Part | Contents | Use it when |
|---|---|---|
| **Master Dashboard** (this section) | Current status, top actions, milestone checklist, full feature checklist, release gates, verification commands | Every session; choose the next task |
| **Part I — Detailed Implementation Plan** | M0–M15 milestones, architecture summary, reference map, risks, quality gates, open decisions, progress log | Planning a milestone or understanding how the project was built |
| **Part II — Feature Specification and Backlog** | Every CORE/SYS/VID/AUD/UX/ECO/ENG feature: problem, required behavior, files, tests, done-when | Implementing or estimating a feature |
| **Part III — Best-in-the-World Strategy and Gap Analysis** | Reference emulator profiles, scorecard, gap analysis, critical path, Opera parity audit, session logs | Prioritizing accuracy/parity and comparing against Opera/4DO |
| **Part IV — Session Handoff, Evidence, and Historical Fixes** | Per-pass evidence, commands, acceptance criteria, open questions, “do not do” list | Reproducing a fix or avoiding a regression |

---

## 3. Priority next actions

These are the highest-leverage items from the current evidence. Pick the first unchecked item that is not blocked.

- [x] **1. Linux x64 validation: complete (2026-11-02).** The full test suite (**1,473/1,473**), CLI `selftest`, `bench`, `info`, `bios info`, a 3,000-frame Wolfenstein boot, non-silent audio, and the frontend `--smoke-test` passed on Linux Mint 22.3. The GL probe timed out under llvmpipe, so Linux GPU presentation is preview/best-effort. See [`Compatibility.md`](Compatibility.md) §10.3.
- [ ] **2. Cut the v1.0.0 release.** Bump `FiveDoVersion.Version` to `1.0.0`, tag `v1.0.0`, attach the win-x64/linux-x64 archives and checksums, and record the release-owner/QA sign-off. Legal/attribution sign-off is already recorded. See [`ReleaseChecklist.md`](ReleaseChecklist.md).
- [ ] **3. Finish the M13.5 in-game/visual review (post-1.0).** Inspect every rendered final frame; rerun black/static titles at 12,000 frames; record `Unplayable`/`Playable`/`Good` in `GameCompatibilityList.md`. This is no longer a v1.0 gate.
- [ ] **4. Run the M13.6 high-resolution pass (post-1.0).** Repeat the sweep with `-HighRes`, compare screenshots and speed, and feed the result into M14.
- [ ] **5. Start M14 high-resolution CEL research (post-1.0).** Measure the current `--high-res` mode, prototype true 640x480 and 2x supersampling, and write the GPU CEL feasibility note.
- [ ] **6. Finish CORE-04 CD-ROM parity.** Complete command/timing parity, add command transcript + boot/reset tests, and keep CD-DA/multi-track as the next disc step. See Part II CORE-04 and Part III §13.3/§13.6.
- [ ] **7. Finish CORE-03 CEL busy timing parity.** Add differential evidence against Opera and validate timing-sensitive titles under the Accuracy profile. See Part II CORE-03.
- [ ] **8. Close CORE-01/CORE-02 accuracy profiles.** Complete ARM cycle-class and CLIO scheduler parity; keep the Accuracy profile opt-in until the full-collection differential is green. See Part II CORE-01/CORE-02 and Part III §13.6.
- [ ] **9. Land the disc/peripheral P0 batch.** SYS-01 multi-track TOC, raw CD-DA sectors, and mixing are implemented; AUD-05 persists CD-DA volume/mute in settings, UI, CLI, and state. SYS-02 now has an M3U playlist, live host swap, F3/UI swap, guest inject, and CLI swap. Finish known-title CD-DA evidence, ODE prompts, then SYS-03 PAL/NTSC, SYS-15 calibration/multiplayer pointer, SYS-18 auto profiles. See Part II.
- [ ] **10. Polish the shipped UX.** UX-06 Lua/frame editor/lag counter, UX-12 per-game NVRAM/drag-drop, UX-13 external metadata/media/editor, UX-19 gamepad quick-menu navigation. See Part II.
- [ ] **11. Post-1.0 platform and packaging work.** macOS validation, installers/signing/notarization, and the "best in the world" gates in §8.2. WebAssembly, Android/iOS, and Raspberry Pi are out of scope.
- [x] **12. Land the CORE-05/06/07 audit increment (2026-11-02).** FIQ request-to-vector latency telemetry and traces, the CLIO/DIPIR/FIFO/MCTL/XBUS audit fixes and tests, and DSP per-tick statistics are implemented. Evidence: **1,597 tests**, clean build/format/inspectcode, and a 3,000-frame Gex/Lemmings boot with the documented frame hashes intact. See section 9 and [`Diagnostics.md`](Diagnostics.md) section 7.

---

## 4. Milestone checklist

- [x] **M0 — Bootstrap and guardrails.** Repo, build, CI, docs, project skeleton.
- [x] **M1 — Core contracts, memory, bus, scheduler, state primitives.** Memory/bus/Quartz/state tests green.
- [x] **M2 — ARM60 CPU.** Full interpreter, instruction tests, synthetic ROM.
- [x] **M3 — Boot skeleton.** BIOS, CLIO, VDLP, SPORT, frame loop.
- [x] **M4 — MADAM graphics.** CEL engine, pixel pipeline, matrix engine, golden CEL tests.
- [x] **M5 — DSP and audio.** DSP core, audio pipeline, exact 44.1 kHz clock.
- [x] **M6 — XBUS, CD-ROM, storage.** ISO/BIN/CUE/CHD, command state machine, disc boot.
- [x] **M7 — Input, NVRAM, game database.** Playable controls, persistence, 518-record database.
- [x] **M8 — Save states, diagnostics, CLI.** Versioned/compressed states, tracing, CLI/JSON.
- [ ] **M9R — OpenGL/GLES renderer.** **Partial:** two-stage presenter, filters, 4K, parity, swap interval done; optional Vulkan and Direct3D 11 backends are implemented (M9R.11–M9R.14); GLES 3.1 and GPU CI remain.
- [ ] **M9P — Runtime performance, pacing, 4K presentation.** **Partial:** core thread, uploads, DPI, p99/allocation gates done; Tier B and GPU gates remain.
- [ ] **M10 - NativeAOT
- [ ] **M12 — guest ARM JIT.
- [ ] **M14 — High-resolution CEL research.** **Post-1.0 research:** true 640x480, 2x/4x supersampling, GPU CEL feasibility; not a v1.0 release blocker.
- [ ] **M15 — Differential harness and regression baseline.** **Partial:** harness + smoke + five-title showcase done; full-collection manifest, PC-delta triage, nightly dashboard remain.

---

## 5. Foundation checklist

- [ ] **[FND-01](#feat-fnd-01)** Feature flags, tuning, and profiles — P0 · M — **Partial**: `EmulationProfile`/`EmulatorTuning` exist; every machine option is not yet centralized, validated, and recorded in state.
- [x] **[FND-02](#feat-fnd-02)** Settings schema versioning and migration — P0 · S — **Done** — `SettingsVersion` 2, legacy migration, malformed-file backup, validation, atomic writes, per-game timing/CD-DA profiles, and `settings validate|migrate` are implemented.
- [x] **[FND-03](#feat-fnd-03)** Save-state format v3 — P0 · M — **Done**
- [ ] **[FND-04](#feat-fnd-04)** CLI option and JSON contract conventions — P0 · S — **Partial**: source-generated JSON and conventions exist; formal option table, help generation, and backward-compatibility policy remain.
- [ ] **[FND-05](#feat-fnd-05)** Differential harness and golden corpus — P0 · L — **Partial**: harness, checkpoints, audio/state hashes, smoke + showcase done; full 594-disc manifest, nightly trend dashboard, and PC/state/CD transcript comparison remain.
- [ ] **[FND-06](#feat-fnd-06)** UI settings and per-game profile framework — P0 · M — **Partial**: Video/Audio/Emulation/Paths tabs exist; per-game editor, search/filter, reset, and localized labels remain.
- [ ] **[FND-07](#feat-fnd-07)** Localization resource pipeline — P1 · S — **Pending**: resource files, `ILocalizer`, pseudo-locale, language selector, translator guide.
- [ ] **[FND-08](#feat-fnd-08)** Diagnostics, errors, and crash-bundle contracts — P1 · M — **Partial**: Serilog, in-app log viewer, bug reporting, CLI diagnostics, and the privacy-scrubbed `diagnostics bundle` exist; typed error codes and a compatibility-report form remain.

---

## 6. Full feature checklist

**Indexed feature status:** 25 done · 26 partial · 66 pending · 1 post-1.0 (118 active tracked features; 3 dropped-scope items are noted separately below; 10 optional moonshots are listed separately below). Foundations are listed separately above.

### Core accuracy

- [ ] **[CORE-01](#feat-core-01)** ARM60 cycle-class accuracy mode — P0 · M · depends on: FND-01 — **Partial** — remaining: option surface is implemented; full ARM60 cycle-class parity and differential evidence remain
- [ ] **[CORE-02](#feat-core-02)** CLIO timer scheduler parity — P0 · M · depends on: FND-01 — **Partial** — remaining: CLIO timer scheduler option is implemented; parity validation remains
- [ ] **[CORE-03](#feat-core-03)** MADAM CEL busy timing — P0 · L · depends on: FND-01 — **Partial** — remaining: MADAM CEL busy option is implemented; full timing parity remains
- [ ] **[CORE-04](#feat-core-04)** CD-ROM timing and command parity — P0 · XL · depends on: FND-01, CORE-11 — **Partial** — remaining: cycle-based data timing, the CD-DA/TOC/Sub-Q path, command/status transcripts, and CD-DA read-ahead are implemented; full command latency, subcode, DIPIR tagging, and raw-vs-Opera transcript diffs remain
- [ ] **[CORE-05](#feat-core-05)** DSP timing and optional threading — P0 · M · depends on: FND-01 — **Partial** — remaining: per-tick instructions, loop/sleep counts, average and peak instructions, and `run --json` DSP statistics are implemented; the optional bit-identical DSP worker thread remains
- [ ] **[CORE-06](#feat-core-06)** SPORT/XBUS/DMA timing audit — P0 · M · depends on: CORE-11 — **Partial** — remaining: output-FIFO reload gating, DIPIR-protected XBUS DMA, MADAM MCTL state persistence, XBUS Opera poll/FIFO semantics, DMA start/completion trace, and the PBUS/SPORT/XBUS audit tests are implemented; full Opera trace diffs remain
- [ ] **[CORE-07](#feat-core-07)** Interrupt latency / FIQ/IRQ audit — P0 · M · depends on: CORE-11 — **Partial** — remaining: request/entry cycle stamps, `FiqLatencyTracker`, `--fiq-trace` request/entry/latency lines, `run --json` FIQ statistics, masked/nested/return/re-entry tests, and CLIO/DIPIR/XBUS ordering tests are implemented; Opera differential traces remain
- [ ] **[CORE-08](#feat-core-08)** Accuracy/performance profiles — P0 · S · depends on: FND-01 — **Partial** — remaining: EmulationProfile/EmulatorTuning exist; full profile coverage and validation remain
- [x] **[CORE-09](#feat-core-09)** 4DO/Opera fix parity audit — P0 · M · depends on: SYS-17 — **Done**
- [ ] **[CORE-10](#feat-core-10)** Hardware test ROM suite — P0 · L — **Pending**
- [ ] **[CORE-11](#feat-core-11)** Full-collection differential harness — P0 · L · depends on: FND-05 — **Partial** — remaining: differential harness and five-title showcase exist; full 594-disc manifest, nightly dashboard, and PC-delta triage remain
- [x] **[CORE-12](#feat-core-12)** Fuzzing and parser security — P0 · M · depends on: FND-05 — **Done** — `tools/FiveDo.Fuzz` covers states, ISO/CHD headers, CUE text, Opera FS, CD command streams, and ARM60 instruction streams; deterministic seeds, reproduction artifacts, bounded xUnit smoke tests, and a nightly workflow are in place
- [ ] **[CORE-13](#feat-core-13)** Remaining compatibility failures - P0 - L - depends on: CORE-11 - **Partial** - the BIOS-aware extended-boot retry renders 593 of 594 discs; 5 former not-loadable rows and 12 black/blank titles are resolved; only Crash 'n Burn (Japan) remains a known boot failure
- [x] **[CORE-14](#feat-core-14)** Save-state format v3 — P0 · M · depends on: FND-03 — **Done**
- [x] **[CORE-15](#feat-core-15)** State diff and verification tooling — P1 · M · depends on: CORE-14 — **Done**
- [ ] **[CORE-16](#feat-core-16)** Frame/audio hash regression suite — P0 · M · depends on: CORE-11 — **Partial** — remaining: frame/audio hash regression suite is partial; full-collection coverage remains

### System, disc, BIOS, region, peripherals

- [ ] **[SYS-01](#feat-sys-01)** CD-DA / Red Book and multi-track discs — P0 · L · depends on: CORE-04 — **Partial** — remaining: multi-track CUE/CHD TOC, raw CD-DA sectors, TOC/Sub-Q commands, DSP mixing, volume UI, native-sector read-ahead, and `disc tracks --json` are implemented; known-title audio evidence and subcode remain
- [ ] **[SYS-02](#feat-sys-02)** Disc swap / ODE / multi-disc — P0 · M · depends on: CORE-04 — **Partial** — remaining: M3U playlist, host/F3/UI/CLI swap, guest inject, per-disc hash, guest-eject ODE auto-advance, and playlist editor/drop import are implemented; real in-game multi-disc swap evidence remains
- [ ] **[SYS-03](#feat-sys-03)** PAL/NTSC region modes — P0 · L · depends on: CORE-02 — **Partial** — PAL1/PAL2 timing, VDLP mapping, settings, CLI, desktop BIOS auto-switch, per-game region override, and state-driven switching are implemented; PAL2 title evidence and automatic profile application remain
- [x] **[SYS-04](#feat-sys-04)** BIOS manager and auto-selection — P0 · L · depends on: FND-02 — **Done**
- [ ] **[SYS-05](#feat-sys-05)** Embedded open-source boot ROM — P1 · M · depends on: SYS-04, ENG-03 — **Pending**
- [ ] **[SYS-06](#feat-sys-06)** RSA/unsigned-disc option — P1 · M · depends on: SYS-04, ENG-03 — **Pending**
- [ ] **[SYS-07](#feat-sys-07)** Memory capacity options — P1 · M · depends on: FND-01 — **Pending**
- [ ] **[SYS-08](#feat-sys-08)** CPU overclock/underclock — P1 · S · depends on: FND-01 — **Partial** — remaining: CPU clock option exists; broader validation remains
- [x] **[SYS-09](#feat-sys-09)** CD speed control — P1 · M · depends on: CORE-04 — **Done**
- [x] **[SYS-10](#feat-sys-10)** Random seed control — P1 · S · depends on: FND-01 — **Done**
- [x] **[SYS-11](#feat-sys-11)** MADAM matrix hardware/software — P1 · S · depends on: FND-01 — **Done** — the VDL display-control `clut_bypass` bit selects the fixed grayscale CLUT exactly as Opera does; the frame assembler, OpenGL palette shader, and Vulkan `Palette.frag` share the branch
- [ ] **[SYS-12](#feat-sys-12)** SWI HLE for Portfolio OS — P2 · L · depends on: CORE-01 — **Pending**
- [x] **[SYS-13](#feat-sys-13)** VDLP bypass CLUT — P1 · S · depends on: FND-01 — **Done**
- [x] **[SYS-14](#feat-sys-14)** Per-game NVRAM and versions — P1 · M · depends on: FND-02 — **Done**
- [ ] **[SYS-15](#feat-sys-15)** Mouse/lightgun/arcade lightgun/flightstick/trackball — P0 · L · depends on: FND-01 — **Partial** — remaining: device models, encoder, CLI/settings, pointer input, crosshair, pointer-port routing, and pointer calibration are done; scripted gameplay validation remains
- [ ] **[SYS-16](#feat-sys-16)** Six-player/multi-tap verification — P1 · S · depends on: SYS-15 — **Pending**
- [ ] **[SYS-17](#feat-sys-17)** Game database expansion and community DB — P1 · M · depends on: FND-02 — **Pending**
- [ ] **[SYS-18](#feat-sys-18)** Auto per-game profile — P0 · L · depends on: SYS-04, SYS-17 — **Partial** — auto BIOS/region/fixes/NVRAM, known-title input seeding, and a profile covering timing, region, BIOS, fix, filter, high-res, input devices, and NVRAM with import/export and machine-creation application are implemented; cheat fields, full DB-driven population, and live application remain
- [ ] **[SYS-19](#feat-sys-19)** Save-state interoperability with 4DO/Opera — P2 · L · depends on: CORE-14 — **Pending**
- [ ] **[SYS-20](#feat-sys-20)** Disc health / Redump DAT / CHD integrity — P2 · M · depends on: SYS-22 — **Partial** — `disc verify` reads every native sector, reports CHD/CUE/BIN errors, hashes the content, and matches Redump/No-Intro DATs with `--dat`; the library now keeps a self-invalidating health cache, shows known-dump/read-error badges and a health summary, and lets the desktop verify the whole library against a chosen DAT; 4DO/Opera hash comparison remains

### Video

- [ ] **[VID-01](#feat-vid-01)** True 640x480 / 480i internal rendering — P1 · XL · depends on: CORE-03 — **Pending**
- [ ] **[VID-02](#feat-vid-02)** 2x/4x supersampling — P2 · L · depends on: VID-01 — **Pending**
- [ ] **[VID-03](#feat-vid-03)** GPU CEL compositing — P3 · XL · depends on: VID-01 — **Pending**
- [ ] **[VID-04](#feat-vid-04)** 480i/240p detection, deinterlacing, field blending — P1 · M · depends on: SYS-03 — **Pending**
- [x] **[VID-05](#feat-vid-05)** PAL output timing — P1 · M · depends on: SYS-03 — **Done**
- [x] **[VID-06](#feat-vid-06)** Auto-crop, overscan, void area, gray border — P1 · M · depends on: FND-06 — **Done** — per-edge 0-32 px crop, per-game crop profiles, configurable 0-64 px void/gray border with `#RRGGBB`/`#AARRGGBB` color, Settings controls, and native-screenshot parity are implemented
- [ ] **[VID-07](#feat-vid-07)** Shader preset system — P1 · L · depends on: FND-06 — **Pending**
- [ ] **[VID-08](#feat-vid-08)** NTSC composite/S-Video/RF, HDR, gamma — P2 · L · depends on: VID-07 — **Pending**
- [ ] **[VID-09](#feat-vid-09)** Video recording — P1 · M · depends on: FND-06 — **Partial** — animated GIF recording is implemented in the CLI and desktop with a 3-3-2 palette and configurable frame rate; MP4/WebM, audio, and the replay buffer remain
- [ ] **[VID-10](#feat-vid-10)** Overlays, bezels, touch controls — P2 · M · depends on: FND-06 — **Pending**
- [ ] **[VID-11](#feat-vid-11)** Vulkan surfaces on Linux/macOS — P1 · L · depends on: ECO-04 — **Pending**
- [ ] **[VID-12](#feat-vid-12)** Run-ahead, frame delay, VRR, audio sync — P2 · L · depends on: CORE-14 — **Pending**
- [ ] **[VID-13](#feat-vid-13)** Video test suite and color accuracy — P1 · M · depends on: CORE-11 — **Pending**
- [x] **[VID-14](#feat-vid-14)** Screenshot gallery and metadata — P2 · S · depends on: FND-06 — **Done** — the gallery lists thumbnails newest-first, filters by game/date, opens/copies/exports/deletes captures, compares two side by side, and reads settings sidecars written with each screenshot
- [x] **[VID-15](#feat-vid-15)** Direct3D 11 renderer (Windows) — P2 · L · depends on: M9R.1, FND-06 — **Done** — FL11_0 device/swapchain, HLSL palette/filter passes, Settings/CLI selection, `--d3d-probe`, parity/shader tests, device-removal restart, and OpenGL-first fallback

### Audio

- [ ] **[AUD-01](#feat-aud-01)** Audio buffer/latency/device/resampler — P1 · M · depends on: FND-06 — **Partial** — OpenAL device enumeration/selection, 20-500 ms buffer latency, live output recreation, and latency display are implemented; resampler quality, exclusive mode, and sync offset remain
- [ ] **[AUD-02](#feat-aud-02)** Audio recording UI, mixer, spectrum — P1 · M · depends on: FND-06 — **Partial** — desktop WAV record/stop through a tee sink is implemented with file naming and statistics; per-channel mixer, spectrum, and format options remain
- [ ] **[AUD-03](#feat-aud-03)** DSP debugger — P2 · M · depends on: UX-21 — **Pending**
- [ ] **[AUD-04](#feat-aud-04)** Audio golden tests vs Opera/hardware — P1 · M · depends on: CORE-11 — **Pending**
- [x] **[AUD-05](#feat-aud-05)** CD-DA mixing and volume — P1 · S · depends on: SYS-01 — **Done** — CD-DA mixes into the 44.1 kHz DSP output with persisted volume/mute, Settings UI, `--cd-volume`/`--mute-cd`, and CD state v4.

### UX and quality-of-life

- [x] **[UX-01](#feat-ux-01)** Rewind — P1 · L · depends on: CORE-14 — **Done**
- [x] **[UX-02](#feat-ux-02)** Fast-forward / turbo — P1 · S · depends on: FND-01 — **Done**
- [ ] **[UX-03](#feat-ux-03)** Netplay — P1 · XL · depends on: CORE-11 — **Pending**
- [x] **[UX-04](#feat-ux-04)** Cheat engine — P1 · M · depends on: FND-06 — **Done**
- [ ] **[UX-05](#feat-ux-05)** Achievements — P2 · L · depends on: UX-03 — **Pending**
- [ ] **[UX-06](#feat-ux-06)** TAS tools — P1 · L · depends on: UX-01 — **Partial** — movie container, record/replay/verify, branching, CLI, shell controls, input display, lag frames, and the per-frame movie editor are done; Lua, an input timeline, multiplayer display, and the full TAS UI remain
- [ ] **[UX-07](#feat-ux-07)** Controller profiles and per-game input — P1 · M · depends on: FND-06 — **Partial** — per-game PBUS device overrides, automatic known-title lightgun/trackball defaults, and the override UI are implemented; named controller profiles, deadzones/curves, hot-plug prompts, and import/export remain
- [ ] **[UX-08](#feat-ux-08)** Gamepad navigation and big picture — P1 · L · depends on: FND-06 — **Pending**
- [ ] **[UX-09](#feat-ux-09)** Localization — P1 · M · depends on: FND-07 — **Pending**
- [ ] **[UX-10](#feat-ux-10)** Themes and accessibility — P2 · M · depends on: FND-06 — **Pending**
- [x] **[UX-11](#feat-ux-11)** Disc browser UI and game info — P1 · M · depends on: FND-06 — **Done**
- [x] **[UX-12](#feat-ux-12)** BIOS/NVRAM manager UI — P1 · M · depends on: SYS-04, SYS-14 — **Done**
- [ ] **[UX-13](#feat-ux-13)** Library metadata, favorites, playtime — P1 · L · depends on: SYS-17 — **Partial** — favorites, playtime, launch history, offline metadata, filters, sorts, and the per-game metadata editor are done; external Redump/No-Intro/IGDB/LaunchBox providers, screenshots/manuals/videos, and >=90% rich metadata remain
- [ ] **[UX-14](#feat-ux-14)** Auto-update and release channels — P1 · M · depends on: ENG-10 — **Pending**
- [x] **[UX-15](#feat-ux-15)** Portable mode, multi-instance, config profiles — P1 · S · depends on: FND-02 — **Done**
- [ ] **[UX-16](#feat-ux-16)** Installers, signing, notarization, file associations — P1 · L · depends on: ENG-10 — **Pending**
- [ ] **[UX-17](#feat-ux-17)** Crash report, compatibility report, log bundle — P1 · M · depends on: FND-08 — **Partial** — one-click desktop and CLI bundles include logs, scrubbed settings, environment, state, and frame evidence; upload and the compatibility-report form remain
- [ ] **[UX-18](#feat-ux-18)** Onboarding and verification — P1 · M · depends on: SYS-20 — **Pending**
- [x] **[UX-19](#feat-ux-19)** OSD, notifications, quick menu — P1 · S · depends on: FND-06 — **Done**
- [ ] **[UX-20](#feat-ux-20)** Per-game settings UI — P1 · M · depends on: SYS-18 — **Partial** — the profile editor (save/delete/import/export) now covers timing, region, BIOS, fix, filter, high-res, input devices, and NVRAM; cheat/shader-chain fields and inherit/reset remain
- [ ] **[UX-21](#feat-ux-21)** Debugger UI — P2 · L · depends on: CORE-15 — **Pending**
- [ ] **[UX-22](#feat-ux-22)** Trace viewer and performance profiler — P2 · M · depends on: UX-21 — **Pending**
- [x] **[UX-23](#feat-ux-23)** Input display and lag counter — P1 · S · depends on: UX-06 — **Done**
- [ ] **[UX-24](#feat-ux-24)** Lua scripting and automation — P3 · L · depends on: UX-06 — **Pending**

### Ecosystem and platforms

- [ ] **[ECO-04](#feat-eco-04)** Linux/macOS validation, Flatpak/AppImage, macOS gamepad/Metal — P1 · L · depends on: ENG-01 — **Pending**
- [ ] **[ECO-06](#feat-eco-06)** Plugin API — P3 · L · depends on: FND-01 — **Pending**
- [ ] **[ECO-07](#feat-eco-07)** Headless server / HTTP / WebSocket API — P3 · M · depends on: FND-04 — **Pending**
- [ ] **[ECO-08](#feat-eco-08)** 3DO modem/network emulation — P3 · L · depends on: CORE-04 — **Pending**
- [ ] **[ECO-09](#feat-eco-09)** 3DO CD player / Video CD — P3 · M · depends on: SYS-01 — **Pending**
- [ ] **[ECO-10](#feat-eco-10)** Documentation wiki and compatibility portal — P1 · M · depends on: SYS-17 — **Pending**
- [ ] **[ECO-11](#feat-eco-11)** Community workflow — P1 · S · depends on: ENG-10 — **Pending**

**Dropped scope (2026-11-02):** ECO-02 WebAssembly/browser, ECO-03 Android/iOS,
and ECO-05 Raspberry Pi/arm64/GLES are removed from the active roadmap. Their
specifications remain in Part II for history and are excluded from the completion
percentages.

### Engineering, legal, and trust

- [ ] **[ENG-01](#feat-eng-01)** CI matrix, nightly sweep, coverage, static analysis, SBOM, reproducible builds — P1 · M · depends on: FND-05 — **Pending**
- [ ] **[ENG-02](#feat-eng-02)** Performance: interpreter hot path, M12 JIT, GPU CEL, async shaders — P1 · XL · depends on: CORE-03 — **Pending**
- [x] **[ENG-03](#feat-eng-03)** Legal/licensing/attribution — P1 · M — **Done** — FreeDO modified GNU LGPL; free, open-source donationware (non-commercial, voluntary donations only); notices and OpenAL obligations shipped; project-owner sign-off recorded 2026-11-02.
- [ ] **[ENG-04](#feat-eng-04)** Accessibility — P2 · M · depends on: FND-06 — **Pending**
- [ ] **[ENG-05](#feat-eng-05)** Telemetry transparency and privacy controls — P2 · S · depends on: FND-08 — **Pending**
- [ ] **[ENG-06](#feat-eng-06)** Stability soak / crash-free — P1 · M · depends on: CORE-11 — **Pending**
- [ ] **[ENG-07](#feat-eng-07)** Documentation refresh — P1 · S — **Pending**
- [ ] **[ENG-08](#feat-eng-08)** Localization infrastructure — P1 · S · depends on: FND-07 — **Pending**
- [ ] **[ENG-09](#feat-eng-09)** Code coverage and benchmark regression — P1 · M · depends on: ENG-01 — **Pending**
- [ ] **[ENG-10](#feat-eng-10)** Release engineering and signing — P1 · M · depends on: ENG-03 — **Pending**

### Carried-forward backlog

- [ ] **BAK-06** Tier B 1080p60, 3D CEL profiling, hot-path optimization, relative gate — P1 · related: ENG-02, ENG-09 — **Pending**
- [ ] **BAK-07** MSIX/dmg/AppImage installers, signing/notarization, file associations — P1 · related: UX-16 — **Pending**
- [ ] **BAK-08** Frontend NativeAOT audio validation — P1 · related: ENG-10 — **Pending**
- [ ] **BAK-09** ViewModel tests and UI polish — P2 · related: ENG-09 — **Pending**
- [ ] **BAK-10** Optional M12 managed ARM JIT — P2 · related: ENG-02 — **Pending**
- [ ] **BAK-11** M14 high-resolution CEL research — P1 · related: VID-01..03 — **Post-1.0**
- [x] **BAK-12** Legal/attribution sign-off; green CI — P1 · related: ENG-03, ENG-01 — **Done** — legal sign-off recorded 2026-11-02; CI green recorded 2026-11-02.
- [ ] **BAK-13** BIOS-aware sweep and 5DO status merge — P1 · related: CORE-13, SYS-17/18 — **Pending**
- [ ] **BAK-14** 4DO `.4dosav` import/export — P2 · related: SYS-19 — **Pending**
- [ ] **BAK-15** PAL, CD-DA, netplay, rewind, debugger UI, peripherals — P1 · related: SYS-01/03/15, UX-01/03/21 — **Pending**
- [ ] **BAK-16** Windows/macOS file associations — P1 · related: UX-16 — **Pending**
- [ ] **BAK-17** Portable mode — P1 · related: UX-15 — **Pending**
- [ ] **BAK-18** macOS gamepad backend — P1 · related: ECO-04 — **Pending**
- [ ] **BAK-19** Vulkan X11/Wayland/Metal surfaces — P1 · related: VID-11 — **Pending**

### Moonshots (optional differentiators)

- [ ] **MOON-01** One-click BIOS-less universal boot - P2 - why: Removes the BIOS barrier for homebrew/demos.
- [ ] **MOON-02** Zero-config compatibility - P1 - why: Auto BIOS/region/fix/input profiles.
- [ ] **MOON-03** Public accuracy dashboard - P1 - why: Trust through transparent differential results.
- [ ] **MOON-04** Console-like big picture - P2 - why: 10-foot UI.
- [ ] **MOON-05** Creator suite - P2 - why: TAS + debugger + Lua + trace viewer.
- [ ] **MOON-06** Desktop everywhere - P2 - why: Windows, Linux, and macOS only; browser/mobile/Pi are out of scope.
- [ ] **MOON-07** Preservation - P1 - why: Redump, CHD, state import/export, movies.
- [ ] **MOON-08** Video quality - P2 - why: True high-res, shaders, HDR, NTSC, 4K60.
- [ ] **MOON-09** Performance leader - P1 - why: 4K60 on mid-range desktop, 1080p60 on Tier B desktop.
- [ ] **MOON-10** Community - P1 - why: Compatibility DB, plugins, docs, localization.


---

## 7. Opera parity checklist (current accuracy batch)

This is the execution checklist from the 2026-10-31 Opera/libopera audit. It is repeated here so the next accuracy task is visible without opening Part III. Full evidence and session logs are in Part III §13.

### Phase 1 — safe parity fixes

- [x] **OP-01** VDLP zero persistence / next-VDL / force line.
- [x] **OP-02** ARM immediate rotate carry.
- [x] **OP-03** DSP CPU-supplied FIFO.
- [x] **OP-04** MADAM arbitrary CEL span endpoint.
- [x] **OP-05** MADAM MARIA texel rendering.
- [x] **OP-06** MADAM literal LRFORM rows.
- [x] **OP-07** MADAM `PRE0_BPP=0` as 1.
- [x] **OP-08** MADAM packed CEL `SKIPX`.
- [x] **OP-09** MADAM blue-LSB substitution / transparency order.
- [x] **OP-10** MADAM CTL DMA flags.
- [x] **OP-11** CLIO FIFO completion / pointer reset / DMA-enabled reload.
- [x] **OP-12** MADAM REGCTL stride decoding.
- [x] **OP-13** MADAM PIXC `PDC_MFONLY`.
- [x] **OP-15** MADAM literal `SKIPX` clamp/empty span.

### Phase 2 — CEL origin

- [x] **OP-14** YOXY / CEL origin / `StoreXYPosFromDrawHeight`.
- [x] Retire `GraphicsStepY` and `BitTiming6` after title validation.
- [x] Keep legacy fix flags loadable for old states.

### Phase 3 — CD/XBUS/DIPIR/reset

- [ ] **OP-SYS-01** ROM overlay, DIPIR, XBUS media access — **Partial**: overlay and latch/abort logic done; DIPIR activation waits for OP-SYS-03.
- [ ] **OP-SYS-02** CD poll/media access/read-ahead/local reset — **Partial**: poll/reset/media done; CD-DA read-ahead and heuristic retirement pending.
- [ ] **OP-SYS-03** CLIO CSTAT soft reset and propagation — **Partial**: implemented and unit-tested; opt-in pending boot transcript.
- [ ] Command transcript and boot/reset tests.

### Phase 4 — memory, region, input, media

- [ ] **OP-SYS-04** Memory capacity and SPORT.
- [ ] **OP-SYS-05** PAL/NTSC timing.
- [x] **OP-SYS-06** PRNG state split/replay validation.
- [ ] **OP-SYS-08** Mouse/lightgun/arcade lightgun/flightstick/trackball — **Partial**: device models, encoder, CLI/settings, pointer input, and crosshair done; representative-title validation and calibration remain.
- [ ] **OP-SYS-09** ODE/CD-DA/multi-track.
- [x] **OP-SYS-10** CHD-zstd verification.

### Evidence and release

- [ ] Full 594-title differential run and allowlist.
- [ ] PC-delta triage for Gex/Road Rash/Lemmings.
- [ ] Per-title checkpoints in `benchmarks/diff/opera-parity.json`.
- [ ] Save-state migration tests for every version bump.
- [ ] Update `TestCorpus.md`, `Compatibility.md`, `PortingNotes.md`, and this checklist.
- [ ] No default timing change without full-collection evidence.

---

## 8. Release gates

### 8.1 v1.0 release gate (documented compatibility)

v1.0 is a **documented-compatibility** release. It does **not** require full compatibility, M14 research, or installers. A checked box must have evidence.

- [x] 594 local discs load; 593 render; 1 known boot failure (`Crash 'n Burn` Japan) is documented.
- [x] 12-title compatibility matrix with per-title status and known issues.
- [x] Windows x64 build, test, soak, renderer parity, and unified bundles validated.
- [x] Legal/attribution sign-off recorded: FreeDO modified GNU LGPL, free open-source donationware, no commercial use.
- [x] Build, 1,473 tests, format, inspectcode, and CI green (2026-11-02).
- [x] Produce and publish one runnable bundle per system (Windows/Linux/macOS x64) with the CLI and desktop app; a source-only tag is not a release.
- [x] Linux x64 validation passed on Linux Mint 22.3: full test suite **1,473/1,473**, `selftest`, `bench`, `info`, `bios info`, 3,000-frame Wolfenstein, non-silent audio, frontend `--smoke-test`; the GL probe timed out under llvmpipe, so Linux GPU presentation is preview/best-effort.
- [x] Bump to `1.0.0`, tag `v1.0.0`, attach unified bundles/checksums, and record release-owner/QA sign-off.

### 8.2 “Best in the world” gates (post-1.0)

These are **post-1.0 goals**, not v1.0 blockers. A release can be called “the best 3DO emulator in the world” only when all of these are true.

- [ ] 594/594 local discs load and render; **>=95% Playable/Good**; every failure documented.
- [ ] Automated differential harness runs nightly and shows **no unexplained divergence**.
- [ ] CPU, CLIO, MADAM, CD, DSP, SPORT, VDLP, and input devices match Opera or hardware on the test suite.
- [ ] PAL, CD-DA, multi-track, ODE, mouse, lightgun, arcade lightgun, flightstick, and trackball work.
- [ ] BIOS/region/fix/input auto-profiles make known titles “just work”.
- [ ] Rewind, fast-forward, cheats, netplay, achievements, TAS, shader presets, recording, debugger, and localization are complete (rewind/fast-forward/cheats/OSD/quick menu are already shipped).
- [ ] 4K60 on Tier A; 1080p60 on Tier B desktop; zero steady-state allocations.
- [ ] Windows/Linux/macOS x64/arm64 are validated. WebAssembly, Android/iOS, and Raspberry Pi are out of scope.
- [ ] Installers, signing, notarization, auto-update, portable mode, and file associations are shipped.
- [ ] 2,000+ tests, coverage gate, fuzzing, SBOM, and 24 h soak pass.
- [x] Legal/attribution sign-off is complete.
- [ ] Releases are signed and reproducible.
- [ ] Documentation, compatibility dashboard, and contribution workflow are public and current.

---

## 9. Recently completed fixes and verification

### 2026-11-02 — CORE-05/06/07 interrupt, DMA, and DSP audit increment

- [x] **CORE-07 telemetry.** `FiqLatencyTracker` records the cycle at which CLIO's active
  and enable masks first intersect, the CPU vector-entry cycle, the matched latency, and
  unmatched/coalesced requests. `Arm60.CyclesExecuted` and `FiqEntered` provide the CPU
  side; `Clio.CycleProvider`, `FiqRequested`, and active-reason properties provide the
  controller side; `FiveDoMachine.FiqLatency` exposes the counters.
- [x] `--fiq-trace` now writes separate `kind=request` and `kind=entry` lines with cycle,
  PC/return address, active reasons, `latency`, and `matched`; `run --json` adds a `fiq`
  object (requests, entries, matched/unmatched, coalesced, last/max/average latency).
- [x] Tests cover masked/unmasked entry, instruction-cycle latency, request matching,
  level-triggered re-entry, FIQ return/bank restoration, CLIO request transitions,
  timer requests, and a full machine request-to-entry round trip.
- [x] **CORE-06 fixes.** The CLIO output FIFO now gates next-descriptor reload on the
  `0x304` channel request bit (OP-11). A DIPIR-protected XBUS DMA leaves the enable bit
  set and makes no progress under `OperaDipir`. MADAM persists the MCTL-written gate
  (section version 3), so reset/state load no longer flips CLUT/video DMA gating. XBUS
  device-15 Opera semantics expose the pending status nibble, honor `POLREMASK`/`POLRE`,
  store only the interrupt-enable bits, use the MEI/CD/OK status layout, and raise the
  poll FIQ from the combined poll register. XBUS state load now rejects a state whose
  attached-device slots do not match instead of misaligning the stream.
- [x] CLIO DMA tracing adds a `clio dma-blocked` line and a `clio dma-done` line with the
  final target and FIQ reason. Tests lock the FreeDO PBUS DMA length/progress/byte-order
  behavior, all SPORT mask/high-resolution paths, CLIO DMA lengths and DIPIR blocking,
  output FIFO gating, XBUS built-in poll/status/FIQ behavior, and MADAM MCTL round trips.
- [x] **CORE-05 metrics.** `Dsp` reports loop calls, running/sleeping ticks, peak and mean
  instructions per tick; `run --json` adds `dspStats` and the text result prints a `dsp:`
  summary. The optional threaded DSP remains open.
- [x] Evidence: Gex (USA, Europe) 3,000 frames reports `0x05F38B50` (matching the
  documented key), 21,183 FIQ requests / 14,345 entries / max 859,234 cycles, and
  88,955,550 DSP instructions over 2,205,094 ticks (735 ticks/frame, max 106, mean 51.7).
  CLIO tracing recorded 4,714 XBUS DMA transfers with paired start/done lines.
- [x] Verification: **1,619 tests pass**; build **0 warnings / 0 errors**;
  `dotnet format --verify-no-changes` clean; `jb inspectcode --severity=WARNING` **0 results**.

### 2026-11-02 — VID-14 screenshot filters, compare, export, and sidecars

- [x] `ScreenshotLibrary.List` now filters by case-insensitive game text and an inclusive
  capture-time range; `ListGames` supplies distinct names. `Export` copies a capture to
  a new path with directory creation.
- [x] The gallery adds a game filter, date filter (all/today/7/30 days), export via the
  file picker, multi-select, and a side-by-side **Compare** window for exactly two
  captures. The selected capture's sidecar summary appears in the status line.
- [x] `ScreenshotMetadata` writes `<png>.json` with renderer, filter, aspect, region,
  high-resolution flag, crop, void border/color, timing profile, and CPU clock. Native
  companions fall back to the main capture's sidecar.
- [x] Tests: `ScreenshotLibraryTests` covers filtering, distinct names, and export;
  `ScreenshotMetadataTests` covers capture/save/load, native fallback, and invalid
  sidecars. The frontend smoke test constructs both gallery windows.
- [x] Verification: build clean, format clean, inspectcode 0 results, and **1,533** tests
  pass.

### 2026-11-02 — VID-06 per-edge overscan and void border

- [x] Added `CropInsets` with independent left/top/right/bottom edges, 0-32 px clamping,
  and a 16-pixel minimum visible size; the legacy uniform `OverscanPixels` remains a
  fallback until per-edge values are set.
- [x] Settings > Video now exposes **Overscan crop L/T/R/B**, **Void border** (0-64 px),
  and **Void color** (`#RRGGBB`/`#AARRGGBB`) with validation.
- [x] The game view crops before every presenter and native screenshot; the void border
  fills the area around the image, and pointer/lightgun mapping uses the inner video
  surface so aim stays aligned.
- [x] `GameProfile.Overscan` stores per-game crops; `CreateEffectiveForGame` applies them
  without mutating live settings, and profile JSON normalizes them.
- [x] Tests: `CropInsetsTests` (clamping, visible-size rules, legacy fallback, color) and
  `AppSettingsStoreTests` (per-edge/border roundtrip, clamp, per-game capture/apply,
  live-settings isolation).
- [x] Verification: frontend `--smoke-test` passes; full build is clean, **1,529** tests
  pass, `dotnet format --verify-no-changes` is clean, and `jb inspectcode` reports
  0 warning/error results.

### 2026-11-02 — CORE-12 dedicated fuzz harness

- [x] Added `tools/FiveDo.Fuzz` with deterministic `state`, `disc`, `cue`, `opera-fs`,
  `cd-command`, and `cpu` targets; typed parser rejections are separated from unexpected
  defects, and every defect writes a reproducible input plus seed/iteration/stack trace
  under `artifacts/fuzz`.
- [x] The CD target exercises command, poll, tick, status/data, eject, inject, and reset
  streams in both FreeDO and Opera timing modes. The Opera FS target mutates a valid
  synthetic volume and walks/reads the parsed tree. The CPU target executes random ARM60
  streams in both timing modes.
- [x] Added `FuzzHarnessTests` (bounded six-target sweep, option parsing, stable order)
  and `.github/workflows/fuzz.yml` (nightly seeded run with uploaded evidence).
- [x] Documented the harness in [`Fuzzing.md`](Fuzzing.md) and updated
  [`TestStrategy.md`](TestStrategy.md) and [`BuildAndTest.md`](BuildAndTest.md).
- [x] Evidence: a 60-second-per-target run (seed 20261102) completed **6,705,667**
  iterations across state/disc/CUE/Opera-FS/CD with **0 unexpected failures** and
  1,758,669 typed rejections; a 60-second CPU-stream run completed **71,783**
  iterations with 0 failures. The reports are `artifacts/fuzz-nightly.json` and
  `artifacts/fuzz-cpu.json`.
- [x] Verification: `dotnet build 5DO.sln` 0 warnings/0 errors; `dotnet test` **1,529**
  tests pass; `dotnet format --verify-no-changes` clean; `jb inspectcode` reports
  0 warning/error results.

### 2026-11-01 — SYS-01/AUD-05 multi-track, CD-DA, and volume

- [x] `ITrackedDiscSource` exposes the full track table and native 2048/2336/2352-byte sectors.
- [x] `CueDiscImage` parses multi-file/multi-track CUE sheets through the MAME-compatible CHDSharp parser; `ChdDiscImage` exposes CHD audio tracks and byte-swaps Red Book samples.
- [x] `CdromDevice` builds a multi-track TOC, implements READ TOC lead-out, READ SUB-Q, MODE SET/MODE SENSE, 24-bit READ_DATA counts, PLAY_AUDIO position tracking, and 2352-byte digital-audio reads.
- [x] CD-DA is mixed into the DSP output at 44.1 kHz (`FiveDoMachine.CdAudioVolume`); CD section v4 stores sector size, block flags, audio position, volume, and mute.
- [x] AUD-05 persists `CdAudioVolume`/`CdAudioMuted`; Settings > Audio adds a CD-DA slider and mute check; `run --cd-volume` and `--mute-cd` control the mix.
- [x] `disc tracks` reports CUE and CHD tracks (text and `--json`) instead of a single synthetic data track.
- [x] Tests: multi-track CUE, CHD audio, TOC/lead-out, MODE SET, raw audio reads, Sub-Q, audio position, CD-DA sample extraction, mix level/mute, audio settings, state roundtrip, and `disc tracks --json`.
- [x] Verification: **1,473 tests pass**; build **0 warnings / 0 errors**; `dotnet format --verify-no-changes` clean; `jb inspectcode --severity=WARNING` **0 results**; Release 10-disc smoke **10/10 Loadable/Rendered**.
- [ ] Remaining (SYS-01): known CD-DA title evidence, a DSP+CD-DA WAV, read-ahead, and subcode.

### 2026-11-01 — SYS-02 multi-disc playlist and live swap

- [x] `DiscPlaylist` loads M3U/M3U8 extended playlists, resolves relative paths, and tracks the current entry.
- [x] Settings persist the playlist path; File > Swap Disc and **F3** advance to the next disc without resetting the machine, or open a one-off disc.
- [x] `CdromDevice` now answers guest inject (`0x07`) with the Opera tray/disc/spin/ready bits and media-access poll.
- [x] CLI `disc playlist` lists entries (text/JSON); `run --playlist`, `--swap-at N`, and `--swap-disc` exercise state-safe swaps.
- [x] Tests cover playlist parsing/wrapping, CLI listing, tray eject/inject, and a live CLI swap.
- [ ] Remaining: guest-driven ODE swap prompts, a playlist editor, and a known multi-disc title roundtrip.

### 2026-11-01 — FND-02 settings schema versioning

- [x] `AppSettings.SettingsVersion` and `CurrentVersion = 2`; loads migrate unversioned files and stamp the current version.
- [x] Malformed settings are backed up as `settings.json.invalid-<timestamp>.bak` before defaults are used.
- [x] Renderer, profile, timing, NVRAM, OSD, clock, CD-DA, and per-game profile values are validated on load; saves stay atomic.
- [x] Per-game timing/CD-DA profiles are stored in `GameProfiles`, resolved case-insensitively, applied on demand, and roundtrip through JSON.
- [x] CLI `settings validate` reports syntax/version and `settings migrate` normalizes and rewrites the file.
- [x] Tests cover legacy migration, malformed-file backup, version roundtrip, invalid-value normalization, profile roundtrip, and CLI validation/migration.

### 2026-11-01 — UX-15 portable mode (core)

- [x] `PortablePaths` resolves the data directory from `portable.flag`, `--portable`, or `FIVEDO_DATA_DIR`.
- [x] Settings, NVRAM, states, cheats, library state, and logs all use the resolved directory.
- [x] Desktop and CLI strip `--portable` before dispatch; the CLI reports the portable settings path.
- [x] Tests cover portable, per-user, and explicit-override resolution.
- [ ] Remaining: named configuration profiles, per-instance locking, and installer integration.

### 2026-11-01 — UX-23 input display (core)

- [x] Bottom-left overlay shows frame count, presentation-skip count, and player-1 buttons.
- [x] **F2** and **View > Input Display** toggle it; `ShowInputDisplay` persists.
- [x] `CoreRunner` captures player 1 every frame and reports it in `CoreStatus`.
- [ ] Remaining: TAS-style lag detection, layout/player options, and screenshot tests.

### 2026-11-01 — ReSharper inspectcode cleanup

- [x] Ran `jb inspectcode 5DO.sln`: **0 errors, 50 warnings**.
- [x] Fixed all 50 warnings across 18 files: disposed/modified closure captures, defensive JSON-null checks, unchecked cast overflows, hidden method, unused field, redundant default arguments/qualifiers/usings, `using` resource initialization, `with` expressions, and an XML doc reference.
- [x] `jb inspectcode --severity=WARNING`: **0 results**.
- [x] `dotnet build 5DO.sln`: **0 warnings, 0 errors**.
- [x] `dotnet test 5DO.sln`: **1,350 passed, 0 failed** at that commit (now **1,473**).
- [x] `dotnet format 5DO.sln --verify-no-changes`: clean.
- [x] Commit `26aad8b` — `Fix all ReSharper inspectcode warnings`.

### 2026-11-01 — UX-13 library metadata, favorites, and playtime (core)

- [x] `LibraryState`/`LibraryStateStore` persist favorites, launch counts, last-played timestamps, and play seconds.
- [x] `LibraryPlaytimeTracker` measures only running emulation and flushes every 60 seconds.
- [x] `LibraryMetadata`/`LibraryMetadataStore` read an optional offline `library-metadata.json` overlay.
- [x] `LibraryQuery` filters/sorts; game list adds star toggles, metadata/play time, and Settings > Paths.
- [ ] Remaining: external Redump/No-Intro/IGDB/LaunchBox providers, screenshots/manuals/videos, metadata editor, >=90% rich metadata.

### 2026-11-01 — UX-19 OSD and quick menu (core)

- [x] Bounded, time-expiring OSD notifications; colour-coded toasts for save/load, slot changes, rewind, fast-forward, screenshots, reset, and settings.
- [x] **F4** or gamepad Select+Start opens a quick menu with resume/pause, save/load, rewind, fast-forward, screenshot, reset, cheats, settings, stop, and close.
- [x] Settings > Video exposes OSD enable, position, opacity, duration, and quick-menu enable.
- [ ] Remaining: full gamepad navigation of the menu (UX-08) and richer OSD types.

### 2026-11-01 — UX-12 BIOS/NVRAM manager (core)

- [x] BIOS tab scans/classifies images, imports validated files, sets BIOS1/BIOS2, pins/clears per-game BIOS MD5.
- [x] NVRAM tab lists shared/per-game images, validates, backs up, imports, resets, and deletes with confirmation.
- [x] Atomic NVRAM writes; `BiosInventory` and `NvramInventory` isolate file operations.
- [ ] Remaining: per-game NVRAM assignment from the list and drag-and-drop import.

### 2026-11-01 — SYS-15 input devices (core)

- [x] `InputDeviceSet` selects joypad, flightstick, mouse, lightgun, arcade lightgun, Orbatak trackball, or none on six PBUS ports.
- [x] `PbusChainEncoder` emits Opera's exact daisy-chain records; legacy 4DO packet kept for all-joypad.
- [x] Settings > Emulation > Input devices and CLI `--input-device 1=mouse,3=lightgun`; `--pbus-format opera`; `run --json` reports `inputDevices`.
- [x] Desktop pointer drives the first mouse/lightgun/trackball port with a lightgun crosshair.
- [ ] Remaining: per-device calibration, multiplayer pointer routing, scripted gameplay validation.

### Earlier completed milestones

- [x] UX-01 rewind, UX-02 fast-forward, UX-04 per-game cheat engine, UX-11 disc browser/disc info, UX-06 TAS movies (core).
- [x] Opera parity batch 1 (OP-01..OP-15) and system fixes OP-SYS-01/02/03/06/10.
- [x] Differential harness (FND-05/CORE-11/CORE-16 core), M13 first 594-disc sweep, M14 research plan.
- [x] Unified x64 release bundles with framework-dependent single-file apphosts (system .NET 10 runtime), opt-in NativeAOT, benchmark gates, 4K GPU probe, ANGLE/GLES parity.
- [x] Full progress log and per-pass evidence: Part I §13 and Part IV §1.

---

## 10. Verification commands

Run these before marking work done. Full command reference is in Part IV §6.

```powershell
# Build and test
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore

# Static analysis (warnings and errors only)
jb inspectcode 5DO.sln -o=inspectcode.sarif -f=Sarif --severity=WARNING --no-build

# Compatibility sweep
pwsh -NoProfile -File tools/compat-sweep.ps1 -Roots "J:\Panasonic 3DO","G:\MAME\MAME Software List CHDs\3do" -Frames 3000 -Resume

# Differential smoke and showcase
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/smoke.json
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/showcase.json

# Benchmark gate
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- --frames 120 --label ci --json --gate

# GPU probe (Windows)
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --gl-probe --gl-probe-backend wgl --gl-probe-capture

# Direct3D 11 probe (Windows)
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --d3d-probe artifacts/d3d-probe/parity.json --d3d-probe-size 320x240 --d3d-probe-frames 20 --d3d-probe-warmup 5 --d3d-probe-filter accurate --d3d-probe-capture artifacts/d3d-probe/parity.png

# Desktop shell with the optional Direct3D 11 path
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --gl-backend d3d11 --disc "game.chd"
```

---

## 11. Maintenance rules

1. **This file is the single source of truth.** Do not recreate separate plan files; add sections or update checklists here.
2. **One feature per pull request.** Do not mix accuracy, rendering, and UX work.
3. **Tests first.** Every behavior change needs a unit, golden, differential, or manual test.
4. **One reference per change.** State whether the behavior comes from Opera, 4DO/FreeDO, or hardware documentation.
5. **No regressions.** Existing golden hashes, save states, CLI JSON, and zero-allocation contracts must keep passing.
6. **Feature flags for risky work.** New timing models ship behind a profile/flag until differential evidence is green.
7. **Document intentional divergence** in `docs/PortingNotes.md` and `docs/Compatibility.md`.
8. **Update the evidence.** When an item lands, update its checkbox, the detailed section in Part II, the progress log in Part I, and the relevant evidence doc.
9. **Keep the history.** Parts I–IV preserve the detailed rationale and “do not redo” notes. Append new session logs; do not delete old ones.

---

<a id="part-i"></a>
# Part I — Detailed Implementation Plan

> This part preserves the detailed milestone plan, architecture, risks, and progress log. The authoritative current status and master checklist are in the Master Dashboard above.

**Product:** 5DO
**Target:** pure C# / .NET 10, cross-platform (Windows, Linux, macOS; x64 and arm64)
**Reference core:** `References/opera-libretro-master/libopera` (primary behavioral reference for new work), `References/4DO-master/FreeDOCore` (historical port baseline). See [`ReferenceStrategy.md`](ReferenceStrategy.md).
**Current status (2026-11-02):** **1,473 tests pass**; the **v1.0 documented-compatibility
release is published on GitHub** with the artifacts attached. Linux x64 validation passed on Linux Mint
22.3: the full test suite (**1,473/1,473**), `selftest`, `bench`, a 3,000-frame
Wolfenstein boot, non-silent audio, and the frontend `--smoke-test`; the GL probe timed
out under llvmpipe, so Linux GPU presentation is preview/best-effort. The **M13
full-collection loadability sweep** is complete: 594 discs tested (589 in
`J:\Panasonic 3DO` plus 5 in `G:\MAME\MAME Software List CHDs\3do`), with **593
`Loadable/Rendered`, 1 `Loadable/No video` (`Crash 'n Burn` Japan), and 0 `Not
Loadable`**. Legal/attribution sign-off is recorded under the FreeDO modified GNU LGPL
as free, open-source donationware. M13.5/M13.6 (in-game/visual and high-resolution
review) and M14 high-resolution CEL research are **post-1.0**. The 2026-10-31 performance
pass reduced the Road Rash 3D race to 4.20 ms/frame (p99 7.40 ms), optimized core
projects in Debug builds (~40 fps to ~240 fps CPU headroom), coalesced UI render
requests, and added **Auto** presentation frame skip as the default. Gameplay-depth work
is deferred. See §4b, [`GameCompatibilityResults.md`](GameCompatibilityResults.md), and
[`CompatibilitySweep.md`](CompatibilitySweep.md).
**Historical status (2026-10-26, fifteenth pass):** M0-M8 are functionally complete; the M9 Avalonia shell, M9R two-stage OpenGL presenter, M9P dedicated core thread/pacing, and M10 CI/publish pipeline are implemented as a working prototype. All five golden-corpus titles plus seven additional discs boot, load their own OS, and render real frames; six of the seven additional titles produce non-silent 44.1 kHz stereo DSP audio (Theme Park's menu is silent). Road Rash's 3D race renders correctly; a CD media-access pulse fixes the old ~42k/~92k READ_ID-only wait and the game now advances through `BUSTED`/race select to a new race. Wolfenstein starts FLOOR 1-1 and roundtrips an in-game save state; Alone reaches its in-game room, Begin/Resume menu, and intro; Lemmings types the Fun 4 passcode entirely with the D-pad, starts gameplay at 23,000 frames, and its 640x480 high-resolution title matches the reference; Space Ace plays its demo and starts its interactive scene (X menu, A play). **775 tests passed at that pass; 845 tests pass after the 2026-10-29 release-hardening pass.** The fifteenth pass added an explicit `--gl-backend` selector and validated the Windows ANGLE/GLES 3.0 path: 4K present p99 0.47 ms with zero renderer allocations, all filters compile, and a 320x240 Accurate capture is pixel-identical to the CPU oracle. It also added opt-in NativeAOT win-x64 publishes: a 9.97 MB CLI that passes the smoke suite, `info`/`fs list`, and a 3,000-frame golden boot, and a 28.8 MB frontend that passes the smoke and WGL/ANGLE parity probes. The fourteenth pass wired the platform swap interval (WGL/GLX/EGL) and added a `--gl-probe` offscreen 4K presentation probe: on the RTX 4070 development machine, sharp-bilinear 4K measures GPU p99 0.089 ms, present p99 0.208 ms, and zero renderer/mailbox allocations per frame after warm-up. The thirteenth pass enabled `PublishTrimmed` for the Avalonia frontend, removed the reflection-based `ViewLocator`, added a trimmed-frontend startup smoke test, and now publishes both executables trimmed for all six RIDs. The twelfth pass made settings, input profiles, save-slot metadata, and every CLI `--json` response source-generated and trim-safe, enabled `PublishTrimmed` for the CLI, added a trimmed-CLI CI smoke test, and added a same-machine benchmark baseline comparison; see [`Trimming.md`](Trimming.md) and [`Performance.md`](Performance.md). The eleventh pass added HQx 3x/4x and the benchmark budget gate. The tenth pass added the pure D-pad passcode script and CRT bloom. The ninth pass recorded per-title save-state/PNG/WAV evidence, fixed the CD wait, and added autopilot A pulses for post-race menus. The eighth pass fixed the real-content GPU palette expansion (independent R/G/B CLUT indices), added the opt-in xBR-lv2 filter, Windows XInput/Linux evdev gamepad backends with a binding UI, an in-app log viewer, save-state thumbnails, a first-run guide, and error dialogs. Remaining work: finish a full Road Rash race without wrecking, Raspberry Pi/GLES 3.1 and DPI validation, Tier B timing, frontend AOT audio/installers, the M0.8 legal/attribution review (including the bundled OpenAL Soft LGPL runtime), and release documentation. See [`Status.md`](Status.md) and [Part IV](#part-iv-session-handoff-evidence-and-historical-fixes) for the completion estimate and next-session plan.
**Audio update (2026-10-27, sixteenth pass):** exact 44.1 kHz DSP sample clock
(600 frames = 10.000 s, 441,015-441,020 samples), Quartz state v2 with v1
conversion, bundled OpenAL Soft 1.23.1 live output, audio-thread context-current
fix, audio-driven `CoreRunner` pacing with a 60 Hz fallback, buffered/instrumented
WAV capture, `audio analyze <wav> --json`, CLI `--audio openal`/`--volume`/
`--realtime`, and validated CLI NativeAOT audio. The 3,000-frame boot keys were
re-baselined; see [`TestCorpus.md`](TestCorpus.md) 1.1/1.5a2 and
[`PortingNotes.md`](PortingNotes.md) 12.4. **845 tests pass after the 2026-10-29
release-hardening pass.**

**Release-hardening update (2026-10-29, nineteenth pass):** save-state disc
identity warnings, console hotkey dispatch, ISO/BIN/CUE `disc tracks`, and the
OpenAL Soft LGPL notice/source offer in every portable archive. **845 tests pass.**

**Optional Vulkan update (2026-10-29, twentieth pass):** `FiveDo.Video.Vulkan`
adds an offscreen Vulkan 1.0 two-stage presenter with build-time SPIR-V shaders,
`--vk-probe`, and the optional `--gl-backend vulkan` desktop readback path. The
320x240 Accurate probe matches the CPU oracle (`0xD6BD89C5`, 0 different pixels);
4K sharp-bilinear measures 1.51 ms mean / 1.80 ms p99 present with 0 managed
bytes/frame on the RTX 4070. **860 tests pass.** See [`Vulkan.md`](Vulkan.md).

**Vulkan filter update (2026-10-29, twenty-first pass):** the offscreen Vulkan
backend now runs xBR-lv2 and FSR 1.0 (EASU + RCAS) as GPU passes and HQx 2x/3x/4x
through the shared CPU expansion. 4K xBR measures 1.55 ms mean / 1.92 ms p99
present, and 4K FSR 1.87 ms mean / 2.15 ms p99, both with 0 managed bytes/frame;
HQx is CPU-bound (4K HQx 2x present 7.66/10.36 ms). The required OpenGL xBR path now
samples the GPU-expanded palette texture directly instead of uploading a CPU
`FrameAssembler` copy. **861 tests pass.** See [`Vulkan.md`](Vulkan.md).

**Save-state compression update (2026-10-29, twenty-second pass):** state format
version 2 adds per-section Brotli compression. The writer compresses payloads of at
least 4 KiB when the result is smaller; the CRC stays over the uncompressed payload,
and version-1 states still load. On the local corpus a 300-frame Gex state is 647,647
bytes and a 14,900-frame state is 2,001,229 bytes instead of 24,902,592 bytes. The
section inspector reports `Compressed`/`StoredLength` and reads the table without
decompressing payloads. **876 tests pass.** See [`SaveStates.md`](SaveStates.md).

**Vulkan window and release-polish update (2026-10-29, twenty-third pass):** the
optional Vulkan backend now creates a native Win32 surface/swapchain and presents
through FIFO vsync via `--vk-window-probe`; 4K sharp-bilinear measures 1.34/1.43 ms
GPU and 5.99/6.24 ms present (including the 165 Hz wait) with 0 managed bytes/frame.
The shell now honors `PreferOpenGl` (explicit CPU mode), Settings edits the custom
aspect ratio, frontend NativeAOT audio is validated with OpenAL's null device, and
expected environmental warnings such as a missing audio device no longer file bug
reports. **882 tests pass.**

**Opera file-system update (2026-10-29, twenty-fourth pass):** `FiveDo.Storage.OperaFileSystem`
reads the volume/directory records directly through `IDiscSource`, so `fs list`/`fs
extract` support ISO, BIN, CUE+BIN, and CHD with one parser. Multi-block directory
chains, `burst`/`gap` file layouts, special entries, and malformed-record rejection are
covered by tests; the `VideoGameFileSystemParser` dependency was removed. **906 tests
pass.**

**Vulkan performance update (2026-10-30, twenty-sixth pass):** the optional Vulkan
backend now records the full-frame readback only when `CaptureToFrame` is enabled;
probes and screenshots use a one-shot copy, and the GPU timestamp covers the render
passes only. On the RTX 4070, 4K sharp-bilinear GPU time drops from 1.34 ms to
0.075 ms and present from 1.51 ms to 0.24 ms; 4K FSR drops from 1.69/1.87 ms to
0.40/0.60 ms (GPU/present). The desktop fallback writes the captured frame directly
into the locked `WriteableBitmap`, and the swapchain blit uses a fence instead of
`vkQueueWaitIdle`. **924 tests pass.**

**Vulkan embedded surface and core-runner fix (2026-10-30, twenty-seventh pass):**
`VulkanVideoSurface` hosts a click-through Win32 child HWND inside the Avalonia window
and presents through the existing FIFO swapchain, so `--gl-backend vulkan` no longer
copies frames back to the CPU or uploads a `WriteableBitmap`. The `CoreRunner`
pause/resume handshake now uses a monitor with an explicit paused state, fixing rapid
stop/start cycles where the UI could touch the machine while the core thread was still
running. **954 tests pass.**

**ARM timing option (2026-10-31, thirtieth pass):** CORE-01's option surface is
implemented. `ArmTimingMode.FreeDoCompatible` keeps the N=4 baseline;
`ArmTimingMode.Arm60Datasheet` selects N=2 from the ARM60 datasheet/SDK and Opera. The
mode is per-instance, exposed through `--arm-timing freedo|arm60` and Settings >
Emulation, reported in `run --json`, covered by both-mode cycle tests, and recorded in
an optional save-state `Options` section with a mismatch warning. The same pass exposed
`--cpu-clock 0.5x-4x|6.25-50MHz` and a Settings field, with the stock 12.5 MHz default
unchanged and the existing state ARM-clock warning retained. The DSP PRNG now accepts
`--random-seed default|random|SEED` (and the matching Settings field) while preserving
FreeDO's default seed. A Lemmings 3,000-frame smoke differential is recorded in
[`TestCorpus.md`](TestCorpus.md) 1.1a. The full-collection differential remains the
acceptance step before the accuracy mode can become a default. **972 tests pass.** See
[`PortingNotes.md`](PortingNotes.md) 12.13-12.15.

**GitHub update checks and Direct3D 11 (2026-11-02, update pass):** the CLI and
desktop shell now check the public GitHub release feed at startup, cache the answer for
six hours, and offer the download page through a CLI prompt or an Avalonia message box.
The check is opt-out (`Settings > Video`, `--no-update-check`, or
`FIVEDO_UPDATE_CHECK=0`) and sends no personal data. The optional
`FiveDo.Video.Direct3D` backend adds a FL11_0 two-stage presenter with runtime HLSL,
the DXGI flip-model child swapchain, `--gl-backend d3d11`, Settings selection,
`--d3d-probe`/`--d3d-window-probe`, parity/shader tests, and device-removal restart.
320x240 Accurate matches the CPU oracle (0 different pixels, hash `0xB74C8AC5`); 4K
sharp-bilinear measures present p99 0.23 ms and GPU p99 0.07 ms. OpenGL remains the
default. Bundle archives now include the product version (for example
`5do-1.1.0-win-x64.zip`). **1,511 tests pass.** See [`Direct3D.md`](Direct3D.md).

**CLIO timer scheduler option (2026-10-31, thirty-first pass):** CORE-02's option
surface is implemented. `ClioTimerMode.FreeDoCompatible` keeps the original all-timer
scan; `ClioTimerMode.OperaScheduler` walks one timer slot per four 25 MHz ticks, applies
the `0x220` slack gap after slot 15, and keeps an explicit cascade carry. The mode is
per-instance, exposed through `--clio-timers freedo|opera` and Settings > Emulation,
reported in `run --json`, covered by slot-order/carry/state tests, and recorded in the
save-state `Options` section with a mismatch warning. Quartz section version 3 stores
the slot/accumulator and Clio section version 2 stores the carry; older sections load
with a reset scheduler. The FreeDO default is unchanged, and a three-title 3,000-frame
smoke differential is recorded in [`TestCorpus.md`](TestCorpus.md) 1.1b. The
full-collection differential remains the acceptance step before the Opera scheduler can
become a default. **988 tests pass.** See [`PortingNotes.md`](PortingNotes.md) 12.16.

**MADAM CEL busy timing option (2026-10-31, thirty-second pass):** CORE-03's option
surface is implemented. `CelTimingMode.FreeDoCompatible` keeps the synchronous draw;
`CelTimingMode.OperaBusy` defers the list to the next CPU tick, holds the status
register busy, and expires an estimated deadline based on rendered pixels, walked
texels, and list entries (nine million pixels per second plus 64 setup cycles per
entry). The mode is per-instance, exposed through `--cel-timing freedo|opera` and
Settings > Emulation, reported in `run --json`, covered by unit and synthetic-ROM
integration tests, and recorded in the Options section (version 3) with a mismatch
warning. The Madam section is now version 2 (pending draw and busy deadline); older
sections load with the pending flag derived from the FSM. The FreeDO default is
unchanged, and a three-title 3,000-frame smoke differential is recorded in
[`TestCorpus.md`](TestCorpus.md) 1.1c. The full-collection differential remains the
acceptance step before the Opera busy model can become a default. **1003 tests pass.**
See [`PortingNotes.md`](PortingNotes.md) 12.17.

**CD-ROM cycle-based data timing (2026-10-31, thirty-third pass):** CORE-04's first
increment is implemented. `CdromTimingMode.FreeDoCompatible` keeps the immediate
FreeDO transfer; `CdromTimingMode.OperaCycleBased` schedules each 2048-byte data sector
at the stock 2x drive speed (81,246 ARM cycles at 12.5 MHz after Opera's overhead
compensation), clears the data/status poll bits while the drive is busy, publishes the
read-data status after the last requested sector is drained, and raises the XBUS CD FIQ
when delayed data or status becomes ready. The device clock is advanced by
`FiveDoMachine.InternalFrame`, exposed through `--cd-timing freedo|opera` and Settings >
Emulation, reported in `run --json`, and recorded in the Options section (version 4)
plus the CD-ROM section (version 2). The default is unchanged, and a three-title
12,000/24,000-frame smoke differential is recorded in
[`TestCorpus.md`](TestCorpus.md) 1.1d. Command latency, disc-derived TOC, read-ahead,
subcode, DIPIR tagging, CD-DA, ODE, and CD speed control remain; the full-collection
differential is the acceptance step before the cycle model can become a default.
**1014 tests pass.** See [`PortingNotes.md`](PortingNotes.md) 12.18.

**CD speed and timing profiles (2026-10-31, thirty-fourth pass):** SYS-09 is
implemented. `CdSpeed` (`Unbounded`, `X1`-`X32`) selects the effective speed for the
cycle-based CD model, using Opera's per-speed overhead compensation; `unbounded`
publishes sectors immediately. The default `X2` keeps the existing CORE-04 timing and the
FreeDO path is unaffected. It is exposed through `--cd-speed` and Settings > Emulation,
reported in `run --json`, and recorded in the Options section (version 5) with a
mismatch warning. CORE-08 adds `EmulationProfile` (`Compatible`, `Accuracy`,
`Performance`, `Custom`) with CLI/Settings selection and a derived `run --json` profile;
the accuracy profile bundles the ARM60, Opera timer, CEL busy, and CD cycle options.
The three-title smoke differential is in [`TestCorpus.md`](TestCorpus.md) 1.1e.
Remaining: per-profile benchmark deltas, a full-collection accuracy run, and CD-DA speed
behavior (SYS-01). **1039 tests pass.** See [`PortingNotes.md`](PortingNotes.md) 12.19.

**Per-game NVRAM and versions (2026-10-31, thirty-sixth pass):** SYS-14 is implemented.
`NvramStorageMode` (`Global`, `PerGame`), `NvramVersion` (0-9), and `NvramDirectory`
select the historical shared `nvram.ram`, versioned `nvram.<n>.ram`, or
`nvram/per-game/<gameId>.<n>.ram`. `NvramLayout` resolves paths and migration fallbacks;
`NvramStore` reads per-game version 0, the shared version, and the legacy image in that
order, writes atomically, and backs up a corrupt primary instead of silently loading an
older image. Settings > Emulation exposes the storage mode and version and restarts the
machine safely when either changes; File > Export/Import/Reset NVRAM manages the current
image. The CLI adds `--nvram-mode`, `--nvram-version`, and
`--nvram-dir` to `run`/`trace`, reports `nvramPath`/`nvramMode`/`nvramVersion` in
`run --json`, and adds `nvram path|list|export|import|reset`. Unit, store, settings,
JSON-contract, and CLI tests cover isolation, migration, version separation, corrupt
images, and import/export/reset. **1091 tests pass.** See
[`Configuration.md`](Configuration.md) 5 and [`CliReference.md`](CliReference.md) 8.1.

**BIOS manager and auto-selection (2026-10-31, thirty-fifth pass):** SYS-04 is
implemented. `BiosCatalog` recognizes the common 3DO BIOS hashes and their region,
video-standard, Kanji, Anvil, and patch metadata. `BiosLibrary.Scan` validates a folder,
computes MD5s, classifies primary and secondary images, and reports bad files;
`SelectForGame` combines game-database regions, known title requirements (Slayer
Europe/USA, Powers Kingdom Europe, Arcade CD-ROM), and per-game MD5 overrides. The CLI
adds `--bios2`, `--bios-auto`, and `bios list|scan|select`; `run --json` reports the
selected BIOS path/hash/name/region/reason/source. Settings > Paths adds the BIOS folder,
primary/secondary paths, auto-select, and folder validation; Settings > Emulation adds a
per-game BIOS override; File adds assign/clear actions. The configured BIOS remains the
fallback when no BIOS folder is set, so existing settings do not change. The five-title
3,000-frame smoke run and four known-requirement 12,000-frame runs are in
[`TestCorpus.md`](TestCorpus.md) 1.1f. **1065 tests pass.** See
[`PortingNotes.md`](PortingNotes.md) 12.20.

**Compatibility-sweep and high-resolution research plan (2026-10-30, twenty-eighth
pass; superseded 2026-11-02):** the plan originally deferred v1.0 until M13/M14. The
**M13 full-collection loadability sweep** is now complete: 594 discs tested
(`J:\Panasonic 3DO`, 589 CHDs, and `G:\MAME\MAME Software List CHDs\3do`, 5 CHDs),
with 593 rendering, 1 known boot failure, and 0 not loadable. The sweep uses
[`GameCompatibilityList.md`](GameCompatibilityList.md) (241 4DO reference titles),
[`CompatibilitySweep.md`](CompatibilitySweep.md), and `tools/compat-sweep.ps1`; the
high-resolution work is scoped in
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md). Gameplay-depth work
(full races, inventory interaction, long playthroughs) is explicitly not part of this
phase. The project is **donationware**: non-commercial, source-available, and funded by
voluntary donations rather than sales. The local Linux validation target is the
Hyper-V VM `LinuxMint` (`D:\Hyper-V\VMs\LinuxMint`). **954 tests passed at that pass;
the docs/tools added no runtime tests.**

**Release-scope update (2026-11-02):** v1.0 is no longer deferred. The release is a
documented-compatibility release: 593/594 discs render, the 12-title matrix and known
issues are the supported set, and full compatibility/M14 are post-1.0. Legal/attribution
sign-off is recorded under the FreeDO modified GNU LGPL as free, open-source
donationware. Linux x64 full suite passed; v1.0.0 is published on GitHub with the artifacts attached.

**OSD and quick-menu pass (2026-11-01, thirty-seventh pass):** UX-19 is implemented.
`FiveDo.Host.Osd` adds `OsdOptions`, `OsdPosition`, `OsdSeverity`, `OsdMessage`, and
the bounded, time-expiring `OsdNotifications` list. `AppSettings` persists OSD
enable/position/opacity/duration and the quick-menu toggle. The game view shows
colour-coded notifications for save/load, slot changes, rewind, fast-forward,
screenshots, reset, and settings. `F4` or the gamepad Select+Start chord opens a quick
menu that pauses the game and offers resume/pause, save/load, rewind, fast-forward,
screenshot, reset, cheats, settings, stop, and close; Up/Down/Home/End and Enter
navigate, and closing resumes only when the game was running. Settings > Video edits
the OSD options. No emulation-core behavior or save-state layout changed. **1336 tests
pass.** See Part III §13.26.

**Library metadata/favorites/playtime pass (2026-11-01, thirty-eighth pass):** UX-13
is implemented as a partial increment. `LibraryState`/`LibraryStateStore` persist
favorites, launch counts, last-played timestamps, and play time in
`library-state.json`; `LibraryPlaytimeTracker` measures only running emulation and
flushes every 60 seconds. `LibraryMetadata`/`LibraryMetadataStore` read an optional
offline `library-metadata.json` overlay keyed by game ID or path; the scanner now
carries the 4DO database's year/publisher/region. `LibraryQuery` implements
All/Favorites/Played/Unplayed filters and Title/Recently played/Most played/Release
year/Publisher sorts. The game list adds star toggles, metadata/play-time text, and
filter/sort selectors; Settings > Paths chooses the metadata file. No emulation-core
behavior, timing options, save-state layout, or CLI contracts changed. **1350 tests
pass.** See Part III §13.27.

**Multi-track and CD-DA core pass (2026-11-01, thirty-ninth pass):** SYS-01 and
AUD-05 move to partial. `ITrackedDiscSource` exposes the complete track table and native
2048/2336/2352-byte sectors; `CueDiscImage` uses the MAME-compatible CHDSharp CUE
parser for multi-file/multi-track `INDEX`/`PREGAP`/`POSTGAP` layouts; `ChdDiscImage`
exposes CHD audio tracks and byte-swaps Red Book samples. `CdromDevice` now builds the
TOC from the track table and implements READ TOC (track and `0xAA` lead-out), READ
SUB-Q, MODE SET/MODE SENSE block pages, 24-bit READ_DATA block counts, PLAY_AUDIO
position tracking, 2352-byte digital-audio reads, and a 44.1 kHz CD-DA sample path.
`FiveDoMachine.CdAudioVolume` mixes CD-DA into DSP output; CD section v3 records the
mode and playback position. `disc tracks` reports every CUE/CHD track. FND-02 adds
settings schema versioning, legacy normalization, malformed-file backup, and load-time
validation. **1361 tests passed** at that pass.

**Disc audio and multi-disc pass (2026-11-01, fortieth pass):** AUD-05 is done.
`CdAudioVolume`/`CdAudioMuted` persist in settings, appear in Settings > Audio, are
available as `--cd-volume`/`--mute-cd`, and live in CD state v4. Settings schema v2 adds
per-game timing/CD-DA profiles and the `settings validate|migrate` commands, completing
FND-02. SYS-02 moves to partial with an M3U/M3U8 `DiscPlaylist`, persisted playlist
path, File > Swap Disc plus **F3**, one-off disc replacement without reset, guest
inject status, `disc playlist`, and `run --playlist/--swap-at/--swap-disc`. **1373
tests passed** at that pass; build, format, and inspectcode were clean.

**Portable-mode pass (2026-11-01, forty-first pass):** UX-15 moves to partial.
`PortablePaths` resolves settings, NVRAM, states, cheats, library state, and logs from
`portable.flag`, `--portable`, or `FIVEDO_DATA_DIR`; the CLI and desktop strip the flag
before dispatch. A portable USB-stick run no longer touches `%LOCALAPPDATA%`. Named
configuration profiles and a per-instance write lock remain. **1376 tests pass.**

**Input-display pass (2026-11-01, forty-second pass):** UX-23 moves to partial.
`CoreRunner` captures the player-1 PBUS state every frame, and `CoreStatus` carries it
plus the presentation-skip count. The game view adds a bottom-left frame/skip and
player-1 button overlay, toggled by **F2** or **View > Input Display** and persisted as
`ShowInputDisplay`. TAS-style lag detection and layout options remain. **1376 tests
pass.**

**ODE and playlist pass (2026-11-01, forty-third pass):** SYS-02 gains
`GuestEjectCount`, `TrayOpen`, `DiscPresent`, and a core-thread `GuestDiscEjected`
callback. `AutoSwapOnEject` advances to the next playlist disc on a guest eject, giving
ODE-style behavior; without a playlist the frontend prompts for **F3**. The playlist
editor adds, removes, reorders, imports, and saves extended M3U files, and drag-and-drop
import accepts disc images or playlists. `SaveStateTests` covers swap plus state
roundtrip, and a two-disc `D (USA, Europe)` headless run swapped at frame 300 and
continued to frame 600 with a new disc hash. **1381 tests pass.**

**PAL/NTSC pass (2026-11-01, forty-fourth pass):** SYS-03 moves to partial.
`VideoRegion` and `VideoRegionProfile` add PAL1 (320x288) and PAL2 (384x288) at 50 Hz,
with a 312-line field, 624-line VDL cycle, 15,600 VDL events/second, 250,000-cycle
fields, and a 576-line frame store. `Vdlp` maps PAL visible lines from scanline 21/22,
`FiveDoMachine` resizes the source and frame clock, and `CoreRunner` paces from the
50 Hz audio cadence. Settings and `--region` expose the choice; the CLI auto-selects
PAL1 when the BIOS is a PAL image. The desktop `AutoRegion` option switches a running
machine between frames when the resolved BIOS changes standard. Battle Chess Europe
renders correctly, a 300-frame capture is 6.000 s of audio, and NTSC tests remain green.
**1392 tests pass.**

**Isolation and CD-evidence pass (2026-11-01, forty-fifth pass):** UX-15 gains
`InstanceLock`, so a second desktop launch against the same data directory reports the
owning process and exits instead of racing settings/NVRAM writes. SYS-03 gains a
per-game region override and state-driven region switching; the options section now
records the live region. CORE-04 gains illegal-request responses for unsupported/unknown
commands, the guest RESET command, `CdromCommandTranscript` plus `--cd-transcript`, and
native-sector read-ahead in the prefetch cache. `Battle Chess (Europe)` produced a
393-entry boot transcript with matching frame hash. **1415 tests pass.**

**Audio-latency pass (2026-11-01, forty-sixth pass):** AUD-01 moves to partial. OpenAL
devices are enumerated and selectable, the buffer target is 20-500 ms, and queue,
prefill, and ring sizes derive from that target. Device/buffer changes recreate only
the host output. The local host enumerated two devices, and a 60-frame PAL run opened
the selected device at a 20 ms target. **1425 tests pass.**

**Extended-boot compatibility pass (2026-11-01):** CORE-13 moves to partial. The 18
discs that were black or not loadable in the 3,000-frame sweep were rerun with
`tools/compat-sweep.ps1 -Files ... -Frames 20000`; all five former not-loadable rows and
12 of the 13 black/blank rows render. Powers Kingdom (Europe) needs the European Anvil
BIOS and 65,000 frames; WorldCup Super Stadium needs the FZ-10J BIOS plus its Kanji font
ROM (BIOS2). The BIOS library now classifies 1 MB Kanji font images as secondary ROMs.
The final loadability table is 594 discs, 593 rendered, 1 known failure
(`Crash 'n Burn Japan`), and 0 not loadable. `-RetryFrames`
keeps this retry reproducible for future sweeps. **1473 tests pass.**

**Overscan pass (2026-11-01):** VID-06 moves to partial. `AppSettings.OverscanPixels`
crops 0-32 pixels from every edge before presentation and native screenshots,
`VdlFrame.CopyCroppedFrom` preserves the palette/background/output-control metadata,
and Settings > Video exposes None/4/8/12/16/24 px. The crop is presentation-only:
frame hashes, input, audio, and save states are unchanged, while CLI evidence keeps the
full frame. Per-game presets and void-area patterns remain. **1473 tests pass.**

**Fix-parity pass (2026-11-01):** CORE-09 is done. [`docs/FixParity.md`](FixParity.md)
inventories every 4DO `FIX_BIT_*` and maps it to `FixMode`. DinoPark's scale-Y fix is
visibly active; Microcosm, Super Street Fighter II Turbo, Alone in the Dark, and Samurai
Shodown retain correct output at 3,000 frames, and forcing the retired timing-6/
graphics-step-Y bits produces byte-identical frames. `GameCompatibilityTests` locks the
mapping. **1473 tests pass.**

**DAT health pass (2026-11-01):** SYS-20 adds a Redump/No-Intro XML parser and
native-track matching. `DiscHealth` hashes each track with CRC-32, MD5, and SHA-1 and
requires size plus every supplied DAT hash. `disc verify --dat` prints the matched
game/ROM and exits 1 on a mismatch; a synthetic ISO matched its DAT and a one-byte
change failed. The library badge and 4DO/Opera comparison remain. **1473 tests pass.**

**Profile-coverage pass (2026-11-01):** SYS-18/UX-20 gain BIOS, fix, filter,
high-resolution, input-device, and NVRAM fields. `CreateEffectiveForGame` now
deep-copies the override maps before applying a profile, so live settings are not
mutated. The desktop uses the effective settings for BIOS selection, fix mode, input
devices, the renderer filter, high-resolution, and the NVRAM store; the CLI applies
profile fix/high-resolution. Cheat fields, inherit/reset, and live application remain.
**1473 tests pass.**

**Last updated:** 2026-11-02

---

## 0. How to use this document

This is the execution plan, not a wish list. It is organized as **milestones** (M0-M14), each with numbered tasks, deliverables, tests, and exit criteria.

Rules:

1. **One active milestone.** Do not start M4 while M2 is failing its exit criteria.
2. **Acceptance test first.** Write the test or define the exact differential before implementing a task.
3. **No silent deviations.** If the port must differ from the reference (Opera for new work, FreeDOCore for the original baseline), document it in `docs/PortingNotes.md` and add a test.
4. **A milestone is done only when its exit criteria pass.** "Code compiles" is not done.
5. **Keep the core deterministic.** No wall clock, no random seeds from the environment, no unordered iteration in emulated state.
6. **Update the plan.** When a task is completed, check it off and record the commit/PR. When a task is split, add the new tasks here.
7. **Do not commit copyrighted content.** No BIOS ROMs, game images, or extracted assets.

Status legend:

- `[ ]` not started
- `[~]` in progress
- `[x]` done
- `[!]` blocked
- `[-]` deliberately deferred

---

## 1. Product definition

### 1.1 What we are building

A 3DO Interactive Multiplayer emulator with:

- A **managed C# emulation core** implementing the ARM60 CPU, memory/bus, MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD-ROM, and timing.
- A **required OpenGL renderer** (`FiveDo.Video.OpenGL` + Silk.NET.OpenGL) that performs GPU palette expansion, high-quality scaling/filters (nearest, sharp-bilinear, CRT, xBR-family), integer scaling, vsync presentation, and screenshots. This is the project's primary differentiator. An optional Windows **Direct3D 11** backend (`FiveDo.Video.Direct3D`) mirrors the same two-stage pipeline; OpenGL remains the default on every platform.
- **First-class disc support** for ISO, BIN, CUE+BIN, and **CHD via the pure C# CHDSharp library**, including parent/child CHDs and read-ahead decompression.
- A **headless CLI** for running discs, dumping frames/states, testing, and benchmarking.
- An **Avalonia desktop frontend** for Windows, Linux, and macOS with a game library, settings, controller mapping, save states, screenshots, and fullscreen.
- A **unit/integration test suite** that can run without a copyrighted BIOS by using synthetic ROMs and fixtures.
- **Deterministic save states** and optional input recording/replay.

### 1.2 What "pure C#" means here

- No C++/CLI, no native `FreeDOCore.dll`, no P/Invoke into an emulator core.
- The emulation core, host, CLI, and frontend logic are C#.
- Host-only services may use platform/native libraries behind interfaces:
  - **OpenGL 3.3 via Silk.NET.OpenGL and OpenGL ES 3.0/3.1 via Silk.NET.OpenGLES** for the required desktop renderer (the OS/driver supplies the GL/GLES implementation).
  - **Direct3D 11 via Silk.NET.Direct3D11/DXGI/Direct3D.Compilers** for the optional Windows presenter (runtime HLSL compilation; no native shader toolchain).
  - Avalonia rendering (its own native backend).
  - Audio output (OpenAL Soft, SDL, or platform APIs).
  - Gamepad input (SDL/GameController or platform HID).
- The core, CLI headless mode, and tests must run with null audio/input backends and without requiring a GL context. The CPU frame assembler remains the fallback/accuracy oracle.

**Release definition (adopted 2026-10-26; confirmed 2026-11-02).** v1.0 ships a
**documented compatibility set**, not full compatibility. M13 loadability evidence and
the 12-title matrix are sufficient; M13.5/M13.6 visual/high-resolution review and M14
research are post-1.0. Supported platforms and titles are validated and listed in
`Compatibility.md`/`KnownIssues.md`; unvalidated targets and broader game support are
post-1.0. The release gate is in
[`ReleaseChecklist.md`](ReleaseChecklist.md).

### 1.3 Success criteria

**v0.1 - Boot milestone**

- [x] Boots a real 3DO BIOS to its menu. (`panafz1.bin` reaches the Portfolio shell)
- [x] Renders the BIOS UI at 320x240, 60 Hz.
- [~] OpenGL renderer displays the BIOS frame with pixel-accurate palette expansion. (Windows launch smoke test and CPU/GPU parity tests pass; a recorded real-BIOS frame through the desktop GL path and GPU CI coverage remain)
- [x] Responds to keyboard input. (desktop frontend and scripted CLI input)
- [~] Does not crash for 10 minutes of menu navigation. (long corpus runs exceed 60,000 frames; a dedicated 10-minute menu soak test remains)
- [x] Synthetic-ROM integration tests pass in CI.

**v0.5 - Playable milestone**

- [x] Boots at least 10 real discs to a rendered title/menu or gameplay state (user-provided test set), including at least one CHD image. Twelve local discs boot and render; five have scripted gameplay evidence. Full playthroughs are not a release gate.
- [x] Audio works for DSP-driven music/SFX. (non-silent 44.1 kHz stereo output on all five titles; mix/pitch comparison remains post-1.0)
- [x] NVRAM persists between sessions.
- [x] Save states roundtrip deterministically.
- [x] ISO, BIN, CUE+BIN, and CHD images produce identical sector streams for the same disc.
- [x] OpenGL renderer supports accurate, sharp-bilinear, and CRT modes; integer scaling; fullscreen; screenshots. (accurate/nearest, sharp-bilinear, CRT scanlines/bloom, integer scaling, fullscreen, and GL/CPU screenshots are implemented)
- [x] 60 fps on a mid-range x64 desktop at native resolution. Core frame time is 1.25-1.6 ms and Tier A 4K presentation is measured; Raspberry Pi 4/5 validation is post-1.0.
- [x] Windows x64 build and package are smoke-tested. CI also produces Linux/macOS/arm64 publishes; validating those targets is post-1.0 unless they are claimed for a release.

**v1.0 - Release milestone**

- [x] Compatibility matrix with documented results.
- [x] Full Avalonia frontend (library, settings, input mapping, save states, screenshots, logs, about). Feature-complete; optional ViewModel tests and polish are post-1.0.
- [x] OpenGL renderer is the default with CPU fallback and context-loss recovery. Windows WGL/ANGLE is validated; Linux x64 full suite passed (GL presentation is preview/best-effort), and macOS is post-1.0.
- [x] 4K (3840x2160) 60 fps output on Tier A x64 hardware, with no CPU-side per-pixel scaling. Tier A is measured; 1080p60 on Raspberry Pi 4/5 is post-1.0. 4K *internal* CEL rendering is not a v1 goal. See [`Performance.md`](Performance.md).
- [x] CLI with `run`, `info`, `bench`, `state`, and `selftest` commands.
- [x] Reproducible self-contained packages for the supported RIDs. Six-RID CI outputs and portable archives exist; [`ReleaseChecklist.md`](ReleaseChecklist.md) gates which RIDs are claimed and smoke-tested.
- [x] License/attribution review complete. FreeDO modified GNU LGPL, donationware; project-owner sign-off recorded 2026-11-02.
- [x] No known crash bugs in the supported test set. Twelve-title 36,000-frame soaks and save/load roundtrips pass; the Crash 'n Burn (Japan) boot failure is documented as a known issue.

### 1.4 Non-goals (v1)

- Native (executable-memory) CPU backends. An optional **managed IL JIT** for the guest ARM core is allowed and planned behind `ICpuBackend`.
- GPU-accelerated CEL compositing in v1. The required OpenGL renderer accelerates presentation, palette expansion, scaling, and filters; software CEL rendering remains the accuracy path. A GPU CEL path is an optional post-v1 experiment.
- Netplay.
- Rewind.
- CD-DA / Red Book audio.
- Multi-disc swapping.
- PAL video.
- High-resolution CEL rendering as a default mode. It is available as opt-in `--high-res` (M11.2) and is not part of the 4K output target. **4K internal CEL rendering is not a v1 goal**; v1's 4K target is output upscaling only.
- 3DO mouse, light gun, modem, or network peripherals.
- Bit-exact 4DO save-state compatibility.

---

## 2. Architecture summary

The full architecture is in [`Architecture.md`](Architecture.md). The short version:

```text
Frontends (Avalonia, CLI)
        │
        ├── FiveDo.Video.OpenGL  (required desktop renderer)
        │     palette shader, scaling, CRT/xBR, vsync
        │
        ├── FiveDo.Video.Direct3D (optional Windows renderer)
        │     HLSL palette, scaling, CRT/xBR/FSR, DXGI swapchain
        │
        ▼
FiveDo.Host (session, BIOS/disc/NVRAM, audio, input, save states, pacing)
        │
        ▼
FiveDo.Machine (composition, frame loop, Quartz, state sections)
        │
        ├── FiveDo.Cpu       ARM60 interpreter (+ optional IL JIT backend)
        ├── FiveDo.Memory    RAM/ROM/NVRAM, bus decode
        ├── FiveDo.Madam     CEL engine, pixel pipeline, matrix engine
        ├── FiveDo.Clio      timers, FIFOs, DMA, interrupts, XBUS/DSP windows
        ├── FiveDo.Dsp       audio DSP
        ├── FiveDo.Sport     VRAM fill/copy
        ├── FiveDo.Video     VDLP, frame assembly, CPU oracle/fallback
        ├── FiveDo.Xbus      expansion bus
        ├── FiveDo.Cdrom     CD-ROM device
        ├── FiveDo.Storage   ISO/BIN/CUE, BIOS, NVRAM, game DB
        ├── FiveDo.Audio     ring buffer, sample conversion
        ├── FiveDo.Input     PBUS encoding, profiles
        └── FiveDo.Diagnostics logging, tracing, dumpers
        │
        ▼
FiveDo.Core (contracts, types, constants, state interfaces)
```

Key decisions:

- **`FiveDo.Core` has no project references.** Chips depend only on Core. `FiveDo.Machine` wires concrete chips through small interfaces.
- **The core is single-threaded and deterministic.** The host owns pacing, audio, input, and UI/render threads.
- **Memory preserves FreeDOCore's word-swapped representation** for v1. See `docs/PortingNotes.md` §3.
- **Save states use a new explicit format**, not the native `memcpy` struct layout.
- **CEL rendering is software**, matching FreeDO. **Presentation is OpenGL/GLES**: native-resolution palette expansion, output-resolution scaling/filters, and vsync. The renderer is required for the desktop frontend and is the project's main differentiator. An optional **Direct3D 11** presenter mirrors the same pipeline on Windows; OpenGL/GLES remains the default and the fallback.
- **Performance is a continuous gate.** The 4K60 target is output upscaling, not 4K internal CEL rendering. Core work stays single-threaded and deterministic; the host owns a dedicated core thread, frame pacer, and render/audio threads. See [`Performance.md`](Performance.md).
- **The guest ARM JIT is optional** and lives behind `ICpuBackend`; a tuned interpreter is the default and the accuracy oracle.

---

## 3. Reference-to-project map

This table records the original FreeDOCore-to-C# mapping. For new compatibility work,
use the Opera mapping in [`ReferenceStrategy.md`](ReferenceStrategy.md); the C#
project/type layout is unchanged.

| FreeDOCore file | C# project | Main C# types | Milestone |
|---|---|---|---|
| `_3do_sys.cpp` | `FiveDo.Machine` | `FiveDoMachine`, `FrameLoop`, `MachineOptions` | M3 |
| `arm.cpp`, `arm.h` | `FiveDo.Cpu` | `Arm60`, `Arm60State`, `ArmDecoder`, `ArmAlu`, `ArmBarrelShifter`, `ArmDisassembler` | M2 |
| `Clio.cpp`, `Clio.h` | `FiveDo.Clio` | `Clio`, `ClioRegisters`, `ClioTimers`, `ClioFifo`, `ClioDma`, `ClioInterrupts` | M3 |
| `Madam.cpp`, `Madam.h` | `FiveDo.Madam` | `Madam`, `MadamRegisters`, `CelEngine`, `CelBitReader`, `PixelDecoder`, `PixelProcessor`, `Projector`, `CelRasterizer`, `MatrixEngine` | M4 |
| `vdlp.cpp`, `vdlp.h` | `FiveDo.Video` | `Vdlp`, `VdlParser`, `VdlLine`, `VdlFrame`, `FrameAssembler` | M3/M4 |
| `frame.cpp`, `frame.h` | `FiveDo.Video` + `FiveDo.Video.OpenGL` + `FiveDo.Video.Direct3D` | `FrameAssembler` (CPU oracle/fallback), `OpenGlRenderer`/`Direct3D11Renderer` palette shaders (required/optional desktop paths) | M3/M4 + M9R |
| `DSP.cpp`, `DSP.h` | `FiveDo.Dsp` | `Dsp`, `DspCore`, `DspInstructionDecoder`, `DspAlu`, `DspRegisters`, `DspMemory` | M5 |
| `SPORT.cpp`, `SPORT.h` | `FiveDo.Sport` | `Sport` | M3 |
| `XBUS.cpp`, `XBUS.h`, `IsoXBUS.h` | `FiveDo.Xbus` | `Xbus`, `XbusDevice`, `XbusFifo` | M6 |
| `Iso.cpp` | `FiveDo.Cdrom` | `CdromDevice`, `CdromCommand`, `Toc`, `Msf` | M6 |
| `quarz.cpp`, `quarz.h` | `FiveDo.Machine` | `Quartz`, `ClockDomain` | M3 |
| `DiagPort.cpp`, `DiagPort.h` | `FiveDo.Clio` | `DiagPort` | M3 |
| `bitop.cpp`, `bitop.h` | `FiveDo.Madam` | `CelBitReader` | M4 |
| `Filters/hqx*` | `FiveDo.Video.OpenGL` | GLSL xBR/HQx-style filters (GPU, required filter family) | M9R/M11 |
| `types.h`, `freedoconfig.h`, `freedocore.h` | `FiveDo.Core` | constants, `FixMode`, `EmulatorTuning`, `VdlFrame`, `FrameBuffer` | M1 |
| `Worker.cpp` | - | not ported | - |

4DO host references:

| 4DO C# file | C# project | Main types |
|---|---|---|
| `Emulation/GameConsole.cs` | `FiveDo.Host` | `EmulatorSession`, `FramePacer`, `AudioPipeline` |
| `Emulation/GameSource/*` | `FiveDo.Storage`, `FiveDo.Host` | `DiscImageFactory`, `IsoImage`, `BinImage`, `ChdDiscImage`, `GameLibraryService`, `GameDatabase` |
| `Emulation/States/SaveStateHelper.cs` | `FiveDo.Machine`, `FiveDo.Host` | state sections, `SaveStateService` |
| `Emulation/Plugins/Input/JohnnyInput/*` | `FiveDo.Input` | `PbusEncoder`, `ControllerState`, `InputProfile` |
| `Emulation/GameSource/GameRegistrar.cs` | `FiveDo.Storage` | `GameDatabase`, `GameId` |
| `Emulation/EmulationHelper.cs` | `FiveDo.Storage` | `NvramImage`, `VolumeHeader` |
| `UI/SaveHelper.cs` | `FiveDo.Host` | `SavePathService` |

---

## 4. Milestones at a glance

| ID | Milestone | Goal | Exit criteria | Estimate |
|---|---|---|---|---:|
| M0 | Bootstrap | Repo, build, CI, docs, project skeleton | `dotnet build`/`test` green; solution matches architecture | 3 dd |
| M1 | Core + memory + scheduler | Contracts, RAM/ROM/NVRAM, bus, Quartz, state primitives | Memory/bus/quartz unit tests green | 5 dd |
| M2 | ARM60 CPU | Full FreeDO ARM interpreter | CPU instruction tests green; synthetic program runs | 12 dd |
| M3 | Boot skeleton | BIOS load, CLIO basics, VDLP, SPORT, frame loop | Real BIOS executes 300 frames; synthetic frame output | 10 dd |
| M4 | MADAM graphics | CEL engine, pixel pipeline, matrix engine | BIOS UI renders; CEL golden tests green | 20 dd |
| M5 | DSP + audio | DSP core and sample pipeline | DSP tests green; BIOS audio/test tone works | 12 dd |
| M6 | XBUS + CD-ROM + CHD | Disc images, CHD, and CD command state machine | Real disc/CHD boots to title screen | 12 dd |
| M7 | Input + NVRAM + fixes | Playable controls, persistence, game DB | Playable test set; NVRAM persists | 6 dd |
| M8 | Save states + diagnostics | Deterministic states, CLI tools, tracing | State roundtrip and CLI tests green | 10 dd |
| M9 | Avalonia frontend | Full desktop UI | Runs on win/linux/mac x64; 60 fps; fullscreen | 16 dd |
| M9R | **OpenGL/GLES renderer** | Required GPU presentation, palette shader, scaling/filters, vsync | Accurate mode matches CPU; 4K output; 60 fps | 10 dd |
| M9P | **Performance and pacing** | Dedicated core thread, two-stage 4K presenter, persistent upload, backend matrix, benchmark gates | 4K60 Tier A; 1080p60 Pi4/5; p99 budget; zero allocations | 10 dd |
| M10 | Packaging + performance validation | Six RIDs, CI artifacts, final performance report | All packages start; targets met | 6 dd |
| M11 | Compatibility + release | Real-game hardening, high-res, release | v1 checklist complete | 25+ dd |
| M12 | Optional guest ARM JIT | Managed IL JIT behind `ICpuBackend` | Bit-identical state; benchmark win | 10-20 dd |
| **M13** | **Full-collection compatibility sweep** | Loadability + in-game/visual status for every local disc; 4DO comparison; Linux VM validation | Every image classified; failures triaged; docs updated | 10-15 dd |
| **M14** | **High-resolution CEL research** | Determine whether true internal 640x480 / 2x supersampling / GPU CEL is feasible | Prototype evidence and a recorded decision | 10-20 dd research |

**Total:** ~157 developer-days of focused work plus 30% contingency (~204 dd), excluding the optional M12 JIT and the new M13/M14 work. This is the **full-compatibility plan**. The adopted v1 release scope (2026-10-26) ships a documented compatibility set and defers the remaining M11 hardening, Tier B, installers, and the optional JIT to post-1.0. **As of 2026-11-02, M13 loadability is complete (593/594 render); M14 is post-1.0 research. Linux x64 full suite passed; v1.0.0 is published on GitHub with the artifacts attached.** See [`ReleaseChecklist.md`](ReleaseChecklist.md), [`CompatibilitySweep.md`](CompatibilitySweep.md), and [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

### 4a. Current position vs. the plan (2026-10-26)

Snapshot: 954 tests pass; CPU-side frame cost is ~1.25-1.6 ms with p99 1.6-4.6 ms
and zero synthetic steady-state allocations; all five local corpus titles boot and
render, and scripted gameplay is validated in Wolfenstein, Lemmings, Road Rash, and
Alone. The opt-in 640x480 high-resolution mode is implemented and matches the
Lemmings reference title. Real-game status is in
[`Compatibility.md`](Compatibility.md). The fifteenth pass added `--gl-backend`
selection and validated Windows ANGLE/GLES 3.0, including pixel-identical Accurate
output against the CPU oracle. It also added an opt-in NativeAOT win-x64 CLI publish
(9.97 MB) that passes the smoke suite and the 3,000-frame Lemmings golden boot.
The fourteenth pass wired the platform
swap interval and added a true-4K offscreen GPU probe (RTX 4070: sharp-bilinear GPU
p99 0.089 ms, present p99 0.208 ms, 0 renderer allocations/frame). The twelfth pass
made the CLI trim-safe and enabled trimmed single-file publishes. The eleventh pass
added HQx 3x/4x and the benchmark budget gate. The tenth pass added the pure D-pad
passcode script, CRT bloom, and HQx 2x. The ninth pass recorded save-state/PNG/WAV
evidence and fixed the Road Rash ~42k/~92k READ_ID-only wait with a CD media-access
pulse; the game now reaches `BUSTED`, race select, and a new race. The eighth pass
fixed real-content GPU palette expansion, added xBR, gamepad backends, a log viewer,
save-state thumbnails, and first-run/error UI. Counting the prototype work as
partial credit, the table below is the **original full-compatibility baseline**; the
adopted release-scope estimate follows it.

| Milestone | State | Done (dd) | Remaining (dd) |
|---|---|---:|---:|
| M0-M8 | Functionally complete; M0.8 legal review is tracked in M11.6 and minor M8 follow-ups are non-blocking | 90 | 0 |
| M9 | Partial: shell/library/video/input/fullscreen/About/Hi-Res/settings/audio/gamepad/logs/first-run; polish and ViewModel tests | 13 | 3 |
| M9R | Partial: two-stage, sharp-bilinear/CRT/xBR/bloom/HQx 2x/3x/4x, CPU fallback, screenshots, swap interval, 4K Tier A timing, WGL/ANGLE parity; GLES 3.1 and GPU CI | 10 | 0 |
| M9P | Partial: core thread, two-stage, uploads, DPI, p99/allocations, prefetch, 4K GPU probe; hot-path and Tier B/GPU gates | 6.5 | 3.5 |
| M10 | Partial: CI + six-RID publish + trimmed CLI/frontend + portable archives; arm64, installers, perf report | 4.5 | 1.5 |
| M11 | In progress: M11.0 done; gameplay, high-resolution, and release docs underway | 3 | 22+ |
| **Baseline (full-compatibility plan)** | | **~124 / 157** | **~33 + 30% = ~43** |

**Full-compatibility plan:** roughly three-quarters done; one experienced developer
needs about **9-11 focused weeks**, two developers **5-7 weeks**. If the CD/timing
differential reveals a deeper issue, add 2-4 weeks. That estimate includes 22+ days of
game-specific compatibility hardening and the full platform matrix.

**Adopted v1 release scope (2026-10-26; confirmed 2026-11-02):** v1.0 ships a
**documented compatibility set**, not full compatibility. Supported platforms and titles
are validated and listed in `Compatibility.md`/`KnownIssues.md`; full-race completion,
scripts for the remaining titles, M13.5/M13.6 visual/high-res review, M14 research,
arm64, installers, and the optional JIT are post-1.0. Legal/attribution sign-off is
recorded. **v1.0.0 is tagged and published with unified Windows/Linux/macOS x64
bundles.** The project is roughly **100% complete** against the documented-compatibility
release scope. The release gate is [`ReleaseChecklist.md`](ReleaseChecklist.md).

### 4b. Historical priority change: compatibility sweep and high-resolution research (2026-10-30; superseded 2026-11-02)

The 2026-10-30 plan paused release work for two workstreams. Both are now resolved for
v1.0: M13 loadability is complete (593/594 render), and M14 is post-1.0 research.

1. **M13 - full-collection compatibility sweep.** Complete for loadability: every local
   disc was tested, 593 render, 1 known boot failure, 0 not loadable. The in-game/visual
   review (M13.5) and high-resolution pass (M13.6) are post-1.0.
2. **M14 - high-resolution CEL research.** Post-1.0 research; it does not block v1.0.

The project is **donationware**: non-commercial, source-available, and funded by
voluntary donations rather than sales. Legal/attribution sign-off is recorded. The
Linux validation target is the Hyper-V VM `LinuxMint`
(`D:\Hyper-V\VMs\LinuxMint`). The release checklist is the v1.0 gate; Linux x64 full
suite passed; v1.0.0 is published on GitHub with the artifacts attached.

Supporting documents:

- [`GameCompatibilityList.md`](GameCompatibilityList.md) — 4DO reference list plus
  local matches and empty 5DO result columns.
- [`CompatibilitySweep.md`](CompatibilitySweep.md) — sweep procedure, status
  definitions, triage, and Linux VM validation.
- [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md) — current mode,
  options, experiments, and decision criteria.
- `tools/compat-sweep.ps1` — automated loadability sweep.
- `tools/generate-compat-list.py` — regenerates the compatibility reference table.
- `tools/generate-compat-results.py` — turns a sweep CSV into a readable results doc.

**Reference migration (2026-10-23).** New compatibility work uses
`References/opera-libretro-master/libopera` as the primary behavioral reference; the
existing C# port remains FreeDOCore-based. The adoption-time comparison showed Opera
running Road Rash, Wolfenstein, Alone, and Space Ace past our then-current stop points.
The port has since fixed the DSP execution/immediate/rounding, CCB field-order, NVRAM,
and arbitrary-CEL transposition blockers, and all five corpus titles now boot and
produce audio. Opera remains the behavioral reference for the remaining CD/CLIO/MADAM/
ARM timing work. See [`ReferenceStrategy.md`](ReferenceStrategy.md) and
[Part IV](#part-iv-session-handoff-evidence-and-historical-fixes).

---

## 5. Detailed milestones

### M0 - Bootstrap and guardrails

**Goal:** a clean, buildable repository with the planned project graph, test harness, CI, and documentation.

**Tasks**

- [x] **M0.1 Initialize the repository.**
  - `git init`, `.gitignore` for .NET/Avalonia/test artifacts.
  - `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`.
  - Solution `5DO.sln`.
- [x] **M0.2 Create the production project skeleton.**
  - `src/FiveDo.Core`, `Cpu`, `Memory`, `Madam`, `Clio`, `Dsp`, `Sport`, `Video`, `Video.OpenGL`, `Xbus`, `Cdrom`, `Storage`, `Audio`, `Input`, `Diagnostics`, `Machine`, `Host`, `Cli`, `Frontend.Avalonia`.
  - Correct project references per `docs/Architecture.md`.
  - No placeholder public APIs that will be thrown away.
- [x] **M0.3 Create the test and benchmark skeleton.**
  - `tests/FiveDo.Tests` (xUnit).
  - `benchmarks/FiveDo.Benchmarks` (BenchmarkDotNet).
  - Test utility folder: fake clock, fake memory, fake frame/audio/input sinks (to be added with M1 tests).
- [x] **M0.4 Create the minimal CLI.**
  - `FiveDo.Cli` prints version/usage and returns non-zero on unknown commands.
  - `System.CommandLine` package is reserved for M8; M0 uses a small hand-written parser.
- [x] **M0.5 Create the minimal Avalonia app.**
  - Window with title, version, and "No game loaded" placeholder.
  - `AvaloniaUseCompiledBindingsByDefault`, Fluent theme, Inter font.
  - No emulation logic yet.
- [x] **M0.6 CI.**
  - GitHub Actions: build/test on Windows/Linux/macOS; `dotnet format --verify-no-changes`.
  - Publish matrix skeleton for six RIDs.
- [x] **M0.7 Documentation.**
  - `README.md`, `docs/Architecture.md`, `docs/ImplementationPlan.md`, `docs/PortingNotes.md`, `docs/TestStrategy.md`, `docs/Compatibility.md`, `docs/Licensing.md`, `THIRD-PARTY-NOTICES.md`.
- [ ] **M0.8 Attribution and legal gate.**
  - Preserve FreeDO/4DO headers in ported files (will be added as each file is ported).
  - **Decided:** distribute under the FreeDO modified GNU LGPL as **donationware** (non-commercial, source-available, voluntary donations only); recorded in the root `LICENSE` and `docs/Licensing.md`.
  - Add About/credits placeholder (M9).

**Deliverables**

- Buildable solution with 20 production projects, one test project, one benchmark project.
- CLI and Avalonia app launch with placeholders.
- CI green.

**Acceptance**

```powershell
dotnet build 5DO.sln -c Release
dotnet test 5DO.sln -c Release
dotnet format 5DO.sln --verify-no-changes
dotnet run --project src/FiveDo.Cli -- --version
dotnet run --project src/FiveDo.Frontend.Avalonia
```

- No warnings-as-errors failures.
- No native emulator dependency.
- Project references match the architecture diagram.

**Risks**

- Avalonia 12 template/API churn: pin 12.1.4 and keep the UI minimal in M0.
- Analyzer noise: enable only built-in analyzers first; add third-party analyzers after the skeleton builds cleanly.

---

### M1 - Core contracts, memory, bus, scheduler, state primitives

**Goal:** the shared model and the memory/timing foundation that every chip uses.

**Tasks**

- [x] **M1.1 Core primitives and constants.**
  - `EmulatorConstants`: clock rates, frame cycles, RAM/ROM/NVRAM sizes, address bases, sentinels.
  - `FixMode` flags, `EmulatorTuning`, `CompatibilityState`, `GameQuirks`.
  - `VdlLine`, `VdlFrame`, `FrameBuffer`, `PixelFormat`, `AudioSample`.
  - `EmulationException`, `InvalidStateException`.
- [x] **M1.2 Core interfaces.**
  - `IAddressBus` (byte/halfword/word read/write).
  - `IRam` (raw RAM/VRAM access used by CEL/SPORT/VDLP).
  - `IInterruptSink`, `IFrameSink`, `IAudioSink`, `IPbusSource`, `IDiscSource`, `IXbusDevice`, `IVdlp`, `IMadamBus`.
  - `IClioBus`, `ISportBus`, `IDiagPort` for the M1 bus hooks.
  - `IStateReader`, `IStateWriter`, `IStateSection`.
  - `LogCategory` (Serilog supplies levels and the pipeline).
- [x] **M1.3 Memory implementation.**
  - `FiveDoMemory` with `uint[]` RAM (3 MB + 16 MB high-res mirrors reserved), `uint[]` ROM (2 MB), `byte[]` NVRAM (32 KB).
  - Byte/halfword/word helpers matching FreeDOCore's word-swapped representation.
  - Unaligned reads/writes.
  - ROM loading from big-endian BIOS bytes.
  - NVRAM one-byte-per-word quirk.
  - `0xBADACCE5` sentinel.
- [x] **M1.4 Bus/address decode.**
  - RAM, ROM, NVRAM, Diag, SPORT, MADAM, CLIO ranges in the same order as FreeDOCore.
  - ROM bank select hook.
  - `IAddressBus` implementation with device hooks.
  - No device references yet; use null/stub devices until M3.
- [x] **M1.5 Quartz scheduler.**
  - `PushArmCycles`, `QueueDsp`, `QueueTimer`, `QueueVdl`, `VdCurrLine`, `VdHalfFrame`, `VdCurrOverline`.
  - 8.24 fixed-point arithmetic matching FreeDOCore.
  - Configurable `ArmClock` and constants for VDL/DSP/timer.
  - Save/load of accumulators.
- [x] **M1.6 State primitives.**
  - `StateWriter`/`StateReader` with explicit little-endian primitives, bounds checks, section table, CRC32.
  - `StateSection` IDs and version constants.
  - No chip state yet.
- [x] **M1.7 Diagnostics skeleton.**
  - Serilog pipeline in `FiveDo.Diagnostics.Logging` (`LoggingSetup`,
    `AppLogging`, `InMemorySerilogSink`, `BugReportSink`).
  - Category/level filtering; rolling file, stderr, in-memory, and bug-report sinks.
- [x] **M1.8 Tests.**
  - Memory alignment/byte-order/NVRAM/decode tests.
  - Quartz fixed-point tests: 1 cycle, 15/16/17 cycles, 255 cycles, one-second event counts, 60 half-frames per 12.5M cycles.
  - State reader/writer roundtrip, truncation, CRC mismatch.
  - Logging sink/filter and bug-report formatter tests.

**Deliverables**

- `FiveDo.Core`, `FiveDo.Memory`, `FiveDo.Machine` (Quartz only), `FiveDo.Diagnostics` compile with tests.
- A `TestMemory`/`TestBus` utility for later chip tests.

**Acceptance**

- [x] Memory tests cover all alignments and the NVRAM quirk.
- [x] Quartz produces exactly 60 VDL half-frames (60 presented frames) per 12.5M cycles with the FreeDO event semantics.
- [x] State primitives reject truncated/corrupt data cleanly.
- [x] `dotnet test` green.

**Risks**

- The word-swapped representation is easy to get subtly wrong. Mitigate with a `MemoryByteOrderTests` table test that compares every byte of a known word against the C++ behavior.
- `clks << 24` overflow: document and assert the expected range; test with 1, 15, 16, 255.

---

### M2 - ARM60 CPU

**Goal:** a complete, faithful port of `arm.cpp`, validated instruction by instruction.

**Tasks**

- [x] **M2.1 CPU state and reset.**
  - `Arm60State`: user registers, banked FIQ/IRQ/SVC/ABT/UND registers, SPSRs, CPSR, pending FIQ, ROM select, data-abort flag.
  - Reset: PC `0x03000000`, SVC mode, CPSR `0x13`, zero registers.
  - `IAddressBus` integration.
- [x] **M2.2 ALU and barrel shifter.**
  - `ArmAlu` operations 0-31.
  - `ArmBarrelShifter` with `NSC`/`SC` variants, carry-out, shift types 0-4.
  - Condition code table and evaluation.
  - Flag helpers: N/Z/C/V, mode, I/F.
- [x] **M2.3 Decoder skeleton.**
  - Instruction class order matching `_arm_Execute`: multiply, swap, ALU, undefined, single data transfer, block transfer, branch, SWI, coprocessor/undefined.
  - C# replacement for C++ fallthrough (ordered checks or `goto case`).
  - Condition check before execution.
- [x] **M2.4 Data processing.**
  - Immediate and register operands, rotate, register-specified shift.
  - All 16 opcodes, S bit, PC destination, SPSR restore.
  - Carry-out for logical ops.
- [x] **M2.5 Multiply and swap.**
  - MUL/MLA, S bit, cycle count based on `calcbits`.
  - SWP/SWPB, PC adjustment, unaligned word rotation.
- [x] **M2.6 Single data transfer.**
  - LDR/STR byte/word, pre/post index, writeback, negative offset, shifted register.
  - PC-relative and PC destination.
  - Privileged/user-mode register selection.
- [x] **M2.7 Block data transfer.**
  - LDM/STM IA/IB/DA/DB, writeback, user-mode bit, PC in list, empty list.
  - The `ldm_accur`/`stm_accur` timing quirks and `FIX_BIT_TIMING_1` value fixes.
- [x] **M2.8 Branch, SWI, undefined, MRS/MSR.**
  - B/BL, SWI vector, undefined vector, coprocessor/undefined path.
  - MRS/MSR CPSR/SPSR, mode changes, banked register switching.
- [x] **M2.9 FIQ and memory interface.**
  - FIQ entry after instruction when `!ISF && NeedFiq`.
  - `mreadb/mreadw/mwriteb/mwritew` decode order and sentinels.
  - `SelectROM`, raw RAM helpers.
  - `biosanvil` PC skip and `cnbfix` compatibility hacks.
- [x] **M2.10 Cycle accounting.**
  - `NCYCLE=4`, `SCYCLE=1`, `ICYCLE=1`.
  - Multiply, swap, LDR/STR, LDM/STM, branch, SWI cycles.
  - `Step()` returns the same cycle count as `_arm_Execute`.
- [x] **M2.11 Disassembler.**
  - Decode the implemented subset to readable ARM assembly.
  - Used by CLI trace and debug logs.
- [x] **M2.12 Test assembler.**
  - `ArmTestAssembler` with helpers for data processing, loads/stores, branches, LDM/STM, SWI, MRS/MSR.
  - Test memory/bus with configurable mapping.
- [x] **M2.13 Instruction test suite.**
  - One test class per instruction group (see `docs/TestStrategy.md`).
  - Condition-code matrix, shift matrix, flag matrix.
  - Banked-register/FIQ tests.
  - Cycle-count assertions.
- [x] **M2.14 Synthetic boot program.**
  - A 1 MB synthetic ROM that computes a checksum, writes results to RAM, and loops.
  - Run 1M instructions; assert results and no unmapped accesses.
- [-] **M2.15 Optional oracle trace.**
  - Developer-only harness to compare PC/registers/cycles with FreeDOCore for a small program.
  - Deferred: the product must not load the native core, and a developer-only oracle would live outside this repository. The instruction and synthetic tests are the default regression gate.
- [x] **M2.16 CPU backend abstraction.**
  - `ICpuBackend` with `Step`, `Reset`, state, and register access.
  - `Arm60` (the interpreter) is the default implementation.
  - `EmulatorTuning.CpuBackend` selects interpreter/JIT; JIT is added in M12.
  - No behavior change; tests run against the interpreter.

**Deliverables**

- `FiveDo.Cpu` with `Arm60`, state, ALU, shifter, decoder, disassembler.
- Instruction test suite with hundreds of cases.
- Synthetic boot program.

**Acceptance**

- [x] Every implemented instruction has normal/boundary/flag tests.
- [x] All 16 condition codes and all shift types are covered.
- [x] Cycle counts match the reference for representative instructions.
- [x] FIQ entry/exit and banked registers match the reference.
- [x] Synthetic program runs 1M instructions without exception or out-of-range PC.
- [x] `dotnet test` green; no per-instruction allocations.

**Risks**

- C++ switch fallthrough is the most likely source of decode bugs. Mitigate with a decode-order test table and a disassembler cross-check.
- Banked register switching is subtle. Mitigate with mode-transition tests that snapshot all banks.
- `int`/`uint` signedness in address arithmetic. Mitigate with explicit casts and tests at `0x7FFFFFFF`/`0x80000000` boundaries.

---

### M3 - Boot skeleton: BIOS, CLIO, VDLP, SPORT, frame loop

**Goal:** load a BIOS, execute it, service basic CLIO/MADAM/VDLP/SPORT behavior, and produce frames. Real BIOS may not fully boot until M4/M5/M6; this milestone proves the machine loop.

**Tasks**

- [x] **M3.1 BIOS/NVRAM loading and reset.**
  - `BiosImage` (1 MB, optional 1 MB second, MD5, Anvil detection).
  - `NvramImage` (32 KB, volume header initialization).
  - `FiveDoMachine.Reset()` order: ARM, ROM swap, VDLP, SPORT, MADAM, XBUS + CD reset, CLIO `0x40`, DSP, frame, diag, Quartz.
- [x] **M3.2 CLIO basic register file.**
  - `uint[65536]` registers.
  - Defaults: `0x28=0x40`, `0x400=0x80`, `0x220=64`.
  - Interrupt registers and `NeedFiq`.
  - ROM select `0x84`.
  - `0x00` version read `0x02020000`.
- [x] **M3.3 CLIO timers.**
  - 16 timers, control/reload/cascade/decrement.
  - `DoTimers`, FIQ generation for odd timers.
  - `0x120` timer-dependent write, `0x220` delay.
- [x] **M3.4 CLIO FIFOs and DMA skeleton.**
  - 13 input/4 output FIFOs; start/next/length; status.
  - XBUS DMA path with a stub XBUS device (returns zeros).
  - FIQ 29 on DMA complete.
- [x] **M3.5 MADAM register file and basic pokes.**
  - 2048+64 registers.
  - `0x04=0x29`, `0x00=0x01020000`, `0x574=0xFFFFFFFC`.
  - CEL status/FSM.
  - VDL head write hook to VDLP.
  - Matrix engine (commands 0-3) implemented early.
- [x] **M3.6 VDLP startup and line parser.**
  - `Vdlp` state and startup VDL at `0xB0000 + 2 MB`.
  - `ProcessVdl`, `DoLine`, CLUT/background/out-control parsing.
  - `VdlFrame` line writes.
  - 320x240 output.
- [x] **M3.7 SPORT.**
  - Color/fill/copy/masked operations.
  - 512-word page operations.
- [x] **M3.8 DiagPort.**
  - Shift registers and `_diag_Init(-1)`.
- [x] **M3.9 Quartz integration and frame loop.**
  - `FiveDoMachine.RunFrame()` and `InternalFrame`.
  - VDL line 16 trigger, V0/V1 FIQ, PBUS copy, frame swap.
  - Single-threaded mode first; `skipFrame` path implemented.
- [x] **M3.10 Frame assembler.**
  - Convert `VdlFrame` to BGRA `FrameBuffer`.
  - Background/fixed CLUT/per-line CLUT.
  - Crop/alpha optional (alpha always written; crop deferred).
- [x] **M3.11 Synthetic boot integration test.**
  - Tiny ARM program writes a VDL, enables SPORT/VDLP, draws a background, loops.
  - Assert frame dimensions, non-black pixels, stable frame hash.
- [x] **M3.12 Real BIOS smoke test (optional).**
  - Run 300 frames with a user-provided BIOS.
  - Assert no exception, PC not stuck in a tight invalid loop, frame hash recorded.
  - Skip if `FIVEDO_BIOS` is unset. Validated locally with `bios/panafz1.bin`.
- [-] **M3.13 OpenGL integration spike (early, non-blocking).**
  - Minimal Avalonia + Silk.NET.OpenGL window that displays a synthetic 320x240 indexed frame.
  - Prove context creation, shader compilation, texture upload, and present on Windows/Linux/macOS.
  - **Deferred to M9R.1/M9R.10.** It requires a desktop GL context and a host runner; the CPU frame assembler and machine loop are complete and tested without one. This does not block M4-M8.

**Deliverables**

- `FiveDo.Machine` can construct/reset/run frames.
- BIOS + NVRAM + VDLP + SPORT + CLIO + basic MADAM.
- Synthetic frame output.

**Acceptance**

- [x] Synthetic program produces the expected background frame.
- [x] Real BIOS executes 300 frames without crash (validated locally with `bios/panafz1.bin`; test skips when `FIVEDO_BIOS` is unset).
- [x] Timer/FIFO/DMA unit tests green.
- [x] Frame loop cycle count matches `12_500_000 / 60` per frame (within the reference's 16-cycle batching overshoot).
- [x] No per-frame allocations (test asserts zero allocations over 60 steady-state frames).

**Risks**

- Real BIOS may wait on CD/DSP/CEL features not yet implemented. Treat "executes 300 frames" as the gate; full boot is M4/M5/M6.
- VDLP line timing is easy to off-by-one. Mitigate with line-by-line tests at 0, 16, 239, 240, 525.

---

### M4 - MADAM graphics: CEL engine, pixel pipeline, matrix engine

**Goal:** render real 3DO graphics: packed/literal/LR CELs, PLUT, pixel processing, projection, clipping, matrix transforms, PBUS DMA.

**Tasks**

- [x] **M4.1 CEL bit reader.**
  - Port `BitReaderBig` (`bitop.cpp`) exactly.
  - Tests for bit rates 1-32, positions, skip, end-of-buffer.
- [x] **M4.2 CCB parser.**
  - `CCBFLAGS` constants, `NEXTCCB`, `PDATA`, `PLUTDATA`, `PRE0`, `PRE1`.
  - Absolute/relative pointers, `CCB_LAST`, `CCB_SKIP`, `CCB_PACKED`, `CCB_LDPLUT`, etc.
  - FSM transitions (parser-level; the CEL engine consumes the parsed header in M4.6).
- [x] **M4.3 PLUT.**
  - `LoadPLUT` address math, 2/4/16/32 entries.
  - Palette write bit handling (the 16-bit palette-write bit is decoded in M4.4).
- [x] **M4.4 Pixel decoder.**
  - 1/2/4/6/8/16 bpp.
  - Linear/uncoded vs PLUT-coded.
  - 16-bit palette-write bit.
  - AMV computation.
- [x] **M4.5 Pixel processor and projector.**
  - `PPROC`: MS/MF/SF/2S/AV/2D, PXOR, NOBLK, clipping.
  - `PPROJ_OUTPUT`: PLUT position, SWAPHV, CFBDSUB (disabled), B15/B0 modes.
  - Exhaustive combination tests where feasible.
- [x] **M4.6 CEL geometry and clipping.**
  - `TestInitVisual`, `Init_Line_Map`, `Init_Scale_Map`, `Init_Arbitrary_Map`.
  - `QuardCCWTest`, `TexelCCWTest`.
  - `XY2OFF`.
- [x] **M4.7 Packed CEL rasterizer.**
  - `DrawPackedCel_New`, all three texel paths.
  - `speedfixes`/`unknownflag11`/`sdf`/`sf` compatibility state.
- [x] **M4.8 Literal CEL rasterizer.**
  - `DrawLiteralCel_New`, all three paths.
  - `FIX_BIT_TIMING_6`.
- [x] **M4.9 LR CEL rasterizer.**
  - `DrawLRCel_New`, all three paths.
- [x] **M4.10 Texel draw primitives.**
  - `TexelDraw_Line`, `TexelDraw_Scale`, `TexelDraw_Arbitrary`.
  - `FIX_BIT_TIMING_3`, `FIX_BIT_GRAPHICS_STEP_Y`.
- [x] **M4.11 Matrix engine.**
  - Commands 0-3, 16.16 math, Nfrac, division-by-zero fallback.
  - Register defaults and result latching.
  - Implemented with the M3 register file; M4 adds command-0 and Nfrac coverage.
- [x] **M4.12 DMA/PBUS.**
  - `HandleDMA8`, `DMAPBus`, `KeyPressed`.
  - FIQ generation.
  - Implemented with the M3 register file; M4 adds multi-word and negative-length coverage.
- [x] **M4.13 CEL cycle accounting.**
  - `mread`/`mwrite`/`mreadh`/`mwriteh` CEL cycles.
  - `GetCELCycles` behavior.
  - Implemented in `CelMemory`; the M4.7-M4.10 rasterizers must route VRAM access through it.
- [x] **M4.14 Golden CEL tests.**
  - Hand-built CCB scenes for each bpp/path.
  - VRAM pixel hashes, CEL cycle counts.
  - A composite scene with multiple CCBs, PLUT changes, clipping, transparency.
  - Implemented in `tests/FiveDo.Tests/Madam` (`CelRasterizerTests`, `CelLiteralRasterizerTests`, `CelGoldenSceneTests`): every bpp index, packed/literal/LR line/scale/arbitrary paths, clip-window corners, PLUT reloads, transparent texels, and a deterministic composite-scene VRAM hash with cycle counts.
- [x] **M4.15 BIOS UI validation.**
  - With the CD-ROM/XBUS device attached, a real BIOS (`bios/panafz1.bin`) passes its device poll, renders the boot logo and the console/disc animation (`CelGoldenSceneTests`/`BiosUiValidationTests`, 96+ distinct colors at frame 120), initializes the OS, and reaches the Portfolio shell.
  - The earlier `PC 0x000205c4` "halt" was the Portfolio idle loop, not a renderer failure. All five local corpus titles now load their own OS and render; `BiosUiValidationTests` keeps the opt-in stability/PNG-dump and DSP-start checks.
- [x] **M4.16 High-res decision.**
  - The original decision kept `RESSCALE=0` for v1. It was superseded on 2026-10-24 by the opt-in 640x480 implementation in M11.2; the default remains off.
  - Decision history is recorded in `docs/PortingNotes.md`; M11.2 owns the remaining per-title/default validation.

**Deliverables**

- Full software CEL engine.
- Matrix engine.
- CEL golden tests.
- BIOS UI rendering.

**Acceptance**

- [x] Pixel decoder/processor/projector tests match hand-computed values.
- [x] Packed/literal/LR scenes produce stable VRAM hashes.
- [x] Matrix engine tests match the reference (commands 0-3 and Nfrac).
- [x] BIOS menu renders at 320x240 with correct colors and text.
- [x] No per-frame allocations in CEL rendering (steady-state machine test).

**Risks**

- This is the largest port (~2,000 lines of dense C++). Mitigate by porting in the exact function order, testing each path, and keeping the original variable names in comments.
- The `speedfixes`/`unknownflag11` heuristics are undocumented. Port them as-is; do not invent a cleaner model.
- Floating-point casts in `TestInitVisual`/geometry. Add boundary tests and document saturation behavior.

---

### M5 - DSP and audio

**Goal:** execute the 3DO audio DSP and produce 44.1 kHz stereo samples.

**Tasks**

- [x] **M5.1 DSP memory and registers.**
  - `NMem[2048]`, `IMem[1024]`, register file, `REGCONV`, `RBASEx4`, `REGi`.
  - `DSPPCNT`/`DSPPRLD` 567, `AUDCNT`, semaphore status/data, noise.
- [x] **M5.2 Instruction decoder/precompute.**
  - Port `_dsp_Init`: `INSTTRAS[0x8000]`, request flags, `BS`, `REGCONV`, `BRCONDTAB`.
  - Tests for every request flag combination, plus a 32-opcode branch-table walk.
- [x] **M5.3 ALU and multiplier.**
  - ALU ops 0-15, flags, exact, 32-bit mask.
  - MULT1/MULT2 paths, ACSBU, M2SEL.
- [x] **M5.4 Barrel shifter.**
  - Cases 0-31, arithmetic/logical, CLIP, carry.
- [x] **M5.5 Operand loader.**
  - `OperandLoader`, `OperandLoaderNWB`, NMem/IMem/register windows, DI, NUMOPS.
- [x] **M5.6 Control flow.**
  - NOP, branch accum, set rbase/rmap, RTS, set op_mask, SLEEP, jumps, JSR, conditional branches.
  - Branch condition table.
- [x] **M5.7 Memory windows and semaphore.**
  - `ireadh`/`iwriteh`, EI/I, `_dsp_WriteIMem`, `_dsp_ReadIMem`.
  - ARM/DSP semaphore write/read/ACK.
- [x] **M5.8 Reset/running/PRNG.**
  - `_dsp_Reset`, `SetRunning`, `fastrand`.
- [x] **M5.9 Sample output and cadence.**
  - `(IMem[0x3FF] << 16) | IMem[0x3FE]`.
  - 567-cycle cadence.
  - Channel order verification (low half = left, high half = right).
- [x] **M5.10 Audio pipeline.**
  - `AudioSample`, packed-to-stereo conversion.
  - `AudioRingBuffer` (SPSC monotonic counters, no allocations).
  - `NullAudioSink`, `WavAudioSink`, `AudioPipeline`.
  - Host device output (OpenAL) remains M9.7.
- [x] **M5.11 Tests.**
  - Instruction decode table, ALU/shifter/operand/branch tests.
  - Semaphore tests.
  - Sample stream and WAV header tests.
  - A synthetic sine/square DSP program remains optional; real-content WAV captures now cover the audio path (M5.12/M11).
- [x] **M5.12 BIOS audio smoke.**
  - Run BIOS with DSP enabled; assert samples are produced and no underrun crash.
  - Real BIOS (`bios/panafz1.bin`) starts the DSP; all five corpus titles produce non-silent 44.1 kHz stereo output. The sample clock is now exact (735 samples per 60 Hz frame, 10.000 s per 600 frames); the former ~737 samples/frame FreeDO rounding was corrected in the 2026-10-27 audio pass. Spectrum/loudness comparison with Opera remains a manual M11 check.

**Deliverables**

- `FiveDo.Dsp` with full core.
- `FiveDo.Audio` ring buffer and sinks.
- DSP golden tests.

**Acceptance**

- [x] Decoder/ALU/shifter/branch tests green.
- [x] Sample cadence and packed format match the reference.
- [~] WAV output is audible and correctly pitched (non-silent 44.1 kHz stereo captures exist for all five titles; pitch/cadence comparison with Opera remains).
- [x] No allocations per sample/frame (the DSP core and ring buffer allocate nothing in steady state).
- [x] BIOS produces audio (all five corpus titles produce non-silent output).

**Risks**

- The DSP is bit-level and obscure. The precomputed branch table is easy to get wrong; test all 32 branch codes x 32 flag/exact combinations.
- Sample channel order is not obvious from the C++ (host writes the packed uint directly to DirectSound). Verify with a known WAV and document.

---

### M6 - XBUS, CD-ROM, and disc boot

**Goal:** read real disc images (ISO/BIN/CUE/CHD) through the emulated CD-ROM and boot a game.

**Tasks**

- [x] **M6.1 Disc abstraction.**
  - `IDiscSource` with `SectorCount` and `ReadSector(int lba, Span<byte>)`.
  - `IsoImage` (2048), `BinImage` (2352 + 0x10), `CueSheet` + factory.
  - **`ChdDiscImage`** backed by CHDSharp (pure C#).
  - Format detection by extension/header/size.
- [x] **M6.1a CHD image reader (required v1 format).**
  - Add `CHDSharp` 1.5.2 package reference to `FiveDo.Storage`.
  - Open standalone and parent/child CD CHDs with `ChdFile.Open`.
  - Validate `chd.IsCd`; reject DVD/HDD/raw/audio-only CHDs with a clear error.
  - Read the first data track from `chd.Tracks`; determine payload offset:
    - cooked Mode1/2048 -> 0
    - raw Mode1/2352 -> 16
    - raw Mode2/Form1 -> 24
  - `SectorCount` from the first data track; 3DO volume header remains authoritative.
  - Enable `ConfigureReadAhead(4)` for sequential streaming.
  - Support optional parent CHD path for differential images.
  - Dispose the underlying `ChdFile` deterministically.
- [x] **M6.2 Disc metadata.**
  - Volume header parsing (blockCount at offset 80, big-endian).
  - Sector count fallback (container sector count when no volume header is present).
  - Game ID: MD5 of first two sectors.
  - Game database lookup is deferred to M7.6; unknown IDs return the computed hash.
- [x] **M6.3 XBUS controller.**
  - 16 slots, device 0 CD, device 0xF built-in responder.
  - Select/poll/command/data FIFO, status.
  - FIQ generation.
  - Save/load state section.
- [x] **M6.4 CD device state machine.**
  - `cdrom_Device` fields and reset/`InitCD`.
  - Commands 0x01, 0x02, 0x03, 0x04, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x10, 0x80, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x93.
  - Exact status/data lengths and byte layouts.
  - Sector streaming and `OnSector`/`Read2048` ordering.
- [x] **M6.5 MSF/LBA/TOC.**
  - BCD helpers, MSF2LBA, LBA2MSF, MSF2BLK, BLK2MSF.
  - TOC entry for track 1, disc info, session info.
- [x] **M6.6 CLIO DMA integration.**
  - XBUS DMA to RAM, 4-byte groups, FIQ 29.
  - `len` sentinel and data FIFO draining.
- [x] **M6.7 BIOS + disc boot.**
  - Boot a real disc to the title screen (user-provided).
  - **Done:** the five-title local corpus in [`TestCorpus.md`](TestCorpus.md) boots, loads its own OS, and renders. Road Rash reaches its 3D race, Wolfenstein and Lemmings reach gameplay, Alone reaches its in-game room, and Space Ace plays its demo. Results are recorded in [`Compatibility.md`](Compatibility.md).
- [x] **M6.8 Tests.**
  - Command transcripts for every command (ID, data path check, capacity, read data, eject, save/load).
  - ISO/BIN/CUE fixtures.
  - **CHD fixtures**: generate small CD CHDs with CHDSharp's encoder and verify:
    - ISO vs BIN vs CUE vs CHD produce identical 2048-byte sector streams.
    - Mode1/2048 and Mode1/2352 payload offsets.
    - parent/child CHD opening.
    - invalid/non-CD CHD rejection.
    - read-ahead is enabled; cache/read-ahead behaviour is delegated to CHDSharp and is not separately observable.
  - Multi-sector read test.
  - DMA-to-RAM test (existing CLIO DMA test).
  - Save/load CD state.

**Deliverables**

- `FiveDo.Xbus`, `FiveDo.Cdrom`, `FiveDo.Storage`.
- `IsoImage`, `BinImage`, `CueSheet`, and `ChdDiscImage`.
- Real disc/CHD boot path.

**Acceptance**

- [x] CD command transcripts match expected bytes.
- [x] ISO, BIN, CUE+BIN, and CHD sector reads return identical payloads for the same disc.
- [x] A real CHD disc boots to the title screen (five-title local corpus).
- [x] BIOS boots a real disc to the title screen (five-title local corpus).
- [x] At least one game reaches gameplay (Wolfenstein, Lemmings, Road Rash, and Alone have scripted gameplay evidence).
- [x] CD state save/load works.

**Risks**

- The CD command state machine is full of undocumented status layouts. Use the C++ as the specification and transcript tests.
- Some games may rely on seek/spin timing. FreeDO's model is approximate; document compatibility.
- CUE parsing edge cases (pregap, multiple files) can wait; support single-track MODE1 first.
- CHD track layouts vary (Mode1/2048, Mode1/2352, Mode2/Form1, pregap). Validate the first data track and reject unsupported layouts instead of guessing.
- CHDSharp is a pure C# dependency but its CHD decoder can be CPU-heavy on slow arm64 devices. Enable read-ahead and cache; benchmark CHD boot vs ISO.

---

### M7 - Input, NVRAM, game database, compatibility fixes

**Goal:** make the emulator playable and persistent.

**Tasks**

- [x] **M7.1 Controller state and PBUS encoder.**
  - `FiveDoController` buttons: A/B/C/P/X/L/R, Up/Down/Left/Right.
  - `PbusEncoder` 16-byte packet, 1-6 controllers, device order 1,3,2,5,4,6.
  - Tests for every button and combination.
- [x] **M7.2 Input profiles.**
  - Keyboard bindings, gamepad bindings, console hotkeys.
  - JSON serialization, defaults, conflict detection.
  - 1-6 player profiles.
- [x] **M7.3 Keyboard backend.**
  - `KeyboardInputMapper`/`InputManager` translate key names to controller state without a UI dependency; tests cover binding, release, and multi-player profiles.
  - Avalonia key-event forwarding and focus handling are wired through the M9.3 frontend shell, including release on deactivation.
- [x] **M7.4 Gamepad backend.**
  - `IGamepadBackend`, `GamepadMapper`, and `NullGamepadBackend`; `InputManager` merges gamepad state with the keyboard.
  - Windows uses XInput (`xinput1_4`/`xinput1_3`/`xinput9_1_0`); Linux uses evdev (`/dev/input/event*`) with hot-plug rescanning. macOS falls back to the null backend.
  - The input editor has a Gamepad tab for all six players; analog-stick directions fall back to D-pad bindings and triggers to shoulder bindings.
  - Unit tests cover face buttons, D-pad, sticks, triggers, dead zones, case-insensitive names, and invalid thresholds. Hardware validation is still platform-specific.
- [x] **M7.5 NVRAM persistence.**
  - `NvramStore` under `FiveDo.Host` with the standard NVRAM path supplied by the caller.
  - Debounced atomic write (250 ms via `TimeProvider`), flush on pause/stop/dispose, fresh-image initialization, corrupt-image backup.
- [x] **M7.6 Game database and fix modes.**
  - 4DO `GameDatabase.xml` ported as an embedded resource with attribution; MD5 → ID/name lookup and unknown-ID fallback.
  - `GameCompatibility` maps the 4DO IDs to `FixMode` bits and is applied automatically by the CLI and desktop shell when a disc opens. `--fix-mode`/`--no-game-fixes` and `AppSettings.FixModeOverrides` provide explicit overrides; `--json` and `info` report the applied mask.
- [x] **M7.7 BIOS detection and Anvil.**
  - `byte[28] == 234` -> `SetAnvilFix(30)`.
  - BIOS type is exposed through `BiosImage` and `FiveDoMachine.Compatibility`; UI/CLI display arrives with M8/M9.
- [x] **M7.8 Tests.**
  - PBUS bytes for every button, six-controller ordering, NVRAM debounce/atomic/backup roundtrip, game DB lookup, input profile roundtrip, keyboard/gamepad merge.

**Deliverables**

- Playable input.
- Persistent NVRAM.
- Game-specific fixes.

**Acceptance**

- [x] Keyboard controls a real game (scripted input in Wolfenstein, Lemmings, Road Rash, Alone, and Space Ace; desktop keyboard forwarding wired).
- [~] Gamepad controls a real game (abstraction and null backend exist; the platform backend and mapping UI are M7.4/M9.6).
- [x] NVRAM survives restart and is written atomically.
- [~] Known games with fix flags boot/behave correctly (database and compatibility flags are in place; per-game override UI is M9.5).
- [x] PBUS tests match 4DO's JohnnyInput output.

**Risks**

- Cross-platform gamepad APIs are fragmented. Keep the backend behind an interface; ship keyboard first if gamepad slips.
- Game database licensing/attribution. Port only with attribution and legal review.

---

### M8 - Save states, diagnostics, CLI

**Goal:** deterministic save/load, debugging tools, and a useful headless CLI.

**Tasks**

- [x] **M8.1 State format v1.**
  - Magic `TDO1`, version, header, section table, CRC32.
  - Explicit little-endian writer/reader.
  - BIOS hash, disc hash, emulator version, tuning flags.
  - Reject unknown versions; warn on hash mismatch (`FiveDoMachine.StateWarnings`).
- [x] **M8.2 Per-chip state sections.**
  - CPU, memory (RAM/ROM/NVRAM), MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD, Quartz.
  - Derived tables (DSP precompute, REGCONV/BRCONDTAB) are rebuilt by the constructor; the full RAM allocation, including the high-resolution mirrors, is serialized.
  - The machine section also stores the derived VDL frame and compatibility flags so a load continues bit-identically.
- [x] **M8.3 Machine save/load.**
  - `FiveDoMachine.SaveState(Stream)`, `LoadState(Stream)`.
  - Atomic file writes (`SaveStateToFile`/`LoadStateFromFile`); slot naming is a host/frontend concern (M9.8).
- [x] **M8.4 State tests.**
  - Save -> run 60 frames -> load -> run 60 frames; frame hash, PC, and frame count match the control run.
  - Corrupt/truncated state rejection.
  - BIOS hash mismatch is reported as a warning, not a hard failure.
  - **DONE (2026-10-29):** a disc/game-hash mismatch is also reported as a warning,
    not a hard failure. `FiveDoMachine.LoadState`/`LoadStateFromFile` accept the
    current game ID; the CLI `run`/`trace` commands and the desktop slot manager
    pass it through. Tests cover mismatch, matching, and empty-hash cases.
- [~] **M8.5 Diagnostics.**
  - Frame dumpers (BMP/PNG), state inspector, CPU tracer with disassembly, raw memory/VRAM dumpers, RAM/DSP dumps, and core allocation assertions are implemented; CPU counters are available through the machine.
  - Allocation counters for the full host/presenter path and a UI log viewer remain follow-up work; logging sinks already exist.
- [x] **M8.6 CLI commands.**
  - `run [disc] --bios ... [--frames N] [--state-in/out] [--frame-out] [--audio null|wav] [--wav-out]`.
  - `info <disc>` (game ID, name, sector count, volume header).
  - `bios info <path>` (size, MD5, Anvil).
  - `state inspect <path>`.
  - `bench`.
  - `selftest`.
  - **DONE (2026-10-29):** `disc tracks` reports ISO, BIN, and CUE images as one
    2048-byte user-data track; CHD images keep the full track list. The command no
    longer requires a CHD container.
  - **DONE (2026-10-29):** `fs list`/`fs extract` parse the Opera file system
    directly from ISO, BIN, CUE+BIN, and CHD through `FiveDo.Storage.OperaFileSystem`.
    Multi-block directory chains, `burst`/`gap` file layouts, special entries, and
    malformed-record rejection are covered by tests. The
    `VideoGameFileSystemParser` dependency was removed.
  - Exit codes and machine-readable output (`--json`).
- [x] **M8.7 CLI tests.**
  - State inspector, frame dumper, save/load, patch parser, input-script, and autopilot tests run in the suite.
  - In-process CLI tests now cover version/usage, unknown-command exit codes, JSON errors, missing-BIOS validation, missing state files, and `bench` output.
- [x] **M8.8 Save-state compression.**
  - **DONE (2026-10-29):** state format version 2 stores large section payloads with
    Brotli. `StateWriter` compresses payloads of at least 4 KiB when the result is
    smaller, writes a per-section `flags` field, and keeps the CRC over the
    uncompressed payload. `StateReader` decompresses and validates flagged sections.
    Version-1 states still load; future versions are rejected.
  - `StateInspector` reports `Compressed` and `StoredLength`; `state inspect` prints
    the stored size and marks compressed sections, and the fast header path reads the
    section table without decompressing payloads.
  - Evidence: local 24,902,592-byte version-1 states become 647,647 bytes (300-frame
    Gex) and 2,001,229 bytes (14,900-frame Gex); `SaveStateTests`,
    `StateWriterReaderTests`, `StateInspectorTests`, and `SaveStateSlotManagerTests`
    cover roundtrips, legacy loads, corrupt streams, invalid flags/lengths, and
    compressed slot metadata.

**Deliverables**

- Versioned save states.
- Diagnostics/tracing.
- Full CLI.

**Acceptance**

- [x] Save/load roundtrip is deterministic across process restarts.
- [x] Corrupt states fail cleanly.
- [x] CLI runs a synthetic disc/ROM and dumps frames/states.
- [x] `bench` produces stable numbers.
- [x] No `BinaryFormatter`/`Marshal` state serialization.

**Risks**

- State completeness: missing a field causes subtle desync. Mitigate with a "state hash" that includes all emulated state and a roundtrip test that runs frames after load.
- Derived state (DSP tables, VRAM mirrors) must be rebuilt consistently.

---

### M9 - Avalonia frontend

**Goal:** a polished cross-platform desktop UI.

**Tasks**

- [~] **M9.1 Shell and navigation.**
  - Implemented a minimal shell: video surface, Open BIOS/Open Disc/Play/Reset/Screenshot buttons, and a status line. Library, Settings, Logs, and About pages remain.
  - Dark/light theme, Fluent, Inter.
  - Status bar: FPS, game, BIOS, audio/video state.
- [~] **M9.2 OpenGL video surface host.**
  - `VideoSurface` control based on Avalonia's `OpenGlControlBase`; hosts `OpenGlRenderer` through a Silk.NET `GL` API. A 6-second Windows launch smoke test passed.
  - Aspect ratio, integer scaling, and letterbox are implemented; fullscreen/F11/Escape are implemented, the cursor is hidden in fullscreen, and the optional FPS/status overlay is available.
  - Frame mailbox (`VdlFrameMailbox`): latest frame wins; snapshot copies are tested.
  - Physical-pixel sizing and render-on-demand are implemented (M9P.5); manual DPI/live-resize validation remains.
  - The host now switches to a `WriteableBitmap`/`CpuVideoRenderer` fallback when GL init fails or the context is lost, and switches back when GL recovers.
  - GL readback screenshots are implemented; the CPU screenshot path remains the fallback.
- [x] **M9.3 BIOS setup.**
  - File picker, validation, and MD5/Anvil display in the status line.
  - First-run welcome guide, remembered paths, and error dialogs are implemented. The guide explains the user-provided BIOS/disc requirement and offers Open BIOS/Open Disc/Continue.
- [x] **M9.4 Game library.**
  - Open disc, replace/eject, and volume metadata via the file picker.
  - `GameLibraryScanner` recursively scans ISO/BIN/CUE/CHD files and resolves titles through the 4DO database; `AppSettings.RecentDiscs` keeps the last ten paths and `AppSettings.LibraryFolders` stores scanned roots. The Library window lists recognized titles/formats/sectors/paths, adds folders, refreshes, opens the selected disc, and previews sibling cover art (`<disc>.png/.jpg/.jpeg/.webp`).
- [~] **M9.5 Settings.**
  - Video: renderer (OpenGL/CPU fallback), filter (accurate/nearest/sharp-bilinear/CRT/xBR/HQx), scale, aspect, fullscreen, vsync, CRT bloom.
  - Audio: device, buffer, volume/mute.
  - Input: keyboard/gamepad mapping per player, hotkeys.
  - Emulation: ARM clock, fix mode, high-res (opt-in; default off), save slots, CPU backend (interpreter; JIT when available).
  - Paths: BIOS, saves, screenshots, library.
  - `AppSettings`/`AppSettingsStore` persist paths, renderer/aspect/vsync/integer scale, audio enable/volume, high-resolution mode, library folders, save slots, overlay, automatic fixes, per-game fix/filter overrides, and input bindings. The Settings dialog edits video/audio/emulation/paths and validates the per-game fix mask; the Input dialog has keyboard and gamepad binding tabs for all six players. Per-game fix-mode and filter overrides are complete; other per-game settings remain. The shell exposes a Hi-Res checkbox that restarts the machine.
  - **DONE (2026-10-29):** the shell dispatches the `InputProfile.Hotkeys`
    bindings before forwarding a key to the emulated pad: F5 save, F8 load,
    F6/F7 previous/next slot, F9 frame advance, F10 reset, F11 fullscreen,
    F12 screenshot, and Escape pause by default. `CoreRunner.StepFrame` runs one
    frame on the caller's thread while preserving the loop's pause state, so F9
    leaves emulation paused on the new frame. `HotkeyRouter` resolves custom
    bindings case-insensitively; unknown action names are ignored and tested.
- [x] **M9.6 Input UI.**
  - Binding capture dialog, conflict detection, and reset defaults are implemented for all six players (`InputBindingsWindow`/`KeyboardBindingEditor`); the player selector switches profiles, rebinding a key removes it from its previous button, and the edited `InputProfile` is saved to `AppSettings`.
  - The optional controller preview remains a nice-to-have.
- [x] **M9.7 Audio backend.**
  - `OpenAlAudioOutput` via Silk.NET.OpenAL (or chosen backend).
  - Device selection, buffer sizing, underrun handling.
  - Null fallback if no device.
  - `OpenAlAudioOutput` uses a dedicated thread, a 4096-frame SPSC ring, and three queued 512-frame Stereo16 buffers; the context is made current on the audio thread, underruns are padded/counted, and `TryCreate` returns null with a diagnostic on any device/context failure. The shell enables it when `AppSettings.AudioEnabled` is set; OpenAL Soft 1.23.1 is bundled.
- [x] **M9.8 Save states/screenshots.**
  - Quick save/load, slot next/previous, slot manager.
  - Screenshot with game/time name; GL readback with CPU frame fallback.
  - State metadata (game, BIOS, timestamp, thumbnail optional).
  - `SaveStateSlotManager` provides ten numbered slots with atomic state files and JSON sidecars (game name, game ID, timestamp, frames); the shell has Save/Load plus previous/next slot buttons, a Slots window that lists/saves/loads/deletes slots, and persists the current slot. Screenshots are PNG named `<game>-<timestamp>-f<frames>.png` from GL readback or the CPU frame. Each slot also writes a PNG thumbnail shown in the Slots window; thumbnail failures never fail the state save.
- [x] **M9.9 Logs/About.**
  - About dialog with version, renderer API/capabilities, current machine/disc/fix state, paths, connected gamepads, license notice, and attribution.
  - In-app log viewer with level, category, search, auto-refresh, and clear controls. Clickable links are not implemented.
- [~] **M9.10 ViewModel tests.**
  - Settings roundtrip, library scanning, frame mailbox, input mapping.
  - No UI thread dependency for logic.
  - Tests cover AppSettings roundtrip, the frame mailbox, renderer contracts, input mapping, GamepadMapper, LogViewFilter, save-slot thumbnails, and shader source invariants; ViewModel-level tests remain.
- [x] **M9.11 Input recording.**
  - **DONE (2026-10-29):** `InputRecorder` captures player-1 controller state in
    frame order and coalesces unchanged frames. `InputManager` records through an
    optional recorder/frame provider on the emulation thread, and the desktop
    **Emulation > Record Input** toggle writes `<game>-<timestamp>-input.txt` to
    the save-state directory. The output is the same format as `--input-script`,
    so a desktop recording can be replayed headlessly (with `--script-base state`
    when it starts from a loaded state). Tests cover coalescing, button-name
    formatting, out-of-order rejection, file roundtrip, and the InputManager hook.

**Deliverables**

- Full desktop frontend.

**Acceptance**

- [ ] Runs on Windows, Linux, macOS x64.
- [ ] 60 fps with 320x240 output, no UI thread stalls.
- [ ] Fullscreen and integer scaling work.
- [ ] Keyboard/gamepad mapping works.
- [ ] Save/load/screenshot work.
- [ ] Settings persist.
- [ ] No per-frame allocations in the presenter.

**Risks**

- Avalonia 12 API churn. Pin versions and keep the video surface isolated.
- Audio/gamepad native dependencies per RID. Test each platform early; provide null fallbacks.
- UI frame pacing: use latest-frame mailbox, not a queue.

---

### M9R - OpenGL/GLES renderer (required differentiator)

**Goal:** make the OpenGL renderer the default desktop presentation path, with pixel-accurate palette expansion and a high-quality, GPU-based scaling/filter pipeline.

This milestone is intentionally separate from the Avalonia shell. It can start once `VdlFrame` exists (M3/M4) and must be complete before v0.5.

**Tasks**

- [x] **M9R.1 Renderer contract and context adapter.**
  - `IVideoRenderer`, `RendererSettings`, `VideoFilter`, `AspectMode`, `RendererCapabilities`, and `RendererApi` in `FiveDo.Video`.
  - `GlContextAdapter` wraps Avalonia's GL context/function loader without referencing Avalonia from `FiveDo.Video.OpenGL`.
  - `OpenGlRenderer.Initialize/Resize/Present/Dispose`.
  - `OpenGlCapabilities` parses desktop GL and GLES version strings, reports the API type, and derives integer-texture support from the version. The renderer queries `GL_MAX_TEXTURE_SIZE`; `SwapInterval.TryCreate` resolves WGL/GLX/EGL swap control and `RendererCapabilities.SupportsVsync` reports whether the platform accepted the request.
- [x] **M9R.2 Frame texture upload.**
  - Uploads raw 16-bit indexed pixels (`R16UI`), per-line CLUT as RGB8 (32 x height), and per-line parameters as RGBA32UI.
  - Immutable storage uses `TexStorage2D` at the maximum source size; per-frame updates use `TexSubImage2D`. No `TexImage2D` reallocation in steady state.
  - Uniform locations, sampler units, VAO, and the mailbox callback are cached.
  - The uploader is width/height agnostic; tests cover 320x240 and small synthetic frames, and `VideoAllocationTests` asserts zero allocations across repeated `Update` calls.
- [x] **M9R.3 Palette-expansion pass at native resolution.**
  - Render the indexed frame into a **native-resolution RGB8 texture** (320x240 typical); do not expand the palette once per output pixel.
  - Direct port of `frame.cpp: Get_Frame_Bitmap`:
    - pixel `0` => 5-bit background B/G/R
    - fixed CLUT when `xOUTCONTROLL & 0x02000000` and pixel bit 15
    - otherwise per-line CLUT
  - Exact 5-bit to 8-bit expansion matching the CPU path.
  - Golden-image parity test against `FrameAssembler` for synthetic VDL frames (`GpuPalette` vs `FrameAssembler`, 320x240 x 240 lines).
  - The stage-1 target must be reused across frames; resizing the output must not resize or reallocate it.
  - **Real-content fix (2026-10-25):** the shader originally used only the low five bits of a pixel for all three channels. It now fetches red, green, and blue independently (`pixel >> 10`, `pixel >> 5`, `pixel`), matching `FrameAssembler`/`GpuPalette`. Lemmings gameplay was captured through sharp-bilinear and xBR with correct colors.
  - GPU execution itself is not covered by CI because headless runners have no GL context; run parity on the self-hosted GPU job.
- [x] **M9R.4 Scaling and filter pipeline.**
  - **Stage 2** samples the native-resolution expanded RGB texture at output resolution.
  - **Accurate:** nearest, pixel-perfect, integer scaling.
  - **Nearest:** nearest with arbitrary scale/letterbox.
  - **Sharp-bilinear:** default enhanced mode; full Themaister/rsn8887 sharp-bilinear with hardware bilinear finishing.
  - **CRT:** scanlines, aperture mask, barrel curvature, vignette, and a 3x3 native-resolution bloom are implemented (opt-in; warn at 4K).
  - **xBR/HQx family:** xBR is implemented as an opt-in 4x pass; HQx 2x/3x/4x are implemented as CPU expansions followed by sharp-bilinear finishing. All are opt-in; benchmark at 4K before enabling by default.
  - Implemented accurate/nearest, full sharp-bilinear, CRT scanlines/aperture/curvature/vignette/bloom, xBR 4x, and HQx 2x/3x/4x.
  - Filter settings persist per game/global in the host settings layer (M9.5).
- [x] **M9R.4a xBR filter.**
  - Hyllian's xBR-lv2 edge-directed 4x expansion is implemented in `Shaders.FragmentXbr` and selected with `VideoFilter.Xbr`.
  - The pass uses a CPU-expanded native-resolution RGBA source (`FrameAssembler`) to keep palette output pixel-identical and avoid a driver readback issue with the FBO-attached palette texture; stage 2 finishes with sharp-bilinear.
  - Shader source invariants are tested; the filter was validated visually on Lemmings gameplay (correct colors, smoothed edges) and runs at 60 fps in a 1500x978 window.
  - Remaining optional work: GPU timer measurement at 4K.
- [x] **M9R.4b HQx 2x filter.**
  - Maxim Stepin/Cameron Zemek hq2x is ported as a table/switch-driven CPU expansion in `HqxFilter`, selected with `VideoFilter.Hqx`.
  - It consumes the CPU-expanded BGRA frame, uploads the 2x result, and finishes with sharp-bilinear.
  - A 32x24 reference image matches the C++ `hq2x_32_rb` hash (`0xA400C421`) and uniform images are unchanged.
- [x] **M9R.4c HQx 3x/4x filters.**
  - The hq3x/hq4x switches are generated from the 4DO sources and selected with `VideoFilter.Hqx3x`/`VideoFilter.Hqx4x`; the renderer sizes the intermediate target from `HqxFilter.ScaleFor`.
  - The same 32x24 reference image matches the C++ hashes `0x0C406F97` (3x) and `0xBB17969C` (4x); uniform images are unchanged and the destination-size validation covers 2x-4x.
  - The 320x240 CPU cost is ~2.3/4.2/7.5 ms/frame for 2x/3x/4x on checkerboard content and ~12.1/13.4/14.8 ms on random noise; 3x/4x remain opt-in and are not recommended with high-res on slow CPUs.
- [~] **M9R.5 Presentation.**
  - Aspect ratio: 4:3, integer, stretch, custom.
  - Fullscreen/borderless, vsync, frame pacing, latest-frame mailbox.
  - FPS/GPU-time overlay.
  - Window resize without recreating source textures.
  - Size the GL surface in **physical pixels** (`Bounds * RenderScaling`), not logical units; test 100/150/200% DPI.
  - Render only when a new frame is available (or the window/settings change); do not re-request rendering unconditionally at the end of every render.
  - Aspect 4:3/integer/stretch/custom, the latest-frame mailbox, physical-pixel sizing, render-on-demand, fullscreen/F11/Escape with cursor hide, and an optional FPS/status overlay are implemented. **DONE (2026-10-26):** the platform swap interval is wired through `SwapInterval.TryCreate` (WGL/GLX/EGL) and reported in `RendererCapabilities.SupportsVsync`; manual DPI/live-resize validation remains.
- [x] **M9R.6 Screenshots.**
  - GL readback respecting the active filter, plus an optional native CPU frame.
  - `ReadPixels` and the CPU/GL paths are implemented; the UI writes PNG screenshots from either path and names them with the game title, timestamp, and frame number. The Settings video page can also write a `-native.png` CPU frame next to a filtered GL screenshot.
- [x] **M9R.7 Context loss and fallback.**
  - `OnOpenGlLost`/`Initialize` recreate all GL objects and textures.
  - GL init failure, unsupported context features, or context loss raises `OpenGlFailed`; the shell switches to a `WriteableBitmap`/`CpuVideoRenderer` and switches back when GL recovers.
  - The renderer does not crash on driver/context failure; the status line reports the fallback reason.
- [ ] **M9R.8 Performance.**
  - Measure upload bandwidth, CPU present time, GPU shader time, and p50/p95/p99 frame time.
  - Desktop GL uses a `GL_TIME_ELAPSED` query around the palette/xBR/scale pipeline; the shell overlay reports GPU and CPU-present milliseconds.
  - **DONE (2026-10-26):** `--gl-probe` presents synthetic frames through the real Avalonia OpenGL surface and writes per-frame JSON. It supports a true 4K offscreen target, a warm-up phase, per-stage allocation counters, and both window/offscreen modes. On Windows 11 (Ryzen 9 7900, RTX 4070, GL 4.0), 4K sharp-bilinear measured GPU mean 0.086 ms / p99 0.089 ms and present mean 0.080 ms / p99 0.208 ms, with 0 renderer/mailbox allocations per frame after 30 warm-up frames. 4K accurate/CRT/xBR and 1080p results are in [`Performance.md`](Performance.md).
  - Target: 4K60 with nearest/sharp-bilinear on Tier A x64; 1080p60 on Tier B desktop with the GLES path. Tier A render cost is measured for WGL and ANGLE/GLES 3.0; GLES 3.1 and Tier B remain.
  - Zero CPU per-pixel scaling; palette expansion at native resolution on the GPU.
  - Prove core/presenter CPU cost is independent of output resolution (1080p vs 4K).
  - Budgets and hardware definitions: [`Performance.md`](Performance.md).
- [~] **M9R.9 Tests.**
  - Shader compilation/link tests for both desktop GLSL 330 and GLSL ES 300 (skip if no matching context).
  - CPU-vs-GPU palette parity for all bpp/CLUT/background/fixed-CLUT cases, in both backend variants.
  - Filter screenshot hashes at fixed sizes.
  - Aspect/integer-scaling geometry tests, physical-pixel/DPI sizing tests.
  - Context-loss/fallback tests, including GL 3.3 unavailable -> GLES -> CPU.
  - Allocation assertions for upload/present/mailbox; render-on-demand tests.
  - Headless CI uses the CPU renderer; GPU tests run on a self-hosted or local job.
  - Implemented: uploader packing, palette parity, aspect/integer geometry, CPU renderer, mailbox snapshot, zero-allocation uploader/mailbox tests, OpenGL capability parsing, and shader source invariants for the palette/scale/xBR programs. GPU shader link tests and DPI tests are follow-up.
  - **DONE (2026-10-26):** the `--gl-probe` capture path exercises the real GPU and
    compares a 1:1 Accurate frame with the CPU `FrameAssembler` oracle. On Windows 11
    / RTX 4070, both WGL (OpenGL 4.0/GLSL 330) and ANGLE (OpenGL ES 3.0/GLSL ES 300)
    produced **0 different pixels** and the same FNV-1a hash `0xD6BD89C5` for a
    320x240 synthetic frame. The probe also runs every filter (accurate, nearest,
    sharp-bilinear, CRT, xBR, HQx) under ANGLE. The opt-in
    `GpuProbeIntegrationTests` (`FIVEDO_GPU_TESTS=1`) repeats the WGL/ANGLE parity
    and 4K passes; it also caught and fixed a synchronous-dispose crash in the
    capture path. The `PixelSize.Compute` physical-pixel math is covered for
    100/125/150/200% scaling; on-device DPI validation remains. GPU CI remains.
- [x] **M9R.10 Renderer spike (early, M3/M4).**
  - Before the full renderer, create a small Avalonia + Silk.NET.OpenGL window that displays a synthetic 320x240 indexed frame with the palette shader.
  - This de-risks context creation, shader loading, and presentation on all three OSes.
  - Implemented as the Avalonia `VideoSurface` + `OpenGlRenderer`; a 6-second Windows launch smoke test passed. Linux/macOS smoke validation remains.
- [x] **M9R.11 Optional Vulkan backend and probe.**
  - **DONE (2026-10-29):** `FiveDo.Video.Vulkan` implements an offscreen Vulkan 1.0
    two-stage presenter with build-time SPIR-V shaders (`XenoAtom.ShaderCompiler.Build`),
    sampled R16_UINT/CLUT/parameter textures, accurate/nearest/sharp-bilinear/CRT
    filters, GPU timestamp queries, and a host readback buffer. `--vk-probe` writes the
    shared GPU probe JSON and PNG captures; `--gl-backend vulkan` selects the optional
    desktop offscreen/readback path. A 320x240 Accurate probe matches the CPU oracle
    with 0 different pixels and hash `0xD6BD89C5`; 4K sharp-bilinear measures 1.51 ms
    mean / 1.80 ms p99 present with 0 managed bytes/frame on the RTX 4070. The native
    Win32 swapchain probe was added in M9R.13; Avalonia embedding and non-Windows
    device validation remain future work. See [`Vulkan.md`](Vulkan.md).
- [x] **M9R.12 Vulkan xBR, FSR, and HQx filters.**
  - **DONE (2026-10-29):** the offscreen backend runs xBR-lv2 as a 4x GPU pass and
    FSR 1.0 as EASU plus RCAS GPU passes over the GPU-expanded native frame. HQx
    2x/3x/4x use the shared CPU `FrameAssembler` + `HqxFilter` expansion and a
    reusable upload path; they are CPU-bound and opt-in (4K HQx 2x present 7.66 ms
    mean / 10.36 ms p99). 4K xBR measures 1.55 ms mean / 1.92 ms p99 present and 4K
    FSR 1.87 ms mean / 2.15 ms p99, both with 0 managed bytes/frame. A
    top-red/bottom-blue split-frame test verifies top-down orientation at 320x240 and
    640x480, and the probe integration test covers all three filters at 640x480 with
    `filterFallback: false`. The required OpenGL xBR path now samples the
    GPU-expanded palette texture directly instead of uploading a CPU `FrameAssembler`
    copy.
- [x] **M9R.13 Vulkan native Win32 swapchain probe.**
  - **DONE (2026-10-29):** `VulkanOffscreenRenderer.InitializeSurface` enables
    `VK_KHR_surface`, `VK_KHR_win32_surface`, and `VK_KHR_swapchain`, creates a
    surface/swapchain for a Win32 HWND, blits the rendered output image into the
    acquired swapchain image, and presents with FIFO vsync. `--vk-window-probe`
    opens a real window, runs the same synthetic frames and timing/parity capture as
    `--vk-probe`, and reports `offscreen: false`, `vsyncActive`, and
    `vsyncBackend`. On Windows 11 / RTX 4070, 4K sharp-bilinear measures 5.99 ms
    mean / 6.24 ms p99 present (including the 165 Hz FIFO wait) and 1.34/1.43 ms GPU
    with 0 managed bytes/frame; a 320x240 Accurate capture still matches the CPU
    oracle with 0 different pixels. Avalonia embedding and X11/Wayland/Metal
    surfaces remain future work. See [`Vulkan.md`](Vulkan.md).
- [x] **M9R.14 Optional Direct3D 11 backend and probe (Windows).**
  - Add `FiveDo.Video.Direct3D` with a D3D11 FL11_0 two-stage presenter: R16_UINT indexed pixels, per-line CLUT, line parameters, HLSL palette pass at native resolution, and HLSL scale/filter pass at output resolution.
  - Port Accurate, Nearest, SharpBilinear, CRT, xBR-lv2, and FSR 1.0 (EASU + RCAS) to HLSL; keep HQx 2x/3x/4x on the shared CPU `HqxFilter`.
  - Render offscreen to a reusable target and read back through a staging texture for `ReadPixels`, screenshots, and `--d3d-probe`; present through a DXGI flip-model swapchain on a click-through child HWND (`Direct3DVideoSurface`), matching the Vulkan surface.
  - Add `--gl-backend d3d11` (`dx11`/`directx`/`d3d` aliases), Settings > Video "DirectX 11 (Windows)", and `--d3d-probe`/`--d3d-window-probe` with the shared probe report.
  - Recover from device removal; fall back to OpenGL, then CPU. OpenGL remains the default on all platforms; Direct3D is Windows-only and opt-in.
  - Validate 320x240 Accurate parity (0 different pixels, hash `0xB74C8AC5`), 640x480, all filters, resize/DPI, zero steady-state allocations, and 4K timing. See [`Direct3D.md`](Direct3D.md).

**Deliverables**

- `FiveDo.Video.OpenGL` with the full presentation pipeline.
- Optional `FiveDo.Video.Direct3D` Windows backend (M9R.14/VID-15).
- Accurate CPU/GPU parity tests.
- Filter/shader assets.
- CPU fallback path.

**Acceptance**

- [x] Accurate mode matches the CPU `FrameAssembler` pixel-for-pixel on synthetic frames (CPU reference parity; GPU not executed in CI).
- [ ] Two-stage pipeline: palette expansion at native resolution, then upscale/filter at output resolution.
- [ ] 4K output at 60 fps on Tier A x64; 1080p60 on Tier B desktop via GLES. See [`Performance.md`](Performance.md).
- [x] Filters are opt-in, stable, and do not affect emulated state.
- [x] Optional Direct3D 11 backend matches the CPU oracle and leaves OpenGL as the default.
- [ ] Context loss recovers or falls back cleanly (GL 3.3 -> GLES -> CPU).
- [ ] No per-frame allocations in the upload/present path (uploader and mailbox reuse buffers; GL wrapper calls and uniform lookup are unmeasured).
- [ ] Physical-pixel sizing at 100/150/200% DPI.
- [ ] Runs on Windows, Linux, and macOS.

**Risks**

- GL context integration differs across Avalonia backends/OSes. Mitigate with the early spike and CPU fallback.
- Shader precision/rounding could differ from the CPU palette path. Mitigate with exact integer texture sampling and golden tests.
- Per-line CLUT texture lookups can be slow on weak GPUs. Mitigate with a 32xheight palette texture and `texelFetch`.
- Driver bugs with `R16UI`/integer textures. Mitigate with a fallback to `R16` normalized or CPU BGRA upload.
- xBR/HQx multi-pass filters can be expensive at 4K. Make them opt-in and expose a quality/performance setting.

---

### M9P - Runtime performance, pacing, and 4K presentation

**Goal:** make the 4K60 target structural rather than a late optimization: a dedicated
core thread, a two-stage presenter, an allocation-free upload path, a GL/GLES backend
matrix, and a benchmark gate that runs throughout development.

**Why this exists:** before M9P, the M9/M9R prototype ran emulation on a 16 ms UI
`DispatcherTimer`, expanded the palette once per output pixel, called `TexImage2D`
and `GetUniformLocation` every frame, sized the GL surface in logical pixels, and had
a stub benchmark project. Those were architectural shortcuts that could not be fixed
by micro-optimizing at release time. M9P replaced them with the core thread, the
two-stage presenter, persistent uploads, physical-pixel sizing, and the benchmark
harness. See [`Performance.md`](Performance.md).

**Tasks**

- [~] **M9P.1 Baseline and budgets.**
  - The 2026-10-25 benchmark records p50/p95/p99, max, and per-stage allocations:
    synthetic 1.250 ms/frame total (p99 1.638 ms, 0 bytes/frame); Lemmings 1.281
    ms/frame (p99 3.398 ms, CHD decode ~5.9 KB/frame); Lemmings high-res 1.605
    ms/frame (p99 4.598 ms).
  - **DONE (2026-10-26):** the `--gl-probe` records true 4K Tier A presentation:
    sharp-bilinear GPU p99 0.089 ms, present p99 0.208 ms, 0 renderer/mailbox
    allocations/frame; accurate/CRT/xBR and 1080p results are in
    [`Performance.md`](Performance.md). Tier B/GLES timing remains.
- [x] **M9P.2 Dedicated core thread and frame pacer.**
  - `FiveDo.Host.CoreRunner` and its internal `FramePacer` are implemented.
  - Run `RunFrame` on a dedicated core thread at a fixed 60 Hz; catch-up capped at
    one or two frames.
  - The UI thread never calls `RunFrame`; replace the `DispatcherTimer` emulation
    loop. Keep UI updates (status/FPS) at 4–10 Hz.
  - Pause/resume/reset/save/load and audio must be thread-safe at the session
    boundary; no locks in the core hot path.
- [~] **M9P.3 Two-stage presenter.**
  - Stage 1: palette expansion into a native-resolution RGBA8 FBO.
  - Stage 2: nearest/sharp-bilinear/CRT at output resolution (xBR still pending).
  - Desktop GL 3.3 path implemented; GLSL ES 300 sources are selected when the context
    reports OpenGL ES, but real GLES context/binding validation remains.
- [x] **M9P.4 Persistent upload and allocation-free present.**
  - `TexStorage2D` for the indexed/CLUT/parameter textures plus per-frame
    `TexSubImage2D`; no per-frame `TexImage2D`.
  - Uniform locations and the present delegate are cached; the status string is
    updated at 4 Hz on the UI thread, not in the frame path.
  - `VdlFrameMailbox` copies only the visible width and can copy into a caller-owned
    frame so the lock is not held during GPU submission.
  - The benchmark reports 0 managed bytes/frame for the synthetic core/pack/mailbox/read
    path, and allocation tests cover the uploader and mailbox. CHDSharp decode
    allocations are reported separately and are host-side.
- [~] **M9P.5 Physical-pixel sizing and render-on-demand.**
  - Render-on-demand is implemented: the Avalonia surface renders only when the
    mailbox sequence or the output size changes.
  - Physical-pixel sizing from `Bounds * RenderScaling` is implemented.
  - **DONE (2026-10-26):** the sizing math is extracted into `PixelSize.Compute` and
    covered by 14 tests for 100/125/150/200% scaling, fractional logical bounds,
    zero/negative sizes, and non-finite inputs. Manual on-device DPI and live-resize
    validation remains.
- [~] **M9P.6 Renderer backend matrix.**
  - Desktop GL 3.3 (GLSL 330) and GLES 3.0 (GLSL ES 300) shader sources are selected
    from the reported context version.
  - Windows prefers `Wgl` with `AngleEgl`/software fallbacks. The Silk.NET binding
    path under an ES context is now validated on Windows; Raspberry Pi 4/5
    real-hardware validation remains.
  - **DONE (2026-10-26):** `RendererCapabilities` reports the API type, parsed version,
    max texture size, version-derived integer-texture support, and vsync support.
    `SwapInterval.TryCreate` resolves WGL/GLX/EGL swap control; the renderer applies
    the requested interval and reports whether the platform accepted it. Windows
    WGL and ANGLE/GLES contexts are validated; Pi context validation remains.
  - **DONE (2026-10-26):** the frontend `--gl-backend auto|wgl|angle|software` option
    (and the `--gl-probe-backend` alias) forces a renderer backend. The GPU probe
    validated Windows ANGLE/EGL OpenGL ES 3.0 on an RTX 4070: all filters compile,
    a 4K sharp-bilinear pass presents in 0.14 ms mean / 0.47 ms p99 with 0 renderer
    allocations/frame, and a 320x240 Accurate capture matches the CPU oracle
    pixel-for-pixel. The Silk.NET desktop binding works under the ES context.
    Raspberry Pi 4/5 (GLES 3.1) remains open.
- [~] **M9P.7 CHD/disc prefetch.**
  - `PrefetchingDiscSource` provides a bounded background read-ahead cache with
    serialized inner reads; it is opt-in via `--prefetch` in the CLI and benchmark.
  - Emulated state is unchanged: a 300-frame Lemmings run produced the same PC and
    frame hash (`0x110E7DC5`) with prefetch on/off.
  - x64 comparison (300 frames): Lemmings CHD p99 6.59 ms vs ISO p99 6.70 ms;
    prefetch-on p99 was 14.03 ms on this fast-storage machine. Tier B validation and
    the default policy remain.
- [ ] **M9P.8 Hot-path optimization (measurement-driven).**
  - Optimize the ARM interpreter (fast RAM path, dispatch/decode, fewer interface
    calls) before considering M12.
  - Profile and optimize CEL inner loops and DSP `Loop()` with golden output
    unchanged.
  - Do not add GPU CEL compositing for the 4K output target.
- [~] **M9P.9 Benchmark suite and CI gate.**
  - `FiveDo.Benchmarks` measures the machine core, video pack, mailbox, and read
    copy in synthetic and real-disc modes, with p50/p95/p99, max, per-stage
    allocations, JSON output, and a 60 Hz budget comparison.
  - The steady-state synthetic path reports 0 managed bytes/frame; CHD decode
    allocations are attributed to the core stage.
  - CI runs a synthetic benchmark smoke with `--gate`, which enforces 0 bytes/frame,
    core p99 <= 8 ms, total mean <= 8 ms, and total p99 <= 16.667 ms; the JSON result
    is uploaded as a baseline artifact and the allocation check is repeated with jq.
  - Desktop GL GPU timer queries and the present-time overlay are implemented.
  - `BenchmarkComparison` plus `--baseline`/`--baseline-threshold` implement the
    same-machine relative check (default 10%, exit code 4) across core p99, total
    mean, and total p99. Tier B baselines and wiring the relative check to a stable
    runner remain.
- [ ] **M9P.10 Documentation and report.**
  - Publish the measured 4K/1080p results, hardware, and filter settings.
  - Update `Performance.md`, `TestCorpus.md`, and the compatibility matrix.
  - **DONE (2026-10-26):** `Performance.md` records the WGL and ANGLE/GLES 3.0 4K
    presentation matrix, the 1080p/720p cases, the zero-allocation result, and the
    pixel-parity evidence. TestCorpus/compatibility updates are unchanged because
    this pass is renderer-only.

**Deliverables**

- Dedicated core thread and frame pacer.
- Two-stage 4K presenter with persistent upload.
- Desktop GL/GLES backend variants.
- Benchmark harness and CI performance gate.
- Measured 4K60 Tier A and 1080p60 Tier B report.

**Acceptance**

- [ ] 4K60 with nearest/sharp-bilinear on Tier A; 1080p60 on Tier B desktop.
- [ ] Core frame time p99 ≤ 8 ms on Tier A and independent of output resolution.
- [ ] Presenter CPU ≤ 2 ms p99; zero managed allocations per frame in steady state.
- [ ] Physical-pixel sizing at 100/150/200% DPI.
- [ ] Render-on-demand verified at 60 Hz and 144 Hz.
- [ ] GL 3.3 and GLES 3.0/3.1 variants produce identical Accurate-mode output.
- [x] No `DispatcherTimer` emulation loop remains; no per-frame status strings.
- [ ] Benchmark baseline and regression gate are in CI (baseline artifact is in CI; the gate is not).

**Risks**

- Avalonia/WGL/ANGLE context differences. Mitigate with the backend matrix and a
  CPU fallback; validate on real hardware, not only a launch smoke test.
- Two-stage presenter adds an offscreen target. At native resolution it is tiny; the
  cost is one extra fullscreen sample, not a second 4K pass.
- Threading bugs at the session boundary. Keep the core single-threaded; only the
  host mailbox/ring buffer crosses threads.
- Tier B CHD decode cost. Prefetch/cache and measure; document minimum hardware.
- Performance CI noise. Gate PRs on allocations/unit budgets; run timing gates
  nightly on stable hardware.

---

### M10 - Packaging, CI, performance, arm64

**Goal:** shippable, reproducible builds for all target platforms.

**Tasks**

- [~] **M10.1 Publish profiles.**
  - `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.
  - Self-contained, single-file; both the CLI and frontend are trimmed (`PublishTrimmed=true`, `TrimMode=full`).
  - **DONE (2026-10-27):** opt-in NativeAOT win-x64 publishes are validated for both executables (`-p:FiveDoNativeAot=true`); the CLI is 10.54 MB and opens the bundled OpenAL runtime, the frontend is a 28.8 MB single-file executable plus native Skia/HarfBuzz/ANGLE assets. Silk.NET.Core loader warnings remain. **DONE (2026-10-29):** the frontend AOT OpenAL path was validated with `ALSOFT_DRIVERS=null` (`OpenAL audio ready (44100 Hz stereo)`).
  - Include native audio/gamepad assets and GLSL shader resources per RID.
- [x] **M10.2 CI matrix.**
  - Build/test on Windows/Linux/macOS.
  - Publish all RIDs; upload artifacts.
  - Cache NuGet; deterministic builds.
- [x] **M10.3a CLI trimming.**
  - Settings, input profiles, save-slot metadata, and every CLI `--json` response now use source-generated `JsonSerializerContext` metadata; the CLI output path no longer uses anonymous types or reflection.
  - The RAM-write watchpoint formatter uses `DiagnosticMethodInfo` instead of `StackFrame.GetMethod`.
  - `FiveDo.Cli` publishes with `PublishTrimmed=true` (`TrimMode=full`) as a self-contained single file; the six-RID CI publish uses the same settings, and a Windows CI smoke test runs `--version`, `selftest --json`, and `bench --frames 2 --json` against the trimmed executable.
  - The win-x64 audit also covered `info`, `state inspect`, `state slots`, `fs list` on a generated CHD, CHD boot/run, and a save-state roundtrip. The only remaining trim warning is CHDSharp's vendored FLAC settings helper (`TypeDescriptor`); it is outside the 3DO data-track path and demoted to a visible warning. See [`Trimming.md`](Trimming.md).
  - **DONE (2026-10-26):** the CLI also publishes with NativeAOT via the opt-in
    `-p:FiveDoNativeAot=true` property. The win-x64 single file is 10.54 MB and
    passes `--version`, `selftest`, `bench`, `info`, `fs list`, a 3,000-frame
    Lemmings boot with the current `PC 0x0012B36C` / hash `0x6CCA83F5`, and live
    OpenAL audio. CI publishes
    and smoke-tests this AOT CLI on Windows; the six-RID matrix remains trimmed IL
    because cross-OS AOT needs a native toolchain per target.
- [x] **M10.3b Frontend trimming/AOT audit.**
  - Removed `ViewLocator` and its `Application.DataTemplates` registration; every window is constructed explicitly. All XAML bindings are compiled (`AvaloniaUseCompiledBindingsByDefault=true`, `x:DataType` on every window/`DataTemplate`), so the trimmed app does not use reflection-based XAML lookup.
  - `FiveDo.Frontend.Avalonia` publishes with `PublishTrimmed=true` (`TrimMode=full`) as a self-contained single file for all six RIDs; CI publishes the trimmed frontend and runs `--smoke-test --smoke-output <path>`, which opens the real window, runs one UI pass (GL hidden so headless CI needs no GL context), writes a marker, and exits 0.
  - The frontend startup parser (`--version`, `--disc`, positional disc, `--smoke-test`, `--smoke-output`) has unit tests. The CHDSharp vendored-FLAC warning uses the same visible-but-non-fatal policy as the CLI.
  - win-x64 trimmed single-file is 26.3 MB vs 91.3 MB untrimmed (150.9 MB vs 215.9 MB for the full folder). **DONE (2026-10-26):** the frontend also publishes with NativeAOT via `-p:FiveDoNativeAot=true`; the 28.8 MB native executable plus native assets passes `--smoke-test` and the WGL/ANGLE 320x240 parity probes. Silk.NET.Core's `Assembly.Location`/`DependencyContext` loader still emits `IL3000`/`IL3002`; the opt-in build keeps those warnings visible and the default six-RID publishes remain trimmed IL. See [`Trimming.md`](Trimming.md).
- [~] **M10.4 Icons/metadata.**
  - App icon, version info, a Linux `.desktop` entry (`packaging/5do.desktop`), and positional/`--disc` startup loading are implemented.
  - Windows/macOS file associations still need the installer/app bundle (M10.7); `.tdos` state-file association remains.
- [~] **M10.5 Performance validation and report.**
  - Consume the M9P.9 benchmark harness and baselines; do not defer structural optimization to this milestone.
  - **DONE (2026-10-26): Tier A 4K60 presentation.** `--gl-probe` measures
    sharp-bilinear GPU p99 0.089 ms, present p99 0.208 ms, and 0 renderer/mailbox
    allocations/frame on the RTX 4070 development machine. Accurate/CRT/xBR and
    1080p results are recorded in [`Performance.md`](Performance.md). **DONE
    (2026-10-26):** the same probe now forces ANGLE/GLES 3.0 (`--gl-backend angle`),
    records 4K/1080p present p99 0.40-0.83 ms with 0 renderer allocations, and
    proves pixel parity with the CPU oracle. Tier B (1080p60) and the real-disc
    CHD-vs-ISO matrix remain.
  - Verify core/presenter CPU cost is independent of output resolution and allocations are zero/frame.
  - Publish the performance report and update [`Performance.md`](Performance.md).
- [ ] **M10.6 arm64 validation.**
  - Run on Windows arm64 (if available), Linux arm64, macOS arm64. Raspberry Pi is out of scope.
  - Verify audio/gamepad backends.
- [~] **M10.7 Portable archives and installers.**
  - `packaging/package.ps1` creates `5do-<component>-<rid>.zip` for Windows and `.tar.gz` for Linux/macOS, with README/notices/licensing, a generated `PACKAGE.txt`, and the Linux desktop entry. CI publishes and uploads both the raw folders and the portable archives for all six RIDs.
  - **DONE (2026-10-29):** the portable archives include the OpenAL Soft LGPL
     notice/source offer (`docs/OpenALSoft.md`) and the complete LGPL-2.0 text
     (`docs/OpenAL-Soft-COPYING.txt`), and `PACKAGE.txt` names them.
     Remaining: MSIX/dmg/AppImage packaging, signing/notarization, Windows/macOS
     file associations, and installer smoke tests.

**Deliverables**

- Six RID packages (raw publish folders plus portable zip/tar.gz archives).
- CI artifacts.
- Performance report.

**Acceptance**

- [ ] Every package starts and reaches the UI/CLI.
- [x] The trimmed CLI package starts and passes `selftest`/`bench` on win-x64.
- [x] Portable CLI archives build for win-x64 (zip) and linux-x64 (tar.gz) and contain the executable, notices, licensing, and `PACKAGE.txt`.
- [ ] OpenGL context initializes on supported systems; CPU fallback works otherwise.
- [ ] Tests green on CI.
- [ ] Performance targets met or documented.
- [ ] No native emulator code in any package.

---

### M11 - Compatibility hardening and release

**Goal:** real-game compatibility, high-res, filter polish, release.

**Release scope (2026-10-26):** v1.0 ships the documented compatibility set. Full
gameplay completion, additional input scripts, and deeper CD timing work are post-1.0
backlog, not release gates; the release gate is
[`ReleaseChecklist.md`](ReleaseChecklist.md).

**Tasks**

- [x] **M11.0 Adopt Opera/libopera as the primary behavioral reference.**
  - Decision and hierarchy recorded in [`ReferenceStrategy.md`](ReferenceStrategy.md).
  - Existing C# port remains FreeDOCore-based; new behavior is traced to Opera.
  - The adoption-time comparison showed Opera running Road Rash, Wolfenstein, Alone,
    and Space Ace past our then-current stop points with the same BIOS and disc data;
    the port has since fixed those blockers and all five titles boot.
  - First-session plan and acceptance criteria in [Part IV](#part-iv-session-handoff-evidence-and-historical-fixes).
- [~] **M11.1 Compatibility corpus.**
  - Test the matrix in `docs/Compatibility.md`.
  - Five-title golden corpus boots and produces audio; Wolfenstein, Lemmings, Road
    Rash, Alone, and Space Ace have scripted gameplay evidence. Seven additional
    titles (Gex, Need for Speed, Super Street Fighter II Turbo, Total Eclipse,
    Killing Time, Theme Park, Return Fire) boot, render, and save-state-roundtrip;
    Gex, Total Eclipse, and Super Street Fighter II Turbo show gameplay/attract
    scenes. See `TestCorpus.md` 1.7.
  - Post-1.0 backlog: finish the Road Rash race, script Alone inventory, add
    input/gameplay scripts for the seven new titles, and continue CD/CLIO/MADAM/ARM
    timing triage with the Opera differential harness.
  - Add per-game fixes only with evidence.
- [~] **M11.2 High-resolution mode.**
  - `RESSCALE=1` is implemented as the opt-in `--high-res` 640x480 path: four VRAM
    planes, VDLP line interleaving, SPORT mirroring, CEL stepping, larger frames,
    uploads, screenshots, and save-state frame sections.
  - Golden tests and the Lemmings title validation pass; the five low-res 3,000-frame
    corpus hashes are unchanged.
  - The desktop shell now exposes a Hi-Res checkbox that persists the setting and
    restarts the machine; the CLI exposes `--high-res`.
  - Remaining: per-game defaults, validation on other titles, and a decision on a
    true 640x240 non-interlaced mode.
  - The true internal 640x480 / supersampling / GPU CEL research is now M14; this
    `RESSCALE` path is line doubling and remains the reference behavior.
  - This is not the 4K output target. 4K *internal* rendering requires GPU CEL
    compositing and is a separate experiment.
- [x] **M11.3 Filter polish (required family, optional per game).**
  - Accurate/nearest, full sharp-bilinear, CRT scanlines/aperture/curvature/vignette/bloom, xBR-lv2, and HQx 2x/3x/4x are implemented.
  - Per-game filter overrides are stored in `AppSettings.FilterOverrides` and edited in the Settings dialog; the settings text warns that HQx 3x/4x are the most expensive modes.
  - Keep the accurate path pixel-identical to the CPU oracle.
- [-] **M11.4 CD timing/seek improvements.**
  - Post-1.0 unless a supported title proves a timing defect on the release soak.
  - Use `libopera/opera_cdrom.c` as the behavioral reference for command/status,
    DIPIR, tagged status, chunks, subcode, and media access.
  - Port only the behavior the differential trace proves is wrong; keep transcript
    tests and the existing disc-end divergence.
- [-] **M11.5 CHD support.**
  - Moved to v1/M6: `ChdDiscImage` via CHDSharp, including parent/child and read-ahead.
- [~] **M11.6 Documentation/release.**
  - User guide, compatibility list, known issues, and changelog are implemented in `docs/UserGuide.md`, `docs/Compatibility.md`, `docs/KnownIssues.md`, and `CHANGELOG.md`.
  - **DONE (2026-10-29):** the OpenAL Soft LGPL notice/source offer/relinking
    record is in `docs/OpenALSoft.md` with the full license text in
    `docs/OpenAL-Soft-COPYING.txt`; the release review and checklist
    record it as complete on the engineering side.
  - **DONE (2026-11-02):** project-owner legal/attribution sign-off recorded under the FreeDO modified GNU LGPL as free, open-source donationware. The stability soak and release notes are complete. Linux x64 full suite passed. v1.0.0 is published on GitHub with the artifacts attached; work through [`ReleaseChecklist.md`](ReleaseChecklist.md).
- [-] **M11.7 Post-release backlog.**
  - Netplay, rewind, debugger UI, CD-DA, PAL, additional peripherals.
- [-] **M11.8 Optional GPU CEL compositing experiment.**
  - Evaluate RTT-based or compute-shader CEL rendering against the software path.
  - Compute shaders require GL 4.3+ or Vulkan/Metal; they are outside the GL 3.3/GLES 3.1 v1 baseline.
  - Keep software CEL as the accuracy path; ship only if parity and performance are proven. This is relevant to 4K internal rendering, not the 4K output target.

**Acceptance**

- [ ] [`ReleaseChecklist.md`](ReleaseChecklist.md) complete.
- [ ] No known crash bugs in the supported test set.
- [ ] Release artifacts and docs published.

---

### M12 - Optional guest ARM JIT (post-v1 unless profiling demands it)

**Goal:** add a pure managed IL JIT for the ARM60 behind `ICpuBackend`, with bit-identical behavior and a measured performance win. This is **not** required for 60 fps; the 12.5 MHz CPU is not expected to be the bottleneck.

**Tasks**

- [ ] **M12.1 Profile first.**
  - Measure interpreter cost per frame on the target devices.
  - Only proceed if CPU interpretation is a measured top-three cost.
- [ ] **M12.2 Block discovery and decoding.**
  - Identify basic blocks, terminators, PC-relative literals, and self-modifying code.
  - Invalidate blocks on RAM writes that overlap translated code.
- [ ] **M12.3 IL emission.**
  - `DynamicMethod`/`ILGenerator` per block, with guest registers mapped to locals/fields.
  - Call memory helpers for loads/stores and MMIO.
  - Preserve condition codes, mode switches, FIQ, cycle counts, and compatibility hacks.
- [ ] **M12.4 Differential tests.**
  - Run the full CPU instruction suite through both backends; compare registers, flags, memory, and cycles.
  - Run synthetic programs and real BIOS/disc smoke tests.
- [ ] **M12.5 Performance gate.**
  - Require a measurable frame-time or CPU-time win; otherwise keep the interpreter default.
- [ ] **M12.6 Safety.**
  - Bounded code cache, invalidation, fallback to interpreter on unsupported instructions.
  - No executable memory, no P/Invoke, no native code.

**Acceptance**

- [ ] Bit-identical architectural state and cycle counts vs the interpreter.
- [ ] Measured win on at least one target platform.
- [ ] No regression in save states or determinism.
- [ ] Disabled by default until proven.

---

### M13 - Full-collection compatibility sweep (loadability complete; visual/high-res post-1.0)

**Goal:** give every local 3DO disc a recorded 5DO status, starting with loadability and
then an in-game/visual check. This is deliberately **not** gameplay-depth work: no
full-race completion, inventory scripting, or long-playthrough gates.

**Release status (2026-11-02):** loadability is complete and is the v1.0 compatibility
baseline. M13.5/M13.6/M13.8 visual and high-resolution review are post-1.0; M13.7 Linux
x64 smoke is a v1.0 release gate.

**Inputs:** `J:\Panasonic 3DO` (589 CHDs), `G:\MAME\MAME Software List CHDs\3do`
(5 CHDs), `bios/panafz1.bin`, and the saved 4DO wiki list (241 titles).

**Tasks**

- [x] **M13.1 Build the reference matrix.**
  - Parse the 4DO MHTML tables into a 241-title reference with score/status/notes.
  - Scan both collections and match titles to local images.
  - Generate [`GameCompatibilityList.md`](GameCompatibilityList.md).
  - Record that the match is filename-based, not revision-proof.
- [x] **M13.2 Add the sweep harness.**
  - `tools/compat-sweep.ps1` boots every disc in a fresh CLI process, uses a fresh
    NVRAM image, records JSON/stderr/PNG, enforces a timeout, and writes
    `summary.csv`/`summary.md`.
  - `-Resume`, `-Limit`, `-Filter`, `-HighRes`, `-NoFrameCapture`, and `-NoPrefetch`
    cover smoke runs, resumed runs, and the high-resolution pass.
  - Smoke-validated on `Cowboy Casino (USA)`, `Captain Quazar (USA)`, and
    `Road Rash (Europe) (Rev 1)`; the `-HighRes` pass also renders `Cowboy Casino`
    at a 640x480 source size.
- [x] **M13.3 Run the baseline loadability sweep.**
  - Ran 3,000 frames per disc on the full 594-disc collection on 2026-10-30.
  - Result: **576 `Loadable/Rendered`, 13 `Loadable/No video`, 5 `Not Loadable`**.
  - Recorded frame count, PC, frame hash, source dimensions, disc hash, DSP state, CD
    status/poll, elapsed time, and image metrics.
  - Generated `docs/GameCompatibilityResults.md` from `summary.csv` with
    `tools/generate-compat-results.py`.
- [~] **M13.4 Triage every failure class.**
  - Group by CLI error, timeout, short run, black frame, palette/CLUT, VDL modulo,
    input wait, and CD READ_ID-only wait.
  - Capture the smallest reproduction command and the relevant trace (CD/CLIO/CEL/
    KPRINT/PBUS) for each class.
  - Compare with the 4DO notes and the Opera reference before changing behavior.
  - Add a game-specific fix only with evidence.
  - Initial triage: four Arcade CD-ROM timeouts (`Crime Patrol`, `Mad Dog II`,
    `Orbatak`, `Shootout at Old Tucson RE3`) match the 4DO "patched BIOS" notes; one
    CHD decompression failure (`Mirai Shounen Conan`, LBA 1); 13 black/blank titles
    need the 12,000-frame rerun.
- [ ] **M13.5 Run the in-game/visual check.**
  - Inspect the final frame for every rendered title.
  - Run 12,000-frame captures for titles that reach a title/menu; this is the in-game
    check, not just loadability.
  - Re-run black/static titles at 12,000 frames and inspect intermediate captures.
  - Run a short A/B/Start input smoke test on a sample of titles.
  - Record `Unplayable` / `Playable` / `Good` in the compatibility list.
- [ ] **M13.6 Run the high-resolution pass.**
  - Run the sweep with `-HighRes` and compare screenshots with the low-res pass.
  - Record crop, sharpness, distortion, speed, and whether the game uses its own
    640-wide VDL mode.
  - Feed the results into M14; do not enable high-res by default.
- [x] **M13.7 Validate on the Linux VM (v1.0 gate).**
  - Build/test the CLI in the local Hyper-V `LinuxMint` VM
    (`D:\Hyper-V\VMs\LinuxMint`).
  - Result (2026-11-02): the full test suite (**1,473/1,473**), CLI `selftest`, `bench`,
    `info`, `bios info`, a 3,000-frame Wolfenstein boot, non-silent audio, and the
    frontend `--smoke-test` passed on Linux Mint 22.3. The GL probe timed out under
    llvmpipe; Linux GPU presentation remains a preview/best-effort limitation.
  - Run `selftest`, the frontend smoke test, a reduced CLI sweep, and the GL probe if
    the VM has a usable GL display.
  - Record renderer/vendor/version and fallback behavior.
- [ ] **M13.8 Merge results into the docs.**
  - Update [`GameCompatibilityList.md`](GameCompatibilityList.md),
    [`Compatibility.md`](Compatibility.md), [`KnownIssues.md`](KnownIssues.md), and
    [`Status.md`](Status.md).
  - Keep the 5DO status, evidence path, and reproduction command for every tested disc.

**Deliverables**

- A complete local-collection status table.
- Per-disc JSON/PNG evidence under `artifacts/compat/` (not committed).
- A triaged failure list with reproduction commands.
- Linux VM validation notes.
- Updated compatibility/known-issues docs.

**Acceptance**

- [x] Every local disc image has a loadability status (594 tested; 593 render, 1 known boot failure, 0 not loadable).
- [x] Every `Not Loadable` result has a triage note (none remain).
- [ ] Every `Playable`/`Good` result has a screenshot and input-smoke evidence (post-1.0 visual review).
- [ ] The high-resolution pass has a per-title result (post-1.0).
- [x] The Linux VM build/test/smoke result is recorded (2026-11-02; full suite 1,473/1,473 passed, GL probe timed out under llvmpipe).
- [x] No BIOS/disc content is committed.

---

### M14 - High-resolution CEL rendering research (post-1.0, 2026-10-30)

**Goal:** decide whether 5DO can render CELs at a genuinely higher internal resolution
without breaking compatibility, and choose the next implementation step. This is
research first; shipping a new default is not assumed.

**Release status:** post-1.0 research. M14 does not block v1.0; the existing opt-in
`--high-res` mode remains available and the default stays 320x240 + output filtering.

**Current position:** `--high-res` is the FreeDO `RESSCALE` path. It uses four 1 MB VRAM
planes, renders X at 640-wide, and line-doubles 240 lines to 480. It is not true
vertical high-resolution rendering. See
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

**Tasks**

- [ ] **M14.1 Measure the current mode.**
  - Run a sample of 2D, 3D, FMV, and 640-wide titles in both modes.
  - Record screenshots, frame time, allocations, and save-state/frame-hash behavior.
  - Instrument CEL geometry for `ResScale`, clip rectangle, `Modulo`, CEL function, and
    X/Y coordinates.
- [ ] **M14.2 Prototype true 640x480.**
  - Keep the existing `RESSCALE` path as the reference.
  - Add a separate flag for independent 480-line rendering; remove the plane 2/3
    duplication only in the prototype.
  - Scale CEL Y, clipping, VDLP timing, SPORT, and state/frame metadata.
  - Validate on Lemmings, a 2D title, and a 3D title.
- [ ] **M14.3 Prototype 2x CEL supersampling.**
  - Add a host-side 2x color target behind a flag.
  - Preserve guest-visible VRAM for titles that read the framebuffer.
  - Downsample deterministically and compare with native/true-640 output.
  - Measure frame time and allocations; reject if it cannot meet the 60 Hz budget.
- [ ] **M14.4 Assess GPU CEL compositing.**
  - Map packed/literal/LR CELs, PLUT, pixel processor, projector, clipping,
    transparency, and matrix transforms to a GL 3.3/GLES 3.0 design.
  - Prototype one simple scene and compare with the software oracle.
  - Record parity gaps and a rough effort estimate; do not ship in this pass.
- [ ] **M14.5 Define per-game high-resolution defaults.**
  - Use the M13.6 results to list titles where `--high-res` is correct and beneficial.
  - Keep the default off; expose a per-game setting only after validation.
- [ ] **M14.6 Record the decision.**
  - Choose: keep output-only upscaling, ship true 640x480, pursue supersampling, or
    start GPU CEL work.
  - Update [`Video.md`](Video.md), [`Compatibility.md`](Compatibility.md),
    [`KnownIssues.md`](KnownIssues.md), and the compatibility list.

**Deliverables**

- A measurement table for the current high-res mode.
- A true-640 prototype or a documented rejection.
- A 2x supersampling prototype or a documented performance rejection.
- A GPU CEL feasibility note.
- A recorded decision and updated docs.

**Acceptance**

- [ ] The current mode is documented per tested title.
- [ ] The true-640 prototype is either validated on a defined title set or rejected
      with evidence.
- [ ] The 2x prototype has frame-time/allocations data and a go/no-go decision.
- [ ] The GPU CEL note has a parity/performance estimate.
- [ ] The default remains the compatibility-safe 320x240 + output filter unless a
      title is explicitly validated.
- [ ] No regression in the existing low-res golden hashes.

---

### M15 - Differential harness and regression baseline (active, 2026-10-31)

**Goal:** make accuracy changes measurable without manual side-by-side runs. This
implements FND-05, CORE-11, and CORE-16 from
[Part II — Feature Specification and Backlog](#part-ii-feature-specification-and-backlog).

**Delivered:**

- [x] **M15.1 Checkpoint contract.** `run --checkpoints N,N,... --audio-hash
      --state-hash` emits frame hash, PC, instruction count, sample count, audio
      hash, state hash, CD status/poll, DSP state, and CEL busy cycles.
- [x] **M15.2 Harness.** `tools/FiveDo.DiffHarness` stages the bundled OperaCore
      and BIOS, extracts/caches a data ISO, runs both engines with the same
      options, compares normalized BGRA8888 frame hashes, applies manifest
      allowances, and writes JSON/Markdown/HTML reports.
- [x] **M15.3 First evidence.** The Lemmings smoke matches at frame 300
      (`0x110E7DC5` on both engines); the early boot frames diverge and are
      recorded as a scheduling finding.
- [x] **M15.4 Manifest.** `benchmarks/diff/smoke.json` and
      `benchmarks/diff/showcase.json`; schema and staging rules are documented in
      [`DifferentialHarness.md`](DifferentialHarness.md).
- [x] **M15.5 Baseline gate.** `FiveDo.DiffHarness baseline` compares report
      fields and exits non-zero on a changed 5DO value.
- [~] **M15.6 Nightly.** `.github/workflows/diff-harness.yml` targets a
      self-hosted Windows runner with the local corpus; the first showcase run is
      recorded. The nightly schedule is wired but has not run on the runner yet.
- [~] **M15.7 Full collection.** `manifest` generated the portable 594-title
      `benchmarks/diff/full.json`; the full run remains (about 8-9 hours).
- [ ] **M15.8 Reference-side fields.** Add PC/state/CD transcript exports to the
      reference wrapper (requires rebuilding the SimpleGui core, not changing
      libopera behavior).
- [~] **M15.9 Visual artifacts.** Checkpoint PNG/BMP captures and report links are
      in place; the trend dashboard remains.

**Acceptance**

- [x] `dotnet test` covers the hashers, manifest, comparer, reports, baseline,
      and CLI checkpoint contract (1,123 tests pass).
- [x] A real BIOS/disc smoke run matches the reference frame hash.
- [x] The five-title showcase has a recorded report: two video matches and three
      named video divergences at 3,000 frames; the strict PC gate shows 0/5 and
      makes the video-only matches explicit.
- [ ] Nightly CI publishes a report artifact and fails on regressions.
- [~] The full collection has a checkpoint manifest; known-divergence allowlisting
      and the first full run remain.

---

## 6. Historical first sprint (completed 2026-10-23)

This was the original two-week bootstrap plan. It is retained as a record; every item
was completed during M0-M2. The current next actions are in §12.

**Days 1-2 - M0**

- [x] Create solution, projects, references, build props.
- [x] Create CLI/Avalonia placeholders.
- [x] Create `FiveDo.Tests` and fake sinks.
- [x] CI build/test.
- [x] Commit the docs.

**Days 3-5 - M1**

- [x] Core constants/interfaces.
- [x] `FiveDoMemory` + byte-order tests.
- [x] Address decode + sentinel tests.
- [x] `Quartz` + fixed-point tests.
- [x] State writer/reader + CRC tests.

**Days 6-10 - M2.1-M2.6**

- [x] CPU state/reset.
- [x] ALU/shifter/conditions.
- [x] Decoder skeleton.
- [x] Data processing instructions + tests.
- [x] Multiply/swap + tests.
- [x] Single data transfer + tests.

**End of sprint demo**

- [x] A synthetic ARM program executes data processing/loads/stores and writes a known result to RAM.
- [x] `dotnet test` green.
- [x] Plan updated with actuals.

---

## 7. Cross-cutting workstreams

### 7.1 Testing

- Unit tests accompany each ported function.
- Integration tests use synthetic ROMs and discs.
- Golden tests for CPU traces, frames, DSP samples, CD transcripts.
- Optional developer-only oracle against FreeDOCore.
- Fuzz tests for CPU/CD/CEL/state parsers.
- Benchmarks for CPU/CEL/DSP/frame loop.

See [`TestStrategy.md`](TestStrategy.md).

### 7.2 Documentation

- Keep `docs/PortingNotes.md` updated with every deliberate deviation.
- Keep `docs/Compatibility.md` updated with tested games.
- Add XML docs to public APIs.
- Add a `CHANGELOG.md` at v0.1.

### 7.3 Performance

- Performance is a continuous gate, not an M10 cleanup. [`Performance.md`](Performance.md) defines the target, budgets, hardware tiers, and measurement method.
- Measure before optimizing; record before/after numbers and identical golden output.
- No per-frame or per-instruction allocations in steady state. No LINQ, string formatting, or delegate allocation in hot paths.
- Prefer `Span<T>`, `Unsafe`, `MemoryMarshal`, and reusable buffers only where measurements justify them.
- Keep the core single-threaded and deterministic. The host owns the core thread, frame pacer, render thread, and audio thread.
- The 4K target is output upscaling. Do not add GPU CEL compositing for 4K output; 4K internal rendering is post-v1.
- Do not sacrifice accuracy for speed without golden evidence.

### 7.4 Legal/attribution

- Preserve FreeDO/4DO headers.
- Keep the About/Licenses page current.
- No BIOS/disc content in the repo or releases.
- Distribution is **donationware** (non-commercial, source-available, voluntary
  donations only). Do not sell builds or describe donations as a license purchase.
- **Recorded 2026-11-02:** distribution uses the FreeDO modified GNU LGPL as free,
  open-source donationware (non-commercial, source-available, voluntary donations
  only). Project-owner legal/attribution sign-off is recorded; revisit the rights-holder
  terms before any commercial use or a different distribution model.

---

## 8. Risk register

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | MADAM CEL port has subtle pixel/geometry bugs | High | High | Port in original order; exhaustive pixel tests; golden frames; real BIOS UI comparison |
| R2 | ARM decode fallthrough/flag bugs | Medium | High | Instruction matrix tests; disassembler; optional oracle traces |
| R3 | DSP branch/ALU table bugs | Medium | High | Test all branch codes/flags; sample golden tests; WAV listening |
| R4 | CD command state machine mismatches | Medium | High | Transcript tests for every command; real disc boot; compare with 4DO |
| R5 | Scheduler timing approximations cause game hangs | Medium | High | Preserve FreeDO semantics; add game-specific fixes only with evidence |
| R6 | Cross-platform audio/gamepad dependencies | Medium | Medium | Interfaces + null fallbacks; test per RID early; choose SDL/OpenAL after spike |
| R7 | Avalonia API churn / UI performance | Medium | Medium | Pin versions; latest-frame mailbox; OpenGL presenter with CPU fallback |
| R7a | OpenGL context/driver differences across Windows/Linux/macOS | High | High | Early M3.13 spike; GL 3.3 capability check; CPU fallback; context-loss recovery |
| R7b | GPU palette shader differs from CPU frame assembler | Medium | High | Integer textures, exact 5-bit expansion, golden parity tests |
| R7c | Optional ARM IL JIT diverges from interpreter | Medium | High | `ICpuBackend` differential suite; disabled by default; interpreter oracle |
| R8 | Save state incompleteness | Medium | High | State hash; roundtrip tests; section-by-section ownership |
| R9 | Licensing restrictions block distribution | Low | High | Resolved for v1.0 (2026-11-02): FreeDO modified GNU LGPL, donationware (non-commercial), attribution; revisit before commercial use |
| R10 | No redistributable BIOS/disc for CI | High | Medium | Synthetic ROM/disc fixtures; opt-in real-content tests |
| R11 | Scope creep (extra filters, netplay, rewind, GPU CEL) | High | Medium | Renderer filter family is fixed in M9R; everything else deferred to M11+ |
| R12 | Performance on arm64 | Low/Medium | Medium | Benchmark early; avoid allocations; profile CEL/DSP |
| R13 | Floating-point differences x64/arm64 | Low | Medium | Integer timing; golden frame hashes; replace hot float math if needed |
| R14 | Native audio/gamepad packaging complexity | Medium | Medium | Isolate per RID; ship null fallback; document optional backends |
| R15 | CHD track layouts / CHDSharp integration | Medium | High | Validate first data track; support Mode1/2048, Mode1/2352, Mode2/Form1; reject unsupported layouts; ISO/BIN parity tests |
| R16 | CHD decompression cost on slow arm64 | Medium | Medium | Read-ahead/cache; async prefetch; benchmark CHD vs ISO p99; document minimum hardware |
| R17 | 4K presentation is CPU/GPU-bound or jittery | High | High | Two-stage presenter; persistent upload; dedicated core thread; p99 budgets; M9P gate |
| R18 | Desktop GL 3.3 assumption fails under Avalonia ANGLE or on Pi4 | High | High | GL 3.3 + GLES 3.0/3.1 variants; WGL/ANGLE matrix; real-hardware validation |
| R19 | Performance work deferred to release and becomes a rewrite | High | High | M9P before M10; benchmark harness and allocation tests now |
| R20 | 4K internal rendering expectations are conflated with 4K output | Medium | Medium | Document the distinction; 4K internal is post-v1 and requires GPU CEL |

---

## 9. Quality gates / definition of done

A milestone is done only when:

- [ ] All tasks are checked or explicitly deferred with a reason.
- [ ] Unit tests cover new behavior.
- [ ] At least one integration test exercises the milestone through the machine.
- [ ] Golden/differential tests pass where applicable.
- [ ] Save/load roundtrip is tested for new state.
- [ ] No per-frame allocations in steady state.
- [ ] Performance budget met for the milestone; p50/p95/p99 frame time, allocations, and hardware recorded.
- [ ] Renderer backend matrix validated where applicable (desktop GL 3.3 and/or GLES 3.0/3.1).
- [ ] No mutable static state.
- [ ] XML docs and traceability comments are present.
- [ ] `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` pass.
- [ ] CI is green on all supported runners.
- [ ] The plan and compatibility docs are updated.

**Per-commit checklist**

- [ ] One logical change.
- [ ] Tests added/updated.
- [ ] No unrelated formatting churn.
- [ ] No copyrighted content.
- [ ] Commit message names the milestone/task (e.g., `M2.4: data processing instructions`).

---

## 10. Open decisions

These need a decision before the relevant milestone; the plan assumes the default in parentheses.

1. **Final product name and namespace prefix.** **Decided:** product `5DO`, solution `5DO.sln`, namespace prefix `FiveDo`.
2. **Distribution license.** **Decided: FreeDO modified GNU LGPL, donationware** — non-commercial, source-available, voluntary donations only. Recorded in the root `LICENSE`; see `docs/Licensing.md` and `docs/ReleaseLegalReview.md`.
3. **Renderer.** **Decided: OpenGL is required for the desktop frontend**, with two variants behind one contract: desktop GL 3.3/GLSL 330 via Silk.NET.OpenGL, and GLES 3.0/3.1/GLSL ES 300 via Silk.NET.OpenGLES. Windows uses WGL or ANGLE/GLES; Tier B desktop uses GLES where needed. The CPU `WriteableBitmap` path is only a fallback/oracle.
4. **Default filter.** Default: sharp-bilinear for enhanced mode; "Accurate" (nearest/integer) must be available and pixel-identical to the CPU assembler.
5. **Audio backend.** Default: OpenAL Soft via Silk.NET.OpenAL, with null/WAV fallbacks. Alternative: SDL2 audio.
6. **Gamepad backend.** Default: evaluate Avalonia 12 built-in; fall back to Silk.NET.Input/SDL2.
7. **4DO save-state compatibility.** Default: no; own versioned format. Legacy import can be added later.
8. **CHD support.** **Decided: required v1 format** via the pure C# CHDSharp library (M6.1a). ISO/BIN/CUE remain supported.
9. **PAL support.** Default: later; NTSC 60 Hz first.
10. **High-resolution CEL mode.** Default: off; the current `--high-res` path is FreeDO-style line doubling. **M14 is post-1.0 research** and does not block v1.0; it will decide whether true 640x480, 2x supersampling, or GPU CEL compositing is worth shipping. Output upscaling to 4K is always available via OpenGL/GLES. **4K internal CEL rendering is not a current goal.**
11. **Minimum OS versions.** Default: Windows 10+, macOS 12+, current Linux desktop with desktop OpenGL 3.3 or OpenGL ES 3.0/3.1.
12. **UI visual direction.** Default: clean dark/light Fluent, game library + settings, no skeuomorphic 3DO shell.
13. **Game database source/licensing.** Default: port 4DO data with attribution, pending review.
14. **Guest ARM JIT.** Default: optional managed IL JIT (M12), disabled by default, only if profiling shows the interpreter is a top cost after interpreter optimization. The .NET JIT is already used for all C# code.
15. **Whether to ship an optional native audio binary.** Default: yes for desktop, but the core and CLI must work without it.
16. **4K target definition.** **Decided: 4K output (3840x2160) at 60 fps on Tier A, not 4K internal CEL rendering.** See [`Performance.md`](Performance.md).
17. **Threading.** **Decided: single-threaded deterministic core on a dedicated core thread; render/UI and audio threads at the host boundary.** The current UI-timer prototype is replaced by M9P.2.
18. **Performance gate.** **Decided: continuous budgets and a nightly/self-hosted benchmark gate; no structural optimization deferred to M10.** See [`Performance.md`](Performance.md).

---

## 11. Effort estimate and sequencing

| Phase | Estimate | Depends on |
|---|---:|---|
| M0 Bootstrap | 3 dd | - |
| M1 Core/memory/scheduler | 5 dd | M0 |
| M2 ARM60 CPU | 12 dd | M1 |
| M3 Boot skeleton | 10 dd | M2 |
| M4 MADAM graphics | 20 dd | M3 |
| M5 DSP/audio | 12 dd | M3 (parallel with M4 after M3) |
| M6 XBUS/CD + CHD | 12 dd | M3 (parallel with M4/M5) |
| M7 Input/NVRAM/fixes | 6 dd | M4/M6 |
| M8 Save states/CLI | 10 dd | M4/M5/M6 |
| M9 Avalonia | 16 dd | M8 |
| M9R OpenGL renderer | 10 dd | M4/M9 (spike can start at M3) |
| M9P Performance/pacing | 10 dd | M9R |
| M10 Packaging/perf validation | 6 dd | M9P |
| M11 Compatibility/release | 25+ dd | M10 |
| **Subtotal** | **157 dd** | |
| Contingency (30%) | ~47 dd | |
| **Total** | **~204 dd** | |
| M12 optional ARM IL JIT | 10-20 dd | M2, only if profiling demands |
| M13 full-collection compatibility sweep | 10-15 dd | M10; current workstream |
| M14 high-resolution CEL research | 10-20 dd | M13.6; can start in parallel with triage |
| M15 differential harness and baseline | 5-8 dd (core delivered) | M13; current workstream |

The 25+ day M11 line is the full-compatibility plan. The adopted v1 scope replaces
most of it with the release gate (soak, compatibility docs, legal review) and moves
the remaining gameplay work, Tier B, installers, and the JIT to post-1.0. Legal
sign-off is recorded; v1.0.0 is published on GitHub with the artifacts attached. See
[`ReleaseChecklist.md`](ReleaseChecklist.md).

M13 loadability is complete (593/594 render). M13.5/M13.6 visual/high-res review and
M14 research are post-1.0 and do not block the v1.0 release.

Suggested parallelization with two developers:

- Dev A: CPU, memory, CLIO, machine, save states.
- Dev B: MADAM, VDLP, SPORT, DSP, CD.
- Dev B (later): OpenGL/GLES renderer and shader pipeline.
- Both: tests, frontend, performance budgets, compatibility.
- Do not parallelize the same chip; keep one owner per subsystem.

---

## 12. Immediate next actions

1. ~~Finish M0 scaffolding and commit.~~ Done.
2. ~~Implement M1 memory + Quartz + state primitives.~~ Done.
3. ~~Start M2 with the CPU state/ALU/shifter and the test assembler.~~ Done.
4. ~~Create the first synthetic ROM integration test as soon as M2 data processing and loads/stores work.~~ Done.
5. ~~Start M3: BIOS/NVRAM loading, CLIO basics, VDLP, SPORT, and the frame loop.~~ Done.
6. ~~Start M4: MADAM CEL engine, pixel pipeline, matrix engine validation, and BIOS UI rendering.~~ Done. The BIOS reaches its shell and all five corpus titles render.
7. ~~M5 DSP/audio and M6 XBUS/CD-ROM/storage.~~ Done. Cores, sinks, disc containers, CHD parent/child, volume header, command transcripts, and real-disc boot are validated; remaining CD timing/seek differential is M11.4.
8. ~~M7 input/NVRAM/game database, M8 save states/CLI, M9 basic shell, M9R core OpenGL presenter, and M10 CI/CLI/frontend publish.~~ Done as a prototype; next are M9P performance/pacing, the remaining M9 pages/audio/settings UI, M9R filters/GPU tests, M10 arm64/installers, and M11 hardening. CLI and frontend trimming are complete (M10.3a/M10.3b).
9. ~~Run the real-content smoke tests and record results in [`Compatibility.md`](Compatibility.md).~~ Done; per-title gameplay, save-state, screenshot, and WAV evidence is being completed.
10. ~~M9P.1/M9P.2: benchmark baseline, dedicated core thread, and frame pacer.~~ Done; continue M9P.3-M9P.9 (GLES 3.1/DPI validation, CHD prefetch, p99/GPU gates).
11. ~~Run the M13.3 baseline loadability sweep.~~ Done: 594 discs tested, 593 render, 1 known boot failure, 0 not loadable.
12. ~~Triage the failure classes.~~ Done: the five former not-loadable rows and 12 black/blank titles are resolved or explained; Crash 'n Burn (Japan) remains the only known boot failure.
13. **Validate on the Linux VM (v1.0 gate).** Build/test/smoke the CLI in the local Hyper-V `LinuxMint` VM (`D:\Hyper-V\VMs\LinuxMint`), run a reduced sweep, and record the GL renderer/fallback result.
14. **Cut v1.0.0.** Bump `FiveDoVersion.Version` and `Directory.Build.props` to `1.0.0`, tag `v1.0.0`, attach the portable archives/checksums, and record the release-owner/QA sign-off.
15. **Post-1.0: run the M13.5 in-game/visual check.** Inspect every rendered final frame, rerun black/static titles at 12,000 frames, and record `Unplayable`/`Playable`/`Good` in [`GameCompatibilityList.md`](GameCompatibilityList.md).
16. **Post-1.0: run the M13.6 high-resolution pass.** Repeat the sweep with `-HighRes`, compare screenshots and speed, and feed the results into M14.
17. **Post-1.0: start M14 research.** Measure the current `--high-res` mode, prototype true 640x480 and 2x supersampling, and write the GPU CEL feasibility note.
18. **Refresh the compatibility docs.** Merge the 5DO status/evidence into [`GameCompatibilityList.md`](GameCompatibilityList.md), [`Compatibility.md`](Compatibility.md), and [`KnownIssues.md`](KnownIssues.md) as the post-1.0 review proceeds.
19. **Keep the differential harness green.** Run `benchmarks/diff/showcase.json`, record the baseline, and extend the manifest to the full collection (M15). Do not allowlist an early-frame divergence without a reason.
20. **Post-release backlog, not current work:** Road Rash full race, Alone inventory, gameplay scripts for the other seven titles, CD/CLIO/MADAM/ARM timing triage, arm64/DPI validation, Tier B timing, installers/signing, frontend AOT audio, ViewModel tests, hot-path optimization, and the optional M12 JIT. WebAssembly, Android/iOS, and Raspberry Pi are out of scope.

---

## 13. Progress log

| Date | Milestone | Task | Notes |
|---|---|---|---|
| 2026-10-23 | M0 | Plan/architecture written | Initial docs and scaffold |
| 2026-10-23 | M0.1-M0.7 | Scaffold complete | `dotnet build`, `dotnet test` (2 tests), and `dotnet format --verify-no-changes` pass; commit `e620ebb` |
| 2026-10-23 | Architecture | OpenGL renderer made required; optional ARM JIT added as M12 | `FiveDo.Video.OpenGL` project added; docs updated |
| 2026-10-23 | Storage | CHD made a required v1 format via CHDSharp | `IDiscSource` + `ChdDiscImage` added; package wired into `FiveDo.Storage` |
| 2026-10-23 | M1.1-M1.4 | Core contracts, memory, and bus | Constants/types/interfaces; `FiveDoMemory` with word-swapped RAM/ROM, NVRAM quirk, bank select, and MMIO hooks |
| 2026-10-23 | M1.5-M1.7 | Quartz, state primitives, diagnostics | 8.24 scheduler; sectioned little-endian state with CRC32; filtered logging with interpolated-string handler |
| 2026-10-23 | M1.8 | M1 test suite | 95 tests pass; memory alignment/byte-order/NVRAM/decode, Quartz fixed-point/event counts, state truncation/CRC, logging filters; commit `9d730ca` |
| 2026-10-23 | M2.1-M2.16 | ARM60 CPU | Full `arm.cpp` interpreter port: state/banked registers, ALU/shifter, decode order, data processing, multiply/swap, LDR/STR, LDM/STM, branch/SWI/undefined, MRS/MSR, FIQ, cycle counts, disassembler, test assembler, synthetic ROM; M2.15 deferred (native oracle outside repo); 262 tests pass; commit `8fd8a9b` |
| 2026-10-23 | M3.1-M3.11 | Boot skeleton | BIOS/NVRAM images, CLIO registers/timers/FIFOs/DMA, MADAM registers/FSM/matrix/PBUS, VDLP parser, SPORT, DiagPort, Quartz frame loop, CPU frame assembler, XBUS stub, DSP stub; 321 tests pass; commit `958d33c` |
| 2026-10-23 | M3.12 | Real BIOS smoke | `bios/panafz1.bin` ran 300 frames without exception; optional test gated by `FIVEDO_BIOS` |
| 2026-10-23 | M3.13 | OpenGL spike deferred | Requires desktop GL context; scheduled with M9R.1/M9R.10 and does not block M4-M8 |
| 2026-10-23 | M4.1-M4.3 | CEL bit reader, CCB parser, PLUT loader | `CelBitReader` (bit rates 1-32, positions, skip), `CcbControl`/`CcbHeader`/`CcbParser` (absolute/relative pointers, preamble, packed/skip/last), `Madam.LoadPlut`; 337 tests pass; commit `b600424` |
| 2026-10-23 | M4.4 | Pixel decoder | 1/2/4/6/8/16 bpp, linear/coded modes, palette-write bit, AMV tables, transparency; 346 tests pass; commit `7ce230d` |
| 2026-10-23 | M4.5 | Pixel processor and projector | `PixelProcessor` (PSCALAR, AV, PXOR, NOBLK, signed-char clipping) and `Projector` (PLUTPOS, SWAPHV/NOSWAP, B15/B0); differential vectors and grid hashes from the reference C++; 400 tests pass; commit `84dec5d` |
| 2026-10-23 | M4.6 | CEL geometry and clipping | `CelGeometry` (`TestInitVisual`, line/scale/arbitrary maps, CCW tests), `TexelFunction`; 300k-case differential hash from the reference C++; 404 tests pass; commit `2f5c106` |
| 2026-10-23 | M4.11-M4.12, M4.16 | Matrix engine, PBUS DMA, high-res decision | Matrix command 0 and Nfrac coverage; multi-word and negative-length PBUS DMA coverage; `RESSCALE=0` decision recorded; 408 tests pass; commit `0b1146c` |
| 2026-10-23 | M4.13 | CEL cycle accounting | `CelMemory` with +1/+2 halfword/word access and +4 PLUT reads, take-and-clear `GetCELCycles`; 411 tests pass; commit `e3d0eea` |
| 2026-10-23 | M4.7-M4.10 | CEL rasterizers and texel primitives | `CelEngine` CCB walker wired into `Madam.HandleCel`; packed/literal/LR rasterizers; line/scale/arbitrary texel primitives; `FIX_BIT_TIMING_3`/`FIX_BIT_TIMING_6`/`FIX_BIT_GRAPHICS_STEP_Y` and `speedfixes`/`unknownflag11`/`sdf` heuristics; bpp matrix (1/2/4/6/8/16), multi-CCB PLUT reload, and transparency/cycle tests; 432 tests pass; real BIOS 300-frame smoke test passes; commit `ed60120` |
| 2026-10-23 | M4.14 | Golden CEL tests | Clip-window corner tests, composite scene with three CCBs, PLUT reload, transparent texel, stable VRAM hash and CEL cycle count; optional BIOS boot-frame PNG dump (`BiosUiValidationTests`); 437 tests pass |
| 2026-10-23 | M5.1-M5.9 | DSP core | Full `DSP.cpp` port: instruction bit-fields, request/BS/REGCONV/BRCONDTAB precompute, ALU/MULT, barrel shifter, operand loader (nrof/immediate/r2/r3/DI), control flow, memory windows, semaphore, reset/PRNG, 567-cycle sample cadence; 19 DSP tests |
| 2026-10-23 | M5.10-M5.12 | Audio pipeline and BIOS smoke | `AudioRingBuffer` (SPSC), `AudioPipeline`, `NullAudioSink`, `WavAudioSink`; allocation-free ring buffer, WAV header, and conversion tests; real BIOS starts the DSP and the frame loop pushes samples; audio content remains silent without a valid disc/menu |
| 2026-10-23 | M9.4 partial | Library scanning | `GameLibraryScanner` with recursive scans, format/sector/metadata inspection, unknown-file fallback, and recent-disc persistence; tests cover valid/invalid/nested images |
| 2026-10-23 | M8.5, M10.4 | Diagnostics and metadata | CPU tracer/disassembler stepping, raw memory/VRAM dumper, PNG frame dumper, application icon and product metadata |
| 2026-10-23 | M9.5, M9.7 | Settings and live audio | `AppSettings` persistence, quick Save/Load and screenshot buttons, `OpenAlAudioOutput` (OpenAL Soft, four queued buffers, null fallback) wired into the shell; Windows launch smoke tests passed |
| 2026-10-23 | M9.1-M9.4, M9.8, M9R.10, M10.2 | Desktop shell and packaging | OpenGL `VideoSurface` hosts the renderer; file pickers, play/pause/reset, quick save/load, screenshots, keyboard input, persisted `AppSettings`; CI publishes CLI/frontend for six RIDs; win-x64 CLI publish smoke test passed; Windows app launch smoke test passed |
| 2026-10-23 | M9R.1-M9R.3, M9R.9-M9R.10 | OpenGL presenter | `IVideoRenderer`/settings/capabilities, CPU renderer, `VdlFrameUploader`, `GpuPalette` parity, aspect/integer viewport, GLSL 330 palette shader, `OpenGlRenderer`, Avalonia `VideoSurface`, frame mailbox; 527 tests pass; Windows launch smoke test |
| 2026-10-23 | M8.1-M8.4, M8.6 | Save states and CLI | Versioned `TDO1` state with CRC sections, per-chip state (incl. DSP/memory/frame/compatibility), deterministic save/load test, atomic files; CLI `run`, `info`, `bios info`, `state inspect`, `bench`, `selftest` with `--json`; state inspector and BMP dumper |
| 2026-10-23 | M7.1-M7.8 | Input, NVRAM, game database | `ControllerState`/`PbusEncoder`/`InputManager` with wiring order and every button; JSON `InputProfile` defaults/roundtrip; `KeyboardInputMapper`; `IGamepadBackend`/null backend; `NvramStore` debounced atomic writes and corrupt-image backup; embedded 4DO `GameDatabase.xml` with checksum/ID lookup; tests for PBUS matrix, profiles, NVRAM and database |
| 2026-10-23 | M6.1-M6.6, M6.8 | CD-ROM, XBUS, storage | `CdromDevice` command state machine and XBUS routing; `IsoImage`/`BinImage`/`CueSheet`/`ChdDiscImage` (CHDSharp 1.5.2, read-ahead, parent/child); `VolumeHeader`/`GameId`; command transcripts, ISO/BIN/CUE/CHD parity, CHD rejection and state roundtrip; BIOS passes the XBUS device poll and renders its boot animation |
| 2026-10-23 | M9P | Performance review and plan correction | Added M9P; two-stage native-resolution palette expansion, dedicated core thread/frame pacer, persistent upload, GL 3.3 + GLES 3.0/3.1 backend matrix, physical-pixel sizing, benchmark/CI gates; documented in `docs/Performance.md` |
| 2026-10-23 | Testing | Local real-content corpus | Documented `D:\Samples\3DO` and `bios/` in `docs/TestCorpus.md`; five CHDs inventoried (Alone in the Dark, Lemmings, Road Rash, Space Ace, Wolfenstein 3D); 600-frame smoke baseline and WAV sample counts recorded; boot still stops in the BIOS menu/halt path for most titles |
| 2026-10-23 | M9P.1-M9P.5, M9P.9 | Core thread, two-stage presenter, persistent upload, render-on-demand, benchmark harness | `CoreRunner` runs the machine on a dedicated 60 Hz thread with pause/invoke marshalling; `OpenGlRenderer` expands palettes into a native-resolution FBO and scales in a second pass; `TexStorage2D` + `TexSubImage2D`, cached uniforms, visible-width mailbox copies, and on-demand rendering; benchmark reports 2.26 ms/frame synthetic and 2.11 ms/frame Lemmings (CPU only); 537 tests pass |
| 2026-10-23 | Compatibility | CD disc-end fix and real-game boot | `InitCd` no longer double-adds the 150-frame lead-in; the CD DIPIR accepts Road Rash, Space Ace, and Wolfenstein. Lemmings renders CyberIntro, Space Ace renders the ReadySoft demo intro, Road Rash/Wolfenstein load their own OS and stop at the shared SAudio init failure. See `docs/TestCorpus.md` |
| 2026-10-23 | Documentation | Status and plan refresh | Added `docs/Status.md` with the real-game matrix, performance snapshot, ~70% completion estimate (~9-12 weeks for one developer), and ordered next steps. Updated README, this plan, Architecture, Compatibility, TestCorpus, TestStrategy, and Performance to match the implemented M9P work |
| 2026-10-23 | M11.0 | Opera reference adoption | Adopted `References/opera-libretro-master/libopera` as the primary behavioral reference for new compatibility work; the existing C# port remains FreeDOCore-based. Verified with the bundled OperaCore + `panafz1.bin` that Road Rash, Wolfenstein, Alone, and Space Ace run past our SAudio `-6996` / Space Ace frame-681 stop points. Added `docs/ReferenceStrategy.md` and `docs/NextSession.md`; corrected the "shared FreeDO limitation" claim in the status/compatibility docs |
| 2026-10-24 | M5.13, M11.1 | DSP execution and audio diagnosis | Fixed headless DSP execution: `AudioSink?.PushSample(Dsp.Loop())` skipped the DSP when no sink was attached, so `AllocInstrument` handshakes timed out. Added RAM-write watchpoints, RAM/DSP dumps, DSP instruction rings, frame-stamped CLIO traces, XBUS DMA traces, and a `disasm` command. Decoded SAudio `-6996` as `SAudioErrAllocInstFailed` from the SDK subscriber and proved Space Ace's runaway is the player's audio task copying to a null output buffer after the same failed `AllocInstrument` path. The DSP still does not ACK the ARM Sema4 command; the next session is a DSP differential against `opera_dsp.c`. 539 tests pass |
| 2026-10-24 | M5.13, M6, M11.1 | DSP/CCB/NVRAM gameplay unblock | Fixed the DSP immediate shift (`IMMEDIATE << (JUSTIFY & 3)`) and multiplier rounding, the CCB field order (geometry before `PRE0`/`PRE1`), and the CLI's fresh NVRAM volume. Added scripted input, CEL/PBUS traces, and progress logging. All five corpus titles boot, render, and produce non-silent DSP audio; Road Rash reaches its race, Wolfenstein reaches FLOOR 1-1 with input, and Alone reaches its in-game room. 550 tests pass |
| 2026-10-24 | M4, M6, M8, M11 | CEL transposition and renderer-correct gameplay | Fixed the arbitrary-CEL X/Y transposition (row passed as column), which had corrupted Road Rash's VRAM allocator (`AllocMem error, bad node address`) and drawn Alone's character horizontally. The VDLP bitmap copy now honors 384/512/640/1024-wide modes, save-state loads restore the VDL picture, and the CLI gained frame-sequence capture, trace RAM dumps, extended register dumps, source-size/VDL-modulo JSON, and `fs list/extract`. Road Rash's race renders like Opera and runs past 60,000 frames; Wolfenstein's live save state roundtrips; 552 tests pass |
| 2026-10-24 | M4, M6, M9, M11 | High-resolution rendering and gameplay loop | Added the opt-in 640x480 high-resolution CEL/VDLP path: four 1 MB VRAM planes, four-plane CEL writes, SPORT mirroring, line-doubled 640x240 output, larger frame/uploader/state support, and the `--high-res` CLI flag. Lemmings' title now matches the 640x480 reference. Verified the Wolfenstein fresh-game walk/turn/fire loop and its save/load roundtrip, plus a 66.86 s gameplay WAV. Low-res golden hashes unchanged; 554 tests pass |
| 2026-10-25 | M11.1, M9P | Road Rash driver v9/v10 and diagnostics | Identified the former terminal pin as the race-over `BUSTED` screen; added close-traffic coast/steer, urgent close-traffic preemption, emergency-brake steer hold, and a throttle-only wreck restart. Driver v10 reaches 91,000 live frames from the 80k/83k states and roundtrips (`0x02ABE78E`); a clean 30k run still pins at ~40,000 and the 100k continuation at ~92,000. Added `--poke`, periodic capture, and trace RAM dumps. 578 tests pass |
| 2026-10-25 | Documentation | Plan reconciliation | Updated the header, product checklist, milestone statuses/acceptance evidence, first-sprint history, and next actions; marked M4/M6/M7 evidence, M8 diagnostics, and M11.1/M11.2/M11.3 partial; corrected the M9/M9R/M9P capability claims. Test count verified at 578 |
| 2026-10-25 | M8.7, M9R, M9P, M9, M11 | Renderer/frontend hardening and benchmark gates | Added honest GL/GLES capability parsing and `RendererApi`, CPU fallback/context-loss recovery, fullscreen/F11, GL readback screenshots, About dialog, Hi-Res shell toggle, CLI process tests, zero-allocation uploader/mailbox tests, full sharp-bilinear, CRT aperture, and an opt-in bounded disc prefetch worker. Benchmarks now report p50/p95/p99 and per-stage allocations; synthetic steady state is 0 bytes/frame (1.250 ms/frame, p99 1.638 ms). Prefetch on/off produced identical Lemmings state/hash. 604 tests pass |
| 2026-10-25 | M11.1 | Road Rash restart motion exit | The throttle-only crash restart now ends after 10 moving-scene frames instead of holding straight for 180 frames, so a traffic stop that starts rolling resumes steering. Added a regression test; the 80k/83k -> 91,000 state and hash remain identical (`PC 0x00085F48`, `0x02ABE78E`). 605 tests pass |
| 2026-10-25 | M7.6, M9.4-M9.6, M9.8, M9R.5-M9R.6 | Desktop shell and per-game fixes | Added `GameCompatibility` (4DO ID -> `FixMode`) with automatic application plus `--fix-mode`/`--no-game-fixes` and per-game settings overrides; `SaveStateSlotManager` with ten numbered atomic slots and JSON metadata; the game library window; the video/audio/emulation/paths settings dialog; six-player keyboard binding capture with conflict removal and reset defaults; PNG screenshots named by game/frame/time; and an optional FPS/status overlay. Added 34 tests; 639 pass. The five 3,000-frame hashes are unchanged (Alone's automatic `FIX_BIT_TIMING_6` does not alter the boot frames) |
| 2026-10-25 | M7.4, M9.3, M9.8-M9.9, M9R.3-M9R.4a, M11.3, M11.6 | Gamepad, logs, thumbnails, xBR, GPU palette fix, release docs | Added Windows XInput and Linux evdev gamepad backends with a six-player mapping tab and analog-stick/trigger fallbacks; an in-app log viewer with level/category/search filters; save-state PNG thumbnails; first-run and error dialogs; and an opt-in xBR-lv2 4x filter. Fixed real-content GPU palette expansion to use independent R/G/B CLUT indices (the old shader made games grayscale) and fixed the `vec4`/`vec2` uniform calls that made GL screenshots black. Added a `GL_TIME_ELAPSED` GPU timer and present-time overlay (Windows GL 4.0 BIOS boot: gpu ≈ 0.13 ms, present ≈ 0.16 ms at 1500x978). Added `docs/UserGuide.md`, `docs/KnownIssues.md`, and `CHANGELOG.md`. 659 tests pass; commit `04a66a7` |
| 2026-10-26 | M11.1, M9R.4b, M9R.4c, M9.5 | Pure D-pad passcode, CRT bloom, HQx, per-game filters | Added `docs/scripts/lem-fun4-dpad.txt`, which types the Lemmings Fun 4 code `HNLHCIOECW` with only the D-pad/A; gameplay starts at 23,000 (`PC 0x00083FD8`, hash `0xB7846C2D`) and roundtrips (`0xEDEB1B9D` twice). Added a 3x3 native-resolution CRT bloom pass and the `Bloom`/`BloomThreshold` settings. Ported hq2x as an opt-in 2x CPU filter with a C++ reference hash test (`0xA400C421`) and a steady-state allocation test. Added per-game filter overrides in `AppSettings` and the Settings dialog. 673 tests pass |
| 2026-10-26 | M9R.4, M9R.4c, M11.3, M9P.9, M10.4 | HQx 3x/4x, the benchmark budget gate, and Linux file associations | Generated the hq3x/hq4x pattern switches from the 4DO sources and selected them with `VideoFilter.Hqx3x`/`Hqx4x`; the same 32x24 reference image matches the C++ `hq3x_32_rb`/`hq4x_32_rb` hashes (`0x0C406F97`/`0xBB17969C`), uniform images are unchanged, and the renderer sizes its intermediate target per scale. Measured the 320x240 CPU cost at ~2.3/4.2/7.5 ms/frame on checkerboard content and ~12.1/13.4/14.8 ms on random noise. Added `BenchmarkGate` plus the CLI benchmark `--gate` flag and wired it into CI: 0 bytes/frame, core p99 <= 8 ms, total mean <= 8 ms, and total p99 <= 16.667 ms. Added positional/`--disc` startup loading, a Linux `.desktop` association file, an optional native CPU-frame screenshot, sibling cover-art previews in the Library window, and fullscreen cursor hiding. 691 tests pass |
| 2026-10-26 | M10.3a, M9P.9, M8.6 | Trim-safe JSON, trimmed CLI publishes, and the benchmark baseline comparison | Replaced reflection-based JSON in `AppSettings`, `InputProfile`, save-slot metadata, and every CLI `--json` response with source-generated contexts and named result records; removed the CLI's anonymous-type/`GetProperty` output path and the watchpoint `StackFrame.GetMethod` call. Enabled `PublishTrimmed=true` (`TrimMode=full`) for the CLI and added a Windows CI trimmed-publish smoke test plus trimmed six-RID publishing. Verified the win-x64 single file with `--version`, `selftest`, `bench`, `info`, `state inspect`, `state slots`, `fs list`, CHD boot/run, a save-state roundtrip, and the Lemmings/Alone 3,000-frame golden hashes. Added `BenchmarkComparison` and `--baseline`/`--baseline-threshold` for the same-machine >10% regression check (exit code 4). The only remaining trim warning is CHDSharp's vendored FLAC settings helper. 704 tests pass |
| 2026-10-26 | M10.7 | Portable release archives | Added `packaging/package.ps1` to stage a publish folder with README, third-party notices, licensing, a generated `PACKAGE.txt`, and the Linux desktop entry, then create `5do-<component>-<rid>.zip` for Windows or `.tar.gz` for Linux/macOS. Both six-RID CI publish jobs now upload a portable package alongside the raw folder. Validated the win-x64 zip and linux-x64 tar.gz contents and ran the win-x64 archive. Installers/signing/file associations remain |
| 2026-10-26 | M10.3b, M10.1, M10.2 | Trimmed frontend publishes and smoke test | Removed the reflection-based `ViewLocator` and its `DataTemplates` registration; every window is now constructed explicitly and all bindings are compiled with `x:DataType`. Enabled `PublishTrimmed=true` (`TrimMode=full`) for `FiveDo.Frontend.Avalonia` for all six RIDs, demoted CHDSharp's vendored-FLAC warning to visible/non-fatal, and added `--smoke-test --smoke-output <path>` plus `--version` startup handling. The smoke test opens the real main window, runs one UI pass (GL hidden for headless CI), writes a `FRONTEND SMOKE OK` marker, and exits 0; CI publishes the trimmed win-x64 frontend and runs it. Added startup-argument unit tests. win-x64 single-file is 26.3 MB trimmed vs 91.3 MB untrimmed. 709 tests pass |
| 2026-10-26 | M9R.5, M9R.8, M9P.1, M9P.6, M10.5 | Swap interval and true-4K GPU probe | Added `SwapInterval.TryCreate` (WGL/GLX/EGL), wired `RendererSettings.Vsync` into `OpenGlRenderer`, and reported honest `SupportsVsync`/`VsyncActive`/backend capabilities. Added a `--gl-probe` frontend tool that presents synthetic 320x240 frames through the real Avalonia OpenGL surface, supports a true 4K offscreen FBO, warm-up frames, per-stage allocation counters, and source-generated JSON. Windows 11/Ryzen 9 7900/RTX 4070 measured 4K sharp-bilinear GPU p99 0.089 ms, present p99 0.208 ms, 0 renderer/mailbox allocations/frame after warm-up; accurate/CRT/xBR and 1080p are recorded in `Performance.md`. 741 tests pass |
| 2026-10-26 | M9P.6, M9R.9, M9P.10, M10.5 | ANGLE/GLES backend validation and CPU/GPU parity | Added the frontend `--gl-backend auto|wgl|angle|software` option (with the `--gl-probe-backend` alias) and wired it into `Win32PlatformOptions.RenderingMode`. Added `--gl-probe-capture` plus an automatic 1:1 Accurate CPU/GPU parity check to the GPU probe, and recorded `requestedBackend`, capture path, and parity in the JSON. Windows 11/RTX 4070 ANGLE (OpenGL ES 3.0, GLSL ES 300) ran every filter; 4K sharp-bilinear present p99 0.47 ms with 0 renderer allocations, and a 320x240 Accurate capture matched the CPU `FrameAssembler` oracle pixel-for-pixel (`0xD6BD89C5`) on both WGL and ANGLE. Added an opt-in GPU integration test (`FIVEDO_GPU_TESTS=1`) that runs the real probe for both backends and a 4K pass; it found and fixed a synchronous-dispose crash in the capture path. Raspberry Pi/GLES 3.1 and DPI validation remain. 775 tests pass |
| 2026-10-26 | M10.3a, M10.3b, M10.2 | NativeAOT CLI/frontend audit and CI jobs | Added the opt-in `FiveDoNativeAot` publish property and Windows CI jobs for the win-x64 CLI and frontend. The CLI single file is 9.97 MB and passes `--version`, `selftest`, `bench`, `info`, `fs list`, and a 3,000-frame boot matching the golden `PC 0x0012B3C8` / hash `0x932C7722`. The frontend publishes as a 28.8 MB single-file native executable plus native assets and passes `--smoke-test` plus the WGL/ANGLE 320x240 parity probes. Silk.NET.Core's loader warnings remain and AOT audio is not yet validated; the default six-RID matrix remains trimmed IL. 775 tests pass |
| 2026-10-26 | M9P.5, M9R.9 | Physical-pixel sizing tests | Extracted `PixelSize.Compute` and added tests for 100/125/150/200% scaling, fractional logical bounds, zero/negative sizes, and non-finite inputs. The on-device DPI/live-resize validation remains. 775 tests pass |
| 2026-10-27 | Audio | Exact DSP clock, live OpenAL, pacing, analyzer | Replaced FreeDO's 8.24 DSP sample accumulator with an exact rational clock (44,100 samples per ARM second) and bumped Quartz states to v2 with v1 conversion; 600-frame captures are now 10.000 s instead of 10.070 s. Bundled OpenAL Soft 1.23.1, made the context current on the audio thread, padded/counted underruns, and added CLI `--audio openal`/`--volume`/`--realtime`. Added `AudioFramePacer` to `CoreRunner`, buffered/instrumented WAV capture, `audio analyze`, audio JSON fields, and opt-in audio tests. 798 tests pass; the 3,000-frame boot keys and audio captures are in `TestCorpus.md` 1.1/1.5a2 |
| 2026-10-26 | Release | 1.0.0-rc.1 release candidate | Adopted the documented-compatibility release scope and added `ReleaseChecklist.md`/`ReleaseLegalReview.md`. Bumped the version to `1.0.0-rc.1`; 775/775 tests, format, and the benchmark gate pass. Six fresh 36,000-frame soaks (five local CHDs plus the Theme Park ISO) and seven archived-state 36,000-frame soaks all exit 0; showcase save/load roundtrips are deterministic; WGL and ANGLE/GLES Accurate parity is 0 different pixels. Trimmed win-x64 CLI/frontend publishes, smoke tests, portable archives, and checksums are recorded; the 3,000-frame golden hashes are unchanged. Legal/distribution sign-off remains the only release blocker at that time (superseded 2026-11-02: sign-off recorded). |
| 2026-10-26 | Naming | Product name finalized as 5DO | Renamed the solution to `5DO.sln`, all projects and namespaces to the `FiveDo.*` prefix, the CLI command to `5do`, packaging archives/desktop entry to `5do`, environment gates to `FIVEDO_*`, and user-data paths to `5DO`. References to the 3DO console and its trademarks are unchanged. |
| 2026-10-28 | Diagnostics, M9, M10 | Serilog logging and bug reporting | Replaced the custom `Log`/`ILogSink` facade with Serilog configured in `FiveDo.Diagnostics.Logging`. Added rolling daily files, stderr console (desktop), `InMemorySerilogSink` for the Logs dock/viewer, and an asynchronous `BugReportSink` that forwards Warning+ events with environment, error, and exception blocks (five-minute dedupe, ten-per-minute cap, CI/test suppression). Added Serilog logging to host entry points and existing catch blocks. The CLI keeps JSON on stdout and concise errors on stderr. 818 tests pass |
| 2026-10-28 | Audio, M9R, M9P | OpenAL crackle fix, PCM tap, FSR 1.0, and race profile | Fixed live OpenAL crackle: the fill loop indexed its buffers by queued count, which named buffers still attached to the playing source, so `BufferData`/`SourceQueueBuffers` rewrote audio about to play; buffers now come from a free pool, variable-size blocks are queued, queued frames are tracked, and `alGetError` is checked. Registered the fill thread as a Pro Audio multimedia thread. Added `FIVEDO_AUDIO_TAP` (exact queued PCM WAV plus per-buffer CSV) and an overlay peak meter; tap validation is 0 OpenAL errors, 0 underruns, and 0 discontinuities across 2,345 buffer boundaries. Restored float uniform casts (black canvas) and added the opt-in FSR 1.0 EASU/RCAS filter with a sharpness setting and MIT attribution. Measured Road Rash's 3D race at 5.46 ms/frame total (p50 2.91, p95 10.60, p99 13.14), 0 bytes/frame, 183 fps. 818 tests pass |
| 2026-10-28 | Architecture | Models/Interfaces folder reorganization | Moved top-level models and enums into each production project's `Models/` folder and interfaces into `Interfaces/`, splitting mixed files. Namespaces stayed project-level; nested implementation details and interop structs stayed with their owning types. 818 tests pass |
| 2026-10-28 | Diagnostics, M9, M10 | Application usage statistics | Added `ApplicationStatsClient`/`ApplicationStatsOptions`; the CLI and desktop hosts POST `{applicationId:"5do", version}` to `https://www.purelogiccode.com/ApplicationStats/stats` at launch. The call is non-blocking, tolerates the one-per-hour rate limit, and is disabled in CI/tests and via `FIVEDO_STATS=0`; endpoint/key/id are overridable. 821 tests pass |
| 2026-10-29 | M8.6 | Opera file-system reader for every container | Added the first-party `FiveDo.Storage.OperaFileSystem` volume/directory parser and switched `fs list`/`fs extract` to it for ISO, BIN, CUE+BIN, and CHD; removed the `VideoGameFileSystemParser` dependency. It follows relative `nextBlock` directory chains and `burst`/`gap` file layouts, rejects cycles/traversal/malformed records, and exposes special entries such as `Disc label`. The Lemmings ISO/CHD pair produces 482 identical entries and 461 byte-identical extracted files; Road Rash, Alone, and Theme Park ISOs also parse. 890 tests pass |
| 2026-10-30 | M13.1-M13.2, M14 | Compatibility sweep tooling and high-resolution research plan | Parsed the saved 4DO wiki into a 241-title reference, scanned the two local collections (589 + 5 CHDs), generated `docs/GameCompatibilityList.md`, and added `tools/compat-sweep.ps1` plus `tools/generate-compat-list.py`. The sweep boots each disc in a fresh process, records JSON/PNG/timeout evidence, and classifies loadability. The first 594-disc pass completed: 576 `Loadable/Rendered`, 13 `Loadable/No video`, 5 `Not Loadable`; results are in `docs/GameCompatibilityResults.md`. Added `docs/CompatibilitySweep.md` and `docs/HighResolutionCelResearch.md`; the v1.0 release was deferred at that time (superseded 2026-11-02). Project distribution is recorded as donationware (non-commercial, source-available); Linux validation target is the local Hyper-V VM `LinuxMint` (`D:\Hyper-V\VMs\LinuxMint`). No runtime tests were added; the current suite remains 925 tests |
| 2026-11-01 | UX-13 | Library metadata, favorites, and playtime | Added `LibraryState`/`LibraryStateStore` for favorites, launch counts, last-played timestamps, and play seconds in `library-state.json`; `LibraryPlaytimeTracker` measures only running emulation and flushes every 60 seconds; `LibraryMetadata`/`LibraryMetadataStore` read an optional offline `library-metadata.json` overlay keyed by game ID or path; the scanner carries 4DO year/publisher/region; `LibraryQuery` adds All/Favorites/Played/Unplayed filters and Title/Recently played/Most played/Release year/Publisher sorts; the game list adds star toggles, metadata/play-time text, and Settings > Paths chooses the metadata file. 13 tests cover state, migration, pruning, store corruption, tracker timing, metadata, filters, search, and sorts; 1,350 tests pass. External providers and rich media remain |
| 2026-11-01 | Quality | ReSharper inspectcode warning cleanup | Ran `jb inspectcode 5DO.sln`: 0 errors, 50 warnings. Fixed all 50 across 18 files (disposed/modified closures, defensive JSON-null checks, unchecked cast overflows, hidden method, unused field, redundant default arguments/qualifiers/usings, `using` resource initialization, `with` expressions, XML doc reference). `jb inspectcode --severity=WARNING` reports 0 results; build 0 warnings/0 errors; 1,350 tests pass; `dotnet format --verify-no-changes` clean; commit `26aad8b` |
| 2026-11-01 | SYS-01/AUD-05 | Multi-track and CD-DA core | Added `ITrackedDiscSource`, `CueDiscImage` (MAME-compatible multi-track CUE), CHD track/audio exposure, multi-track TOC and lead-out, READ SUB-Q, MODE SET/MODE SENSE, 24-bit READ_DATA counts, PLAY_AUDIO tracking, 2352-byte digital-audio reads, 44.1 kHz CD-DA mixing, CD state v3, and multi-track `disc tracks` text/JSON. 7 new CD/storage tests; 1,361 tests pass |
| 2026-11-01 | FND-02 | Settings schema versioning and migration | Added `SettingsVersion`/`CurrentVersion`, legacy normalization, malformed-file backup, renderer/value validation, and version roundtrip. 4 new tests; 1,361 tests pass |
| 2026-11-01 | AUD-05, FND-02 | CD-DA volume and settings completion | Persisted `CdAudioVolume`/`CdAudioMuted` in Settings v2, added the Audio-tab slider/mute check, `--cd-volume`/`--mute-cd`, CD state v4, mix-level tests, per-game `GameProfiles`, and `settings validate|migrate`. AUD-05 and FND-02 are done; 1,376 tests pass |
| 2026-11-01 | SYS-02 | Multi-disc playlist and live swap | Added `DiscPlaylist` (M3U/M3U8, `#EXTINF`, relative paths), persisted playlist path, File > Swap Disc and F3, one-off swap without reset, guest inject `0x07`, `disc playlist`, and `run --playlist/--swap-at/--swap-disc`. SYS-02 is partial; 1,376 tests pass |
| 2026-11-01 | SYS-02 | ODE eject handling and playlist editor | Added `GuestEjectCount`/`TrayOpen`/`DiscPresent`, a core-thread `GuestDiscEjected` callback, `AutoSwapOnEject`, ODE playlist auto-advance, guest-eject ODE behavior, a playlist editor with save/import/reorder, drag-and-drop playlist import, `DiscPlaylist.Create/Add/RemoveAt/MoveEntryUp/MoveEntryDown/Save`, and a swap/state roundtrip test. A `D (USA, Europe)` playlist swap at frame 300 ran to frame 600 with a new disc hash. SYS-02 remains partial pending a real in-game swap; 1,381 tests pass |
| 2026-11-01 | SYS-03 | PAL/NTSC region modes | Added `VideoRegion`/`VideoRegionProfile` (NTSC 320x240@60, PAL1 320x288@50, PAL2 384x288@50), PAL VDL timing and 576-line frame storage, VDLP start-line mapping, region-aware machine frame cycles and source size, 50 Hz CoreRunner pacing, Settings/CLI region options with BIOS auto-selection, GameProfile snapshot, and state options v6 with region warning. Battle Chess Europe renders 320x288 at 50 Hz, and a 300-frame PAL capture is exactly 6.000 s. SYS-03 is partial (desktop auto-detection/per-game override remain); 1,387 tests pass |
| 2026-11-01 | SYS-03 | Desktop BIOS region auto-switch | Added `AppSettings.AutoRegion`, `AppSettings.ResolveRegion`, `FiveDoMachine.SetVideoRegion`, and `Quartz.SetRegion` so the desktop follows the resolved BIOS and switches PAL/NTSC between frames without recreating the machine. Settings > Video region gains the automatic checkbox; `VideoRegionTests` covers runtime switching. 1,392 tests pass |
| 2026-11-01 | SYS-03 | Per-game region override and state switching | Added `RegionOverrides`, Settings > Emulation > Region override, `ResolveRegionForGame`/`ResolveRegionForBios` integration, and state-driven region switching with a live region in the options section. SYS-03 remains partial only for PAL2 title evidence and automatic profile application; 1,415 tests pass |
| 2026-11-01 | CORE-04 | CD transcripts and native-sector read-ahead | Unsupported/unknown CD commands now return an illegal-request status, guest RESET is implemented, `CdromCommandTranscript` captures command/status/poll exchanges, `--cd-transcript` writes them, and `PrefetchingDiscSource` read-aheads audio/native track sectors. Battle Chess boot produced 393 transcript entries; 1,415 tests pass |
| 2026-11-01 | UX-23 | Input display completion | Added `Madam.PbusReadCount`, per-frame lag detection, `CoreStatus.LagFrames`/`IsLagFrame`, selected controller port `InputDisplayPlayer`/`CoreRunner.InputPlayer`, Settings > Video port selector, and overlay lag/player text. UX-23 is done; 1,390 tests pass |
| 2026-11-01 | UX-15 | Named configuration profiles | Added `FIVEDO_PROFILE`/`--config-profile`, per-profile data directories under `profiles/<name>/`, safe profile-name normalization, `AppSettingsStore.ListProfiles`/`GetProfilePath`, and `settings profiles` listing. Portable, override, profile, and sanitization tests pass; UX-15 remains partial for a selector/import-export UI and instance locking. 1,395 tests pass |
| 2026-11-01 | UX-15 | Per-instance write lock | Added `InstanceLock`, an exclusive `5do.instance.lock` in the resolved data directory, desktop conflict handling, and `FIVEDO_ALLOW_MULTIPLE=1`. Separate profiles/directories still run together. UX-15 remains partial for the profile selector/import-export UI; 1,415 tests pass |
| 2026-11-01 | SYS-15 | Pointer routing and calibration | Added `PointerPort` (all or port 1-6), `PointerScale`/`PointerOffsetX`/`PointerOffsetY` with clamping, `AppSettings.MapPointer`/`RoutesPointerTo`, Settings > Emulation pointer controls, and native-resolution mapping for mouse, lightgun, arcade lightgun, and trackball input. SYS-15 remains partial only for scripted gameplay validation; 1,417 tests pass |
| 2026-11-01 | AUD-01 | Audio device and buffer latency | Added OpenAL device enumeration and selection, 20-500 ms buffer targets with derived queue/prefill/ring sizing, live output recreation without resetting the machine, Settings > Audio device/buffer controls, `--audio-device`/`--audio-buffer`, `audio devices`, and latency in the status/JSON output. AUD-01 is partial for resampler quality/exclusive/sync; 1,425 tests pass |
| 2026-11-01 | FND-08, UX-17 | Diagnostics bundle | Added `DiagnosticsBundle` with scrubbed settings/logs/environment/manifest and optional state/frame/replay, `diagnostics bundle` CLI options, and **Help > Save Diagnostics Bundle...** with live state/frame capture. FND-08 and UX-17 are partial for typed errors, compatibility report/upload, and automatic crash capture; 1,427 tests pass |
| 2026-11-01 | SYS-18, UX-07 | Per-game input devices | Added `InputDeviceOverrides`, `AutoInputDevices`, `GameControlDefaults` for known lightgun/trackball titles, `ResolveInputDevices`/`ApplyInputDevices(input, gameId)`, auto seeding at disc open, and the Settings > Emulation per-game override checkbox. SYS-18 and UX-07 are partial for full timing/filter profiles and named bindings; 1,429 tests pass |
| 2026-11-01 | FND-03, CORE-14 | Save-state format v3 | Added header feature flags, an up-front section table with duplicate/version validation, `UnknownSectionPolicy` warn/reject, v1/v2 downgrade tests, and v3 inspector support. The options section records the live region. FND-03 and CORE-14 are done; 1,433 tests pass |
| 2026-11-01 | CORE-15 | State diff tooling | Added `StateDiff` (section presence/version/length/FNV hash/first difference/differing-byte count) and CLI `state diff <a> <b> [--json]`. A real Battle Chess PAL pair (100 vs 120 frames) reports CPU first difference at byte 4 and Memory at 24,603. CORE-15 is done; 1,437 tests pass |
| 2026-11-01 | VID-14 | Screenshot gallery | Added `ScreenshotLibrary` metadata parsing/sorting/delete, `AppSettings.ResolveScreenshotDirectory`, and **File > Screenshot Gallery...** with thumbnails, open/copy/delete/refresh. VID-14 is partial for filters/compare/export; 1,440 tests pass |
| 2026-11-01 | VID-09 | GIF recording | Added `GifRecorder` (GIF89a, 3-3-2 palette, looping, 2-50 fps accumulator), CLI `--record-gif/--record-fps/--record-frames` with JSON fields, and **File > Record GIF...** with live start/stop and frame-skip restoration. A 40-frame PAL capture produced a 20-frame 320x288 GIF that Pillow decoded fully; VID-09 is partial for MP4/WebM/audio/replay; 1,444 tests pass |
| 2026-11-01 | AUD-02 | Desktop WAV recording | Added `TeeAudioSink`, **File > Record Audio...** with start/stop and finalize-on-stop, WAV naming/duration reporting, and keeping the recorder attached across audio-device changes. AUD-02 is partial for mixer/spectrum/format options; 1,447 tests pass |
| 2026-11-01 | SYS-18, UX-20, FND-06 | Per-game profile editor | Added `GameProfileJson` with normalization, `AppSettings.CreateEffectiveForGame`, machine-creation application, Settings > Emulation save/delete/import/export, and CLI `run/trace --game-profile`. SYS-18/UX-20/FND-06 remain partial for BIOS/fix/filter/high-res fields and inherit/reset; 1,450 tests pass |
| 2026-11-01 | CORE-12 | Parser fuzzing | Added `ParserFuzzTests` with deterministic state mutations, random ISO/CHD headers, and CUE text mutations; all failures stay within the documented exception types. CORE-12 is partial for a nightly fuzz project and command/Opera-FS targets; 1,453 tests pass |
| 2026-11-01 | UX-06 | Movie frame editor | Extended `MovieEditor` with per-frame set/button/insert/remove and added **File > Movie Editor...** (frame list, player-1 button checkboxes, insert/delete/trim, Save As). UX-06 remains partial for Lua, an input timeline, and multiplayer display; 1,457 tests pass |
| 2026-11-01 | UX-13 | Game metadata editor | Added `LibraryMetadataStore.Upsert`/`Remove` and **File > Game Metadata...** for the current disc (title, developer, publisher, year, genre, regions, players, description), with a library rescan after saving. UX-13 remains partial for external providers and media; 1,458 tests pass |
| 2026-11-01 | UX-15 | Profile selector and import/export UI | Added `default-profile.txt` resolution, Settings > Paths profile selector, Set default, Create, Open folder, Import, and Export actions. UX-15 is done; installer integration belongs to UX-16; 1,459 tests pass |
| 2026-11-01 | SYS-20 | Disc integrity verifier | Added `DiscVerifier` and `disc verify` (native-sector reads, LBA errors, FNV-1a hash, JSON). Battle Chess verified 143,810 sectors / 338,241,120 bytes with no errors. SYS-20 is partial for Redump DAT matching and the UI badge; 1,473 tests pass |
| 2026-11-01 | CORE-13 | Extended-boot compatibility retry | Reran all 18 non-rendered discs: 5 former not-loadable rows and 12 black/blank titles now render. Powers Kingdom (Europe) reaches its live-action intro at 65,000 frames with the Anvil BIOS; WorldCup Super Stadium renders with the FZ-10J BIOS plus its Kanji font ROM. Only Crash 'n Burn (Japan) remains a known boot failure. Added `-Files`/`-RetryFrames` and `-Bios2` to the sweep; 1,473 tests pass |
| 2026-11-01 | VID-06 | Overscan crop | Added `AppSettings.OverscanPixels` (0-32), `VdlFrame.CopyCroppedFrom`, presentation/native-screenshot crop, and Settings > Video > **Overscan crop**. The crop is presentation-only and keeps frame hashes/input/states unchanged. VID-06 is partial for per-game presets, void patterns, and custom per-edge crops; 1,473 tests pass |
| 2026-11-01 | CORE-09 | 4DO/Opera fix parity audit | Added `docs/FixParity.md`: every 4DO `FIX_BIT_*` is mapped or explicitly retired. Timing-6 (Alone in the Dark) and GraphicsStepY (Samurai Shodown) are replaced by generic CEL models, with byte-identical forced-bit evidence. `GameCompatibilityTests` locks every ID; 1,473 tests pass |
| 2026-11-01 | SYS-20 | Redump DAT matching | Added `RedumpDat` (Redump/No-Intro XML parser with hash normalization) and `DiscHealth` (native-track CRC-32/MD5/SHA-1 matching). `disc verify --dat <path>` reports the matched game/ROM and exits 1 on a mismatch. Synthetic ISO/DAT evidence matched and a one-byte change failed; 1,473 tests pass |
| 2026-11-01 | SYS-18, UX-20 | Full per-game profile fields | Extended `GameProfile` with BIOS MD5, fix mask, filter, high-resolution, six input devices, and NVRAM mode/version. `CreateEffectiveForGame` deep-copies overrides before applying; the desktop uses effective BIOS/fix/input/filter/high-res/NVRAM and the CLI applies profile fix/high-res. SYS-18/UX-20 remain partial for cheats, inherit/reset, and live application; 1,473 tests pass |
| 2026-11-01 | UX-15 | Portable mode | Added `PortablePaths` and redirected settings, NVRAM, cheats, library state, and logs through `portable.flag`, `--portable`, or `FIVEDO_DATA_DIR`; desktop/CLI strip the flag and tests cover all three resolution paths. UX-15 is partial (named profiles and instance locking remain); 1,376 tests pass |
| 2026-11-01 | UX-23 | Input display | Added the bottom-left frame/skip and player-1 button overlay, F2 and View > Input Display toggles, `ShowInputDisplay`, and per-frame controller capture in `CoreStatus`. UX-23 is partial (TAS lag semantics and layout options remain); 1,376 tests pass |
| 2026-11-02 | Release | v1.0 scope finalized; legal sign-off recorded | Recorded project-owner legal/attribution sign-off under the FreeDO modified GNU LGPL as free, open-source donationware (non-commercial; voluntary donations only). M14 high-resolution CEL research moved to post-1.0 and removed from the v1.0 gate; M13.5/M13.6 visual/high-res review also post-1.0. The 594-disc loadability evidence (593 render, 1 known boot failure) and 12-title matrix are the v1.0 compatibility baseline. Remaining at that point: Linux x64 smoke in the Hyper-V `LinuxMint` VM (`D:\Hyper-V\VMs\LinuxMint`), version bump/tag, and artifact attachment. |
| 2026-11-02 | Release | v1.0.0 release | Bumped `FiveDoVersion.Version` and `Directory.Build.props` to `1.0.0`, moved the CHANGELOG `[Unreleased]` section to `[1.0.0]`, tagged `v1.0.0`, published win-x64/linux-x64 CLI and frontend archives, and created the GitHub release with SHA-256 checksums. Full Windows and Linux suites pass (1,473/1,473); Windows frontend smoke reports `FRONTEND SMOKE OK 1.0.0`; benchmark gate passes (1.39 ms/frame, 0 bytes/frame). Release: <https://github.com/purelogiccode/5DO/releases/tag/v1.0.0>. |
| 2026-11-02 | M13.7 | Linux x64 validation | Validated the self-contained `linux-x64` CLI and frontend on the Hyper-V `LinuxMint` VM (Linux Mint 22.3, x86_64, Mesa 25.2.8 llvmpipe) and ran the full test suite: **1,473/1,473 passed** with .NET SDK 10.0.301 plus the .NET 8 runtime required by the shader compiler. CLI `selftest` 4/4, `bench` 466 fps, `info`/`bios info` on Wolfenstein 3D, a 3,000-frame boot (PC `0x00028190`, frame hash `0x9A3B5131`, 2,205,093 samples, non-silent 50 s audio), and frontend `--smoke-test` passed. The GL probe timed out under the VM's SSH/llvmpipe session, so Linux GPU presentation is preview/best-effort. Adjusted the diagnostics bundle test to skip user-name assertions for two-character names (`vm`), matching the scrubber's documented minimum length. Superseded by the v1.0.0 release row. |
| 2026-11-02 | M10.7 | Unified single-file bundles | Replaced the per-component CLI/frontend archives with one bundle per system (`5do-win-x64.zip`, `5do-linux-x64.tar.gz`, `5do-osx-x64.tar.gz`). Each bundle contains framework-dependent single-file CLI and desktop apphosts (`PublishSingleFile=true`, `PublishTrimmed=false`), the OpenAL/Skia/ANGLE native libraries, README/license/notices/whatsnew/release notes, and `PACKAGE.txt`. The .NET 10 runtime is required on the host and is not bundled. PDB and XML documentation files are removed. Added `packaging/package-bundle.ps1` and `packaging/make-tar.py`, a reusable `build-bundle.yml` workflow, a `release.yml` workflow that publishes the bundles and removes obsolete per-component assets, and wiki publishing in `docs.yml`. Fixed the CI `--locked-mode` restore by regenerating RID-agnostic `packages.lock.json` files. Local bundles are written to `src/FiveDo.Frontend.Avalonia/bin/Release`. |
| 2026-11-02 | M10.7 | System .NET runtime only | Removed the private `runtime/` folder from the release bundles. The apphosts now resolve the .NET 10 runtime from the host installation, and the bundles contain no runtime binaries. Removed all PDB and XML documentation files from the archives; only runtime dependencies and docs remain. Updated README/FAQ/Installation/ReleaseChecklist/ReleaseLegalReview/wiki and the CI bundle checks to enforce the no-runtime and no-XML rules. |
| 2026-11-02 | M10.8 | v1.1.0 maintenance update | Bumped the version to `1.1.0`. Moved `docs/licenses/OpenAL-Soft-COPYING.txt` to `docs/OpenAL-Soft-COPYING.txt` and removed the nested folder; the bundle script copies the file into the bundle's `docs/` root. Fixed duplicate game-library rows by deduplicating scanner results by 3DO game ID (preferring readable copies). Removed the frame/PC/cycle counter from the desktop status bar. Added Serilog calls to the remaining CLI/frontend catch blocks. Verified the existing Serilog warning+ bug-report sink, environment/exception formatting, and ApplicationStats launch calls in both hosts. |
| 2026-11-03 | M9R | Display refresh rate | Added `DisplayRefreshRate` (Settings > Video, default 60 Hz, `0` = match display). Above 60 Hz the desktop re-presents the latest mailbox frame on a `DispatcherTimer` (or on the compositor's animation frames for match-display) so the swap chain runs at the requested rate; the CPU fallback retimes its render timer. The 3DO still produces 60 fields per second (50 for PAL), so the setting lowers presentation latency but does not add motion. The status bar and overlay report the measured display rate when the setting is not 60. Added settings validation, load clamping, a schema version bump to 3, and tests. |
| 2026-11-03 | Release | v1.2.0 feature release | Bumped the version to `1.2.0` and built the Windows/Linux/macOS x64 bundles. The release includes the optional Windows Direct3D 11 presenter, startup GitHub release checks, the six-target fuzz harness, per-edge overscan/void border, screenshot gallery filters/compare/export/sidecars, FIQ request-to-vector telemetry, DSP per-tick statistics, CLIO/DIPIR/FIFO/MCTL/XBUS audit fixes, flat 2048-byte CHD and raw `.iso` support, and display-refresh control. WebAssembly/browser, Android/iOS, and Raspberry Pi were removed from scope. Full suite **1,597 tests**; release workflow <https://github.com/purelogiccode/5DO/actions/runs/41581528432> passed all bundles and published <https://github.com/purelogiccode/5DO/releases/tag/v1.2.0>; all `SHA256SUMS.txt` entries verify. |
| 2026-11-03 | Release | v1.2.1 arm64 bundles | Added `win-arm64`, `linux-arm64`, and `osx-arm64` to the packaging script and CI matrix, producing six versioned bundles. CI verifies PE ARM64, ELF aarch64, and Mach-O arm64 apphosts; x64 bundles keep runtime smoke tests. No emulator behavior changed. Release workflow <https://github.com/purelogiccode/5DO/actions/runs/41583317988> passed and published <https://github.com/purelogiccode/5DO/releases/tag/v1.2.1>; all six `SHA256SUMS.txt` entries verify. |
| 2026-11-04 | SYS-20 | Library disc-health badge | Added `DiscHealthCache`/`DiscHealthCacheStore` (`library-health.json`, self-invalidating by size/timestamp), `LibraryHealthVerifier` (full read plus optional Redump/No-Intro DAT match), `DumpStatus`/`DumpBadge`/`DumpTip` on library entries, badges in the grid and list views, **File > Verify Library Discs...** with progress and DAT selection, a Settings > Paths **Disc DAT** field, and the same **Verify Health**/**DAT...** actions in the Game Library window. `DiscHealthCacheTests` covers cache roundtrip/staleness/badges/summaries plus verifier matches, mismatches, missing files, unreadable images, and progress. SYS-20 remains partial for 4DO/Opera hash comparison; 1,619 tests pass and the frontend smoke test reports `FRONTEND SMOKE OK 1.2.1`. |

---

<a id="part-ii"></a>
# Part II — Feature Specification and Backlog

> Complete feature specifications: problem, required behavior, files, tests, and acceptance criteria. Use the master checklist above to choose work; use this part to implement it.

**Status:** detailed build specification, 2026-10-31  
**Purpose:** tell a developer exactly what to add to 5DO, where to add it, how to test it, and when it is done.  
**Companion:** [Part III — Best-in-the-World Strategy and Gap Analysis](#part-iii-best-in-the-world-strategy-and-gap-analysis) contains the comparison, scorecard, and phased roadmap. This document is the implementation-level breakdown.

---

## 0. How to use this document

### 0.1 Structure

1. **Feature index** - every feature at a glance, grouped by subsystem.
2. **Foundation features** - cross-cutting infrastructure that must land before most features.
3. **Detailed specifications** - one section per feature with problem, current behavior, required behavior, files, tests, and acceptance criteria.
4. **File/project work map** - which project and files each feature touches.
5. **Implementation order** - dependency-safe sequence.
6. **Test plan** - unit, integration, differential, manual.
7. **Release checklist** - what "done" means for the whole project.
8. **Reference map** - exact Opera/4DO/5DO source locations.

### 0.2 Conventions

| Field | Meaning |
|---|---|
| **ID** | Stable feature identifier. Use it in commits, issues, and tests. |
| **Priority** | P0 = correctness/compatibility; P1 = expected best-in-class; P2 = ecosystem multiplier; P3 = long-term. |
| **Effort** | S = under 3 developer-days; M = 3-10; L = 10-30; XL = 30-90+. |
| **Status** | `Pending`, `Partial`, `In progress`, `Done`, `Blocked`. |
| **Depends on** | Feature or foundation that must land first. |
| **Files** | Primary files/projects to change. |
| **Done when** | Objective acceptance criteria. |

### 0.3 Rules

1. **One feature per pull request.** Do not mix accuracy changes with UI work.
2. **Tests first.** Every behavior change needs a unit, golden, differential, or manual test.
3. **One reference per change.** State whether the behavior comes from Opera, 4DO/FreeDO, or hardware documentation.
4. **No native emulator dependency.** The core stays pure managed C#.
5. **No regressions.** Existing golden hashes, save states, CLI JSON, and zero-allocation contracts must keep passing.
6. **Feature flags for risky work.** New timing models ship behind a profile/flag until differential evidence is green.
7. **Document intentional divergence.** Add it to `docs/PortingNotes.md` and `docs/Compatibility.md`.
8. **No libretro core.** 5DO is a standalone emulator; Opera's libretro core is a behavioral reference only, not a product target.

### 0.4 Definition of done template

A feature is done only when:

- [ ] The required behavior is implemented.
- [ ] Unit tests cover normal, boundary, and error cases.
- [ ] At least one integration test exercises it through `FiveDoMachine`.
- [ ] Save/load roundtrip is tested if it has state.
- [ ] CLI/JSON and UI changes are documented.
- [ ] No steady-state allocations were added to the hot path.
- [ ] Golden/differential tests pass.
- [ ] `docs/` is updated.
- [ ] The feature is listed as done in the Master Dashboard checklist.

---

## 1. Feature index

### 1.1 Core accuracy

| ID | Feature | Priority | Effort | Depends on | Status |
|---|---|---|---|---|---|
| CORE-01 | ARM60 cycle-class accuracy mode | P0 | M | FND-01 | Partial |
| CORE-02 | CLIO timer scheduler parity | P0 | M | FND-01 | Partial |
| CORE-03 | MADAM CEL busy timing | P0 | L | FND-01 | Partial |
| CORE-04 | CD-ROM timing and command parity | P0 | XL | FND-01, CORE-11 | Partial |
| CORE-05 | DSP timing and optional threading | P0 | M | FND-01 | Pending |
| CORE-06 | SPORT/XBUS/DMA timing audit | P0 | M | CORE-11 | Pending |
| CORE-07 | Interrupt latency / FIQ/IRQ audit | P0 | M | CORE-11 | Pending |
| CORE-08 | Accuracy/performance profiles | P0 | S | FND-01 | Partial |
| CORE-09 | 4DO/Opera fix parity audit | P0 | M | SYS-17 | Done |
| CORE-10 | Hardware test ROM suite | P0 | L | none | Pending |
| CORE-11 | Full-collection differential harness | P0 | L | FND-05 | Partial |
| CORE-12 | Fuzzing and parser security | P0 | M | FND-05 | Done |
| CORE-13 | Remaining compatibility failures | P0 | L | CORE-11 | Partial |
| CORE-14 | Save-state format v3 | P0 | M | FND-03 | Done |
| CORE-15 | State diff and verification tooling | P1 | M | CORE-14 | Done |
| CORE-16 | Frame/audio hash regression suite | P0 | M | CORE-11 | Partial |

### 1.2 System, disc, BIOS, region, peripherals

| ID | Feature | Priority | Effort | Depends on | Status |
|---|---|---|---|---|---|
| SYS-01 | CD-DA / Red Book and multi-track discs | P0 | L | CORE-04 | Partial |
| SYS-02 | Disc swap / ODE / multi-disc | P0 | M | CORE-04 | Partial |
| SYS-03 | PAL/NTSC region modes | P0 | L | CORE-02 | Partial |
| SYS-04 | BIOS manager and auto-selection | P0 | L | FND-02 | Done |
| SYS-05 | Embedded open-source boot ROM | P1 | M | SYS-04, ENG-03 | Pending |
| SYS-06 | RSA/unsigned-disc option | P1 | M | SYS-04, ENG-03 | Pending |
| SYS-07 | Memory capacity options | P1 | M | FND-01 | Pending |
| SYS-08 | CPU overclock/underclock | P1 | S | FND-01 | Partial |
| SYS-09 | CD speed control | P1 | M | CORE-04 | Done |
| SYS-10 | Random seed control | P1 | S | FND-01 | Done |
| SYS-11 | MADAM matrix hardware/software | P1 | S | FND-01 | Pending |
| SYS-12 | SWI HLE for Portfolio OS | P2 | L | CORE-01 | Pending |
| SYS-13 | VDLP bypass CLUT | P1 | S | FND-01 | Done |
| SYS-14 | Per-game NVRAM and versions | P1 | M | FND-02 | Done |
| SYS-15 | Mouse/lightgun/arcade lightgun/flightstick/trackball | P0 | L | FND-01 | Partial |
| SYS-16 | Six-player/multi-tap verification | P1 | S | SYS-15 | Pending |
| SYS-17 | Game database expansion and community DB | P1 | M | FND-02 | Pending |
| SYS-18 | Auto per-game profile | P0 | L | SYS-04, SYS-17 | Pending |
| SYS-19 | Save-state interoperability with 4DO/Opera | P2 | L | CORE-14 | Pending |
| SYS-20 | Disc health / Redump DAT / CHD integrity | P2 | M | SYS-22 | Partial |
| SYS-21 | Physical CD drive support | P2 | M | none | Pending |
| SYS-22 | Disc image conversion/repair | P2 | M | SYS-01 | Pending |

### 1.3 Video

| ID | Feature | Priority | Effort | Depends on | Status |
|---|---|---|---|---|---|
| VID-01 | True 640x480 / 480i internal rendering | P1 | XL | CORE-03 | Pending |
| VID-02 | 2x/4x supersampling | P2 | L | VID-01 | Pending |
| VID-03 | GPU CEL compositing | P3 | XL | VID-01 | Pending |
| VID-04 | 480i/240p detection, deinterlacing, field blending | P1 | M | SYS-03 | Pending |
| VID-05 | PAL output timing | P1 | M | SYS-03 | Done |
| VID-06 | Auto-crop, overscan, void area, gray border | P1 | M | FND-06 | Done |
| VID-07 | Shader preset system | P1 | L | FND-06 | Pending |
| VID-08 | NTSC composite/S-Video/RF, HDR, gamma | P2 | L | VID-07 | Pending |
| VID-09 | Video recording | P1 | M | FND-06 | Partial |
| VID-10 | Overlays, bezels, touch controls | P2 | M | FND-06 | Pending |
| VID-11 | Vulkan surfaces on Linux/macOS | P1 | L | ECO-04 | Pending |
| VID-12 | Run-ahead, frame delay, VRR, audio sync | P2 | L | CORE-14 | Pending |
| VID-13 | Video test suite and color accuracy | P1 | M | CORE-11 | Pending |
| VID-14 | Screenshot gallery and metadata | P2 | S | FND-06 | Done |
| VID-15 | Direct3D 11 renderer (Windows) | P2 | L | M9R.1, FND-06 | Done |

### 1.4 Audio

| ID | Feature | Priority | Effort | Depends on | Status |
|---|---|---|---|---|---|
| AUD-01 | Audio buffer/latency/device/resampler | P1 | M | FND-06 | Partial |
| AUD-02 | Audio recording UI, mixer, spectrum | P1 | M | FND-06 | Partial |
| AUD-03 | DSP debugger | P2 | M | UX-21 | Pending |
| AUD-04 | Audio golden tests vs Opera/hardware | P1 | M | CORE-11 | Pending |
| AUD-05 | CD-DA mixing and volume | P1 | S | SYS-01 | Done |

### 1.5 UX and quality-of-life

| ID | Feature | Priority | Effort | Depends on | Status |
|---|---|---|---|---|---|
| UX-01 | Rewind | P1 | L | CORE-14 | Done |
| UX-02 | Fast-forward / turbo | P1 | S | FND-01 | Done |
| UX-03 | Netplay | P1 | XL | CORE-11 | Pending |
| UX-04 | Cheat engine | P1 | M | FND-06 | Done |
| UX-05 | Achievements | P2 | L | UX-03 | Pending |
| UX-06 | TAS tools | P1 | L | UX-01 | Pending |
| UX-07 | Controller profiles and per-game input | P1 | M | FND-06 | Partial |
| UX-08 | Gamepad navigation and big picture | P1 | L | FND-06 | Pending |
| UX-09 | Localization | P1 | M | FND-07 | Pending |
| UX-10 | Themes and accessibility | P2 | M | FND-06 | Pending |
| UX-11 | Disc browser UI and game info | P1 | M | FND-06 | Done 2026-11-01 |
| UX-12 | BIOS/NVRAM manager UI | P1 | M | SYS-04, SYS-14 | Done 2026-11-01 |
| UX-13 | Library metadata, favorites, playtime | P1 | L | SYS-17 | Partial |
| UX-14 | Auto-update and release channels | P1 | M | ENG-10 | Pending |
| UX-15 | Portable mode, multi-instance, config profiles | P1 | S | FND-02 | Partial |
| UX-16 | Installers, signing, notarization, file associations | P1 | L | ENG-10 | Pending |
| UX-17 | Crash report, compatibility report, log bundle | P1 | M | FND-08 | Partial |
| UX-18 | Onboarding and verification | P1 | M | SYS-20 | Pending |
| UX-19 | OSD, notifications, quick menu | P1 | S | FND-06 | Done |
| UX-20 | Per-game settings UI | P1 | M | SYS-18 | Partial |
| UX-21 | Debugger UI | P2 | L | CORE-15 | Pending |
| UX-22 | Trace viewer and performance profiler | P2 | M | UX-21 | Pending |
| UX-23 | Input display and lag counter | P1 | S | UX-06 | Done |
| UX-24 | Lua scripting and automation | P3 | L | UX-06 | Pending |

### 1.6 Ecosystem and platforms

| ID | Feature | Priority | Effort | Depends on | Status |
|---|---|---|---|---|---|
| ECO-02 | WebAssembly/browser build | P3 | XL | none | Out of scope (2026-11-02) |
| ECO-03 | Android/iOS frontend | P3 | XL | ECO-04 | Out of scope (2026-11-02) |
| ECO-04 | Linux/macOS validation, Flatpak/AppImage, macOS gamepad/Metal | P1 | L | ENG-01 | Pending |
| ECO-05 | Raspberry Pi/arm64/GLES optimization | P1 | L | ENG-02 | Out of scope (2026-11-02) |
| ECO-06 | Plugin API | P3 | L | FND-01 | Pending |
| ECO-07 | Headless server / HTTP / WebSocket API | P3 | M | FND-04 | Pending |
| ECO-08 | 3DO modem/network emulation | P3 | L | CORE-04 | Pending |
| ECO-09 | 3DO CD player / Video CD | P3 | M | SYS-01 | Pending |
| ECO-10 | Documentation wiki and compatibility portal | P1 | M | SYS-17 | Pending |
| ECO-11 | Community workflow | P1 | S | ENG-10 | Pending |

### 1.7 Engineering, legal, and trust

| ID | Feature | Priority | Effort | Depends on | Status |
|---|---|---|---|---|---|
| ENG-01 | CI matrix, nightly sweep, coverage, static analysis, SBOM, reproducible builds | P1 | M | FND-05 | Pending |
| ENG-02 | Performance: interpreter hot path, M12 JIT, GPU CEL, async shaders | P1 | XL | CORE-03 | Pending |
| ENG-03 | Legal/licensing/attribution | P1 | M | none | Done (2026-11-02) |
| ENG-04 | Accessibility | P2 | M | FND-06 | Pending |
| ENG-05 | Telemetry transparency and privacy controls | P2 | S | FND-08 | Pending |
| ENG-06 | Stability soak / crash-free | P1 | M | CORE-11 | Pending |
| ENG-07 | Documentation refresh | P1 | S | none | Pending |
| ENG-08 | Localization infrastructure | P1 | S | FND-07 | Pending |
| ENG-09 | Code coverage and benchmark regression | P1 | M | ENG-01 | Pending |
| ENG-10 | Release engineering and signing | P1 | M | ENG-03 | Pending |

### 1.8 Carried-forward backlog

| ID | Item | Priority | Related new IDs | Status |
|---|---|---|---|---|
| BAK-01 | Finish a full Road Rash 5.5-mile race | P1 | UX-06, CORE-04 | Pending |
| BAK-02 | Script Alone in the Dark inventory/exploration | P1 | UX-06, CORE-09 | Pending |
| BAK-03 | Input/gameplay scripts for seven titles | P1 | UX-06, CORE-11 | Pending |
| BAK-04 | CD/CLIO/MADAM/ARM timing triage with Opera | P0 | CORE-01..04 | Pending |
| BAK-05 | arm64/DPI/Linux/macOS validation | P1 | ECO-04 | Pending |
| BAK-06 | Tier B 1080p60, 3D CEL profiling, hot-path optimization, relative gate | P1 | ENG-02, ENG-09 | Pending |
| BAK-07 | MSIX/dmg/AppImage installers, signing/notarization, file associations | P1 | UX-16 | Pending |
| BAK-08 | Frontend NativeAOT audio validation | P1 | ENG-10 | Pending |
| BAK-09 | ViewModel tests and UI polish | P2 | ENG-09 | Pending |
| BAK-10 | Optional M12 managed ARM JIT | P2 | ENG-02 | Pending |
| BAK-11 | M14 high-resolution CEL research | P1 | VID-01..03 | Post-1.0 |
| BAK-12 | Legal/attribution sign-off; green CI | P1 | ENG-03, ENG-01 | Done (2026-11-02) |
| BAK-13 | BIOS-aware sweep and 5DO status merge | P1 | CORE-13, SYS-17/18 | Pending |
| BAK-14 | 4DO `.4dosav` import/export | P2 | SYS-19 | Pending |
| BAK-15 | PAL, CD-DA, netplay, rewind, debugger UI, peripherals | P1 | SYS-01/03/15, UX-01/03/21 | Pending |
| BAK-16 | Windows/macOS file associations | P1 | UX-16 | Pending |
| BAK-17 | Portable mode | P1 | UX-15 | Pending |
| BAK-18 | macOS gamepad backend | P1 | ECO-04 | Pending |
| BAK-19 | Vulkan X11/Wayland/Metal surfaces | P1 | VID-11 | Pending |

### 1.9 Moonshots

| ID | Idea | Priority | Why |
|---|---|---|---|
| MOON-01 | One-click BIOS-less universal boot | P2 | Removes the BIOS barrier for homebrew/demos. |
| MOON-02 | Zero-config compatibility | P1 | Auto BIOS/region/fix/input profiles. |
| MOON-03 | Public accuracy dashboard | P1 | Trust through transparent differential results. |
| MOON-04 | Console-like big picture | P2 | 10-foot UI. |
| MOON-05 | Creator suite | P2 | TAS + debugger + Lua + trace viewer. |
| MOON-06 | Desktop everywhere | P2 | Windows, Linux, and macOS desktop; browser/mobile/Pi are out of scope. |
| MOON-07 | Preservation | P1 | Redump, CHD, state import/export, movies. |
| MOON-08 | Video quality | P2 | True high-res, shaders, HDR, NTSC, 4K60. |
| MOON-09 | Performance leader | P1 | 4K60 desktop, 1080p60 Tier B desktop. |
| MOON-10 | Community | P1 | Compatibility DB, plugins, docs, localization. |

---

## 2. Foundation features

These are not user-visible features, but most other work depends on them. Build them first.

<a id="feat-fnd-01"></a>
### FND-01 - Feature flags, tuning, and profiles

**Priority:** P0 | **Effort:** M | **Depends on:** none

**Problem.** `EmulatorTuning` is a small immutable record with only ARM clock, fix mode, CPU backend, and high-resolution. New options (timing mode, region, memory size, CD speed, input devices, etc.) need a coherent model and a migration path.

**Add.**
- `EmulationProfile` enum: `Compatible`, `Accuracy`, `Performance`, `Custom`.
- `EmulatorOptions` (or extend `EmulatorTuning`) with every machine-level option:
  - ARM timing mode.
  - Region/mode.
  - DRAM/VRAM capacity.
  - CPU clock multiplier.
  - CD speed.
  - Random seed.
  - MADAM matrix mode.
  - SWI HLE.
  - VDLP bypass CLUT.
  - High-resolution mode.
  - Input device map.
  - Per-game fix mode.
- Validation for every option; clear error messages.
- A `ToStateOptions()` snapshot so a save state records the options it was created with.
- CLI/settings mapping.

**Files.** `src/FiveDo.Core/Models/EmulatorTuning.cs`, new `EmulatorOptions.cs`/`EmulationProfile.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `src/FiveDo.Host/Models/AppSettings.cs`, `src/FiveDo.Cli/Program.cs`.

**Tests.** Defaults, validation, profile mapping, state snapshot, settings roundtrip.

**Done when.** Every new machine option has one source of truth, is validated, is persisted, and is recorded in save states.

---

<a id="feat-fnd-02"></a>
### FND-02 - Settings schema versioning and migration

**Priority:** P0 | **Effort:** S | **Depends on:** FND-01 | **Status:** Done (2026-11-01)

**Problem.** `settings.json` had no version field. New options were added ad hoc; old files were accepted by `System.Text.Json` but there was no migration policy.

**Implemented.**
- `AppSettings.SettingsVersion` with `AppSettings.CurrentVersion = 2`; every load stamps the current version and every save writes it.
- Legacy migration: unversioned files are normalized (renderer preference, CPU clock, enums, NVRAM version, OSD ranges, profile detection).
- Malformed files are copied to `settings.json.invalid-<timestamp>.bak` before defaults are returned, so user data is not silently lost.
- Unknown properties are ignored by the source-generated serializer; writes remain atomic through the temporary file and rename.
- `GameProfiles` store per-game timing and CD-DA snapshots keyed by 4DO game ID; `GetGameProfile`, `SetGameProfile`, and `ApplyGameProfile` resolve keys case-insensitively.
- CLI `settings validate [path]` reports existence, JSON validity, and schema version; `settings migrate [path]` normalizes and rewrites the file without discarding a malformed original.
- Future schema changes append validation and a migration step; older files receive documented defaults for properties they do not contain.

**Files.** `src/FiveDo.Host/Models/AppSettings.cs`, `Models/GameProfile.cs`, `AppSettingsStore.cs`, `HostJsonContext.cs`, `docs/Configuration.md`.

**Tests.** v1 load, migration, unknown property, malformed file, atomic write, per-game profile roundtrip, CLI validate/migrate.

**Done when.** A settings file from any previous release loads with documented defaults and no data loss. **Met.**

---

<a id="feat-fnd-03"></a>
### FND-03 - Save-state format v3

**Priority:** P0 | **Effort:** M | **Depends on:** FND-01 | **Status:** Done (2026-11-01)

**Problem.** State format v2 is compressed but has no feature flags or option snapshot. New options (region, memory size, input devices, CD-DA position, rewind) need forward-compatible state.

**Implemented.**
- `StateFormat.Version` is 3. The header uses the old reserved word for `HeaderFlagFeatureFlags`/`HeaderFlagSectionTable`, followed by a feature word and an up-front section table (`id`, `version`, `flags`).
- Feature flags record the options snapshot, section table, compression, and video region. Unknown feature bits and header flags produce warnings rather than rejecting a newer state.
- `FiveDoMachine.UnknownSectionPolicy` selects `Warn` (skip and warn) or `Reject` (clear `InvalidStateException`).
- The section table is validated for duplicates, count bounds, unsupported entry flags, membership, and declared-vs-actual versions.
- v1 and v2 states are still accepted; their reserved header word is zero and the table/feature fields are absent.
- The options section records the live video region and the section version is 6.
- Rewind continues to use its own keyframe buffer rather than delta state sections.

**Files.** `src/FiveDo.Core/StateFormat.cs`, `StateUnknownSectionPolicy.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `src/FiveDo.Host/StateInspector.cs`, `docs/SaveStates.md`.

**Tests.** `SaveStateTests` covers v2/v1 downgrades, the v3 header/table, and warn-vs-reject unknown sections; `StateInspectorTests` covers v1/v2 inspection. **Done when.** v3 states are self-describing, forward-compatible, and every new subsystem can add a section without breaking old states.

---

<a id="feat-fnd-04"></a>
### FND-04 - CLI option and JSON contract conventions

**Priority:** P0 | **Effort:** S | **Depends on:** FND-01

**Problem.** New features will add many CLI options and JSON fields. Without conventions, the CLI will become inconsistent.

**Add.**
- Naming rules (`--region`, `--cpu-clock`, `--memory`, `--cd-speed`, `--input-device`, `--arm-timing`).
- Boolean/flag rules; `--no-*` forms.
- JSON source-generated records; camelCase except existing `state` PascalCase.
- Exit codes and error shapes.
- Help text and docs generated from one table if possible.
- Backward compatibility policy.

**Files.** `src/FiveDo.Cli/Program.cs`, `CliJsonModels.cs`, `CliJsonContext.cs`, `docs/CliReference.md`.

**Tests.** Parsing, unknown options, JSON contracts, exit codes.

**Done when.** Every new option follows the same conventions and is documented.

---

<a id="feat-fnd-05"></a>
### FND-05 - Differential harness and golden corpus

**Priority:** P0 | **Effort:** L | **Depends on:** none | **Status:** Partial

**Implemented (2026-10-31).** `tools/FiveDo.DiffHarness` runs 5DO and the bundled
OperaCore reference library with the same BIOS, disc data, frame count, input
script, and machine options. The CLI adds `--checkpoints`, `--audio-hash`, and
`--state-hash`; the harness stages the reference DLL and BIOS, extracts/caches a
data ISO from CHD, compares normalized BGRA8888 frame hashes, applies manifest
allowances, and writes JSON/Markdown/HTML reports. `benchmarks/diff/smoke.json`
matches Lemmings (USA) at frame 300 (`0x110E7DC5` on both engines);
`benchmarks/diff/showcase.json` covers five titles. An opt-in
`.github/workflows/diff-harness.yml` targets a self-hosted Windows runner with the
local corpus. Remaining: PC/state/CD-transcript fields on the reference side, the
full 594-disc manifest, and the nightly trend dashboard. See
[`docs/DifferentialHarness.md`](DifferentialHarness.md).

**Problem.** Accuracy work is manual. `docs/ReferenceStrategy.md` still lists the differential harness as pending.

**Add.**
- Headless runner that launches 5DO and Opera with the same BIOS, disc, input script, frame count, and options.
- Compare PC, frame hash, audio hash, state hash, CD transcript, and timing.
- Per-title checkpoint manifest.
- JSON report and HTML dashboard.
- Nightly CI job.
- Known-divergence allowlist with reasons.

**Files.** New `tools/FiveDo.DiffHarness/` or PowerShell + CLI, `benchmarks/`, `.github/workflows/`, `docs/ReferenceStrategy.md`.

**Tests.** Harness self-test with a synthetic ROM; a known-good title; a known-divergence title.

**Done when.** One command runs the full collection and produces a pass/fail report with no unexplained divergence.

---

<a id="feat-fnd-06"></a>
### FND-06 - UI settings and per-game profile framework

**Priority:** P0 | **Effort:** M | **Depends on:** FND-01/02 | **Status:** Partial (2026-11-01)

**Problem.** The Settings dialog has Video/Audio/Emulation/Paths tabs. New features need many more controls and per-game overrides.

**Add.**
- `SettingsPage`/`SettingsSection` view models.
- Validation and "apply/restart required" metadata.
- Per-game profile editor.
- Search/filter for settings.
- Reset section/defaults.
- Localized labels.

**Implemented.** The four tabs cover video/audio/emulation/paths, validation errors are reported without closing the dialog, and the Emulation tab now includes per-game region, input-device, and timing/CD-DA profile overrides with JSON import/export. Remaining: search/filter, reset-section/defaults, a `SettingsPage` view-model split, and localized labels.

**Files.** `src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml`, `SettingsWindow.axaml.cs`, new view models, `AppSettings.cs`.

**Tests.** View-model roundtrips, validation, per-game override resolution, reset.

**Done when.** Every machine option is editable, validated, and overridable per game without hand-editing JSON.

---

<a id="feat-fnd-07"></a>
### FND-07 - Localization resource pipeline

**Priority:** P1 | **Effort:** S | **Depends on:** none

**Problem.** All UI strings are hard-coded English. 4DO ships seven languages.

**Add.**
- Resource files (`.resx` or JSON) with a stable key scheme.
- `ILocalizer`/`Strings` service.
- Pseudo-locale for layout testing.
- Language selector and fallback.
- Contribution guide for translators.

**Files.** `src/FiveDo.Frontend.Avalonia/Resources/`, `App.axaml`, all views, `docs/`.

**Tests.** Key completeness, fallback, pseudo-locale, language switch.

**Done when.** The UI runs in at least English plus a pseudo-locale, and adding a language requires only a resource file.

---

<a id="feat-fnd-08"></a>
### FND-08 - Diagnostics, errors, and crash-bundle contracts

**Priority:** P1 | **Effort:** M | **Depends on:** none | **Status:** Partial (2026-11-01)

**Problem.** Diagnostics are strong but spread across Serilog, CLI flags, and ad-hoc dialogs. New features need consistent errors and support bundles.

**Implemented.**
- `DiagnosticsBundle` writes a zip with `manifest.json` (schema version, build/platform, selected evidence), `environment.txt`, sanitized `settings.json`, recent `logs/`, and optional `state.bin`, `frame.png`, and input replay.
- Settings sanitization removes path-valued keys and replaces the home, app, and temp paths plus the account name with placeholders. Text logs and notes are scrubbed the same way.
- `--out`, `--state`, `--frame`, `--movie`, `--settings`, `--logs`, and `--notes` control the CLI bundle; **Help > Save Diagnostics Bundle...** captures the live state and current frame from the desktop.
- Manifest and settings JSON are emitted with `Utf8JsonWriter` so the trimmed CLI keeps working with reflection-free JSON.

**Remaining.**
- Typed error codes and a user-facing message/technical-detail contract for BIOS/disc/state/audio/video/netplay failures.
- A compatibility-report form and upload flow.
- Input-replay capture from the live desktop recorder (the CLI accepts an existing replay path).

**Files.** `src/FiveDo.Diagnostics/`, `src/FiveDo.Cli/Program.cs`, `src/FiveDo.Frontend.Avalonia/`, `docs/Diagnostics.md`.

**Tests.** `DiagnosticsBundleTests` covers bundle contents, missing evidence, settings key removal, path/user scrubbing, and manifest fields.

**Done when.** A user can produce one file that lets a developer reproduce a bug.

---

## 3. Detailed feature specifications

## 3.1 Core accuracy

<a id="feat-core-01"></a>
### CORE-01 - ARM60 cycle-class accuracy mode

**Priority:** P0 | **Effort:** M | **Depends on:** FND-01 | **Status:** Partial

**Implemented (2026-10-31).** `ArmTimingMode` (`FreeDoCompatible` N=4,
`Arm60Datasheet` N=2), `EmulatorTuning.ArmTiming`, per-instance cycle selection,
`--arm-timing` on `run`/`trace`, the Settings combo, per-instruction cycle tests in
both modes, and an optional save-state `Options` section with a mismatch warning.
A Lemmings 3,000-frame smoke differential is recorded in
[`docs/TestCorpus.md`](TestCorpus.md) 1.1a (default PC/hash unchanged; ARM60 mode
diverges as expected). Remaining: run the full-collection differential and record the
divergence report before considering the accuracy mode a default candidate.

**Problem.** 5DO hard-codes FreeDO's `Ncycle = 4`. Opera uses `Ncycle = 2` with ARM60 datasheet/SDK references. N-cycles dominate memory accesses and block transfers; a 2x difference changes timing-sensitive behavior.

**Current.** `src/FiveDo.Cpu/Arm60.cs:24` defines `Ncycle = 4`; all instruction cycle calculations use the constant.

**Add.**
- `ArmTimingMode` enum: `FreeDoCompatible`, `Arm60Datasheet`.
- `EmulatorOptions.ArmTiming`.
- Replace the constant with a per-instance table/field.
- Cycle assertions for every instruction class in both modes.
- CLI `--arm-timing freedo|arm60`.
- Settings option and state snapshot.
- A differential report showing the effect on the 594-disc corpus.

**Files.** `src/FiveDo.Cpu/Arm60.cs`, `src/FiveDo.Core/Models/EmulatorTuning.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `src/FiveDo.Cli/Program.cs`, `src/FiveDo.Host/Models/AppSettings.cs`, `tests/FiveDo.Tests/Cpu/`.

**Tests.** Per-instruction cycle tests; LDM/STM timing; branch timing; full CPU conformance; differential run.

**Done when.** Both modes pass conformance; the default is unchanged; the accuracy profile is documented.

**Reference.** `References/opera-libretro-master/libopera/opera_arm.c:100-113`; `docs/ReferenceStrategy.md` §6.

---

<a id="feat-core-02"></a>
### CORE-02 - CLIO timer scheduler parity

**Priority:** P0 | **Effort:** M | **Depends on:** FND-01 | **Status:** Partial

**Implemented (2026-10-31).** `ClioTimerMode` (`FreeDoCompatible`, `OperaScheduler`),
per-slot `Quartz` scheduling with the `0x220` slack gap, `Clio.DoTimers(int)` with an
explicit cascade carry, `--clio-timers freedo|opera` on `run`/`trace`, the Settings
combo, slot-order/carry/state tests, and a save-state `Options` snapshot with a mismatch
warning. Quartz section version 3 and Clio section version 2 carry the new state; older
sections load with a reset scheduler. The default FreeDO path is unchanged, and the
3,000-frame Lemmings/Gex/Road Rash smoke differential is recorded in
[`docs/TestCorpus.md`](TestCorpus.md) 1.1b. Remaining: a timer test ROM and the
full-collection differential before the Opera scheduler can become a default.

**Problem.** 5DO's `Clio.DoTimers` services all 16 timers per timer event, following FreeDO. Opera schedules per-timer deadlines with slack and carry.

**Current.** `src/FiveDo.Clio/Clio.cs` `DoTimers()` loops timers 0-15 on every timer event; `Quartz.QueueTimer()` uses a single accumulator.

**Add.**
- `TimerState` with counter, reload, control, deadline, and cascade carry.
- Per-timer next-event scheduling; only due timers decrement.
- Preserve cascade/reload/IRQ semantics.
- State section version bump.
- Diagnostics: timer trace with deadline/counter/flag.

**Files.** `src/FiveDo.Clio/Clio.cs`, `src/FiveDo.Machine/Quartz.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `tests/FiveDo.Tests/Clio/`.

**Tests.** Each timer mode, cascade, reload, IRQ mask, boundary counts, save/load.

**Done when.** A timer test ROM matches Opera; golden titles unchanged.

**Reference.** `References/opera-libretro-master/libopera/opera_clock.c`, `opera_clio.c:887-960`.

---

<a id="feat-core-03"></a>
### CORE-03 - MADAM CEL busy timing

**Priority:** P0 | **Effort:** L | **Depends on:** FND-01 | **Status:** Partial

**Implemented (2026-10-31).** `CelTimingMode` (`FreeDoCompatible`, `OperaBusy`),
`EmulatorTuning.CelTiming`, per-instance CEL work counters and busy deadline, the
`--cel-timing` CLI option on `run`/`trace`, the Settings combo, a synthetic-ROM
status-polling integration test, a Madam section version 2 with the pending/busy fields,
and an Options section version 3 with the mode and a mismatch warning. The FreeDO default
is unchanged; a three-title 3,000-frame smoke differential is recorded in
[`docs/TestCorpus.md`](TestCorpus.md) 1.1c. Remaining: the full-collection
differential, a CEL-status polling title, and a frame-time regression check before the
Opera busy model can become a default.

**Problem.** 5DO renders CEL lists synchronously and discards the returned CEL cycles. The CPU never observes CEL busy time.

**Current.** `FiveDoMachine.RunFrame` calls `Madam.HandleCel()` and immediately sets `Fsm = Idle`; `CelEngine.HandleCel()` returns cycles that are ignored.

**Add.**
- `CelBusyUntil` (64-bit cycle deadline), `CelPixelsDrawn`, `CelTexelsWalked`.
- Work estimate from the CCB list and CEL type/size.
- Budget of ~9M pixels/second.
- Process CEL work in bounded chunks; keep `Fsm = InProcess` until the deadline.
- Status register returns busy while active.
- Feed CEL cycles into `Quartz`/frame accounting.
- Save-state fields.
- Trace fields.

**Files.** `src/FiveDo.Madam/Madam.cs`, `CelEngine.cs`, `CelRasterizer.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `src/FiveDo.Core/StateSectionIds.cs`.

**Tests.** CEL status polling; busy duration; pause/resume; save/load; frame-time regression.

**Done when.** CEL status behaves realistically; golden titles unchanged or improved; no >10% frame-time regression.

**Reference.** `References/opera-libretro-master/libopera/opera_madam.c:489-527`, `:3233`, `:3506`.

---

<a id="feat-core-04"></a>
### CORE-04 - CD-ROM timing and command parity

**Priority:** P0 | **Effort:** XL | **Depends on:** FND-01, CORE-11 | **Status:** Partial

**Implemented (2026-10-31).** `CdromTimingMode` (`FreeDoCompatible`, `OperaCycleBased`),
cycle-based 2048-byte data-sector scheduling at the stock 2x drive speed (81,246 ARM
cycles at 12.5 MHz after Opera's overhead compensation), delayed `POLDT`/`POLST`,
deferred read-data status, and the XBUS CD FIQ on delayed readiness. The mode is
exposed through `--cd-timing freedo|opera` and Settings > Emulation, reported in
`run --json`, covered by device, state, CLI, and machine tests, and recorded in the
CD-ROM section (version 2) plus the Options section (version 4). The default FreeDO
path is unchanged; a three-title 12,000/24,000-frame smoke differential is in
[`docs/TestCorpus.md`](TestCorpus.md) 1.1d. TOC from the disc source, CD-DA, and guest
eject/inject are implemented. Unsupported/unknown commands now publish the Opera
illegal-request status instead of silently doing nothing, the guest RESET command clears
transfer state and restores the 2048-byte sector size, and `CdromCommandTranscript`
captures the full command/status/poll exchange. `PrefetchingDiscSource` now read-aheads
native track sectors, so CD-DA playback consumes prefetched audio instead of
decompressing every sector on the sample clock. Remaining: raw-vs-Opera transcript
diffs, command latency, subcode, DIPIR tagging, and the full-collection differential.
SYS-09 adds the CD speed selector.

**Problem.** 5DO's CD device is a frame-based FreeDO port. Opera models cycle-based sector readiness, command latency, DIPIR, read-ahead, subcode, media access, CD-DA, and ODE.

**Current.** `src/FiveDo.Cdrom/CdromDevice.cs` executes commands immediately and reads sectors synchronously; the media-access pulse is a heuristic.

**Add.**
- `CdromTiming` with `NextReadyCycle`.
- Command latency table for seek/read/status.
- Cycle-based data-sector readiness.
- TOC from the disc source, not a hard-coded single track.
- Read-ahead and subcode.
- DIPIR/tagged status/chunks as proven by differential traces.
- Media-access behavior.
- Transcript tests.
- Keep the disc-end divergence documented.

**Files.** `src/FiveDo.Cdrom/CdromDevice.cs`, `src/FiveDo.Storage/`, `src/FiveDo.Xbus/Xbus.cs`, `tests/FiveDo.Tests/Cdrom/`.

**Tests.** Command transcript vs Opera; seek/read timing; TOC; FIQ/poll; save/load; full sweep.

**Done when.** Golden CD transcripts match Opera or have documented tests; no CD-related sweep regression; CD-DA/ODE can layer on the model.

**Reference.** `References/opera-libretro-master/libopera/opera_cdrom.c:789-1157`, `:170-184`, `:565`; `docs/ReferenceStrategy.md` §6.

---

<a id="feat-core-05"></a>
### CORE-05 - DSP timing and optional threading

**Priority:** P0 | **Effort:** M | **Depends on:** FND-01 | **Status:** Partial (2026-11-02)

**Implemented (2026-11-02).** `Dsp` now reports `LoopCalls`, `RunningLoops`,
`SleepingLoops`, `MaxInstructionsPerLoop`, and `AverageInstructionsPerLoop` alongside the
existing instruction counter. `run --json` exposes a `dspStats` object and the text
result prints a one-line summary. Tests cover running and sleeping ticks and the
per-tick peak/mean. The optional bit-identical `DspWorker` thread and its save-state
handshake remain open.

**Problem.** 5DO runs one DSP sample per scheduler event on the core thread. Opera offers an experimental threaded DSP. 5DO has no DSP timing/thread option.

**Current.** `src/FiveDo.Dsp/Dsp.cs` `Loop()` executes until the sample boundary; `FiveDoMachine.InternalFrame` calls it once per sample event.

**Add.**
- DSP instruction/sample timing metrics.
- Optional `DspWorker` with an SPSC command/audio ring.
- Deterministic mode remains default.
- Thread start/stop/state barriers.
- Audio thread remains the only OpenAL consumer.
- Save-state handshake.

**Files.** `src/FiveDo.Dsp/Dsp.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `src/FiveDo.Audio/`, `src/FiveDo.Host/CoreRunner.cs`.

**Tests.** Sample clock exactness; deterministic output with/without thread; start/stop; save/load; audio tap.

**Done when.** The threaded option is bit-identical and optional; no audio regression.

**Reference.** `References/opera-libretro-master/opera_lr_dsp.c`, `opera_lr_dsp_threaded.ic`.

---

<a id="feat-core-06"></a>
### CORE-06 - SPORT/XBUS/DMA timing audit

**Priority:** P0 | **Effort:** M | **Depends on:** CORE-11 | **Status:** Partial (2026-11-02)

**Implemented (2026-11-02).** The audit closed four concrete gaps and locked the
remaining reference behavior with tests:

- CLIO output-FIFO next-descriptor reload now requires the `0x304` channel request bit
  (`OP-11`), matching Opera's `dma_channel_enabled`.
- CLIO XBUS DMA checks `XbusAccessAborts` before clearing `0x304` under `OperaDipir`; a
  protected transfer stays enabled and makes no progress.
- MADAM persists the MCTL-written gate in section version 3, so cold reset re-enables
  the default gates and state load preserves the guest's disabled gates.
- XBUS device-15 Opera semantics expose `_pollDevice` in `GetPoll`, honor `POLREMASK`
  and `POLRE`, store only the interrupt-enable bits, use Opera's MEI/CD/OK status
  layout, and raise the poll FIQ from the combined poll register.
- XBUS state load rejects a state whose attached-device slot set does not match the
  live controller instead of misaligning the remaining sections.
- CLIO tracing adds `clio dma-blocked` and `clio dma-done` lines.

Tests cover FreeDO PBUS DMA lengths, DEST/PROGRESS, the untouched FreeDO control
slot, CPU-visible byte order, negative lengths, SPORT masks and high-resolution planes,
CLIO DMA lengths/DIPIR blocking, output FIFO gating, and the XBUS built-in responder
and state mismatch. Remaining: full Opera PBUS/SPORT trace diffs on DMA-heavy titles
and a decision on whether an Opera-compatible PBUS layout becomes an opt-in mode.

**Problem.** 5DO ports FreeDO's DMA behavior; Opera has additional DMA8/PBUS control-port semantics. DMA/PBUS bugs can corrupt VRAM or stall CD loads.

**Add.**
- Audit SPORT fill/copy, XBUS DMA, CLIO FIFO, MADAM PBUS.
- Add timing/ordering tests.
- Add trace fields.
- Compare against Opera on a DMA-heavy title.

**Files.** `src/FiveDo.Sport/Sport.cs`, `src/FiveDo.Xbus/Xbus.cs`, `src/FiveDo.Clio/Clio.cs`, `src/FiveDo.Madam/Madam.cs`.

**Tests.** DMA byte order, FIFO wrap, PBUS packet, SPORT copy, save/load.

**Done when.** DMA/PBUS traces match the reference on the golden titles; no allocator/CD regression.

---

<a id="feat-core-07"></a>
### CORE-07 - Interrupt latency / FIQ/IRQ audit

**Priority:** P0 | **Effort:** M | **Depends on:** CORE-11 | **Status:** Partial (2026-11-02)

**Implemented (2026-11-02).** The 3DO wires every device interrupt to CLIO's FIQ
controller; there is no separate IRQ line, and both FreeDO and Opera check only FIQ
after each instruction. 5DO now measures that path end to end:

- `Arm60.CyclesExecuted` counts ARM clock cycles; `Arm60.FiqEntered` reports the entry
  cycle and the value written to `LR_fiq`. Vector entry charges no extra cycles, matching
  both references.
- `Clio.CycleProvider`, `FiqRequested`, and active-reason properties report the cycle at
  which the active and enable masks first intersect. Repeated level-triggered raises
  while pending are coalesced; enable-mask writes can start a request.
- `FiqLatencyTracker` records requests, entries, matched/unmatched entries, coalesced
  requests, last/max/average latency, and the pending request cycle.
- `--fiq-trace` writes `kind=request` and `kind=entry` lines with cycle, PC/return
  address, active reasons, `latency`, and `matched`; `run --json` adds a `fiq` object.
- Tests cover masked/unmasked entry, instruction-cycle latency, request matching,
  level-triggered re-entry, return/bank restoration, CLIO transitions, timer requests,
  and a machine-level request-to-entry round trip. CLIO/DIPIR/XBUS ordering is covered by
  the `ClioFiqTelemetryTests` and existing DIPIR latch tests.

Observed on Gex 3,000 frames: 21,183 requests, 14,345 entries (13,855 matched, 490
unmatched), 7,328 coalesced, mean 7,430 cycles, max 859,234 cycles. Remaining: compare
the request/entry timeline against an Opera trace and classify the long masked windows
(they are candidates for CORE-04/CORE-13).

**Problem.** Timing-sensitive titles depend on FIQ entry latency and CLIO/DIPIR ordering. 5DO has no explicit latency tests.

**Add.**
- Measure cycles from interrupt request to vector entry.
- Test FIQ/IRQ masking, bank switching, return.
- Audit CLIO/DIPIR/XBUS interrupt ordering.
- Add a trace with reason masks and latency.
- Document invariants.

**Files.** `src/FiveDo.Cpu/Arm60.cs`, `src/FiveDo.Clio/Clio.cs`, `src/FiveDo.Xbus/Xbus.cs`, tests.

**Tests.** Masked/unmasked, nested, pending, return, latency bounds.

**Done when.** Latency is measured, bounded, and documented; differential traces agree.

---

<a id="feat-core-08"></a>
### CORE-08 - Accuracy/performance profiles

**Priority:** P0 | **Effort:** S | **Depends on:** FND-01 | **Status:** Partial

**Implemented (2026-10-31).** `EmulationProfile` (`Compatible`, `Accuracy`, `Performance`,
`Custom`), `EmulationProfiles.Apply/Detect/Matches`, CLI `--profile` on `run`/`trace`,
Settings > Emulation > Timing profile, `AppSettings.Profile` with automatic `Custom`
detection, and `run --json` `profile` are implemented. The accuracy profile enables
ARM60 N=2, the Opera CLIO scheduler, CEL busy timing, cycle-based CD reads, and the stock
2x drive. The performance profile uses a 1.5x CPU clock with FreeDO timing; it is
explicitly not an accuracy guarantee. Profiles never change per-game fixes, high-res, or
the random seed. The individual options remain the save-state source of truth. Remaining:
per-profile benchmark deltas and a full-collection accuracy-profile run.

**Problem.** Users need a safe default and an accuracy option. Risky timing changes must be opt-in until proven.

**Add.**
- `Compatible` (current behavior, safe).
- `Accuracy` (ARM datasheet timing, CEL busy, CD cycle timing, etc. as they land).
- `Performance` (optional overclock/threading, no accuracy guarantees).
- `Custom`.
- UI/CLI selection; docs; state warning.

**Files.** `EmulatorTuning.cs`, `AppSettings.cs`, `Program.cs`, docs.

**Tests.** Profile mapping, validation, state snapshot, docs.

**Done when.** Profiles are explicit, documented, and never change silently.

---

<a id="feat-core-09"></a>
### CORE-09 - 4DO/Opera fix parity audit

**Priority:** P0 | **Effort:** M | **Depends on:** SYS-17 | **Status:** Done (2026-11-01)

**Problem.** 5DO ports the 4DO fix bits; Opera has additional game-specific behavior and IDs.

**Implemented.**
- [`docs/FixParity.md`](FixParity.md) inventories every 4DO `FIX_BIT_*` value, the IDs that use it, and the 5DO mapping.
- `FIX_BIT_TIMING_1/2/3/5` map to the matching `FixMode` bits. `FIX_BIT_TIMING_6` (Alone in the Dark) and `FIX_BIT_GRAPHICS_STEP_Y` (Samurai Shodown) are explicitly retired because the generic CEL origin/literal-row models replaced them.
- Local evidence: DinoPark's fix changes the rendered title scene; Microcosm and Super Street Fighter II Turbo retain their mappings without a 3,000-frame regression; forcing the retired Alone/Samurai bits produces byte-identical frames.
- Opera has no per-game fix table; its generic accuracy models are the reference for the retired bits.
- `GameCompatibilityTests` covers every mapped ID, case-insensitive/full-MD5 lookups, unknown IDs, collisions, and explicit overrides.

**Files.** `src/FiveDo.Host/GameCompatibility.cs`, `docs/FixParity.md`, tests.

**Tests.** Per-fix frame hash, unknown game, collision handling.

---

<a id="feat-core-10"></a>
### CORE-10 - Hardware test ROM suite

**Priority:** P0 | **Effort:** L | **Depends on:** none | **Status:** Pending

**Problem.** Unit tests are strong but not a hardware oracle. There is no test ROM suite for CPU/MADAM/VDLP/DSP/CD/SPORT/CLIO.

**Add.**
- Curate or write test ROMs (CPU instructions, flags, modes; MADAM CEL; VDLP; DSP; CD; SPORT; CLIO).
- A runner that boots each ROM, captures output/frame hash, and compares against expected.
- CI integration (nightly or opt-in).
- Document how to add a ROM.

**Files.** New `tests/TestRoms/`, `tools/TestRomRunner/`, tests.

**Tests.** Each ROM passes; failure produces a useful diff.

**Done when.** The suite runs one command and gives a per-subsystem pass/fail report.

---

<a id="feat-core-11"></a>
### CORE-11 - Full-collection differential harness

**Priority:** P0 | **Effort:** L | **Depends on:** FND-05 | **Status:** Partial

**Implemented (2026-10-31).** The harness and checkpoint schema from FND-05 are
in place, with a passing Lemmings smoke run and a five-title showcase manifest.
The `manifest` command generates a portable 594-title manifest from the local
roots (`benchmarks/diff/full.json`, generated 2026-10-31). Remaining: run the full
collection, add per-title checkpoint manifests and allowances, and run the nightly
job. The stock reference ABI exposes video/audio/input only; an optional locally
built debug core adds PC/ARM-state/CD-FIFO fields (the harness auto-detects them),
while a full CD transcript still requires a reference-side export. See
[`docs/DifferentialHarness.md`](DifferentialHarness.md).

**Problem.** Accuracy evidence is limited to a handful of titles and manual runs.

**Add.**
- Run all 594 local discs with both 5DO and Opera.
- Same BIOS, disc, input script, frame count, options.
- Compare PC, frame hash, audio hash, state hash, CD transcript.
- Per-title checkpoint manifest.
- Dashboard and nightly CI.
- Known-divergence allowlist.

**Files.** `tools/FiveDo.DiffHarness/`, `benchmarks/`, `.github/workflows/`, `docs/`.

**Tests.** Harness self-test; one known-good; one known-divergence; full run.

**Done when.** The full collection runs nightly with a pass/fail report and trend history.

---

<a id="feat-core-12"></a>
### CORE-12 - Fuzzing and parser security

**Priority:** P0 | **Effort:** M | **Depends on:** FND-05 | **Status:** Done (2026-11-02)

**Problem.** Disc/state/CUE/CHD/Opera-FS parsers handle untrusted files. A malformed file must never crash or read out of bounds.

**Implemented.**
- `ParserFuzzTests` mutates a valid save state 80 times and requires only `InvalidStateException` from the loader; section length caps and CRC checks reject the rest.
- Random 8 KB ISO and CHD headers are opened 20 times each and must fail with `DiscImageException`, `InvalidDataException`, `IOException`, or another documented typed error.
- A valid CUE is text-mutated 80 times; any successful parse must expose a non-negative sector count.
- `tools/FiveDo.Fuzz` is a standalone deterministic harness with `state`, `disc`, `cue`, `opera-fs`, `cd-command`, and `cpu` targets. It records typed rejections separately from unexpected defects, writes the input/seed/iteration and stack trace to `artifacts/fuzz`, and exits nonzero on a defect.
- The CD target drives commands, polls, ticks, status/data reads, eject, inject, and reset in both FreeDO and Opera timing modes. The Opera FS target mutates a synthetic valid volume and walks/reads the parsed tree. The CPU target executes random ARM60 words in both timing modes and checks bounded instruction counts.
- `FuzzHarnessTests` runs a bounded six-target smoke sweep in the normal suite. `.github/workflows/fuzz.yml` runs a seeded nightly sweep and uploads the JSON report and reproductions.
- 2026-11-02 evidence: a 60-second-per-target run (seed 20261102) completed 6,705,667 iterations across state/disc/CUE/Opera-FS/CD with 0 unexpected failures and 1,758,669 typed rejections; a 60-second CPU-stream run completed 71,783 iterations with 0 failures. The reports are `artifacts/fuzz-nightly.json` and `artifacts/fuzz-cpu.json`.
- Deterministic seeds make every case reproducible with `--target`, `--seed`, and `--iterations`.

**Remaining.**
- A longer bounds/overflow campaign and DSP/MADAM fuzz targets are future extensions, not gates for this item.

**Files.** `tools/FiveDo.Fuzz/`, `tests/FiveDo.Tests/Storage/FuzzHarnessTests.cs`, `docs/Fuzzing.md`, `.github/workflows/fuzz.yml`, `src/FiveDo.Storage/`, `src/FiveDo.Cdrom/`, `src/FiveDo.Cpu/`.

**Tests.** `FuzzHarnessTests` covers all-target bounded execution, option parsing, and stable target order; the nightly workflow covers the long run.

**Done when.** Fuzzing runs clean for a defined period and every crash is fixed or documented. **Met:** the nightly workflow defines the period, and the 2026-11-02 runs were clean.

---

<a id="feat-core-13"></a>
### CORE-13 - Remaining compatibility failures

**Priority:** P0 | **Effort:** L | **Depends on:** CORE-11 | **Status:** Partial (2026-11-01)

**Problem.** Crash 'n Burn (Japan) is still stuck; WorldCup Super Stadium needed the
FZ-10J BIOS plus its Kanji font ROM and now renders. The 2026-10-30 baseline also had
13 black/blank titles and 5 timeouts at 3,000 frames.

**Implemented.**
- `tools/compat-sweep.ps1` gains `-Files` (explicit disc paths) and `-RetryFrames` (rerun a blank row at a deeper boot frame count, keeping the rendered result).
- The 2026-11-01 extended-boot retry reran all 18 non-rendered rows. All five former not-loadable rows and 12 of the 13 black/blank rows render: 20,000 frames with `panafz10` covers most; Powers Kingdom (Europe) needs 65,000 frames with `panafz10e-anvil`, and WorldCup Super Stadium needs the FZ-10J BIOS plus its Kanji font ROM (BIOS2).
- `BiosCatalog`/`BiosLibrary` now recognize 1 MB Kanji font images by their 3DO volume-header signature and classify them as secondary ROMs, so a font ROM can no longer be selected as a primary BIOS. The FZ-1J, FZ-1J (no RSA), FZ-10J, and FZ-1J Kanji font hashes are catalogued, and secondary selection prefers the matching FZ-1/FZ-10 family.
- `docs/GameCompatibilityResults.md` now records 593 rendered and 1 known failure, with the BIOS/BIOS2 and frame count in each retried row.
- `docs/Compatibility.md` 13.2 records the BIOS-aware triage, including the patched FZ-10 requirement for the Arcade CD-ROM titles, the Anvil requirement for Europe, and the regional Slayer split.

**Remaining.**
- `Crash 'n Burn (Japan)` stays black or on the Anvil boot screen at 65,000 frames with every local BIOS/font pairing; it needs a bootable Japanese FZ-1/FZ-10 BIOS or a better dump. Root-cause the boot path if a suitable image appears.
- Run the 12,000-frame in-game/visual review for the rendered titles.
- Add scripted gameplay evidence for the 18 retried titles.

**Files.** `tools/compat-sweep.ps1`, `docs/GameCompatibilityList.md`, `docs/GameCompatibilityResults.md`, `docs/Compatibility.md`, tests.

**Tests.** Each fixed title boots/renders/plays; no regression.

**Done when.** Every local disc has a final status with evidence; remaining failures are documented as hardware/BIOS/dump issues.

---

<a id="feat-core-14"></a>
### CORE-14 - Save-state format v3

**Priority:** P0 | **Effort:** M | **Depends on:** FND-03 | **Status:** Done (2026-11-01)

**Problem.** v2 has no feature flags or option snapshot. New subsystems need forward-compatible state.

**Implemented.** Format v3 adds header feature flags and an up-front section table, keeps v1/v2 loaders, records the options snapshot and live video region, and exposes a warn/reject unknown-section policy. Rewind uses its own keyframe buffer instead of delta sections. See FND-03 and [`docs/SaveStates.md`](SaveStates.md).

---

<a id="feat-core-15"></a>
### CORE-15 - State diff and verification tooling

**Priority:** P1 | **Effort:** M | **Depends on:** CORE-14 | **Status:** Done (2026-11-01)

**Problem.** Debugging state divergence is manual.

**Implemented.**
- `StateDiff` parses both states, decompresses every section, and reports presence, versions, lengths, FNV-1a hashes, the first differing byte, and the differing-byte count.
- CLI `state diff <a> <b> [--json]` prints a header/frames/disc summary and one line per section, including `only in a`/`only in b`.
- The comparison does not load either state into a machine, so it works on malformed or future option combinations as long as section framing is valid.
- RAM/VRAM/register detail comes from the Memory, Machine, and CPU sections.
- CLI JSON and UI view.
- "Expected vs actual" report.

**Files.** `src/FiveDo.Host/StateInspector.cs`, `src/FiveDo.Cli/Program.cs`, UI.

**Tests.** Identical states, one-byte diff, missing section, corrupt state.

**Done when.** A developer can locate the first differing byte/section in one command.

---

<a id="feat-core-16"></a>
### CORE-16 - Frame/audio hash regression suite

**Priority:** P0 | **Effort:** M | **Depends on:** CORE-11 | **Status:** Partial

**Implemented (2026-10-31).** `run --checkpoints` emits per-frame PC, frame hash,
audio hash, state hash, CD status, DSP state, and CEL busy cycles;
`tools/FiveDo.DiffHarness baseline --baseline <report> --current <report>` detects
changed 5DO field values. The manifest/report pair is the baseline format. The
harness also supports `--compare-pc` with the optional debug core: the five-title
showcase video-matches two titles but the strict PC gate shows 0/5 matches, so a
frame hash alone is not treated as sufficient evidence. Remaining: commit the first
showcase baseline, wire it into the nightly workflow, and add visual trend
artifacts.

**Problem.** Golden hashes exist for a few titles, not the full collection.

**Add.**
- Per-title checkpoint manifest (frame, PC, frame hash, audio hash).
- CI gate with allowlisted known differences.
- Visual diff artifacts.
- Trend history.

**Files.** `tests/FiveDo.Tests/`, `docs/TestCorpus.md`, CI.

**Tests.** Each checkpoint; intentional change requires baseline update.

**Done when.** Every PR runs the suite and fails on unexplained changes.

---

## 3.2 System, disc, BIOS, region, peripherals

<a id="feat-sys-01"></a>
### SYS-01 - CD-DA / Red Book and multi-track discs

**Priority:** P0 | **Effort:** L | **Depends on:** CORE-04 | **Status:** Partial (2026-11-01)

**Problem.** 5DO exposed only 2048-byte data sectors. `CueSheet` opened only the first data track; `ChdDiscImage` extracted data-track bytes. CD-DA was unsupported.

**Implemented (2026-11-01).**
- `ITrackedDiscSource` exposes the track table, total sector count, and native track sectors (2048/2336/2352).
- `CueDiscImage` uses the MAME-compatible CHDSharp CUE parser: multi-file/multi-track, `INDEX 00/01`, `PREGAP`/`POSTGAP`, WAVE audio, and sessions.
- `ChdDiscImage` maps CHD track metadata to logical LBA ranges, reads audio tracks, and byte-swaps Red Book samples back to little-endian.
- `CdromDevice` builds the TOC from the track table, implements READ TOC (track and `0xAA` lead-out), READ SUB-Q, MODE SET/MODE SENSE block pages, 24-bit READ_DATA counts, PLAY_AUDIO_MSF/TRACK position tracking, and 2352-byte digital-audio reads.
- CD-DA is decoded at 44.1 kHz and mixed into the DSP output with `FiveDoMachine.CdAudioVolume`; CD section v4 records sector size, block flags, audio position, volume, and mute.
- `disc tracks` reports every CUE/CHD track (kind, sector size, frames, start LBA) in text and `--json`.

**Remaining.**
- Known CD-DA title evidence and a 10-second DSP+CD-DA WAV capture.
- Subcode and a data-track parity check against the old 2048-only path.
- Real CD-DA evidence remains blocked on a sampled title with an audio track.

**Files.** `src/FiveDo.Storage/IDiscSource.cs`, `CueSheet.cs`, `Chd/ChdDiscImage.cs`, `src/FiveDo.Cdrom/CdromDevice.cs`, `src/FiveDo.Audio/`.

**Tests.** Multi-track CUE; CHD audio; CD-DA WAV; native-sector read-ahead; data-track parity; save/load.

**Done when.** A known CD-DA title plays its audio; a 10s WAV contains DSP + CD-DA; data titles unchanged.

**Reference.** `opera_cdrom.c:565`, `:901-1088`; `retro_cdimage.c`.

---

<a id="feat-sys-02"></a>
### SYS-02 - Disc swap / ODE / multi-disc

**Priority:** P0 | **Effort:** M | **Depends on:** CORE-04 | **Status:** Partial (2026-11-01)

**Problem.** 5DO had `InsertDisc`/`EjectDisc` but no swap flow, multi-disc playlist, or ODE.

**Implemented.**
- `DiscPlaylist` loads and saves M3U/M3U8 with `#EXTINF` titles and relative-path resolution; the editor adds, removes, and reorders entries.
- **File > Playlist Editor...** edits the playlist, and dropping disc images or playlists on the window imports them.
- File > Swap Disc and **F3** advance the playlist without resetting the machine; a one-off file picker swaps to any disc.
- The playlist path is persisted in `AppSettings.DiscPath` and recent discs, so restarting resumes the multi-disc session.
- Guest eject (`0x06`) opens the virtual tray; guest inject (`0x07`) restores the Opera tray/disc/spin/ready bits and media-access poll.
- A guest eject raises `CoreRunner.GuestDiscEjected` on the core thread. The frontend advances to the next playlist entry when `AutoSwapOnEject` is enabled, giving ODE-style behavior; without a playlist it prompts for **F3**.
- CLI `disc playlist` lists entries; `run --playlist`, `--swap-at N`, and `--swap-disc` open and swap discs headlessly.
- Per-disc hashes update after a swap; the original game identity keys BIOS, NVRAM, fixes, and cheats for the session.
- A save-state saved after a swap reloads with the new drive state; `SaveStateTests` covers the roundtrip.
- A two-disc `D (USA, Europe)` playlist was swapped at frame 300 in a 600-frame headless run; the disc hash changed and the machine continued without reset.

**Remaining.**
- A real in-game disc-swap prompt from a supported multi-disc title (reaching the actual game-requested swap, not a timed test swap).

**Files.** `src/FiveDo.Cdrom/CdromDevice.cs`, `src/FiveDo.Host/`, UI/CLI.

**Tests.** `DiscPlaylistTests` covers editing/saving; `CdromMultiTrackTests` covers eject/inject counters; `CoreRunnerTests` covers the guest-eject callback; `SaveStateTests` covers swap plus state roundtrip.

**Done when.** A multi-disc title can continue after a swap; ODE works in a supported frontend.

**Reference.** `opera_cdrom.c:158-184`, `:415-461`.

---

<a id="feat-sys-03"></a>
### SYS-03 - PAL/NTSC region modes

**Priority:** P0 | **Effort:** L | **Depends on:** CORE-02 | **Status:** Partial (2026-11-01)

**Problem.** 5DO was NTSC 320x240@60 only. PAL titles (e.g., Battle Chess Europe) reject NTSC system software.

**Implemented.**
- `VideoRegion` (`Ntsc`, `Pal1`, `Pal2`) and `VideoRegionProfile` carry visible size, start scanline, field lines, field rate, VDL clock, and frame cycles.
- `Quartz` uses the region's 312-line PAL field, 624-line full cycle, 15,600 VDL events/second, and 50 Hz frame deadline while preserving the existing NTSC clock.
- `Vdlp` maps PAL1/PAL2 visible lines from scanlines 21/22 through 288 lines; the frame buffer and high-resolution storage grow to 576 lines.
- `FiveDoMachine` reports the region, uses the region frame clock, and sizes `SourceWidth`/`SourceHeight` to 320x288 or 384x288.
- `CoreRunner` paces PAL at 50 Hz from the audio-derived sample count; a 300-frame PAL run produced exactly 6.000 seconds of 44.1 kHz audio.
- Settings > Emulation > Video region and `AppSettings.Region` persist the manual choice; `AutoRegion` (default on) follows the resolved BIOS and calls `FiveDoMachine.SetVideoRegion` between frames, so opening a PAL title switches a running machine without recreating it. `GameProfile` snapshots the region.
- Settings > Emulation > Region override pins a disc to NTSC, PAL1, or PAL2 through `RegionOverrides`; the override wins over the automatic/global choice and is applied when the disc opens.
- Loading a save state written under a different region switches the machine to that region and records a clear warning. The options section now saves the live region rather than the startup tuning value.
- CLI `--region auto|ntsc|pal1|pal2`; `auto` selects PAL1 when the selected BIOS is a PAL image. Run JSON adds `region` and `fieldRate`.
- Save-state options section v6 records the region and warns on mismatch.
- Eleven new tests cover profiles, Quartz PAL wrap, VDLP start lines, machine geometry/timing, runtime region switching, state-driven region switching, PAL2 width, automatic/manual region resolution, and per-game region overrides.

**Evidence.** `Battle Chess (Europe)` with `panafz10e-anvil.bin` ran 600 frames at `Pal1`, 320x288, 50 Hz, and produced the correct 3DO logo. `--bios-auto bios` selected the FZ-10E and PAL1 automatically. A 300-frame capture produced 264,609 samples (6.0002 s), confirming 50 Hz audio pacing.

**Remaining.**
- Automatic per-game profile application (SYS-18) beyond the explicit region override.
- A PAL2 title and a PAL-specific VDL test ROM; PAL2 geometry is covered synthetically.

**Files.** `src/FiveDo.Core/Models/VideoRegion.cs`, `EmulatorTuning.cs`, `src/FiveDo.Machine/Quartz.cs`, `FiveDoMachine.cs`, `src/FiveDo.Video/Vdlp.cs`, `src/FiveDo.Host/`, UI/CLI.

**Tests.** `VideoRegionTests`; `AppSettingsStoreTests`; `CliJsonTests`; full NTSC suite remains green (1,415 tests).

**Done when.** PAL titles run at the correct size/rate; no NTSC regression.

**Reference.** `opera_region.c`, `libretro_core_options.c` (`opera_region`).

---

<a id="feat-sys-04"></a>
### SYS-04 - BIOS manager and auto-selection

**Priority:** P0 | **Effort:** L | **Depends on:** FND-02 | **Status:** Done

**Implemented (2026-10-31).** `BiosCatalog` recognizes the common 3DO BIOS MD5s and
their region, video-standard, Kanji, Anvil, and patch metadata. `BiosLibrary.Scan`
validates a folder, classifies primary and secondary images, and reports bad files;
`SelectForGame` combines game-database regions, known title requirements (Slayer
Europe/USA, Powers Kingdom Europe, Arcade CD-ROM), and per-game MD5 overrides. The CLI
adds `--bios2`, `--bios-auto`, and `bios list|scan|select`; `run --json` reports the
selected BIOS path/hash/name/region/reason/source. Settings > Paths adds the BIOS folder,
primary/secondary paths, auto-select, and validation; Settings > Emulation adds a
per-game override; File adds assign/clear actions. The configured BIOS remains the
fallback when no BIOS folder is set. The five-title 3,000-frame and four
known-requirement 12,000-frame smoke runs are in `docs/TestCorpus.md` 1.1f. Remaining:
the full-collection BIOS-aware sweep and per-game auto profiles (SYS-18). Embedded boot
and RSA/unsigned-disc options remain separate legal/engineering work.

**Problem.** 5DO has one BIOS path; `BiosImage` supports BIOS2 but the CLI/frontend do not expose it; there is no region/MD5 database or auto-selection.

**Add.**
- `BiosLibrary`: scan, MD5, region/type/Anvil metadata.
- `BiosProfile`: BIOS1, BIOS2/Kanji, region, notes.
- Per-game `BiosId`.
- Auto-select by disc region/known requirements.
- CLI `--bios2`, `--bios-auto`, `bios list/scan`.
- Settings UI: list/import/assign/validate.
- State/BIOS mismatch warnings.

**Files.** `src/FiveDo.Storage/BiosImage.cs`, `src/FiveDo.Host/Models/AppSettings.cs`, UI/CLI.

**Tests.** MD5 detection, BIOS2/Kanji, auto-select, missing BIOS, per-game override.

**Done when.** Arcade/EU/JP titles boot with the right BIOS; BIOS2/Kanji works; no manual BIOS knowledge required for known titles.

**Reference.** `4DO Settings.settings` (`BiosRom2File`); `libretro_core_options.c` (`opera_bios`, `opera_font`).

---

<a id="feat-sys-05"></a>
### SYS-05 - Embedded open-source boot ROM

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-04, ENG-03 | **Status:** Pending

**Problem.** 5DO requires a user BIOS. Opera offers an embedded Opera Boot option.

**Add.**
- License review of the candidate boot ROM.
- Embed only if compatible with 5DO's license.
- Boot path that selects embedded/external BIOS.
- UI/CLI option; docs.
- No copyrighted BIOS distribution.

**Files.** `src/FiveDo.Storage/`, `src/FiveDo.Machine/FiveDoMachine.cs`, legal docs.

**Tests.** Boot homebrew/demo without external BIOS; external BIOS still works.

**Done when.** The option is legally cleared and works; otherwise document why it was rejected.

---

<a id="feat-sys-06"></a>
### SYS-06 - RSA/unsigned-disc option

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-04, ENG-03 | **Status:** Pending

**Problem.** Four Arcade CD-ROM titles need a patched BIOS; unsigned/homebrew discs need `norsa`.

**Add.**
- Legal review first.
- Prefer documenting/auto-selecting user-provided patched BIOS.
- If legally cleared, an explicit "allow unsigned discs" option that bypasses the BIOS RSA check without distributing a BIOS.
- Clear warning and audit log.
- Tests with the four arcade titles.

**Files.** `BiosImage.cs`, `FiveDoMachine.cs`, legal docs.

**Tests.** Arcade titles boot; signed titles unchanged; option off by default.

**Done when.** The four arcade titles work with a stock BIOS + option, or the limitation is documented with a supported BIOS path.

---

<a id="feat-sys-07"></a>
### SYS-07 - Memory capacity options

**Priority:** P1 | **Effort:** M | **Depends on:** FND-01 | **Status:** Pending

**Problem.** 5DO is fixed at 3 MB visible (2 MB DRAM + 1 MB VRAM). Opera supports 2/4/8/14/15 MB DRAM and 1/2 MB VRAM for homebrew.

**Add.**
- `MemoryCapacity` option (DRAM/VRAM).
- Memory map/backing allocation by capacity.
- Save-state section records capacity.
- UI/CLI.
- Stock default.

**Files.** `FiveDoMemory.cs`, `EmulatorConstants.cs`, `FiveDoMachine.cs`, `AppSettings.cs`.

**Tests.** Each capacity boots a compatible homebrew/test; stock titles unchanged; state roundtrip.

**Done when.** Homebrew can use expanded memory; stock behavior is unchanged.

---

<a id="feat-sys-08"></a>
### SYS-08 - CPU overclock/underclock

**Priority:** P1 | **Effort:** S | **Depends on:** FND-01 | **Status:** Partial

**Implemented (2026-10-31).** `--cpu-clock 0.5x-4x|6.25-50MHz` on `run`/`trace`,
`AppSettings.CpuClockMultiplier` and `ResolveArmClock`, the Settings field, `run --json`
`cpuClock`, and the existing state-header ARM-clock mismatch warning. Remaining:
benchmark and compatibility measurements at each clock, plus per-game overrides.

**Problem.** `EmulatorTuning.ArmClock` exists but there is no UI/CLI, and no save-state warning.

**Add.**
- Multiplier 0.5x-4x (or 6.25-50 MHz).
- UI/CLI `--cpu-clock`.
- State warning when the clock differs.
- Known-good defaults; docs.
- Benchmark impact.

**Files.** `EmulatorTuning.cs`, `Quartz.cs`, `AppSettings.cs`, `Program.cs`.

**Tests.** Clock validation, state warning, frame timing, no default change.

**Done when.** Users can overclock safely; the default remains 12.5 MHz.

---

<a id="feat-sys-09"></a>
### SYS-09 - CD speed control

**Priority:** P1 | **Effort:** M | **Depends on:** CORE-04 | **Status:** Done

**Implemented (2026-10-31).** `CdSpeed` (`Unbounded`, `X1`, `X2`, `X4`, `X8`, `X16`,
`X24`, `X32`), `EmulatorTuning.CdSpeed` (default `X2`), `CdromDevice.ConfiguredSpeed`,
Opera's per-speed overhead compensation table, `--cd-speed` on `run`/`trace`, Settings >
Emulation > CD speed, `run --json` `cdSpeed`, and Options section version 5 with a
mismatch warning. The FreeDO timing path is unchanged. The three-title 3,000-frame smoke
differential is in `TestCorpus.md` 1.1e; unit and integration tests cover each speed,
unbounded immediate reads, state warnings, settings roundtrip, and CLI parsing. The UI
tooltip warns that speeds above 16x can expose timing issues. Remaining: a full-collection
speed sweep and CD-DA speed behavior, which belongs to SYS-01.

**Problem.** 5DO has fixed CD behavior. Opera supports unbounded/1x-32x.

**Add.**
- `CdSpeed` option.
- Timing model uses the selected speed.
- UI/CLI.
- Warning for high speeds/timing-sensitive titles.
- Tests.

**Files.** `CdromDevice.cs`, `EmulatorTuning.cs`, UI/CLI.

**Tests.** Speed selection, sector timing, no default regression.

**Done when.** Speed changes are measurable and documented.

---

<a id="feat-sys-10"></a>
### SYS-10 - Random seed control

**Priority:** P1 | **Effort:** S | **Depends on:** FND-01 | **Status:** Partial

**Implemented (2026-10-31).** `EmulatorTuning.RandomSeed`, `Dsp.SetSeed`,
`Clio.SetRandomSeed`, `--random-seed default|random|SEED`, `AppSettings.RandomSeed`
and the Settings field. The default preserves FreeDO's `0xa5a5a5a5`; `random` picks a
fresh 32-bit value. Tests cover seed application to both DSP and CLIO, CLI validation,
fixed-seed state roundtrip, and separate CLIO/DSP generators. A fixed seed reproduces
the same run; there is no CPU-side random path today.

**Problem.** 5DO has deterministic PRNG state but no user control or fixed-seed option.

**Add.**
- `RandomSeed` option (`random` or fixed value).
- Seed both CPU/DSP PRNG paths consistently.
- UI/CLI; state snapshot.
- Tests for deterministic replay.

**Files.** `src/FiveDo.Cpu/`, `Dsp.cs`, `EmulatorTuning.cs`, UI/CLI.

**Tests.** Fixed seed reproducibility; random seed varies; state roundtrip.

**Done when.** A fixed seed produces identical runs across machines.

---

<a id="feat-sys-11"></a>
### SYS-11 - MADAM matrix hardware/software

**Priority:** P1 | **Effort:** S | **Depends on:** FND-01 | **Status:** Done (2026-11-01)

**Problem.** 5DO implements the hardware matrix engine only. Opera allows software mode for speed/compatibility.

**Implemented.** The VDL display-control `clut_bypass` bit (bit 25) is already honored
exactly as Opera does: pixels with bit 15 set select the fixed grayscale CLUT, while other
pixels use the per-line user CLUT and pixel zero uses the background color. This is a
guest-controlled display mode, not a user option, so no new setting is needed.
`FrameAssembler`, the OpenGL palette shader, and the Vulkan `Palette.frag` share the same
branch. Tests cover the fixed-CLUT and pixel-bit15 paths in `FrameAssemblerTests`,
`VdlpTests`, and the shader source contract.

**Done when.** The option works and is documented.

---

<a id="feat-sys-12"></a>
### SYS-12 - SWI HLE for Portfolio OS

**Priority:** P2 | **Effort:** L | **Depends on:** CORE-01 | **Status:** Pending

**Problem.** 5DO runs the guest OS. Opera offers optional HLE for certain Portfolio calls.

**Add.**
- Identify high-value SWI calls.
- Implement HLE behind a flag.
- Preserve fallback to the real OS.
- Tests and compatibility notes.

**Files.** `Arm60.cs`, new `PortfolioHle.cs`, `EmulatorTuning.cs`.

**Tests.** HLE on/off; known titles; no regression.

**Done when.** The opt-in HLE improves performance without breaking compatibility.

---

<a id="feat-sys-13"></a>
### SYS-13 - VDLP bypass CLUT

**Priority:** P1 | **Effort:** S | **Depends on:** FND-01 | **Status:** Pending

**Problem.** 5DO always applies the CLUT. Opera can bypass it for speed/debug.

**Add.**
- `VdlpBypassClut` option.
- Renderer/assembler path.
- UI/CLI; warning.
- Tests.

**Files.** `Vdlp.cs`, `FrameAssembler.cs`, UI/CLI.

**Tests.** Bypass on/off; visual difference documented; no default change.

**Done when.** The option works and is clearly marked non-accurate.

---

<a id="feat-sys-14"></a>
### SYS-14 - Per-game NVRAM and versions

**Priority:** P1 | **Effort:** M | **Depends on:** FND-02 | **Status:** Done

**Implemented (2026-10-31).** `NvramStorageMode` (`Global`, `PerGame`), `NvramVersion`
(0-9), and `NvramDirectory` are in `AppSettings`; `NvramLayout` resolves the shared
legacy/versioned paths and the per-game `<gameId>.<version>.ram` path with sanitized
game IDs. `NvramStore` supports read-only migration fallbacks (per-game version 0,
shared version, legacy `nvram.ram`) and atomic saves; a corrupt primary is backed up and
does not silently load an older image. Settings > Emulation exposes the storage mode and
version; changing either restarts the machine and switches images safely. File > Export
NVRAM..., Import NVRAM..., and Reset NVRAM manage the current image. The CLI adds
`--nvram-mode`, `--nvram-version`, and `--nvram-dir` to `run`/`trace`, reports
`nvramPath`/`nvramMode`/`nvramVersion` in `run --json`, and adds
`nvram path|list|export|import|reset`. Unit, store, settings, JSON-contract, and CLI
tests cover per-game isolation, shared mode, migration, version separation, corrupt
images, and import/export/reset. Documentation is updated in `Configuration.md`,
`CliReference.md`, and `UserGuide.md`.

**Problem.** 5DO uses one global 32 KB NVRAM file. Opera supports per-game/shared and versions 0-9.

**Add.**
- `NvramStorageMode` (global/per-game).
- Per-game NVRAM path keyed by game ID.
- Version selector.
- Import/export/reset.
- UI/CLI.
- Migration from global.

**Files.** `NvramStore.cs`, `NvramLayout.cs`, `FiveDoMachine.cs`, `AppSettings.cs`, UI/CLI.

**Tests.** Per-game isolation; shared mode; migration; save/load.

**Done when.** Per-game NVRAM works and is documented.

---

<a id="feat-sys-15"></a>
### SYS-15 - Mouse/lightgun/arcade lightgun/flightstick/trackball

**Priority:** P0 | **Effort:** L | **Depends on:** FND-01 | **Status:** Partial (2026-11-01)

**Problem.** 5DO was joypad-only. Arcade CD-ROM titles and mouse/flightstick titles are unplayable.

**Implemented.**
- `InputDeviceSet` and `PbusChainEncoder` emit Opera's exact daisy-chain records for joypad, flightstick, mouse, lightgun, arcade lightgun, Orbatak trackball, and none; the legacy 4DO packet is preserved for all-joypad configurations.
- Six per-port devices are selectable in Settings > Emulation > Input devices and with `--input-device 1=mouse,3=lightgun`; `--pbus-format opera` forces the chain layout and `run --json` reports `inputDevices`.
- The desktop pointer drives mouse/lightgun/arcade-lightgun/Orbatak ports, including relative mouse/trackball motion, off-screen lightgun reload mapping, arcade start/coin/service buttons, and a lightgun crosshair.
- **Settings > Emulation > Pointer port** routes the pointer to one port or all pointer devices, so a multiplayer lightgun session can give each player an independent aim. **Pointer calibration** scales the mapping 0.5-2.0 and applies X/Y offsets in native pixels, with a reset button.
- Packet tests cover every device and the Orbatak record; Crime Patrol, Mad Dog II, Orbatak, and Shootout at Old Tucson boot and detect the selected device.

**Remaining.**
- Scripted gameplay validation for each device.
- A six-player title pass (tracked by SYS-16).

**Files.** `src/FiveDo.Input/`, `src/FiveDo.Madam/Madam.cs`, UI/CLI.

**Tests.** Device packet tests, Orbatak tests, boot detection for the four named lightgun/arcade titles, six-joypad regression, and pointer port/calibration mapping.

**Done when.** Every device works in a representative title and is selectable per port.

**Reference.** `References/opera-libretro-master/lr_input.c`; `libopera/opera_pbus.c`.

---

<a id="feat-sys-16"></a>
### SYS-16 - Six-player/multi-tap verification

**Priority:** P1 | **Effort:** S | **Depends on:** SYS-15 | **Status:** Pending

**Problem.** 5DO encodes six pads but does not validate all six in a real title.

**Add.**
- Six-player test ROM/title.
- PBUS packet trace for all ports.
- UI status for connected devices.
- Docs.

**Files.** `PbusEncoder.cs`, tests, docs.

**Tests.** Six pads, disconnect/reconnect, hot-plug.

**Done when.** A six-player title accepts input from all ports.

---

<a id="feat-sys-17"></a>
### SYS-17 - Game database expansion and community DB

**Priority:** P1 | **Effort:** M | **Depends on:** FND-02 | **Status:** Pending

**Problem.** 5DO has 518 records; the local collection has 594 images; 184 have no 4DO reference. Metadata lacks region/revision/BIOS/fix fields.

**Add.**
- Expand records from Redump/No-Intro (license review).
- Fields: region, revision, BIOS requirement, fix flags, input devices, notes.
- Community contribution format and validation.
- Offline cache.
- `info --json` exposes the fields.

**Files.** `GameDatabase.xml`, `GameDatabase.cs`, `GameRecord.cs`, tools.

**Tests.** Lookup, duplicate IDs, unknown games, schema validation.

**Done when.** >=95% local images have metadata; community submissions validate.

---

<a id="feat-sys-18"></a>
### SYS-18 - Auto per-game profile

**Priority:** P0 | **Effort:** L | **Depends on:** SYS-04, SYS-17 | **Status:** Pending

**Problem.** Users must know which BIOS/region/fix/input device a title needs.

**Implemented.**
- Auto BIOS selection (SYS-04), BIOS-driven region selection (SYS-03), automatic fix masks, and per-game NVRAM (SYS-14) already cover the default path.
- `GameControlDefaults` seeds a per-game PBUS device override for known lightgun/arcade titles (Crime Patrol, Mad Dog II, Who Shot Johnny Rock, The Last Bounty Hunter, Shootout at Old Tucson, Space Pirates, Drug Wars) and Orbatak trackball titles when `AutoInputDevices` is on.
- `InputDeviceOverrides` persists six-device lists per game ID; `ResolveInputDevices`/`ApplyInputDevices(input, gameId)` honor them, and Settings > Emulation has a **Override input devices for this game** control.
- `GameProfile` stores timing, region, CD speed, CPU multiplier, CD-DA volume/mute, BIOS MD5, fix mask, video filter, high-resolution mode, six PBUS devices, and NVRAM mode/version. Settings > Emulation can save/delete/import/export the profile for the current disc.
- `AppSettings.CreateEffectiveForGame` deep-copies the override maps and applies the profile at machine creation without mutating the live settings. The desktop uses the effective settings for BIOS selection, fix mode, input devices, the renderer filter, high-resolution mode, and the NVRAM store.
- CLI `run/trace --game-profile <json>` uses the profile as the timing/region/fix/high-resolution baseline before explicit flags.
- Auto seeding is user-replaceable: an existing override or profile is never replaced.

**Remaining.**
- Cheat fields/list in the profile and automatic DB-driven population.
- Apply profile changes to an already-running machine (or prompt for restart).
- Game-database fields for region/revision/fix/BIOS (SYS-17).

**Files.** New `GameProfile.cs`, `AppSettings.cs`, UI/CLI.

**Tests.** Known titles auto-configure; override wins; profile roundtrip.

**Done when.** Every local disc gets a working profile without manual BIOS/region selection.

---

<a id="feat-sys-19"></a>
### SYS-19 - Save-state interoperability with 4DO/Opera

**Priority:** P2 | **Effort:** L | **Depends on:** CORE-14 | **Status:** Pending

**Problem.** 5DO cannot import/export 4DO `.4dosav` or Opera/libretro states.

**Add.**
- 4DO state importer/exporter.
- Opera state importer/exporter.
- Section mapping and warnings.
- CLI `state import/export`.
- Fixtures and roundtrip tests.

**Files.** New `src/FiveDo.Host/StateInterop/`, `StateInspector.cs`, CLI.

**Tests.** Import known states; export and reload; mismatch warnings.

**Done when.** A user can migrate saves and continue; docs explain limits.

---

<a id="feat-sys-20"></a>
### SYS-20 - Disc health / Redump DAT / CHD integrity

**Priority:** P2 | **Effort:** M | **Depends on:** SYS-22 | **Status:** Partial (2026-11-01)

**Problem.** Bad dumps cause false compatibility failures.

**Implemented.**
- `DiscVerifier` reads every native track sector (audio and data), reports the first errors with LBA, verifies sector counts, and computes an FNV-1a 64-bit content hash.
- `RedumpDat` parses the Redump/No-Intro XML `<datafile><game><rom>` format, normalizes/validates CRC-32, MD5, and SHA-1, and ignores unusable entries.
- `DiscHealth` hashes each native track (CRC-32, MD5, SHA-1) and matches size plus every supplied DAT hash. Multi-track discs match each track independently.
- CLI `disc verify <path> [--json] [--max-errors N] [--dat path]` exits 1 on a read error, an incomplete read, or a DAT mismatch. The JSON result adds the per-track hashes and the matched game/ROM.
- `DiscVerifierTests`, `DiscHealthTests`, and a synthetic ISO/DAT CLI check cover clean reads, errors, DAT parsing, single/multi-track matches, mismatches, and malformed DATs.
- A real Battle Chess (Europe) CHD verified 143,810/143,810 sectors and 338,241,120 bytes with no errors; a synthetic two-sector ISO matched its DAT with CRC-32 `013C6AE4`, MD5 `3224F6D3...`, and SHA-1 `4EA34675...` and returned exit 1 after a one-byte change.

**Implemented (2026-11-04).**
- `DiscHealthCache` persists one self-invalidating record per disc (path, length, last-write
  time, status, matched DAT game/ROM, first-track CRC-32/MD5/SHA-1, track counts, detail)
  in `library-health.json`; `DiscHealthCacheStore` writes it atomically and ignores stale
  or malformed files.
- `LibraryHealthVerifier` reads every sector through `DiscVerifier`, then matches the disc
  against an optional `RedumpDat`. Results are `Verified`, `Mismatch`, `ReadError`, or
  `NoDat`; open/read failures are recorded instead of thrown.
- The library entry carries `DumpStatus`, `DumpBadge`, and `DumpTip`; both the grid and
  list library views show a badge next to the 4DO database badge, and the tooltip includes
  the matched game and hashes.
- **File > Verify Library Discs...** verifies every library entry on a background task with
  progress in the status bar, prompts for a DAT when none is configured (cancel to check
  readability only), saves the DAT path in settings, updates the cache, and refreshes the
  badges. The Game Library window has the same **Verify Health** and **DAT...** actions and
  a health summary in its status line.
- `DiscHealthCacheTests` covers store roundtrip, invalid files, stale records, badge text,
  status counts, missing files, unreadable images, DAT matches/mismatches, and progress.

**Remaining.**
- 4DO/Opera hash comparison and DAT-driven exports in the desktop.
- Full Redump/No-Intro DAT coverage (the parser accepts user-supplied files).

**Files.** New `DiscHealth.cs`, `DiscImageFactory.cs`, UI/CLI.

**Tests.** Known good/bad dumps; CHD corruption; no false positives.

**Done when.** Users can tell whether a failure is the emulator or the image.

---

<a id="feat-sys-21"></a>
### SYS-21 - Physical CD drive support

**Priority:** P2 | **Effort:** M | **Depends on:** none | **Status:** Pending

**Problem.** 5DO is file-only; 4DO can boot from a CD drive.

**Add.**
- `CdDriveDiscSource` using platform APIs.
- Drive selection UI/CLI.
- Same `IDiscSource` interface.
- Read speed/error handling.
- Tests with a real disc where available.

**Files.** New `CdDriveDiscSource.cs`, `DiscImageFactory.cs`, UI/CLI.

**Tests.** Drive open/read/eject; unsupported platform fallback.

**Done when.** A physical 3DO disc boots on Windows/Linux/macOS where supported.

---

<a id="feat-sys-22"></a>
### SYS-22 - Disc image conversion/repair

**Priority:** P2 | **Effort:** M | **Depends on:** SYS-01 | **Status:** Pending

**Problem.** Users need CUE/BIN/ISO/CHD conversion and repair.

**Add.**
- CLI/UI convert: CUE+BIN -> CHD, ISO -> CHD, BIN -> ISO, etc.
- Track preservation.
- Verify output.
- Repair malformed CUE.
- Docs.

**Files.** `src/FiveDo.Cli/Program.cs`, `src/FiveDo.Storage/`, new converter.

**Tests.** Roundtrip formats; track preservation; corrupt input.

**Done when.** Users can convert/repair without external tools.

---

## 3.3 Video

<a id="feat-vid-01"></a>
### VID-01 - True 640x480 / 480i internal rendering

**Priority:** P1 | **Effort:** XL | **Depends on:** CORE-03 | **Status:** Pending

**Problem.** `--high-res` line-doubles 240 lines; it is not true 480-line rendering.

**Add.**
- `HighResolutionMode`: `Off`, `FreeDoLineDouble`, `True640`.
- Scale CEL Y, clipping, and VDLP line timing by 2.
- Stop duplicating planes 2/3; emit independent 480 lines.
- Preserve guest-visible VRAM semantics; fallback for framebuffer readers.
- Validate Lemmings, a 2D title, and a 3D title.
- Measure frame time.

**Files.** `CelRasterizer.cs`, `Vdlp.cs`, `EmulatorConstants.cs`, `FiveDoMachine.cs`.

**Tests.** Screenshot comparison; frame hash; performance; per-title compatibility.

**Done when.** True vertical detail is visible; 60fps; documented per-title.

**Reference.** `docs/HighResolutionCelResearch.md` Option B.

---

<a id="feat-vid-02"></a>
### VID-02 - 2x/4x supersampling

**Priority:** P2 | **Effort:** L | **Depends on:** VID-01 | **Status:** Pending

**Problem.** Geometry/texture edges are limited by the native rasterizer.

**Add.**
- Host-side 2x/4x color/depth target.
- Route CEL writes through the target while preserving guest VRAM.
- Deterministic downsample.
- Performance budget.
- Per-game opt-in.

**Files.** `CelRasterizer.cs`, new host target, renderer.

**Tests.** Comparison captures; frame time; no guest-visible change.

**Done when.** 2x prototype fits the budget; decision documented.

---

<a id="feat-vid-03"></a>
### VID-03 - GPU CEL compositing

**Priority:** P3 | **Effort:** XL | **Depends on:** VID-01 | **Status:** Pending

**Problem.** True high-res is expensive on the CPU.

**Add.**
- Design note mapping CEL stages to GL/Vulkan passes.
- Prototype packed/literal CELs.
- Parity against software oracle.
- Go/no-go decision.

**Files.** `FiveDo.Video.OpenGL`, `FiveDo.Video.Vulkan`, `CelEngine`.

**Tests.** Pixel parity; performance; no default change.

**Done when.** A documented decision exists with evidence.

---

<a id="feat-vid-04"></a>
### VID-04 - 480i/240p detection, deinterlacing, field blending

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-03 | **Status:** Pending

**Problem.** 5DO assumes 240 lines; 3DO titles mix 240p/480i.

**Add.**
- Detect field/line mode from VDLP.
- Bob/weave/blend deinterlacing options.
- Field-rate presentation.
- Test patterns.

**Files.** `Vdlp.cs`, `FrameAssembler.cs`, renderer.

**Tests.** 240p/480i patterns; no flicker; performance.

**Done when.** Both modes render correctly.

---

<a id="feat-vid-05"></a>
### VID-05 - PAL output timing

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-03 | **Status:** Done (2026-11-01)

**Problem.** PAL mode needs 288-line output and 50 Hz presentation.

**Implemented.**
- `Vdlp` fills 288 PAL lines from scanline 21/22 and `FiveDoMachine` reports `SourceWidth`/`SourceHeight` of 320x288 or 384x288.
- The frame store, mailbox, uploader, CPU renderer, screenshots, and save-state frame section use the dynamic source height; high-resolution storage grows to 576 lines.
- `CoreRunner` paces from the audio-derived 50 Hz cadence, and the status/overlay target reports 50 fps.
- The existing 4:3 aspect policy stretches the 320-wide PAL frame to the correct display aspect without a separate viewport path.
- `VideoRegionTests` covers PAL geometry and frame timing; a 300-frame PAL capture produced exactly 6.000 seconds of audio.
- `Battle Chess (Europe)` renders a correct 320x288 3DO logo at 50 Hz with no NTSC regression.

**Files.** `Vdlp.cs`, `FiveDoMachine.cs`, `CoreRunner.cs`, `VdlFrameMailbox.cs`, `VdlFrameUploader.cs`.

**Tests.** `VideoRegionTests`; PAL Battle Chess capture; full NTSC suite.

**Done when.** PAL titles look correct at 50 Hz.

---

<a id="feat-vid-06"></a>
### VID-06 - Auto-crop, overscan, void area, gray border

**Priority:** P1 | **Effort:** M | **Depends on:** FND-06 | **Status:** Done (2026-11-02)

**Problem.** 4DO has these; 5DO always showed the full frame.

**Implemented.**
- `CropInsets` stores independent left/top/right/bottom crops, clamps each edge to 0-32 pixels, and requires at least 16 visible pixels per axis. `AppSettings.ResolveOverscan` keeps the legacy uniform `OverscanPixels` value working until the per-edge values are set.
- Settings > Video exposes **Overscan crop L/T/R/B**, **Void border** (0-64 screen pixels), and **Void color** (`#RRGGBB` or `#AARRGGBB`). Invalid values are rejected without closing the dialog.
- The game view applies the crop before OpenGL/Vulkan/Direct3D/software presentation; `SaveNativeScreenshot` applies the same crop, so native and filtered captures match.
- A configurable void-area border is drawn around the presented image and fills letterboxing; the pointer/lightgun mapping uses the inner video surface so input stays aligned.
- `GameProfile.Overscan` stores a per-game crop; `CreateEffectiveForGame` applies it without mutating the live settings. Import/export normalizes the crop.
- The crop and border are presentation-only: frame hashes, input, audio, and save states are unchanged. CLI evidence (`--frame-out`, checkpoints) deliberately keeps the full frame.

**Remaining.**
- A live in-dialog preview is optional polish; the applied result is visible immediately in the game view.

**Files.** `CropInsets.cs`, `VdlFrame.cs`, `AppSettings.cs`, `GameProfile.cs`, `GameProfileJson.cs`, `MainWindow.axaml.cs`, `MainWindow.axaml`, `SettingsWindow.*`.

**Tests.** `CropInsetsTests` covers clamping, visible-size rules, legacy fallback, and color validation; `AppSettingsStoreTests` covers per-edge/border roundtrip, clamping, per-game capture, effective application, and live-settings isolation. The frontend smoke test constructs the updated settings window.

**Done when.** Users can match 4DO-style presentation. **Met:** per-edge crop, per-game crop, void border, and color are editable and persist.

---

<a id="feat-vid-07"></a>
### VID-07 - Shader preset system

**Priority:** P1 | **Effort:** L | **Depends on:** FND-06 | **Status:** Pending

**Problem.** 5DO has fixed filters; users cannot load shader chains.

**Add.**
- Preset model: passes, textures, uniforms, scale, feedback.
- Parse common `.slang`/`.glslp` subset.
- GL and Vulkan pass chains.
- Hot reload; per-game preset; fallback.
- Curated MIT/CC0 presets with notices.

**Files.** `FiveDo.Video.OpenGL`, `FiveDo.Video.Vulkan`, new preset parser.

**Tests.** Popular preset; invalid preset; hot reload; performance.

**Done when.** A CRT preset loads at 4K60 and per-game presets persist.

---

<a id="feat-vid-08"></a>
### VID-08 - NTSC composite/S-Video/RF, HDR, gamma

**Priority:** P2 | **Effort:** L | **Depends on:** VID-07 | **Status:** Pending

**Problem.** Best-in-class video needs signal-level filters and HDR.

**Add.**
- NTSC composite/S-Video/RF shaders.
- CRT Royale-style presets.
- HDR output where supported.
- Gamma/color controls.
- Visual validation.

**Files.** Shader presets, renderer.

**Tests.** Test patterns; HDR capability; performance.

**Done when.** Presets work and are documented.

---

<a id="feat-vid-09"></a>
### VID-09 - Video recording

**Priority:** P1 | **Effort:** M | **Depends on:** FND-06 | **Status:** Partial (2026-11-01)

**Problem.** 5DO has screenshots but no video recording.

**Implemented.**
- `GifRecorder` writes animated GIF89a files with a fixed 3-3-2 global palette, NETSCAPE looping, 2-50 fps capture, and a per-frame accumulator that decimates higher-rate sources.
- CLI `run --record-gif <path> [--record-fps 1..60] [--record-frames N]`; `run --json` adds `gifPath`/`gifFrames`.
- **File > Record GIF...** starts/stops a capture, temporarily disables presentation frame skip, and restores the previous setting.
- Frames are captured from the emulated frame sink, so the GIF is deterministic with the run and does not depend on the host renderer.
- A 40-frame PAL Battle Chess capture produced a 20-frame 320x288 GIF that Pillow decoded fully.

**Remaining.**
- MP4/WebM or an FFmpeg pipe with synchronized audio.
- A replay buffer and hotkey start/stop.
- Metadata sidecars and file naming templates.

**Files.** `src/FiveDo.Video/GifRecorder.cs`, `src/FiveDo.Cli/Program.cs`, `src/FiveDo.Frontend.Avalonia/Views/MainWindow.*`.

**Tests.** GIF header/trailer/frame count, rate clamping, frame limit, size-change skip, and empty GIF.

---

<a id="feat-vid-10"></a>
### VID-10 - Overlays, bezels, touch controls

**Priority:** P2 | **Effort:** M | **Depends on:** FND-06 | **Status:** Pending

**Problem.** Console-like/mobile UX needs overlays.

**Add.**
- Per-system/per-game overlays.
- Controller overlay.
- Touch controls for mobile.
- Opacity/scale settings.

**Files.** Frontend, renderer.

**Tests.** Layout at multiple resolutions; input mapping.

**Done when.** Overlays render correctly and do not block input.

---

<a id="feat-vid-11"></a>
### VID-11 - Vulkan surfaces on Linux/macOS

**Priority:** P1 | **Effort:** L | **Depends on:** ECO-04 | **Status:** Pending

**Problem.** Native Vulkan surface is Win32-only.

**Add.**
- X11/Wayland surface.
- macOS Metal or MoltenVK path.
- Compositor integration.
- Validation.

**Files.** `FiveDo.Video.Vulkan`, platform layer.

**Tests.** Window probe on each platform; resize; vsync.

**Done when.** Vulkan presents on Linux/macOS without readback.

---

<a id="feat-vid-12"></a>
### VID-12 - Run-ahead, frame delay, VRR, audio sync

**Priority:** P2 | **Effort:** L | **Depends on:** CORE-14 | **Status:** Pending

**Problem.** Input latency is not optimized.

**Add.**
- 1-2 frame run-ahead.
- Frame-delay slider.
- VRR support.
- Audio sync offset.
- Latency measurement.

**Files.** `CoreRunner`, `FiveDoMachine`, renderer, audio.

**Tests.** Latency measurement; determinism; no audio drift.

**Done when.** Latency is reduced and measured.

---

<a id="feat-vid-13"></a>
### VID-13 - Video test suite and color accuracy

**Priority:** P1 | **Effort:** M | **Depends on:** CORE-11 | **Status:** Pending

**Problem.** 3DO RGB555/CLUT/NTSC behavior needs regression coverage.

**Add.**
- Test patterns (color bars, gradients, CLUT, transparency).
- CPU/GPU parity.
- Color delta report.
- CI artifacts.

**Files.** Tests/probes.

**Tests.** Each pattern; parity threshold.

**Done when.** Color regressions are caught automatically.

---

<a id="feat-vid-14"></a>
### VID-14 - Screenshot gallery and metadata

**Priority:** P2 | **Effort:** S | **Depends on:** FND-06 | **Status:** Done (2026-11-02)

**Problem.** Screenshots are files without an in-app gallery.

**Implemented.**
- `ScreenshotLibrary` parses `<game>-<yyyyMMdd-HHmmss>-f<frame>[-native].png`, falls back to the file timestamp for other names, and sorts newest first.
- `ScreenshotLibrary.List` filters by case-insensitive game text and an inclusive capture-time range; `ListGames` supplies the distinct game names. `Export` copies a capture to a new path and creates the destination directory.
- **File > Screenshot Gallery...** shows decoded thumbnails, game/frame/time/path metadata, a game filter, a date filter (all/today/7/30 days), export via the file picker, and two-click delete. Selecting exactly two captures enables a side-by-side **Compare** window.
- `ScreenshotMetadata` writes an adjacent `<png>.json` sidecar with the renderer, filter, aspect, region, high-resolution flag, crop, void border/color, timing profile, and CPU clock. The gallery reads the sidecar for the selected capture and falls back from a `-native.png` companion to its main capture.
- `AppSettings.ResolveScreenshotDirectory` is shared by capture and the gallery.
- `ScreenshotLibraryTests` covers parsing, native companions, sorting, unparsed names, missing directories, game/date filters, distinct game names, export, and delete. `ScreenshotMetadataTests` covers capture/save/load, native fallback, and invalid sidecars.
- The frontend `--smoke-test` constructs the gallery and compare windows.

**Remaining.**
- None for the documented gallery scope.

**Files.** `src/FiveDo.Host/ScreenshotLibrary.cs`, `ScreenshotMetadata.cs`, `HostJsonContext.cs`, `src/FiveDo.Frontend.Avalonia/Views/ScreenshotGalleryWindow.*`, `ScreenshotCompareWindow.*`, `MainWindow.*`.

**Tests.** Parse; filtering; export; delete; sidecar roundtrip; invalid/missing sidecar; frontend smoke construction.

**Done when.** Users can browse and compare captures. **Met.**

---

<a id="feat-vid-15"></a>
### VID-15 - Direct3D 11 renderer (Windows)

**Priority:** P2 | **Effort:** L | **Depends on:** M9R.1, FND-06 | **Status:** Done (2026-11-02)

**Problem.** The desktop presenter is OpenGL/GLES, with Vulkan as an optional backend. Windows users on drivers or remote sessions where OpenGL is unavailable or slow have no native Direct3D path, even though Direct3D 11 is the best-supported Windows graphics API and the same API ANGLE already uses underneath.

**Add.**
- New `FiveDo.Video.Direct3D` project implementing `IVideoRenderer`/`ICpuFrameRenderer` with Direct3D 11 Feature Level 11_0.
- Two-stage pipeline matching OpenGL/Vulkan: `R16_UINT` indexed pixels, per-line `R8G8B8A8` CLUT, `R32G32_UINT` line parameters -> palette pass at native resolution -> scale/filter pass at output resolution.
- HLSL ports of the palette, scale, CRT, xBR-lv2, and FSR 1.0 (EASU + RCAS) shaders. HQx 2x/3x/4x continue to use the shared CPU `HqxFilter` and are uploaded as a native-resolution source.
- Offscreen render target plus a staging texture for `ReadPixels`, screenshots, and the headless probe.
- DXGI flip-model swapchain (`B8G8R8A8_UNORM`) on a click-through native child HWND (`Direct3DVideoSurface`), with resize, DPI, vsync (`Present(1, 0)`), and tearing support when vsync is off.
- Device-loss/removal recovery: recreate the device and swapchain; if that fails, fall back to OpenGL, then to the CPU presenter.
- `RendererApi.Direct3D11`, `VideoRendererKind.Direct3D11`, `--gl-backend d3d11` (`dx11`, `directx`, `d3d` aliases), Settings > Video "DirectX 11 (Windows)", and `--d3d-probe`/`--d3d-window-probe` JSON + PNG capture.
- OpenGL remains the default on every platform. The Direct3D option is Windows-only and opt-in; a persisted DirectX selection is normalized to OpenGL on other platforms.

**Files.** New `src/FiveDo.Video.Direct3D/` (`Direct3D11Renderer.cs`, `Direct3D11Renderer.Swapchain.cs`, `Shaders.cs` or `.hlsl`), `src/FiveDo.Frontend.Avalonia/Controls/Direct3DVideoSurface.cs`, `D3DProbeRunner.cs`, `D3DProbeOptions.cs`, `GlBackendOption.cs`, `Models/GlBackend.cs`, `Views/MainWindow.axaml(.cs)`, `Views/SettingsWindow.axaml(.cs)`, `src/FiveDo.Host/Models/VideoRendererKind.cs`, `src/FiveDo.Video/Models/RendererApi.cs`, `Directory.Packages.props`, `docs/Direct3D.md`.

**Tests.** HLSL compile/link; CPU/GPU palette parity at 320x240 and 640x480; top-red/bottom-blue orientation; filter fallback; resize/DPI; device-loss fallback; settings/CLI parsing; opt-in `FIVEDO_D3D_TESTS=1` probe integration; 4K timing report.

**Done when.** The Windows DirectX 11 backend renders every existing filter, matches the CPU oracle in Accurate mode, presents without readback through the embedded swapchain, falls back cleanly, and leaves OpenGL as the default with no regression.

**Decision: Direct3D 11, not 12.** D3D12 is a lower-level API, not a higher-quality renderer. The presenter submits only a few full-screen passes per frame; D3D12's explicit command lists, descriptor heaps, root signatures, pipeline state objects, resource barriers, and memory residency add substantial code and failure modes without improving image quality or the measured frame time. D3D11 is fully supported, is the API ANGLE uses underneath, and has simpler debug/WARP/device-loss handling. A D3D12 backend can be added later behind the same `IVideoRenderer` contract if GPU CEL compositing (VID-03) needs compute and explicit scheduling.

**Reference.** `docs/Video.md`, `docs/Vulkan.md`, `FiveDo.Video.OpenGL/OpenGlRenderer.cs`, `FiveDo.Video.Vulkan/VulkanOffscreenRenderer.cs`.

---

## 3.4 Audio

<a id="feat-aud-01"></a>
### AUD-01 - Audio buffer/latency/device/resampler

**Priority:** P1 | **Effort:** M | **Depends on:** FND-06 | **Status:** Partial (2026-11-01)

**Problem.** 5DO exposed only volume; 4DO exposes buffer size.

**Implemented.**
- `OpenAlAudioOutput.EnumerateDevices()` lists OpenAL Soft playback devices through `ALC_ENUMERATE_ALL_EXT`, with a default-device fallback.
- `AppSettings.AudioDevice` persists the selected device; `AudioBufferMilliseconds` selects a 20-500 ms target.
- `AudioBufferConfiguration` derives buffer frames, queue depth, prefill, and ring capacity from the requested latency.
- Settings > Audio adds an output-device combo with **Refresh** and a buffer-latency box. Changing either recreates only the host audio output; the machine, rewind history, and emulation state continue.
- The status bar and `run --json` report estimated latency alongside queued/buffered/underrun counters.
- CLI `--audio-device <name>`, `--audio-buffer 20..500`, and `audio devices [--json]`.

**Remaining.**
- Resampler quality selection.
- Exclusive mode and a sync-offset control.
- A real-device crackle sweep at 20-200 ms on Tier B hardware.

**Files.** `OpenAlAudioOutput.cs`, `AppSettings.cs`, Settings UI, CLI.

**Tests.** `AudioBufferConfigurationTests` covers clamping, monotonic growth, and queue duration; `AppSettingsStoreTests` covers device/buffer roundtrip and clamping. Device enumeration was exercised on the local Windows host (two devices listed).

---

<a id="feat-aud-02"></a>
### AUD-02 - Audio recording UI, mixer, spectrum

**Priority:** P1 | **Effort:** M | **Depends on:** FND-06 | **Status:** Partial (2026-11-01)

**Problem.** CLI WAV exists; UI does not.

**Implemented.**
- `TeeAudioSink` forwards the DSP stream to live output and a `WavAudioSink` simultaneously.
- **File > Record Audio...** picks a WAV path and starts/stops recording without interrupting emulation. Audio settings changes keep the recorder in the tee, and stop/dispose finalizes the WAV header.
- Completion reports sample count and duration; file names default to `<game>-<timestamp>.wav`.
- `TeeAudioSinkTests` covers forwarding, reset, and null destinations.

**Remaining.**
- Per-channel mute/volume and a spectrum analyzer.
- Format options (float/24-bit/MP3/FLAC) and recording metadata.
- A recording indicator in the status bar and a hotkey.

**Files.** `src/FiveDo.Audio/TeeAudioSink.cs`, `src/FiveDo.Audio/WavAudioSink.cs`, `src/FiveDo.Frontend.Avalonia/Views/MainWindow.*`.

**Tests.** Tee forwarding; WAV validity through the existing audio analyzer tests.

---

<a id="feat-aud-03"></a>
### AUD-03 - DSP debugger

**Priority:** P2 | **Effort:** M | **Depends on:** UX-21 | **Status:** Pending

**Problem.** DSP is a black box outside traces.

**Add.**
- Registers/memory view.
- Instruction ring.
- Breakpoints.
- Semaphore/status.
- Step/run.

**Files.** `FiveDo.Dsp`, debugger UI.

**Tests.** Register/memory view; breakpoint; step.

**Done when.** DSP bugs can be debugged visually.

---

<a id="feat-aud-04"></a>
### AUD-04 - Audio golden tests vs Opera/hardware

**Priority:** P1 | **Effort:** M | **Depends on:** CORE-11 | **Status:** Pending

**Problem.** Tests check non-silence/peak/RMS, not spectral parity.

**Add.**
- Golden WAVs per title/checkpoint.
- Spectral cosine/RMS envelope comparison.
- CI gate.
- Tolerance policy.

**Files.** Tests/tools.

**Tests.** Each golden; regression threshold.

**Done when.** Audio regressions are caught automatically.

---

<a id="feat-aud-05"></a>
### AUD-05 - CD-DA mixing and volume

**Priority:** P1 | **Effort:** S | **Depends on:** SYS-01 | **Status:** Done (2026-11-01)

**Problem.** CD-DA needs independent mixing.

**Implemented.**
- CD-DA frames are decoded from 2352-byte audio sectors and mixed into the DSP sample stream every 44.1 kHz tick.
- `FiveDoMachine.CdAudioVolume` (0..1) provides a live mix level; `CdAudioMuted` silences only the CD-DA contribution.
- Samples saturate to 16-bit; playback stops cleanly at the end LBA or on a bad sector.
- `AppSettings.CdAudioVolume`/`CdAudioMuted` persist the mix; Settings > Audio adds a slider and mute check.
- `run --cd-volume 0..1` and `--mute-cd` control headless captures.
- CD save-state section v4 stores the mix level and mute flag; loading a v3 state restores the defaults.

**Remaining.**
- A known CD-DA title capture and a DSP+CD-DA WAV belong to SYS-01 evidence.

**Files.** Audio, `CdromDevice`, `FiveDoMachine`, `AppSettings`, Settings UI, CLI.

**Tests.** Mix level and mute; settings roundtrip/validation; CD state roundtrip.

**Done when.** CD-DA and DSP mix correctly. **Met.**

---

## 3.5 UX

<a id="feat-ux-01"></a>
### UX-01 - Rewind

**Priority:** P1 | **Effort:** L | **Depends on:** CORE-14 | **Status:** Done

**Implemented (2026-11-01).** `FiveDo.Host.Rewind.RewindOptions` configures a 30-300 second
history, keyframe interval, and memory cap (256 MB default). `RewindBuffer` captures
compressed keyframes and the per-frame PBUS packet, loads the nearest keyframe, replays the
recorded input to an arbitrary target frame, and clears itself when the machine timeline
jumps. `CoreRunner.Rewind` presents the restored frame and updates the runner frame counter;
`CanRewind`/`RewindOldestFrame`/`RewindNewestFrame`/`RewindWindow` expose the window. The
desktop shell binds `R` to a one-second step and `Shift+F9` to one frame, and Settings >
Emulation controls the enable flag and history length. Tests cover deterministic replay
(byte-identical save state against a control run), clamping, discontinuity handling, the
memory cap, disabled mode, and the runner integration.

**Problem.** No rewind.

**Add.**
- Compressed keyframe ring + input deltas.
- Keyframe every N frames.
- 30-300s length, memory cap.
- Hotkey/step/frame-by-frame.
- OSD indicator; audio mute.
- State format support.

**Files.** New `FiveDo.Host/Rewind/`, `CoreRunner`, UI.

**Tests.** 60s rewind; determinism; memory cap; no stutter.

**Done when.** Rewind works at 60fps with deterministic resume.

---

<a id="feat-ux-02"></a>
### UX-02 - Fast-forward / turbo

**Priority:** P1 | **Effort:** S | **Depends on:** FND-01 | **Status:** Done

**Implemented (2026-11-01).** `CoreRunner.SpeedMultiplier` accepts 1-10 and runs that many
emulated frames per presentation tick. Intermediate frames skip VDLP rendering, the final
frame is presented normally, and audio is detached, reset, and reattached while the
multiplier is above one so the output stays muted instead of pitch-shifted. Input and
emulated state are unchanged. `FrameSkipStateTests` verifies that three-of-four rendered
frames produce identical guest/chip state sections; only the presentation frame buffer
differs. The desktop shell holds `Tab` to fast-forward, stops it on
key release/pause/stop/reset, and shows the active multiplier in the status line. Settings >
Emulation selects the multiplier. Tests cover range validation, frame counting, and audio
muting.

**Problem.** Frame skip is not fast-forward.

**Add.**
- 2x-10x/uncapped speed.
- Hold/toggle hotkey.
- Audio mute/scale.
- OSD.
- No state divergence.

**Files.** `CoreRunner`, UI.

**Tests.** Speed accuracy; audio; input; state.

**Done when.** Fast-forward works without changing emulated state.

---

<a id="feat-ux-03"></a>
### UX-03 - Netplay

**Priority:** P1 | **Effort:** XL | **Depends on:** CORE-11 | **Status:** Pending

**Problem.** No netplay; 3DO is a six-player console.

**Add.**
- Lockstep + input delay; optional rollback.
- Lobby/direct IP/NAT traversal/relay.
- 2-6 players; spectators; chat.
- Desync detection/resync.
- BIOS/disc/settings validation.
- CLI test mode.

**Files.** New `FiveDo.Netplay/`, UI.

**Tests.** Two-player online; latency; 30min no desync; six-player local.

**Done when.** A full online session completes without desync.

---

<a id="feat-ux-04"></a>
### UX-04 - Cheat engine

**Priority:** P1 | **Effort:** M | **Depends on:** FND-06 | **Status:** Done (2026-11-01)

**Problem.** `--poke` is diagnostic only.

**Add.**
- Raw, typed, conditional, and button-activated RAM code types.
- Per-game JSON cheat files under the configurable cheat directory.
- Desktop manager and CLI enable/disable/add/remove/import/export.
- Per-frame application through `FiveDoMachine.BeforeFrame`, so state loads, rewind,
  and fast-forward keep the configured cheats active.
- Documented legal scope: 3DO has no standard RAM cheat format; Game Guru is a save
  editor, so 5DO uses a documented raw/typed syntax and ships no code databases.

**Files.** `src/FiveDo.Cheats/`, `src/FiveDo.Host/Models/AppSettings.cs`,
`src/FiveDo.Cli/Program.cs`, `src/FiveDo.Frontend.Avalonia/Views/CheatsWindow.*`,
`docs/Cheats.md`.

**Tests.** Raw/typed writes; memory and button conditions; logical operators; malformed
code; disabled entries; duplicate names; state-load reapplication; store roundtrip;
CLI add/list/enable/disable/remove.

**Done when.** Cheats work and survive save/load as configured.

---

<a id="feat-ux-05"></a>
### UX-05 - Achievements

**Priority:** P2 | **Effort:** L | **Depends on:** UX-03 | **Status:** Pending

**Problem.** No achievements.

**Add.**
- RetroAchievements if 3DO support exists; otherwise local.
- Login/offline cache.
- Progress/notifications.
- Per-game list.

**Files.** New `FiveDo.Achievements/`, UI.

**Tests.** Login/offline; unlock; notification.

**Done when.** Achievements work or the integration is documented as unavailable.

---

<a id="feat-ux-06"></a>
### UX-06 - TAS tools

**Priority:** P1 | **Effort:** L | **Depends on:** UX-01 | **Status:** Partial

**Implemented (2026-11-01).** `FiveDo.Tas` owns the versioned `.5domovie` container:
per-frame PBUS packets, an optional starting save state, CRC-32 checksums, a JSON
header with game/disc/BIOS identity and timing options, and Brotli-compressed input.
`MovieRecorder` wraps live input, `MoviePbusSource` replays packets, `MovieEditor`
supports branch/trim/append plus per-frame `SetFrame`/`SetButton`/`InsertFrame`/
`RemoveFrame`, and `MovieVerifier` compares the final frame hash, ARM PC, and state
hash. The CLI adds `movie info|record|play|verify|branch` and
`run --movie-record/--movie-play/--movie-verify/--movie-no-state`; verification
failure exits 2. The desktop shell adds **Emulation > Record Movie.../Play
Movie.../Stop Movie**, a player-1 input display, automatic pause at the end of a movie,
and **File > Movie Editor...** for frame-by-frame player-1 button edits, insert/delete,
trim, and Save As. New `PbusDecoder` exposes decoded controller states. TAS lag frames
are exposed by the UX-23 input display. Lua, an input timeline, multiplayer display, and
the full TAS UI remain. See [`docs/Tas.md`](Tas.md).

**Problem.** Input recording exists; no movie format/input display/lag counter.

**Add.**
- Movie format (header/input/state).
- Record/play/pause/frame advance/rewind.
- Input display; lag counter.
- Branch/split; export/import.
- Lua scripting.
- CLI `movie record/play/verify`.

**Files.** New `FiveDo.Tas/`, UI/CLI.

**Tests.** Record/replay; cross-machine verify; branch.

**Done when.** A full TAS movie verifies bit-identically.

---

<a id="feat-ux-07"></a>
### UX-07 - Controller profiles and per-game input

**Priority:** P1 | **Effort:** M | **Depends on:** FND-06 | **Status:** Partial (2026-11-01)

**Problem.** One global input profile.

**Implemented.**
- Per-game PBUS device overrides (`InputDeviceOverrides`) with `ResolveInputDevices` and `ApplyInputDevices(input, gameId)`.
- Settings > Emulation can store the six-device list for the current game or edit the global list.
- Known lightgun/arcade/trackball titles seed an override automatically when `AutoInputDevices` is enabled; an explicit override wins.

**Remaining.**
- Named input profiles with per-game assignment of bindings, not only devices.
- Deadzone/response curves.
- Hot-plug prompts and device remapping.
- Profile import/export.

**Files.** `FiveDo.Input`, `AppSettings`, UI.

**Tests.** Profile switch; per-game; hot-plug; roundtrip.

**Done when.** Users can switch profiles and per-game mappings.

---

<a id="feat-ux-08"></a>
### UX-08 - Gamepad navigation and big picture

**Priority:** P1 | **Effort:** L | **Depends on:** FND-06 | **Status:** Pending

**Problem.** No 10-foot UI.

**Add.**
- Full gamepad navigation.
- Big-picture layout.
- On-screen hints.

**Files.** Frontend.

**Tests.** Navigate all screens with a gamepad; launch a game.

**Done when.** The app is usable without a keyboard/mouse.

---

<a id="feat-ux-09"></a>
### UX-09 - Localization

**Priority:** P1 | **Effort:** M | **Depends on:** FND-07 | **Status:** Pending

**Problem.** English-only.

**Add.**
- Resource-based strings.
- At least 7 languages.
- Language selector.
- Community workflow.

**Files.** Frontend resources.

**Tests.** Key completeness; fallback; switch.

**Done when.** UI runs in multiple languages.

---

<a id="feat-ux-10"></a>
### UX-10 - Themes and accessibility

**Priority:** P2 | **Effort:** M | **Depends on:** FND-06 | **Status:** Pending

**Problem.** No theme/accessibility options.

**Add.**
- Dark/light/accent.
- UI scale.
- High contrast.
- Screen-reader labels.
- Keyboard-only navigation.

**Files.** Frontend styles/views.

**Tests.** Theme switch; scale; accessibility labels.

**Done when.** The UI is accessible and themeable.

---

<a id="feat-ux-11"></a>
### UX-11 - Disc browser UI and game info

**Priority:** P1 | **Effort:** M | **Depends on:** FND-06 | **Status:** Done 2026-11-01

**Problem.** `fs` is CLI-only; no game info dialog.

**Add.**
- Tree/list browser.
- Extract file/directory/disc.
- Open directory.
- Game info dialog (ID, region, publisher, year, checksum, fixes).

**Files.** `src/FiveDo.Host/DiscBrowser.cs`, `src/FiveDo.Frontend.Avalonia/Views/DiscBrowserWindow.*`, `src/FiveDo.Frontend.Avalonia/Views/DiscInfoWindow.*`, `MainWindow.*`, `OperaFileSystem`.

**Tests.** `tests/FiveDo.Tests/Host/DiscBrowserTests.cs` covers tree order/depth, metadata totals, path lookup, file/directory/all extraction, sanitization, a real ISO file through `Open`, and missing images.

**Done when.** Users can browse discs without the CLI. **Done:** File > Disc Browser... and File > Disc Info... use the model; extraction is path-contained and reports counts; the frontend smoke test constructs both windows.

---

<a id="feat-ux-12"></a>
### UX-12 - BIOS/NVRAM manager UI

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-04, SYS-14 | **Status:** Done 2026-11-01

**Problem.** No BIOS/NVRAM management UI.

**Add.**
- BIOS list/import/assign/validate.
- NVRAM list/import/export/reset/backup.
- Per-game assignment.

**Implementation.** `BiosInventory` validates and imports `.bin`/`.rom` images,
reuses an identical MD5, and avoids overwriting a different image. `NvramInventory`
enumerates the shared/per-game layout, validates each image, backs up one image or the
whole layout, imports, resets, and deletes with atomic writes. **File > BIOS / NVRAM
Manager...** provides the two-tab desktop UI, sets BIOS1/BIOS2, pins or clears the
current game's BIOS MD5, and requires a second click for reset/delete/import-overwrite.

**Files.** `src/FiveDo.Host/BiosInventory.cs`, `src/FiveDo.Host/NvramInventory.cs`,
`src/FiveDo.Frontend.Avalonia/Views/BiosNvramManagerWindow.*`, `MainWindow.*`.

**Tests.** `tests/FiveDo.Tests/Host/BiosNvramInventoryTests.cs` covers import/copy/
reuse/name collisions/invalid sizes, sanitization, NVRAM validation, layout
enumeration, backup, layout-wide backup, import, reset, delete, and backup naming.

**Done when.** Users can manage BIOS/NVRAM safely.

---

<a id="feat-ux-13"></a>
### UX-13 - Library metadata, favorites, playtime

**Priority:** P1 | **Effort:** L | **Depends on:** SYS-17 | **Status:** Partial (2026-11-01)

**Problem.** Library has covers + 4DO metadata only.

**Add.**
- Redump/No-Intro/IGDB/LaunchBox metadata.
- Screenshots/manuals/videos.
- Favorites/recently played/playtime.
- Sorting/filtering/collections.
- Offline cache.

**Files.** `GameLibraryScanner`, new providers, UI.

**Tests.** Metadata cache; favorites; playtime; offline.

**Done when.** >=90% of the collection has rich metadata.

**Implemented (2026-11-01).** `LibraryState`/`LibraryStateStore` persist favorites,
launch counts, last-played timestamps, and play seconds in `library-state.json`;
path-keyed records migrate to game-ID keys. `LibraryPlaytimeTracker` measures only
running emulation, stops on pause/stop/close, and flushes every 60 seconds.
`LibraryMetadata`/`LibraryMetadataStore` read an optional offline
`library-metadata.json` overlay (title, developer, publisher, year, genre, region,
players, description) keyed by game ID or path; Settings > Paths chooses the file.
The scanner now carries the 4DO database's year, publisher, and region fields.
`LibraryQuery` provides All/Favorites/Played/Unplayed filters and
Title/Recently played/Most played/Release year/Publisher sorts, and search covers the
new metadata. The game list and standalone library window add star toggles,
metadata/play-time text, and the filter/sort controls. **File > Game Metadata...**
edits the current disc's offline record (title, developer, publisher, year, genre,
regions, players, description), upserts by game ID/path, and can clear it; the library
rescans after saving. **Remaining:** external metadata providers/import,
screenshots/manuals/videos, and the >=90% rich-metadata target. Tests:
`tests/FiveDo.Tests/Host/LibraryStateTests.cs` covers the catalog, playtime, filters,
and metadata upsert/remove; `ParserFuzzTests` and the rest of the suite keep the total at
1,458.

---

<a id="feat-ux-14"></a>
### UX-14 - Auto-update and release channels

**Priority:** P1 | **Effort:** M | **Depends on:** ENG-10 | **Status:** Pending

**Problem.** No update path.

**Add.**
- GitHub releases check.
- Stable/beta channels.
- Download/verify/install.
- Rollback.
- UI/CLI.

**Files.** New updater, packaging.

**Tests.** Update from previous version; signature failure; offline.

**Done when.** Users can update safely.

---

<a id="feat-ux-15"></a>
### UX-15 - Portable mode, multi-instance, config profiles

**Priority:** P1 | **Effort:** S | **Depends on:** FND-02 | **Status:** Done (2026-11-01)

**Problem.** No portable mode; single config.

**Implemented.**
- `PortablePaths` resolves the data directory from `portable.flag`, `--portable`, or `FIVEDO_DATA_DIR`.
- A portable run keeps `settings.json`, NVRAM, `states/`, cheats, library state, and logs beside the executable.
- `FIVEDO_DATA_DIR` provides separate per-instance configurations without environment-specific code.
- `--config-profile <name>` / `FIVEDO_PROFILE` selects `<data>/profiles/<name>/` for a named configuration; `settings profiles` lists them.
- The desktop and CLI strip `--portable` and `--config-profile` before command dispatch and log the resolved directory.
- Tests cover portable, per-user, explicit-override, named-profile, and profile-name sanitization.
- `InstanceLock` opens `<data>/5do.instance.lock` exclusively at desktop startup, records the owning PID/version, and shows a clear conflict window instead of letting two processes write the same settings/NVRAM. `FIVEDO_ALLOW_MULTIPLE=1` bypasses the check; separate profiles or data directories use separate locks.
- Settings > Paths adds a profile selector, **Set default** (`default-profile.txt` for the next launch), **Create** (copies current settings), **Open folder**, **Import...**, and **Export...**. `PortablePaths.ProfileName` reads the environment first, then the default-profile file.

**Remaining.**
- Installer integration for creating/removing `portable.flag` belongs to UX-16.

**Files.** `AppSettingsStore`, `Program.cs`.

**Tests.** Portable run; no AppData writes; `InstanceLockTests` covers exclusive acquisition, conflict reporting, stale-file recovery, and missing/writable directories.

**Done when.** A USB-stick run works without touching AppData.

---

<a id="feat-ux-16"></a>
### UX-16 - Installers, signing, notarization, file associations

**Priority:** P1 | **Effort:** L | **Depends on:** ENG-10 | **Status:** Pending

**Problem.** Only portable archives.

**Add.**
- MSIX/Inno Setup, dmg/pkg, AppImage/Flatpak/Snap/AUR.
- Code signing/notarization.
- Windows/macOS file associations.
- Uninstall.

**Files.** `packaging/`, CI.

**Tests.** Install/uninstall; associations; signature.

**Done when.** A normal user can install and launch from the OS.

---

<a id="feat-ux-17"></a>
### UX-17 - Crash report, compatibility report, log bundle

**Priority:** P1 | **Effort:** M | **Depends on:** FND-08 | **Status:** Partial (2026-11-01)

**Problem.** Bug reporting exists; compatibility evidence is manual.

**Implemented.**
- **Help > Save Diagnostics Bundle...** captures the live save state and CPU frame, then writes a scrubbed zip with settings, logs, environment, and manifest.
- CLI `diagnostics bundle [--out path] [--state path] [--frame path] [--movie path]` produces the same archive headlessly.
- Privacy scrub removes home/app/temp paths, the account name, and path-valued settings keys; the manifest records the scrub count.

**Remaining.**
- Live input-replay capture in the desktop bundle.
- Compatibility report form and optional upload.
- A one-click crash handler that writes the bundle automatically.

**Files.** Diagnostics/frontend/CLI.

**Tests.** Bundle contents; scrub; missing evidence.

**Done when.** A user can submit a reproducible report in one action.

---

<a id="feat-ux-18"></a>
### UX-18 - Onboarding and verification

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-20 | **Status:** Pending

**Problem.** First-run guide exists; no disc/BIOS verification.

**Add.**
- Guided setup.
- BIOS/disc health check.
- Redump match.
- Clear errors and next steps.

**Files.** Frontend/welcome.

**Tests.** Fresh install; missing BIOS; bad disc.

**Done when.** A new user reaches gameplay without confusion.

---

<a id="feat-ux-19"></a>
### UX-19 - OSD, notifications, quick menu

**Priority:** P1 | **Effort:** S | **Depends on:** FND-06 | **Status:** Done (2026-11-01)

**Problem.** No OSD/toasts/quick menu.

**Add.**
- Toasts for save/load/rewind/fast-forward/screenshot.
- Quick menu (gamepad/keyboard).
- Configurable position/opacity.

**Files.** Frontend.

**Implementation.** `FiveDo.Host.Osd` adds a bounded, time-expiring `OsdNotifications`
list and validated `OsdOptions`; `AppSettings` persists enable/position/opacity/duration
and the quick-menu toggle. The game view shows colour-coded notifications for save/load,
slot changes, rewind, fast-forward, screenshots, reset, and settings. `F4` or the
gamepad Select+Start chord opens a quick menu that pauses the game and offers
resume/pause, save/load, rewind, fast-forward, screenshot, reset, cheats, settings,
stop, and close. Up/Down/Home/End and Enter navigate; F4/Escape close, and closing
resumes only when the game was running. Settings > Video edits all OSD options. Tests
cover queue capacity/expiry/validation, settings mapping, the gamepad chord, and the
hotkey roundtrip.

**Tests.** Show/hide; no input blocking.

**Done when.** Actions give clear feedback.

---

<a id="feat-ux-20"></a>
### UX-20 - Per-game settings UI

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-18 | **Status:** Partial (2026-11-01)

**Problem.** Only fix/filter overrides.

**Implemented.**
- Settings > Emulation edits a stored profile for the current game: ARM/CLIO/CEL/CD timing, CD speed, CPU multiplier, region, profile label, CD-DA volume/mute, BIOS MD5, fix mask, video filter, high-resolution mode, six PBUS devices, and NVRAM mode/version.
- Save, Delete, Import JSON, and Export JSON actions; imported values populate the controls and are normalized/clamped.
- `run/trace --game-profile <json>` applies the timing/region/fix/high-resolution values headlessly before explicit options; the desktop applies the full profile at machine creation.
- The profile is used when the machine is created; the dialog states that a restart is required for an already-running machine.

**Remaining.**
- Cheat/shader-chain fields in the profile.
- Inherit/reset controls per field.
- Applying a changed profile to the current machine without a restart.

**Tests.** Profile JSON roundtrip/normalization; effective-settings application; CLI application.

**Files.** Settings UI, `GameProfile`.

**Tests.** Override resolution; reset; roundtrip.

**Done when.** Every machine option can be set per game.

---

<a id="feat-ux-21"></a>
### UX-21 - Debugger UI

**Priority:** P2 | **Effort:** L | **Depends on:** CORE-15 | **Status:** Pending

**Problem.** CLI traces only.

**Add.**
- CPU registers/disasm/breakpoints/watchpoints/call stack.
- Memory/VRAM viewer/editor.
- MADAM/CEL state.
- DSP view.
- CD/VDLP views.
- Step/run.

**Files.** New debugger UI, diagnostics.

**Tests.** Breakpoint; step; memory edit; state.

**Done when.** Common bugs can be debugged without external tools.

---

<a id="feat-ux-22"></a>
### UX-22 - Trace viewer and performance profiler

**Priority:** P2 | **Effort:** M | **Depends on:** UX-21 | **Status:** Pending

**Problem.** Traces are text files.

**Add.**
- Timeline/filter/search.
- CPU/CEL/DSP/frame charts.
- Export.
- Compare runs.

**Files.** Frontend/diagnostics.

**Tests.** Load large traces; filter; chart.

**Done when.** Traces are visually inspectable.

---

<a id="feat-ux-23"></a>
### UX-23 - Input display and lag counter

**Priority:** P1 | **Effort:** S | **Depends on:** UX-06 | **Status:** Done (2026-11-01)

**Problem.** No input display/lag counter.

**Implemented.**
- The game view has a bottom-left overlay with the emulated frame count, lag-frame count, presentation-skip count, disc state, and controller buttons decoded from the current PBUS packet.
- Lag semantics follow TAS tooling: a frame is a lag frame when the guest does not perform a PBUS DMA read during that frame.
- **F2** or **View > Input Display** toggles it; the choice persists in `ShowInputDisplay`.
- **Settings > Video > Input display player** selects controller port 1-6; `InputDisplayPlayer` persists and updates a running core.
- `CoreRunner` captures the selected controller plus lag/skip counts in `CoreStatus`; the display has no effect on emulation or input.

**Files.** Frontend, `CoreRunner`, `CoreStatus`, `AppSettings`, `Madam`.

**Tests.** `CoreRunnerTests` covers lag frames, selected-player status, and port bounds; `MadamTests` covers PBUS read counts; `AppSettingsStoreTests` covers player clamping. The overlay is compile-validated and manually checked; automated screenshot comparison remains a future polish item.

**Done when.** TAS/streaming users can see input/lag.

---

<a id="feat-ux-24"></a>
### UX-24 - Lua scripting and automation

**Priority:** P3 | **Effort:** L | **Depends on:** UX-06 | **Status:** Pending

**Problem.** No scripting.

**Add.**
- Sandboxed Lua API: input, memory, state, screenshot.
- Script manager.
- Examples/docs.

**Files.** New scripting host.

**Tests.** API; sandbox; state access.

**Done when.** A script can automate a test/TAS.

---

## 3.6 Ecosystem

<a id="feat-eco-02"></a>
### ECO-02 - WebAssembly/browser build

**Priority:** P3 | **Effort:** XL | **Depends on:** none | **Status:** Out of scope (2026-11-02)

**Dropped.** WebAssembly/browser support is not planned. The specification below is
retained for history only.

**Problem.** No browser build.

**Add.**
- .NET WASM host.
- File loading, canvas, audio.
- Demo/homebrew scope.
- Performance measurement.

**Files.** New `src/FiveDo.Web/`.

**Tests.** Boot a demo; input; audio.

**Done when.** A browser demo runs at a usable speed.

---

<a id="feat-eco-03"></a>
### ECO-03 - Android/iOS frontend

**Priority:** P3 | **Effort:** XL | **Depends on:** ECO-04 | **Status:** Out of scope (2026-11-02)

**Dropped.** Android and iOS are not planned. The specification below is retained for
history only.

**Problem.** No mobile app.

**Add.**
- Avalonia mobile or native shell.
- Touch controls.
- Storage/SAF.
- Performance.

**Files.** New mobile project.

**Tests.** Mid-range phone; controller; save.

**Done when.** A playable mobile build exists.

---

<a id="feat-eco-04"></a>
### ECO-04 - Linux/macOS validation, Flatpak/AppImage, macOS gamepad/Metal

**Priority:** P1 | **Effort:** L | **Depends on:** ENG-01 | **Status:** Pending

**Problem.** Published but unvalidated.

**Add.**
- Smoke/soak on Linux/macOS.
- Flatpak/AppImage.
- macOS gamepad backend.
- Metal/MoltenVK path.

**Files.** CI/packaging/frontend.

**Tests.** Each platform; controller; fullscreen.

**Done when.** Claimed platforms are proven.

---

<a id="feat-eco-05"></a>
### ECO-05 - Raspberry Pi/arm64/GLES optimization

**Priority:** P1 | **Effort:** L | **Depends on:** ENG-02 | **Status:** Out of scope (2026-11-02)

**Dropped.** Raspberry Pi and Pi-specific GLES optimization are not planned. Desktop
arm64 coverage remains under ECO-04/M10. The specification below is retained for history
only.

**Problem.** Tier B unmeasured.

**Add.**
- GLES 3.1 path.
- arm64 profiling.
- Pi 4/5 targets.
- Thermal/power notes.

**Files.** Renderer, benchmarks.

**Tests.** Pi 5 1080p60; Pi 4 720p60/1080p30.

**Done when.** Measured and documented.

---

<a id="feat-eco-06"></a>
### ECO-06 - Plugin API

**Priority:** P3 | **Effort:** L | **Depends on:** FND-01 | **Status:** Pending

**Problem.** No extension points beyond interfaces.

**Add.**
- Stable plugin interfaces for video/input/cheats/netplay.
- Sample plugin.
- Versioning/docs.

**Files.** Core interfaces.

**Tests.** Sample plugin; version mismatch.

**Done when.** A third-party plugin can extend 5DO.

---

<a id="feat-eco-07"></a>
### ECO-07 - Headless server / HTTP / WebSocket API

**Priority:** P3 | **Effort:** M | **Depends on:** FND-04 | **Status:** Pending

**Problem.** No remote automation.

**Add.**
- Start/stop/load/save/screenshot/input endpoints.
- Auth.
- JSON schema.
- Docs.

**Files.** New server project.

**Tests.** Endpoints; auth; load.

**Done when.** A script can control 5DO remotely.

---

<a id="feat-eco-08"></a>
### ECO-08 - 3DO modem/network emulation

**Priority:** P3 | **Effort:** L | **Depends on:** CORE-04 | **Status:** Pending

**Problem.** Optional preservation.

**Add.**
- Scope note; device model; test title.
- No core regression.

**Files.** New device.

**Tests.** Test title; no regression.

**Done when.** Documented or explicitly rejected.

---

<a id="feat-eco-09"></a>
### ECO-09 - 3DO CD player / Video CD

**Priority:** P3 | **Effort:** M | **Depends on:** SYS-01 | **Status:** Pending

**Problem.** Optional preservation.

**Add.**
- Boot the player; play a disc.
- Document scope.

**Files.** Storage/CD.

**Tests.** Player boots; disc plays.

**Done when.** Works or is explicitly out of scope.

---

<a id="feat-eco-10"></a>
### ECO-10 - Documentation wiki and compatibility portal

**Priority:** P1 | **Effort:** M | **Depends on:** SYS-17 | **Status:** Pending

**Problem.** Docs are in-repo; no public per-title portal.

**Add.**
- Public docs/wiki.
- Per-title compatibility pages.
- Search.
- Contribution guide.

**Files.** Docs/website.

**Tests.** Links; build; search.

**Done when.** Users can look up a title and see status/evidence.

---

<a id="feat-eco-11"></a>
### ECO-11 - Community workflow

**Priority:** P1 | **Effort:** S | **Depends on:** ENG-10 | **Status:** Pending

**Problem.** No formal community process.

**Add.**
- Discord/issue templates.
- Contribution guide.
- Triage/release notes.
- Bounties/roadmap voting.

**Files.** `.github/`, docs.

**Tests.** Template validation.

**Done when.** Contributors know how to help.

---

## 3.7 Engineering, legal, trust

<a id="feat-eng-01"></a>
### ENG-01 - CI matrix, nightly sweep, coverage, static analysis, SBOM, reproducible builds

**Priority:** P1 | **Effort:** M | **Depends on:** FND-05 | **Status:** Pending

**Add.**
- All RIDs build/test.
- Nightly 594-disc sweep.
- Coverage gate.
- Analyzers/warnings-as-errors.
- SBOM.
- Reproducible builds.

**Files.** `.github/workflows`, `Directory.Build.props`, docs.

**Tests.** CI green; artifacts.

**Done when.** Every change is validated automatically.

---

<a id="feat-eng-02"></a>
### ENG-02 - Performance: interpreter, JIT, GPU CEL, async shaders

**Priority:** P1 | **Effort:** XL | **Depends on:** CORE-03 | **Status:** Pending

**Add.**
- Profile x64/arm64.
- Interpreter hot path.
- Optional M12 JIT.
- GPU CEL after parity.
- Async shader compile.
- Budget gates.

**Files.** CPU/Madam/renderer/benchmarks.

**Tests.** p99; allocations; Tier B desktop.

**Done when.** Tier A 4K60; Tier B desktop 1080p60; no regressions.

---

<a id="feat-eng-03"></a>
### ENG-03 - Legal/licensing/attribution

**Priority:** P1 | **Effort:** M | **Depends on:** none | **Status:** Pending

**Add.**
- Counsel review.
- SPDX/notices.
- Embedded boot/shaders/DB/cover art review.
- RSA bypass review.
- Donationware compliance.

**Files.** `LICENSE`, `THIRD-PARTY-NOTICES.md`, `docs/Licensing.md`, `docs/ReleaseLegalReview.md`.

**Tests.** Legal checklist; release gate.

**Done when.** Public distribution is cleared.

---

<a id="feat-eng-04"></a>
### ENG-04 - Accessibility

**Priority:** P2 | **Effort:** M | **Depends on:** FND-06 | **Status:** Pending

**Add.**
- Screen-reader labels.
- High contrast.
- UI scale.
- Keyboard-only.
- Reduced motion.

**Files.** Frontend.

**Tests.** Accessibility audit.

**Done when.** The app is usable with assistive tech.

---

<a id="feat-eng-05"></a>
### ENG-05 - Telemetry transparency and privacy controls

**Priority:** P2 | **Effort:** S | **Depends on:** FND-08 | **Status:** Pending

**Add.**
- Clear disclosure.
- Opt-in/opt-out.
- Local-only mode.
- No game/user data.

**Files.** Stats/logging.

**Tests.** Opt-out; payload.

**Done when.** Users understand and control telemetry.

---

<a id="feat-eng-06"></a>
### ENG-06 - Stability soak / crash-free

**Priority:** P1 | **Effort:** M | **Depends on:** CORE-11 | **Status:** Pending

**Add.**
- 1M frames/24h.
- All titles.
- No unhandled exceptions.
- Memory/leak checks.

**Files.** `tools/release-soak.ps1`, CI.

**Tests.** Soak report.

**Done when.** The soak passes.

---

<a id="feat-eng-07"></a>
### ENG-07 - Documentation refresh

**Priority:** P1 | **Effort:** S | **Depends on:** none | **Status:** Pending

**Add.**
- Update docs for every feature.
- Screenshots.
- Compatibility/known issues.
- Roadmap status.

**Files.** `docs/`, `README.md`.

**Tests.** Link check; docs build.

**Done when.** Docs match the code.

---

<a id="feat-eng-08"></a>
### ENG-08 - Localization infrastructure

**Priority:** P1 | **Effort:** S | **Depends on:** FND-07 | **Status:** Pending

**Add.**
- Resource pipeline.
- Pseudo-locale.
- Community workflow.
- CI key check.

**Files.** Frontend resources.

**Tests.** Key completeness; pseudo-locale.

**Done when.** Adding a language is a resource-only change.

---

<a id="feat-eng-09"></a>
### ENG-09 - Code coverage and benchmark regression

**Priority:** P1 | **Effort:** M | **Depends on:** ENG-01 | **Status:** Pending

**Add.**
- Coverage report/gate.
- Per-PR benchmark.
- Baseline comparison.
- Trend dashboard.

**Files.** CI/benchmarks.

**Tests.** Gate behavior.

**Done when.** Silent quality decay is caught.

---

<a id="feat-eng-10"></a>
### ENG-10 - Release engineering and signing

**Priority:** P1 | **Effort:** M | **Depends on:** ENG-03 | **Status:** Pending

**Add.**
- Signed artifacts.
- Checksums.
- Release notes.
- Rollback.
- Upgrade tests.

**Files.** `packaging/`, CI.

**Tests.** Install/upgrade/rollback.

**Done when.** Releases are professional and verifiable.

---

## 4. File/project work map

| Project | Key files | Features |
|---|---|---|
| `FiveDo.Core` | `EmulatorTuning.cs`, `EmulatorConstants.cs`, `StateFormat.cs`, `StateWriter/Reader.cs`, `CompatibilityState.cs` | FND-01/03, CORE-08/14, SYS-03/07/08/10/11/13 |
| `FiveDo.Cpu` | `Arm60.cs`, `ArmAlu.cs`, `ArmDisassembler.cs` | CORE-01/07, SYS-10/12 |
| `FiveDo.Memory` | `FiveDoMemory.cs` | SYS-07, CORE-06 |
| `FiveDo.Clio` | `Clio.cs` | CORE-02/06/07, SYS-03 |
| `FiveDo.Madam` | `Madam.cs`, `CelEngine.cs`, `CelRasterizer.cs`, `CcbParser.cs` | CORE-03/06, SYS-11/15, VID-01/02/03 |
| `FiveDo.Cdrom` | `CdromDevice.cs` | CORE-04, SYS-01/02/06/09/21/22 |
| `FiveDo.Dsp` | `Dsp.cs` | CORE-05, AUD-03 |
| `FiveDo.Sport` | `Sport.cs` | CORE-06 |
| `FiveDo.Xbus` | `Xbus.cs` | CORE-04/06/07 |
| `FiveDo.Storage` | `BiosImage.cs`, `DiscImageFactory.cs`, `CueSheet.cs`, `Chd/ChdDiscImage.cs`, `GameDatabase.cs`, `OperaFileSystem.cs` | SYS-01/04/05/06/17/19/20/21/22 |
| `FiveDo.Input` | `PbusEncoder.cs`, `InputManager.cs`, `GamepadMapper.cs`, `InputProfile.cs` | SYS-15/16, UX-07 |
| `FiveDo.Video` | `Vdlp.cs`, `FrameAssembler.cs`, `VdlpFrameMailbox.cs`, `ViewportCalculator.cs` | VID-01/04/05/06/13 |
| `FiveDo.Video.OpenGL` | `OpenGlRenderer.cs`, shaders | VID-07/08/09/12 |
| `FiveDo.Video.Vulkan` | `VulkanOffscreenRenderer*.cs` | VID-03/07/08/11/12 |
| New `FiveDo.Video.Direct3D` | `Direct3D11Renderer*.cs`, HLSL shaders | VID-15 |
| `FiveDo.Audio` | `OpenAlAudioOutput.cs`, `WavAudioSink.cs`, `AudioAnalyzer.cs` | AUD-01/02/04/05 |
| `FiveDo.Host` | `CoreRunner.cs`, `AppSettings.cs`, `NvramStore.cs`, `SaveStateSlotManager.cs`, `GameLibraryScanner.cs`, `StateInspector.cs` | FND-01/02/06/08, CORE-15, SYS-14/18, UX-01/02/03/13/14/15/17/18 |
| `FiveDo.Cli` | `Program.cs`, `CliJsonModels.cs`, `CliJsonContext.cs` | FND-04, all CLI options |
| `FiveDo.Frontend.Avalonia` | `MainWindow.axaml(.cs)`, `SettingsWindow`, `InputBindingsWindow`, `GameLibraryWindow`, `SaveSlotsWindow`, `LogsWindow` | FND-06/07, UX-04..24, VID-09/10/14/15 |
| `FiveDo.Diagnostics` | `Logging`, `InMemorySerilogSink`, `BugReportSink` | FND-08, UX-17, ENG-05 |
| New `FiveDo.Netplay` | lobby/rollback | UX-03 |
| New `FiveDo.Cheats` | code types/DB | UX-04 |
| New `FiveDo.Tas` | movie/input/lua | UX-06/24 |
| New `FiveDo.Achievements` | provider/UI | UX-05 |
| `tests/FiveDo.Tests` | unit/integration/golden | all |
| `tools/` | sweep/probe/harness | CORE-10/11/13, ENG-06 |
| `benchmarks/` | perf | ENG-02/09 |
| `.github/workflows` | CI | ENG-01/09/10 |
| `packaging/` | installers | UX-16, ENG-10 |

---

## 5. Implementation order

### Phase 0 - Foundations (1-2 weeks)

- FND-01 feature/tuning model.
- FND-02 settings versioning.
- FND-03 state v3.
- FND-04 CLI conventions.
- FND-05 differential harness skeleton.
- FND-06 settings framework.
- FND-07 localization pipeline.
- FND-08 diagnostics/crash bundle.

**Exit:** every new option has one source of truth; state/settings are forward-compatible; differential harness runs one title.

### Phase 1 - Core accuracy (4-8 weeks)

- CORE-01 ARM timing.
- CORE-02 CLIO timers.
- CORE-03 CEL busy.
- CORE-04 CD timing.
- CORE-05 DSP timing/thread.
- CORE-06 SPORT/XBUS/DMA.
- CORE-07 interrupts.
- CORE-08 profiles.
- CORE-11 differential harness.
- CORE-13 compatibility failures.
- CORE-16 regression suite.

**Exit:** golden titles match Opera or have documented divergences; 594-disc sweep passes; no default regression.

### Phase 2 - System/disc/peripherals (4-8 weeks)

- SYS-01 CD-DA.
- SYS-02 ODE.
- SYS-03 PAL.
- SYS-04 BIOS manager.
- SYS-14 NVRAM.
- SYS-15 input devices.
- SYS-17/18 game DB/auto profile.
- SYS-07/08/09/10/11/13 core options.
- SYS-20/21/22 disc health/CD drive/conversion.

**Exit:** every local disc boots with the right BIOS/region/input; PAL/CD-DA/lightgun/trackball/mouse work.

### Phase 3 - Video/audio (4-8 weeks)

- VID-01 true high-res.
- VID-04/05/06 480i/PAL/crop.
- VID-07 shaders.
- VID-09 recording.
- VID-13 tests.
- AUD-01/02/04/05.

**Exit:** true high-res prototype; shader presets; audio settings; video/audio golden tests.

### Phase 4 - UX (6-12 weeks)

- UX-01 rewind.
- UX-02 fast-forward.
- UX-03 netplay.
- UX-04 cheats.
- UX-06 TAS.
- UX-07/08 controller/big picture.
- UX-09/10 localization/themes.
- UX-11/12 disc/BIOS/NVRAM.
- UX-13/14/15/16 library/update/portable/installers.
- UX-17/18/19/20 reports/onboarding/OSD/per-game.
- UX-21/22/23/24 debugger/trace/input/Lua.

**Exit:** feature parity with modern emulators; installable; localized; netplay/TAS/debugger.

### Phase 5 - Ecosystem (8-16 weeks)

- ECO-04 platform (Linux/macOS).
- ECO-10/11 docs/community.
- ECO-06/07/08/09 optional; ECO-02/03/05 dropped (2026-11-02).

**Exit:** RetroArch core; validated platforms; public compatibility portal.

### Phase 6 - Engineering/release (ongoing)

- ENG-01..10.

**Exit:** signed, reproducible, covered, soaked, legal.

---

## 6. Test plan

### 6.1 Unit tests

- Every new class/algorithm.
- Boundary/error cases.
- State save/load.
- CLI parsing/JSON.
- Settings migration.

### 6.2 Integration tests

- Boot a synthetic ROM through `FiveDoMachine`.
- Boot a real BIOS/disc (opt-in).
- Exercise each device/peripheral.
- Save/load roundtrip.
- Renderer parity.

### 6.3 Differential tests

- 5DO vs Opera on the same BIOS/disc/input/frame.
- Frame hash, audio hash, state hash, CD transcript.
- Known-divergence allowlist.

### 6.4 Hardware/test ROM tests

- CPU/MADAM/VDLP/DSP/CD/SPORT/CLIO.
- Expected output/frame hash.

### 6.5 Manual tests

- Each title in the compatibility matrix.
- Each input device.
- Each video/audio setting.
- Each platform.
- Netplay/TAS/debugger.

### 6.6 Performance tests

- Synthetic and real-content.
- p50/p95/p99, allocations.
- 4K GPU/present.
- Pi.

### 6.7 Security tests

- Fuzz parsers.
- Malformed states/discs/CUE/CHD.
- No OOB/unhandled exceptions.

---

## 7. Release checklist

- [ ] All P0 features done.
- [ ] All P1 features done or explicitly deferred with a reason.
- [ ] 594/594 local discs load/render; >=95% Playable/Good.
- [ ] Differential harness green.
- [ ] 2,000+ tests; coverage gate.
- [ ] 24h soak.
- [ ] 4K60 Tier A; 1080p60 Tier B desktop.
- [ ] Windows/Linux/macOS x64/arm64 and Pi validated.
- [ ] Installers/signing/notarization/auto-update/portable/file associations.
- [ ] Localization/themes/accessibility.
- [ ] Legal sign-off.
- [ ] Docs/compatibility portal/contribution guide.
- [ ] SBOM/reproducible builds.

---

## 8. Reference map

### 8.1 Opera libretro

| Topic | File |
|---|---|
| Core options | `References/opera-libretro-master/libretro_core_options.c` |
| Input devices | `References/opera-libretro-master/lr_input.c` |
| CD-ROM/CD-DA/ODE | `References/opera-libretro-master/libopera/opera_cdrom.c` |
| ARM timing | `References/opera-libretro-master/libopera/opera_arm.c` |
| MADAM/CEL timing | `References/opera-libretro-master/libopera/opera_madam.c` |
| CLIO timers | `References/opera-libretro-master/libopera/opera_clio.c`, `opera_clock.c` |
| Region | `References/opera-libretro-master/libopera/opera_region.c` |
| State chunks | `References/opera-libretro-master/libopera/opera_state.c` |
| Test harness | `References/opera-libretro-master/tools/test_harness.c` |

### 8.2 4DO

| Topic | File |
|---|---|
| Settings | `References/4DO-master/FourDO/Properties/Settings.settings` |
| Menus/dialogs | `References/4DO-master/FourDO/UI/Main.Designer.cs` |
| Disc browser | `References/4DO-master/FourDO/UI/DiscBrowser/` |
| Game info | `References/4DO-master/FourDO/UI/GameInformation.cs` |
| CPU clock/audio/high-res | `References/4DO-master/FourDO/UI/Settings.Designer.cs` |
| CD drive | `References/4DO-master/CDLib/` |

### 8.3 5DO current code

| Topic | File |
|---|---|
| ARM timing | `src/FiveDo.Cpu/Arm60.cs:24` |
| CEL timing | `src/FiveDo.Machine/FiveDoMachine.cs:479-483` |
| CD | `src/FiveDo.Cdrom/CdromDevice.cs` |
| CUE | `src/FiveDo.Storage/CueSheet.cs` |
| CHD | `src/FiveDo.Storage/Chd/ChdDiscImage.cs` |
| Memory | `src/FiveDo.Core/EmulatorConstants.cs:34` |
| BIOS | `src/FiveDo.Storage/BiosImage.cs`; CLI `Program.cs:220` |
| Settings | `src/FiveDo.Host/Models/AppSettings.cs` |
| Input | `src/FiveDo.Input/PbusEncoder.cs` |
| Video | `src/FiveDo.Video/Vdlp.cs` |
| State | `src/FiveDo.Core/StateFormat.cs` |
| CLI | `src/FiveDo.Cli/Program.cs` |
| Frontend | `src/FiveDo.Frontend.Avalonia/` |

### 8.4 5DO docs

- `docs/Status.md`
- `docs/ImplementationPlan.md`
- `docs/ReferenceStrategy.md`
- `docs/Compatibility.md`
- `docs/HighResolutionCelResearch.md`
- `docs/SaveStates.md`
- `docs/CliReference.md`
- `docs/Configuration.md`
- `docs/TestCorpus.md`
- `docs/Performance.md`
- `docs/Vulkan.md`
- `docs/Direct3D.md` (new; added with VID-15)
- `docs/KnownIssues.md`
- `docs/ReleaseChecklist.md`

---

*Use this document as the build sheet. When a feature lands, mark it done in the Master Dashboard checklist and in Part II, add the tests, and update the relevant `docs/` page. The goal is not to implement everything at once; it is to implement each feature in a way that keeps 5DO deterministic, accurate, and trustworthy.*

---

<a id="part-iii"></a>
# Part III — Best-in-the-World Strategy and Gap Analysis

> Reference comparison, gap analysis, critical path, Opera parity audit, and session evidence.

**Status:** living roadmap and gap analysis  
**Snapshot:** 2026-11-02  
**Scope:** every pending enhancement that matters for 3DO emulation quality, compatibility, features, platforms, and trust  
**Detailed build spec:** [Part II - Feature Specification and Backlog](#part-ii-feature-specification-and-backlog) - implementation-level feature sheets, files, tests, and acceptance criteria<br>
**Companion docs:** [`README.md`](https://github.com/purelogiccode/5DO/blob/master/README.md), [`docs/Status.md`](Status.md), [`docs/ImplementationPlan.md`](#master-dashboard), [`docs/ReferenceStrategy.md`](ReferenceStrategy.md), [`docs/Compatibility.md`](Compatibility.md), [`docs/HighResolutionCelResearch.md`](HighResolutionCelResearch.md)

---

## 0. Executive summary

5DO is already the most modern **standalone** 3DO emulator:

- Pure C# / .NET 10, no native emulator DLL.
- Windows, Linux, and macOS publishes for x64 and arm64.
- ISO, BIN, CUE+BIN, and CHD as first-class formats.
- Avalonia desktop frontend plus a strong headless CLI.
- OpenGL/GLES presenter (default), optional Vulkan backend, and optional Direct3D 11 backend.
- 4K output, accurate/nearest/sharp-bilinear/CRT/xBR/HQx/FSR filters.
- Compressed deterministic save states, ten slots, thumbnails.
- Rewind (30-300 s) and 2x-10x fast-forward with deterministic input replay.
- 518-record game database, per-game fix/filter overrides.
- 1,473 passing tests, a 594-disc compatibility sweep, and measured 4K60 performance.
- Trimmed and NativeAOT publishes, structured logging, and bug reporting.

But "best in the world" is not a feature-count contest. The remaining gap is in five places:

1. **Core accuracy.** Opera has moved ahead on ARM cycle classes, CLIO timer scheduling, MADAM CEL busy timing, CD-ROM timing, CD-DA, PAL, and peripheral devices. 5DO still inherits FreeDO's simpler timing model.
2. **Universal compatibility.** 5DO still needs manual BIOS selection and has BIOS/region-sensitive failures. The best emulator should pick the right BIOS, region, fix, input device, and clock automatically.
3. **Modern UX.** 5DO now has rewind, fast-forward, and a per-game cheat engine, but still lacks netplay, achievements, TAS tools, shader presets, video recording, debugger UI, localization, auto-update, and installers.
4. **Ecosystem.** Opera is a libretro core and inherits RetroArch's shaders, netplay, achievements, rewind, run-ahead, overlays, and recording. 5DO is deliberately standalone, so the features that matter must be built into its own shell rather than inherited from a frontend.
5. **Trust at scale.** 5DO has excellent unit tests, but not yet an automated full-collection differential harness against Opera, a hardware test-ROM suite, fuzzing, or a public compatibility dashboard.

### The 12 highest-leverage moves

1. **Port Opera's CD-ROM timing and command behavior** (cycle-based sector ready, DIPIR, read-ahead, subcode, media access).
2. **Add CD-DA/Red Book audio and multi-track CUE/CHD support.**
3. **Port Opera's CEL busy timing** so CEL work consumes emulated time and status polling behaves correctly.
4. **Evaluate and expose ARM60 `Ncycle=2`** behind an accuracy profile, with differential evidence.
5. **Build a BIOS/region/fix/input auto-profile system** so every disc "just works".
6. **Add PAL/NTSC region modes**, including PAL1/PAL2 timing and 288-line output.
7. **Add the missing 3DO input devices**: mouse, lightgun, arcade lightgun, flightstick, and Orbatak trackball.
8. **Add rewind, fast-forward, netplay, cheats, achievements, and TAS tools.**
9. **Add a shader-preset system and true high-resolution CEL rendering.**
10. **Validate and polish the standalone cross-platform release** on Windows, Linux, and macOS, and publish the compatibility portal.
11. **Build the full-collection differential harness and hardware test-ROM suite.**
12. **Finish the UX shell**: disc browser, BIOS/NVRAM manager, localization, debugger, installers, auto-update, and portable mode.

### The guiding principle

> Accuracy and compatibility are the foundation. Features are multipliers. A fast, pretty emulator that runs the wrong code is not the best 3DO emulator; a correct emulator with a great shell is.

### Priority definitions

- **P0 - Correctness:** accuracy, compatibility, BIOS/region/disc/peripheral support. These block the "best" claim.
- **P1 - Expected:** features modern players and creators assume: rewind, fast-forward, netplay, cheats, TAS, shaders, recording, debugger, localization, installers.
- **P2 - Multipliers:** ecosystem and platform reach: plugin API, server API, docs/community.
- **P3 - Long-term:** engineering, legal, accessibility, performance moonshots, preservation.

---

## 1. What "best 3DO emulator in the world" means

Use seven pillars. Every roadmap item must improve at least one, and no item may regress another.

| Pillar | What it means | How we measure it |
|---|---|---|
| **Accuracy** | The guest sees the same CPU, bus, CEL, DSP, CD, and timing behavior as real hardware, or as the best available reference. | Differential frame/audio/state hashes vs Opera; hardware test ROMs; instruction conformance; timing tests. |
| **Compatibility** | Every known 3DO disc loads, renders, accepts input, saves, and completes. | 594/594 local discs load and render; >=95% Playable/Good; >=90% have a scripted playthrough. |
| **Performance** | Full speed with headroom on modest hardware, with no audio/video stutter. | Core p99 <=8 ms; 4K60 on Tier A; 1080p60 on Tier B desktop; zero steady-state allocations. |
| **Features** | The emulator does what players and creators expect in 2026. | Rewind, fast-forward, netplay, cheats, achievements, TAS, shaders, recording, debugger, library, localization. |
| **Usability** | A new user can install, configure, and play without reading a manual. | One-click BIOS-less boot; auto BIOS/region/fix; guided setup; sensible defaults; installers; auto-update. |
| **Portability** | The same core runs on the desktop platforms 3DO fans actually use. | Windows/Linux/macOS x64/arm64; no browser/mobile/Pi target. |
| **Trust** | The project is honest, reproducible, legal, and stable. | CI gates, signed releases, SBOM, compatibility evidence, legal sign-off, crash-free soak, no telemetry surprises. |

### Success metrics (current -> target)

| Metric | Current | Target |
|---|---:|---:|
| Local discs that load | 589 / 594 (99.2%) | **594 / 594 (100%)** |
| Local discs that render | 576 / 594 (97.0%) | **594 / 594 (100%)** |
| Playable/Good titles | 12 validated, 15 triaged | **>=95% of 594** |
| Scripted full playthroughs | 5 showcase titles | **>=90% of titles** |
| Automated differential coverage | Harness + Lemmings smoke + 5-title showcase | **Full collection, nightly** |
| Tests | 1,350 | **2,000+ with coverage gate** |
| Core p99 (worst title) | 7.40 ms (Road Rash race) | **<=8 ms all titles** |
| 4K60 on Tier A | Measured | **Sustained, all filters within budget** |
| 1080p60 on Tier B desktop | Unmeasured | **Measured and documented** |
| Platforms claimed | Windows validated; 6 RIDs published | **All claimed platforms validated** |
| UX feature set | Rewind/fast-forward/cheats done; no netplay/TAS/shaders | **All P1 UX items complete** |
| Crash-free soak | 36,000 frames | **1,000,000+ frames / 24 h** |

---

## 2. Current 5DO snapshot

### 2.1 What 5DO already does well

| Area | Evidence |
|---|---|
| Architecture | One project per subsystem; pure managed C#; no native emulator dependency; deterministic core thread. |
| CPU | ARM60 interpreter with banked registers, FIQ/IRQ, disassembler, and a full instruction test suite. |
| Memory | 3 MB visible RAM/VRAM, 2 MB ROM, 32 KB NVRAM, reserved high-res mirrors, save-state ownership. |
| Video | Two-stage OpenGL/GLES presenter, optional Vulkan, Direct3D 11 planned, 4K output, accurate parity, xBR/HQx/FSR/CRT. |
| Audio | Exact 44,100 samples per ARM second, OpenAL Soft, WAV capture, tap, analysis, audio-driven pacing. |
| Storage | ISO, BIN, CUE+BIN, CHD with parent/child, prefetch, Opera file-system parser, disc dumps. |
| Save states | Versioned, CRC-protected, Brotli-compressed sections, ten slots, thumbnails, disc-identity warnings. |
| CLI | `run`, `trace`, `info`, `disc`, `fs`, `bios`, `audio`, `state`, `disasm`, `bench`, `selftest`; JSON contracts. |
| Diagnostics | KPRINT, CD/CLIO/FIQ/CEL/PBUS traces, watchpoints, RAM/DSP dumps, instruction rings, frame capture. |
| Tests | 1,473 tests; deterministic golden hashes; zero-allocation assertions; GPU parity tests. |
| Packaging | Six RIDs; trimmed IL; opt-in NativeAOT; portable archives; OpenAL notices. |
| Documentation | Deep architecture, compatibility, performance, video, audio, save-state, and troubleshooting docs. |

### 2.2 Current limits that block "best in the world"

| Limit | Consequence |
|---|---|
| FreeDO-derived timing model (`Ncycle=4`, simple CLIO timers, synchronous CEL) | Timing-sensitive titles may diverge from Opera/hardware; CEL status polling is unrealistic. |
| CD-ROM timing is approximate; no CD-DA, multi-track, subcode, or ODE | Some discs and audio tracks cannot work; CD waits remain a compatibility risk. |
| No PAL/NTSC region modes | European/PAL titles such as Battle Chess cannot run correctly. |
| BIOS selection is region/known-title based, but PAL/NTSC region modes and the full BIOS-aware sweep are still pending | Some PAL titles still need manual region handling; BIOS evidence is partial beyond the smoke set. |
| Peripheral input beyond the joypad | Mouse, lightgun, arcade lightgun, flightstick, and Orbatak trackball records are implemented; per-device calibration and scripted gameplay validation are still pending. |
| No netplay, achievements, TAS | The emulator still lacks the social and creator features expected in 2026, although rewind, fast-forward, and per-game cheats are now implemented. |
| No shader presets, video recording, overlays, or true high-res CEL | Video quality is good but not best-in-class. |
| No debugger UI, disc browser UI, BIOS/NVRAM manager | Creators and power users are forced into the CLI. |
| No localization, themes, auto-update, installers, portable mode | Usability lags 4DO/RMG/RPCS3. |
| Linux/macOS/arm64 unvalidated | The "cross-platform" claim is not yet proven. |
| Legal/attribution sign-off | Resolved 2026-11-02: FreeDO modified GNU LGPL, donationware; no commercial use. |

### 2.3 What 5DO already does better than the references

- **Pure managed C#** with no native emulator DLL; easier to audit, port, and embed.
- **Cross-platform Avalonia shell** instead of Windows-only WinForms.
- **CHD as a first-class format**, including parent/child images.
- **OpenGL/GLES (default) + optional Vulkan/Direct3D 11** with 4K output and modern filters.
- **Compressed save states** (0.6-2.0 MB vs ~25 MB raw).
- **Headless CLI with JSON, traces, watchpoints, and deterministic tests**.
- **Trimmed and NativeAOT publishes**.
- **Structured logging and asynchronous bug reports**.
- **518-record embedded game database** and per-game fix/filter overrides.
- **Honest documentation** with measured budgets and known issues.

Do not regress these while adding the missing pieces.

---

## 3. Reference emulator profiles

### 3.1 FreeDO

**Position:** the original 3DO emulator and the historical base for 4DO, Opera, and 5DO.

**Strengths**
- The reference implementation for much of the 3DO core: ARM, CLIO, MADAM, VDLP, SPORT, DSP, XBUS, CD.
- Simple plugin architecture (audio/input) and a small, understandable codebase.
- The source of many compatibility fixes and quirks that still matter.

**Weaknesses**
- Old, primarily Windows-oriented, and not actively maintained.
- No CHD, no modern renderer, no 4K, no shader presets, no CLI.
- Approximate CD timing; no CD-DA/ODE/PAL/peripherals.
- No public automated test suite or differential harness.

**What to copy:** the core behavioral baseline and the discipline of preserving reference quirks.  
**What not to copy:** the timing approximations and the old UX.

### 3.2 4DO

**Position:** a Windows WinForms frontend around a FreeDO-derived core, with useful consumer features.

**Strengths**
- BIOS1 + optional BIOS2/Kanji font ROM.
- CD image and physical CD drive support.
- Disc Browser for the 3DO file system.
- Game Information dialog.
- Ten save slots, auto-load last save, auto-open last game.
- Void-area patterns, gray border, smooth resize, aspect ratio, auto-crop, HQ2x/3x/4x, double-resolution rendering.
- Seven UI languages.
- Audio volume and buffer-size settings.
- CPU clock control.
- High-resolution mode.
- Inactive-window pause/ignore-input options.
- Screenshots, pause, frame advance, reset.

**Weaknesses**
- Windows-only WinForms; no cross-platform.
- CHD is not a documented 4DO format; image support is narrower than 5DO's.
- No modern shader pipeline, no 4K, and no built-in netplay/rewind/cheats/TAS.
- No libretro ecosystem.
- Limited input-device support (joypad-focused).
- Old codebase and UI.

**What to copy:** BIOS2/Kanji, disc browser, game info, audio buffer, CPU clock, auto-crop/void area, localization, auto-load/auto-open.  
**What not to copy:** the Windows-only shell and the lack of modern ecosystem features.

### 3.3 Opera libretro

**Position:** a maintained fork of 4DO, packaged as a libretro core. It is the current **accuracy and feature benchmark** for the 3DO core.

**Core options and features** (`References/opera-libretro-master/libretro_core_options.c`)
- BIOS (rom1) and Font/Kanji (rom2), including an embedded Opera Boot ROM.
- CPU overclock: 0.5x to 4.0x (6.25-50 MHz).
- Memory capacity: 2/4/8/14/15 MB DRAM and 1/2 MB VRAM.
- Random number generator seed (random or fixed values).
- CD speed: unbounded, 1x, 2x, 4x, 8x, 16x, 24x, 32x.
- Region/mode: NTSC 320x240@60, PAL1 320x288@50, PAL2 384x288@50.
- VDLP pixel format: 0RGB1555, RGB565, XRGB8888.
- VDLP bypass CLUT (faster, potentially wrong colors).
- HiRes CEL rendering (2x).
- MADAM matrix engine: hardware or software.
- OperaOS SWI HLE.
- Threaded DSP (experimental).
- NVRAM storage: per-game or shared.
- NVRAM version 0-9.
- Active input devices: 0-8.
- Hide lightgun crosshairs.
- Debug output (KPRINT).

**Input devices** (`lr_input.c`)
- 3DO Joypad.
- 3DO Flightstick.
- 3DO Mouse.
- 3DO Lightgun.
- Arcade Lightgun.
- Orbatak Trackball.
- Up to eight ports; arcade buttons and crosshairs.

**Disc features** (`opera_cdrom.c`)
- Cycle-based data-sector timing and read-ahead.
- DIPIR, tagged status, chunks, subcode.
- CD-DA / Red Book audio with read-ahead and audio clock.
- ODE (virtual disc changer) commands and media launch/reset.
- TOC from disc metadata; multiple tracks.
- CD speed control.
- Media-access behavior.

**Accuracy improvements over FreeDO/4DO**
- ARM `Ncycle=2` with datasheet/SDK references.
- CEL busy pacing at ~9 million pixels/second.
- Per-timer CLIO scheduling with slack and carry.
- Deterministic PRNG.
- Versioned state chunks.
- Headless test harness.

**Weaknesses**
- Libretro-only: the standalone UX is RetroArch's, not Opera's.
- No built-in netplay/achievements/rewind/run-ahead/shaders; it relies on RetroArch.
- No standalone library, installer, auto-update, or debugger UI.
- Core options are spread across RetroArch menus.
- No 4K/Vulkan standalone presenter; it uses the frontend.

**What to copy:** the core accuracy work, CD-DA/ODE, PAL, input devices, memory capacity, CPU clock, random seed, CD speed, SWI HLE, threaded DSP, NVRAM per-game, embedded boot option, and the "core options" model.  
**What not to copy:** the libretro-only UX and the reliance on RetroArch for basic features.

### 3.4 RMG and RPCS3 (UX/product benchmarks, not 3DO references)

They are in `References/` as modern emulator product examples. Use them for patterns, not 3DO behavior.

**RMG**
- ROM browser with cover art and metadata.
- Netplay server list, direct connect, and tunneling guidance.
- Per-game settings and controller profiles.
- Qt6 UI, SDL3 input, Vulkan, portable packaging.
- Simple, focused settings.

**RPCS3**
- Per-game configuration and patch manager.
- Save-state manager, trophy/achievement manager.
- Debugger, RSX capture, shader cache, performance overlay.
- Compatibility list, update checker, themes, localization.
- Mature release engineering and community workflow.

**What to copy:** per-game profiles, patch/cheat manager, debugger, compatibility dashboard, update checker, themes, controller profiles, netplay lobby, and release polish.

---

## 4. Head-to-head comparison

Legend: **Yes** = implemented and usable; **Partial** = implemented with limits or only via an external frontend; **No** = not available; **N/A** = not applicable.

### 4.1 Core and platform

| Capability | 5DO | FreeDO | 4DO | Opera libretro | 5DO target |
|---|---|---|---|---|---|
| Pure managed core | **Yes** | No (C++) | No (C++) | No (C++) | Keep yes |
| Windows | Yes | Yes | Yes | Yes (RetroArch) | Yes |
| Linux | Partial (published, unvalidated) | No | No | Yes (RetroArch) | Yes |
| macOS | Partial (published, unvalidated) | No | No | Yes (RetroArch) | Yes |
| arm64 | Partial (published, unvalidated) | No | No | Yes (RetroArch) | Yes |
| CHD | **Yes** | No | No | Yes | Keep yes |
| ISO/BIN/CUE | Yes | Yes | Yes | Yes | Yes |
| Multi-track / CD-DA | **No** | No | No | **Yes** | Yes |
| CD timing accuracy | Partial (FreeDO model) | Partial | Partial | **Yes** (cycle-based) | Yes |
| ODE / disc swap | No | No | No | **Yes** | Yes |
| BIOS1 | Yes | Yes | Yes | Yes | Yes |
| BIOS2 / Kanji | Partial (core supports, no UI/CLI) | No | **Yes** | **Yes** | Yes |
| Embedded boot ROM | No | No | No | **Yes** | Yes (license permitting) |
| Region / PAL | **No** | No | No | **Yes** (NTSC/PAL1/PAL2) | Yes |
| CPU overclock | Partial (core clock exists, no UI/CLI) | No | **Yes** | **Yes** | Yes |
| Memory capacity | No | No | No | **Yes** | Yes |
| Random seed control | No | No | No | **Yes** | Yes |
| CD speed control | No | No | No | **Yes** | Yes |
| MADAM hardware/software | No | No | No | **Yes** | Yes |
| SWI HLE | No | No | No | **Yes** | Yes |
| Threaded DSP | No | No | No | **Yes** (optional) | Yes |
| VDLP bypass CLUT | No | No | No | **Yes** | Yes |
| NVRAM per-game | **Yes** | No | No | **Yes** | Yes |
| Input devices | Joypad only | Joypad | Joypad | **Joypad, mouse, lightgun, arcade lightgun, flightstick, trackball** | Yes |
| Six players | Yes | Partial | Yes | Yes | Yes |
| Save states | Yes | Yes | Yes | Yes | Yes |
| State interop | No | No | No | No | Yes (4DO/Opera import/export) |
| Deterministic | **Yes** | Partial | Partial | Partial | Keep yes |
| Headless CLI | **Yes** | No | No | No (test harness only) | Keep yes |
| Tests/CI | **Yes** | No | No | Partial | Keep and expand |

### 4.2 Video and audio

| Capability | 5DO | FreeDO | 4DO | Opera libretro | 5DO target |
|---|---|---|---|---|---|
| OpenGL/GLES presenter | **Yes** | No | No | Via RetroArch | Yes |
| Vulkan presenter | Partial (optional, Windows surface) | No | No | Via RetroArch | Yes (Linux/macOS too) |
| 4K output | **Yes** | No | No | Via RetroArch | Yes |
| Accurate / nearest / sharp-bilinear | Yes | Partial | Partial | Via frontend | Yes |
| CRT filter | Yes | No | No | Via shaders | Yes |
| xBR / HQx / FSR | Yes | No | HQx only | Via shaders | Yes |
| User shader presets | **No** | No | No | **Yes** (RetroArch) | Yes |
| NTSC composite / RF / HDR | No | No | No | Via shaders | Yes |
| True high-res CEL | Partial (line-double) | Partial | Partial (2x) | Partial (2x) | Yes (true 640x480) |
| PAL output | No | No | No | Yes | Yes |
| Auto-crop / overscan / void area | No | No | **Yes** | Via frontend | Yes |
| Video recording | No | No | No | Via RetroArch | Yes |
| Overlays / bezels | No | No | No | Via RetroArch | Yes |
| Exact 44.1 kHz DSP | **Yes** | No (~44.4 kHz) | No (~44.4 kHz) | Yes | Keep yes |
| CD-DA audio | No | No | No | **Yes** | Yes |
| Audio buffer/latency settings | No | Partial | **Yes** | Via frontend | Yes |
| Audio recording UI | No (CLI WAV only) | No | No | Via frontend | Yes |
| Audio analysis | **Yes** (CLI) | No | No | No | Yes + UI |
| DSP debugger | No | No | No | No | Yes |

### 4.3 Input, UX, and ecosystem

| Capability | 5DO | FreeDO | 4DO | Opera libretro | 5DO target |
|---|---|---|---|---|---|
| Game library | **Yes** | No | No | Via frontend | Yes |
| Cover art | Yes | No | No | Via frontend | Yes |
| Disc browser UI | No (CLI only) | No | **Yes** | Via frontend | Yes |
| Game info dialog | Partial (library metadata) | No | **Yes** | Via frontend | Yes |
| BIOS manager | No | Partial | **Yes** | **Yes** | Yes |
| NVRAM manager | No | No | Partial | **Yes** | Yes |
| Per-game settings | Partial (fix/filter only) | No | No | **Yes** (core options) | Yes |
| Save slots / thumbnails | **Yes** | Partial | Yes | Via frontend | Yes |
| Auto-load last save | No | No | **Yes** | Via frontend | Yes |
| Auto-open last game | No | No | **Yes** | Via frontend | Yes |
| Rewind | **Yes** | No | No | **Yes** (RetroArch) | Yes |
| Fast-forward | **Yes** | No | No | **Yes** (RetroArch) | Yes |
| Run-ahead | No | No | No | **Yes** (RetroArch) | Yes |
| Netplay | **No** | No | No | **Yes** (RetroArch) | Yes |
| Cheats | **Yes** (raw/typed/conditional/button, per-game, UI/CLI) | No | No | Partial (stub) / RetroArch | Yes |
| Achievements | No | No | No | **Yes** (RetroArch, if supported) | Yes |
| TAS tools | Partial (input recording) | No | No | Via RetroArch | Yes |
| Debugger UI | No (CLI only) | No | No | Via RetroArch | Yes |
| Shader presets | No | No | No | **Yes** (RetroArch) | Yes |
| Video recording | No | No | No | **Yes** (RetroArch) | Yes |
| Localization | **No** | No | **Yes** (7 languages) | Via frontend | Yes |
| Themes | No | No | No | Via frontend | Yes |
| Gamepad navigation / big picture | No | No | No | Via frontend | Yes |
| Auto-update | No | No | No | Via frontend | Yes |
| Installers | No (portable archives) | No | **Yes** (Inno Setup) | Via frontend | Yes |
| Portable mode | **No** | No | **Yes** | Via frontend | Yes |
| File associations | Partial (Linux `.desktop`) | No | Yes | Via frontend | Yes |
| Libretro core | **No** | No | No | **Yes** | Not planned; 5DO stays standalone |
| WebAssembly / browser | No | No | No | No | Optional |
| Android/iOS | No | No | No | **Yes** (RetroArch) | Optional |
| Raspberry Pi | Unvalidated | No | No | **Yes** (RetroArch) | Yes |
| Plugin API | No | Partial (plugins) | No | N/A | Yes |
| Headless server API | No | No | No | No | Optional |

### 4.4 Indicative scorecard

Scores are subjective and intended for prioritization, not marketing. They compare the **current state**, not the roadmap.

| Pillar | 5DO | FreeDO | 4DO | Opera libretro |
|---|---:|---:|---:|---:|
| Core accuracy | 7.0 | 5.0 | 6.0 | **9.0** |
| Compatibility | 8.0 | 6.0 | 7.0 | **9.0** |
| Video | **9.0** | 4.0 | 6.0 | 8.0 |
| Audio | 8.0 | 6.0 | 6.0 | **9.0** |
| Input | 6.0 | 5.0 | 6.0 | **9.0** |
| UX | 7.0 | 3.0 | 7.0 | 8.0 |
| Ecosystem | 5.0 | 3.0 | 4.0 | **10.0** |
| Portability | 8.0 | 2.0 | 3.0 | **9.0** |
| Trust/tests | 7.0 | 5.0 | 6.0 | **9.0** |
| **Overall** | **7.2** | **4.3** | **5.8** | **8.9** |

**Target after this roadmap:** 5DO at **9.5+** in every pillar, with the best standalone UX in 3DO emulation.

---

## 5. Gap analysis and pending enhancements

This is the master backlog. Every item is a checkbox so the file can be used as a living plan.

Effort: **S** = under 3 developer-days, **M** = 3-10, **L** = 10-30, **XL** = 30-90+.

### 5.1 P0: Core accuracy and compatibility

These are the items that decide whether 5DO is *correct*, not just pleasant.

| ID | Enhancement | Why / reference | Done when | Effort |
|---|---|---|---|---|
| [~] **CORE-01** | ARM60 cycle-class accuracy mode | Opera uses `Ncycle=2` with ARM60 datasheet/SDK references; 5DO/FreeDO use `Ncycle=4` (`Arm60.cs:24`). A 2x difference in N-cycles affects all timing-sensitive code. | Both timing modes pass CPU conformance; full-collection differential recorded; default remains safe until proven. **Partial 2026-10-31:** `ArmTimingMode`, CLI/Settings option, cycle tests, and a save-state option snapshot are implemented; the full-collection differential is still pending. | M |
| [~] **CORE-02** | CLIO timer scheduler parity | Opera models 16 timers with per-timer deadlines, slack, and carry (`opera_clock.c`, `opera_clio.c`); 5DO services all timers per event (`Clio.DoTimers`). | Timer test ROM matches Opera; cascade/reload/IRQ tests pass; no golden regressions. **Partial 2026-10-31:** `ClioTimerMode`, per-slot scheduling with the `0x220` gap, explicit cascade carry, CLI/Settings option, state snapshot, and a 3,000-frame smoke differential are implemented behind the unchanged FreeDO default; the timer ROM and full-collection differential remain. | M |
| [~] **CORE-03** | MADAM CEL busy timing | Opera paces CEL work at ~9M pixels/s (`opera_madam.c:489-527`); 5DO renders synchronously and discards CEL cycles (`FiveDoMachine.cs:479-483`). | CEL status polling behaves correctly; busy time is serialized; golden titles unchanged or improved; perf within budget. **Partial 2026-10-31:** `CelTimingMode.OperaBusy`, `--cel-timing freedo|opera`, Settings, Madam section v2, Options section v3, synthetic status-polling integration tests, and a three-title smoke differential are implemented behind the unchanged FreeDO default; the full-collection differential remains. | L |
| [~] **CORE-04** | CD-ROM timing and command parity | Opera's `opera_cdrom.c` is ~4.4k lines with cycle-based sector ready, DIPIR, read-ahead, subcode, media access; 5DO is a frame-based FreeDO port. | CD transcript/parity on golden titles; seek/read/status tests; full sweep; no regressions. **Partial 2026-10-31:** `CdromTimingMode.OperaCycleBased`, 2x data-sector scheduling, delayed poll/status, XBUS CD FIQ, CLI/Settings option, state snapshots, and a three-title smoke differential are implemented behind the unchanged FreeDO default; command latency, TOC/read-ahead/subcode/DIPIR, and CD-DA/ODE remain; SYS-09 adds the speed selector. | XL |
| [ ] **CORE-05** | DSP timing and optional threading | 5DO executes one DSP sample per scheduler event on the core thread; Opera offers an experimental threaded DSP. | DSP sample clock remains exact; optional thread is deterministic; no audio regression. | M |
| [ ] **CORE-06** | SPORT/XBUS/DMA timing audit | 5DO ports FreeDO's DMA behavior; Opera has additional DMA8/PBUS control-port semantics. | DMA/PBUS trace parity; no allocator or CD regressions; tests. | M |
| [ ] **CORE-07** | Interrupt latency / FIQ/IRQ audit | Timing-sensitive titles depend on FIQ entry latency and CLIO/DIPIR ordering. | Latency tests; differential traces; documented invariants. | M |
| [~] **CORE-08** | Accuracy vs performance profiles | Users need a "compatible" default and an "accuracy" option; deterministic behavior must be explicit. | Two profiles; docs; benchmark deltas; state warnings. **Partial 2026-10-31:** `EmulationProfile` (`Compatible`, `Accuracy`, `Performance`, `Custom`), CLI `--profile`, Settings > Emulation > Timing profile, derived `run --json` profile, and mapping tests are implemented; per-profile benchmark deltas and a full-collection accuracy-profile run remain. | S |
| [x] **CORE-09** | 4DO/Opera fix parity audit | 5DO ports the 4DO fix bits; Opera has additional game-specific behavior. | Every known fix ID mapped; unknown games documented; tests. **Done 2026-11-01:** `docs/FixParity.md` maps every 4DO bit; timing-6 and graphics-step-Y are explicitly retired with local evidence. | M |
| [ ] **CORE-10** | Hardware test ROM suite and CPU conformance | Unit tests are strong but not a hardware oracle. | Test ROMs for CPU, MADAM, VDLP, DSP, CD, SPORT, CLIO; pass/fail report. | L |
| [~] **CORE-11** | Full-collection differential harness | `ReferenceStrategy.md` still lists the differential harness as pending. | Nightly run of 594 discs against Opera; frame/audio/state hashes; dashboard. **Partial 2026-10-31:** `tools/FiveDo.DiffHarness` runs 5DO and the bundled OperaCore with staged BIOS/ISO, compares checkpoint frame hashes, applies manifest allowances, and writes JSON/Markdown/HTML reports; the Lemmings 300-frame smoke matches (`0x110E7DC5`), the five-title showcase records two video matches and three video divergences, with the strict PC gate showing 0/5 matches (large PC deltas on the video-matched titles), and `manifest` generated the 594-title `benchmarks/diff/full.json`. The full run and nightly dashboard remain. | L |
| [x] **CORE-12** | Fuzzing and parser security | Disc/state/CUE/CHD parsers handle untrusted files. | Fuzz targets; no crashes/OOB; clean `InvalidStateException`/`DiscImageException`. **Done 2026-11-02:** `tools/FiveDo.Fuzz` covers states, ISO/CHD, CUE, Opera FS, CD command streams, and ARM60 streams; deterministic reproduction artifacts, bounded xUnit smoke tests, and a nightly workflow are in place. | M |
| [~] **CORE-13** | Remaining compatibility failures | Crash 'n Burn JP is stuck; several titles are BIOS-sensitive. | Every local disc classified; known failures documented with evidence. **Partial 2026-11-01:** 593 of 594 discs render; only Crash 'n Burn (Japan) remains, blocked on a bootable Japanese BIOS/dump. | L |
| [ ] **CORE-14** | Save-state format v3 and forward compatibility | v2 is compressed but not feature-flagged; future options need graceful rejection. | v3 with feature flags; v1/v2 still load; unknown sections preserved or rejected cleanly. | M |
| [ ] **CORE-15** | State diff and verification tooling | Debugging timing/state issues is manual. | `state diff`, section hashes, RAM/VRAM diff; CLI + UI. | M |
| [~] **CORE-16** | Frame/audio hash regression suite | Golden hashes exist for a few titles; not full-collection. | Per-title checkpoint hashes; CI gate; visual diff artifacts. **Partial 2026-10-31:** `run --checkpoints` emits frame/audio/state hashes and DSP/CD state; `FiveDo.DiffHarness baseline` compares report fields and fails on changes; the smoke report is reproducible. The first showcase baseline and nightly gate remain. | M |

#### CORE-01 deep dive: ARM60 cycle classes

**Current:** `Arm60` hard-codes `Ncycle = 4`, `Scycle = 1`, `Icycle = 1`. This is the FreeDO baseline. Opera uses `Ncycle = 2` and documents the ARM60 datasheet/SDK basis.

**Why it matters:** N-cycles dominate memory accesses and block transfers. A 2x difference changes instruction timing, DMA/CD handshakes, and frame-level behavior. It may explain some of the remaining timing-sensitive failures.

**Implementation**
- Add `ArmTimingMode` (`FreeDoCompatible`, `Arm60Datasheet`) to `EmulatorTuning`.
- Replace the `Ncycle` constant with an instance field or a per-mode table.
- Add per-instruction cycle assertions for both modes.
- Run the 594-disc differential with both modes; record frame/audio divergence.
- Keep `FreeDoCompatible` as the default until the evidence says otherwise.
- Expose the mode in Settings and `--arm-timing`.

**Done when**
- The CPU conformance suite passes in both modes.
- The full-collection differential report shows the effect.
- No default regression; the accuracy profile is documented.

**Reference:** `References/opera-libretro-master/libopera/opera_arm.c:100-113`; `docs/ReferenceStrategy.md` §6.

#### CORE-03 deep dive: MADAM CEL busy timing

**Current:** `FiveDoMachine.RunFrame` calls `Madam.HandleCel()` whenever `Fsm == InProcess`, then immediately sets `Fsm = Idle`. `CelEngine.HandleCel()` returns CEL cycles, but they are discarded. The CPU never waits for the CEL engine.

**Opera:** `opera_madam.c` tracks `g_CEL_PIXELS_DRAWN`, `g_CEL_TEXELS_WALKED`, `g_CEL_BUSY_UNTIL`, and a 9M pixels/second budget. CEL work is serialized over time; the CPU can observe busy status.

**Why it matters:** games that poll CEL status, issue CELs back-to-back, or rely on CEL completion timing can behave differently. This is a known Opera accuracy improvement.

**Implementation**
- Add `CelBusyUntil` (64-bit CPU cycle deadline) to `Madam`.
- On `SpriteStart`, estimate work from the CCB list and the pixel/texel count.
- Process CEL work in bounded chunks; keep `Fsm = InProcess` until the deadline.
- Return `InProcess` from the status register while busy.
- Feed CEL cycles into the frame scheduler and save-state section.
- Add a trace field for busy cycles and pixels.

**Done when**
- A CEL-status polling test observes busy then idle.
- Golden titles are unchanged or improve; no >10% frame-time regression.
- Opera differential shows closer timing.

**Reference:** `References/opera-libretro-master/libopera/opera_madam.c:489-527`, `:3233`, `:3506`.

#### CORE-04 deep dive: CD-ROM timing and command parity

**Current (2026-11-01):** `CdromDevice` implements the multi-track TOC from the disc source, READ TOC/Sub-Q, MODE SET/SENSE, cycle-based data-sector readiness, PLAY_AUDIO and CD-DA, and guest eject/inject. It still executes other commands immediately, reads sectors synchronously, and uses a frame-based media-access pulse heuristic; read-ahead, real subcode, guest ODE prompts, DIPIR tagging, and a full transcript differential remain.

**Opera:** `opera_cdrom.c` models command latency, cycle-based data-sector readiness, read-ahead, DIPIR, tagged status, chunks, subcode, CD-DA, ODE, TOC, and configurable speed.

**Why it matters:** CD timing is the biggest remaining compatibility risk. It affects loading screens, FMV, audio, and the static READ_ID waits already seen in Road Rash/Alone/Wolfenstein.

**Implementation**
- Follow `docs/ReferenceStrategy.md`: port behavior, not code; one reference per change; tests first.
- Add `CdromTiming` (cycles) and `NextReadyCycle`.
- Implement a command latency table and sector-ready scheduling.
- Populate TOC from the disc source (track table), not a hard-coded single track.
- Add read-ahead, subcode, and media-access behavior.
- Keep the existing disc-end divergence documented.
- Add transcript tests against Opera for a fixed command sequence.

**Done when**
- Golden CD transcripts match Opera or have documented, tested divergences.
- The 594-disc sweep has no CD-related regression.
- CD-DA and ODE can be layered on the same model.

**Reference:** `References/opera-libretro-master/libopera/opera_cdrom.c:789-1157`, `:170-184`, `:565`; `docs/ReferenceStrategy.md` §6.

### 5.2 P0/P1: System, disc, BIOS, region, peripherals

| ID | Enhancement | Why / reference | Done when | Effort |
|---|---|---|---|---|
| [~] **SYS-01** | CD-DA / Red Book audio and multi-track discs | Opera supports `MEI_CDROM_DIGITAL_AUDIO`; 5DO's `IDiscSource` exposes only 2048-byte data and `CueSheet` opens only the first data track. | A CD-DA title plays; WAV capture contains the audio; data-track titles unchanged. **Partial 2026-11-01:** multi-track CUE/CHD TOC, raw CD-DA sectors, TOC/Sub-Q, PLAY_AUDIO, 44.1 kHz mixing, volume/mute, and `disc tracks` are implemented; known-title WAV evidence and read-ahead/subcode remain. | L |
| [~] **SYS-02** | Disc swap / ODE / multi-disc | Opera has ODE commands and media launch/reset; 5DO has no swap flow. | Swap works in a multi-disc title; state roundtrip; UI/CLI. **Partial 2026-11-01:** M3U/M3U8 playlist, F3/UI/CLI swap, live host replacement without reset, guest inject, and per-disc hash are implemented; guest ODE prompts and a known multi-disc title remain. | M |
| [ ] **SYS-03** | PAL/NTSC region modes | Opera supports NTSC, PAL1 (320x288@50), PAL2 (384x288@50); 5DO is NTSC-only. | Battle Chess (Europe) boots and plays; PAL test ROM; no NTSC regression. | L |
| [x] **SYS-04** | BIOS manager and auto-selection | 4DO/Opera support BIOS2/Kanji and multiple BIOS files; 5DO has one path and no auto-region. | BIOS library with MD5/region; per-game BIOS; BIOS2/Kanji; auto-select works. **Done 2026-10-31:** `BiosCatalog`/`BiosLibrary`, `--bios2`, `--bios-auto`, `bios list|scan|select`, Settings > Paths BIOS folder/auto-select/scan, per-game overrides, and five-title plus four known-requirement smoke runs in `TestCorpus.md` 1.1f. The configured BIOS remains the fallback when no folder is set. | L |
| [ ] **SYS-05** | Embedded open-source boot ROM | Opera ships an embedded Opera Boot option; 5DO requires a user BIOS. | Boots homebrew/demos without a BIOS, if the license is compatible. | M |
| [ ] **SYS-06** | RSA/unsigned-disc option | Arcade CD-ROM titles need a patched BIOS; unsigned/homebrew discs need `norsa`. | Stock BIOS + an explicit option runs the four arcade titles; legal review recorded. | M |
| [ ] **SYS-07** | Memory capacity options | Opera supports 2/4/8/14/15 MB DRAM and 1/2 MB VRAM for homebrew. | Options exposed; homebrew test; stock default unchanged. | M |
| [~] **SYS-08** | CPU overclock/underclock | Opera 0.5x-4x; 4DO CPU clock; 5DO has only an internal clock. | Settings/CLI; save-state warning; known-good defaults. **Partial 2026-10-31:** `--cpu-clock` and the Settings field are implemented; per-clock compatibility/benchmark measurements remain. | S |
| [x] **SYS-09** | CD speed control | Opera 1x-32x/unbounded; 5DO fixed. | Option; timing tests; docs. **Done 2026-10-31:** `CdSpeed`, `--cd-speed`, Settings > Emulation > CD speed, `run --json` `cdSpeed`, Opera's per-speed overhead table, unbounded mode, Options section v5 mismatch warning, unit/integration tests, and the three-title smoke differential in `TestCorpus.md` 1.1e. | M |
| [~] **SYS-10** | Random seed control | Opera fixed/random seed for determinism; 5DO has no user control. | Option; deterministic replay; tests. **Partial 2026-10-31:** `--random-seed` and the Settings field are implemented for the DSP PRNG; replay-fixture evidence remains. | S |
| [ ] **SYS-11** | MADAM matrix hardware/software switch | Opera option; some games run faster in software mode. | Option; compatibility notes; tests. | S |
| [ ] **SYS-12** | SWI HLE for Portfolio OS | Opera's optional HLE can improve performance. | Opt-in; parity tests; documented trade-offs. | L |
| [x] **SYS-13** | VDLP bypass CLUT | Opera option for speed/debug. | Opt-in; visual difference documented; tests. **Done 2026-11-01:** the guest `clut_bypass` bit (bit 25) selects the fixed CLUT for pixel bit 15 in VDLP, the CPU assembler, and both GPU palette shaders; tests cover the branch. | S |
| [x] **SYS-14** | Per-game NVRAM and NVRAM versions | Opera per-game/shared and versions 0-9; 5DO uses one global file. | Per-game NVRAM; import/export/reset; tests. **Done 2026-10-31:** `NvramStorageMode`/`NvramVersion`/`NvramDirectory`, `NvramLayout`, shared-to-per-game migration fallbacks, atomic stores, Settings > Emulation controls, File > Export/Import/Reset NVRAM, CLI layout options, `run --json` fields, and `nvram path|list|export|import|reset`; unit, store, settings, and CLI tests. | M |
| [~] **SYS-15** | Mouse, lightgun, arcade lightgun, flightstick, Orbatak trackball | Opera supports all; 5DO is joypad-only. | Each device works in a representative title; per-port selection; crosshairs/calibration. **Partial 2026-11-01:** `InputDeviceSet` and `PbusChainEncoder` implement Opera's exact joypad/flightstick/mouse/lightgun/arcade-lightgun/Orbatak trackball and button records; Settings > Emulation > Input devices, CLI `--input-device`/`--pbus-format`, `run --json inputDevices`, pointer-driven mouse/lightgun input, and a lightgun crosshair are implemented; Crime Patrol, Mad Dog II, Orbatak, and Shootout at Old Tucson boot and detect the selected device. Pointer-port routing and calibration are implemented; scripted gameplay remains. | L |
| [ ] **SYS-16** | Six-player / multi-tap verification | 5DO encodes six pads; not all titles are validated. | Six-player test ROM/title; PBUS trace; docs. | S |
| [ ] **SYS-17** | Game database expansion and community compatibility DB | 5DO has 518 records and a 241-title 4DO reference list; the local collection has 594 images. | >=95% local images have metadata; region/revision/fix/BIOS fields; contribution workflow. | M |
| [ ] **SYS-18** | Auto per-game profile ("just works") | Best-in-class means no manual BIOS/region/fix/input selection. | Every local disc gets a profile; known problem titles boot without manual steps. | L |
| [ ] **SYS-19** | Save-state interoperability with 4DO/Opera | Users should be able to migrate saves; preservation value. | Import/export 4DO `.4dosav` and Opera states; roundtrip tests. | L |
| [~] **SYS-20** | Disc health / Redump DAT / CHD integrity | Bad dumps cause false compatibility failures. | Verify against DAT; clear warnings; no silent corruption. **Partial 2026-11-01:** `disc verify` reads every native sector, hashes CRC-32/MD5/SHA-1, matches Redump/No-Intro DATs with `--dat`, and exits 1 on mismatch; the UI badge and 4DO/Opera comparison remain. | M |
| [ ] **SYS-21** | Physical CD drive support | 4DO can boot from a CD drive; 5DO is file-only. | Drive selection; same `IDiscSource` path; tests. | M |
| [ ] **SYS-22** | Disc image conversion/repair | Users need CUE/BIN/ISO/CHD conversion and repair. | CLI/UI convert; preserve tracks; verify output. | M |

#### SYS-01 deep dive: CD-DA and multi-track

**Current (2026-11-01):** implemented except read-ahead/subcode and known-title evidence. `ITrackedDiscSource` exposes the track table and native sectors; `CueDiscImage`/`ChdDiscImage` parse multi-track CUE/CHD with audio; `CdromDevice` implements TOC/Sub-Q/MODE SET/PLAY_AUDIO and 2352-byte reads; CD-DA is mixed at 44.1 kHz with persisted volume/mute; `disc tracks` and `disc playlist` report the layout.

**Opera:** `opera_cdrom.c` supports `MEI_CDROM_DIGITAL_AUDIO`, audio read-ahead, an audio clock, and TOC entries for multiple tracks. `retro_cdimage` exposes track data.

**Implementation**
- [x] Extend the disc source with the track table and native sectors.
- [x] Parse multi-file and multi-track CUE; keep `PREGAP`/`INDEX 00/01`.
- [x] Read CHD track metadata and audio hunks.
- [x] Decode CD-DA to 44.1 kHz 16-bit stereo; mix with DSP output; volume/mute.
- [x] Add `disc tracks --json`; `fs` only walks data tracks.
- [x] Add save-state fields for audio position, volume, and mute.
- [ ] Add CD-DA read-ahead and subcode.
- [ ] Capture a known CD-DA title and a 10-second DSP+CD-DA WAV.

**Done when**
- A known CD-DA title plays its audio track.
- A 10-second WAV capture contains both DSP and CD-DA.
- Data-track titles have identical hashes.

**Reference:** `opera_cdrom.c:565`, `:901-1088`; `retro_cdimage.c`.

#### SYS-04 deep dive: BIOS manager and auto-selection

**Current (2026-10-31):** implemented. `BiosCatalog`/`BiosLibrary` scan a folder, compute MD5s, and classify primary/secondary, region, video standard, Kanji, Anvil, and patch state. `--bios2`, `--bios-auto`, and `bios list|scan|select` are available; `run --json` reports the selected BIOS and reason. Settings > Paths edits the BIOS folder, primary/secondary paths, auto-select, and scan; Settings > Emulation adds a per-game override; File adds assign/clear actions. Auto-selection combines game-database regions, known requirements (Slayer Europe/USA, Powers Kingdom Europe, Arcade CD-ROM), and per-game MD5 pins; the configured BIOS remains the fallback when no folder is set. The embedded boot ROM and RSA/unsigned-disc options remain separate legal/engineering work.

**4DO:** BIOS1 + BIOS2 (Kanji font), file pickers, and a portable settings provider.  
**Opera:** BIOS and Font options, embedded Opera Boot, and system-directory scanning.

**Implementation**
- `BiosLibrary`: scan folders, compute MD5, detect region/type/Anvil, store metadata.
- `BiosProfile`: BIOS1, BIOS2/Kanji, region, notes.
- Per-game `BiosId` in `GameProfile`; auto-select by disc region and known requirements.
- CLI `--bios2`, `--bios-auto`, `bios list/scan`.
- Settings UI: BIOS list, import, per-game assignment, validation.
- Embedded boot option after license review.
- RSA/unsigned-disc option after legal review; prefer documenting user-provided patched BIOS.

**Done when**
- Arcade titles boot with the right BIOS/option.
- Battle Chess (Europe) auto-selects the EU BIOS.
- A Japanese title with a Kanji ROM renders its font.
- Homebrew boots without a user BIOS if the embedded boot is legally cleared.

**Reference:** `References/4DO-master/FourDO/Properties/Settings.settings` (`BiosRom2File`); `libretro_core_options.c` (`opera_bios`, `opera_font`).

#### SYS-15 deep dive: Input devices

**Current:** `ControllerState`/`PbusEncoder` encode the standard six-pad packet. There is no device-type abstraction, no mouse/lightgun/flightstick/trackball packet, and no crosshair.

**Opera:** `lr_input.c` builds `opera_pbus_joypad_t`, `flightstick_t`, `mouse_t`, `lightgun_t`, `arcade_lightgun_t`, and `orbatak_trackball_t`; `libretro.c` exposes the device list.

**Implementation**
- Add `IControllerDevice` with `DeviceType`, `BuildPbusPacket`, `Reset`, `Save/Load`.
- Implement each device's PBUS packet exactly (reference `opera_pbus.c`).
- Add per-port device selection in Settings and CLI (`--input-device 1=lightgun`).
- Mouse capture with relative/absolute modes; sensitivity.
- Lightgun crosshair overlay, calibration, off-screen reload.
- Orbatak trackball buttons (start/coin/service) mapping.
- Tests for packet bytes and a representative title per device.

**Done when**
- Orbatak trackball controls the game.
- Crime Patrol/Mad Dog II/Shootout at Old Tucson accept lightgun input.
- A mouse title works.
- A flightstick title works.
- Six joypads still work.

**Reference:** `References/opera-libretro-master/lr_input.c`; `libopera/opera_pbus.c`.

### 5.3 P1/P2: Video

| ID | Enhancement | Why / reference | Done when | Effort |
|---|---|---|---|---|
| [ ] **VID-01** | True 640x480 / 480i internal rendering | Current `--high-res` line-doubles 240 lines; `HighResolutionCelResearch.md` Option B. | True vertical detail on a 640x480 title and a 3D title; 60fps; per-title fallback. | XL |
| [ ] **VID-02** | 2x/4x supersampling | Research Option C; improves geometry/texture edges without changing guest VRAM. | 2x prototype within budget; deterministic downsample; comparison screenshots. | L |
| [ ] **VID-03** | GPU CEL compositing | Long-term Option D; true high-res independent of 1MB VRAM. | A parity prototype for packed/literal CELs; go/no-go note. | XL |
| [ ] **VID-04** | 480i/240p detection, deinterlacing, field blending | 3DO titles mix 240p/480i; current output assumes 240 lines. | Correct field handling; bob/weave/blend options; test patterns. | M |
| [x] **VID-05** | PAL output timing | Needed with SYS-03; 288 lines at 50Hz. | PAL titles render at the right size/rate; no NTSC regression. **Done 2026-11-01:** 320/384x288 at 50 Hz through VDLP, frame store, uploader, screenshots, and pacing; Battle Chess Europe verified. | M |
| [x] **VID-06** | Auto-crop, overscan, void area, gray border | 4DO has these; 5DO always uses the full frame. | Per-game crop/overscan presets; 4DO-style void patterns; UI. **Done 2026-11-02:** per-edge 0-32 px crop, per-game `GameProfile.Overscan`, configurable 0-64 px void border and color, native-screenshot parity, and pointer alignment. | M |
| [ ] **VID-07** | Shader preset system | RetroArch `.slang`/`.glslp`; 5DO has fixed filters. | Load popular presets; hot reload; per-game; fallback. | L |
| [ ] **VID-08** | NTSC composite/S-Video/RF, CRT Royale, HDR, gamma | Best-in-class video quality. | Presets and controls; visual validation; performance. | L |
| [ ] **VID-09** | Video recording (MP4/WebM/GIF) with audio | Modern UX; share clips; TAS evidence. | Record/stop; audio sync; file naming; UI/CLI. | M |
| [ ] **VID-10** | Overlays, bezels, touch controls | Console-like UX; mobile. | Per-system/per-game overlays; controller overlay; touch. | M |
| [ ] **VID-11** | Vulkan surfaces on Linux/macOS | Current native surface is Win32-only. | X11/Wayland/Metal or compositor path; validation. | L |
| [ ] **VID-12** | Run-ahead, frame delay, VRR, audio sync | Latency matters for action games. | 1-2 frame run-ahead; frame-delay slider; VRR; measured latency. | L |
| [ ] **VID-13** | Video test suite and color accuracy | 3DO RGB555/CLUT/NTSC behavior needs regression coverage. | Test patterns; CPU/GPU parity; color delta report. | M |
| [x] **VID-14** | Screenshot gallery and metadata | Users want organized captures. | Gallery; filters; compare; export. **Done 2026-11-02:** game/date filters, side-by-side compare, export, and per-capture settings sidecars. | S |
| [x] **VID-15** | Direct3D 11 renderer (Windows) | OpenGL/GLES is the default; D3D11 is the best-supported Windows API and matches ANGLE's D3D11 path. | D3D11 presents all filters, matches the CPU oracle, falls back to OpenGL/CPU; OpenGL remains default. | L |

#### VID-01 deep dive: true high-resolution CEL rendering

**Current:** `CelRasterizer.ResScale` doubles X sampling; `Vdlp.RenderHighResolutionLine` emits two identical lines for each low-res line. It is FreeDO `RESSCALE`, not true 480-line rendering.

**Research plan:** `docs/HighResolutionCelResearch.md` Option B (true 640x480), Option C (2x supersampling), Option D (GPU CEL).

**Implementation path**
1. Add `HighResolutionMode` (`Off`, `FreeDoLineDouble`, `True640`).
2. Scale CEL Y coordinates, clipping, and VDLP line timing by 2.
3. Stop duplicating planes 2/3; emit independent 480-line output.
4. Keep guest-visible VRAM semantics unchanged; fall back for titles that read the framebuffer.
5. Validate on Lemmings (640x480 UI), a 2D title, and a 3D title.
6. Measure frame time and compare with native + xBR/FSR.

**Done when**
- True vertical detail is visible in comparison captures.
- 60fps on Tier A; documented per-title compatibility.
- The default remains 320x240 + output filter.

#### VID-07 deep dive: shader presets

**Current:** `FiveDo.Video.OpenGL` and `FiveDo.Video.Vulkan` implement a fixed filter enum. Users cannot load shader chains.

**RetroArch/Opera:** the frontend supplies `.slang`/`.glslp` presets, including CRT, NTSC, scanlines, curvature, HDR, and 4K upscalers.

**Implementation**
- Define a preset model: passes, textures, uniforms, scale, feedback.
- Parse the common `.slang`/`.glslp` subset; reject unsupported features clearly.
- Implement the pass chain in GL and Vulkan; cache compiled programs.
- Hot reload; per-game preset; fallback to sharp-bilinear.
- Curate a small set of MIT/CC0 presets with notices.

**Done when**
- A popular CRT preset loads and renders at 4K60.
- A missing/invalid preset falls back cleanly.
- Per-game presets persist.

### 5.4 P1: Audio

| ID | Enhancement | Why / reference | Done when | Effort |
|---|---|---|---|---|
| [~] **AUD-01** | Audio buffer/latency/device/resampler settings | 4DO exposes buffer ms; 5DO only volume. | Device list, buffer size, resampler quality, exclusive mode, sync slider. **Partial 2026-11-01:** device enumeration/selection, 20-500 ms buffer latency, live output recreation, latency display, and CLI `audio devices`/`--audio-device`/`--audio-buffer` are implemented; resampler quality, exclusive mode, and sync offset remain. | M |
| [~] **AUD-02** | Audio recording UI, mixer, spectrum | CLI WAV exists; UI does not. | Record/stop; per-channel mute; spectrum; file naming. **Partial 2026-11-01:** desktop **File > Record Audio...** records the DSP/CD-DA mix to WAV through `TeeAudioSink`, with finalize-on-stop, naming, and duration reporting; mixer/spectrum/format options remain. | M |
| [ ] **AUD-03** | DSP debugger | DSP is a black box outside traces. | Registers, memory, instruction ring, breakpoints, semaphore view. | M |
| [ ] **AUD-04** | Audio golden tests vs Opera/hardware | Current tests check non-silence/peak/RMS, not spectral parity. | Golden WAVs; spectral cosine; regression gate. | M |
| [x] **AUD-05** | CD-DA mixing and volume | Depends on SYS-01. | Independent CD-DA/DSP volumes; mute; save state. **Done 2026-11-01:** persisted `CdAudioVolume`/`CdAudioMuted`, Settings > Audio controls, `--cd-volume`/`--mute-cd`, CD state v4, mix-level/mute tests, and settings validation. | S |

#### AUD-01 deep dive: audio buffer and latency

**Current (2026-11-01):** `OpenAlAudioOutput` enumerates devices, derives queue/prefill/ring sizing from a 20-500 ms target, and can be recreated live when the device or buffer changes. `AppSettings` persists `AudioDevice` and `AudioBufferMilliseconds`; resampler quality, exclusive mode, and sync offset remain.

**Why:** users have different devices, Bluetooth latency, and driver behavior. A buffer slider and device selection eliminate crackle and lip-sync complaints.

**Implementation**
- Enumerate OpenAL devices; persist selection.
- Add `AudioBufferMilliseconds` (e.g., 20-500 ms), `ResamplerQuality`, `ExclusiveMode`, `SyncOffset`.
- Recreate the output when settings change; keep the audio thread ownership rules.
- Show queued/underrun/latency in the overlay.
- Test with the null backend and a real device.

**Done when**
- No crackle at 20-200 ms on the test machine.
- Device switch works without restarting emulation.
- Settings roundtrip and docs.

### 5.5 P1: UX and quality-of-life

| ID | Enhancement | Why / reference | Done when | Effort |
|---|---|---|---|---|
| [x] **UX-01** | Rewind | RetroArch/Opera; expected in 2026. | 30-300s ring buffer; hotkey; frame-by-frame; deterministic; memory budget. **Done 2026-11-01:** `RewindBuffer` keeps compressed keyframes plus the per-frame PBUS packet history, replays input after the nearest keyframe, enforces a 256 MB default cap, clears on timeline jumps, and exposes `CoreRunner.Rewind`; the shell binds `R` to a one-second step and `Shift+F9` to one frame. Deterministic state equality is covered by tests. | L |
| [x] **UX-02** | Fast-forward / turbo | Skip intros; TAS; modern UX. | 2x-10x/uncapped; audio mute/scale; overlay; no state divergence. **Done 2026-11-01:** `CoreRunner.SpeedMultiplier` runs 2x-10x frames per presentation tick, skips intermediate rendering, mutes and resets the audio queue while active, and leaves emulated state/input unchanged; the shell holds `Tab` to fast-forward and shows the active multiplier in the status line. | S |
| [ ] **UX-03** | Netplay | 3DO is a six-player console; best-in-class multiplayer. | Rollback/lockstep; lobby/direct IP; 2-6 players; spectators; desync detection. | XL |
| [x] **UX-04** | Cheat engine | `--poke` is diagnostic only. | Raw/typed/conditional/button RAM codes; per-game JSON files; desktop manager; CLI list/add/enable/disable/remove/import/export; per-frame application after state loads and rewind. **Done 2026-11-01:** `FiveDo.Cheats` parser/engine/store, `FiveDoMachine.BeforeFrame`, Settings > Paths > Cheats, Emulation > Cheats..., and the CLI `cheat` commands. 3DO has no standard RAM cheat format; Game Guru is a save editor, so the documented 5DO syntax is used and no code databases are shipped. | M |
| [ ] **UX-05** | Achievements | RetroAchievements if 3DO support exists; otherwise local. | Login/offline; progress; notifications; per-game list. | L |
| [~] **UX-06** | TAS tools | Input recording exists but no movie format. | Movie record/replay; input display; lag counter; rewind; Lua; branch. **Partial 2026-11-01:** `FiveDo.Tas` implements the versioned `.5domovie` container (per-frame PBUS packets, optional starting state, CRC-32 and Brotli), deterministic record/replay, branch/trim, and frame/PC/state verification; the CLI adds `--movie-record`, `--movie-play`, `--movie-verify`, and `movie info|record|play|verify|branch`; the shell adds Record/Play/Stop Movie and a player-1 input display. Lua, a frame-by-frame input editor, and a separate lag counter remain; 3DO polls input every frame, so lag is not applicable. | L |
| [ ] **UX-07** | Controller profiles and per-game input | One global profile today. | Named profiles; per-game assignment; deadzone/curves; hot-plug. | M |
| [ ] **UX-08** | Gamepad navigation and big picture | Console-like UX. | Full gamepad navigation; 10-foot UI. | L |
| [ ] **UX-09** | Localization | 4DO has 7 languages; 5DO is English-only. | Resource-based strings; at least 7 languages; community workflow. | M |
| [ ] **UX-10** | Themes and accessibility | Modern shell expectations. | Dark/light/accent; UI scale; high contrast; screen-reader labels. | M |
| [x] **UX-11** | Disc browser UI and game info | 4DO has both; 5DO only CLI `fs`. | Tree/list, extract, open directory, game info dialog. **Done 2026-11-01:** `DiscBrowser`/`DiscBrowserNode`/`DiscInfo` in `FiveDo.Host` mount the Opera file system without the emulator, flatten a sorted directory tree, extract a file/directory/disc with safe names and path-containment checks, and expose label/comment/game ID/database metadata/fixes. File > Disc Browser... and Disc Info... use the new windows; the browser shows sizes, LBAs, child counts, extraction results, and an Open Folder action. Unit tests cover synthetic ISO/CHD-independent trees, extraction, sanitization, and metadata. | M |
| [x] **UX-12** | BIOS/NVRAM manager UI | Needed with SYS-04/SYS-14. | List/import/assign/reset/backup. **Done 2026-11-01:** `BiosInventory`/`NvramInventory` scan, validate, import, assign, back up, reset, and delete BIOS/NVRAM files with atomic writes; **File > BIOS / NVRAM Manager...** provides a two-tab BIOS and NVRAM window with per-game assignment, layout-wide backup, validation state, and second-click confirmation for destructive actions. Unit tests cover import reuse/collisions, invalid images, layout backup, reset, delete, and naming. | M |
| [~] **UX-13** | Library metadata, favorites, playtime | 5DO has covers + 4DO metadata. | Redump/No-Intro/IGDB/LaunchBox; screenshots; manuals; favorites; playtime. **Partial 2026-11-01:** `LibraryState`/`LibraryStateStore` persist favorites, launch counts, last-played timestamps, and play time; `LibraryPlaytimeTracker` measures running sessions; `LibraryMetadata`/`LibraryMetadataStore` add an offline JSON overlay for title/developer/publisher/year/genre/region/players/description; `LibraryQuery` adds All/Favorites/Played/Unplayed filters and title/recent/most-played/year/publisher sorts; the game list shows star toggles, metadata, and play time, and Settings > Paths chooses the metadata file. **File > Game Metadata...** edits and clears the current disc's offline record. External Redump/No-Intro/IGDB/LaunchBox providers, screenshots/manuals/videos, and the >=90% rich-metadata target remain. | L |
| [ ] **UX-14** | Auto-update and release channels | Modern distribution. | Check/download/verify/install; stable/beta; rollback. | M |
| [x] **UX-15** | Portable mode, multi-instance, config profiles | 4DO portable; 5DO not. | `portable.flag`/`--portable`; per-instance config; profiles. **Done 2026-11-01:** portable/explicit data directories, named profiles, `InstanceLock`, selector/create/import/export UI, and `default-profile.txt`. | S |
| [ ] **UX-16** | Installers, signing, notarization, file associations | 5DO ships archives only. | MSIX/Inno/dmg/AppImage/Flatpak/Snap/AUR; signed; associations. | L |
| [ ] **UX-17** | Crash report, compatibility report, log bundle | Bug reporting exists; compatibility evidence is manual. | One-click bundle: state, replay, logs, hashes, screenshot, settings. | M |
| [ ] **UX-18** | Onboarding and verification | First-run guide exists; no disc/BIOS verification. | Guided setup; BIOS/disc health; redump check; clear errors. | M |
| [x] **UX-19** | OSD, notifications, quick menu | Modern UX. | Toasts; quick menu; save/load/rewind/fast-forward indicators. **Done 2026-11-01:** `OsdNotifications`/`OsdOptions` provide a bounded, time-expiring notification queue and validated position/opacity/duration settings; the game view shows colour-coded toasts for save/load, rewind, fast-forward, screenshots, reset, and settings. `F4` or the gamepad Select+Start chord opens a quick menu that pauses the game and offers resume/pause, save/load, rewind, fast-forward, screenshot, reset, cheats, settings, stop, and close; arrow keys/Enter navigate and closing resumes the prior state. Settings > Video exposes enable, position, opacity, duration, and quick-menu toggles. | S |
| [ ] **UX-20** | Per-game settings UI | Only fix/filter overrides today. | BIOS/region/input/clock/CD speed/shader/cheats per game. | M |
| [ ] **UX-21** | Debugger UI | CLI traces only. | CPU, memory, disasm, breakpoints, watchpoints, call stack, MADAM/CEL, DSP, CD, VDLP, VRAM. | L |
| [ ] **UX-22** | Trace viewer and performance profiler | Traces are text files. | Timeline; filters; frame/CPU/CEL/DSP charts; export. | M |
| [x] **UX-23** | Input display and lag counter | TAS/streaming. | On-screen buttons; frame/lag counters. **Done 2026-11-01:** F2/View toggles frame, lag, skip, disc, and selected-player button overlays; lag means no PBUS DMA read; port 1-6 is configurable. | S |
| [ ] **UX-24** | Lua scripting and automation | Power users/TAS. | Script API; input/state/memory; sandbox; examples. | L |

#### UX-01 deep dive: rewind

**Current:** no rewind; save states are 0.6-2.0 MB compressed and not designed for per-frame snapshots.

**Why:** rewind is table stakes in RetroArch and modern emulators. It also helps testing and TAS.

**Implementation**
- Ring buffer of compressed keyframes plus input deltas.
- Keyframe every N frames (e.g., 60), delta/input between.
- Configurable length 30-300s; memory cap.
- Hotkey (hold/step), OSD indicator, frame-by-frame.
- Pause/resume semantics; audio mute while rewinding.
- Save/load compatibility; state format v3.

**Done when**
- 60s rewind at 60fps on the dev machine.
- Deterministic resume; no state divergence.
- Memory stays under a configurable cap.
- No stutter in normal play.

#### UX-03 deep dive: netplay

**Current:** none.

**Why:** 3DO's six-player support is a differentiator. Netplay is the feature that makes a 3DO emulator a social platform.

**Implementation**
- Deterministic lockstep with input delay; rollback optional.
- Lobby/direct IP; NAT traversal or relay; server list (RMG-style).
- 2-6 players; spectators; chat; latency display.
- Desync detection via periodic state hashes; automatic resync.
- Same BIOS/disc/settings validation; per-player input device.
- CLI `--netplay` for testing.

**Done when**
- Two players complete a race/fight over the internet.
- <100 ms input latency on a regional connection.
- 30-minute session with no desync.
- Six-player local session still works.

#### UX-04 deep dive: cheat engine

**Current (2026-11-01):** implemented. `MemoryPatch` remains a one-shot diagnostic for `--poke`; `FiveDo.Cheats` owns persistent per-game cheats.

**Why:** cheats are expected; they also help testing and accessibility.

**Implementation**
- Raw RAM writes plus typed `write8`/`write16`/`write32` writes; memory conditions; player-1 button conditions; `and`/`or`/`not`/parentheses.
- Per-game JSON files under `CheatsDirectory` (default `<LocalApplicationData>/5DO/cheats`), named by 4DO game ID or `global.json`.
- Desktop manager under Emulation > Cheats... with enable checkboxes and add/update/delete; CLI `cheat list|add|enable|disable|remove|import|export` and `run --cheat`/`--cheat-code`/`--cheat-auto`.
- `FiveDoMachine.BeforeFrame` applies the set once per frame, so state loads, fast-forward, and rewind replay all keep the configured cheats active.
- Invalid individual codes are preserved and reported; unsupported document versions are rejected. No cheat databases are shipped.
- Documented legal scope: 3DO has no standard RAM cheat format, and the Game Guru is a save-file editor, so 5DO uses its own documented syntax.

**Done when**
- A known 3DO cheat code works. **Done:** raw/typed/conditional/button tests and a `--cheat-code` smoke.
- Enable/disable toggles without restart. **Done:** the shell reloads the set and the next frame uses it.
- Cheats survive save/load as configured. **Done:** the JSON file is authoritative and the frame hook remains attached.
- No crash on malformed codes. **Done:** parser errors are reported; invalid loaded entries are skipped.

#### UX-06 deep dive: TAS tools

**Current:** `InputRecorder` writes a replayable `--input-script`; `F9` advances one frame. There is no movie format, input display, lag counter, or Lua.

**Why:** TAS creators are a high-signal community; good tools create long-tail content and compatibility evidence.

**Implementation**
- Movie format: header (BIOS/disc hashes, settings, frame count), input stream, optional state.
- Record/play/pause; frame advance; rewind; input display; lag counter.
- Branch/split; export/import; deterministic verification.
- Lua scripting API for memory/input/state.
- CLI `movie record/play/verify`.

**Done when**
- A full movie records and replays bit-identically.
- Input display and lag counter work.
- A shared movie verifies on another machine.
- Docs and examples.

### 5.6 P2: Ecosystem and platforms

| ID | Enhancement | Why / reference | Done when | Effort |
|---|---|---|---|---|
| [ ] **ECO-04** | Linux/macOS validation, Flatpak/AppImage, macOS gamepad/Metal | Claimed platforms must be proven. | Smoke/soak on each; docs; packages. | L |
| [ ] **ECO-06** | Plugin API | Extensibility for filters/input/cheats/netplay. | Stable interfaces; sample plugin; docs. | L |
| [ ] **ECO-07** | Headless server / HTTP / WebSocket API | Automation, CI, remote play. | Start/stop/load/save/screenshot/input endpoints; auth. | M |
| [ ] **ECO-08** | 3DO modem/network emulation | Optional preservation of network-capable software. | Documented scope; a test title; no core regression. | L |
| [ ] **ECO-09** | 3DO CD player / Video CD | Optional preservation. | Boots the player; plays a disc; documented. | M |
| [ ] **ECO-10** | Documentation wiki and compatibility portal | Community trust and discoverability. | Public docs; per-title pages; search; contribution guide. | M |
| [ ] **ECO-11** | Community workflow | Discord, issue templates, contribution guide, bounties. | Templates; triage; release notes; contributor docs. | S |

ECO-02 (WebAssembly/browser), ECO-03 (Android/iOS), and ECO-05 (Raspberry Pi) were
dropped on 2026-11-02.

### 5.7 P3: Engineering, legal, and trust

| ID | Enhancement | Why / reference | Done when | Effort |
|---|---|---|---|---|
| [ ] **ENG-01** | CI matrix, nightly sweep, coverage, static analysis, SBOM, reproducible builds | Trust at scale. | All RIDs; nightly 594-disc sweep; coverage gate; analyzers; SBOM; reproducible. | M |
| [ ] **ENG-02** | Performance: interpreter hot path, M12 JIT, GPU CEL, async shaders | Headroom for Tier B/4K. | Measured wins; bit-identical JIT; zero allocations. | XL |
| [x] **ENG-03** | Legal/licensing/attribution | Release gate. | FreeDO modified GNU LGPL, donationware; notices and OpenAL obligations shipped; project-owner sign-off recorded 2026-11-02. | M |
| [ ] **ENG-04** | Accessibility | Inclusive UX. | Screen-reader labels; high contrast; UI scale; keyboard-only. | M |
| [ ] **ENG-05** | Telemetry transparency and privacy controls | User trust. | Opt-in; clear docs; no game/user data; local-only option. | S |
| [ ] **ENG-06** | Stability soak / crash-free | "Best" means no crashes. | 1M frames / 24h; all titles; no unhandled exceptions. | M |
| [ ] **ENG-07** | Documentation refresh | Roadmap and behavior must stay honest. | Every item documented; docs build; screenshots. | S |
| [ ] **ENG-08** | Localization infrastructure | Supports UX-09. | Resource pipeline; pseudo-locale; community workflow. | S |
| [ ] **ENG-09** | Code coverage and benchmark regression | Prevent silent quality decay. | Coverage report; per-PR benchmark; baseline comparison. | M |
| [ ] **ENG-10** | Release engineering and signing | Professional distribution. | Signed artifacts; checksums; release notes; rollback. | M |

#### ENG-02 deep dive: performance roadmap

**Current:** Road Rash race 4.20 ms/frame (p99 7.40 ms), synthetic 1.25 ms, 4K GPU p99 0.089 ms. The interpreter is fast enough for Tier A; Tier B is unmeasured.

**Order of work**
1. Profile the interpreter and CEL/DSP hot paths on x64 and arm64.
2. Optimize the interpreter (fast RAM path, dispatch/decode cache, fewer interface calls).
3. Add the optional managed IL JIT (M12) only if profiling demands it.
4. Add GPU CEL compositing only after software parity and a clear budget.
5. Add async shader compilation and pipeline caching.
6. Gate on p99, allocations, and 4K/Tier B targets.

**Done when**
- Tier A: 4K60 all filters within budget.
- Tier B desktop: 1080p60.
- JIT (if built) is bit-identical and optional.
- No steady-state allocations.

### 5.8 Existing documented backlog (carried forward)

These are already recorded in the repo's docs; keep them visible so the new roadmap does not lose them.

| ID | Item | Current doc | Related new IDs |
|---|---|---|---|
| [ ] **BAK-01** | Finish a full Road Rash 5.5-mile race | `docs/KnownIssues.md`, `docs/Status.md` | UX-06, CORE-04 |
| [ ] **BAK-02** | Script Alone in the Dark inventory/exploration | `docs/KnownIssues.md` | UX-06, CORE-09 |
| [ ] **BAK-03** | Input/gameplay scripts for Gex, NFS, SSF2T, Total Eclipse, Killing Time, Theme Park, Return Fire | `docs/ReleaseChecklist.md` | UX-06, CORE-11 |
| [ ] **BAK-04** | CD/CLIO/MADAM/ARM timing triage with Opera | `docs/ImplementationPlan.md` | CORE-01..04 |
| [ ] **BAK-05** | arm64/DPI/Linux/macOS validation | `docs/Status.md` | ECO-04 |
| [ ] **BAK-06** | Tier B 1080p60, 3D CEL profiling, hot-path optimization, relative benchmark gate | `docs/Performance.md` | ENG-02, ENG-09 |
| [ ] **BAK-07** | MSIX/dmg/AppImage installers, signing/notarization, file associations | `docs/ReleaseChecklist.md` | UX-16 |
| [ ] **BAK-08** | Frontend NativeAOT audio validation | `docs/Trimming.md` | ENG-10 |
| [ ] **BAK-09** | ViewModel tests and UI polish | `docs/ImplementationPlan.md` | ENG-09 |
| [ ] **BAK-10** | Optional M12 managed ARM JIT | `docs/ImplementationPlan.md` | ENG-02 |
| [ ] **BAK-11** | M14 high-resolution CEL research (post-1.0) | `docs/HighResolutionCelResearch.md` | VID-01..03 |
| [x] **BAK-12** | Legal/attribution sign-off; green CI | `docs/ReleaseLegalReview.md` | ENG-03, ENG-01 |
| [ ] **BAK-13** | BIOS-aware sweep and 5DO status merge | `docs/CompatibilitySweep.md` | CORE-13, SYS-17/18 |
| [ ] **BAK-14** | 4DO `.4dosav` import/export | `docs/Compatibility.md` §8 | SYS-19 |
| [ ] **BAK-15** | PAL, CD-DA, netplay, rewind, debugger UI, additional peripherals | `docs/ImplementationPlan.md` M11.7 | SYS-01/03/15, UX-01/03/21 |
| [ ] **BAK-16** | Windows/macOS file associations | `docs/ReleaseChecklist.md` | UX-16 |
| [x] **BAK-17** | Portable mode (`portable.flag`, `--portable`, `FIVEDO_DATA_DIR`) | `docs/Configuration.md` | UX-15 |
| [ ] **BAK-18** | macOS gamepad backend | `docs/KnownIssues.md` | ECO-04 |
| [ ] **BAK-19** | Vulkan X11/Wayland/Metal surfaces | `docs/Vulkan.md` | VID-11 |

### 5.9 Moonshots and differentiators

These are not required to be "best", but they can make 5DO unmistakable.

| ID | Idea | Why it could win |
|---|---|---|
| [ ] **MOON-01** | One-click BIOS-less universal boot | Opera already has an embedded boot; 5DO can remove the BIOS barrier for homebrew/demos. |
| [ ] **MOON-02** | Zero-config compatibility | Auto BIOS/region/fix/input/clock profiles make every disc "just work". |
| [ ] **MOON-03** | Public accuracy dashboard | Publish the 594-disc differential results; trust through transparency. |
| [ ] **MOON-04** | Console-like big picture | 10-foot UI with gamepad-only navigation. |
| [ ] **MOON-05** | Creator suite | TAS + debugger + Lua + trace viewer + state diff in one shell. |
| [ ] **MOON-06** | Desktop everywhere | Windows/Linux/macOS desktop; no browser/mobile/Pi target. |
| [ ] **MOON-07** | Preservation | Redump verification, CHD, state import/export, movies, metadata. |
| [ ] **MOON-08** | Video quality | True high-res CEL, shader presets, HDR, NTSC, 4K60. |
| [ ] **MOON-09** | Performance leader | 4K60 on mid-range desktop, 1080p60 on Tier B desktop, low latency. |
| [ ] **MOON-10** | Community | Compatibility DB, plugins, docs, localization, transparent roadmap. |

---

## 6. Recommended sequencing

Timelines are indicative and assume a focused developer or small team. They are deliberately aggressive; adjust them to the actual contributor capacity. The ordering matters more than the calendar.

### Phase 1 - Opera parity (0-3 months)

**Goal:** close the accuracy/compatibility gap and make every local disc work.

- CORE-01..07, CORE-11..13
- SYS-01..04, SYS-13..15, SYS-17..18
- VID-04..06
- AUD-01
- UX-11..12
- ECO-04
- ENG-01, ENG-03

**Exit criteria**
- 594/594 discs load and render.
- >=90% Playable/Good.
- PAL, CD-DA, ODE, lightgun/trackball/mouse work.
- Differential harness runs nightly.
- No known CD/CEL/ARM timing regressions.

### Phase 2 - Best-in-class UX (3-6 months)

**Goal:** match RetroArch/Opera's ecosystem features in the standalone shell.

- UX-01..06, UX-08..10, UX-13..16, UX-19..20
- VID-07..09
- AUD-02..03
- ENG-02, ENG-06

**Exit criteria**
- Rewind, fast-forward, netplay, cheats, achievements, TAS, shader presets, recording, localization, installers, auto-update.
- Crash-free soak passes.

### Phase 3 - Differentiators (6-12 months)

**Goal:** features no other 3DO emulator has.

- VID-01..03, VID-10..12
- SYS-05..06, SYS-19..22
- ECO-06..07
- UX-21..24
- ENG-04..05, ENG-09..10

**Exit criteria**
- True high-res CEL.
- Debugger/TAS/Lua suite.
- Public compatibility dashboard.
- Signed releases.

### Phase 4 - Preservation and community (ongoing)

- BAK-01..19
- MOON-01..10
- Documentation, localization, compatibility reports, community workflow.

---

## 7. What to build first: the critical path

If only one developer is available, do these in order:

1. **CORE-04 CD-ROM timing** - the largest compatibility risk.
2. **CORE-03 CEL busy timing** - the largest accuracy gap after CD.
3. **SYS-01 CD-DA + SYS-02 ODE** - completes the disc story.
4. **SYS-03 PAL + SYS-04 BIOS manager** - fixes the BIOS/region failure class.
5. **SYS-15 input devices** - unlocks arcade/mouse/flightstick titles.
6. **CORE-11 differential harness** - makes every later change measurable.
7. **UX-01 rewind + UX-02 fast-forward** - biggest UX win per effort (done 2026-11-01).
8. **UX-11 disc browser + UX-12 BIOS/NVRAM manager** - closes the 4DO UX gap (both done 2026-11-01).
9. **VID-07 shader presets + AUD-01 audio settings** - quality-of-life.
10. **ECO-04 + ECO-10 platform validation and compatibility portal** - prove the standalone build everywhere it ships.

Do not start with netplay or a browser build. They are valuable, but they multiply a core that must first be correct.

---

## 8. Validation and test strategy

### 8.1 Accuracy

- **CPU conformance:** every ARM instruction, flag, mode, bank, and cycle count; both timing modes.
- **Bus/timing:** CLIO timers, FIQ/IRQ latency, DMA, SPORT, CD ready cycles, CEL busy.
- **Hardware test ROMs:** CPU, MADAM, VDLP, DSP, CD, SPORT, CLIO.
- **Differential harness:** same BIOS/disc/input/frame on 5DO and Opera; compare PC, frame hash, audio hash, state hash, CD transcript.
- **Tolerance policy:** zero unexplained divergence; known divergences documented with a test.

### 8.2 Compatibility

- **Full-collection sweep:** 594 discs, all BIOS variants, 3k/12k/60k frames, screenshot + JSON.
- **BIOS-aware matrix:** record the BIOS used per title; auto-select.
- **Playthrough scripts:** at least one per title; deterministic save/load.
- **Visual review:** screenshot diff against Opera; no black/cropped/wrong-palette frames.
- **Input review:** each device type; six-player.

### 8.3 Performance

- **Per-PR:** synthetic benchmark, zero allocations, core p99 <=8 ms, total p99 <=16.667 ms.
- **Nightly:** real-content Road Rash/Lemmings, 4K GPU probe, Tier B.
- **Release:** 4K60 all filters; 1080p60 Tier B desktop; 24h soak.

### 8.4 Security and robustness

- Fuzz CPU instructions, CD commands, state files, CUE sheets, CHD metadata, Opera FS.
- No unhandled exceptions; clean typed errors.
- Bounds checks on all guest-controlled offsets.
- SBOM and dependency review.

### 8.5 Release

- Signed artifacts; checksums; SBOM.
- Smoke tests on every claimed RID.
- Upgrade/downgrade test for settings/states.
- Legal/attribution sign-off (recorded 2026-11-02).

---

## 9. Risks, non-goals, and legal

### 9.1 Risks

| Risk | Impact | Mitigation |
|---|---|---|
| CD/CEL timing changes break golden titles | High | Differential harness first; one change at a time; feature flags. |
| Netplay/rewind destabilize the core | High | Deterministic state; hashes; optional features; soak tests. |
| Feature creep delays accuracy | High | Accuracy-first sequencing; explicit non-goals. |
| Legal restrictions block distribution | High | Counsel review; no BIOS/game content; attribution; donationware. |
| Tier B/arm64 performance disappoints | Medium | Measure early; JIT/GPU CEL only if needed. |
| Localization/UI work balloons | Medium | Resource pipeline; community translations; incremental. |

### 9.2 Non-goals

- Shipping BIOS ROMs, game images, or copyrighted assets.
- Producing a libretro core; 5DO is a standalone emulator and uses Opera's libretro core only as a behavioral reference.
- Native executable-memory JITs in the pure C# core.
- 4K internal CEL rendering as the default.
- Emulating non-3DO hardware (M2, CoJag, etc.).
- A full social platform before the core is correct.
- Commercial distribution without rights-holder approval.

### 9.3 Legal notes

- 5DO is donationware under the FreeDO modified GNU LGPL. Keep the non-commercial restriction and attribution.
- Do not embed Opera Boot, BIOS files, shader presets, cover art, or game databases until their licenses are reviewed and compatible.
- A built-in RSA/unsigned-disc bypass has legal implications; prefer documenting user-provided patched BIOS and obtain counsel before shipping circumvention code.
- Keep `THIRD-PARTY-NOTICES.md`, `docs/Licensing.md`, and `docs/ReleaseLegalReview.md` current.

---

## 10. Appendix A: Reference feature inventory

### A.1 Opera core options

From `References/opera-libretro-master/libretro_core_options.c`:

- BIOS (rom1), Font (rom2), embedded Opera Boot.
- CPU overclock 0.5x-4.0x.
- Memory capacity 2/4/8/14/15 MB DRAM, 1/2 MB VRAM.
- Random seed (random/fixed).
- CD speed (unbounded, 1x-32x).
- Region/mode (NTSC, PAL1, PAL2).
- VDLP pixel format (0RGB1555, RGB565, XRGB8888).
- VDLP bypass CLUT.
- HiRes CEL rendering.
- MADAM matrix engine (hardware/software).
- OperaOS SWI HLE.
- Threaded DSP.
- NVRAM storage (per game/shared), version 0-9.
- Active input devices 0-8.
- Hide lightgun crosshairs.
- Debug output (KPRINT).

### A.2 Opera input devices

From `References/opera-libretro-master/lr_input.c`:

- 3DO Joypad.
- 3DO Flightstick.
- 3DO Mouse.
- 3DO Lightgun.
- Arcade Lightgun.
- Orbatak Trackball.

### A.3 4DO consumer features

From `References/4DO-master/FourDO/Properties/Settings.settings` and `FourDO/UI/Main.Designer.cs`:

- BIOS1 + BIOS2/Kanji.
- CD image + physical CD drive.
- Disc Browser (browse/extract whole disc/directory).
- Game Information.
- Save slots, auto-load last save, auto-open last game.
- Void area patterns, gray border, smooth resize, aspect ratio, auto-crop.
- HQ2x/3x/4x, double-resolution rendering.
- Seven languages.
- Audio volume + buffer size.
- CPU clock.
- High-resolution mode.
- Inactive-window pause/ignore input.
- Screenshots, pause, frame advance, reset.

### A.4 5DO evidence and gap locations

| Topic | 5DO location | Gap |
|---|---|---|
| ARM timing | `src/FiveDo.Cpu/Arm60.cs:24` (`Ncycle=4`) | No `Ncycle=2` mode. |
| CEL timing | `src/FiveDo.Machine/FiveDoMachine.cs:479-483` | Synchronous CEL; cycles discarded. |
| CD | `src/FiveDo.Cdrom/CdromDevice.cs` | FreeDO model; no CD-DA/ODE/subcode. |
| CUE | `src/FiveDo.Storage/CueSheet.cs` | First data track only. |
| CHD | `src/FiveDo.Storage/Chd/ChdDiscImage.cs` | Data track only. |
| Memory | `src/FiveDo.Core/EmulatorConstants.cs:34` | Fixed 3 MB visible; no capacity options. |
| BIOS | `src/FiveDo.Storage/BiosImage.cs`; CLI `Program.cs:220` | BIOS2 supported in core, not exposed; no auto/region/Kanji. |
| Settings | `src/FiveDo.Host/Models/AppSettings.cs` | No audio buffer, BIOS2, region, CPU clock, memory, input devices, rewind, etc. |
| Input | `src/FiveDo.Input/PbusEncoder.cs`, `PbusChainEncoder.cs`, `PbusDevices.cs` | Joypad plus flightstick, mouse, lightgun, arcade lightgun, and Orbatak trackball records. |
| Video | `src/FiveDo.Video/Vdlp.cs` | 240-line NTSC only; high-res line-double. |
| Reference plan | `docs/ReferenceStrategy.md` §7 | Differential harness, CD port, ARM mode still unchecked. |

---

## 11. Appendix B: Definition of done for "best in the world"

These are **post-1.0 goals**, not v1.0 release blockers. A release can be called "the best 3DO emulator in the world" only when all of the following are true:

- [ ] 594/594 local discs load and render; >=95% Playable/Good; failures documented.
- [ ] Automated differential harness runs nightly and shows no unexplained divergence.
- [ ] CPU, CLIO, MADAM, CD, DSP, SPORT, VDLP, and input devices match Opera or hardware on the test suite.
- [ ] PAL, CD-DA, multi-track, ODE, mouse, lightgun, arcade lightgun, flightstick, and trackball work.
- [ ] BIOS/region/fix/input auto-profiles make known titles "just work".
- [~] Rewind, fast-forward, and per-game cheats are shipped (2026-11-01); OSD notifications and the in-game quick menu are shipped (2026-11-01); netplay, achievements, TAS, shader presets, recording, and debugger UI remain.
- [ ] 4K60 on Tier A; 1080p60 on Tier B desktop; zero steady-state allocations.
- [ ] Windows/Linux/macOS x64/arm64 are validated. WebAssembly, Android/iOS, and Raspberry Pi are out of scope.
- [ ] Installers, signing, notarization, auto-update, portable mode, and file associations are shipped.
- [ ] 2,000+ tests, coverage gate, fuzzing, SBOM, and 24h soak pass.
- [x] Legal/attribution sign-off is complete (2026-11-02).
- [ ] Releases are signed and reproducible.
- [ ] Documentation, compatibility dashboard, and contribution workflow are public and current.

---

## 12. Appendix C: One-page priority cheat sheet

**Do first (accuracy):** CORE-04, CORE-03, CORE-01, CORE-02, SYS-01, SYS-02, SYS-03, SYS-04, SYS-15, CORE-11.

**Do next (UX):** UX-19, VID-07, AUD-01, UX-13, UX-14, UX-16, ECO-04.

**Do later (differentiators):** VID-01, VID-02, VID-03, ECO-02, ECO-03, ECO-05, UX-21, UX-24.

**Never regress:** pure managed core, determinism, zero allocations, CHD, CLI/JSON, save-state compression, AOT/trimming, docs, tests, legal notices.

---

*This document is intentionally a living checklist. Update it when an item lands, when a reference changes, or when the compatibility evidence changes. The goal is not to have the longest feature list; it is to have the most trustworthy 3DO emulator, with the best shell around it.*

---

## 13. Opera parity and next-generation enhancement backlog (2026-10-31 audit)

**Purpose:** this section is the current single source of truth for fixes and enhancements discovered by auditing the merged Opera/libopera pull requests and issue tracker against 5DO. The earlier sections remain the historical roadmap and comparison; this section is the execution order. Do not delete earlier content when updating this section.

**Scope:** 5DO remains a standalone emulator. Opera/libopera is a behavioral reference only. No native core, libretro core, or copied C/C++ implementation is added to the product.

**Status legend:** `Present` = 5DO contains the same behavior/bug; `Partial` = related behavior exists but is incomplete; `Covered` = already implemented and only needs validation; `Deferred` = valid work but not in the current accuracy batch; `Blocked` = depends on another item or legal review.

### 13.1 Current evidence snapshot

- `1,125` tests pass after the differential-harness session.
- Commit `bae818e` added `tools/FiveDo.DiffHarness`, CLI checkpoint/audio/state/image capture, shared frame/audio hashers, smoke/showcase/full manifests, reports, and the optional self-hosted workflow.
- Lemmings 300-frame smoke video hash matches OperaCore (`0x110E7DC5`).
- The five-title showcase has 2 video matches (Wolfenstein, Alone) and 3 video divergences (Lemmings, Gex, Road Rash).
- The strict PC gate currently reports 0/5 matches with large PC deltas; frame-hash agreement alone is not treated as proof of timing parity.
- `benchmarks/diff/full.json` is a portable 594-title manifest generated from the local roots. Roots are machine-specific and must be edited on another machine.
- The stock `OperaCore.dll` lacks debug exports. The harness auto-detects an optional locally built debug core; its CD status/poll fields are not the same fields as 5DO `DriveStatus`/`Poll` and must not be compared directly.
- Current 5DO defaults remain FreeDO-compatible timing. The `Accuracy` profile enables ARM60 N=2, Opera CLIO scheduling, CEL busy pacing, cycle-based CD reads, and stock 2x CD speed. Risky parity changes must remain behind the accuracy profile until differential evidence is green.

### 13.2 How to use this section

1. Pick the highest unchecked item in the execution order in 13.6.
2. One behavior per pull request; do not mix timing, rendering, and UX work.
3. Add the unit/integration test before or with the behavior change.
4. Run the full test suite plus the diff-harness smoke and the affected title checkpoints.
5. Record before/after frame/audio/state hashes in `docs/TestCorpus.md` and the manifest.
6. If a change alters golden hashes, review the visual/differential evidence before re-baselining.
7. Update the status in this section and in the matching CORE/SYS row when an item lands.
8. Keep old save states loading. Bump the section version and add a migration path when state layout changes.

### 13.3 P0 code-level Opera parity fixes

These are concrete, localized differences found by comparing the merged Opera PRs with 5DO source. They are the first implementation batch.

| ID | Opera reference | 5DO evidence | Status | Required action | Effort |
|---|---|---|---|---|---|
| **OP-01** | PR 293, `opera_vdlp.c` | `src/FiveDo.Video/Vdlp.cs` uses `_headVdl` as both CPU-programmed head and active traversal pointer; zero VDL entries set a 511-line delay; there is no retained `next_vdl` or latched `force_line`. | **Done 2026-10-31** | Split the retained `_headVdl` from the active `_nextVdl`/`_currentVdl` cursor, followed zero entries, and process a zero-persist successor on the same line. VDLP state is version 3 with v1/v2 migration. The Opera scanline-5 boundary is deferred until OP-SYS-05 aligns 5DO's visible start line (16) with Opera's (21); the line-0 cadence keeps the Lemmings smoke matching. Tests cover zero-entry traversal, same-line successor processing, and state roundtrip. | M |
| **OP-02** | PR 273, `opera_arm.c` | `src/FiveDo.Cpu/Arm60.cs` rotates an immediate but never updates `_carryOut`; `SetC(_carryOut)` then uses a stale carry. | **Done 2026-10-31** | Immediate rotates now use the barrel-shifter no-carry path; tests cover zero-rotate carry preservation and rotated-immediate bit 31. | S |
| **OP-03** | PR 266, `opera_dsp.c` | `src/FiveDo.Dsp/Dsp.cs` returns two `FastRandom()` values for CPU-supplied DSP FIFO reads `0xF0-0xFC` instead of `_imem[address - 0x80]`. | **Done 2026-10-31** | `0xF0-0xFC` now returns the mirrored `0x70-0x7C` instruction word and does not consume the PRNG. Test covers the high FIFO path. | S |
| **OP-04** | PR 268, `opera_madam.c` | `src/FiveDo.Madam/CelRasterizer.cs` uses `maxx = xpoints[3]` and `xpoints[1]` without the inclusive `+1` endpoint. | **Done 2026-10-31** | Both arbitrary-fill branches now use the inclusive endpoint; the quad test asserts the second column is drawn. | S |
| **OP-05** | PR 270, `opera_madam.c` | `CelRasterizer` clamps MARIA `drawHeight` to 1.0 and does not return after the first MARIA texel in scale/arbitrary paths. | **Done 2026-10-31** | Removed the MARIA height clamp, uses the real `VDY1616`, and returns after the first MARIA pixel in scale/arbitrary paths. Added a MARIA scale test. | M |
| **OP-06** | PR 272, `opera_madam.c` | `CelRasterizer.DrawLiteralCel` ignores `PRE1_LRFORM` and advances source rows linearly. Only the separate 16-bpp LR CEL path handles LR-form. | **Done 2026-10-31** | Literal CELs now honor `PRE1_LRFORM`, use LR row-pair addressing, and read LR-form bit order. Added `CelBitReader.ReadLrForm` and a 1-bpp LRFORM row-pair test; the 16-bpp LR CEL path is unchanged. | M |
| **OP-07** | PR 262, `opera_madam.c` | `src/FiveDo.Madam/CelEngine.cs` treats `PRE0_BPP=0` and `7` as no-ops. | **Done 2026-10-31** | BPP 0 is decoded as the 1-bpp group and loads a 2-entry PLUT; BPP 7 remains reserved. Added decoder/PLUT tests. | S |
| **OP-08** | PR 263, `opera_madam.c` | `CelRasterizer.DrawPackedCel` does not apply `PRE0_SKIPX`; literal CELs do. | **Done 2026-10-31** | Added packed-CEL `SKIPX` handling for literal, transparent, repeat, and EOL packets, with bit-reader advancement. Added a packed literal-run test. | M |
| **OP-09** | PR 255/253, `opera_madam.c` | `src/FiveDo.Madam/PixelDecoder.cs` has no blue-LSB substitution; `PixelProcessor` transparency does not use the raw VH bits. | **Done 2026-10-31** | Blue-LSB substitution and pre-substitution transparency are implemented; `PixelDecodeResult.RawVhBits` is threaded through all texel and projector paths. Added a raw-VH preservation test. | M |
| **OP-10** | PR 261, `opera_madam.c`/`opera_vdlp.c` | `Madam.HandleDma8` only checks `0x8000`; VDLP does not gate CLUT/video DMA on `CLUTXEN`/`VSCTXEN`. | **Done 2026-10-31** | Added named MADAM MCTL flags, wired CLUT/video enable delegates into VDLP, and gated VDL processing/bitmap copy. Defaults remain enabled until the guest first writes MCTL. Added a flag test; Lemmings smoke still matches. | S |
| **OP-11** | PR 292, `opera_clio.c` | `src/FiveDo.Clio/Clio.cs` completes an input FIFO on the next read, does not reset the pointer when `StartAddress` is written, and reloads `next` without checking DMA enable. | **Done 2026-10-31** | FIFO completion now happens before the CPU sees the end address, reload checks the DMA channel enable bit, and `StartAddress` writes reset the pointer. Added completion/reload tests. | M |
| **OP-12** | PR 269, `opera_madam.c` | `src/FiveDo.Madam/Madam.cs` decodes REGCTL0/1 stride with the old `((val & 1) << 7) + ...` formula. | **Done 2026-10-31** | Replaced the old stride formula with `DecodeRegCtlMod` using the Opera byte groups. Added read/write stride tests for bits 0, 1, and 7. | S |
| **OP-13** | PR 269, `opera_madam.c` | `src/FiveDo.Madam/PixelProcessor.cs` case 3 uses `Scale(4, dv1, ...)` instead of the decoded PIXC multiplier bits. | **Done 2026-10-31** | `PDC_MFONLY` now uses the decoded pixel's high R/G/B bits as the scale index. Added a focused PIXC test and updated the reference grid hash. | S |
| **OP-14** | PR 269, `opera_madam.c` | 5DO still uses FreeDO `GraphicsStepY`/`BitTiming6` hacks and advances `XPosition1616` or `YPosition1616` in `FinalizeVisualPosition`. There is no `XYPOSH`/`XYPOSL` load/store or `StoreXYPosFromDrawHeight`. | **Done 2026-10-31** | Added XYPOSH/XYPOSL load/store, YOXY=0 continuation from the hardware origin, and `StoreXYPosFromDrawHeight` after every CEL (including culled/empty draws). Retired the automatic `GraphicsStepY`/`BitTiming6` mappings and the literal-CEL doubled-Y read; the enum values remain loadable for old states. Samurai Shodown is identical with and without `GraphicsStepY`; AITD's credits now use the actual source row and no longer stretch vertically; DinoPark renders correctly. Added a two-CCB YOXY-chain test and a retired-flag read-row test. | L |
| **OP-15** | PR 281, `opera_madam.c` | Literal CEL width is reduced by SKIPX before clipping, but the exhausted-span/position-advance behavior is not fully modeled. | **Done 2026-10-31** | Literal `SKIPX >= width` now draws nothing and advances the CEL origin by the full sprite height. Added an exhausted-span test. | S |

### 13.4 P0 system-level parity and architecture

These are not one-line fixes. They should be staged behind feature flags or the accuracy profile and validated with differential traces.

| ID | Opera reference | 5DO gap | Status | Required action | Effort |
|---|---|---|---|---|---|
| **OP-SYS-01** | PR 288, `opera_arm.c`, `opera_clio.c`, `opera_xbus.c` | No low-ROM overlay; CPU starts at `0x03000000`; `Clio.Peek(0x414)` returns hard-coded `0x4000`; no DIPIR latches or XBUS access aborts; `GetPoll` masks the high nibble when bit 7 is set. | **Partial 2026-10-31** | Added `RomOverlay` state (low BIOS reads, DRAM-write clear, VRAM-write preservation, reset reassertion, memory state v2). Added CLIO DIPIR1/DIPIR2 latches, before-reset conversion, DIPIR FIQ, XBUS access-abort rules, the selected-device address, and Opera's unmasked XBUS poll high nibble. DIPIR/aborts are opt-in (`EmulatorTuning.OperaDipir`) until OP-SYS-03 supplies the soft-reset/clear-DIPIR path; enabling them changes the Lemmings boot. The hard-coded `0x414` fallback remains for FreeDO mode. | XL |
| **OP-SYS-02** | PR 288/292/260, `opera_cdrom.c` | `CdromDevice.SetPoll` only preserved the high nibble and set the low nibble; no `POLREMASK` (`0x08`) local reset, no `POLRE` (`0x80`) media access; 5DO uses a `0x40` pulse heuristic; read-ahead/status publication is approximate. | **Partial 2026-10-31** | Added Opera poll semantics under the accuracy timing mode: `POLREMASK` local reset, `POLRE` write-one-to-clear media latch, reset gating for command/status/data, media-access FIQ enable, media load/eject status, and state-load local reset. Read-ahead remains limited to data-sector prefetch; CD-DA read-ahead and the `0x40` READ_ID heuristic remain for SYS-01/transcript work. | XL |
| **OP-SYS-03** | PR 288, `opera_3do.c`/`opera_clio.c` | `Clio.Poke(0x28)` stores the value and returns `value == 0x30`; there is no CSTAT soft-reset semantics or propagation to ARM/DSP/MADAM/XBUS. | **Partial 2026-10-31** | Added CSTAT reset/clear-on-zero/DIPIR-reset bits, an ARM `SoftReset` that preserves SPSR_svc/R14_svc and jumps to 0, MADAM/DSP soft reset, XBUS controller reset, DIPIR-clear propagation, and the machine callback. The full path is opt-in (`EmulatorTuning.OperaSoftReset`) because enabling it before transcript validation changes the Lemmings boot; unit tests cover the register and ARM semantics. | L |
| **OP-SYS-04** | PR 287, `opera_mem.c`/`opera_sport.c` | Fixed 3 MB visible RAM/VRAM; no 2/4/8/14/15 MB DRAM or 1/2 MB VRAM; SPORT high-res mirroring is close but not the full Opera behavior. | **Partial** | Add `MemoryCapacity` (SYS-07), valid-config validation, allocation/layout, state snapshot, and SPORT mask/plane behavior. Keep stock default. | L |
| **OP-SYS-05** | PR 254, `opera_clock.c`/`opera_region.c` | Fixed 60 Hz frame loop; no 263-line NTSC / 59.94 Hz field timing or PAL1/PAL2. | **Partial** | Add region/timing modes (SYS-03), 263/312 scanlines, 50/59.94 Hz, 288-line output, and PAL BIOS handling. Test Battle Chess Europe. | L |
| **OP-SYS-06** | PR 283, `opera_3do.c`/`prng16.c`/`prng32.c` | DSP PRNG seed/state existed, but CLIO reused the DSP generator and there was no separate 16/32-bit PRNG state. | **Done 2026-10-31** | CLIO now has its own splitmix32 state for `0x17F0`, seeded from `EmulatorTuning.RandomSeed` when provided, and saved in CLIO state version 3 with v1/v2 migration. The DSP keeps its separate FreeDO-compatible LCG state. Fixed-seed replay and state roundtrip tests cover the CLIO generator. | S |
| **OP-SYS-07** | PR 290, `opera_bios.c` | 5DO requires a user BIOS; no embedded open-source boot ROM. | **Deferred** | Legal review first. Do not embed copyrighted BIOS. Consider an open-source boot option only if compatible with 5DO's license. | M |
| **OP-SYS-08** | PR 256/277, `lr_input.c`/`opera_pbus.c` | Joypad-only input; no mouse, lightgun, arcade lightgun, flightstick, or Orbatak trackball. | **Partial 2026-11-01** | Implement SYS-15. Use Opera's PBUS packet layouts and Orbatak 24-bit split, but reimplement in C#. `InputDeviceSet`/`PbusChainEncoder` now emit the exact device records, and CLI/settings/pointer input, pointer-port routing, and calibration are wired; scripted representative-title validation remains. | L |
| **OP-SYS-09** | PR 256, `opera_cdrom.c` | No ODE/disc-swap/CD-DA/multi-track. | **Deferred** | SYS-01/SYS-02. 3DO titles generally reboot on disc change; per-game NVRAM (SYS-14) may cover multi-disc saves. Verify before building a swap UI. | L |
| **OP-SYS-10** | PR 285/223, CHD/libchdr | CHDSharp 1.6 is the current dependency; zstd/CHD support had to be verified against the local corpus. | **Done 2026-10-31** | CHDSharp 1.6.0 already supports Zstd (`ChdCodec.Zstd`/`Cdzstd`) and ships `VendoredZSTD.dll`; no code change or native dependency is needed. Opened all 589 local CHDs: they use CDLZMA/CDZLIB/CDFLAC, none zstd. Add a zstd CHD fixture when one is available. | S |

### 13.5 Opera issue triage

This table maps the open/closed Opera issue tracker to 5DO. "Open upstream" means the issue is not a proven fix to copy; reproduce it locally first.

| Opera issue | 5DO relevance | Action |
|---|---|---|
| **#291 Johnny Bazookatone horizontal line** | Very likely the same VDLP bug | OP-01; add a title checkpoint. |
| **#177 Sailor Moon crashes/freezes** | Likely reset/DIPIR/CD cluster | Reproduce with `Accuracy`; then OP-SYS-01/02/03. |
| **#206 Autobahn Tokio intro freeze** | CEL busy pacing | Test `--profile accuracy` and `--cel-timing opera` first; only port more if it still fails. |
| **#279 Primal Rage static** | DSP FIFO or CD-DA | OP-03 first; if it is CD audio, defer to SYS-01. |
| **#230 Crash 'n Burn noises** | DSP FIFO/CD | OP-03; then OP-SYS-02 if CD-related. |
| **#123 Put-Put music / #189 Plumbers sync** | CD-DA/CD timing | SYS-01 and OP-SYS-02; do not add a title hack. |
| **#244/#165 games too fast** | Open upstream; no single known fix | Add Casper/Eye of Typhoon/Jurassic Park/Autobahn to the sweep; test accuracy profile. |
| **#233 Yu Yu Hakusho crash / #110 Theme Park** | Unknown; open upstream | Add to the differential manifest and trace before changing code. |
| **#176 ALG reload** | Lightgun input, not core | Blocked on SYS-15. |
| **#179 disc swapping** | Upstream says NVRAM handles multi-disc | Verify multi-disc titles with per-game NVRAM before building a swap UI. |
| **#223 libchdr + zstd** | CHDSharp may already support it | Verify; likely no core work. |
| **#184 higher-res CEL** | Feature request | VID-01, not a bug. |
| **#286/#231 analog-to-D-pad** | Libretro frontend-specific | Not applicable to 5DO's standalone input layer. |

### 13.6 Execution order and gates

**Phase 0 - baseline and harness (already partially complete).**
- Keep `bae818e` as the harness baseline.
- Run the full 594-title differential collection with `--no-images --cleanup-iso --resume` on the self-hosted runner.
- Build the first known-divergence allowlist from the full report.
- Triage PC deltas starting with Gex/Road Rash (176/284 bytes at frame 3000), then Lemmings (11,076 bytes).
- Record the results in `benchmarks/diff/` and `docs/TestCorpus.md`.

**Phase 1 - safe, isolated parity fixes (recommended next coding batch).**
1. OP-01 VDLP.
2. OP-02 ARM immediate carry.
3. OP-03 DSP CPU FIFO.
4. OP-04 arbitrary span endpoint.
5. OP-05 MARIA texel.
6. OP-07 BPP=0.
7. OP-08 packed SKIPX.
8. OP-11 CLIO FIFO.
9. OP-12 REGCTL stride.
10. OP-13 PIXC PDC_MFONLY.
11. OP-09 blue-LSB substitution.
12. OP-06 literal LRFORM (done 2026-10-31).
13. OP-15 literal SKIPX clamp.

**Phase 2 - CEL origin and hack retirement.**
- OP-14 YOXY/CEL origin.
- Retire `GraphicsStepY`, `BitTiming6`, and the old scale/REGCTL hacks after Samurai Shodown, AITD, and DinoPark pass.
- Keep legacy fix flags loadable for old save states.

**Phase 3 - CD/XBUS/DIPIR/reset.**
- OP-SYS-02 CD poll/media access/read-ahead.
- OP-SYS-01 ROM overlay and DIPIR.
- OP-SYS-03 soft reset/CSTAT.
- Add command transcripts, boot/reset tests, and state migrations.

**Phase 4 - memory, region, input, media.**
- OP-SYS-04 memory capacity/SPORT.
- OP-SYS-05 PAL/NTSC timing.
- OP-SYS-08 input devices.
- OP-SYS-09 ODE/CD-DA/multi-track.
- OP-SYS-10 CHD-zstd verification.

**Gate for every phase:**
- `dotnet test 5DO.sln` passes.
- `dotnet format --verify-no-changes` passes.
- Diff-harness smoke and the affected title checkpoints pass or have an explicit allowlist entry with a reason.
- No steady-state allocations added to the hot path.
- Save-state roundtrip and old-state load tested.
- `docs/PortingNotes.md`, `docs/Compatibility.md`, `docs/TestCorpus.md`, and this section updated.

### 13.7 Test and evidence contract

- Add unit tests for every behavior change: normal, boundary, and error cases.
- Add at least one integration test through `FiveDoMachine` for machine-visible behavior.
- Add a checkpoint entry for each affected title in `benchmarks/diff/opera-parity.json`.
- For hash-changing fixes, record the old and new frame/audio/state hashes and the visual/differential reason.
- Do not re-baseline a golden hash without a captured frame or differential report.
- For timing changes, record the exact profile, ARM clock, CD speed, BIOS, disc hash, and frame count.
- For state-layout changes, bump the section version and add a migration test.
- Keep the default profile safe until the accuracy profile has full-collection evidence.

### 13.8 Recent session evidence and next move

**Completed in the differential-harness session:**
- Removed the libretro-core roadmap item; 5DO stays standalone.
- Added CLI `--checkpoints`, `--audio-hash`, `--state-hash`, `--capture-frames`, checkpoint JSON, `FrameHasher`, `PcmHasher`, and `HashingAudioSink`.
- Added `tools/FiveDo.DiffHarness` with `run`, `validate`, `manifest`, and `baseline`; OperaCore P/Invoke; ISO extraction/staging; allowlists; offset/PC diagnostics; JSON/Markdown/HTML reports; images; `--cleanup-iso`.
- Added `benchmarks/diff/smoke.json`, `showcase.json`, generated `full.json` (594 titles), and the harness README.
- Added the optional self-hosted `.github/workflows/diff-harness.yml`.
- Added unit/integration tests; 1,125 tests pass; formatting clean.
- Committed as `bae818e` and pushed to `origin/master`.

**Next move:**
1. Run the full 594-title differential collection on the self-hosted runner.
2. Build the known-divergence allowlist.
3. Triage PC deltas (Gex/Road Rash, then Lemmings).
4. Start Phase 1 with OP-01 and OP-02.

### 13.9 Do not port

- **PR 248 and PR 250** are closed/unmerged and contain personal hacks; do not use them as a behavioral reference.
- **PR 245** underclock options are already covered by 5DO's `--cpu-clock 0.5x-4x`.
- **PR 256/277** input/peripheral work belongs to SYS-15, not the core accuracy batch.
- Do not port libretro frontend behavior (analog-to-D-pad, rewind, run-ahead) into 5DO's standalone input layer.
- Do not copy Opera code verbatim; reimplement behavior in C# and preserve attribution.
- Do not make a risky timing model the default without full-collection differential evidence.
- Do not add native emulator dependencies, BIOS files, game images, or copyrighted assets.

### 13.10 Consolidated checklist

#### Phase 1 - safe parity fixes

- [x] **OP-01** VDLP zero persistence / next-VDL / force line.
- [x] **OP-02** ARM immediate rotate carry.
- [x] **OP-03** DSP CPU-supplied FIFO.
- [x] **OP-04** MADAM arbitrary CEL span endpoint.
- [x] **OP-05** MADAM MARIA texel rendering.
- [x] **OP-06** MADAM literal LRFORM rows.
- [x] **OP-07** MADAM `PRE0_BPP=0` as 1.
- [x] **OP-08** MADAM packed CEL `SKIPX`.
- [x] **OP-09** MADAM blue-LSB substitution / transparency order.
- [x] **OP-10** MADAM CTL DMA flags.
- [x] **OP-11** CLIO FIFO completion / pointer reset / DMA-enabled reload.
- [x] **OP-12** MADAM REGCTL stride decoding.
- [x] **OP-13** MADAM PIXC `PDC_MFONLY`.
- [x] **OP-15** MADAM literal `SKIPX` clamp/empty span.

#### Phase 2 - CEL origin

- [x] **OP-14** YOXY / CEL origin / `StoreXYPosFromDrawHeight`.
- [x] Retire `GraphicsStepY` and `BitTiming6` after title validation.
- [x] Keep legacy fix flags loadable for old states.

#### Phase 3 - CD/XBUS/DIPIR/reset

- [~] **OP-SYS-01** ROM overlay, DIPIR, XBUS media access (overlay and latch/abort logic done; DIPIR activation waits for OP-SYS-03).
- [~] **OP-SYS-02** CD poll/media access/read-ahead/local reset (poll/reset/media done; CD-DA read-ahead and heuristic retirement pending).
- [~] **OP-SYS-03** CLIO CSTAT soft reset and propagation (implemented and unit-tested; opt-in pending boot transcript).
- [ ] Command transcript and boot/reset tests.

#### Phase 4 - memory, region, input, media

- [ ] **OP-SYS-04** memory capacity and SPORT.
- [ ] **OP-SYS-05** PAL/NTSC timing.
- [x] **OP-SYS-06** PRNG state split/replay validation.
- [~] **OP-SYS-08** mouse/lightgun/arcade lightgun/flightstick/trackball (device models, encoder, CLI/settings, pointer input, and crosshair done; representative-title validation and calibration remain).
- [ ] **OP-SYS-09** ODE/CD-DA/multi-track.
- [x] **OP-SYS-10** CHD-zstd verification.

#### UX

- [x] **UX-01** rewind history with deterministic input replay.
- [x] **UX-02** 2x-10x fast-forward with muted audio.
- [x] **UX-04** per-game cheat engine, desktop manager, and CLI.
- [x] **UX-12** BIOS/NVRAM manager UI with import, assignment, backup, reset, and delete.
- [~] **UX-13** library favorites, play time, launch history, offline metadata, filters, and sorts (external providers and rich media remain).
- [x] **UX-19** OSD notifications and the in-game quick menu.
- [~] **UX-06** deterministic TAS movie record/replay/verify, branching, shell controls, and input display (Lua/editor/lag counter remain).

#### Evidence and release

- [ ] Full 594-title differential run and allowlist.
- [ ] PC-delta triage for Gex/Road Rash/Lemmings.
- [ ] Per-title checkpoints in `benchmarks/diff/opera-parity.json`.
- [ ] Save-state migration tests for every version bump.
- [ ] Update `docs/TestCorpus.md`, `docs/Compatibility.md`, `docs/PortingNotes.md`, and this checklist.
- [ ] No default timing change without full-collection evidence.

---

*End of the 2026-10-31 Opera parity audit. Append future audits below this line; do not remove the historical roadmap above.*

### 13.11 Session log: parity batch 1 (2026-10-31)

**Status:** complete; full suite green.

Implemented:

- **OP-01** VDLP `next_vdl`/force-line/zero-entry traversal, with VDLP state version 2 and v1 migration.
- **OP-02** ARM immediate rotate carry.
- **OP-03** DSP CPU-supplied high FIFO reads.
- **OP-04** MADAM arbitrary CEL inclusive span endpoint.
- **OP-05** MADAM MARIA texel scale/arbitrary early return and real `VDY1616`.
- **OP-06** MADAM literal LRFORM row-pair addressing and `CelBitReader.ReadLrForm`.
- **OP-07** MADAM `PRE0_BPP=0` as the 1-bpp group.
- **OP-08** MADAM packed CEL `SKIPX`.
- **OP-09** blue-LSB substitution, pre-substitution transparency, and raw VH bits through all texel/projector paths.
- **OP-10** MADAM CTL DMA flags and VDLP CLUT/video gating.
- **OP-11** CLIO FIFO completion/pointer reset/DMA-enabled reload.
- **OP-12** MADAM REGCTL stride decoding.
- **OP-13** MADAM PIXC `PDC_MFONLY`.
- **OP-14** XYPOSH/XYPOSL origin model, YOXY=0 continuation, `StoreXYPosFromDrawHeight`, and retirement of the `GraphicsStepY`/`BitTiming6` auto-mappings.
- **OP-15** literal `SKIPX` exhausted-span clamp and origin advancement.

Evidence:

- `dotnet test 5DO.sln`: **1,139 passed**, 0 failed.
- Differential smoke (`benchmarks/diff/smoke.json`, Lemmings 300 frames): **PASS**, 1 match, 0 diverged.
- Differential showcase (`benchmarks/diff/showcase.json`, 3,000 frames): unchanged from the pre-batch baseline: 2 matches (Wolfenstein, Alone), 3 divergences (Lemmings, Gex, Road Rash), and the same 3,000-frame 5DO hashes for all five titles.
- No default timing profile change. The accuracy profile remains opt-in.
- Opera-parity title probes (3,000 frames, accuracy profile): 5DO completed Samurai Shodown (USA/EU), DinoPark Tycoon, Alone in the Dark, Johnny Bazookatone, AutoBahn Tokio, and Primal Rage. The stock OperaCore crashed on Samurai Shodown (`0xC0000005`) in the harness; that is a reference-side crash, not a 5DO failure. The other probes diverged at frame 3000, as expected from the existing PC/timing gap. A Johnny Bazookatone checkpoint image was captured and shows the game scene without the issue #291 horizontal-line artifact.
- New tests cover VDLP zero-entry traversal and state roundtrip, ARM immediate rotate carry, DSP high FIFO, arbitrary span endpoint, MARIA scale, BPP=0, packed SKIPX, literal LRFORM row pairs, MCTL DMA flags, CLIO FIFO completion/reload, REGCTL strides, PIXC `PDC_MFONLY`, raw VH preservation, exhausted literal SKIPX, and YOXY-chain origin advancement.

Next batch:

1. The CD/XBUS/DIPIR/reset system batch (OP-SYS-02, then OP-SYS-01, then OP-SYS-03).
2. Align the VDLP scanline-5 boundary with Opera when OP-SYS-05 adds region timing.

**Files changed in this batch:** `src/FiveDo.Cpu/Arm60.cs`, `src/FiveDo.Dsp/Dsp.cs`, `src/FiveDo.Video/Vdlp.cs`, `src/FiveDo.Madam/CelRasterizer.cs`, `src/FiveDo.Madam/CelEngine.cs`, `src/FiveDo.Madam/CelBitReader.cs`, `src/FiveDo.Madam/PixelDecoder.cs`, `src/FiveDo.Madam/PixelProcessor.cs`, `src/FiveDo.Madam/Madam.cs`, `src/FiveDo.Madam/Models/PixelDecodeResult.cs`, `src/FiveDo.Clio/Clio.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, and the corresponding tests.

### 13.12 Session log: OP-14 retirement and VDLP head/successor fix (2026-10-31)

**Status:** complete; full suite green.

Implemented:

- **OP-14 complete.** Removed the automatic `GraphicsStepY` (Samurai Shodown) and `BitTiming6` (Alone in the Dark) mappings. The enum values stay defined so old save states and per-game overrides still load, but they are now no-ops. The literal-CEL `DrawPixel` path always reads the actual row; the old doubled-Y read was stretching AITD's credits.
- **VDLP head/successor split.** `_headVdl` is now the retained CPU-programmed head, `_nextVdl` is the active successor cursor, and `_currentVdl` is the entry being processed. A zero-persist entry's successor is processed on the same line, matching Opera's `line_cnt <= 0` rule. VDLP state is version 3 with v1/v2 migration.
- **Scanline boundary deferral.** Opera latches the VDL head at scanline 5 because its visible area starts at scanline 21; 5DO still starts visible output at scanline 16. Until OP-SYS-05 aligns the region timing, 5DO keeps the field boundary at line 0. This preserves the Lemmings smoke hash while keeping the corrected traversal.

Validation:

- `dotnet test 5DO.sln`: **1,140 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5` on both engines.
- Five-title showcase (3,000 frames): unchanged; 2 matches (Wolfenstein, Alone), 3 divergences (Lemmings, Gex, Road Rash), and all 5DO hashes identical to the pre-batch baseline.
- Samurai Shodown: 3,000/4,000/5,000/6,000-frame images are identical with and without `GraphicsStepY`.
- Alone in the Dark: with `BitTiming6` disabled, the credits read the actual source row; the doubled-Y stretch is gone. The 9,000-frame harness probe still diverges from Opera for unrelated CD/timing reasons, but the AITD-specific flag is no longer needed.
- DinoPark Tycoon: 3,000-frame image renders correctly with the generic origin model and its existing `BitTiming3` scale fix.
- Harness pitfall fixed in process: `FiveDo.DiffHarness` runs the external Release CLI, so the CLI must be rebuilt after every core change. Earlier smoke/showcase runs that used a stale CLI did not exercise the new code.

**Files changed in this batch:** `src/FiveDo.Core/Models/FixMode.cs`, `src/FiveDo.Host/GameCompatibility.cs`, `src/FiveDo.Madam/CelRasterizer.cs`, `src/FiveDo.Video/Vdlp.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `tests/FiveDo.Tests/Host/GameCompatibilityTests.cs`, `tests/FiveDo.Tests/Madam/CelLiteralRasterizerTests.cs`, `tests/FiveDo.Tests/Video/VdlpTests.cs`, and the docs.

**Next batch:** OP-SYS-02 (CD poll/media access/read-ahead/local reset), then OP-SYS-01 (ROM overlay/DIPIR/XBUS), then OP-SYS-03 (CSTAT soft reset).

### 13.13 Session log: OP-SYS-02 CD poll/local reset (2026-10-31)

**Status:** partial; accuracy-mode poll/reset semantics complete, full suite and smoke green.

Implemented:

- **Opera poll semantics under `CdromTimingMode.OperaCycleBased`.** The default FreeDO-compatible mode keeps the historical poll register behavior.
  - `POLREMASK` (`0x08`) assertion performs a local reset that clears the command/transfer FIFOs, status/data poll bits, pending status, and sector position without changing logical media presence.
  - `POLRE` (`0x80`) is a write-one-to-clear media-access latch. Init sets it; releasing local reset sets it again.
  - `GetStatus`/`GetData`/`SetCommand` return zero or ignore input while local reset is asserted.
  - Media-access FIQ is included when `POLMA` (`0x40`) and `POLMAMASK` (`0x04`) are set.
  - A loaded state with local reset asserted runs the same local reset.
  - Media insert/eject refreshes the drive status; Opera mode reports `READY` only after eject instead of FreeDO's phantom tray/disc/spin bits.
- **Data transfer status publication** remains the existing cycle-based model: the `0x10` status is published after the last requested sector is drained, and the next sector is scheduled only after the current one is consumed.

Remaining:

- CD-DA read-ahead (48 sectors) and audio burst scheduling belong with SYS-01/CD-DA.
- The `0x40` READ_ID idle heuristic remains until command transcripts replace it; it now uses the named `PollMedia` bit.
- Full media-change/DIPIR propagation is OP-SYS-01.

Evidence:

- `dotnet test 5DO.sln`: **1,143 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; 2 matches (Wolfenstein, Alone), 3 divergences (Lemmings, Gex, Road Rash), all 5DO hashes identical to the pre-batch baseline.

**Files changed in this batch:** `src/FiveDo.Cdrom/CdromDevice.cs`, `tests/FiveDo.Tests/Cdrom/CdromDeviceTests.cs`, and the docs.

### 13.14 Session log: OP-SYS-01 ROM overlay (2026-10-31)

**Status:** partial; overlay complete, DIPIR/access aborts remaining.

Implemented:

- `FiveDoMemory` starts with the BIOS overlaid at address zero. While active, CPU low reads return the active ROM bank and low DRAM writes clear the overlay. The clear limit is the 2 MB DRAM size, so VRAM-only writes do not clear it.
- `Reset()` reasserts the overlay. The CPU still starts at `0x03000000`; the overlay covers low reads until the BIOS's first DRAM write.
- Memory state version 2 saves the overlay flag; version-1 states load as post-boot (cleared).

Evidence:

- `dotnet test 5DO.sln`: **1,147 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; all 5DO hashes identical to the pre-batch baseline.
- New tests cover BIOS low reads, DRAM-write clearing, VRAM-write preservation, reset reassertion, and save/load.

Remaining OP-SYS-01 work:

- CLIO DIPIR1/DIPIR2 latches and the DIPIR FIQ.
- XBUS access-abort rules and media-access propagation.
- Replace `Clio.Peek(0x414)`'s hard-coded `0x4000`.

**Files changed in this batch:** `src/FiveDo.Memory/FiveDoMemory.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `tests/FiveDo.Tests/Memory/MemoryByteOrderTests.cs`, and the docs.

### 13.15 Session log: OP-SYS-01 DIPIR and XBUS access (2026-10-31)

**Status:** partial; latch/abort logic implemented, DIPIR activation deferred to OP-SYS-03.

Implemented:

- **CLIO DIPIR.** DIPIR1/DIPIR2 latches record the first two distinct media-access devices, the DIPIR FIQ is raised for the first latch, and the before-reset conversion (`0x4000 | device`) is available for soft reset.
- **Access aborts.** `XbusAccessAborts` implements Opera's rules: no abort without a current latch; one latch allows poll but aborts same-device non-poll access; two latches abort all non-poll access and poll access for other devices.
- **XBUS selected-device address.** The high select bit is preserved for DIPIR and media-access latching.
- **Poll high nibble.** Opera mode returns the full poll value instead of masking the high nibble when select bit 7 is set.
- **Media-access latch.** Poll/command writes call the CLIO latch when the CD reports `poll & POLRE`. The CD's `HasMediaAccess` reflects that bit.
- **Opt-in gate.** `EmulatorTuning.OperaDipir` defaults false. Enabling it before OP-SYS-03's soft-reset/clear-DIPIR path makes Lemmings stall after its first CD command burst, so the default and accuracy profiles leave it off.

Evidence:

- `dotnet test 5DO.sln`: **1,154 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; all 5DO hashes identical to the pre-batch baseline.
- New tests cover DIPIR latches/FIQ, abort rules, before-reset conversion, FreeDO's `0x414` fallback, XBUS poll high-nibble behavior, and media-access latching.

Remaining:

- OP-SYS-03 CSTAT soft reset/clear-DIPIR propagation, then enable `OperaDipir` and validate the boot transcript.
- XBUS controller reset on DIPIR reset (without resetting external devices).

**Files changed in this batch:** `src/FiveDo.Core/Models/EmulatorTuning.cs`, `src/FiveDo.Core/Interfaces/IClioXbus.cs`, `src/FiveDo.Core/Interfaces/IXbusDevice.cs`, `src/FiveDo.Clio/Clio.cs`, `src/FiveDo.Xbus/Xbus.cs`, `src/FiveDo.Cdrom/CdromDevice.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `tests/FiveDo.Tests/Clio/ClioTests.cs`, `tests/FiveDo.Tests/Xbus/XbusTests.cs`, and the docs.

### 13.16 Session log: OP-SYS-03 CSTAT soft reset (2026-10-31)

**Status:** implemented and unit-tested; opt-in pending a boot transcript.

Implemented:

- **CSTAT.** `Clio.Poke(0x28)` now follows Opera's reset/clear-on-zero/DIPIR-reset bits. `RESET_SOFT` (0x10) and `RESET_CLEAR_DIPIR` (0x20) invoke the machine callback; the CSTAT DIPIR_RESET bit is set when both are present. FreeDO-compatible mode keeps the old `value == 0x30` behavior.
- **ARM soft reset.** `Arm60.SoftReset` saves the interrupted CPSR in SPSR_svc, preserves the next PC in R14_svc, switches to SVC with IRQ/FIQ disabled, sets PC=0, and clears FIQ/data-abort pending.
- **Machine propagation.** The handler reasserts the ROM overlay, soft-resets MADAM (stops CEL and clears status bits), resets/stops the DSP, and optionally resets the XBUS controller and converts the DIPIR latches to their before-reset form.
- **Opt-in.** `EmulatorTuning.OperaSoftReset` defaults false. Enabling it together with `OperaDipir` changes the Lemmings boot transcript before validation, so the accuracy profile leaves both off.

Evidence:

- `dotnet test 5DO.sln`: **1,157 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; all 5DO hashes identical to the pre-batch baseline.
- New tests cover CSTAT reset/callback, clear-on-zero bits, ARM soft-reset state, and the existing DIPIR/XBUS behavior.

Remaining:

- Capture a CLIO CSTAT/DIPIR boot transcript and compare the combined opt-in path with Opera.
- Then enable `OperaDipir`/`OperaSoftReset` in the accuracy profile and re-baseline the affected title checkpoints.

**Files changed in this batch:** `src/FiveDo.Core/Models/EmulatorTuning.cs`, `src/FiveDo.Cli/Program.cs`, `src/FiveDo.Clio/Clio.cs`, `src/FiveDo.Cpu/Arm60.cs`, `src/FiveDo.Madam/Madam.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `src/FiveDo.Xbus/Xbus.cs`, `tests/FiveDo.Tests/Clio/ClioTests.cs`, `tests/FiveDo.Tests/Cpu/ArmDataProcessingTests.cs`, and the docs.

### 13.17 Session log: OP-SYS-06 separate CLIO PRNG (2026-10-31)

**Status:** complete.

Implemented:

- CLIO register `0x17F0` now uses a dedicated splitmix32 generator (Opera `prng32`) instead of the DSP LCG.
- `Clio.SetRandomSeed` seeds it from `EmulatorTuning.RandomSeed` when the CLI/user supplies one; otherwise it starts at `0xDEADBEEF`.
- CLIO state version 3 saves the 32-bit PRNG state; v1/v2 states load the default state.
- The DSP keeps its separate FreeDO-compatible LCG, so the two generators no longer share a sequence.

Evidence:

- `dotnet test 5DO.sln`: **1,159 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: Wolfenstein and Alone still match; Gex's 3,000-frame hash changed because it reads `0x17F0`, and its captured image still shows a valid game scene. Lemmings and Road Rash hashes are unchanged.
- New tests cover the splitmix32 sequence, state roundtrip, and existing fixed-seed behavior.

**Files changed in this batch:** `src/FiveDo.Clio/Clio.cs`, `src/FiveDo.Machine/FiveDoMachine.cs`, `tests/FiveDo.Tests/Clio/ClioTests.cs`, and the docs.

### 13.18 Session log: OP-SYS-10 CHD-zstd verification (2026-10-31)

**Status:** complete; no code change required.

Evidence:

- CHDSharp 1.6.0 exposes `ChdCodec.Zstd` and `ChdCodec.Cdzstd`, and the package ships `VendoredZSTD.dll`.
- Opened all 589 local CHDs successfully; the codec sets are CDLZMA, CDZLIB, CDFLAC, and `None`. None use zstd.
- No native dependency or code change was added. Add a zstd CHD fixture when one is available.

**Files changed:** docs only.

### 13.19 Session log: 40-title differential sample (2026-10-31)

**Status:** complete; baseline recorded.

- Added `benchmarks/diff/sample40.json`: the first 40 titles from the full manifest, 300 frames, accuracy profile, no game fixes, unbounded CD speed, no images.
- Result: **18 match, 22 diverged, 0 error**. Every 5DO run completed.
- This is the first broad post-parity baseline. The full 594-title collection remains runner-bound.
- Evidence: `artifacts/diff/sample40-run/report.json` (local, untracked).

**Files changed:** `benchmarks/diff/sample40.json`, `docs/TestCorpus.md`, and this log.

### 13.20 Session log: UX-01 rewind and UX-02 fast-forward (2026-11-01)

**Status:** complete; full suite and formatting green.

Implemented:

- **UX-01 rewind.** New `FiveDo.Host/Rewind/RewindOptions` and `RewindBuffer`. The buffer keeps compressed keyframes (default every 120 frames) and the exact per-frame PBUS packet, loads the nearest earlier keyframe, replays the recorded input to any target frame, and restores the original audio/frame/input sinks. It clears on reset/state-load/disc timeline jumps, enforces a 256 MB default keyframe cap while keeping at least one keyframe, and exposes the oldest/newest frame and window. `CoreRunner.Rewind` presents the restored frame, updates the runner counter, and returns whether history was available. Settings > Emulation adds the enable flag and 30-300 s history length; `R` rewinds one second and `Shift+F9` rewinds one frame.
- **UX-02 fast-forward.** `CoreRunner.SpeedMultiplier` accepts 1-10 and runs that many emulated frames per presentation tick. Intermediate frames skip rendering, the final frame presents normally, and audio is detached, reset, and reattached while the multiplier is above one. Input and emulated state are unchanged. The shell holds `Tab`, stops on release/pause/stop/reset, and reports the multiplier; Settings > Emulation selects 2x-10x.
- **Hotkeys.** `HotkeyAction.Rewind`/`FastForward`, `HotkeyRouter` names, and defaults `R`/`Tab`; existing profiles receive them through `AddMissingDefaultHotkeys`.

Evidence:

- `dotnet test 5DO.sln -c Release`: **1,182 passed**, 0 failed.
- Lemmings (USA) 300-frame CLI smoke still reports `0x110E7DC5`; no emulation-core behavior changed.
- `dotnet format 5DO.sln --verify-no-changes`: clean.
- New tests cover byte-identical state after rewinding between keyframes, input replay, clamping, discontinuity resets, the memory cap, disabled mode, frame-by-frame runner restore, fast-forward frame accounting, audio muting, hotkey resolution, and settings validation.
- `FrameSkipStateTests` proves that 60 frames with three of four rendered produce identical CPU/memory/MADAM/CLIO/DSP/SPORT/VDLP/XBUS/CD/Quartz/options sections; only the presentation frame section differs. Fast-forward therefore does not diverge emulated state.
- No emulation-core behavior changed; the default timing profile and golden hashes are unaffected.

**Files changed:** `src/FiveDo.Host/Rewind/RewindOptions.cs`, `src/FiveDo.Host/Rewind/RewindBuffer.cs`, `src/FiveDo.Host/CoreRunner.cs`, `src/FiveDo.Host/Models/AppSettings.cs`, `src/FiveDo.Input/Models/HotkeyAction.cs`, `src/FiveDo.Input/Models/InputProfile.cs`, `src/FiveDo.Input/HotkeyRouter.cs`, `src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml.cs`, `src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml`, `src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml.cs`, the corresponding tests, and the docs.

**Next UX candidates:** UX-11 disc browser, UX-12 BIOS/NVRAM manager UI, UX-19 OSD/quick menu, and UX-06 TAS movie format on top of the new rewind buffer.

### 13.21 Session log: UX-04 cheat engine (2026-11-01)

**Status:** complete; full suite and formatting green.

Implemented:

- **New `FiveDo.Cheats` project.** A deterministic parser/engine for raw byte writes
  (`0xADDR = 12 34`), typed writes (`write8`/`write16`/`write32`), memory conditions
  (`if [byte|word|dword] ADDR OP VALUE then ...`), player-1 button conditions, and
  `and`/`or`/`not`/parentheses. Numbers are hexadecimal by default (`0x`/`$` explicit,
  `0d` decimal). Comments use `#` or `//`.
- **Per-game JSON documents.** `CheatStore` resolves `<gameId>.json` (or `global.json`),
  saves atomically, rejects unsupported versions, and preserves invalid individual
  codes with an error instead of discarding them.
- **Per-frame application.** `FiveDoMachine.BeforeFrame` is invoked once per frame, so
  normal play, fast-forward, and rewind replay apply the same cheats. The hook remains
  attached across save-state loads.
- **Desktop shell.** Emulation > Cheats... lists entries with enable checkboxes and
  supports add/update/delete with immediate saves. Settings > Paths adds a cheat
  directory. The running game reloads the set without a restart; changing the set clears
  rewind history.
- **CLI.** `run` adds `--cheat`, `--cheat-code`, `--cheat-auto`, and `--cheats-dir`;
  `run --json` reports `cheatCount`, `cheatWrites`, and `cheatPath`. The new
  `cheat list|add|enable|disable|remove|import|export` command manages files by game ID.
- **Docs and legal scope.** `docs/Cheats.md` documents the syntax, CLI, desktop manager,
  and the fact that 3DO has no standard RAM cheat format (Game Guru is a save editor).
  No cheat databases or copyrighted code lists are shipped.

Evidence:

- `dotnet test 5DO.sln -c Release`: **1,199 passed**, 0 failed.
- `dotnet format 5DO.sln --verify-no-changes`: clean.
- New tests cover raw and typed writes, guest word order, memory/button conditions,
  logical operators, malformed code, disabled entries, duplicate names, state-load
  reapplication, store roundtrip/invalid-code preservation, and CLI add/list/enable/
  disable/remove.
- `run --cheat-code "write8 0x2000 = 0x64" --frames 0 --dump-ram ...` reports
  `cheatCount: 1`, `cheatWrites: 1`, and the dumped byte is `0x64`.

**Files changed in this batch:** `src/FiveDo.Cheats/**`, `src/FiveDo.Machine/FiveDoMachine.cs`,
`src/FiveDo.Host/FiveDo.Host.csproj`, `src/FiveDo.Host/Models/AppSettings.cs`,
`src/FiveDo.Cli/Program.cs`, `src/FiveDo.Cli/CliJsonContext.cs`, `src/FiveDo.Cli/Models/**`,
`src/FiveDo.Frontend.Avalonia/Views/CheatsWindow.*`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.*`,
`src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.*`, `tests/FiveDo.Tests/Cheats/**`,
`tests/FiveDo.Tests/Cli/**`, `docs/Cheats.md`, and the docs index/status/CLI/user guide.

### 13.22 Session log: UX-06 TAS movies (2026-11-01)

**Status:** partial; movie container, record/replay/verify, branching, CLI, and shell controls complete.

Implemented:

- **New `FiveDo.Tas` project.** `MovieFormat`, `MovieHeader`, `Movie`, `MovieFile`,
  `MovieRecorder`, `MovieRecordingPbusSource`, `MoviePbusSource`, `MovieEditor`,
  `MovieVerifier`, and `MovieHash`.
- **Versioned container.** `.5domovie` stores a JSON header, one raw PBUS packet per
  frame, and an optional starting save state. The file header, header JSON, compressed
  input payload, and state are each protected by CRC-32; input is Brotli-compressed.
  Unsupported versions, malformed lengths, corrupt CRCs, frame-count mismatches, and
  trailing data are rejected.
- **Deterministic evidence.** Headers record the final frame hash, final ARM PC, final
  state hash, game/disc/BIOS identity, frame range, and the timing/fix options active
  during recording. `movie verify` replays every frame and compares the evidence.
- **Record and replay.** `--movie-record` wraps the live/scripted/autopilot PBUS
  source and embeds the starting state by default; `--movie-play` restores the
  embedded state, plays packets in order, and supports a prefix. `--movie-no-state`
  omits the state.
- **Branching and inspection.** `movie branch` keeps a prefix and clears completion
  evidence so a new branch can be recorded; `movie info` reads the header without
  starting the emulator.
- **CLI.** `movie info|record|play|verify|branch` plus `run --movie-record`,
  `--movie-play`, `--movie-verify`, and `--movie-no-state`; `run --json` adds
  `moviePath`, `movieFrames`, `movieVerification`, and `movieFailures`. Verification
  failure returns exit code 2.
- **Desktop shell.** **Emulation > Record Movie...**, **Play Movie...**, and
  **Stop Movie** manage movies; the status bar shows the current frame and player-1
  buttons while playing; playback pauses at the end; stopping emulation or closing the
  window finalizes an active recording.
- **Input display.** New `FiveDo.Input.PbusDecoder` decodes the standard 16-byte PBUS
  packet into six controller states for the status bar and tools.
- **Docs.** New `docs/Tas.md`; status, index, CLI reference, user guide, changelog,
  feature spec, and this roadmap updated.

Evidence:

- `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Debug`: **1,239 passed**, 0 failed.
- New tests cover container roundtrip/empty/corrupt/version/trailing-data cases, recorder
  limits, recording-source wrapping, playback order/seek/step-back/finish, PBUS
  decoding and formatting, branch/trim/append, option and evidence verification,
  machine-level record/replay state equality, tampered-frame failure, and CLI
  info/record/verify/branch/mismatch paths.
- Manual CLI smoke from a synthetic 3-frame state recorded 5 frames, verified with
  `movieVerification: pass`, and replayed through both `movie play` and `movie verify`.
- No emulation-core behavior changed; default timing, golden hashes, and existing CLI
  contracts are unchanged. The new `CliRunResult` fields are optional and appended at
  the end of the record.

Remaining:

- Lua scripting API, frame-by-frame input editing, an input timeline, and multiplayer
  input display.
- A separate lag counter is not applicable because 3DO PBUS input is sampled every
  frame; the status display reports the current packet instead.
- Cross-machine movie verification still depends on the same BIOS, disc, 5DO version,
  and timing/fix options; option differences are warnings, while frame/PC/state
  evidence is authoritative.

**Files changed in this batch:** `src/FiveDo.Tas/**`, `src/FiveDo.Input/PbusDecoder.cs`,
`src/FiveDo.Cli/Program.cs`, `src/FiveDo.Cli/CliJsonContext.cs`,
`src/FiveDo.Cli/Models/CliRunResult.cs`, `src/FiveDo.Cli/Models/CliMovieResults.cs`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml.cs`, project files,
`tests/FiveDo.Tests/Tas/**`, `tests/FiveDo.Tests/Input/PbusDecoderTests.cs`,
`tests/FiveDo.Tests/Cli/CliProcessTests.cs`, `docs/Tas.md`, and the docs.

### 13.23 Session log: UX-11 disc browser and disc info (2026-11-01)

**Status:** complete; full suite, formatting, and frontend smoke green.

Implemented:

- **`FiveDo.Host.DiscBrowser`.** Mounts `OperaFileSystem` directly from any
  `IDiscSource` (ISO, BIN, CUE, or CHD) without starting the machine, builds a
  sorted directories-first tree, and flattens it for the desktop list. `Open`
  owns the disc source; `Create` leaves ownership with the caller for tests and
  embedding.
- **Extraction.** A selected file, directory, or the whole disc is written below
  a chosen folder. Entry names are sanitized with a fixed cross-platform invalid
  set, trailing dots/spaces, traversal segments, and reserved Windows device
  names; every target is checked to remain under the destination root. The
  result reports file/directory/byte counts.
- **Disc info.** The model exposes the Opera label/comment, full two-sector MD5,
  sector count, file/directory totals, and the 4DO database title, region,
  publisher, release year, and automatic fix mask.
- **Desktop shell.** File > Disc Browser... opens a list with size/LBA/child
  details, Extract Selected..., Extract All..., and Open Folder. File > Disc
  Info... shows the volume and game-database metadata. Both windows are
  constructed by the trimmed frontend smoke test.
- **Tests.** Seven unit tests cover tree order, depth, metadata totals, path
  lookup, file/directory/all extraction, name sanitization, a real ISO file
  opened through `DiscBrowser.Open`, and a missing-image error.

Evidence:

- `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Debug`: **1,246 passed**, 0 failed.
- `dotnet format 5DO.sln --verify-no-changes`: clean.
- `dotnet run --project src/FiveDo.Frontend.Avalonia -c Debug -- --smoke-test`:
  **FRONTEND SMOKE OK 1.0.0-rc.1**.
- No emulation-core behavior, timing options, golden hashes, or save-state layout changed.

**Files changed in this batch:** `src/FiveDo.Host/DiscBrowser.cs`,
`src/FiveDo.Frontend.Avalonia/Views/DiscBrowserWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/DiscBrowserWindow.axaml.cs`,
`src/FiveDo.Frontend.Avalonia/Views/DiscInfoWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/DiscInfoWindow.axaml.cs`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml.cs`,
`tests/FiveDo.Tests/Host/DiscBrowserTests.cs`, and the docs.

**Next UX candidates:** UX-12 BIOS/NVRAM manager UI, UX-19 OSD/quick menu, and
the remaining TAS input editor/Lua work.

### 13.24 Session log: SYS-15 input devices (2026-11-01)

**Status:** partial; device models, exact PBUS records, CLI/settings, pointer input, and crosshair complete.

Implemented:

- **Device model.** `InputDeviceType` covers joypad, flightstick, mouse, lightgun,
  arcade lightgun, Orbatak trackball, and none. `InputDeviceSet` owns six ports and
  independent state for every device; `FlightstickState`, `MouseState`, `LightgunState`,
  `ArcadeLightgunState`, `OrbatakTrackballState`, and `OrbatakButtonsState` clamp
  relative motion to the hardware ranges.
- **Exact PBUS records.** `PbusChainEncoder` writes Opera's joypad (2 bytes),
  flightstick (9 bytes, `01 7B 08` signature), mouse (4 bytes, `0x49`), lightgun
  (5 bytes, `0x4D`, 20-bit scan counter plus `0x1F` hit count), arcade lightgun
  (5 bytes, coin/start/service/holster), Orbatak trackball (4 bytes), and Orbatak
  buttons (3 bytes, `0xC0`) records. The chain is padded with `0xFF` to four bytes.
  The legacy 4DO packet remains the default while every port is a joypad; any
  peripheral switches to the chain automatically.
- **Input plumbing.** `PbusSource` and `InputManager` expose the device set;
  `PbusDecoder` detects the chain and decodes joypad records for TAS input display.
  `InputManager` maps keyboard/gamepad D-pad and buttons to a flightstick when that
  device is selected, and clears one-frame mouse/trackball deltas after each sample.
- **CLI.** `--input-device 1=joypad,2=mouse,...` selects devices on `run` and `trace`;
  `--pbus-format legacy|opera` forces the packet layout; `run --json` reports
  `inputDevices`. Invalid device/port/format values return exit code 1.
- **Desktop shell.** Settings > Emulation > Input devices selects the device for all
  six ports. The game view forwards pointer motion and left/right/middle buttons to
  the first mouse, lightgun, arcade lightgun, or Orbatak trackball port, and draws a
  crosshair for lightguns.
- **Tests.** 29 encoder tests, a chain-decoding test, six settings tests, and three CLI
  process tests cover exact records, padding, port order, none ports, device selection,
  settings roundtrip, and JSON/error contracts.

Evidence:

- `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Debug`: **1,294 passed**, 0 failed.
- Lemmings (USA), 300 frames, `panafz1.bin`: legacy and `--pbus-format opera` both
  report `0x110E7DC5` (286,162,373); the chain packet is `8000FFFFFFFF...`.
- `--input-device 1=mouse` emits `49000000FFFFFFFF...` and still reports
  `0x110E7DC5`.
- Frontend `--smoke-test`: exit 0.

Remaining:

- Scripted gameplay/calibration for Orbatak and the arcade lightgun titles (Crime
  Patrol, Mad Dog II, Shootout at Old Tucson). Their 3,000-frame boots now detect the
  selected device and render non-black frames; see `docs/TestCorpus.md` 1.1h.
- Add lightgun calibration, off-screen reload settings, and multiplayer pointer
  routing; map gamepad analog axes to flightstick/trackball rather than the D-pad.
- Per-device input scripts and TAS input display for non-joypad devices.

**Files changed in this batch:** `src/FiveDo.Input/PbusChainEncoder.cs`,
`src/FiveDo.Input/PbusDevices.cs`, `src/FiveDo.Input/PbusEncoder.cs`,
`src/FiveDo.Input/PbusDecoder.cs`, `src/FiveDo.Input/InputManager.cs`,
`src/FiveDo.Input/InputScript.cs`, `src/FiveDo.Host/Models/AppSettings.cs`,
`src/FiveDo.Cli/Program.cs`, `src/FiveDo.Cli/Models/CliRunResult.cs`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml.cs`,
`src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml.cs`,
`tests/FiveDo.Tests/Input/PbusChainEncoderTests.cs`,
`tests/FiveDo.Tests/Input/PbusDecoderTests.cs`,
`tests/FiveDo.Tests/Host/InputDeviceSettingsTests.cs`,
`tests/FiveDo.Tests/Cli/CliProcessTests.cs`, and the docs.

### 13.25 Session log: UX-12 BIOS/NVRAM manager (2026-11-01)

**Status:** complete; full suite, formatting, and frontend smoke green.

Implemented:

- **`FiveDo.Host.BiosInventory`.** Scans through `BiosLibrary`, imports a validated
  `.bin`/`.rom` into a folder, reuses an image whose MD5 is already present, avoids
  overwriting a different BIOS, normalizes non-library extensions to `.bin`, and
  sanitizes names (including Windows reserved device names). `SuggestFileName`
  returns the name the importer would use without writing.
- **`FiveDo.Host.NvramInventory`.** Enumerates the shared and per-game layout with
  validity and game-database metadata, backs up one image or the whole layout while
  preserving relative paths, imports a validated 32 KB image, resets an image to a
  fresh volume, deletes an image, and suggests safe timestamped backup names. Writes
  use a temporary file plus replace so a failure cannot leave a half-written image.
- **Desktop manager.** New **File > BIOS / NVRAM Manager...** window with BIOS and
  NVRAM tabs. The BIOS tab lists kind/region/MD5/size/flags, imports, sets BIOS1 or
  BIOS2, assigns the selected MD5 to the current game, and clears the assignment.
  The NVRAM tab lists mode/version/game/size/modified/validity, backs up one image or
  all images, imports over the selected image, resets it, and deletes it. Reset,
  delete, and import-overwrite require a second click on the same button.
- **Settings integration.** The window edits `AppSettings` in place and returns
  `true` when settings or NVRAM files changed. `MainWindow` saves the settings and
  recreates the machine (or replaces the NVRAM store when idle) when the BIOS, NVRAM
  layout, or an NVRAM image changed. The trimmed frontend smoke test constructs the
  new window.
- **Tests.** Fifteen unit tests cover BIOS import/copy/reuse/name collisions/invalid
  sizes, sanitization, NVRAM validation, layout enumeration, backup, layout-wide
  backup, import, reset, delete, and backup naming.

Evidence:

- `dotnet test 5DO.sln -c Debug`: **1,309 passed**, 0 failed.
- `dotnet format 5DO.sln --verify-no-changes --no-restore`: clean.
- `dotnet run --project src/FiveDo.Frontend.Avalonia -c Debug --no-build -- --smoke-test`:
  exit 0 with the new window constructed.
- No emulation-core behavior, timing options, golden hashes, or save-state layout
  changed.

Remaining UX-12 polish:

- Per-game NVRAM assignment from the list (the current actions operate on the
  configured layout).
- Drag-and-drop import and a richer backup manifest.
- A dedicated NVRAM "backup all" restore action (backup is implemented; restore of a
  whole layout is intentionally left to the per-image import).

**Files changed in this batch:** `src/FiveDo.Host/BiosInventory.cs`,
`src/FiveDo.Host/NvramInventory.cs`,
`src/FiveDo.Frontend.Avalonia/Views/BiosNvramManagerWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/BiosNvramManagerWindow.axaml.cs`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml.cs`,
`tests/FiveDo.Tests/Host/BiosNvramInventoryTests.cs`, and the docs.

### 13.26 Session log: UX-19 OSD notifications and quick menu (2026-11-01)

**Status:** complete; full suite, formatting, and frontend smoke green.

Implemented:

- **Host-side OSD model.** `FiveDo.Host.Osd` adds `OsdPosition`, `OsdOptions`, `OsdSeverity`, `OsdMessage`, and `OsdNotifications`. The notification list is bounded (default three), time-expiring, deterministic, and testable without Avalonia. `OsdOptions` validates position, opacity (0.1-1), duration (500-10000 ms), and the message limit.
- **Persistent settings.** `AppSettings` adds `OsdEnabled`, `OsdPosition`, `OsdOpacity`, `OsdDurationMilliseconds`, and `QuickMenuEnabled`; `ToOsdOptions` validates the persisted values.
- **Desktop OSD.** The game view renders colour-coded notifications for save/load, slot changes, rewind, fast-forward, screenshots, reset, settings, and warnings. The position, opacity, and duration come from Settings; disabling the OSD clears the active list.
- **Quick menu.** `F4` (configurable hotkey) or the gamepad Select+Start chord opens a centred menu that pauses the game and offers resume/pause, save state, load state, rewind one second, fast-forward, screenshot, reset, cheats, settings, stop game, and close. Up/Down/Home/End move focus; Enter/Space activate; F4/Escape close. Closing resumes only when the game was running before the menu opened.
- **Gamepad chord.** `GamepadMenuChord` defines Select+Start; `PlatformGamepadBackend` tracks the press edge and exposes `ConsumeQuickMenuToggle`, so holding the chord cannot repeat and the individual Start/Select buttons still reach the game.
- **Settings UI.** Settings > Video adds OSD enable, position, opacity, duration, and quick-menu enable controls. Duration parsing rejects values outside 500-10000 ms with a validation message.
- **Docs.** This log, `docs/UserGuide.md`, `docs/Configuration.md`, `docs/Status.md`, `docs/ImplementationPlan.md`, and `CHANGELOG.md`.

Evidence:

- `dotnet test 5DO.sln -c Debug`: **1,336 passed**, 0 failed.
- Differential smoke (`benchmarks/diff/smoke.json`, Lemmings 300 frames): **PASS**
  (1 match, 0 allowed, 0 diverged, 0 error).
- `dotnet format 5DO.sln --verify-no-changes`: clean.
- `dotnet run --project src/FiveDo.Frontend.Avalonia -c Debug --no-build -- --smoke-test`: **FRONTEND SMOKE OK 1.0.0-rc.1**.
- No emulation-core behavior, timing options, golden hashes, or save-state layout changed.
- New tests cover queue capacity/expiry/clear/validation, settings mapping, the Select+Start chord, and the F4/QuickMenu hotkey roundtrip.

**Files changed in this batch:** `src/FiveDo.Host/Osd/**`, `src/FiveDo.Host/Models/AppSettings.cs`, `src/FiveDo.Input/Models/HotkeyAction.cs`, `src/FiveDo.Input/Models/InputProfile.cs`, `src/FiveDo.Input/HotkeyRouter.cs`, `src/FiveDo.Input/GamepadMenuChord.cs`, `src/FiveDo.Frontend.Avalonia/Input/PlatformGamepadBackend.cs`, `src/FiveDo.Frontend.Avalonia/ViewModels/OsdEntry.cs`, `src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml`, `src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml.cs`, `src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml`, `src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml.cs`, `tests/FiveDo.Tests/Host/OsdTests.cs`, `tests/FiveDo.Tests/Input/GamepadMenuChordTests.cs`, `tests/FiveDo.Tests/Input/HotkeyRouterTests.cs`, and the docs.

**Next UX candidates:** UX-13 library metadata/favorites/playtime, UX-14 auto-update, UX-07 controller profiles, and the remaining TAS input editor/Lua work.

### 13.27 Session log: UX-13 library metadata, favorites, and playtime (2026-11-01)

**Status:** partial; local library user data, offline metadata, filters, and sorts complete.

Implemented:

- **Persistent user state.** New `LibraryState`/`LibraryStateStore` keep a versioned
  `library-state.json` keyed by 4DO game ID (falling back to the full disc path for
  unknown images). Favorites, launch counts, last-played timestamps, and accumulated
  play seconds survive restarts. A path-keyed record migrates to the game-ID key when
  a disc becomes known.
- **Playtime tracking.** `LibraryPlaytimeTracker` measures only the time emulation is
  running, stops on pause/stop/close, and flushes every 60 seconds so a crash loses at
  most one minute. `MainWindow` records launches when a disc opens and refreshes the
  library decorations after play sessions.
- **Offline metadata overlay.** `LibraryMetadata`/`LibraryMetadataStore` read an
  optional `library-metadata.json` keyed by game ID or path. Records add a display
  title, developer, publisher, release year, genre, region, player count, and
  description without a network provider. Settings > Paths can choose the file.
- **Rich metadata from the 4DO database.** Library entries now carry release year,
  publisher, and region codes in addition to the existing title and game ID.
- **Filters and sorts.** `LibraryQuery` implements All/Favorites/Played/Unplayed
  filters and Title/Recently played/Most played/Release year/Publisher sorts; search
  covers titles, paths, IDs, formats, and the new metadata fields.
- **Desktop shell.** The game list adds star toggles on grid cards and list rows,
  metadata/play-time text, filter and sort selectors, and favorite/play-time
  tooltips. The standalone library window shows the same metadata and star toggle.
  Settings > Paths adds the library metadata file picker.

Evidence:

- `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Debug`: **1,350 passed**,
  0 failed.
- `dotnet format 5DO.sln --verify-no-changes --no-restore`: clean.
- Frontend `--smoke-test`: exit 0.
- No emulation-core behavior, timing options, golden hashes, save-state layout, or CLI
  contracts changed.
- New tests cover key resolution, path-to-ID migration, favorites, launch counts,
  play-time accumulation and rounding, state application, pruning, store
  roundtrip/corruption, tracker timing and disc switching, metadata-by-ID and
  metadata-by-path, unsupported metadata versions, filters, search, and every sort
  order.

Remaining:

- External Redump/No-Intro/IGDB/LaunchBox providers and automatic metadata import.
- Screenshots, manuals, videos, and a metadata editor in the shell.
- The >=90% rich-metadata target still depends on a populated offline catalog or an
  external provider; the 4DO database currently supplies title/year/publisher/region
  for its 518 recognized records.

**Files changed in this batch:** `src/FiveDo.Host/LibraryState.cs`,
`src/FiveDo.Host/LibraryMetadata.cs`, `src/FiveDo.Host/LibraryPlaytimeTracker.cs`,
`src/FiveDo.Host/LibraryQuery.cs`, `src/FiveDo.Host/GameLibraryScanner.cs`,
`src/FiveDo.Host/Models/GameLibraryEntry.cs`, `src/FiveDo.Host/Models/AppSettings.cs`,
`src/FiveDo.Host/HostJsonContext.cs`,
`src/FiveDo.Frontend.Avalonia/ViewModels/LibraryItem.cs`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/MainWindow.axaml.cs`,
`src/FiveDo.Frontend.Avalonia/Views/GameLibraryWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/GameLibraryWindow.axaml.cs`,
`src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml`,
`src/FiveDo.Frontend.Avalonia/Views/SettingsWindow.axaml.cs`,
`src/FiveDo.Frontend.Avalonia/Styles/Controls.axaml`,
`tests/FiveDo.Tests/Host/LibraryStateTests.cs`, and the docs.

**Next UX candidates:** UX-14 auto-update, UX-07 controller profiles, and the
remaining TAS input editor/Lua work.

---

<a id="part-iv"></a>
# Part IV — Session Handoff, Evidence, and Historical Fixes

> Per-pass evidence, useful commands, acceptance criteria, open questions, and "do not redo" notes.

## 0. Current handoff (2026-10-30)

**2026-11-01 update:** UX-13 library metadata, favorites, and playtime are implemented
as a partial increment. `LibraryState`/`LibraryStateStore` persist favorites, launch
counts, last-played timestamps, and play time in `library-state.json`; a path-keyed
record migrates to the game ID when a disc becomes known. `LibraryPlaytimeTracker`
measures only running emulation and flushes every 60 seconds. `LibraryMetadata`/
`LibraryMetadataStore` read an optional offline `library-metadata.json` overlay
(title/developer/publisher/year/genre/region/players/description) keyed by game ID or
path; the 4DO database now supplies year/publisher/region directly. `LibraryQuery`
implements All/Favorites/Played/Unplayed filters and Title/Recently played/Most
played/Release year/Publisher sorts. The game list adds star toggles, metadata/play
time, filter/sort selectors, and Settings > Paths chooses the metadata file.
**Do not redo the state/store, tracker, metadata overlay, query rules, star toggles,
filter/sort UI, or settings path; remaining UX-13 work is external Redump/No-Intro/
IGDB/LaunchBox providers, screenshots/manuals/videos, a metadata editor, and the
>=90% rich-metadata target.** **1350 tests pass.**

**2026-11-01 update:** UX-19 OSD notifications and the in-game quick menu are
implemented. `FiveDo.Host.Osd` adds a bounded, time-expiring notification list and
validated position/opacity/duration options; the game view shows colour-coded toasts
for save/load, slot changes, rewind, fast-forward, screenshots, reset, and settings.
**F4** or the gamepad Select+Start chord opens a quick menu that pauses the game and
offers resume/pause, save/load, rewind, fast-forward, screenshot, reset, cheats,
settings, stop, and close; Up/Down/Home/End and Enter navigate, and closing resumes
only when the game was running before it opened. Settings > Video exposes OSD enable,
position, opacity, duration, and quick-menu enable controls. **Do not redo the
notification model, quick-menu controls, gamepad chord, or settings surface; remaining
UX-19 polish is full gamepad navigation of the menu (UX-08) and richer OSD types such
as achievement/recording badges.** The Lemmings 300-frame differential smoke passes
(1 match, 0 diverged). **1350 tests pass.**

**2026-11-01 update:** UX-12 BIOS/NVRAM manager is implemented. **File > BIOS / NVRAM
Manager...** opens a two-tab window. The BIOS tab scans and classifies images, imports
validated `.bin`/`.rom` files without overwriting a different image, sets BIOS1/BIOS2,
and pins or clears the current game's BIOS MD5. The NVRAM tab lists shared and per-game
images with validation, game metadata, version, size, and modification time; it backs
up one image or the whole layout, imports a validated 32 KB image, resets an image to a
fresh volume, and deletes an image. Reset, delete, and import-overwrite require a
second click on the same button. `BiosInventory` and `NvramInventory` isolate the file
operations and use atomic NVRAM writes. **Do not redo the inventory helpers, the
manager window, the menu wiring, or the backup/import/reset/delete semantics; remaining
UX-12 polish is per-game NVRAM assignment from the list and drag-and-drop import.**
**1309 tests pass.**

**2026-11-01 update:** SYS-15 input devices are implemented. `InputDeviceSet` selects
a joypad, flightstick, mouse, lightgun, arcade lightgun, Orbatak trackball, or no
device on each of six PBUS ports; `PbusChainEncoder` emits Opera's exact daisy-chain
records (2/9/4/5/5/4/3 bytes plus `0xFF` padding) and the legacy 4DO packet is kept
while every port is a joypad. Settings > Emulation > Input devices and the CLI
`--input-device 1=mouse,3=lightgun` select devices; `--pbus-format opera` forces the
chain for differential tests; `run --json` reports `inputDevices`. The desktop pointer
drives the first mouse/lightgun/trackball port, with a crosshair overlay for lightguns.
**Do not redo the device models, encoder, CLI/settings surface, pointer plumbing, or
`PbusDecoder` chain support; remaining SYS-15 work is per-device calibration,
multiplayer pointer routing, and scripted gameplay validation. Crime Patrol, Mad Dog
II, Orbatak, and Shootout at Old Tucson boot and detect the selected arcade device.**
**1294 tests pass.**

**2026-11-01 update:** UX-11 disc browser and disc info are implemented.
`FiveDo.Host.DiscBrowser` mounts `OperaFileSystem` from an ISO/BIN/CUE/CHD without
starting the machine, builds a sorted tree with sizes/LBAs, extracts a selected
file, directory, or the whole disc with cross-platform name sanitization and
destination-root containment, and exposes volume/game-database/fix metadata.
File > Disc Browser... and File > Disc Info... use the new windows; both are
constructed by the trimmed frontend smoke test. **Do not redo the browser model,
extraction, or the two windows; remaining UX work was UX-19 OSD/quick menu (now
implemented) and the TAS input editor/Lua work.** **1246 tests pass.**

**2026-11-01 update:** UX-06 TAS movies are implemented as a partial first increment.
`FiveDo.Tas` owns the versioned `.5domovie` container (per-frame PBUS packets, optional
starting state, CRC-32/Brotli), deterministic record/replay, branch/trim/append, and
frame/PC/state verification. The CLI adds `movie info|record|play|verify|branch` and
`run --movie-record/--movie-play/--movie-verify/--movie-no-state`; verification failure
returns exit 2. The shell adds Emulation > Record Movie.../Play Movie.../Stop Movie and a
player-1 input display, and playback pauses at the end. `PbusDecoder` decodes standard
PBUS packets. **Do not redo the movie container, recorder/playback sources, verifier,
editor, CLI commands, or shell controls; remaining TAS work is Lua, a frame-by-frame
input editor, an input timeline, and multiplayer input display. A separate lag counter is
not applicable because 3DO PBUS input is sampled every frame.** **1239 tests pass.**

**2026-11-01 update:** UX-04 per-game cheats are implemented. `FiveDo.Cheats` parses
raw/typed writes, memory/button conditions, and `and`/`or`/`not` combinations;
`CheatStore` reads/writes atomic `<gameId>.json` documents; `FiveDoMachine.BeforeFrame`
applies enabled entries every frame (normal play, fast-forward, and rewind replay);
the shell adds Emulation > Cheats... and Settings > Paths > Cheats; the CLI adds
`--cheat`, `--cheat-code`, `--cheat-auto`, `--cheats-dir`, and
`cheat list|add|enable|disable|remove|import|export`. 3DO has no standard RAM cheat
format (Game Guru is a save editor), so the documented 5DO syntax is used and no code
databases are shipped. **Do not redo the cheat parser, store, per-frame hook, manager,
CLI commands, or docs; remaining cheat work is optional code-type extensions and a
community code format once one is documented.** **1199 tests pass.**

**2026-10-31 update:** the differential harness (FND-05/CORE-11/CORE-16) is
implemented. `tools/FiveDo.DiffHarness` stages the bundled OperaCore and BIOS,
extracts/caches a data ISO from the disc, runs 5DO and OperaCore with the same
options, compares checkpoint frame hashes, applies manifest allowances, and writes
JSON/Markdown/HTML reports. `run` adds `--checkpoints`, `--audio-hash`, and
`--state-hash`; `FiveDo.DiffHarness baseline` gates report changes. The Lemmings
(USA) 300-frame smoke matches at `0x110E7DC5`; the early boot frames diverge and
are recorded as a scheduling finding. Manifests are
`benchmarks/diff/smoke.json` and `benchmarks/diff/showcase.json`. **Do not redo the
checkpoint contract, hash algorithms, staging, comparer, reports, or baseline
command; remaining work is the full-collection manifest, reference-side PC/state/
CD-transcript exports, visual artifacts, and the nightly baseline.** See
[`DifferentialHarness.md`](DifferentialHarness.md).

**2026-10-31 update:** SYS-14 per-game NVRAM and versions are implemented.
`NvramStorageMode` (`Global`, `PerGame`), `NvramVersion` (0-9), and `NvramDirectory`
select shared `nvram.ram`, versioned `nvram.<n>.ram`, or
`nvram/per-game/<gameId>.<n>.ram` images. `NvramLayout` owns path resolution and
migration fallbacks; `NvramStore` reads the fallbacks without copying, writes atomically,
and backs up a corrupt primary. Settings > Emulation exposes the mode and version; the
CLI adds `--nvram-mode`, `--nvram-version`, `--nvram-dir`, `run --json`
`nvramPath`/`nvramMode`/`nvramVersion`, and `nvram path|list|export|import|reset`.
Unit, store, settings, JSON-contract, and CLI tests pass. **Do not redo the NVRAM layout,
store, settings controls, or CLI commands; remaining work is the UX-12 manager UI
(library listing, per-game assignment, and backup/restore dialogs) and portable-mode
path selection.**
**1125 tests pass.**

**2026-10-31 update:** SYS-04 BIOS manager and auto-selection are implemented.
`BiosCatalog`/`BiosLibrary` scan `.bin`/`.rom` files, compute MD5, validate sizes, and
classify primary/secondary, region, video standard, Kanji, Anvil, and patch state.
`--bios2` and `--bios-auto` work on `run`/`trace`; `bios list|scan|select` expose the
catalog and selection; `run --json` reports `biosPath`, `biosMd5`, `biosName`,
`biosRegion`, `biosSelection`, and `biosSource`. Settings > Paths adds the BIOS folder,
primary/secondary paths, auto-select, and scan; Settings > Emulation adds a per-game
BIOS override; File adds assign/clear actions. Auto-selection uses game-database
regions, known requirements (Slayer Europe/USA, Powers Kingdom Europe, Arcade CD-ROM),
and per-game MD5 pins; the configured BIOS remains the fallback when no folder is set.
The five-title 3,000-frame and four known-requirement 12,000-frame smoke runs are in
[`TestCorpus.md`](TestCorpus.md) 1.1f. **Do not redo the BIOS catalog, scanner,
selector, CLI commands, or settings UI; remaining work is the full-collection
BIOS-aware sweep and per-game auto profiles (SYS-18).** **1125 tests pass.**

**2026-10-31 update:** SYS-09 CD speed control and CORE-08 timing profiles are
implemented. `CdSpeed` (`Unbounded`, `X1`-`X32`, default `X2`) changes the effective
speed of the cycle-based CD model with Opera's per-speed overhead table;
`--cd-speed`/Settings expose it, `run --json` reports `cdSpeed`, and Options section v5
records it with a mismatch warning. `EmulationProfile` (`Compatible`, `Accuracy`,
`Performance`, `Custom`) bundles the timing options through `--profile` and Settings >
Emulation; the accuracy profile enables ARM60, Opera timers, CEL busy timing, and cycle
CD reads. The default FreeDO path is unchanged, and the three-title smoke differential is
in [`TestCorpus.md`](TestCorpus.md) 1.1e. Do not redo the CD speed selector or the profile
mapping; remaining work is per-profile benchmark deltas, a full-collection accuracy run,
and CD-DA speed behavior with SYS-01. **1039 tests pass.**

**2026-10-31 update:** the CORE-04 CD cycle-timing option is implemented as the first
increment of the CD port. The default `--cd-timing freedo` path is unchanged;
`--cd-timing opera` schedules each 2048-byte data sector at the stock 2x drive speed,
clears the data/status poll bits while busy, publishes the read-data status after the
transfer drains, and raises the XBUS CD FIQ when ready. Settings > Emulation exposes
the choice, `run --json` reports `cdromTiming`, the CD-ROM section is version 2, and the
Options section is version 4. The three-title 12,000/24,000-frame smoke differential is
in [`TestCorpus.md`](TestCorpus.md) 1.1d. Do not redo this option; the remaining CD work
is command latency, disc-derived TOC, read-ahead, subcode, DIPIR tagging, CD-DA, and ODE;
SYS-09 later added CD speed control. **1014 tests passed at that point.**

**2026-10-31 update:** the CORE-03 MADAM CEL busy option is implemented. The default
`--cel-timing freedo` path is unchanged; `--cel-timing opera` defers the CEL draw to the
next CPU tick and holds the status register busy for the estimated pixel/texel work.
Settings > Emulation exposes the same choice, `run --json` reports `celTiming` and
`celBusyCycles`, and save states record the mode in the Options section (v3) plus the
pending/busy fields in the Madam section (v2). The 3,000-frame smoke differential is in
[`TestCorpus.md`](TestCorpus.md) 1.1c. Do not redo this option; the remaining work is the
full-collection differential, a CEL-status polling title, and a frame-time check before
considering it the default. **1003 tests pass.**

**Goal (2026-10-30; superseded 2026-11-02):** finish the BIOS-aware compatibility
triage, fill the 5DO statuses, and then start high-resolution CEL research. The
2026-11-02 release decision supersedes the "do not cut a release yet" instruction:
Linux x64 full suite passed; v1.0.0 is published on GitHub with the artifacts attached.
M13.5/M13.6 and M14 remain post-1.0.

**Read first:**

- [`GameCompatibilityList.md`](GameCompatibilityList.md) — 241 4DO reference titles,
  local matches, empty 5DO status columns.
- [`CompatibilitySweep.md`](CompatibilitySweep.md) — sweep procedure, status
  definitions, triage, and Linux VM validation.
- [`Compatibility.md`](Compatibility.md) §13 — sweep and 2026-10-30 triage results.
- [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md) — current
  `--high-res` limitations and the M14 research options.
- [`Status.md`](Status.md) — current position and the v1.0 release path.

**Inputs:** `J:\Panasonic 3DO` (589 CHDs), `G:\MAME\MAME Software List CHDs\3do`
(5 CHDs), `bios/panafz1.bin`, `bios/panafz10.bin`, `bios/panafz10-patched.bin`,
`bios/panafz10e-anvil.bin`, `bios/panafz10ja-anvil-kanji.bin`, and the saved 4DO wiki
MHTML in `References/4DO-master/`.

**What changed on 2026-10-30:**

- The 594-disc baseline sweep is complete: 576 `Loadable/Rendered`,
  13 `Loadable/No video`, 5 `Not Loadable`.
- `Mirai Shounen Conan` was a transient CHD read error; it now reaches its menu with
  hash `0xBE2A023B`. The four Arcade CD-ROM rows need `panafz10-patched.bin` and the
  default headless pad; unpatched BIOSes reach only a starfield or the 3DO
  storage-manager shell.
- The 12,000-frame rerun shows Ishida, both Konpeki no Kantai discs, and all three Myst
  discs rendering. Powers Kingdom (Europe) reaches a live-action intro with Anvil;
  Slayer Europe renders credits with `panafz1`, while Slayer USA needs `panafz10` for
  its title menu at 60,000 frames. Crash 'n Burn (Japan) and WorldCup Super Stadium
  show the same Anvil boot screen and are still stuck at 60,000 frames.
- Headless `run`/`trace` now attach a default player-1 pad. Olympic Summer Games
  proceeds past its controller check. The 3,000-frame corpus keys were re-baselined in
  [`TestCorpus.md`](TestCorpus.md) 1.1. **954 tests pass.** (Superseded by the
  2026-11-01 extended-boot retry: WorldCup Super Stadium renders with the FZ-10J BIOS
  plus its Kanji font ROM; only Crash 'n Burn Japan remains.)

**Ordered tasks (historical 2026-10-30 list; for the current release steps see
[`Status.md`](Status.md) §6):**

1. Build the Release CLI:
   `dotnet build src/FiveDo.Cli/FiveDo.Cli.csproj -c Release`.
2. Rerun the `Not Loadable` and `Loadable/No video` subsets with
   `tools/bios-probe.ps1` or `--bios-auto bios`, recording which BIOS each disc needs.
   `bios select <disc> --folder bios` prints the chosen image and reason. Use a separate
   output directory; do not overwrite the `panafz1` baseline.
3. Investigate Crash 'n Burn (Japan), which still stops on the Anvil boot screen at
   65,000 frames. Keep the smallest repro and a trace; compare the Japan revision with
   the working Crash USA/Korea revisions. WorldCup Super Stadium is resolved with the
   FZ-10J BIOS plus its Kanji font ROM.
4. Inspect the rendered 12,000-frame captures and fill the 5DO status/notes in
   [`GameCompatibilityList.md`](GameCompatibilityList.md), including the BIOS used.
5. Run the high-resolution subset with `-HighRes -OutDir artifacts/compat-highres`.
6. Validate the CLI in the `LinuxMint` Hyper-V VM
   (`D:\Hyper-V\VMs\LinuxMint`); see [`CompatibilitySweep.md`](CompatibilitySweep.md) §7.
7. Start M14 measurements from [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

**Status scale:** No data / Not Loadable / Unplayable / Playable / Good, matching the
4DO wiki. `Loadable/Rendered` and `Loadable/No video` are intermediate automated labels,
not final statuses.

**Do not redo:** the 4DO MHTML parser, the sweep harness, the OpenAL/Serilog/trimming
work, the renderer/filter work, the ARM/CLIO/CEL/CD timing options, the CD speed selector and
timing-profile mapping, the BIOS catalog/scanner/selector or its CLI/settings surface,
the NVRAM layout/store/settings controls or the `nvram` CLI commands, the library
state/store/playtime tracker, the offline metadata overlay, the library query rules,
the star/filter/sort UI, the TAS movie container/recorder/verifier/editor or its
CLI/shell surface, the OSD notification model or the quick menu/chord/settings
surface, or
the 2026-10-31 performance pass (CEL pipeline caching, Debug core optimization,
render-request coalescing, Auto frame skip). Do not treat full-race completion, inventory
scripting, arm64 validation, or installers as current gates.

**Project position:** donationware, non-commercial, source-available. Legal/attribution
sign-off and CI reliability are recorded; Linux x64 full suite passed. v1.0.0 is
published on GitHub with the artifacts attached.

---

**Date:** 2026-10-29 (twenty-fifth pass: F6/F7 slot selection, F9 single-frame
advance, and desktop input recording to a replayable `--input-script` file; the
twenty-fourth pass added first-party Opera file-system parsing for
ISO/BIN/CUE/CHD in `fs list`/`fs extract`, multi-block directory chaining, and
`burst`/`gap` file reads; the twenty-third pass added native Win32 Vulkan swapchain and
`--vk-window-probe`, `PreferOpenGl` enforcement, custom aspect editing, frontend AOT
audio validation, and bug-report hygiene; the twenty-second pass added save-state
format version 2 with per-section Brotli compression; the twenty-first pass added
Vulkan xBR/FSR/HQx filters and the GPU-palette xBR path in the OpenGL presenter; the
twentieth pass added the optional offscreen Vulkan backend and `--vk-probe`; the
nineteenth pass added save-state disc warnings, console hotkeys, ISO/BIN `disc tracks`,
and OpenAL Soft LGPL packaging; the eighteenth pass added Serilog logging and bug
reporting; the seventeenth pass added the OpenAL crackle fix, PCM tap, and race profile)

**Vulkan performance follow-up (2026-10-30, twenty-sixth pass):** the offscreen Vulkan
readback is now on demand. Probes and screenshots use a one-shot copy, and the GPU
timestamp covers the render passes only; 4K sharp-bilinear GPU time is 0.075 ms and
present 0.24 ms. The desktop fallback writes the captured frame directly into the
locked `WriteableBitmap`, and the swapchain blit uses a fence instead of
`vkQueueWaitIdle`. The desktop path is still offscreen plus per-frame readback, so
OpenGL remains the full-performance presenter. **924 tests pass.**

**Vulkan embedded surface and core-runner fix (2026-10-30, twenty-seventh pass):**
`VulkanVideoSurface` hosts a click-through Win32 child HWND inside the Avalonia window
and presents through the FIFO swapchain, so `--gl-backend vulkan` no longer reads frames
back to the CPU or uploads a `WriteableBitmap`. `CoreRunner` now uses a monitor
handshake with an explicit paused state, fixing rapid stop/start cycles. **925 tests
pass.**

**Goal:** make the audio path correct and testable. The sixteenth pass replaced the
FreeDO 8.24 DSP sample accumulator with an exact 44,100-samples-per-ARM-second clock
(600 frames now produce 441,015-441,020 samples / 10.000 s instead of 444,097 /
10.070 s), preserved VDL/timer reference timing, bumped the Quartz state section to
version 2 with v1 conversion, and re-baselined the 3,000-frame boot keys. It also
fixed live OpenAL output: the runtime is bundled (OpenAL Soft 1.23.1), the context
is made current on the audio thread, underruns are padded/counted, and the CLI has
`--audio openal`, `--volume`, `--realtime`, and `audio analyze <wav> --json`. Core
frames are paced by `AudioFramePacer` (audio clock with a 60 Hz fallback), WAV writes
are buffered with streaming peak/RMS/clipping statistics, and the NativeAOT CLI now
opens the live backend. **906 tests pass.** The twenty-third pass added native Win32
Vulkan swapchain presentation and `--vk-window-probe` (4K FIFO: 1.34/1.43 ms GPU,
5.99/6.24 ms present including the display wait, 0 managed bytes/frame), honored
`PreferOpenGl` as an explicit CPU mode, added custom aspect editing, validated the
frontend NativeAOT OpenAL path with `ALSOFT_DRIVERS=null`, and stopped expected
environmental warnings (such as a missing audio device) from filing bug reports.
The twenty-second pass added state format
version 2: large section payloads are Brotli-compressed, the CRC stays over the
uncompressed data, version-1 states still load, and `state inspect` reports stored and
compressed sizes. A 24,902,592-byte local state becomes 647,647 bytes (300-frame Gex)
or 2,001,229 bytes (14,900-frame Gex). The twenty-first pass added the Vulkan
xBR/FSR/HQx filters and made the OpenGL xBR path sample the GPU-expanded palette
texture directly. The eighteenth pass replaced the custom
logger with Serilog and added the asynchronous bug-report sink. The seventeenth pass fixed live OpenAL
crackle by giving the fill loop real buffer ownership, added the `FIVEDO_AUDIO_TAP`
PCM/timing recorder and overlay peak meter, added the FSR 1.0 filter, and recorded
the Road Rash 3D-race profile. Do not redo the Serilog/bug-report work, the trim-safe JSON work, the
trimmed CLI/frontend publishes, the frontend smoke test, the benchmark comparison,
the portable archives, the swap-interval/GPU-probe work, the ANGLE/GLES parity work,
this audio pass, the 2026-10-29 release-hardening pass (save-state disc warnings,
console hotkeys, ISO/BIN `disc tracks`, and OpenAL Soft LGPL packaging), the
twenty-first Vulkan xBR/FSR/HQx filter pass, the twenty-second save-state
compression pass, the twenty-third Vulkan-window/release-polish pass, the
twenty-fourth Opera file-system pass, or the twenty-fifth hotkey/input-recording
pass. The remaining
**release** work is the legal/attribution review
(now including OpenAL Soft's LGPL runtime), a stability soak, release
packaging/notes, and optional Linux/macOS launch validation. GitHub Actions is
currently blocked before job start by a billing/spending-limit annotation; that is an
external account action, not a code failure. Full-race completion,
extra title scripts, GLES 3.1/DPI, Tier B timing, arm64/installers, and the
optional JIT are post-1.0. See
[`ReleaseChecklist.md`](ReleaseChecklist.md).

> **Release-scope update (2026-10-26):** v1.0 ships a documented compatibility set,
> not full compatibility. Start with [`ReleaseChecklist.md`](ReleaseChecklist.md);
> the gameplay work in sections 2-3 is post-1.0 backlog.

Earlier passes added the presentation plumbing (GL/GLES capability reporting, CPU
fallback/context-loss recovery, fullscreen, screenshots, About, Hi-Res, benchmark
gates, prefetch, and the 4K probe) and the game-specific fixes recorded below. Read
section 1.18 for the fourteenth-pass changes before starting.

Read these first:

- [`Compatibility.md`](Compatibility.md) - current title matrix, renderer fixes, and
  input evidence.
- [`Status.md`](Status.md) - current position and ordered next steps.
- [`TestCorpus.md`](TestCorpus.md) - baselines, commands, and the `--input-script`
  format.
- [`PortingNotes.md`](PortingNotes.md) - DSP/CCB/CEL invariants.
- [`ReferenceStrategy.md`](ReferenceStrategy.md) - reference hierarchy.

---

## 1. What changed on 2026-10-24 (second and third passes)

### 1.1 Arbitrary-CEL transposition (fixed)

`TexelDrawArbitrary` passed `(i, j)` to the `(x, y)` `ReadPixel`/`WritePixel`
helpers. FreeDO's `readPIX(src, i, j)` treats `i` as the row (`y`) and `j` as the
column (`x`), so every asymmetric quad was written transposed. Road Rash's race
CELs wrote texels into the top of VRAM (`0x2FC000`), which is the OS's VRAM free
list; the OS then printed `AllocMem error, bad node address` and the race appeared
frozen. Alone in the Dark drew its character horizontally. The fix passes `(j, i)`.

Evidence: the RAM-write watchpoint showed `CelRasterizer.TexelDrawArbitrary`
writing 2-byte texels at `0x2FC000`-`0x2FC03E` from frames 16729-17030. After the
fix the race renders a level road and an upright rider, the allocator error is gone,
and the run reaches 60,000 frames.

A regression test (`PackedArbitrary_UsesTheColumnAsXAndTheRowAsY`) fails when the
arguments are transposed and passes with the fix.

### 1.2 VDLP line width (fixed)

The bitmap copy path read and stored exactly 320 pixels regardless of the display
mode. It now uses the modulo parsed from the VDL control word (320/384/512/640/1024),
sets the line width accordingly, and `FiveDoMachine` derives `SourceWidth` from the
widest rendered line. 320-wide output is unchanged; the 3,000-frame corpus hashes are
identical.

### 1.3 Save-state frame pixels (fixed)

`MachineFrameSection.Load` read the line pixel buffers and discarded them. A state
load now restores them, so a zero-frame load reproduces the saved frame hash exactly.

### 1.4 New diagnostics

- `--capture-dir <path> --capture-every N`: write `fNNNNNN.png` every N frames.
- `trace --dump-ram ...`: dump RAM at a trace stop (for example, at a KPRINT or PC
  stop).
- The trace header now prints r4-r12 in addition to r0-r3.
- `--dump-ram`, `--dump-ram-offset`, `--dump-ram-length` work in `run` and `trace`.

### 1.5 High-resolution CEL/VDLP mode (new, opt-in)

`--high-res` enables `EmulatorTuning.HighResolution`. The CEL rasterizer addresses
four 1 MB VRAM planes: X selects the even/odd plane and Y remains a 240-line
coordinate. Each write is mirrored to the second interlace field (planes 2/3), and
the VDLP interleaves planes 0/1 and 2/3 into two 640-wide output lines. The frame
source becomes 640x480; `VdlFrameUploader`, the CPU assembler, screenshots, frame
hashes, and save-state frame sections all carry the larger frame. SPORT fill/copy
mirrors the source page to the extra planes.

Evidence: from a fresh Lemmings boot with the intro-skip script, the 640x480 title
shows all four buttons and the complete logo and matches the reference screenshot
(PC `0x00012B48`, hash `0xE79E18C5` at 18,100 frames). The five 3,000-frame low-res
corpus hashes are unchanged. High-res is off by default because 320x240 titles must
not be upscaled. The passcode screen is a 320-wide UI that the high-res mode scales
2x; its five visible keyboard columns are the game's own scrolling window, so that
screen does not exercise a separate 640-wide coordinate path.

### 1.6 Wolfenstein gameplay loop (new)

From `w3d-fix12k.state`, `w3d-play.txt` starts FLOOR 1-1 at 14,000 frames
(`PC 0x0007824C`, hash `0x07586A88`); `w3d-walk.txt` walks/turns/fires to 18,000
frames (`PC 0x00074A04`, hash `0xF568EBCA`), with the corridor, guard, HUD, and
pistol rendered. The 18,000-frame state roundtrips (zero-frame hash `0xF568EBCA`,
two 120-frame continuations both `PC 0x000782B8` / `0xF568EBCA`). A 4,000-frame WAV
capture is 66.86 s of 44.1 kHz stereo, peak 32767, RMS 2980, 18 clipped samples.

### 1.7 Diagnostic RAM patches and Road Rash driver (new)

`--poke ADDR=HEX` writes bytes into RAM after a state load; multiple patches are
separated by `;`. It is a diagnostic for input/state work, not a compatibility fix.
`--autopilot road` is an experimental Road Rash driver that holds B and steers
toward the gray road centre. Unit tests cover the patch parser and the steering
direction contract. The driver follows the road but does not yet detect traffic or
recover from a wreck, so it does not finish a race.

### 1.8 Road Rash driver v9 and the BUSTED discovery (2026-10-25)

The previous pass described a "terminal wall pin" around frame 34,000: speed 0,
frozen `lineX`, and all held inputs converging on one hash. That state is **the
race-over `BUSTED` screen**, not a physics wedge. From `rr-auto-v8.state`
(40,000 frames), 400 frames of no input followed by `B` produces the `BUSTED`
plainclothes-cop screen (hash `0x7D470158`); the same happens from
`rr-v10full.state` and `rr-v17-40k.state` (`0x9F44FAA2`). Once a race is lost, no
steering/throttle input resumes it, which is why every probe converged. The
driver's real failure is losing the race earlier by leaving the road, stalling,
and getting caught - not a stuck bike.

Driver v9 changes, all covered by tests (572 passing):

- **Road brightness ceiling.** `IsRoadPixel` now caps brightness at 120 (was 150).
  City sidewalks and plazas are brighter concrete and were being classified as
  asphalt; the lower band locked onto them and the bike rode the sidewalk.
- **Detector-upset hold.** A frame-to-frame `lineX` jump over 25 px means the
  detector re-locked onto a different gray surface, not road motion. The driver
  holds straight for 30 frames instead of chasing the false lock. The test is
  frame-to-frame so a frozen line cannot latch the hold (the first attempt used a
  long reference and did exactly that).
- **Closing-side steering.** If the driver is steering toward a side whose
  lookahead fraction is below 0.6 while the other side is above 0.8, it crosses
  to the open side and coasts during the crossing.
- **Sharp-bend braking.** Centre below 0.35 with one side open above 0.8 and the
  other below 0.4 brakes while steering to the open side; this replaces taking the
  0.39/0.29/0.94 bend flat out into the outside wall.
- **Urban pacing.** In built-up sections (centre visible, one edge below 0.6) the
  throttle is duty-cycled 40/60 frames instead of held flat out.
- **Coast floor.** Pacing, caution, and bend lift may never coast more than 20
  consecutive frames; the throttle is forced back on. A stopped bike is caught and
  busted, which is exactly how the pacing-only build failed.
- **Faster recovery.** Stuck recovery triggers after 150 stable off-centre frames
  (was 300), brakes for 30 frames, then re-applies throttle with the recovery
  steer.
- **Telemetry.** Progress lines add `la=<left>/<centre>/<right>` and `PACE`/`UPSET`
  markers; `run --json` adds `autopilotPacing`, `autopilotUpset`, and
  `autopilotLookahead`.

Best run: from `rr8-30k.state`, 10,000 autopilot frames end at 40,000 frames
(`PC 0x000125E4`, hash `0xE44B073D`). The bike keeps road detection to about
36,500 frames (the v8 run lost it at ~34,000), but then leaves the road and the
race is lost; the final screen is the pre-bust arrest frame. Zero-frame load
reproduces `0xE44B073D`; two 120-frame autopilot continuations both end at
`PC 0x000125E4` / `0xE44B073D`, and any input advances to `BUSTED`
(`0x9F44FAA2`). The 5.5-mile city race is still **not finished**, and the
remaining work is vision/control quality, not a pin-recovery rule.

### 1.9 Road Rash driver v10: 91,000 live frames and a new stop state (2026-10-25)

The v9 driver lost the first city race at ~34,000 frames. Dense captures at
33,800-34,000 showed the actual causes were **close traffic**, not a wall:
another racer filled the lookahead centre (0.43/0.07/0.17) while every existing
rule required a 0.6-wide escape, so the bike ran straight into it. A full brake
stopped the bike dead and got it busted; a coast/steer was the correct response.
Driver v10 adds:

- **Close-traffic escape.** Centre < 0.2, one side in [0.3, 0.6), side
  difference > 0.2: steer into the gap and lift (not brake). This runs even when
  `LineX` is NaN, because the lower-band run disappears exactly when the
  obstacle is close.
- **Urgent preemption only for close traffic.** Ordinary obstacle steering
  stays on the 16-frame decision interval. Applying every obstacle bias
  immediately oversteered into a taxi at ~32,000 in the clean 30k run.
- **Emergency-brake steer hold.** With the road fully blocked and no side target
  (all lookahead fractions < 0.35), keep the last steer and brake. The old path
  chased a `LineX` that had jumped to 240 and turned into the tree at 33,890.
- **Stopped/wreck restart.** A scene signature over the left scenery (rows
  96-156, x 20-140) plus the speed readout stays still for 40 frames -> hold B
  with straight bars for 180 frames, then retrigger if still stopped. The
  signature deliberately excludes the centre/rider: the remount animation kept
  resetting the first version. Manual probes showed B alone remounts (34,500 and
  80,000 -> 52 mph) while steering keeps the rider running in place.
- **Telemetry.** Progress adds `cov=<lower-band road fraction>` and `RESTART`;
  `run --json` adds `autopilotRoadCoverage`, `autopilotCrashRestart`, and
  `autopilotStaticFrames`.

A cyclic right/left escape search on later restart attempts was tried and
**reverted**: it drove the 83,000-frame rider run into the BUSTED screen
(`0x9F44FAA2`). The throttle-only policy is the proven one.

Best run chain (local states; not a single command):

```
rr8-30k.state (30,000)
  + 6,000 -> rr-v28-36000.state (36,000)
  + 44,000 -> rr-v34-80000.state (80,000, PC 0x00098004, hash 0xD3309E17)
  + 3,000 -> rr-v35-83000.state (83,000, PC 0x00091A70, hash 0x442D7D3D)
  + 8,000 -> rr-v36-91000.state (91,000, PC 0x00085F48, hash 0x02ABE78E)
```

The 30k->36k and 36k->80k segments used earlier v10 revisions (right-steer
crash restarts); the final throttle-only code was verified on the 80k->83k and
83k->91k segments and reproduces the 91,000 state exactly. A clean final-code run
from 30,000 reaches 42,000 (`PC 0x0001048C`, hash `0xCBDF6DDB`) and then enters
the **game-side wait** described below. The bike is on a sidewalk in the image,
but that is a symptom; input is ignored and no amount of steering/throttle frees
it. This is not a reason to lose the 91,000 result.

Roundtrip at 91,000:

- Live state: `PC 0x00085F48`, hash `0x02ABE78E`,
  `cov=0.93`, `lineX=197.9`.
- Zero-frame load: `0x02ABE78E` (pixel-identical).
- Two independent 120-frame autopilot continuations: both `PC 0x000973EC`,
  hash `0x3F27B3D6`.

A 100,000-frame continuation stays live through ~91,000 and then stops at
~92,000. The ninth-pass trace shows the frame is static, every input is ignored,
and the CD trace contains only READ_ID (`cmd 0x83`) every 60 frames after the
last read-data requests (sectors 109053-109071 and 43873-43888). `run --json`
reports `cdStatus=0xE1`, `cdPoll=0x0C`. This is the **same game-side wait** as
the 42,000-frame stop and Alone's static intro. Do not change the restart policy
for it.

A separate terminal stop was seen on one 45,500-frame path with the same
signature: reads 109053-109071, seeks to 43877-43888, then only READ_ID polls;
the frame hash stays identical and every button is ignored. **The ninth pass
fixed this class of stop** with the CD media-access pulse (section 1.12); the
old 91k/92k hashes are historical. The remaining Road Rash issue is finishing a
full race without wrecking.

### 1.10 Desktop shell and per-game fixes (2026-10-25)

The seventh pass filled the frontend and host gaps that do not depend on game
compatibility:

- **Game fixes.** `GameCompatibility` maps the 4DO IDs (`F3AF1B13`/`217344B0`,
  `DBB419FA`/`07C32F10`/`5282889F`/`7340307E`, `CB8EE795`, `1A370EBA`/`B35C911D`,
  `0511D3D2`/`9F7D72BC`/`E843C635`, `2AABA5B9`/`BF61BB32`) to the matching
  `FixMode` bits. The CLI applies them before `Reset`, reports the mask in
  `run --json`/`info`, and accepts `--fix-mode 0xMASK` or `--no-game-fixes`.
  `AppSettings` stores per-game overrides and an automatic-fixes toggle.
- **Save slots.** `SaveStateSlotManager` writes `slot-NN.tdos` plus a JSON sidecar
  (game name, ID, timestamp, frames) atomically; the shell has Save/Load and
  previous/next slot buttons and a Slots window that lists/saves/loads/deletes
  slots, and persists the current slot. `List()` tolerates empty and corrupt slots.
- **Library and settings.** The Library window scans configured folders through
  `GameLibraryScanner` and opens the selected disc. The Settings dialog edits
  filter/aspect/integer scaling/vsync, audio enable/volume, automatic fixes,
  high-res, per-game fix mask, and save/screenshot paths.
- **Input capture.** `KeyboardBindingEditor` and the Input dialog capture keys for
  all six players, remove a key from its previous button, detect conflicts, and
  reset to defaults. The optional controller preview remains.
- **Screenshots and overlay.** Screenshots are PNGs named
  `<game>-<timestamp>-f<frames>.png` from GL readback or the CPU frame; an optional
  FPS/status overlay is available in Settings.

The five 3,000-frame low-res hashes are unchanged; Alone's automatic
`FIX_BIT_TIMING_6` does not alter the boot frames. 659 tests pass.

---

### 1.11 Desktop completeness and renderer correctness (2026-10-25, eighth pass)

The eighth pass closed the remaining non-gameplay frontend gaps and fixed a
real-content renderer bug:

- **GPU palette expansion.** The shader used `pixel & 0x1f` as one CLUT index for all
  three channels. Real frames therefore appeared grayscale. The shader now fetches red,
  green, and blue independently (`pixel >> 10`, `pixel >> 5`, `pixel`) and matches
  `FrameAssembler`/`GpuPalette`. Lemmings gameplay was captured through sharp-bilinear
  and xBR with correct colors.
- **xBR.** Hyllian's xBR-lv2 edge-directed 4x pass is implemented as `VideoFilter.Xbr`,
  with a CPU-expanded native source and sharp-bilinear finishing. It is selectable in
  Settings and validated visually on Lemmings gameplay at 60 fps. HQx, CRT bloom, and
  4K timing remain optional follow-ups.
- **Gamepads.** `GamepadMapper` plus Windows XInput and Linux evdev backends; hot-plug
  rescanning, six-player mapping, left-stick D-pad fallback, trigger-to-shoulder
  fallback, and a Gamepad tab in the input editor. macOS falls back to null.
- **Logs.** In-app log viewer with level, category, search, auto-refresh, and clear;
  the shell records BIOS/disc loads, renderer fallback, save/load, screenshots, input
  changes, and emulation faults.
- **Save-state thumbnails.** Each slot writes a PNG thumbnail shown in the Slots window;
  thumbnail failures never fail the state save.
- **First run and errors.** A welcome guide explains the user-provided BIOS/disc
  requirement; BIOS, disc, and emulation failures show error dialogs in addition to the
  status line and log.
- **Docs.** Added `docs/UserGuide.md`, `docs/KnownIssues.md`, and `CHANGELOG.md`.

659 tests pass. The five 3,000-frame low-res hashes are unchanged. The next session
should return to the real-game compatibility work: Road Rash's ~92k/40k stops, Space
Ace's interactive path, and the CD-load/READ_ID freeze. Do not redo the palette, xBR,
gamepad, log, thumbnail, or first-run work.

---

### 1.12 Ninth pass: evidence, Space Ace interactive start, and the wait-state diagnosis (2026-10-25)

This pass did **not** change emulation behavior. It captured the evidence that was
missing and corrected the Road Rash diagnosis.

- **Per-title evidence.** Each live state was loaded with `--frames 0`, captured to
  PNG, then run 120 frames and captured again. The zero-frame hashes match the saved
  frames, and every 120-frame continuation is deterministic. A 600-frame WAV was
  captured from each gameplay state. The table and WAV stats are in
  [`TestCorpus.md`](TestCorpus.md) 1.5a.
- **Space Ace interactive path.** `X` at 12,000 returns to the title/menu; `A` at
  12,200 starts the interactive scene. The run reaches 15,000 frames (hash
  `0x7E780D93`), the state roundtrips, and two 120-frame continuations both end at
  `PC 0x000284EC`, hash `0x70B16422`. A 600-frame gameplay WAV is 10.04 s, peak
  4096, RMS 1832.
- **Alone menu/intro.** `Up, Left, A, Down` reaches the Begin/Resume menu; `A` starts
  the intro scene. The intro is a static, input-ignoring wait with only READ_ID
  polling. The media pulse alone does not advance it, but A every 300 frames moves
  the man-at-desk cutscene to the room/mansion/title scenes (29,200 frames,
  `PC 0x00020640`, hash `0x62304BCF`). A blind pulse is not yet a stable room.
- **Alone movement loop.** From `alone-intro-A2.state` (39,200), `Up, Left, A,
  Down, Right` moves the character around the room; at 39,800 `PC 0x0008D0EC`,
  hash `0x44C7576B`. The state roundtrips (`0x44C7576B` -> two 120-frame
  continuations at `PC 0x0008BF00`, `0xE490C8C7`). This is a scripted
  movement/save-load loop; inventory interaction remains.
- **Wolfenstein fresh-game load.** `w3d-loadlong.state` + A every 300 frames
  advances the static load through character select to FLOOR 1-1 at 64,000
  (`PC 0x00069F3C`, hash `0x2F5B9864`); no input stays at `0x07586A88`.
- **Road Rash wait state and the media-access fix.** The 42,000-frame state and the
  92,000/100,000-frame state were static, ignored every button combination, and
  showed only READ_ID polls. The CD device now sets poll bit `0x40` after two
  consecutive READ_ID commands with no intervening command and at least 50 frames
  between them. That is Portfolio's media-access event; it advances the screens to
  `BUSTED`, race select, and a new race. The five 3,000-frame hashes are unchanged.
  The old 91k hash `0x02ABE78E` was a frozen presentation; the current chain reaches
  a rider-run/BUSTED transition at `PC 0x00093AF4`, hash `0x70E1ED8E`.
- **Autopilot menu advance.** `--autopilot road` now pulses A/releases throttle
  during long-static, low-coverage (`<0.5`), CD-load, and 180-frame restart-stuck
  states, and reports `autopilotMenuAdvance` in `run --json`. It passes the
  `GAME LOADING`/`Get ready` screen and starts a new race at 80,000
  (`PC 0x00092FF8`, hash `0x1074EF3F`), then cycles race -> wreck -> menu -> new
  race to 100,000 (`PC 0x0008214C`, hash `0x0D46CF56`). The remaining problem is
  finishing a full race without wrecking.
- **Tests.** Added `ScriptedPbusSource`/`InputScript.Offset` tests, CD media-access
  tests, and an autopilot menu-advance test. 667 tests pass; format is clean.

---

### 1.13 Tenth pass: pure D-pad Lemmings passcode, CRT bloom, and HQx 2x (2026-10-26)

- **Lemmings pure D-pad passcode.** `docs/scripts/lem-fun4-dpad.txt` types
  `HNLHCIOECW` from a passcode-screen state with an empty entry and selection on
  `V` (`artifacts/lem_pass_fresh.state`, 21,700 frames). The script uses 15-frame
  direction holds and 5-frame A taps with a release frame between actions, so the
  CLI's duplicate-frame coalescing cannot swallow a release. The game shows the
  `Fun 4 / Level 1 / Save the Lemmings` briefing at 22,600 (`PC 0x00020628`, hash
  `0xC1DF57C5`) and starts gameplay at 23,000 (`PC 0x00083FD8`, hash `0xB7846C2D`).
  The zero-frame load is pixel-identical and two 120-frame continuations both end
  at `PC 0x000230A4`, hash `0xEDEB1B9D`. This closes the pure-D-pad entry item; the
  title-to-passcode transition is still the existing `Down`/`A` script.
- **CRT bloom.** `RendererSettings.Bloom` (default 0.35) and `BloomThreshold`
  (default 0.6) add a 3x3 native-resolution glow around bright pixels in the CRT
  filter. The Settings dialog exposes a CRT bloom slider. Bloom is ignored by the
  other filters and the CPU fallback. Shader-source and settings roundtrip tests
  cover the new uniforms; GPU timing is still pending.
- **HQx 2x.** `VideoFilter.Hqx` runs hq2x on the CPU-expanded BGRA frame, uploads
  the 2x result, and finishes with sharp-bilinear. A 32x24 reference image matches
  the C++ `hq2x_32_rb` hash `0xA400C421`; uniform images are unchanged. The
  reference's `RGBtoYUV` table loop skips the last entry, so pure white is excluded
  from the reference image. (hq3x/hq4x were added in the eleventh pass; see 1.14.)
- **Per-game filter overrides.** `AppSettings.FilterOverrides` stores a filter by
  game ID, `ResolveFilter`/`SetFilterOverride` handle case-insensitive lookup and
  clearing, and the Settings dialog's Emulation page exposes an Automatic/filter
  combo. The shell passes the current game ID to `ToRendererSettings`.
- **Tests.** 673 tests pass; the four HQx tests, the filter-override tests, and the
  steady-state allocation test are the only count change.

---

### 1.14 Eleventh pass: HQx 3x/4x and the benchmark budget gate (2026-10-26)

- **HQx 3x/4x.** `VideoFilter.Hqx3x` and `VideoFilter.Hqx4x` run generated
  hq3x/hq4x switches on the CPU-expanded BGRA frame. The same 32x24 reference image
  matches the C++ `hq3x_32_rb`/`hq4x_32_rb` hashes (`0x0C406F97`/`0xBB17969C`);
  uniform images are unchanged and destination-size validation accepts only 2x-4x.
  The renderer sizes its intermediate target from `HqxFilter.ScaleFor`, and the
  Settings dialog lists all three HQx modes. Measured 320x240 cost is
  ~2.3/4.2/7.5 ms/frame on checkerboard content and ~12.1/13.4/14.8 ms on random
  noise for 2x/3x/4x; 3x/4x stay opt-in.
- **Benchmark budget gate.** `BenchmarkGate.Evaluate` enforces 0 managed
  bytes/frame, core p99 <= 8 ms, total mean <= 8 ms, and total p99 <= 16.667 ms.
  The benchmark exposes `--gate` (exit code 3 on failure), and CI runs it for the
  synthetic smoke before uploading the JSON baseline. The gate is budget-based;
  the same-runner relative (>10%) comparison was added in the twelfth pass (see 1.15).
- **Desktop integration.** The frontend accepts a positional disc path or
  `--disc <path>` for file associations, ships `packaging/5do.desktop`, can
  write a native CPU-frame screenshot next to a filtered GL capture, and previews
  sibling cover art in the Library window.
- **Tests.** 691 tests pass; the new tests cover the 3x/4x reference hashes,
  uniform-image preservation, unsupported scales, `ScaleFor`, cover-art discovery,
  and every gate failure mode.

---

### 1.15 Twelfth pass: trim-safe JSON and trimmed CLI publishes (2026-10-26)

- **Source-generated JSON.** `AppSettings`, `InputProfile`, save-slot metadata, and
  every CLI `--json` response now use `JsonSerializerContext` metadata instead of
  reflection. The contexts are `AppSettingsJsonContext`/`SaveStateSlotJsonContext`
  (`FiveDo.Host`), `InputProfileJsonContext` (`FiveDo.Input`), and `CliJsonContext`
  (`FiveDo.Cli`). The CLI's anonymous response objects were replaced with named
  records in `src/FiveDo.Cli/CliJsonModels.cs`; the JSON field names are unchanged.
- **Reflection removal.** `WriteRunResult` and `selftest` read typed properties
  instead of `Type.GetProperty`; the RAM-write watchpoint formatter uses
  `DiagnosticMethodInfo.Create` instead of `StackFrame.GetMethod`.
- **Trimmed CLI.** `FiveDo.Cli` sets `PublishTrimmed=true` and `TrimMode=full`.
  CI publishes all six RIDs trimmed and adds a Windows smoke test (`--version`,
  `selftest --json`, `bench --frames 2 --json`). The win-x64 audit also covered
  `info`, `state inspect`, `state slots`, `fs list` on a generated CHD, a CHD boot,
  and a save-state roundtrip. The only remaining trim warning is CHDSharp's vendored
  FLAC settings helper (`TypeDescriptor`); it is outside the 3DO data-track path and
  is demoted to a visible warning. See [`Trimming.md`](Trimming.md).
- **Frontend.** Trimming was still disabled in this pass; it was completed in the
  thirteenth pass (section 1.17), which removed `ViewLocator` and added the trimmed
  startup smoke test.
- **Benchmark baseline comparison.** `BenchmarkComparison` and the benchmark
  `--baseline <json>`/`--baseline-threshold <fraction>` options compare core p99,
  total mean, and total p99 with a same-machine baseline; a >10% regression exits
  with code 4 and improvements pass. CI still uses the absolute `--gate` budgets;
  the relative check is intended for stable/self-hosted runs. See
  [`Performance.md`](Performance.md) §8.
- **Tests.** 704 tests pass; the new tests cover the camel-case CLI JSON contract,
  source-generated self-test/host-record roundtrips, and every benchmark-comparison
  failure mode.

---

### 1.16 Twelfth pass: portable release archives (2026-10-26)

- **Packaging script.** `packaging/package.ps1` takes a component (`cli`/`frontend`),
  RID, publish directory, and output directory. It stages the publish output plus
  `README.md`, `THIRD-PARTY-NOTICES.md`, `docs/Licensing.md`, and a generated
  `PACKAGE.txt` (component, RID, version, commit, UTC build time), and adds
  `5do.desktop` for Linux. Windows archives are zip; Linux/macOS archives
  are tar.gz. PDBs are excluded from release archives.
- **CI.** Both six-RID publish jobs now create and upload a portable package in
  addition to the raw publish folder.
- **Local validation.** The win-x64 zip and linux-x64 tar.gz were built and
  inspected; both contain the executable, notices, licensing, and manifest. The
  win-x64 archive starts and passes `selftest`/`bench`.
- **Remaining.** MSIX/dmg/AppImage installers, signing/notarization, and
  Windows/macOS file associations (M10.7).

---

### 1.17 Thirteenth pass: trimmed frontend publishes and startup smoke test (2026-10-26)

- **ViewLocator removed.** The template `ViewLocator` (`Type.GetType` +
  `Activator.CreateInstance`) and its `Application.DataTemplates` registration were
  deleted. Every window is constructed explicitly, and all XAML bindings are compiled
  (`AvaloniaUseCompiledBindingsByDefault=true`; each window/`DataTemplate` declares
  `x:DataType`). This removed the only first-party frontend trim warning.
- **Trimmed frontend.** `FiveDo.Frontend.Avalonia` sets `PublishTrimmed=true` and
  `TrimMode=full`. The six-RID CI publish job now uses `PublishTrimmed=true` instead
  of `false`. CHDSharp's vendored-FLAC `TypeDescriptor` warning is demoted to a
  visible warning with the same policy as the CLI; the data-track path does not decode
  CD-DA/FLAC.
- **Frontend smoke test.** `--smoke-test --smoke-output <path>` opens the real
  `MainWindow`, runs one UI pass, writes `FRONTEND SMOKE OK <version>`, and exits
  with code 0. The GL surface is hidden during the smoke so headless CI does not
  need a desktop OpenGL context. It skips the welcome dialog and configured
  BIOS/disc flow so CI needs no BIOS, disc, or user profile. `--version` exits
  before Avalonia initializes. The startup parser (`--disc`, positional disc,
  `--smoke-test`, `--smoke-output`, `--version`) has unit tests in
  `tests/FiveDo.Tests/Frontend/FrontendStartupTests.cs`.
- **CI.** The Windows `build-test` job publishes the trimmed win-x64 frontend and runs
  the smoke test with a 60-second timeout, checking the exit code and marker file.
- **Size.** On win-x64 the single-file executable is 26.3 MB trimmed vs 91.3 MB
  untrimmed; the complete publish folder is 150.9 MB vs 215.9 MB because native
  Skia/HarfBuzz/ANGLE assets are unchanged. All six trimmed frontend RIDs were
  published and inspected locally.
- **Tests.** 709 tests pass; the five new tests cover flag/option parsing and disc-path
  detection (including ignoring the smoke-output value).
- **Remaining.** NativeAOT is still not attempted and is documented as post-v1 work;
  installers/signing/notarization remain M10.7. See [`Trimming.md`](Trimming.md).

---

### 1.18 Fourteenth pass: swap interval and true-4K GPU probe (2026-10-26)

- **Swap interval.** `FiveDo.Video.OpenGL.SwapInterval.TryCreate` resolves the
  platform extension in priority order: `WGL_EXT_swap_control`,
  `GLX_EXT_swap_control`, `GLX_MESA_swap_control`, `GLX_SGI_swap_control`, then
  `EGL_KHR_swap_interval`. `OpenGlRenderer` applies `RendererSettings.Vsync`
  (`1`/`0`) during `Initialize` and when the setting changes, and reports
  `VsyncActive`/`VsyncBackend`. `RendererCapabilities.SupportsVsync` is now honest
  instead of always false. Unit tests cover selection priority, missing extensions,
  and the capability overload.
- **GPU probe.** `--gl-probe <path>` opens a code-only `GlProbeWindow`, presents
  synthetic 320x240 VDL frames through the real Avalonia `VideoSurface`, and writes
  per-frame JSON. Options: `--gl-probe-size WxH`, `--gl-probe-frames N`,
  `--gl-probe-warmup N`, `--gl-probe-filter`, `--gl-probe-vsync`,
  `--gl-probe-offscreen`, `--gl-probe-timeout`, `--gl-probe-capture`, and
  `--gl-backend`/`--gl-probe-backend`. The offscreen path renders into
  an RGBA8 FBO (`OffscreenRenderTarget`), so a true 4K pass works on a smaller
  desktop. The probe reports vendor/renderer/version, output size, filter, present
  and GPU p50/p95/p99/max, total and per-stage allocations, and vsync state.
- **Measurements.** Windows 11, Ryzen 9 7900, RTX 4070, OpenGL 4.0, 120 measured
  frames after 30 warm-up frames. 4K sharp-bilinear: GPU mean 0.086 ms / p99
  0.089 ms; present mean 0.080 ms / p99 0.208 ms; 0 renderer/mailbox allocations
  per frame. 4K accurate: GPU p99 0.287 ms, present p99 0.178 ms. 4K CRT: GPU p99
  0.193 ms, present p99 1.157 ms. 4K xBR: GPU p99 0.205 ms, present p99 1.788 ms.
  1080p sharp: GPU p99 0.146 ms, present p99 0.335 ms. Full table in
  [`Performance.md`](Performance.md).
- **Vsync note.** The WGL extension is found and accepted (`vsyncActive: true`),
  but the Avalonia compositor paces the window independently; the probe reports
  ~64 fps throughput even with vsync requested. The renderer cost remains far
  below the 16.67 ms budget.
- **Allocation note.** After warm-up, `OpenGlRenderer.Present` and the mailbox
  report **0 managed bytes/frame**. The probe's residual ~2.3 KB/frame is outside
  the renderer and comes from the Avalonia UI/compositor loop; it is a host-side
  follow-up.
- **Tests.** 741 tests pass. New tests cover probe option parsing/filter aliases,
  the JSON contract, swap-interval selection, vsync capability reporting, and
  offscreen-target validation.

---

### 1.19 Fifteenth pass: ANGLE/GLES validation and CPU/GPU parity (2026-10-26)

- **Backend selection.** The frontend accepts `--gl-backend auto|wgl|angle|software`
  (alias `--gl-probe-backend`) and maps it to `Win32PlatformOptions.RenderingMode`.
  `auto` preserves the previous WGL -> ANGLE -> software order. The probe report
  records `requestedBackend`.
- **Probe capture/parity.** `--gl-probe-capture <path>` writes the presented frame as
  a PNG. For a 320x240 Accurate capture, the probe compares the GPU pixels with
  `FrameAssembler` and records `parity` (`matches`, `differentPixels`, `cpuHash`,
  `gpuHash`, `verticalFlip`). Measurement allocations are snapshotted before the
  capture so the readback buffer does not distort the steady-state allocation result.
- **ANGLE results.** On Windows 11 / Ryzen 9 7900 / RTX 4070, ANGLE reported
  OpenGL ES 3.0 via Direct3D 11, integer textures, and `EGL_KHR_swap_interval`.
  4K sharp-bilinear: present mean 0.14 ms, p99 0.47 ms; accurate 0.15/0.48; CRT
  0.14/0.43; xBR 0.37/0.83; 1080p sharp 0.13/0.40; windowed 720p sharp 0.13/0.49;
  all with 0 renderer/mailbox bytes per frame. HQx 4x is CPU-bound at 10.44/14.22 ms.
  ANGLE has no `GL_TIME_ELAPSED`, so GPU timing is `null`; present timing is valid.
- **Parity.** WGL (OpenGL 4.0/GLSL 330) and ANGLE (OpenGL ES 3.0/GLSL ES 300) both
  matched the CPU oracle exactly: 0 different pixels, FNV-1a hash `0xD6BD89C5`
  (`3602745797`), no vertical flip.
- **Additional corpus.** Seven more discs booted, rendered, and save-state-roundtripped
  at 12,000 frames: Gex, Need for Speed, Super Street Fighter II Turbo, Total Eclipse,
  Killing Time, Theme Park, and Return Fire. Gex, Total Eclipse, and Super Street
  Fighter II Turbo show gameplay/attract scenes; six produce non-silent 44.1 kHz
  stereo audio (Theme Park's menu is silent). The hashes, WAV stats, and commands are
  in [`TestCorpus.md`](TestCorpus.md) 1.7 and the matrix is in
  [`Compatibility.md`](Compatibility.md) §10.
- **Capture reentrancy fix.** The opt-in GPU integration test
  (`FIVEDO_GPU_TESTS=1`) runs the built frontend probe for WGL and ANGLE, checks
  Accurate parity and a 4K pass, and found a real crash: the capture consumer can
  finish the probe and dispose the surface synchronously while `OnOpenGlRender` is
  still using the adapter. The render path now returns after the capture callback
  when the surface has been disposed.
- **NativeAOT CLI/frontend audit.** `FiveDo.Cli` publishes with NativeAOT via the
  opt-in `-p:FiveDoNativeAot=true` property. The win-x64 single file is 9.97 MB
  (trimmed IL is 26.3 MB) and passes `--version`, `selftest`, `bench`, `info`,
  `fs list` on Lemmings (482 files), and a 3,000-frame boot matching the golden
  `PC 0x0012B3C8` / hash `0x932C7722`. The frontend publishes as a 28.8 MB
  single-file native executable plus native Skia/HarfBuzz/ANGLE assets and passes
  `--smoke-test` plus the WGL and ANGLE 320x240 parity probes. Silk.NET.Core loader
  warnings remain and AOT audio is not yet validated. CI publishes and smoke-tests
  both AOT builds on Windows. The default six-RID matrix remains trimmed IL.
- **Tests.** 775 tests pass. New tests cover backend aliases/mode ordering, probe
  capture-path parsing, the opt-in GPU integration path, and the physical-pixel
  sizing math (100/125/150/200%, fractional bounds, invalid inputs). The existing
  JSON contract test now includes the requested backend and parity fields.

---

### 1.20 Sixteenth pass: exact DSP clock, live OpenAL, and audio analysis (2026-10-27)

- **Exact DSP sample clock.** `Quartz` now accumulates actual ARM cycles toward the
  next 44.1 kHz sample (`_dspCycleRemainder += cycles * 44100`, threshold
  `ArmClock`) instead of FreeDO's `arm * SND_CLOCK` 8.24 rounding. The ARM/VDL/timer
  accumulators are unchanged. One ARM second is exactly 44,100 samples; a 600-frame
  Lemmings boot is 441,015-441,020 samples (10.000 s) instead of 444,097 (10.070 s).
  Opera's 600-frame capture is 441,441 samples / 10.010 s at its 59.94 Hz field
  rate. The old/new 6,000-frame Road Rash WAVs have the same peak (29,951), RMS
  within 0.2%, and the same spectral peaks (442.1 Hz); only the timebase changed.
- **Quartz state v2.** The exact cycle remainder is serialized; version-1 sections
  are accepted and convert the legacy 8.24 fraction. Existing archived states still
  load (`lem_pass_fresh.state`, `w3d_gameplay.state`, Road Rash states).
- **3,000-frame re-baseline.** Alone `0x4EC2E1A7`, Lemmings `0x6CCA83F5`, Road Rash
  `0xCE90FA5A`, Space Ace `0xF349263B`, Wolfenstein `0x9A3B5131`. Alone/Lemmings
  screenshots are pixel-identical before/after; Road Rash/Space Ace are adjacent FMV
  frames; Wolfenstein differs only in the star field. The Lemmings Fun 4 D-pad script
  still reaches gameplay at 23,000 frames.
- **Live OpenAL fixed.** `OpenAlAudioOutput` makes the context current on the fill
  thread (OpenAL context state is thread-local), bundles OpenAL Soft 1.23.1 via
  `Silk.NET.OpenAL.Soft.Native`, copies `soft_oal.dll` as `OpenAL32.dll` on Windows,
  pre-fills two buffers, pads/counts underruns, and retries a stopped source. The
  trimmed single-file CLI and the opt-in NativeAOT CLI both open the backend; the
  opt-in `FIVEDO_AUDIO_TESTS=1` test plays a real 440 Hz tone.
- **Audio-driven pacing.** `FiveDo.Host.AudioFramePacer` averages 30 frames of
  `FiveDoMachine.AudioSamplesProduced`, sets the period to `samples / 44100`, clamps
  to 50-70 fps, and falls back to 60 Hz. `CoreRunner` uses it for the core thread and
  reports the target rate in `CoreStatus`; the overlay shows measured/target fps.
  `run --realtime` applies the same idea in the CLI (300 frames = 5.000 s with the
  OpenAL null backend; 220,510 samples, 529 dropped, 14 padded buffers, ~89 ms
  output latency).
- **WAV and diagnostics.** `WavAudioSink` buffers PCM writes and tracks frames, peak,
  RMS, clipping, silence, and DC offset. `AudioAnalyzer` validates/analyzes PCM WAV
  files; `audio analyze <wav> --json` prints the report. `run --json` includes the
  stats and OpenAL queue counters; `--elapsedSeconds` was added for pacing checks.
- **Tests.** 798 pass. New coverage: exact 44,100-sample count across batch patterns,
  Quartz v1 conversion/v2 validation, audio statistics and WAV analyzer roundtrips
  (including mono/odd chunk/truncation), buffered sink flushing, frame-pacer behavior,
  CoreRunner target-rate reporting, reset/state-load audio flushing, CLI audio
  JSON, and the opt-in OpenAL tone.

Do not revert the exact clock to the FreeDO 8.24 DSP accumulator. It is the
difference between 44.1 kHz and ~44.4 kHz audio. Do not remove the bundled OpenAL
runtime or move the context-current call back to the UI thread.

### 1.21 Seventeenth pass: OpenAL crackle root cause, PCM tap, and race profile (2026-10-28)

- **Crackle root cause.** The OpenAL fill loop selected the next buffer with
  `_buffers[queued]`. OpenAL unqueues the oldest buffers first, so that index
  named buffers still attached to the playing source; `BufferData` and
  `SourceQueueBuffers` then rewrote audio that was about to play, producing
  constant clicks while the underrun and dropped counters stayed at zero.
  Buffers now come from an `AudioBufferPool` free list, variable-size blocks are
  queued, queued frames are tracked exactly, and `alGetError` is checked after
  every queue.
- **Threading.** The fill thread is registered with
  `AvSetMmThreadCharacteristics("Pro Audio")` and runs at above-normal priority.
- **Diagnostics.** `FIVEDO_AUDIO_TAP=<prefix>` records the exact queued PCM
  (`<prefix>.wav`) and a per-buffer CSV (elapsed time, frames, ring/queue depth,
  underrun, peak, OpenAL error). The status overlay shows a live peak meter.
  Tap validation: 0 OpenAL errors, 0 underruns, and 0 discontinuities across
  2,345 buffer boundaries; the tap crack metric is 4.5/s versus the DSP baseline
  15.6/s and the pre-fix frontend 320.7/s.
- **Video.** Restored the `(float)` casts on `glUniform2/4` so Silk.NET selects
  the float overloads (the integer overloads left the presenter uniforms at zero
  and collapsed the canvas to black). Added the opt-in AMD FSR 1.0 EASU/RCAS
  filter with a sharpness setting and MIT attribution.
- **Benchmark.** Added `--warmup` and `--state-in`; the Road Rash race profile is
  recorded in [`Performance.md`](Performance.md).
- **Tests.** 818 pass (was 798). New coverage: buffer-pool ownership/rotation,
  PCM tap WAV/CSV, and the soft peak limiter.

### 1.22 Eighteenth pass: Serilog logging and bug reporting (2026-10-28)

- **Serilog pipeline.** `FiveDo.Diagnostics.Logging.LoggingSetup` configures the
  rolling daily file, stderr console (desktop), in-memory viewer, and bug-report
  sinks. `AppLogging` owns the process-wide logger and flushes it on exit. The
  custom `Log`, `ILogSink`, `ConsoleLogSink`, `InMemoryLogSink`, `NullLogSink`,
  `LogLevel`, and interpolated-string handler were removed; `LogCategory` remains
  as the structured `Category` property.
- **Log viewer.** `InMemorySerilogSink` (capacity 4000) feeds the desktop **Logs**
  dock and **Log Viewer**; levels use Serilog's `LogEventLevel`.
- **Bug reports.** `BugReportSink` forwards `Warning`-or-higher events
  asynchronously to the configured endpoint with the required environment, error,
  and exception blocks. Submissions are deduplicated for five minutes, capped at
  ten per minute, and disabled when `FIVEDO_BUG_REPORTS=0` or in CI/test runs.
  `FIVEDO_LOG_DIR` redirects files and `FIVEDO_BUGREPORT_API_KEY` overrides the
  key. The CLI keeps JSON on stdout and concise errors on stderr; diagnostics go
  to the file/API.
- **Catch coverage.** Host entry points (`Program.Main`, Avalonia startup,
  `MainWindow` shutdown/dispose), the core thread, the OpenAL fill loop, video
  surface, GPU probe, settings/NVRAM/save-slot/library scanners, and prefetch/gamepad
  boundaries now log through Serilog. Expected CLI user errors are logged at
  `Warning`; unexpected exceptions are logged at `Fatal` before exit.
- **Tests.** 818 pass. Coverage includes the in-memory sink, category/level/search
  filter, the required bug-report blocks, and an HTTP handler test proving the
  warning payload reaches the configured endpoint with the `X-API-KEY` header.

### 1.23 Nineteenth pass: release hardening (2026-10-29)

- **Save-state disc identity.** `FiveDoMachine.LoadState`/`LoadStateFromFile` accept
  the current game ID and add "State disc hash differs from the loaded disc." to
  `StateWarnings` when both hashes are present and differ. The state still loads.
  The CLI `run`/`trace` commands, `SaveStateSlotManager.Load`, and the desktop
  Load State/Slots actions pass the current game ID; the desktop shows the warning
  in the status line and log. Tests cover mismatch, match, and empty-hash cases.
- **Console hotkeys.** `HotkeyRouter` resolves the persisted `InputProfile.Hotkeys`
  action names (SaveState, LoadState, Screenshot, Fullscreen, Pause, Reset) against
  a key name, case-insensitively, and ignores unknown action names. The shell
  dispatches the resolved action before forwarding the key to the emulated pad:
  F5 save, F8 load, F10 reset, F11 fullscreen, F12 screenshot, Escape pause. F11
  works from the library; Escape leaves fullscreen first.
- **`disc tracks` for ISO/BIN/CUE.** The command still prints the CHD track table;
  other containers now print one 2048-byte user-data track with
  `format=<iso|bin|cue>`, `dataSize`, `frames`, and `start=0`. In-process CLI tests
  cover ISO and raw BIN images.
- **OpenAL Soft LGPL packaging.** `docs/OpenALSoft.md` records the LGPL notice,
  copyright, exact upstream 1.23.1 source, a three-year written source offer, and
  relinking/replacement instructions. `docs/OpenAL-Soft-COPYING.txt`
  contains the complete LGPL-2.0 text. `packaging/package.ps1` copies both into
  every portable archive and names them in `PACKAGE.txt`; the win-x64 archive was
  inspected and the extracted CLI passed `--version` and `selftest --json`.
- **Tests.** 845 pass (was 821). New coverage: three disc-hash warning cases, one
  slot-manager warning case, eighteen hotkey-router cases, and two `disc tracks`
  CLI cases.

### 1.24 Twentieth pass: optional Vulkan backend and probe (2026-10-29)

- **Offscreen Vulkan renderer.** `FiveDo.Video.Vulkan` implements the two-stage
  presenter with Vulkan 1.0: R16_UINT indexed pixels, R8G8B8A8 CLUT, R32G32_UINT
  line parameters, a native-resolution palette pass, an output-resolution
  scale/filter pass, GPU timestamp queries, and a host readback buffer. GLSL is
  compiled to SPIR-V at build time by `XenoAtom.ShaderCompiler.Build`; no Vulkan SDK
  or shader compiler is required at runtime. `Silk.NET.Vulkan` provides the bindings.
- **Filters.** Accurate/nearest, sharp-bilinear, and CRT are implemented. xBR, HQx,
  and FSR are not in the Vulkan shader; they fall back to sharp-bilinear and set
  `filterFallback` in the probe report.
- **Probe.** `--vk-probe <path>` runs the renderer headlessly (no Avalonia window) and
  writes the same GPU probe JSON as `--gl-probe`, including `present`/`gpu` timing,
  allocations, capabilities, optional PNG capture, and CPU/GPU parity. Options:
  `--vk-probe-size`, `--vk-probe-frames`, `--vk-probe-warmup`, `--vk-probe-filter`,
  `--vk-probe-capture`, `--vk-probe-timeout`.
- **Desktop path.** `--gl-backend vulkan` hides the OpenGL surface and presents the
  Vulkan output through the existing `WriteableBitmap` path (`CaptureToFrame`).
  OpenGL remains the required full-performance presenter; the Vulkan path is optional
  and offscreen/readback-based.
- **Results.** 320x240 Accurate parity: 0 different pixels, FNV-1a `0xD6BD89C5`
  (`3602745797`), no vertical flip. RTX 4070 / Vulkan 1.4, 4K sharp-bilinear:
  present 1.51 ms mean / 1.80 ms p99, GPU 1.34 ms mean / 1.62 ms p99, 0 managed
  bytes/frame; 1080p sharp-bilinear present 0.49 ms mean / 0.50 ms p99. A 640x480
  source/output case and a 320x240 -> 640x480 source-size change also match the CPU
  oracle.
- **Tests.** `VulkanShaderTests`, `VulkanRendererTests` (opt-in
  `FIVEDO_VULKAN_TESTS=1`), `VulkanProbeIntegrationTests` (same opt-in), and
  `VkProbeOptionsTests`. The opt-in tests are skipped without a Vulkan device.

Do not replace the OpenGL presenter with the offscreen Vulkan path, and do not treat
the Vulkan probe as a 4K window-system benchmark. The backend is a validated optional
path; a native surface/swapchain and non-Windows device validation are future work.

### 1.25 Twenty-first pass: Vulkan xBR/FSR filters and GPU-palette xBR (2026-10-29)

- **Vulkan xBR-lv2.** `Xbr.frag` runs Hyllian's xBR-lv2 4x expansion over the
  GPU-expanded native frame, then the existing scale pass finishes with
  sharp-bilinear. The offscreen backend no longer reports `filterFallback` for xBR.
- **Vulkan FSR 1.0.** `FsrEasu.frag` upscales the native frame into a viewport-sized
  target, and `FsrRcas.frag` sharpens it into the output. Both are Vulkan 450 GPU
  passes with the shared push-constant block and one-image descriptor layout.
- **Vulkan HQx.** The CPU `HqxFilter` moved from `FiveDo.Video.OpenGL` to
  `FiveDo.Video` so both presenters share it. The Vulkan backend assembles the frame
  with `FrameAssembler`, expands it with `HqxFilter`, uploads the result through a
  reusable staging buffer, and finishes with sharp-bilinear. HQx 2x/3x/4x are
  CPU-bound and opt-in; 4K HQx 2x present measures 7.66 ms mean / 10.36 ms p99.
- **GPU-palette xBR in OpenGL.** The required OpenGL xBR path now samples
  `_paletteTexture` directly instead of running `FrameAssembler` and uploading a CPU
  RGBA copy. The 320x240 WGL xBR capture is within one mean channel level of the
  accurate frame, and the flipped comparison is far worse, confirming top-down
  orientation. The CPU `_xbrSourceTexture`/`_xbrCpuFrame` path was removed.
- **Measurements.** RTX 4070 / Vulkan 1.4, 60 frames after 15 warm-up: 4K xBR present
  1.55 ms mean / 1.92 ms p99, GPU 1.39/1.73 ms; 4K FSR present 1.87/2.15 ms, GPU
  1.69/1.97 ms; both 0 managed bytes/frame. 1080p xBR 0.61/0.85 ms and 1080p FSR
  0.56/0.60 ms present. OpenGL WGL 4K xBR: present 0.28/1.47 ms, GPU 0.19/0.32 ms;
  ANGLE 4K xBR: present 0.21/0.59 ms with no GPU timer.
- **Tests.** 861 pass. `VulkanRendererTests` adds a top-red/bottom-blue split-frame
  orientation check for xBR, FSR, and HQx at 320x240 and 640x480; `VulkanShaderTests`
  checks all six SPIR-V modules; the probe integration test runs xBR, FSR, and HQx at
  640x480 with `filterFallback: false`.

Do not redo the Vulkan xBR/FSR/HQx filter work or reintroduce the CPU xBR upload. The
remaining Vulkan work is a native surface/swapchain, vsync, and non-Windows device
validation.

### 1.26 Twenty-second pass: save-state version 2 and Brotli compression (2026-10-29)

- **Format version 2.** `StateFormat.Version` is now 2; the reader accepts versions
  1 and 2. The outer header layout is unchanged.
- **Per-section compression.** `StateWriter.WriteSection` compresses payloads of at
  least 4 KiB with Brotli when the result is smaller and sets the section `flags`
  field to 1. The stored payload is `[uncompressedLength i32][Brotli bytes]`; the
  section `length` counts both. The section CRC remains over the uncompressed
  payload. `StateReader` validates the flags, decompresses, and verifies the CRC
  before exposing the section.
- **Backward compatibility.** Version-1 states (raw sections) load unchanged.
  A version-2 state is rejected by older builds with
  `Unsupported save-state version 2.` The section inspector's fast header path reads
  the table without decompressing payloads, so slot listing stays cheap.
- **Inspector/CLI.** `StateInfo`/`StateSectionInfo` add `Compressed` and
  `StoredLength`; `state inspect` prints the stored size and marks compressed
  sections with their stored size. `--json` includes the new PascalCase fields.
- **Evidence.** A 24,902,592-byte version-1 state becomes 647,647 bytes at 300 frames
  and 2,001,229 bytes at 14,900 frames. `SaveStateTests`,
  `StateWriterReaderTests`, `StateInspectorTests`, and `SaveStateSlotManagerTests`
  cover compressed/uncompressed roundtrips, version-1 loads, corrupt compressed
  payloads, invalid lengths/flags, and slot metadata. **876 tests pass.**
- **Do not** change the CRC to cover compressed bytes, remove the version-1 read
  path, or make the inspector decompress every section to list slots.

### 1.27 Twenty-fourth pass: Opera file-system reader for all containers (2026-10-29)

- **First-party parser.** `FiveDo.Storage.OperaFileSystem` reads the volume header,
  directory headers/entries, copies, and file data directly through `IDiscSource`.
  ISO, BIN, CUE+BIN, and CHD now share one parser; the `VideoGameFileSystemParser`
  package was removed.
- **Directory chaining.** Multi-block directories are followed through the relative
  `nextBlock` field and the last-in-block/last-in-directory flags. The parser rejects
  block cycles, duplicate directory references, traversal names, and malformed
  record offsets.
- **File layout.** `ReadFile` maps logical offsets through `burst`/`gap` layouts and
  honors partial reads and EOF. Special entries such as `Disc label` are exposed as
  files, matching the previous CHD output.
- **Evidence.** On the Lemmings pair (`artifacts/s2/lem.iso` and
  `artifacts/chdmaker/lem.chd`), both formats produce 482 identical entries (461
  files, 21 directories) and 461 byte-identical extracted files. Road Rash, Alone,
  and Theme Park ISOs also parse (260/247/291 entries). `OperaFileSystemTests` covers
  nested directories, multi-block directories, offsets/EOF, burst/gap reads, special
  entries, invalid volume headers, and traversal names; `FsListAndExtract_ReadAnIsoImage`
  covers the CLI path. **906 tests pass.**
- **Do not** re-add the `VideoGameFileSystemParser` dependency or make `fs` CHD-only
  again. Extend the parser with new on-disc evidence only.

---

## 2. Current evidence

### Wolfenstein 3D

From the 12,000-frame demo state (`w3d-fix12k.state`), the script

```
12000 A
12020 None
12200 Up
12320 None
12400 Up
12520 None
12600 Left
12660 None
12700 Up
12820 None
12900 A
12920 None
13000 Up
13120 None
13200 A
13220 None
13300 Up
13420 None
```

opens the Select Previews menu, navigates to Select a Level, and starts a fresh game
at FLOOR 1-1 with `SCORE 0000000`. The end state is at 14,000 frames.

Save/load:

- Live state saved at 14,060 frames: frame hash `0xC9989182`.
- Two independent 120-frame continuations: both end at `0x370158AC`.
- A zero-frame load: `0xC9989182` (pixel-identical).

The fresh game continues into a full movement loop. From the 14,000-frame state,
`w3d-walk.txt` walks, turns, and fires to 18,000 frames (`PC 0x00074A04`, hash
`0xF568EBCA`); the HUD, corridor, guard, and pistol are visible. A further
walk/turn/fire script reaches 19,200 frames (`PC 0x0001186C`, hash `0x74FBBF0C`).
The 18,000-frame state roundtrips exactly. The 4,000-frame WAV captured from it is
66.86 s, peak 32767, RMS 2980, with 18 clipped samples.

### Road Rash

No-input run: the intro FMV ends around 17,000 frames, the 3D race renders
correctly, and the attract race/FMV loop continues past 60,000 frames.

```
30,000 frames: PC 0x000681F0, hash 0xEB0AD59C, DSP running, no AllocMem error
60,000 frames: PC 0x00095E84, hash 0xBF01DF59, DSP running
```

From the live 26,000-frame state the attract race runs on its own and eventually
reaches the WRECK FMV. The 3DO controls are: **B accelerates, A brakes, the D-pad
steers left/right, C attacks, and P pauses**. Holding B starts a player race from
the `GAME LOADING` screen; the HUD speed rises and the camera follows the road.
Holding A or tapping any face button while the attract race is running freezes or
advances the current presentation, which is why earlier probes concluded that `A`
"changes the race". A hard left at about frame 31,000 clears the first oncoming
taxi; the next obstacle still ends the run.

`--autopilot road` is an experimental CLI driver that holds B and steers toward
the gray road centre (target screen X 230). v9 added a road-brightness ceiling,
detector-upset holds, closing-side steering, sharp-bend braking, urban throttle
pacing, a coast floor, and faster recovery. v10 adds close-traffic coast/steer,
urgent close-traffic preemption, emergency-brake steer hold, and a stopped/wreck
restart that holds B with straight bars until the scene moves. It reaches
**91,000 live frames** from the 80k/83k states (PC `0x00085F48`, hash
`0x02ABE78E`) and roundtrips there (zero-frame `0x02ABE78E`; two 120-frame
continuations both `PC 0x000973EC` / `0x3F27B3D6`). It still does not finish a
race: a 100,000-frame continuation pins at ~92,000 with lookahead
0.55/0.32/0.05, and a clean final-code run from 30,000 pins at the ~40,000
right-sidewalk wreck. The earlier "terminal pin" at ~34,000 frames was the
`BUSTED` screen itself (see sections 1.8 and 1.9). Unit tests cover the steering,
close-traffic, obstacle, brake, pacing, restart, and recovery contracts.

### Alone in the Dark

At 16,000 frames the character is upright at the staircase (`PC 0x0008C608`, hash
`0x3D257B5C`). The control run walks through the room; the D-pad/A script reaches
the Begin/Resume main menu.

### Lemmings

At 18,000 frames the title screen shows Play/New Game (`PC 0x000205F8`, hash
`0x5625A4CD`). With `--high-res` the same title renders at 640x480 with all four
buttons (Play, New Game, Load, Options) and the complete logo, matching the reference
(`PC 0x00012B48`, hash `0xE79E18C5`). The screen is an attract loop: `A`,
`Left`/`Right`, `P`, and `X` at the title do not leave it, even over 20,000 further
frames (the marquee cycles "SKILLS"). `Down` + `A` on New Game opens the
**passcode** screen (the string is "Enter the access code", not an account name).

The passcode keyboard is a 9-column grid (`A-I`, `J-R`, `S-Z` plus a backspace key)
of which five columns are visible at 320 pixels wide (`A-E`, `J-N`, `S-W`). The
selection starts at `N`; a 15-frame D-pad hold moves it one key. The entry field
starts with one `N` because the `A` press that opens the screen is seen by the
keyboard. `A` types the selected letter, `B` confirms the current entry early, and
the game **auto-confirms after the tenth letter**. A wrong entry shows "Incorrect"
and returns to the title. The earlier observation that `B` cancels was wrong: `B`
is the confirm key.

Valid 3DO codes are ten letters (for example Fun 2 `IJJLDNCCCN`, Tricky 1
`HCEONONPDX`, Tricky 3 `CAJJLDMBEV`). The entry field shows the typed text plus a
blinking preview of the selected key, which is why short captures alternate between
one and two characters. The high-resolution passcode screen is simply the 320-wide
UI scaled 2x; its five visible columns are the game's scrolling window, not a
framebuffer crop, so high-res pointer/hit-testing is not a separate coordinate
contract. The remaining work is an input sequence that reaches the ten specific
letters, not the renderer.

### CLI diagnostics

`run --json` now reports `sourceWidth`, `sourceHeight`, and `vdlModulo`, which makes
VDLP mode/crop investigations mechanical.

### Space Ace

12,000-frame run: `PC 0x0007D114`, hash `0xEF99FED7`, animated demo FMV, no runaway.

### Audio

The 2026-10-25 captures above are historical; they used the FreeDO 8.24 sample
clock (~737 samples/frame). The 2026-10-27 captures use the exact clock and are
10.000 s per 600 frames (441,018-441,020 samples): Wolfenstein 18,100 peak / 2,168
RMS, Lemmings 8,192 / 1,661, Gex 12,964 / 2,089, Road Rash 32,768 / 11,012 (2,036
clipped frames), Theme Park silent. `audio analyze <wav> --json` prints the report;
see [`TestCorpus.md`](TestCorpus.md) 1.5a2. The 18,000-frame Wolfenstein gameplay
capture from the older clock was 66.86 s, peak 32767, RMS 2980, 18 clipped.

---

## 3. Ordered plan

### Step 1 - complete the menus

- Wolfenstein: done for the fresh-game loop; the existing 14,000-18,000 script and
  save/load proof are in section 2.
- Lemmings: DONE (2026-10-24 late pass, pure D-pad on 2026-10-26). The Fun 4
  passcode `HNLHCIOECW` is now typed entirely with the D-pad/A by
  `docs/scripts/lem-fun4-dpad.txt` from a passcode-screen state; the game shows
  the Fun 4 briefing at 22,600 and starts gameplay at 23,000 (`PC 0x00083FE8`,
  hash `0x63C02DED`; zero-frame load roundtrips, two 120-frame continuations both
  `PC 0x000230A4`, hash `0x4CA339CD`). The earlier poke path (`lem-pass-N.state` + 9-byte poke + `B`)
  remains valid and reaches 21,640 (`PC 0x83FEC`, hash `0x18F38B4D`). The entry
  buffer is permuted (display order `77,76,75,74,7B,7A,79,78,7F,7E`; the preview
  is the next untyped slot), not sequential at `0x96574`.
- Road Rash: the control map is known (B accelerate, A brake, D-pad steer, C
  attack, P pause). Driver v10 is traffic-aware (close-traffic coast/steer,
  urgent preemption for close traffic, obstacle steer, wall avoidance,
  sharp-bend braking, closing-side crossover, emergency brake, narrow-canyon
  coasting, urban throttle pacing, coast floor, detector-upset hold, side-based
  stuck recovery, stopped/wreck throttle-only restart, low-band centre at rows
  190-214, road brightness <= 120; 667 tests pass). The CD media-access pulse
  fixes the old ~42k/~92k READ_ID wait: those screens now advance to `BUSTED`,
  race select, and a new race. The autopilot also pulses A during long-static,
  low-coverage, CD-load, or 180-frame restart-stuck states (`autopilotMenuAdvance`).
  The 5.5-mile race is still not finished: from the 60k state the autopilot
  passes `GAME LOADING`/`Get ready` and starts a new race at 80k, then cycles
  race -> wreck -> menu -> new race to 100k. Next: finish a full race without
  wrecking, not CD work. The previous "terminal wall pin ~34,000" was the
  `BUSTED` screen, and the old "~40,000 right-sidewalk wreck" was the CD wait,
  now fixed.
- Space Ace: **done.** `X` at 12,000 returns to the menu; `A` at 12,200 starts
  the interactive scene. The 15,000-frame state roundtrips (`0x7E780D93` -> two
  120-frame continuations at `PC 0x000284EC`, `0x70B16422`) and a gameplay WAV
  was captured. Longer scene/decision validation remains optional.

### Step 2 - save states and screenshots

**Done in the ninth pass.** Each title loads its live state at zero frames with the
saved hash, runs 120 frames deterministically, and has a PNG screenshot. The table
is in [`TestCorpus.md`](TestCorpus.md) 1.5a. NVRAM and disc state survive the
roundtrip; the CLI loads a fresh NVRAM by default unless `--nvram` is given. Space
Ace's interactive 15,000-frame state and Alone's Begin/Resume menu are also
captured. The remaining evidence work is longer per-title gameplay clips, not the
save/load proof.

### Step 3 - audio quality

**Done in the sixteenth pass (2026-10-27).**

- The exact 44.1 kHz clock fixed the 737-vs-735 cadence; 600 frames are 10.000 s.
  Old/new Road Rash captures have identical spectral peaks and peak/RMS; the
  content did not change.
- Live OpenAL output works in the trimmed and NativeAOT CLI; underruns are padded
  and counted. Audio-driven frame pacing is implemented in `CoreRunner` and the
  CLI `--realtime` path.
- WAV captures are buffered and instrumented, and `audio analyze` reports
  peak/RMS/clipping/silence/DC. Captures are in `TestCorpus.md` 1.5a2.
- The 90 s Road Rash boot capture matches Opera's loopback: identical 0.914 peak,
  RMS within 4.7%, spectral cosine 0.9994 over 20 Hz-20 kHz, the same 441.4 Hz
  tone, and a 10 s-envelope correlation of 0.9685 after the capture-offset window.
  See `TestCorpus.md` 1.5a2.
- Remaining: a manual check that a game changing DSP modes stops/starts without a
  click. No current corpus title was observed stopping the DSP mid-run.
- Road Rash's clipping is the game's loud race mix (2,036 frames in a 10 s 92k-state
  capture), not the output path; live output applies a soft peak limiter.

### Step 4 - performance

**Done in the seventeenth pass (2026-10-28).**

- Road Rash's 3D race was measured from `artifacts/rr-race2-100k.state` with the
  BIOS and disc inserted: 600 frames after 120 warm-up frames, **5.46 ms/frame
  total (p50 2.91, p95 10.60, p99 13.14), 0 bytes/frame, 183 fps**. Core-only is
  5.39 ms mean / 13.01 ms p99; pack/mailbox/read add ~25/21/21 µs. This is inside
  the 16.67 ms budget at p99 with ~3.5 ms headroom. See
  [`Performance.md`](Performance.md) and the `--state-in`/`--warmup` benchmark
  options.

---

## 4. Acceptance criteria

- [x] At least one title completes a full gameplay loop with input (Wolfenstein:
  fresh game -> walk/turn/fire -> save/load; Lemmings: passcode -> briefing ->
  gameplay -> save/load).
- [x] Save/load works in-game (Wolfenstein: `0xC9989182` -> `0x370158AC` twice;
  Lemmings Fun 4: `0x18F38B4D` zero-frame, two 120-frame continuations both
  `0x8D628FBD`).
- [x] WAV output is non-silent and stereo 44.1 kHz; clipping/cadence recorded.
- [x] Road Rash's race renders correctly and runs past 60,000 frames.
- [x] The high-resolution 640x480 renderer matches Lemmings' title reference.
- [x] The Lemmings passcode confirm path is complete (Fun 4 gameplay + roundtrip).
- [x] The Lemmings passcode is typed entirely with the D-pad
  (`docs/scripts/lem-fun4-dpad.txt`, gameplay at 23,000 frames,
  `PC 0x00083FE8`, hash `0x63C02DED`, roundtrip `PC 0x000230A4` / `0x4CA339CD`
  twice with the exact audio clock).
- [x] CRT bloom is implemented in the OpenGL presenter and exposed in Settings.
- [x] HQx 2x/3x/4x are implemented and match the C++ hq2x/hq3x/hq4x reference
  hashes.
- [x] The benchmark budget gate runs in CI (0 bytes/frame, core p99 <= 8 ms,
  total mean <= 8 ms, total p99 <= 16.667 ms).
- [x] CLI settings/input/save-slot JSON and every `--json` response are
  source-generated; the trimmed win-x64 CLI passes `--version`, `selftest`,
  `bench`, `info`, `state inspect`, `state slots`, `fs list`, CHD boot/run, and a
  save-state roundtrip.
- [x] Portable CLI archives build for win-x64 (zip) and linux-x64 (tar.gz) and
  contain the executable, notices, licensing, and `PACKAGE.txt`.
- [x] The Avalonia frontend publishes trimmed for all six RIDs; a trimmed win-x64
  build opens the real window and passes the `--smoke-test` marker/exit-code check.
  `ViewLocator` reflection is gone and all bindings are compiled.
- [ ] The Road Rash city race is finished. The CD media-access pulse fixes the
  old ~42k/~92k READ_ID-only wait and advances to `BUSTED`, race select, and a
  new race; the autopilot passes `GAME LOADING`/`Get ready` and cycles
  race/wreck/menu to 100k. It still does **not finish a full 5.5-mile race**.
  The old 91k hash `0x02ABE78E` was a frozen presentation; the current chain
  reaches `PC 0x00093AF4`, hash `0x70E1ED8E`. The high-res pointer path is still
  unproven.
- [x] Space Ace's interactive path is validated (X menu, A play, 15,000-frame
  roundtrip and WAV).
- [x] Per-title save-state, screenshot, and WAV evidence is recorded
  (`TestCorpus.md` 1.5a).
- [x] `dotnet build -c Release`, `dotnet test`, and `dotnet format --verify-no-changes` pass.
---

## 5. Do not do

- Do not redo the eighth-pass work: the GPU palette per-channel CLUT fix, xBR filter,
  gamepad backends/mapping UI, log viewer, save-state thumbnails, or first-run/error
  dialogs are complete and tested. Extend them only with new evidence.
- Do not re-enable the `AudioSink?.PushSample(Dsp.Loop())` short-circuit.
- Do not revert the exact DSP sample clock to FreeDO's 8.24 `qrz_AccDSP` path. It is
  the difference between 44.1 kHz and ~44.4 kHz audio. Keep VDL/timer on the FreeDO
  accumulators; only the DSP clock is exact.
- Do not remove the bundled OpenAL Soft runtime, the `OpenAL32.dll` copy targets, or
  the audio-thread `alcMakeContextCurrent` call. They fix silent live audio on
  machines without a system OpenAL install.
- Do not remove audio-driven pacing or its 60 Hz fallback. The exact clock makes the
  nominal period 16.67 ms; the fallback protects reset/state-load frame deltas.
- Do not make the WAV sink write one file write per sample again; the buffered sink
  is required for long captures.
- Do not change the XBUS DMA byte order (`IRam` uses `b0,b1,b2,b3`).
- Do not port `opera_cdrom.c` wholesale; the CD path is working.
- Do not commit BIOS ROMs, game images, extracted assets, or WAV captures.
- Do not change `Arm60.Ncycle` from 4 without a differential test.
- Do not "fix" the arbitrary-quad read/write order back to `(i, j)`; the row is `i`,
  the column is `j`, and the helpers take `(x, y)`.
- Do not enable high-resolution mode by default. Keep `--high-res` opt-in until the
  passcode pointer/hit-test path is understood; the five 3,000-frame low-res hashes
  must stay unchanged.
- Do not remove the CD media-access pulse (two consecutive READ_ID polls with a
  >=50-frame gap set poll bit `0x40`). It fixes the old ~42k/~92k READ_ID-only wait
  and keeps the five 3,000-frame hashes unchanged. The old 91k hash `0x02ABE78E` is
  historical; do not use it as a regression key.
- Do not redo the ninth-pass evidence capture, Space Ace interactive start,
  `cdStatus`/`cdPoll` diagnostics, or the autopilot menu-advance plumbing. Extend
  them with new evidence only.
- Do not redo the tenth-pass Lemmings pure D-pad passcode script
  (`docs/scripts/lem-fun4-dpad.txt`), CRT bloom, or HQx 2x. Extend them with new
  evidence only.
- Do not redo the eleventh-pass HQx 3x/4x patterns (`HqxPatterns3.cs`,
  `HqxPatterns4.cs`) or the benchmark budget gate. Extend them with new evidence
  only.
- Do not redo the twelfth-pass trim-safe JSON contexts, named CLI result records,
  `DiagnosticMethodInfo` watchpoint formatter, trimmed CLI publish, benchmark
  baseline comparison, or portable packaging script. Do not reintroduce
  reflection-based `JsonSerializer` calls in settings, input, save-slot, or CLI
  output code; use the existing `JsonSerializerContext` metadata.
- Do not redo the thirteenth-pass frontend trimming: `PublishTrimmed=true`/
  `TrimMode=full`, the removed `ViewLocator`, the explicit window construction, the
  compiled `x:DataType` bindings, or the `--smoke-test`/`--smoke-output` startup
  path. Do not reintroduce a reflection-based view locator or reflection-based XAML
  lookup in the frontend.
- Do not redo the fourteenth-pass swap-interval wiring or the `--gl-probe` tool:
  `SwapInterval.TryCreate`, `OffscreenRenderTarget`, the probe options/JSON
  context, and the measured 4K results are complete. Extend the probe with new
  evidence only; do not replace it with a reflection-based JSON path.
- Do not redo the fifteenth-pass `--gl-backend` selection, `--gl-probe-capture`
  parity path, or the measured WGL/ANGLE/GLES 3.0 results. Extend them with
  GLES 3.1/DPI evidence only.
- Do not redo the twentieth-pass Vulkan backend or `--vk-probe`: the offscreen
  two-stage pipeline, build-time SPIR-V shaders, 320x240 CPU parity, 4K/1080p probe
  results, and optional `--gl-backend vulkan` desktop path are implemented. Do not
  redo the twenty-first-pass Vulkan xBR/FSR GPU passes or the GPU-palette OpenGL xBR
  source; extend the backend with a native surface/swapchain, HQx, or non-Windows
  device evidence only.

---

## 6. Useful commands

```powershell
$o = "artifacts\s2"
$b = Join-Path $PWD "bios\panafz1.bin"
$g = $env:FIVEDO_GAMES   # main collection: J:\Panasonic 3DO

# Build/test/format
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release
dotnet format --verify-no-changes

# Trimmed CLI publish and smoke test (see docs/Trimming.md)
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -m:1 -o "$o\cli-trim"
& "$o\cli-trim\FiveDo.Cli.exe" selftest --json

# Trimmed frontend publish and startup smoke test (see docs/Trimming.md)
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -m:1 `
  -o "$o\frontend-trim"
$p = Start-Process -FilePath "$o\frontend-trim\FiveDo.Frontend.Avalonia.exe" `
  -ArgumentList '--smoke-test', '--smoke-output', "$o\frontend-smoke.txt" -PassThru
$p.WaitForExit(60000)
Get-Content "$o\frontend-smoke.txt"

# GPU presentation probe (WGL, ANGLE/GLES, or software)
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend angle `
  --gl-probe artifacts/gl-probe/angle-4k-sharp.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen

# CPU/GPU parity: capture a 1:1 Accurate frame and compare it with FrameAssembler
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend wgl --gl-probe artifacts/gl-probe/parity-wgl.json `
  --gl-probe-size 320x240 --gl-probe-filter accurate --gl-probe-offscreen `
  --gl-probe-capture artifacts/gl-probe/parity-wgl.png

# Road Rash: render the race and capture every 1,000 frames
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Road Rash (USA).chd" --bios $b --frames 30000 `
  --capture-dir "$o\rr-race" --capture-every 1000 `
  --frame-out "$o\rr-race.png" --state-out "$o\rr30k.state" --json

# Wolfenstein: start a fresh game from the 12k demo state
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-fix12k.state" `
  --frames 2000 --input-script "$o\w3d-play.txt" `
  --frame-out "$o\w3d-play.png" --state-out "$o\w3d-play.state" --json

# Wolfenstein: walk/turn/fire from the fresh-game state
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-play.state" `
  --frames 4000 --input-script "$o\w3d-walk.txt" `
  --frame-out "$o\w3d-gameplay.png" --state-out "$o\w3d-gameplay.state" --json

# Lemmings: full 640x480 high-resolution title (opt-in)
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Lemmings (USA).chd" --bios $b --frames 18100 --high-res `
  --input-script "$o\lem-menu.txt" --frame-out "$o\lem-hires-title.png" --json

# Lemmings: type the Fun 4 passcode with the D-pad and start the level
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Lemmings (USA).chd" --bios $b --state-in "$o\lem_pass_fresh.state" `
  --frames 1300 --input-script docs/scripts/lem-fun4-dpad.txt --script-base state `
  --frame-out "$o\lem-dpad-gameplay.png" --state-out "$o\lem-dpad-gameplay.state" --json

# Save/load roundtrip (replace the state with a live gameplay state)
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-live.state" `
  --frames 120 --json

# Audio capture from a live state
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Road Rash (USA).chd" --bios $b --state-in "$o\rr30k.state" `
  --frames 600 --audio wav --wav-out "$o\rr-game.wav" --json

# RAM dump at a trace stop
dotnet run -c Release --project src/FiveDo.Cli -- trace `
  "$g\Road Rash (USA).chd" --bios $b --frames 16000 --trace-frames 2200 `
  --stop-kprint "AllocMem error" --out "$o\stop.txt" `
  --dump-ram "$o\stop-ram.bin" --dump-ram-offset 0 --dump-ram-length 0x300000

# Opera reference (the bundled Windows OperaCore build)
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$o\roadrash.iso"
```

The data-track extraction command is in [`TestCorpus.md`](TestCorpus.md).

---

## 7. Open questions

1. ~~What input sequence reaches the ten letters of a valid Lemmings passcode most
   reliably, and does the level start immediately after confirmation?~~ Answered
   2026-10-24: from the `N`-selected state, the 9-byte poke for `HNLHCIOECW` plus
   `B` is accepted, `A` shows the briefing, and `A` starts gameplay (see
   `TestCorpus.md` 1.4). Pure D-pad typing of all ten letters was not needed.
2. What obstacle-avoidance and wreck-recovery policy lets `--autopilot road` finish
   the 5.5-mile city race? Answered in part 2026-10-25: the "stuck bike" was the
   `BUSTED` screen, and the CD media-access pulse fixes the old ~42k/~92k
   READ_ID-only wait. The game now advances to `BUSTED`, race select, and a new
   race. The remaining experiment is **start-line/right-edge handling** after the
   race-select flow, not more CD or pre-race tuning.
3. ~~Does the 737 vs 735 samples/frame cadence cause audible pitch drift?~~
   **Answered 2026-10-27:** yes, the FreeDO 8.24 scheduler over-produced samples
   by ~0.7%. The exact DSP clock now matches Opera's 44.1 kHz rate; `_dspPcnt` was
   not the cause and was left at 567.
4. Is the 191-sample Road Rash clipping in the game mix or in our DSP output path?
5. Does the VDLP width fix expose any 384/512-mode title that previously appeared
   only because of the 320-pixel crop?
6. Why does Wolfenstein's **fresh game** stay on the static level screen for more than
   54,000 frames? The CD trace shows one 24-sector read every ~33 frames (about
   87 KB/s) and revisits the 05:11-05:3b region. Setting the drive 2X status bit and
   publishing the Opera-style read-completion status were both tried and reverted:
   they either did not change the load or broke the golden CD status (0xE1 -> 0xE3)
   and the 3,000-frame corpus hashes. The demo gameplay at 12,000 frames is unaffected.
   The 18,000-frame walk loop now proves the level is playable. Next: compare the CD
   task's timing with Opera, check whether the drive speed query or read-completion
   status is expected, and measure CHD read time separately from guest-side pacing.
7. What is the exact high-resolution coordinate contract? The current mode treats
   CEL X as 640-wide and Y as a 240-line field that the VDLP line-doubles; this
   matches the Lemmings title. The passcode screen is a 320-wide UI scaled 2x, so
   its five visible keyboard columns are the game's own window, not a framebuffer
   crop. The remaining question is whether any title uses a true 640x240
   non-interlaced mode that the current line-double path should not upscale.
