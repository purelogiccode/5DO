# Trimming and AOT Notes

This document records the trimming audit for the managed emulator. The core, host,
and CLI must remain fully managed C#; trimming is a packaging optimization, not an
emulation feature.

## Current status (2026-10-26, thirteenth pass)

| Component | Trimming | NativeAOT |
|---|---|---|
| `ThreeDo.Cli` | **Enabled** (`PublishTrimmed=true`, `TrimMode=full`) | Not attempted |
| `ThreeDo.Frontend.Avalonia` | **Enabled** (`PublishTrimmed=true`, `TrimMode=full`) | Not attempted |
| Core/host assemblies | Trimmed as dependencies of both | Not attempted |

Both executables publish as self-contained single-file builds for all six RIDs. A
trimmed win-x64 publish of each is smoke-tested on the Windows CI runner: the CLI
with `--version`, `selftest --json`, and `bench --frames 2 --json`, and the
frontend with `--smoke-test --smoke-output <path>` (exit code 0 plus a
`FRONTEND SMOKE OK` marker).

## What was fixed

1. **Reflection-based JSON.** Settings, input profiles, save-slot sidecars, and
   every CLI `--json` response now serialize through source-generated
   `JsonSerializerContext` metadata:
   - `AppSettingsJsonContext` (`ThreeDo.Host`)
   - `SaveStateSlotJsonContext` (`ThreeDo.Host`)
   - `InputProfileJsonContext` (`ThreeDo.Input`)
   - `CliJsonContext` (`ThreeDo.Cli`)

   The contexts use camel-case naming where the historical CLI contract requires
   it and `UseStringEnumConverter` for the settings/profile files. The CLI's
   anonymous response objects were replaced with named records in
   `src/ThreeDo.Cli/CliJsonModels.cs`.

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
   and runs this smoke test.

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
dotnet build 3DOEmulator.sln -c Release
dotnet test tests/ThreeDo.Tests -c Release
dotnet format 3DOEmulator.sln --verify-no-changes

# Trimmed CLI publish (Windows)
dotnet publish src/ThreeDo.Cli/ThreeDo.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -m:1 `
  -o artifacts/cli-trim

# Smoke tests against the trimmed CLI
artifacts/cli-trim/ThreeDo.Cli.exe --version
artifacts/cli-trim/ThreeDo.Cli.exe selftest --json
artifacts/cli-trim/ThreeDo.Cli.exe bench --frames 2 --json
artifacts/cli-trim/ThreeDo.Cli.exe info game.chd --json
artifacts/cli-trim/ThreeDo.Cli.exe run game.iso --bios panafz1.bin --frames 120 --json

# Trimmed frontend publish (Windows)
dotnet publish src/ThreeDo.Frontend.Avalonia/ThreeDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -m:1 `
  -o artifacts/frontend-trim

# Frontend smoke test: opens the real window, runs one UI pass (GL hidden), exits 0
$process = Start-Process `
  -FilePath artifacts/frontend-trim/ThreeDo.Frontend.Avalonia.exe `
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

NativeAOT is still not attempted for either executable and is not on the v1
critical path. Before enabling it:

- Avalonia's XAML/compiled-binding path and its native platform backends need an
  AOT audit (the desktop frontend is the only Avalonia consumer).
- Silk.NET OpenGL/OpenAL and the Windows XInput/Linux evdev gamepad backends need
  source-generated or function-pointer interop validation.
- The emulation core itself has no reflection or dynamic code; the CLI's trimmed
  publish already proves the core, storage, CHD, and JSON paths are AOT-friendly.

Installer packaging, signing, and notarization remain separate M10.7 work.
