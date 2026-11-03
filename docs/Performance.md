# Performance and 4K60 Strategy

**Status:** target definition and implementation baseline (2026-10-31). The desktop
path now has a dedicated audio-paced core thread (60 Hz fallback), a two-stage palette/scale presenter,
immutable texture storage with per-frame `TexSubImage2D`, cached uniform locations,
full sharp-bilinear and CRT scanline/aperture/curvature/vignette filters, opt-in
xBR/HQx/FSR filters, an opt-in bounded disc
prefetch cache, a platform swap-interval path, and a real benchmark harness with
p50/p95/p99 and per-stage allocations. CPU-side frame cost is ~1.25-1.6 ms, leaving
~15 ms of headroom at 60 Hz; the synthetic steady state is allocation-free. The GPU
probe now records true 4K offscreen timings: sharp-bilinear GPU p99 0.089 ms and
present p99 0.208 ms on the RTX 4070 development machine. Manual DPI validation, the
GLES path, and Tier B timing remain open; see §9 and M9P in
[`ImplementationPlan.md`](ImplementationPlan.md). The overall project position is
summarized in [`Status.md`](Status.md).

**Feasibility assessment:** desktop 4K60 is high confidence because the CPU has
~15 ms of headroom and the 4K pass is one fullscreen textured quad after the
native-resolution palette expansion. Tier B desktop 1080p60 is plausible but
unmeasured. The remaining risk is frame pacing, the Avalonia/WGL compositor, and p99
spikes, not raw GPU fill rate. 4K internal CEL rendering is not a v1 goal.

This document is the performance contract for the emulator. It defines what "4K60"
means, the frame budget, the presentation pipeline, the supported renderer backends,
and how performance is measured. Accuracy remains the first requirement: an
optimization is accepted only when golden output is unchanged.

---

## 1. What "4K 60 fps" means

The v1 target is **3840x2160 output at 60 Hz** while emulating the 3DO at its native
320x240 (or wider VDLP) resolution. It is **not** 4K internal CEL rendering.

- **Output resolution:** the swapchain/control surface is 4K and the GPU performs
  the final upscale/filter.
- **Internal resolution:** CEL rendering and VRAM remain at the emulated resolution.
  `RESSCALE=1` (high-resolution CEL) is an opt-in 640x480 feature; it is not part of
  the default 4K target.
- **4K internal rendering is not a v1 goal.** It would require GPU CEL compositing or
  a fundamentally different renderer and is tracked only as an experiment.

The default 4K path must use nearest or sharp-bilinear. CRT and xBR/HQx are opt-in;
they may require a faster GPU and must show a performance warning at 4K.

### Hardware tiers

| Tier | Example hardware | Target |
|---|---|---|
| A | Mid-range x64 desktop/laptop from ~2020 or newer (GTX 1650 / RX 6400 / Iris Xe / Apple M1 class) | 4K60 with nearest/sharp-bilinear; 1080p60 with CRT/xBR |
| B | Integrated x64 GPUs and older desktop CPUs | 1080p60 with nearest/sharp-bilinear |
| C | Older/low-power devices | Native 320x240 or 720p60; CPU fallback allowed |

Tier B requires the OpenGL ES 3.0/3.1 path described in §6. Raspberry Pi is out of
scope; the GLES path remains for Windows ANGLE and other desktop GLES contexts.

---

## 2. Frame budget

At 60 Hz one frame is **16.67 ms**. Budgets are wall-clock time on Tier A unless
stated otherwise:

| Stage | Budget | Notes |
|---|---:|---|
| Core emulation (CPU + memory + MADAM/CEL + DSP + VDLP + CD) | ≤ 8 ms p99 | Target ≤ 6 ms p50; must be independent of output resolution |
| Presenter CPU (mailbox copy, upload, draw submission) | ≤ 2 ms p99 | No per-frame allocations |
| GPU filter/upscale at 4K | ≤ 8 ms | Nearest/sharp-bilinear; CRT/xBR may exceed this |
| Total frame | ≤ 16.67 ms p99 | No persistent overrun after warm-up |
| Core on Tier B desktop | ≤ 12 ms p99 | 1080p output; CHD streaming must not stall |
| DSP | ≤ 1 ms/frame | 44.1 kHz stereo |
| Indexed upload | ≤ 0.5 ms CPU | ~175 KB/frame at 320x240 |
| Allocations | 0 bytes/frame | Core and presenter, steady state |
| Managed heap | < 150 MB resident | Including GL textures and caches |
| Startup to first frame | < 1 s | After BIOS/disc validation |
| Audio latency | ≤ 100 ms typical | 8192-frame ring + eight variable-size OpenAL buffers with a 3072-frame prefill; current steady state is ~113 ms (the crack-free margin), the old 3-buffer/4096-ring config was ~89 ms |

The core cost must not grow with output resolution. A 4K window must cost the same
CPU time as a 1080p window apart from the presenter's final draw call.

---

## 3. Presentation pipeline

The presenter must use **two stages**, not one output-resolution shader:

```text
VdlFrame (indexed, 320x240 typical)
        │
        ▼
Stage 1: palette expansion at native resolution
  - R16UI indexed pixels, per-line CLUT, per-line background/out-control
  - fragment shader writes an RGB8 (or RGBA8) texture of size 320x240
  - ~76,800 pixels/frame, independent of window size
        │
        ▼
Stage 2: scaling/filter at output resolution
  - nearest / sharp-bilinear / CRT / xBR / HQx / FSR
  - samples the expanded RGB texture into the 4K framebuffer
  - ~8.3M pixels/frame at 4K
  - xBR adds a 4x offscreen pass (1280x960 for 320x240 sources) over the
    GPU-expanded native frame, then finishes with sharp-bilinear. It is opt-in;
    benchmark it at 4K before enabling by default.
  - HQx 2x/3x/4x adds a CPU expansion and a larger intermediate texture.
  - FSR 1.0 adds two GPU passes: EASU to the viewport, then RCAS to the output.
        │
        ▼
Window / fullscreen
```

Why this matters: a single-pass shader at 4K performs palette lookup, CLUT fetch,
background/fixed-CLUT decisions, and control-word fetch for every one of the 8.3M
output pixels. That is ~108x more palette work than expanding the native 320x240
frame once. The two-stage design keeps the expensive per-emulated-pixel work at
native resolution and leaves only a cheap texture-filtering pass at 4K.

### Upload rules

- Allocate textures once with immutable storage (`TexStorage2D`) sized to the maximum
  supported source (1280x240), then update with `TexSubImage2D` each frame.
- Do **not** call `TexImage2D` per frame; it reallocates storage and can stall the
  driver.
- Use a small PBO ring or persistent mapped buffer when available; fall back to
  `TexSubImage2D` with orphaning. The upload is only ~175 KB/frame, so the goal is
  avoiding driver reallocation, not raw bandwidth.
- Cache uniform locations, texture units, VAO, and sampler bindings. Do not call
  `GetUniformLocation` or build strings in `Present`.
- Do not allocate delegates, strings, arrays, or LINQ iterators in the per-frame
  path. Cache the renderer callback used by the frame mailbox.

Implemented in `OpenGlRenderer` (desktop GL 3.3): `TexStorage2D` for the indexed,
CLUT, and parameter textures; `TexSubImage2D` per frame; cached uniform locations;
separate palette and scale programs; and a native-resolution RGBA FBO for stage 1.
`VdlFrameMailbox` copies only the visible line width, and `Read` copies into a
caller-owned frame so the mailbox lock is not held during GPU submission.
`VdlFrameUploader` uses span copies instead of per-pixel loops. The GLES shader
variant is selected from the context version; real-hardware validation remains.

---

## 4. Core performance strategy

The core remains **single-threaded and deterministic**. Parallelizing CPU/CEL/DSP
would complicate determinism and is not required: the ARM60 runs at ~12.5 MHz and
the native CEL workload is ~77k pixels/frame.

Priority order:

1. **Measure first.** Use the benchmark harness (§8) and real discs from
   [`TestCorpus.md`](TestCorpus.md). Profile CPU, CEL, DSP, VDLP, and CHD separately.
2. **Interpreter before JIT.** Optimize `Arm60.Step`: fast RAM path, decode cache or
   direct dispatch, fewer interface calls per instruction, no per-instruction
   allocations. A tuned interpreter is likely sufficient at 12.5 MHz.
3. **CEL inner loops.** Keep the ported per-pixel math, but use `Span<T>`,
   `Unsafe`/`MemoryMarshal`, bounds-check elimination, and precomputed tables where
   measurements justify it. Keep golden VRAM hashes unchanged.
4. **DSP.** Keep the precomputed tables and struct-based instruction decode; ensure
   `Loop()` has no hidden allocations and benchmark samples/second.
5. **Optional guest JIT (M12).** Only if profiling shows CPU interpretation is a
   top-three cost. It must be bit-identical and disabled by default.

Do not add GPU CEL compositing for the 4K output target. Software CEL at native
resolution is the accuracy path. GPU CEL is only relevant to 4K *internal* rendering,
which is post-v1.

---

## 5. Threading and frame pacing

The desktop frontend now uses `FiveDo.Host.CoreRunner`: a dedicated core thread
paced by the audio clock with a hybrid sleep/spin pacer and a low-rate (4 Hz)
status sink. `AudioFramePacer` averages 30 frames of `AudioSamplesProduced` deltas
and sets the period to `samples / 44100` seconds; it falls back to 60 Hz when the
delta is zero or implausible. With the exact DSP clock the nominal period remains
16.67 ms. UI actions that touch the machine (reset, save/load, disc changes,
screenshots) are marshalled through `CoreRunner.Invoke`, which pauses the loop for
the duration of the action. The Avalonia control renders only when the mailbox
sequence or the output size changes.

Target model:

- **Core thread:** `CoreRunner` runs `RunFrame` using its private `FramePacer` and a
  high-resolution timer; the period comes from `AudioFramePacer` and falls back to
  60 Hz. No wall-clock reads inside emulated state.
- **Render/UI thread:** consumes the latest completed frame from a lock-free or
  triple-buffered mailbox and submits GPU work. It never calls `RunFrame`.
- **Audio thread:** consumes the SPSC ring buffer; never calls into the core. It
  makes the OpenAL context current on its own thread.
- **Frame pacing:** audio-derived (44.1 kHz) with a 60 Hz fallback; a stall longer
  than 100 ms resyncs the pacer; skip presentation (not emulation) if the renderer is
  late. The current live configuration uses an 8192-frame ring, eight variable-size
  buffers, and a 3072-frame prefill (~113 ms steady-state latency); the earlier
  3-buffer/4096-ring config was ~89 ms but underran.
- **Mailbox:** latest frame wins. Do not queue frames. Avoid holding a lock while
  submitting GPU work; use a seqlock/triple buffer or copy only the visible pixels.
- **Render on demand:** request a new render only when a new frame is available (or
  when the window/settings change). Do not re-request rendering unconditionally at
  the end of every render, or a 144 Hz display will render a 60 fps emulator 144
  times per second.
- **UI updates:** status text/FPS overlays update at 4–10 Hz, not every frame. Never
  format a string or trigger layout in the per-frame path.
- **DPI:** size the GL surface in **physical pixels** (`Bounds * RenderScaling` or
  the platform pixel size), not Avalonia logical units. A 4K display at 150–200%
  scaling must still render 3840x2160, not 1920x1080.

### Frame skipping

`CoreRunner.FrameSkip` (`FrameSkipMode.Off|Auto|OneInTwo|OneInThree|OneInFour|OneInFive`)
suppresses **presentation only**. Every frame still calls
`FiveDoMachine.RunFrame(skipFrame: true)`, which runs the ARM CPU, CEL engine, DSP,
timers, CD-ROM, CLIO, and PBUS input. The skipped flag suppresses only
`Vdlp.DoLine` and the `FrameSink.OnFrame` callback; `FrameSink.OnFrameTrigger` still
fires so a multitask sink sees the frame boundary. Game speed, audio, input, and save
states are therefore identical to a full-rate run.

`Auto` uses actual work per presented frame, not the pacer's schedule offset. A startup
stall leaves a constant offset but is not a reason to drop frames, so Auto compares the
measured per-frame work against the audio-derived period:

- three consecutive presented frames above 1.05x the period increase the cadence, up
  to one-in-three (level 2 for Auto);
- 120 presented frames below 0.90x the period decrease it again;
- fixed modes use the selected 2-5 cadence immediately.

The Avalonia surface also coalesces core-thread render requests: a mailbox sequence
already covers the latest frame, so at most one UI callback is pending at a time. This
keeps a slow UI thread from building a dispatcher backlog without dropping emulated
frames.

---

## 6. Renderer backend matrix

Avalonia's Windows default rendering mode is ANGLE (OpenGL ES), with software as a
fallback. A desktop `#version 330 core` shader is therefore not portable by default,
and Raspberry Pi 4 exposes OpenGL ES 3.1 rather than desktop GL 3.3.

The renderer must support two shader/API variants behind one contract:

| Backend | API / shader | Platforms | Notes |
|---|---|---|---|
| Desktop GL | OpenGL 3.3 core, GLSL 330 | Windows via WGL, Linux GLX/EGL, macOS 4.1 core | Primary desktop path; integer textures and `texelFetch`. Validated on Windows 11 / RTX 4070 (OpenGL 4.0) with `--gl-backend wgl`. |
| GLES | OpenGL ES 3.0/3.1, GLSL ES 300 | Windows ANGLE, other desktop GLES contexts | Same two-stage pipeline; `R16UI` + `usampler2D`. Validated on Windows 11 / RTX 4070 via ANGLE (OpenGL ES 3.0) with `--gl-backend angle`. |
| Vulkan (optional) | Vulkan 1.0, SPIR-V | Windows/Linux/macOS with a Vulkan loader | Offscreen two-stage pipeline with optional CPU readback and `--vk-probe`; a native Win32 swapchain probe (`--vk-window-probe`) presents through FIFO vsync. 4K sharp-bilinear measured 0.24 ms mean / 1.06 ms p99 present and 0.075/0.091 ms GPU, 4K xBR 0.32/0.80 ms present and 0.13/0.14 ms GPU, 4K FSR 0.60/1.30 ms present and 0.40/0.42 ms GPU, and 4K HQx 2x 6.82/10.93 ms (CPU-bound), all with 0 managed bytes/frame on the RTX 4070. The 4K window probe measures 0.077/0.088 ms GPU and 5.96/6.53 ms present including the 165 Hz FIFO wait. The desktop `--gl-backend vulkan` path hosts the swapchain in a native Win32 child window inside the Avalonia window, so it does not copy the frame back; non-Windows platforms fall back to offscreen/readback. |

Implementation requirements:

- Detect `GlVersion.Type` (desktop GL vs GL ES) and version at context creation.
- Select the matching shader source and the matching Silk.NET binding
  (`Silk.NET.OpenGL` vs `Silk.NET.OpenGLES`); keep the renderer logic shared.
- On Windows, either configure `Win32PlatformOptions.RenderingMode` to prefer
  `Wgl` with `AngleEgl` fallback, or implement the GLES path. Do not assume the
  default ANGLE context accepts GLSL 330. The frontend's `--gl-backend
  auto|wgl|angle|software` option forces a backend for validation; the default
  remains `auto` (WGL, then ANGLE, then software).
- Keep `RendererCapabilities` honest: `SupportsIntegerTextures` and
  `SupportsOpenGl33` are not the same thing. The GLES path should report the actual
  API/version and fall back to CPU only when integer textures/`texelFetch` are
  unavailable.
- GPU compute (for a future GPU CEL experiment) requires GL 4.3+ or Vulkan/Metal.
  It is not part of the GL 3.3/GLES 3.1 v1 baseline.

The CPU `FrameAssembler` remains the accuracy oracle and fallback. The CPU fallback
is not expected to hit 4K60; it exists for compatibility and headless tests.

---

## 7. CHD and disc I/O

CHDSharp decoding is pure managed code and can be CPU-heavy on Tier B. The core
thread must not block on a multi-hundred-millisecond decompression during gameplay.

- Keep emulated CD timing deterministic; never read wall-clock time inside the core.
- `PrefetchingDiscSource` provides a bounded host-side worker/cache for CHD/ISO
  reads. It is opt-in via `--prefetch`; the core consumes sectors at emulated time,
  and a cache miss blocks briefly without changing emulated state.
- CHD vs ISO streaming is benchmarked on x64; record Tier B p99 frame time next.
- Keep the existing read-ahead configuration and validate it against real discs in
  [`TestCorpus.md`](TestCorpus.md).

---

## 8. Measurement

### Microbenchmarks

Use `benchmarks/FiveDo.Benchmarks` (stopwatch harness; BenchmarkDotNet can be added
later for microbenchmarks):

- `Arm60.Step` on a mixed instruction stream (MIPS).
- `CelEngine.HandleCel` on representative packed/literal/LR scenes (pixels/ms).
- `Dsp.Loop` (samples/second).
- `FiveDoMachine.RunFrame` on a synthetic scene (ms/frame, allocations/frame).
- `FrameAssembler` (MB/s).
- `AudioRingBuffer` (samples/second, allocations).
- `VdlFrameUploader`/presenter CPU path (ms/frame, allocations).
- `OpenGlRenderer` upload/present (MB/s, CPU ms; GPU time via timer queries).

### Measured baseline (2026-10-25)

`benchmarks/FiveDo.Benchmarks` runs a synthetic machine by default and accepts
`--bios`/`--disc` for a real title, plus `--high-res` and `--prefetch`. Release
build, development machine (x64), 600 frames:

| Case | Total ms/frame | p50 | p95 | p99 | Allocations |
|---|---:|---:|---:|---:|---:|
| Synthetic | **1.250** | 1.218 | 1.434 | 1.638 | **0 bytes/frame** |
| Lemmings CHD | **1.281** | 1.088 | 2.005 | 3.398 | ~5.9 KB/frame (CHD) |
| Lemmings CHD, high-res | **1.605** | 1.398 | 2.497 | 4.598 | ~5.8 KB/frame (CHD) |

The synthetic run reports ~172 MIPS; the Lemmings run reports ~43 MIPS because the
guest spends time in VDLP/CEL and idle paths. CPU-side headroom against the
16.67 ms budget is ~15.4 ms. The read copy is deliberately performed outside the
mailbox lock so GPU submission cannot stall the core thread. CHD decode allocations
are host-side and attributed to the core stage; the synthetic core/pack/mailbox/read
path is allocation-free in steady state. These numbers are **CPU only**; the 4K GPU
stage is measured separately by the `--gl-probe` below. The benchmark is not a
substitute for a Tier B percentile run.

**Road Rash 3D race (2026-10-28).** The state-driven benchmark loads the archived
100k-frame race state, warms up 120 frames, then measures 600 frames of the actual
3D race with the BIOS and disc inserted. Command:

```powershell
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 600 --warmup 120 --bios bios/panafz1.bin `
  --disc "J:\Panasonic 3DO\Road Rash (USA).chd" `
  --state-in artifacts/rr-race2-100k.state --label road-rash-race --json
```

| Case | Total ms/frame | p50 | p95 | p99 | Allocations |
|---|---:|---:|---:|---:|---:|
| Road Rash 3D race (before hot-path pass) | **5.46** | 2.91 | 10.60 | 13.14 | **0 bytes/frame** |
| Road Rash 3D race (current) | **4.20** | 2.15 | 7.13 | 7.40 | **0 bytes/frame** |

Core-only (current): 4.16 ms mean, p50 2.12, p95 7.09, p99 7.32, max 8.10; 240 fps
total, 13.9 MIPS, 57,693 instructions/frame, 208,344 cycles/frame. The
pack/mailbox/read stages cost ~17/8/9 µs/frame. This is the heaviest real-content
scene measured so far and remains inside the 16.67 ms frame budget at p99 with
~9.3 ms of headroom. The hot-path pass cached per-CCB CEL registers, replaced
per-pixel address multiplies with row-span counters, removed the per-texel PLUT
length check, and added fast DSP instruction-memory paths. The frame hash is
unchanged (`0x78A1706F`) and managed allocations remain zero. The old profile is
kept as the before measurement. The synthetic `--gate` remains the CI budget check;
this race profile is a reference measurement, not a gate input.

**Debug builds (2026-10-31).** The core projects are compiled with JIT
optimizations in Debug (`Directory.Build.props`). Before this change, a Debug build
of the same Road Rash race measured 24.98 ms/frame (40 fps) on the reference
machine; after it, the Debug build measures 4.30 ms/frame, matching Release within
noise. The frontend/UI projects remain unoptimized for normal XAML debugging. If a
debugger is configured to suppress JIT optimization on module load, disable that
option or use Start Without Debugging for gameplay.

**GPU timer query (2026-10-25).** The desktop renderer now wraps the palette/xBR/scale
pipeline in a `GL_TIME_ELAPSED` query on desktop GL. On the Windows development machine
(OpenGL 4.0, 1500x978 output, BIOS boot, sharp-bilinear), the overlay reported
**gpu ≈ 0.13 ms** and **present ≈ 0.16 ms** per frame. This is a small native-resolution
scene, not a 4K measurement; it validates that the timing path works and that the
presenter is far below the 2 ms CPU budget at this size.

**GPU presentation probe (2026-10-26).** The frontend now has a developer probe that
opens the real Avalonia OpenGL surface, presents synthetic 320x240 VDL frames, and
writes per-frame present/GPU timings as JSON. It supports a true 4K offscreen target
so the measurement is independent of the desktop resolution, and a warm-up phase so
one-time shader/FBO allocations are excluded. Command:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-probe artifacts/gl-probe/4k-sharp.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen
```

Measured on Windows 11, Ryzen 9 7900, NVIDIA RTX 4070, OpenGL 4.0 (offscreen 4K
unless noted). Sharp-bilinear/accurate/CRT rows use 120 measured frames after 30
warm-up frames; the xBR/FSR rows use 60 after 15.

| Case | Output | Present mean | Present p99 | GPU mean | GPU p99 | Renderer alloc |
|---|---:|---:|---:|---:|---:|---:|
| Sharp-bilinear | 3840x2160 | **0.080 ms** | **0.208 ms** | **0.086 ms** | **0.089 ms** | **0 B/frame** |
| Accurate (nearest) | 3840x2160 | 0.082 ms | 0.178 ms | 0.091 ms | 0.287 ms | 0 B/frame |
| CRT | 3840x2160 | 0.147 ms | 1.157 ms | 0.191 ms | 0.193 ms | 0 B/frame |
| xBR 4x | 3840x2160 | 0.283 ms | 1.466 ms | 0.185 ms | 0.324 ms | 0 B/frame |
| FSR (EASU + RCAS) | 3840x2160 | 0.132 ms | 1.191 ms | 0.441 ms | 0.483 ms | 0 B/frame |
| Sharp-bilinear | 1920x1080 | 0.093 ms | 0.335 ms | 0.080 ms | 0.146 ms | 0 B/frame |

The probe reports ~64 fps throughput even with vsync disabled because the Avalonia
compositor paces the window independently of the control's swap interval. The
renderer cost itself is far below the 16.67 ms budget, so a 4K60 presentation is
GPU-headroom-bound, not renderer-bound. The probe also validates the platform
swap-interval path: on Windows WGL it resolves `WGL_EXT_swap_control` and reports
`VsyncActive: true` when requested; the compositor may still pace independently.
`DisplayRefreshRate` (Settings > Video) can re-present the latest frame above 60 Hz
(default 60). It lowers presentation latency but does not add motion, because the
3DO still produces 60 fields per second (50 for PAL).
The renderer/mailbox present path reports **0 managed bytes/frame** after warm-up.
The residual probe allocation (~2.3 KB/frame) is outside `OpenGlRenderer.Present`
and comes from the Avalonia UI/compositor loop; it is a host-side follow-up, not
the upload/present path.

**ANGLE/GLES validation (2026-10-26).** The same probe can force the ANGLE/EGL
backend and select the GLSL ES 300 shader variant. On the same Windows 11 / RTX 4070
machine, ANGLE reported `Google Inc. (NVIDIA)` / Direct3D 11, OpenGL ES 3.0,
integer textures, and `EGL_KHR_swap_interval`:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend angle `
  --gl-probe artifacts/gl-probe/angle-4k-sharp.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen
```

| ANGLE case | Output | Present mean | Present p99 | Renderer alloc |
|---|---:|---:|---:|---:|
| Sharp-bilinear | 3840x2160 | **0.14 ms** | **0.47 ms** | **0 B/frame** |
| Accurate (nearest) | 3840x2160 | 0.15 ms | 0.48 ms | 0 B/frame |
| CRT | 3840x2160 | 0.14 ms | 0.43 ms | 0 B/frame |
| xBR 4x | 3840x2160 | 0.37 ms | 0.83 ms | 0 B/frame |
| HQx 4x (CPU filter) | 3840x2160 | 10.44 ms | 14.22 ms | 0 B/frame |
| Sharp-bilinear | 1920x1080 | 0.13 ms | 0.40 ms | 0 B/frame |
| Sharp-bilinear, windowed | 1280x720 | 0.13 ms | 0.49 ms | 0 B/frame |

ANGLE does not expose `GL_TIME_ELAPSED`, so the GPU timing column is `null`; the
CPU present times and the pixel-parity result below are still measured. The
renderer/mailbox path remains allocation-free after warm-up.

**Optional Vulkan (2026-10-29).** The headless `--vk-probe` runs the
`FiveDo.Video.Vulkan` offscreen backend on the same RTX 4070 (Vulkan 1.4). It waits
for each frame. The per-frame GPU readback is only recorded when `CaptureToFrame` is
enabled; the probe performs a one-shot readback after the timing loop, so GPU time
covers the upload, palette, and filter passes. 60 measured frames after 15 warm-up
frames:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --vk-probe artifacts/vk-probe/4k-sharp.json `
  --vk-probe-size 3840x2160 --vk-probe-frames 60 --vk-probe-warmup 15 `
  --vk-probe-filter sharp-bilinear
```

| Vulkan case | Output | Present mean | Present p99 | GPU mean | GPU p99 | Managed bytes/frame |
|---|---:|---:|---:|---:|---:|---:|
| Sharp-bilinear | 3840x2160 | 0.240 ms | 1.060 ms | 0.075 ms | 0.091 ms | 0 |
| Accurate | 3840x2160 | 0.221 ms | 0.687 ms | 0.071 ms | 0.080 ms | 0 |
| CRT | 3840x2160 | 0.364 ms | 1.015 ms | 0.175 ms | 0.187 ms | 0 |
| xBR | 3840x2160 | 0.316 ms | 0.796 ms | 0.132 ms | 0.143 ms | 0 |
| FSR (EASU + RCAS) | 3840x2160 | 0.604 ms | 1.303 ms | 0.404 ms | 0.415 ms | 0 |
| HQx 2x (CPU-bound) | 3840x2160 | 6.816 ms | 10.934 ms | 0.130 ms | 0.179 ms | 0 |
| Sharp-bilinear | 1920x1080 | 0.169 ms | 0.790 ms | 0.030 ms | 0.040 ms | 0 |
| xBR | 1920x1080 | 0.214 ms | 0.375 ms | 0.085 ms | 0.088 ms | 0 |
| FSR (EASU + RCAS) | 1920x1080 | 0.269 ms | 0.781 ms | 0.116 ms | 0.122 ms | 0 |

Present time includes the per-frame `vkQueueWaitIdle` and is a synchronous end-to-end
number; the OpenGL probe measures asynchronous command submission. The GPU column is
the comparable renderer cost. A 320x240 Accurate probe reports 0 different pixels and
the same `0xD6BD89C5` (`3602745797`) hash as the OpenGL backends. The Vulkan path is
optional and does not replace the measured WGL/ANGLE presenter; see
[`Vulkan.md`](Vulkan.md).

**Optional Vulkan Win32 swapchain (2026-10-29).** `--vk-window-probe` opens a real
window and presents through a FIFO swapchain on the same RTX 4070 (Vulkan 1.4,
165 Hz display). Present includes the acquire/present wait; GPU time is the
comparable rendering cost. 60 measured frames after 15 warm-up frames:

| Window probe case | Output | Present mean | Present p99 | GPU mean | GPU p99 | Managed bytes/frame |
|---|---:|---:|---:|---:|---:|---:|
| Sharp-bilinear, FIFO | 3840x2160 | 5.963 ms | 6.534 ms | 0.077 ms | 0.088 ms | 0 |
| Sharp-bilinear, FIFO | 640x480 | 5.935 ms | 6.811 ms | 0.019 ms | 0.031 ms | 0 |

The probe reports `vsyncActive: true`, `vsyncBackend: "fifo"`, and `offscreen: false`;
a 320x240 Accurate capture still matches the CPU oracle with 0 different pixels. This
validates the native surface/swapchain path but is not yet the Avalonia presentation
path.

**CPU/GPU parity (2026-10-26).** `--gl-probe-capture <path>` writes the presented
frame as a PNG and, for a 1:1 Accurate capture, compares it with the CPU
`FrameAssembler` oracle. A 320x240 capture through both backends produced
**0 different pixels**, CPU/GPU FNV-1a hash `0xD6BD89C5` (`3602745797`), with no
vertical flip:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend wgl --gl-probe artifacts/gl-probe/parity-wgl.json `
  --gl-probe-size 320x240 --gl-probe-filter accurate --gl-probe-offscreen `
  --gl-probe-capture artifacts/gl-probe/parity-wgl.png
```

Repeat with `--gl-backend angle` for the GLES variant. Both runs report
`"Parity": { "Matches": true, "DifferentPixels": 0 }`. This is the first real GL
and GLES parity evidence; the probe is still a manual/self-hosted GPU tool.

**FSR 1.0 (2026-10-28).** FSR is opt-in and runs EASU into a viewport-sized FBO, then
RCAS into the output. It adds two GPU passes and a sharpness uniform. The 4K WGL probe
measures present 0.132 ms mean / 1.191 ms p99 and GPU 0.441/0.483 ms; the optional
Vulkan backend measures present 0.604/1.303 ms and GPU 0.404/0.415 ms (excluding the
optional per-frame readback). Use `--gl-probe-filter fsr` or `--vk-probe-filter fsr` to
reproduce on the target GPU.

The 100/150/200% DPI validation remains open. Headless CI keeps the
CPU renderer and allocation tests.

**HQx CPU cost (2026-10-26).** The opt-in HQx filters run on the CPU at native
resolution before the final scale. On the x64 development machine, 300 iterations of
a 320x240 source took **~2.3 / 4.2 / 7.5 ms/frame** for hq2x/hq3x/hq4x on
flat/checkerboard content and **~12.1 / 13.4 / 14.8 ms/frame** on random-noise
worst-case content (which exercises most of the 256 patterns). Real pixel art is
expected to stay near the flat/checker figure, but HQx is still opt-in and should be
measured per title before enabling it on Tier B. The 640x480 high-res source measured
~49.7 ms/frame for hq2x random noise in the same microbenchmark; HQx is not
recommended with `--high-res` on slow CPUs.

The opt-in prefetch cache (`--prefetch`) produced identical Lemmings state/frame
hash in a 300-frame run. On the x64 development machine it increased short-run p99
and allocations because the storage path is already fast; it is intended for slow
CHD/Tier B storage and remains opt-in until measured there.

Run it with:

```powershell
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- --frames 600
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" `
  --label lemmings

# Same-machine relative regression check: record a baseline, then compare
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 600 --json > baseline.json
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 600 --json --baseline baseline.json --baseline-threshold 0.10
```

`--baseline` compares core p99, total mean, and total p99 against the recorded JSON.
A metric more than `--baseline-threshold` (default 10%) slower fails with exit code 4;
improvements never fail. Use the same machine, power profile, and filter settings for
both runs. The absolute `--gate` budgets still run independently in CI.

### End-to-end benchmark

The CLI `bench` command should run a real BIOS + disc (from the local corpus) and
report:

- Frames, wall time, p50/p95/p99 frame time.
- Core time vs presenter time.
- Allocations per frame and GC counts.
- GPU time and upload bandwidth when a GL context is available.
- Output resolution and filter mode.

Use `GC.GetAllocatedBytesForCurrentThread()` and `BenchmarkDotNet`'s
`MemoryDiagnoser`; use `dotnet-counters`/EventPipe for GC and thread contention.
Profile 1080p and 4K separately to prove core cost is resolution-independent.

### CI policy

- Per-PR: build, unit/integration tests, allocation assertions, CPU-renderer
  parity. Do not gate PRs on noisy GPU timings.
- The CI workflow runs a synthetic benchmark smoke on Ubuntu with `--gate` (0
  bytes/frame, core p99 <= 8 ms, total mean <= 8 ms, total p99 <= 16.667 ms) and
  uploads the JSON baseline as an artifact.
- The same-machine relative check (`--baseline`, default 10%) is available for
  nightly/self-hosted runs where hardware and power state are stable. It is not
  wired to the ephemeral GitHub-hosted runner yet.
- Nightly or self-hosted: BenchmarkDotNet baselines, real-content smoke, 4K GPU
  timing. Fail on a sustained >10% regression or a missed budget after warm-up.
- Store the baseline JSON and the hardware description with each run. A performance
  change needs before/after numbers and identical golden output.

---

## 9. Current gaps (2026-10-23 review)

Resolved in the 2026-10-23 M9P pass:

- Dedicated audio-paced core thread with `CoreRunner`, pause/invoke marshalling, and a
  low-rate status sink.
- Two-stage presenter: native-resolution palette expansion into an FBO, then a
  scale/filter pass at output resolution.
- `TexStorage2D` allocation plus per-frame `TexSubImage2D`; cached uniform locations.
- Render-on-demand: the Avalonia surface renders only when the mailbox sequence or
  the output size changes.
- `VdlFrameMailbox` copies only the visible width; the present delegate is cached.
- Real benchmark harness with synthetic and real-disc modes.

Still open:

1. Physical-pixel sizing from `Bounds * RenderScaling` is implemented; manual
   validation at 100/150/200% DPI and live resize remains.
2. The renderer selects GLSL ES 300 sources when the context reports OpenGL ES.
   The Windows frontend now prefers WGL and can force WGL/ANGLE/software with
   `--gl-backend`. ANGLE/GLES 3.0 is validated on Windows, including pixel parity.
3. The 4K GPU percentile run is recorded for WGL and ANGLE/GLES (see the GPU
   presentation probe above). 100/150/200% DPI validation remains.
4. `PrefetchingDiscSource` is implemented as an opt-in bounded worker/cache, but
   Tier B timing and the default policy are not measured.
5. Real-game compatibility is partial: all five corpus titles boot and render, and
   Wolfenstein, Lemmings, Road Rash, Alone, and Space Ace have scripted gameplay
   evidence, but the Road Rash race is not finished. See
   [`Compatibility.md`](Compatibility.md) and [`TestCorpus.md`](TestCorpus.md).
6. The benchmark reports p50/p95/p99 and per-stage allocations, and CI enforces the
   absolute budget gate plus the zero-allocation assertion. The same-machine
   relative check exists (`--baseline`) but is not yet wired to a stable runner.

These gaps do not invalidate the architecture. They do mean performance work cannot
be deferred to M10: the backend matrix and physical-pixel sizing are structural
changes and belong in M9P before release polish.

---

## 10. Acceptance gates

- [x] 4K60 render cost with nearest/sharp-bilinear on Tier A (RTX 4070: GPU p99
      0.09-0.29 ms, present p99 ≤ 0.34 ms). Tier B 1080p60 remains open.
- [x] Core frame time p99 ≤ 8 ms on Tier A and independent of output resolution.
- [x] Presenter CPU ≤ 2 ms p99; GPU frame within budget for the selected filter.
- [x] Zero managed allocations per frame in the renderer/mailbox present path
      (steady state, GPU probe after warm-up). Core/pack/mailbox/read also report
      0 bytes/frame in the synthetic benchmark.
- [x] Desktop GL 3.3 and GLES 3.0 shader variants compile and render identical
      Accurate-mode output. Validated with `--gl-backend wgl` (OpenGL 4.0/GLSL 330)
      and `--gl-backend angle` (OpenGL ES 3.0/GLSL ES 300): 320x240 Accurate
      captures match the CPU oracle pixel-for-pixel (`0xD6BD89C5`). GLES 3.1/Pi
      hardware remains open.
- [ ] Physical-pixel sizing at 100/150/200% DPI; no logical-pixel downscaling.
- [ ] Render-on-demand and frame pacing verified with a 60 Hz and a 144 Hz display.
- [ ] Real-content smoke and performance results recorded with BIOS, disc, commit,
      and hardware.
