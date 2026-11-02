# Fuzzing and parser security

5DO reads several untrusted formats: save states, ISO/BIN/CUE/CHD images, the Opera
file system, and guest-driven CD-ROM command streams. The standalone
`tools/FiveDo.Fuzz` harness mutates those inputs deterministically and reports any
exception that is not a documented, typed rejection.

The harness is not a coverage-guided libFuzzer/AFL replacement. It is a fast,
reproducible regression and nightly safety net that runs on every platform with the
normal .NET SDK and no native toolchain.

## Targets

| Target | Input | What it exercises |
|---|---|---|
| `state` | Valid save state plus random bytes | Versioned state header, section table, CRC, decompression, every section loader, and one frame after a successful load |
| `disc` | Zero-filled ISO and random CHD bytes | ISO/CHD openers, sector reads, and container validation |
| `cue` | Valid CUE+BIN plus mutated text | CUE parser, file resolution, track/index parsing, and sector reads |
| `opera-fs` | Synthetic Opera volume plus mutations | Volume header, directory chain, entry names/copies, cycle/depth limits, and file reads |
| `cd-command` | Random command/poll/tick/read stream | `CdromDevice` command state machine in both FreeDO and Opera timing modes, including eject, inject, reset, and data/status drains |
| `cpu` | Random ARM60 words plus a random CPSR | Decode/execute bounds, banked-register switching, data-abort handling, and both ARM timing modes |

The `state`, `disc`, and `cue` targets expect many inputs to be rejected. A rejection is
a **typed failure** when it uses one of the documented exception types:

- `InvalidStateException`
- `DiscImageException`
- `InvalidDataException`
- `IOException` and `UnauthorizedAccessException`
- `ArgumentException`
- `NotSupportedException`
- `FormatException`
- `SecurityException`

Any other exception is an **unexpected failure**. The harness writes the input, the
failure text, and the exact seed/iteration to the reproduction directory and exits with
code 1.

## Usage

```powershell
# Time-limited run (the nightly default is 120 seconds per target)
dotnet run --project tools/FiveDo.Fuzz -c Release -- --seconds 60 --target all --json

# Bounded deterministic run
dotnet run --project tools/FiveDo.Fuzz -c Release -- --iterations 500 --seed 42 --target state,cue

# One target with a human-readable report
dotnet run --project tools/FiveDo.Fuzz -c Release -- --seconds 30 --target opera-fs
```

Options:

| Option | Meaning |
|---|---|
| `--target LIST` | `all`, or a comma-separated list of `state`, `disc`, `cue`, `opera-fs`, `cd`, `cpu`. |
| `--iterations N` | Maximum iterations per target. When omitted, the run is time-limited. |
| `--seconds N` | Seconds per target. Defaults to 30 when `--iterations` is omitted. |
| `--seed N` | Base deterministic seed. Defaults to 1. |
| `--out DIR` | Reproduction directory. Defaults to `artifacts/fuzz`. |
| `--json` | Emit the machine-readable report. |
| `-h`, `--help` | Show help. |

The report includes per-target iteration and typed/unexpected counts. The process exits
with code 0 when there are no unexpected failures, 1 when a defect is found, and 2 for
invalid command-line options.

## Reproducing a failure

A failure directory contains:

```text
artifacts/fuzz/state-seed397-iter42/
  payload.bin
  failure.txt
```

`failure.txt` records the target, seed, iteration, description, exception type, message,
and stack trace. Rerun the same case with the reported seed and enough iterations:

```powershell
dotnet run --project tools/FiveDo.Fuzz -c Release -- `
  --target state --seed 397 --iterations 43 --out artifacts/fuzz-repro
```

The generator is deterministic for a given seed and target, so iteration 42 is the same
input. Add a focused regression test to `tests/FiveDo.Tests` once the defect is fixed;
the nightly run is the broad safety net, not a substitute for a targeted test.

## Nightly CI

[`.github/workflows/fuzz.yml`](https://github.com/purelogiccode/5DO/blob/master/.github/workflows/fuzz.yml)
runs the harness every night with a run-specific seed and uploads the JSON report and any
reproduction directories. It can also be started manually with a custom duration.

## Adding a target

1. Implement `IFuzzTarget` in `tools/FiveDo.Fuzz/`.
2. Generate a `FuzzCase` with a description and, when needed, companion files.
3. Execute the parser or device and let documented rejections propagate.
4. Register the target in `FuzzTargetKind`, `FuzzOptions.EnumerateTargets`, and
   `FuzzTargetFactory`.
5. Add a bounded run to `FuzzHarnessTests` and update this table.

Keep case generation cheap and deterministic. Use `FuzzContext.Random` only; do not use
`Random.Shared`, wall-clock time, or machine-specific paths in generated input.

## Scope and limitations

- The harness targets parsers, the CD command boundary, and the ARM60 instruction
  decoder. It does not fuzz DSP, MADAM, or GPU pipelines; those have their own
  deterministic conformance and golden tests.
- The `cd-command` target uses a synthetic 16-block Opera disc. It validates command
  state-machine safety, not title-specific CD timing.
- The `disc` target does not generate valid compressed CHD payloads; valid CHD coverage
  comes from the compatibility corpus and `disc verify`.
- A clean run is evidence of robustness, not a proof of security. Report security issues
  through the process in [`SECURITY.md`](https://github.com/purelogiccode/5DO/blob/master/SECURITY.md).

## Evidence

- 2026-11-02: a 60-second-per-target run (seed 20261102) completed **6,705,667
  iterations** across the state, disc, CUE, Opera FS, and CD targets with **0 unexpected
  failures** and 1,758,669 typed rejections. A 60-second CPU-stream run completed
  **71,783 iterations** with 0 failures. The JSON reports are `artifacts/fuzz-nightly.json`
  and `artifacts/fuzz-cpu.json`.
- The nightly workflow is the ongoing gate. Record a new clean run here when a release
  gate requires it.
