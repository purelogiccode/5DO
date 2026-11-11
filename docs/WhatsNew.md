# What's new in 5DO 1.8.0

**Released:** 2026-11-11

5DO 1.8.0 is a **presentation and toolchain release**. It adds the MMPX 2x pixel-art
filter and a Catmull-Rom bicubic filter across the GPU and CPU presenters, adds wider
aspect-ratio presets, and moves the build to **.NET 11 / C# 15**.

## Highlights

### MMPX 2x

- Morgan McGuire and Mara Gagiu's **MMPX** filter runs as a 2x GPU rule pass in
  OpenGL/GLES, Direct3D 11, and Vulkan, followed by sharp-bilinear finishing.
- The CPU fallback runs the same rules through `MmpxFilter`, which is also the
  reference oracle for the opt-in GPU parity tests.
- MMPX preserves the original palette, curves, diagonals, sharp corners, and
  single-pixel features without introducing new colours.
- The reference luminance tie-break and triangle-tip rules are active for opaque VDL
  frames, including a new regression test for the triangle-tip case.

### Bicubic

- A **Catmull-Rom 4x4** filter is available in Settings > Video > Filter and in the
  GPU probes as `bicubic`/`catmull-rom`.
- It runs as a single scale pass with clamped edges in OpenGL/GLES, Direct3D 11, and
  Vulkan, and in the CPU `FrameScaler`; no intermediate target is allocated.
- It is a good match for 3DO 3D and full-motion content at fractional scales.

### Wider aspect presets

- Settings > Video > Aspect now offers **4:3, 5:4, 16:9, 16:10, and 21:9** alongside
  `Integer`, `Stretch`, and `Custom`.
- The presets letterbox or pillarbox the source without changing the emulated
  resolution. Wider presets fit the requested target directly; the integer-scale
  preference still applies to `Integer` and 4:3.

### .NET 11 and C# 15

- The SDK is pinned to **11.0.100** in `global.json` (the previous `11.0.0` value was
  not a valid SDK feature band).
- All projects target `net11.0`; the CLI and desktop app use **C# 15**.
- Microsoft.Extensions DependencyInjection moves to **11.0.0**. The SDK supplies the
  implicit `Microsoft.NET.ILLink.Tasks` 11.0.0 reference, and Meziantou.Analyzer moves
  to **3.0.324**.

## Fixed

- The invalid `global.json` SDK pin no longer leaves `dotnet --info` reporting the
  repository configuration as invalid.
- MMPX luma comparisons no longer collapse to zero, so the reference triangle-tip and
  luminance tie-break rules work as intended.
- OpenGL now samples the MMPX intermediate with linear filtering for sharp-bilinear
  finishing, matching Direct3D 11 and Vulkan.
- The status bar reports the active renderer and the resolved video filter together.

## Validation

- Full suite **2,158/2,158** passes in Debug with .NET SDK 11.0.100.
- `dotnet build`, `dotnet format --verify-no-changes`, and
  `jb inspectcode --severity=WARNING` are clean.
- CI builds and tests on Windows, Linux, and macOS, publishes the trimmed CLI/frontend,
  validates both NativeAOT apphosts, and produces the six release bundles.
- Six framework-dependent bundles contain the CLI, desktop app, README, license,
  notices, `whatsnew.md`, and release/legal docs, with no runtime, PDB, or XML files.

## Requirements

- The **.NET 11 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Optional: network access for the online cover catalog. Local covers and the core
  emulator work offline.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.8.0-win-x64.zip` |
| Windows arm64 | `5do-1.8.0-win-arm64.zip` |
| Linux x64 | `5do-1.8.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.8.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.8.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.8.0-osx-arm64.dmg` |

Each bundle contains the framework-dependent single-file CLI and desktop app, `README.md`,
`LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.8.0) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- Physical macOS gamepad detection still needs a macOS host with a connected controller.
- Signing and notarization require a Windows certificate and an Apple developer account.
