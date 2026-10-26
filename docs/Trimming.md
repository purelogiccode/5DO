# Trimming and AOT Notes

This document records the trimming audit for the managed emulator. The core, host,
and CLI must remain fully managed C#; trimming is a packaging optimization, not an
emulation feature.

## Current status (2026-10-26)

| Component | Trimming | NativeAOT |
|---|---|---|
| `ThreeDo.Cli` | **Enabled** (`PublishTrimmed=true`, `TrimMode=full`) | Not attempted |
| `ThreeDo.Frontend.Avalonia` | Disabled | Not attempted |
| Core/host assemblies | Trimmed as dependencies of the CLI | Not attempted |

The CLI is published as a self-contained single-file executable for all six RIDs.
A trimmed win-x64 publish is smoke-tested on the Windows CI runner with
`--version`, `selftest --json`, and `bench --frames 2 --json`.

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

4. **CHDSharp warning.** The only remaining trim warning is in CHDSharp's
   vendored FLAC assembly:

   ```
   VendoredFlac.Interfaces.FlacDeps.AudioDecoderSettingsExtensions.Init
   uses System.ComponentModel.TypeDescriptor.GetProperties
   ```

   The 3DO path reads the first data track; it does not decode CD-DA/FLAC audio.
   The CLI project demotes `IL2026`/`IL2104` to warnings only when
   `PublishTrimmed` is true, so the warning remains visible in the publish log
   instead of hiding all trim analysis. If CHDSharp fixes the helper upstream,
   remove the demotion.

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

# Smoke tests against the trimmed executable
artifacts/cli-trim/ThreeDo.Cli.exe --version
artifacts/cli-trim/ThreeDo.Cli.exe selftest --json
artifacts/cli-trim/ThreeDo.Cli.exe bench --frames 2 --json
artifacts/cli-trim/ThreeDo.Cli.exe info game.chd --json
artifacts/cli-trim/ThreeDo.Cli.exe run game.iso --bios panafz1.bin --frames 120 --json
```

The local audit also covered `state inspect`, `state slots`, `fs list` on a
generated CHD, a CHD boot/run, and a save-state roundtrip through the trimmed
executable. The trimmed win-x64 CLI reproduces the 3,000-frame golden results for
Lemmings (`PC 0x0012B3C8`, hash `0x932C7722`) and Alone in the Dark
(`PC 0x000C4CB4`, hash `0xF4BDF79B`) from `artifacts/regression/`; the frame
hashes, PCs, cycle counts, disc IDs, and fix modes match.

`-m:1` avoids a .NET SDK 10.0.301 `GenerateDepsFile` race seen when several
project references generate dependency files in parallel during a trimmed
publish. The CI publish step uses the same switch.

## Frontend blockers

The Avalonia frontend is still published untrimmed. Known work before enabling
trimming there:

- Avalonia's XAML loader and compiled-binding paths need a trim audit.
- `ViewLocator` uses `Type.GetType` and `Activator.CreateInstance`; either remove
  it or replace it with explicit view construction.
- The desktop gamepad/audio backends and Silk.NET bindings need a trim/AOT
  audit; the CLI does not reference the frontend or the OpenGL presenter.
- NativeAOT requires an additional Avalonia and Silk.NET investigation; it is
  not on the v1 critical path.

The CLI publish is the first step: it proves the emulation core, storage layer,
CHD reader, and JSON stores are trim-safe before the UI stack is considered.
