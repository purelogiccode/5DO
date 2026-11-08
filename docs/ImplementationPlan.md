<a id="master-dashboard"></a>
# 5DO — Implementation Plan

**Snapshot:** 2026-11-08 (feasibility revision)
**Current release:** `1.5.0`
**Repository:** <https://github.com/purelogiccode/5DO><br>
**Branch:** `master`<br>
**Status:** **2,051 tests pass**; build, format, and InspectCode are clean. The local collection now inventories **1,371 disc images**, which collapse to **414 unique titles** and a **378-title game test set**. The new 3-way matrix matches **232 titles to 4DO** and **246 to Phoenix** (194 to both), with **0 loadability regressions**, **2 titles where 5DO is ahead of 4DO**, and **0 untested**; the four no-video results are secondary media/homebrew, not retail game regressions. Tier B batches 1-3 reviewed **75 titles** (54 `Good`, 21 `Playable`); **339 titles** still have automated loadability only. The remaining `Behind` rows are **17 vs 4DO / 20 vs Phoenix**, all explicit and documented. The 50-title accuracy panel keeps **50/50 frame hashes** and **150/150 audio hashes** at frames 60/120/300. The 594-disc legacy sweep remains **593 rendered, 1 known failure** (Crash 'n Burn Japan). Full evidence: `docs/CompatibilityComparison.md`, `docs/DifferentialHarness.md` §8, `docs/GameCompatibilityResults.md`.
**Goal:** beat 4DO and Phoenix on **measured, player-visible compatibility**, and remain the best standalone desktop 3DO emulator on Windows, Linux, and macOS.<br>
**Plan shape:** **three phases, one corpus, one comparison, four gates.** Research, product, and speculative timing work are out of scope until the compatibility gate is green.

> **2026-11-08 feasibility revision.** The previous plan carried 49 open IDs, eight gates, and an 800-850 developer-day backlog. That is not a plan; it is a wish list. This revision keeps every stable ID for traceability, but only **nine backlog entries** are allowed to block a release. Everything else is explicitly deferred or closed in §3.4, with a revisit condition. The full per-image differential is replaced by the **414-title unique corpus**; the open-ended timing audit is replaced by a **time-boxed residual** that must show a game-visible effect to stay open.

> **This is the single source of truth.** It replaces the former dashboard, milestone log, feature spec, parity audit, and session handoff. Historical evidence stays in the linked docs; this file is the canonical backlog.

---

## 1. How to use this plan

1. **Work the phases in order.** Phase 1 is the only compatibility phase; do not start Phase 2 before every unique title has a status, and do not start Phase 3 before the peer comparison has no unexplained regression.
2. **The unique corpus is the test set.** Compatibility claims are made on the **378-title game test set**, not on 1,371 regional copies. The image inventory is for loadability and duplicate accounting only.
3. **One feature per change, tests first.** Every behavior change needs a unit, golden, differential, or scripted test. State the reference: Opera, 4DO, hardware documentation, or a 5DO decision.
4. **Evidence before checkbox.** Mark an item done only when its "Done when" line is satisfied and the evidence is committed or linked.
5. **No work without a player-visible outcome.** If a task cannot change a game's status, a gate, or a release artifact, it belongs in §3.4, not in the backlog.
6. **Timing work is conditional.** Open `CORE-02`/`CORE-03`/`CORE-06`/`CORE-07` only when a matrix regression or a panel divergence points at that subsystem. A trace difference with no game-visible effect is documentation, not a release blocker.
7. **No default timing change without panel evidence.** A 40-title, 300-frame sample is a smoke test, not a gate.

### Legend

- `[ ]` — remaining work. `[x]` is used only in the completed log.
- **Pending** — not started, or research/design only.
- **Partial** — a usable subset ships; the remaining scope is listed in the item.
- **Conditional** — work only if the measurement panel proves it changes a game-visible outcome.
- **Deferred** — explicitly out of scope for this plan; see §3.4.
- **Closed** — a recorded decision not to pursue the work; it is not "done".
- **Priority** — **P0** correctness/compatibility, **P1** release evidence, **P2/P3** post-compatibility.
- **Effort** — **S** under 3 developer-days, **M** 3-10, **L** 10-30, **XL** 30-90+.

---

## 2. Current state

### 2.1 Verified snapshot (2026-11-08)

| Gate | Result |
|---|---|
| Build | `dotnet build 5DO.sln -c Debug` - **0 warnings, 0 errors** |
| Tests | **2,051 / 2,051 passed** in Debug |
| Format | `dotnet format --verify-no-changes` - clean |
| Static analysis | `jb inspectcode --severity=WARNING` - **0 results** |
| CLI | `5DO 1.5.0 (FiveDo)`; `selftest --json` - all checks `ok: true` |
| Corpus inventory | **1,371 images**, **653 distinct game IDs**, **414 unique titles**, **378 game test set** |
| 3-way comparison | 4DO **232** matched, Phoenix **246** matched, both **194**; **0 regressions**, **2 ahead of 4DO**, **17 `Behind` vs 4DO / 20 vs Phoenix** (75 reviewed titles, all explicit) |
| 50-title panel | **50/50 captured, 0 errors**; **50/50 frame hashes** at 60/120/300; **150/150 audio hashes** |
| Scripted input | 5 titles, 900 frames; **5/5 frame hashes** at 60/300/600; input changes guest output |
| 3,000-frame sample | 4/5 video matches; Road Rash still diverges; first behavioral difference is CD cadence |
| Legacy sweep | **594 discs, 593 rendered, 1 known failure** (Crash 'n Burn Japan) |
| Visual/input validation | **12 titles** in the curated matrix; a handful have scripted gameplay |

The corpus and comparison numbers come from `artifacts/corpus/inventory.json` and `docs/data/compatibility-comparison.json`; the `J:` corpus is not mounted in every session. Re-run §7 before quoting them.

### 2.2 What "better than 4DO and Phoenix" means

"Better" is not a feature list. It is measured on the **same BIOS, same unique titles, and same input scripts**:

| Claim | Evidence required |
|---|---|
| Better coverage | All **378** unique games have a dated 5DO status; every image-only exception is explained. |
| No regressions | **Zero** titles where 4DO or Phoenix loads/plays and 5DO does not; every remaining gap has a root cause. |
| Better playability | The **top 50** titles are **100% reaching gameplay** and **≥95% Good or Playable**. |
| Better accuracy | The 50-title panel has no video or audio divergence; remaining CD cadence differences are documented and do not change the player-visible path. |
| Better disc support | Only the matrix titles that need CD-DA, multi-track, or multi-disc are verified; not a speculative full-collection program. |
| Better input | The matrix titles that need mouse, lightgun, arcade lightgun, flightstick, or trackball are verified. |
| Better product | Save states/rewind, CHD, library, per-game profiles, 4K output, and six-platform packaging stay shipped; new product work is deferred. |

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

1. **Loadability is not playability.** The matrix has **75 manual gameplay grades** and **339 automated loadability results**, with **0 untested**; the four `No video` rows are secondary media/homebrew. Tier B has reviewed 75 titles; the rest still need review.
2. **The peer comparison is now explicit, not unknown.** After Tier B batches 1-3, **75 titles have a manual grade** and **17 rows remain `Behind` vs 4DO / 20 vs Phoenix**. Every one has a recorded reason (lightgun menu, media disc, instructional disc, or a missing title-specific script); none is an unexplained loading failure.
3. **The CD cadence residual is still open.** Eleven 50-title panel titles diverge in command cadence at frame 300; video and audio still match. It is a diagnostic difference until a title shows a player-visible effect.
4. **Platform evidence is asymmetric.** Windows and Linux are validated; macOS is documented best-effort. That is acceptable for the compatibility claim but must be stated in the release notes.

**Honest current claim:** 5DO is a better *product* than 4DO, a credible challenger to Phoenix, and — on the tested panel — video/audio-accurate. It is **not yet a proven playability win**. Phase 1 exists to settle that.

### 2.5 Completed IDs (traceability only)

| ID | What shipped | Evidence |
|---|---|---|
| TEST-08 | Unique-corpus inventory and 5DO/4DO/Phoenix comparison: 1,371 images collapsed to 414 titles by game ID, canonical test-set selection, peer matching, regression/ahead verdicts, and committed JSON/CSV | `CompatibilityComparison.md`; `data/unique-corpus.csv`; `data/compatibility-comparison.json`; `tools/corpus-inventory.py`, `tools/generate-compat-comparison.py`. Log 2026-11-08. |
| TEST-07 | Reference oracle: fixed-seed deterministic captures, PC/CPSR/instructions, CD cycle/PC/instruction stamps, transcript and cadence classification, timer/read/register traces, instruction-phase triage, audio hash gating with sample alignment, scripted input, and the nightly baseline gate | `DifferentialHarness.md` §4.1/§4.2/§6/§8; `TestCorpus.md` 1.1bd; `test93-panel50-audio`, `test94-samples5-input`; 2,051 tests. Log 2026-11-08. |
| SYS-06 | RSA/unsigned-disc option; two-site in-memory patch; no BIOS or game content bundled | `BiosRsaPatch`; known No-RSA MD5s reproduced; signed-title hashes unchanged. Log 2026-11-06. |
| SYS-08 | CPU clock matrix 0.5x-4x; stock 12.5 MHz default unchanged; non-stock clocks are a compatibility variable | `Performance.md` §8.1/§8.3; five-title A/B. Log 2026-11-05. |
| SYS-12 | Portfolio OS SWI HLE (14 SWIs, opt-in); per-comment histograms | `ArmSwiHleTests`; 847,860 LLE steps vs 24,578 HLE; default-off hashes unchanged. Log 2026-11-04. |
| SYS-17 | Game database 653 records; 1,366 images; 100% metadata coverage; `gamedb scan/export/merge` | `GameDatabase.md`; 12 storage/host/CLI tests. Log 2026-11-05. |
| SYS-18 | Auto per-game profile; BIOS/region/fix/input applied with zero manual steps | Five-title evidence (WorldCup, Powers Kingdom, Slayer, Crash 'n Burn, Crime Patrol). Log 2026-11-05. |
| UX-26 | Online cover catalog: 473 unique WebP covers, 645/653 game IDs mapped, catalog + on-demand + 27 MB ZIP, annotation-stripped Jaro-Winkler fallback | `CoverArt.md`; 14 host tests; packager catalog reload/hash verification. Log 2026-11-07. |
| VID-04 | 480i/240p detection; bob/weave/blend; interlace-aware geometry; screenshots/GIF | `InterlaceProcessor`; 16 tests; five-title bob-vs-auto hashes. Log 2026-11-06. |

**Full completed-ID index (including the nine above):** FND-01, FND-02, FND-03, CORE-09, CORE-12, CORE-14, CORE-15, SYS-04, SYS-06, SYS-07, SYS-08, SYS-09, SYS-10, SYS-11, SYS-12, SYS-13, SYS-14, VID-04, VID-05, VID-06, VID-14, VID-15, AUD-05, TEST-07, TEST-08, UX-01, UX-02, UX-04, UX-11, UX-12, UX-15, UX-19, UX-23, UX-26, ENG-03, PERF-02, VID-12, BAK-12, BAK-17.

**Milestone aliases:** M9R/M9P → P3/P6/TEST-04; M10 → PERF-02; M12 → PERF-08; M13.5/M13.6 → TEST-06; M14 → VID-01/02/03; M15 → CORE-11/16 + FND-05.

---

## 3. Missing features — canonical backlog

Each entry is a single feature or a tightly-scoped bundle. **No feature appears in more than one category.** The backlog is deliberately short: everything that does not block Gates A-D lives in §3.4.

### 3.1 P0 — Playability proof

- [ ] **TEST-06 — Playability audit on the unique corpus**
  **Status:** Partial · **Priority:** P0 · **Effort:** L
  **Remaining:**
  - **Tier A (all 378 unique games): done.** Every unique title has a 5DO status: **371 automated loadability results, 43 manual grades, 0 untested**. The 44 previously untested images now have a status; the four `No video` results are secondary media/homebrew, not retail games.
  - **Tier B (target ~100 titles):** every title where 4DO or Phoenix reports `Playable`/`Good` and 5DO differs, plus every `Behind`/`Regression` row; run a 10-minute scripted input pass and record a checkpoint screenshot.
  - **Tier C (top 50):** full gameplay review to `Good`/`Playable`/`Unplayable` with a reason and evidence.
  **Done when:** all 378 games have a reviewed status; Tier B has zero unexplained regressions; Tier C is ≥95% Good/Playable; the matrix and per-game database are updated.
  **Evidence (2026-11-08 batch 1):** the ten comparison diffs were run through a 10-minute 5DO-only scripted pass (`artifacts/tierb-behind-clean`, `benchmarks/diff/tierb-behind.json`). Five reached and sustained gameplay (**Battle Chess**, **Crash 'n Burn**, **Myst**, **Olympic Summer Games**, **Orbatak**) and were promoted to `Good`; the other five stay `Playable` pending title-specific scripts (Crime Patrol, Mad Dog II, Guardian War, Shootout at Old Tucson, Slayer).
  **Evidence (2026-11-08 batch 2):** 32 titles where both 4DO and Phoenix claim `Playable`/`Good` were reviewed with `--bios-auto`; **25 were promoted to `Good`** and **7 kept `Playable`** (20th Century Almanac, BladeForce, Carrier, Club 3DO, DeathKeep, Dennis Miller, Digital Dreamware). The ledger is `docs/data/playability-grades.csv`.
  **Evidence (2026-11-08 batch 3):** 32 more peer-flagged titles were run with `--bios-auto`; **24 were promoted to `Good`** and **8 kept `Playable`** (ESPN Golf, Family Feud, Hell, Insect War, John Madden Football, Jurassic Park Interactive, Kingdom, Life Stage). An 18,000-frame audio-hash pass produced non-zero hashes and `dspRunning` on all 32. The comparison now has **75 reviewed titles** (54 `Good`, 21 `Playable`), **339 automated loadability only**, and **17/20 explicit `Behind` rows**. See `TestCorpus.md` §1.1bk.
  **Why first:** this is the only evidence that answers "better than 4DO and Phoenix" for a player. The old 594-title "renders" count does not.
  **Ref:** `docs/CompatibilityComparison.md`; `docs/Compatibility.md` §10/§13.

- [ ] **CORE-13 — Matrix-driven compatibility fixes**
  **Status:** Partial · **Priority:** P0 · **Effort:** L
  **Remaining:** review every title that `CompatibilityComparison.md` marks `Behind`; fix or re-grade it with evidence; fix Crash 'n Burn (Japan); retry any Tier A `No video` that is a retail title; add a regression test or matrix note for each change.
  **Done when:** the matrix has no unexplained regression; every remaining exception has a root cause and a documented workaround; Tier B is clean.
  **Rule:** do not add speculative per-title fixes. Every fix starts from a matrix row.
  **Ref:** `References/4DO-master/FourDO/Emulation/GameSource/GameDatabase.xml`; `docs/GameCompatibilityResults.md`.

- [ ] **CORE-11/16 + FND-05 — Regression harness on the unique corpus**
  **Status:** Partial · **Priority:** P0 · **Effort:** M
  **Remaining:** keep the 50-title panel green; add the 378-title loadability set to the weekly run; keep the image inventory as the duplicate/region record; publish a nightly dashboard with frame/audio/CD fields; document every allowance with a date.
  **Done when:** the nightly run has no unexplained divergence, and every allowance is documented and dated.
  **Rule:** a frame-hash match without PC/CD/audio evidence is a smoke result, not a pass.
  **Evidence (2026-11-08):** the audio-gated 50-title panel (`test93-panel50-audio/report.json`) captures **50/50 with 0 errors**, **50/50 frame hashes**, and **150/150 audio hashes** at 60/120/300; it leaves **39/50 phase-only** and **11 CD-only first behavioral divergences at f=300**. The same-frame retired-instruction delta is **11-20** at f=60, `-46..2,164` at f=120, and `-36,104..3,591` at f=300; **50/50 first PC/CPSR divergences are same-frame phase**. See `DifferentialHarness.md` §8.

### 3.2 P0 — Accuracy, time-boxed

- [ ] **CORE-04 — CD-ROM command and timing residual**
  **Status:** Partial · **Priority:** P0 · **Effort:** M (time-boxed)
  **Remaining:** close the 20th Century guest read-schedule residual or document it as a guest-scheduling difference with no video/audio impact; keep the command/status/metadata transcripts green; verify CD-DA and multi-disc only on matrix titles that need them.
  **Done when:** the 50-title panel has no video/audio divergence; every remaining CD cadence difference is documented with its first mismatch and its player-visible impact; no 594-sweep regression.
  **Why time-boxed:** the command table, status bytes, READ_ID payload, sector-time formula, FIQ delivery, and timer period already match. The residual is guest software pacing, not a CD register-contract bug.
  **Evidence (2026-11-08 increment 4):** the 5DO timer trace now emits the reference's FIQ/XBUS event kinds (`fiqgen`, `fiqwrite`, `xbussel`, `xbuspoll`, `xbusfiq` with source 1-5). On 20th Century both engines read the active-FIQ register **exactly 99 times** with identical masks. 5DO leaves the OS idle loop on the VBlank FIQ at **59,021,271** and issues `READ_DATA` at **59,063,402**; Opera stays idle until `READ_ID` at **70,080,576**. The periodic `READ_ID` cadence matches within **+0.21M cycles**. The residual is the guest file-system read-request schedule. **2,051 tests pass.**
  **Ref:** `opera_cdrom.c`; `docs/ReferenceStrategy.md` §6.

- [ ] **CORE-02 / CORE-03 / CORE-06 / CORE-07 — Conditional timing evidence**
  **Status:** Conditional · **Priority:** P1 · **Effort:** S each
  **Remaining:** open only when a matrix regression or a panel divergence points at CLIO timers, CEL busy, SPORT/XBUS/DMA, or FIQ/IRQ ordering. Record the trace, the game-visible effect, and the fix. Otherwise leave the subsystem at its measured-parity state and cite the evidence in `DifferentialHarness.md` §8.
  **Done when:** each item is either closed with reference evidence or promoted to P0 with a named title that changes status.
  **Evidence:** timer 14 is 12,384 cycles in Opera and 12,396 in 5DO; the Lemmings READ_ID-to-READ_ERROR residual is guest copy/decrypt work; the XBUS CD FIQ is level-held like the reference. `CORE-01` is closed (see §3.5).

- [ ] **AUD-01 — Audio output quality, reduced scope**
  **Status:** Partial · **Priority:** P1 · **Effort:** S
  **Remaining:** one 10-minute real-time soak on Windows and Linux with the null device and one real device; confirm 0 dropped samples/underruns; keep the existing sync-offset and device-recreation tests.
  **Done when:** the soak is clean on both platforms and the result is recorded in `Audio.md`.
  **Evidence:** `AudioSyncMilliseconds` applies a fixed host offset; `OpenAlAudioOutput` recreates output safely and exposes drop/underrun/latency counters; a 240-frame AOT null-device run reported 0 drops and 0 underruns.

### 3.3 P1 — Release blockers (not accuracy blockers)

- [ ] **ECO-04 — Windows and Linux release validation**
  **Status:** Partial · **Priority:** P0 for release · **Effort:** M
  **Remaining:** Windows x64/arm64 bundle smoke; Linux x64 (Hyper-V `LinuxMint`) full suite, CLI smoke, frontend `--smoke-test`, and a 3,000-frame disc boot; macOS x64/arm64 build validation with GPU presentation documented as best-effort.
  **Done when:** Windows and Linux pass the release gate; macOS builds and is documented; the six bundles are published with `SHA256SUMS.txt`.
  **Note:** this blocks the release gate, not the compatibility claim.

- [ ] **SYS-03 / SYS-15 / SYS-01 / SYS-02 — Conditional disc and input coverage**
  **Status:** Conditional · **Priority:** P1 · **Effort:** S each
  **Remaining:** run only the matrix titles that need PAL, lightgun/arcade/flightstick/trackball input, CD-DA, or multi-disc. Record the title, BIOS, input script, and result. Do not build a speculative full-library program.
  **Done when:** every matrix title with that requirement is verified or documented as a known limitation.

- [ ] **ENG-01/09 — Minimal release CI**
  **Status:** Partial · **Priority:** P1 · **Effort:** S
  **Remaining:** keep `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`, and `jb inspectcode --severity=WARNING` green on Windows; run the docs JSON `--check`; publish the six bundles and checksums. SBOM and reproducible-build work are release-note items, not blockers.
  **Done when:** every release candidate passes the four commands and publishes the bundles.

### 3.4 Deferred and cut (not release blockers)

These items are explicitly **not** part of this plan. They stay in the repository's history and stable-ID index; they do not block Gates A-D. Reopen only under the stated condition.

| ID or area | Decision | Why | Reopen if |
|---|---|---|---|
| CORE-01 ARM60 cycle-class | **Closed** | The 50-title panel proved the startup difference is frame-boundary phase, not a cycle model; no game-visible effect. | A title's frame hash changes with the ARM timing model and no other cause explains it. |
| CORE-10 hardware test ROM suite | **Dropped** | The differential oracle, 2,051 unit tests, and fuzz targets already catch the regressions a ROM suite would. | A hardware-only defect appears that the oracle cannot reproduce. |
| AUD-04 audio golden corpus | **Folded** into TEST-07 | The cumulative audio hash at aligned sample counts is the regression gate. | A waveform defect passes the hash gate. |
| VID-13 video test suite | **Folded** into TEST-07 + the 12-title visual matrix | Frame hashes plus the curated visual matrix cover the same claim. | A color/presenter defect passes both. |
| TEST-01 save-state migration matrix | **Reduced** to current-version round-trip | `selftest` already round-trips every profile; old state versions are not a supported product surface. | A user reports a state that the loader mishandles. |
| TEST-04 GPU/platform CI matrix | **Reduced** to one Windows + one Linux smoke per release | A full GL/GLES/Vulkan/D3D11 matrix costs more than the compatibility claim needs. | A platform-specific presenter regression escapes the smoke. |
| TEST-05 performance regression matrix | **Reduced** to `bench --gate` in CI | The measured frame budget is stable; a trend dashboard is product work. | A release shows a >10% regression on a real title. |
| SYS-05 embedded boot ROM | **Closed** | GPL-2.0-only licensing risk; retail titles still need a user BIOS, so it does not change the compatibility claim. | Legal sign-off and a distributable ROM exist. |
| SYS-16 six-player/multi-tap | **Deferred** | No matrix title requires it; hardware is unavailable. | A matrix title requires six players. |
| PERF-01/04/05/06/07, ENG-02 | **Deferred** | No measured gate failure; performance is already inside the 60 fps budget on the reference machine. | A measured Tier A/B budget miss or a real-title stall. |
| UX-03/05/06/07/20/21/22/24 | **Deferred** | Product work; does not change compatibility. | Gate B is green and a product roadmap is opened. |
| VID-01/02/03/07/08/11/16/17 | **Deferred** | Research/presentation work; 4DO and Phoenix do not require it. | Gate B is green and a visual-quality program is opened. |
| AUD-03, TEST-02, ECO-09 | **Deferred** | Debugger/UI/CD-player work; not a release blocker. | Post-compatibility roadmap. |
| Full 594/1,371-image differential | **Replaced** by the 414-title corpus | Regional copies do not add compatibility signal; they add hours and noise. | A region-specific defect is suspected. |
| Top-100 audit | **Replaced** by Tier A 378 + Tier B ~100 + Tier C 50 | The corpus has 378 games, not 594; Tier B already covers every peer-flagged title. | The matrix shows a blind spot outside Tier B. |

### 3.5 Closed decisions (not backlog)

These are recorded decisions, not completed work. Do not reopen them without new measurement.

| ID | Decision | Evidence | Revisit only if |
|---|---|---|---|
| PERF-08 | **Stop investment** in the managed ARM JIT. Keep `--cpu-backend jit` as an experimental, opt-in backend; the interpreter remains the default and the accuracy oracle. | Three passes. Best result: Lemmings **1.23-1.37 ms/frame** vs **1.00-1.02** for the interpreter (~1.25x slower), despite a synthetic win (0.89-0.99 vs 1.20-1.30). ~24% of instructions still fall back. Hashes/instructions match. `Performance.md` §8.4. | A measured 1.5x end-to-end win on a real title, or a platform where the interpreter cannot hit 60 fps. |
| CORE-05 | **Do not implement** the threaded DSP. The handoff and validation cost exceeds the work it would offload. | DSP share on Lemmings: **0.141 ms/frame (~14%)**. A per-sample worker is slower; a speculative pipeline must validate every ARM write and replay on change. `Performance.md` §8.5. | A title where DSP exceeds ~30% of frame time, or a bit-identical speculative pipeline measures a real win. |
| CORE-01 | **Close** the ARM60 cycle-class mode as a compatibility item. Keep the existing `--arm-timing arm60` option as an experimental switch. | The frame-boundary phase fix removed the apparent one-field offset; the panel shows same-frame phase, not a cycle-model gap. | A title changes status with `--arm-timing arm60` and no other cause explains it. |

---

## 4. Suggested execution order

This is the phased order. The IDs are the canonical entries in §3.

| Phase | Window | IDs | Exit gate |
|---|---|---|---|
| **0 — Corpus** | Done | TEST-08 | 1,371 images → 414 titles; 378-game test set; 4DO/Phoenix comparison committed. |
| **1 — Playability** | Weeks 1-6 | TEST-06, CORE-13, CORE-11/16 | All 378 classified; zero unexplained regressions; top 50 ≥95% Good/Playable. |
| **2 — Accuracy** | Weeks 7-8 | CORE-04; CORE-02/03/06/07 conditional; AUD-01 | 50-title panel clean; every CD/timing residual documented with player-visible impact. |
| **3 — Release** | Weeks 9-10 | ECO-04, ENG-01/09; SYS-01/02/03/15 conditional | Windows + Linux passed; six bundles published; docs current. |

**If you only do three things:** `TEST-06`, `CORE-13`, and `CORE-11/16`. They produce the playability proof, the fixes that proof drives, and the regression signal that keeps both honest.

**Rule:** do not start Phase 3 while any Phase 1 P0 item is open. Do not start §3.4 work at all until Gate B is green.

### 4.1 Effort reality

By the plan's own S/M/L/XL scale, the **blocking path through Gate D is roughly 60-80 developer-days**, down from 240-850 in the previous plan. The savings come from the unique corpus, the folded evidence items, and the explicit cut list — not from lowering the bar.

- A 378-title loadability pass is ~1 hour at the current CLI speed.
- A Tier B 10-minute scripted pass is ~2-3 hours of emulated runtime for ~100 titles, plus visual review.
- Tier C is 50 titles × review; budget 1-2 weeks even with automation.
- The capture, audit, and hardware half does not compress with AI assistance.

A realistic wall-clock target for Gate D is **8-12 weeks** at the project's recent AI-assisted velocity, with Tier C review as the bottleneck. The deferred list is intentionally large; it is the post-compatibility product roadmap, not this plan.

---

## 5. "Best 3DO emulator" gates

These are the release/quality gates for the goal, not a second backlog. They reference canonical IDs only. A gate is green only when the evidence is committed, reproducible, and dated.

| Gate | Required evidence | Required IDs |
|---|---|---|
| **A. Corpus** | Unique-corpus inventory and canonical test set; 4DO and Phoenix matching; summary counts and unmatched list | TEST-08 |
| **B. Compatibility** | All 378 games classified; **zero unexplained regressions**; top 50 **100% reach gameplay**, **≥95% Good/Playable**; every exception documented | TEST-06, CORE-13, CORE-11/16; SYS-01/02/03/15 conditional |
| **C. Accuracy** | 50-title panel: no video or audio divergence; 11 CD-cadence divergences documented; no unexplained PC/CPSR state divergence; audio soak clean | CORE-04, CORE-11/16, AUD-01; CORE-02/03/06/07 conditional |
| **D. Release** | Build/test/format/InspectCode clean; Windows and Linux smoke; six bundles and `SHA256SUMS.txt`; docs current | ECO-04, ENG-01/09 |

**Release rule:** Gates A-D must be green. There is no Gate 5-8. Performance, platforms beyond Windows/Linux, product, and research are **not** release blockers for the compatibility claim; they are post-compatibility roadmap items.

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
7. **Do not ship copyrighted BIOS/game content.** Embedded boot ROM work is closed (`SYS-05`); retail titles still need a user-supplied BIOS.
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

# Unique corpus inventory and 3-way comparison (TEST-08)
python tools/corpus-inventory.py --jobs 8 --out artifacts/corpus/inventory.json
python tools/generate-compat-comparison.py --inventory artifacts/corpus/inventory.json --sweep artifacts/compat-full/summary-merged.csv --sweep artifacts/compat-unique/summary.csv --reference docs/GameCompatibilityList.md

# Compatibility sweep (full images for loadability; unique test set for the matrix)
pwsh -NoProfile -File tools/compat-sweep.ps1 -Roots "J:\Panasonic 3DO","G:\MAME\MAME Software List CHDs\3do" -Frames 3000 -Resume
pwsh -NoProfile -File tools/compat-sweep.ps1 -FileList artifacts/corpus/unique-testset.txt -Frames 3000 -Resume -OutDir artifacts/compat-unique

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
10. **Record closures.** A "won't do" decision needs a measurement and a revisit condition, like §3.5.
11. **Refresh the snapshot monthly** or after every release: version, test count, compatibility counts, performance numbers, and platform validation status.
12. **Keep the cross-document anchors stable.** `#2-current-state`, `#3-missing-features-canonical-backlog`, `#4-suggested-execution-order`, `#7-verification-commands`, and `#9-completed-log` are linked from `NextSession.md`.

---

## 9. Completed log

Append new entries here; do not delete history. Each entry is a completed canonical item or a release milestone.

- **2026-11-08 - TEST-06 Tier B batch 3 (32 peer-flagged titles).** Generated `benchmarks/diff/tierb-panel-3.json` and ran the 36,000-frame `--bios-auto` pass. All 32 completed; **24 promoted to `Good`** and **8 kept `Playable`** (ESPN Golf, Family Feud, Hell, Insect War, John Madden Football, Jurassic Park Interactive, Kingdom, Life Stage). An 18,000-frame audio-hash pass produced non-zero hashes and `dspRunning` on all 32. The comparison now has **75 reviewed titles** (54 `Good`, 21 `Playable`), **339 automated loadability only**, and **17/20 explicit `Behind` rows**. See `TestCorpus.md` §1.1bk.

- **2026-11-08 - TEST-06 Tier B batch 2 (32 peer-flagged titles).** Generated the next batch with `tools/build-tierb-manifest.py` (both 4DO and Phoenix claim `Playable`/`Good`, 5DO had only loadability). All 32 completed a 36,000-frame `--bios-auto` pass; **25 were promoted to `Good`** and **7 kept `Playable`** with recorded reasons (20th Century Almanac, BladeForce, Carrier, Club 3DO, DeathKeep, Dennis Miller, Digital Dreamware). An 18,000-frame audio-hash pass produced non-zero hashes and `dspRunning` on all 32; 13,243,230 samples each (Defcon 5: 15,876,000). Added the Tier B grade ledger `docs/data/playability-grades.csv` and the contact-sheet tool. The comparison now has **43 reviewed titles** (30 `Good`, 13 `Playable`) and **10/12 explicit `Behind` rows**. See `TestCorpus.md` §1.1bj.

- **2026-11-08 - TEST-06 Tier B batch 1 (10 comparison diffs).** Ran the ten titles where 5DO differed from 4DO or Phoenix through a 5DO-only 36,000-frame scripted pass (`tools/playability-sweep.py`, `benchmarks/diff/tierb-behind.json`, `benchmarks/diff/scripts/tierb-10min.txt`) with captures at 600/3,600/18,000/36,000. Five titles reached and sustained gameplay and were promoted to `Good`: Battle Chess, Crash 'n Burn, Myst, Olympic Summer Games, and Orbatak. The five promoted titles were re-run on stock BIOS behavior with **13,243,230 samples** and non-zero audio hashes; Crash 'n Burn's 18,000-frame WAV is non-silent (peak 7,631, RMS 1,190, 0 clipped). Crime Patrol, Mad Dog II, Guardian War, Shootout at Old Tucson, and Slayer remain `Playable` pending title-specific scripts. The comparison moved from **8 `Behind` vs 4DO / 10 vs Phoenix** to **4 vs 4DO / 5 vs Phoenix**, with 0 loadability regressions. See `TestCorpus.md` §1.1bi.

- **2026-11-08 - TEST-08 unique-corpus inventory and 5DO/4DO/Phoenix comparison.** Added `tools/corpus-inventory.py`, which fingerprints every local image with the 5DO `gameId` (MD5 of the first two 2048-byte sectors) and reads the database metadata, and `tools/generate-compat-comparison.py`, which collapses identical images, groups regional revisions, chooses a canonical disc, parses both saved compatibility lists, and writes the comparison. On the current `J:`/`G:` collection the inventory finds **1,371 images**, **653 distinct game IDs**, and **414 unique titles**, including a **378-title game test set**; **232 titles match 4DO** and **246 match Phoenix** (194 both). A 44-disc gap-fill pass with `-AllowUnsigned` classified the remaining titles: **40 rendered**, **4 no video** (all secondary media/homebrew), so **0 titles are untested**. The matrix finds **0 loadability regressions**, **2 titles where 5DO is ahead of 4DO** (Orbatak and Shootout at Old Tucson), **8 `Behind` rows vs 4DO**, and **10 vs Phoenix** (all manual `Playable` vs peer `Good`, not failures). `docs/CompatibilityComparison.md`, `docs/data/unique-corpus.csv`, and `docs/data/compatibility-comparison.json` are committed. `tools/compat-sweep.ps1` gained `-FileList` and `-AllowUnsigned` for the unique test set and homebrew images. **2,051 tests pass**; build and format are clean.

- **2026-11-08 - CORE-04/CORE-07 guest wake-source parity and 20th Century read schedule.** The 5DO `--clio-timer-trace` now emits the reference debug core's FIQ/XBUS events: `event=fiqgen` (CD bit-4 edge only), `event=fiqwrite` for `0x40`-`0x6c`, `event=xbussel`/`event=xbuspoll`, and `event=xbusfiq` with the reference source codes (1 tick, 2 poll tick, 3 built-in command, 4 device command/poll, 5 built-in poll); two new `ClioTests` and an `XbusTests` tick-source test cover the names and the edge rule. The probe (`artifacts/diff/probe-20th-wake`, 20th Century, 290 frames) shows both engines read the active-FIQ register **exactly 99 times** during the 57.8M-59.0M wait with identical masks (38x `00000001`, 33x `80000001`, 23x `80000801`, 5x `80000003`). The guest is in the Portfolio OS idle loop at `0x205f4..0x20600`; 5DO leaves it on the VBlank FIQ at **59,021,271** and issues `READ_DATA` at **59,063,402**, while Opera stays idle until its `READ_ID` at **70,080,576**. On 900-frame captures (`artifacts/diff/probe-20th-wake-900`) the periodic `READ_ID` cadence then matches (Opera 70.08M/82.59M/95.11M/107.62M/120.13M/132.64M; 5DO 70.29M/82.80M/95.31M/107.83M/120.34M/132.85M, **+0.21M phase**) and the divergence is 5DO's `READ_DATA` bursts between polls (59.06M/59.12M/61.56M/64.07M/66.57M/69.07M). The `READ_ID` payload is identical (`83 00 10 00 01 00 00 00 00 00 00 e3`). FIQ delivery, masks, timer scheduling, SPORT, the command table, status bytes, and the ID payload are therefore not the residual; the guest file-system read-request schedule is. The next measurement is the FS read request and the CD task wake queue. **2,051 tests pass**; build and format are clean.

- **2026-11-08 - CORE-04 CD register trace and status/metadata parity.** Added reference-side recording of every CD command byte, poll write/read, status read, and data read with cycle and PC (`sg_debug_cd_register_*`), harness `--cd-register-trace` output (`fivedo.cdreg.txt`/`opera.cdreg.txt`), and 5DO `datr` tracing for non-empty data reads. On 20th Century Video Almanac (290 frames, unbounded speed) the command packets, status bytes, and 2048-byte sector data are **identical through command 105**, the last command before the 5DO `READ_DATA`/Opera `READ_ID` sequence divergence. The trace exposed four reference divergences now fixed: the XBUS 2x status bit (`MEI_CDROM_DOUBLE_SPEED` is `0x80`, and `cdrom_mode_reset` starts at double speed; the guest mode byte is stored separately and re-applied on inject); `EffectiveSpeed` follows `cdrom_effective_speed` (unbounded stays unbounded, configured >1x drops to 1x while the guest selects single speed); the readable-disc limit is the 3DO volume header's `volume_block_count` (sector 0 offset `0x50`), not the physical image size (20th Century: 327,168 declared vs 327,320 physical); and the TOC control includes the Q-position bit plus the 8-byte READ_SESSION_INFO payload. The CD-ROM save-state section is version 5 for the guest speed mode and readable limit. The remaining 20th Century wait is guest sleep time (6 frames vs 60) with matching CD values, so the residual is not the CD register contract; the next measurement is the wake source (FIQ/timer/SPORT). The 11-title `cd-cadence` panel keeps the same first mismatch indices. The confirmation panel (`test110-panel50-metadata`, 50 titles, 300 frames) captures **50/50 with 0 errors**, **0 frame-hash divergences**, **150/150 aligned audio fields**, **39 phase-only** titles, and the same **11 CD-only first behavioral divergences** at f=300. **2,048 tests pass**; build and format are clean. The local InspectCode host crashed before emitting a result in this session; the prior complete run was clean. Evidence: `artifacts/diff/test104-20th-cdreg3` through `test110-panel50-metadata`; `DifferentialHarness.md` §8; `PortingNotes.md` 12.19; `TestCorpus.md` 1.1bf.

- **2026-11-08 - TEST-07 audio gating, scripted input, and the nightly baseline.** The differential harness now gates the cumulative audio hash at aligned sample counts, reports a count difference as `cumulative hash not comparable ... (reference 16.16 field-rate quantization, 1.214 ppm)`, and adds audio aligned/lag/diverged summary rows plus an HTML card. The alignment probe (`audio-probe.json`, Lemmings, 3,000 frames) measured 5DO's exact `60000/1001` rate against OperaCore's `3,928,227/65,536` (59.939987 Hz, 1.214 ppm lower): the sample counts match through 1,000 frames, then the reference is **+1 at 2,000** and **+2 at 3,000** (2.68 predicted). The 50-title panel (`test93-panel50-audio/report.json`) captures **50/50 with 0 errors**, **50/50 frame hashes**, and **150/150 audio hashes** at 60/120/300; the 11 CD-only first behavioral divergences at f=300 remain. Scripted input is wired through both engines: `test94-samples5-input` matches **5/5 frame hashes at 60/300/600**, the generic menu script changes guest output at 600/900, the no-input control matches the same checkpoints, and Road Rash's f=600 divergence reproduces without input (`test95-roadrash-noinput`). The nightly workflow now captures fresh (`--force`), uploads `report.html`, and fails only on a 5DO field change against the checked-in `benchmarks/diff/baseline/showcase-report.json`; `baseline --write` generates the trimmed baseline. **2,040 tests pass**; build, format, and InspectCode are clean. **Moves TEST-07 to completed.**
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
| BIOS (rom1), including embedded Opera Boot | Partial (retail BIOS); embedded boot closed as not distributable | SYS-05 (closed) |
| Font/Kanji ROM (rom2) | Shipped | — |
| CPU overclock 0.5×-4× | Shipped (experimental; not seeded into profiles) | SYS-08 (done) |
| Memory capacity 2/4/8/14/15 MB DRAM, 1/2 MB VRAM | Shipped | SYS-07 (done) |
| Random-number seed | Shipped | — |
| CD speed unbounded/1×-32× | Shipped | — |
| Region NTSC/PAL1/PAL2 | Partial | SYS-03 |
| VDLP pixel format 0RGB1555/RGB565/XRGB8888 | Deferred | VID-16 (deferred) |
| VDLP bypass CLUT | Shipped | — |
| HiRes CEL rendering | Partial (line-doubled); true 640x480 deferred | VID-01 (deferred) |
| MADAM matrix hardware/software | Shipped | — |
| OperaOS SWI HLE | Shipped (opt-in) | SYS-12 (done) |
| NVRAM storage per-game/shared | Shipped | — |
| NVRAM version 0-9 | Shipped | — |
| Active input devices 0-8 | Shipped (device cap); per-device evidence partial | SYS-15 |
| Hide lightgun crosshairs | Shipped | SYS-15 |
| Debug output (KPRINT) | Shipped | — |
| Joypad/flightstick/mouse/lightgun/arcade lightgun/trackball | Partial | SYS-15 |
| Up to eight input ports | Shipped; six-player verification deferred | SYS-15 (SYS-16 deferred) |
| Cycle-based CD timing, read-ahead, DIPIR, tagged status, chunks, subcode | Partial | CORE-04 |
| CD-DA/Red Book, ODE, multi-track TOC | Partial | SYS-01, SYS-02 |
| ARM N=2 cycle model | Experimental option; closed as a compatibility item | CORE-01 (closed) |
| CEL busy pacing | Measured parity; conditional | CORE-03 (conditional) |
| Per-timer CLIO scheduling | Measured parity; conditional | CORE-02 (conditional) |
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

**Priority changes in the 2026-11-06 rewrite (historical traceability):**

> This table records the 2026-11-06 rewrite. The 2026-11-08 feasibility revision
> supersedes it: `SYS-05` is closed, `CORE-01` is closed, and `TEST-06` now starts
> with the unique corpus (Tier A complete) rather than a 594-disc audit.

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
- [Game compatibility results](GameCompatibilityResults.md) - per-disc sweep table.
- [5DO vs 4DO vs Phoenix](CompatibilityComparison.md) - unique-title comparison.
- [Known issues](KnownIssues.md) — current limitations.
- [Performance](Performance.md) — budgets and measurements.
- [Test strategy](TestStrategy.md) and [Test corpus](TestCorpus.md) — test evidence.
- [Differential harness](DifferentialHarness.md) — Opera comparison.
- [Reference strategy](ReferenceStrategy.md) — Opera/4DO mapping.
- [High-resolution CEL research](HighResolutionCelResearch.md) — VID-01/02/03.
- [Release checklist](ReleaseChecklist.md) — release gate.
- [Porting notes](PortingNotes.md) — intentional divergences.
