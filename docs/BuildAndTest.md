# Build, Test, and Publish

This document is the practical build/test/publish guide for the repository. For the
test design rationale, see [`TestStrategy.md`](TestStrategy.md). For trimming and
NativeAOT details, see [`Trimming.md`](Trimming.md). For packaging contents, see
[`packaging/README.md`](https://github.com/purelogiccode/5DO/blob/master/packaging/README.md).

---

## 1. Prerequisites

| Requirement | Version / notes |
|---|---|
| .NET SDK | **10.0.301** (pinned by `global.json` with `rollForward: disable`). |
| OS | Windows 10+ (validated), a current Linux desktop, or macOS 12+. Linux and macOS bundles are built and architecture-validated; runtime validation is post-1.0. |
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
  FiveDo.Video.Vulkan/       # optional Vulkan presenter and probes
  FiveDo.Video.Direct3D/     # optional Direct3D 11 presenter (Windows)
  FiveDo.Xbus/               # XBUS controller and devices
  FiveDo.Cdrom/              # MEI CD-ROM device
  FiveDo.Storage/            # ISO/BIN/CUE/CHD, BIOS/NVRAM, game database
  FiveDo.Audio/              # WAV/OpenAL/audio analysis/tap
  FiveDo.Input/              # controller state, PBUS, profiles, scripts
  FiveDo.Tas/                # TAS movie container, record/replay, branching
  FiveDo.Diagnostics/        # logging, tracing, PNG/frame dumps, benchmark gates
  FiveDo.Machine/            # composition root, frame loop, Quartz
  FiveDo.Host/               # core runner, pacing, settings, save slots, library, fixes
  FiveDo.Cli/                # headless commands
  FiveDo.Frontend.Avalonia/  # desktop shell and GPU probe
tests/FiveDo.Tests/          # xUnit tests, one folder per subsystem
benchmarks/FiveDo.Benchmarks # stopwatch frame benchmark and CI gate
packaging/                   # bundle/archive scripts and Linux desktop entry
tools/                       # release soak and HQx rule-table generation
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

The current suite is **1,815 tests** (verified 2026-11-06) and completes in a few seconds
on the development machine. The build treats warnings as errors and runs the .NET
analyzers plus Meziantou and Roslynator analyzers.

Debug builds compile the emulator core projects (`FiveDo.Core`, `Cpu`, `Memory`,
`Machine`, `Madam`, `Clio`, `Dsp`, `Sport`, `Video`, `Xbus`, `Cdrom`, and `Host`) with
JIT optimizations, while the frontend/UI projects stay unoptimized. This keeps IDE
gameplay near Release speed; pass `-p:FiveDoOptimizeDebug=false` to restore fully
unoptimized Debug core assemblies. If Visual Studio's debugger is set to suppress JIT
optimization on module load, gameplay will still be slow while attached; use Start
Without Debugging or disable that debugger option.

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

# Parser and CD command-stream fuzzing (deterministic)
dotnet run --project tools/FiveDo.Fuzz -c Release -- --seconds 30 --target all --json

# Reproduce one reported fuzz case
dotnet run --project tools/FiveDo.Fuzz -c Release -- --target state --seed 397 --iterations 43
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
| `Input/` | PBUS encoding/decoding, profiles, binding editor, scripts, gamepad mapper |
| `Tas/` | movie container, recorder/playback, branch/trim, verification, zero-allocation playback |
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
| `FIVEDO_GPU_TESTS=1` | Runs the real WGL + ANGLE probe integration test, including an HQx probe. Requires a desktop GL/ANGLE context and a built frontend. |
| `FIVEDO_VULKAN_TESTS=1` | Runs the in-process Vulkan parity/source-size, xBR/FSR/HQx orientation, and HQx CPU-expansion parity tests plus the Vulkan probe integration test. Requires a Vulkan device and a built frontend. |
| `FIVEDO_D3D_TESTS=1` | Runs the opt-in Direct3D 11 parity/source-size, letterbox, xBR/FSR/HQx orientation, and HQx CPU-expansion parity tests. Requires Windows and a Direct3D 11 device. |
| `FIVEDO_UPDATE_CHECK=0` | Disables the CLI/frontend startup GitHub release check (also set automatically in CI/tests). |
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

## 4a. Differential harness

The reference harness is a separate tool; it is not part of the shipped
emulator and is not run by the default test job when the BIOS/disc corpus is
absent.

```powershell
dotnet build tools/FiveDo.DiffHarness -c Release
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  validate benchmarks/diff/smoke.json --strict
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run benchmarks/diff/smoke.json --out artifacts/diff/smoke
```

It needs `bios/panafz1.bin`, the local disc collection, and the bundled
`References/CPP_SimpleGui/.../OperaCore.dll`. The Lemmings smoke is the quick
health check; the five-title showcase takes about ten minutes because the
reference core runs in real time. See
[`DifferentialHarness.md`](DifferentialHarness.md) for the manifest and report
contracts.

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
| `--gl-backend auto|wgl|angle|software|vulkan|d3d11` | `auto` | Renderer backend. `--gl-probe-backend` is an alias. Direct3D 11 is Windows-only. |

A 1:1 `accurate` capture at 320x240 triggers an automatic CPU/GPU parity comparison
against `FrameAssembler`; the report includes `Parity.Compared`, `Matches`,
`DifferentPixels`, `CpuHash`, `GpuHash`, and `VerticalFlip`.

### Vulkan probe

The optional Vulkan backend has a headless probe. It does not open an Avalonia window
and requires a Vulkan 1.0 device and loader.

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/4k-sharp.json `
  --vk-probe-size 3840x2160 --vk-probe-frames 30 --vk-probe-warmup 10 `
  --vk-probe-filter sharp-bilinear
```

| Option | Default | Meaning |
|---|---:|---|
| `--vk-probe <path>` | required | Output JSON path. |
| `--vk-probe-size WxH` | 1920x1080 | Output size, 16x16..16384x16384. |
| `--vk-probe-frames N` | 60 | Measured frames, 1..100000. |
| `--vk-probe-warmup N` | 10 | Unmeasured warm-up frames, 0..10000. |
| `--vk-probe-filter <name>` | `accurate` | `accurate`, `nearest`, `sharp-bilinear`, `crt`, `xbr`, `fsr`, `hqx`, `hqx3x`, `hqx4x`; HQx uses the GPU rule table. |
| `--vk-probe-capture <path>` | none | Write a PNG of the rendered frame. |
| `--vk-probe-timeout N` | 30 | Overall timeout in seconds, 1..600. |

Set `FIVEDO_VULKAN_TESTS=1` to run the opt-in in-process parity/source-size,
xBR/FSR/HQx orientation, and HQx CPU-expansion parity tests plus the probe integration
test. See [`Vulkan.md`](Vulkan.md)
for the measured results and limitations.

### Direct3D 11 probe

The optional Direct3D 11 backend has a headless probe and a native Win32 swapchain probe.
It requires Windows and Direct3D 11 feature level 11_0.

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --d3d-probe artifacts/d3d-probe/4k-sharp.json `
  --d3d-probe-size 3840x2160 --d3d-probe-frames 30 --d3d-probe-warmup 10 `
  --d3d-probe-filter sharp-bilinear
```

| Option | Default | Meaning |
|---|---:|---|
| `--d3d-probe <path>` | required | Output JSON path. |
| `--d3d-probe-size WxH` | 1920x1080 | Output size, 16x16..16384x16384. |
| `--d3d-probe-frames N` | 60 | Measured frames, 1..100000. |
| `--d3d-probe-warmup N` | 10 | Unmeasured warm-up frames, 0..10000. |
| `--d3d-probe-filter <name>` | `accurate` | Same filter names as the OpenGL/Vulkan probes. |
| `--d3d-probe-capture <path>` | none | Write a PNG of the rendered frame. |
| `--d3d-probe-timeout N` | 30 | Overall timeout in seconds, 1..600. |
| `--d3d-window-probe <path>` | - | Native Win32 flip-model swapchain probe; accepts the matching `--d3d-window-probe-*` options. |

Set `FIVEDO_D3D_TESTS=1` to run the opt-in parity, size-change, letterbox,
xBR/FSR/HQx orientation, and HQx CPU-expansion parity tests. The HLSL compile test runs
on Windows without an opt-in
flag. See [`Direct3D.md`](Direct3D.md) for measured results and limitations.

See [`Video.md`](Video.md) and [`Performance.md`](Performance.md) for interpretation.

---

## 6. Publish

### Framework-dependent single file (release shape)

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true -p:PublishTrimmed=false -m:1 `
  -o artifacts/cli/win-x64

dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true -p:PublishTrimmed=false -m:1 `
  -o artifacts/frontend/win-x64
```

Release bundles are produced for `win-x64`, `win-arm64`, `linux-x64`,
`linux-arm64`, `osx-x64`, and `osx-arm64`. They contain no .NET runtime; users must
install the .NET 10 runtime on their machine.

Both executables are single-file framework-dependent apphosts. The frontend also ships
native Skia/HarfBuzz/ANGLE assets. OpenAL Soft is copied as `OpenAL32.dll` on Windows
by `Directory.Build.targets`; Linux/macOS use the package's native names. PDB and XML
documentation files are removed by `packaging/package-bundle.ps1`.

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
CI), writes `FRONTEND SMOKE OK <version>` plus `windowState=<state>`, and exits 0.
Add `--fullscreen` to verify the launch mode (`windowState=FullScreen`).

### Portable bundles

```powershell
pwsh -File packaging/package-bundle.ps1 -Rid win-x64 `
  -CliPublishDirectory artifacts/cli/win-x64 `
  -FrontendPublishDirectory artifacts/frontend/win-x64 `
  -OutputDirectory artifacts/bundles
```

The bundle contains both framework-dependent apphosts and their native libraries,
`README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`,
`docs/Licensing.md`, `docs/ReleaseLegalReview.md`, the latest
`docs/ReleaseNotes-*.md`, and a generated `PACKAGE.txt` with component, RID, version,
commit, and build time. Linux bundles also include `5do.desktop`. PDB and XML
documentation files are removed, and no .NET runtime is copied into the bundle. The
archive name includes the product version, for example
`5do-1.4.0-win-x64.zip`, `5do-1.4.0-win-arm64.zip`, or `5do-1.4.0-linux-arm64.tar.gz`; the version is read from
`Directory.Build.props` unless `-Version` is supplied.

---

## 7. CI jobs

The workflow is [`.github/workflows/ci.yml`](https://github.com/purelogiccode/5DO/blob/master/.github/workflows/ci.yml).

| Job | Runner | What it does |
|---|---|---|
| `build-test` | Windows, Ubuntu, macOS | Restore, Release build, all tests. Ubuntu also runs format check, benchmark smoke + gate, allocation gate, and uploads the baseline. Windows runs trimmed CLI and frontend publish smoke tests. |
| `aot-cli` | Windows | NativeAOT win-x64 CLI publish, `--version`/`selftest`/`bench`, artifact upload. |
| `aot-frontend` | Windows | NativeAOT win-x64 frontend publish, smoke test, artifact upload. |
| `bundle` | Windows, Ubuntu | Builds the unified Windows/Linux/macOS x64 bundles with framework-dependent single-file apphosts; the .NET 10 runtime is required on the host and not bundled. The macOS x64 bundle is cross-published on Ubuntu and validated as Mach-O x86_64. Runs CLI/frontend smoke tests and uploads the bundle. |
| `ci-complete` | Ubuntu | Aggregate gate that fails if any required CI job fails or is cancelled. |

Separate workflows add:

| Workflow | Trigger | What it does |
|---|---|---|
| `docs.yml` | Push/PR touching docs or the wiki, manual | Strict MkDocs build, GitHub Pages deployment from `master`, and curated wiki publishing. |
| `release.yml` | Tag push or manual | Builds the six x64/arm64 bundles, writes `SHA256SUMS.txt`, updates the GitHub release, and removes obsolete per-component assets. |
| `codeql.yml` | Push/PR, weekly, manual | CodeQL analysis for C#, GitHub Actions, and Python. |
| `dependency-review.yml` | Pull request | Blocks high-severity dependency changes and comments on the PR. |
| `scorecard.yml` | Push, weekly, manual | OpenSSF Scorecard analysis, SARIF upload, and published results. |

CI does not download BIOS ROMs or game images. GPU tests and real-audio tests are
opt-in and not part of the default matrix. The repository is public; secret scanning
with push protection and private vulnerability reporting are enabled in GitHub
settings.

### 7.1 Local Linux VM validation

The local Linux validation target is the Hyper-V VM `LinuxMint`
(`D:\Hyper-V\VMs\LinuxMint`, generation 2, 8 GB assigned). It is a manual validation
target, not a CI replacement. The guest is reachable over SSH; machine-specific
credentials are in the gitignored `AGENTS.local.md`.

Suggested flow:

1. Start the VM (`Start-VM -Name LinuxMint`) and confirm the guest has the .NET 10 SDK,
   or publish `linux-x64` on the host and copy the publish folder into the guest.
2. Copy the repository or the published CLI/frontend into the guest.
3. Run `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`.
4. Run `dotnet run -c Release --project src/FiveDo.Cli -- selftest --json`.
5. Run the frontend smoke test (`--smoke-test`) and, when the VM has a usable GL
   display, the OpenGL probe (`--gl-probe`).
6. Run a reduced compatibility sweep with `-NoFrameCapture`; the PNG analysis uses
   `System.Drawing.Common`, which is Windows-oriented. Record the CLI JSON results.
7. Record the renderer/vendor/version and any fallback behavior in
   [`Compatibility.md`](Compatibility.md) and [`KnownIssues.md`](KnownIssues.md).

The Linux pass should use the same BIOS/disc set as the Windows pass. Do not commit
BIOS or disc images.

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
| `Version` | `1.4.0` |
| `InvariantGlobalization` | `true` |
| `EnableNETAnalyzers` | `true` |
| `AnalysisLevel` | `latest-recommended` |
| `FiveDoOpenAlSoftVersion` | `1.23.1` |

Central package versions live in `Directory.Packages.props`. The notable dependencies
are Avalonia 12.1.4, CommunityToolkit.Mvvm, Silk.NET.OpenGL/OpenAL, CHDSharp 1.6.0,
xUnit, and BenchmarkDotNet (present but the current benchmark harness is custom).
The 3DO Opera file-system reader used by `5do fs` is first-party
(`FiveDo.Storage.OperaFileSystem`).

---

## 9. Build/test troubleshooting

| Symptom | Cause / fix |
|---|---|
| `dotnet` not found or wrong SDK | Install .NET 10 SDK 10.0.301; `global.json` pins that version with `rollForward: disable`. |
| `NETSDK1045` | The installed SDK is older than .NET 10. |
| Restore fails behind a proxy | Configure NuGet proxy/credentials; CI uses the standard NuGet feed. |
| `OpenAL32.dll` missing at runtime | Rebuild so `Directory.Build.targets` copies OpenAL Soft; or copy the package's `soft_oal.dll` as `OpenAL32.dll` beside the executable. |
| GPU tests fail on a headless runner | They are opt-in; do not set `FIVEDO_GPU_TESTS` without a desktop GL/ANGLE context. |
| Audio test fails with no device | Set `ALSOFT_DRIVERS=null` or skip `FIVEDO_AUDIO_TESTS`. |
| Frontend smoke test times out | It needs a desktop session; run it interactively or in a CI job with a window station. |
| Benchmark gate fails on a busy machine | Close background work; the gate is wall-clock and intended for a quiet CI runner. |
| `dotnet format` reports changes | Run `dotnet format 5DO.sln`, then rebuild/test. |
| Long-path errors on Windows | Enable long paths or clone to a shorter path. |
| `TypeLoadException`/`MissingMethodException` after switching branches or pulling | Stale `bin`/`obj` output from an earlier build. Delete `bin`/`obj` and rebuild. |
| CHD test fails | CHDSharp reads valid MAME CD CHDs and flat 2048-byte cooked-ISO CHDs; DVD/HDD/raw CHDs with other unit sizes are rejected by design. |

If a failure is emulation-related rather than build-related, see
[`Troubleshooting.md`](Troubleshooting.md) and [`Diagnostics.md`](Diagnostics.md).
