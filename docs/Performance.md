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
  - HQx 2x/3x/4x adds a GPU rule-table pass and a larger intermediate texture.
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
| Vulkan (optional) | Vulkan 1.0, SPIR-V | Windows/Linux/macOS with a Vulkan loader | Offscreen two-stage pipeline with optional CPU readback and `--vk-probe`; a native Win32 swapchain probe (`--vk-window-probe`) presents through FIFO vsync. 4K sharp-bilinear measured 0.24 ms mean / 1.06 ms p99 present and 0.075/0.091 ms GPU, 4K xBR 0.32/0.80 ms present and 0.13/0.14 ms GPU, 4K FSR 0.60/1.30 ms present and 0.40/0.42 ms GPU, and 4K HQx 2x/3x/4x 0.26/0.41, 0.29/0.34, and 0.34/0.52 ms present with 0.10/0.12/0.15 ms GPU, all with 0 managed bytes/frame on the RTX 4070. The 4K window probe measures 0.077/0.088 ms GPU and 5.96/6.53 ms present including the 165 Hz FIFO wait. The desktop `--gl-backend vulkan` path hosts the swapchain in a native Win32 child window inside the Avalonia window, so it does not copy the frame back; non-Windows platforms fall back to offscreen/readback. |

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
- `PrefetchingDiscSource` provides a bounded host-side worker/cache for uncompressed
  image readers. CHD images use CHDSharp's own background hunk read-ahead directly:
  wrapping the CHD reader in a second sector look-ahead let background reads hold the
  inner-reader lock and stall foreground CD reads for several frames. The sector
  worker runs below normal priority and pauses between read-ahead sectors so a
  foreground read can take the lock.
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

> **2026-11-05 refresh:** the synthetic fixture's unconditional branch previously fell
> through into zero-filled ROM, so the old 1.250 ms/172 MIPS row measured
> condition-failed NOPs rather than the intended ALU/store loop. The loop now returns to
> its first instruction. Interleaved A/B runs measure the current interpreter at
> **1.20-1.30 ms/frame** and the current JIT at **0.89-0.99 ms/frame**, with identical
> instructions and frame hash. See §8.4 and §8.6.

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
| HQx 4x (GPU pass) | 3840x2160 | 0.35 ms | 0.55 ms | 0 B/frame |
| Sharp-bilinear | 1920x1080 | 0.13 ms | 0.40 ms | 0 B/frame |
| Sharp-bilinear, windowed | 1280x720 | 0.13 ms | 0.49 ms | 0 B/frame |

ANGLE does not expose `GL_TIME_ELAPSED`, so the GPU timing column is `null`; the
CPU present times and the pixel-parity result below are still measured. The
renderer/mailbox path remains allocation-free after warm-up.

**ANGLE revalidation (2026-11-05).** The same `--gl-backend angle` 4K offscreen probe
reported OpenGL ES 3.0 (`Google Inc. (NVIDIA)`, D3D11), sharp-bilinear present mean
**0.67 ms** and p99 **1.29 ms** over 120 measured frames after 30 warm-up frames, with
**0 B/frame** in the renderer (the residual ~2.4 KB/frame is the Avalonia/compositor loop).
This keeps PERF-06's GLES/ANGLE path validated on current code; real GLES 3.1 hardware and
Tier B remain open.

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
| HQx 2x | 3840x2160 | 0.264 ms | 0.405 ms | 0.099 ms | 0.100 ms | 0 |
| HQx 3x | 3840x2160 | 0.287 ms | 0.336 ms | 0.119 ms | 0.121 ms | 0 |
| HQx 4x | 3840x2160 | 0.344 ms | 0.522 ms | 0.150 ms | 0.153 ms | 0 |
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

**HQx GPU cost (2026-11-07).** The opt-in HQx filters now run as GPU rule-table passes
over the palette texture. On the RTX 4070, 4K hq2x/hq3x/hq4x measured
**0.264/0.287/0.344 ms present** and **0.099/0.119/0.150 ms GPU** with 0 managed
bytes/frame (Vulkan probe). The WGL probe measured 0.229/0.322/0.250 ms present and
0.109/0.132/0.163 ms GPU; Direct3D 11 measured 0.183/0.150/0.140 ms present and
0.086/0.108/0.138 ms GPU. The CPU `HqxFilter` remains the reference oracle and still
matches the 4DO reference hashes; it is no longer in the presentation path.

The opt-in prefetch cache (`--prefetch`) produced identical Lemmings state/frame
hash in a 300-frame run. On the x64 development machine it increased short-run p99
and allocations because the storage path is already fast; it is intended for slow
Tier B storage and remains opt-in until measured there. CHD images bypass the cache
because CHDSharp already performs hunk read-ahead; a 20-second desktop Road Rash run
with that direct path reported 0 audio underruns and no skipped frames.

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

Native platform runtime smoke (2026-11-10) validates CLI/frontend startup on macOS
x64/arm64, Linux arm64, and Windows arm64, but it hides the GL surface; the GPU
measurements below therefore remain open. `FrameAssembler` now exposes 0RGB1555,
RGB565, and XRGB8888 conversion paths for presentation parity, and the shell's void
patterns are composited behind every presenter; the default BGRA path is unchanged.

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

### 8.1 CPU clock compatibility matrix (2026-11-04)

`5do bench --cpu-clock` measures the synthetic machine at the supported multipliers.
Release build, 300 frames, development machine, `--frames 300 --json`:

| Clock | Cycles/frame | Instructions | Wall time | Frames/s | Frame hash |
|---|---:|---:|---:|---:|---|
| 0.5x (6.25 MHz) | 104,176 | 32,294,560 | 0.440 s | 682 | `0x6CDFBDC5` |
| 1x (12.5 MHz, stock) | 208,336 | 64,584,160 | 0.560 s | 536 | `0x6CDFBDC5` |
| 2x (25 MHz) | 416,672 | 129,168,320 | 0.988 s | 304 | `0x6CDFBDC5` |
| 4x (50 MHz) | 833,344 | 258,336,640 | 2.990 s | 100 | `0x6CDFBDC5` |

The hash is identical at every clock, confirming that the clock changes the cycle budget
rather than guest behavior for this synthetic program. Cost scales linearly with the
emulated clock (about 1.7x from 1x to 2x and 3.0x from 2x to 4x), so a 4x clock still
leaves ~10 ms/frame at 60 Hz on this machine but is not a compatibility guarantee. The
supported range is 0.5x-4x; the stock 12.5 MHz default is unchanged. Save states record
the clock in the header and warn when it differs from the running machine.

Per-title clock behavior is a compatibility question, not a speed switch: a title that
relies on a fixed cycle budget for CD/CLIO/CEL polling can time out or run faster at
another clock. Use the stock clock unless a specific title has documented evidence in
[`Compatibility.md`](Compatibility.md).

### 8.2 Portfolio OS SWI HLE benchmark (2026-11-04)

The SWI HLE replaces the guest Portfolio OS fixed-point math handler with the host
routines in `ArmSwiHle`. A synthetic benchmark booted the real `bios/panafz1.bin`,
wrote a loop calling `SWI #0x50008` (`MulMat44Mat44_F16`) 4,096 times into RAM, and
stepped the CPU until the loop returned. The same 4x4 matrix was used for both runs.
Release build, development machine:

| Mode | CPU steps | ARM cycles | Wall time |
|---|---:|---:|---:|
| Guest handler (`--swi-hle` off) | 847,860 | 4,054,999 | 0.025 s |
| Host HLE (`--swi-hle` on) | 24,578 | 65,538 | 0.002 s |

The HLE removes ~97% of the interpreted instructions and ~12x the CPU wall time for this
math-heavy loop. The result matrix is bit-identical for the benchmark inputs.

HLE is intentionally not a timing model: it charges only the SWI instruction, not the
guest handler's ~990 cycles or its register clobbers. Titles that poll cycle-sensitive
hardware around matrix calls can therefore change behavior. In the local collection,
`AutoBahn Tokio` (3,891 HLE calls in 3,000 frames) and `Ballz` (2,880 calls) boot and run
with `--swi-hle`, but their frame hashes differ from the default run; both match again
when the affected SWI is left to the guest. Default-off behavior is unchanged and the
Lemmings differential smoke remains an exact match. Use `--swi-comments` to see which
SWIs a title calls, and prefer per-game profiles over enabling HLE globally.

### 8.3 Per-title CPU clock behavior (2026-11-05)

`run --cpu-clock` is an experimental override, not a compatibility mode. Five
representative titles were run for 3,000 frames at 0.5x, 1x, 2x, and 4x on the
development machine (Release build, `--audio null`). Every title produced a different
frame hash at every non-stock clock because the guest observes the changed cycle budget:

| Title | 0.5x hash | 1x hash (stock) | 2x hash | 4x hash |
|---|---|---|---|---|
| Road Rash (USA) | `0xA76F3CA3` | `0x5E9EE515` | `0x2D0CC941` | `0xA34F62A6` |
| Lemmings (USA) | `0xD1E79432` | `0x9C2E3236` | `0xB1CE635A` | `0x9B1B6AB5` |
| Alone in the Dark (USA) | `0x53F35D1D` | `0xF4AC018A` | `0x175B8FE4` | `0xF81EDFC5` |
| Ballz (World) | `0x010AAC8D` | `0x66240BDD` | `0x580166F1` | `0x0A7D8B13` |
| AutoBahn Tokio (Japan) | `0x61933B4B` | `0x03AD517A` | `0x7EF31876` | `0x4E42604F` |

The 1x hashes match the current golden runs. Two titles also showed a large wall-time
change at high clocks: AutoBahn Tokio took 9.1 s at 1x, 84.4 s at 2x, and 271.7 s at 4x
for the same 3,000 frames, which is consistent with a guest timing loop that spins for a
fixed wall-clock interval. That is a compatibility signal, not a speed win.

**Decision:** the stock 12.5 MHz clock remains the default and the only supported
compatibility clock. Non-stock clocks are available for experiments and per-game
profiles, but no per-title clock override ships in the database. The synthetic benchmark
matrix in §8.1 remains the performance reference because its hashes are clock-invariant.

### 8.4 Managed IL JIT (2026-11-05, PERF-08 Partial)

The `Jit` backend (`--cpu-backend jit`) compiles hot straight-line ARM60 blocks into
`DynamicMethod` delegates. It is an execution strategy behind the existing `ICpuBackend`
contract, not a second CPU model:

- Only data-processing, immediate-offset load/store, and branch instructions are compiled.
  Multiply, swap, multi-register transfers, SWIs, MRS/MSR, mode changes, PC writes, and
  register-specified shifts remain on the interpreter.
- Stores and branches terminate a block. A store terminator returns to the machine so the
  CEL FSM is checked before the next instruction.
- Every compiled instruction is verified against the current code word before it executes.
  A changed first word returns a stale sentinel; a changed later word returns the cycles
  already committed. The next call recompiles or falls back. This keeps self-modifying code,
  DMA, ROM-bank switches, and save-state loads correct.
- The block stops at the frame loop's 16-cycle batch boundary using the machine's current
  `batch` value, so CLIO/CD/DSP/VDLP event timing matches the interpreter.
- Blocks compile only after 8,192 executions. The threshold keeps one-shot boot and
  decompression code on the interpreter and bounds the JIT code footprint.

**Correctness.** The interpreter and JIT were stepped in lockstep for 100,000,000
instructions of Lemmings (USA), with the same PC, CPSR, registers, cycle count, and
instruction count after every block. Full-machine hashes also match:

| Title | Frames | Interpreter hash | JIT hash | Instructions |
|---|---:|---|---|---:|
| Lemmings (USA) | 3,000 | `0x9C2D31F6` | `0x9C2D31F6` | 194,135,071 |
| Road Rash (USA) | 1,000 | `0x41D2AC9A` | `0x41D2AC9A` | 60,055,006 |
| Powers Kingdom (Europe) | 1,000 | `0xDCEB4C05` | `0xDCEB4C05` | 57,545,603 |

**Microbenchmarks (Release, 20M instructions per loop).** The JIT is substantially faster
on the instruction classes it compiles:

| Loop | Interpreter | JIT | Speedup |
|---|---:|---:|---:|
| ALU register chain (9 DP + branch) | 50.3 M/s | 167.8 M/s | 3.3x |
| S-bit ALU chain | 72.4 M/s | 134.4 M/s | 1.9x |
| Compare chain | 68.9 M/s | 151.4 M/s | 2.2x |
| Load loop | 72.9 M/s | 137.4 M/s | 1.9x |
| Store loop | 52.0 M/s | 42.5 M/s | 0.82x |
| Branch-only loop | 131.4 M/s | 106.3 M/s | 0.81x |

**End-to-end boot results.** The frame loop, CEL/VDLP work, and short block boundaries
still dominate, so the JIT is **not yet faster than the interpreter** on real boot code:

| Title | Frames | Interpreter | JIT | Result |
|---|---:|---:|---:|---|
| Lemmings (USA) | 300 | 0.48 s | 0.87 s | 1.8x slower |
| Lemmings (USA) | 3,000 | 4.31 s | 5.04 s | 1.17x slower |
| Road Rash (USA) | 1,000 | 1.40 s | 2.05 s | 1.46x slower |
| Powers Kingdom (Europe) | 1,000 | 1.52 s | 2.13 s | 1.40x slower |

**Decision.** The interpreter remains the default and the accuracy oracle. `Jit` is an
experimental, opt-in backend; it is bit-identical on the evidence above and falls back
safely, but it does not meet the 1.5x end-to-end gate (local boot results are 1.17-1.85x slower). The remaining work is trace or
superblock chaining across conditional branches, helper inlining for `SetZn`/`SetCv`/
condition checks, and reducing JIT code-cache pressure. PERF-08 stays **Partial**.

**Second pass (2026-11-05).** The block compiler now keeps PC, CPSR, operands, and flags in
block locals, continues through RAM stores and conditional-branch fallthroughs, reads/writes
RAM through the `IRam` word accessor, and invalidates a block only when a guest write
overlaps its instruction bytes. Per-instruction memory verification is gone. Compiled R15
operands use the block-local PC, fixing exception/trampoline returns (`MOV LR, PC`).

Because host load drifts by more than the effect size, the numbers below come from an
interleaved same-host A/B (HEAD build vs current build, 600 frames after 200 warm-up, run
alternately):

| Case | Previous JIT (HEAD) | Current JIT | Current interpreter | Result |
|---|---:|---:|---:|---|
| Synthetic ALU/store loop | 1.32-1.36 ms/frame | **0.78 ms/frame** | 1.32-1.36 ms/frame | **1.7x faster than the old JIT** |
| Lemmings (USA) boot | 1.58-2.60 ms/frame | 1.40-1.78 ms/frame | 1.08-1.25 ms/frame | faster than old JIT, ~1.3-1.4x slower than interpreter |

Both builds produced the same frame hash in every paired run (`0x6AD58DC5` synthetic,
`0x2A53D449` on the 600-frame Lemmings checkpoint). A separate 1,000-frame CLI A/B also
matched exactly on Road Rash (USA) `0x41D2A1DA`, Powers Kingdom (Europe) `0xDCE31DC5`, and
Lemmings (USA) `0x110E7DC5`, with identical instruction and cycle counts. The synthetic
fixture's branch previously fell through into zero-filled ROM, so it measured
condition-failed NOPs; the loop now returns to its first instruction. The JIT still loses on
real code because the 16-cycle event batch forces roughly 20,000 block entries per frame;
each entry pays the prologue, cache lookup, delegate call, and flush. A self-loop chaining
experiment reduced synthetic block calls by about a quarter but did not improve Lemmings and
was reverted. The next step is a batched trace that keeps the block locals across event
callbacks, not more per-instruction optimization.

**Third pass (2026-11-06).** Compiled blocks now share a PC/CPSR/cycle/instruction context
through `ref` parameters, so a chain of blocks inside one 16-cycle batch no longer reloads
and flushes the architectural state at every block boundary. The FIQ exit stores the
post-vector context, which fixes a frame-loop rewind to the interrupted PC. Register-specified
data-processing shifts (`LSL`/`LSR`/`ASR`/`ROR` by `Rs`) are compiled with the interpreter's
extra internal cycle, and a new parity test covers register shifts including counts above
32. The compile-hit counter is now an 8,192-entry direct-mapped table; the old dictionary
could be cleared by cold code and keep a hot loop below the threshold. The 4,096-hit
threshold remains the best measured point (2,048 and 8,192 were both slower).

Interleaved same-host A/B, 600 frames after 200 warm-up:

| Case | Previous JIT (HEAD) | Current JIT | Current interpreter | Result |
|---|---:|---:|---:|---|
| Synthetic ALU/store loop | 1.60-1.66 ms/frame | **0.89-0.99 ms/frame** | 1.20-1.30 ms/frame | **~1.3x faster than the interpreter** |
| Lemmings (USA) boot | 1.60-1.66 ms/frame | **1.23-1.37 ms/frame** | 1.00-1.02 ms/frame | ~20% faster than the old JIT, still ~1.25x slower than the interpreter |

The 600-frame Lemmings checkpoint hash is unchanged (`0x2A53D449`). A separate 500-frame CLI
A/B also matched exactly on Road Rash (USA), Powers Kingdom (Europe), and Lemmings (USA),
with identical instruction and cycle counts. The JIT still loses on real code because about
24% of executed instructions fall back to the interpreter and the average compiled block
covers only ~2.5 instructions; the 16-cycle event boundary and cold-code fallbacks dominate
the remaining gap. The interpreter remains the default and the accuracy oracle. PERF-08
stays **Partial**.

### 8.5 Rewind capture stall (2026-11-05)

**Symptom.** Gameplay showed sub-second freezes every two seconds. The rewind history was
enabled by default (`RewindOptions.Enabled = true`, keyframe every 120 frames) and captured
its keyframe with `SaveState` on the emulation thread. The capture used
`CompressionLevel.Optimal`, a byte-at-a-time CRC over the whole payload, and wrote the
entire 22 MB RAM backing.

**Measured before (Lemmings, Release).** `RewindBuffer.Capture` p50 was 0 ms (input copy
only), but each keyframe took **139.3 ms** and allocated about 75 MB. Twenty captures
allocated 1.5 GB and forced 12 gen2 collections. An uncompressed `SaveState` still needed
about 67 ms because the CRC alone cost ~55 ms for 26 MB.

**Changes.**

1. `StateCrc32` now uses a slicing-by-8 table. The value is unchanged, and a
   reference-implementation test covers it.
2. Memory state section version 4 writes only the live `HiresRamSize` range instead of the
   full 22 MB backing. The unused tail is cleared on load. A stock 2 MB DRAM / 1 MB VRAM
   state drops from ~26 MB to ~10 MB uncompressed.
3. Rewind snapshots use `CompressionLevel.Fastest` and skip the per-section CRC
   (`SectionFlagUnchecked`); persisted `.tdos` files keep `Optimal` compression and
   always validate CRCs.
4. `RewindBuffer` reuses its capture stream, state buffers, `StateWriter`, payload buffer,
   and compression scratch; trimmed keyframes return their arrays to a pool.
5. The BIOS MD5 used in the state header is cached instead of hashing 1 MB every capture.
6. The frontend requests the host-side prefetch cache for uncompressed images. CHD
   uses CHDSharp's own hunk read-ahead directly, so a second look-ahead cannot hold
   the reader lock and stall the emulation thread.

**Measured after (same 1,200-frame run).** Keyframe captures fell to **7.4-9.2 ms**
(one 14.1 ms outlier) and the run allocated **16 MB** instead of 1.5 GB with no gen0/gen2
collections. End-to-end frame work was p50 **1.34 ms**, p95 **2.13 ms**, p99 **6.85 ms**,
max **14.76 ms** for Lemmings with the mailbox and rewind active. The per-frame rewind
input copy remains p50 0 ms / p99 0.006 ms.

**Remaining.** The snapshot still serializes the ROM and the full VDL frame. Skipping the
ROM for same-session rewind snapshots would remove another ~2 MB and ~1 ms per keyframe;
continuous CHD allocation (PERF-04) still needs an allocation gate.

### 8.6 Interpreter and high-resolution VDLP hot paths (2026-11-05)

**Interpreter (ENG-02).** `FiveDoMemory` implements `IAddressBus.ReadInstruction`, which
returns the ROM word directly or the DRAM word when the ROM overlay is clear, instead of
running the full MMIO decode order for every instruction fetch. `Arm60.ExecuteDataProcessing`
now inlines LSL#0 and rotated-immediate operand handling, and applies flag-preserving ALU
operations directly instead of calling `ArmAlu.Execute`. LDM/STM register-list iteration uses
`BitOperations.PopCount` and `TrailingZeroCount`, and the bus PC-adjust source is cached
instead of rediscovered on every store.

Interleaved same-host A/B (HEAD vs current, 600 frames after 200 warm-up, run alternately):

| Case | Before (HEAD) | After | Change |
|---|---:|---:|---:|
| Synthetic ALU/store loop | 1.55-1.63 ms/frame | **1.32-1.36 ms/frame** | ~16% faster |
| Lemmings (USA) boot | 1.39-1.50 ms/frame | **1.08-1.25 ms/frame** | ~21% faster |

The synthetic frame hash is unchanged (`0x6AD58DC5`); Lemmings remains
`0x79CE0214`. ENG-02 stays Partial because a decoded block cache and a batched loop are
still missing, and the machine event path now dominates more of the frame.

**High-resolution VDLP (PERF-05).** Both the low-resolution DMA line copy and
`RenderHighResolutionLine` previously called `IRam.Read16` four times per pixel. They now
read the raw `IRam.Words` span directly, preserving the source lane and plane stride. The
same interleaved A/B measured Lemmings high-res at **2.42-2.47 ms/frame** before and
**1.30-1.43 ms/frame** after (about 1.8x faster) with an identical high-res frame hash. The
remaining high-res cost is the CEL rasterizer writing four pixels per source texel plus the
doubled VDL output size.

Reproduce with:

```powershell
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 2000 --warmup 200 --json

dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --bios bios/panafz1.bin --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" `
  --frames 2000 --warmup 200 --json

dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --bios bios/panafz1.bin --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" `
  --frames 1000 --warmup 200 --high-res --json
```

### 8.7 Latency features and AOT (2026-11-06)

**Run-ahead cost (VID-12).** Run-ahead needs one machine snapshot per displayed frame. The
first implementation used the full compressed rewind state and cost ~16 ms for capture plus
restore, which cannot fit a 60 Hz budget. The shipped snapshot path uses an uncompressed
`MachineStateSnapshot` with a reusable `StateWriter`, and two state-format changes remove
avoidable work:

- Machine frame section v2 writes only the visible source lines and copies pixels as
  little-endian blocks instead of ~1.4 million `WriteUInt16` calls. Version-1 sections
  still load.
- `FiveDoMemory.Load` clears only the range that was previously addressable instead of the
  unused 22 MB backing tail on every restore.

Measured with the synthetic bench on this machine (`bench --frames 120 --latency`, Release):

| Metric | Value |
|---|---:|
| Uncompressed snapshot | 9,348,438 bytes |
| Capture | ~3.6 ms/frame |
| Restore | ~2.0 ms/frame |

A visible frame on Lemmings costs ~1.2 ms, so one-frame run-ahead measures roughly
6-8 ms/frame including the extra emulated frame. Two-frame run-ahead is available but
suited to faster titles. Each depth step removes one field of emulator-side input delay
(**16.7 ms at 60 Hz, 20 ms at 50 Hz PAL**). `CoreRunner` auto-disables run-ahead after 45
consecutive frames that exceed 1.35x the audio-derived period, so a slow title falls back
to the safe path instead of running permanently late.

**Software input-to-present latency.** `LatencyTelemetry` timestamps the guest PBUS read
and the host frame-submit callback. The FPS overlay reports the rolling mean and maximum.
This covers everything 5DO controls; display scanout, compositor latency, and pixel response
still require an external photodiode or high-speed-camera measurement. The default is zero
run-ahead, zero frame delay, vsync on, and zero audio sync.

**AOT/trimming (PERF-02).** See `Trimming.md` for the 2026-11-06 sizes and startup
measurements: CLI AOT `--version` 69 ms, `bench --frames 2` 65 ms, frontend AOT
`--version` 70 ms, frontend AOT smoke 1.2 s. The AOT CLI realtime OpenAL run with a 40 ms
audio sync offset reported 0 dropped samples and 0 underruns over 240 Lemmings frames.

**Completion:** VID-12's feature scope is complete and marked done. Full optical
input-to-photon measurement and real adaptive-sync hardware confirmation remain external
validation caveats, not missing software features. PERF-02 and SYS-12 are also complete;
AUD-01 keeps only resampler quality, exclusive mode, and long-session platform drift work.
