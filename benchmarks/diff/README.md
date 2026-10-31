# Differential manifests

These manifests drive `tools/FiveDo.DiffHarness`. They are evidence definitions,
not product configuration.

- `smoke.json` - one title, 300 frames, one matched checkpoint. Use it as the fast
  health check after changing the harness, CLI checkpoint path, or reference
  staging.
- `showcase.json` - the five-title showcase with 3,000-frame checkpoints, a
  two-frame offset window, and checkpoint images.
- `full.json` - the generated 594-disc collection manifest. Its `roots` object
  currently points at the machine-specific `J:/Panasonic 3DO` and
  `G:/MAME/MAME Software List CHDs/3do` paths; every title uses `${games}` or
  `${games2}` so only the roots need editing on another machine.

Both manifests assume the local corpus and the BIOS at `bios/panafz1.bin`.
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
```

The reference core is paced to 60 Hz, so 3,000 frames take about 50 seconds per
title. Use `--resume` to reuse captures. `--force` is required after changing
frame counts, checkpoints, options, or the image/offset diagnostics.

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
