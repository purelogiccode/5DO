# 5DO 1.8.0

**Released:** 2026-11-11

5DO 1.8.0 is a presentation and toolchain update. It adds MMPX 2x and Catmull-Rom
bicubic filtering to every presenter, adds 5:4/16:9/16:10/21:9 aspect presets, and
moves the repository to .NET 11 and C# 15.

## Presentation

### MMPX 2x

MMPX is Morgan McGuire and Mara Gagiu's style-preserving 2x pixel-art magnification
filter. It predicts four destination pixels from each source pixel using the 3x3
neighbourhood and the two-pixel diamond points, preserving the source palette and
single-pixel features.

- GPU rule pass in OpenGL/GLES, Direct3D 11, and Vulkan, followed by sharp-bilinear
  finishing.
- CPU reference implementation (`MmpxFilter`) used by the software presenter.
- Opt-in GPU parity tests compare the complete 2x expansion against the CPU oracle.
- Reference luminance tie-break and triangle-tip rules are active for opaque VDL
  frames. A regression test covers the triangle-tip pattern.

### Catmull-Rom bicubic

- 4x4 texel sampling with `a = -0.5`, clamped to the source edges.
- Single scale pass in OpenGL/GLES, Direct3D 11, and Vulkan; CPU implementation in
  `FrameScaler.ScaleBicubic`.
- No intermediate target and no palette re-expansion.
- Probe names: `bicubic` and `catmull-rom`.

### Aspect presets

Settings > Video > Aspect now exposes `Integer`, `FourThree`, `FiveFour`,
`SixteenNine`, `SixteenTen`, `TwentyOneNine`, `Stretch`, and `Custom`. Fixed ratios
letterbox or pillarbox without changing the emulated resolution. `IntegerScale`
continues to apply to `Integer` and 4:3; the wider presets fit their requested target
directly.

### Status bar

The renderer label now includes the resolved filter, for example `OpenGL 4.6 · MMPX`,
and refreshes when settings or per-game overrides change.

## Toolchain

- SDK pinned to **11.0.100** with `rollForward: latestFeature`.
- All projects target **`net11.0`**; the CLI and frontend use **C# 15**.
- Microsoft.Extensions DependencyInjection **11.0.0**.
- The .NET 11 SDK supplies the implicit `Microsoft.NET.ILLink.Tasks` **11.0.0**
  reference; the explicit central version was removed because CPM rejects versions for
  SDK-implicit packages.
- Meziantou.Analyzer **3.0.324**.
- Removed the obsolete .NET 10 ILCompiler pin; the .NET 11 SDK supplies ILCompiler
  11.0.0 for NativeAOT.

## Fixes

- `global.json` previously used `11.0.0`, which is not a valid SDK version. The pin is
  now `11.0.100`, and `dotnet --info` no longer reports the configuration as invalid.
- MMPX luma was zeroed in the CPU and shader implementations, which disabled the
  reference triangle-tip and luminance tie-break rules. It now uses the reference
  `r + g + b + 1` term for opaque frames.
- OpenGL sampled the MMPX intermediate with nearest filtering while the sharp-bilinear
  finish expected linear filtering. It now matches Direct3D 11 and Vulkan.

## Validation

- Full suite **2,158/2,158** passes in Debug with .NET SDK 11.0.100.
- `dotnet build`, `dotnet format --verify-no-changes`, and
  `jb inspectcode --severity=WARNING` are clean.
- CI covers Windows, Linux, and macOS; trimmed and NativeAOT CLI/frontend smoke; and
  six framework-dependent release bundles.
- Bundles contain `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and
  the release/legal docs, with no runtime, PDB, or XML files.
- The opt-in Direct3D 11, Vulkan, and OpenGL parity tests compare MMPX and bicubic
  output with the CPU oracle.

## Requirements

- The **.NET 11 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime.
- A legally obtained 1 MB 3DO BIOS ROM.
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.8.0-win-x64.zip` |
| Windows arm64 | `5do-1.8.0-win-arm64.zip` |
| Linux x64 | `5do-1.8.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.8.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.8.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.8.0-osx-arm64.dmg` |

See the [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.8.0) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- Physical macOS gamepad detection still needs a macOS host with a connected controller.
- Signing and notarization require a Windows certificate and an Apple developer account.
