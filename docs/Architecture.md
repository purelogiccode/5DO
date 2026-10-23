# 3DOEmulator Architecture

**Status:** proposed architecture, M0 baseline.
**Scope:** the emulator core, host layer, CLI, Avalonia frontend, tests, and build layout.

---

## 1. Design goals

1. **One console subsystem per project.** The CPU, memory, MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD-ROM, storage, audio, and input each live in their own assembly.
2. **The core is platform-neutral.** `ThreeDo.*` core projects target `net10.0`, use no UI toolkit, no file I/O, no wall clock, and no native interop.
3. **The host owns the outside world.** BIOS files, disc images, NVRAM files, audio devices, gamepads, windows, and settings live in `ThreeDo.Host` and the frontends.
4. **The frontend is replaceable.** The CLI and Avalonia app both consume `ThreeDo.Host`; neither is referenced by the core.
5. **The port is traceable.** Every ported C++ unit has a named C# counterpart and a comment pointing back to the FreeDOCore file/function.
6. **Determinism by construction.** Fixed timestep, integer scheduler, explicit state serialization, no hidden globals, no wall-clock reads inside the core.

---

## 2. System context

```text
┌─────────────────────────────────────────────────────────────────────┐
│                         Frontends (replaceable)                     │
│                                                                     │
│   ThreeDo.Frontend.Avalonia                 ThreeDo.Cli             │
│   - views / viewmodels                      - headless commands     │
│   - video surface                           - frame/state dumps     │
│   - settings / library                      - benchmarks            │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                         ThreeDo.Host                                │
│  EmulatorSession, FramePacer, AudioPipeline, InputManager,          │
│  BiosLoader, NvramStore, DiscLoader, GameDatabase, SaveStateService │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                        ThreeDo.Machine                              │
│  ThreeDoMachine, FrameLoop, Quartz, machine composition,            │
│  state sections, device wiring                                      │
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
                        │   ThreeDo.Core    │
                        │ contracts, types, │
                        │ constants, state  │
                        └───────────────────┘
```

**Dependency rule:** arrows point *downward only*. `ThreeDo.Core` has no project references. No chip project references another chip project; they communicate through small interfaces defined in `ThreeDo.Core` and are wired by `ThreeDo.Machine`. This avoids the circular dependencies that the original C++ code solved with globals and `extern`.

---

## 3. Project catalog

| Project | Responsibility | Key types | References |
|---|---|---|---|
| `ThreeDo.Core` | Contracts, shared value types, constants, compatibility flags, logging abstraction, state reader/writer interfaces | `IAddressBus`, `IRam`, `IInterruptSink`, `IFrameSink`, `IAudioSink`, `IPbusSource`, `IDiscSource`, `IXbusDevice`, `IVdlp`, `IMadamBus`, `VdlFrame`, `VdlLine`, `FrameBuffer`, `FixMode`, `EmulatorTuning`, `IStateReader/Writer` | none |
| `ThreeDo.Cpu` | ARM60 interpreter, condition codes, banked registers, cycle accounting, disassembler | `Arm60`, `Arm60State`, `ArmDecoder`, `ArmAlu`, `ArmBarrelShifter`, `ArmDisassembler` | Core |
| `ThreeDo.Memory` | Physical RAM/ROM/NVRAM, word-swapped representation, mirrors, byte/halfword/word helpers | `ThreeDoMemory`, `Ram`, `RomBank`, `Nvram`, `MemoryAccess` | Core |
| `ThreeDo.Madam` | MADAM registers, CEL engine, pixel decoder/processor/projector, matrix engine, pixel bus, DMA | `Madam`, `MadamRegisters`, `CelEngine`, `CelBitReader`, `PixelDecoder`, `PixelProcessor`, `Projector`, `CelRasterizer`, `MatrixEngine` | Core |
| `ThreeDo.Clio` | CLIO registers, timers, FIFOs, DMA, interrupts, DSP interface, XBUS register window, ROM select | `Clio`, `ClioRegisters`, `ClioTimers`, `ClioFifo`, `ClioDma`, `ClioInterrupts` | Core |
| `ThreeDo.Dsp` | 3DO audio DSP: instruction decode/precompute, ALU, multiplier, barrel shifter, NMem/IMem, semaphore, sample output | `Dsp`, `DspCore`, `DspInstructionDecoder`, `DspAlu`, `DspRegisters`, `DspMemory` | Core |
| `ThreeDo.Sport` | SPORT VRAM fill/copy engine | `Sport` | Core |
| `ThreeDo.Video` | VDLP display-list parser, line/frame assembly, palette expansion, frame extraction, scalers | `Vdlp`, `VdlParser`, `FrameAssembler`, `FrameBufferFactory`, `Scalers/*` | Core |
| `ThreeDo.Xbus` | XBUS controller, device selection, poll/status/data FIFOs, device interface | `Xbus`, `XbusDevice`, `XbusFifo` | Core |
| `ThreeDo.Cdrom` | Mitsumi/MEI CD-ROM device, command state machine, TOC/MSF/LBA, sector streaming | `CdromDevice`, `CdromCommand`, `Toc`, `Msf` | Core |
| `ThreeDo.Storage` | Disc image readers (ISO/BIN/CUE), BIOS validation, NVRAM file format, game database, game ID | `IsoImage`, `BinImage`, `CueSheet`, `DiscImageFactory`, `BiosImage`, `NvramImage`, `GameDatabase`, `GameId` | Core |
| `ThreeDo.Audio` | Sample format conversion, ring buffer, null/WAV sinks, resampling helpers | `AudioRingBuffer`, `AudioSample`, `NullAudioSink`, `WavAudioSink` | Core |
| `ThreeDo.Input` | Controller state, PBUS encoding, input profiles, keyboard/gamepad abstractions | `ThreeDoController`, `PbusEncoder`, `InputProfile`, `InputBinding` | Core |
| `ThreeDo.Diagnostics` | Logging, tracing, disassembly helpers, profilers, memory/frame dumpers | `ILogSink`, `Log`, `CpuTracer`, `MemoryDumper`, `FrameDumper`, `PerformanceCounters` | Core |
| `ThreeDo.Machine` | Machine composition root, frame loop, Quartz scheduler, device wiring, state sections | `ThreeDoMachine`, `MachineOptions`, `FrameLoop`, `Quartz`, `StateSection` | Core + all chips |
| `ThreeDo.Host` | Session lifecycle, frame pacing, BIOS/disc/NVRAM loading, audio/input backends, save states, settings | `EmulatorSession`, `FramePacer`, `AudioPipeline`, `InputManager`, `BiosLoader`, `NvramStore`, `SaveStateService` | Machine, Storage, Audio, Input, Diagnostics |
| `ThreeDo.Cli` | Headless command-line interface | `Program`, `RunCommand`, `InfoCommand`, `BenchCommand` | Host |
| `ThreeDo.Frontend.Avalonia` | Cross-platform desktop UI | `App`, `MainWindow`, viewmodels, views, services | Host + Avalonia |
| `ThreeDo.Tests` | Unit/integration tests (subfolders per subsystem) | test fixtures, ARM assembler, fake sinks | production projects |
| `ThreeDo.Benchmarks` | BenchmarkDotNet benchmarks | CPU, CEL, DSP, frame benchmarks | Cpu, Madam, Dsp, Machine |

### Why `ThreeDo.Machine` is separate from `ThreeDo.Core`

The core contracts project must not know about concrete chips. The machine is the composition root that knows all concrete chips and wires them together. This preserves the dependency rule and makes each chip independently testable with fake collaborators.

---

## 4. Runtime composition

`ThreeDoMachine` is constructed once per emulated console:

```csharp
var memory = new ThreeDoMemory(biosRom, nvram);
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
machine = new ThreeDoMachine(cpu, memory, madam, clio, dsp, sport, vdlp, xbus, cdrom, quartz, tuning);
```

### Wiring rules

- **Interrupts:** every device that can raise a FIQ depends on `IInterruptSink`, implemented by `Clio`. No chip calls `Clio` directly.
- **CEL memory:** `Madam` receives the `Ram` object directly because the original C++ uses raw RAM reads/writes (`mread`, `mwrite`) that bypass the MMIO map.
- **VDLP memory:** `Vdlp` receives `Ram` directly and reads VRAM at the +2 MB offset, matching `_mem_read32((addr & 0xFFFFF) + 2 MB)`.
- **SPORT memory:** `Sport` receives `Ram` directly, matching the original `VRAM` pointer.
- **XBUS devices:** `Xbus` owns up to 16 `IXbusDevice` slots; device 0 is the CD-ROM.
- **DSP interface:** `Clio` owns the DSP register windows and calls `IDsp` methods; `Dsp` raises audio FIQ through `IInterruptSink`.
- **Frame sink:** `Vdlp` writes into a `VdlFrame`; `ThreeDoMachine` calls `IFrameSink.OnFrame` at the VDL swap point.
- **Input:** `ThreeDoMachine` asks `IPbusSource` for the 16-byte PBUS packet once per frame at the V1 line, exactly as FreeDOCore does.

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

---

## 6. Frame execution

`ThreeDoMachine.RunFrame()` mirrors `_3do_Frame`:

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
        ▼
FrameAssembler converts 3DO pixels to BGRA8888:
  - pixel 0 => background color
  - fixed CLUT flag (OUT control bit 25) => FIXED_CLUT
  - otherwise per-line CLUT
        │
        ▼
FrameBuffer (320x240, 384x288, 640x480, ...)
        │
        ├── Avalonia WriteableBitmap (CPU blit, v1)
        └── optional OpenGL/Silk.NET backend (later)
```

The core never knows about Avalonia. `IFrameSink` receives a `VdlFrame`; `ThreeDo.Host` converts and publishes a `FrameBuffer` to the frontend.

### Video rules

- Base output is **320x240, 4:3**, 60 Hz NTSC.
- Wider VDLP modes are parsed but the first playable milestone may crop to 320 pixels. The `VdlLine` capacity is 1280 pixels so 384/512/640/1024 modes can be enabled without changing the data model.
- High-resolution (`RESSCALE`) is a later milestone. It changes VRAM mirroring, VDLP line duplication, CEL stepping, SPORT copies, and frame extraction, so it must be implemented as a coherent feature with its own tests, not as isolated conditionals.
- HQ2X/HQ3X/HQ4X are optional post-processing filters. They are not part of the core accuracy path.

---

## 8. Audio pipeline

```text
DSP core produces a 32-bit packed sample:
  IMem[0x3FE] << 16 | IMem[0x3FF]
        │
        ▼
IAudioSink.PushSample(uint)
        │
        ▼
ThreeDo.Audio.AudioRingBuffer (lock-free SPSC)
        │
        ▼
Host backend:
  - NullAudioSink (tests/CLI)
  - WavAudioSink (debug/replay)
  - OpenAL Soft via Silk.NET.OpenAL (desktop, v1)
  - platform-specific backend (optional later)
```

- Sample rate is **44,100 Hz**, stereo, signed 16-bit.
- The DSP runs on the core thread; the audio backend runs on its own thread and only consumes the ring buffer.
- No allocations in the steady-state audio path.
- Audio-driven frame pacing is a later refinement. v1 uses a fixed 60 Hz pacer with ring-buffer underrun/overrun handling.

---

## 9. Input pipeline

```text
Keyboard / gamepad
        │
        ▼
ThreeDo.Input.InputManager (host)
  - maps physical inputs to ThreeDoControllerState
  - supports 1-6 controllers
        │
        ▼
PbusEncoder.Encode(controllers) -> 16 bytes
        │
        ▼
ThreeDoMachine asks IPbusSource once per frame (V1 line)
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

`ThreeDo.Host` is the only layer allowed to touch the operating system:

- **`EmulatorSession`** - owns a `ThreeDoMachine`, runs a dedicated core thread, exposes `Start/Pause/Resume/Stop/Reset`, `RunFrames(n)`, `SaveState/LoadState`, and frame events.
- **`FramePacer`** - fixed 60 Hz scheduling with `Stopwatch`; optional audio-driven correction later.
- **`BiosLoader`** - validates 1 MB BIOS1 and optional ≤1 MB BIOS2; computes MD5; detects Anvil BIOS (`byte[28] == 234`).
- **`DiscLoader`** - opens ISO/BIN/CUE, detects 2048 vs 2352 sector size, validates sector count.
- **`NvramStore`** - 32 KB NVRAM file, atomic write, debounce (ported from 4DO's 250 ms timer).
- **`GameDatabase`** - MD5-of-first-two-sectors game ID, names, and fix-mode flags.
- **`AudioPipeline`** - ring buffer plus selected backend.
- **`InputManager`** - keyboard/gamepad mapping and PBUS encoding.
- **`SaveStateService`** - slots, versioning, hashes, atomic file writes.
- **`SettingsStore`** - JSON settings with source generation, per-platform config directory.
- **`GameLibraryService`** - scans folders for supported disc images and reads metadata.

### Settings locations

| Platform | Config | Saves | Screenshots |
|---|---|---|---|
| Windows | `%APPDATA%\3DOEmulator` | `%APPDATA%\3DOEmulator\Saves` | `%APPDATA%\3DOEmulator\Screenshots` |
| Linux | `$XDG_CONFIG_HOME/3doemulator` or `~/.config/3doemulator` | same `Saves` | same `Screenshots` |
| macOS | `~/Library/Application Support/3DOEmulator` | `.../Saves` | `.../Screenshots` |

Portable mode: if a `portable.flag` file exists next to the executable, use `<exe-dir>/Data` instead.

---

## 11. State ownership

| State | Owner | Included in save state? |
|---|---|---|
| CPU registers, CPSR, banked registers, FIQ pending | `Arm60` | yes |
| 3 MB RAM (and high-res mirrors derived) | `ThreeDoMemory` | yes (3 MB; mirrors rebuilt) |
| ROM banks | `ThreeDoMemory` | yes (for exact restore) |
| NVRAM | `ThreeDoMemory` | yes |
| MADAM registers, CEL FSM, PLUT, PBUS queue, matrix results | `Madam` | yes |
| CLIO registers, timers, FIFOs, DMA state | `Clio` | yes |
| DSP registers, NMem, IMem, semaphore, running flag, PRNG seed | `Dsp` | yes (precomputed tables rebuilt) |
| SPORT color/source/destination | `Sport` | yes |
| VDLP head/current bitmap/palette/linedelay | `Vdlp` | yes |
| XBUS selection/poll/FIFO state | `Xbus` | yes |
| CD device command/status/data/TOC state | `CdromDevice` | yes |
| Quartz accumulators/line counters | `Quartz` | yes |
| Disc contents | host `IDiscSource` | no; store disc hash and warn on mismatch |
| Input state | host | no (optional replay recording) |
| Derived `VdlFrame` / `FrameBuffer` | machine/host | no; regenerated |

---

## 12. Threading model

- **Core thread:** CPU, memory, chips, scheduler, DSP, VDLP, CD. Single-threaded and deterministic.
- **Audio thread:** consumes the ring buffer; never calls into the core.
- **UI thread:** Avalonia; receives frame snapshots and status updates; never touches emulated state.
- **No locks in the core hot path.** The only cross-thread state is the audio ring buffer, the latest-frame mailbox, and session lifecycle flags.

A `FrameMailbox` holds the most recent completed `FrameBuffer`. If the UI is slow, old frames are dropped rather than queued, preventing latency growth.

---

## 13. Error handling and diagnostics

- **Fatal configuration errors** (missing BIOS, invalid disc, unsupported state version) throw `EmulationException` from the host, not the core.
- **Non-fatal emulation anomalies** (unmapped MMIO read, unknown CD command, malformed CEL) are logged at `Warning`/`Debug` and the core continues with the same sentinel behavior as FreeDOCore (`0xBADACCE5`, zero, etc.).
- **No exceptions in the hot path.** Device methods return values; diagnostics use `ILogSink.IsEnabled` guards.
- **Deterministic logging:** log categories and levels, no timestamps in core logs; host sinks may add timestamps.
- **Debug builds** can enable `CpuTracer`, `ExecutionProfiler`, `MemoryDumper`, and `FrameDumper`.

---

## 14. Extensibility points

| Extension | Interface | Example |
|---|---|---|
| Video presenter | `IFrameSink` | Avalonia bitmap, OpenGL, null/hash sink |
| Audio output | `IAudioSink` / host `IAudioOutput` | null, WAV, OpenAL, platform audio |
| Input backend | `IPbusSource` / host `IInputBackend` | keyboard, SDL gamepad, scripted replay |
| Disc format | `IDiscSource` | ISO, BIN, CUE, CHD |
| Save state format | `IStateReader/Writer` | v1 native format, legacy 4DO import |
| Renderer/filter | `IFramePostProcessor` | nearest, bilinear, HQ2X/3X/4X |
| Debugger | `ICpuTraceSink` | console, file, in-app debugger |

---

## 15. Coding conventions

- File-scoped namespaces; one public top-level type per file.
- `sealed` classes unless inheritance is required.
- `readonly` fields where possible; no mutable static state.
- `uint`/`ushort`/`byte` for emulated registers; `int` only where the original uses signed arithmetic.
- Explicit `unchecked` around intentional wraparound.
- Constants for all bit masks and register offsets; never bare magic numbers in new code.
- XML documentation on public types; ported code includes a `// FreeDO: <file>:<function>` comment.
- Tests are named `Method_Scenario_ExpectedResult`.
- No `unsafe` unless a measured hot path requires it; `Unsafe`/`MemoryMarshal` are acceptable.
