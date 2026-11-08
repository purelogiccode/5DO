<a id="master-dashboard"></a>
# 5DO — Implementation Plan and Missing-Feature Backlog

**Snapshot:** 2026-11-08 (1.5.0 release)
**Current release:** `1.5.0`
**Repository:** <https://github.com/purelogiccode/5DO><br>
**Branch:** `master`<br>
**Status:** **2,043 tests pass**; `dotnet build` reports **0 warnings / 0 errors**; `dotnet format --verify-no-changes` is clean; `jb inspectcode --severity=WARNING` reports **zero** warnings or errors. **594 discs load, 593 render, 12 visually validated**; the 50-title oracle panel captures **50/50** titles, **all 50 match every frame hash** at frames 60/120/300, and **all 50 match the cumulative audio hash at those checkpoints** (150/150 audio fields). Scripted input is wired and validated on a five-title, 900-frame panel: **5/5 frame hashes at 60/300/600**, and the generic menu script demonstrably changes guest output at 600/900. The frame-boundary phase is resolved: the harness now samples Opera's ARM/CD state in the checkpoint's own field, and 5DO ends a field on the 263-line scanline boundary like the reference, leaving only **11-20 retired instructions** at frame 60 and **−46..2,195** at frame 120 on the 50-title panel. The CD count now matches on **50/50 titles at f=60 and f=120**; the first behavioral divergence moved to **11 titles at f=300** (8 count differences and 3 one-command sequence differences), with **39/50 titles phase-only**; Daedalus Encounter differs only by a same-frame PC phase at f=300. The timer-14 period is now measured equal on both engines (12,384/12,396 cycles); the Lemmings READ_ID wait residual is guest copy/decrypt work, not CLIO scheduling. The audio-gated confirmation panel `test93-panel50-audio` keeps **50/50 frame hashes, 0 errors**, matches **150/150 audio fields**, and leaves **39 phase-only** titles plus **11 CD-only behavioral divergences** at f=300; Dragon's Lair is 110/108. The top-100 playability audit is the next gate.<br>
**Goal:** beat 4DO and Opera-libretro on **measured in-game compatibility and accuracy**, and remain the best standalone desktop 3DO emulator on Windows, Linux, and macOS.<br>
**Plan shape:** four phases, **measurement first**. Product and research work is deferred until the compatibility gate is green.

> **2026-11-06 rewrite.** This revision keeps every stable ID, every completed-log entry, and the five anchors other documents link to. It changes the order of work: truth first, disc and timing second, playability proof third, product and research last. `TEST-07` is the only new ID. `TEST-06` is promoted to P0. `SYS-05` is blocked on licensing rather than engineering. `PERF-08` and `CORE-05` are **closed with measured negative evidence**. The six completed items that were still sitting in the backlog moved to §2.5.

> **This is the single source of truth.** It replaces the former dashboard, milestone log, feature spec, parity audit, and session handoff. Historical evidence stays in the linked docs; this file is the canonical backlog.

---

## 1. How to use this plan

1. **Work the phases in order.** Do not start Phase 2 before Phase 0 has produced a trustworthy oracle. Do not start product or research work while any Phase 0-3 P0 item is open.
2. **One feature per change.** Do not mix core accuracy, rendering, and UX work in one pull request.
3. **Tests first.** Every behavior change needs a unit, golden, differential, or scripted manual test.
4. **One reference per change.** State whether the behavior comes from Opera, 4DO/FreeDO, hardware documentation, or a 5DO-specific decision. Port behavior, not code.
5. **Evidence before checkbox.** Mark an item done only when its "Done when" line is satisfied and the evidence is committed or linked.
6. **A frame-hash match is not accuracy evidence.** Record PC, state, CD, and audio evidence, or state explicitly which fields were unavailable.
7. **No duplicate tracking.** Each feature has one canonical entry below. IDs are stable. `BAK-*` and `MOON-*` are retired aliases (see Appendix B); do not recreate them.
8. **No default timing change without panel evidence.** A 40-title, 300-frame sample is a smoke test, not a gate.

### Legend

- `[ ]` — remaining work. `[x]` is used only in the completed log.
- **Pending** — not started, or research/design only.
- **Partial** — a usable subset ships; the remaining scope is listed in the item.
- **Conditional** — work only if the measurement panel proves it changes a game-visible outcome.
- **Blocked** — depends on another item, legal review, or hardware.
- **Closed** — a recorded decision not to pursue the work; it is not "done".
- **Priority** — **P0** correctness/compatibility, **P1** expected best-in-class evidence, **P2** ecosystem multiplier, **P3** long-term.
- **Effort** — **S** under 3 developer-days, **M** 3-10, **L** 10-30, **XL** 30-90+.

---

## 2. Current state

### 2.1 Verified snapshot (2026-11-08)

| Gate | Result |
|---|---|
| Build | `dotnet build 5DO.sln -c Debug` - **0 warnings, 0 errors** |
| Tests | **2,043 / 2,043 passed** in Debug |
| Format | `dotnet format --verify-no-changes` - clean |
| CLI | `5DO 1.5.0 (FiveDo)`; `selftest --json` - all checks `ok: true` |
| Synthetic benchmark | 240 frames in 0.816 s — **294 fps (~3.4 ms/frame)** |
| Real-title benchmark | Road Rash race **4.20 ms/frame**, p99 **7.40 ms** (`Performance.md` §8.5) |
| Reference oracle | `OperaCore.dll` and the optional debug core are built and staged |
| 40-title differential | **18 matched / 22 diverged** at frame 300 (`artifacts/diff/sample40-run/report.json`) |
| 5-title TEST-07 panel | Deterministic captures; first divergence **PC at frame 60**; CD commands 9/9 at f=60; samples 44,144/44,144 with the exact clock |
| Scripted-input TEST-07 panel | 5 titles, 900 frames, generic menu script; **5/5 frame hashes at 60/300/600**; input changes guest output at 600/900; no-input control confirms the matches are not from an ignored script |
| 3,000-frame TEST-07 panel | Lemmings, Alone, Space Ace, and Wolfenstein match the frame hash at 3,000; Road Rash still diverges at 1,000/3,000 |
| CD command sequence | First divergence localized to READ_DATA (`0x10`) vs READ_ID (`0x83`): Lemmings 105, Road Rash 559, Wolfenstein 389 |
| 50-title panel | **50/50 captured, 0 errors**; **50/50 match every frame hash** at 60/120/300; **150/150 cumulative audio hashes match** at those checkpoints; **50/50 first PC/CPSR divergences are same-frame phase**; only CD command cadence differs (11 titles at f=300) |
| 5-title showcase | **5/5 video matches** at 300 frames; **4/5** at 3,000 (Road Rash still diverges); PC is phase-aligned at f=60 on all five |
| Compatibility sweep | **594 load, 593 render, 1 known boot failure** (Crash 'n Burn Japan) |
| Visual/input validation | **12 titles** in the curated matrix; a handful have scripted gameplay |

The real-title and sweep numbers come from the local corpus and the linked docs; the `J:` corpus is not mounted in every session. Re-run the commands in §7 before quoting them.

### 2.2 What "better than 4DO and Opera" means

"Better" is not a feature list. It is measured on the **same BIOS, same disc images, and same input scripts**:

| Claim | Evidence required |
|---|---|
| Better compatibility | Top 100 titles: **100% reach gameplay**, **≥95% Good or Playable**, every exception with a root cause and workaround. |
| Better accuracy | On the 50-title panel: **no divergence before gameplay**; after gameplay, PC/CPSR/state/CD/audio deltas are triaged and documented. |
| Fewer regressions | **Zero regressions** against 4DO and Opera on a curated known-issues list; every 4DO/Opera failure is fixed or explained. |
| Better disc support | CD-DA, multi-track, multi-disc, subcode/read-ahead, PAL, and ODE behavior verified on real titles. |
| Better input | Mouse, lightgun, arcade lightgun, flightstick, trackball, and six-player titles verified. |
| Better product | Save states/rewind, CHD, library, per-game profiles, 4K output, six-platform packaging. |

A **Good** title boots, reaches gameplay, survives 10 minutes of scripted input without a hang or crash, and has no visible or audio defect a player would notice. **Playable** means it runs but has a documented defect. **Unplayable** means it does not reach or sustain gameplay.

### 2.3 Shipped baseline

These are done and must not regress. They are listed here once; the backlog contains only missing work.

| Area | Shipped capabilities |
|---|---|
| Core | ARM60 interpreter; CLIO timers, FIFO, DMA, interrupts; MADAM CEL/pixel/matrix; VDLP; SPORT; DSP; XBUS; CD-ROM; NVRAM; Quartz state v4; save states v3; rewind buffer; fast-forward; cheats; TAS movie container/record/replay/verify/branch; six-port PBUS input |
| Disc | ISO/BIN/CUE/CHD, parent/child CHD, multi-track TOC, CD-DA mixing and volume, M3U/M3U8 playlist, live/F3/UI/CLI swap, guest inject, Opera FS parser, prefetch cache, Redump/No-Intro DAT verification, library health badges |
| Video | OpenGL/GLES two-stage presenter; optional Vulkan offscreen and Win32 surface; optional Direct3D 11 Windows presenter; 4K output; nearest/accurate/sharp-bilinear/CRT/xBR/HQx/FSR (HQx is a GPU rule-table pass); per-edge overscan; void border/color; opt-in line-doubled high-res; 480i/240p detection with bob/weave/blend field handling; presentation frame delay; VRR/adaptive-sync compositor presentation; screenshots; gallery filters/compare/export/sidecars; GIF recording |
| Audio | Exact 44.1 kHz clock; OpenAL Soft; WAV capture; `audio analyze`; CD-DA mix; volume/mute; audio-driven pacing; −250…250 ms audio sync offset; queue-latency/underrun counters; AOT null-device validation |
| Frontend | Library scan/dedupe; local and online cover art; favorites/playtime/history; offline metadata editor; settings; keyboard/gamepad bindings; ten save slots with thumbnails; screenshot gallery; log viewer; disc browser/info; BIOS/NVRAM manager; OSD/quick menu; input display; playlist editor; portable mode; instance lock; diagnostics bundle; startup update check; startup auto-open of the last BIOS/disc; 0-2 frame run-ahead; 0-8 frame presentation delay; VRR option; software input-latency overlay |
| CLI | `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks/verify/playlist`, `library verify`, `covers update/download/match/verify/cache`, `fs`, `bios`, `nvram`, `cheat`, `movie`, `settings`, `audio`, `state`, `disasm`, `update` |
| Release | v1.5.0 six bundles; versioned archives; checksums; legal/attribution sign-off; OpenAL LGPL obligations |

**Scope decisions (unchanged):** WebAssembly/browser, Android/iOS, Raspberry Pi, and Steam Deck are **out of scope**. Desktop Windows, Linux, and macOS (x64/arm64) remain the supported targets. 4K *internal* CEL rendering is research, not a release blocker; 4K *output* upscaling is supported. Distribution remains free, open-source **donationware** under the FreeDO modified GNU LGPL (non-commercial; voluntary donations only).

### 2.4 Evidence gaps that block the claim

1. **The oracle is video- and audio-clean but not finished.** The 50-title panel captures 50/50 with 0 errors, **all 50 match every frame hash and the cumulative audio hash** at frames 60/120/300 (150/150 audio fields), and **all 50 first PC/CPSR divergences are same-frame instruction phase** (11-20 retired instructions at f=60). The transcript classifier shows that **39/50 command streams are identical, 8 are prefix-only, and 3 differ by one READ_ID poll placement**, and every significant cadence step has the same command-issue PC on both sides. Scripted input is wired through both engines. What remains is the wait-loop phase itself (CD readiness vs timer/frame timing) and per-title gameplay input for TEST-06.
2. **"Renders" is not "plays".** The 594-disc sweep only proves a non-black final frame. Only 12 titles have visual/input validation. The plan's own gate asks for ≥95% Good/Playable across 594 titles; that audit has not been done.
3. **Matching video now implies matching audio and execution at the panel checkpoints.** All 50 panel titles match video and the cumulative audio hash at 60/120/300, and every PC/CPSR difference instruction-aligns to the previous reference frame. At 3,000 frames 4/5 sample titles match video; Road Rash still diverges, and the CD command count remains the first behavioral difference (Lemmings 365/352, Road Rash 851/836). The audio probe measures the reference's 1.214 ppm field-clock quantization: sample counts match through 1,000 frames and drift by +1/+2 at 2,000/3,000, so long-run audio hashes are labeled as an alignment state rather than a waveform difference. The new transcript classifier shows the command stream itself matches on 47/50 titles (39 identical, 8 prefix-only); the three exceptions shift a READ_ID poll by one command.
4. **Evidence is thin where it matters.** The 40-title sample stops at 5 seconds of emulated time. Real divergence appears in menus, FMV, streaming, and gameplay.

**Honest current claim:** 5DO is a better *product* than 4DO today, and a credible challenger to Opera, but it is **not yet a proven accuracy win**. The next two quarters should be spent closing that gap.

### 2.5 Completed IDs (traceability only)

| ID | What shipped | Evidence |
|---|---|---|
| TEST-07 | Reference oracle: fixed-seed deterministic captures, PC/CPSR/instructions, CD cycle/PC/instruction stamps, transcript and cadence classification, timer/read/register traces, instruction-phase triage, audio hash gating with sample alignment, scripted input, and the nightly baseline gate | `DifferentialHarness.md` §4.1/§4.2/§6/§8; `TestCorpus.md` 1.1bd; `test93-panel50-audio`, `test94-samples5-input`; 2,043 tests. Log 2026-11-08. |
| SYS-06 | RSA/unsigned-disc option; two-site in-memory patch; no BIOS or game content bundled | `BiosRsaPatch`; known No-RSA MD5s reproduced; signed-title hashes unchanged. Log 2026-11-06. |
| SYS-08 | CPU clock matrix 0.5x-4x; stock 12.5 MHz default unchanged; non-stock clocks are a compatibility variable | `Performance.md` §8.1/§8.3; five-title A/B. Log 2026-11-05. |
| SYS-12 | Portfolio OS SWI HLE (14 SWIs, opt-in); per-comment histograms | `ArmSwiHleTests`; 847,860 LLE steps vs 24,578 HLE; default-off hashes unchanged. Log 2026-11-04. |
| SYS-17 | Game database 653 records; 1,366 images; 100% metadata coverage; `gamedb scan/export/merge` | `GameDatabase.md`; 12 storage/host/CLI tests. Log 2026-11-05. |
| SYS-18 | Auto per-game profile; BIOS/region/fix/input applied with zero manual steps | Five-title evidence (WorldCup, Powers Kingdom, Slayer, Crash 'n Burn, Crime Patrol). Log 2026-11-05. |
| UX-26 | Online cover catalog: 473 unique WebP covers, 645/653 game IDs mapped, catalog + on-demand + 27 MB ZIP, annotation-stripped Jaro-Winkler fallback | `CoverArt.md`; 14 host tests; packager catalog reload/hash verification. Log 2026-11-07. |
| VID-04 | 480i/240p detection; bob/weave/blend; interlace-aware geometry; screenshots/GIF | `InterlaceProcessor`; 16 tests; five-title bob-vs-auto hashes. Log 2026-11-06. |

**Full completed-ID index (including the eight above):** FND-01, FND-02, FND-03, CORE-09, CORE-12, CORE-14, CORE-15, SYS-04, SYS-06, SYS-07, SYS-08, SYS-09, SYS-10, SYS-11, SYS-12, SYS-13, SYS-14, VID-04, VID-05, VID-06, VID-14, VID-15, AUD-05, TEST-07, UX-01, UX-02, UX-04, UX-11, UX-12, UX-15, UX-19, UX-23, UX-26, ENG-03, PERF-02, VID-12, BAK-12, BAK-17.

**Milestone aliases:** M9R/M9P → P3/P6/TEST-04; M10 → PERF-02; M12 → PERF-08; M13.5/M13.6 → TEST-06; M14 → VID-01/02/03; M15 → CORE-11/16 + FND-05.

---

## 3. Missing features — canonical backlog

Each entry is a single feature or a tightly-scoped bundle. The `Tracks` field preserves legacy IDs. **No feature appears in more than one category.** A few legacy aliases (`BAK-04`, `BAK-05`, `BAK-13`, `BAK-18`) intentionally have no separate checkbox; they are covered by the canonical items listed in Appendix B.

The sections below are ordered by **what moves the compatibility claim**, not by subsystem.

### 3.1 P0 — Measurement, oracle, and proof

- [ ] **TEST-06 — In-game playability and visual review**
  **Status:** Pending · **Priority:** P0 · **Effort:** L
  **Remaining:** audit the **top 100 titles first** with scripted 10-minute runs; record `Good`/`Playable`/`Unplayable` with a reason and a checkpoint screenshot; then extend to the full local set; rerun the black/static titles at 12,000+ frames; repeat with high-resolution only after the base audit; feed results into `GameCompatibilityList.md` and the per-game database.
  **Done when:** the top 100 have a reviewed status and evidence; then every local disc has one; the compatibility matrix distinguishes loadable, playable, and good.
  **Why first:** this is the only evidence that answers "better than 4DO and Opera" for a player. A 594-title "renders" count does not.
  **Ref:** `docs/Compatibility.md` §10/§13; `docs/GameCompatibilityResults.md`.

- [ ] **CORE-11/16 + FND-05 — Full-collection differential and hash regression**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** CORE-11, CORE-16, FND-05
  **Remaining:** keep the 50-title panel green (video, PC phase, and audio are now clean; CD cadence is the only behavioral field), then run the full 594; PC-delta triage; state/CD transcript comparison; frame/audio hash coverage; scripted-input panel coverage; nightly dashboard; allowlist and triage policy.
  **Done when:** the nightly run has no unexplained divergence, and every allowance is documented and dated; the 50-title panel is green before the full run.
  **Rule:** a frame-hash match without PC/CD/audio evidence is a smoke result, not a pass.
  **Evidence (2026-11-07):** the line-based field-boundary panel (`test64-panel50-line-boundary/report.json`) captures **50/50 with 0 errors** and **50/50 frame hashes** at 60/120/300. The same-frame retired-instruction delta is **11-20** at f=60 (average 15.3) and **23-2,223** at f=120 (most titles around 200); every first PC/CPSR divergence is same-frame phase. The first behavioral divergence moved from **36 titles at f=120** to **11 titles at f=300**: 8 `cdCommandCount` differences and 3 one-command `cdCommands` sequence differences (Creature Shock, Foes of Ali, N.O.B.). The other 39 titles have no behavioral divergence at the checkpoints. See `DifferentialHarness.md` §8.
  **Evidence (2026-11-08):** the audio-gated 50-title panel (`test93-panel50-audio/report.json`, local corpus, debug core `opera-build-timers/OperaCore.dll`) captures **50/50 with 0 errors**, **50/50 frame hashes**, and **150/150 audio hashes** at 60/120/300; the nightly workflow now captures the showcase fresh and compares it against the checked-in baseline. It leaves **39/50 phase-only** and **11 CD-only first behavioral divergences at f=300** (8 count, 3 one-command sequence); Dragon's Lair is 110/108. The same-frame retired-instruction delta is **11-20** at f=60 (average 15.3), `-46..2,164` at f=120, and `-36,104..3,591` at f=300; **50/50 first PC/CPSR divergences are same-frame phase**. The prior `test91-panel50-heuristic-retired` run had one full match (Daedalus Encounter) and 38 phase-only; the only change is Daedalus's f=300 PC sample, which is a same-frame phase, not a video or audio difference. See `DifferentialHarness.md` §8.

### 3.2 P0 — Accuracy and compatibility

- [ ] **CORE-04 — CD-ROM command and timing parity**
  **Status:** Partial · **Priority:** P0 · **Effort:** XL · **Tracks:** CORE-04
  **Remaining:** command latency table; per-command sector readiness; READ_DATA completion/read-ahead semantics (the transcript classifier shows the command table matches on 47/50 panel titles; the residual is guest wait cadence); TOC/Sub-Q/raw-sector behavior; data-sector subcode and read-ahead; CD-side tagged status/chunks; raw-vs-Opera command/status/boot/reset transcripts; CD-DA read-ahead and multi-disc layering on the same model. The CD register and CLIO read traces are now wired in; the Lemmings READ_ID wait residual is data-dependent guest copy/decrypt work (+8,694 instructions), not CD readiness or timer scheduling. The READ_ID media-access heuristic was retired after an A/B showed it raised a false POLMA level FIQ and shifted Space Ace's 3,000-frame FMV hash; Road Rash's 3,000-frame hash is unchanged. Finish the 300-frame cadence triage and the 20th Century long-run FMV read-ahead (332/311 at 3,000 frames with video still matching).
  **Done when:** fixed command transcripts match Opera or have documented, tested divergences; the 594-disc sweep has no CD regression; CD-DA and ODE layer cleanly on the same model; streaming/FMV titles (Road Rash and Need for Speed are already in the corpus) show no regression.
  **Why first in Phase 1:** this is the largest behavioral gap between 5DO (~1.9k lines of CD model) and Opera (~3.8k) and the most likely cause of real-game failures.
  **Evidence (2026-11-07):** after the video fixes, the 50-title panel is **video-clean at 60/120/300 (50/50 frame hashes)** and **PC/CPSR is proven frame-boundary phase (50/50 align to Opera frame 59)**. The **only remaining behavioral divergence is the CD command count**: 36 titles first differ at f=120 by 1-2 commands (in-flight cadence; all recover by f=300), and 13 at f=300 — seven FMV titles issue +3/+4 extra READ_DATA commands while six are 1-2 behind. The 3,000-frame sample keeps the pattern: Lemmings 365/352, Road Rash 851/836, Space Ace 349/348, Wolfenstein 862/861, Alone 932/932; video matches at 3,000 on 4/5 (Road Rash still diverges). The first long-run sequence divergence remains READ_DATA (`0x10`) vs READ_ID (`0x83`). The next target is READ_DATA completion/read-ahead and FIQ publication timing, not the command table. **Correction (increment 5):** the line-based field boundary removed the f=120 divergences; the current panel matches the CD count on 50/50 titles at f=60 and f=120, with 11 first behavioral divergences at f=300.
  **2026-11-07 increment:** READ_DATA now rejects an invalid MSF (MEI address error) and a transfer that starts at or runs past the last disc sector (MEI end address), and a new READ_DATA aborts the previous transfer so a stale sector cannot leak into the FIFO. Three CD tests cover the error paths and the abort. The 5-title and 20th Century CD counts are unchanged by this validation. A separate oracle build with the current Opera transfer-completion read-status rule (instead of the local build's first-sector publication) also produced identical counts, so past-end reads are not the cause of the residual FMV cadence difference.
  **2026-11-07 increment 2 (first-sector publication and deadline accumulation):** the oracle's per-command PC/instruction stamps showed that 5DO spent **+207,707 cycles / +86,511 instructions** between the last boot READ_DATA and the READ_ID handoff. The cause was the status-publication rule: 5DO waited for the whole transfer, while the oracle publishes POLST as soon as the first requested sector is queued (`cdrom_data_publish_read_status_if_ready`, `data_len != 0`). Matching the oracle moved that handoff to **-818 cycles / -376 instructions** and improved the 20th Century command-104 cycle delta at f=300 from **+324,027** to **+111,990**. READ_DATA deadlines now also accumulate from the previous deadline (`cdrom_data_schedule_next`) instead of re-basing to `now + sector_cycles`, so a late guest no longer slows the transfer. The confirmation panel (`test55-panel50-first-sector/report.json`) captures **50/50 with 0 errors**, **50/50 frame hashes at 60/120/300**, and the same **36/13/1** first-CD-divergence distribution as test43. The 5-title sample keeps **5/5 frame-hash matches at 60/120/300** at both unbounded and stock 2x speed, and the 3,000-frame sample keeps the established 4/5 video result (Road Rash still diverges) with CD counts Alone 932/932, Lemmings 365/352, Road Rash 851/836, Space Ace 349/348, Wolfenstein 862/861. 20th Century keeps all four video hashes through 3,000 frames and ends at CD 332/311; the remaining long-run gap is command cadence, not video. **1,919 tests pass.**
  **2026-11-07 increment 3 (transcript classification and cadence steps):** the harness now reports `cdCadence` and classifies the final transcript. On the 50-title panel **39 streams are identical, 8 are prefix-only, and 3 are sequence-shifted by one READ_ID poll** (Chiki Chiki, Dragon's Lair, Virtual Horror); the command table itself matches on the other 47. **24/50 titles have a significant cycle step and every one is issued from the same PC on both engines**, with the retired-instruction delta jumping alongside the cycle delta. The fresh five-title run (`test60-cadence-samples5/report.json`) shows Alone 361/361 (`+379` cycles), Road Rash 395/395 (`+468`), Wolfenstein 341/341 (`-345`), Lemmings 109/105 with a single `+22,834` step at command 51 (PC `0x00022140`, `+8,242` instructions), and Space Ace 168/168 with a single `+80,567` step at command 164 (PC `0x0002aa18`, `+32,330` instructions). The residual `cdCommandCount` difference is therefore guest wait-loop cadence, not READ_DATA command/status divergence; the next measurement is a CD register-access trace around the step. **1,921 tests pass.**
  **2026-11-07 increment 4 (CD register trace):** `--cd-register-trace` records every CD command/poll/status/data access with cycle and PC. Space Ace drains the READ_DATA status by cycle 58,934,229, then has **no CD access from 58,935,434 to 59,173,228** (237,794 cycles) before command 164 at 59,174,910. Lemmings drains all 12 READ_ID status bytes by 29,431,952, then has **no CD access until 31,599,968** (2.17M cycles). The 60-100k and 20-60k cadence steps are not CD readiness waits; the guest waits on a non-CD source (timer/VBlank) and returns. This moves the residual lead from CORE-04 to CORE-07 and leaves the CD command/status model matching the reference on the tested panel. **1,922 tests pass.**
  **2026-11-07 increment 5 (line-based field boundary):** after the frame-boundary fix, the confirmation panel (`test64-panel50-line-boundary/report.json`) matches the CD count on **50/50 titles at f=60 and f=120** (down from 36 first divergences at f=120), and the first behavioral divergence moved to **11 titles at f=300**: 8 `cdCommandCount` differences and 3 one-command `cdCommands` sequence differences. The remaining count outliers still show the ~30,000-instruction guest wait-loop cadence step. This is the current CORE-04 residual; the command table itself matches on the other 47 titles. **1,923 tests pass.**
  **2026-11-08 increment (CD poll enables and level FIQ):** the reference `opera_cdrom_init` resets `poll` to `CDROM_POLL_INTEN_MASK` (`0x07`) and latches `POLRE` on the boot disc access (`0x87`); 5DO reset to `0x80` only. The guest driver reads and writes the poll register back, so 5DO's read-modify-write cleared all three interrupt enables and never raised the CD FIQ for READ_ID/READ_ERROR completions. `CdromDevice.ResetPoll` now uses `0x87` in Opera semantics, and `FiveDoMachine` re-raises FIQ 4 while `Xbus.FiqPending` in `OperaCycleBased` mode (the reference asks every device for `XBP_FIQ` each batch). The 5-title sample keeps 5/5 frame hashes; Lemmings still has the 109/105 command count because the remaining wait is a data-dependent guest copy loop, not CD readiness. The final 50-title panel (`test91-panel50-heuristic-retired`) keeps 50/50 frame hashes, 0 errors, improves Daedalus Encounter to a full match, and leaves 38 phase-only plus the same 11 CD-only behavioral divergences; Dragon's Lair remains 111/108. An A/B isolated the retired READ_ID media-access heuristic: the poll fix alone and the level FIQ alone both kept Space Ace's 3,000-frame hash at `0x46916C61` (Opera), while the combination shifted it; retiring the heuristic restored the hash and left Road Rash's 3,000-frame hash unchanged. **1,925 tests pass.**
  **2026-11-08 increment 2 (poll reset parity and 900-frame cadence):** Opera treats `POLREMASK` (`0x08`) as a one-shot FIFO reset and stores only the three interrupt enables (`0x07`); 5DO persisted the reset bit and gated command/status/data accesses until the next poll write, and re-latched POLRE when the bit was released. `CdromDevice.SetPoll` now matches the reference and the defensive gates are gone; new tests cover the reset pulse, write-one-to-clear POLRE, and parameter bytes not being recorded as opcodes. A 20th Century register trace shows the guest writes `0x87`, `0x07`, and `0x35` but never `0x08`, so this is not the `109/105` cadence cause. The new 900-frame `cd-cadence` probe shows the 11 panel count gaps grow (20th Century **231/211**, Creature Shock 242/241, ESPN 166/154, Dragon's Lair 169/159, Life Stage 143/131, Pebble Beach 179/167, Virtual Horror 166/155, Woody 140/128; Foes of Ali 288/287 and N.O.B. 245/244 are prefix-only). The first opcode mismatch is READ_DATA (`0x10`) vs READ_ID (`0x83`) on eight titles (Virtual Horror is the reverse), so the residual is a real guest CD read-schedule difference, not a 300-frame in-flight artifact. **2,043 tests pass.**
  **Ref:** `opera_cdrom.c`; `docs/ReferenceStrategy.md` §6.

- [ ] **CORE-03 — MADAM CEL busy timing parity**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** CORE-03
  **Remaining:** validate the deferred draw and pixel/texel busy deadline against Opera across CEL-status polling titles; tune the busy budget; record CEL busy traces.
  **Done when:** CEL busy traces match Opera within documented tolerances; timing-sensitive titles behave correctly; no frame-time regression.
  **Evidence:** the 2026-11-04 increment fixed line-rendered literal/packed pixel accounting and locked the 9M-pixel/s plus 64-cycle-per-CEL formula with tests.
  **Ref:** `opera_madam.c`.

- [ ] **CORE-02 — CLIO timer scheduler parity**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-02
  **Remaining:** validate Opera's per-slot scheduler, slack gap, and cascade carry on the panel; add differential timer evidence; decide whether the Opera scheduler can become the default.
  **Done when:** timer traces match Opera within documented tolerances; no golden-corpus regression.
  **Evidence:** 16-bit timer-register masking in Opera mode, per-timer telemetry (`--clio-timer-stats`), bounded `--clio-timer-trace`, a reference-model conformance test, and a `--clio-timers` A/B override. The mode stays opt-in until panel evidence is green.
  **2026-11-08 increment:** the reference debug core now records CLIO timer writes/underflows and reads with cycle/PC stamps, matched by 5DO's `--clio-timer-trace`/`--clio-read-trace`. On Lemmings under the accuracy profile both engines use the Opera scheduler; timer 14 underflows every **12,384 cycles (Opera)** and **12,396 (5DO)**, a constant ~285-cycle phase offset, not a period error. The old "~10,400 cycles" figure was the FreeDO-compatible mode. The READ_ID-to-READ_ERROR residual (+22,834 cycles, +8,694 retired instructions) is guest copy/decrypt work between commands, not timer scheduling. The trace also exposed and fixed the CD poll-enable reset and level-held XBUS FIQ (CORE-04/CORE-07). **1,925 tests pass.**
  **Ref:** `opera_clio.c`, `opera_clock.c`.

- [ ] **CORE-06 — SPORT/XBUS/DIPIR timing and DMA audit**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-06
  **Remaining:** full Opera trace diffs for SPORT/XBUS/DMA; DIPIR activation completion; XBUS poll/FIFO semantics; DMA start/completion traces; PBUS/SPORT edge cases; reset and state interactions.
  **Done when:** trace diffs are clean or documented; DMA/FIQ tests stay green; no regression on golden titles.

- [ ] **CORE-07 — FIQ/IRQ latency and interrupt audit**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-07
  **Remaining:** Opera differential traces for masked/nested/return/re-entry interrupts; request-to-vector latency distributions; CLIO/DIPIR/XBUS ordering; timer and FIQ edge cases; document telemetry fields. The frame-boundary phase is resolved. The timer-14 period is measured equal (12,384/12,396 cycles) and the XBUS CD FIQ is now level-held like the reference; the remaining Lemmings wait is guest copy/decrypt work, not interrupt ordering.
  **Done when:** latency metrics match Opera within tolerance; traces and `run --json` statistics are documented and regression-tested.
  **Evidence (2026-11-07):** the harness now exposes `--cd-register-trace` (`cdreg f=… cycle=… pc=… op value`). Space Ace has no CD register access for 237,794 cycles between command 163 and command 164, and Lemmings has none for 2.17M cycles between READ_ID and READ_ERROR; the guest is waiting on a non-CD source (timer/VBlank), not CD readiness. This follows the transcript classifier's 39-identical / 8-prefix / 3-READ_ID-shift panel result and the 24/50 significant steps that all issue from the same PC on both engines. The next measurement is a CLIO timer/VBlank trace around the step. The prior phase evidence remains: 48 panel titles' first PC/CPSR divergence at frame 60 is instruction-phase aligned to Opera frame 59 (residual -55..-256 instructions) and 2 titles first diverge at f=120 aligned to frame 119, so the frame-boundary PC difference is proven phase rather than an interrupt-ordering bug. The command-50 boot handoff was a separate event at about frame 142: 5DO spent **+207,707 cycles / +86,511 retired instructions** there because it waited for the whole READ_DATA transfer before publishing status; matching the oracle's first-sector publication reduced that to **-818 / -376** (see CORE-04). The residual universal frame-60 phase is therefore **not** the command-50 handoff; the next step is to trace the frame-60 boundary itself.
  **2026-11-07 increment (frame boundary):** the frame-60 phase had two causes. The harness finalized Opera's checkpoint at the next video callback, reading ARM/CD state one field late, and 5DO ended a field on the rounded 208,541-cycle budget while Opera ends on the 263-line scanline boundary. Capturing ARM/CD state in the checkpoint's own callback and ending `RunFrame` on `Quartz.VdlFieldIndex` reduced the same-frame retired-instruction delta at f=60 to **8-17** and at f=120 to **24-84** on the five-title sample, with PCs matching within one instruction. The residual is below one 16-cycle batch. The confirmation panel (`test64-panel50-line-boundary/report.json`) is **50/50 hashes, 0 errors**, with a same-frame delta of **11-20** at f=60 and **23-2,223** at f=120; the first behavioral divergence moved to **11 titles at f=300** (8 count, 3 sequence). The next measurement is the CLIO timer/VBlank trace around the remaining CD cadence step. **Timer-14 trace:** on Lemmings the READ_ID-to-READ_ERROR wait (commands 50-51) is **2,169,618 cycles** in 5DO and **2,146,784** in Opera (+22,834). There are **no FIQ events** and no CD register accesses during the wait; timer 14 underflows **209 times** at roughly 10,400 cycles per period. `--clio-timers opera` makes the period about 12,400 cycles and shifts the boot timeline, so the opt-in Opera scheduler is not the fix. This narrows the residual to the timer-14 period/guest wait loop (CORE-02) rather than interrupt ordering.
  **2026-11-08 increment (level-held XBUS FIQ):** the reference `opera_xbus_tick` asks every XBUS device for `XBP_FIQ` each clock batch; the CD plugin returns a level (`POLST`/`POLDT`/`POLMA` against the enables). 5DO raised FIQ 4 only on sector/status publication, so a command completion whose active bit had been cleared was never re-raised. `FiveDoMachine` now calls `Clio.GenerateFiq(4,0)` while `Xbus.FiqPending` in `OperaCycleBased` mode; `Xbus.FiqPending` covers every attached device and the built-in responder. The reference timer period is 12,384 cycles versus 12,396 in 5DO, and the remaining +22,834-cycle Lemmings wait is +8,694 guest instructions in a data-dependent copy/decrypt loop, not FIQ latency. **1,925 tests pass.**

- [ ] **CORE-01 — ARM60 cycle-class accuracy mode**
  **Status:** Conditional · **Priority:** P0 · **Effort:** M · **Tracks:** CORE-01
  **Remaining:** localize the startup instruction-count offset and align the first captured field using the new instruction counter and CD cycle stamps; do not change the default until the 50-title panel shows a game-visible outcome. The 2026-11-04 40-title A/B found ARM60 17/40 matches versus FreeDO 15/40 with identical Opera captures: a two-title delta on a frame-hash metric is not sufficient evidence. Per-class costs and the `--cycle-classes` ledger are complete and regression-tested.
  **Evidence (2026-11-07):** the harness now instruction-aligns the first PC divergence against neighboring reference frames. On the 50-title panel all **50/50** first PC divergences align with **Opera frame 59** and differ by only **-55 to -256 retired instructions**, while the same-frame delta is ~89,000 — exactly one field of instructions. The offset is therefore a startup **frame-boundary phase**, not a per-instruction cycle-model error; CORE-01 is not implicated. The FreeDO N=4 model remains far worse (3,855,498 instructions at f=60), confirming N=2. Removing the one-field phase is a boot-timing/CORE-07 task, not a cycle-class change. The 2026-11-07 frame-boundary increment resolved it: the apparent one-field offset was a harness sampling-order bug plus 5DO's rounded cycle-budget field termination, not an ARM cycle-class error. CORE-01 remains conditional and is not implicated.
  **Done when:** the panel shows a reproducible game-visible difference; cycle counts match Opera within documented tolerances; golden hashes are unchanged or improved; no full-collection regression.
  **Ref:** `opera_arm.c`, `opera_clock.c`.

- [ ] **CORE-13 — Remaining compatibility failures**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** CORE-13
  **Remaining:** Crash 'n Burn (Japan); any 12k/65k-frame black/static titles; BIOS-aware sweep status merge; title-specific fixes; regression tests and compatibility notes. Use the 4DO game database and its fix flags as the first source for known-bad titles.
  **Done when:** every remaining exception has a root cause and a documented workaround, or 594/594 render; `GameCompatibilityList.md` is current; the top-100 audit is clean.
  **Evidence (2026-11-07):** the 50-title panel is now **video-clean (50/50 frame hashes at 60/120/300)** after fixing the 24-bit VDL background and the zero-entry VDL stop. The remaining panel divergences are CD cadence only. At 3,000 frames Space Ace, Wolfenstein, Alone, and Lemmings match Opera's frame hash; Road Rash still diverges at 1,000 and 3,000. This is the first concrete CORE-13 lead from the new oracle: the video gap was a presentation/VDL bug, not a title-specific fix. The residual long-run difference tracks CD cadence and the one-field boot phase. **Correction (2026-11-07):** the one-field boot phase was a harness sampling-order bug plus 5DO's rounded cycle-budget field termination; the line-based field boundary removed it. The remaining long-run difference tracks CD cadence.
  **Ref:** `References/4DO-master/FourDO/Emulation/GameSource/GameDatabase.xml`; `docs/GameCompatibilityResults.md`.

- [ ] **SYS-01 — CD-DA and multi-track completion**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-01
  **Remaining:** CD-DA read-ahead and subcode; a known-title in-emulator CD-DA capture; a 10-second DSP+CD-DA WAV; multi-track edge cases.
  **Done when:** a known CD-DA title plays its audio; a 10-second mixed WAV contains both DSP and CD-DA; data-track titles keep identical hashes.
  **Evidence:** `ChdDiscImage` opens audio-only CHDs, gives them a stable TOC-derived game ID, and reports `audioCd`/`trackCount`; the three local `[CD-Audio]` bonus discs are catalogued but not playable in-emulator because the BIOS still needs a data track.
  **Ref:** `opera_cdrom.c`; `docs/Audio.md`.

- [ ] **SYS-02 — ODE and multi-disc completion**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** SYS-02
  **Remaining:** guest-driven ODE swap prompts; a real in-game multi-disc swap with save/state round-trip; playlist edge cases and recovery.
  **Done when:** a known multi-disc title swaps in-game and continues; state load after swap is correct.

- [ ] **SYS-03 — PAL/NTSC completion**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-03
  **Remaining:** PAL2 title evidence; automatic region/profile application; full PAL library validation; 50 Hz audio/pacing evidence; region-aware screenshots, saves, and CLI output.
  **Done when:** PAL1/PAL2 titles render and pace correctly; NTSC behavior is unchanged; per-game region selection applies automatically.

- [ ] **SYS-15 — Input devices, calibration, and options**
  **Status:** Partial · **Priority:** P0 · **Effort:** L · **Tracks:** SYS-15
  **Remaining:** per-device calibration; scripted gameplay evidence for mouse, lightgun, arcade lightgun, flightstick, and Orbatak trackball; arcade start/coin/service mapping.
  **Done when:** a representative title works for each device; calibration and crosshair options are documented and tested.
  **Evidence:** the 2026-11-04 increment added the hide-lightgun-crosshair option, eight PBUS ports, an active-device cap of 0-8 (`--active-devices`/Settings), and a 256-byte PBUS chain buffer.
  **Ref:** `lr_input.c`, `opera_pbus.c`, `libretro_core_options.c`.

- [ ] **SYS-16 — Six-player and multi-tap verification**
  **Status:** Pending · **Priority:** P0 · **Effort:** S · **Tracks:** SYS-16
  **Remaining:** six-player test ROM/title; hardware multi-tap trace.
  **Done when:** six pads and multi-tap are verified on hardware or a test ROM.
  **Evidence:** eight PBUS ports, the Opera chain for ports 7-8, a 256-byte MADAM queue, and an eight-player packet test through `FiveDoMachine` (`EightPlayerPacket_ReachesTheMadamPbusQueue`).

- [ ] **CORE-08 — Accuracy/performance profile coverage**
  **Status:** Partial · **Priority:** P0 · **Effort:** S · **Tracks:** CORE-08
  **Remaining:** profile validation on the 50-title panel and then the full collection; decide when the Accuracy profile can become the default.
  **Done when:** every profile round-trips through CLI/Settings/state and the Accuracy profile can be enabled by default after panel + full A/B evidence.
  **Evidence:** `bench --profile` reports the derived profile and timing modes; `selftest` validates determinism and state round-trip for every named profile; the differential harness accepts `--profile`. A 40-title, 300-frame A/B reports 17/40 matches for both `compatible` and `accuracy` with identical per-title statuses; the panel A/B remains.

- [ ] **AUD-01 — Audio output quality: resampler, exclusive mode, and sync offset**
  **Status:** Partial · **Priority:** P0 · **Effort:** M · **Tracks:** AUD-01
  **Remaining:** resampler quality options; exclusive-mode output; long-session device-change/drift validation on Windows and Linux (macOS after hardware). Audio sync offset, queue-latency reporting, OpenAL device recreation, underrun/drop counters, and documentation are complete.
  **Done when:** audio quality/latency targets are met on Windows/Linux/macOS; device switching is safe; no crackle or drift in extended runs.
  **Evidence:** `AudioSyncMilliseconds` (−250…250 ms) applies a fixed host offset by inserting silence or dropping leading samples and reserves ring capacity for positive offsets. `OpenAlAudioOutput` recreates output safely on device/buffer/sync changes and exposes dropped-sample/underrun/queued-frame/latency counters. AOT CLI realtime Lemmings with `--audio openal --audio-sync 40` under `ALSOFT_DRIVERS=null` reported 0 dropped samples and 0 underruns over 240 frames. See `Audio.md` §3 and `Trimming.md`.

### 3.3 Release blockers (not accuracy blockers)

- [ ] **ECO-04 — Linux/macOS platform validation and packaging**
  **Status:** Partial · **Priority:** P1 for release · **Effort:** L · **Tracks:** ECO-04
  **Remaining:** validate OpenGL/GLES on real Linux and macOS GPUs plus llvmpipe fallback; macOS runtime validation; macOS gamepad backend; Flatpak/AppImage packaging and desktop integration; platform smoke evidence.
  **Done when:** full suite + frontend smoke pass on Linux and macOS hardware; Flatpak/AppImage installs and runs; gamepad works on macOS.
  **Note:** this blocks the six-platform release gate, not the "better than 4DO/Opera" accuracy claim. 4DO is Windows-only; Opera's platform coverage is not the comparison that matters.

- [ ] **SYS-05 — Embedded open-source boot ROM**
  **Status:** Blocked · **Priority:** P2 · **Effort:** M · **Tracks:** SYS-05
  **Remaining:** legal review of the **GPL-2.0-only** Opera Boot ROM (`trapexit/opera-boot`, embedded via `opera_boot.h`); decide whether to ship it as an optional separate component or not at all. It is a homebrew boot ROM, not a retail BIOS: retail titles still need a user-supplied BIOS. No engineering blocker.
  **Done when:** legal sign-off is recorded and homebrew/demos boot without a user BIOS, or the item is closed as not distributable.
  **Ref:** `opera_boot.h`, `opera_bios.c`.

### 3.4 P1 — Evidence and robustness

These items strengthen the claim after the Phase 0-3 work is green. They are not a reason to delay `TEST-07` or `CORE-04`.

- [ ] **CORE-10 — Hardware test ROM suite**
  **Status:** Pending · **Priority:** P1 · **Effort:** L · **Tracks:** CORE-10
  **Remaining:** test ROMs for CPU, CLIO, MADAM, DSP, SPORT, VDLP, CD, input; golden outputs; CI integration; documentation.
  **Done when:** the suite runs in CI and catches timing/behavior regressions without a copyrighted BIOS.

- [ ] **AUD-04 — Audio golden tests vs Opera**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** AUD-04
  **Remaining:** WAV corpus; spectral/RMS/envelope comparison; DSP mode changes; CD-DA mixes; hardware reference captures.
  **Done when:** the corpus runs in CI with documented tolerances and catches regressions. Requires the fixed seed from `TEST-07`.

- [ ] **VID-13 — Video test suite and color accuracy**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** VID-13
  **Remaining:** test patterns; RGB555/CLUT/NTSC behavior; CPU/GPU parity; high-res comparison; color delta report.
  **Done when:** every presenter/filter passes the suite within documented color tolerances.

- [ ] **TEST-01 — Save-state migration matrix**
  **Status:** Pending · **Priority:** P1 · **Effort:** M
  **Remaining:** test every version bump; v1-v3 states; option/region/disc-hash changes; warn/reject policies; 4DO/Opera interop once save-state interoperability lands.
  **Done when:** every supported state version loads or fails with a typed, tested error.

- [ ] **ENG-01 + ENG-09 — CI quality gates**
  **Status:** Pending · **Priority:** P1 · **Effort:** M · **Tracks:** ENG-01, ENG-09
  **Remaining:** full OS/arch matrix; coverage gate; static analysis; benchmark regression; SBOM; reproducible builds; six-bundle smoke; artifact checksums.
  **Done when:** every release candidate passes all gates and publishes signed, reproducible evidence.

- [ ] **TEST-04 — GPU and platform CI matrix**
  **Status:** Pending · **Priority:** P1 · **Effort:** L
  **Remaining:** GL/GLES/Vulkan/D3D11 coverage; Windows/Linux/macOS; x64/arm64; software fallback; GPU probe artifacts.
  **Done when:** each backend is exercised in CI or a documented hardware lab, with parity artifacts.

- [ ] **TEST-05 — Performance regression matrix**
  **Status:** Pending · **Priority:** P1 · **Effort:** M
  **Remaining:** Tier A/B and arm64 baselines; filters; high-res; interpreter and opt-in JIT; relative >10% gate; published trend.
  **Done when:** regressions fail the gate and the trend is visible to maintainers.

### 3.5 Deferred — performance and product

Deferred until Gate 3 is green. These do not make the emulator more accurate, and most of them do not matter to a player choosing between 5DO, 4DO, and Opera. Most are Partial or Pending; the full status and evidence stay in the canonical ID history. Gate 5 in §5 pulls the specific performance items it needs after compatibility is proven.

**Performance (only if a measured gate fails):**

- [ ] **PERF-07 — Tier B 1080p60 budgets and profiling** · P1 · M
  **Remaining:** profile integrated GPUs and older CPUs; 1080p60 per-filter budgets; p99 and allocation targets; reference-machine matrix. **Done when:** Tier B sustains 1080p60 with p99 under budget.
- [ ] **PERF-01 — Zero-allocation steady state on every real path** · P1 · M
  **Remaining:** the ~2.3 KB/frame Avalonia/compositor residual; CHD decode allocations in the core stage; uploads, readback, audio, gallery/OSD. **Done when:** 0 managed bytes/frame after warm-up; CI gate.
- [ ] **PERF-04 — Host disc streaming and CHD decompression** · P2 · M
  **Remaining:** allocation-free CHD/ISO prefetch under continuous streaming; decompression buffers; cache sizing; read-ahead tuning. **Done when:** no steady-state allocation or presentation stall; emulated CD timing unchanged.
- [ ] **PERF-05 — High-resolution CEL performance** · P2 · M
  **Remaining:** the CEL rasterizer still writes four times the pixels; per-title fallback and a 60 fps Tier B measurement. **Done when:** high-res meets its frame-time budget and degrades gracefully.
- [ ] **PERF-06 — GLES 3.1/ANGLE renderer performance** · P2 · M
  **Remaining:** GLES 3.1 feature path; real-hardware ANGLE/Tier B percentile runs; state-change reduction; pipeline-cache measurement. **Done when:** GLES 3.1/ANGLE match desktop GL and meet 1080p60 budgets.
- [ ] **ENG-02 — Interpreter hot-path optimization** · P1 · L
  **Remaining:** the real-title boot path is dominated by machine events, DSP, and memory-mapped I/O, so the measured Lemmings gain is inside noise; a decoded block cache, a batched interpreter loop that preserves the per-instruction CEL FSM check, and further single-data-transfer specialization remain. **Done when:** a documented speedup with no accuracy or state change. The 2026-11-05 increment measured synthetic 1.58→1.33 ms/frame and Lemmings 1.43→1.13 ms/frame.

**Product (after compatibility is proven):**

- [ ] **UX-03 — Netplay** · P2 · XL — lobby/direct connect; lockstep or rollback; NAT guidance; desync detection; state handoff. **Done when:** two clients complete a 10-minute session with no unexplained desync.
- [ ] **UX-05 — Achievements** · P2 · L — provider integration; offline cache; unlock UI; per-game progress; privacy. **Done when:** unlocks persist and offline behavior is documented.
- [ ] **UX-06 — TAS tooling completion** · P2 · L — full TAS UI; input timeline; multiplayer display; lag detection; movie library; branch comparison. **Done when:** record/edit/branch/verify/share works from desktop and CLI.
- [ ] **UX-07 — Controller profiles and input polish** · P2 · M — named profiles; deadzones/curves; hot-plug prompts; import/export; per-game profiles; conflict detection. **Done when:** profiles round-trip and survive device changes.
- [ ] **UX-20 + FND-06 — Per-game settings UI completion** · P2 · M — cheat/shader-chain fields; inherit/reset; search/filter; live application; localization. **Done when:** every shipped option is editable per game with inherit/reset.
- [ ] **UX-21/22 — Debugger and profiler suite** · P2 · L — CPU/memory/CEL debugger; breakpoints/watchpoints; step/run-to; trace viewer; frame profiler. **Done when:** a developer can debug a stuck title without the CLI.
- [ ] **UX-24 — Lua scripting and automation** · P3 · L — sandboxed host; memory/input/state/screenshot APIs; per-game scripts; CLI hooks. **Done when:** example scripts automate a test run and a gameplay sequence.
- [ ] **ECO-09 — 3DO CD player / Video CD** · P3 · M — audio-CD player UI; track navigation; VCD/MPEG research. **Done when:** a compatible disc plays with documented limitations.
- [ ] **VID-07 — Shader preset system** · P2 · L — `.slang`/`.glslp` presets; hot reload; per-game presets; parameter UI. **Done when:** popular presets load and fall back safely.
- [ ] **VID-11 + BAK-19 — Vulkan surfaces on Linux/macOS** · P2 · L — X11/Wayland; macOS Metal/MoltenVK or compositor embedding; no CPU readback. **Done when:** native presentation with parity.
- [ ] **VID-16 — VDLP output pixel format selection** · P3 · S — 0RGB1555/RGB565/XRGB8888; host-only conversion. **Done when:** the option changes only host conversion and is covered by tests.
- [ ] **VID-17 — Void-area patterns** · P3 · S — 4DO-style void patterns; per-game presets; preview. **Done when:** patterns match across presenters and screenshots.
- [ ] **AUD-03 — DSP debugger** · P3 · M — registers/memory; instruction trace; breakpoints; step/run-to; integration with the debugger suite. **Done when:** a DSP program can be debugged interactively.
- [ ] **TEST-02 + BAK-09 — ViewModel and UI tests** · P2 · M — settings, library, dialogs, hotkeys, gallery, playlist, profiles; headless UI smoke; accessibility. **Done when:** UI regressions are caught without manual clicking.

### 3.6 Research — only after Gate 3

- [ ] **VID-01 + BAK-11 — True 640x480/480i internal rendering** · P1 · XL
  **Remaining:** measure the current line-doubled mode; prototype true vertical 640x480; scale CEL/VDLP/clipping; independent 480-line output; per-title fallback. **Done when:** true vertical detail is visible on a 640x480 title and a 3D title, 60 fps on Tier A, default remains 320x240 + filter.
  **Note:** 4DO and Opera do not do this; it is not needed to win the comparison. `docs/HighResolutionCelResearch.md`.
- [ ] **VID-02 — 2x/4x supersampling** · P2 · L — prototype; deterministic downsample; geometry/texture comparison; performance budget. **Done when:** edges improve without changing guest VRAM semantics, with a go/no-go.
- [ ] **VID-03 — GPU CEL compositing** · P3 · XL — parity prototype for packed/literal CELs; VRAM semantics; fallback. **Done when:** the prototype matches the software renderer, with a go/no-go.
- [ ] **VID-08 — NTSC composite/S-Video/RF, HDR, and gamma** · P2 · L — signal models; CRT Royale-class presets; HDR; gamma/color controls. **Done when:** validated against reference captures and performance budgets.

### 3.7 Closed decisions (not backlog)

These are recorded decisions, not completed work. Do not reopen them without new measurement.

| ID | Decision | Evidence | Revisit only if |
|---|---|---|---|
| PERF-08 | **Stop investment** in the managed ARM JIT. Keep `--cpu-backend jit` as an experimental, opt-in backend; the interpreter remains the default and the accuracy oracle. | Three passes. Best result: Lemmings **1.23-1.37 ms/frame** vs **1.00-1.02** for the interpreter (~1.25x slower), despite a synthetic win (0.89-0.99 vs 1.20-1.30). ~24% of instructions still fall back; average block ~2.5 instructions. Hashes/instructions match. `Performance.md` §8.4. | A measured 1.5x end-to-end win on a real title, or a platform where the interpreter cannot hit 60 fps. |
| CORE-05 | **Do not implement** the threaded DSP. The handoff and validation cost exceeds the work it would offload. | DSP share on Lemmings: 112.8 ms / 800 frames = **0.141 ms/frame (~14%)**; ~0.19 µs of DSP work per sample. A per-sample worker is slower; a speculative pipeline must validate every ARM write to FIFO/DSP state and replay on change. `Performance.md` §8.5. | A title where DSP exceeds ~30% of frame time, or a bit-identical speculative pipeline measures a real win. |

---

## 4. Suggested execution order

This is the phased order. The IDs are the canonical entries in §3.

| Phase | Window | IDs | Exit gate |
|---|---|---|---|
| **0 — Truth** | Weeks 1-3 | TEST-07 (done), CORE-11/16 | Two byte-identical reference captures; PC/CD/audio fields populated; 50-title first-divergence report. |
| **1 — Disc** | Weeks 4-13 | CORE-04, SYS-01, SYS-02 | CD transcripts match or have documented divergences; CD-DA and multi-disc pass; no 594 regression. |
| **2 — Timing** | Weeks 14-21 | CORE-03, CORE-02, CORE-06, CORE-07; CORE-01 only if the panel proves it | CEL/CLIO/SPORT/FIQ traces within tolerance; timing-sensitive titles fixed. |
| **3 — Proof** | Weeks 22-33 | TEST-06, CORE-13, SYS-03, SYS-15, SYS-16, CORE-08, AUD-01 | Top-100 audit: 100% reach gameplay, ≥95% Good/Playable; 4DO/Opera head-to-head; zero unexplained regressions. |
| **4 — Release** | Weeks 34-40 | ECO-04, §3.4 evidence items | Six-platform release gate; audio/video golden suites; CI matrix; 24h soak. |
| **5 — Product/research** | After Gate 3 | §3.5-3.6 | Separate roadmap; no accuracy trade-off. |

**If you only do three things:** `CORE-04`, `TEST-06`, and `CORE-11/16`. The oracle (`TEST-07`) is now the foundation; those three produce the biggest behavioral fix, the playability proof, and the full-collection regression signal.

**Rule:** do not start Phase 5 while any Phase 0-3 P0 item is open. The repo's history shows product and performance work landing while `CORE-01`-`CORE-04` stayed partial; that is the pattern this rewrite exists to break.

### 4.1 Effort reality

By the plan's own S/M/L/XL scale, the **full backlog is roughly 800-850 developer-days** (~3.4 years solo). The focused path through Gate 3 is roughly **240 developer-days** (~12 months at a traditional solo pace), but the critical path is validation, not typing:

- OperaCore is real-time paced: a 3,000-frame capture is ~50 s per title, so a full 594-title pass is ~8-9 hours. The 50-title panel is ~1 hour per pass.
- The top-100 audit is 100 × 10-minute scripted runs plus visual review; budget 3-5 weeks even with automation.
- If you are driving implementation with AI agents, the coding half compresses; the capture, audit, hardware, and legal half does not.

A realistic wall-clock target for Gate 3 is **5-7 months at the project's recent AI-assisted velocity** (the 240 developer-days above are the traditional-effort estimate), with the top-100 audit as the bottleneck. The full plan remains a multi-year program; that is acceptable only if it is treated as post-compatibility product work.

---

## 5. "Best 3DO emulator" gates

These are the release/quality gates for the goal, not a second backlog. They reference canonical IDs only. A gate is green only when the evidence is committed, reproducible, and dated.

| Gate | Required evidence | Required IDs |
|---|---|---|
| **0. Oracle** | Deterministic reference captures; PC/CPSR/state/CD/audio fields on both sides; first-divergence report for 50 titles | TEST-07, CORE-11/16 |
| **1. Disc** | Command transcripts match or have documented divergences; CD-DA, multi-track, multi-disc, read-ahead; no 594 regression | CORE-04, SYS-01, SYS-02 |
| **2. Timing** | CEL/CLIO/SPORT/FIQ traces within tolerance; timing-sensitive titles fixed | CORE-02, CORE-03, CORE-06, CORE-07; CORE-01 conditional |
| **3. Compatibility** | Top 100: 100% reach gameplay, ≥95% Good/Playable; 4DO/Opera head-to-head; every exception documented | TEST-06, CORE-13, SYS-03, SYS-15, SYS-16, CORE-08 |
| **4. Audio** | No crackle/drift in extended runs; CD-DA mix; audio golden corpus | AUD-01, AUD-04 |
| **5. Performance** | 4K60 Tier A; 1080p60 Tier B; p99 and allocation budgets | PERF-07, PERF-01, TEST-05 |
| **6. Platforms** | Windows/Linux/macOS x64/arm64 validated; installers/signing | ECO-04 |
| **7. Trust** | 2,000+ tests; nightly differential; coverage/benchmark gates; fuzzing; SBOM; 24h soak | ENG-01/09, CORE-10, TEST-01, TEST-04, TEST-05, VID-13 |
| **8. Product** | Netplay, achievements, TAS, shaders, debugger, localization | §3.5 (deferred) |

**Release rule:** Gates 0-7 must be green. Gate 8 is not a release blocker for the compatibility claim, and must not delay it.

---

## 6. Reference strategy

### 6.1 Primary and secondary references

| Reference | Role | Use it for |
|---|---|---|
| `References/opera-libretro-master` | **Timing/behavior oracle** | CD-ROM, DSP, peripherals, SWI HLE, boot ROM, accuracy fixes |
| `References/4DO-master` | **Compatibility oracle** | Game database, fix flags, known-bad titles, UI behavior |
| `References/fourdo-code-r389-trunk` | Historical 4DO snapshot | Older behavior and regression comparisons |
| `References/FourDO Ported` | Porting notes/code | 4DO-to-C# mapping and conventions |
| `References/freedo-master`, `References/freedo-master (1)` | Original core reference | FreeDO quirks, ARM/CLIO/MADAM/DSP baseline |
| `References/CPP_SimpleGui` | Opera test harness | Differential runs and reference captures |
| `References/Docs`, `References/Docs2`, `References/Docs3` | Hardware/SDK documentation | ARM60, 3DO, CD-ROM, VDLP, and Portfolio details |
| `References/RMG-master`, `References/rpcs3-0.0.43` | **UX benchmarks only** | Library, netplay, controller profiles, packaging, per-game config, debugger |

**Reference hierarchy:** Opera wins on timing where it is measurable; 4DO wins on known game-specific fixes; hardware documentation wins when the two disagree. RMG and rpcs3 are product inspiration, not accuracy references.

### 6.2 Rules

1. **Port behavior, not code.** 5DO is a clean C# implementation; do not copy C/C++ source.
2. **One reference per change.** Record the source in the commit/PR and in `docs/PortingNotes.md` if the behavior diverges.
3. **Tests first.** Add the failing test or differential fixture before the fix.
4. **Preserve determinism.** No wall clock, unordered iteration, or environment-dependent seeds in emulated state.
5. **Keep the core single-threaded by default.** Threaded work must be opt-in and bit-identical; `CORE-05` is closed.
6. **Do not anchor to Opera's bugs.** Opera is a reference, not ground truth. If the hardware documentation is clear, follow the hardware and record why.
7. **Do not ship copyrighted BIOS/game content.** Embedded boot ROM work needs legal review; `SYS-05` is blocked until then.
8. **Document intentional divergence** in `docs/PortingNotes.md` and `docs/Compatibility.md`.

---

## 7. Verification commands

Run these before marking work done.

```powershell
# Build and test
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore

# Static analysis
jb inspectcode 5DO.sln --output=inspectcode.sarif --format=Sarif --sEverity=WARNING --no-build

# Compatibility sweep
pwsh -NoProfile -File tools/compat-sweep.ps1 -Roots "J:\Panasonic 3DO","G:\MAME\MAME Software List CHDs\3do" -Frames 3000 -Resume

# Differential smoke and showcase (current oracle)
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/smoke.json
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/showcase.json
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/showcase.json --compare-pc --core artifacts/diff/opera-build/out/OperaCore.dll

# After TEST-07: the 50-title accuracy panel
dotnet run --project tools/FiveDo.DiffHarness -c Release -- --manifest benchmarks/diff/panel50.json --strict --compare-pc --core artifacts/diff/opera-build/out/OperaCore.dll

# Benchmark gate
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- --frames 120 --label ci --json --gate

# GPU probes (Windows)
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --gl-probe --gl-probe-backend wgl --gl-probe-capture
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --d3d-probe artifacts/d3d-probe/parity.json --d3d-probe-size 320x240 --d3d-probe-frames 20 --d3d-probe-warmup 5 --d3d-probe-filter accurate --d3d-probe-capture artifacts/d3d-probe/parity.png
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --vk-probe

# Frontend smoke
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --smoke-test

# Frontend fullscreen launch evidence (marker records windowState=FullScreen)
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --smoke-test --fullscreen --smoke-output artifacts/frontend-fullscreen-smoke.txt
```

**Differential rule:** attach the report (manifest hash, BIOS hash, disc hash, frame count, build) to any accuracy claim. A frame-hash match alone is not evidence of accuracy.

**Linux validation:** use the local Hyper-V VM `LinuxMint` (`D:\Hyper-V\VMs\LinuxMint`). Read `AGENTS.local.md` for credentials. Publish `linux-x64` on the host and copy the output into the guest, or install the SDK in the guest. Record evidence in `docs/Compatibility.md` §10.3 and `docs/KnownIssues.md`.

**Release:** run the commands in [`ReleaseChecklist.md`](ReleaseChecklist.md); build bundles with `packaging/package-bundle.ps1`; keep a local copy in `src/FiveDo.Frontend.Avalonia/bin/Release`; remove PDB/XML files; publish `SHA256SUMS.txt`.

---

## 8. Maintenance rules

1. **This file is the single source of truth.** Do not recreate separate plan, backlog, handoff, or roadmap files.
2. **One canonical entry per feature.** Do not add a second checklist for the same work. Reference IDs instead.
3. **Retire duplicates.** `BAK-*` and `MOON-*` are aliases only; see Appendix B. Do not reintroduce them.
4. **Check the box only when "Done when" is satisfied.** Include behavior, tests, docs, and no regressions.
5. **Move completed items to §2.5 and the completed log.** Do not leave them in the missing-feature category.
6. **One feature per pull request.** No mixed accuracy/rendering/UX changes.
7. **No default timing change without panel evidence.** A 40-title, 300-frame sample is a smoke test, not a gate.
8. **Keep evidence current.** Update `TestCorpus.md`, `Compatibility.md`, `PortingNotes.md`, and `KnownIssues.md` when behavior changes.
9. **Keep the plan readable.** Keep §§1-8 lean; the completed log is append-only history and the appendices are reference material. Put long evidence in the linked docs; keep the decision and the number here.
10. **Record closures.** A "won't do" decision needs a measurement and a revisit condition, like §3.7.
11. **Refresh the snapshot monthly** or after every release: version, test count, compatibility counts, performance numbers, and platform validation status.
12. **Keep the cross-document anchors stable.** `#2-current-state`, `#3-missing-features-canonical-backlog`, `#4-suggested-execution-order`, `#7-verification-commands`, and `#9-completed-log` are linked from `NextSession.md`.

---

## 9. Completed log

Append new entries here; do not delete history. Each entry is a completed canonical item or a release milestone.

- **2026-11-08 — TEST-07 audio gating, scripted input, and the nightly baseline.** The differential harness now gates the cumulative audio hash at aligned sample counts, reports a count difference as `cumulative hash not comparable ... (reference 16.16 field-rate quantization, 1.214 ppm)`, and adds audio aligned/lag/diverged summary rows plus an HTML card. The alignment probe (`audio-probe.json`, Lemmings, 3,000 frames) measured 5DO's exact `60000/1001` rate against OperaCore's `3,928,227/65,536` (59.939987 Hz, 1.214 ppm lower): the sample counts match through 1,000 frames, then the reference is **+1 at 2,000** and **+2 at 3,000** (2.68 predicted). The 50-title panel (`test93-panel50-audio/report.json`) captures **50/50 with 0 errors**, **50/50 frame hashes**, and **150/150 audio hashes** at 60/120/300; the 11 CD-only first behavioral divergences at f=300 remain. Scripted input is wired through both engines: `test94-samples5-input` matches **5/5 frame hashes at 60/300/600**, the generic menu script changes guest output at 600/900, the no-input control matches the same checkpoints, and Road Rash's f=600 divergence reproduces without input (`test95-roadrash-noinput`). The nightly workflow now captures fresh (`--force`), uploads `report.html`, and fails only on a 5DO field change against the checked-in `benchmarks/diff/baseline/showcase-report.json`; `baseline --write` generates the trimmed baseline. **2,040 tests pass**; build, format, and InspectCode are clean. **Moves TEST-07 to completed.**
- **2026-11-08 — UX-27 Tooltip coverage, host logging hardening, XML-doc enforcement, and UI test expansion.** Every window in `FiveDo.Frontend.Avalonia` now exposes `ToolTip.Tip` on all interactive controls and on the information labels that previously had none: `DiscInfoWindow` (all 24 fields plus Close), `DiscBrowserWindow` field labels/values, `GameMetadataWindow` (developer, publisher, genre), `MovieEditorWindow` (11 pad buttons), `CheatsWindow` syntax help, `DiscPlaylistWindow` status, `ScreenshotCompareWindow` captions and panes, `MainWindow` library/icon/log headers, and the BIOS/NVRAM tab strip. A new `FrontendToolTipTests` parses every `Views/*.axaml` as XML and fails if an interactive element (`Button`, `TextBox`, `ComboBox`, `Slider`, `CheckBox`, `ListBox`, `TabControl`, `MenuItem`, …) has no `ToolTip.Tip` on itself or an ancestor, so the requirement is enforced by CI. Logging: added the missing `Debug` events to fallback/cleanup catch blocks (`Direct3D`/`Vulkan` surface initialization, cover resolution/download, DAT reads, void-color and refresh-rate fallbacks, random-seed parsing), guarded `App.Initialize` and the donation/update dialog `ShowAsync` entry points with `try`/`catch` + log, and kept every `Warning`+ event in the bug-report sink with the required environment/error/exception blocks. Naming/visibility: `CoverCache.LoadAsyncCore`→`LoadCoreAsync` and `OnCompareClick`→`OnCompareClickAsync` (the only production async methods without the `Async` suffix; `Main` is the framework exception), `Program` properties and `AsyncEvent.Run` are now `internal` to match their internal containing types. Documentation: `GenerateDocumentationFile` is enabled for `FiveDo.Cli` and `FiveDo.Frontend.Avalonia`, closing the last five CS1591 gaps. Dead code: InspectCode can be run at `WARNING` with **0 results**; a reference scan of the two host projects found no unreferenced members. Added 94 tests (LoggingInfrastructure, LibraryPresentation, UpdateInfo, FrontendToolTip); the suite is **2,033 tests**, with build, `dotnet format`, and InspectCode clean.

- **2026-11-08 — CORE-02/CORE-04/CORE-07 CLIO timer/read trace, CD poll enables, and level-held XBUS FIQ.** Added reference-side CLIO timer/read/XBUS/FIQ event instrumentation (`opera_clio_debug_*`, `sg_debug_timer_*`) and harness `--timer-trace`; 5DO gained `--clio-read-trace` and cycle stamps on `--clio-trace`. Measured timer 14 underflow periods on Lemmings under the accuracy profile: **12,384 cycles in Opera, 12,396 in 5DO**, a constant ~285-cycle phase offset, proving the old "~10,400" figure was the FreeDO mode and the residual is not the timer period. The READ_ID-to-READ_ERROR wait remains +22,834 cycles but is **+8,694 retired instructions** in a data-dependent guest copy/decrypt loop; the first 0x118 read is 29,491,640/29,469,632 and the last pre-READ_ERROR reads are identical. The trace exposed two reference-parity bugs: Opera resets the CD poll to `CDROM_POLL_INTEN_MASK` (`0x07`) plus the boot media latch (`0x87`), while 5DO reset to `0x80` and lost all enables on the guest's read-modify-write; and Opera asks every XBUS device for `XBP_FIQ` each batch (level-held) while 5DO only raised FIQ 4 on sector/status publication. `CdromDevice.ResetPoll`, `Xbus.FiqPending`, and the `FiveDoMachine` level check now match the reference. The A/B then isolated the old READ_ID media-access heuristic: poll fix only and level FIQ only both kept Space Ace's 3,000-frame hash at `0x46916C61`, while both together diverged; retiring the heuristic restored the hash and left Road Rash's `0xD5895AD5` unchanged. The final 50-title panel (`test91-panel50-heuristic-retired`) keeps **50/50 frame hashes, 0 errors**, improves **Daedalus Encounter to a full match**, and leaves **38 phase-only** plus the same **11 CD-only behavioral divergences**; the 5-title sample keeps **5/5 frame hashes**. The 3,000-frame sample (`test92-samples5-long-final`) keeps **4/5 video matches** (Alone `0x175B8FE4`, Lemmings `0xEBDB01CA`, Space Ace `0x46916C61`, Wolfenstein `0x175B8FE4`); Road Rash remains the only video divergence (`0xD5895AD5` vs `0x0528251F`). Lemmings is still 109/105 commands at f=300. **1,925 tests pass**; `dotnet format` and InspectCode are clean. Evidence: `artifacts/diff/test69-timer-traces` through `test92-samples5-long-final`; `TestCorpus.md` 1.1az; `PortingNotes.md` 12.38.

- **2026-11-07 — TEST-07/CORE-07 frame-boundary parity and checkpoint capture order.** Two independent defects produced the universal frame-60 phase. `OperaCoreHost` finalized each checkpoint at the next video callback, so ARM PC/CPSR/instructions and CD state belonged to the following field while the audio hash was already correct; the harness now captures ARM/CD state in the checkpoint's own video callback and fills the audio hash when that field's audio callback completes. `FiveDoMachine.RunFrame` also ended on the rounded 208,541-cycle budget while Opera ends on the 263-line scanline boundary (`opera_3do_process_frame`), and the discarded last-batch overshoot accumulated about 3.3 instructions per field; `RunFrame` now ends on `Quartz.VdlFieldIndex`, matching the reference's `line < opera_region_scanlines()` loop. On the five-title sample (`test63-line-boundary/report.json`) the same-frame retired-instruction delta at f=60 fell from ~89,000 (misaligned) / ~219 (after the capture fix) to **8-17**, and at f=120 to **24-84**; PCs match within one instruction. The 50-title confirmation panel (`test64-panel50-line-boundary/report.json`) captures **50/50 with 0 errors** and **50/50 frame hashes**, with a same-frame delta of **11-20** at f=60 and **23-2,223** at f=120. The CD count matches on **50/50 titles at f=60 and f=120** (down from 36 first divergences at f=120), and the first behavioral divergence moved to **11 titles at f=300** (8 count differences, 3 one-command sequence differences); the other 39 titles are phase-only. The 3,000-frame showcase keeps **4/5 video matches** (Road Rash is the only frame-hash divergence) with CD counts Lemmings 365/352, Gex 548/547, Road Rash 851/836, Wolfenstein 862/861, and Alone 932/932 (Alone's sequence hash differs but the video matches). This supersedes the earlier "5DO frame 60 aligns with Opera frame 59" interpretation. **1,923 tests pass**; build, format, and the focused machine/Quartz suites are clean.

- **2026-11-07 — TEST-07/CORE-04 CD register trace and cadence root-cause split.** Added `--cd-register-trace` (`cdreg f=<frame> cycle=<cycle> pc=<pc> op value`) covering command writes, poll writes/reads, status reads, data writes, and empty data reads. On Space Ace the READ_DATA status is drained by cycle 58,934,229, then there is **no CD register access for 237,794 cycles** before command 164 at 59,174,910. On Lemmings all 12 READ_ID status bytes are drained by 29,431,952, then there is **no CD access until 31,599,968** (2.17M cycles). Both large cadence steps are therefore guest waits on a non-CD source (timer/VBlank), not CD readiness. This confirms the transcript-classifier result (39 identical / 8 prefix / 3 READ_ID-shift streams) and moves the residual lead from CORE-04 to CORE-07. **1,922 tests pass**; build, format, and the focused CD/DiffComparer suites are clean.

- **2026-11-07 — TEST-07/CORE-04 CD transcript classification and cadence-step evidence.** Added `CdCadenceAnalyzer` to the differential harness. Every report now carries `cdCadence` (Equal / PrefixAhead / SequenceDiverged, first mismatch, last matched cycle/instruction delta) and the largest cadence step (cycle jump, opcodes, command-issue PCs, retired-instruction jump); the Markdown/HTML reports add a `CD cadence` column and detail line. On the existing 50-title panel the command streams are **39 identical, 8 prefix-only, and 3 sequence-shifted**; the three shifts are a one-command READ_ID poll placement (Chiki Chiki 111/112, Dragon's Lair 111/108, Virtual Horror 110/107). **24/50 titles have a significant step above 1,000 cycles and all 24 issue the step command from the same PC on both engines** (17 at 60-100k, six at 20-60k, one >=100k). The fresh five-title `test60-cadence-samples5/report.json` confirms Alone 361/361 (`+379`), Road Rash 395/395 (`+468`), Wolfenstein 341/341 (`-345`), Lemmings 109/105 with one `+22,834` step at command 51 (PC `0x00022140`, `+8,242` instructions), and Space Ace 168/168 with one `+80,567` step at command 164 (PC `0x0002aa18`, `+32,330` instructions). The residual `cdCommandCount` difference is guest wait-loop cadence, not a divergent CD command table; the next measurement is a CD register-access trace around the step. `benchmarks/diff/samples5.json` now points at the local `J:/Panasonic 3DO` corpus used by the panel. **1,921 tests pass**; build, format, and the focused DiffComparer suite are clean.

- **2026-11-07 — TEST-07 per-command PC/instruction stamps and READ_DATA first-sector parity.** The debug oracle now exports `sg_debug_cd_pc_at` and `sg_debug_cd_instructions_at`; 5DO records the same PC/retired-instruction stamps with every issued opcode. `--cd-commands` writes `opcode cycle pc instructions`, and captures carry `cdCommandPcs`/`cdCommandInstructions`. The new evidence showed the 20th Century boot handoff (last READ_DATA to READ_ID) cost **+207,707 cycles / +86,511 instructions**; matching the oracle's first-sector POLST publication reduced it to **-818 / -376**, and the f=300 command-104 cycle delta fell from **+324,027** to **+111,990**. READ_DATA deadlines now accumulate like `cdrom_data_schedule_next` instead of re-basing to `now + sector_cycles`. The confirmation panel (`test55-panel50-first-sector`) is **50/50 hashes, 0 errors**, with **48/50 first PC divergences at frame 59 and 2/50 at frame 119**, and the same **36/13/1** CD count distribution as test43. The 5-title sample keeps 5/5 frame hashes at 60/120/300 at unbounded and stock 2x speed; the 3,000-frame sample keeps the established 4/5 video result (Road Rash still diverges) and the same CD counts. **1,919 tests pass**; build, format, and InspectCode are clean.
- **2026-11-07 — Frontend `-fullscreen` launch alias.** The desktop shell now accepts `-fullscreen` in addition to `-f` and `--fullscreen`, with a unit test and an end-to-end smoke check. `--smoke-test --fullscreen` enters fullscreen before the smoke marker is written; the marker records `windowState=FullScreen` (normal smoke records `windowState=Normal`). `docs/UserGuide.md` documents all three spellings. **1,919 tests pass.**
- **2026-11-07 — CORE-04 READ_DATA validation and negative oracle evidence.** `CdromDevice.ReadDataCommand` now rejects an invalid MSF with MEI address error, rejects a transfer that starts at or runs past the last disc sector with MEI end address, and aborts any previous transfer so a stale sector cannot leak into the FIFO. Three CD tests cover the two error paths and the abort. The 5-title and 20th Century CD counts are unchanged. A separate oracle build with the current Opera transfer-completion read-status rule also produced identical counts, ruling out both the local oracle's first-sector status publication and past-end reads as the cause of the residual FMV cadence difference. **1,916 tests pass.** **Superseded:** the first-sector entry above shows the publication rule did change the handoff cycle/instruction timing; the earlier count-only test was too narrow.
- **2026-11-07 — 50-title panel video-clean (CORE-13/VID-13 lead).** The VDL background word now keeps all 24 bits and the CPU/OpenGL/Direct3D background paths output them directly; the previous 5-bit narrowing rendered `0x010101` as pure black. A zero VDL display-list word is now a stop that retains the previous control word and bitmap, matching `opera_vdlp.c`, instead of blanking the last visible scanline. The VDL frame save-state section is version 4 and converts version 1-3 packed backgrounds on load. Result: the 50-title panel matches **50/50 frame hashes** at frames 60/120/300 (up from 32/50), and the 5-title panel matches **5/5** (Space Ace and Wolfenstein 3D were previously divergent). Road Rash still diverges at 1,000/3,000. **1,916 tests pass**; build, format, and InspectCode are clean. Evidence: `artifacts/diff/test43-panel50-bg/report.json`, `artifacts/diff/test39-vdlstop-samples5/report.json`, `artifacts/diff/test44-long-bg/report.json`.
- **2026-11-07 — TEST-07 instruction-phase triage and harness robustness.** The harness now searches the checkpoint frame window for the neighboring reference frame whose retired-instruction count aligns with 5DO, records `pcPhaseFrame`/`pcPhaseInstructionDelta` per title, and labels every PC/CPSR divergence as an instruction phase in the report. The 50-title panel proves all 50 first PC divergences align to Opera frame 59 with a residual of only -55..-256 instructions (same-frame delta ~89,000), fixing the earlier "fixed startup offset" interpretation. ISO staging is now atomic (`.tmp` + move, no partial file on failure), retries once on a transient CHD read error, and a per-title capture exception is reported as an `Error` instead of aborting the panel. **1,916 tests pass**.

- **2026-11-08 - v1.5.0.** GPU HQx 2x/3x/4x rule-table passes in OpenGL/GLES, Direct3D 11, and Vulkan; 24-bit VDL background and zero-entry stop; reference field-boundary `RunFrame`; CD poll/FIQ/READ_DATA parity and first-sector status; instruction-phase and CD-cadence harness reporting; atomic ISO staging with a transient-CHD retry; `-fullscreen` desktop alias. The 50-title panel matches **50/50 frame hashes** at 60/120/300 with **0 errors**; the command-50 boot handoff fell from +207,707 cycles / +86,511 instructions to -818 / -376. VDLP state is version 4 and Machine state version 3 for the 24-bit background. **1,939 tests pass**; build, format, and InspectCode are clean. See `WhatsNew.md` and `ReleaseNotes-1.5.0.md`.

- **2026-11-07 - v1.4.0.** Six framework-dependent single-file bundles plus `SHA256SUMS.txt`; online cover art (UX-26), VID-12 latency controls, SYS-06 unsigned-disc support, the exact 60000/1001 NTSC field clock and rational VDL accumulator, VDLP blanking/background fixes, live OpenAL device-switching and test-tone fixes, and differential-harness CD-sequence/cycle evidence. The machine frame section now preserves dimensions and pixel aspect across load, run-ahead invalidates its snapshot after external machine actions, and the cover CLI/UI fail clearly when the catalog is unavailable. **1,905 tests pass**; build, format, and InspectCode are clean. Release run <https://github.com/purelogiccode/5DO/actions/runs/42399055155>; CI run <https://github.com/purelogiccode/5DO/actions/runs/42398646569>. See `WhatsNew.md` and `ReleaseNotes-1.4.0.md`.

- **2026-11-07 - UX-26 online cover catalog.** Added `FiveDo.Host/Covers` (catalog model and validation, content-addressed cache, conditional HTTP downloader, local/online resolver, annotation stripping, Jaro-Winkler, token/no-space/subtitle aliases, sequel-number guard, region/year/retail tie-breaks), the `covers` CLI, Settings > Covers, and `Configuration > Cover Art`. The packager deduplicates the 1,356-file source collection to **473 unique images**, encodes 600 px WebP q85 (**25.4 MB**, **26.7 MB** ZIP), maps **645/653** 4DO game IDs (9 manual overrides, 5 weak, 8 homebrew/emulator entries without covers), and reloads the generated catalog to verify every image hash and unique game-ID assignment. The catalog is hosted at `https://assets.purelogiccode.com/5do/v1`; see `CoverArt.md`. 14 host cover tests pass.

- **2026-11-06 — InspectCode cleanup.** Removed the last seven `jb inspectcode --sEverity=WARNING` warnings without changing behavior. `StateWriter` lost two redundant `(ushort)0` casts. `JitInstruction` no longer stores the unused `Terminator` property (the classifier's `terminator` local still stops the block) and declares `LayoutKind.Auto`. The unused `EmitStoreUserConstant` helper was deleted. `EmitDataProcessing` and `EmitBranch` no longer take an unused `state` local. `EnterFiqFromJit` is invoked only from generated JIT IL, so it is now looked up with `nameof` and carries a scoped `SuppressMessage("ReSharper", "UnusedMember.Local")` with a justification rather than being deleted. InspectCode reports **0 results**; 1,885 tests pass and format is clean.

- **2026-11-06 — Exact NTSC field clock and rational VDL (TEST-07/CORE-13 lead).** NTSC was modeled at 60.00 Hz (208,333 cycles/field) instead of the hardware and reference 60000/1001 (208,541.67). The 1 ms per 60 fields difference produced a 44-sample audio drift at frame 60 (44,101 vs 44,144) and 2,514 samples at 3,000 frames. The VDL accumulator also added the per-batch rounded 8.24 ARM fraction, running the scanline clock ~0.09% fast and slipping the VDLP phase. `VideoRegionProfile` now exposes exact numerator/denominator and `GetFrameCycles`; `Quartz` uses an exact rational VDL accumulator (`cycles * fieldLines * fieldRateNumerator` against `armClock * fieldRateDenominator`) and the Quartz state section is version 4 (v1-3 states convert their 8.24 fraction on load). On the 50-title panel every sample delta is now **≤9** at frame 300, and **32/50 titles match every frame hash** at 60/120/300. At 3,000 frames Space Ace, Wolfenstein, Alone, and Lemmings match Opera's hash exactly; Road Rash still diverges. 1,885 tests pass.
- **2026-11-06 — TEST-07 command-cycle and instruction-parity instrumentation.** `CdromCommandTranscript` records the ARM cycle of every issued opcode; the debug reference core adds `sg_debug_cd_cycle_at` (exact command boundary via `cmd_idx`, fixing the old byte-modulo-seven transcript for one-byte ABORT packets) and `sg_debug_arm_instructions`. The harness now reports the first **behavioral** divergence separately from PC/CPSR frame-boundary phase, prints the first CD command cycle delta, and records retired-instruction counts. 50-title result: 50/50 captured, 0 errors; CD count matches 49/50 at frame 60; The absolute instruction count carries a fixed ~89,000-instruction startup offset, but per-frame retirement matches within 0.1% on 4/5 titles; PC/CPSR is a phase sample, not a cycle-model gap. See `DifferentialHarness.md` §8 and `TestCorpus.md` 1.1ax.

- **2026-11-06 - TEST-07 oracle increment.** Added `sg_debug_set_seed`, `sg_debug_arm_cpsr`, `sg_debug_cd_command_count`, `sg_debug_cd_command_hash`, and `sg_debug_cd_command_at` to the debug reference core; added fixed-seed plumbing (default 1488), CPSR and CD command/sequence comparison, `sampleTolerance`, and `firstDivergence` reporting to `FiveDo.DiffHarness`; added issue-time command counters and the opcode list to `CdromCommandTranscript` and `CdromDevice`. `benchmarks/diff/samples5.json` runs five local titles at 300 frames. Two consecutive runs produced identical checkpoint arrays for both engines. Result: 0 match / 5 diverged / 0 error; first divergence is PC at frame 60 on all five; Lemmings CD commands match 9/9 at f=60 and 50/50 at f=120, then 108/105 at f=300; CPSR differs at f=60 by the FIQ-disable bit. Added the `panel` generator and produced `benchmarks/diff/panel50.json` (50 evenly spaced titles from the 594-disc manifest). The 3,000-frame follow-up shows Lemmings matching the frame hash at every checkpoint, Wolfenstein/Alone recovering after transient mismatches, and Road Rash/Space Ace visibly diverging; a ±60-frame window found no neighboring hash match. 1,881 tests pass. See `DifferentialHarness.md` §8 and `TestCorpus.md` 1.1aw.
- **2026-11-06 — Plan rewrite (focused execution plan).** Reorganized the backlog around the compatibility claim. Added `TEST-07` (reference oracle instrumentation and first-divergence metric), promoted `TEST-06` to P0 as a staged top-100 playability audit, moved performance and product work to §3.5, moved research to §3.6, and closed `PERF-08`/`CORE-05` with their measured negative evidence. `SYS-05` is blocked on GPL-2.0 licensing, not engineering. No IDs were retired; `SYS-06`, `SYS-08`, `SYS-12`, `SYS-17`, `SYS-18`, and `VID-04` moved to §2.5. The plan retains the five anchors other docs link to.
- **2026-11-06 - VID-12 latency features.** Added 0-2 frame run-ahead and 0-8 frame presentation delay in `CoreRunner`, VRR/adaptive-sync presentation, and a −250…250 ms audio sync offset. Run-ahead uses a reusable uncompressed `MachineStateSnapshot`; hidden frames suppress audio/host submission but still render through `Vdlp.DoLine` so visible hashes match the normal timeline. State frame section v2 writes only visible lines and the RAM loader clears only the previously addressable tail. Each depth step removes one field of emulator-side input delay (16.7 ms at 60 Hz, 20 ms at 50 Hz PAL). `bench --latency` measured a 9,348,438-byte snapshot, ~3.6-3.9 ms capture, and ~1.6-2.0 ms restore; one-frame run-ahead stays inside the 60 Hz budget on a typical title, and the runner auto-disables after 45 overruns or a rejected snapshot. `LatencyTests` covers snapshot round-trip, 1-/2-frame timeline parity, visible-only audio, frame-delay holding, range validation, and telemetry. Settings, per-game profiles, the FPS overlay, and `--audio-sync` expose the options. Real AOT frontend run: Lemmings booted with run-ahead 1, frame delay 1, VRR, and audio sync 40 ms; OpenAL initialized at 44.1 kHz through the null device with no fault. Full optical input-to-photon measurement and real adaptive-sync hardware confirmation remain validation caveats documented in `Performance.md` §8.7 and `KnownIssues.md`. See `Audio.md` §3 and `Video.md` §7.
- **2026-11-06 - PERF-02/BAK-08 NativeAOT, trimming, and audio validation.** CLI and frontend AOT win-x64 single-file publishes build and start. The CLI AOT executable passes `--version`, `selftest --json`, `bench --frames 2 --json`, and `bench --latency`; a 3,000-frame Lemmings boot and a 240-frame realtime OpenAL run with `--audio openal --audio-sync 40` under `ALSOFT_DRIVERS=null` completed with 0 dropped samples and 0 underruns. The AOT frontend `--smoke-test` returns `FRONTEND SMOKE OK 1.3.0`, opens the real OpenGL 4.0 surface, boots Lemmings, and logs `OpenAL audio ready (44100 Hz stereo, 100 ms, sync 40 ms)` with run-ahead 1, frame delay 1, and VRR enabled. Sizes: CLI AOT 16.6 MB exe / 18.7 MB runtime folder; frontend AOT 34.5 MB exe / 54.6 MB runtime folder; trimmed frontend 27.9 MB exe / 48.0 MB folder; trimmed CLI 19.4 MB. Startup: CLI AOT `--version` 69 ms, `bench --frames 2` 65 ms; AOT frontend `--version` 70 ms and smoke 1.2 s; trimmed frontend smoke 3.2 s. Added four opt-in `.pubxml` profiles. Artifacts remain framework-dependent by default; AOT/trimming stay opt-in. Silk.NET `IL3000`/`IL3002` and CHDSharp/SharpGen `IL2104` warnings remain visible but non-fatal. See `Trimming.md`.
- **2026-11-06 - SYS-06 RSA/unsigned-disc option.** Added `BiosRsaPatch`, the documented two-site media-RSA bypass, applied only in memory. Applying it to retail `panafz10.bin` and `panafz10e-anvil.bin` reproduces the known No-RSA MD5s `1477bda8...` and `cf11bbb5...` exactly. The BIOS catalog now identifies the No-RSA FZ-1J/FZ-10/FZ-10E images, automatic selection prefers them when `--allow-unsigned-discs`/Settings > Paths is enabled, and a compatible retail image is patched otherwise. `run`/`trace` report `biosRsaPatched`/`biosRsaDisabled`, `bios info` reports `rsaDisabled`/`rsaPatchSites`, and `bios select` accepts `--allow-unsigned-discs`. Signed Wolfenstein 3D and Road Rash 600-frame hashes are unchanged; the option never writes the BIOS file and no BIOS/game content is bundled. See `TestCorpus.md` 1.1au.
- **2026-11-06 - v1.3.0.** Six framework-dependent single-file bundles plus `SHA256SUMS.txt`; 480i/240p field handling (auto detection, bob, weave, blend), interlace-aware viewport geometry, screenshot/GIF support, per-game and CLI mode selection, and 1,833 tests. The release is presentation-only over 1.2.2: default bob hashes and save states are unchanged. Release workflow <https://github.com/purelogiccode/5DO/actions/runs/42235476257>; docs/wiki workflow <https://github.com/purelogiccode/5DO/actions/runs/42235456021>.
- **2026-11-06 - VID-04 480i/240p field handling.** Added `InterlaceProcessor` with bob, weave, blend, and content-based auto detection; wired it through `FiveDoMachine`, `AppSettings`/`GameProfile`, Settings > Video, screenshots/GIF, and `--deinterlace`; added `VdlFrame.PixelAspect` and interlace-aware viewport scaling so a woven 320x480 frame keeps its display geometry and integer scaling. Sixteen processor tests plus bob-vs-auto hashes on five local titles (3,000 frames for Road Rash, Space Ace, and Alone in the Dark) are unchanged. See `Video.md` §1.
- **2026-11-05 - PERF-06 ANGLE revalidation.** Re-ran the 4K offscreen probe through ANGLE/D3D11 on the RTX 4070: OpenGL ES 3.0, integer textures, sharp-bilinear present mean 0.67 ms / p99 1.29 ms over 120 measured frames, with 0 B/frame in the renderer. Real GLES 3.1 hardware and Tier B percentile runs remain open, so PERF-06 stays Partial. See `Performance.md` §8.
- **2026-11-05 - ENG-02 interpreter hot-path increment.** Added a direct DRAM/ROM instruction fetch (`IAddressBus.ReadInstruction`), inline LSL#0 and rotated-immediate operand handling, a flag-preserving data-processing dispatch that avoids the generic `ArmAlu` call, `BitOperations.PopCount`/`TrailingZeroCount` LDM/STM iteration, and a cached PC-adjust source. Interleaved same-host A/B: synthetic 1.58→1.33 ms/frame and Lemmings 1.43→1.13 ms/frame, with unchanged hashes. ENG-02 stays Partial pending a decoded block cache; see `Performance.md` §8.6.
- **2026-11-05 - PERF-08 managed IL JIT second pass.** Replaced per-instruction verification with exact RAM-write invalidation, kept PC/CPSR/operands/flags in block locals, compiled through RAM stores and conditional-branch fallthroughs, added direct `IRam` load/store fast paths, and fixed R15 operands to use the block-local PC. Correctness: 1,000-frame CLI hashes match the interpreter on Road Rash `0x41D2A1DA`, Powers Kingdom `0xDCE31DC5`, and Lemmings `0x110E7DC5`, with identical instructions/cycles. On the synthetic ALU/store loop the old JIT measured 1.32-1.36 ms/frame and the new JIT 0.78 ms/frame (1.7x faster); Lemmings improved from 1.58-2.60 to 1.40-1.78 ms/frame but remains slower than the 1.08-1.25 ms/frame interpreter, so the interpreter remains the default and PERF-08 stays Partial. See `Performance.md` §8.4.
- **2026-11-06 - PERF-08 JIT context ABI and register shifts.** Compiled blocks now share PC/CPSR/cycle/instruction state through `ref` parameters, so a chain of blocks inside one 16-cycle batch does not reload and flush architectural state per block. The FIQ exit stores the post-vector context, fixing a frame-loop rewind to the interrupted PC. Register-specified shifts are compiled with the interpreter's extra internal cycle and have a dedicated parity test. The compile-hit counter is an 8,192-entry direct-mapped table; 4,096 hits remains the best measured threshold. Lemmings improved from 1.60-1.66 to 1.23-1.37 ms/frame (~20% faster than the previous JIT) but is still ~1.25x slower than the 1.00-1.02 ms/frame interpreter; the synthetic loop is 0.89-0.99 versus 1.20-1.30 ms/frame. Hashes and instruction counts match on the 600-frame Lemmings checkpoint and 500-frame CLI runs for Road Rash, Powers Kingdom, and Lemmings. PERF-08 stays Partial. See `Performance.md` §8.4.
- **2026-11-06 - CORE-05 threaded-DSP analysis.** Measured the DSP share on Lemmings (USA): 0.141 ms/frame of ~1.0 ms/frame (~14%), with 3.2M DSP instructions, 441k loop calls, and ~16.8 instructions per running loop. A per-sample worker handoff would cost more than the ~0.19 µs of DSP work, and a one-sample-ahead speculative pipeline must validate every ARM write to FIFO buffers, FIFO descriptors, semaphore, DSP memory, and CPU-supplied coefficients, replaying on any change. No threaded path was shipped because it would be slower on the measured workload and risks FIQ/FIFO timing; CORE-05 remains Pending. See the CORE-05 item for the full analysis.
- **2026-11-05 - PERF-05 high-resolution VDLP copy.** High-resolution scanline DMA now reads the raw word backing span directly instead of four `IRam.Read16` calls per pixel, with the same lane and plane handling. Interleaved A/B on Lemmings measured high-res 2.42-2.47→1.30-1.43 ms/frame (about 1.8x) with an identical high-res frame hash; PERF-05 stays Partial pending rasterizer and Tier B work. See `Performance.md` §8.6.
- **2026-11-05 - Rewind capture stall and state-size increment.** Fixed the sub-second gameplay freezes caused by the default rewind history capturing a keyframe on the emulation thread every 120 frames. Rewind keyframes now use `CompressionLevel.Fastest` and unchecked transient CRCs, the memory section (version 4) stores only the live high-resolution RAM range, `StateCrc32` is slicing-by-8, and `RewindBuffer`/`StateWriter` pool their capture stream, state arrays, payload buffer, and compression scratch. The BIOS MD5 in the header is cached, and the frontend opens discs through the prefetch cache. On Lemmings the worst keyframe capture fell from **139.3 ms** to **7.4-9.2 ms** (one 14.1 ms outlier), allocations fell from ~1.5 GB to 16 MB per 1,200 frames, and the uncompressed stock-layout state fell from ~26 MB to ~10 MB. See `Performance.md` §8.5 and `SaveStates.md`.
- **2026-11-04 - SYS-07 memory capacity options.** Added the eight Opera DRAM/VRAM layouts (`21`, `22`, `41`, `42`, `81`, `82`, `E2`, `F1`) through `MemoryConfiguration`/`MemoryLayout`, dynamic RAM/VRAM decode, VRAM base and masks for VDLP/SPORT/CEL, capacity-derived MADAM RED SYSBITS, `--memory`/Settings/profile/state plumbing, memory state section v3, Options section v8, a homebrew probe ROM that uses 4 MB DRAM + 2 MB VRAM, and 34 memory tests. Stock hashes are unchanged; expanded memory is documented as homebrew-only.
- **2026-11-04 - SYS-08 clock matrix increment.** `bench --cpu-clock`/`--memory` measure the supported clock range; the 0.5x-4x matrix and unchanged stock default are recorded in `Performance.md` §8.1.
- **2026-11-05 - Library duplicate visibility.** The library identifies discs by the MD5 of their first two data sectors (the 4DO game ID), so renamed copies and GoodTools/Redump/No-Intro variants collapse to one entry. The header now reports `653 games · 1,366 images`, and **Settings > Paths > Show duplicate disc images** lists every file. `GameLibraryScanner.BeginScan`/`InspectPending`/`Scan` accept `deduplicate`, and `library verify` checks every image. Added scanner tests for duplicate listing and `ImageCount`.
- **2026-11-05 - Settings game folders and fast library listing.** Added a **Game folders** editor to **Settings > Paths**: type or paste one or more folder paths, press Enter or click Add, Browse, or Remove. The list persists in `LibraryFolders` and refreshes the library when it changes. `GameLibraryScanner` now builds a per-directory cover index instead of calling `File.Exists` for four extensions per disc, sibling folder, and cover folder, and `LibraryItem` decodes covers on bounded background tasks at 256 px so the non-virtualizing grid never blocks the list. On the local 2,732-image collection the cached listing dropped from 2.8 s to 0.33 s (86% of the old time was cover probes), and a cold listing now returns 2,732 placeholders in 17 ms before background metadata hashing and cover decoding. Added cover-index tests for cached entries and PNG-over-JPG precedence.
- **2026-11-05 - PERF-08 managed IL JIT (partial).** Added `Arm60.Jit.cs`, a `DynamicMethod` JIT behind `--cpu-backend jit` that compiles hot data-processing, load/store, and branch blocks. Blocks verify every instruction against current memory, stop at the machine's 16-cycle batch boundary, return after stores/branches, and fall back for unsupported instructions, SWIs, mode changes, register shifts, self-modifying code, and runtimes without dynamic code. Correctness: 100M-instruction lockstep parity on Lemmings and identical 3,000-frame Lemmings plus 1,000-frame Road Rash/Powers Kingdom hashes. Per-class microbenchmarks are 1.9-3.3x faster, but end-to-end boot is 1.17-1.85x slower, so PERF-08 remains Partial and the interpreter remains the default. See `Performance.md` §8.4.
- **2026-11-05 - PERF-04 prefetch buffer increment.** `PrefetchingDiscSource` now reuses evicted cache arrays through a bounded pool, keeps one worker buffer for 2048-byte data reads and one for 2352-byte audio reads, and clears the pool on dispose. Cache hits are allocation-free on the caller thread and covered by a 1,000-read test. Continuous miss/eviction streaming still needs an end-to-end allocation gate.
- **2026-11-05 - SYS-18 auto per-game profile completion.** Database `inputDevices` now drives `AppSettings.ResolveInputDevices` and CLI runs without `--input-device`; auto-input seeding no longer persists a shadowing override. The settings dialog applies one effective profile snapshot, updates input/filter/high-resolution live, and restarts only for BIOS/NVRAM/fix/timing changes. `GameProfile` gained a `CheatBundle` field and `Clone`. Evidence: WorldCup Super Stadium (JP) auto-selected FZ-10J + Kanji and rendered "Story of the World Cup"; Powers Kingdom Europe auto-selected Anvil/PAL1; Slayer USA auto-selected FZ-10; Crash 'n Burn USA applied fix bit 1; Crime Patrol auto-selected the lightgun and rendered its intro.
- **2026-11-05 - SYS-17 game database expansion.** Grew `GameDatabase.xml` from 518 to 653 records, added `revision`, `discNumber`/`discTotal`, `bios`/`bios2`, `fixMode`, `inputDevices`, `nvram`, and `cheats`, and wired `fixMode` into `GameCompatibility`, BIOS fields into `BiosLibrary`, and input fields into `GameControlDefaults`. Added `gamedb stats|scan|export|merge`, the filename parser, serial retry for transient CHD read failures, and `docs/GameDatabase.md`. `gamedb stats --folder "J:\Panasonic 3DO"` reports 1,366 readable images, 653 unique IDs, and 100% metadata coverage with no errors; the three former exceptions were audio-only `[CD-Audio]` CHDs, which now open and get TOC-derived IDs.
- **2026-11-05 - SYS-01 audio-only CHD increment.** `ChdDiscImage` no longer requires a data track, audio-only discs get a stable TOC-derived `GameId`, and `info` reports `audioCd`/`trackCount`. This let the game database catalogue the three local `[CD-Audio]` bonus discs. In-emulator playback still needs a disc with a data track or a guest CD player, so SYS-01 remains partial.
- **2026-11-05 - SYS-08 CPU clock validation.** Recorded the five-title 0.5x/1x/2x/4x A/B in `Performance.md` §8.3. Every non-stock clock changed the frame hash, and AutoBahn Tokio's wall time rose from 9.1 s at 1x to 84.4 s at 2x and 271.7 s at 4x. The stock 12.5 MHz default and the no-per-title-clock decision are unchanged.
- **2026-11-04 - SYS-12 Portfolio OS SWI HLE.** Ported Opera's 14 fixed-point math SWIs (`0x50000`-`0x50012`, with the object/Dot4 cases left to the guest), added `--swi-hle`/Settings/profile/state v9 plumbing, `--swi-comments` histograms, 33 CPU/machine tests that match `opera_fixedpoint_math.c`, and a real-BIOS synthetic 4x4 benchmark. Default-off hashes are unchanged; HLE is an opt-in performance mode and intentionally skips the guest handler's cycles and register clobbers, so some titles (AutoBahn Tokio, Ballz) change frame hashes.
- **2026-11-04 - FND-01 machine option registry.** Added `MachineOptionRegistry` (13 validated options with CLI/Settings/profile/state mappings), typed `MachineOptionValidationException`/`UnknownMachineOptionException`, Opera DIPIR and soft-reset controls, `--opera-dipir`/`--opera-soft-reset`/`--cpu-backend`, `settings options` generated documentation, `docs/MachineOptions.md`, save-state Options v7 coverage for every option, mismatch warnings, and CLI/Settings/profile parity tests.
- **2026-11-04 - Plan rewrite.** Consolidated the former dashboard, parts I-IV, feature spec, parity audit, and handoff into this single backlog; retired duplicate `BAK-*`/`MOON-*` trackers.
- **2026-11-06 — v1.2.2.** Six framework-dependent single-file bundles plus `SHA256SUMS.txt`; interpreter, high-resolution VDLP, and rewind performance; opt-in managed JIT; expanded game database; library health/DAT verification and duplicate image counts; eight Opera memory layouts; eight PBUS ports; opt-in Portfolio OS SWI HLE; audio-only CHD cataloging; 1,815 tests.
- **2026-11-03 — v1.2.1.** Six x64/arm64 bundles plus `SHA256SUMS.txt`; CI verified every apphost machine type and every checksum.
- **2026-11-03 — v1.2.0.** Direct3D 11 presenter, startup release checks, deterministic fuzz harness, per-edge overscan/void border, screenshot gallery filters/compare/export, display-refresh control, flat CHD/raw ISO detection, FIQ/DSP telemetry, CLIO/DIPIR/FIFO/MCTL/XBUS audit fixes.
- **2026-11-02 — Hardening pass.** Fuzz harness, overscan profiles, gallery features, FIQ request-to-vector latency, DSP per-tick statistics, 1,622 tests.
- **2026-11-01 — Disc/UX/accuracy pass.** CD-DA and multi-track, M3U/live swap, PAL/NTSC, BIOS manager, per-game NVRAM, rewind, fast-forward, cheats, TAS, disc browser, input devices, OSD/quick menu, library metadata, portable mode, instance lock, save-state format v3.
- **Earlier:** v1.0.0 documented-compatibility release; 594-disc sweep; legal/attribution sign-off; six-RID packaging; OpenGL/GLES, Vulkan, and Direct3D 11 presenters; 4K output; differential harness.

---

## Appendix A — Opera option coverage

This is a crosswalk, not a second backlog. "Canonical item" is the only place the remaining work is tracked. Rows updated in the 2026-11-06 rewrite reflect the plan's own completed evidence.

| Opera feature/option | 5DO status | Canonical item |
|---|---|---|
| BIOS (rom1), including embedded Opera Boot | Partial (retail BIOS); embedded boot blocked on licensing | SYS-05 |
| Font/Kanji ROM (rom2) | Shipped | — |
| CPU overclock 0.5×-4× | Shipped (experimental; not seeded into profiles) | SYS-08 (done) |
| Memory capacity 2/4/8/14/15 MB DRAM, 1/2 MB VRAM | Shipped | SYS-07 (done) |
| Random-number seed | Shipped | — |
| CD speed unbounded/1×-32× | Shipped | — |
| Region NTSC/PAL1/PAL2 | Partial | SYS-03 |
| VDLP pixel format 0RGB1555/RGB565/XRGB8888 | Missing | VID-16 |
| VDLP bypass CLUT | Shipped | — |
| HiRes CEL rendering | Partial (line-doubled) | VID-01 |
| MADAM matrix hardware/software | Shipped | — |
| OperaOS SWI HLE | Shipped (opt-in) | SYS-12 (done) |
| NVRAM storage per-game/shared | Shipped | — |
| NVRAM version 0-9 | Shipped | — |
| Active input devices 0-8 | Shipped (device cap); per-device evidence partial | SYS-15 |
| Hide lightgun crosshairs | Shipped | SYS-15 |
| Debug output (KPRINT) | Shipped | — |
| Joypad/flightstick/mouse/lightgun/arcade lightgun/trackball | Partial | SYS-15 |
| Up to eight input ports | Partial (ports 7-8 chain; six-player verification) | SYS-15, SYS-16 |
| Cycle-based CD timing, read-ahead, DIPIR, tagged status, chunks, subcode | Partial | CORE-04 |
| CD-DA/Red Book, ODE, multi-track TOC | Partial | SYS-01, SYS-02 |
| ARM N=2 cycle model | Conditional | CORE-01 |
| CEL busy pacing | Partial | CORE-03 |
| Per-timer CLIO scheduling | Partial | CORE-02 |
| Threaded DSP | Closed — not pursuing | CORE-05 |
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
| BAK-08 | Completed (PERF-02) |
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

**Priority changes in the 2026-11-06 rewrite (traceability):**

| ID | Before | After | Reason |
|---|---|---|---|
| TEST-07 | (did not exist; BAK-01/02/03 were dangling aliases) | P0, Phase 0 | The oracle is the prerequisite for every accuracy claim. |
| TEST-06 | P1 | P0, Phase 3 | The playability audit is the proof of "better than 4DO/Opera". |
| SYS-05 | P0 | Blocked, P2 | GPL-2.0 licensing; homebrew-only; no retail impact. |
| CORE-01 | P0 | Conditional P0 | The 17/40 vs 15/40 result is a two-title delta on a weak metric. |
| PERF-08 | P2 | Closed | Three passes; still ~1.25x slower than the interpreter on real titles. |
| CORE-05 | P1 | Closed | DSP is ~14% of frame time; handoff cost exceeds the work. |
| §3.5 | P1/P2/P3 backlog | Deferred | Product/performance work does not move the compatibility claim. |

**Dropped scope (do not add checkboxes):** WebAssembly/browser (ECO-02), Android/iOS (ECO-03), Raspberry Pi/arm64 GLES optimization (ECO-05), Steam Deck. The libretro core wrapper is not planned; 5DO stays standalone.

---

## Appendix C — Source-of-truth links

- [Status](Status.md) — short current answer.
- [Compatibility](Compatibility.md) — supported set and platform evidence.
- [Game compatibility results](GameCompatibilityResults.md) — per-disc sweep table.
- [Known issues](KnownIssues.md) — current limitations.
- [Performance](Performance.md) — budgets and measurements.
- [Test strategy](TestStrategy.md) and [Test corpus](TestCorpus.md) — test evidence.
- [Differential harness](DifferentialHarness.md) — Opera comparison.
- [Reference strategy](ReferenceStrategy.md) — Opera/4DO mapping.
- [High-resolution CEL research](HighResolutionCelResearch.md) — VID-01/02/03.
- [Release checklist](ReleaseChecklist.md) — release gate.
- [Porting notes](PortingNotes.md) — intentional divergences.
