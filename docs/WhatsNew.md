# What's new in 5DO 1.12.0

**Released:** 2026-11-17

5DO 1.12.0 is the **macOS GPU presentation** update. macOS arm64 now renders the 3DO
through Vulkan/MoltenVK on Metal instead of falling back to a blank OpenGL surface,
with all eight filters, a packaged DMG, and a full validation pass on a rented NoMac
host. The build also no longer requires the .NET 8 runtime.

## Highlights

### macOS arm64 renders at 60 fps

Validated on a NoMac Apple M5 Pro (Virtual), macOS 26.6.2, arm64:

- **Vulkan/MoltenVK presenter** - offscreen Vulkan renders into the shared bitmap
  presenter; the status bar reports `Vulkan (Metal)`.
- **60 fps gameplay** - Wolfenstein 3D ran at 59.8-60.1 fps in the packaged app.
- **All eight filters** - Accurate, Nearest, SharpBilinear, Bicubic, xBR, HQx, MMPX,
  and FSR 1.0 pass the Vulkan probe with no fallback.
- **2,394/2,394 tests** pass, plus CLI `selftest` and frontend `--smoke-test`.
- **DMG/PKG/tar.gz** - built natively; smoke tests pass from the tarball and mounted
  DMG; SHA-256 verifies.

### Self-contained bundles

Every bundle embeds the .NET 11 runtime in its single-file apphosts, so no separate
runtime install is required. The macOS arm64 bundle also includes the Vulkan loader and
MoltenVK, with no Homebrew dependency.

### macOS fixes

- `VK_KHR_portability_enumeration` is enabled for MoltenVK.
- Homebrew's `/opt/homebrew/lib/libvulkan.1.dylib` is loaded directly.
- The OpenGL probe fails immediately with an explanation instead of timing out.
- OpenGL/Vulkan/Direct3D selections use the Vulkan presenter on macOS, with a CPU
  fallback if MoltenVK is missing.
- The status bar shows `(GPU)` for the Vulkan readback path.

### Builds no longer need .NET 8

`XenoAtom.ShaderCompiler.Build` ships a net8.0 tool. The build now rolls it forward to
the installed .NET 9/10/11 runtime when .NET 8 is absent; when .NET 8 is present it is
still preferred. This fixes the `You must install or update .NET` build failure on
clean machines.

## Install

The bundles are **self-contained**: the .NET 11 runtime is embedded, so no separate
runtime install is required. The macOS arm64 bundle also includes the Vulkan loader and
MoltenVK. No system OpenAL install is required.

## Known limits

- Native Metal surface not implemented yet (`PLAT-14`).
- MoltenVK is bundled with the macOS arm64 DMG; no Homebrew install is required.
- macOS audio live-switch is fixed; all three audio tests pass.
- Retina/DPI and physical gamepads are not yet validated. Intel macOS support was
  dropped in this release; Apple Silicon is the only supported macOS target.
- The DMG is unsigned; Gatekeeper may require an explicit exception.

See the [1.12.0 release notes](ReleaseNotes-1.12.0.md), [compatibility evidence](Compatibility.md)
§10.4, and [known issues](KnownIssues.md).
