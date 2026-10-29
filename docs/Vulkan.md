# Optional Vulkan Backend

**Status:** implemented as an optional offscreen presenter and validation probe.
OpenGL/GLES remains the required desktop presenter; Vulkan is not a v1 release gate.

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
        v
  scale/filter pass at output resolution
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
| `Xbr`, `Hqx`, `Hqx3x`, `Hqx4x`, `Fsr` | Not implemented; the backend uses sharp-bilinear and reports `filterFallback: true`. |

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

Desktop shell with the optional Vulkan path:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend vulkan --disc "game.chd"
```

Options: `--vk-probe` (required output JSON), `--vk-probe-size WxH` (default
1920x1080), `--vk-probe-frames` (60), `--vk-probe-warmup` (10), `--vk-probe-filter`
(`accurate`, `nearest`, `sharp-bilinear`/`sharp`, `crt`, plus the OpenGL aliases),
`--vk-probe-capture <png>`, and `--vk-probe-timeout` (30 seconds).

## Measured results

Development machine: Windows 11, Ryzen 9 7900, RTX 4070, Vulkan 1.4, 30 measured frames
after 10 warm-up frames. The probe waits for each frame; readback is excluded from the
timing loop.

| Case | Present mean | Present p99 | GPU mean | GPU p99 | Managed bytes/frame |
|---|---:|---:|---:|---:|---:|
| 4K sharp-bilinear | 1.508 ms | 1.797 ms | 1.344 ms | 1.615 ms | 0 |
| 4K accurate | 1.505 ms | 1.752 ms | 1.341 ms | 1.501 ms | 0 |
| 4K CRT | 1.508 ms | 1.635 ms | 1.336 ms | 1.382 ms | 0 |
| 1080p sharp-bilinear | 0.489 ms | 0.502 ms | 0.344 ms | 0.348 ms | 0 |

320x240 Accurate parity: 0 different pixels, CPU/GPU FNV-1a hash `3602745797`
(`0xD6BD89C5`), no vertical flip. The same hash is produced by the OpenGL WGL/ANGLE
probes. A 640x480 source/output case and a source-size change (320x240 -> 640x480) also
match the CPU oracle; this covers expanded-target recreation and descriptor rebinding.

These are development-machine numbers, not a hardware guarantee.

## Build and packaging

- `Silk.NET.Vulkan` 2.23.0 provides the Vulkan bindings.
- `XenoAtom.ShaderCompiler.Build` 1.2.2 compiles `Frame.vert`, `Palette.frag`, and
  `Scale.frag` to SPIR-V at build time and embeds the byte arrays in the assembly. No
  shader compiler or Vulkan SDK is required at runtime.
- The Vulkan loader (`vulkan-1.dll` on Windows, `libvulkan.so.1` on Linux, the MoltenVK
  loader on macOS) is provided by the operating system/driver; the project does not
  bundle it.
- The desktop frontend still publishes trimmed; the Vulkan probe path has no
  reflection-based JSON.

## Tests

- `VulkanShaderTests` checks that all three generated shader modules are SPIR-V and that
  `RendererApi.Vulkan` is reported correctly.
- `VulkanRendererTests` (`FIVEDO_VULKAN_TESTS=1`) renders 320x240 and 640x480 frames and
  compares them with `FrameAssembler`, including background, fixed-CLUT, and per-line
  CLUT paths.
- `VulkanProbeIntegrationTests` (`FIVEDO_VULKAN_TESTS=1`) runs the built frontend probe
  for 320x240 parity and a 4K sharp-bilinear pass.
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
- No xBR/HQx/FSR shaders; unsupported filters fall back to sharp-bilinear.
- No vsync control.
- The desktop Vulkan path renders at the source size (320x240 or 640x480) and lets
  Avalonia scale the bitmap; it is not the 4K presentation path.
- Vulkan 1.1+ features (storage buffers in fragment shaders, timeline semaphores) are not
  required; the backend requests Vulkan 1.0 and uses sampled images and push constants.
- Device/driver validation on Linux, macOS/MoltenVK, arm64, and integrated GPUs is
  pending.
