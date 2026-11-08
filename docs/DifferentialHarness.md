# Differential Harness

**Status:** implemented 2026-10-31; TEST-07 oracle instrumentation 2026-11-06;
audio gating, scripted input, and the nightly baseline gate 2026-11-08.
**Features:** FND-05, CORE-11, CORE-16.
**Tool:** `tools/FiveDo.DiffHarness`.
**Manifests:** `benchmarks/diff/`.

The harness runs 5DO and the bundled Opera reference core with the same BIOS,
disc data, input script, frame count, and machine options, then compares
deterministic checkpoint hashes. It is a developer/CI tool; it is not part of the
shipped emulator.

## 1. What it compares

| Field | 5DO | OperaCore | Compared by default |
|---|---|---|---|
| Frame hash | Assembled BGRA8888 frame, FNV-1a | XRGB8888 presenter frame converted to BGRA8888, FNV-1a | Yes |
| DSP sample count | `run --json` | Audio callback count | Yes, with `sampleTolerance` |
| Audio hash | Packed DSP samples, FNV-1a | Interleaved stereo samples, FNV-1a | Yes on the 300-frame panel; the cumulative hash requires equal sample counts (see §4.1) |
| ARM PC | `run --json` | Optional debug export | No; enable with `--compare-pc` |
| ARM CPSR | `run --json` | Optional debug export | No; `--compare-pc` also compares CPSR when both sides expose it |
| CD command count/hash | `CdromCommandTranscript` issue-time counters | Optional debug export | No; enable with `--compare-cd` |
| CD command sequence | Full opcode list at the final checkpoint | Optional debug export | No; included with `--compare-cd` at the final checkpoint |
| CD command cycle | `CdromCommandTranscript.IssuedCycles` | `sg_debug_cd_cycle_at` | No; diagnostic delta at the last common command |
| CD command PC | `CdromCommandTranscript.IssuedProgramCounters` | `sg_debug_cd_pc_at` | No; diagnostic at the last common command |
| CD command instructions | `CdromCommandTranscript.IssuedInstructions` | `sg_debug_cd_instructions_at` | No; diagnostic, separates extra work from extra idle time |
| CD cadence step | Largest change in the cumulative command-cycle delta | Same | No; diagnostic, reports the step command, matching/mismatching PC, and the retired-instruction jump |
| ARM instructions | `run --json` | `sg_debug_arm_instructions` | No; diagnostic parity count |
| PC instruction phase | Retired-instruction count per checkpoint | Retired-instruction count per checkpoint | Diagnostic; `pcPhaseFrame` reports the neighboring reference frame whose count aligns with 5DO |
| Save-state hash | Optional `--state-hash` | ARM-only optional debug hash | No |

The frame hash is the same algorithm as the emulator's `run --json` `frameHash`.
It is computed from the assembled BGRA8888 pixels, so a matching hash means the
two engines produced byte-identical visible output for that frame. The report's
**PC delta** column is the largest absolute `Opera PC - 5DO PC` across the
checkpoints; it is always shown when the debug core exposes a PC, even when the
video fields match.

Every compared field is gated. The report also records the **first divergence**:
the earliest checkpoint and field without an allowance. Use it as the triage
entry point; later checkpoint differences may cascade from the same cause. A
frame-hash match is not evidence that the instruction streams agree.

Because PC and CPSR are sampled at the frame boundary, a few hundred cycles of
phase difference moves the PC without changing a pixel. The harness therefore
also prints the **first behavioral divergence**, which ignores PC/CPSR and uses
only frame hash, samples, audio, state, and CD. A title whose only failures are
PC/CPSR is reported as "behavioral observations match"; it is still `Diverged`
for the strict gate, but it is not evidence of a game-visible defect.

The report also records an **instruction-phase** alignment for PC/CPSR
divergences. When the checkpoint window contains a neighboring reference frame
whose retired-instruction count is within a tenth of a frame of 5DO's count (and
the same-frame count is farther away), the report prints the aligned frame and
the residual instruction delta. `pcPhaseFrame` and `pcPhaseInstructionDelta` on
each title carry the same evidence. This is a triage label, not a pass: the
strict gate still fails until the phase itself is removed.

5DO counts a CD command when the first command byte is written
(`CommandOpcodeObserver`); the reference counts `XBP_SET_COMMAND` at the same
protocol phase. A command still in flight at a checkpoint can produce a 1-3
command count difference. The cumulative command hash is only comparable when the
counts match.

The final-checkpoint `cdCommands` field also records a transcript classification.
`Equal` means every opcode matches. `PrefixAhead` means one transcript is a prefix
of the other, so the difference is in-flight or trailing commands, not a changed
command table. `SequenceDiverged` names the first opcode that differs. The same
diagnostic reports the largest **cadence step**: the command where the
5DO-minus-Opera cycle delta changed most. When the command-issue PCs match at that
step and the retired-instruction delta jumps with the cycle delta, the guest ran
the same path and spent extra instructions waiting; it did not execute a different
CD driver path. The analysis is written to the report as `cdCadence` so a panel
can be triaged without re-parsing the 5DO and Opera capture arrays.

## 2. Quick start

```powershell
# Build the CLI and the harness. The harness runs the external Release CLI, so
# rebuild the CLI after every core change or it will silently test the old build.
dotnet build src/FiveDo.Cli -c Release
dotnet build tools/FiveDo.DiffHarness -c Release

# Validate a manifest without touching any discs.
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  validate benchmarks/diff/smoke.json --strict

# Run the smoke title (300 frames, one checkpoint).
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/smoke.json --out artifacts/diff/smoke

# Run the five-title showcase (3000 frames each).
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/showcase.json --out artifacts/diff/showcase

# Build the optional debug reference core (deterministic seed + PC/CPSR/CD exports).
cmake -S artifacts/diff/opera-build -B artifacts/diff/opera-build-current -G Ninja `
  -DCMAKE_BUILD_TYPE=Release `
  -DCMAKE_C_COMPILER=C:/mingw64/bin/gcc.exe `
  -DCMAKE_CXX_COMPILER=C:/mingw64/bin/g++.exe
cmake --build artifacts/diff/opera-build-current

# The five-title TEST-07 panel: fixed seed, PC/CPSR/CD comparison.
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/samples5.json --out artifacts/diff/test07-samples5 `
  --no-images --seed 1488 --compare-cd --core artifacts/diff/opera-build-current/OperaCore.dll

# CORE-02/CORE-07 timer and read trace (both engines).
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/samples5.json --out artifacts/diff/timer-trace `
  --no-images --timer-trace --title lemmings --force `
  --compare-pc --compare-cd --core artifacts/diff/opera-build-current/OperaCore.dll

# Strict mode with a debug core that exposes PC/CD state.
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/showcase.json --out artifacts/diff/showcase-debug `
  --core artifacts/diff/opera-build-current/OperaCore.dll --compare-pc --compare-cd

# Generate a full-collection manifest after changing the disc roots.
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  manifest --root "J:\Panasonic 3DO" `
  --root "G:\MAME\MAME Software List CHDs\3do" `
  --out benchmarks/diff/full.json --frames 3000 --checkpoint 3000

# Generate the deterministic 50-title TEST-07 panel from the full manifest.
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  panel --from benchmarks/diff/full.json --out benchmarks/diff/panel50.json `
  --count 50 --core artifacts/diff/opera-build-current/OperaCore.dll `
  --work artifacts/diff/work-panel50 --name "5DO 50-title accuracy panel"
```

The `panel` command selects evenly spaced titles (including the first and last),
preserves the source `roots`, BIOS, and machine options, clears per-title frame
overrides and allowlists, and applies the TEST-07 comparison policy: PC, CPSR, CD
command count/hash, sample tolerance 512, and audio informational.

Outputs:

- `artifacts/diff/<run>/<title>/fivedo.capture.json`
- `artifacts/diff/<run>/<title>/opera.capture.json`
- `artifacts/diff/<run>/<title>/fivedo.stdout.json`
- `artifacts/diff/<run>/<title>/fivedo.stderr.txt`
- `artifacts/diff/<run>/<title>/opera.log.txt`
- `artifacts/diff/<run>/<title>/images/` (checkpoint PNG/BMP)
- `artifacts/diff/<run>/report.json`
- `artifacts/diff/<run>/report.md`
- `artifacts/diff/<run>/report.html`

Useful run flags: `--resume`, `--force`, `--title <filter>`, `--limit N`,
`--no-images`, `--cleanup-iso`, `--seed <n>`, `--compare-pc`, `--compare-cd`,
`--compare-audio`, `--pc-tolerance <bytes>`,
`--arm-timing freedo|arm60`, `--clio-timers freedo|opera`,
`--cel-timing freedo|opera`, `--cd-timing freedo|opera`, `--timer-trace`,
`--cd-register-trace`, and `--profile compatible|accuracy|performance|custom`.
The default fixed seed is 1488; it is applied to both engines and recorded in each
capture and in the report provenance.
The timing and profile overrides replace the manifest option for every 5DO capture, so
one manifest can drive both sides of an ARM, CLIO, CEL, CD, or profile A/B run without
editing the manifest. Use a fresh `--out` directory (or `--force`) when changing an
override, because cached 5DO captures are reused by `--resume` without re-reading the
machine options. For a profile A/B, keep the manifest's `cdSpeed: "unbounded"` (the
reference core runs unbounded); the resulting 5DO `profile` field is `Custom` because
the drive speed differs, but the ARM/CLIO/CEL/CD timing modes follow the selected
profile. Every override is recorded in the report provenance (`armTimingOverride`,
`clioTimersOverride`, `celTimingOverride`, `cdTimingOverride`, `profileOverride`) so an
A/B report is self-describing. `--compare-pc` requires a reference core with the
optional debug exports; without them the PC field is reported as unavailable rather
than silently passing.

Exit codes:

| Code | Meaning |
|---:|---|
| 0 | Every compared field matched or had a manifest allowance. |
| 1 | At least one unexplained divergence or capture error. |
| 2 | Manifest, path, or command-line error. |

## 3. Manifest schema

```json
{
  "schemaVersion": 1,
  "name": "5DO differential showcase",
  "roots": {
    "games": "J:/Panasonic 3DO",
    "mame": "G:/MAME/MAME Software List CHDs/3do"
  },
  "defaults": {
    "bios": "bios/panafz1.bin",
    "frames": 3000,
    "checkpoints": [3000],
    "inputScript": null,
    "timeoutSeconds": 600,
    "workDirectory": "artifacts/diff/work",
    "options": {
      "profile": "accuracy",
      "cdSpeed": "unbounded",
      "noGameFixes": true,
      "stateHash": true
    },
    "compare": {
      "compareAudio": false,
      "comparePc": false,
      "compareCpsr": false,
      "compareCd": false,
      "compareSamples": true,
      "sampleTolerance": 512,
      "pcTolerance": 0,
      "frameWindow": 2
    }
  },
  "titles": [
    {
      "id": "lemmings-usa",
      "name": "Lemmings (USA)",
      "disc": "${games}/Lemmings (USA).chd",
      "iso": null,
      "bios": null,
      "frames": null,
      "checkpoints": null,
      "inputScript": null,
      "options": null,
      "allow": []
    }
  ]
}
```

Rules:

- `schemaVersion` must be `1`.
- Title ids are case-insensitively unique.
- `roots` maps `${name}` tokens in paths to absolute disc roots. This keeps the
  full 594-disc manifest portable across machines.
- `frames` must be positive; a checkpoint may not be after `frames`.
- Omitted `checkpoints` means the final frame only.
- Relative paths resolve against the repository root, then against the manifest
  directory.
- `iso` is optional. When present, the harness stages that ISO for OperaCore.
  Otherwise it extracts the 2048-byte data sectors from `disc` into
  `<workDirectory>/discs/<id>/<id>.iso`. Extraction works for ISO, BIN, CUE, and
  CHD; it is cached by title id.
- `options.extraArgs` passes raw 5DO CLI arguments for options the manifest does
  not model yet.
- `compare.frameWindow` captures that many neighboring frames on both engines and
  diagnoses a frame-hash mismatch as **Offset** when the 5DO hash matches a
  neighboring Opera frame. It never turns an offset into a pass; it only names
  the likely cause.
- `compare.comparePc` gates the ARM PC; `compare.compareCpsr` gates the ARM CPSR.
  `--compare-pc` forces PC on and also compares CPSR when both sides expose it.
  `compare.pcTolerance` allows a small absolute PC delta in bytes.
- `compare.compareCd` gates the CD command count and cumulative opcode hash.
  `--compare-cd` forces it on. The count is issue-time on both sides; a command in
  flight at a checkpoint can still differ by 1-3 commands. At the final checkpoint
  it also compares the full opcode sequence and reports the first divergent
  command index with a seven-opcode context window.
- `compare.compareSamples` gates the DSP sample count; `compare.sampleTolerance`
  absorbs the reference field-clock quantization described in §4.1.
  `compare.compareAudio` gates the cumulative audio hash. The hash is compared
  only when both engines produced the same sample count; a count difference is
  reported as an alignment state (`cumulative hash not comparable`) rather than a
  waveform divergence. The 300-frame panel and the 300-frame five-title sample
  enable it; 3,000-frame manifests leave it informational because the reference
  constant drifts by about one sample per 1,100 frames.
- The fixed seed comes from `options.randomSeed` when set, otherwise from
  `--seed` (default 1488). `random` is rejected: a differential run needs a fixed
  seed. The applied seed is recorded in each `CaptureResult` and in the report
  provenance.

### 3.1 Allowlist

```json
"allow": [
  {
    "field": "frameHash",
    "from": 100,
    "to": 200,
    "reason": "Boot screen appears 200 fields earlier in the reference",
    "issue": "CORE-04"
  }
]
```

A field can use `checkpoint` (exact frame) or `from`/`to` (inclusive range).
Every allowance needs a non-empty reason. Allowed divergences are counted and
shown in the report but do not fail the run. Do not allowlist a field without a
reproduction or a tracking issue.

## 4. Checkpoint semantics

The CLI adds `--checkpoints 100,200,300`, `--audio-hash`, and `--state-hash` to
`run`. At each requested frame it records:

```json
{
  "frame": 300,
  "pc": 132600,
  "instructions": 25712666,
  "frameHash": 286162373,
  "samples": 220508,
  "audioHash": 4121120901,
  "stateHash": 2860634904,
  "cdStatus": 225,
  "cdPoll": 13,
  "dspPc": 45,
  "dspSema": 0,
  "dspSeed": 2779096485,
  "celBusyCycles": 0
}
```

The CLI forces the frame-by-frame run path when checkpoints are requested, so
scripted input, frame capture, and checkpoints share the same frame boundary.

For OperaCore, the checkpoint is finalized at the start of the next video
callback, after that frame's audio callback. This makes its audio sample count
and audio hash align with 5DO's end-of-frame checkpoint.

### 4.1 Audio sample alignment

Both engines hash the same packed DSP output, so matching cumulative hashes are
bit-identical waveform evidence. The hash is only comparable while the two
engines have produced the same number of samples.

5DO follows the exact NTSC field rate `60000/1001` (59.940059940 Hz). OperaCore's
16.16 constant is `3,928,227 / 65,536` (59.939987183 Hz), which is **1.214 ppm
lower**. A field is therefore 0.253 ARM cycles longer in the reference, or
0.000893 samples per field. The measured Lemmings alignment is:

| Frame | 5DO samples | Opera samples | Delta | Cumulative audio hash |
|---:|---:|---:|---:|---|
| 60 | 44,144 | 44,144 | 0 | match |
| 120 | 88,288 | 88,288 | 0 | match |
| 300 | 220,720 | 220,720 | 0 | match |
| 600 | 441,441 | 441,441 | 0 | match |
| 1,000 | 735,735 | 735,735 | 0 | match |
| 2,000 | 1,471,470 | 1,471,471 | +1 | not comparable |
| 3,000 | 2,207,205 | 2,207,207 | +2 | not comparable |

The predicted drift is 2.68 samples at 3,000 frames; the measured +2 is that
quantization. 5DO does not copy the reference constant: the hardware
documentation and the project's exact-clock decision specify 60000/1001. The
audio field instead reports the sample delta and the 1.214 ppm explanation, so a
long-run mismatch is triaged as a reference-clock alignment state. A waveform
difference at an aligned sample count is reported as
`audio content differs at an aligned sample count` and remains a hard failure.
The one-title probe is `benchmarks/diff/audio-probe.json`.

### 4.2 Scripted input

`inputScript` accepts a path to a text file parsed by `FiveDo.Input.InputScript`:
each non-comment line is `frame buttons`, the state persists until the next
line, and `None` releases every button. Frames may be decimal or `0x`-prefixed;
button names are `ControllerButton` names or the aliases `Start`/`Play`/`Pause`
(P), `Stop` (X), and the D-pad directions. Example:

```text
# frame buttons
0     None
90    P
100   None
330   A
340   None
```

The harness passes the file to `run --input-script` and to the reference input
callback. Both engines apply the entry whose frame is at or before the current
emulated frame, before that frame executes. The generic
`benchmarks/diff/scripts/menu-advance.txt` is the current panel script; it presses
Start and A with gaps so boot logos, menus, and FMV prompts advance.

Evidence (`test94-samples5-input`, 900 frames, five titles):

- All five match the frame hash at f=60, f=300, and f=600.
- The script changes the 5DO frame at f=600 for Space Ace and at f=900 for
  Alone, Lemmings, Road Rash, and Wolfenstein, so the buttons reach the guest.
- Without the script (`test96-samples5-noinput`) the same checkpoints match on
  the same titles, so the f=60/300/600 matches are not a side effect of an
  ignored script.
- Alone's f=900 frame differs from the reference hash but both captures are the
  same dark transition/noise screen; this is a pixel-level difference in a
  transition, not a visibly different menu. Road Rash already diverges at f=600
  with no input (`test95-roadrash-noinput`), so its result is not input-induced.
- At f=900 the sample count is one ahead in the reference, so the cumulative
  audio hash is reported as the §4.1 alignment state for all five titles.

A menu script is not gameplay evidence. Per-title scripts that reach and sustain
gameplay are the TEST-06 work; keep the generic script for plumbing/parity only.

## 5. OperaCore staging and limitations

The harness uses `References/CPP_SimpleGui/cmake-build-release/SimpleGui/Core/OperaCore.dll`,
a SimpleGui core wrapper around libopera. It is a reference build, not product
code. It is loaded through its `sg_*` C ABI.

- The core finds its BIOS in a `BIOS/` directory next to the DLL. The harness
  copies the DLL and the selected BIOS into
  `<workDirectory>/opera-runtime/<core-md5>-<bios-md5>/`, so the user's BIOS and
  the reference tree are never modified.
- The core writes `<iso>.srm` next to the staged ISO. The harness deletes that
  file before each fresh run and keeps the staged ISO inside the work directory.
- The reference core is NTSC, 2 MB DRAM + 1 MB VRAM, hardware matrix, no SWI HLE,
  no VDLP CLUT bypass, and `opera_cdrom_set_speed(0)` (unbounded). The manifest
  therefore uses `--profile accuracy --cd-speed unbounded --no-game-fixes` for
  5DO comparisons.
- The ABI exposes video, audio, input, and lifecycle callbacks. The optional debug
  core adds `sg_debug_arm_pc`, `sg_debug_arm_cpsr`, `sg_debug_arm_instructions`,
  `sg_debug_arm_state_hash`, `sg_debug_cd_status`, `sg_debug_cd_poll`,
  `sg_debug_cd_command_count`, `sg_debug_cd_command_hash`,
  `sg_debug_cd_command_at`, `sg_debug_cd_cycle_at`, `sg_debug_cd_pc_at`,
  `sg_debug_cd_instructions_at`, and `sg_debug_set_seed`. The
  harness detects those symbols with `NativeLibrary.TryGetExport`; a stock core
  simply leaves the fields null. Build the debug core from
  `References/CPP_SimpleGui/Cores/OperaCore` with the small getters in
  `opera_arm.c`/`opera_xbus_cdrom_plugin.c` and pass
  `--core artifacts/diff/opera-build-current/OperaCore.dll`. The fields are
  evidence, not part of the default pass/fail gate: a 4-byte PC delta is normal
  when the frame hash matches, and the CD FIFO status is not the same value as
  5DO's `DriveStatus`.
- The reference tree is gitignored, so the debug getters are local tooling, not
  product code. Reproduce them on a fresh `References/CPP_SimpleGui` checkout by
  adding the `sg_debug_*` exports in `Cores/OperaCore/sg_opera_core.cpp`, the
  per-command arrays/getters in `libopera/opera_xbus_cdrom_plugin.c`/`.h`, and the
  PC/instruction calls at the plugin's `XBP_SET_COMMAND` `cmd_idx == 0` boundary.
  A core without them still runs; the harness reports those fields as
  unavailable instead of failing.
- `sg_debug_cd_command_at`/`sg_debug_cd_cycle_at` are recorded when
  `cmd_idx == 0`, the exact first byte of a command packet. The old debug plugin
  counted bytes modulo seven, which misaligned after a one-byte ABORT (0x08)
  packet. Both sides now record one opcode and one cycle stamp per packet.
- `sg_debug_cd_pc_at`/`sg_debug_cd_instructions_at` record the guest PC and retired
  ARM instruction count at that same command boundary. A matching cycle delta with
  a different PC means a different code path; a matching PC with a larger
  instruction delta means the same path ran an extra loop. The harness writes the
  arrays as `cdCommandPcs`/`cdCommandInstructions` in each capture; they are
  diagnostic-only, not a gate.
- `--timer-trace` writes `fivedo.timers.txt`, `fivedo.reads.txt`, `fivedo.clio.txt`,
  and `opera.timers.txt` per title. The reference core exposes
  `sg_debug_timer_event_count`, `sg_debug_timer_cycle_at`, `sg_debug_timer_pc_at`,
  `sg_debug_timer_value_at`, `sg_debug_timer_kind_at`, `sg_debug_timer_slot_at`,
  `sg_debug_timer_reload_at`, and `sg_debug_timer_fiq_at`. It records timer
  counter/control/slack writes, underflows, CLIO timer/VCNT/FIQ reads, XBUS
  select/poll/FIQ events, and FIQ-register writes with the same cycle and PC stamps
  as 5DO's `--clio-timer-trace`/`--clio-read-trace`/`--clio-trace`. The arrays are
  diagnostic-only; a core without the exports still runs.
- The debug core is local tooling. The `opera_clio_debug_*` helpers live in
  `opera_clio.c`/`opera_clio.h`; `opera_xbus.c` calls them for XBUS levels. Reproduce
  them on a fresh `References/CPP_SimpleGui` checkout if the gitignored tree is
  rebuilt.
- `--cd-register-trace` writes `fivedo.cdreg.txt` and `opera.cdreg.txt` per title. The
  reference core exposes `sg_debug_cd_register_count`, `..._cycle_at`, `..._pc_at`,
  `..._op_at`, and `..._value_at`. It records command bytes (`cmdw`), poll writes and
  reads (`pollw`/`pollr`), status reads (`statusr`), data writes (`dataw`), and data
  reads (`datr`) with cycle and PC stamps, matching 5DO's `--cd-register-trace`. The
  5DO trace also records `datr_empty` for an empty data read. The arrays are
  diagnostic-only; a core without the exports still runs and logs that the trace was
  not written. Command/status/data value comparison uses the `cmdw`/`statusr`/`datr`
  sequences; poll counts include guest polling only.
- `sg_debug_arm_instructions` counts calls to `opera_arm_execute`. The harness
  records it beside 5DO's `instructions` and prints the delta per checkpoint; it
  is diagnostic-only, not a gate.
- `sg_debug_set_seed(seed)` seeds `std::rand`, `prng16`, and `prng32` before
  `sg_init`; the harness applies a fixed seed (default 1488) and records it. Two
  consecutive runs with the same seed produced identical checkpoint arrays for
  both engines on 2026-11-06.
- 5DO uses the exact NTSC field rate (60000/1001, 208,541.67 cycles/field);
  OperaCore's 16.16 constant is 1.214 ppm lower (see §4.1). A 300-frame run
  matches the sample count exactly on the panel; the 3,000-frame run drifts by
  +2 samples in the reference. `sampleTolerance` remains a 512-sample safety
  margin. The 300-frame manifests gate the cumulative audio hash; long-run
  manifests report the sample lag as an alignment state.
- A checkpoint sample is assembled from the checkpoint's own field: the harness
  captures PC/CPSR/instructions/CD state inside the video callback and adds the
  audio hash after that field's audio callback. Earlier builds finalized the
  sample at the next callback, which read ARM/CD state one field late while the
  audio was already correct; that produced the apparent universal "frame 60
  matches Opera frame 59" phase.
- The core is paced in real time, so a 3000-frame capture takes about 50 seconds
  per title. Use `--resume` for long runs.

## 6. Baseline regression mode

```powershell
FiveDo.DiffHarness.exe baseline `
  --baseline artifacts/diff/showcase/report.json `
  --current artifacts/diff/nightly/report.json
```

This compares each title/checkpoint/field's 5DO value. Any changed value exits
with code 1. It is the CORE-16 gate for changes that should not alter existing
golden results. The nightly workflow runs the showcase fresh (`--force`) and then
compares it against `benchmarks/diff/baseline/showcase-report.json`. Update that
file with the new `report.json` when a hash change is intentional and reviewed;
the report's `report.html` is uploaded as the nightly artifact.

## 7. CI

`.github/workflows/diff-harness.yml` is an opt-in/nightly workflow for a
self-hosted Windows runner that has the BIOS and disc corpus. GitHub-hosted
runners run the harness unit tests through the normal test job; they cannot run
the reference core or the local discs. The nightly captures the showcase fresh,
uploads `report.html`/`report.md`/`report.json`, and fails when a 5DO field
changes against the checked-in baseline (not merely because a baseline-known
title still diverges). Intentional changes update the baseline in the same
change.

## 8. Current evidence

- **2026-11-08 CD register trace, status parity, and the 20th Century read schedule
  (CORE-04).** `--cd-register-trace` now writes matched `fivedo.cdreg.txt` and
  `opera.cdreg.txt` files. The reference plugin records every command byte, poll
  write/read, status read, and data read with cycle and PC; 5DO records `datr` for
  every non-empty data read in addition to `datr_empty`. On 20th Century Video Almanac
  (290 frames, unbounded CD speed, accuracy profile) the two engines' command packets,
  status bytes, and sector data are **identical through command 105**; the first
  command sequence divergence is command 106 (5DO `READ_DATA` vs Opera `READ_ID`).
  The trace also exposed four CD metadata divergences that are now fixed: the XBUS 2x
  status bit (MODE SELECT page 3 uses `0x80`, and the drive starts at double speed),
  the guest speed cap (`cdrom_effective_speed`), the readable-disc limit (3DO volume
  header `volume_block_count`, not the physical image size), and the TOC Q-position
  bit plus READ_SESSION_INFO payload. The remaining 20th Century wait is guest sleep
  time (6 frames in 5DO vs 60 in Opera) with matching CD values, so CORE-04's residual
  is not a CD register-contract difference. The 11-title `cd-cadence` panel keeps the
  same first mismatch indices. The confirmation panel
  (`test110-panel50-metadata/report.json`, 50 titles, 300 frames) captures **50/50
  titles with 0 errors**, **0 frame-hash divergences**, **150/150 aligned audio
  fields**, **39 phase-only** titles, and the same **11 CD-only first behavioral
  divergences** at f=300. **2,048 tests pass.** Build and format are clean; the local
  InspectCode host crashed before emitting a result in this session, and the prior
  complete run was clean.

- **2026-11-08 audio gating, scripted input, and nightly baseline (TEST-07).**
  The 300-frame panel now compares the cumulative audio hash. The alignment probe
  (`benchmarks/diff/audio-probe.json`, Lemmings) matches the sample count and hash
  through 1,000 frames, then the reference is **+1 at 2,000** and **+2 at 3,000**
  frames because OperaCore's 16.16 field-rate constant is 1.214 ppm below 5DO's
  exact `60000/1001` (see §4.1). The panel
  (`test93-panel50-audio/report.json`) captures **50/50 titles with 0 errors**,
  **50/50 frame hashes**, and **150/150 audio hashes** at 60/120/300; the 11
  CD-only first behavioral divergences at f=300 remain. Scripted input
  (`scripts/menu-advance.txt`, `samples5-input.json`) matches **5/5 frame hashes
  at 60/300/600** and demonstrably changes guest output at 600/900; the no-input
  control matches the same checkpoints, and Road Rash's f=600 divergence
  reproduces without input. The nightly workflow captures the showcase fresh,
  uploads the HTML report, and compares against
  `benchmarks/diff/baseline/showcase-report.json`; `baseline --write` generates
  that trimmed file. **2,043 tests pass.**

- **2026-11-08 CLIO timer/read trace and CD FIQ parity (CORE-02/CORE-04/CORE-07).**
  `--timer-trace` writes `fivedo.timers.txt`, `fivedo.reads.txt`, `fivedo.clio.txt`,
  and `opera.timers.txt` with matched cycle/PC stamps. The timer-14 underflow period
  is **12,384 cycles in Opera and 12,396 in 5DO** under the accuracy profile, so the
  old "~10,400" figure was the FreeDO-compatible mode. The Lemmings
  READ_ID-to-READ_ERROR wait is still +22,834 cycles but +8,694 retired instructions:
  the guest runs a data-dependent copy/decrypt loop, not a timer or CD wait. The
  trace exposed two parity bugs: 5DO reset the Opera CD poll to `0x80` instead of
  `0x87` (`0x07` enables plus the boot media latch), so the guest's read-modify-write
  cleared every enable; and 5DO raised FIQ 4 only on sector/status publication while
  `opera_xbus_tick` level-checks every device each batch. `CdromDevice.ResetPoll`,
  `Xbus.FiqPending`, and the machine-level level check now match. The final
  50-title panel (`test91-panel50-heuristic-retired/report.json`) keeps **50/50
  frame hashes, 0 errors, 49/50 same-frame PC phase, 38 phase-only**, improves
  Daedalus Encounter to a full match, and retains the same 11 CD-only first
  behavioral divergences at f=300. The five-title sample keeps 5/5 frame hashes;
  Space Ace's 3,000-frame hash returns to `0x46916C61` after the retired READ_ID
  heuristic, and Road Rash stays `0xD5895AD5`. The 3,000-frame sample
  (`test92-samples5-long-final`) keeps **4/5 video matches** (Alone, Lemmings,
  Space Ace, Wolfenstein); Road Rash is the only video divergence. **1,925 tests pass.**

- **2026-11-07 frame-boundary parity (TEST-07/CORE-07).** Two independent fixes
  removed the universal frame-60 phase. First, `OperaCoreHost` finalized each
  checkpoint at the next video callback, so PC/CPSR/instructions and CD state
  were read after the following field had executed; the audio hash was already
  from the checkpoint field. The harness now captures ARM/CD state in the
  checkpoint's own video callback and fills the audio hash when that field's
  audio callback completes. Second, `FiveDoMachine.RunFrame` stopped on the
  rounded ARM-cycle budget (208,541 cycles) while Opera stops on the 263-line
  NTSC field boundary (`opera_3do_process_frame`). The discarded batch overshoot
  accumulated ~3.3 instructions per field. `RunFrame` now ends when
  `Quartz.VdlFieldIndex` changes, matching the reference's
  `line < opera_region_scanlines()` loop. On the five-title sample
  (`test63-line-boundary/report.json`) the same-frame retired-instruction delta
  at f=60 is **8-17** instructions (was ~219 after the capture fix and ~89,000
  before it), and at f=120 it is **24-84**; PCs match within one instruction.
  The 50-title confirmation panel (`test64-panel50-line-boundary/report.json`)
  captures **50/50 with 0 errors** and **50/50 frame hashes**; the same-frame
  delta is **11-20** at f=60 and **23-2,223** at f=120, the CD count matches on
  **50/50 titles at f=60 and f=120**, and the first behavioral divergence moved
  to **11 titles at f=300** (8 count, 3 one-command sequence differences). The
  other 39 titles are phase-only. This supersedes the earlier "5DO frame 60
  aligns with Opera frame 59" interpretation: the one-field offset was a harness
  sampling-order bug.
- **2026-11-07 timer-14 wait trace (CORE-07/CORE-02).** On Lemmings the
  READ_ID-to-READ_ERROR wait between commands 50 and 51 is **2,169,618 cycles**
  in 5DO and **2,146,784** in Opera (+22,834 cycles). There are **no FIQ events**
  and no CD register accesses during the wait. 5DO's timer 14 underflows **209
  times** at roughly 10,400 cycles per period (`--clio-timer-trace`); Opera's
  wait is consistent with a roughly 1% shorter period. `--clio-timers opera`
  makes the period about 12,400 cycles and shifts the whole boot timeline, so the
  opt-in Opera scheduler is not the fix. This narrows the remaining CD cadence
  difference to the timer-14 period/guest wait loop, not the CD command table.
- **2026-11-07 CD cadence triage (TEST-07/CORE-04).** The harness now classifies
  every final CD transcript and reports the largest cadence step with its
  command-issue PC and retired-instruction jump. On the existing 50-title panel
  (`test55-panel50-first-sector/report.json`) the transcripts are **39 identical,
  8 prefix-only, and 3 sequence-shifted**. The three shifts are one-command
  READ_ID poll placement among READ_DATA commands: Chiki Chiki has one extra
  Opera READ_ID (111/112 commands), while Dragon's Lair and Virtual Horror have
  one extra 5DO READ_ID (111/108 and 110/107). **24/50 titles have a cadence step
  above 1,000 cycles, and every one has the same command-issue PC on both
  engines**: 17 steps are 60-100k cycles, six are 20-60k, and one is >=100k.
  The 60-100k group is a single READ_DATA-to-READ_DATA step where 5DO retires
  about 32k more instructions in the same guest path. The fresh five-title run
  (`test60-cadence-samples5/report.json`) confirms the same picture: Alone
  **361/361**, Road Rash **395/395**, and Wolfenstein **341/341** commands with
  final cycle deltas of `+379`, `+468`, and `-345`; Lemmings is a 105-command
  prefix with one `+22,834` step at command 51 (PC `0x00022140` on both sides,
  `+8,242` instructions); Space Ace is **168/168** with one `+80,567` step at
  command 164 (PC `0x0002aa18` on both sides, `+32,330` instructions). This
  localizes the residual `cdCommandCount` difference to guest wait-loop cadence,
  not a divergent command table. The next measurement is a CD register-access
  trace around the step to separate a CD readiness wait from a timer/frame wait.
- **2026-11-07 CD register trace (TEST-07/CORE-04).** `--cd-register-trace`
  records every CD command/poll/status/data access with its cycle and PC. On
  Space Ace, command 163 is issued at cycle 58,933,287, the READ_DATA status is
  drained by 58,934,229, and there are **no CD register accesses from
  58,935,434 to 59,173,228** (237,794 cycles); command 164 follows at
  59,174,910. On Lemmings, READ_ID is issued at 29,431,455, all 12 status bytes
  are drained by 29,431,952, and there are **no CD register accesses until
  31,599,968**, the start of the next command sequence. The 60-100k and 20-60k
  cadence steps are therefore **not CD readiness waits**: the guest leaves the CD
  driver and waits on a non-CD source (timer/VBlank) before returning. This moves
  the residual cadence lead from CORE-04 to CORE-07.
- **2026-11-07 first-sector 50-title panel.** `test55-panel50-first-sector/report.json`
  (local corpus, debug core `opera-build-pc`, MD5 `53c26d4cb701e3d6e3bb28e877cd502b`)
  captured **50/50 titles with 0 errors** and **50/50 titles match every frame
  hash** at 60/120/300. **48/50 first PC divergences align with Opera frame 59**
  and **2/50 first diverge at f=120 and align with frame 119** (the f=60
  checkpoint itself matched on those two). The first behavioral divergence is
  still only `cdCommandCount`: **36 at f=120, 13 at f=300, 1 at f=60**
  (Icebreaker II), exactly the test43 distribution. One title hit a transient
  CHD read error in the first pass; `--resume` retried it and the final report has
  0 errors.
- **2026-11-07 boot-handoff triage (CORE-04/CORE-07).** The new per-command
  `sg_debug_cd_pc_at`/`sg_debug_cd_instructions_at` stamps localized the 20th
  Century (USA) handoff between the last boot READ_DATA (command 49) and the
  READ_ID (command 50): 5DO spent **+207,707 cycles / +86,511 retired
  instructions** beyond the reference at command 50, with the same guest PC
  (`0x00022140`) on both sides. Matching the oracle's first-sector POLST
  publication (`opera_cdrom.c`, `cdrom_data_publish_read_status_if_ready` with
  `data_len != 0`) reduced the same deltas to **-818 cycles / -376
  instructions**. The f=300 command-104 cycle delta fell from **+324,027** to
  **+111,990**. The 5-title sample keeps **5/5 frame-hash matches** at
  60/120/300 (also at stock 2x), and the 3,000-frame sample keeps the established
  4/5 video result (Road Rash still diverges) with the same CD counts as the
  previous baseline. This also corrects the earlier interpretation: the command-50
  handoff is a separate event at about frame 142, not the source of the
  universal frame-60 one-field phase.
- **2026-11-07 50-title panel: video-clean and PC-phase-clean.** The panel
  (`artifacts/diff/test43-panel50-bg/report.json`, local corpus) captured
  **50/50 titles with 0 errors**. **All 50 match every frame hash** at frames
  60/120/300 (up from 32/50), and **all 50 first PC divergences are instruction
  phase**: 5DO frame 60 aligns with Opera frame 59 with a residual of only
  **-55 to -256 retired instructions**, while the same-frame delta is about
  **89,000**. The only remaining behavioral divergence is `cdCommandCount`:
  36 titles first differ at f=120 (1-2 commands in flight; all recover by
  f=300), 13 at f=300 (seven FMV titles at +3/+4, six at -1/-2), and Icebreaker
  II at f=60. The enabling video fixes were a 24-bit VDL background (the old
  5-bit narrowing turned `0x010101` into black) and treating a zero VDL entry
  as a stop that retains the previous bitmap/control word, matching
  `opera_vdlp.c`; both are covered by tests.
- **2026-11-07 3,000-frame panel.** Video matches at 3,000 on Alone, Lemmings,
  Space Ace, and Wolfenstein 3D; Road Rash still diverges at 1,000 and 3,000.
  CD counts are 365/352 (Lemmings), 851/836 (Road Rash), 349/348 (Space Ace),
  862/861 (Wolfenstein), and 932/932 (Alone). Transient video mismatches at
  f=1000 on Alone and Space Ace recover by f=3000. This separates the remaining
  long-run issue into CD cadence and Road Rash-specific scene divergence.
- **2026-11-06 exact-clock panel (TEST-07).** The previous 50-title panel
  (`artifacts/diff/test21-panel50/report.json`, local corpus) captured **50/50
  titles with 0 errors**. **32/50 match every frame hash** at frames 60/120/300;
  **49/50 match the CD command count at frame 60**; every final sample delta is
  **≤9**. The first divergence is PC at frame 60 on 48/50 titles. PC/CPSR are
  frame-boundary phase samples: the report now prints the first **behavioral**
  divergence separately (frame hash, audio, samples, state, CD). The remaining
  behavioral differences are a 1-2 command CD cadence shift during boot/menu
  traffic and a fixed startup instruction-count offset (the per-frame rate matches).
- **Reference instruction parity (2026-11-06).** The debug core exports
  `sg_debug_arm_instructions`; the harness records it next to 5DO's
  `instructions`. At frame 60, Opera's absolute count is **89,338-90,063
  instructions higher** on all five sample titles (Lemmings 5,383,387 vs
  5,472,729). The offset is already present at frame 1 and stays fixed; the
  per-frame retirement rate matches within **0.1%** over 240 frames on 4/5
  titles (Lemmings diverges where its CD path diverges). This is a
  startup/first-field capture offset, not a per-instruction cycle-model error.
  The FreeDO N=4 mode is still far worse (3,855,498 instructions). Align the
  first captured field before using PC as a gate.
- **CD command cycle stamps (2026-11-06).** `CdromCommandTranscript` records
  the ARM cycle at each opcode issue and the CLI writes `opcode cycle` lines.
  The debug core's `sg_debug_cd_cycle_at` records the same at the exact command
  boundary (`cmd_idx == 0`), which also fixes the old byte-count-modulo-seven
  transcript for one-byte ABORT packets. On Lemmings, the first 105 opcodes
  match but 5DO reaches command 104 at cycle 57,736,851 vs Opera's 57,621,696
  (**+115,155 cycles, ~9.2 ms**). Road Rash is +16,680, Space Ace +80,866,
  Wolfenstein +13,343, and Alone +15,163 at their last common command. The
  divergence is cadence, not the command table.
- **Exact NTSC clock (2026-11-06).** NTSC now uses 60000/1001 (208,541 cycles
  at 12.5 MHz), and the VDL accumulator is exact rational instead of the
  per-batch rounded 8.24 value. Samples at frame 60 went from 44,101 vs 44,144
  to 44,145 vs 44,144; at frame 300 from 220,508 vs 220,720 to 220,728 vs
  220,720. At 3,000 frames Alone, Lemmings, Space Ace, and Wolfenstein match
  Opera's frame hash; Road Rash still diverges at 1,000 and 3,000.
- `benchmarks/diff/samples5.json` (2026-11-06, TEST-07): five local titles,
  300 frames, checkpoints 60/120/300, fixed seed 1488, accuracy profile,
  `cdSpeed=unbounded`, `noGameFixes=true`, PC/CPSR/CD comparison, sample
  tolerance 512. Two consecutive runs produced **identical checkpoint arrays for
  both engines**. Result: **0 match, 5 diverged, 0 error**. The **first divergence
  was the ARM PC at frame 60** on all five titles. Lemmings (USA):
  - f=60: PC `0x00005aa0` vs `0x00005960`; CPSR `0x600000d3` vs `0x800000d3`
    (N/Z/C flag difference; the FIQ bit is set in both, and the exact-clock evidence above shows this is a frame-boundary phase sample); CD commands **9/9 match**.
  - f=120: PC `0x00005988` vs `0x00005974`; CPSR matches; CD commands **50/50 match**.
  - f=300: PC `0x000205f4` vs `0x00020600`; CPSR matches; CD commands **108/105**
    (three extra commands in 5DO by this checkpoint).
  - Sample counts: **44,101/44,144**, **88,202/88,288**, **220,508/220,720** —
    the systematic 59.94/60 Hz offset.
  - Maximum absolute PC delta across the five titles: Alone in the Dark 21,020;
    Road Rash 7,644; Space Ace 6,084; Wolfenstein 3D 3,172; Lemmings 320 bytes.
  This is the first TEST-07 panel; see `TestCorpus.md` 1.1aw.
- `benchmarks/diff/samples5-long.json` (2026-11-06): the same five titles at
  3,000 frames with checkpoints 60/300/1000/3000. Lemmings matched the frame hash
  at all four checkpoints despite PC drift; Wolfenstein and Alone recovered after
  transient mismatches; **Road Rash and Space Ace stayed visibly divergent**
  (f=1000/3000 images show different animation frames/scenes). A ±60-frame
  offset probe on Road Rash found no neighboring hash match, so this is not a
  small scheduling offset. CD command counts stayed close but not exact
  (Road Rash 611/598 at f=1000). The full sequence comparison localizes the first
  command divergence: Lemmings command 105, Road Rash 559, and Wolfenstein 389
  show 5DO issuing `0x10` (READ_DATA) where Opera issues `0x83` (READ_ID); Alone
  diverges at command 433 with equal counts; Space Ace's first 348 commands match
  and 5DO has one extra `0x10`. This is a READ_DATA completion/read-ahead
  difference, not a random divergence. This is the first long-run evidence that
  separates PC drift from visible divergence.
- `benchmarks/diff/smoke.json`: Lemmings (USA), 300 frames, checkpoint 300.
  Result: **Match** (`frameHash 0x110e7dc5` on both engines; first pass
  2026-10-31).
- `benchmarks/diff/showcase.json`: five showcase titles, 3000 frames, one
  checkpoint. First result: **2 video matches** (Wolfenstein 3D, Alone in the
  Dark) and **3 video divergences** (Lemmings, Gex, Road Rash). With the debug
  core and `--compare-pc`, **0/5 titles match**: the two video matches still have
  PC deltas of 184,696 and 739,184 bytes. See `docs/TestCorpus.md` 1.1g for the
  hashes, PCs, and images.
- `benchmarks/diff/sample40.json`: first 40 full-collection titles, 300 frames,
  one checkpoint, accuracy timing, no game fixes. The 2026-11-04 CLIO timer A/B
  (same build and Opera captures, `--clio-timers freedo` on the second run)
  produced **17 match, 23 diverged, 0 error** in both modes. All 40 final frame
  hashes were identical; 37 checkpoint PCs moved. The baseline is 17 rather than
  the previously recorded 18 because the CEL pixel-accounting fix changed the 5DO
  accuracy hashes. This is the current broad post-parity baseline; the full
  594-title run remains runner-bound.
- CORE-01 ARM timing A/B on the same 40-title sample (identical Opera captures):
  `--arm-timing freedo` produced **15 match, 25 diverged**, and
  `--arm-timing arm60` produced **17 match, 23 diverged**, with 0 errors. Only two
  frame hashes changed, both from Diverged to Match. The full 594-title run remains
  the gate; see `TestCorpus.md` 1.1a1.
- CORE-08 profile A/B over the same 40-title sample: `--profile compatible` and
  `--profile accuracy` both produced **17 match, 23 diverged, 0 error**, with identical
  per-title statuses. The override is covered by
  `MergeOptions_ProfileOverrideWinsOverDefaultsAndTitle`; the full-collection A/B
  remains the gate before Accuracy can become the default; see `TestCorpus.md` 1.1ak1.
- A six-title CEL-heavy accuracy-profile run on 2026-11-04 (3,000 frames; Road
  Rash, Gex, AutoBahn Tokio, Johnny Bazookatone, Primal Rage, and DinoPark Tycoon)
  completed both engines with **0 errors and 6 divergences**. The CEL busy
  work-accounting fix changes the 5DO hashes but does not close the differential;
  see `TestCorpus.md` 1.1c1.
- `benchmarks/diff/full.json`: generated from the 594-disc collection on
  2026-10-31 (589 main + 5 MAME) and validated with `--strict`. A two-title
  300-frame smoke (`--limit 2 --frames 300 --no-images --cleanup-iso`) ran both
  titles end to end, confirming root expansion, staging, and cleanup. The full
  run is a nightly/self-hosted workload; a 3,000-frame reference pass takes
  roughly 8-9 hours because OperaCore is real-time paced. Use
  `--no-images --cleanup-iso --resume` on a scratch disk: each title's ISO is
  extracted, used, and removed instead of keeping ~180 GB of staged data.

The early boot frames already exposed a real timing divergence: Opera reaches
the Lemmings boot screen earlier than 5DO. The frame at 300 matches exactly, so
the renderer/CLUT path agrees; the remaining difference is scheduling. Keep
early frames in title-specific allowlists only when the divergence is understood.

`frameWindow` adds offset diagnostics: when a 5DO checkpoint hash equals an Opera
neighboring frame within the window, the report calls the title **Offset** instead
of **Diverged** and prints the matching reference frame. This distinguishes
scheduling drift from a real pixel/state difference. The showcase runs with a
window of 2 and records checkpoint images (`fNNNNNN.png` for 5DO and
`fNNNNNN.bmp` for Opera) under each title directory.

The optional debug core adds PC evidence to the report without changing the
pass/fail rule. On the Lemmings smoke the frame hash matches while the PC delta is
4 bytes; at frame 3000 the delta is 11,076 bytes, proving that matching video at a
checkpoint does not mean the instruction streams agree.

## 9. Next increments

1. **TEST-06:** replace the generic menu script with per-title input scripts that
   reach and sustain gameplay for the top-100 audit. Record `Good`/`Playable`/
   `Unplayable`, a reason, and a checkpoint screenshot per title.
2. **CORE-04/CORE-13:** close the 11 CD-only first behavioral divergences at
   f=300. The transcript, cadence, and register traces already show the command
   table and issue PCs match; the residual is the guest wait loop and the
   data-dependent copy/decrypt work, not a CD-readiness wait.
3. **CORE-11/16:** run the full 594-title collection on the fresh-capture nightly
   baseline. The 50-title panel is video/audio/PC-phase clean; the known CD
   cadence is the only behavioral field.
4. Keep a visual artifact for every panel checkpoint. The nightly showcase
   already captures images; the 300/900-frame panels should use the same output
   when the audit begins, and the first hash mismatch is the regression signal.
5. Extend audio comparison past the reference's 1.214 ppm clock quantization.
   The current gate is exact only while sample counts align; a resampled or
   windowed comparison would let long-run captures gate waveform content instead
   of reporting a lag state.
