# Build, Test, and Publish

This document is the practical build/test/publish guide for the repository. For the
test design rationale, see [`TestStrategy.md`](TestStrategy.md). For trimming and
NativeAOT details, see [`Trimming.md`](Trimming.md). For packaging contents, see
[`../packaging/README.md`](../packaging/README.md).

---

## 1. Prerequisites

| Requirement | Version / notes |
|---|---|
| .NET SDK | **10.0.100** or newer. `global.json` rolls forward to the latest 10.0 feature band. |
| OS | Windows 10+ (validated), current Linux desktop, macOS 12+ (publish outputs only for the latter two in this release candidate). |
| GPU | OpenGL 3.3 desktop or OpenGL ES 3.0/3.1 for the GPU presenter. The CPU fallback runs without one. |
| Audio | No system OpenAL install is required. OpenAL Soft 1.23.1 is bundled by the build. |
| Optional | A legally obtained 3DO BIOS (1 MB) and disc images for real-content validation. Tests do not need them. |
| Optional | `jq` for the CI allocation-gate command. |
| Optional | PowerShell 7 (`pwsh`) for packaging and soak scripts. |

The repository does **not** include BIOS ROMs, game images, or extracted assets.

Check the SDK:

```powershell
dotnet --version
dotnet --list-sdks
```

---

## 2. Repository layout

```text
5DO.sln
Directory.Build.props        # net10.0, nullable, warnings-as-errors, version, analyzers
Directory.Build.targets      # OpenAL Soft native copy for Windows builds
Directory.Packages.props     # central package versions
global.json                  # SDK pin/roll-forward
src/
  FiveDo.Core/               # contracts, value types, constants, state primitives, logging
  FiveDo.Cpu/                # ARM60 interpreter and disassembler
  FiveDo.Memory/             # RAM/ROM/NVRAM, word-swapped bus
  FiveDo.Madam/              # CEL/pixel/matrix engine
  FiveDo.Clio/               # timers, FIFOs, DMA, interrupts, XBUS/DSP windows
  FiveDo.Dsp/                # audio DSP
  FiveDo.Sport/              # VRAM fill/copy engine
  FiveDo.Video/              # VDLP, frame assembly, mailbox, CPU presenter
  FiveDo.Video.OpenGL/       # OpenGL/GLES presenter, filters, shaders
  FiveDo.Xbus/               # XBUS controller and devices
  FiveDo.Cdrom/              # MEI CD-ROM device
  FiveDo.Storage/            # ISO/BIN/CUE/CHD, BIOS/NVRAM, game database
  FiveDo.Audio/              # WAV/OpenAL/audio analysis/tap
  FiveDo.Input/              # controller state, PBUS, profiles, scripts
  FiveDo.Diagnostics/        # logging, tracing, PNG/frame dumps, benchmark gates
  FiveDo.Machine/            # composition root, frame loop, Quartz
  FiveDo.Host/               # core runner, pacing, settings, save slots, library, fixes
  FiveDo.Cli/                # headless commands
  FiveDo.Frontend.Avalonia/  # desktop shell and GPU probe
tests/FiveDo.Tests/          # xUnit tests, one folder per subsystem
benchmarks/FiveDo.Benchmarks # stopwatch frame benchmark and CI gate
packaging/                   # portable archive script and Linux desktop entry
tools/                       # release soak and HQx generation
docs/                        # this documentation set
```

Project dependency rule: arrows point downward only. `FiveDo.Core` has no project
references; chip projects reference only Core; `FiveDo.Machine` references all chips;
`FiveDo.Host` references Machine plus host services; frontends reference Host. See
[`Architecture.md`](Architecture.md) section 3.

---

## 3. Restore, build, test

From the repository root:

```powershell
dotnet restore 5DO.sln
dotnet build 5DO.sln -c Release
dotnet test 5DO.sln -c Release
```

The current suite is **818 tests** (verified 2026-10-28) and completes in a few seconds
on the development machine. The build treats warnings as errors and runs the .NET
analyzers plus Meziantou and Roslynator analyzers.

Useful variants:

```powershell
# Debug build and test
dotnet build 5DO.sln
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj

# One test class
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj --filter "FullyQualifiedName~ArmDataProcessingTests"

# One test by name fragment
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj --filter "DisplayName~state_roundtrip"

# List tests without running
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj --list-tests

# Formatting check (CI runs this on Linux)
dotnet format 5DO.sln --verify-no-changes --no-restore

# Apply formatting locally
dotnet format 5DO.sln
```

### Test project layout

The suite is one xUnit project with subsystem folders:

| Folder | Covers |
|---|---|
| `Core/` | state framing/CRC, logging, constants, version |
| `Cpu/` | ARM data processing, memory, block transfer, multiply/swap, conditions, control flow, modes/banked registers, cycles, disassembler, synthetic boot |
| `Memory/` | byte order, unaligned access, mirrors, NVRAM |
| `Madam/` | CEL bit reader, CCB parser, geometry, rasterizers, golden scenes, pixel decode/process, projector |
| `Clio/` | registers, timers, FIFOs, DMA, interrupts |
| `Dsp/` | instruction decode, ALU, multiplier, memory, semaphore, sample cadence |
| `Sport/` | VRAM fill/copy, high-resolution mirroring |
| `Video/` | VDLP parsing, frame assembly, viewport, uploader, mailbox, HQx, shaders, swap interval, capabilities, allocations |
| `Xbus/` | device selection, FIFOs, polling |
| `Cdrom/` | command state machine, status/data, TOC, READ_ID pulse |
| `Storage/` | ISO/BIN/CUE/CHD, BIOS, NVRAM, volume header, game database, prefetch |
| `Audio/` | ring buffer, WAV writer/analyzer, statistics, limiter, tap, OpenAL (opt-in) |
| `Input/` | PBUS encoding, profiles, binding editor, scripts, gamepad mapper |
| `Machine/` | composition, frame loop, Quartz, save/load, BIOS UI validation |
| `Host/` | core runner, pacing, settings, save slots, state inspector, NVRAM, library, compatibility, autopilot |
| `Diagnostics/` | frame timing, benchmark gate/comparison, diagnostic tools |
| `Frontend/` | startup arguments, GL backend parsing, probe options, pixel sizing, opt-in GPU probe |
| `Cli/` | JSON contracts, process-level smoke tests |
| `TestUtilities/` | ARM test assembler/rig, fake chips/MMIO, PNG writer |

### Opt-in test environment variables

| Variable | Effect |
|---|---|
| `FIVEDO_BIOS=<path>` | Enables real-BIOS machine/boot validation tests. |
| `FIVEDO_FRAME_DUMP=<dir>` | Writes boot-frame PNGs when a real BIOS is used. |
| `FIVEDO_GPU_TESTS=1` | Runs the real WGL + ANGLE probe integration test. Requires a desktop GL/ANGLE context and a built frontend. |
| `FIVEDO_AUDIO_TESTS=1` | Opens a real OpenAL device and plays a short tone. On headless machines set `ALSOFT_DRIVERS=null` to use OpenAL's null backend. |
| `FIVEDO_AUDIO_TAP=<prefix>` | Not a test switch; enables the live PCM/timing tap for manual audio diagnosis. |
| `FIVEDO_GAMES=<dir>` | Used by `tools/release-soak.ps1`, not by `dotnet test`. |

Examples:

```powershell
$env:FIVEDO_BIOS = "C:\3DO\panafz1.bin"
$env:FIVEDO_FRAME_DUMP = "artifacts\bios-frames"
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release

$env:FIVEDO_GPU_TESTS = "1"
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release --filter "FullyQualifiedName~GpuProbeIntegrationTests"

$env:FIVEDO_AUDIO_TESTS = "1"
$env:ALSOFT_DRIVERS = "null"
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release --filter "FullyQualifiedName~OpenAlAudioOutputTests"
```

Real-content tests skip cleanly when the required environment variables are absent.

---

## 4. Benchmarks

The benchmark is a dependency-free stopwatch harness, not BenchmarkDotNet. It runs a
synthetic ARM program by default so CI can gate the core without copyrighted content.

```powershell
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- --frames 600
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- --frames 600 --json
```

Real-content variants:

```powershell
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- `
  --bios bios/panafz1.bin --disc "game.chd" --frames 600 --label "disc"

dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- `
  --state-in artifacts/compat-current/roadrash-race.state --frames 600 --label "roadrash-race"
```

Options:

| Option | Default | Meaning |
|---|---:|---|
| `--frames N` | 600 | Measured frames after warm-up. |
| `--warmup N` | 60 | Unmeasured warm-up frames (JIT, DSP tables, packers). With `--state-in`, advances the loaded state into the scene under test. |
| `--bios <path>` | none | Load a real BIOS instead of the synthetic program. |
| `--disc <path>` | none | Insert a disc with `--bios`. |
| `--state-in <path>` | none | Load a save state instead of booting. |
| `--prefetch` | off | Enable the bounded disc prefetch cache. |
| `--high-res` | off | Enable the opt-in 640x480 mode. |
| `--label <text>` | `synthetic` / `disc` / `state` | Report label. |
| `--json` | off | Emit the JSON result. |
| `--gate` | off | Enforce the CI budgets; exit code 3 on failure. |
| `--baseline <json>` | none | Compare against a previously recorded result. |
| `--baseline-threshold <fraction>` | `0.10` | Allowed regression (10%). Exit code 4 on failure. |

JSON shape (abridged):

```json
{
  "ok": true,
  "label": "synthetic",
  "frames": 600,
  "warmupFrames": 60,
  "stateWarnings": [],
  "core": { "totalMs": 0.0, "msPerFrame": 0.0, "p50Ms": 0.0, "p95Ms": 0.0, "p99Ms": 0.0, "maxMs": 0.0, "fps": 0.0, "millionInstructionsPerSecond": 0.0, "instructionsPerFrame": 0 },
  "pack": { "totalMs": 0.0, "microsecondsPerFrame": 0.0 },
  "mailbox": { "totalMs": 0.0, "microsecondsPerFrame": 0.0 },
  "read": { "totalMs": 0.0, "microsecondsPerFrame": 0.0 },
  "total": { "msPerFrame": 0.0, "p50Ms": 0.0, "p95Ms": 0.0, "p99Ms": 0.0, "maxMs": 0.0, "fps": 0.0, "budget60Ms": 16.667, "headroom": 0.0 },
  "allocations": { "totalBytes": 0, "bytesPerFrame": 0, "coreBytesPerFrame": 0, "packBytesPerFrame": 0, "mailboxBytesPerFrame": 0, "readBytesPerFrame": 0 },
  "machine": { "cyclesPerFrame": 208333, "pc": 0, "frameHash": 0 }
}
```

The gate (`BenchmarkGate`) requires:

- `allocations.bytesPerFrame == 0`
- `core.p99Ms <= 8.0`
- `total.msPerFrame <= 8.0`
- `total.p99Ms <= 16.667`

CI runs:

```bash
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release --no-build -- \
  --frames 120 --label ci --json --gate > artifacts/benchmark.json
jq -e '.allocations.bytesPerFrame == 0' artifacts/benchmark.json
```

The baseline comparison is same-machine only; do not compare numbers across machines.
See [`Performance.md`](Performance.md) for the measured baselines and methodology.

---

## 5. GPU presentation probe

The probe opens the real Avalonia OpenGL surface, presents synthetic 320x240 VDL
frames at a requested output size, and writes per-frame present/GPU timings, capabilities,
allocations, and optional CPU/GPU parity as JSON. It does not load a BIOS or disc.

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-probe artifacts/gl-probe/4k-sharp.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen
```

Options:

| Option | Default | Meaning |
|---|---:|---|
| `--gl-probe <path>` | required | Output JSON path. |
| `--gl-probe-size WxH` | 3840x2160 | Physical output size, 16x16..16384x16384. |
| `--gl-probe-frames N` | 120 | Measured frames, 1..100000. |
| `--gl-probe-warmup N` | 30 | Unmeasured warm-up frames, 0..10000. |
| `--gl-probe-filter <name>` | `sharp-bilinear` | `accurate`, `nearest`, `sharp-bilinear`, `crt`, `xbr`, `hqx`, `hqx3x`, `hqx4x`, `fsr` (aliases accepted). |
| `--gl-probe-vsync` | off | Request vsync. |
| `--gl-probe-offscreen` | off | Render to an offscreen FBO at the requested size. |
| `--gl-probe-capture <path>` | none | Write a PNG of the presented frame. |
| `--gl-probe-timeout N` | 30 | Overall timeout in seconds, 1..600. |
| `--gl-backend auto|wgl|angle|software` | `auto` | Renderer backend. `--gl-probe-backend` is an alias. |

A 1:1 `accurate` capture at 320x240 triggers an automatic CPU/GPU parity comparison
against `FrameAssembler`; the report includes `Parity.Compared`, `Matches`,
`DifferentPixels`, `CpuHash`, `GpuHash`, and `VerticalFlip`.

See [`Video.md`](Video.md) and [`Performance.md`](Performance.md) for interpretation.

---

## 6. Publish

### Trimmed self-contained single file (default release shape)

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -m:1 `
  -o artifacts/cli/win-x64

dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -m:1 `
  -o artifacts/frontend/win-x64
```

Supported RIDs in CI:

`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

Both executables publish trimmed by default. The frontend also ships native
Skia/HarfBuzz/ANGLE assets. OpenAL Soft is copied as `OpenAL32.dll` on Windows by
`Directory.Build.targets`; Linux/macOS use the package's native names.

### NativeAOT (opt-in, win-x64)

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:FiveDoNativeAot=true -p:PublishSingleFile=true -m:1 `
  -o artifacts/cli-aot

dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj -c Release -r win-x64 `
  --self-contained true -p:FiveDoNativeAot=true -m:1 `
  -o artifacts/frontend-aot
```

AOT is not part of the six-RID default matrix. The CLI AOT path opens live OpenAL; the
frontend AOT audio path is not validated yet. See [`Trimming.md`](Trimming.md).

### Smoke tests

```powershell
artifacts/cli/win-x64/FiveDo.Cli.exe --version
artifacts/cli/win-x64/FiveDo.Cli.exe selftest --json
artifacts/cli/win-x64/FiveDo.Cli.exe bench --frames 2 --json

artifacts/frontend/win-x64/FiveDo.Frontend.Avalonia.exe `
  --smoke-test --smoke-output artifacts/frontend-smoke.txt
```

The frontend smoke test opens the real window, runs one UI pass (GL hidden for headless
CI), writes `FRONTEND SMOKE OK <version>`, and exits 0.

### Portable archives

```powershell
pwsh -File packaging/package.ps1 -Component cli -Rid win-x64 `
  -PublishDirectory artifacts/cli/win-x64 -OutputDirectory artifacts/packages
```

The archive contains the publish output (minus PDBs), `README.md`,
`THIRD-PARTY-NOTICES.md`, `docs/Licensing.md`, `docs/ReleaseLegalReview.md`, the latest
`docs/ReleaseNotes-*.md`, and a generated `PACKAGE.txt` with component, RID, version,
commit, and build time. Linux archives also include `5do.desktop`.

---

## 7. CI jobs

The workflow is [`.github/workflows/ci.yml`](../.github/workflows/ci.yml).

| Job | Runner | What it does |
|---|---|---|
| `build-test` | Windows, Ubuntu, macOS | Restore, Release build, all tests. Ubuntu also runs format check, benchmark smoke + gate, allocation gate, and uploads the baseline. Windows runs trimmed CLI and frontend publish smoke tests. |
| `aot-cli` | Windows | NativeAOT win-x64 CLI publish, `--version`/`selftest`/`bench`, artifact upload. |
| `aot-frontend` | Windows | NativeAOT win-x64 frontend publish, smoke test, artifact upload. |
| `publish-cli` | Ubuntu | Six-RID trimmed single-file CLI publish, portable archive, uploads folder + archive. |
| `publish-frontend` | Ubuntu | Six-RID trimmed single-file frontend publish, portable archive, uploads folder + archive. |

CI does not download BIOS ROMs or game images. GPU tests and real-audio tests are
opt-in and not part of the default matrix.

---

## 8. Build properties and dependencies

Key properties in `Directory.Build.props`:

| Property | Value |
|---|---|
| `TargetFramework` | `net10.0` |
| `LangVersion` | `latest` |
| `Nullable` | `enable` |
| `ImplicitUsings` | `disable` |
| `TreatWarningsAsErrors` | `true` |
| `Deterministic` | `true` |
| `Version` | `1.0.0-rc.1` |
| `InvariantGlobalization` | `true` |
| `EnableNETAnalyzers` | `true` |
| `AnalysisLevel` | `latest-recommended` |
| `FiveDoOpenAlSoftVersion` | `1.23.1` |

Central package versions live in `Directory.Packages.props`. The notable dependencies
are Avalonia 12.1.4, CommunityToolkit.Mvvm, Silk.NET.OpenGL/OpenAL, CHDSharp 1.5.2,
VideoGameFileSystemParser 1.4.0, xUnit, and BenchmarkDotNet (present but the current
benchmark harness is custom).

---

## 9. Build/test troubleshooting

| Symptom | Cause / fix |
|---|---|
| `dotnet` not found or wrong SDK | Install .NET 10 SDK 10.0.100+; `global.json` requires the 10.0 feature band. |
| `NETSDK1045` | The installed SDK is older than .NET 10. |
| Restore fails behind a proxy | Configure NuGet proxy/credentials; CI uses the standard NuGet feed. |
| `OpenAL32.dll` missing at runtime | Rebuild so `Directory.Build.targets` copies OpenAL Soft; or copy the package's `soft_oal.dll` as `OpenAL32.dll` beside the executable. |
| GPU tests fail on a headless runner | They are opt-in; do not set `FIVEDO_GPU_TESTS` without a desktop GL/ANGLE context. |
| Audio test fails with no device | Set `ALSOFT_DRIVERS=null` or skip `FIVEDO_AUDIO_TESTS`. |
| Frontend smoke test times out | It needs a desktop session; run it interactively or in a CI job with a window station. |
| Benchmark gate fails on a busy machine | Close background work; the gate is wall-clock and intended for a quiet CI runner. |
| `dotnet format` reports changes | Run `dotnet format 5DO.sln`, then rebuild/test. |
| Long-path errors on Windows | Enable long paths or clone to a shorter path. |
| CHD test fails | CHDSharp only reads valid MAME CD CHDs; DVD/HDD/raw CHDs are rejected by design. |

If a failure is emulation-related rather than build-related, see
[`Troubleshooting.md`](Troubleshooting.md) and [`Diagnostics.md`](Diagnostics.md).
