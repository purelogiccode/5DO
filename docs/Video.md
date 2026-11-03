# Video and GPU Presenters

This document describes how 5DO turns MADAM/VDLP output into a window image: the
frame data model, the CPU accuracy oracle, the two-stage GPU presenter, every filter
(including FSR 1.0), backend selection, vsync, screenshots, the GPU probe, and known
limitations.

Source map:

| Area | Files |
|---|---|
| VDLP parser | `src/FiveDo.Video/Vdlp.cs` |
| Frame model | `src/FiveDo.Core/VdlFrame.cs`, `VdlLine.cs`, `FrameBuffer.cs` |
| CPU assembler | `src/FiveDo.Video/FrameAssembler.cs`, `GpuPalette.cs` |
| GPU uploader | `src/FiveDo.Video/VdlFrameUploader.cs` |
| Mailbox | `src/FiveDo.Video/VdlFrameMailbox.cs` |
| Viewport/aspect | `src/FiveDo.Video/ViewportCalculator.cs` |
| CPU fallback | `src/FiveDo.Video/CpuVideoRenderer.cs` |
| GPU presenter | `src/FiveDo.Video.OpenGL/OpenGlRenderer.cs`, `Shaders.cs` |
| Optional Vulkan presenter | `src/FiveDo.Video.Vulkan/VulkanOffscreenRenderer.cs`, `Frame.vert`, `Palette.frag`, `Scale.frag`, `Xbr.frag`, `FsrEasu.frag`, `FsrRcas.frag` |
| Optional Direct3D 11 presenter | `src/FiveDo.Video.Direct3D/Direct3D11Renderer.cs`, `Shaders.cs` |
| Filters | `src/FiveDo.Video/HqxFilter.cs`, `HqxPatterns*.cs`, xBR/FSR shader bodies (OpenGL, Vulkan, and Direct3D 11) |
| Capabilities/vsync | `OpenGlCapabilities.cs`, `SwapInterval.cs` |
| Avalonia surface | `src/FiveDo.Frontend.Avalonia/Controls/VideoSurface.cs` |
| GPU probes | `src/FiveDo.Frontend.Avalonia/GlProbeWindow.cs`, `GlProbeOptions.cs`, `VkProbeRunner.cs`, `VkProbeOptions.cs` |

---

## 1. Resolution and frame model

The 3DO renders at **320x240** (NTSC, 4:3) in the default mode. Wider VDLP bitmap
modes are parsed as **384, 512, 640, or 1024** pixels per line. The opt-in
**high-resolution** mode (`--high-res`, Settings > Emulation, or
`EmulatorTuning.HighResolution`) presents a line-doubled **640x480** frame. The CEL
X coordinate is 640-wide, but the source remains 240 lines; see the limitation below.

`VdlFrame` contains:

- `SourceWidth`, `SourceHeight`: visible dimensions.
- `Lines`: 960 reusable `VdlLine` objects (240 visible lines x 4 for high-res).
- `VdlLine`:
  - `Width`: valid pixels in this line.
  - `Pixels` / `PixelBuffer`: up to 1280 indexed 16-bit pixels.
  - `ClutB/G/R`: 32 entries per component.
  - `OutControl`, `ClutDma`, `Background`.

The frame object is reused every frame; the host must copy it before handing it to
another thread (`VdlFrameMailbox` does this).

### VDLP

`Vdlp` parses the display list in VRAM, exactly like FreeDO's `vdlp.cpp`:

- VRAM is read at `(addr & 0xFFFFF) + 2 MB`.
- The current display-list entry selects a control/background/CLUT-DMA command.
- Bitmap lines copy indexed pixels from the previous/current bitmap, honoring
  `Modulo` (320/384/512/640/1024).
- The line's CLUT is copied into the `VdlLine`.
- At the V1 line, the machine sets the frame source dimensions and presents.

High-resolution mode uses four 1 MB VRAM planes. A low-resolution bitmap line supplies
the even/odd columns of two adjacent high-resolution lines; planes 0/1 form the even
line and planes 2/3 form the odd line. CEL rasterization, SPORT copies, and VDLP line
copying all honor the same plane layout. Lemmings' 640x480 title is the primary
validation case.

**This is line doubling, not true internal 640x480 rendering.** The 240-line source is
written to both interlace fields, so horizontal CEL sampling improves but vertical
detail remains 240 lines. True 640x480, 2x supersampling, and GPU CEL compositing are
research topics in [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md); the
compatibility-safe default remains 320x240 plus output filtering.

---

## 2. CPU frame assembler (accuracy oracle)

`FrameAssembler.Assemble` is the reference for what the GPU must produce. It is used by
the CLI, headless tests, save-state thumbnails, CPU fallback, and the GPU parity probe.

For each visible pixel:

```text
pixel = line.Pixels[x]
if pixel == 0:
    B = expand5(background bits 0..4)
    G = expand5(background bits 5..9)
    R = expand5(background bits 10..14)
else if (line.OutControl & 0x02000000) != 0 && (pixel & 0x8000) != 0:
    B = expand5(pixel bits 0..4)
    G = expand5(pixel bits 5..9)
    R = expand5(pixel bits 10..14)
else:
    B = line.ClutB[pixel & 0x1F]
    G = line.ClutG[(pixel >> 5) & 0x1F]
    R = line.ClutR[(pixel >> 10) & 0x1F]
```

`expand5(v) = (v << 3) | (v >> 2)`, matching the GLSL bit replication. The result is
BGRA8888 or RGBA8888 in a `FrameBuffer`; alpha is 255.

**The GPU palette shader is a direct port of this algorithm**, including independent
R/G/B CLUT indices. A 320x240 `Accurate` GPU capture must match the CPU assembler
pixel-for-pixel; the probe verifies this with an FNV-1a hash and a pixel diff.

---

## 3. Two-stage GPU pipeline

The presenter deliberately separates palette expansion from output scaling:

```text
VdlFrame
  |
  +--> indexed pixel texture: R16UI, width x height
  +--> palette texture:       RGB8, 32 entries x height
  +--> line-param texture:    RGBA32UI, 1 x height (background, out-control)
        |
        v
Stage 1: palette expansion at native resolution (e.g. 320x240)
  -> RGB8/RGBA8 native-resolution FBO texture
        |
        v
Stage 2: scaling/filtering at output resolution (up to 4K)
  -> window / fullscreen framebuffer
```

Why two stages: a single output-resolution shader would perform palette/CLUT work for
every one of ~8.3M 4K pixels (~108x the native 320x240 pixel count). The two-stage
design performs palette work once per emulated pixel and leaves a cheap texture-filter
pass for the output.

### Uploads

`VdlFrameUploader` packs:

- `Pixels`: `ushort[1280 * 480]` (max source).
- `Clut`: `byte[32 * 3 * 480]` (R/G/B per entry per line).
- `LineParams`: `uint[2 * 480]` (background, out-control).

`OpenGlRenderer` allocates immutable storage with `TexStorage2D` and updates with
`TexSubImage2D` (or the equivalent path) each frame. Uniform locations, the VAO, and the
present callback are cached. Steady-state presentation performs **0 managed
allocations/frame** on the measured path.

Approximate upload cost for 320x240:

- Indexed pixels: ~150 KB/frame.
- CLUT: ~23 KB/frame.
- Line params: ~2 KB/frame.

The upload is independent of output resolution.

### Mailbox

The machine reuses its `VdlFrame`, so `VdlFrameMailbox` keeps a snapshot. `Write` copies
the visible width under a short lock. `Read(destination, reader)` copies into a
caller-owned frame, releases the lock, then invokes the renderer so GPU submission
cannot stall the emulation thread. The mailbox drops stale frames rather than queueing
them; the core never waits for the GPU.

---

## 4. Filters

`VideoFilter` values and behavior:

| Filter | Stage 2 behavior | Cost / notes |
|---|---|---|
| `Accurate` | Nearest sampling, integer scaling; 1:1 at 320x240. | Accuracy oracle; matches CPU `FrameAssembler` pixel-for-pixel. |
| `Nearest` | Nearest sampling with arbitrary scale/letterbox. | Cheapest; can shimmer at non-integer scales. |
| `SharpBilinear` | Integer prescale then hardware bilinear sub-texel blend. | Default enhanced mode; crisp at integer scales, cheap. |
| `Crt` | Barrel distortion, scanlines, 3-pixel aperture mask, vignette, 3x3 bloom. | Opt-in; more GPU work. |
| `Xbr` | GPU palette expansion, xBR-lv2 4x edge-directed pass, sharp-bilinear finish. | Opt-in; 1280x960 intermediate for 320x240; 4K WGL present 0.28/1.47 ms. |
| `Hqx` | CPU palette expansion, HQx 2x, upload, sharp-bilinear finish. | Opt-in; CPU cost ~2.3 ms/frame on checkerboard, ~12.1 ms on noise (320x240). |
| `Hqx3x` | HQx 3x, same finish. | ~4.2 ms checkerboard, ~13.4 ms noise. |
| `Hqx4x` | HQx 4x, same finish. | ~7.5 ms checkerboard, ~14.8 ms noise; most expensive. |
| `Fsr` | FSR 1.0 EASU upscale to viewport, then RCAS sharpen to output. | Opt-in; two GPU passes; 4K WGL present 0.13/1.19 ms; sharpness slider. |

### Accurate / Nearest

`Accurate` uses `AspectMode`/integer scaling and nearest sampling. At a 320x240 output
it is 1:1. `Nearest` uses the same sampling but allows arbitrary scale/letterboxing.
Neither touches emulated state.

### Sharp-bilinear

The shader prescales by the integer output/source factor and clamps the sub-texel
fraction to the integer-scale region before the hardware bilinear sample. This keeps
integer scales crisp and avoids the blur of a plain bilinear stretch. It is the default
`SharpBilinear` filter.

### CRT

The CRT shader (`uFilter == 2`) applies:

- Mild barrel distortion: `centered *= 1 + 0.055 * dot(centered, centered)`.
- Black outside the curved screen (does not stretch edge texels).
- Scanlines: `color *= 1 - uScanline * (fragY & 1)`.
- Aperture mask: 3-pixel R/G/B-ish mask, mixed by `uScanline * 0.5`.
- Vignette: `1 - 0.12 * dot(centered, centered)`.
- Bloom: 3x3 native-resolution glow around pixels above `BloomThreshold`, scaled by
  `Bloom`. Sampling the native source keeps the glow soft and resolution-independent.
  Bloom is only active in CRT mode.

`RendererSettings.ScanlineStrength` defaults to 0.25; `Bloom` defaults to 0.35;
`BloomThreshold` defaults to 0.6. Settings exposes Bloom (and FSR sharpness); the other
values are renderer defaults.

### xBR

`Xbr` samples the GPU-expanded palette texture at native resolution, runs the xBR-lv2
4x edge-directed shader into a 4x offscreen target, then finishes with sharp-bilinear.
The earlier CPU `FrameAssembler` expansion and upload were removed on 2026-10-29; the
320x240 WGL/ANGLE captures remain top-down and within one mean channel level of the
accurate frame. If the 4x target exceeds `MaxTextureSize`, the renderer falls back to
the normal sharp-bilinear path instead of failing.

### HQx

`HqxFilter` (in `FiveDo.Video`, shared by the OpenGL and Vulkan presenters) is a CPU
port of Maxim Stepin's hq2x/hq3x/hq4x (via Cameron Zemek's C port,
LGPL-2.1-or-later). It consumes the CPU-expanded BGRA frame, so palette expansion
stays identical to `FrameAssembler`. The 2x/3x/4x pattern switches are generated data
(`HqxPatterns*.cs`) and match the C++ `hq2x_32_rb`/`hq3x_32_rb`/`hq4x_32_rb` reference
hashes. HQx 3x/4x allocate larger intermediate frames; they are the most expensive
filters and are not recommended with `--high-res` on slow CPUs.

### FSR 1.0

`Fsr` runs AMD FidelityFX Super Resolution 1.0 as two spatial passes:

1. **EASU** (edge-adaptive spatial upsampling) reads the native-resolution expanded
   texture and writes a viewport-sized FBO. It uses the output/source ratio, 12-tap
   directional/edge analysis, and a 3x3 neighborhood to reconstruct edges.
2. **RCAS** (robust contrast-adaptive sharpening) reads the EASU result and writes the
   final output rectangle. It computes per-channel lobes, clamps the lobe, and applies
   the sharpening kernel.

`RendererSettings.FsrSharpness` is 0 (soft) to 1 (maximum). The shader uniform is in
stops: `(1 - FsrSharpness) * 2`, so 1.0 gives 0 stops (maximum sharpening) and 0.0
gives 2 stops (soft). The Settings > Video slider edits this value. The shader is
adapted to GLSL 3.30 / GLSL ES 300 and is attributed to AMD under the MIT license in
`THIRD-PARTY-NOTICES.md`.

FSR is presentation-only and does not feed back into VRAM or emulation. The 4K WGL
probe measures present 0.132 ms mean / 1.191 ms p99 and GPU 0.441/0.483 ms; the
optional Vulkan backend measures present 0.604/1.303 ms and GPU 0.404/0.415 ms
(excluding the optional per-frame readback). It is opt-in and costs two GPU passes.

---

## 5. Aspect, integer scaling, and viewport

`ViewportCalculator.Compute` returns a centered destination rectangle:

- `Stretch`: full output rectangle.
- `FourThree`: target aspect 4:3.
- `Custom`: `RendererSettings.CustomAspect` (default 4:3; the Settings UI exposes the
  enum but not a custom-ratio editor in this release).
- `Integer`: target aspect = source aspect, then integer scale with letterboxing.

`IntegerScale` also forces integer scaling when `Aspect == FourThree`. The scale is
`floor(min(maxWidth/sourceWidth, maxHeight/sourceHeight))`, at least 1. The rectangle is
centered in the output.

The renderer clears to black before drawing, so letterbox/pillarbox bars are black.

`RendererSettings.OutputResolution` selects the final output size. `Window` follows the
physical window size; the fixed presets render the filtered image offscreen and scale it
into the window. This is output upscaling only: the emulated frame and CEL/VRAM resolution
do not change. The CPU fallback applies the same viewport policy when it scales its native
frame into a fixed output buffer.

---

## 6. Backends, capabilities, and fallback

| Backend | API / shader | Platforms | Notes |
|---|---|---|---|
| Desktop GL | OpenGL 3.3 core / GLSL 330 | Windows WGL, Linux GLX/EGL, macOS 4.1 core | Primary desktop path; `R16UI`, `usampler2D`, `texelFetch`. |
| GLES | OpenGL ES 3.0/3.1 / GLSL ES 300 | Windows ANGLE, other desktop GLES contexts | Same two-stage pipeline. |
| Vulkan | Vulkan 1.0 / SPIR-V 1.0 | Windows/Linux/macOS with a Vulkan loader and device | Optional offscreen backend and probe; embedded Win32 child surface for the desktop path; CPU readback fallback. |
| Direct3D 11 | Direct3D 11 / HLSL SM 5.0 | Windows 10/11 with feature level 11_0 | Optional Windows backend and probe; embedded Win32 child swapchain; OpenGL remains the default. |
| CPU | `WriteableBitmap` + `FrameAssembler` | Any | Fallback when no usable GL/GLES/Vulkan/Direct3D context; accuracy oracle. |

On Windows, `--gl-backend auto|wgl|angle|software|vulkan` selects the Avalonia rendering
mode:

- `auto` (default): WGL, then ANGLE/EGL, then software.
- `wgl`: force desktop OpenGL.
- `angle`: force ANGLE/GLES.
- `software`: force Avalonia software rendering (no GL surface).
- `vulkan`: hide the OpenGL surface and use the optional embedded Vulkan Win32 child
  surface; platforms without a Win32 surface fall back to the offscreen/readback path.
- `d3d11` (Windows): hide the OpenGL surface and use the optional embedded Direct3D 11
  Win32 child swapchain. Other platforms normalize this selection to OpenGL.

`GlBackendOption` also accepts aliases: `default`, `desktop`, `gl`, `angleegl`, `egl`,
`gles`, `cpu`, `vk`, `dx11`, `directx`, and `d3d`. The option is wired through `Win32PlatformOptions.RenderingMode`;
on non-Windows platforms the platform's default context selection applies.

`OpenGlCapabilities.Detect` parses `GL_VERSION` and reports:

- `Api`: `OpenGl`, `OpenGlEs`, or `Cpu`.
- `MajorVersion`/`MinorVersion`.
- `MaxTextureSize` (`GL_MAX_TEXTURE_SIZE`).
- `SupportsIntegerTextures`: major >= 3 (R16UI/usampler2D/texelFetch).
- `SupportsVsync`: whether a swap-interval extension was found.
- `SupportsOpenGl33`: desktop GL only.

If GL initialization fails or the context is lost, the frontend logs the error and
switches to `CpuVideoRenderer` + `WriteableBitmap`. Calling `Initialize` again recreates
all GL objects; the frontend re-uploads the latest frame. The CPU fallback is not
expected to hit 4K60; it exists for compatibility and headless/CI use.

### Optional Vulkan backend

`FiveDo.Video.Vulkan` is an optional, offscreen Vulkan 1.0 backend. It mirrors the
two-stage pipeline with SPIR-V shaders compiled at build time:

```text
VdlFrame
  -> R16_UINT pixel texture / R8G8B8A8 CLUT / R32G32_UINT line params
  -> palette pass at native resolution
  -> optional xBR 4x pass or FSR EASU pass
  -> scale/filter pass (or FSR RCAS) at output resolution
  -> B8G8R8A8 (or R8G8B8A8) color target
  -> readback buffer
```

The desktop shell uses it when `--gl-backend vulkan` is selected. Because it renders
offscreen, the result is copied into the existing `WriteableBitmap` path; the OpenGL
presenter remains the full-performance path. The probe uses the same renderer without
Avalonia and is the recommended way to validate a Vulkan device:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/4k-sharp.json `
  --vk-probe-size 3840x2160 --vk-probe-frames 30 --vk-probe-warmup 10 `
  --vk-probe-filter sharp-bilinear
```

Supported filters are `accurate`, `nearest`, `sharp-bilinear`, `crt`, `xbr`, `fsr`,
and `hqx`/`hqx3x`/`hqx4x`. xBR runs as a 4x GPU pass over the GPU-expanded native
frame and finishes with sharp-bilinear; FSR runs EASU into a viewport-sized target,
then RCAS into the output. HQx uses the shared CPU `FrameAssembler` + `HqxFilter`
expansion, uploads the result, and finishes with sharp-bilinear; it is CPU-bound and
opt-in. The desktop Vulkan path hosts a native Win32 child window inside the Avalonia
window, so it presents through a swapchain without CPU readback. It is still optional
and does not replace the WGL/ANGLE presenter; non-Windows platforms fall back to the
offscreen/readback path. The `--vk-window-probe` validates FIFO presentation separately.

A 320x240 `accurate` probe compares the GPU output with `FrameAssembler` and reports the
same FNV-1a hash as the OpenGL probe (`0xD6BD89C5`, 0 different pixels, no vertical flip).
On the development RTX 4070, 4K sharp-bilinear measured 0.24 ms mean / 1.06 ms p99
present and 0.075/0.091 ms GPU time, with 0 managed bytes/frame; 4K xBR measured
0.32/0.80 ms present and 0.13/0.14 ms GPU; 4K FSR measured 0.60/1.30 ms present and
0.40/0.42 ms GPU; 1080p sharp-bilinear measured 0.17/0.79 ms present, 1080p xBR
0.21/0.38 ms, and 1080p FSR 0.27/0.78 ms. GPU time excludes the optional per-frame
readback. See [`Vulkan.md`](Vulkan.md) for the full report and limitations.

### Optional Direct3D 11 backend

`FiveDo.Video.Direct3D` is an optional Windows backend. It mirrors the same two-stage
pipeline with HLSL shaders compiled at runtime:

```text
VdlFrame
  -> R16_UINT pixel texture / R8G8B8A8 CLUT / R32G32_UINT line params
  -> palette pass at native resolution
  -> optional xBR 4x pass or FSR EASU pass
  -> scale/filter pass (or FSR RCAS) at output resolution
  -> B8G8R8A8 output target
  -> DXGI flip-model swapchain (desktop) or staging readback (probe)
```

Select it with `--gl-backend d3d11` or **Settings > Video > Graphics API > DirectX 11
(Windows)**. The option is Windows-only; a persisted selection is normalized to OpenGL
on other platforms. If Direct3D initialization or presentation fails, the shell tries
OpenGL and then the CPU presenter. OpenGL remains the fresh-install default.

The offscreen probe validates device timing and CPU parity:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --d3d-probe artifacts/d3d-probe/4k-sharp.json `
  --d3d-probe-size 3840x2160 --d3d-probe-frames 30 --d3d-probe-warmup 10 `
  --d3d-probe-filter sharp-bilinear
```

`--d3d-window-probe` presents through the native flip-model swapchain and reports
`vsyncBackend: "fifo"` when vsync is active. On the development RTX 4070, 4K
sharp-bilinear measured present p99 0.23 ms and GPU p99 0.07 ms; a 320x240 Accurate
capture matched the CPU oracle with 0 different pixels. See
[`Direct3D.md`](Direct3D.md) for the full report and limitations.

---

## 7. Vsync and presentation

`SwapInterval.TryCreate` resolves the first available platform extension from the
context's `GetProcAddress`:

1. `wglSwapIntervalEXT` (`WGL_EXT_swap_control`).
2. `glXSwapIntervalEXT` + current display/drawable (`GLX_EXT_swap_control`).
3. `glXSwapIntervalMESA` (`GLX_MESA_swap_control`).
4. `glXSwapIntervalSGI` (`GLX_SGI_swap_control`).
5. `eglSwapInterval` + current display (`EGL`).

The renderer reports `VsyncActive` and `VsyncBackend`. A missing extension is not an
error; `SupportsVsync` is false and the platform keeps its default presentation
behavior. The Avalonia compositor may pace the window independently, so a requested
vsync may not cap presentation at 60 Hz; the probe reports renderer/present cost
separately.

Frame pacing is audio-derived on the core thread (44.1 kHz, 60 Hz fallback); the UI
renders on demand when the mailbox sequence or output size changes. Status/FPS updates
run at 4 Hz.

---

## 8. Screenshots

**Screenshot** writes a PNG from the OpenGL output when available, from the Direct3D 11
readback when that presenter is active, otherwise from the CPU frame:

```text
<game>-<yyyyMMdd-HHmmss>-f<frames:D6>.png
```

With **Also save the native CPU frame with screenshots**, a second file is written:

```text
<game>-<yyyyMMdd-HHmmss>-f<frames:D6>-native.png
```

The filtered screenshot comes from `OpenGlRenderer.ReadPixels`, which reads RGBA,
flips bottom-up rows to top-down, and converts to BGRA. The native screenshot uses
`FrameAssembler`. The default directory is Pictures; change it in Settings > Paths.

---

## 9. GPU probes

The OpenGL probe is a developer/validation tool that opens the real Avalonia GL surface,
presents synthetic 320x240 VDL frames, and writes JSON. It never loads a BIOS or disc.
The Vulkan probe runs the optional offscreen backend headlessly and writes the same
report shape with `RequestedBackend: "Vulkan"`. The Direct3D probe runs the optional
Direct3D 11 backend headlessly (or through a Win32 swapchain with
`--d3d-window-probe`) and reports `RequestedBackend: "Direct3D11"`.

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-probe artifacts/gl-probe/4k-sharp.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen
```

Options are listed in [`BuildAndTest.md`](BuildAndTest.md) section 5. `--gl-probe-filter`
accepts `accurate`, `nearest`, `sharp-bilinear`/`sharp`, `crt`, `xbr`/`xbrlv2`,
`fsr`/`fsr1`/`fsr10`, `hqx`/`hqx2x`/`hq2x`, `hqx3x`/`hq3x`, `hqx4x`/`hq4x`.

### Report fields

The report is PascalCase JSON:

| Field | Meaning |
|---|---|
| `Ok`, `Error` | Completion status/message. |
| `RequestedBackend`, `Backend` | CLI request vs detected backend. |
| `Vendor`, `Renderer`, `Api` | GL strings/API. |
| `MajorVersion`, `MinorVersion`, `MaxTextureSize` | Capabilities. |
| `SupportsIntegerTextures`, `SupportsVsync` | Feature flags. |
| `VsyncRequested`, `VsyncActive`, `VsyncBackend` | Vsync request/result. |
| `SourceWidth`, `SourceHeight` | Synthetic source (320x240). |
| `OutputWidth`, `OutputHeight` | Physical output. |
| `Filter`, `Offscreen` | Probe settings. |
| `CapturePath` | Optional PNG. |
| `Parity` | `{ Compared, Matches, DifferentPixels, CpuHash, GpuHash, VerticalFlip }` for a 1:1 Accurate capture. |
| `Frames`, `WarmupFrames` | Measured/warm-up counts. |
| `ElapsedSeconds`, `Fps` | Wall-clock/present rate. |
| `AllocatedBytes`, `AllocatedBytesPerFrame` | Total managed allocation. |
| `RendererAllocatedBytes`, `RendererAllocatedBytesPerFrame` | Renderer + mailbox path. |
| `RendererOnlyAllocatedBytes`, `RendererOnlyAllocatedBytesPerFrame` | Renderer `Present` only. |
| `RendererMeasureAllocations` | Whether stage counters were active. |
| `RendererPresentAllocatedBytes`, `UploadAllocatedBytes`, `PaletteAllocatedBytes`, `ScaleAllocatedBytes` | Last-frame stage allocation. |
| `Present` | `{ MeanMs, P50Ms, P95Ms, P99Ms, MaxMs }`. |
| `Gpu` | Same timing summary, or null if GPU timing is unavailable. |
| `FilterFallback` | True when the backend substituted sharp-bilinear for an unsupported filter. |

The Vulkan probe uses the same report shape and adds no GL-specific fields:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/parity.json `
  --vk-probe-size 320x240 --vk-probe-frames 20 --vk-probe-warmup 5 `
  --vk-probe-filter accurate --vk-probe-capture artifacts/vk-probe/parity.png
```

### Parity

A capture at 320x240 with `accurate` triggers a CPU/GPU comparison. The probe computes
the CPU oracle with `FrameAssembler`, compares RGB pixels (trying both orientations),
and reports `DifferentPixels = 0` when they match. The documented Windows result is
`CpuHash = GpuHash = 0xD6BD89C5` with 0 different pixels on both WGL and ANGLE.

### Measured Windows results

On Windows 11, Ryzen 9 7900, RTX 4070 (OpenGL 4.0):

- 4K sharp-bilinear WGL: GPU p99 0.089 ms, present p99 0.208 ms, 0 renderer/mailbox
  allocations/frame after warm-up.
- 4K sharp-bilinear ANGLE/GLES 3.0: present p99 0.47 ms, 0 renderer allocations.
- 320x240 Accurate parity: pixel-identical to the CPU oracle on both backends.

These are development-machine numbers, not a hardware guarantee. Tier B desktop,
100/150/200% DPI, and macOS/Linux GL launch validation remain open.

---

## 10. Performance notes

- Core cost is independent of output resolution: palette expansion is at native
  resolution, and the output pass is one/few fullscreen quads.
- 4K output is upscaling, not 4K internal CEL rendering. `--high-res` is 640x480
  internal, not 4K.
- The CPU side currently runs at ~1.25-1.6 ms/frame for the synthetic benchmark;
  Road Rash's 3D race measured 5.46 ms/frame total (p50 2.91, p95 10.60, p99 13.14)
  with 0 bytes/frame.
- xBR/HQx add CPU work proportional to the native frame; HQx 4x is the most expensive.
- FSR adds two GPU passes; its 4K WGL and Vulkan budgets are recorded above.
- The CPU fallback uses `WriteableBitmap` and is not a 4K60 path.

See [`Performance.md`](Performance.md) for the full budget and methodology.

---

## 11. Tests

| Test | Covers |
|---|---|
| `VdlpTests` | Display-list parsing, bitmap widths, line duplication, high-res. |
| `FrameAssemblerTests` | Background/fixed-CLUT/per-line CLUT, BGRA/RGBA. |
| `GlRendererContractTests` | `GpuPalette` vs CPU assembler, viewport aspect/integer/custom/stretch, renderer contract/settings. |
| `VdlFrameMailboxTests` | Snapshot copy, sequence, no retention. |
| `VideoAllocationTests` | Zero-allocation uploader/mailbox steady state. |
| `OffscreenRenderTargetTests` | Offscreen probe target creation. |
| `ShaderSourceTests` | GLSL 330/ES 300 sources, uniforms, FSR/CRT strings. |
| `HqxFilterTests` | 2x/3x/4x reference hashes, uniform image, allocation. |
| `OpenGlCapabilitiesTests` | Version parsing, API, integer textures, vsync. |
| `SwapIntervalTests` | WGL/GLX/EGL selection and return conventions. |
| `GlRendererContractTests` | Renderer contract, settings validation. |
| `GpuProbeIntegrationTests` (opt-in `FIVEDO_GPU_TESTS=1`) | Real WGL/ANGLE 320x240 parity and 4K pass. |
| `VulkanShaderTests` | All six generated SPIR-V modules and the `RendererApi.Vulkan` capability contract. |
| `VulkanRendererTests` (opt-in `FIVEDO_VULKAN_TESTS=1`) | CPU parity, fixed/background CLUTs, source/output size changes, xBR/FSR/HQx split-frame orientation. |
| `VulkanProbeIntegrationTests` (opt-in `FIVEDO_VULKAN_TESTS=1`) | Real Vulkan 320x240 parity, a 4K probe pass, and 640x480 xBR/FSR/HQx passes. |
| `VkProbeOptionsTests` | Vulkan probe option parsing and defaults. |
| `Direct3D11ShaderTests` | All six HLSL modules compile for shader model 5.0. |
| `Direct3D11RendererTests` (opt-in `FIVEDO_D3D_TESTS=1`) | CPU parity, source/output size changes, letterboxing, xBR/FSR/HQx orientation. |
| `D3dProbeIntegrationTests` (opt-in `FIVEDO_D3D_TESTS=1`) | Real probe parity report. |
| `D3dProbeOptionsTests` | Direct3D probe option parsing and defaults. |

---

## 12. Limitations

- GLES 3.1, Tier B desktop timing, and DPI validation are not complete. Raspberry Pi is out of scope.
- Linux/macOS OpenGL launch validation is not complete.
- The Avalonia compositor may pace presentation independently of the requested vsync.
- The GPU probe's residual ~2.3 KB/frame allocation is outside
  `OpenGlRenderer.Present` and comes from the Avalonia UI/compositor loop; the
  renderer/mailbox path is zero-allocation after warm-up.
- HQx 3x/4x are CPU-heavy. xBR now uses the GPU palette texture in both backends.
- FSR's 4K WGL and Vulkan budgets are recorded above; it remains opt-in.
- The Vulkan backend is offscreen and readback-based. It has no swapchain/window-system
  surface and no vsync; HQx uses the shared CPU expansion and is CPU-bound. OpenGL
  remains the required desktop presenter. Vulkan device/driver validation outside the
  development machine is pending.
- The Direct3D 11 backend is Windows-only for desktop presentation. It is optional and
  OpenGL remains the default; non-Windows settings normalize to OpenGL. Device removal
  falls back through OpenGL to the CPU presenter.
- No GPU CEL compositing; CEL rendering remains software at native resolution.
- No PAL video modes; high-resolution is the opt-in 640x480 mode, not a general 4K
  internal renderer.
- Custom aspect ratio is represented in `RendererSettings` but not edited by the
  current Settings dialog.
