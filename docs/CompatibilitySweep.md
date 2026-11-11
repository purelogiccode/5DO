# Full-Collection Compatibility Sweep

**Status:** first loadability pass complete (2026-10-30): **594 discs tested, 576
rendered, 13 black/blank, 5 not loadable**. After the extended-boot and BIOS cross-test
triage: **593 loadable/rendered, 1 known no-video case (`Crash 'n Burn` Japan), 0
not-loadable rows**. The four Arcade CD-ROM titles require `panafz10`-class BIOS
behavior, and several black-screen titles are BIOS- or region-sensitive. See
[`GameCompatibilityResults.md`](GameCompatibilityResults.md) and
[`Compatibility.md`](Compatibility.md) §13.2.
**Release status (2026-11-02):** loadability is complete and is the v1.0 compatibility
baseline. M13.5/M13.6 visual and high-resolution review are post-1.0; the M13.7 Linux
x64 smoke test is the remaining v1.0 platform gate.
**Goal:** give every disc in the local 3DO collection a recorded 5DO status, starting
with loadability and then an in-game/visual check. Gameplay-depth scripts (full races,
inventory interaction, long playthroughs) are deliberately deferred.

This document is the procedure and evidence policy for the sweep. The reference list
and the per-title result columns are in
[`GameCompatibilityList.md`](GameCompatibilityList.md).

---

## 1. Inputs

| Input | Path | Count |
|---|---|---:|
| Main collection | `J:\Panasonic 3DO` | 1,366 CHD images |
| MAME software-list collection | `G:\MAME\MAME Software List CHDs\3do` | 5 CHD images |
| Unique-title inventory | `artifacts/corpus/inventory.json` | **1,371 images → 414 titles** |
| Unique game test set | `artifacts/corpus/unique-testset.txt` | **378 games** |
| BIOS | `bios/panafz1.bin` (baseline) | 1 MB, user-provided |
| Alternate BIOSes | `panafz10.bin`, `panafz10-patched.bin`, `panafz10e-anvil.bin`, `panafz10ja-anvil-kanji.bin` | 1 MB each; used for BIOS-sensitive titles |
| 4DO reference list | `References/4DO-master/4DO Compatibility List ⚈ 3DO emulator Wiki ».mhtml` | 241 titles |
| Phoenix reference list | `References/Phoenix/3DO Compatibility List _ Arts-Union.mhtml` | 302 titles (100% list) |

The two collections contain regional variants and alternate revisions. The **loadability
sweep** may test every image; the **compatibility matrix** tests one canonical disc per
unique title, because regional copies do not add playability signal. `tools/corpus-inventory.py`
fingerprints every image with the 5DO `gameId`, and `tools/generate-compat-comparison.py`
collapses duplicates, groups revisions, chooses the canonical disc, and writes the 5DO/4DO/Phoenix
matrix. The full mapping is `docs/data/unique-corpus.csv`; the comparison is
`docs/CompatibilityComparison.md`.

Regenerate the reference table after changing a collection or the 4DO snapshot:

```powershell
python tools/generate-compat-list.py
```

Build the unique-title inventory and the 5DO/4DO/Phoenix comparison after changing
the collection, a reference list, or the sweep results:

```powershell
python tools/corpus-inventory.py --jobs 8 --out artifacts/corpus/inventory.json
python tools/generate-compat-comparison.py `
  --inventory artifacts/corpus/inventory.json `
  --sweep artifacts/compat-full/summary-merged.csv `
  --reference docs/GameCompatibilityList.md
```

`corpus-inventory.py` caches by path; pass `--refresh` to re-fingerprint every image.
`generate-compat-comparison.py` writes `docs/CompatibilityComparison.md`,
`docs/data/unique-corpus.csv`, `docs/data/compatibility-comparison.json`, and the
canonical path list in `artifacts/corpus/unique-testset.txt`.

Tier B playability grades are recorded in `docs/data/playability-grades.csv`
(`file,status,notes,batch`). The comparison generator merges that ledger over the
manual grades in `GameCompatibilityList.md`, so a batch review does not require
editing the 241-title 4DO table. Batch runs use
`python tools/build-tierb-manifest.py` and `python tools/playability-sweep.py`;
review the captures with `python tools/tierb-contact-sheet.py`.

The older generator uses only the Python standard library and rewrites
[`GameCompatibilityList.md`](GameCompatibilityList.md). It matches titles by normalized
filename, not by disc hash, so verify a match before treating it as the same revision.
The new comparison groups by `gameId` first and is the authoritative 3-way view.

After updating [`GameCompatibilityResults.md`](GameCompatibilityResults.md) or the reference
list, rebuild the data used by the [compatibility browser](CompatibilityBrowser.md):

```powershell
python tools/generate-compat-site.py
```

Add `--import-screenshots` when the sweep artifacts contain new final-frame PNGs; the script
copies them into `docs/assets/compat/` and records their paths in
`docs/data/compatibility.json`. The docs CI job runs `--check` and fails if the JSON is stale.

---

## 2. Status definitions

The sweep uses the same five-level scale as the 4DO wiki:

| Status | Meaning in 5DO |
|---|---|
| **No data** | Not tested yet, or the disc could not be identified. |
| **Not Loadable** | The CLI fails, times out, or does not complete the requested frames. |
| **Unplayable** | Boots and renders something, but freezes, ignores input, or has severe graphical/audio defects. |
| **Playable** | Boots to a menu/in-game scene and responds to input, but has known issues or cannot be completed. |
| **Good** | Boots, renders, plays audio, responds to input, and has no known significant problem in the tested path. |

For the automated loadability pass, two intermediate labels are recorded:

- **Loadable/Rendered** — the CLI completed the frame count and the final frame is
  non-black with several distinct colors.
- **Loadable/No video** — the CLI completed the frame count, but the final frame is
  black or nearly blank. This may be a legitimate black transition or a real failure;
  a human must inspect the screenshot and a later frame.

These intermediate labels are converted to the five-level status after visual review.

The compatibility plan uses a **binary playable rule** on top of the quality scale:
`Good` and `Playable` both count as playable; the quality label is information, not a
losing comparison against a peer's `Good`. The beyond-title pass records the stricter
result separately:

| Beyond-title status | Meaning |
|---|---|
| **Playable** | A distinct menu, options, character-select, gameplay, or FMV/content screen is reached; a loop that never leaves the title/attract screen does not count. |
| **Title-only** | Boot/title/attract only. |
| **No video** | No usable non-black frame. |
| **Not loadable** | The CLI failed, timed out, or produced no captures. |

A `Rendered` title stays **Unverified** in the comparison until a beyond-title pass runs;
it becomes `Behind` only after that pass fails.

---

## 3. Automated loadability sweep

### 3.1 Build the CLI

```powershell
dotnet build src/FiveDo.Cli/FiveDo.Cli.csproj -c Release
```

### 3.2 Smoke-test the sweep

Run two discs first. This verifies the CLI path, BIOS path, JSON parsing, timeout
handling, and screenshot analysis without spending an hour on the full collection.

```powershell
pwsh -File tools/compat-sweep.ps1 `
  -Limit 2 -Frames 3000 -TimeoutSeconds 120 `
  -OutDir artifacts/compat-smoke
```

### 3.3 Run the full collection

```powershell
pwsh -File tools/compat-sweep.ps1 `
  -Frames 3000 -TimeoutSeconds 180 `
  -OutDir artifacts/compat
```

`3000` frames is 50 seconds of emulated time at 60 Hz. It reaches the title/menu for
most discs. Use `-Frames 12000` (about 3.3 minutes) for a deeper boot check when the
first pass leaves a title at a black or static screen.

The base pass is only a screening tool. The 2026-11-01 retry of the 18 non-rendered
rows used 20,000 frames with `panafz10` and 65,000 frames with `panafz10e-anvil` for
Anvil-dependent titles. It rendered 18 of the 18 after WorldCup Super Stadium was retried with the FZ-10J
BIOS plus its Kanji font ROM (BIOS2). Only Crash 'n Burn Japan remains a known boot
failure; the BIOS library now classifies 1 MB Kanji font images as secondary ROMs.

### 3.3.1 Retry only the blank rows

`-RetryFrames` reruns a disc once when the first pass completes with a blank frame, and
keeps the retried row only if it renders. `-Files` targets explicit paths instead of
scanning the roots:

```powershell
pwsh -File tools/compat-sweep.ps1 `
  -Files "J:\Panasonic 3DO\Myst (USA).chd","J:\Panasonic 3DO\Powers Kingdom (Europe).chd" `
  -Bios bios/panafz10.bin -Frames 20000 -RetryFrames 65000 `
  -TimeoutSeconds 600 -OutDir artifacts/compat-retry
```

For a per-title BIOS, run a second pass with `-Bios bios/panafz10e-anvil.bin` and merge
the better row. Record the BIOS in the report row; a result is only meaningful for the
BIOS that produced it.

The sweep:

- starts a fresh CLI process for every disc,
- uses a fresh in-memory NVRAM image (no shared NVRAM between games),
- enables the bounded disc prefetch cache by default,
- writes `result.json`, `stderr.txt`, and `frame.png` per disc,
- writes `summary.csv` and `summary.md` in the output directory,
- classifies `Not Loadable` on non-zero exit, timeout, invalid JSON, or a short frame
  count.

After a full run, turn the CSV into the committed results document:

```powershell
python tools/generate-compat-results.py `
  --results artifacts/compat-full/summary.csv `
  --out docs/GameCompatibilityResults.md
```

Useful options:

| Option | Purpose |
|---|---|
| `-Resume` | Skip discs whose `result.json` already exists and parses. |
| `-Bios <path>` | BIOS image; the default is `bios/panafz1.bin`. |
| `-Limit N` | Test only the first N discs. |
| `-Filter '*.chd'` | File-name filter; the default is CHD. |
| `-HighRes` | Pass `--high-res` to the CLI for the high-resolution pass. |
| `-NoFrameCapture` | Faster run without PNG evidence or image metrics. |
| `-NoPrefetch` | Disable the CLI's bounded disc cache. |
| `-Build` | Build the Release CLI before the sweep. |
| `-Cli <path>` | Use a specific CLI executable or `.dll`. |
| `-MinNonBlackPercent N` | Rendered threshold; defaults to 3% of sampled pixels. |
| `-MinColors N` | Rendered threshold; defaults to 8 distinct colors. |

The rendered test rejects the 3DO boot starfield, which has many colors but only about
2% non-black sampled pixels. Adjust the thresholds only when a known title needs it.

### 3.3.2 BIOS selection

`panafz1.bin` is the baseline BIOS, not a universal reference. The 2026-10-30 triage
found that the four Arcade CD-ROM rows need `panafz10-patched.bin` plus the default
headless pad; unpatched `panafz1`/`panafz10` reach only a starfield or the 3DO
storage-manager shell. Crash 'n Burn, Powers Kingdom (Europe), and WorldCup Super
Stadium only leave their black or solid-blue boot state with the Anvil BIOS. Slayer is
region-sensitive: Europe renders with `panafz1`, while USA needs `panafz10`,
GoldStar, or Sanyo. Always record the BIOS used with a result.

The `bios-probe.ps1` helper retries the sweep's failures with several BIOS images and
keeps the best result:

```powershell
pwsh -File tools/bios-probe.ps1 `
  -Csv artifacts/compat-full/summary.csv `
  -Bios 'bios/panafz1.bin,bios/panafz10-patched.bin,bios/panafz10e-anvil.bin' `
  -Frames 12000 -OutDir artifacts/bios-probe
```

Do not compare a `panafz1` black screen with a `panafz10-patched` render as if they
were the same test. The final 5DO status needs the BIOS in its notes.

### 3.4 Deeper in-game pass

The 3,000-frame pass is a **loadability/title-screen** check. For an in-game check,
rerun the titles that reached a title/menu with 12,000 frames (about 3.3 minutes of
emulated time) and capture a frame sequence:

```powershell
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "J:\Panasonic 3DO\<game>.chd" --bios bios/panafz1.bin `
  --frames 12000 --prefetch `
  --capture-dir artifacts/in-game/<game> --capture-every 3000 `
  --frame-out artifacts/in-game/<game>-final.png --json
```

Use the captures to answer: does the game leave the title/menu, render an in-game
scene, and respond to a short A/B/Start input? Record the answer in the 5DO status
column. A game that only shows a title screen is `Playable` at best, not `Good`.

### 3.5 Resume and rerun policy

The full sweep can take roughly 60-120 minutes depending on disc count, CHD decode
speed, and timeouts. `-Resume` makes it safe to stop and continue; the script refreshes
`summary.csv` every ten discs and keeps prior rows when resuming. A disc is rerun when
its `result.json` is missing, does not contain `"ok": true`, or has no prior summary
row.

Do not delete a failed `result.json` until its evidence has been reviewed; it is the
record of the failure.

---

## 4. Beyond-title playability pass

The loadability sweep answers "does it render?". The **beyond-title** pass answers the
compatibility plan's playable rule: does the title advance past the title/attract screen
to a distinct menu, options, character-select, gameplay, or FMV/content screen, and does
scripted input change the machine?

Run the peer-gap queue (titles a peer reports playable while 5DO is still unverified):

```powershell
python tools/beyond-title-sweep.py `
  --scope peer-gap --jobs 10 `
  --out artifacts/beyond-title `
  --ledger docs/data/beyond-title-results.csv
```

Scopes are `peer-gap` (the 147-title peer-gap queue), `unverified` (every non-playable
title in the 378-game set), and `all` (all 378 titles). `--only "base,base"` selects
specific titles, and `--limit N` caps a pilot.

The runner starts one CLI process per title with auto BIOS selection, captures at
`120, 600, 1200, 2400, 3600, 4800, 6000, 7200, 8400, 9600, 10800, 12000` frames, and
records CLI checkpoints at `120, 600, 1800, 3600, 7200, 12000`. It then repeats the
first 3,600 frames **without** the input script. Exact checkpoint frame hashes and
capture difference hashes prove whether scripted input changed the machine. Pass
`--deep-control` to repeat the full frame count instead; that catches titles whose
first input effect appears after 3,600 frames.

The classifier writes one of four final states:

| Status | Meaning |
|---|---|
| **Playable** | A non-black beyond-title screen is reached and scripted input changes the machine, or the first rendered content screen accepts input, or several content screens advance without input. |
| **Title-only** | The title/attract screen is static or loops and no distinct beyond-title screen follows. |
| **No video** | Every capture is black or nearly blank. |
| **Not loadable** | The CLI failed, timed out, or produced no captures. |

Ambiguous cases carry `review=yes` in the summary. Review them with the contact-sheet
tool before publishing a ledger:

```powershell
python tools/beyond-title-contact-sheet.py --dir artifacts/beyond-title --status Playable --max 20
python tools/beyond-title-contact-sheet.py --dir artifacts/beyond-title --status Title-only --max 20
```

Per-title BIOS, BIOS2, input-device, script, frame-count, and unsigned-disc overrides are
read from `benchmarks/beyond-title/overrides.json`, keyed by the comparison `base`:

```json
{
  "worldcup super stadium": {
    "bios": "bios/panafz10j.bin",
    "bios2": "bios/panafz1j-kanji.bin"
  }
}
```

Every row in `docs/data/beyond-title-results.csv` records the observed status, date,
BIOS, BIOS2, disc hash, game ID, capture frame, screenshot path, failure reason, and
input/control timing. The comparison generator reads that ledger first, ahead of manual
grades and the old loadability sweep.

### 4.1 Regression gate

The full 378-title pass is the regression gate. Run it nightly or weekly, either from a
local scheduled task or a self-hosted runner with the collection mounted:

```powershell
pwsh -File tools/run-beyond-title-gate.ps1 -Scope all
pwsh -File tools/run-beyond-title-gate.ps1 -Scope all -UpdateBaseline
pwsh -File tools/run-beyond-title-gate.ps1 -Scope all -SkipExisting
```

The gate builds the Release CLI, runs the sweep, regenerates the comparison and the
evidence page, and fails when a title that was `Playable` in
`docs/data/beyond-title-baseline.json` changes to a failing state. It also runs
`tools/verify-beyond-title-ledger.py`, which checks 378/378 coverage, screenshot
existence, BIOS/disc-hash/frame/date fields, and every peer-playable title. Newly tested
titles and improvements are allowed. A known, dated exception goes in
`docs/data/beyond-title-exceptions.json`:

```json
{
  "exceptions": [
    {
      "file": "Example (USA).chd",
      "reason": "Tracked in COMPAT-10; under bisect.",
      "expires": "2027-01-31"
    }
  ]
}
```

An expired exception fails the gate. `-SkipExisting` is for incremental work, not for
the gate itself.

### 4.2 Disc-format checks

The 2026-11-08 beyond-title pass recorded these format checks:

- **Multi-disc swap.** Night Trap (USA, Europe) Disc 1 was run for 7,200 frames with
  `--swap-disc "Night Trap (USA, Europe) (Disc 2).chd" --swap-at 3600`. The CLI logged
  the swap, the reported `discHash` changed to Disc 2, all 7,200 frames completed, and
  the final capture was live-action video (`artifacts/compat-checks/f007200.png`).
- **CD-DA.** The audio-only A&M Music Sampler and the Oyaji Hunter Mahjong CD-Audio
  image both reach the FZ-10 BIOS CD player. After the 2026-11-10 subcode fix the
  player lists track 1 at `0:00`; the transport controls remain inert and the WAV
  capture has peak/RMS 0. This is the documented SYS-01 audio-only BIOS-player
  residual, not a title beyond-title blocker. All 378 game titles use a data track 1.

### 4.3 Tier C gameplay-depth pass

Tier B answers "does 5DO reach and sustain gameplay?" with a generic 10-minute script.
Tier C closes the quality gap for the titles that stop at a menu: it gives each
remaining `Playable` title a title-specific start/menu script and records a `Good`
grade only when the run reaches gameplay and survives the full checkpoint window.

The remaining-`Playable` gap is tracked in `benchmarks/diff/tierc-panel.json`; the
scripts live in `benchmarks/diff/scripts/tierc/`. Run the baseline and a probe batch
with:

```powershell
python tools/playability-sweep.py --manifest benchmarks/diff/tierc-panel.json `
  --out artifacts/tierc-baseline --jobs 6
python tools/playability-sweep.py --manifest benchmarks/diff/tierc-batch-3.json `
  --out artifacts/tierc-batch-3 --jobs 6
python tools/playability-sweep.py --manifest benchmarks/diff/tierc-batch-4.json `
  --out artifacts/tierc-batch-4 --jobs 6
```

The 3DO **C button is the usual confirm/select button** in these menus. The Tier B
generic script presses A early and stops before many menus appear, which is why titles
such as PGA Tour 96, N.O.B., and BladeForce stay on their first screen. Tier C scripts
use C for menu confirmation, press it at the frame where the menu is actually visible,
and switch to A/B/directions for gameplay.

Grade rules:

- **Good:** reaches gameplay, survives the scripted window (usually 36,000 frames, about
  10 minutes) without a hang or crash, and has no player-visible rendering defect.
- **Playable:** runs but still has a documented gap (menu-only, media/reference disc, or
  a script that cannot start a round).
- **Unplayable:** does not reach or sustain gameplay.

Input scripts must hold a button for at least one field. Write `3600 C` followed by
`3620 None`, not two entries at frame 3600: `InputScript.Parse` coalesces duplicate
frames and keeps the last entry, so a same-frame press/release can discard the press.
The Tier C batches were re-run with explicit 20-frame holds after an early generator
emitted same-frame releases.

Media and reference discs (Club 3DO, Dennis Miller, Digital Dreamware, ESPN Golf,
Life Stage) stay `Playable` with a non-game note; the *Return Fire - Maps O' Death*
standalone data disc keeps its documented peer invalidation. The quality result is
recorded in `docs/data/playability-grades.csv` under the `tierc-*` batch name.

The Tier C input format accepts one optional pointer token per line after the pad
buttons: `@mouse dx dy [left,middle,right,shift]`,
`@lightgun x y [trigger,option,reload,offscreen]`,
`@arcadegun x y [trigger,service,coins,start,holster,offscreen]`, and
`@trackball dx dy [start1,start2,coin1,coin2,service]`. Select the matching device with
`--input-device <port>=<device>`; pointer buttons persist and relative motion applies to
one frame. The reference `OperaCore` bridge is joypad-only, so pointer runs are 5DO-only
evidence. See [`CliReference.md`](CliReference.md#input-and-automation-options).

The second Tier C increment promoted **Carrier - Fortress at Sea**, **Family Feud**,
**Insect War**, and **N. O. B.** to `Good`. The P0 quality pass then promoted
**BladeForce**, **Off-world Interceptor**, and **PGA Tour 96** to `Good` and opened the
Jurassic Park **Security Systems** terminal. The ledger is **91 `Good`, 10 `Playable`**:
seven media/reference/add-on discs plus DeathKeep, Kingdom, and Jurassic Park.
See [`TestCorpus.md`](TestCorpus.md) 1.1bo and 1.1bp.

The suspected core-defect triage is closed: DeathKeep's Start Game black screen also
appears on OperaCore at 48,000 frames, and Kingdom's intro returns to the same title
screen on both engines at 60,000, so both are content/script limits. BladeForce was also
a script limit, and the P0 pass now clears it: the name-entry screen needs `A`, `Down`,
`A` to start the Training Mission and reach the Supply Depot. The weekly
`compat-baseline.yml` workflow now includes a `quality-ledger` job that runs
`tools/verify-playability-grades.py` against
`docs/data/playability-grades-baseline.json` and fails on a `Good` -> `Playable` drop.

---

## 5. In-game / visual check

Loadability is not the same as a usable picture. After the automated pass:

1. Open `artifacts/compat/summary.md` and sort by `Status`.
2. For every `Loadable/Rendered` row, inspect `frame.png` and confirm the image is a
   real title/menu/game scene rather than a test pattern, corrupt palette, or frozen
   frame.
3. For `Loadable/No video`, rerun at `-Frames 12000` and inspect the final frame. Some
   games show black during a load or FMV transition; the 2026-10-30 pass moved six of
   the 13 black/blank discs into the rendered set at 6,000-9,000 frames.
4. If a title is still black or a solid color at 12,000 frames, rerun it with
   `panafz10-patched.bin` and then the Anvil BIOS before classifying it. Do not
   assume the `panafz1` result is the final result.
5. For a sample of titles, run a second pass with a short input script (A/B/Start) to
   confirm the game responds. This is an input smoke test, not a gameplay-depth test.
6. Record `Playable`, `Unplayable`, or `Good` in
   [`GameCompatibilityList.md`](GameCompatibilityList.md) with a one-line note and the
   BIOS used.

A useful visual-check command for one disc:

```powershell
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "J:\Panasonic 3DO\<game>.chd" --bios bios/panafz1.bin `
  --frames 12000 --prefetch `
  --capture-dir artifacts/check/<game> --capture-every 3000 `
  --frame-out artifacts/check/<game>-final.png --json
```

Compare the final frame with the same frame from the 4DO/Opera reference when the
title is listed there. The reference comparison is evidence, not an acceptance target
for every visual difference.

---

## 6. High-resolution pass

The current `--high-res` mode is the FreeDO-style 640x480 path: CELs are rasterized
into four 1 MB VRAM planes and VDLP line-doubles 240 lines to 480. It is not a true
640x480 internal render for every title. See
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

Run the high-resolution pass separately so low-resolution results remain comparable:

```powershell
pwsh -File tools/compat-sweep.ps1 `
  -Frames 3000 -TimeoutSeconds 180 -HighRes `
  -OutDir artifacts/compat-highres
```

For each title that renders, record:

- whether the high-res image is complete or cropped,
- whether text/HUD is sharper or distorted,
- whether the frame is faster/slower,
- whether the game's own 640-wide mode is being used,
- whether the result is better than low-res + xBR/FSR output scaling.

Do not enable high-res by default until a title has been visually validated.

---

## 7. Triage workflow for failures

Group failures before changing code:

| Signature | First checks |
|---|---|
| CLI error before frames | Disc format/CHD track layout, BIOS hash, file path, `info` output. |
| Timeout / short run | Last `pc`, `cdStatus`, `cdPoll`, KPRINT, CD trace; compare with 4DO notes. |
| Black frame but frames complete | Inspect earlier captures; check VDL modulo, CEL trace, palette/CLUT, `sourceWidth/Height`. |
| Renders but wrong colors | Compare with the CPU oracle and the 4DO notes; check palette/CLUT and VDP mode. |
| Renders but frozen/ignores input | PBUS trace, CD READ_ID wait, FIQ/timer state, input script. |
| Audio silent | DSP running flag, AudioFolio semaphore, WAV analysis; do not assume silence is a failure. |

For each failure, record the smallest reproduction command and the evidence paths in
the 5DO notes column. Add a game-specific fix only when the trace proves the behavior
and the 4DO/Opera reference confirms it.

---

## 8. Linux validation in the local Hyper-V VM

The local VM is `LinuxMint` in `D:\Hyper-V\VMs\LinuxMint` (Hyper-V generation 2,
8 GB assigned). The VM is a validation target, not a CI replacement. It is reachable
over SSH; machine-specific credentials are in the gitignored `AGENTS.local.md`.

Suggested procedure:

1. Start the VM and confirm the guest has the .NET 11 SDK (or publish a
   `linux-x64` build on the host and copy it into the guest).
2. Copy the repository or a published CLI/frontend folder into the guest.
3. Run `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`.
4. Run `dotnet build src/FiveDo.Cli -c Release` and
   `dotnet run -c Release --project src/FiveDo.Cli -- selftest --json`.
5. Run the frontend smoke test (`--smoke-test`) and, if the VM has a GL-capable
   display, the OpenGL probe (`--gl-probe`).
6. Run a reduced compatibility sweep with `-NoFrameCapture` (System.Drawing image
   analysis is Windows-oriented) and record the CLI results.
7. Record the Linux GL renderer/vendor/version and any fallback behavior in
   [`Compatibility.md`](Compatibility.md) and [`KnownIssues.md`](KnownIssues.md).

The Linux pass should use the same BIOS/disc set as the Windows pass for a meaningful
comparison. Do not commit BIOS or disc images.

**Result (2026-11-02):** the full test suite (**1,473/1,473**) passed. The self-contained
`linux-x64` CLI passed `selftest` (4/4), `bench` (466 fps), `info`/`bios info`, and a
3,000-frame Wolfenstein boot (frame hash `0x9A3B5131`, non-silent 50 s audio). The
frontend `--smoke-test` passed with OpenAL ready. The GL probe timed out under the VM's
llvmpipe/SSH session, so Linux GPU presentation is preview/best-effort. See
[`Compatibility.md`](Compatibility.md) §10.3.

---

## 9. Completion criteria

The **loadability sweep** is complete for v1.0 when:

- every local disc image has a loadability status in
  [`GameCompatibilityList.md`](GameCompatibilityList.md),
- every `Not Loadable` result has a triage note and a reproduction command,
- the Linux x64 build/test/smoke result is recorded.

The **post-1.0 visual review** adds:

- a screenshot and a short input check for every `Playable`/`Good` result,
- a high-resolution pass with a recorded decision per tested title,
- the top failure classes have either a fix or a documented known issue.

Full gameplay completion, long-play scripts, and netplay/rewind remain post-release
work.
