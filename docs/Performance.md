# Performance and 4K60 Strategy

**Status:** target definition and implementation baseline (2026-10-23). The desktop
path now has a dedicated 60 Hz core thread, a two-stage palette/scale presenter,
immutable texture storage with per-frame `TexSubImage2D`, cached uniform locations,
and a real benchmark harness. CPU-side frame cost is ~2.1-2.3 ms, leaving ~14 ms of
headroom at 60 Hz. Manual DPI validation, the GLES backend, and GPU 4K timing remain
open; see §9 and M9P in [`ImplementationPlan.md`](ImplementationPlan.md). The overall
project position is summarized in [`Status.md`](Status.md).

**Feasibility assessment:** desktop 4K60 is high confidence because the CPU has
~14 ms of headroom and the 4K pass is one fullscreen textured quad after the
native-resolution palette expansion. Pi 5 is likely but unmeasured; Pi 4 is more
realistically 1080p60. The remaining risk is frame pacing, the Avalonia/WGL
compositor, and p99 spikes, not raw GPU fill rate. 4K internal CEL rendering is not
a v1 goal.

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
  `RESSCALE=1` (high-resolution CEL) is a separate post-v1 feature.
- **4K internal rendering is not a v1 goal.** It would require GPU CEL compositing or
  a fundamentally different renderer and is tracked only as an experiment.

The default 4K path must use nearest or sharp-bilinear. CRT and xBR/HQx are opt-in;
they may require a faster GPU and must show a performance warning at 4K.

### Hardware tiers

| Tier | Example hardware | Target |
|---|---|---|
| A | Mid-range x64 desktop/laptop from ~2020 or newer (GTX 1650 / RX 6400 / Iris Xe / Apple M1 class) | 4K60 with nearest/sharp-bilinear; 1080p60 with CRT/xBR |
| B | Raspberry Pi 4/5 class arm64, integrated x64 GPUs | 1080p60 with nearest/sharp-bilinear |
| C | Older/low-power devices | Native 320x240 or 720p60; CPU fallback allowed |

Tier B requires the OpenGL ES 3.0/3.1 path described in §6. Do not promise desktop
OpenGL 3.3 on Raspberry Pi 4: its conformant API is OpenGL ES 3.1 (Mesa V3D exposes
desktop OpenGL 3.1, not 3.3).

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
| Core on Tier B (Pi 4/5) | ≤ 12 ms p99 | 1080p output; CHD streaming must not stall |
| DSP | ≤ 1 ms/frame | 44.1 kHz stereo |
| Indexed upload | ≤ 0.5 ms CPU | ~175 KB/frame at 320x240 |
| Allocations | 0 bytes/frame | Core and presenter, steady state |
| Managed heap | < 150 MB resident | Including GL textures and caches |
| Startup to first frame | < 1 s | After BIOS/disc validation |
| Audio latency | ≤ 50 ms | No underruns in normal play |

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
  - nearest / sharp-bilinear / CRT / xBR
  - samples the expanded RGB texture into the 4K framebuffer
  - ~8.3M pixels/frame at 4K
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

The desktop frontend now uses `ThreeDo.Host.CoreRunner`: a dedicated core thread
paced at 60 Hz with a hybrid sleep/spin pacer and a low-rate (4 Hz) status sink.
UI actions that touch the machine (reset, save/load, disc changes, screenshots)
are marshalled through `CoreRunner.Invoke`, which pauses the loop for the duration
of the action. The Avalonia control renders only when the mailbox sequence or the
output size changes.

Target model:

- **Core thread:** dedicated `EmulatorSession` thread runs `RunFrame` at a fixed
  60 Hz using `FramePacer` and a high-resolution timer. No wall-clock reads inside
  emulated state.
- **Render/UI thread:** consumes the latest completed frame from a lock-free or
  triple-buffered mailbox and submits GPU work. It never calls `RunFrame`.
- **Audio thread:** consumes the SPSC ring buffer; never calls into the core.
- **Frame pacing:** fixed 60 Hz, catch-up capped at one or two frames; skip
  presentation (not emulation) if the renderer is late. Optional audio-driven
  correction later.
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

---

## 6. Renderer backend matrix

Avalonia's Windows default rendering mode is ANGLE (OpenGL ES), with software as a
fallback. A desktop `#version 330 core` shader is therefore not portable by default,
and Raspberry Pi 4 exposes OpenGL ES 3.1 rather than desktop GL 3.3.

The renderer must support two shader/API variants behind one contract:

| Backend | API / shader | Platforms | Notes |
|---|---|---|---|
| Desktop GL | OpenGL 3.3 core, GLSL 330 | Windows via WGL, Linux GLX/EGL, macOS 4.1 core | Primary desktop path; integer textures and `texelFetch` |
| GLES | OpenGL ES 3.0/3.1, GLSL ES 300 | Windows ANGLE, Raspberry Pi 4/5, other GLES devices | Same two-stage pipeline; `R16UI` + `usampler2D` |

Implementation requirements:

- Detect `GlVersion.Type` (desktop GL vs GL ES) and version at context creation.
- Select the matching shader source and the matching Silk.NET binding
  (`Silk.NET.OpenGL` vs `Silk.NET.OpenGLES`); keep the renderer logic shared.
- On Windows, either configure `Win32PlatformOptions.RenderingMode` to prefer
  `Wgl` with `AngleEgl` fallback, or implement the GLES path. Do not assume the
  default ANGLE context accepts GLSL 330.
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
- Add a host-side prefetch worker with a bounded sector cache for CHD/ISO reads.
  The core consumes sectors at emulated time; if a sector is not ready, the host may
  block briefly, but the emulated state remains deterministic.
- Benchmark CHD vs ISO boot and streaming; record p99 frame time, not just averages.
- Keep the existing read-ahead configuration and validate it against real discs in
  [`TestCorpus.md`](TestCorpus.md).

---

## 8. Measurement

### Microbenchmarks

Use `benchmarks/ThreeDo.Benchmarks` (stopwatch harness; BenchmarkDotNet can be added
later for microbenchmarks):

- `Arm60.Step` on a mixed instruction stream (MIPS).
- `CelEngine.HandleCel` on representative packed/literal/LR scenes (pixels/ms).
- `Dsp.Loop` (samples/second).
- `ThreeDoMachine.RunFrame` on a synthetic scene (ms/frame, allocations/frame).
- `FrameAssembler` (MB/s).
- `AudioRingBuffer` (samples/second, allocations).
- `VdlFrameUploader`/presenter CPU path (ms/frame, allocations).
- `OpenGlRenderer` upload/present (MB/s, CPU ms; GPU time via timer queries).

### Measured baseline (2026-10-23)

`benchmarks/ThreeDo.Benchmarks` now runs a synthetic machine by default and accepts
`--bios`/`--disc` for a real title. Release build, development machine (x64):

| Case | Core ms/frame | Video pack µs | Mailbox write µs | Read copy µs | Total ms/frame |
|---|---:|---:|---:|---:|---:|
| Synthetic | 2.177 | 42.8 | 23.4 | 27.2 | 2.270 |
| Lemmings (USA), 600 frames | 2.061 | 40.7 | 17.1 | 18.1 | 2.137 |

The synthetic run reports ~96 MIPS; the Lemmings run reports ~26 MIPS because the
guest spends time in the VDLP/CEL and idle paths. CPU-side headroom against the
16.67 ms budget is ~14.4 ms. The read copy is deliberately performed outside the
mailbox lock so GPU submission cannot stall the core thread. These numbers are
**CPU only**: the 4K GPU stage and timer queries are not yet measured, and the
benchmark is not a substitute for the p99/percentile run described below.

Run it with:

```powershell
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- --frames 600
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin --disc "$env:THREEDO_GAMES\Lemmings (USA).chd" `
  --label lemmings
```

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
- The CI workflow runs a synthetic benchmark smoke on Ubuntu and uploads the JSON
  baseline as an artifact. It does not fail on timing yet.
- Nightly or self-hosted: BenchmarkDotNet baselines, real-content smoke, 4K GPU
  timing. Fail on a sustained >10% regression or a missed budget after warm-up.
- Store the baseline JSON and the hardware description with each run. A performance
  change needs before/after numbers and identical golden output.

---

## 9. Current gaps (2026-10-23 review)

Resolved in the 2026-10-23 M9P pass:

- Dedicated 60 Hz core thread with `CoreRunner`, pause/invoke marshalling, and a
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
2. The renderer selects GLSL ES 300 sources when the context reports OpenGL ES, and
   the Windows frontend now prefers WGL. ANGLE/Pi real-hardware validation and the
   Silk.NET binding path under an ES context remain.
3. No GPU timer-query/4K percentile run yet; the current numbers are CPU-side.
4. CHD prefetch on a host worker is not implemented; the core still reads
   synchronously through the disc source.
5. Real-game compatibility is partial: Lemmings and Space Ace render, Road Rash and
   Wolfenstein load their own OS but stop at `SAudio channel 0` initialization, and
   Alone in the Dark fails earlier. **Opera passes all four problem titles**, so these
   are our bugs, not a shared FreeDO limitation (see
   [`ReferenceStrategy.md`](ReferenceStrategy.md) and [`TestCorpus.md`](TestCorpus.md)).
6. The benchmark harness reports averages, not p50/p95/p99, and does not assert
   zero allocations per frame in CI.

These gaps do not invalidate the architecture. They do mean performance work cannot
be deferred to M10: the backend matrix and physical-pixel sizing are structural
changes and belong in M9P before release polish.

---

## 10. Acceptance gates

- [ ] 4K60 with nearest/sharp-bilinear on Tier A; 1080p60 on Tier B.
- [ ] Core frame time p99 ≤ 8 ms on Tier A and independent of output resolution.
- [ ] Presenter CPU ≤ 2 ms p99; GPU frame within budget for the selected filter.
- [ ] Zero managed allocations per frame in core and presenter (steady state).
- [ ] Desktop GL 3.3 and GLES 3.0/3.1 shader variants compile and render identical
      Accurate-mode output.
- [ ] Physical-pixel sizing at 100/150/200% DPI; no logical-pixel downscaling.
- [ ] Render-on-demand and frame pacing verified with a 60 Hz and a 144 Hz display.
- [ ] Real-content smoke and performance results recorded with BIOS, disc, commit,
      and hardware.
