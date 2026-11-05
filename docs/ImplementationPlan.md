<a id="master-dashboard"></a>
# 5DO — Implementation Plan and Missing-Feature Backlog

**Snapshot:** 2026-11-04
**Current release:** `1.2.1`
**Repository:** <https://github.com/purelogiccode/5DO><br>
**Branch:** `master`<br>
**Status:** **1,773 tests pass** (last recorded); `dotnet build` reports **0 warnings / 0 errors**; `dotnet format --verify-no-changes` is clean; `jb inspectcode --severity=WARNING` reports **0 results**.<br>
**Goal:** implement **every Opera core feature**, match or exceed Opera's accuracy, and ship the best standalone 3DO emulator on Windows, Linux, and macOS.

> **This is the single source of truth.** It replaces the former 8,000-line dashboard, milestone log, feature spec, parity audit, and session handoff. Historical evidence stays in the linked docs; this file is the canonical backlog.

---

## 1. How to use this plan

1. **Start with the urgent list.** Pick the first unchecked item that is not blocked. Do not start optional or research work while a P0 accuracy/compatibility item is open.
2. **One feature per change.** Do not mix core accuracy, rendering, and UX work in one pull request.
3. **Tests first.** Every behavior change needs a unit, golden, differential, or scripted manual test.
4. **One reference per change.** State whether the behavior comes from Opera, 4DO/FreeDO, hardware documentation, or a 5DO-specific decision. Port behavior, not code.
5. **Mark it done only when its “Done when” line is satisfied.** Then move the entry to the completed log at the end of this file.
6. **No duplicate tracking.** Each feature has one canonical entry below. IDs are stable. `BAK-*` and `MOON-*` are retired aliases (see Appendix B); do not recreate them.
7. **No default timing change without full-collection differential evidence.**

### Legend

- `[ ]` — remaining work. `[x]` is used only in the completed log.
- **Pending** — not started, or research/design only.
- **Partial** — a usable subset ships; the remaining scope is listed in the item.
- **Blocked** — depends on another item, legal review, or hardware.
- **Priority** — **P0** correctness/compatibility, **P1** expected best-in-class, **P2** ecosystem multiplier, **P3** long-term.
- **Effort** — **S** under 3 developer-days, **M** 3–10, **L** 10–30, **XL** 30–90+.

---

## 2. Current state

| Area | What ships today | What is still missing |
|---|---|---|
| Core emulation | ARM60 interpreter, CLIO timers/FIFO/DMA/FIQ, MADAM CEL/pixel/matrix engines, DSP at exactly 44.1 kHz, SPORT, VDLP, XBUS, CD-ROM, input, configurable DRAM/VRAM (8 Opera layouts), NVRAM, deterministic save states v3 | Full Opera timing parity (ARM/CLIO/CEL/CD), SWI HLE timing parity, embedded boot ROM, unsigned-disc boot, CD subcode/DIPIR/read-ahead |
| Frontend | Avalonia shell, library, metadata/favorites/playtime, settings, gamepad mapping, save slots/thumbnails, screenshots, log viewer, disc browser, BIOS/NVRAM manager, rewind, fast-forward, cheats, TAS movies, OSD/quick menu, input display, portable mode, instance lock, diagnostics bundle, update check | Netplay, achievements, Lua, big-picture UI, localization, themes/accessibility, installers, auto-update, debugger/profiler |
| Renderer | Two-stage OpenGL/GLES (default), optional Vulkan (Windows surface), optional Direct3D 11 (Windows), 4K output, nearest/accurate/sharp-bilinear/CRT/xBR/HQx/FSR, per-edge overscan, void border, screenshot gallery | True 640x480/480i CEL rendering, supersampling, GPU CEL, shader presets, NTSC/HDR, recording, overlays/bezels, Linux/macOS Vulkan surfaces, 480i deinterlacing |
| Audio | Exact 44.1 kHz DSP clock, OpenAL Soft output, WAV capture, `audio analyze`, CD-DA mixing/volume/mute | Resampler quality/exclusive mode/sync offset, mixer/spectrum, DSP debugger, audio golden corpus |
| Disc support | ISO, BIN, CUE+BIN, CHD parent/child, multi-track CUE/CHD TOC, CD-DA sectors/mixing, M3U multi-disc playlist, live swap, guest inject, Opera file system, prefetch, DAT health | Full CD command/timing parity, subcode, read-ahead, known-title CD-DA evidence, real in-game multi-disc swap, ODE prompts, physical drive, image conversion |
| Compatibility | 594 discs: **594 load**, **593 render**, 1 known boot failure (Crash 'n Burn Japan), 0 not loadable | 594/594 render; visual/in-game review; per-game auto-profiles |
| Differential harness | FiveDO.DiffHarness, checkpoints, audio/state hashes, smoke + five-title showcase, 594-title manifest generator | Full 594-title run, PC-delta triage, CD/state transcripts, nightly dashboard |
| Tests | **1,773** passing; fuzz harness for six parsers; build/format/inspectcode clean | 2,000+ tests, coverage gate, hardware test ROM suite, audio/video golden suites, soak, GPU/platform CI |
| Performance | Road Rash 3D race **4.20 ms/frame**, p99 **7.40 ms**; 4K Tier A measured; zero-allocation synthetic path | Tier B 1080p60, GLES 3.1/ANGLE, arm64 tuning, threaded DSP, managed JIT, zero allocations on all real paths |
| Platforms | Six bundles: Windows/Linux/macOS × x64/arm64, framework-dependent single-file apphosts; Windows x64 validated; Linux x64 tests/smoke pass (GPU preview under llvmpipe); macOS and arm64 cross-published but not hardware-validated | macOS runtime validation, arm64 hardware validation, Linux/macOS GPU presentation, macOS gamepad, Flatpak/AppImage, installers/signing |
| Release | **v1.2.1** published with six versioned bundles and `SHA256SUMS.txt`; .NET 10 runtime required (not bundled); legal/attribution sign-off recorded | Signing, notarization, auto-update, reproducible builds, SBOM |

**Scope decisions (unchanged):** WebAssembly/browser, Android/iOS, Raspberry Pi, and Steam Deck are **out of scope**. Desktop Windows, Linux, and macOS (x64/arm64) remain the supported targets. 4K *internal* CEL rendering is research, not a release blocker; 4K *output* upscaling is supported. Distribution remains free, open-source **donationware** under the FreeDO modified GNU LGPL (non-commercial; voluntary donations only).

### 2.1 Shipped baseline

These are done and must not regress. They are listed here once; the backlog below contains only missing work.

| Area | Shipped capabilities |
|---|---|
| Core | ARM60 interpreter; CLIO timers, FIFO, DMA, interrupts; MADAM CEL/pixel/matrix; VDLP; SPORT; DSP; XBUS; CD-ROM; NVRAM; Quartz state v3; save states v3; rewind buffer; fast-forward; cheats; TAS movie container/record/replay/verify/branch; six-port PBUS input |
| Disc | ISO/BIN/CUE/CHD, parent/child CHD, multi-track TOC, CD-DA mixing and volume, M3U/M3U8 playlist, live/F3/UI/CLI swap, guest inject, Opera FS parser, prefetch cache, Redump/No-Intro DAT verification, library health badges |
| Video | OpenGL/GLES two-stage presenter; optional Vulkan offscreen and Win32 surface; optional Direct3D 11 Windows presenter; 4K output; nearest/accurate/sharp-bilinear/CRT/xBR/HQx/FSR; per-edge overscan; void border/color; opt-in line-doubled high-res; screenshots; gallery filters/compare/export/sidecars; GIF recording |
| Audio | Exact 44.1 kHz clock; OpenAL Soft; WAV capture; `audio analyze`; CD-DA mix; volume/mute; audio-driven pacing |
| Frontend | Library scan/dedupe; favorites/playtime/history; offline metadata editor; settings; keyboard/gamepad bindings; ten save slots with thumbnails; screenshot gallery; log viewer; disc browser/info; BIOS/NVRAM manager; OSD/quick menu; input display; playlist editor; portable mode; instance lock; diagnostics bundle; startup update check; startup auto-open of the last BIOS/disc |
| CLI | `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks/verify/playlist`, `library verify`, `fs`, `bios`, `nvram`, `cheat`, `movie`, `settings`, `audio`, `state`, `disasm`, `update` |
| Release | v1.2.1 six bundles; versioned archives; checksums; legal/attribution sign-off; OpenAL LGPL obligations |

**Completed IDs (traceability only):** FND-01, FND-02, FND-03, CORE-09, CORE-12, CORE-14, CORE-15, SYS-04, SYS-07, SYS-09, SYS-10, SYS-11, SYS-13, SYS-14, VID-05, VID-06, VID-14, VID-15, AUD-05, UX-01, UX-02, UX-04, UX-11, UX-12, UX-15, UX-19, UX-23, ENG-03, BAK-12, BAK-17.

**Milestone aliases:** M9R/M9P → P3/P6/TEST-04; M10 → PERF-02; M12 → PERF-08; M13.5/M13.6 → TEST-06; M14 → VID-01/02/03; M15 → CORE-11/16 + FND-05.

---

## 3. Missing features — canonical backlog

Each entry below is a single feature or a tightly-scoped bundle. The `Tracks` field preserves legacy IDs. **No feature appears in more than one category.** A few legacy aliases (`BAK-04`, `BAK-05`, `BAK-13`, `BAK-18`) intentionally have no separate checkbox; they are covered by the canonical items listed in Appendix B.

### 3.1 Priority — urgent

- [ ] **CORE-01 — ARM60 cycle-class accuracy mode**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-01
  **Remaining:** run the full-collection differential on timing-sensitive titles; the 2026-11-04 40-title A/B found ARM60 17/40 matches versus FreeDO 15/40 with identical Opera captures, and the decision keeps the ARM60 mode opt-in until the 594-disc evidence is green. Per-class costs and the `--cycle-classes` ledger are complete and regression-tested.
  **Done when:** cycle counts match Opera within documented tolerances; golden hashes are unchanged or improved; no full-collection regression.
  **Ref:** `opera_arm.c`, `opera_clock.c`.

- [ ] **CORE-02 — CLIO timer scheduler parity**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-02
  **Remaining:** validate Opera's per-slot scheduler, slack gap, and cascade carry across the full collection; add differential timer evidence; decide whether the Opera scheduler can become the default. The 2026-11-04 increment added 16-bit timer-register masking in Opera mode, per-timer telemetry (`--clio-timer-stats`), a bounded `--clio-timer-trace`, a reference-model conformance test, and a `--clio-timers` A/B override in the differential harness; the mode stays opt-in pending the full 594-disc run.
  **Done when:** timer traces match Opera within documented tolerances; no golden-corpus regression.
  **Ref:** `opera_clio.c`, `opera_clock.c`.

- [ ] **CORE-03 — MADAM CEL busy timing parity**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** CORE-03
  **Remaining:** validate the deferred draw and pixel/texel busy deadline against Opera across CEL-status polling titles; tune the busy budget. The 2026-11-04 increment fixed line-rendered literal/packed pixel accounting and locked the 9M-pixel/s plus 64-cycle-per-CEL formula with tests.
  **Done when:** CEL busy traces match Opera within documented tolerances; timing-sensitive titles behave correctly; no frame-time regression.
  **Ref:** `opera_madam.c`.

- [ ] **CORE-08 — Accuracy/performance profile coverage**
  **Status:** Partial · **Priority:** P0 · **Effort:** S · **Tracks:** CORE-08
  **Remaining:** profile validation across the full collection; decide when the Accuracy profile can become the default. The 2026-11-04 increment completed the remaining CLI surfaces: `bench --profile` reports the derived profile and timing modes, `selftest` validates determinism and state round-trip for every named profile, and the differential harness accepts `--profile` for a full-collection A/B. A 40-title, 300-frame A/B reports 17/40 matches for both `compatible` and `accuracy` with identical per-title statuses; the 594-title A/B remains. The Accuracy profile stays opt-in until that full A/B report shows no regression against the Compatible baseline.
  **Done when:** every profile round-trips through CLI/Settings/state and the Accuracy profile can be enabled by default after evidence is green.

- [x] **SYS-08 — CPU overclock/underclock validation**
  **Status:** Complete · **Priority:** P0 · **Effort:** S · **Tracks:** SYS-08
  **Remaining:** none. Non-stock clocks remain experimental and are not seeded into profiles.
  **Done when:** the clock matrix is documented and the stock 12.5 MHz default is unchanged.
  **Evidence:** `Performance.md` §8.1 records the 0.5x-4x synthetic matrix; §8.3 adds the 2026-11-05 five-title A/B (Road Rash, Lemmings, Alone in the Dark, Ballz, AutoBahn Tokio). Every non-stock clock changed the frame hash, and AutoBahn Tokio slowed from 9.1 s at 1x to 84.4 s at 2x and 271.7 s at 4x, confirming that non-stock clocks are a compatibility variable rather than a speed switch. The stock 12.5 MHz default is unchanged.

- [ ] **CORE-04 — CD-ROM command and timing parity**
  **Status:** Partial · **Priority:** P0 · **Effort:** XL · **Tracks:** CORE-04
  **Remaining:** command latency table; per-command sector readiness; TOC/Sub-Q/raw-sector behavior; data-sector subcode and read-ahead; CD-side tagged status/chunks; retire the READ_ID media-access heuristic; raw-vs-Opera command/status/boot/reset transcripts.
  **Done when:** fixed command transcripts match Opera or have documented, tested divergences; the 594-disc sweep has no CD regression; CD-DA and ODE layer cleanly on the same model.
  **Ref:** `opera_cdrom.c`; `docs/ReferenceStrategy.md` §6.

- [ ] **CORE-06 — SPORT/XBUS/DIPIR timing and DMA audit**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-06
  **Remaining:** full Opera trace diffs for SPORT/XBUS/DMA; DIPIR activation completion; XBUS poll/FIFO semantics; DMA start/completion traces; PBUS/SPORT edge cases; reset and state interactions.
  **Done when:** trace diffs are clean or documented; DMA/FIQ tests stay green; no regression on golden titles.

- [ ] **CORE-07 — FIQ/IRQ latency and interrupt audit**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-07
  **Remaining:** Opera differential traces for masked/nested/return/re-entry interrupts; request-to-vector latency distributions; CLIO/DIPIR/XBUS ordering; timer and FIQ edge cases; document telemetry fields.
  **Done when:** latency metrics match Opera within tolerance; traces and `run --json` statistics are documented and regression-tested.

- [ ] **SYS-01 — CD-DA and multi-track completion**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-01
  **Remaining:** CD-DA read-ahead and subcode; known-title CD-DA evidence; a 10-second DSP+CD-DA WAV; multi-track edge cases.
  **Done when:** a known CD-DA title plays its audio; a 10-second mixed WAV contains both DSP and CD-DA; data-track titles keep identical hashes.

- [ ] **SYS-02 — ODE and multi-disc completion**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** SYS-02
  **Remaining:** guest-driven ODE swap prompts; a real in-game multi-disc swap with save/state round-trip; playlist edge cases and recovery.
  **Done when:** a known multi-disc title swaps in-game and continues; state load after swap is correct.

- [ ] **SYS-03 — PAL/NTSC completion**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-03
  **Remaining:** PAL2 title evidence; automatic region/profile application; full PAL library validation; 50 Hz audio/pacing evidence; region-aware screenshots, saves, and CLI output.
  **Done when:** PAL1/PAL2 titles render and pace correctly; NTSC behavior is unchanged; per-game region selection applies automatically.

- [ ] **SYS-05 — Embedded open-source boot ROM**
  **Status:** Pending · **Priority:** P0 · **Effort:** M · **Tracks:** SYS-05
  **Remaining:** a legally cleared embedded open-source Opera Boot ROM; default/fallback selection; no-BIOS homebrew boot; CLI/Settings/state/tests.
  **Done when:** homebrew/demos boot without a user BIOS and legal sign-off is recorded.
  **Ref:** `opera_boot.h`, `opera_bios.c`.

- [ ] **SYS-06 — RSA/unsigned-disc option**
  **Status:** Pending · **Priority:** P0 · **Effort:** M · **Tracks:** SYS-06
  **Remaining:** explicit unsigned-disc support (prefer a documented `norsa` BIOS or user-provided patch); legal review; CLI/Settings/state; tests.
  **Done when:** unsigned software runs with an explicit option and legal sign-off is recorded.

- [ ] **SYS-12 — Portfolio OS SWI HLE**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-12
  **Remaining:** full-collection compatibility/performance validation; decide whether to charge the skipped guest handler cycles; document the titles where HLE changes timing. The 2026-11-04 increment implemented Opera's 14-SWI HLE set (`0x50000`-`0x50012`, excluding the object/Dot4 fallbacks), the `--swi-hle`/Settings/profile opt-in, save-state Options v9, `--swi-comments` diagnostics, 33 CPU/machine tests that match `opera_fixedpoint_math.c`, and a real-BIOS synthetic 4x4 benchmark (4,096 calls: 847,860 LLE steps / 4,054,999 cycles versus 24,578 HLE steps / 65,538 cycles). Default-off hashes are unchanged; HLE is intentionally not bit-identical to the guest matrix path because it skips the handler's cycles and register clobbers.
  **Done when:** HLE is opt-in, passes supported-title tests, and shows a measured performance gain without behavior changes when disabled.
  **Ref:** `opera_swi_hle_0x5XXXX.h`.

- [ ] **SYS-15 — Input devices, calibration, and options**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-15
  **Remaining:** per-device calibration; scripted gameplay evidence for mouse, lightgun, arcade lightgun, flightstick, and Orbatak trackball; arcade start/coin/service mapping. The 2026-11-04 increment added the hide-lightgun-crosshair option, eight PBUS ports, an active-device cap of 0-8 (`--active-devices`/Settings), and a 256-byte PBUS chain buffer.
  **Done when:** a representative title works for each device; calibration and crosshair options are documented and tested.
  **Ref:** `lr_input.c`, `opera_pbus.c`, `libretro_core_options.c` (`opera_active_devices`, `opera_hide_lightgun_crosshairs`).

- [ ] **SYS-16 — Six-player and multi-tap verification**
  **Status:** Pending · **Priority:** P0 · **Effort:** S · **Tracks:** SYS-16
  **Remaining:** six-player test ROM/title; hardware multi-tap trace. The 2026-11-04 increment added eight PBUS ports, the Opera chain for ports 7-8, a 256-byte MADAM queue, and an eight-player packet test through `FiveDoMachine` (`EightPlayerPacket_ReachesTheMadamPbusQueue`).
  **Done when:** six pads and multi-tap are verified on hardware or a test ROM.

- [x] **SYS-17 — Game database expansion and community DB**
  **Status:** Complete · **Priority:** P0 · **Effort:** M · **Tracks:** SYS-17
  **Remaining:** none for the local-collection gate. NVRAM/cheat seeding is SYS-18 follow-up work.
  **Done when:** at least 95% of local images have metadata and the DB has a repeatable contribution path.
  **Evidence:** the embedded database grew from 518 to 650 records. `gamedb stats --folder "J:\Panasonic 3DO"` reports 1,363 readable images, 650 unique IDs, 1,363 matched images, and 100% coverage of readable images; the three unreadable images are audio-only `[CD-Audio]` CHDs with no data track. The schema now carries `revision`, `discNumber`/`discTotal`, `bios`/`bios2`, `fixMode`, `inputDevices`, `nvram`, and `cheats`; `fixMode` drives `GameCompatibility`, `bios`/`bios2` drive `BiosLibrary`, and `inputDevices` drives `GameControlDefaults`. `gamedb scan|export|merge` provide the repeatable import/export workflow, and 10 new storage/host/CLI tests cover the parser, merge/save roundtrip, device spec, and commands. See `docs/GameDatabase.md`.

- [ ] **SYS-18 — Auto per-game profile completion**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-18
  **Remaining:** live profile application; known-title input seeding; cheat fields; full DB-driven population; profile import/export; tests.
  **Done when:** known problem titles boot with zero manual BIOS/region/fix/input steps and profile changes apply live.

- [ ] **CORE-13 — Remaining compatibility failures**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** CORE-13
  **Remaining:** Crash 'n Burn (Japan); any 12k/65k-frame black/static titles; BIOS-aware sweep status merge; title-specific fixes; regression tests and compatibility notes.
  **Done when:** 594/594 discs render, or every remaining exception has a root cause and documented workaround; `GameCompatibilityList.md` is current.

- [ ] **VID-04 — 480i/240p detection, deinterlacing, and field blending**
  **Status:** Pending · **Priority:** P0 · **Effort:** M · **Tracks:** VID-04
  **Remaining:** detect field/frame mode; bob/weave/blend options; per-game mode; screenshot and recording behavior; PAL/NTSC interaction; test patterns.
  **Done when:** 480i titles render with correct field handling; 240p output is unchanged; options are documented and tested.

- [ ] **AUD-01 — Audio output quality: resampler, exclusive mode, and sync offset**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** AUD-01
  **Remaining:** resampler quality options; exclusive-mode output; audio sync offset; device-change and underrun behavior; latency measurement; documentation.
  **Done when:** audio quality/latency targets are met on Windows/Linux/macOS; device switching is safe; no crackle or drift in extended runs.

- [ ] **ECO-04 — Linux/macOS platform validation and packaging**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** ECO-04
  **Remaining:** validate OpenGL/GLES on real Linux and macOS GPUs plus llvmpipe fallback; macOS runtime validation; macOS gamepad backend; Flatpak/AppImage packaging and desktop integration; platform smoke evidence.
  **Done when:** full suite + frontend smoke pass on Linux and macOS hardware; Flatpak/AppImage installs and runs; gamepad works on macOS.

### 3.2 Increase performance

- [ ] **CORE-05 — Optional threaded DSP**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** CORE-05
  **Remaining:** bit-identical DSP worker thread; deterministic synchronization with the CPU/audio clock; save-state/rewind compatibility; performance measurement; disabled by default.
  **Done when:** threaded and single-threaded runs produce identical audio/state hashes; a measured speed gain exists; fallback is automatic.
  **Ref:** `opera_lr_dsp.c`, `opera_lr_dsp_regular.ic`, `opera_lr_dsp_threaded.ic`.

- [ ] **ENG-02 — Interpreter hot-path optimization**
  **Status:** Pending · **Priority:** P1 · **Effort:** L · **Tracks:** ENG-02
  **Remaining:** interpreter dispatch/decode/memory fast paths; block cache and invalidation; allocation-free hot loops; flags/PC/cycle parity; benchmark evidence.
  **Done when:** hot-path improvements deliver a documented speedup with no accuracy or state change.

- [ ] **PERF-08 — Managed ARM JIT**
  **Status:** Pending · **Priority:** P2 · **Effort:** XL · **Tracks:** BAK-10
  **Remaining:** managed IL JIT behind `ICpuBackend`; block cache/invalidation; flags/PC/cycle parity; state compatibility; differential tests; tuned interpreter remains the accuracy oracle.
  **Done when:** JIT output is bit-identical to the interpreter on the test corpus, delivers at least a 1.5× speedup on hot titles, and falls back safely.

- [ ] **PERF-07 — Tier B 1080p60 budgets and profiling**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** BAK-06
  **Remaining:** profile integrated GPUs and older CPUs; 1080p60 per-filter budgets; p99 and allocation targets; reference-machine matrix; documented tuning guidance.
  **Done when:** Tier B desktop sustains 1080p60 with p99 under budget; allocation-free behavior is tracked by the zero-allocation item.

- [ ] **PERF-01 — Zero-allocation steady state on every real path**
  **Status:** Partial · **Priority:** P1 · **Effort:** M
  **Remaining:** eliminate the ~2.3 KB/frame Avalonia/compositor residual; remove CHD decode allocations from the core stage; prove zero allocations for uploads, readback, audio, and gallery/OSD paths.
  **Done when:** synthetic and representative real titles report 0 managed bytes/frame after warm-up; CI enforces the gate.

- [ ] **PERF-02 + BAK-08 — NativeAOT/trimming startup, size, and audio validation**
  **Status:** Partial · **Priority:** P2 · **Effort:** M · **Tracks:** BAK-08
  **Remaining:** frontend NativeAOT audio validation; startup-time and size targets; trimming compatibility; optional publish profiles; document which artifacts are AOT and which are framework-dependent.
  **Done when:** CLI and frontend AOT builds start, render, play audio, and pass smoke tests; startup/size are recorded.

- [ ] **VID-12 — Latency features: run-ahead, frame delay, VRR, audio sync**
  **Status:** Pending · **Priority:** P1 · **Effort:** L · **Tracks:** VID-12
  **Remaining:** 1–2 frame run-ahead; frame-delay slider; VRR/adaptive-sync presentation; audio sync adjustment; measured input-to-photon latency; per-game options.
  **Done when:** latency options work without desync or state corruption; measurements are documented; defaults remain safe.

- [ ] **PERF-04 — Host disc streaming and CHD decompression**
  **Status:** Partial · **Priority:** P2 · **Effort:** M
  **Remaining:** allocation-free CHD/ISO prefetch; decompression buffers; cache sizing; read-ahead tuning; avoid frame spikes while keeping the emulated CD model unchanged.
  **Done when:** disc streaming does not allocate in steady state or stall presentation; emulated CD timing is unaffected.
  **Note:** emulated CD read-ahead/subcode is tracked in the CD items above; this item is host-side only.

- [ ] **PERF-05 — High-resolution CEL performance**
  **Status:** Partial · **Priority:** P2 · **Effort:** M
  **Remaining:** optimize the existing opt-in high-res mode; VRAM plane handling; rasterizer cost; per-title fallback; target 60 fps on Tier A/B where possible.
  **Done when:** high-res mode meets its documented frame-time budget and degrades gracefully on slower hardware.

- [ ] **PERF-06 — GLES 3.1/ANGLE renderer performance**
  **Status:** Partial · **Priority:** P2 · **Effort:** M
  **Remaining:** GLES 3.1 feature path; ANGLE parity/performance; state-change reduction; shader/pipeline cache; mobile-desktop GPU validation.
  **Done when:** GLES 3.1 and ANGLE match the desktop GL output and meet 1080p60 budgets.

### 3.3 Optional features

- [ ] **UX-03 — Netplay**
  **Status:** Pending · **Priority:** P2 · **Effort:** XL · **Tracks:** UX-03
  **Remaining:** lobby/direct connect; lockstep or rollback; NAT/tunneling guidance; desync detection; state handoff; chat/ready flow; tests.
  **Done when:** two clients complete a 10-minute session with no unexplained desync; reconnect and state handoff work.

- [ ] **UX-05 — Achievements**
  **Status:** Pending · **Priority:** P2 · **Effort:** L · **Tracks:** UX-05
  **Remaining:** provider integration (RetroAchievements or equivalent); offline cache; unlock UI; per-game progress; privacy controls; tests.
  **Done when:** achievements unlock and persist across sessions; offline behavior is documented.

- [ ] **UX-06 — TAS tooling completion**
  **Status:** Partial · **Priority:** P2 · **Effort:** L · **Tracks:** UX-06
  **Remaining:** full TAS UI; input timeline; multiplayer display; lag detection; movie library; branch comparison; deterministic replay evidence.
  **Done when:** a complete TAS workflow (record, edit, branch, verify, share) works from the desktop and CLI.

- [ ] **UX-24 — Lua scripting and automation**
  **Status:** Pending · **Priority:** P3 · **Effort:** L · **Tracks:** UX-24
  **Remaining:** sandboxed Lua host; memory/input/state/screenshot APIs; per-game scripts; CLI hooks; examples and documentation.
  **Done when:** example scripts automate a test run and a gameplay sequence without destabilizing the core.

- [ ] **UX-07 — Controller profiles and input polish**
  **Status:** Partial · **Priority:** P2 · **Effort:** M · **Tracks:** UX-07
  **Remaining:** named profiles; deadzones/curves; hot-plug prompts; import/export; per-game profiles; conflict detection; tests.
  **Done when:** profiles round-trip, apply per game, and survive device changes.

- [ ] **UX-20 + FND-06 — Per-game settings UI completion**
  **Status:** Partial · **Priority:** P2 · **Effort:** M · **Tracks:** UX-20, FND-06
  **Remaining:** cheat/shader-chain fields; inherit/reset; search/filter; live application; localized labels; profile import/export from the UI.
  **Done when:** every shipped option is editable per game, with inherit/reset and immediate effect.

- [ ] **UX-21/22 — Debugger and profiler suite**
  **Status:** Pending · **Priority:** P2 · **Effort:** L · **Tracks:** UX-21, UX-22
  **Remaining:** CPU/memory/CEL debugger (DSP debugging is tracked separately in the audio section); breakpoints/watchpoints; step/run-to; trace viewer; frame/performance profiler; exportable traces.
  **Done when:** a developer can debug a stuck title and profile a frame without the CLI.

- [ ] **ECO-09 — 3DO CD player / Video CD**
  **Status:** Pending · **Priority:** P3 · **Effort:** M · **Tracks:** ECO-09
  **Remaining:** audio-CD player UI; track navigation; VCD/MPEG expansion research and playback if feasible; tests.
  **Done when:** a compatible disc plays with correct controls and documented limitations.

- [ ] **VID-07 — Shader preset system**
  **Status:** Pending · **Priority:** P2 · **Effort:** L · **Tracks:** VID-07
  **Remaining:** `.slang`/`.glslp`-style presets; hot reload; per-game presets; parameter UI; fallback on unsupported GPUs.
  **Done when:** popular presets load, render correctly, and fall back safely.

- [ ] **VID-11 + BAK-19 — Vulkan surfaces on Linux/macOS**
  **Status:** Pending · **Priority:** P2 · **Effort:** L · **Tracks:** VID-11, BAK-19
  **Remaining:** X11/Wayland surface; macOS Metal/MoltenVK path or compositor embedding; no CPU readback; validation.
  **Done when:** the optional Vulkan backend presents natively on Linux and macOS with parity and no readback.

- [ ] **VID-16 — VDLP output pixel format selection**
  **Status:** Pending · **Priority:** P3 · **Effort:** S
  **Remaining:** select 0RGB1555/RGB565/XRGB8888 output; document behavior; parity tests.
  **Done when:** the format option changes only host conversion, not emulated output, and is covered by tests.
  **Ref:** `libretro_core_options.c` (`opera_vdlp_pixel_format`).

- [ ] **VID-17 — Void-area patterns**
  **Status:** Pending · **Priority:** P3 · **Effort:** S
  **Remaining:** 4DO-style void patterns in addition to the solid color; per-game presets; preview.
  **Done when:** patterns render identically across presenters and screenshots.

- [ ] **AUD-03 — DSP debugger**
  **Status:** Pending · **Priority:** P3 · **Effort:** M · **Tracks:** AUD-03
  **Remaining:** DSP registers/memory; instruction trace; breakpoints; step/run-to; state inspection; integration with the debugger suite.
  **Done when:** a DSP program can be debugged interactively and traces export cleanly.

### 3.4 Future research features

- [ ] **VID-01 + BAK-11 — True 640x480/480i internal rendering**
  **Status:** Research · **Priority:** P1 · **Effort:** XL · **Tracks:** VID-01, BAK-11
  **Remaining:** measure the current line-doubled mode; prototype true vertical 640x480; scale CEL/VDLP/clipping; independent 480-line output; per-title fallback; performance and visual comparison.
  **Done when:** true vertical detail is visible on a 640x480 title and a 3D title, 60 fps is met on Tier A, and the default remains 320x240 + filter.
  **Ref:** `docs/HighResolutionCelResearch.md`.

- [ ] **VID-02 — 2x/4x supersampling**
  **Status:** Research · **Priority:** P2 · **Effort:** L · **Tracks:** VID-02
  **Remaining:** 2x/4x supersampling prototype; deterministic downsample; geometry/texture comparison; performance budget.
  **Done when:** supersampling improves edges without changing guest VRAM semantics, and a go/no-go decision is recorded.

- [ ] **VID-03 — GPU CEL compositing**
  **Status:** Research · **Priority:** P3 · **Effort:** XL · **Tracks:** VID-03
  **Remaining:** parity prototype for packed/literal CELs; GPU CEL architecture; VRAM semantics; fallback; feasibility note.
  **Done when:** a prototype matches the software renderer on representative CELs, with a recorded go/no-go decision.

- [ ] **VID-08 — NTSC composite/S-Video/RF, HDR, and gamma**
  **Status:** Research · **Priority:** P2 · **Effort:** L · **Tracks:** VID-08
  **Remaining:** composite/S-Video/RF signal models; CRT Royale-class presets; HDR output; gamma/color controls; visual validation.
  **Done when:** signal and HDR modes are validated against reference captures and meet performance budgets.

### 3.5 Testing

Testing items are cross-cutting suites and gates. Feature-specific test evidence stays in that feature's “Done when” line.

- [ ] **CORE-10 — Hardware test ROM suite**
  **Status:** Pending · **Priority:** P1 · **Effort:** L · **Tracks:** CORE-10
  **Remaining:** test ROMs for CPU, CLIO, MADAM, DSP, SPORT, VDLP, CD, input; golden outputs; CI integration; documentation.
  **Done when:** the suite runs in CI and catches timing/behavior regressions without a copyrighted BIOS.

- [ ] **CORE-11/16 + FND-05 — Full-collection differential and hash regression**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** CORE-11, CORE-16, FND-05
  **Remaining:** full 594-title differential run; PC-delta triage; state/CD transcript comparison; frame/audio hash coverage; nightly dashboard; allowlist and triage policy.
  **Done when:** the nightly run has no unexplained divergence, and every allowance is documented and dated.

- [ ] **AUD-04 — Audio golden tests vs Opera**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** AUD-04
  **Remaining:** WAV corpus; spectral/RMS/envelope comparison; DSP mode changes; CD-DA mixes; hardware reference captures.
  **Done when:** the corpus runs in CI with documented tolerances and catches regressions.

- [ ] **VID-13 — Video test suite and color accuracy**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** VID-13
  **Remaining:** test patterns; RGB555/CLUT/NTSC behavior; CPU/GPU parity; high-res comparison; color delta report.
  **Done when:** every presenter/filter passes the suite within documented color tolerances.

- [ ] **TEST-01 — Save-state migration matrix**
  **Status:** Pending · **Priority:** P1 · **Effort:** M
  **Remaining:** test every version bump; v1–v3 states; option/region/disc-hash changes; warn/reject policies; 4DO/Opera interop once save-state interoperability lands.
  **Done when:** every supported state version loads or fails with a typed, tested error.

- [ ] **ENG-01 + ENG-09 — CI quality gates**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** ENG-01, ENG-09
  **Remaining:** full OS/arch matrix; coverage gate; static analysis; benchmark regression; SBOM; reproducible builds; six-bundle smoke; artifact checksums.
  **Done when:** every release candidate passes all gates and publishes signed, reproducible evidence.

- [ ] **TEST-02 + BAK-09 — ViewModel and UI tests**
  **Status:** Pending · **Priority:** P2 · **Effort:** M · **Tracks:** BAK-09
  **Remaining:** settings, library, dialogs, hotkeys, gallery, playlist, and profile ViewModel tests; headless UI smoke; accessibility checks.
  **Done when:** UI regressions are caught without manual clicking.

- [ ] **TEST-04 — GPU and platform CI matrix**
  **Status:** Pending · **Priority:** P1 · **Effort:** L
  **Remaining:** GL/GLES/Vulkan/D3D11 coverage; Windows/Linux/macOS; x64/arm64; software fallback; GPU probe artifacts.
  **Done when:** each backend is exercised in CI or a documented hardware lab, with parity artifacts.

- [ ] **TEST-05 — Performance regression matrix**
  **Status:** Pending · **Priority:** P1 · **Effort:** M
  **Remaining:** Tier A/B and arm64 baselines; filters; high-res; threaded DSP/JIT; relative >10% gate; published trend.
  **Done when:** regressions fail the gate and the trend is visible to maintainers.

- [ ] **TEST-06 — In-game/visual and high-resolution compatibility review**
  **Status:** Pending · **Priority:** P1 · **Effort:** L
  **Remaining:** inspect final frames for every rendered disc; rerun black/static titles at 12,000+ frames; record Unplayable/Playable/Good; repeat with high-res; feed results into the compatibility and high-resolution items.
  **Done when:** every local disc has a reviewed status and the compatibility matrix distinguishes loadable, playable, and good.

--

## 4. Suggested execution order

This is an ID-only sequence; descriptions live in the canonical entries above.

1. **Foundation:** TEST-01, CORE-10.
2. **Accuracy:** CORE-01, CORE-02, CORE-03, CORE-08, SYS-08, CORE-04, CORE-06, CORE-07.
3. **Disc and platform:** SYS-01, SYS-02, SYS-03, SYS-05, SYS-06, SYS-07, ECO-04.
4. **Opera parity and “just works”:** SYS-12, SYS-15, SYS-16, SYS-17, SYS-18, CORE-13.
5. **Visual/audio correctness:** VID-04, AUD-01, CORE-11/16 + FND-05, AUD-04, VID-13.
6. **Performance:** CORE-05, ENG-02, PERF-08, PERF-07, PERF-01, TEST-05.
7. **Product polish:** UX-03..UX-25, ECO-06..ECO-11, VID-07..VID-17, AUD-02/03, SYS-19/21/22, ENG-05.
8. **Research:** VID-01/02/03, VID-08, ECO-08, RES-01/02.

**Rule:** do not start step 7 or 8 while a step 2–5 P0 item is open, unless the work is independent and explicitly marked as such.

---

## 5. “Best 3DO emulator” gates

These are the release/quality gates for the goal, not a second backlog. They reference canonical IDs only.

| Gate | Required evidence | Required IDs |
|---|---|---|
| Compatibility | 594/594 discs load and render; ≥95% Playable/Good; every exception documented | CORE-13, TEST-06, SYS-17, SYS-18 |
| Accuracy | No unexplained differential divergence; ARM/CLIO/CEL/CD/SPORT/FIQ parity | CORE-01, CORE-02, CORE-03, CORE-08, SYS-08, CORE-04, CORE-06, CORE-07, CORE-11/16 + FND-05 |
| Opera feature parity | All core options implemented or explicitly scoped | SYS-05, SYS-06, SYS-07, SYS-12, SYS-15, SYS-16, VID-16, CORE-05 |
| Disc/peripherals | CD-DA, multi-track, ODE, PAL, mouse, lightgun, flightstick, trackball, six-player | SYS-01, SYS-02, SYS-03, SYS-15, SYS-16, CORE-04 |
| Performance | 4K60 Tier A; 1080p60 Tier B; zero steady-state allocations; arm64 parity | PERF-07, PERF-01, PERF-03, PERF-06, TEST-05 |
| Platforms | Windows/Linux/macOS x64/arm64 validated; installers/signing | ECO-04, UX-16, PERF-03 |
| Trust | 2,000+ tests; nightly differential; coverage/benchmark gates; fuzzing; SBOM; 24h soak | CORE-10, CORE-11/16 + FND-05, AUD-04, VID-13, ENG-06, ENG-01 + ENG-09, TEST-03 |
| Product | Rewind, fast-forward, cheats, netplay, achievements, TAS, shaders, recording, debugger, localization | UX-03, UX-05, UX-06, UX-24, VID-07, VID-09, UX-21/22, UX-09 + FND-07 + ENG-08 |
| Release | Signed, reproducible bundles; auto-update; docs/compatibility portal current | UX-14, UX-16 + ENG-10, ECO-10 + ENG-07, ECO-11 |

---

## 6. Reference strategy

### 6.1 Primary and secondary references

| Reference | Role | Use it for |
|---|---|---|
| `References/opera-libretro-master` | **Primary behavioral reference** | Core options, CD-ROM, DSP, peripherals, SWI HLE, boot ROM, accuracy fixes |
| `References/4DO-master` | Historical port baseline | 4DO behavior, fixes, game DB, UI features |
| `References/fourdo-code-r389-trunk` | Historical 4DO snapshot | Older behavior and regression comparisons |
| `References/FourDO Ported` | Porting notes/code | 4DO-to-C# mapping and conventions |
| `References/freedo-master`, `References/freedo-master (1)` | Original core reference | FreeDO quirks, ARM/CLIO/MADAM/DSP baseline |
| `References/CPP_SimpleGui` | Opera test harness | Differential runs and reference captures |
| `References/Docs`, `References/Docs2`, `References/Docs3` | Hardware/SDK documentation | ARM60, 3DO, CD-ROM, VDLP, and Portfolio details |
| `References/RMG-master` | Modern emulator UX benchmark | Library, netplay lobby, controller profiles, packaging |
| `References/rpcs3-0.0.43` | Modern emulator product benchmark | Per-game config, debugger, compatibility list, release polish |

### 6.2 Rules

1. **Port behavior, not code.** 5DO is a clean C# implementation; do not copy C/C++ source.
2. **One reference per change.** Record the source in the commit/PR and in `docs/PortingNotes.md` if the behavior diverges.
3. **Tests first.** Add the failing test or differential fixture before the fix.
4. **Preserve determinism.** No wall clock, unordered iteration, or environment-dependent seeds in emulated state.
5. **Keep the core single-threaded by default.** Threaded DSP/JIT must be opt-in and bit-identical.
6. **Do not ship copyrighted BIOS/game content.** Embedded boot ROM work needs legal review.
7. **Document intentional divergence** in `docs/PortingNotes.md` and `docs/Compatibility.md`.

---

## 7. Verification commands

Run these before marking work done.

```powershell
# Build and test
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore

# Static analysis
jb inspectcode 5DO.sln -o=inspectcode.sarif -f=Sarif --severity=WARNING --no-build

# Compatibility sweep
pwsh -NoProfile -File tools/compat-sweep.ps1 -Roots "J:\Panasonic 3DO","G:\MAME\MAME Software List CHDs\3do" -Frames 3000 -Resume

# Differential smoke and showcase
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/smoke.json
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/showcase.json

# Benchmark gate
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- --frames 120 --label ci --json --gate

# GPU probes (Windows)
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --gl-probe --gl-probe-backend wgl --gl-probe-capture
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --d3d-probe artifacts/d3d-probe/parity.json --d3d-probe-size 320x240 --d3d-probe-frames 20 --d3d-probe-warmup 5 --d3d-probe-filter accurate --d3d-probe-capture artifacts/d3d-probe/parity.png
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --vk-probe

# Frontend smoke
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --smoke-test
```

**Linux validation:** use the local Hyper-V VM `LinuxMint` (`D:\Hyper-V\VMs\LinuxMint`). Read `AGENTS.local.md` for credentials. Publish `linux-x64` on the host and copy the output into the guest, or install the .NET SDK in the guest. Record evidence in `docs/Compatibility.md` §10.3 and `docs/KnownIssues.md`.

**Release:** run the commands in [`ReleaseChecklist.md`](ReleaseChecklist.md); build bundles with `packaging/package-bundle.ps1`; keep a local copy in `src/FiveDo.Frontend.Avalonia/bin/Release`; remove PDB/XML files; publish `SHA256SUMS.txt`.

---

## 8. Maintenance rules

1. **This file is the single source of truth.** Do not recreate separate plan, backlog, handoff, or roadmap files.
2. **One canonical entry per feature.** Do not add a second checklist for the same work. Reference IDs instead.
3. **Retire duplicates.** `BAK-*` and `MOON-*` are aliases only; see Appendix B. Do not reintroduce them.
4. **Check the box only when “Done when” is satisfied.** Include behavior, tests, docs, and no regressions.
5. **Move completed items to the completed log.** Do not leave them in the missing-feature category.
6. **One feature per pull request.** No mixed accuracy/rendering/UX changes.
7. **No default timing change without full-collection evidence.**
8. **Keep evidence current.** Update `TestCorpus.md`, `Compatibility.md`, `PortingNotes.md`, and `KnownIssues.md` when behavior changes.
9. **Refresh the snapshot monthly** or after every release: version, test count, compatibility counts, performance numbers, and platform validation status.

---

## 9. Completed log

Append new entries here; do not delete history. Each entry is a completed canonical item or a release milestone.

- **2026-11-04 - SYS-07 memory capacity options.** Added the eight Opera DRAM/VRAM layouts (`21`, `22`, `41`, `42`, `81`, `82`, `E2`, `F1`) through `MemoryConfiguration`/`MemoryLayout`, dynamic RAM/VRAM decode, VRAM base and masks for VDLP/SPORT/CEL, capacity-derived MADAM RED SYSBITS, `--memory`/Settings/profile/state plumbing, memory state section v3, Options section v8, a homebrew probe ROM that uses 4 MB DRAM + 2 MB VRAM, and 34 memory tests. Stock hashes are unchanged; expanded memory is documented as homebrew-only.
- **2026-11-04 - SYS-08 clock matrix increment.** `bench --cpu-clock`/`--memory` measure the supported clock range; the 0.5x-4x matrix and unchanged stock default are recorded in `Performance.md` §8.1.
- **2026-11-05 - SYS-17 game database expansion.** Grew `GameDatabase.xml` from 518 to 650 records, added `revision`, `discNumber`/`discTotal`, `bios`/`bios2`, `fixMode`, `inputDevices`, `nvram`, and `cheats`, and wired `fixMode` into `GameCompatibility`, BIOS fields into `BiosLibrary`, and input fields into `GameControlDefaults`. Added `gamedb stats|scan|export|merge`, the filename parser, serial retry for transient CHD read failures, and `docs/GameDatabase.md`. `gamedb stats --folder "J:\Panasonic 3DO"` reports 1,363 readable images, 650 unique IDs, and 100% metadata coverage of readable images; the three unreadable images are audio-only `[CD-Audio]` CHDs.
- **2026-11-05 - SYS-08 CPU clock validation.** Recorded the five-title 0.5x/1x/2x/4x A/B in `Performance.md` §8.3. Every non-stock clock changed the frame hash, and AutoBahn Tokio's wall time rose from 9.1 s at 1x to 84.4 s at 2x and 271.7 s at 4x. The stock 12.5 MHz default and the no-per-title-clock decision are unchanged.
- **2026-11-04 - SYS-12 Portfolio OS SWI HLE.** Ported Opera's 14 fixed-point math SWIs (`0x50000`-`0x50012`, with the object/Dot4 cases left to the guest), added `--swi-hle`/Settings/profile/state v9 plumbing, `--swi-comments` histograms, 33 CPU/machine tests that match `opera_fixedpoint_math.c`, and a real-BIOS synthetic 4x4 benchmark. Default-off hashes are unchanged; HLE is an opt-in performance mode and intentionally skips the guest handler's cycles and register clobbers, so some titles (AutoBahn Tokio, Ballz) change frame hashes.
- **2026-11-04 - FND-01 machine option registry.** Added `MachineOptionRegistry` (13 validated options with CLI/Settings/profile/state mappings), typed `MachineOptionValidationException`/`UnknownMachineOptionException`, Opera DIPIR and soft-reset controls, `--opera-dipir`/`--opera-soft-reset`/`--cpu-backend`, `settings options` generated documentation, `docs/MachineOptions.md`, save-state Options v7 coverage for every option, mismatch warnings, and CLI/Settings/profile parity tests.
- **2026-11-04 - Plan rewrite.** Consolidated the former dashboard, parts I-IV, feature spec, parity audit, and handoff into this single backlog; retired duplicate `BAK-*`/`MOON-*` trackers.
- **2026-11-03 — v1.2.1.** Six x64/arm64 bundles plus `SHA256SUMS.txt`; CI verified every apphost machine type and every checksum.
- **2026-11-03 — v1.2.0.** Direct3D 11 presenter, startup release checks, deterministic fuzz harness, per-edge overscan/void border, screenshot gallery filters/compare/export, display-refresh control, flat CHD/raw ISO detection, FIQ/DSP telemetry, CLIO/DIPIR/FIFO/MCTL/XBUS audit fixes.
- **2026-11-02 — Hardening pass.** Fuzz harness, overscan profiles, gallery features, FIQ request-to-vector latency, DSP per-tick statistics, 1,622 tests.
- **2026-11-01 — Disc/UX/accuracy pass.** CD-DA and multi-track, M3U/live swap, PAL/NTSC, BIOS manager, per-game NVRAM, rewind, fast-forward, cheats, TAS, disc browser, input devices, OSD/quick menu, library metadata, portable mode, instance lock, save-state format v3.
- **Earlier:** v1.0.0 documented-compatibility release; 594-disc sweep; legal/attribution sign-off; six-RID packaging; OpenGL/GLES, Vulkan, and Direct3D 11 presenters; 4K output; differential harness.

---

## Appendix A — Opera option coverage

This is a crosswalk, not a second backlog. “Canonical item” is the only place the remaining work is tracked.

| Opera feature/option | 5DO status | Canonical item |
|---|---|---|
| BIOS (rom1), including embedded Opera Boot | Partial | SYS-05, SYS-06 |
| Font/Kanji ROM (rom2) | Shipped | — |
| CPU overclock 0.5×–4× | Partial | SYS-08 |
| Memory capacity 2/4/8/14/15 MB DRAM, 1/2 MB VRAM | Shipped (SYS-07) | — |
| Random-number seed | Shipped | — |
| CD speed unbounded/1×–32× | Shipped | — |
| Region NTSC/PAL1/PAL2 | Partial | SYS-03 |
| VDLP pixel format 0RGB1555/RGB565/XRGB8888 | Missing | VID-16 |
| VDLP bypass CLUT | Shipped | — |
| HiRes CEL rendering | Partial (line-doubled) | VID-01 |
| MADAM matrix hardware/software | Shipped | — |
| OperaOS SWI HLE | Partial (opt-in) | SYS-12 |
| Threaded DSP | Missing | CORE-05 |
| NVRAM storage per-game/shared | Shipped | — |
| NVRAM version 0–9 | Shipped | — |
| Active input devices 0–8 | Missing | SYS-15 |
| Hide lightgun crosshairs | Missing | SYS-15 |
| Debug output (KPRINT) | Shipped | — |
| Joypad/flightstick/mouse/lightgun/arcade lightgun/trackball | Partial | SYS-15 |
| Up to eight input ports | Partial | SYS-15 |
| Cycle-based CD timing, read-ahead, DIPIR, tagged status, chunks, subcode | Partial | CORE-04 |
| CD-DA/Red Book, ODE, multi-track TOC | Partial | SYS-01, SYS-02 |
| ARM N=2 cycle model | Partial | CORE-01 |
| CEL busy pacing | Partial | CORE-03 |
| Per-timer CLIO scheduling | Partial | CORE-02 |
| Deterministic PRNG | Shipped | — |
| Versioned state chunks | Shipped | — |
| Headless test harness | Shipped | — |

**Additional 4DO/Opera UX gaps already covered elsewhere:** physical CD drive (SYS-21), disc conversion (SYS-22), void patterns (VID-17), auto-load/auto-open (shipped), localization (UX-09), installers/file associations (UX-16), session focus behavior (UX-25).

---

## Appendix B — Retired and duplicate IDs

| Retired ID | Canonical replacement |
|---|---|
| BAK-01 | TEST-07 |
| BAK-02 | TEST-07 |
| BAK-03 | TEST-07 |
| BAK-04 | CORE-01, CORE-02, CORE-03, CORE-08, SYS-08, CORE-04 |
| BAK-05 | ECO-04, PERF-03, TEST-04 |
| BAK-06 | PERF-07 |
| BAK-07 | UX-16 + ENG-10 |
| BAK-08 | PERF-02 |
| BAK-09 | TEST-02 |
| BAK-10 | PERF-08 |
| BAK-11 | VID-01 |
| BAK-12 | Completed 2026-11-02 |
| BAK-13 | CORE-11/16 + FND-05, CORE-13 |
| BAK-14 | SYS-19 |
| BAK-15 | Retired; covered by SYS-01/03/15 and UX-01/03/21 |
| BAK-16 | UX-16 + ENG-10 |
| BAK-17 | Completed (UX-15) |
| BAK-18 | ECO-04 |
| BAK-19 | VID-11 |
| MOON-01 | SYS-05, SYS-06 |
| MOON-02 | SYS-17, SYS-18 |
| MOON-03 | CORE-11/16 + FND-05 |
| MOON-04 | UX-08 |
| MOON-05 | UX-06, UX-21/22, UX-24 |
| MOON-06 | ECO-04, PERF-03 |
| MOON-07 | SYS-19, SYS-20, SYS-22 |
| MOON-08 | VID-01/02/03, VID-07/08 |
| MOON-09 | PERF-07, ENG-02, PERF-08 |
| MOON-10 | SYS-17, SYS-18, ECO-10 + ENG-07, ECO-11, UX-09 |

**Dropped scope (do not add checkboxes):** WebAssembly/browser (ECO-02), Android/iOS (ECO-03), Raspberry Pi/arm64 GLES optimization (ECO-05), Steam Deck. The libretro core wrapper is not planned; 5DO stays standalone.

---

## Appendix C — Source-of-truth links

- [Status](Status.md) — short current answer.
- [Compatibility](Compatibility.md) — supported set and platform evidence.
- [Known issues](KnownIssues.md) — current limitations.
- [Performance](Performance.md) — budgets and measurements.
- [Test strategy](TestStrategy.md) and [Test corpus](TestCorpus.md) — test evidence.
- [Differential harness](DifferentialHarness.md) — Opera comparison.
- [High-resolution CEL research](HighResolutionCelResearch.md) — VID-01/02/03.
- [Release checklist](ReleaseChecklist.md) — release gate.
- [Porting notes](PortingNotes.md) — intentional divergences.
- [Reference strategy](ReferenceStrategy.md) — Opera/4DO mapping.
