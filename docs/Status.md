# Project Status and Next Steps

**Snapshot:** 2026-11-09, after the two Tier C gameplay-depth increments and the 1.6.0
compatibility-completion release; the TEST-07
audio-gating, scripted-input, and nightly
baseline update; the 1.5.0 GPU-HQx, video-clean VDLP, field-boundary, and
CD-parity update; the 1.4.0 cover-art, latency, unsigned-disc, exact-clock,
and differential-harness update; the TEST-07 oracle increment, the VID-12 latency update,
the PERF-02 AOT/trimming audit, the 1.3.0 field-handling update, the 1.2.2
performance/library update, the 1.2.1 arm64 packaging update, the 1.2.0
feature/accuracy update, the rewind-capture stall fix, SYS-12 SWI HLE, SYS-17
game-database expansion, SYS-08 clock validation, the third PERF-08 JIT pass, the ENG-02
interpreter hot-path increment, and the PERF-05 high-resolution VDLP copy: Direct3D 11,
startup release checks, the fuzz harness, per-edge overscan/void border, screenshot
gallery filters, FIQ/DMA/DSP telemetry, flat CHD/raw ISO support, and display-refresh
control. The earlier first full-collection sweep, performance, timing-option, BIOS,
NVRAM, rewind, cheat, TAS, disc-browser, input, OSD/quick-menu, library, CD-DA,
multi-disc, portable, PAL/NTSC, diagnostics, save-state, overscan, and fix-parity
passes are retained below. Road Rash's
100k-state race now measures
4.20 ms/frame total (p99 7.40 ms),
Debug core builds are optimized, and Auto presentation frame skip is the default.
**594 discs tested: 593 Loadable/Rendered, 1 Loadable/No video (known boot failure), 0 Not Loadable.**

**Corpus update (2026-11-08):** the current collection is **1,371 images → 414 unique
titles / 378 game test set**. Under the compatibility plan's binary beyond-title rule,
**375 of 378 games are Playable** — the full 147-title peer gap is verified, the
comparison has **0 `Behind` and 0 `Regression` rows**, and 5DO is a strict superset of
the **252-title peer union** (after the evidenced *Return Fire – Maps O' Death* data-disc
invalidation). The original **≥254** numeric target is exceeded. The two `Title-only`
rows and one `No video` row are non-retail test images (`Joypad Test v1.0`,
*Return Fire – Maps O' Death* data disc, and `LameNES Emulator`). Per-title BIOS, disc
hash, capture frame, screenshot, and failure reason live in
[`BeyondTitleEvidence.md`](BeyondTitleEvidence.md) and
`docs/data/beyond-title-results.csv`. The unique-title 5DO/4DO/Phoenix comparison is in
[`CompatibilityComparison.md`](CompatibilityComparison.md); the 594-disc legacy sweep
above remains the per-image baseline.

**1.6.0 release update (2026-11-08):** **v1.6.0** completes the compatibility plan on the
current corpus: **1,371 images / 414 unique titles / 378 game-test-set titles**, with
**375 Playable**, **0 `Behind`/`Regression`**, and a strict superset of the 252-title peer
union. The release publishes the authoritative 5DO/4DO/Phoenix matrix and the
beyond-title ledger, tightens CD poll reset, guest drive speed, SESSION INFO, TOC
Q-position, and volume-header readable-disc behavior, and moves the CD-ROM save-state
section to version 5. Settings and NVRAM persistence now fall back to a direct copy when
an atomic replace is denied, preserve newer-schema settings as
`settings.json.v<version>.bak`, and survive null/short `Players` arrays; the desktop adds
tooltip coverage and logs expected fallback paths. The suite is **2,061 tests**; build,
format, and InspectCode are clean; `verify-beyond-title-ledger.py` and
`generate-compat-comparison.py --check` pass; Road Rash boots for 600 frames in both the
compatible and accuracy profiles. The six framework-dependent bundles and
`SHA256SUMS.txt` are published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.6.0>. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42552318341> passed all six bundle
jobs and the publish job; CI run
<https://github.com/purelogiccode/5DO/actions/runs/42552303788> passed every required
job; docs run
<https://github.com/purelogiccode/5DO/actions/runs/42552303805> deployed Pages and the
wiki. All published checksums verify. See
[`WhatsNew.md`](WhatsNew.md) and [`ReleaseNotes-1.6.0.md`](ReleaseNotes-1.6.0.md).

**Tier C gameplay-depth update (2026-11-09):** the first Tier C increment re-ran the
25 titles still graded `Playable` after Tier B with title-specific start and gameplay
scripts. Eight were promoted to `Good`: **Olympic Soccer** (full match to FULL-TIME),
**John Madden Football** (live football gameplay through 36,000 frames), **Crime
Patrol** and **Mad Dog II** (live-action lightgun scenes at 48,000), **Shootout at Old
Tucson** (lightgun scene with the ammo HUD), **Guardian War** (world-map gameplay
through 36,000 frames), **Hell - A Cyberpunk Thriller** (3D adventure gameplay at
44,000/48,000 frames), and **Slayer** (first-person dungeon gameplay at 80,000/90,000
frames). A fixed-script run withdrew the earlier PGA Tour 96 promotion; the Player List
stays put, so it remains `Playable`.
The CSV ledger moved to **101 rows: 84 `Good`, 17 `Playable`**. The 378-title playable
count stays **375** because Tier C changes the quality grade, not the binary
compatibility result. The 25-title baseline completed 25/25. See
[`TestCorpus.md`](TestCorpus.md) 1.1bm and [`CompatibilitySweep.md`](CompatibilitySweep.md)
4.3.

**Tier C second increment (2026-11-09):** input scripts now support pointer devices
(`@mouse`, `@lightgun`, `@arcadegun`, `@trackball`) with one-frame relative motion and
persistent buttons; `ScriptedPbusSource` applies them per configured port and the parser
has six new tests. Four more titles were promoted to `Good`: **Carrier - Fortress at
Sea** (trap menu to a live trap approach and WAVE OFF at 14,000 frames), **Family Feud**
(home questions and letter keyboard at 9,000/12,000), **Insect War** (first-person HUD
and WARNING state at 58,000/62,000), and **N. O. B.** (VITALITY HUD gameplay at 9,000).
At that point the ledger was **101 rows: 88 `Good`, 13 `Playable`**; seven of the
remaining rows were media/reference/add-on discs, and six were game leads (BladeForce,
DeathKeep, Jurassic Park, Kingdom, Off-world Interceptor, PGA Tour 96). Differential
triage closed the three
suspected core defects as content/script limits: DeathKeep and Kingdom show the same
black screen/title screen on OperaCore, and BladeForce reaches the same Main Options
menu on both engines. The quality ledger gate (`tools/verify-playability-grades.py` and
`compat-baseline.yml`) now fails on a `Good` -> `Playable` drop. The suite is **2,068
tests**; build, format, and InspectCode are clean. See [`TestCorpus.md`](TestCorpus.md)
1.1bo and [`CliReference.md`](CliReference.md#input-and-automation-options).

**P0 quality closure (2026-11-09):** the remaining menu/cutscene leads were converted
into mission, terminal, and round evidence. **BladeForce** now types a name, selects
START GAME, and reaches the Supply Depot first-person mission at 70,000/80,000/90,000
frames. **Off-world Interceptor** launches its first mission from the loadout at 12,000
frames. **PGA Tour 96** reaches Hole 1 at 64,000 frames. **Jurassic Park Interactive**
opens the Security Systems / Security Tracking terminal, and **Alone in the Dark** adds
room exploration plus the Actions menu (`Fight`/`Open/Search`/`Close`/`Push`). The three
promotions move the ledger to **101 rows: 91 `Good`, 10 `Playable`**. AUD-01's
10-minute real-time soak passes on Windows with both the null and real OpenAL devices
and on the Linux VM with the null driver: 0 dropped samples and 0 underruns on all three
runs. The Linux VM has no PCM endpoint, so a Linux real-device soak remains a hardware
limitation. Road Rash's full 5.5-mile race is still open; a fresh 150,000-frame autopilot
run ends with the rider off the bike. See [`TestCorpus.md`](TestCorpus.md) 1.1bp and
[`Audio.md`](Audio.md) §9.

**1.5.0 release update (2026-11-08):** **v1.5.0** moves HQx 2x/3x/4x to shared
rule-table GPU passes in OpenGL/GLES, Direct3D 11, and Vulkan, with the CPU filter kept
as the reference oracle. The 3DO background is now 24-bit end to end, a zero VDL
display-list word is a stop, `RunFrame` ends on the reference field boundary, and CD
poll/FIQ/READ_DATA behavior is closer to Opera. The 50-title panel is **50/50 frame
hashes at 60/120/300 with 0 errors**: the same-frame instruction delta is 11-20 at
frame 60 and 23-2,223 at frame 120, and the remaining behavioral divergence is CD
command cadence. The command-50 boot handoff fell from +207,707 cycles / +86,511
instructions to -818 / -376. The suite is **2,033 tests**; build, format, and
InspectCode are clean. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42445302517> passed all six bundle
jobs and the publish job; CI run
<https://github.com/purelogiccode/5DO/actions/runs/42445297515> passed every required
job. The six bundles and `SHA256SUMS.txt` are attached at
<https://github.com/purelogiccode/5DO/releases/tag/v1.5.0>, all checksums verify, and
local copies are kept in `src/FiveDo.Frontend.Avalonia/bin/Release`. See
[`WhatsNew.md`](WhatsNew.md), [`ReleaseNotes-1.5.0.md`](ReleaseNotes-1.5.0.md), and
[`DifferentialHarness.md`](DifferentialHarness.md).

**TEST-07 audio and scripted-input update (2026-11-08):** the differential harness now
gates the cumulative audio hash when both engines have produced the same sample count.
The 50-title panel matches **150/150 audio fields** at frames 60/120/300, so all 50
titles match video, samples, and audio at the panel checkpoints. The one measured
long-run limit is the reference clock: 5DO follows the exact `60000/1001` NTSC rate
(59.940059940 Hz), while OperaCore's 16.16 constant is `3,928,227/65,536`
(59.939987183 Hz), **1.214 ppm lower**. On Lemmings the sample counts match through
1,000 frames, then the reference is **+1 at 2,000** and **+2 at 3,000** frames
(2.68 predicted); the harness labels that as an alignment state instead of a waveform
divergence. A difference at an aligned count is still a hard failure. Scripted input is
wired into both engines: the five-title, 900-frame `samples5-input` panel matches
**5/5 frame hashes at 60/300/600**, the generic menu script changes guest output at
600/900, and the no-input control confirms the matches are not from an ignored script.
The nightly workflow now runs the showcase fresh, uploads `report.html`, and fails only
when a 5DO field changes against the checked-in baseline. The suite is **2,048 tests**;
build, format, and InspectCode are clean. See
[`DifferentialHarness.md`](DifferentialHarness.md) §4.1/§4.2/§6 and
[`TestCorpus.md`](TestCorpus.md) 1.1bd.

**CORE-04 CD register and metadata parity update (2026-11-08):** the debug reference core
now records every CD command byte, poll write/read, status read, and data read with
cycle and PC, and the harness writes matched `fivedo.cdreg.txt`/`opera.cdreg.txt` traces
with `--cd-register-trace`. On 20th Century Video Almanac the two engines' command
packets, status bytes, and 2048-byte sector data are identical through command 105, the
last command before the sequence divergence; the remaining difference is guest sleep
time, not the CD register contract. The trace exposed and fixed the XBUS 2x status bit
(`MEI_CDROM_DOUBLE_SPEED` is `0x80`, and the drive starts at double speed), the guest
speed cap in `cdrom_effective_speed`, the readable-disc limit (3DO volume header
`volume_block_count`, not the physical image size), and the TOC Q-position bit plus the
8-byte READ_SESSION_INFO payload. The confirmation panel
(`test110-panel50-metadata`, 50 titles, 300 frames) captures **50/50 with 0 errors**,
**0 frame-hash divergences**, **150/150 aligned audio fields**, **39 phase-only**
titles, and the same **11 CD-only first behavioral divergences** at f=300. The CD-ROM
save-state section is version 5. The suite is **2,048 tests**; build and format are
clean. See [`TestCorpus.md`](TestCorpus.md) 1.1bf and
[`PortingNotes.md`](PortingNotes.md) 12.19.

**1.4.0 release update (2026-11-07):** **v1.4.0** adds online cover art (473 unique
WebP covers, 645/653 game IDs mapped), VID-12 run-ahead/frame-delay/VRR/audio-sync
controls, SYS-06 unsigned-disc support, the exact 60000/1001 NTSC field clock and rational
VDL accumulator, VDLP blanking/background fixes, and the live OpenAL device-switching and
test-tone fixes. The suite is **1,905 tests**; build, format, and InspectCode are clean.
The 50-title differential panel remains the current accuracy evidence: 32/50 titles match
every frame hash at 60/120/300, every sample delta is within 9, and Space Ace,
Wolfenstein 3D, Alone in the Dark, and Lemmings match Opera at 3,000 frames while Road
Rash still diverges. See [`WhatsNew.md`](WhatsNew.md),
[`ReleaseNotes-1.4.0.md`](ReleaseNotes-1.4.0.md), [`DifferentialHarness.md`](DifferentialHarness.md),
and [`CoverArt.md`](CoverArt.md).

**Post-1.4.0 differential update (2026-11-07):** the 50-title panel is now
**video-clean**: **50/50 titles match every frame hash** at frames 60/120/300 (up
from 32/50), with **0 errors** in `test55-panel50-first-sector`. **48/50 first PC
divergences instruction-align to Opera frame 59** and **2/50 to frame 119** (the
f=60 checkpoint itself matched on those two), at a residual of only -55..-415
instructions (same-frame delta ~89,000). The
fixes are a 24-bit VDL background (the old 5-bit narrowing rendered near-black as
pure black) and treating a zero VDL display-list word as a stop that retains the
previous bitmap, matching `opera_vdlp.c`. The only remaining behavioral
divergence is CD command cadence: 36 titles first differ by 1-2 commands at
frame 120 (in-flight at the checkpoint) and 13 at frame 300 (seven FMV titles at
+3/+4, six at -1/-2). The 5-title panel matches **5/5** video at 300 frames; at
3,000 frames Alone, Lemmings, Space Ace, and Wolfenstein match and Road Rash
still diverges. The harness now records `pcPhaseFrame`/`pcPhaseInstructionDelta`
and per-command PC/retired-instruction stamps. Those stamps showed the earlier
command-50 handoff was **+207,707 cycles / +86,511 instructions** of extra guest
work; matching the oracle's first-sector READ_DATA status publication reduced it
to **-818 / -376**, and the f=300 command-104 cycle delta fell from **+324,027**
to **+111,990**. READ_DATA deadlines now accumulate from the previous deadline
instead of re-basing to `now`. The residual universal frame-60 one-field phase is
separate from that handoff and remains open. The harness also stages ISOs
atomically, retries a transient CHD read once, and reports a per-title capture
failure as an `Error` instead of aborting the run. The suite is **1,922 tests**;
build, format, and InspectCode are clean. See
[`DifferentialHarness.md`](DifferentialHarness.md) and
[`TestCorpus.md`](TestCorpus.md) 1.1az.

**CD cadence triage (2026-11-07):** the harness now classifies every final CD
transcript and reports the largest cumulative cycle-delta step with its
command-issue PC and retired-instruction jump (`cdCadence`, plus a `CD cadence`
column in the reports). The 50-title panel resolves to **39 identical command
streams, 8 prefix-only, and 3 one-command READ_ID poll shifts** (Chiki Chiki,
Dragon's Lair, Virtual Horror). **24/50 titles have a significant step and all 24
issue the step command from the same PC on both engines** (17 at 60-100k cycles,
six at 20-60k, one >=100k). The fresh five-title run confirms Alone 361/361
(`+379` cycles at the last command), Road Rash 395/395 (`+468`), Wolfenstein
341/341 (`-345`), Lemmings 109/105 with one `+22,834` step at command 51
(`+8,242` instructions), and Space Ace 168/168 with one `+80,567` step at
command 164 (`+32,330` instructions). The residual count difference is guest
wait-loop cadence, not a divergent CD command table. The new `--cd-register-trace`
shows **no CD register access during the 237,794-cycle Space Ace wait or the
2.17M-cycle Lemmings wait**, so the guest is waiting on a non-CD source
(timer/VBlank); this moves the next measurement to CORE-07. The suite is
**1,922 tests**. See [`DifferentialHarness.md`](DifferentialHarness.md) 8 and
[`TestCorpus.md`](TestCorpus.md) 1.1bb.

**Frame-boundary parity (2026-11-07):** the universal frame-60 PC/CPSR phase had
two causes. The harness finalized each Opera checkpoint at the next video
callback, so ARM/CD state belonged to the following field while the audio hash
was already correct; the harness now captures ARM/CD state in the checkpoint's
own callback. `FiveDoMachine.RunFrame` also stopped on the rounded
208,541-cycle budget while Opera stops on the 263-line scanline boundary, so the
discarded last-batch overshoot accumulated about 3.3 instructions per field;
`RunFrame` now ends on `Quartz.VdlFieldIndex`. On the 50-title confirmation
panel the same-frame retired-instruction delta is **11-20** at f=60 (average
15.3) and **23-2,223** at f=120, with **50/50 frame hashes** and **0 errors**.
The CD count matches on **50/50 titles at f=60 and f=120** (down from 36 first
divergences at f=120), and the first behavioral divergence moved to **11 titles
at f=300** (8 count, 3 sequence); the other 39 titles are phase-only. This
supersedes the "5DO frame 60 aligns with Opera frame 59" interpretation. The
3,000-frame showcase keeps **4/5 video matches** (Road Rash is the only
frame-hash divergence) with CD counts Lemmings 365/352, Gex 548/547, Road Rash
851/836, Wolfenstein 862/861, and Alone 932/932. The suite is **1,923 tests**.
See [`DifferentialHarness.md`](DifferentialHarness.md) 8 and
[`TestCorpus.md`](TestCorpus.md) 1.1bc.

Release workflow
<https://github.com/purelogiccode/5DO/actions/runs/42399055155> passed all six bundle
jobs and published six versioned archives plus `SHA256SUMS.txt`; all six checksums verify.
CI run <https://github.com/purelogiccode/5DO/actions/runs/42398646569> passed every
required job, including the six bundle builds, NativeAOT CLI/frontend, format, tests, and
benchmark gates. The release is published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.4.0>. Local bundles are kept under
`src/FiveDo.Frontend.Avalonia/bin/Release`.

**TEST-07 exact-clock and 50-title panel update (2026-11-06):** the earlier claim that the frame-60 CPSR difference was the FIQ-disable bit was wrong: `0x600000d3` and `0x800000d3` differ in the N/Z/C condition flags. The real defect was the field clock. 5DO now runs NTSC at the exact 60000/1001 field rate (208,541 cycles at 12.5 MHz) and uses a rational VDL accumulator, removing both the 1 ms/60-field drift and the per-batch 8.24 rounding that ran the scanline clock ~0.09% fast. Sample counts now track Opera within 1-8 samples at 300 frames (all 50 panel titles are within 9 at frame 300), and **32/50 titles match every frame hash** at frames 60/120/300. The 50-title panel captured 50/50 with 0 errors; CD counts match 49/50 at frame 60. The absolute instruction count carries a fixed ~89,000 startup offset, but the per-frame retirement rate matches within 0.1% on 4/5 titles, so PC/CPSR is a phase sample rather than a cycle-model gap. The new CD cycle stamps localize the remaining CD cadence difference. At 3,000 frames Alone, Lemmings, Space Ace, and Wolfenstein match Opera's hash; Road Rash still diverges. The suite is now **1,905 tests**. See [`DifferentialHarness.md`](DifferentialHarness.md) and [`TestCorpus.md`](TestCorpus.md) 1.1ax.
**TEST-07 oracle increment (2026-11-06, superseded by the exact-clock update above; the FIQ-bit and 59.94/60 Hz statements below are incorrect):** the differential harness now applies a
fixed seed to both engines (`--seed`, default 1488), compares ARM CPSR and CD
command counts/opcode hashes, and reports the **first divergence** in the console,
JSON, and Markdown. Two consecutive runs of the new five-title
`benchmarks/diff/samples5.json` panel produced identical checkpoint arrays for both
engines. The first divergence is the ARM PC at frame 60 on all five titles;
Lemmings' CD commands match 9/9 at frame 60 and 50/50 at frame 120, then 108/105 at
frame 300, and its CPSR differs at frame 60 by the FIQ-disable bit. The cumulative
audio hash is recorded but not gated because the 59.94/60 Hz field-rate offset
leaves a systematic sample-count delta. The `panel` command generated
`benchmarks/diff/panel50.json` (50 evenly spaced titles from the 594-disc
manifest). A 3,000-frame follow-up separates PC drift from visible divergence:
Lemmings matches the frame hash at every checkpoint, while Road Rash and Space
Ace show different animation frames/scenes and no ±60-frame hash match. The full
CD command sequence localizes the first command divergence to READ_DATA (`0x10`)
versus READ_ID (`0x83`) transitions, giving CORE-04 a concrete next target. The
suite is **1,881 tests**. See
[`DifferentialHarness.md`](DifferentialHarness.md) 8 and
[`TestCorpus.md`](TestCorpus.md) 1.1aw.

**Performance increment (2026-11-05):** the default rewind history no longer stalls the
emulation thread. Keyframe capture on Lemmings fell from **139.3 ms** to **7.4-9.2 ms**
(one 14.1 ms outlier), per-1,200-frame allocations fell from ~1.5 GB to 16 MB with no
gen0/gen2 collections, and the frame loop measured p50 **1.34 ms** / p99 **6.85 ms** /
max **14.76 ms** with the mailbox and rewind active. The memory save-state section
(version 4) now stores only the live RAM/VRAM range, cutting a stock-layout uncompressed
state from ~26 MB to ~10 MB, and the frontend opens discs through the prefetch cache.
See [`Performance.md`](Performance.md) 8.5. The suite is **1,866 tests**.

**CPU and high-res increment (2026-11-06):** the third PERF-08 pass gives compiled blocks a
shared PC/CPSR/cycle/instruction context, compiles register-specified shifts, and replaces
the compile-hit dictionary with a direct-mapped table. Lemmings improved from 1.60-1.66 to
**1.23-1.37 ms/frame** (about 20% faster than the old JIT) but is still ~1.25x slower than
the 1.00-1.02 ms/frame interpreter, so the interpreter remains the default; the synthetic
loop is 0.89-0.99 ms/frame versus 1.20-1.30 for the interpreter. The second pass had replaced
per-instruction JIT verification with exact RAM-write block invalidation and fixed compiled
R15 operands. ENG-02 added direct RAM/ROM instruction fetch, inline LSL#0/rotated-immediate
operand paths, a flag-preserving ALU dispatch, and population-count LDM/STM iteration; an
interleaved A/B measured synthetic **1.58→1.33 ms/frame** and Lemmings **1.43→1.13 ms/frame**
with unchanged hashes. PERF-05 now reads the raw VRAM word span for high-resolution scanline
DMA, cutting Lemmings high-res from **2.42-2.47** to **1.30-1.43 ms/frame** (about 1.8x) with
an identical high-res hash. See [`Performance.md`](Performance.md) 8.4 and 8.6.

**Latency and AOT increment (2026-11-06):** VID-12 adds 0-2 frame run-ahead, 0-8 frame
presentation delay, VRR-friendly compositor presentation, a −250…250 ms audio sync offset,
per-game profile fields, and software input-latency telemetry. Run-ahead uses a reusable
uncompressed `MachineStateSnapshot`; `bench --latency` measures a 9,348,438-byte snapshot,
~3.6-3.9 ms capture, and ~1.6-2.0 ms restore. AOT CLI/frontend win-x64 builds start, render,
play audio, and pass smoke tests; the AOT frontend booted Lemmings with run-ahead 1, frame
delay 1, VRR, and audio sync 40 ms. PERF-02 is complete; VID-12 and SYS-12 are marked done.
Full optical input-to-photon measurement and real adaptive-sync hardware confirmation remain
documented validation caveats. See [`Performance.md`](Performance.md) 8.7 and
[`Trimming.md`](Trimming.md).

**Release status (2026-11-02):** **v1.0.0 is released** as a documented-compatibility
release. The version is bumped, the source is tagged `v1.0.0`, and one unified bundle
per system (Windows, Linux, macOS x64) plus checksums is attached to the
[GitHub release](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0). Linux x64
validation passed on Linux Mint 22.3: the full test suite
(**1,473/1,473**), `selftest`, `bench`, a 3,000-frame Wolfenstein boot, non-silent
audio, and the frontend `--smoke-test`. The GL probe timed out under the VM's
llvmpipe/SSH session, so Linux GPU presentation is a **preview/best-effort** limitation.
Legal/attribution sign-off is recorded under the FreeDO modified GNU LGPL as free,
open-source donationware (non-commercial; voluntary donations only). M13 loadability is
complete; M13.5/M13.6 visual/high-resolution review and M14 high-resolution CEL research
are **post-1.0**.

**Field-handling update (2026-11-06):** **v1.3.0** was the previous release,
published at <https://github.com/purelogiccode/5DO/releases/tag/v1.3.0>. It adds
`InterlaceProcessor` with auto 480i detection, bob, weave, and blend modes; per-game and
`--deinterlace` selection; interlace-aware viewport geometry; screenshot/GIF capture of
the presented frame; and `run --json` mode/detection fields. The suite is **1,866 tests**.
Bob-vs-auto hashes match on five local titles (3,000 frames for Road Rash, Space Ace, and
Alone in the Dark; 300 for Wolfenstein 3D and Lemmings), forced weave produces a 320x480
frame, and high-resolution output remains 640x480. The CLI and core default to bob; the
desktop defaults to auto. No local title is confirmed 480i, so synthetic complementary
field tests are the current 480i evidence.

Release workflow <https://github.com/purelogiccode/5DO/actions/runs/42235476257> passed
all six bundle jobs and published six versioned archives plus `SHA256SUMS.txt`; the docs
workflow <https://github.com/purelogiccode/5DO/actions/runs/42235456021> deployed the
Pages site and wiki. All release checksums verify. Local bundles and an archive of every
historical release asset are kept under
`src/FiveDo.Frontend.Avalonia/bin/Release`.

**Performance and library update (2026-11-06):** **v1.2.2** was the previous release,
published at <https://github.com/purelogiccode/5DO/releases/tag/v1.2.2>. It adds the
interpreter, high-resolution VDLP, and rewind performance work; the opt-in managed JIT;
the expanded game database and library health/DAT verification; eight Opera memory
layouts; eight PBUS ports; opt-in Portfolio OS SWI HLE; and audio-only CHD cataloging.
The suite is **1,815 tests**. Release workflow
<https://github.com/purelogiccode/5DO/actions/runs/42169040644> passed all six bundle
jobs and published six versioned archives plus `SHA256SUMS.txt`; all six checksums
verify. The docs workflow deployed the Pages site and published the wiki. A post-1.2.1
audit fixed eight bugs: the JIT register-shift PC delta, stale ROM-bank and soft-reset
blocks, FIQ entry telemetry, the prefetch track-sector length, legacy PBUS slots 7-8,
the pointer-exit port bound, and the ARM clock range.

**Arm64 packaging update (2026-11-03):** **v1.2.1** was the arm64 packaging release,
published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.2.1>. It added
framework-dependent Windows arm64, Linux arm64, and macOS arm64 bundles alongside the
x64 bundles, for six versioned archives plus `SHA256SUMS.txt`. CI verifies each arm64
apphost's PE/ELF/Mach-O machine type; the x64 bundles keep their runtime smoke tests.
No emulator behavior changed. Release workflow
<https://github.com/purelogiccode/5DO/actions/runs/41583317988> passed all six bundle
jobs; every checksum was verified after publication.

**Feature update (2026-11-03):** **v1.2.0** added the optional
Windows Direct3D 11 presenter, startup GitHub release checks, deterministic fuzzing,
per-edge overscan/void border, screenshot gallery filters/compare/export, display-refresh
control, flat 2048-byte CHD and raw `.iso` detection, FIQ request-to-vector latency
telemetry, DSP per-tick statistics, and the CLIO/DIPIR/FIFO/MCTL/XBUS audit fixes. The
1.2.0 suite was **1,597/1,597** passing. WebAssembly/browser, Android/iOS, and Raspberry Pi
were removed from the roadmap; desktop Windows/Linux/macOS remain supported. The three
versioned bundles and `SHA256SUMS.txt` are attached to the release; every checksum was
verified after publication.

**Maintenance update (2026-11-02):** **v1.1.0** remains available at
<https://github.com/purelogiccode/5DO/releases/tag/v1.1.0>. It adds game
library deduplication by 3DO game ID, moves the OpenAL license text to
`docs/OpenAL-Soft-COPYING.txt`, removes the frame/PC/cycle counter from the desktop
status bar, and completes the Serilog catch-block coverage and launch-statistics
wiring. The 1.1.0 suite was **1,597/1,597** passing. The bundle layout is unchanged:
framework-dependent single-file apphosts, no bundled .NET runtime, no PDB/XML files.

**Latest work (2026-11-05):** Settings > Paths now edits game library folders directly
(type or paste a path, Browse, or Remove), and the scanner indexes cover images once per
folder instead of probing every cover extension for every disc. Cover art now decodes on
bounded background tasks, so the non-virtualizing grid does not block the list. On the local
2,732-image collection the cached listing fell from 2.8 s to 0.33 s, and a cold listing shows
all entries in 17 ms before background metadata hashing. The library now reports both counts
(for example `653 games · 1,366 images`) because discs are identified by the 4DO game ID
(MD5 of the first two data sectors); **Settings > Paths > Show duplicate disc images** lists
every renamed or GoodTools/Redump/No-Intro copy. PERF-08 added an opt-in managed IL JIT (`--cpu-backend jit`) for hot data-processing, load/store, and branch blocks. A third pass shares a PC/CPSR/cycle context across blocks, compiles register-specified shifts, and uses a direct-mapped compile-hit cache. It is bit-identical on the test corpus and a 500-frame CLI A/B matches Road Rash, Powers Kingdom, and Lemmings hashes/instructions/cycles, but Lemmings boot improved only to 1.23-1.37 ms/frame and is still ~1.25x slower than the interpreter, so PERF-08 remains Partial and the interpreter remains the default. SYS-17 expanded the game database from 518 to 653
records and added `gamedb stats|scan|export|merge`, `docs/GameDatabase.md`, and a
filename parser for Redump/GoodTools/No-Intro layouts. The schema now carries revision,
disc number, BIOS/BIOS2, fix mode, input devices, NVRAM, and cheat fields; fix, BIOS, and
input values are consumed at runtime. `gamedb stats --folder "J:\Panasonic 3DO"` reports
1,366 readable images, 653 unique IDs, and 100% metadata coverage with no errors
(the three exceptions are audio-only `[CD-Audio]` CHDs). SYS-08 added the five-title
0.5x-1x-2x-4x A/B in `Performance.md` §8.3 and kept the stock 12.5 MHz default; every
non-stock clock changed guest behavior. The suite is **1,866 tests**.

**SYS-12 (2026-11-04):** Portfolio OS SWI HLE is available behind `--swi-hle`,
Settings > Emulation, and per-game profiles. It handles Opera's 14 fixed-point math SWIs
on the host, records the option in save-state Options v9, and reports per-comment
coverage with `--swi-comments`. The host routines match `opera_fixedpoint_math.c`; a
real-BIOS 4x4 benchmark cuts 847,860 guest steps to 24,578. HLE is an opt-in performance
mode and skips the guest handler's cycles/register clobbers, so some titles change
behavior. Default-off hashes are unchanged. Input devices also support eight PBUS ports:
ports 7-8 use the Opera daisy chain, `--active-devices`/Settings caps the polled ports at
0-8, and the MADAM PBUS buffer matches Opera's 256 bytes (state section v4). The
hide-lightgun-crosshair option is also available.

**SYS-07 (2026-11-04):** all eight Opera DRAM/VRAM layouts
(2/4/8/14/15 MB DRAM and 1/2 MB VRAM) are selectable behind `--memory`, Settings > Emulation >
Memory, per-game profiles, CLI JSON, and save states. VRAM is decoded directly above
DRAM, VDLP/SPORT/CEL use the live layout, and MADAM RED SYSBITS reports the capacity to
the BIOS. A homebrew probe ROM uses 4 MB DRAM + 2 MB VRAM; retail titles remain on the
stock 2 MB/1 MB default because Opera documents expanded memory as homebrew-only. The
CPU-clock matrix (0.5x-4x) and `bench --cpu-clock`/`--memory` were also added; the suite
is now **1,866 tests**. See [`TestCorpus.md`](TestCorpus.md) 1.1al, 1.1an, 1.1ao, 1.1aq, 1.1ar, 1.1as, 1.1at, and
[`Performance.md`](Performance.md) 8.1.

**Latest work (2026-11-02):** the CLI and desktop shell check the public GitHub release
feed at startup (cached, opt-out) and offer the download page; `5do update` exposes the
same check. The optional `FiveDo.Video.Direct3D` Windows backend adds a FL11_0
two-stage presenter, runtime HLSL, a DXGI flip-model child swapchain,
`--gl-backend d3d11`, Settings selection, and `--d3d-probe`/`--d3d-window-probe`.
320x240 Accurate matches the CPU oracle (0 different pixels); 4K sharp-bilinear
measures present p99 0.23 ms and GPU p99 0.07 ms. OpenGL remains the default.

The 2026-11-02 hardening pass added `tools/FiveDo.Fuzz` (state, disc, CUE, Opera-FS,
CD command-stream, and ARM60 targets plus a nightly workflow), per-edge overscan and
per-game crop profiles with a configurable void border, and screenshot gallery filters,
side-by-side compare, export, and settings sidecars. The same pass added FIQ
request-to-vector latency telemetry and `--fiq-trace` request/entry lines, closed the
CLIO output-FIFO/DIPIR/MADAM-MCTL/XBUS poll audit gaps, and exposed DSP per-tick
statistics in `run --json`. The 1.2.0 suite was **1,597 tests**.

SYS-01 and AUD-05 now expose multi-track CUE/CHD tables and CD-DA: `ITrackedDiscSource`
carries native 2048/2336/2352-byte sectors, `CueDiscImage` handles multi-file/multi-track
`INDEX`/`PREGAP`/`POSTGAP` layouts, `ChdDiscImage` exposes audio tracks, and
`CdromDevice` implements multi-track READ TOC/lead-out, READ SUB-Q, MODE SET/SENSE,
PLAY_AUDIO position tracking, 2352-byte digital-audio reads, and guest inject. CD-DA is
mixed into the 44.1 kHz DSP output with persisted volume/mute; CD state v4 stores the
mode, audio position, volume, and mute. `disc tracks` reports every track, and SYS-02
adds an M3U/M3U8 playlist, playlist editor, drag-and-drop import, File > Swap Disc/F3,
guest-eject ODE auto-advance, and `run --playlist/--swap-at/--swap-disc`. SYS-03 adds
NTSC/PAL1/PAL2 timing: PAL runs at 50 Hz with 288 visible lines, a 312-line field, and
region-aware VDLP/machine timing. Battle Chess Europe renders correctly at 320x288.
Per-game region overrides and state-driven region switching are supported. CORE-04 now
returns illegal-request statuses for unsupported/unknown CD commands, implements the
guest RESET command, exposes `CdromCommandTranscript`/`run --cd-transcript`, and
read-aheads native audio sectors in the disc prefetch cache.
FND-02 is complete with settings schema v2, legacy normalization, malformed-file backup,
per-game profiles, and `settings validate|migrate`. UX-15 adds portable mode:
`portable.flag`, `--portable`, `FIVEDO_DATA_DIR`, and named profiles redirect settings,
NVRAM, cheats, library state, and logs; `InstanceLock` prevents two desktop processes
from writing the same directory. **F2** toggles the frame/lag/skip and selected-player
input overlay. AUD-01 adds OpenAL device selection and a 20-500 ms buffer target with
live output recreation; `audio devices` lists playback devices. FND-08/UX-17 add
`diagnostics bundle` and **Help > Save Diagnostics Bundle...** with path/account
scrubbing. Save-state format v3 now declares feature flags and a section table, keeps
v1/v2 readable, records the live region, and supports warn/reject unknown sections.
Remaining disc work is known-title
CD-DA evidence and a real in-game multi-disc swap, plus subcode. A 10-disc
Release smoke after the
TOC change reported 10/10 Loadable/Rendered. **1,473 tests pass.**
The five not-loadable rows are resolved or explained: Mirai Shounen Conan now runs
(transient CHD read error), and the four Arcade CD-ROM titles load with
`panafz10-patched.bin` plus the default headless pad. The extended-boot retry renders
all 18 former failures except one: 593 of 594 discs now render, and Crash 'n Burn
(Japan) remains a known boot failure. WorldCup Super Stadium needs the FZ-10J BIOS
plus its Kanji font ROM (BIOS2), which the BIOS library now classifies correctly. Headless
`run`/`trace` now attach a default player-1 pad,
fixing Olympic Summer Games' controller check. Fifteen triaged titles now have a
reviewed 5DO status in [`GameCompatibilityList.md`](GameCompatibilityList.md); the
rest remain `No data`. The CORE-01 ARM timing option (`--arm-timing freedo|arm60`), its
save-state snapshot, `--cpu-clock 0.5x-4x`, and `--random-seed` are implemented with
tests. `run --cycle-classes` adds a per-instruction-class instruction/cycle ledger
(`cycleClasses` in `run --json`), and the exact N=2/N=4 class-cost table is documented in
`PortingNotes.md` 12.28. A 40-title A/B reports 17/40 matches with ARM60 versus 15/40
with FreeDO (`TestCorpus.md` 1.1a1); the differential harness now accepts
`--arm-timing`, `--cel-timing`, and `--cd-timing` alongside `--clio-timers` and
`--profile`. The CORE-02 CLIO timer option (`--clio-timers freedo|opera`) now implements
Opera's per-slot scan and cascade carry behind the safe FreeDO-compatible default, with
a state snapshot, 16-bit timer-register masking in Opera mode, a reference-model
conformance test, per-timer telemetry (`--clio-timer-stats`), a bounded
`--clio-timer-trace`, and the smoke/A-B differentials in `TestCorpus.md` 1.1b. The
reference debug core now records matching timer/read events (`--timer-trace`), which show
the timer-14 period equal at 12,384/12,396 cycles (`TestCorpus.md` 1.1az).
The CORE-03 CEL option (`--cel-timing freedo|opera`) now implements Opera's deferred
draw and pixel/texel busy deadline behind the unchanged synchronous default, with a
Madam section v2 busy snapshot, an Options v3 mode snapshot, and a three-title
3,000-frame smoke differential in `TestCorpus.md` 1.1c.
The CORE-04 CD option (`--cd-timing freedo|opera`) now implements Opera's cycle-based
2048-byte data-sector readiness at the stock 2x drive speed, including delayed data
polling, deferred read-data status, the XBUS CD FIQ, a CD-ROM section v2 clock
snapshot, and an Options v4 mode snapshot. The immediate FreeDO path remains the
default; the three-title smoke differential is in `TestCorpus.md` 1.1d. The Opera
poll reset now keeps the three interrupt enables (`0x87`), and the XBUS CD FIQ is
level-held each clock batch as in `opera_xbus_tick`; `TestCorpus.md` 1.1az records
the fix and the remaining guest copy/decrypt wait.
SYS-09 adds `--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x` (Settings > Emulation > CD
speed), Opera's per-speed overhead table, an Options v5 speed snapshot with a mismatch
warning, and the three-title 3,000-frame smoke differential in `TestCorpus.md` 1.1e.
CORE-08 adds `EmulationProfile` (`Compatible`, `Accuracy`, `Performance`, `Custom`),
`--profile`, and Settings > Emulation > Timing profile; the accuracy profile enables the
ARM60, Opera timer, CEL busy, and CD cycle options together, and `run --json` reports the
profile derived from the actual options. `bench --profile` measures any named profile and
reports its timing modes, `selftest` checks determinism plus state round-trip for every
named profile, and the differential harness accepts `--profile` for a full-collection A/B.
A 40-title A/B reports 17/40 matches for both `compatible` and `accuracy` with identical
per-title statuses (`TestCorpus.md` 1.1ak1); the 594-title A/B remains.
SYS-14 adds per-game NVRAM and versions. `NvramStorageMode` (`Global`, `PerGame`),
`NvramVersion` (0-9), and `NvramDirectory` select shared `nvram.ram`, versioned
`nvram.<n>.ram`, or `nvram/per-game/<gameId>.<n>.ram` images. The store reads migration
fallbacks from per-game version 0, the shared version, and the legacy image; saves stay
atomic, and a corrupt primary is backed up rather than replaced by an older image.
Settings > Emulation exposes the mode and version. The CLI adds `--nvram-mode`,
`--nvram-version`, and `--nvram-dir`, reports the resolved path in `run --json`, and
adds `nvram path|list|export|import|reset`.
SYS-04 adds the BIOS manager: a built-in MD5 catalog, `BiosLibrary` scanning/validation,
region/Kanji/Anvil/patch metadata, `--bios2` and `--bios-auto`, `bios list|scan|select`,
Settings > Paths BIOS folder/auto-select/scan, per-game BIOS overrides, and the
Settings > Emulation override combo. Auto-selection uses the game database region plus
known requirements for Slayer Europe/USA, Powers Kingdom (Europe), and Arcade CD-ROM
discs; the configured BIOS remains the fallback when no BIOS folder is set. The
five-title 3,000-frame smoke run and four known-requirement 12,000-frame runs are in
[`TestCorpus.md`](TestCorpus.md) 1.1f. The first Opera parity batch (VDLP, ARM carry, DSP FIFO, MADAM span/MARIA/BPP0/SKIPX/LRFORM/blue-LSB/REGCTL/PIXC, CLIO FIFO), OP-14 (XYPOSH/XYPOSL origin, retired `GraphicsStepY`/`BitTiming6`), OP-SYS-02 (Opera CD poll/local reset), OP-SYS-01/03's ROM overlay, DIPIR, XBUS, and CSTAT soft-reset work, and OP-SYS-06's separate CLIO PRNG are complete (the DIPIR/soft-reset path remains opt-in pending a boot transcript); see `FixParity.md` and the completed log in `ImplementationPlanArchive.md`.
UX-01/UX-02 add rewind and fast-forward. `RewindBuffer` keeps compressed keyframes plus
the per-frame PBUS packet, replays input deterministically after the nearest keyframe,
clears on timeline jumps, and enforces a 256 MB default cap; `CoreRunner.Rewind` restores
the machine and presents the frame. `CoreRunner.SpeedMultiplier` runs 2x-10x frames per
presentation tick with intermediate rendering skipped and audio muted. The shell binds
`R` (one-second rewind), `Shift+F9` (one-frame rewind), and hold-`Tab` (fast-forward);
Settings > Emulation controls the history length and multiplier. UX-04 adds per-game RAM
cheats: `FiveDo.Cheats` parses raw, typed, conditional, and button-activated codes;
`CheatStore` owns the per-game JSON files; `FiveDoMachine.BeforeFrame` applies them every
frame (including rewind replay and fast-forward); the shell adds **Emulation > Cheats...**
and Settings > Paths > Cheats; the CLI adds `--cheat`, `--cheat-code`, `--cheat-auto`,
`--cheats-dir`, and `cheat list|add|enable|disable|remove|import|export`. UX-06 adds
deterministic TAS movies: `FiveDo.Tas` owns the versioned `.5domovie` container
(per-frame PBUS packets, optional starting state, CRC-32 and Brotli), record/replay,
branch/trim, and verification; the CLI adds `--movie-record`, `--movie-play`,
`--movie-verify`, and `movie info|record|play|verify|branch`; the shell adds
**Emulation > Record Movie.../Play Movie.../Stop Movie** and a live player-1 input
display. UX-11 adds a disc browser and disc-info dialog: `FiveDo.Host.DiscBrowser`
mounts the Opera file system without starting the machine, shows a sorted
directories-first tree with sizes/LBAs, extracts a file, directory, or the whole
disc with safe path containment, and reports the volume label/comment, game ID,
4DO database metadata, and automatic fixes. File > Disc Browser... and File > Disc
Info... expose it in the shell. SYS-15 adds the missing 3DO input devices:
`InputDeviceSet` selects a joypad, flightstick, mouse, lightgun, arcade lightgun,
Orbatak trackball, or no device on each of six ports; `PbusChainEncoder` emits Opera's
daisy-chain records and keeps the legacy 4DO packet while every port is a joypad. The
desktop shell adds Settings > Emulation > Input devices, pointer-driven mouse/lightgun
input with a crosshair, and the CLI adds `--input-device` and `--pbus-format`. Crime
Patrol, Mad Dog II, Orbatak, and Shootout at Old Tucson boot and detect the selected
arcade device; scripted gameplay and calibration remain.
UX-12 adds **File > BIOS / NVRAM Manager...**, a two-tab manager that scans and
classifies BIOS images, imports validated `.bin`/`.rom` files, sets BIOS1/BIOS2, pins
or clears per-game BIOS assignments, and lists shared/per-game NVRAM images with
validation, game metadata, version, size, and modification time. The NVRAM tab backs
up one image or the whole layout, imports a validated 32 KB image, resets an image to a
fresh volume, and deletes an image with a second-click confirmation. `BiosInventory`
and `NvramInventory` isolate the file operations for tests and use atomic writes.
UX-19 adds OSD notifications and the in-game quick menu. `FiveDo.Host.Osd` owns a
bounded, time-expiring notification list and validated position/opacity/duration
options; the game view shows colour-coded toasts for save/load, slot changes, rewind,
fast-forward, screenshots, reset, and settings. **F4** or the gamepad Select+Start
chord opens a quick menu that pauses the game and offers resume/pause, save/load,
rewind, fast-forward, screenshot, reset, cheats, settings, stop, and close; Up/Down
and Enter navigate, and closing resumes the prior state. Settings > Video exposes the
OSD and quick-menu options. UX-13 adds persistent library favorites, launch counts,
last-played timestamps, and play time (`library-state.json`); a 60-second flush
bounds crash loss. `LibraryPlaytimeTracker` measures only running emulation. An
optional offline `library-metadata.json` overlay adds title/developer/publisher/
year/genre/region/players/description by game ID or path, while the 4DO database now
supplies year/publisher/region directly. The game list adds star toggles,
metadata/play-time text, All/Favorites/Played/Unplayed filters, and
Title/Recently played/Most played/Release year/Publisher sorts; Settings > Paths
chooses the metadata file. External metadata providers, rich media, and the >=90%
rich-metadata target remain. The Lemmings 300-frame differential smoke still passes
(1 match, 0 diverged). **1381 tests pass.**
The differential harness (FND-05/CORE-11/CORE-16) is implemented:
`tools/FiveDo.DiffHarness` stages the bundled OperaCore and BIOS, runs both engines
with identical options, compares checkpoint frame hashes, applies manifest
allowances, and writes JSON/Markdown/HTML reports. The Lemmings (USA) 300-frame
smoke matches at frame 300 (`0x110E7DC5` on both engines); the five-title showcase
manifest is `benchmarks/diff/showcase.json`, and its first 3,000-frame run
video-matched Wolfenstein 3D and Alone in the Dark while Lemmings, Gex, and Road
Rash diverged. The strict PC gate shows 0/5 matches: the video matches still have
PC deltas of 184,696 and 739,184 bytes, so frame hashes alone are not sufficient
evidence. The `manifest` command generated the portable 594-title
`benchmarks/diff/full.json`. See
[`DifferentialHarness.md`](DifferentialHarness.md) and
[`TestCorpus.md`](TestCorpus.md) 1.1g. The remaining work is the full-collection
run, state/CD transcript fields, and the nightly baseline.
The v1.0 documented-compatibility release is ready: M13 loadability is complete
(593/594 render), legal/attribution sign-off is recorded, and M13.5/M13.6 plus M14
are post-1.0. The project is **donationware** (non-commercial, source-available,
voluntary donations only). See
[`CompatibilitySweep.md`](CompatibilitySweep.md),
[`GameCompatibilityList.md`](GameCompatibilityList.md), and
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
**Release definition:** documented compatibility, not full compatibility; see
[`ReleaseChecklist.md`](ReleaseChecklist.md).
**Scope:** pure C# 3DO emulator, desktop GL/GLES presenter, optional Vulkan and Direct3D 11 backends, CLI, and deterministic tests.
**Reference:** `References/opera-libretro-master/libopera` is now the primary
behavioral reference for new compatibility work; see
[`ReferenceStrategy.md`](ReferenceStrategy.md).

> **2026-10-30 priority change (superseded 2026-11-02):** the v1.0 release was
> deferred for the **M13 full-collection loadability and in-game sweep** and **M14
> high-resolution CEL research**. M13 loadability is now complete (594 tested, 593
> render, 1 known boot failure, 0 not loadable); M13.5/M13.6 and M14 are post-1.0.
> Gameplay-depth work (full races, inventory interaction, long playthroughs) remains
> post-1.0. The project is **donationware**: non-commercial, source-available, and
> funded by voluntary donations rather than sales. The Linux validation target is the
> local Hyper-V VM `LinuxMint` (`D:\Hyper-V\VMs\LinuxMint`). See
> [`CompatibilitySweep.md`](CompatibilitySweep.md),
> [`GameCompatibilityList.md`](GameCompatibilityList.md), and
> [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

This is the short, current answer to "where are we?" The documentation index is
[documentation index](index.md). The detailed task list is in
[`ImplementationPlan.md`](ImplementationPlan.md), the performance contract in
[`Performance.md`](Performance.md), the local game corpus in
[`TestCorpus.md`](TestCorpus.md), the release gate in
[`ReleaseChecklist.md`](ReleaseChecklist.md), and the compatibility completion
plan/next actions in [`ImplementationPlan.md`](ImplementationPlan.md).

---

## 1. What works today

- **Core:** ARM60 interpreter, configurable 2-15 MB DRAM / 1-2 MB VRAM (stock 3 MB total) with 2 MB ROM, shared/per-game NVRAM, CLIO timers/FIQ/FIFO/DMA,
  MADAM CEL/pixel/matrix engines, VDLP, SPORT, DSP, Quartz scheduler, XBUS, CD-ROM,
  ISO/BIN/CUE/CHD storage, input/PBUS, game database, deterministic save states.
- **Save states:** state format version 2 compresses large section payloads with Brotli
  while keeping the CRC over the uncompressed data. Version-1 states still load. A
  24,902,592-byte local state becomes 647,647 bytes (300-frame Gex) or 2,001,229 bytes
  (14,900-frame Gex); the section inspector reads the header without decompressing
  payloads, and `state inspect` reports stored/compressed sizes.
- **BIOS:** `panafz1.bin` boots, runs the splash/animation, initializes the OS, and
  reaches the shell. The earlier `PC 0x205C4` "halt" was the Portfolio idle loop.
  SYS-04 adds a BIOS manager: `BiosLibrary` scans `.bin`/`.rom` files, computes MD5, and
  classifies region, video standard, Kanji, Anvil, and patch state; the built-in catalog
  covers the common FZ-1/FZ-10/FZ-10E/FZ-10JA, GoldStar, Sanyo, patched arcade, Kanji
  font, and arcade support images. `--bios-auto` and Settings > Paths > Auto-select BIOS
  choose a region-appropriate primary plus optional BIOS2; `--bios2` loads an explicit
  secondary. `bios list|scan|select` expose the catalog, and per-game BIOS overrides are
  stored by 4DO game ID. The configured `BiosPath` remains the fallback, so existing
  settings keep their exact BIOS.
- **Video:** the desktop two-stage presenter expands palettes at native resolution
  into an FBO, then scales/filters to the output. Desktop GL 3.3 and GLSL ES 300
  sources are selected from the context version; the frontend can force the backend
  with `--gl-backend auto|wgl|angle|software`. Windows ANGLE/EGL OpenGL ES 3.0 is
  validated on the development RTX 4070: all filters compile and a 320x240 Accurate
  capture matches the CPU oracle pixel-for-pixel. The VDLP bitmap path now honors the
  modulo-derived line width (320/384/512/640/1024). The palette shader was fixed in the
  eighth pass to use independent red, green, and blue CLUT indices; the previous shader
  used only the low five bits and made real games grayscale. An opt-in high-resolution
  mode (`--high-res`, `EmulatorTuning.HighResolution`) renders CELs and bitmap lines
  into four 1 MB VRAM planes and presents 640x480 by line-doubling the 640x240 source;
  Lemmings' title screen now matches the 640x480 reference. This is not true vertical
  640x480 rendering; the internal high-resolution research is M14. The mode is off by default
  because 320x240 titles must not be upscaled, and the desktop shell exposes it as a
  Hi-Res checkbox that restarts the machine. An opt-in xBR-lv2 4x filter, HQx
  2x/3x/4x filters, and CRT bloom are available in Settings; xBR and HQx are GPU
  passes over the GPU-expanded native source with sharp-bilinear finishing, and CRT
  bloom adds a 3x3 native-resolution glow in the CRT filter. The CPU HQx reference
  hashes remain the oracle, and the GPU HQx rule table matches them in the opt-in
  parity tests.
- **Performance:** the machine runs on a dedicated core thread paced by the 44.1 kHz
  audio clock (60 Hz fallback) with a frame pacer; the UI renders on demand; uploads use `TexStorage2D` + `TexSubImage2D`;
  uniform locations and the present delegate are cached; physical-pixel sizing is
  implemented and unit-tested at 100/125/150/200% scaling. The benchmark reports
  p50/p95/p99 and per-stage allocations, and the
  synthetic steady state is 0 bytes/frame. An opt-in bounded prefetch cache is
  available for CHD/ISO streaming. The `--gl-probe` validates both WGL and
  ANGLE/GLES: 4K sharp-bilinear presents in 0.14 ms mean / 0.47 ms p99 with 0
  renderer allocations/frame on the RTX 4070; a 320x240 Accurate capture matches
  the CPU oracle on both backends.
- **Optional Vulkan:** `FiveDo.Video.Vulkan` is an offscreen Vulkan 1.0 two-stage
  presenter with build-time SPIR-V shaders, sampled R16_UINT/CLUT/parameter
  textures, accurate/nearest/sharp-bilinear/CRT/xBR/FSR/HQx filters, GPU timestamp
  queries, and a host readback buffer. xBR-lv2 runs as a 4x GPU pass; FSR 1.0 runs
  EASU plus RCAS; HQx 2x/3x/4x run as a GPU rule-table pass over the palette texture.
  `--vk-probe` writes the shared
  GPU probe JSON and PNG captures; `--vk-window-probe` creates a native Win32
  swapchain, presents through FIFO vsync, and writes the same report with
  `offscreen: false`; `--gl-backend vulkan` hosts the optional desktop Win32 child
  surface. The 320x240 Accurate probe matches the CPU oracle
  (`0xD6BD89C5`, 0 different pixels); 4K sharp-bilinear measures 0.24 ms mean /
  1.06 ms p99 present and 0.075/0.091 ms GPU, 4K xBR 0.32/0.80 ms present and
  0.13/0.14 ms GPU, 4K FSR 0.60/1.30 ms present and 0.40/0.42 ms GPU, and 4K HQx
  2x/3x/4x 0.26/0.41, 0.29/0.34, and 0.34/0.52 ms present with 0.10/0.12/0.15 ms
  GPU, all with 0 managed bytes/frame on the RTX 4070. The 4K window probe reports
  0.077/0.088 ms GPU and 5.96/6.53 ms present (including the 165 Hz FIFO wait), 0
  managed bytes/frame. Compositor-embedded/X11/Wayland/Metal surfaces are future
  work. See [`Vulkan.md`](Vulkan.md).
- **Renderer robustness:** GL/GLES capability parsing reports the API and version,
  fullscreen/F11/Escape work, GL readback screenshots are implemented, and GL init
  failure or context loss switches the shell to the CPU `WriteableBitmap` presenter.
  An optional FPS/status overlay is available in the settings dialog.
- **Desktop shell:** the game library scans configured folders through the 4DO
  database, previews sibling cover art, and opens the selected disc. Library
  favorites, play time, launch history, and an optional offline metadata overlay
  persist in `library-state.json`/`library-metadata.json`; the game list adds star
  toggles, metadata/play-time text, collection filters, and sort orders. The settings
  dialog edits video, audio, emulation, and paths; keyboard bindings for all eight players can be captured with
  automatic conflict removal and reset-to-defaults; gamepads are mapped through a
  Gamepad tab (Windows XInput, Linux evdev, macOS IOKit HID). Save states use ten numbered slots with
  next/previous, a list/save/load/delete window, JSON metadata, and PNG thumbnails;
  screenshots are PNG files named by game, time, and frame. **File > Disc Browser...**
  lists the current disc's Opera tree and extracts a file, directory, or the whole
  disc; **File > Disc Info...** shows the volume, game ID, database metadata, and
  automatic fixes. **File > BIOS / NVRAM Manager...** lists the BIOS library, sets
  BIOS1/BIOS2 and per-game assignments, and backs up, imports, resets, or deletes
  shared/per-game NVRAM images. The shell has a first-run
  guide, error dialogs, and an in-app diagnostic log viewer with level/category/search
  filters. The app accepts a positional disc path or `--disc <path>` and ships a Linux
  `.desktop` entry; Windows/macOS associations remain installer work. Per-game 4DO fix
  flags are applied automatically when a disc opens; explicit masks can be set in the
  settings dialog. The shell dispatches the configurable `InputProfile.Hotkeys`
  bindings (F5 save, F8 load, F6/F7 previous/next slot, F9 frame advance, F10
  reset, F11 fullscreen, F12 screenshot, Escape pause by default) before forwarding
  keys to the emulated pad. **Emulation > Record Input** captures player-1 input to
  a replayable script. Loading a state
  whose recorded disc hash differs from the current disc reports a warning in the
  status line and log instead of loading silently.
- **Logging and bug reports:** Serilog is configured once by each host through
  `FiveDo.Diagnostics.Logging`. The pipeline writes rolling daily files, stderr
  (desktop), the in-memory **Log Viewer**, and an asynchronous bug-report sink for
  `Warning`-or-higher events. Reports include the required environment, error, and
  exception blocks; submission is deduplicated for five minutes, capped at ten per
  minute, and disabled in CI/test runs. `FIVEDO_BUG_REPORTS=0` disables it and
  `FIVEDO_LOG_DIR` redirects the files.
- **Cheats:** per-game JSON cheat files are applied before every frame by
  `FiveDoMachine.BeforeFrame`, so state loads, rewind replay, and fast-forward keep the
  configured entries active. The engine supports raw byte writes, typed
  `write8`/`write16`/`write32` writes, memory conditions, player-1 button conditions, and
  `and`/`or`/`not` combinations. Emulation > Cheats... and Settings > Paths > Cheats
  manage files; the CLI exposes `cheat list|add|enable|disable|remove|import|export` and
  `run --cheat`/`--cheat-code`/`--cheat-auto`. See [`Cheats.md`](Cheats.md).
- **Per-game fixes:** `GameCompatibility` maps the 4DO game IDs to `FixMode` bits.
  The CLI exposes `--fix-mode 0xMASK` and `--no-game-fixes`, and `run --json` /
  `info` report the applied mask. Alone in the Dark's `FIX_BIT_TIMING_6` is applied
  automatically; the current 3,000-frame boot keys are in
  [`TestCorpus.md`](TestCorpus.md) 1.1 after the audio-clock rebaseline.
- **Tooling:** CLI `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks`
  (`disc tracks` covers ISO/BIN/CUE as well as CHD), `fs list/extract` (the
  first-party Opera parser covers ISO/BIN/CUE/CHD),
  `disasm`, plus KPRINT, CD, CLIO, FIQ, RAM-write watchpoints,
  RAM/DSP dumps, DSP-instruction rings, CEL command/geometry traces, PBUS packet
  traces, CPU instruction-ring diagnostics, scripted input, NVRAM load/save, periodic
  frame capture (`--capture-dir`/`--capture-every`), RAM patches at run start
  (`--poke ADDR=HEX`), an experimental Road Rash road-following driver
  (`--autopilot road`), an opt-in bounded disc prefetch cache (`--prefetch`), and RAM
  dumps at trace stops. `run --json` reports the source size, VDL modulo, road
  coverage, autopilot state (including `autopilotMenuAdvance`), and CD status/poll
  for mode/crop, driver, and wait-state investigations.
- **Audio:** the DSP executes in every run, including headless runs with no host
  audio sink. The AudioFolio semaphore handshake works, instrument programs run, and
  the corpus produces non-silent 44.1 kHz stereo WAV output. The DSP sample clock is
  now exact: 44,100 samples per emulated second (600 frames = 441,015-441,020
  samples, 10.000 s), replacing FreeDO's 8.24 accumulator that ran ~0.7% fast
  (444,097 samples / 10.070 s). Road Rash's 6,000-frame capture has the same peak,
  RMS, and spectral peaks as before; only the timebase changed. Live output uses
  a bundled OpenAL Soft 1.23.1 runtime (no system OpenAL install required), makes
  the context current on the audio thread, pads underruns, and reports
  dropped/underrun/queued counters. The fill loop now rents only buffers OpenAL
  has released (the old queued-count index rewrote buffers still attached to the
  playing source and caused constant crackle), and `FIVEDO_AUDIO_TAP=<prefix>`
  records the exact queued PCM plus a per-buffer timing log. `CoreRunner` paces frames against the audio
  clock (with a 60 Hz fallback), the CLI can play live with `--audio openal
  [--realtime]`, and `audio analyze <wav> --json` reports peak/RMS/clipping/DC
  statistics. Exact-clock 600-frame captures: Wolfenstein 18,100 / 2,168,
  Lemmings 8,192 / 1,661, Gex 12,964 / 2,089, Road Rash 32,768 / 11,012 (2,036
  clipped), Theme Park silent; see `TestCorpus.md` 1.5a2. The trimmed win-x64 and
  linux-x64 CLI and the opt-in NativeAOT CLI open the same backend. The opt-in
  NativeAOT frontend completed 10-minute null and default-device soaks with
  **0 dropped samples / 0 underruns** (2026-11-10).
- **Input:** joypad, flightstick, mouse, lightgun, arcade lightgun, and Orbatak
  trackball devices are supported on six PBUS ports. The legacy 4DO packet remains the
  default for all-joypad configurations; selecting a peripheral emits Opera's
  daisy-chain records. Settings > Emulation > Input devices and the CLI
  `--input-device` select devices; `--pbus-format opera` forces the chain layout. The
  desktop pointer drives the first mouse/lightgun/trackball port and shows a crosshair
  for lightguns. See [`Architecture.md`](Architecture.md) §9.
- **Input scripting:** the CLI accepts `--input-script <path>` with
  `frame button[,button...]` lines for repeatable controller taps. Input has been
  validated in Road Rash (race state/wreck), Wolfenstein (menus, gameplay, level
  start), Alone (scene/menu/intro), Lemmings (menu and passcode entry), and Space Ace
  (X menu, A play).
- **Tests:** 1381 passing (`dotnet test -c Release`), including high-resolution CEL and
  VDLP regression coverage, RAM-patch and Road Rash autopilot contracts, CLI process
  tests, zero-allocation uploader/mailbox assertions, OpenGL capability parsing,
  bounded prefetch-cache tests, game-fix mapping, save-slot metadata/roundtrips and
  thumbnails, keyboard/gamepad binding capture, gamepad mapping, log filtering,
  source-generated CLI JSON contracts, frontend startup-argument parsing, renderer
  backend selection, GPU-probe capture/parity contracts, shader source invariants,
  Vulkan xBR/FSR/HQx split-frame orientation, save-state disc-hash warnings, hotkey
  routing, input-recorder coalescing/roundtrip, ISO/BIN `disc tracks`, Opera FS parsing
  (multi-block directories, `burst`/`gap` reads, invalid headers, and CLI ISO
  extraction), and BIOS catalog/scan/selection/override tests.
- **Packaging:** CI builds/tests/formats and publishes one bundle per system
  (`5do-win-x64.zip`, `5do-linux-x64.tar.gz`, `5do-osx-x64.tar.gz`). Each bundle
  contains the CLI and desktop app as framework-dependent single-file apphosts, the
  README/license/notices/whatsnew, and a generated `PACKAGE.txt`. The .NET 10 runtime
  is required on the host and is **not** bundled; PDB and XML documentation files are
  removed. Native OpenAL Soft/Skia/ANGLE libraries stay beside the apphosts for LGPL
  replacement. The CLI also has an opt-in NativeAOT win-x64 publish
  (`-p:FiveDoNativeAot=true`) that is CI-smoke-tested with `--version`, `selftest`,
  `bench`, `info`, `fs list`, a golden 3,000-frame boot, and live OpenAL audio. The
  frontend has an opt-in NativeAOT single-file publish that passes `--smoke-test` and
  the WGL/ANGLE parity probes. Unsigned installer/package scaffolding now covers the
  Windows Inno Setup installer with `.iso`/`.cue`/`.chd` associations, Linux
  `.deb`/`.rpm` packages with a desktop entry and `application/x-3do-disc` MIME type,
  and macOS `.app`/`.dmg`/`.pkg` with a document UTI. See
  [`Installers.md`](Installers.md) and
  [`packaging/README.md`](https://github.com/purelogiccode/5DO/blob/master/packaging/README.md).

---

## 1a. Latest compatibility sweep (2026-10-30)

The first full loadability pass over both collections is complete. It booted every
disc for 3,000 frames and recorded JSON/PNG evidence under `artifacts/compat-full/`.
The committed summary is [`GameCompatibilityResults.md`](GameCompatibilityResults.md).

| Result | Count | Meaning |
|---|---:|---|
| `Loadable/Rendered` | **593** | Completed 3,000 frames (18 rows retried at 20,000-65,000 frames); final frame is non-black. |
| `Loadable/No video` | **1** | Completed 65,000 frames; still stuck on a boot screen. |
| `Not Loadable` | **0** | No CLI error or timeout remains. |
| **Total** | **594** | 589 main collection + 5 MAME software-list images. |

The 2026-11-01 extended-boot retry reran every one of the 18 non-rendered rows. The
five former `Not Loadable` rows (`Crime Patrol`, `Mad Dog II`, `Mirai Shounen Conan`,
`Orbatak`, and `Shootout at Old Tucson RE3`) now complete and render at 20,000 frames
with `panafz10`, and 11 of the 13 former black/blank rows render at 20,000 frames or at
65,000 with the Anvil BIOS. The two remaining rows are known Anvil boot-screen hangs:
`Crash 'n Burn (Japan)`. WorldCup Super Stadium now renders when the FZ-10J BIOS is paired with its Kanji font ROM (BIOS2).

The 2026-11-01 retry reran all 13 former `Loadable/No video` titles. Ishida Yoshio
Kudan no Igo Seiha, both Konpeki no Kantai discs, all three Myst discs, Olympic Summer
Games, and all three Slayer discs render at 20,000 frames with `panafz10`; Powers
Kingdom (Europe) renders its live-action intro at 65,000 frames with
`panafz10e-anvil`. Only `Crash 'n Burn (Japan)` remains on the Anvil boot screen at 65,000 frames;
WorldCup Super Stadium renders with the FZ-10J BIOS and its Kanji font ROM.

`Loadable/Rendered` is **not** a final compatibility grade; it still needs the visual
and input review described in [`CompatibilitySweep.md`](CompatibilitySweep.md).

## 2. Real-game status (local corpus, `panafz1.bin`)

The table below is the curated five-title showcase set. The full local collection
(594 images across `J:\Panasonic 3DO` and `G:\MAME\MAME Software List CHDs\3do`) is
being swept in M13; the working matrix is
[`GameCompatibilityList.md`](GameCompatibilityList.md).

The 2026-10-24 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots, renders, plays DSP audio**; reaches the title screen and the passcode screen; with `--high-res` the 640x480 title renders all four buttons and the full logo, matching the reference. The Fun 4 code `HNLHCIOECW` is now typed entirely with the D-pad (`docs/scripts/lem-fun4-dpad.txt`); gameplay starts at 23,000 frames (`PC 0x00083FE8`, hash `0x63C02DED`) and roundtrips (`PC 0x000230A4`, `0x4CA339CD` twice). 600-frame WAV evidence in `TestCorpus.md` 1.5a | Not re-tested this pass; expected to pass |
| Space Ace (USA) | **Boots, renders, plays DSP audio**; ReadySoft player runs the animated demo past 12,000 frames. The interactive path is now validated: X returns to the title/menu, A starts the scene; the 15,000-frame state roundtrips (`0x7E780D93` -> `PC 0x000284EC`, `0x70B16422` twice) and the gameplay WAV is non-silent | **Plays the demo intro for 60 s, no runaway** |
| Road Rash (USA) | **Boots, renders, plays DSP audio**; the 3D race renders correctly and the attract race runs past 60,000 frames with no allocator error; B accelerates, A brakes, the D-pad steers, C attacks, and P pauses. The CD media-access pulse fixes the old ~42k/~92k READ_ID-only wait: those screens advance to `BUSTED`, the race-select menu, and a new race. The autopilot now passes `GAME LOADING`/`Get ready` and cycles race -> wreck -> menu -> new race to 100k, but still does not finish a full 5.5-mile race | **Renders the intro FMV, then a race** |
| Wolfenstein 3D (USA) | **Boots, renders, plays DSP audio**; A opens the preview menu, selecting a level starts a fresh game (FLOOR 1-1), the player can walk/turn/fire, and save/load roundtrips in-game (`0xF568EBCA` -> `PC 0x000782B8` twice); 600-frame WAV evidence captured. The fresh-game load advances with A pulses (character select -> FLOOR 1-1 at 64,000, hash `0x2F5B9864`) | **Renders the intro for 60 s** |
| Alone in the Dark (USA) | **Boots, renders, plays DSP audio**; reaches the in-game staircase/room with an upright character; the Up/Left/A/Down sequence reaches the Begin/Resume menu, A starts the intro scene, A pulses pass the intro, and a scripted room movement sequence roundtrips (`0x44C7576B` -> two 120-frame continuations at `PC 0x0008BF00`, `0xE490C8C7`) | **Renders the intro/loading for 60 s** |

**Additional corpus (2026-10-26).** Seven more titles were booted, rendered, and
save-state-roundtripped with `panafz1.bin`: Gex, Need for Speed, Super Street
Fighter II Turbo, Total Eclipse, Killing Time, Theme Park, and Return Fire. Gex,
Total Eclipse, and Super Street Fighter II Turbo show gameplay/attract scenes;
Need for Speed, Killing Time, Theme Park, and Return Fire render menus. Six produce
non-silent 44.1 kHz stereo audio; Theme Park's captured menu is silent. Hashes and
commands are in [`TestCorpus.md`](TestCorpus.md) 1.7 and the matrix in
[`Compatibility.md`](Compatibility.md) §10.

The CD disc-end fix on 2026-10-23 is what unblocked Road Rash, Space Ace, and
Wolfenstein: `InitCd` no longer double-adds the 150-frame lead-in, so the CD DIPIR
accepts discs whose volume block count is less than the physical track length.

The 2026-10-25 eighth pass fixed a real-content renderer bug: the GPU palette shader
used the low five bits of each pixel as a single CLUT index for all three channels,
so games appeared grayscale. It now uses the red, green, and blue indices independently
and matches the CPU frame assembler. Lemmings gameplay was captured through
sharp-bilinear and xBR with correct colors. The 2026-10-29 pass removed the former
CPU-expanded xBR source; xBR now samples the GPU palette texture directly.

The 2026-10-24 session found and fixed the common blockers:

1. **DSP execution.** `AudioSink?.PushSample(Dsp.Loop())` skipped the DSP entirely
   when the host had no audio sink, which is the default headless path. The machine
   now always executes the DSP; the sink only receives samples.
2. **DSP immediate operands.** The loader shifted un-justified immediates left by
   two instead of zero. The AudioFolio tail program's `Sema4Status & 8` test became
   `Sema4Status & 0x20`, so the DSP always skipped the ARM command. The fix uses the
   reference's `IMMEDIATE << (JUSTIFY & 3)`. SAudio `-6996` is gone and the DSP now
   runs instrument code.
3. **DSP multiplier rounding.** MUXB=3 now masks the product
   (`(MULT1 * (Y >> 15)) & ~1`) instead of masking the shifted accumulator first.
4. **CCB field order.** The parser now reads position, size, perspective, and
   pixel-processor control before `PRE0`/`PRE1`, matching FreeDO. The old order
   mis-decoded every 3D CEL; Road Rash's race produced a bogus 1987x583 CEL and
   appeared to hang. The rasterizer's missing `cnt_cross > 2` x-point sort was also
   ported.
5. **NVRAM initialization.** The CLI now loads a fresh 32 KB NVRAM volume by default
   (and `--nvram <path>` persists it). Without it, Alone in the Dark saw zero free
   save slots, displayed its save-space warning, and shut down. It now reaches the
   in-game room/inventory scene.
6. **Arbitrary-CEL transposition.** `TexelDrawArbitrary` passed `(i, j)` to `(x, y)`
   read/write helpers, so every asymmetric quad was written transposed. Road Rash's
   race CELs overwrote the OS VRAM free list at `0x2FC000` (`AllocMem error, bad node
   address`) and the race froze; Alone's character was drawn horizontally. Passing
   `(j, i)` fixed both. The VDLP bitmap copy now uses the modulo-derived line width
   (no 320-pixel crop), and state loads restore the VDL pixel buffers.

Space Ace's former frame-681 crash was a downstream consequence of the failed
`AllocInstrument`: the player copied a decoded buffer to a null pointer, wiping the
ARM vectors. It now runs past 12,000 frames.

**Gameplay is now reachable and renderer-correct.** Road Rash's 3D race renders like
the Opera reference and runs past 60,000 frames; Wolfenstein 3D starts a fresh game,
walks/turns/fires, and its save states roundtrip; Alone in the Dark's in-game character
is upright and its Begin/Resume menu is reachable; Lemmings completes a Fun 4 passcode
(`HNLHCIOECW`) end-to-end into gameplay with save/load roundtrip, and its 640x480
high-resolution title matches the reference; Space Ace starts its interactive scene
(X -> menu, A -> play) and plays. Per-title save-state, PNG, and WAV evidence is
recorded in `TestCorpus.md` 1.5a.

The ninth pass corrected the Road Rash diagnosis and fixed the wait: the ~42k and
~92k stops were a static READ_ID-only CD wait, not wall pins or driving failures.
The CD device now raises a media-access event (`0x40`) after two consecutive
READ_ID polls with an idle gap; this advances those screens to `BUSTED`, race
select, and a new race. `run --json` exposes `cdStatus`/`cdPoll`, and the autopilot
reports `autopilotMenuAdvance` while it pulses A through the post-race flow. The
3,000-frame keys were later re-baselined by the exact audio clock; see
[`TestCorpus.md`](TestCorpus.md) 1.1. From the 60k state the autopilot now starts
a new race at 80k and cycles race -> wreck -> menu -> new race to 100k; the
remaining Road Rash work is finishing a full race without wrecking, not the CD
wait. That is post-1.0 backlog; it is not a v1 release gate.

---

## 3. Performance snapshot

Release build, development machine, 600 frames, CPU side only:

| Case | Total | p50 | p95 | p99 | Allocations |
|---|---:|---:|---:|---:|---:|
| Synthetic | **1.250 ms/frame** | 1.218 ms | 1.434 ms | 1.638 ms | **0 bytes/frame** |
| Lemmings CHD | **1.281 ms/frame** | 1.088 ms | 2.005 ms | 3.398 ms | ~5.9 KB/frame (CHD decode) |
| Lemmings CHD, high-res | **1.605 ms/frame** | 1.398 ms | 2.497 ms | 4.598 ms | ~5.8 KB/frame (CHD decode) |
| Road Rash 3D race (100k state) | **4.20 ms/frame** | 2.15 ms | 7.13 ms | 7.40 ms | **0 bytes/frame** |

This leaves ~15.4 ms of headroom against the 16.67 ms frame budget for the synthetic
case. The 2026-10-31 hot-path pass reduced the Road Rash race from 5.46 to 4.20 ms
mean and from 13.14 to 7.40 ms p99 (frame hash unchanged). The core projects are also
compiled with JIT optimizations in Debug, so an IDE Debug run is no longer limited to
~40 fps. CI now runs the synthetic benchmark with `--gate`: it fails on any steady-state
allocation, core p99 > 8 ms, total mean > 8 ms, or total p99 > 16.667 ms, and uploads
the JSON result as the baseline artifact. The benchmark's `--baseline` option also
implements the same-machine >10% relative comparison (core p99, total mean, total
p99; exit code 4); wiring it to CI awaits a stable/self-hosted runner. **Tier B
desktop numbers and GLES timing are not measured yet.** CHD decode allocations are
host-side and are attributed to the core stage; the synthetic
core/pack/mailbox/read path is allocation-free in steady state.

The desktop renderer uses a `GL_TIME_ELAPSED` query and reports GPU/present time in
the FPS overlay. The fourteenth pass added a true-4K offscreen `--gl-probe` and
measured the presentation path on Windows 11 (Ryzen 9 7900, RTX 4070, OpenGL 4.0):

| 4K case (3840x2160) | Present mean | Present p99 | GPU mean | GPU p99 | Renderer alloc |
|---|---:|---:|---:|---:|---:|
| Sharp-bilinear | **0.080 ms** | **0.208 ms** | **0.086 ms** | **0.089 ms** | **0 B/frame** |
| Accurate | 0.082 ms | 0.178 ms | 0.091 ms | 0.287 ms | 0 B/frame |
| CRT | 0.147 ms | 1.157 ms | 0.191 ms | 0.193 ms | 0 B/frame |
| xBR 4x | 0.283 ms | 1.466 ms | 0.185 ms | 0.324 ms | 0 B/frame |
| FSR (EASU + RCAS) | 0.132 ms | 1.191 ms | 0.441 ms | 0.483 ms | 0 B/frame |

The xBR row now uses the GPU-expanded palette texture directly; the previous path
uploaded a CPU `FrameAssembler` copy. The optional Vulkan offscreen backend was also
measured on the same machine: 4K sharp-bilinear present 0.24 ms mean / 1.06 ms p99,
GPU 0.075/0.091 ms, 0 managed bytes/frame; 4K xBR present 0.32/0.80 ms, GPU
0.13/0.14 ms; 4K FSR present 0.60/1.30 ms, GPU 0.40/0.42 ms; 4K HQx 2x/3x/4x
present 0.32/0.83, 0.32/0.48, and 0.40/0.69 ms with GPU 0.10/0.11, 0.12/0.13, and
0.15/0.16 ms; 1080p sharp-bilinear present 0.17/0.79 ms, 1080p xBR
0.21/0.38 ms, and 1080p FSR 0.27/0.78 ms, all with 0 managed bytes/frame. GPU time
excludes the optional per-frame readback. Its 320x240 Accurate output matches the CPU
oracle (`0xD6BD89C5`, 0 different pixels). See [`Vulkan.md`](Vulkan.md).

The platform swap interval is wired through `SwapInterval.TryCreate` (WGL/GLX/EGL);
the Windows WGL path resolves `WGL_EXT_swap_control` and reports `vsyncActive: true`
when requested. The Avalonia compositor paces the window independently, so the probe
reports ~64 fps throughput even when vsync is requested; the renderer cost itself is
far below the 16.67 ms budget. `DisplayRefreshRate` (Settings > Video, default 60)
re-presents the latest frame above 60 Hz when the user wants a faster swap rate; it
does not add motion because the emulated 3DO still outputs 60 fields per second.

### 4K60 feasibility

- **Desktop (Tier A): measured.** The core is ~1.25-1.6 ms/frame; the 4K presentation
  pass is one fullscreen textured quad after native-resolution palette expansion.
  The RTX 4070 measured GPU p99 0.089 ms and present p99 0.208 ms for sharp-bilinear,
  leaving ample headroom for 60 Hz. The remaining risk is the Avalonia/WGL compositor
  and frame pacing, not the emulator core.
- **Tier B desktops: unmeasured.** The presenter should handle 1080p60; integrated
  GPUs and older CPUs may be the limiting factor in complex scenes.
- **4K internal CEL rendering is not a v1 goal.** The target is 4K output, not 4K
  internal rendering.

CPU p99 and per-stage allocations are measured, and Tier A 4K GPU/present p99 is now
recorded. Remaining performance work is post-1.0: GLES 3.1 and DPI validation,
Tier B timing, and the CI relative gate. WebAssembly, Android/iOS, and Raspberry Pi
are out of scope. See [`Performance.md`](Performance.md).

---

## 4. How far from a release?

The original 157 developer-day plan assumed full compatibility hardening and a full
platform matrix. **v1.0 adopts a documented-compatibility release scope instead.**
Ship the platforms and titles that are validated, publish the per-title matrix and
known issues, and move the rest to the post-1.0 backlog. Most emulators release this
way; full compatibility is not a v1 requirement.

**As of 2026-11-02 the v1.0.0 documented-compatibility release is published on
[GitHub](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0).** M13 loadability is
complete (594 tested, 593 render, 1 known boot failure, 0 not loadable), legal/attribution
sign-off is recorded, and M14 is post-1.0 research. Linux x64 CLI/frontend smoke and the
full test suite passed (see [`Compatibility.md`](Compatibility.md) §10.3); the GL probe
timed out under llvmpipe, so Linux GPU presentation is preview/best-effort. The release
artifacts and checksums are attached to the GitHub release.

| Release workstream | State | Remaining |
|---|---|---:|
| M0-M8 core, CPU, boot, MADAM, DSP, CD, input, save states | Functionally complete | 0 |
| M9 Avalonia frontend | Feature-complete for the tested workflow | 0 (optional polish is post-1.0) |
| M9R OpenGL presenter | Windows WGL + ANGLE validated; CPU fallback, filters, parity, 4K probe | 0 on Windows; Linux x64 full suite passed, GL probe timed out under llvmpipe |
| M9P performance/pacing | Tier A measured, zero-allocation steady state, CI gate | 0 (Tier B/Pi and hot-path are post-1.0) |
| M10 packaging | Unified Windows/Linux/macOS x64 bundles, framework-dependent single-file apps, optional NativeAOT, unsigned installer/package scaffolding | 0 (signed/notarized installers require certificates; macOS package build needs a macOS host) |
| M11 compatibility | 12 titles boot/render; five have scripted gameplay evidence; matrix and known issues documented | 0 |
| **M13 compatibility sweep** | **Loadability complete: 594 tested, 593 render, 1 known boot failure** | M13.5/M13.6 post-1.0; Linux x64 full suite passed |
| **M14 high-res CEL research** | **Research plan written; post-1.0** | **Not a v1.0 gate** |
| M11 release/legal | OpenAL notice/source offer/relinking record and license text ship in every bundle; FreeDO modified GNU LGPL, donationware | Legal sign-off recorded 2026-11-02 |

**Current estimate:** roughly **100% complete** against the documented-compatibility
release scope. v1.0.0 is published on GitHub with the release artifacts.

**Release decisions (2026-11-02):** Windows x64 is validated; Linux x64 CLI/frontend
smoke and the full test suite passed (GPU presentation is preview/best-effort); the current release version is `1.6.0`;
installers, signing, and notarization are post-1.0; the compatibility set is the 12
documented titles, with five showcase titles carrying scripted gameplay evidence. The
distribution model is recorded as **donationware** (non-commercial, source-available).

The project license is recorded in [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE) as the FreeDO modified
GNU LGPL, so release builds are **donationware**: non-commercial, source-available, and
funded by voluntary donations rather than sales. The project-owner legal/attribution
sign-off is recorded in [`ReleaseLegalReview.md`](ReleaseLegalReview.md). The OpenAL Soft
LGPL-2.0-or-later binary-distribution obligation is handled: the notice, complete
license text, three-year source offer, and relinking instructions ship in
[`OpenALSoft.md`](OpenALSoft.md) and
[`OpenAL-Soft-COPYING.txt`](OpenAL-Soft-COPYING.txt). See
[`Licensing.md`](Licensing.md), [`ReleaseLegalReview.md`](ReleaseLegalReview.md), and
[`THIRD-PARTY-NOTICES.md`](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md).

Gameplay-depth and full-compatibility work is **post-release**, not a current gate:
finishing Road Rash's 5.5-mile race, scripting Alone's inventory, adding gameplay
scripts for the other seven titles, and further CD/CLIO/MADAM/ARM timing triage.
M13.5/M13.6 visual/high-resolution review and M14 research are post-1.0. The current
v1.0 gate is release mechanics (version bump, tag, and artifacts).

---

## 5. Current path (compatibility and video quality)

### P0 - v1.0 release work

1. **M13 loadability sweep: complete.** 594 discs tested: 593 `Loadable/Rendered`,
   1 known `Loadable/No video` failure (`Crash 'n Burn Japan`), 0 `Not Loadable`. See
   [`GameCompatibilityResults.md`](GameCompatibilityResults.md).
2. **Failure triage: complete.** The five former not-loadable rows and 12 black/blank
   titles are resolved or explained; Crash 'n Burn (Japan) remains the only known boot
   failure. WorldCup Super Stadium renders with the FZ-10J BIOS plus its Kanji font ROM.
3. **Linux x64 validation: complete (2026-11-02).** The full test suite
   (**1,473/1,473**), CLI `selftest`, `bench`, `info`, `bios info`, a 3,000-frame
   Wolfenstein boot, non-silent audio, and the frontend `--smoke-test` passed on Linux
   Mint 22.3. The GL probe timed out under llvmpipe, so Linux GPU presentation is
   preview/best-effort. See [`Compatibility.md`](Compatibility.md) §10.3.
4. **Release v1.0.0: complete (2026-11-02).** The version is bumped, the source is
   tagged `v1.0.0`, the unified Windows/Linux/macOS x64 bundles and checksums are
   attached to the [GitHub release](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0),
   and the release-owner/QA sign-off is recorded.
5. **M13.5/M13.6 visual and high-resolution review (post-1.0).** Inspect every rendered
   final frame, record `Unplayable`/`Playable`/`Good`, and run the `-HighRes` comparison.

### P1 - research and later work

6. **M14 high-resolution CEL research (post-1.0):** measure the current mode, prototype
   true 640x480 and 2x supersampling, and write the GPU CEL feasibility note.
7. **Compatibility fixes:** only after the failure classes are triaged and compared
   with the 4DO/Opera reference.
8. **Gameplay depth (deferred):** finish a full Road Rash race, script Alone's
   inventory, and add input scripts for the other seven titles.
9. **Platforms/performance/packaging (post-1.0):** Pi/GLES 3.1, arm64 hardware, DPI,
   Tier B timing, installers/signing, file associations, and the optional M12 JIT.
10. **Release status:** v1.0.0 is published on GitHub with the artifacts attached.
    Legal/attribution sign-off is recorded, CI is green, and Linux x64 full suite
    passed. Post-1.0 work remains.

---

## 6. Next session

**v1.0.0 is published on GitHub** (source tag `v1.0.0`, unified
Windows/Linux/macOS x64 bundles and checksums attached). Linux x64 validation passed on
Linux Mint 22.3: the full test suite (**1,473/1,473**), CLI `selftest`/`bench`/`info`,
a 3,000-frame Wolfenstein boot, non-silent audio, and the frontend `--smoke-test`. The
GL probe timed out under llvmpipe and is a documented preview/best-effort limitation. The BIOS-aware
retry is complete: **593 of 594 discs render**, and only Crash 'n Burn (Japan) remains
stuck on the Anvil boot screen. The next session is **post-1.0**:

1. The v1.0.0 GitHub release is published; collect feedback and compatibility reports.
2. Run the M13.5/M13.6 in-game/visual and high-resolution review.
3. Start M14 from [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

Do not re-enable the null audio-sink short-circuit, do not change the XBUS DMA byte
order (the raw `IRam` order is correct), do not port `opera_cdrom.c` wholesale, and do
not redo the trimmed CLI/frontend or source-generated JSON work; see
[`Trimming.md`](Trimming.md). Do not treat full-race completion, scripts for the other
seven titles, Pi/arm64 validation, or installers as current gates; they are later
backlog.

---

## 7. Reproduce

```powershell
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release

# The corpus roots are machine-specific; set them once per shell.
$env:FIVEDO_GAMES = "J:\Panasonic 3DO"
$env:FIVEDO_MAME  = "G:\MAME\MAME Software List CHDs\3do"
$env:FIVEDO_DISC  = "$env:FIVEDO_GAMES\Road Rash (USA).chd"

# Full-collection loadability sweep (3000 frames, resume-safe)
pwsh -File tools/compat-sweep.ps1 -Frames 3000 -Resume -OutDir artifacts/compat

# Regenerate the 4DO reference table after changing the collection
python tools/generate-compat-list.py

# Build/test/format
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release
dotnet format --verify-no-changes

# Synthetic CPU-side baseline
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- --frames 600

# Real disc baseline
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin `
  --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" --label lemmings

# Boot a disc and dump the last frame
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --nvram artifacts/lemmings.ram --frame-out artifacts/lemmings.png

# Scripted input, frame sequence, and PBUS packet trace
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Wolfenstein 3D (USA).chd" --bios bios/panafz1.bin `
  --frames 12000 --input-script artifacts/input.txt `
  --capture-dir artifacts/w3d-frames --capture-every 1000 `
  --frame-out artifacts/w3d.png --state-out artifacts/w3d.state

# RAM dump at a KPRINT stop
dotnet run -c Release --project src/FiveDo.Cli -- trace `
  "$env:FIVEDO_DISC" --bios bios/panafz1.bin `
  --frames 16000 --trace-frames 2200 --stop-kprint "AllocMem error" --ring 40000 `
  --out artifacts/rr-trace.txt --dump-ram artifacts/rr-stop-ram.bin `
  --dump-ram-offset 0 --dump-ram-length 0x300000

# Opera reference: extract the data track to ISO, then run the bundled SimpleGui
dotnet run -c Release --project src/FiveDo.Cli -- disc dump `
  "$env:FIVEDO_DISC" --sector 0 --count 328130 `
  --out "$env:TEMP\roadrash.iso"
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\roadrash.iso"
```

The desktop frontend is `dotnet run -c Release --project src/FiveDo.Frontend.Avalonia`.
See [`ReferenceStrategy.md`](ReferenceStrategy.md) for the full differential procedure.
