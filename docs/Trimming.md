# Trimming and AOT Notes

This document records the trimming audit for the managed emulator. The core, host,
and CLI must remain fully managed C#; trimming is a packaging optimization, not an
emulation feature.

## Current status (2026-10-26, fifteenth pass)

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

## NativeAOT CLI audit (2026-10-26)

The CLI publishes with NativeAOT when `FiveDoNativeAot=true`:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:FiveDoNativeAot=true -p:PublishSingleFile=true -m:1 `
  -o artifacts/cli-aot
```

The win-x64 single-file executable is **9.97 MB** (versus 26.3 MB for the trimmed
IL single file). The audit covered:

- `--version`, `selftest --json`, and `bench --frames 2 --json`.
- `info` and `fs list` on `Lemmings (USA).chd` (482 files).
- A 3,000-frame Lemmings boot reproducing the golden `PC 0x0012B3C8` /
  hash `0x932C7722`.
- The only remaining warning is CHDSharp's vendored-FLAC `TypeDescriptor` helper
  (`IL2104`), outside the 3DO data-track path.

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
  `FRONTEND SMOKE OK`).
- WGL and ANGLE `--gl-probe` 320x240 Accurate captures: `ok: true`, parity
  `matches: true`, `differentPixels: 0`, and the same CPU/GPU hash on both backends.
- A 1280x720 ANGLE sharp-bilinear pass with 0 renderer allocations/frame.

Silk.NET.Core's native-asset loader (`DefaultPathResolver`) still uses
`Assembly.Location`/`Assembly.CodeBase` and `DependencyContext.Default`, so the ILC
analyzer reports `IL3000`/`IL3002` for single-file. The opt-in frontend AOT build
keeps `PublishSingleFile=true`, demotes those ILC warnings to visible warnings, and
the resulting app starts and renders. The loader warnings primarily affect optional
native audio discovery; **audio under single-file AOT is not yet validated**, and the
default six-RID publishes remain trimmed IL. A future cleanup should replace the
Silk.NET loader dependency or install a custom native-asset resolver.

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
   `MainWindow`, runs one UI pass, writes a version marker, and exits with code 0.
   The GL surface is hidden during the smoke so headless CI does not need a desktop
   OpenGL context. The smoke skips the welcome dialog and the configured BIOS/disc
   flow so CI does not need a BIOS, disc, or user profile. `--version` exits before
   Avalonia initializes. The CI Windows job publishes the trimmed win-x64 frontend
   and runs this smoke test. The trimmed win-x64 executable also runs the
   `--gl-probe` 4K offscreen path (source-generated `GlProbeJsonContext`) and
   reports 0 renderer/mailbox allocations per frame after warm-up.

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

The CLI audit also covered `state inspect`, `state slots`, `fs list` on a
generated CHD, a CHD boot/run, and a save-state roundtrip through the trimmed
executable. The trimmed win-x64 CLI reproduces the 3,000-frame golden results for
Lemmings (`PC 0x0012B3C8`, hash `0x932C7722`) and Alone in the Dark
(`PC 0x000C4CB4`, hash `0xF4BDF79B`) from `artifacts/regression/`; the frame
hashes, PCs, cycle counts, disc IDs, and fix modes match.

`-m:1` avoids a .NET SDK 10.0.301 `GenerateDepsFile` race seen when several
project references generate dependency files in parallel during a trimmed
publish. The CI publish steps use the same switch.

## Remaining NativeAOT work

The CLI and the frontend single-file layout are AOT-validated on win-x64. Remaining:

- Silk.NET.Core's `Assembly.Location`/`DependencyContext` native-asset loader still
  emits `IL3000`/`IL3002`; audio under single-file AOT is not yet validated. A
  Silk.NET-compatible resolver or a custom `DllImportResolver` is the cleanup.
- Cross-OS AOT publishing needs a native toolchain per target (the current six-RID
  matrix is produced on one Linux runner and uses trimmed IL).
- The frontend AOT smoke test covers startup and the WGL/ANGLE probes; a full in-app
  BIOS/disc/audio session under AOT is not yet validated.
- Installer packaging, signing, and notarization remain separate M10.7 work.
