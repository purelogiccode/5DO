# Trimming and AOT Notes

This document records the trimming audit for the managed emulator. The core, host,
and CLI must remain fully managed C#; trimming is a packaging optimization, not an
emulation feature.

## Current status (2026-11-06)

**PERF-02/BAK-08 is complete.** The release bundles remain **framework-dependent and untrimmed**. Both apps publish as
single-file apphosts (`PublishSingleFile=true`, `PublishTrimmed=false`) and require the
.NET 10 runtime on the host. No runtime is embedded or copied into the bundle. This keeps
the CLI and desktop app as single executables and avoids the per-app trimming divergence
that would make a merged bundle unsafe.

Trimming and NativeAOT remain opt-in developer/audit paths. Both win-x64 audits now cover
startup, rendering, audio, and a real disc session:

| Component | Release bundle | Opt-in trimming / AOT |
|---|---|---|
| `FiveDo.Cli` | Single-file, untrimmed, system runtime | `PublishTrimmed=true`; NativeAOT win-x64 audit passed (`-p:FiveDoNativeAot=true`) |
| `FiveDo.Frontend.Avalonia` | Single-file, untrimmed, system runtime | `PublishTrimmed=true`; NativeAOT win-x64 audit passed (`-p:FiveDoNativeAot=true`); OpenAL and real-game session validated; Silk.NET/SharpGen loader warnings remain |
| Core/host assemblies | System runtime + single-file apphosts | Trimmed/AOT as dependencies of the opt-in builds |

The CI `build-test` job still exercises trimmed win-x64 CLI/frontend publishes and
NativeAOT win-x64 builds. The `bundle` job builds the actual release bundles with
`PublishTrimmed=false` and runs the CLI/frontend smoke tests.

### Measured results (win-x64, 2026-11-06)

| Artifact | Size | Startup / smoke |
|---|---:|---|
| CLI AOT single file | **16.6 MB** | `--version` 69 ms; `bench --frames 2` 65 ms |
| CLI AOT runtime folder (excluding PDB/XML) | 18.7 MB | — |
| CLI trimmed single file | **19.4 MB** | `selftest --json` passes |
| Frontend AOT single file | **34.5 MB** | `--version` 70 ms; `--smoke-test` 1.2 s |
| Frontend AOT runtime folder (excluding PDB/XML) | 54.6 MB | — |
| Frontend trimmed single file | **27.9 MB** | `--smoke-test` 3.2 s |
| Frontend trimmed runtime folder (excluding PDB/XML) | 48.0 MB | — |

The AOT frontend smoke test constructs the real `MainWindow` and the settings dialog,
creates the OpenGL 4.0 surface, and writes `FRONTEND SMOKE OK 1.3.0`. A real Lemmings
boot under the AOT frontend logged `OpenAL audio ready (44100 Hz stereo, 100 ms, sync
40 ms)`, `Emulation started`, and the VRR compositor path with no core fault. The AOT
CLI ran a realtime Lemmings session with `--audio openal --audio-sync 40` under
`ALSOFT_DRIVERS=null`: 0 dropped samples and 0 underruns over 240 frames. A 3,000-frame
AOT boot produced the same hashes and PC as the IL build.

The optional publish profiles are in each project's `Properties/PublishProfiles`:

- `NativeAot-win-x64.pubxml` and `Trimmed-win-x64.pubxml` for `FiveDo.Cli`
- `NativeAot-win-x64.pubxml` and `Trimmed-win-x64.pubxml` for `FiveDo.Frontend.Avalonia`

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -p:PublishProfile=NativeAot-win-x64
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj -p:PublishProfile=Trimmed-win-x64
```

### Latency snapshot format

PERF-02 also covers the state cost used by VID-12. The machine frame state section is now
version 2 and writes only the visible source lines, using little-endian pixel blocks; the
RAM loader clears only the previously addressable range instead of the whole 22 MB backing
store. On this machine `bench --latency` reports a 9,348,438-byte uncompressed snapshot,
capture ~3.6 ms/frame, and restore ~2.0 ms/frame in Release. That keeps one-frame run-ahead
inside the 60 Hz budget on a typical title. Old version-1 machine sections still load.

## NativeAOT CLI audit (2026-10-27)

The CLI publishes with NativeAOT when `FiveDoNativeAot=true`:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:FiveDoNativeAot=true -p:PublishSingleFile=true -m:1 `
  -o artifacts/cli-aot
```

The win-x64 single-file executable is **16.6 MB** (versus the 19.4 MB trimmed IL
single file) plus the bundled `OpenAL32.dll`. The audit covered:

- `--version`, `selftest --json`, and `bench --frames 2 --json`.
- `info` and `fs list` on the Lemmings ISO/CHD pair (482 entries each).
- A 3,000-frame Lemmings boot; the audio-clock keys are in
  [`TestCorpus.md`](TestCorpus.md) 1.1.
- **Live OpenAL audio** with `--audio openal` (validated with the OpenAL null
  backend because the test machine has no playback endpoint at audit time). The
  linux-x64 trimmed CLI was also run under WSL with the published `libopenal.so`
  and the null backend.
- Silk.NET.Core loader warnings (`IL3000`/`IL3002`) are visible but non-fatal; the
  loader falls back to `AppContext.BaseDirectory`, where `OpenAL32.dll` is copied.
  CHDSharp's vendored-FLAC `TypeDescriptor` helper (`IL2104`) remains outside the
  3DO data-track path.

The CLI AOT executable does not replace the release bundles (untrimmed single-file
apphosts with the system runtime): NativeAOT cross-compilation is limited (for example,
osx-arm64 AOT is not produced from a Linux runner). The frontend has a separate opt-in
AOT audit below.

## NativeAOT frontend audit (2026-10-26)

The frontend publishes with NativeAOT when `FiveDoNativeAot=true`:

```powershell
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:FiveDoNativeAot=true -m:1 `
  -o artifacts/frontend-aot
```

The publish produces a **34.5 MB** native executable plus the native
Skia/HarfBuzz/ANGLE assets in the same folder (Avalonia's native backends are not
embedded). The audit covered:

- `--version` and the real-window `--smoke-test --smoke-output` path (exit 0,
  `FRONTEND SMOKE OK`). The smoke also constructs the settings dialog so its XAML
  and the audio test-tone controls are validated.
- WGL and ANGLE `--gl-probe` 320x240 Accurate captures: `ok: true`, parity
  `matches: true`, `differentPixels: 0`, and the same CPU/GPU hash on both backends.
- A 1280x720 ANGLE sharp-bilinear pass with 0 renderer allocations/frame.
- A real Lemmings (USA) boot with run-ahead 1, frame delay 1, VRR, and audio sync 40 ms.
  OpenAL initialized at 44.1 kHz through the bundled runtime with `ALSOFT_DRIVERS=null`,
  the emulation thread started, and no core fault or run-ahead auto-disable was logged.
- The headless `--audio-soak` path ran 600 s on the null device and on
  `OpenAL Soft on PG34WQ15R2B (NVIDIA High Definition Audio)`: 35,968/35,966 frames,
  **0 dropped samples**, **0 underruns**, 0 OpenAL errors, DSP running. Later repeats
  hit HDMI-endpoint power/session instability (1-2 transient underruns, then no
  endpoint); sample drops stayed 0. See [`Audio.md`](Audio.md) §9.

Silk.NET.Core's native-asset loader (`DefaultPathResolver`) still uses
`Assembly.Location`/`Assembly.CodeBase` and `DependencyContext.Default`, so the ILC
analyzer reports `IL3000`/`IL3002` for single-file. The opt-in frontend AOT build
keeps `PublishSingleFile=true`, demotes those ILC warnings to visible warnings, and
the resulting app starts and renders. The loader warnings primarily affect optional
native audio discovery; both the **CLI** and **frontend** single-file AOT paths now
open the bundled OpenAL runtime. The frontend was validated on 2026-10-29 with
`ALSOFT_DRIVERS=null`: the log reports `OpenAL audio ready (44100 Hz stereo)` and the
smoke test exits 0. A physical-device listen remains a manual check. The default
six-RID publishes remain trimmed IL. A future cleanup should replace the Silk.NET
loader dependency or install a custom native-asset resolver for the frontend.

## What was fixed

1. **Reflection-based JSON.** Settings, input profiles, save-slot sidecars, and
   every CLI `--json` response now serialize through source-generated
   `JsonSerializerContext` metadata:
   - `AppSettingsJsonContext` (`FiveDo.Host`)
   - `SaveStateSlotJsonContext` (`FiveDo.Host`)
   - `InputProfileJsonContext` (`FiveDo.Input`)
   - `CliJsonContext` (`FiveDo.Cli`)
   - `GlProbeJsonContext` (`FiveDo.Frontend.Avalonia`, the developer GPU probe)

   The contexts use camel-case naming where the historical CLI contract requires
   it and `UseStringEnumConverter` for the settings/profile files. The CLI's
   anonymous response objects were replaced with named records in
   `src/FiveDo.Cli/CliJsonModels.cs`.

2. **Reflection over CLI result objects.** `WriteRunResult` and `selftest` no
   longer use `Type.GetProperty` to read anonymous-type properties; they use the
   strongly typed result records.

3. **Stack-trace reflection.** The RAM-write watchpoint formatter uses
   `DiagnosticMethodInfo.Create(StackFrame)` instead of the trim-unsafe
   `StackFrame.GetMethod()`.

4. **Frontend view construction.** `ViewLocator` (`Type.GetType` plus
   `Activator.CreateInstance`) was removed, and its `Application.DataTemplates`
   registration was deleted. Every window is constructed explicitly (`MainWindow`,
   `WelcomeWindow`, `SettingsWindow`, `GameLibraryWindow`, `SaveSlotsWindow`,
   `InputBindingsWindow`, `LogsWindow`). All XAML bindings are compiled:
   `AvaloniaUseCompiledBindingsByDefault=true`, and each window/`DataTemplate`
   declares `x:DataType`. The XAML compiler emits the loader IL at build time, so
   the trimmed app does not depend on reflection-based XAML lookup.

5. **Frontend smoke test.** `--smoke-test --smoke-output <path>` opens the real
   `MainWindow`, constructs the settings dialog, runs one UI pass, writes a version
   marker, and exits with code 0.
   The GL surface is hidden during the smoke so headless CI does not need a desktop
   OpenGL context. The smoke skips the welcome dialog and the configured BIOS/disc
   flow so CI does not need a BIOS, disc, or user profile. `--version` exits before
   Avalonia initializes. The CI Windows job publishes the trimmed win-x64 frontend
   and runs this smoke test. The trimmed win-x64 executable also runs the
   `--gl-probe` 4K offscreen path (source-generated `GlProbeJsonContext`) and
   reports 0 renderer/mailbox allocations per frame after warm-up. The trimmed
   win-x64 build was also re-checked with the `--vk-probe` xBR, FSR, and HQx paths
   after the twenty-first-pass Vulkan filters; all returned `ok: true` with
   `filterFallback: false`.

6. **CHDSharp warning.** The only remaining trim warning is in CHDSharp's
   vendored FLAC assembly:

   ```
   VendoredFlac.Interfaces.FlacDeps.AudioDecoderSettingsExtensions.Init
   uses System.ComponentModel.TypeDescriptor.GetProperties
   ```

   The 3DO path reads the first data track; it does not decode CD-DA/FLAC audio.
   Both executable projects demote `IL2026`/`IL2104` to warnings only when
   `PublishTrimmed` is true, so the warning remains visible in the publish log
   instead of hiding all trim analysis. If CHDSharp fixes the helper upstream,
   remove the demotion.

7. **Cross-RID publishes.** All six trimmed frontend RIDs (win-x64, win-arm64,
   linux-x64, linux-arm64, osx-x64, osx-arm64) were published and inspected
   locally during the trim audit. CI now builds the three untrimmed x64 release
   bundles and still exercises trimmed win-x64 publishes in `build-test`.

On win-x64 the frontend single-file executable is **27.9 MB** trimmed versus
the larger untrimmed single-file layout. The complete runtime publish folder is
**48.0 MB** excluding PDB/XML, because the native Skia/HarfBuzz/ANGLE assets are
unchanged.

## Verification

```powershell
# Build, tests, and formatting
dotnet build 5DO.sln -c Release
dotnet test tests/FiveDo.Tests -c Release
dotnet format 5DO.sln --verify-no-changes

# Trimmed CLI publish (Windows)
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -m:1 `
  -o artifacts/cli-trim

# Smoke tests against the trimmed CLI
artifacts/cli-trim/FiveDo.Cli.exe --version
artifacts/cli-trim/FiveDo.Cli.exe selftest --json
artifacts/cli-trim/FiveDo.Cli.exe bench --frames 2 --json
artifacts/cli-trim/FiveDo.Cli.exe info game.chd --json
artifacts/cli-trim/FiveDo.Cli.exe run game.iso --bios panafz1.bin --frames 120 --json

# Opt-in NativeAOT CLI publish and smoke test (win-x64)
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:FiveDoNativeAot=true -p:PublishSingleFile=true -m:1 `
  -o artifacts/cli-aot
artifacts/cli-aot/FiveDo.Cli.exe selftest --json
artifacts/cli-aot/FiveDo.Cli.exe bench --frames 2 --json
artifacts/cli-aot/FiveDo.Cli.exe bench --frames 120 --latency
$env:ALSOFT_DRIVERS = "null"
artifacts/cli-aot/FiveDo.Cli.exe run game.chd --bios bios/panafz10.bin `
  --frames 240 --audio openal --audio-sync 40 --realtime --json

# Opt-in NativeAOT frontend single-file publish and smoke test (win-x64)
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:FiveDoNativeAot=true -m:1 `
  -o artifacts/frontend-aot
artifacts/frontend-aot/FiveDo.Frontend.Avalonia.exe --version
# --smoke-test --smoke-output <path> opens the real window and exits 0

# Trimmed frontend publish (Windows)
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -m:1 `
  -o artifacts/frontend-trim

# Frontend smoke test: opens the real window, runs one UI pass (GL hidden), exits 0
$process = Start-Process `
  -FilePath artifacts/frontend-trim/FiveDo.Frontend.Avalonia.exe `
  -ArgumentList '--smoke-test', '--smoke-output', 'artifacts/frontend-smoke.txt' `
  -PassThru
$process.WaitForExit(60000)
$process.ExitCode
Get-Content artifacts/frontend-smoke.txt
```

The CLI audit also covered `state inspect`, `state slots`, `fs list` on the
Lemmings ISO/CHD pair, a CHD boot/run, and a save-state roundtrip through the trimmed
executable. The trimmed win-x64 CLI reproduces the current 3,000-frame golden
results for Lemmings (`PC 0x0012B36C`, hash `0x6CCA83F5`) and Alone in the Dark
(`PC 0x000C4C94`, hash `0x4EC2E1A7`) from `artifacts/regression/`; the frame
hashes, PCs, cycle counts, disc IDs, and fix modes match. These keys postdate the
exact DSP audio clock but predate the 2026-10-30 headless PBUS fix; see
[`TestCorpus.md`](TestCorpus.md) 1.1 for the current keys.

`-m:1` avoids a .NET SDK 10.0.301 `GenerateDepsFile` race seen when several
project references generate dependency files in parallel during a trimmed
publish. The CI publish steps use the same switch.

## Managed IL JIT and NativeAOT

The optional PERF-08 JIT uses `DynamicMethod`/`System.Reflection.Emit`. NativeAOT
runtimes do not support dynamic code, so `RuntimeFeature.IsDynamicCodeSupported` is
false and `--cpu-backend jit` falls back to the interpreter without affecting the
default path. The AOT smoke tests use the interpreter.

## Remaining NativeAOT work

The CLI and the frontend single-file layout are AOT-validated on win-x64. Remaining:

- Silk.NET.Core's `Assembly.Location`/`DependencyContext` native-asset loader still
  emits `IL3000`/`IL3002`; both single-file AOT audio paths are validated (the
  frontend with OpenAL's null device). A Silk.NET-compatible resolver or a custom
  `DllImportResolver` is the cleanup. SharpGen and CHDSharp also emit non-fatal
  `IL2104` trim warnings.
- Cross-OS AOT publishing needs a native toolchain per target; the release bundles
  are produced on native runners and do not use AOT.
- The frontend AOT audio path is validated for 600 s on the null device and a real
  Windows playback endpoint. Linux/macOS AOT audio remains a separate platform check.
- Unsigned installer/package scaffolding is in `packaging/`; signing and notarization
  remain separate M10.7 work.
