# 5DO 1.8.0

**Released:** 2026-11-11

5DO 1.8.0 adds MMPX 2x and Catmull-Rom bicubic filtering to every presenter, adds
5:4/16:9/16:10/21:9 aspect presets, and moves the build to .NET 11 / C# 15.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.8.0-win-x64.zip` |
| Windows arm64 | `5do-1.8.0-win-arm64.zip` |
| Linux x64 | `5do-1.8.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.8.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.8.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.8.0-osx-arm64.dmg` |

The apps are framework-dependent. Install the **.NET 11 Runtime** from
<https://dotnet.microsoft.com/download/dotnet/11.0> first; the bundles do not include
the runtime.

See the [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.8.0) for
`SHA256SUMS.txt`.

## Highlights

- **MMPX 2x** runs as a GPU rule pass in OpenGL/GLES, Direct3D 11, and Vulkan, with
  sharp-bilinear finishing and a CPU reference implementation.
- **Bicubic** adds Catmull-Rom 4x4 sampling in one pass on every presenter and in the
  CPU scaler.
- **Aspect presets** add 5:4, 16:9, 16:10, and 21:9 alongside 4:3, integer, stretch,
  and custom.
- **.NET 11 / C# 15** with SDK `11.0.100`, `net11.0` projects, and DependencyInjection
  11.0.0.

## Fixed

- The invalid `global.json` SDK pin (`11.0.0`) is now `11.0.100`.
- MMPX luma now uses the reference `r + g + b + 1` term, restoring the triangle-tip and
  luminance tie-break rules; a regression test covers it.
- OpenGL now uses linear filtering for the MMPX intermediate so sharp-bilinear
  finishing matches Direct3D 11 and Vulkan.

## Validation

- Full suite **2,158/2,158** passes in Debug with .NET SDK 11.0.100.
- Build, `dotnet format --verify-no-changes`, and
  `jb inspectcode --severity=WARNING` are clean.
- CI builds and tests on Windows, Linux, and macOS, runs trimmed and NativeAOT smoke,
  and publishes six framework-dependent bundles with no runtime, PDB, or XML files.
