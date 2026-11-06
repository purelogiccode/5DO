# Differential Harness

**Status:** implemented 2026-10-31; TEST-07 oracle instrumentation 2026-11-06.
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
| Audio hash | Packed DSP samples, FNV-1a | Interleaved stereo samples, FNV-1a | No; enable with `--compare-audio` once sample counts align |
| ARM PC | `run --json` | Optional debug export | No; enable with `--compare-pc` |
| ARM CPSR | `run --json` | Optional debug export | No; `--compare-pc` also compares CPSR when both sides expose it |
| CD command count/hash | `CdromCommandTranscript` issue-time counters | Optional debug export | No; enable with `--compare-cd` |
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

5DO counts a CD command when the first command byte is written
(`CommandOpcodeObserver`); the reference counts `XBP_SET_COMMAND` at the same
protocol phase. A command still in flight at a checkpoint can produce a 1-3
command count difference. The cumulative command hash is only comparable when the
counts match.

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
`--cel-timing freedo|opera`, `--cd-timing freedo|opera`, and
`--profile compatible|accuracy|performance|custom`. The default fixed seed is
1488; it is applied to both engines and recorded in each capture and in the
report provenance.
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
  flight at a checkpoint can still differ by 1-3 commands.
- `compare.compareSamples` gates the DSP sample count; `compare.sampleTolerance`
  allows the systematic 59.94/60 Hz field-rate offset (43 samples at frame 60,
  212 at frame 300). `compare.compareAudio` gates the cumulative audio hash; keep
  it off until the sample streams are aligned, because a sample-count offset makes
  the cumulative hashes incomparable.
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
  core adds `sg_debug_arm_pc`, `sg_debug_arm_cpsr`, `sg_debug_arm_state_hash`,
  `sg_debug_cd_status`, `sg_debug_cd_poll`, `sg_debug_cd_command_count`,
  `sg_debug_cd_command_hash`, and `sg_debug_set_seed`. The harness detects those
  symbols with `NativeLibrary.TryGetExport`; a stock core simply leaves the fields
  null. Build the debug core from `References/CPP_SimpleGui/Cores/OperaCore` with
  the small getters in `opera_arm.c`/`opera_xbus_cdrom_plugin.c` and pass
  `--core artifacts/diff/opera-build-current/OperaCore.dll`. The fields are
  evidence, not part of the default pass/fail gate: a 4-byte PC delta is normal
  when the frame hash matches, and the CD FIFO status is not the same value as
  5DO's `DriveStatus`.
- `sg_debug_set_seed(seed)` seeds `std::rand`, `prng16`, and `prng32` before
  `sg_init`; the harness applies a fixed seed (default 1488) and records it. Two
  consecutive runs with the same seed produced identical checkpoint arrays for
  both engines on 2026-11-06.
- The reference core is paced to the region refresh rate (59.94 Hz NTSC), while
  5DO advances 60 fields per second. A 300-frame run therefore differs by about
  212 samples. `sampleTolerance` absorbs the count; the cumulative audio hash is
  still not comparable until the streams are aligned or resampled.
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
golden results. Intentional changes require a new baseline report.

## 7. CI

`.github/workflows/diff-harness.yml` is an opt-in/nightly workflow for a
self-hosted Windows runner that has the BIOS and disc corpus. GitHub-hosted
runners run the harness unit tests through the normal test job; they cannot run
the reference core or the local discs.

## 8. Current evidence

- `benchmarks/diff/samples5.json` (2026-11-06, TEST-07): five local titles,
  300 frames, checkpoints 60/120/300, fixed seed 1488, accuracy profile,
  `cdSpeed=unbounded`, `noGameFixes=true`, PC/CPSR/CD comparison, sample
  tolerance 512. Two consecutive runs produced **identical checkpoint arrays for
  both engines**. Result: **0 match, 5 diverged, 0 error**. The **first divergence
  was the ARM PC at frame 60** on all five titles. Lemmings (USA):
  - f=60: PC `0x00005aa0` vs `0x00005960`; CPSR `0x600000d3` vs `0x800000d3`
    (FIQ-disable bit differs); CD commands **9/9 match**.
  - f=120: PC `0x00005988` vs `0x00005974`; CPSR matches; CD commands **50/50 match**.
  - f=300: PC `0x000205f4` vs `0x00020600`; CPSR matches; CD commands **108/105**
    (three extra commands in 5DO by this checkpoint).
  - Sample counts: **44,101/44,144**, **88,202/88,288**, **220,508/220,720** —
    the systematic 59.94/60 Hz offset.
  - Maximum absolute PC delta across the five titles: Alone in the Dark 21,020;
    Road Rash 7,644; Space Ace 6,084; Wolfenstein 3D 3,172; Lemmings 320 bytes.
  This is the first TEST-07 panel; see `TestCorpus.md` 1.1aw.
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

1. Run the generated `benchmarks/diff/panel50.json` against the full corpus; the
   five-title `samples5.json` panel is the current execution evidence.
2. Align or resample the audio streams so `compareAudio` can gate the cumulative
   hash; the 59.94/60 Hz sample-count offset currently makes it incomparable.
3. Add a CD command-tail or per-index sequence comparison to distinguish
   in-flight count drift from a real command-sequence divergence.
4. Triage the frame-60 PC/CPSR divergence (CORE-07). It is now the first
   divergence on all five sample titles; the Lemmings CPSR difference is the FIQ
   disable bit.
5. Wire `baseline` into the nightly workflow and publish `report.html` as an
   artifact.
6. Add visual artifacts (checkpoint PNGs) once the hash gate is stable.
