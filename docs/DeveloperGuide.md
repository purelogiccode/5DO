# Developer Guide

This guide is for working on the 5DO source. It maps the solution, explains the
runtime and invariants, lists extension points, and records the conventions and known
architectural debt. For the design rationale, see [`Architecture.md`](Architecture.md);
for porting rules, see [`PortingNotes.md`](PortingNotes.md); for tests, see
[`TestStrategy.md`](TestStrategy.md) and [`BuildAndTest.md`](BuildAndTest.md).

---

## 1. Source tree

```text
src/
  FiveDo.Core/               contracts, value types, constants, state primitives, logging
  FiveDo.Cpu/                ARM60 interpreter, decoder, ALU, shifter, disassembler, SWI HLE
  FiveDo.Memory/             DRAM/VRAM, ROM banks, NVRAM, CPU address bus
  FiveDo.Madam/              registers, CEL engine, pixel decode/process/project, matrix
  FiveDo.Clio/               registers, timers, FIFOs, DMA, interrupts, XBUS/DSP windows
  FiveDo.Dsp/                DSP core, instruction decode, ALU, multiplier, memory
  FiveDo.Sport/              VRAM fill/copy engine
  FiveDo.Video/              VDLP, frame assembly, mailbox, CPU presenter, viewport
  FiveDo.Video.OpenGL/       OpenGL/GLES presenter, shaders, filters, swap interval
  FiveDo.Video.Vulkan/       optional Vulkan presenter, Win32 swapchain surface, SPIR-V shaders
  FiveDo.Xbus/               XBUS controller and device slots
  FiveDo.Cdrom/              MEI CD-ROM device
  FiveDo.Storage/            ISO/BIN/CUE/CHD, BIOS/NVRAM, game database, prefetch
  FiveDo.Audio/              ring, WAV, OpenAL, limiter, tap, analysis
  FiveDo.Input/              controller state, PBUS, profiles, scripts, gamepad mapper
  FiveDo.Diagnostics/        PNG/frame dumps, tracing, timing, benchmark gates
  FiveDo.Machine/            composition root, frame loop, Quartz scheduler
  FiveDo.Cheats/             per-game cheat parser, engine, and JSON document store
  FiveDo.Tas/                TAS movie container, record/replay, branching, verification, input timeline
  FiveDo.Scripting/          Sandboxed Lua input scripting (MoonSharp)
  FiveDo.Host/               core runner, pacing, settings, slots, library, fixes, autopilot, cheats
  FiveDo.Cli/                headless commands and JSON contracts
  FiveDo.Frontend.Avalonia/  desktop shell, video surface, dialogs, GPU probe
tests/FiveDo.Tests/          xUnit suite, one folder per subsystem
benchmarks/FiveDo.Benchmarks custom frame benchmark and CI gate
packaging/                   bundle/archive, Windows/Linux/macOS installer scripts, desktop entry
tools/                       release soak, collection compatibility sweep, 4DO reference/result generators, HQx rule-table generation, differential harness
docs/                        this documentation set
```

---

## 2. Dependency graph

```text
FiveDo.Frontend.Avalonia ---> FiveDo.Host
        |                         |
        +--> FiveDo.Video.OpenGL  +--> FiveDo.Machine
                                  +--> FiveDo.Cheats
                                  +--> FiveDo.Tas
                                  +--> FiveDo.Storage
                                  +--> FiveDo.Audio
                                  +--> FiveDo.Input
                                  +--> FiveDo.Video
                                  +--> FiveDo.Diagnostics
                                  +--> FiveDo.Core

FiveDo.Cli ----------------> FiveDo.Host

FiveDo.Machine ------------> Core, Cpu, Memory, Madam, Clio, Dsp, Sport,
                             Video, Xbus, Cdrom, Diagnostics

FiveDo.Video.OpenGL -------> Core, Video
FiveDo.Storage ------------> Core (+ CHDSharp)
FiveDo.Audio --------------> Core (+ Silk.NET.OpenAL)
FiveDo.Input --------------> Core
FiveDo.Cheats ------------> Machine, Input
FiveDo.Tas --------------> Core, Input
FiveDo.Scripting --------> Core, Input, Tas, MoonSharp
FiveDo.Video --------------> Core
FiveDo.Diagnostics --------> Core
FiveDo.Memory -------------> Core
FiveDo.Cpu ----------------> Core
FiveDo.Madam --------------> Core
FiveDo.Clio ---------------> Core
FiveDo.Dsp ----------------> Core
FiveDo.Sport --------------> Core
FiveDo.Xbus ---------------> Core
FiveDo.Cdrom --------------> Core
```

**Rules:**

1. Arrows point downward only. `FiveDo.Core` has no project references.
2. No chip references another chip; they communicate through Core interfaces and are
   wired by `FiveDo.Machine`.
3. The core is platform-neutral: no UI, file I/O, wall clock, or native interop in
   `Core`/chips. `FiveDo.Host` and frontends own the outside world.
4. `FiveDo.Video.OpenGL` consumes `VdlFrame` only; it never touches emulated state.
5. The CLI and desktop both consume `FiveDo.Host`; neither is referenced by the core.

---

## 3. Project catalog

| Project | Responsibility | Key types | References |
|---|---|---|---|
| `FiveDo.Core` | Contracts, shared types, constants, fix mode, logging, state primitives | `IAddressBus`, `IRam`, `ICpuBackend`, `IInterruptSink`, `IFrameSink`, `IAudioSink`, `IPbusSource`, `IDiscSource`, `IXbusDevice`, `IVdlp`, `IMadamBus`, `VdlFrame`, `VdlLine`, `FrameBuffer`, `FixMode`, `EmulatorTuning`, `IStateReader/Writer`, `StateFormat`, `StateSectionIds` | none |
| `FiveDo.Cpu` | ARM60 interpreter, optional managed IL JIT, decoder, ALU, shifter, disassembler, Portfolio OS SWI HLE | `Arm60`, `Arm60State`, `ArmDecoder`, `ArmAlu`, `ArmBarrelShifter`, `ArmDisassembler`, `ArmSwiHle` | Core |
| `FiveDo.Memory` | RAM/ROM/NVRAM, word-swapped representation, mirrors, bus decode | `FiveDoMemory`, `Ram`, `RomBank`, `Nvram` | Core |
| `FiveDo.Madam` | MADAM registers, CEL engine, pixel decoder/processor/projector, matrix, PLUT, PBUS | `Madam`, `CelEngine`, `CelBitReader`, `PixelDecoder`, `PixelProcessor`, `Projector`, `CelRasterizer`, `CelGeometry`, `CcbParser` | Core |
| `FiveDo.Clio` | CLIO registers, timers, FIFOs, DMA, interrupts, DSP interface, XBUS window, ROM select, diag port | `Clio`, `DiagPort` | Core |
| `FiveDo.Dsp` | DSP instruction decode/execute, ALU, multiplier, NMem/IMem, semaphore, sample output | `Dsp`, `DspInstruction` | Core |
| `FiveDo.Sport` | VRAM fill/copy, high-res plane mirroring | `Sport` | Core |
| `FiveDo.Video` | VDLP display-list parser, line/frame assembly, CPU oracle, mailbox, viewport, CPU fallback, shared HQx rule table, CPU reference filter, and MMPX CPU reference filter | `Vdlp`, `FrameAssembler`, `GpuPalette`, `VdlFrameUploader`, `VdlFrameMailbox`, `ViewportCalculator`, `CpuVideoRenderer`, `RendererSettings`, `IVideoRenderer`, `HqxFilter`, `HqxRuleTable`, `MmpxFilter` | Core |
| `FiveDo.Video.OpenGL` | Desktop GL/GLES presenter, two-stage palette/scale, filters, shaders, swap interval | `OpenGlRenderer`, `OpenGlCapabilities`, `SwapInterval`, `Shaders`, `GlContextAdapter`, `OffscreenRenderTarget` | Core, Video, Silk.NET.OpenGL |
| `FiveDo.Video.Vulkan` | Optional offscreen Vulkan 1.0 presenter, build-time SPIR-V palette/scale/HQx/MMPX/xBR/FSR shaders, accurate/nearest/sharp-bilinear/bicubic/CRT/xBR/FSR/HQx/MMPX, readback | `VulkanOffscreenRenderer`, generated `VulkanShaders` | Core, Video, Silk.NET.Vulkan |
| `FiveDo.Xbus` | XBUS controller, device slots, poll/status/data FIFOs | `Xbus` | Core |
| `FiveDo.Cdrom` | MEI CD-ROM command state machine, TOC/MSF/LBA, sector streaming | `CdromDevice` | Core |
| `FiveDo.Storage` | ISO/BIN/CUE/CHD readers, prefetch, BIOS/NVRAM, volume header, game ID/database | `IsoImage`, `BinImage`, `CueSheet`, `ChdDiscImage`, `PrefetchingDiscSource`, `DiscImageFactory`, `BiosImage`, `NvramImage`, `VolumeHeader`, `GameId`, `GameDatabase`, `GameDatabaseScanner`, `GameDatabaseNameParser` | Core, CHDSharp |
| `FiveDo.Audio` | Ring buffer, WAV capture, OpenAL output, soft limiter, tap, analysis | `AudioRingBuffer`, `AudioPipeline`, `WavAudioSink`, `OpenAlAudioOutput`, `AudioSoftLimiter`, `AudioTap`, `AudioAnalyzer`, `AudioStatistics` | Core, Silk.NET.OpenAL |
| `FiveDo.Input` | Controller state, PBUS encoding, profiles, scripts, keyboard/gamepad mapping, input recording | `ControllerState`, `PbusEncoder`, `InputProfile`, `KeyboardInputMapper`, `InputManager`, `GamepadMapper`, `InputScript`, `InputRecorder` | Core |
| `FiveDo.Diagnostics` | Logging, bug reports, launch statistics, donation verification, PNG/frame dumps, CPU tracing, timing, benchmark gates | `BugReportSink`, `ApplicationStatsClient`, `DonationVerificationClient`, `PngDumper`, `FrameDumper`, `CpuTracer`, `FrameTiming`, `BenchmarkGate`, `BenchmarkComparison` | Core |
| `FiveDo.Machine` | Composition root, frame loop, Quartz scheduler, state sections, device wiring | `FiveDoMachine`, `Quartz` | Core + chips |
| `FiveDo.Cheats` | Per-game cheat parser, typed/conditional/button engine, atomic JSON store | `CheatSet`, `CheatEntry`, `CheatDocument`, `CheatStore` | Machine, Input |
| `FiveDo.Tas` | TAS movie container, recorder/playback PBUS sources, branch/trim editor, frame/PC/state verifier | `MovieFile`, `Movie`, `MovieHeader`, `MovieRecorder`, `MoviePbusSource`, `MovieEditor`, `MovieVerifier` | Core, Input |
| `FiveDo.Scripting` | Sandboxed Lua input scripting and frame/button emission | `LuaInputScript` | Core, Input, Tas, MoonSharp |
| `FiveDo.Host` | Core thread/pacing, settings, BIOS/disc/NVRAM loading, audio/input backends, save slots, library, fixes, autopilot | `CoreRunner`, `AudioFramePacer`, `AppSettings`, `AppSettingsStore`, `NvramStore`, `SaveStateSlotManager`, `StateInspector`, `GameLibraryScanner`, `GameCompatibility`, `MemoryPatch`, `RoadRashAutopilot` | Core, Machine, Storage, Audio, Input, Video, Diagnostics |
| `FiveDo.Cli` | Headless commands, JSON contracts | `Program`, `CliJsonModels` | Host |
| `FiveDo.Frontend.Avalonia` | Desktop shell, library, settings, input, save slots, logs, donation banner, GPU probes | `MainWindow`, `VideoSurface`, `SettingsWindow`, `InputBindingsWindow`, `SaveSlotsWindow`, `GameLibraryWindow`, `LogsWindow`, `WelcomeWindow`, `DonationEmailWindow`, `GlProbeWindow`, `GlProbeOptions`, `VkProbeRunner`, `VkProbeOptions`, `GlBackendOption`, `PlatformGamepadBackend` | Host, Video.OpenGL, Video.Vulkan, Avalonia |
| `FiveDo.Tests` | Unit/integration/golden/CLI/frontend tests | subsystem fixtures, ARM test assembler/rig, fake chips/MMIO, PNG writer | all production projects |
| `FiveDo.Benchmarks` | Synthetic/real-content frame benchmark, gate, baseline | `Program` | Cpu, Madam, Dsp, Diagnostics, Machine, Storage, Video |
| `FiveDo.DiffHarness` | 5DO vs OperaCore differential runs, manifests, allowlists, reports, baseline gate | `Program`, `DiffManifestLoader`, `DiffComparer`, `OperaCoreHost`, `DiffReportWriter` | Core, Video, Audio, Input, Storage |

---

## 4. Runtime composition

`FiveDoMachine` is the composition root:

```csharp
var memory = new FiveDoMemory(tuning);
var clio   = new Clio(tuning);
var sport  = new Sport(memory.Ram, compatibility);
var vdlp   = new Vdlp(memory.Ram, compatibility);
var madam  = new Madam { Vdlp = vdlp, Interrupts = clio, Fifo = clio,
                         Bus = memory.Bus, Ram = memory.Ram, Compatibility = compatibility };
var xbus   = new Xbus { Interrupts = clio };
var cdrom  = new CdromDevice();
xbus.Attach(cdrom);
var dsp    = new Dsp { Host = clio };

clio.Ram = memory.Ram;
clio.RomSelector = memory;
clio.Xbus = xbus;
clio.Dsp = dsp;
clio.Madam = madam;

memory.Madam = madam;
memory.Clio = clio;
memory.Sport = sport;
memory.DiagPort = diag;

var cpu    = new Arm60(memory.Bus, clio, tuning, compatibility);
var quartz = new Quartz(tuning, () => clio.TimerDelay);
```

Wiring rules:

- **Interrupts:** every device raises FIQ through `IInterruptSink` (CLIO).
- **CEL memory:** MADAM gets the raw `Ram` object because the CEL engine uses raw
  `mread`/`mwrite` semantics that bypass the MMIO map.
- **VDLP memory:** `Vdlp` reads VRAM at the +2 MB offset, matching
  `_mem_read32((addr & 0xFFFFF) + 2 MB)`.
- **SPORT memory:** `Sport` gets raw `Ram` for the VRAM pointer behavior.
- **XBUS:** device 0 is the CD-ROM; up to 16 slots.
- **DSP:** CLIO owns the register windows; DSP raises audio FIQ through CLIO.
- **Frame:** VDLP writes `VdlFrame`; `FiveDoMachine` calls `IFrameSink.OnFrame` at V1.
- **Input:** `FiveDoMachine` asks `IPbusSource` for the PBUS packet at V1 (16-byte legacy joypad layout or the Opera device chain, up to the 256-byte MADAM PBUS buffer).

---

## 5. Frame loop

`FiveDoMachine.RunFrame` mirrors `_3do_Frame`:

```text
Clio.OnFrame()
total = 0; batch = 0; frameCycles = ArmClock / 60
while total < frameCycles:
    if Madam.Fsm == InProcess:
        Madam.HandleCel(); Madam.Fsm = Idle; continue
    if instructionObserver != null:
        observer(PC, CPSR, Bus.Read32(PC))
    batch += Cpu.Step()
    if batch < 16: continue
    InternalFrame(batch, skipFrame)
    total += batch; batch = 0
framesExecuted++; lastFrameCycles = total
```

`InternalFrame` mirrors `_3do_InternalFrame`:

1. `Quartz.PushArmCycles(cycles)`.
2. If `QueueDsp()`: `Dsp.Loop()`, increment `AudioSamplesProduced`, push to the sink.
3. If `QueueTimer()`: `Clio.DoTimers()`.
4. If `!QueueVdl()`: return.
5. `line = Quartz.VdCurrLine`; `Clio.UpdateVcnt(line, halfFrame)`.
6. If not `skipFrame`: `Vdlp.DoLine(line, frame)`.
7. If `line == 16 && skipFrame`: `FrameSink.OnFrameTrigger()`.
8. If `line == Clio.V0Line`: FIQ reason `1 << 0`.
9. If `line != Clio.V1Line`: return.
10. FIQ reason `1 << 1`; copy PBUS input; `Madam.KeyPressed`.
11. Set `SourceWidth`/`SourceHeight` (640x480 in high-res), widen to the widest line.
12. If not `skipFrame`: `FrameSink.OnFrame(frame)`.

**Invariants:**

- The reference processes at most one DSP/timer/VDL event per `InternalFrame` call.
  Do not drain all due events in a loop without golden-frame evidence.
- The 16-cycle batching threshold (`batch < 16`) is reference behavior.
- `Quartz.PushArmCycles` expects batches < 256 cycles; `clks << 24` would overflow.
- The DSP must run even when no audio sink is attached; the guest uses it for
  allocation/semaphore handshakes.
- The VDL frame is reused; the sink must copy/upload before returning.

`RunFrames(n)` simply calls `RunFrame()` n times. `RenderFrameMultitask()` renders 256
lines from the current VDLP state for the optional multitask path.

---

## 6. Subsystem invariants

### Core contracts

- `IAddressBus` preserves FreeDOCore's decode order and word-swapped byte convention.
- `IRam` is raw RAM/VRAM access; offsets are byte offsets into the allocation, not CPU
  addresses.
- `IStateSection` writes explicit fields; never serialize object layouts.
- `VdlFrame`/`VdlLine` are reusable; copy before cross-thread handoff.
- `AudioSample` packs left in bits 0..15 and right in bits 16..31.
- `FixMode` bits are persisted; never renumber them.

### CPU

- One instruction per `Step()`, with the reference's cycle count.
- Preserve decode order, C++ switch fallthrough, banked registers, the CLIO PC-adjust
  quirk, Anvil/Crash 'n Burn hacks, and immediate data-processing carry-out.
- All mutable state is instance state; independent machines can run in parallel.
- The interpreter is the compatibility oracle; a future JIT must be bit-identical.

### Memory

- RAM/ROM are `uint[]` words whose values match a little-endian C++ build after
  `_bswap`/`memcpy`.
- Byte accesses use lane `addr & 3`; halfword accesses use `addr & 3`; CPU byte/halfword
  accesses apply `addr ^ 3`/`addr ^ 2`.
- NVRAM is a `byte[32 * 1024]` because FreeDO maps one byte per 32-bit word.
- The RAM backing includes 16 reserved 1 MB high-res mirrors; only 3 MB are CPU-visible.
- ROM loading converts big-endian BIOS words with `ReadUInt32BigEndian`.

### MADAM/CEL

- `Madam` owns 2048+64 registers, PLUT, PBUS queue, CEL FSM, matrix results.
- `CelEngine` walks CCBs; `CelRasterizer` draws packed/literal/LR/arbitrary CELs.
- `CelMemory` accounts +1/+2/+4 CEL cycle costs.
- `CelRasterizer.PreparePipeline` caches the CCB's pixel source/target, modulos, PLUT,
  flags, and control words once per CEL. It must run after `PrepareGeometry` and before
  any pixel write; batched fast paths that bypass `CelMemory` must add the same
  read/write cycle counts explicitly.
- High-res `ResScale` addresses four 1 MB VRAM planes; preserve plane 0/1 vs 2/3
  semantics.
- Fix-mode quirks (`BitTiming3/6`, `GraphicsStepY`, speedfixes, unknownflag11) are
  applied in the CEL path.

### CLIO

- Registers/timers/FIFOs/DMA/interrupts; `DoTimers` and `UpdateVcnt` drive the frame.
- XBUS/DSP windows are routed through CLIO.
- ROM bank select is register `0x84`.
- The PC-adjust pending flag is consumed by the CPU/memory.

### DSP

- 16-bit fixed-point core with NMem (2048) and IMem (1024), semaphore, multiplier, ALU,
  barrel shifter.
- `Loop()` executes one DSP instruction/sample event; precomputed tables are rebuilt on
  load.
- Semaphore handshakes must work without an audio sink.
- Sample output is packed left/right; the host clock is exact 44.1 kHz.

### SPORT

- Fill/copy engine with color/source/destination registers.
- In high-res mode, copies mirror across all four 1 MB planes.

### VDLP/Video

- Parses display lists at `(addr & 0xFFFFF) + 2 MB`.
- Line widths 320/384/512/640/1024; `VdlLine.MaxPixels = 1280`.
- High-res line-doubles 640x240 to 640x480 via planes.
- The CPU `FrameAssembler` is the oracle; the GPU `Accurate` mode must match.

### XBUS/CD-ROM

- `Xbus` owns up to 16 `IXbusDevice` slots; device 0 is the CD.
- `CdromDevice` implements MEI commands, poll/status/data, TOC/MSF/LBA, and the
  READ_ID media-access pulse (two polls >= 50 frames apart set poll bit `0x40`).
- CD timing is approximate; do not port `opera_cdrom.c` wholesale without evidence.

### Storage

- `IDiscSource` normalizes every container to 2048-byte sectors.
- ISO direct; BIN 2352 with payload at 16/24; CUE first data track; CHD CD via CHDSharp.
- `GameId` is MD5 of the first two sectors.
- `BiosImage` requires exactly 1 MB BIOS1; BIOS2 optional <=1 MB.
- `NvramImage` fresh header matches 4DO.
- `PrefetchingDiscSource` is opt-in and bounded.

### Audio

- Exact 44.1 kHz DSP clock; packed sample format.
- Ring buffer is SPSC, power-of-two, drop-on-full.
- OpenAL fill loop rents released buffers only; variable-size blocks; 8 buffers;
  3072-frame prefill; soft limiter 0.8/0.9.
- `IAudioSink.Reset` drops old audio on reset/state load.
- WAV capture is raw DSP output; tap records exact queued PCM.

### Input

- `ControllerButton` flags map to the two PBUS bytes.
- Legacy PBUS order is 1, 3, 2, 5, 4, 6; active high byte has bit 7 set. Ports 7-8 and mixed peripherals use the Opera daisy chain.
- `InputScript` frames are absolute; `--script-base state` offsets.
- `InputRecorder` coalesces unchanged player-1 states into the same script format;
  `InputManager.Recorder` is called on the emulation thread and `FrameProvider`
  supplies the frame number. The desktop writes `<game>-<timestamp>-input.txt`.
- Keyboard/gamepad profiles are per-player; gamepad fallbacks for sticks/triggers.
- `HotkeyRouter` resolves the persisted console hotkeys; the shell dispatches
  them before forwarding a key to the emulated pad. F11 works from the library,
  and Escape leaves fullscreen before the pause action runs. `EscapeQuitPolicy`
  turns Escape into a two-step shortcut: pause first, close the shell on the next
  press while still paused. It is disarmed when emulation resumes or stops, when the
  quick menu opens, and when the window loses focus. F6/F7 change slots;
  F9 steps one frame through `CoreRunner.StepFrame` and leaves emulation paused.

### Diagnostics

- `Log` levels/categories; no timestamps in core logs.
- `CpuTracer`, `FrameTiming`, `PngDumper`, `FrameDumper`.
- `BenchmarkGate`: 0 bytes/frame, core p99 <= 8 ms, total mean <= 8 ms, total p99
  <= 16.667 ms.
- `BenchmarkComparison`: same-machine, default 10% regression, exit 4.

### Machine/Quartz

- `FiveDoMachine` owns reset order, frame loop, state sections, device wiring.
- `Quartz` uses 8.24 fixed point for ARM/VDL/timer and an exact rational DSP clock.
- Reset order: CPU, memory, VDLP, SPORT, MADAM, XBUS, CLIO, DSP, Diag, Quartz; audio
  sink reset.
- State load restores sections and calls `AudioSink.Reset`.

### Host

- `CoreRunner` owns the core thread; all machine access goes through `Invoke`.
- `AudioFramePacer` derives the period from samples; 60 Hz fallback; 50-70 clamp.
- `AppSettings`/`AppSettingsStore` are source-generated JSON, atomic writes.
- `NvramStore` 250 ms debounce, atomic writes, `.bad` on corruption.
- `SaveStateSlotManager` slots/metadata/thumbnails; state header authoritative.
- `GameLibraryScanner` extensions/depth/cover art.
- `GameCompatibility` maps 4DO IDs to fix bits.
- `RoadRashAutopilot` is a test harness; do not generalize it without evidence.

### CLI

- `Program` dispatches commands; `ParsedOptions` parses `--name value`/flags.
- JSON contracts are source-generated in `CliJsonModels.cs`.
- Most JSON is camelCase; `state inspect`/`state slots` are PascalCase.
- Exit codes 0/1/2; benchmark 3/4.
- Diagnostics write text files; `--frame-out` PNG/BMP; `--dump-dsp` little-endian words.

### Frontend

- `MainWindow` owns the shell, session lifecycle, settings, library, logs, screenshots.
- `VideoSurface` hosts the GL context and `OpenGlRenderer`.
- `CpuVideoRenderer` + `WriteableBitmap` fallback.
- `SettingsWindow`, `InputBindingsWindow`, `SaveSlotsWindow`, `GameLibraryWindow`,
  `LogsWindow`, `WelcomeWindow`.
- `PlatformGamepadBackend` selects XInput (Windows), evdev (Linux), or IOKit HID
  (macOS); the macOS source maps Generic Desktop joystick/gamepad elements through
  `MacOsHidGamepadMapper` and the same eight-player profile. The mapper is unit-tested
  on every host; hardware detection requires a macOS host.
- `GlProbeWindow`/`GlProbeOptions` implement the GPU probe.
- Compiled bindings, no reflection-based `ViewLocator`.

### Tests/benchmarks

- One xUnit project with subsystem folders; opt-in real BIOS/GPU/audio tests.
- `TestUtilities` has an ARM assembler/rig, fake chips/MMIO, PNG writer.
- `FiveDo.Benchmarks` is a stopwatch harness, not BenchmarkDotNet; it measures core,
  pack, mailbox, read copy, allocations, p50/p95/p99, gate, baseline.

---

## 7. Threading model

| Thread | Owns | Rules |
|---|---|---|
| Core thread | CPU, memory, chips, scheduler, DSP, VDLP, CD | Single-threaded, deterministic; `CoreRunner.Run` calls `RunFrame`; UI actions use `Invoke`. |
| Render/UI thread | Avalonia window, GL context, presenter | Consumes `VdlFrameMailbox`; never calls `RunFrame`; renders on demand. |
| Audio fill thread | OpenAL source/buffers | Consumes the ring; never calls into the core; makes the context current on its own thread. |
| Host timers | status/log refresh | Low rate (4-10 Hz); no per-frame formatting. |

**Cross-thread state:** audio ring, latest-frame mailbox, session lifecycle flags. No
locks in the core hot path. `CoreRunner.Invoke` pauses the loop, runs the action, and
resumes. `VdlFrameMailbox.Read` copies under a short lock and invokes the renderer
outside it.

**Pacing:** `AudioFramePacer` (30-frame average, 50-70 fps clamp, 60 Hz fallback) sets
`FramePacer`'s period. `FramePacer` sleeps for the bulk of the wait and spins for the
last millisecond; a >100 ms stall resyncs rather than chasing the past.

---

## 8. State ownership

| State | Owner | In `.tdos`? |
|---|---|---|
| CPU registers/CPSR/banked registers/FIQ/ROM select/data abort | `Arm60` | yes |
| Full RAM backing (largest layout: 14 MB DRAM + four 2 MB VRAM planes) | `FiveDoMemory` | yes |
| ROM banks | `FiveDoMemory` | yes |
| NVRAM | `FiveDoMemory` | yes |
| MADAM registers/PLUT/PBUS/CEL FSM/matrix temps | `Madam` | yes |
| CLIO registers/timers/FIFOs/DMA | `Clio` | yes |
| DSP registers/NMem/IMem/semaphore/seed | `Dsp` | yes |
| SPORT color/source/destination | `Sport` | yes |
| VDLP head/current bitmap/palette/line delay | `Vdlp` | yes |
| XBUS select/poll/FIFOs/attached device state | `Xbus` | yes |
| CD command/status/data/TOC | `CdromDevice` | yes (also via XBUS) |
| Quartz accumulators/line counter | `Quartz` | yes (v2) |
| Compatibility flags + VDL frame | `FiveDoMachine` | yes (Machine section) |
| Disc contents | host `IDiscSource` | no (hash recorded, not validated) |
| Input state | host | no |
| Audio queue | host audio backend | no |
| Settings/save metadata | host | no |

---

## 9. Error handling and logging

- **Fatal configuration errors** (missing/invalid BIOS, unsupported disc, invalid state)
  throw host/storage exceptions (`InvalidDataException`, `DiscImageException`,
  `InvalidStateException`, `ArgumentException`). The CLI catches these and exits 1.
- **Non-fatal emulation anomalies** (unmapped MMIO, unknown CD command, malformed CEL)
  log at Warning/Debug and continue with the reference sentinel behavior
  (`0xBADACCE5`, zero, etc.).
- **No exceptions in the hot path.** Device methods return values; diagnostics use
  Serilog level guards.
- **Logging:** Serilog with levels/categories. `LoggingSetup` configures rolling
  files, the in-memory viewer, stderr (desktop), and the bug-report sink. The
  desktop uses `InMemorySerilogSink` (4000 entries). The CLI keeps stdout/stderr
  for its JSON/product contract and writes diagnostics to the file/API.
- **State warnings:** `FiveDoMachine.StateWarnings` reports version/BIOS/unknown-section
  warnings; the CLI includes them in `run --json` and the UI logs them.
- **Core faults:** `CoreRunner.Faulted` reports the exception; the thread stops and
  logs through Serilog.

---

## 10. Extension points

| Extension | Interface | Existing examples |
|---|---|---|
| Video presenter | `IFrameSink`, `IVideoRenderer` | OpenGL/GLES, CPU `WriteableBitmap`, null/hash sink |
| Audio output | `IAudioSink` | null, WAV, OpenAL |
| Input backend | `IPbusSource`, `IGamepadBackend` | keyboard, scripted, XInput, evdev, IOKit HID |
| Disc format | `IDiscSource` | ISO, BIN, CUE, CHD, prefetch wrapper |
| Save state | `IStateReader`/`IStateWriter`, `IStateSection` | native `.tdos` sections |
| CPU backend | `ICpuBackend` | ARM60 interpreter; optional IL JIT (not implemented) |
| XBUS device | `IXbusDevice` | CD-ROM; future peripherals |
| Renderer/filter | `IVideoRenderer`, shader variants | nearest, sharp-bilinear, bicubic, CRT, xBR, HQx, MMPX, FSR |
| Logging | `Serilog.ILogger`, `ILogEventSink` | file, stderr, in-memory viewer, bug-report API |
| Benchmark policy | `BenchmarkGate`, `BenchmarkComparison` | CI gate, same-machine baseline |

### Adding a renderer filter

For HQx-style pattern filters, use the shared `HqxRuleTable` generated by
`tools/generate_hqx_rules.py` instead of duplicating the 256-pattern rules in
each shader language. The table encodes the pattern/subpixel blend rules and is
uploaded once as an `RG32UI` texture.

1. Add the enum value in `RendererContracts.cs`.
2. Implement the pass in `OpenGlRenderer` and shaders in `Shaders.cs` (GLSL 330 + ES
   300 variants).
3. Add settings/validation if it has parameters.
4. Add tests: shader source, settings, parity/cost if applicable.
5. Add UI enum (the combo binds `Enum.GetValues<VideoFilter>()`) and docs.
6. Measure at 4K; keep it opt-in if expensive.

### Adding a disc format

1. Implement `IDiscSource` (2048-byte sectors, `SectorCount`, `ReadSector`).
2. Add detection/opening in `DiscImageFactory`.
3. Add tests for geometry, payload offset, errors.
4. Update `GameLibraryScanner` extension list and docs.
5. Keep the CD device container-agnostic.

### Adding a state section

1. Choose a stable ID in `StateSectionIds` (never renumber).
2. Implement `IStateSection` on the owner.
3. Write/read explicit fields; bump the section version if the layout changes.
4. Add it to `FiveDoMachine.SaveState` and the required set/switch in `LoadState`.
5. Add roundtrip/truncation/CRC tests and docs.

### Adding a CLI command

1. Add the dispatch in `Program.Main`.
2. Parse with `ParsedOptions`; add a result record + `[JsonSerializable]` in
   `CliJsonModels.cs`.
3. Add `--json` and text output; keep exit codes consistent.
4. Add process/JSON tests.
5. Document in `CliReference.md`.

---

## 11. Coding conventions

- File-scoped namespaces; one public top-level type per file.
- `sealed` unless inheritance is required.
- `readonly` fields where possible; no mutable static state (constants/tables are fine).
- `uint`/`ushort`/`byte` for emulated registers; `int` only where the reference uses
  signed arithmetic.
- Explicit `unchecked` around intentional wraparound.
- Constants for masks/register offsets; no bare magic numbers in new code.
- XML documentation on public types; ported code includes `// FreeDO: <file>:<function>`.
- Tests named `Method_Scenario_ExpectedResult`.
- No `unsafe` unless a measured hot path requires it; `Unsafe`/`MemoryMarshal` are
  acceptable.
- No LINQ, string formatting, delegate allocation, or managed allocation in
  per-instruction/per-frame paths.
- Prefer concrete types/generic constraints over interface dispatch in hot loops; keep
  Core interfaces at architectural boundaries.
- Shaders live in `FiveDo.Video.OpenGL/Shaders.cs`, have GLSL 330/ES 300 variants, and
  are embedded/tested.
- JSON uses source-generated contexts; do not add reflection-based `JsonSerializer`
  overloads to trimmed paths.
- Keep the trimmed/AOT paths healthy: no reflection, no dynamic assembly loading, no
  `TypeDescriptor` on first-party paths.

---

## 12. How to make a change

1. **Reproduce and reference.** Identify the FreeDO/Opera behavior and the expected
   output. Add a failing test or record a real-content hash.
2. **Choose the layer.** Core contract if cross-chip; chip project if internal; host if
   OS/wall-clock; frontend if UI.
3. **Keep determinism.** No wall clock, no random, no hidden globals, no thread in the
   core.
4. **Add state.** If the feature is stateful, add a section/fields and roundtrip tests.
5. **Add diagnostics.** A trace/log/CLI flag is cheaper than guessing.
6. **Test.** Unit + integration + golden/differential where applicable; opt-in
   real-content/GPU/audio tests for hardware paths.
7. **Measure.** Run the benchmark/gate; for presentation, run the GPU probe.
8. **Format.** `dotnet format 5DO.sln --verify-no-changes` (or apply).
9. **Document.** Update the user/reference/developer docs and `CHANGELOG.md`.
10. **Validate trimmed/AOT.** Publish trimmed and run the smoke tests if you touched
    JSON, reflection, or startup.

Definition of done for a ported subsystem:

- Reference behavior documented.
- Unit tests for edge cases.
- Golden/differential evidence where content allows.
- Save/load roundtrip if stateful.
- No steady-state allocations.
- Diagnostics available.
- Docs updated.

---

## 13. Debugging entry points

| Entry | Use |
|---|---|
| `FiveDoMachine.FrameSink` | Capture/replace frame output. |
| `FiveDoMachine.PbusSource` | Script input. |
| `FiveDoMachine.AudioSink` | WAV/OpenAL/custom sink. |
| `FiveDoMachine.StateWarnings` | Inspect load warnings. |
| `FiveDoMachine.Memory.RamWriteObserver` | RAM watchpoints. |
| `FiveDoMachine.Clio.TraceWriter` / `FiqObserver` | CLIO/FIQ traces. |
| `FiveDoMachine.Cdrom.TraceWriter` / `CommandObserver` | CD traces/stop conditions. |
| `FiveDoMachine.Madam.KPrint` / `CelTraceWriter` | KPRINT/CEL traces. |
| `FiveDoMachine.Dsp.DspTraceEnabled` / `DspTrace` | DSP instruction ring. |
| `CoreRunner.Faulted` | Core thread exceptions. |
| `CoreRunner.Invoke` | Safe machine access from UI. |
| `VdlFrameMailbox` | Frame handoff. |
| `StateInspector` | Header/section inspection. |
| `BenchmarkGate`/`BenchmarkComparison` | Performance policy. |
| `GlProbeWindow` | Real GL path measurement/parity. |

CLI equivalents are in [`Diagnostics.md`](Diagnostics.md).

---

## 14. Reference mapping

| FreeDO/4DO source | C# counterpart |
|---|---|
| `arm.cpp` | `FiveDo.Cpu/Arm60`, `Arm60State`, `ArmDecoder`, `ArmAlu`, `ArmBarrelShifter` |
| `_3do_sys.cpp`, `frame.cpp` | `FiveDo.Machine/FiveDoMachine`, `FiveDo.Video/FrameAssembler` |
| `Clio.cpp` | `FiveDo.Clio/Clio`, `DiagPort` |
| `Madam.cpp`, CEL sources | `FiveDo.Madam/Madam`, `CelEngine`, `CelRasterizer`, `Pixel*`, `Projector` |
| `DSP.cpp` | `FiveDo.Dsp/Dsp`, `DspInstruction` |
| `SPORT.cpp` | `FiveDo.Sport/Sport` |
| `vdlp.cpp` | `FiveDo.Video/Vdlp` |
| `XBUS.cpp`, `Iso.cpp` | `FiveDo.Xbus/Xbus`, `FiveDo.Cdrom/CdromDevice`, `FiveDo.Storage` |
| `quarz.cpp` | `FiveDo.Machine/Quartz` |
| `DiagPort.cpp` | `FiveDo.Clio/DiagPort` |
| `Filters/` | `FiveDo.Video.OpenGL` shaders/filters |
| `Worker.cpp` | `FiveDo.Host/CoreRunner`, `AudioFramePacer` |
| `GameDatabase.xml` | `FiveDo.Storage/GameDatabase` (embedded resource) |
| Opera/libopera | Behavioral reference, not a source port; see [`ReferenceStrategy.md`](ReferenceStrategy.md) |

---

## 15. Known architectural debt

These are real, current limitations. Do not "fix" them casually without tests.

1. **Word-swapped memory representation.** RAM/ROM words match a little-endian C++
   build; byte/halfword accesses apply lane XORs. A clean big-endian refactor would
   touch hundreds of places and must be guarded by the full suite/state compatibility.
2. **Large save states.** The Memory section writes the live RAM/VRAM range in the
   backing, including high-res mirrors; the Machine section writes all 960 VDL lines.
   Large sections are Brotli-compressed (typically 0.6-2.0 MB), but the in-memory state
   and the uncompressed payloads remain ~10 MB for the stock layout and ~26 MB for the
   largest. Version-1 states are still readable. Further reductions (trimming duplicate
   mirrors or the ROM) are a format change.
3. **Disc hash is warned, not enforced.** Loading a state from a different disc adds
   a `StateWarnings` entry but still loads; the CLI and desktop surface the warning.
4. **`Renderer` and `OutputResolution` are honored at startup and when Settings is
   applied.** The shell can use OpenGL, Vulkan, or the CPU renderer; changing the
   backend recreates the active presenter without restarting emulation. Fixed output
   resolutions render offscreen and scale into the window. GL init failure or context
   loss still falls back automatically.
5. **`fs` has its own Opera parser.** `FiveDo.Storage.OperaFileSystem` reads the
   volume/directory records directly from any `IDiscSource`, so ISO, BIN, CUE+BIN,
   and CHD share one code path. `disc dump`, `info`, and `disc tracks` remain the
   raw-container inspection tools.
6. **CLI JSON casing is split.** `state inspect`/`state slots` are PascalCase for
   historical compatibility; other commands are camelCase.
7. **Some stale code comments.** For example, `EmulatorTuning.HighResolution` says the
   mode is reserved, but it is implemented; `Vdlp` says high-res is left for M11, but it
   is implemented. Treat behavior/tests as authoritative.
8. **Frontend NativeAOT audio is validated on Windows.** The opt-in frontend AOT build
   runs the real machine/CoreRunner/OpenAL path through the null and default playback
   devices; the 2026-11-10 10-minute soaks report 0 drops/0 underruns. See
   `Audio.md` §9.
9. **No guest JIT.** The ARM60 interpreter is the only backend; `ICpuBackend` exists
    for a future managed IL JIT.
10. **`AudioPipeline` is not the live path.** It is a reusable host ring sink; the
    desktop/CLI live path uses `OpenAlAudioOutput` and `WavAudioSink` directly.
12. **Custom aspect is edited as a numeric ratio.** `RendererSettings.CustomAspect`
    exists and the Settings dialog exposes a positive width/height field; presets are
    still the common 4:3/16:9 modes.
13. **CD timing is approximate.** The MEI state machine/poll behavior is a reference
    port, not a cycle-accurate drive model. Do not port `opera_cdrom.c` wholesale.
14. **Platform validation gaps.** Windows x64 is validated; Linux/macOS launch, arm64,
    GLES 3.1, Tier B desktop timing, and DPI are incomplete. Raspberry Pi is out of scope.
15. **Old docs drift.** If a doc disagrees with code, fix the doc in the same change;
    this guide and the [documentation index](index.md) are the entry points.

---

## 16. Related documents

- [`Architecture.md`](Architecture.md) - design goals, composition, pipelines.
- [`BuildAndTest.md`](BuildAndTest.md) - build/test/publish/CI.
- [`TestStrategy.md`](TestStrategy.md) - test layers and evidence.
- [`PortingNotes.md`](PortingNotes.md) - C++ to C# rules and quirks.
- [`ReferenceStrategy.md`](ReferenceStrategy.md) - Opera/FreeDO hierarchy.
- [`Audio.md`](Audio.md), [`Video.md`](Video.md) - subsystem deep dives.
- [`SaveStates.md`](SaveStates.md) - state format.
- [`Diagnostics.md`](Diagnostics.md) - traces/probes.
- [`Troubleshooting.md`](Troubleshooting.md) - symptom-driven fixes.
- [`Trimming.md`](Trimming.md) - trim/AOT constraints.
- [`Performance.md`](Performance.md) - budgets and measurements.
- [`Compatibility.md`](Compatibility.md), [`KnownIssues.md`](KnownIssues.md) - current behavior.
