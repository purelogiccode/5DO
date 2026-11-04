# Differential Harness

**Status:** implemented 2026-10-31; first showcase capture in progress.
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
| Audio hash | Packed DSP samples, FNV-1a | Interleaved stereo samples, FNV-1a | No (reference PRNG seed is wall-clock) |
| DSP sample count | `run --json` | Audio callback count | No |
| ARM PC | `run --json` | Optional debug export | No (shown as delta) |
| Save-state hash | Optional `--state-hash` | ARM-only optional debug hash | No |
| CD transcript | `--cd-trace` (future) | Not exposed | No |

The frame hash is the same algorithm as the emulator's `run --json` `frameHash`.
It is computed from the assembled BGRA8888 pixels, so a matching hash means the
two engines produced byte-identical visible output for that frame. The report's
**PC delta** column is the largest absolute `Opera PC - 5DO PC` across the
checkpoints; it is always shown when the debug core exposes a PC, even when the
video fields match.

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

# Strict mode with a debug core that exposes PC/CD state.
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/showcase.json --out artifacts/diff/showcase-debug `
  --core artifacts/diff/opera-build/out/OperaCore.dll --compare-pc

# Generate a full-collection manifest after changing the disc roots.
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  manifest --root "J:\Panasonic 3DO" `
  --root "G:\MAME\MAME Software List CHDs\3do" `
  --out benchmarks/diff/full.json --frames 3000 --checkpoint 3000
```

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
`--no-images`, `--cleanup-iso`, `--compare-pc`, and `--pc-tolerance <bytes>`.
`--compare-pc` requires a reference core with the optional debug exports; without
them the PC field is reported as unavailable rather than silently passing.

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
      "compareSamples": false,
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
- The ABI exposes video, audio, input, and lifecycle callbacks only. It does not
  expose PC, state hashes, CD status, or CD transcripts. Those fields are `null`
  on the Opera side and are not compared by default.
- An optional locally built debug core can export `sg_debug_arm_pc`,
  `sg_debug_arm_cpsr`, `sg_debug_arm_state_hash`, `sg_debug_cd_status`, and
  `sg_debug_cd_poll`. The harness detects those symbols with
  `NativeLibrary.TryGetExport`; a stock core simply leaves the fields null. Build
  the debug core from `References/CPP_SimpleGui/Cores/OperaCore` with the small
  getters in `opera_arm.c`/`opera_xbus_cdrom_plugin.c` and pass
  `--core artifacts/diff/opera-build/out/OperaCore.dll`. The fields are evidence,
  not part of the default pass/fail gate: a 4-byte PC delta is normal when the
  frame hash matches, and the CD FIFO status is not the same value as 5DO's
  `DriveStatus`.
- The core seeds `std::rand`, `prng16`, and `prng32` from `std::time(nullptr)`.
  Frame hashes stay usable; audio hashes are informational until the reference
  accepts a fixed seed. Keep `compareAudio` false unless the run is known to be
  deterministic.
- The core is paced to the region refresh rate, so a 3000-frame capture takes
  about 50 seconds per title. Use `--resume` for long runs.

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
  one checkpoint, accuracy profile, no game fixes. Result: **18 match, 22
  diverged, 0 error**. This is the current broad post-parity baseline; the full
  594-title run remains runner-bound.
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

1. Add PC and state hashes to a reference-side export (requires rebuilding the
   SimpleGui OperaCore wrapper, not modifying libopera behavior).
2. Add a CD command transcript to the 5DO capture and a reference-side transcript.
3. Extend the showcase manifest to the full 594-disc corpus with per-title
   checkpoints and reasons.
4. Wire `baseline` into the nightly workflow and publish `report.html` as an
   artifact.
5. Add visual artifacts (checkpoint PNGs) once the hash gate is stable.
