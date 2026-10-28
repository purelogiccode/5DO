# Video and the OpenGL Presenter

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
| Filters | `HqxFilter.cs`, `HqxPatterns*.cs`, xBR/FSR shader bodies |
| Capabilities/vsync | `OpenGlCapabilities.cs`, `SwapInterval.cs` |
| Avalonia surface | `src/FiveDo.Frontend.Avalonia/Controls/VideoSurface.cs` |
| GPU probe | `src/FiveDo.Frontend.Avalonia/GlProbeWindow.cs`, `GlProbeOptions.cs` |

---

## 1. Resolution and frame model

The 3DO renders at **320x240** (NTSC, 4:3) in the default mode. Wider VDLP bitmap
modes are parsed as **384, 512, 640, or 1024** pixels per line. The opt-in
**high-resolution** mode (`--high-res`, Settings > Emulation, or
`EmulatorTuning.HighResolution`) renders CELs and bitmap lines at **640x480**.

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
| `Xbr` | CPU palette expansion, upload, xBR-lv2 4x edge-directed pass, sharp-bilinear finish. | Opt-in; 1280x960 intermediate for 320x240; expensive at 4K. |
| `Hqx` | CPU palette expansion, HQx 2x, upload, sharp-bilinear finish. | Opt-in; CPU cost ~2.3 ms/frame on checkerboard, ~12.1 ms on noise (320x240). |
| `Hqx3x` | HQx 3x, same finish. | ~4.2 ms checkerboard, ~13.4 ms noise. |
| `Hqx4x` | HQx 4x, same finish. | ~7.5 ms checkerboard, ~14.8 ms noise; most expensive. |
| `Fsr` | FSR 1.0 EASU upscale to viewport, then RCAS sharpen to output. | Opt-in; two GPU passes; sharpness slider. |

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

`Xbr` expands the palette on the CPU at native resolution, uploads BGRA to a texture,
runs the xBR-lv2 4x edge-directed shader into a 4x offscreen target, then finishes with
sharp-bilinear. The CPU expansion is deliberate: a driver readback issue with the
FBO-attached palette texture made the GPU palette path unreliable for this filter. If
the 4x target exceeds `MaxTextureSize`, the renderer falls back to the normal
sharp-bilinear path instead of failing.

### HQx

`HqxFilter` is a CPU port of Maxim Stepin's hq2x/hq3x/hq4x (via Cameron Zemek's C
port, LGPL-2.1-or-later). It consumes the CPU-expanded BGRA frame, so palette expansion
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

FSR is presentation-only and does not feed back into VRAM or emulation. No separate
4K FSR budget measurement is recorded yet; it is opt-in and costs two GPU passes.

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

---

## 6. Backends, capabilities, and fallback

| Backend | API / shader | Platforms | Notes |
|---|---|---|---|
| Desktop GL | OpenGL 3.3 core / GLSL 330 | Windows WGL, Linux GLX/EGL, macOS 4.1 core | Primary desktop path; `R16UI`, `usampler2D`, `texelFetch`. |
| GLES | OpenGL ES 3.0/3.1 / GLSL ES 300 | Windows ANGLE, Raspberry Pi 4/5, other GLES | Same two-stage pipeline. |
| CPU | `WriteableBitmap` + `FrameAssembler` | Any | Fallback when no usable GL/GLES context; accuracy oracle. |

On Windows, `--gl-backend auto|wgl|angle|software` selects the Avalonia rendering mode:

- `auto` (default): WGL, then ANGLE/EGL, then software.
- `wgl`: force desktop OpenGL.
- `angle`: force ANGLE/GLES.
- `software`: force Avalonia software rendering (no GL surface).

`GlBackendOption` also accepts aliases: `default`, `desktop`, `gl`, `angleegl`, `egl`,
`gles`, `cpu`. The option is wired through `Win32PlatformOptions.RenderingMode`; on
non-Windows platforms the platform's default context selection applies.

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

**Screenshot** writes a PNG from the OpenGL output when available, otherwise from the
CPU frame:

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

## 9. GPU probe

The probe is a developer/validation tool that opens the real Avalonia GL surface,
presents synthetic 320x240 VDL frames, and writes JSON. It never loads a BIOS or disc.

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

These are development-machine numbers, not a hardware guarantee. Tier B (Raspberry Pi
4/5, GLES 3.1), 100/150/200% DPI, and macOS/Linux GL launch validation remain open.

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
- FSR adds two GPU passes; no separate 4K budget is recorded yet.
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

---

## 12. Limitations

- Raspberry Pi 4/5 GLES 3.1, Tier B timing, and DPI validation are not complete.
- Linux/macOS OpenGL launch validation is not complete.
- The Avalonia compositor may pace presentation independently of the requested vsync.
- The GPU probe's residual ~2.3 KB/frame allocation is outside
  `OpenGlRenderer.Present` and comes from the Avalonia UI/compositor loop; the
  renderer/mailbox path is zero-allocation after warm-up.
- HQx 3x/4x are CPU-heavy; xBR uses a CPU palette expansion for driver compatibility.
- FSR has no recorded 4K budget measurement yet.
- No GPU CEL compositing; CEL rendering remains software at native resolution.
- No PAL video modes; high-resolution is the opt-in 640x480 mode, not a general 4K
  internal renderer.
- Custom aspect ratio is represented in `RendererSettings` but not edited by the
  current Settings dialog.
