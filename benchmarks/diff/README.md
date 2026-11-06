<p align="center">
  <a href="https://github.com/purelogiccode/5DO/actions/workflows/ci.yml"><img src="https://github.com/purelogiccode/5DO/actions/workflows/ci.yml/badge.svg?branch=master" alt="CI status"></a>
  <a href="https://github.com/purelogiccode/5DO/actions/workflows/diff-harness.yml"><img src="https://github.com/purelogiccode/5DO/actions/workflows/diff-harness.yml/badge.svg?branch=master" alt="Differential harness"></a>
  <a href="https://purelogiccode.github.io/5DO/DifferentialHarness.html"><img src="https://img.shields.io/badge/docs-differential%20harness-2ea44f" alt="Documentation"></a>
  <a href="../../LICENSE"><img src="https://img.shields.io/badge/license-FreeDO%20modified%20LGPL-blue" alt="License"></a>
</p>

# Differential manifests

These manifests drive `tools/FiveDo.DiffHarness`. They are evidence definitions,
not product configuration.

- `smoke.json` - one title, 300 frames, one matched checkpoint. Use it as the fast
  health check after changing the harness, CLI checkpoint path, or reference
  staging.
- `showcase.json` - the five-title showcase with 3,000-frame checkpoints, a
  two-frame offset window, and checkpoint images.
- `samples5.json` - the local `D:/Samples/3DO` five-title TEST-07 panel:
  300 frames, checkpoints 60/120/300, fixed seed 1488, PC/CPSR/CD comparison,
  and a 512-sample tolerance for the 59.94/60 Hz field-rate offset.
- `samples5-long.json` - the same five local titles at 3,000 frames with
  checkpoints 60/300/1000/3000. Use it to separate boot-time PC drift from
  visible in-game divergence.
- `panel50.json` - the generated 50-title TEST-07 accuracy panel: evenly spaced
  titles from `full.json`, 300 frames, checkpoints 60/120/300, PC/CPSR/CD
  comparison, sample tolerance 512. Run it on a machine with the full corpus.
- `full.json` - the generated 594-disc collection manifest. Its `roots` object
  currently points at the machine-specific `J:/Panasonic 3DO` and
  `G:/MAME/MAME Software List CHDs/3do` paths; every title uses `${games}` or
  `${games2}` so only the roots need editing on another machine.

These manifests assume the local corpus and the BIOS at `bios/panafz1.bin`.
Relative paths resolve from the repository root. The harness stages the OperaCore
DLL and BIOS under `artifacts/diff/work/` and never modifies `References/` or the
disc collection.

Run:

```powershell
dotnet build src/FiveDo.Cli -c Release
dotnet build tools/FiveDo.DiffHarness -c Release

tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/smoke.json --out artifacts/diff/smoke

tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/showcase.json --out artifacts/diff/showcase

# TEST-07 panel: fixed seed, PC/CPSR/CD comparison.
# Build the debug core first (see docs/DifferentialHarness.md section 2).
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/samples5.json --out artifacts/diff/test07-samples5 `
  --no-images --seed 1488 --compare-cd `
  --core artifacts/diff/opera-build-current/OperaCore.dll
```

The reference core is paced to 60 Hz, so 3,000 frames take about 50 seconds per
title. Use `--resume` to reuse captures. `--force` is required after changing
frame counts, checkpoints, options, or the image/offset diagnostics. For a
CORE-08 profile A/B, use fresh output directories so cached captures from the
other profile are not reused:

```powershell
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/full.json --out artifacts/diff/full-compatible `
  --profile compatible --no-images --cleanup-iso --resume

tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/full.json --out artifacts/diff/full-accuracy `
  --profile accuracy --no-images --cleanup-iso --resume
```

For an isolated timing-mode A/B, use the same fresh-output pattern with
`--arm-timing freedo|arm60`, `--clio-timers freedo|opera`,
`--cel-timing freedo|opera`, or `--cd-timing freedo|opera`. The override is applied to
every 5DO capture and recorded in report provenance; the reference captures are reused
by `--resume`.

Regenerate the full manifest after changing the collection:

```powershell
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe manifest `
  --root "J:\Panasonic 3DO" `
  --root "G:\MAME\MAME Software List CHDs\3do" `
  --out benchmarks/diff/full.json --frames 3000 --checkpoint 3000
```

A full 594-title run at 3,000 frames is roughly 8-9 hours because the reference
core runs in real time; use the self-hosted nightly workflow and `--resume`.

See `docs/DifferentialHarness.md` for the schema, allowlist policy, report
formats, and current limitations.
