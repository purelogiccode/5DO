# Optional Vulkan Backend

**Status:** implemented as an optional offscreen presenter and validation probe.
OpenGL/GLES remains the required desktop presenter; Vulkan is not a v1 release gate.
The backend now implements xBR-lv2, FSR 1.0 (EASU + RCAS), and HQx 2x/3x/4x in
addition to accurate, nearest, sharp-bilinear, and CRT.

## What it is

`FiveDo.Video.Vulkan` is a pure C# Vulkan 1.0 backend that mirrors the OpenGL
two-stage pipeline:

```text
VdlFrame
  |
  +--> indexed pixels      R16_UINT texture (max 1024x480)
  +--> per-line CLUT       R8G8B8A8_UNORM texture (32 x height)
  +--> line parameters     R32G32_UINT texture (background, out-control)
        |
        v
  palette pass at native resolution (SPIR-V fragment shader)
        |
        +--> xBR-lv2 4x pass (optional)
        +--> FSR EASU pass at viewport resolution (optional)
        |
        v
  scale/filter pass at output resolution (or FSR RCAS)
        |
        v
  B8G8R8A8_UNORM (or R8G8B8A8_UNORM) target
        |
        v
  host readback buffer
```

The palette shader is a direct port of `FrameAssembler`/`GpuPalette`: pixel zero uses
the per-line background, the fixed CLUT is selected by output-control bit 25 plus pixel
bit 15, and the per-line CLUT uses independent red, green, and blue indices. A 320x240
`Accurate` render matches the CPU oracle exactly.

The renderer is intentionally offscreen. It does not create an instance surface,
swapchain, or presentation queue. The desktop shell can display it through the existing
`WriteableBitmap` path (`--gl-backend vulkan`), and the headless probe is the primary
validation tool.

## Filters

| Filter | Vulkan behavior |
|---|---|
| `Accurate` | Nearest sampling, integer scaling; pixel-identical to `FrameAssembler` at 1:1. |
| `Nearest` | Nearest sampling with arbitrary scale/letterbox. |
| `SharpBilinear` | The same integer-prescale/sub-texel clamp as the OpenGL shader. |
| `Crt` | Barrel distortion, scanlines, aperture mask, vignette, and 3x3 bloom. |
| `Xbr` | Hyllian's xBR-lv2 4x GPU pass over the GPU-expanded native frame, finished with sharp-bilinear. |
| `Fsr` | FSR 1.0 EASU at viewport resolution followed by the RCAS sharpening pass. |
| `Hqx`, `Hqx3x`, `Hqx4x` | CPU `FrameAssembler` expansion plus `HqxFilter`, uploaded as a native-resolution source and finished with sharp-bilinear. CPU-bound and opt-in. |

## Commands

Headless parity probe (320x240, 1:1 Accurate):

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/parity.json `
  --vk-probe-size 320x240 --vk-probe-frames 20 --vk-probe-warmup 5 `
  --vk-probe-filter accurate --vk-probe-capture artifacts/vk-probe/parity.png
```

4K sharp-bilinear timing:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/4k-sharp.json `
  --vk-probe-size 3840x2160 --vk-probe-frames 30 --vk-probe-warmup 10 `
  --vk-probe-filter sharp-bilinear
```

4K xBR and FSR timing:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/4k-xbr.json `
  --vk-probe-size 3840x2160 --vk-probe-frames 60 --vk-probe-warmup 15 `
  --vk-probe-filter xbr

dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/4k-fsr.json `
  --vk-probe-size 3840x2160 --vk-probe-frames 60 --vk-probe-warmup 15 `
  --vk-probe-filter fsr
```

Desktop shell with the optional Vulkan path:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend vulkan --disc "game.chd"
```

Options: `--vk-probe` (required output JSON), `--vk-probe-size WxH` (default
1920x1080), `--vk-probe-frames` (60), `--vk-probe-warmup` (10), `--vk-probe-filter`
(`accurate`, `nearest`, `sharp-bilinear`/`sharp`, `crt`, `xbr`, `fsr`, `hqx`,
`hqx3x`, `hqx4x`, plus the OpenGL aliases), `--vk-probe-capture <png>`, and
`--vk-probe-timeout` (30 seconds).

## Measured results

Development machine: Windows 11, Ryzen 9 7900, RTX 4070, Vulkan 1.4. The xBR, FSR,
and HQx rows use 60 measured frames after 15 warm-up frames; the earlier baseline rows
use 30 frames after 10 warm-up frames. The probe waits for each frame; readback is
excluded from the timing loop.

| Case | Present mean | Present p99 | GPU mean | GPU p99 | Managed bytes/frame |
|---|---:|---:|---:|---:|---:|
| 4K sharp-bilinear | 1.508 ms | 1.797 ms | 1.344 ms | 1.615 ms | 0 |
| 4K accurate | 1.505 ms | 1.752 ms | 1.341 ms | 1.501 ms | 0 |
| 4K CRT | 1.508 ms | 1.635 ms | 1.336 ms | 1.382 ms | 0 |
| 4K xBR | 1.549 ms | 1.918 ms | 1.389 ms | 1.728 ms | 0 |
| 4K FSR (EASU + RCAS) | 1.873 ms | 2.154 ms | 1.694 ms | 1.967 ms | 0 |
| 1080p sharp-bilinear | 0.489 ms | 0.502 ms | 0.344 ms | 0.348 ms | 0 |
| 1080p xBR | 0.606 ms | 0.848 ms | 0.423 ms | 0.591 ms | 0 |
| 1080p FSR (EASU + RCAS) | 0.562 ms | 0.600 ms | 0.434 ms | 0.436 ms | 0 |
| 4K HQx 2x | 7.661 ms | 10.355 ms | 1.386 ms | 1.431 ms | 0 |

320x240 Accurate parity: 0 different pixels, CPU/GPU FNV-1a hash `3602745797`
(`0xD6BD89C5`), no vertical flip. The same hash is produced by the OpenGL WGL/ANGLE
probes. A 640x480 source/output case and a source-size change (320x240 -> 640x480) also
match the CPU oracle; this covers expanded-target recreation and descriptor rebinding.
The xBR, FSR, and HQx probes run at 640x480 and 4K with `filterFallback: false` and
zero managed bytes per frame. xBR samples the GPU-expanded native frame directly; the
offscreen backend does not perform a CPU palette expansion or readback for it. A
top-red/bottom-blue split-frame test verifies that xBR, FSR, and HQx preserve top-down
orientation at 320x240 and 640x480. HQx uses the same CPU expansion as the OpenGL
presenter and is CPU-bound; the 4K row above is present time, not a 60 fps claim.

These are development-machine numbers, not a hardware guarantee.

## Build and packaging

- `Silk.NET.Vulkan` 2.23.0 provides the Vulkan bindings.
- `XenoAtom.ShaderCompiler.Build` 1.2.2 compiles `Frame.vert`, `Palette.frag`,
  `Scale.frag`, `Xbr.frag`, `FsrEasu.frag`, and `FsrRcas.frag` to SPIR-V at build time
  and embeds the byte arrays in the assembly. No shader compiler or Vulkan SDK is
  required at runtime.
- The Vulkan loader (`vulkan-1.dll` on Windows, `libvulkan.so.1` on Linux, the MoltenVK
  loader on macOS) is provided by the operating system/driver; the project does not
  bundle it.
- The desktop frontend still publishes trimmed; the Vulkan probe path has no
  reflection-based JSON.

## Tests

- `VulkanShaderTests` checks that all six generated shader modules (frame, palette,
  scale, xBR, FSR EASU, FSR RCAS) are SPIR-V and that `RendererApi.Vulkan` is reported
  correctly.
- `VulkanRendererTests` (`FIVEDO_VULKAN_TESTS=1`) renders 320x240 and 640x480 frames and
  compares them with `FrameAssembler`, including background, fixed-CLUT, and per-line
  CLUT paths. It also renders a top-red/bottom-blue split frame through xBR, FSR, and
  HQx at 320x240 and 640x480, asserts that all three preserve top-down orientation, and
  checks that HQx does not report a fallback.
- `VulkanProbeIntegrationTests` (`FIVEDO_VULKAN_TESTS=1`) runs the built frontend probe
  for 320x240 parity, a 4K sharp-bilinear pass, and 640x480 xBR/FSR/HQx passes with
  `filterFallback: false`.
- `VkProbeOptionsTests` covers defaults, size/frames/filter/capture parsing, and invalid
  values.

```powershell
$env:FIVEDO_VULKAN_TESTS = "1"
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release `
  --filter "FullyQualifiedName~Vulkan"
```

The opt-in tests are skipped by default because CI runners do not expose a Vulkan device.
The SPIR-V and option-parsing tests run everywhere.

## Limitations and next steps

- No window-system surface or swapchain; presentation is offscreen plus CPU readback.
  A native Win32/X11/Metal surface and swapchain are future work.
- xBR and FSR 1.0 are GPU passes. HQx 2x/3x/4x are CPU expansions (shared with the
  OpenGL presenter) and are CPU-bound; they are opt-in and not recommended at 4K on
  slow CPUs.
- No vsync control on the offscreen path.
- The desktop Vulkan path renders at the source size (320x240 or 640x480) and lets
  Avalonia scale the bitmap; it is not the 4K presentation path.
- Vulkan 1.1+ features (storage buffers in fragment shaders, timeline semaphores) are not
  required; the backend requests Vulkan 1.0 and uses sampled images and push constants.
- Device/driver validation on Linux, macOS/MoltenVK, arm64, and integrated GPUs is
  pending.
