# 5DO 1.12.0

**Released:** 2026-11-17

5DO 1.12.0 is the **macOS GPU presentation** release. macOS arm64 now renders the
3DO through Vulkan/MoltenVK instead of falling back to a blank OpenGL surface, with
the Metal-backed offscreen renderer, all eight GPU filters, and a packaged DMG that
was smoke-tested on real Apple hardware. The release also removes the external .NET 8
build requirement and records the first full macOS validation evidence.

## Highlights

### macOS arm64 GPU presentation

Validated on a rented NoMac host: Apple M5 Pro (Virtual), macOS 26.6.2, arm64, Apple
Paravirtual GPU with unified memory and Metal.

- **Vulkan/MoltenVK presenter:** the desktop shell maps OpenGL, Vulkan, and Direct3D
  selections to an offscreen `VulkanOffscreenRenderer` and copies its readback into the
  existing `WriteableBitmap` presenter. The status bar reports `Vulkan (Metal)`.
- **60 fps gameplay:** A 320x240 Wolfenstein 3D session ran at 59.8-60.1 fps after the
  loading screen, and the packaged `.app` reproduced it.
- **All GPU filters:** Accurate, Nearest, SharpBilinear, Bicubic, xBR, HQx, MMPX, and
  FSR 1.0 pass the Vulkan probe with `filterFallback: false` (529-975 fps at 640x480).
- **Full suite:** **2,394/2,394 tests** pass on macOS arm64, plus CLI `selftest` and the
  frontend `--smoke-test`.
- **Real game boot:** 600 frames of Wolfenstein 3D in 0.888 s (~676 fps) with a
  non-black final frame written to PNG.
- **Packaging:** `5do-1.12.0-osx-arm64.dmg`, `.pkg`, and `.tar.gz` are built natively;
  CLI and frontend smoke tests pass from both the extracted tarball and the mounted DMG;
  SHA-256 checksums verify.

### Self-contained bundles

Every release bundle now embeds the .NET 11 runtime in its single-file apphosts. A
download runs without installing the runtime separately; the bundle still contains no
`runtime/` folder because the runtime is inside each executable. The macOS arm64 bundle
also includes `libvulkan.1.dylib`, `libMoltenVK.dylib`, `MoltenVK_icd.json`, and both
Apache-2.0 license texts, so no Homebrew install is required for GPU rendering.

### macOS fixes

- **Vulkan instances now enable `VK_KHR_portability_enumeration`.** MoltenVK is a
  portability driver; without the instance flag `vkCreateInstance` returned
  `ErrorIncompatibleDriver`.
- **The bundled Vulkan loader is preferred.** The macOS arm64 bundle ships
  `libvulkan.1.dylib`, `libMoltenVK.dylib`, and `MoltenVK_icd.json`; Homebrew is only a
  development fallback. `DYLD_LIBRARY_PATH` is not required.
- **The OpenGL probe fails immediately on macOS.** Avalonia's macOS backend has no
  `OpenGlControlBase`, so the old probe waited for its timeout. It now reports the
  limitation and points at `--vk-probe`.
- **The shell no longer selects an unusable OpenGL surface on macOS.** OpenGL, Vulkan,
  and Direct3D selections normalize to the offscreen Vulkan presenter; if MoltenVK is
  unavailable the CPU presenter is used.
- **The UI label distinguishes GPU and CPU filtering.** The status bar now shows
  `(GPU)` for the Vulkan readback path instead of `(CPU)`.

### Builds no longer require the .NET 8 runtime

`XenoAtom.ShaderCompiler.Build` ships a net8.0 `dotnet-shaderc` tool. The build now
sets `DOTNET_ROLL_FORWARD=Major` for the shader target, so a machine with only the
.NET 9/10/11 runtime builds the Vulkan shaders; when .NET 8 is installed it is still
preferred. This removes the `You must install or update .NET` failure on clean
development machines.

## Installation

The bundles are **self-contained**: the .NET 11 runtime is embedded in the single-file
apphosts, so no separate runtime install is required. macOS is distributed as a
drag-to-Applications `.dmg`; the internal `.tar.gz` is the CI extraction/smoke
intermediate. The macOS arm64 bundle also includes the Vulkan loader and MoltenVK, so
no Homebrew install is needed for GPU rendering. The bundled OpenAL Soft 1.23.1 runtime
means no system OpenAL install is required.

## Known limits

- **No native Metal surface yet.** macOS uses offscreen Vulkan plus a per-frame readback
  into a bitmap; a native `CAMetalLayer`/`VK_EXT_metal_surface` presenter is tracked as
  `PLAT-14`.
- **Retina/DPI and physical gamepads are not yet validated.** Intel macOS support was
  dropped in this release; Apple Silicon is the only supported macOS target.
- **The DMG is unsigned.** Gatekeeper may require right-click Open or an explicit
  exception until signing/notarization is available (`UX-16`).

## Evidence

- [Compatibility.md](Compatibility.md) §10.4 - dated macOS arm64 validation table.
- [KnownIssues.md](KnownIssues.md) - macOS OpenGL and packaging limitations.
- [Vulkan.md](Vulkan.md) - MoltenVK portability and loader details.
- [ImplementationPlan.md](ImplementationPlan.md) §3.3 - `PLAT-14`..`PLAT-18`.
