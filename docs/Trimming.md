# Trimming and AOT Notes

This document records the trimming audit for the managed emulator. The core, host,
and CLI must remain fully managed C#; trimming is a packaging optimization, not an
emulation feature.

## Current status (2026-10-28)

See [`BuildAndTest.md`](BuildAndTest.md) for the build/publish commands and CI jobs.
The 2026-10-28 audio/FSR changes did not add reflection or trim-unsafe code.

| Component | Trimming | NativeAOT |
|---|---|---|
| `FiveDo.Cli` | **Enabled** (`PublishTrimmed=true`, `TrimMode=full`) | **Opt-in win-x64 audit passed** (`-p:FiveDoNativeAot=true`) |
| `FiveDo.Frontend.Avalonia` | **Enabled** (`PublishTrimmed=true`, `TrimMode=full`) | **Opt-in win-x64 audit passed** (`-p:FiveDoNativeAot=true`); Silk.NET loader warnings remain |
| Core/host assemblies | Trimmed as dependencies of both | NativeAOT-compatible as dependencies of the CLI/frontend |

Both executables publish as self-contained single-file builds for all six RIDs. A
trimmed win-x64 publish of each is smoke-tested on the Windows CI runner: the CLI
with `--version`, `selftest --json`, and `bench --frames 2 --json`, and the
frontend with `--smoke-test --smoke-output <path>` (exit code 0 plus a
`FRONTEND SMOKE OK` marker). CI also publishes NativeAOT win-x64 builds of both
executables and runs the same smoke commands.

## NativeAOT CLI audit (2026-10-27)

The CLI publishes with NativeAOT when `FiveDoNativeAot=true`:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:FiveDoNativeAot=true -p:PublishSingleFile=true -m:1 `
  -o artifacts/cli-aot
```

The win-x64 single-file executable is **10.54 MB** (versus the trimmed IL single
file) plus the bundled `OpenAL32.dll`. The audit covered:

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

The CLI AOT executable does not replace the default six-RID trimmed publishes:
NativeAOT cross-compilation is limited (for example, osx-arm64 AOT is not produced
from a Linux runner). The frontend has a separate opt-in AOT audit below. The
opt-in property keeps the default publish matrix unchanged.

## NativeAOT frontend audit (2026-10-26)

The frontend publishes with NativeAOT when `FiveDoNativeAot=true`:

```powershell
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:FiveDoNativeAot=true -m:1 `
  -o artifacts/frontend-aot
```

The publish produces a **28.8 MB** native executable plus the native
Skia/HarfBuzz/ANGLE assets in the same folder (Avalonia's native backends are not
embedded). The audit covered:

- `--version` and the real-window `--smoke-test --smoke-output` path (exit 0,
  `FRONTEND SMOKE OK`). The smoke also constructs the settings dialog so its XAML
  and the audio test-tone controls are validated.
- WGL and ANGLE `--gl-probe` 320x240 Accurate captures: `ok: true`, parity
  `matches: true`, `differentPixels: 0`, and the same CPU/GPU hash on both backends.
- A 1280x720 ANGLE sharp-bilinear pass with 0 renderer allocations/frame.

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
   locally; CI publishes all six RIDs trimmed.

On win-x64 the frontend single-file executable is **26.3 MB** trimmed versus
**91.3 MB** untrimmed. The complete publish folder is **150.9 MB** versus
**215.9 MB**, because the native Skia/HarfBuzz/ANGLE assets are unchanged.

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

## Remaining NativeAOT work

The CLI and the frontend single-file layout are AOT-validated on win-x64. Remaining:

- Silk.NET.Core's `Assembly.Location`/`DependencyContext` native-asset loader still
  emits `IL3000`/`IL3002`; both single-file AOT audio paths are validated (the
  frontend with OpenAL's null device). A Silk.NET-compatible resolver or a custom
  `DllImportResolver` is the cleanup.
- Cross-OS AOT publishing needs a native toolchain per target (the current six-RID
  matrix is produced on one Linux runner and uses trimmed IL).
- The frontend AOT smoke test covers startup and the WGL/ANGLE probes; a full in-app
  BIOS/disc/audio session under AOT is not yet validated.
- Installer packaging, signing, and notarization remain separate M10.7 work.
