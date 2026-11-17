# 5DO 1.12.0

**Released:** 2026-11-17

5DO 1.12.0 adds **macOS arm64 GPU presentation**. macOS now renders through
Vulkan/MoltenVK on Metal instead of falling back to a blank OpenGL surface, with all
eight GPU filters and a packaged DMG smoke-tested on Apple hardware. The build also no
longer requires the .NET 8 runtime.

## macOS arm64

Validated on a rented NoMac Apple M5 Pro (Virtual), macOS 26.6.2, arm64:

- **Vulkan/MoltenVK presenter** - the shell maps OpenGL/Vulkan/Direct3D selections to
  the offscreen Vulkan renderer and copies its readback into the bitmap presenter.
- **60 fps gameplay** - Wolfenstein 3D ran at 59.8-60.1 fps in the packaged app.
- **All eight filters** - Accurate, Nearest, SharpBilinear, Bicubic, xBR, HQx, MMPX,
  and FSR pass without fallback.
- **2,394/2,394 tests** pass, plus CLI `selftest` and frontend `--smoke-test`.
- **DMG/PKG/tar.gz** built natively; CLI and frontend smoke pass from the tarball and
  the mounted DMG; SHA-256 verifies.

## Self-contained bundles

Every bundle embeds the .NET 11 runtime in its single-file apphosts, so no separate
runtime install is required. The macOS arm64 bundle also includes the Vulkan loader and
MoltenVK, with no Homebrew dependency.

## Fixes

- Enabled `VK_KHR_portability_enumeration` for MoltenVK.
- Prefer the bundled Vulkan loader and MoltenVK; Homebrew is a development fallback.
- Fail the OpenGL probe immediately on macOS instead of timing out.
- Stop selecting Avalonia's unusable OpenGL surface on macOS; use Vulkan/MoltenVK with
  a CPU fallback.
- Show `(GPU)` instead of `(CPU)` for the Vulkan readback path.
- Roll the shader compiler forward so only the .NET 11 SDK is required to build.

## Install

The bundles are **self-contained**: the .NET 11 runtime is embedded, so no separate
runtime install is required. The macOS arm64 bundle includes the Vulkan loader and
MoltenVK. No system OpenAL install is required.

## Known limits

- Native Metal surface not implemented yet (`PLAT-14`).
- MoltenVK is bundled with the macOS arm64 DMG; no Homebrew install is required.
- macOS audio live-switch is fixed; all three audio tests pass.
- Retina/DPI and physical gamepads are not yet validated. Intel macOS support was
  dropped in this release; Apple Silicon is the only supported macOS target.
- The DMG is unsigned; Gatekeeper may require an explicit exception.
