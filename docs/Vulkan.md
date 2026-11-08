# Optional Vulkan Backend

**Status:** implemented as an optional offscreen presenter, a validation probe, a
native Win32 swapchain probe, and an embedded Win32 child surface for the desktop
shell. OpenGL/GLES remains the required desktop presenter; Vulkan is not a v1 release
gate. The backend now implements xBR-lv2, FSR 1.0 (EASU + RCAS), and HQx 2x/3x/4x in
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
        +--> host readback buffer (offscreen probe and captures)
        |
        +--> Win32 swapchain (desktop child surface and window probe)
```

The palette shader is a direct port of `FrameAssembler`/`GpuPalette`: pixel zero uses
the per-line background, the fixed CLUT is selected by output-control bit 25 plus pixel
bit 15, and the per-line CLUT uses independent red, green, and blue indices. A 320x240
`Accurate` render matches the CPU oracle exactly.

The renderer is offscreen by default. It does not create an instance surface, swapchain,
or presentation queue unless `InitializeSurface` is called. `--vk-window-probe` creates
a Win32 window, enables `VK_KHR_surface`/`VK_KHR_win32_surface`/`VK_KHR_swapchain`, and
blits the rendered output image into the current swapchain image before presenting. The
desktop shell hosts a native Win32 child window inside the Avalonia window through
`VulkanVideoSurface` (`--gl-backend vulkan`), so the steady-state desktop path has no
GPU-to-CPU readback and no `WriteableBitmap` upload. The offscreen readback path remains
for the headless probe, screenshots, non-Windows platforms, and fallback if the child
surface cannot be created. The headless probe remains the primary validation tool.

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

Native Win32 swapchain probe (opens a real window, presents through a Vulkan
swapchain, and writes the same JSON report):

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-window-probe artifacts/vk-probe/window-4k.json `
  --vk-window-probe-size 3840x2160 --vk-window-probe-frames 60 `
  --vk-window-probe-warmup 15 --vk-window-probe-filter sharp-bilinear
```

Options: `--vk-probe` (headless output JSON), `--vk-window-probe` (native window
output JSON), `--vk-probe-size`/`--vk-window-probe-size` (default 1920x1080),
`--vk-probe-frames`/`--vk-window-probe-frames` (60),
`--vk-probe-warmup`/`--vk-window-probe-warmup` (10),
`--vk-probe-filter`/`--vk-window-probe-filter` (`accurate`, `nearest`,
`sharp-bilinear`/`sharp`, `crt`, `xbr`, `fsr`, `hqx`, `hqx3x`, `hqx4x`, plus the
OpenGL aliases), `--vk-probe-capture`/`--vk-window-probe-capture <png>`, and
`--vk-probe-timeout`/`--vk-window-probe-timeout` (30 seconds). The window probe
requests FIFO (vsync) presentation; the headless probe is uncapped.

## Measured results

Development machine: Windows 11, Ryzen 9 7900, RTX 4070, Vulkan 1.4. All rows use
60 measured frames after 15 warm-up frames. The probe waits for each frame. The
per-frame GPU readback is recorded only when `CaptureToFrame` is enabled; the probe
leaves it disabled and performs a one-shot readback after the timing loop. GPU time
covers the upload, palette, and filter passes, not the readback.

| Case | Present mean | Present p99 | GPU mean | GPU p99 | Managed bytes/frame |
|---|---:|---:|---:|---:|---:|
| 4K sharp-bilinear | 0.240 ms | 1.060 ms | 0.075 ms | 0.091 ms | 0 |
| 4K accurate | 0.221 ms | 0.687 ms | 0.071 ms | 0.080 ms | 0 |
| 4K CRT | 0.364 ms | 1.015 ms | 0.175 ms | 0.187 ms | 0 |
| 4K xBR | 0.316 ms | 0.796 ms | 0.132 ms | 0.143 ms | 0 |
| 4K FSR (EASU + RCAS) | 0.604 ms | 1.303 ms | 0.404 ms | 0.415 ms | 0 |
| 1080p sharp-bilinear | 0.169 ms | 0.790 ms | 0.030 ms | 0.040 ms | 0 |
| 1080p xBR | 0.214 ms | 0.375 ms | 0.085 ms | 0.088 ms | 0 |
| 1080p FSR (EASU + RCAS) | 0.269 ms | 0.781 ms | 0.116 ms | 0.122 ms | 0 |
| 4K HQx 2x | 0.264 ms | 0.405 ms | 0.099 ms | 0.100 ms | 0 |
| 4K HQx 3x | 0.287 ms | 0.336 ms | 0.119 ms | 0.121 ms | 0 |
| 4K HQx 4x | 0.344 ms | 0.522 ms | 0.150 ms | 0.153 ms | 0 |

Present time includes the per-frame `vkQueueWaitIdle` and is therefore a synchronous
end-to-end number; the OpenGL probe measures asynchronous command submission. Compare
the GPU column for renderer cost.

Native Win32 swapchain probe, same machine, 60 measured frames after 15 warm-up
frames, FIFO presentation on a 165 Hz display:

| Case | Present mean | Present p99 | GPU mean | GPU p99 | Managed bytes/frame |
|---|---:|---:|---:|---:|---:|
| 4K sharp-bilinear, FIFO | 5.963 ms | 6.534 ms | 0.077 ms | 0.088 ms | 0 |
| 640x480 sharp-bilinear, FIFO | 5.935 ms | 6.811 ms | 0.019 ms | 0.031 ms | 0 |

Present time includes the swapchain acquire/present wait and the display refresh
interval; GPU time is the comparable rendering cost. At 165 Hz the FIFO floor is
~6.06 ms, so both rows are display-paced. The window probe reports `vsyncActive: true`
and `vsyncBackend: "fifo"`; a 320x240 Accurate capture still matches the CPU oracle
with 0 different pixels.

320x240 Accurate parity: 0 different pixels, CPU/GPU FNV-1a hash `3602745797`
(`0xD6BD89C5`), no vertical flip. The same hash is produced by the OpenGL WGL/ANGLE
probes. A 640x480 source/output case and a source-size change (320x240 -> 640x480) also
match the CPU oracle; this covers expanded-target recreation and descriptor rebinding.
The xBR, FSR, and HQx probes run at 640x480 and 4K with `filterFallback: false` and
zero managed bytes per frame. xBR samples the GPU-expanded native frame directly; the
offscreen backend does not perform a CPU palette expansion or readback for it. A
top-red/bottom-blue split-frame test verifies that xBR, FSR, and HQx preserve top-down
orientation at 320x240 and 640x480. HQx 2x/3x/4x are GPU rule-table passes over the
palette texture and match the CPU `HqxFilter` expansion in the opt-in parity tests.

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

- `VulkanShaderTests` checks that all seven generated shader modules (frame, palette,
  scale, HQx, xBR, FSR EASU, FSR RCAS) are SPIR-V and that `RendererApi.Vulkan` is
  reported correctly.
- `VulkanRendererTests` (`FIVEDO_VULKAN_TESTS=1`) renders 320x240 and 640x480 frames and
  compares them with `FrameAssembler`, including background, fixed-CLUT, and per-line
  CLUT paths. It also renders a top-red/bottom-blue split frame through xBR, FSR, and
  HQx at 320x240 and 640x480, asserts that all three preserve top-down orientation,
  checks that HQx does not report a fallback, and compares HQx 2x/3x/4x and the
  multi-frame synthetic probe frame with the CPU `HqxFilter` expansion.
- `VulkanProbeIntegrationTests` (`FIVEDO_VULKAN_TESTS=1`) runs the built frontend probe
  for 320x240 parity, a 4K sharp-bilinear pass, and 640x480 xBR/FSR/HQx passes with
  `filterFallback: false`.
- `VkProbeOptionsTests` covers headless/window defaults, size/frames/filter/capture
  parsing, shared option aliases, and invalid values.
- `VulkanProbeIntegrationTests.WindowProbe_PresentsThroughASwapchain`
  (`FIVEDO_VULKAN_TESTS=1`, Windows) opens a window and checks `offscreen: false`,
  FIFO vsync, CPU parity, and zero managed bytes/frame. A CI-run window probe would
  require a desktop session and is not part of the default suite.

```powershell
$env:FIVEDO_VULKAN_TESTS = "1"
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release `
  --filter "FullyQualifiedName~Vulkan"
```

The opt-in tests are skipped by default because CI runners do not expose a Vulkan device.
The SPIR-V and option-parsing tests run everywhere.

## Limitations and next steps

- The native swapchain is a Win32 path. The desktop shell embeds it as a native child
  window inside the Avalonia window; X11/Wayland/Metal surfaces are future work. The
  child window is outside Avalonia's compositor, so Avalonia-drawn overlays over the
  video rectangle (for example the diagnostic overlay) are obscured; the status bar and
  header remain visible. Non-Windows platforms fall back to the offscreen/readback path.
- The per-frame readback is only recorded when `CaptureToFrame` is enabled; probes and
  screenshots use a one-shot copy. The embedded Win32 surface does not read back at all.
  The offscreen fallback still needs the readback every frame and remains slower than
  the direct OpenGL presenter.
- xBR, FSR 1.0, and HQx 2x/3x/4x are GPU passes. HQx uses the shared 256x32
  `R32G32Uint` rule table and is no longer CPU-bound; it remains opt-in because it
  adds an intermediate `source * scale` target.
- The offscreen path has no vsync control. The window probe requests FIFO and reports
  the selected present mode; it does not yet expose mailbox/immediate as user settings.
- Vulkan 1.1+ features (storage buffers in fragment shaders, timeline semaphores) are not
  required; the backend requests Vulkan 1.0 and uses sampled images and push constants.
- Device/driver validation on Linux, macOS/MoltenVK, arm64, and integrated GPUs is
  pending.
