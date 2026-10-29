# 5DO Architecture

**Status:** implemented through M9P desktop (M0-M8 complete; M9/M9R/M9P/M10 implemented as a working prototype). The dedicated core thread, two-stage presenter, persistent upload, physical-pixel sizing, FSR/xBR/HQx filters, and render-on-demand described below are implemented. Raspberry Pi/GLES 3.1, DPI, Tier B timing, and the remaining M9P items are pending. See [`Status.md`](Status.md) for the current position, [`DeveloperGuide.md`](DeveloperGuide.md) for the source map, and [`docs/README.md`](README.md) for the documentation index.
**Reference:** the C# port was originally derived from FreeDOCore; since 2026-10-23, `References/opera-libretro-master/libopera` is the primary behavioral reference for new compatibility work. See [`ReferenceStrategy.md`](ReferenceStrategy.md).
**Scope:** the emulator core, host layer, CLI, Avalonia frontend, tests, and build layout.

---

## 1. Design goals

1. **One console subsystem per project.** The CPU, memory, MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD-ROM, storage, audio, and input each live in their own assembly.
2. **The core is platform-neutral.** `FiveDo.*` core projects target `net10.0`, use no UI toolkit, no file I/O, no wall clock, and no native interop.
3. **The host owns the outside world.** BIOS files, disc images, NVRAM files, audio devices, gamepads, windows, and settings live in `FiveDo.Host` and the frontends.
4. **The frontend is replaceable.** The CLI and Avalonia app both consume `FiveDo.Host`; neither is referenced by the core.
5. **The port is traceable.** Every ported C++ unit has a named C# counterpart and a comment pointing back to the FreeDOCore file/function.
6. **Determinism by construction.** Fixed timestep, integer scheduler, explicit state serialization, no hidden globals, no wall-clock reads inside the core.
7. **Performance is a first-class requirement.** The v1 target is 4K (3840x2160) 60 fps output on Tier A hardware and 1080p60 on Raspberry Pi 4/5 class devices, with core cost independent of output resolution. See [`Performance.md`](Performance.md).

---

## 2. System context

```text
┌─────────────────────────────────────────────────────────────────────┐
│                         Frontends (replaceable)                     │
│                                                                     │
│   FiveDo.Frontend.Avalonia                 FiveDo.Cli               │
│   - views / viewmodels                      - headless commands     │
│   - settings / library                      - frame/state dumps     │
│   - hosts FiveDo.Video.OpenGL              - benchmarks             │
│     (palette shader, scaling, CRT/xBR/FSR)                          │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                         FiveDo.Host                                 │
│  CoreRunner, AudioFramePacer, AppSettings/Store, NvramStore,        │
│  SaveStateSlotManager, StateInspector, GameLibraryScanner,          │
│  GameCompatibility, MemoryPatch, RoadRashAutopilot                  │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                        FiveDo.Machine                               │
│  FiveDoMachine, Quartz, machine composition, state sections,        │
│  device wiring                                                      │
└───────┬─────────┬─────────┬─────────┬─────────┬─────────┬───────────┘
        │         │         │         │         │         │
   ┌────▼───┐ ┌───▼────┐ ┌──▼────┐ ┌──▼───┐ ┌───▼───┐ ┌───▼────┐
   │  Cpu   │ │ Memory │ │ Madam │ │ Clio │ │  Dsp  │ │ Sport  │
   └────┬───┘ └───┬────┘ └──┬────┘ └──┬───┘ └──┬────┘ └───┬────┘
        │         │         │         │        │          │
   ┌────▼───┐ ┌───▼────┐ ┌──▼────┐ ┌──▼────┐ ┌─▼─────┐ ┌──▼─────┐
   │ Video  │ │  Xbus  │ │ Cdrom │ │Storage│ │ Audio │ │ Input  │
   └────┬───┘ └───┬────┘ └──┬────┘ └──┬────┘ └──┬────┘ └──┬─────┘
        │         │         │         │        │          │
        └─────────┴─────────┴────┬────┴────────┴──────────┘
                                  │
                        ┌─────────▼─────────┐
                        │   FiveDo.Core     │
                        │ contracts, types, │
                        │ constants, state  │
                        └───────────────────┘
```

**Dependency rule:** arrows point *downward only*. `FiveDo.Core` has no project references. No chip project references another chip project; they communicate through small interfaces defined in `FiveDo.Core` and are wired by `FiveDo.Machine`. This avoids the circular dependencies that the original C++ code solved with globals and `extern`.

**Renderer rule:** the Avalonia frontend hosts `FiveDo.Video.OpenGL` and uses it as the primary desktop presenter. `FiveDo.Video.Vulkan` is an optional offscreen/readback presenter and probe; it is never the default. The CLI uses the CPU/null renderer. Both GPU backends consume completed `VdlFrame` data and never touch emulated state.

---

## 3. Project catalog

| Project | Responsibility | Key types | References |
|---|---|---|---|
| `FiveDo.Core` | Contracts, shared value types, constants, compatibility flags, logging abstraction, state reader/writer interfaces | `IAddressBus`, `IRam`, `ICpuBackend`, `IPcAdjustSource`, `IInterruptSink`, `IFrameSink`, `IAudioSink`, `IPbusSource`, `IDiscSource`, `IXbusDevice`, `IVdlp`, `IMadamBus`, `VdlFrame`, `VdlLine`, `FrameBuffer`, `FixMode`, `EmulatorTuning`, `CompatibilityState`, `StateFormat`, `StateSectionIds`, `IStateReader/Writer` | none |
| `FiveDo.Cpu` | ARM60 interpreter, condition codes, banked registers, cycle accounting, disassembler | `Arm60`, `Arm60State`, `ArmDecoder`, `ArmAlu`, `ArmBarrelShifter`, `ArmDisassembler` | Core |
| `FiveDo.Memory` | Physical RAM/ROM/NVRAM, word-swapped representation, mirrors, byte/halfword/word helpers | `FiveDoMemory` | Core |
| `FiveDo.Madam` | MADAM registers, CEL engine, pixel decoder/processor/projector, matrix engine, pixel bus, DMA | `Madam`, `CelEngine`, `CelBitReader`, `CelRasterizer`, `CelGeometry`, `CelMemory`, `CcbParser`, `PixelDecoder`, `PixelProcessor`, `Projector` | Core |
| `FiveDo.Clio` | CLIO registers, timers, FIFOs, DMA, interrupts, DSP interface, XBUS register window, ROM select, diagnostic port | `Clio`, `DiagPort` | Core |
| `FiveDo.Dsp` | 3DO audio DSP: instruction decode/precompute, ALU, multiplier, barrel shifter, NMem/IMem, semaphore, sample output | `Dsp`, `DspInstruction`, `DspTraceEntry` | Core |
| `FiveDo.Sport` | SPORT VRAM fill/copy engine, high-res plane mirroring | `Sport` | Core |
| `FiveDo.Video` | VDLP display-list parser, line/frame assembly, palette expansion reference path, frame extraction, scaler contracts, CPU fallback presenter, mailbox | `Vdlp`, `FrameAssembler`, `GpuPalette`, `VdlFrameUploader`, `VdlFrameMailbox`, `ViewportCalculator`, `IVideoRenderer`, `VideoFilter`, `RendererSettings`, `CpuVideoRenderer` | Core |
| `FiveDo.Video.OpenGL` | **Required desktop renderer**: desktop GL 3.3 and GLES 3.0/3.1 context integration, two-stage native-resolution palette expansion + output upscaling, persistent texture upload, scaling/filter pipeline (including FSR/xBR/HQx), vsync presentation, screenshots | `OpenGlRenderer`, `OpenGlCapabilities`, `SwapInterval`, `HqxFilter`, `Shaders`, `GlContextAdapter`, `OffscreenRenderTarget` | Core, Video, Silk.NET.OpenGL |
| `FiveDo.Video.Vulkan` | **Optional offscreen Vulkan 1.0 renderer**: build-time SPIR-V palette/scale shaders, sampled source textures, accurate/nearest/sharp-bilinear/CRT filters, timestamp queries, host readback | `VulkanOffscreenRenderer`, `VulkanShaders` (generated) | Core, Video, Silk.NET.Vulkan |
| `FiveDo.Xbus` | XBUS controller, device selection, poll/status/data FIFOs, device interface | `Xbus` | Core |
| `FiveDo.Cdrom` | Mitsumi/MEI CD-ROM device, command state machine, TOC/MSF/LBA, sector streaming | `CdromDevice` | Core |
| `FiveDo.Storage` | Disc image readers (ISO/BIN/CUE/CHD), bounded host prefetch, BIOS validation, NVRAM file format, game database, game ID | `IsoImage`, `BinImage`, `CueSheet`, `ChdDiscImage`, `PrefetchingDiscSource`, `DiscImageFactory`, `BiosImage`, `NvramImage`, `VolumeHeader`, `GameDatabase`, `GameId` | Core, CHDSharp |
| `FiveDo.Audio` | Sample format conversion, ring buffer, null/WAV/live sinks, soft limiter, PCM tap, statistics, WAV analysis | `AudioRingBuffer`, `AudioPipeline`, `NullAudioSink`, `WavAudioSink`, `AudioSoftLimiter`, `AudioTap`, `AudioStatistics`, `AudioAnalyzer`, `OpenAlAudioOutput` | Core, Silk.NET.OpenAL |
| `FiveDo.Input` | Controller state, PBUS encoding, input profiles, keyboard/gamepad abstractions, scripts | `ControllerState`, `ControllerButton`, `PbusEncoder`, `PbusSource`, `InputProfile`, `PlayerProfile`, `KeyboardInputMapper`, `InputManager`, `GamepadMapper`, `InputScript` | Core |
| `FiveDo.Diagnostics` | Serilog configuration, in-memory log viewer sink, bug reporting, launch statistics, tracing, disassembly helpers, frame timing statistics, memory/frame dumpers, benchmark gates | `LoggingSetup`, `AppLogging`, `InMemorySerilogSink`, `BugReportSink`, `BugReportFormatter`, `ApplicationStatsClient`, `ApplicationStatsOptions`, `CpuTracer`, `MemoryDumper`, `FrameDumper`, `PngDumper`, `FrameTiming`, `BenchmarkGate`, `BenchmarkComparison` | Core, Serilog |
| `FiveDo.Machine` | Machine composition root, frame loop, Quartz scheduler, device wiring, state sections | `FiveDoMachine`, `Quartz` | Core + all chips + Diagnostics |
| `FiveDo.Host` | Core thread lifecycle, audio-derived frame pacing, BIOS/disc/NVRAM loading, audio/input backends, save slots, settings, library, fixes, autopilot | `CoreRunner`, `AudioFramePacer`, `AppSettings`, `AppSettingsStore`, `NvramStore`, `SaveStateSlotManager`, `StateInspector`, `GameLibraryScanner`, `GameCompatibility`, `MemoryPatch`, `RoadRashAutopilot` | Machine, Storage, Audio, Input, Video, Diagnostics |
| `FiveDo.Cli` | Headless command-line interface, JSON contracts | `Program`, `CliJsonModels` | Host |
| `FiveDo.Frontend.Avalonia` | Cross-platform desktop UI, library/settings/input/slots/logs, OpenGL video surface, GPU probes | `App`, `MainWindow`, `VideoSurface`, `SettingsWindow`, `InputBindingsWindow`, `SaveSlotsWindow`, `GameLibraryWindow`, `LogsWindow`, `WelcomeWindow`, `GlProbeWindow`, `GlProbeOptions`, `VkProbeRunner`, `VkProbeOptions`, `GlBackendOption`, `PlatformGamepadBackend` | Host, Video.OpenGL, Video.Vulkan, Avalonia |
| `FiveDo.Tests` | Unit/integration/golden/CLI/frontend tests (subfolders per subsystem) | test fixtures, ARM assembler/rig, fake chips/MMIO, PNG writer | production projects |
| `FiveDo.Benchmarks` | Frame benchmark, CI gate, baseline comparison | `Program` | Cpu, Madam, Dsp, Diagnostics, Machine, Storage, Video |

### Why `FiveDo.Machine` is separate from `FiveDo.Core`

The core contracts project must not know about concrete chips. The machine is the composition root that knows all concrete chips and wires them together. This preserves the dependency rule and makes each chip independently testable with fake collaborators.

---

## 4. Runtime composition

`FiveDoMachine` is constructed once per emulated console:

```csharp
var memory = new FiveDoMemory(biosRom, nvram);
var cpu    = new Arm60(memory.Bus, tuning);
var madam  = new Madam(memory.Ram, tuning);
var clio   = new Clio(madam, tuning);
var dsp    = new Dsp(tuning);
var sport  = new Sport(memory.Ram, tuning);
var vdlp   = new Vdlp(memory.Ram, tuning);
var xbus   = new Xbus(clio, tuning);
var cdrom  = new CdromDevice(discSource, tuning);
var quartz = new Quartz(tuning);

xbus.Attach(cdrom);
madam.Interrupts = clio;
madam.Vdlp = vdlp;
clio.Interrupts = clio;
clio.Xbus = xbus;
clio.Dsp = dsp;
vdlp.FrameSink = frameSink;
machine = new FiveDoMachine(cpu, memory, madam, clio, dsp, sport, vdlp, xbus, cdrom, quartz, tuning);
```

### Wiring rules

- **Interrupts:** every device that can raise a FIQ depends on `IInterruptSink`, implemented by `Clio`. No chip calls `Clio` directly.
- **CEL memory:** `Madam` receives the `Ram` object directly because the original C++ uses raw RAM reads/writes (`mread`, `mwrite`) that bypass the MMIO map.
- **VDLP memory:** `Vdlp` receives `Ram` directly and reads VRAM at the +2 MB offset, matching `_mem_read32((addr & 0xFFFFF) + 2 MB)`.
- **SPORT memory:** `Sport` receives `Ram` directly, matching the original `VRAM` pointer.
- **XBUS devices:** `Xbus` owns up to 16 `IXbusDevice` slots; device 0 is the CD-ROM.
- **DSP interface:** `Clio` owns the DSP register windows and calls `IDsp` methods; `Dsp` raises audio FIQ through `IInterruptSink`.
- **Frame sink:** `Vdlp` writes into a `VdlFrame`; `FiveDoMachine` calls `IFrameSink.OnFrame` at the VDL swap point.
- **Input:** `FiveDoMachine` asks `IPbusSource` for the 16-byte PBUS packet once per frame at the V1 line, exactly as FreeDOCore does.

---

## 5. Address map

The bus decodes in the same order as FreeDOCore's `mreadw`/`mwritew`:

| Range | Size | Device | Notes |
|---|---:|---|---|
| `0x00000000`-`0x002FFFFF` | 3 MB | DRAM + VRAM | Word reads/writes direct; byte access uses the FreeDO word-swapped convention |
| `0x03000000`-`0x030FFFFF` | 1 MB window | ROM bank 0/1 | `CLIO` register `0x84` selects bank 1; bank 0 is BIOS1, bank 1 is BIOS2 |
| `0x03100000`-`0x0313FFFF` | 256 KB | Unmapped / reserved | Returns `0xBADACCE5` on reads |
| `0x03140000`-`0x0317FFFF` | 256 KB window | NVRAM | 32 KB backed; word accesses map one byte per word (FreeDO quirk) |
| `0x03180000`-`0x031FFFFF` | 512 KB window | Diag port | Word read/write only |
| `0x03200000`-`0x032FFFFF` | 1 MB | SPORT | Register window plus VRAM fill/copy |
| `0x03300000`-`0x033FFFFF` | 1 MB | MADAM | 2048+64 32-bit registers, mirrored |
| `0x03400000`-`0x034FFFFF` | 1 MB | CLIO | 65536 32-bit registers, FIFOs, XBUS window, DSP windows |

### Memory representation (critical)

The port preserves FreeDOCore's **word-swapped** representation for v1:

- RAM and ROM are stored as `uint[]` words whose numeric values match what a little-endian C++ build sees after `_bswap`/`memcpy`.
- `_mem_read8(addr)` returns the byte at lane `addr & 3` of the word; `_mem_read16(addr)` returns the halfword at lane `addr & 3`; both support unaligned reads by composing bytes.
- CPU byte accesses apply `addr ^ 3`; CPU halfword accesses apply `addr ^ 2`, exactly as FreeDOCore.
- NVRAM is a `byte[32 * 1024]` because FreeDOCore maps one byte per 32-bit NVRAM word. This is intentional and must not be "fixed" without compatibility tests.
- ROM loading converts each 4-byte big-endian BIOS word into the internal word value (`BinaryPrimitives.ReadUInt32BigEndian`), matching `_3do_Init`'s `_bswap` loop.

This choice avoids auditing hundreds of XORs in MADAM/CLIO/VDLP during the first port. A later optional refactor to a clean big-endian model must be guarded by the full test suite and save-state compatibility tests.

### 5a. Disc storage and CHD

`FiveDo.Storage` exposes one abstraction to the CD device:

```csharp
public interface IDiscSource : IDisposable
{
    int SectorCount { get; }
    int SectorSize { get; }          // 2048 for 3DO
    void ReadSector(int lba, Span<byte> destination);
}
```

Supported containers:

| Format | Path | Notes |
|---|---|---|
| ISO | `IsoImage` | 2048-byte sectors, direct LBA mapping |
| BIN | `BinImage` | 2352-byte sectors, payload at `0x10` |
| CUE+BIN | `CueSheet` + `BinImage` | Single data track; pregap-aware |
| **CHD** | `ChdDiscImage` | MAME CHD via **CHDSharp** (pure C#); CD/GD-ROM track table; 2048-byte payload extracted from raw 2352-byte sectors |

CHD details:

- `CHDSharp` is a pure C# library (MIT + LGPL-2.1 component) with no native dependencies. It reads CHD v1-v5 and all MAME codecs.
- `ChdFile.ReadSector(lba, ...)` returns the first 2352 bytes of the 2448-byte CD frame. `ChdDiscImage` extracts the 2048-byte user payload based on the first data track:
  - cooked Mode 1/2048 -> offset `0`
  - raw Mode 1/2352 -> offset `16`
  - raw Mode 2 Form 1 -> offset `24`
- `SectorCount` comes from the first data track's `Frames`; the 3DO volume header remains the authoritative sector count for the emulated CD device.
- Differential parent/child CHDs are supported via an optional parent path (`ChdFile.Open(path, parentPath, ...)`). Standalone CHDs are the common case.
- `ConfigureReadAhead(n)` is enabled by default (4 hunks) because 3DO games stream data sequentially. The core reads on one thread; the CHD reader is not shared across threads.
- DVD, HDD, raw, and audio-only CHDs are rejected with a clear `InvalidDataException`; 3DO software is CD-ROM based.
- CHD is a **first-class v1 format**, not a later optional add-on. The game library scans `.chd` alongside `.iso`, `.bin`, and `.cue`.

---

## 6. Frame execution

`FiveDoMachine.RunFrame()` mirrors `_3do_Frame`:

```text
cycles = 0
while cycles < 12_500_000 / 60:
    if MADAM.Fsm == InProcess:
        MADAM.HandleCel()
        MADAM.Fsm = Idle
        continue
    executed = CPU.Step()
    accumulator += executed
    if accumulator >= 16:
        InternalFrame(accumulator)
        cycles += accumulator
        accumulator = 0
```

`InternalFrame(cycles)` mirrors `_3do_InternalFrame`:

1. `Quartz.PushArmCycles(cycles)`.
2. If a DSP sample is due, `AudioSink.PushSample(Dsp.Loop())`.
3. If a timer tick is due, `Clio.DoTimers()`.
4. If a VDL line is due:
   - `Clio.UpdateVcnt(line, halfFrame)`.
   - `Vdlp.DoLine(line, frame)` unless `skipFrame` (multitask mode).
   - At line 16 in multitask mode, signal the host frame trigger.
   - At `Clio.V0Line`, raise FIQ reason `1 << 0`.
   - At `Clio.V1Line`, raise FIQ reason `1 << 1`, copy PBUS input, set `frame.SourceWidth = 320`, `frame.SourceHeight = 240`, and swap/present the frame unless `skipFrame`.

The original code processes at most one DSP/timer/VDL event per `InternalFrame` call. The port keeps that behavior initially. A later accuracy task may drain all due events in a loop, but only with golden-frame evidence.

`RunFrame` is called by the host's dedicated core thread (`CoreRunner` + its private `FramePacer`), never by the Avalonia UI thread. The UI consumes completed frames from the mailbox. This is required for stable 60 fps at 4K and is tracked by M9P.2.

### Multitask mode

FreeDOCore has two modes:

- `FDP_DO_EXECFRAME`: render each VDL line as it is emulated.
- `FDP_DO_EXECFRAME_MT`: skip line rendering, signal the host at line 16, then the host calls `FDP_DO_FRAME_MT` to render 256 lines from the current VDLP state.

The first playable version uses the single-threaded mode. The `skipFrame` path and `DoFrameMultitask` equivalent are still implemented for parity and for a future decoupled renderer.

---

## 7. Video pipeline

```text
MADAM CEL writes 16-bit pixels into VRAM
        │
        ▼
VDLP parses the display list and builds one VdlLine per scanline:
  - 320 pixels (up to 1280 for high-res/wider modes)
  - 32-entry CLUT R/G/B
  - OUT control word
  - CLUT DMA word
  - background color
        │
        ▼
VdlFrame (reused double/triple buffer)
        │
        ├──────────────────────────────────────────────┐
        │                                              │
        ▼                                              ▼
OpenGL/GLES renderer (required desktop path)   CPU FrameAssembler (oracle/fallback)
  stage 1: palette expansion at native res      - used by CLI/headless tests
  stage 2: upscale/filter at output res         - used when no GL context exists
  persistent textures (TexStorage2D/SubImage2D) - must match GL "Accurate" mode
  present with vsync
        │
        ▼
Window / fullscreen
```

**Two-stage rule:** palette expansion must run once per emulated pixel (320x240),
not once per output pixel. At 4K, a single output-resolution palette shader performs
~108x more palette/CLUT work than expanding the native frame into a small RGB
texture and then upscaling it. See [`Performance.md`](Performance.md) §3.

The core never knows about Avalonia or OpenGL. `IFrameSink` receives a `VdlFrame`; the host hands it to `IVideoRenderer`. The OpenGL renderer is the **primary, required** desktop renderer. The CPU `FrameAssembler` remains in the codebase as the accuracy oracle and as a fallback for headless runs or systems without a usable desktop GL 3.3 or GLES 3.0/3.1 context.

### Video rules

- Base output is **320x240, 4:3**, 60 Hz NTSC.
- Wider VDLP modes are parsed as 384/512/640/1024 pixels per line. The `VdlLine` capacity is 1280 pixels so all modes fit without changing the data model.
- High-resolution (`RESSCALE`) is implemented as an opt-in 640x480 mode. It changes VRAM mirroring (four 1 MB planes), VDLP line duplication, CEL stepping, SPORT copies, and frame extraction, so it is a coherent feature with its own tests rather than isolated conditionals.
- **OpenGL "Accurate" mode must match the CPU `FrameAssembler` pixel-for-pixel.** Enhanced filters are opt-in and never affect emulated state.
- The renderer receives indexed 16-bit pixels and per-line palette/control data, not a pre-expanded BGRA image. Palette expansion happens on the GPU, which cuts CPU work and enables high-quality upscaling at 1080p/1440p/4K.

### OpenGL/GLES renderer pipeline (required)

```text
VdlFrame
  ├── pixel texture:  R16UI, width x height (320x240 typical)
  ├── palette texture: RGB8, 32 x height (per-line CLUT)
  └── line-param texture: RGBA32UI, 1 x height
        (background, out-control, clut-dma flags)
        │
        ▼
Stage 1 - palette expansion at native resolution (320x240):
  p = texelFetch(pixelTex, xy)
  if p == 0:              background(line)
  else if fixedClut(line) && (p & 0x8000): fixed grayscale CLUT
  else:                    palette(p, line)
  -> RGB8 native-resolution texture (reused each frame)
        │
        ▼
Stage 2 - scaling/filter at output resolution (up to 4K):
  - Nearest (pixel-perfect integer scaling)
  - Sharp-bilinear (default enhanced)
  - CRT (scanlines, mask, curvature, vignette, bloom)
  - xBR 4x / HQx 2x/3x/4x (optional; CPU-expanded source)
  - FSR 1.0 EASU + RCAS (optional; two GPU passes)
        │
        ▼
Framebuffer -> window / fullscreen (vsync, latest-frame mailbox)
```

**Performance properties**

- Raw indexed upload is ~150 KB/frame for 320x240, plus ~23 KB of per-line CLUT and ~2 KB of line parameters.
- Palette expansion runs at 320x240 (~77k pixels/frame), not at 4K (~8.3M pixels/frame).
- 4K output costs the CPU nothing beyond the same small upload; the GPU performs the upscale/filter pass (FSR/xBR/HQx add extra passes and are opt-in).
- Textures use immutable storage (`TexStorage2D`) plus `TexSubImage2D`/PBO updates; no per-frame `TexImage2D` reallocation.
- The core remains single-threaded and deterministic; only presentation is GPU-accelerated.
- A `FrameMailbox` drops stale frames instead of queueing them; the core never waits for the GPU.

**Accuracy properties**

- The palette-expansion shader is a direct port of `frame.cpp: Get_Frame_Bitmap`:
  - pixel `0` => 5-bit background B/G/R
  - `xOUTCONTROLL & 0x02000000` and pixel bit 15 => fixed grayscale CLUT
  - otherwise per-line `xCLUTB/G/R`
- The CPU `FrameAssembler` is the golden oracle. A GPU frame and a CPU frame must hash identically in Accurate mode.
- Enhanced filters are presentation-only. They never feed back into VRAM, save states, or emulation decisions.

**Context integration**

- Avalonia supplies the GL context through `OpenGlControlBase` (or an equivalent host control).
- `FiveDo.Video.OpenGL` uses Silk.NET.OpenGL for the API and owns textures, shaders, VAOs, and the framebuffer.
- The renderer project does not reference Avalonia; the frontend passes an `IGlContext`/function-loader adapter. This keeps the renderer usable by other hosts later.
- On context loss, the frontend recreates the renderer and re-uploads the latest frame. If neither desktop GL 3.3 nor GLES 3.0/3.1 is available, the frontend falls back to the CPU `WriteableBitmap` path and logs a warning.

### 7b. Renderer backends and 4K target

The renderer contract is API-neutral; the host selects a shader/API variant:

| Variant | API / shader | Typical host |
|---|---|---|
| Desktop GL | OpenGL 3.3 core / GLSL 330 | Windows WGL, Linux GLX/EGL, macOS 4.1 core |
| GLES | OpenGL ES 3.0/3.1 / GLSL ES 300 | Windows ANGLE (Avalonia default), Raspberry Pi 4/5 |

Avalonia's Windows default is ANGLE (OpenGL ES), so a desktop-only `#version 330
core` path is not portable. The frontend must either prefer `Win32RenderingMode.Wgl`
with an ANGLE fallback or provide the GLES path. Raspberry Pi 4 exposes conformant
OpenGL ES 3.1, not desktop GL 3.3, so Tier B uses the GLES variant.

Frame pacing and threading are host responsibilities:

- Dedicated core thread paced by the 44.1 kHz audio clock (60 Hz fallback); no `RunFrame` on the UI thread.
- Render/UI thread consumes the latest mailbox frame and submits GPU work.
- Audio thread consumes the SPSC ring buffer.
- Physical-pixel sizing (`Bounds * RenderScaling`), render-on-demand, and no
  per-frame UI text formatting.
- 4K60 is output upscaling; 4K internal CEL rendering is not a v1 goal.

The full budget table, measurement methodology, and current gaps are in
[`Performance.md`](Performance.md).

---

## 7a. CPU backends and JIT

.NET already JIT-compiles the C# interpreter. When this document says "JIT", it means an **optional guest ARM dynamic recompiler**, not the normal .NET JIT.

```text
ICpuBackend
  ├── Arm60            (default, reference behavior)
  └── (optional future IL JIT backend)
```

- `ICpuBackend` exposes `Step()`, `Reset()`, `Save/LoadState`, and the register/state surface.
- The interpreter is the compatibility oracle and the default backend.
- Optimize the interpreter before adding a JIT: fast RAM path, direct dispatch/decode cache, fewer interface calls per instruction, and no per-instruction allocations. Measure on the benchmark corpus first.
- An optional IL JIT can compile ARM basic blocks into `DynamicMethod` delegates using pure managed `System.Reflection.Emit`. This stays within the "pure C#" rule.
- The 3DO ARM60 runs at ~12.5 MHz, so a tuned interpreter is expected to be fast enough. The JIT is a long-term optimization, not a prerequisite for 60 fps.
- Any JIT backend must produce bit-identical architectural state and cycle counts to the interpreter for the same program. It is enabled only after differential tests pass.
- Native executable-memory JITs (x86/ARM code emission) are out of scope for the pure C# core.

---

## 8. Audio pipeline

```text
DSP core produces a 32-bit packed sample:
  IMem[0x3FF] << 16 | IMem[0x3FE]
        │
        ▼
IAudioSink.PushSample(uint)
        │
        ▼
FiveDo.Audio.AudioRingBuffer (lock-free SPSC)
        │
        ▼
Host backend:
  - NullAudioSink (tests/CLI)
  - WavAudioSink (debug/replay; buffered; AudioStatistics)
  - OpenAlAudioOutput (desktop/CLI; OpenAL Soft 1.23.1 is bundled)
  - platform-specific backend (optional later)
        │
        ▼
AudioAnalyzer (offline WAV peak/RMS/clipping/DC report)
```

- Sample rate is **44,100 Hz**, stereo, signed 16-bit, from an exact rational DSP clock: 44,100 samples per ARM second. It is independent of the VDL/timer 8.24 accumulators.
- The DSP runs on the core thread; the audio backend runs on its own thread and only consumes the ring buffer. The OpenAL context is made current on that thread because OpenAL's current context is thread-local.
- The ring drops the newest sample when full and reports it. The OpenAL backend queues variable-size buffers containing exactly the samples the DSP produced, keeps up to eight buffers in flight after a 3072-frame prefill lead, and counts underruns. Buffers are rented from a free pool so a buffer still attached to the source is never rewritten; the fill thread is registered as a Windows "Pro Audio" multimedia thread and runs above normal priority. `IAudioSink.Reset` drops queued audio on machine reset/state load so the new timeline does not replay the old one. `FIVEDO_AUDIO_TAP=<prefix>` records the exact queued PCM (`<prefix>.wav`) plus a per-buffer timing log (`<prefix>.csv`).
- Live OpenAL applies a soft peak limiter (0.8 threshold, 0.9 ceiling) after volume; WAV captures keep the raw DSP output.
- No allocations in the steady-state audio path.
- Audio-driven frame pacing: `CoreRunner` feeds the samples produced by each frame into `AudioFramePacer`, which sets the next frame period to `samples / 44100`. A 30-frame rolling average absorbs jitter, implausible deltas reset the window, and the period is clamped to 50-70 fps with a 60 Hz fallback.
- The CLI exposes the same clock with `--realtime`, and `audio analyze <wav> --json` reports capture statistics.

---

## 9. Input pipeline

```text
Keyboard / gamepad
        │
        ▼
FiveDo.Input.InputManager (host)
  - maps physical inputs to `ControllerState`
  - supports 1-6 controllers
        │
        ▼
PbusEncoder.Encode(controllers) -> 16 bytes
        │
        ▼
FiveDoMachine asks IPbusSource once per frame (V1 line)
        │
        ▼
Madam.KeyPressed(bytes) -> PBUSQueue -> CLIO DMA -> controller hardware model
```

PBUS format (ported from 4DO's JohnnyInput):

| Byte | Meaning |
|---|---|
| 0 | `0x00` |
| 1 | `0x48` |
| 2,3 | Controller 1 low/high |
| 4,5 | Controller 3 low/high |
| 6,7 | Controller 2 low/high |
| 8,9 | Controller 5 low/high |
| 10,11 | Controller 4 low/high |
| 12,13 | `0x00`, `0x80` |
| 14,15 | Controller 6 low/high |

Low byte bits: `0x04` L, `0x08` R, `0x10` X, `0x20` P, `0x40` C, `0x80` B.
High byte bits: `0x01` A, `0x02` Left, `0x04` Right, `0x08` Up, `0x10` Down, `0x80` connected/power.

---

## 10. Host layer

`FiveDo.Host` is the only layer allowed to touch the operating system:

- **`CoreRunner`** - owns the dedicated core thread, frame/audio pacers, pause/resume/stop, and `Invoke` marshalling. It does not own the machine; the frontend/CLI constructs it.
- **`AudioFramePacer`** - derives the frame period from DSP samples and falls back to 60 Hz; `CoreRunner` has a private `FramePacer` for sleep/spin deadlines.
- **`BiosImage`** (`FiveDo.Storage`) - validates 1 MB BIOS1 and optional <=1 MB BIOS2; computes MD5; detects Anvil BIOS (`byte[28] == 234`).
- **`DiscImageFactory`** (`FiveDo.Storage`) - opens ISO/BIN/CUE/CHD, detects sector size, validates geometry.
- **`NvramStore`** - 32 KB NVRAM file, atomic write, 250 ms debounce, `.bad` recovery.
- **`GameDatabase`** / **`GameCompatibility`** - MD5-of-first-two-sectors game ID, metadata, and fix-mode mapping.
- **`AudioPipeline`** (`FiveDo.Audio`) - reusable ring sink; the live path uses `OpenAlAudioOutput`/`WavAudioSink` directly.
- **`InputManager`** / **`KeyboardInputMapper`** / **`PlatformGamepadBackend`** - keyboard/gamepad mapping and PBUS encoding.
- **`SaveStateSlotManager`** - slots, metadata, thumbnails, atomic writes; **`StateInspector`** reads headers/sections.
- **`AppSettings` / `AppSettingsStore`** - source-generated JSON settings and atomic writes.
- **`GameLibraryScanner`** - scans folders for supported disc images and reads metadata.
- **`MemoryPatch`** / **`RoadRashAutopilot`** - diagnostic RAM patches and the Road Rash test driver.

### Settings locations

| Platform | Settings / NVRAM | Default saves | Default screenshots |
|---|---|---|---|
| Windows | `%LOCALAPPDATA%\5DO` | `%LOCALAPPDATA%\5DO\states` | Pictures |
| Linux | `$XDG_DATA_HOME/5DO` or `~/.local/share/5DO` | `<that>/states` | Pictures |
| macOS | .NET `LocalApplicationData/5DO` (verify in About/Settings) | `<that>/states` | Pictures |

`AppSettingsStore.DefaultDirectory` is `Path.Combine(LocalApplicationData, "5DO")`;
`nvram.ram` and `settings.json` live there. A save state is ~25 MB (full RAM backing +
VDL frame). **Portable mode is not implemented**; a `portable.flag` file has no effect.
See [`Configuration.md`](Configuration.md) and [`SaveStates.md`](SaveStates.md).

---

## 11. State ownership

| State | Owner | Included in save state? |
|---|---|---|
| CPU registers, CPSR, banked registers, FIQ pending, ROM select, data abort | `Arm60` | yes |
| Full RAM backing (3 MB visible + 16 x 1 MB high-res mirrors) | `FiveDoMemory` | yes |
| ROM banks | `FiveDoMemory` | yes (for exact restore) |
| NVRAM | `FiveDoMemory` | yes |
| MADAM registers, CEL FSM, PLUT, PBUS queue, matrix results | `Madam` | yes |
| CLIO registers, timers, FIFOs, DMA state | `Clio` | yes |
| DSP registers, NMem, IMem, semaphore, running flag, PRNG seed | `Dsp` | yes (precomputed tables rebuilt) |
| SPORT color/source/destination | `Sport` | yes |
| VDLP head/current bitmap/palette/linedelay | `Vdlp` | yes |
| XBUS selection/poll/FIFO state and attached device state | `Xbus` | yes |
| CD device command/status/data/TOC state | `CdromDevice` | yes (also via XBUS) |
| Quartz accumulators/line counters | `Quartz` | yes (section v2) |
| Compatibility flags and the derived `VdlFrame` | `FiveDoMachine` | yes (Machine section) |
| Disc contents | host `IDiscSource` | no; the game ID is recorded in the header but not validated on load |
| Input state | host | no |
| Derived `FrameBuffer` / audio queue | host | no; regenerated/cleared |

A typical `.tdos` is ~25 MB because the memory section stores the full RAM backing and
the machine section stores all 960 VDL lines. See [`SaveStates.md`](SaveStates.md).

---

## 12. Threading model

- **Core thread:** CPU, memory, chips, scheduler, DSP, VDLP, CD. Dedicated, single-threaded, and deterministic. Implemented as `FiveDo.Host.CoreRunner` with an audio-derived `FramePacer` (60 Hz fallback); the UI thread never calls `RunFrame`, and machine-touching UI actions go through `CoreRunner.Invoke` (pause, action, resume).
- **Render thread / UI thread:** Avalonia owns the GL/GLES context and presents frames. The renderer consumes the latest `VdlFrame` snapshot; it never calls into emulated state.
- **Audio thread:** consumes the ring buffer and drives the OpenAL source; it never calls into the core. It makes the OpenAL context current on its own thread.
- **No locks in the core hot path.** The only cross-thread state is the audio ring buffer, the latest-frame mailbox, and session lifecycle flags.

A `VdlFrameMailbox` holds the most recent completed `VdlFrame`. If the renderer is slow, old frames are dropped rather than queued, preventing latency growth. The renderer uploads the frame to a texture and applies the two-stage filter pipeline; the core does not wait for the GPU. The mailbox copies only the visible line width, and `Read(destination, reader)` copies into a caller-owned frame before invoking the renderer so the lock is not held during GPU submission. The present delegate is cached.

**Frame pacing:** audio-derived on the core thread (60 Hz fallback), render-on-demand when a new frame is available. Implemented in `CoreRunner`; the Avalonia surface renders only when the mailbox sequence or the output size changes. Status/FPS updates run at 4 Hz, not every frame. A stall longer than 100 ms resyncs the pacer instead of chasing the past.

---

## 13. Error handling and diagnostics

- **Fatal configuration errors** (missing BIOS, invalid disc, unsupported state version) throw host/storage exceptions (`InvalidDataException`, `DiscImageException`, `InvalidStateException`, `ArgumentException`); the CLI catches these and exits 1. `EmulationException` is the base type for fatal emulator errors.
- **Non-fatal emulation anomalies** (unmapped MMIO read, unknown CD command, malformed CEL) are logged at `Warning`/`Debug` and the core continues with the same sentinel behavior as FreeDOCore (`0xBADACCE5`, zero, etc.).
- **No exceptions in the hot path.** Device methods return values; diagnostics use Serilog level guards.
- **Structured logging:** Serilog levels and `Category` properties; hosts configure the rolling file, console, in-memory viewer, and bug-report sinks.
- **Debug builds** can enable `CpuTracer`, `MemoryDumper`, `FrameDumper`, and the CLI trace/dump options.

---

## 14. Extensibility points

| Extension | Interface | Example |
|---|---|---|
| Video presenter | `IFrameSink` / `IVideoRenderer` | OpenGL (required), CPU BGRA fallback, null/hash sink |
| Audio output | `IAudioSink` | null, WAV, OpenAL |
| Input backend | `IPbusSource` / `IGamepadBackend` | keyboard, XInput, evdev, scripted replay |
| Disc format | `IDiscSource` | ISO, BIN, CUE, CHD, prefetch wrapper |
| Save state format | `IStateReader/Writer`, `IStateSection` | native `.tdos` sections (no 4DO import in this release) |
| Renderer/filter | `IVideoRenderer` / shader variants | nearest, sharp-bilinear, CRT, xBR, HQx 2x/3x/4x, FSR |
| CPU backend | `ICpuBackend` | ARM60 interpreter; optional IL JIT (not implemented) |
| Diagnostics | `Serilog.ILogger`, `CpuTracer`, `FrameDumper` | rolling file, in-memory viewer, bug-report API, CLI `trace` |

---

## 15. Coding conventions

- File-scoped namespaces; one public top-level type per file.
- Production projects keep domain models and enums in `Models/` and interfaces in
  `Interfaces/`; services, parsers, renderers, and other logic stay in their feature
  folders or project root. Folders are organizational only; namespaces remain
  project-level (for example `FiveDo.Core`).
- `sealed` classes unless inheritance is required.
- `readonly` fields where possible; no mutable static state.
- `uint`/`ushort`/`byte` for emulated registers; `int` only where the original uses signed arithmetic.
- Explicit `unchecked` around intentional wraparound.
- Constants for all bit masks and register offsets; never bare magic numbers in new code.
- XML documentation on public types; ported code includes a `// FreeDO: <file>:<function>` comment.
- Tests are named `Method_Scenario_ExpectedResult`.
- No `unsafe` unless a measured hot path requires it; `Unsafe`/`MemoryMarshal` are acceptable.
- No LINQ, string formatting, delegate allocation, or other managed allocation in per-instruction or per-frame paths.
- Prefer concrete types or generic constraints over interface dispatch in measured hot loops; keep the small Core interfaces at architectural boundaries.
- Shaders live in `FiveDo.Video.OpenGL/Shaders`, have desktop GLSL 330 and GLSL ES 300 variants, and are embedded as resources with a source hash logged at startup.
- The OpenGL/GLES renderer is a required desktop component; the CPU renderer is a fallback and accuracy oracle, not the primary path.
