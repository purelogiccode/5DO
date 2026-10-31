# Full-Collection Compatibility Sweep

**Status:** first loadability pass complete (2026-10-30): **594 discs tested, 576
rendered, 13 black/blank, 5 not loadable**. The 12,000-frame rerun resolved six of the
13 black/blank titles, and BIOS cross-testing showed that the four Arcade CD-ROM
timeouts and several black-screen titles are BIOS-sensitive. See
[`GameCompatibilityResults.md`](GameCompatibilityResults.md) and
[`Compatibility.md`](Compatibility.md) §13.2.
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
| Main collection | `J:\Panasonic 3DO` | 589 CHD images |
| MAME software-list collection | `G:\MAME\MAME Software List CHDs\3do` | 5 CHD images |
| BIOS | `bios/panafz1.bin` (baseline) | 1 MB, user-provided |
| Alternate BIOSes | `panafz10.bin`, `panafz10e-anvil.bin`, `panafz10ja-anvil-kanji.bin` | 1 MB each; used for BIOS-sensitive titles |
| 4DO reference list | `References/4DO-master/4DO Compatibility List ⚈ 3DO emulator Wiki ».mhtml` | 241 titles |

The two collections contain regional variants and alternate revisions. The sweep tests
every image, not one image per title. The 4DO list is a reference for known issues; it
is not a guarantee that a matching filename is the same disc revision.

Regenerate the reference table after changing a collection or the 4DO snapshot:

```powershell
python tools/generate-compat-list.py
```

The generator uses only the Python standard library and rewrites
[`GameCompatibilityList.md`](GameCompatibilityList.md). It matches titles by normalized
filename, not by disc hash, so verify a match before treating it as the same revision.

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

### 3.3.1 BIOS selection

`panafz1.bin` is the baseline BIOS, not a universal reference. The 2026-10-30 triage
found that all four Arcade CD-ROM rows hang at about frame 1,740 with `panafz1.bin`
but boot with `panafz10.bin`. Crash 'n Burn, Powers Kingdom (Europe), and WorldCup
Super Stadium only leave their black or solid-blue boot state with the Anvil BIOS;
Slayer needs `panafz10`, GoldStar, or Sanyo. Always record the BIOS used with a
result:

```powershell
pwsh -File tools/compat-sweep.ps1 `
  -Bios bios/panafz10.bin -Frames 3000 -TimeoutSeconds 180 `
  -OutDir artifacts/compat-panafz10
```

Do not compare a `panafz1` black screen with a `panafz10` render as if they were the
same test. The final 5DO status needs the BIOS in its notes.

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

## 4. In-game / visual check

Loadability is not the same as a usable picture. After the automated pass:

1. Open `artifacts/compat/summary.md` and sort by `Status`.
2. For every `Loadable/Rendered` row, inspect `frame.png` and confirm the image is a
   real title/menu/game scene rather than a test pattern, corrupt palette, or frozen
   frame.
3. For `Loadable/No video`, rerun at `-Frames 12000` and inspect the final frame. Some
   games show black during a load or FMV transition; the 2026-10-30 pass moved six of
   the 13 black/blank discs into the rendered set at 6,000-9,000 frames.
4. If a title is still black or a solid color at 12,000 frames, rerun it with
   `panafz10.bin` and then the Anvil BIOS before classifying it. Do not assume the
   `panafz1` result is the final result.
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

## 5. High-resolution pass

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

## 6. Triage workflow for failures

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

## 7. Linux validation in the local Hyper-V VM

The local VM is `LinuxMint` in `D:\Hyper-V\VMs\LinuxMint` (Hyper-V generation 2,
8 GB assigned). The VM is a validation target, not a CI replacement.

Suggested procedure:

1. Start the VM and confirm the guest has the .NET 10 SDK (or publish a
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

---

## 8. Completion criteria

The compatibility sweep is complete for a release candidate when:

- every local disc image has a row/status in
  [`GameCompatibilityList.md`](GameCompatibilityList.md),
- every `Not Loadable` result has a triage note and a reproduction command,
- every `Playable`/`Good` result has a screenshot and a short input check,
- the high-resolution pass has a recorded decision per tested title,
- Linux VM build/test/smoke results are recorded,
- the top failure classes have either a fix or a documented known issue.

Full gameplay completion, long-play scripts, and netplay/rewind remain post-release
work.
