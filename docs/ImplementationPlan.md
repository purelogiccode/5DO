# 3DOEmulator - Detailed Implementation Plan

**Working title:** 3DOEmulator
**Target:** pure C# / .NET 10, cross-platform (Windows, Linux, macOS; x64 and arm64)
**Reference core:** `References/4DO-master/FreeDOCore` (primary), `References/opera-libretro-master/libopera` (secondary)
**Status:** M0 scaffold complete; M1 core/memory/scheduler/state complete; M2 ARM60 CPU complete; M3 boot skeleton complete (M3.13 OpenGL spike deferred to M9R); M4 MADAM graphics complete through M4.14; M4.15 renders the BIOS boot animation but the interactive menu still needs further tracing; M5 DSP/audio core and pipeline complete (real-content audio validation pending); M6 XBUS/CD-ROM/storage complete except real-disc boot validation (the local corpus in [`TestCorpus.md`](TestCorpus.md) now provides five real CHDs); M7 input/PBUS, NVRAM persistence, and the game database are complete, with platform keyboard/gamepad backends scheduled with the M9 frontend; M8 deterministic save states and the headless CLI are complete; M9 desktop shell and M9R OpenGL presenter exist as a Windows prototype; M10 packaging/CI is partially complete (CI builds/publishes CLI and frontend for six RIDs; a win-x64 CLI publish was smoke-tested). The 2026-10-23 performance review added **M9P** (runtime performance and pacing) because the prototype still runs emulation on the UI timer, uses a single output-resolution shader, reallocates textures per frame, and has a stub benchmark project. M0.8 legal/attribution review remains pending. The OpenGL/GLES renderer is a **required** desktop component; the optional guest ARM JIT is a separate backend path. See [`Performance.md`](Performance.md).
**Last updated:** 2026-10-23

---

## 0. How to use this document

This is the execution plan, not a wish list. It is organized as **milestones** (M0-M11), each with numbered tasks, deliverables, tests, and exit criteria.

Rules:

1. **One active milestone.** Do not start M4 while M2 is failing its exit criteria.
2. **Acceptance test first.** Write the test or define the exact differential before implementing a task.
3. **No silent deviations.** If the port must differ from FreeDOCore, document it in `docs/PortingNotes.md` and add a test.
4. **A milestone is done only when its exit criteria pass.** "Code compiles" is not done.
5. **Keep the core deterministic.** No wall clock, no random seeds from the environment, no unordered iteration in emulated state.
6. **Update the plan.** When a task is completed, check it off and record the commit/PR. When a task is split, add the new tasks here.
7. **Do not commit copyrighted content.** No BIOS ROMs, game images, or extracted assets.

Status legend:

- `[ ]` not started
- `[~]` in progress
- `[x]` done
- `[!]` blocked
- `[-]` deliberately deferred

---

## 1. Product definition

### 1.1 What we are building

A 3DO Interactive Multiplayer emulator with:

- A **managed C# emulation core** implementing the ARM60 CPU, memory/bus, MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD-ROM, and timing.
- A **required OpenGL renderer** (`ThreeDo.Video.OpenGL` + Silk.NET.OpenGL) that performs GPU palette expansion, high-quality scaling/filters (nearest, sharp-bilinear, CRT, xBR-family), integer scaling, vsync presentation, and screenshots. This is the project's primary differentiator.
- **First-class disc support** for ISO, BIN, CUE+BIN, and **CHD via the pure C# CHDSharp library**, including parent/child CHDs and read-ahead decompression.
- A **headless CLI** for running discs, dumping frames/states, testing, and benchmarking.
- An **Avalonia desktop frontend** for Windows, Linux, and macOS with a game library, settings, controller mapping, save states, screenshots, and fullscreen.
- A **unit/integration test suite** that can run without a copyrighted BIOS by using synthetic ROMs and fixtures.
- **Deterministic save states** and optional input recording/replay.

### 1.2 What "pure C#" means here

- No C++/CLI, no native `FreeDOCore.dll`, no P/Invoke into an emulator core.
- The emulation core, host, CLI, and frontend logic are C#.
- Host-only services may use platform/native libraries behind interfaces:
  - **OpenGL 3.3 via Silk.NET.OpenGL and OpenGL ES 3.0/3.1 via Silk.NET.OpenGLES** for the required desktop renderer (the OS/driver supplies the GL/GLES implementation).
  - Avalonia rendering (its own native backend).
  - Audio output (OpenAL Soft, SDL, or platform APIs).
  - Gamepad input (SDL/GameController or platform HID).
- The core, CLI headless mode, and tests must run with null audio/input backends and without requiring a GL context. The CPU frame assembler remains the fallback/accuracy oracle.

### 1.3 Success criteria

**v0.1 - Boot milestone**

- [ ] Boots a real 3DO BIOS to its menu.
- [ ] Renders the BIOS UI at 320x240, 60 Hz.
- [ ] OpenGL renderer displays the BIOS frame with pixel-accurate palette expansion.
- [ ] Responds to keyboard input.
- [ ] Does not crash for 10 minutes of menu navigation.
- [ ] Synthetic-ROM integration tests pass in CI.

**v0.5 - Playable milestone**

- [ ] Boots at least 10 real discs to gameplay (user-provided test set), including at least one CHD image.
- [ ] Audio works for DSP-driven music/SFX.
- [ ] NVRAM persists between sessions.
- [ ] Save states roundtrip deterministically.
- [ ] ISO, BIN, CUE+BIN, and CHD images produce identical sector streams for the same disc.
- [ ] OpenGL renderer supports accurate, sharp-bilinear, and CRT modes; integer scaling; fullscreen; screenshots.
- [ ] 60 fps on a mid-range x64 desktop and a Raspberry Pi 4 class arm64 device at native resolution.
- [ ] Windows/Linux/macOS x64 builds; arm64 builds produced and smoke-tested.

**v1.0 - Release milestone**

- [ ] Compatibility matrix with documented results.
- [ ] Full Avalonia frontend (library, settings, input mapping, save states, screenshots, logs, about).
- [ ] OpenGL renderer is the default on all desktop platforms, with CPU fallback and context-loss recovery.
- [ ] 4K (3840x2160) 60 fps output on Tier A x64 hardware and 1080p60 on Raspberry Pi 4/5 class arm64, with no CPU-side per-pixel scaling. 4K *internal* CEL rendering is not a v1 goal. See [`Performance.md`](Performance.md).
- [ ] CLI with `run`, `info`, `bench`, `state`, and `selftest` commands.
- [ ] Reproducible self-contained packages for all six RIDs.
- [ ] License/attribution review complete.
- [ ] No known crash bugs in the supported test set.

### 1.4 Non-goals (v1)

- Native (executable-memory) CPU backends. An optional **managed IL JIT** for the guest ARM core is allowed and planned behind `ICpuBackend`.
- GPU-accelerated CEL compositing in v1. The required OpenGL renderer accelerates presentation, palette expansion, scaling, and filters; software CEL rendering remains the accuracy path. A GPU CEL path is an optional post-v1 experiment.
- Netplay.
- Rewind.
- CD-DA / Red Book audio.
- Multi-disc swapping.
- PAL video.
- High-resolution CEL rendering (planned after v0.5; see M11). **4K internal CEL rendering is not a v1 goal**; v1's 4K target is output upscaling only.
- 3DO mouse, light gun, modem, or network peripherals.
- Bit-exact 4DO save-state compatibility.

---

## 2. Architecture summary

The full architecture is in [`docs/Architecture.md`](docs/Architecture.md). The short version:

```text
Frontends (Avalonia, CLI)
        │
        ├── ThreeDo.Video.OpenGL  (required desktop renderer)
        │     palette shader, scaling, CRT/xBR, vsync
        │
        ▼
ThreeDo.Host (session, BIOS/disc/NVRAM, audio, input, save states, pacing)
        │
        ▼
ThreeDo.Machine (composition, frame loop, Quartz, state sections)
        │
        ├── ThreeDo.Cpu       ARM60 interpreter (+ optional IL JIT backend)
        ├── ThreeDo.Memory    RAM/ROM/NVRAM, bus decode
        ├── ThreeDo.Madam     CEL engine, pixel pipeline, matrix engine
        ├── ThreeDo.Clio      timers, FIFOs, DMA, interrupts, XBUS/DSP windows
        ├── ThreeDo.Dsp       audio DSP
        ├── ThreeDo.Sport     VRAM fill/copy
        ├── ThreeDo.Video     VDLP, frame assembly, CPU oracle/fallback
        ├── ThreeDo.Xbus      expansion bus
        ├── ThreeDo.Cdrom     CD-ROM device
        ├── ThreeDo.Storage   ISO/BIN/CUE, BIOS, NVRAM, game DB
        ├── ThreeDo.Audio     ring buffer, sample conversion
        ├── ThreeDo.Input     PBUS encoding, profiles
        └── ThreeDo.Diagnostics logging, tracing, dumpers
        │
        ▼
ThreeDo.Core (contracts, types, constants, state interfaces)
```

Key decisions:

- **`ThreeDo.Core` has no project references.** Chips depend only on Core. `ThreeDo.Machine` wires concrete chips through small interfaces.
- **The core is single-threaded and deterministic.** The host owns pacing, audio, input, and UI/render threads.
- **Memory preserves FreeDOCore's word-swapped representation** for v1. See `docs/PortingNotes.md` §3.
- **Save states use a new explicit format**, not the native `memcpy` struct layout.
- **CEL rendering is software**, matching FreeDO. **Presentation is OpenGL/GLES**: native-resolution palette expansion, output-resolution scaling/filters, and vsync. The renderer is required for the desktop frontend and is the project's main differentiator.
- **Performance is a continuous gate.** The 4K60 target is output upscaling, not 4K internal CEL rendering. Core work stays single-threaded and deterministic; the host owns a dedicated core thread, frame pacer, and render/audio threads. See [`Performance.md`](Performance.md).
- **The guest ARM JIT is optional** and lives behind `ICpuBackend`; a tuned interpreter is the default and the accuracy oracle.

---

## 3. Reference-to-project map

| FreeDOCore file | C# project | Main C# types | Milestone |
|---|---|---|---|
| `_3do_sys.cpp` | `ThreeDo.Machine` | `ThreeDoMachine`, `FrameLoop`, `MachineOptions` | M3 |
| `arm.cpp`, `arm.h` | `ThreeDo.Cpu` | `Arm60`, `Arm60State`, `ArmDecoder`, `ArmAlu`, `ArmBarrelShifter`, `ArmDisassembler` | M2 |
| `Clio.cpp`, `Clio.h` | `ThreeDo.Clio` | `Clio`, `ClioRegisters`, `ClioTimers`, `ClioFifo`, `ClioDma`, `ClioInterrupts` | M3 |
| `Madam.cpp`, `Madam.h` | `ThreeDo.Madam` | `Madam`, `MadamRegisters`, `CelEngine`, `CelBitReader`, `PixelDecoder`, `PixelProcessor`, `Projector`, `CelRasterizer`, `MatrixEngine` | M4 |
| `vdlp.cpp`, `vdlp.h` | `ThreeDo.Video` | `Vdlp`, `VdlParser`, `VdlLine`, `VdlFrame`, `FrameAssembler` | M3/M4 |
| `frame.cpp`, `frame.h` | `ThreeDo.Video` + `ThreeDo.Video.OpenGL` | `FrameAssembler` (CPU oracle/fallback), `OpenGlRenderer` palette shader (required desktop path) | M3/M4 + M9R |
| `DSP.cpp`, `DSP.h` | `ThreeDo.Dsp` | `Dsp`, `DspCore`, `DspInstructionDecoder`, `DspAlu`, `DspRegisters`, `DspMemory` | M5 |
| `SPORT.cpp`, `SPORT.h` | `ThreeDo.Sport` | `Sport` | M3 |
| `XBUS.cpp`, `XBUS.h`, `IsoXBUS.h` | `ThreeDo.Xbus` | `Xbus`, `XbusDevice`, `XbusFifo` | M6 |
| `Iso.cpp` | `ThreeDo.Cdrom` | `CdromDevice`, `CdromCommand`, `Toc`, `Msf` | M6 |
| `quarz.cpp`, `quarz.h` | `ThreeDo.Machine` | `Quartz`, `ClockDomain` | M3 |
| `DiagPort.cpp`, `DiagPort.h` | `ThreeDo.Clio` | `DiagPort` | M3 |
| `bitop.cpp`, `bitop.h` | `ThreeDo.Madam` | `CelBitReader` | M4 |
| `Filters/hqx*` | `ThreeDo.Video.OpenGL` | GLSL xBR/HQx-style filters (GPU, required filter family) | M9R/M11 |
| `types.h`, `freedoconfig.h`, `freedocore.h` | `ThreeDo.Core` | constants, `FixMode`, `EmulatorTuning`, `VdlFrame`, `FrameBuffer` | M1 |
| `Worker.cpp` | - | not ported | - |

4DO host references:

| 4DO C# file | C# project | Main types |
|---|---|---|
| `Emulation/GameConsole.cs` | `ThreeDo.Host` | `EmulatorSession`, `FramePacer`, `AudioPipeline` |
| `Emulation/GameSource/*` | `ThreeDo.Storage`, `ThreeDo.Host` | `DiscImageFactory`, `IsoImage`, `BinImage`, `ChdDiscImage`, `GameLibraryService`, `GameDatabase` |
| `Emulation/States/SaveStateHelper.cs` | `ThreeDo.Machine`, `ThreeDo.Host` | state sections, `SaveStateService` |
| `Emulation/Plugins/Input/JohnnyInput/*` | `ThreeDo.Input` | `PbusEncoder`, `ControllerState`, `InputProfile` |
| `Emulation/GameSource/GameRegistrar.cs` | `ThreeDo.Storage` | `GameDatabase`, `GameId` |
| `Emulation/EmulationHelper.cs` | `ThreeDo.Storage` | `NvramImage`, `VolumeHeader` |
| `UI/SaveHelper.cs` | `ThreeDo.Host` | `SavePathService` |

---

## 4. Milestones at a glance

| ID | Milestone | Goal | Exit criteria | Estimate |
|---|---|---|---|---:|
| M0 | Bootstrap | Repo, build, CI, docs, project skeleton | `dotnet build`/`test` green; solution matches architecture | 3 dd |
| M1 | Core + memory + scheduler | Contracts, RAM/ROM/NVRAM, bus, Quartz, state primitives | Memory/bus/quartz unit tests green | 5 dd |
| M2 | ARM60 CPU | Full FreeDO ARM interpreter | CPU instruction tests green; synthetic program runs | 12 dd |
| M3 | Boot skeleton | BIOS load, CLIO basics, VDLP, SPORT, frame loop | Real BIOS executes 300 frames; synthetic frame output | 10 dd |
| M4 | MADAM graphics | CEL engine, pixel pipeline, matrix engine | BIOS UI renders; CEL golden tests green | 20 dd |
| M5 | DSP + audio | DSP core and sample pipeline | DSP tests green; BIOS audio/test tone works | 12 dd |
| M6 | XBUS + CD-ROM + CHD | Disc images, CHD, and CD command state machine | Real disc/CHD boots to title screen | 12 dd |
| M7 | Input + NVRAM + fixes | Playable controls, persistence, game DB | Playable test set; NVRAM persists | 6 dd |
| M8 | Save states + diagnostics | Deterministic states, CLI tools, tracing | State roundtrip and CLI tests green | 10 dd |
| M9 | Avalonia frontend | Full desktop UI | Runs on win/linux/mac x64; 60 fps; fullscreen | 16 dd |
| M9R | **OpenGL/GLES renderer** | Required GPU presentation, palette shader, scaling/filters, vsync | Accurate mode matches CPU; 4K output; 60 fps | 10 dd |
| M9P | **Performance and pacing** | Dedicated core thread, two-stage 4K presenter, persistent upload, backend matrix, benchmark gates | 4K60 Tier A; 1080p60 Pi4/5; p99 budget; zero allocations | 10 dd |
| M10 | Packaging + performance validation | Six RIDs, CI artifacts, final performance report | All packages start; targets met | 6 dd |
| M11 | Compatibility + release | Real-game hardening, high-res, release | v1 checklist complete | 25+ dd |
| M12 | Optional guest ARM JIT | Managed IL JIT behind `ICpuBackend` | Bit-identical state; benchmark win | 10-20 dd |

**Total:** ~157 developer-days of focused work plus 30% contingency (~204 dd), excluding the optional M12 JIT. This is a multi-month project for one experienced developer; correctness and performance gates matter more than the estimate.

---

## 5. Detailed milestones

### M0 - Bootstrap and guardrails

**Goal:** a clean, buildable repository with the planned project graph, test harness, CI, and documentation.

**Tasks**

- [x] **M0.1 Initialize the repository.**
  - `git init`, `.gitignore` for .NET/Avalonia/test artifacts.
  - `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`.
  - Solution `3DOEmulator.sln`.
- [x] **M0.2 Create the production project skeleton.**
  - `src/ThreeDo.Core`, `Cpu`, `Memory`, `Madam`, `Clio`, `Dsp`, `Sport`, `Video`, `Video.OpenGL`, `Xbus`, `Cdrom`, `Storage`, `Audio`, `Input`, `Diagnostics`, `Machine`, `Host`, `Cli`, `Frontend.Avalonia`.
  - Correct project references per `docs/Architecture.md`.
  - No placeholder public APIs that will be thrown away.
- [x] **M0.3 Create the test and benchmark skeleton.**
  - `tests/ThreeDo.Tests` (xUnit).
  - `benchmarks/ThreeDo.Benchmarks` (BenchmarkDotNet).
  - Test utility folder: fake clock, fake memory, fake frame/audio/input sinks (to be added with M1 tests).
- [x] **M0.4 Create the minimal CLI.**
  - `ThreeDo.Cli` prints version/usage and returns non-zero on unknown commands.
  - `System.CommandLine` package is reserved for M8; M0 uses a small hand-written parser.
- [x] **M0.5 Create the minimal Avalonia app.**
  - Window with title, version, and "No game loaded" placeholder.
  - `AvaloniaUseCompiledBindingsByDefault`, Fluent theme, Inter font.
  - No emulation logic yet.
- [x] **M0.6 CI.**
  - GitHub Actions: build/test on Windows/Linux/macOS; `dotnet format --verify-no-changes`.
  - Publish matrix skeleton for six RIDs.
- [x] **M0.7 Documentation.**
  - `README.md`, `docs/Architecture.md`, `docs/ImplementationPlan.md`, `docs/PortingNotes.md`, `docs/TestStrategy.md`, `docs/Compatibility.md`, `docs/Licensing.md`, `THIRD-PARTY-NOTICES.md`.
- [ ] **M0.8 Attribution and legal gate.**
  - Preserve FreeDO/4DO headers in ported files (will be added as each file is ported).
  - Decide project distribution model; record it in `docs/Licensing.md` (pending owner decision/legal review).
  - Add About/credits placeholder (M9).

**Deliverables**

- Buildable solution with 19 production projects, one test project, one benchmark project.
- CLI and Avalonia app launch with placeholders.
- CI green.

**Acceptance**

```powershell
dotnet build 3DOEmulator.sln -c Release
dotnet test 3DOEmulator.sln -c Release
dotnet format 3DOEmulator.sln --verify-no-changes
dotnet run --project src/ThreeDo.Cli -- --version
dotnet run --project src/ThreeDo.Frontend.Avalonia
```

- No warnings-as-errors failures.
- No native emulator dependency.
- Project references match the architecture diagram.

**Risks**

- Avalonia 12 template/API churn: pin 12.1.4 and keep the UI minimal in M0.
- Analyzer noise: enable only built-in analyzers first; add third-party analyzers after the skeleton builds cleanly.

---

### M1 - Core contracts, memory, bus, scheduler, state primitives

**Goal:** the shared model and the memory/timing foundation that every chip uses.

**Tasks**

- [x] **M1.1 Core primitives and constants.**
  - `EmulatorConstants`: clock rates, frame cycles, RAM/ROM/NVRAM sizes, address bases, sentinels.
  - `FixMode` flags, `EmulatorTuning`, `CompatibilityState`, `GameQuirks`.
  - `VdlLine`, `VdlFrame`, `FrameBuffer`, `PixelFormat`, `AudioSample`.
  - `EmulationException`, `InvalidStateException`.
- [x] **M1.2 Core interfaces.**
  - `IAddressBus` (byte/halfword/word read/write).
  - `IRam` (raw RAM/VRAM access used by CEL/SPORT/VDLP).
  - `IInterruptSink`, `IFrameSink`, `IAudioSink`, `IPbusSource`, `IDiscSource`, `IXbusDevice`, `IVdlp`, `IMadamBus`.
  - `IClioBus`, `ISportBus`, `IDiagPort` for the M1 bus hooks.
  - `IStateReader`, `IStateWriter`, `IStateSection`.
  - `ILogSink`, `LogLevel`, `LogCategory`.
- [x] **M1.3 Memory implementation.**
  - `ThreeDoMemory` with `uint[]` RAM (3 MB + 16 MB high-res mirrors reserved), `uint[]` ROM (2 MB), `byte[]` NVRAM (32 KB).
  - Byte/halfword/word helpers matching FreeDOCore's word-swapped representation.
  - Unaligned reads/writes.
  - ROM loading from big-endian BIOS bytes.
  - NVRAM one-byte-per-word quirk.
  - `0xBADACCE5` sentinel.
- [x] **M1.4 Bus/address decode.**
  - RAM, ROM, NVRAM, Diag, SPORT, MADAM, CLIO ranges in the same order as FreeDOCore.
  - ROM bank select hook.
  - `IAddressBus` implementation with device hooks.
  - No device references yet; use null/stub devices until M3.
- [x] **M1.5 Quartz scheduler.**
  - `PushArmCycles`, `QueueDsp`, `QueueTimer`, `QueueVdl`, `VdCurrLine`, `VdHalfFrame`, `VdCurrOverline`.
  - 8.24 fixed-point arithmetic matching FreeDOCore.
  - Configurable `ArmClock` and constants for VDL/DSP/timer.
  - Save/load of accumulators.
- [x] **M1.6 State primitives.**
  - `StateWriter`/`StateReader` with explicit little-endian primitives, bounds checks, section table, CRC32.
  - `StateSection` IDs and version constants.
  - No chip state yet.
- [x] **M1.7 Diagnostics skeleton.**
  - `Log`, `ConsoleLogSink`, `InMemoryLogSink`, `NullLogSink`.
  - Category/level filtering; no allocations when disabled.
- [x] **M1.8 Tests.**
  - Memory alignment/byte-order/NVRAM/decode tests.
  - Quartz fixed-point tests: 1 cycle, 15/16/17 cycles, 255 cycles, one-second event counts, 60 half-frames per 12.5M cycles.
  - State reader/writer roundtrip, truncation, CRC mismatch.
  - Logging filter tests.

**Deliverables**

- `ThreeDo.Core`, `ThreeDo.Memory`, `ThreeDo.Machine` (Quartz only), `ThreeDo.Diagnostics` compile with tests.
- A `TestMemory`/`TestBus` utility for later chip tests.

**Acceptance**

- [x] Memory tests cover all alignments and the NVRAM quirk.
- [x] Quartz produces exactly 60 VDL half-frames (60 presented frames) per 12.5M cycles with the FreeDO event semantics.
- [x] State primitives reject truncated/corrupt data cleanly.
- [x] `dotnet test` green.

**Risks**

- The word-swapped representation is easy to get subtly wrong. Mitigate with a `MemoryByteOrderTests` table test that compares every byte of a known word against the C++ behavior.
- `clks << 24` overflow: document and assert the expected range; test with 1, 15, 16, 255.

---

### M2 - ARM60 CPU

**Goal:** a complete, faithful port of `arm.cpp`, validated instruction by instruction.

**Tasks**

- [x] **M2.1 CPU state and reset.**
  - `Arm60State`: user registers, banked FIQ/IRQ/SVC/ABT/UND registers, SPSRs, CPSR, pending FIQ, ROM select, data-abort flag.
  - Reset: PC `0x03000000`, SVC mode, CPSR `0x13`, zero registers.
  - `IAddressBus` integration.
- [x] **M2.2 ALU and barrel shifter.**
  - `ArmAlu` operations 0-31.
  - `ArmBarrelShifter` with `NSC`/`SC` variants, carry-out, shift types 0-4.
  - Condition code table and evaluation.
  - Flag helpers: N/Z/C/V, mode, I/F.
- [x] **M2.3 Decoder skeleton.**
  - Instruction class order matching `_arm_Execute`: multiply, swap, ALU, undefined, single data transfer, block transfer, branch, SWI, coprocessor/undefined.
  - C# replacement for C++ fallthrough (ordered checks or `goto case`).
  - Condition check before execution.
- [x] **M2.4 Data processing.**
  - Immediate and register operands, rotate, register-specified shift.
  - All 16 opcodes, S bit, PC destination, SPSR restore.
  - Carry-out for logical ops.
- [x] **M2.5 Multiply and swap.**
  - MUL/MLA, S bit, cycle count based on `calcbits`.
  - SWP/SWPB, PC adjustment, unaligned word rotation.
- [x] **M2.6 Single data transfer.**
  - LDR/STR byte/word, pre/post index, writeback, negative offset, shifted register.
  - PC-relative and PC destination.
  - Privileged/user-mode register selection.
- [x] **M2.7 Block data transfer.**
  - LDM/STM IA/IB/DA/DB, writeback, user-mode bit, PC in list, empty list.
  - The `ldm_accur`/`stm_accur` timing quirks and `FIX_BIT_TIMING_1` value fixes.
- [x] **M2.8 Branch, SWI, undefined, MRS/MSR.**
  - B/BL, SWI vector, undefined vector, coprocessor/undefined path.
  - MRS/MSR CPSR/SPSR, mode changes, banked register switching.
- [x] **M2.9 FIQ and memory interface.**
  - FIQ entry after instruction when `!ISF && NeedFiq`.
  - `mreadb/mreadw/mwriteb/mwritew` decode order and sentinels.
  - `SelectROM`, raw RAM helpers.
  - `biosanvil` PC skip and `cnbfix` compatibility hacks.
- [x] **M2.10 Cycle accounting.**
  - `NCYCLE=4`, `SCYCLE=1`, `ICYCLE=1`.
  - Multiply, swap, LDR/STR, LDM/STM, branch, SWI cycles.
  - `Step()` returns the same cycle count as `_arm_Execute`.
- [x] **M2.11 Disassembler.**
  - Decode the implemented subset to readable ARM assembly.
  - Used by CLI trace and debug logs.
- [x] **M2.12 Test assembler.**
  - `ArmTestAssembler` with helpers for data processing, loads/stores, branches, LDM/STM, SWI, MRS/MSR.
  - Test memory/bus with configurable mapping.
- [x] **M2.13 Instruction test suite.**
  - One test class per instruction group (see `docs/TestStrategy.md`).
  - Condition-code matrix, shift matrix, flag matrix.
  - Banked-register/FIQ tests.
  - Cycle-count assertions.
- [x] **M2.14 Synthetic boot program.**
  - A 1 MB synthetic ROM that computes a checksum, writes results to RAM, and loops.
  - Run 1M instructions; assert results and no unmapped accesses.
- [-] **M2.15 Optional oracle trace.**
  - Developer-only harness to compare PC/registers/cycles with FreeDOCore for a small program.
  - Deferred: the product must not load the native core, and a developer-only oracle would live outside this repository. The instruction and synthetic tests are the default regression gate.
- [x] **M2.16 CPU backend abstraction.**
  - `ICpuBackend` with `Step`, `Reset`, state, and register access.
  - `Arm60` (the interpreter) is the default implementation.
  - `EmulatorTuning.CpuBackend` selects interpreter/JIT; JIT is added in M12.
  - No behavior change; tests run against the interpreter.

**Deliverables**

- `ThreeDo.Cpu` with `Arm60`, state, ALU, shifter, decoder, disassembler.
- Instruction test suite with hundreds of cases.
- Synthetic boot program.

**Acceptance**

- [x] Every implemented instruction has normal/boundary/flag tests.
- [x] All 16 condition codes and all shift types are covered.
- [x] Cycle counts match the reference for representative instructions.
- [x] FIQ entry/exit and banked registers match the reference.
- [x] Synthetic program runs 1M instructions without exception or out-of-range PC.
- [x] `dotnet test` green; no per-instruction allocations.

**Risks**

- C++ switch fallthrough is the most likely source of decode bugs. Mitigate with a decode-order test table and a disassembler cross-check.
- Banked register switching is subtle. Mitigate with mode-transition tests that snapshot all banks.
- `int`/`uint` signedness in address arithmetic. Mitigate with explicit casts and tests at `0x7FFFFFFF`/`0x80000000` boundaries.

---

### M3 - Boot skeleton: BIOS, CLIO, VDLP, SPORT, frame loop

**Goal:** load a BIOS, execute it, service basic CLIO/MADAM/VDLP/SPORT behavior, and produce frames. Real BIOS may not fully boot until M4/M5/M6; this milestone proves the machine loop.

**Tasks**

- [x] **M3.1 BIOS/NVRAM loading and reset.**
  - `BiosImage` (1 MB, optional 1 MB second, MD5, Anvil detection).
  - `NvramImage` (32 KB, volume header initialization).
  - `ThreeDoMachine.Reset()` order: ARM, ROM swap, VDLP, SPORT, MADAM, XBUS + CD reset, CLIO `0x40`, DSP, frame, diag, Quartz.
- [x] **M3.2 CLIO basic register file.**
  - `uint[65536]` registers.
  - Defaults: `0x28=0x40`, `0x400=0x80`, `0x220=64`.
  - Interrupt registers and `NeedFiq`.
  - ROM select `0x84`.
  - `0x00` version read `0x02020000`.
- [x] **M3.3 CLIO timers.**
  - 16 timers, control/reload/cascade/decrement.
  - `DoTimers`, FIQ generation for odd timers.
  - `0x120` timer-dependent write, `0x220` delay.
- [x] **M3.4 CLIO FIFOs and DMA skeleton.**
  - 13 input/4 output FIFOs; start/next/length; status.
  - XBUS DMA path with a stub XBUS device (returns zeros).
  - FIQ 29 on DMA complete.
- [x] **M3.5 MADAM register file and basic pokes.**
  - 2048+64 registers.
  - `0x04=0x29`, `0x00=0x01020000`, `0x574=0xFFFFFFFC`.
  - CEL status/FSM.
  - VDL head write hook to VDLP.
  - Matrix engine (commands 0-3) implemented early.
- [x] **M3.6 VDLP startup and line parser.**
  - `Vdlp` state and startup VDL at `0xB0000 + 2 MB`.
  - `ProcessVdl`, `DoLine`, CLUT/background/out-control parsing.
  - `VdlFrame` line writes.
  - 320x240 output.
- [x] **M3.7 SPORT.**
  - Color/fill/copy/masked operations.
  - 512-word page operations.
- [x] **M3.8 DiagPort.**
  - Shift registers and `_diag_Init(-1)`.
- [x] **M3.9 Quartz integration and frame loop.**
  - `ThreeDoMachine.RunFrame()` and `InternalFrame`.
  - VDL line 16 trigger, V0/V1 FIQ, PBUS copy, frame swap.
  - Single-threaded mode first; `skipFrame` path implemented.
- [x] **M3.10 Frame assembler.**
  - Convert `VdlFrame` to BGRA `FrameBuffer`.
  - Background/fixed CLUT/per-line CLUT.
  - Crop/alpha optional (alpha always written; crop deferred).
- [x] **M3.11 Synthetic boot integration test.**
  - Tiny ARM program writes a VDL, enables SPORT/VDLP, draws a background, loops.
  - Assert frame dimensions, non-black pixels, stable frame hash.
- [x] **M3.12 Real BIOS smoke test (optional).**
  - Run 300 frames with a user-provided BIOS.
  - Assert no exception, PC not stuck in a tight invalid loop, frame hash recorded.
  - Skip if `THREEDO_BIOS` is unset. Validated locally with `bios/panafz1.bin`.
- [-] **M3.13 OpenGL integration spike (early, non-blocking).**
  - Minimal Avalonia + Silk.NET.OpenGL window that displays a synthetic 320x240 indexed frame.
  - Prove context creation, shader compilation, texture upload, and present on Windows/Linux/macOS.
  - **Deferred to M9R.1/M9R.10.** It requires a desktop GL context and a host runner; the CPU frame assembler and machine loop are complete and tested without one. This does not block M4-M8.

**Deliverables**

- `ThreeDo.Machine` can construct/reset/run frames.
- BIOS + NVRAM + VDLP + SPORT + CLIO + basic MADAM.
- Synthetic frame output.

**Acceptance**

- [x] Synthetic program produces the expected background frame.
- [x] Real BIOS executes 300 frames without crash (validated locally with `bios/panafz1.bin`; test skips when `THREEDO_BIOS` is unset).
- [x] Timer/FIFO/DMA unit tests green.
- [x] Frame loop cycle count matches `12_500_000 / 60` per frame (within the reference's 16-cycle batching overshoot).
- [x] No per-frame allocations (test asserts zero allocations over 60 steady-state frames).

**Risks**

- Real BIOS may wait on CD/DSP/CEL features not yet implemented. Treat "executes 300 frames" as the gate; full boot is M4/M5/M6.
- VDLP line timing is easy to off-by-one. Mitigate with line-by-line tests at 0, 16, 239, 240, 525.

---

### M4 - MADAM graphics: CEL engine, pixel pipeline, matrix engine

**Goal:** render real 3DO graphics: packed/literal/LR CELs, PLUT, pixel processing, projection, clipping, matrix transforms, PBUS DMA.

**Tasks**

- [x] **M4.1 CEL bit reader.**
  - Port `BitReaderBig` (`bitop.cpp`) exactly.
  - Tests for bit rates 1-32, positions, skip, end-of-buffer.
- [x] **M4.2 CCB parser.**
  - `CCBFLAGS` constants, `NEXTCCB`, `PDATA`, `PLUTDATA`, `PRE0`, `PRE1`.
  - Absolute/relative pointers, `CCB_LAST`, `CCB_SKIP`, `CCB_PACKED`, `CCB_LDPLUT`, etc.
  - FSM transitions (parser-level; the CEL engine consumes the parsed header in M4.6).
- [x] **M4.3 PLUT.**
  - `LoadPLUT` address math, 2/4/16/32 entries.
  - Palette write bit handling (the 16-bit palette-write bit is decoded in M4.4).
- [x] **M4.4 Pixel decoder.**
  - 1/2/4/6/8/16 bpp.
  - Linear/uncoded vs PLUT-coded.
  - 16-bit palette-write bit.
  - AMV computation.
- [x] **M4.5 Pixel processor and projector.**
  - `PPROC`: MS/MF/SF/2S/AV/2D, PXOR, NOBLK, clipping.
  - `PPROJ_OUTPUT`: PLUT position, SWAPHV, CFBDSUB (disabled), B15/B0 modes.
  - Exhaustive combination tests where feasible.
- [x] **M4.6 CEL geometry and clipping.**
  - `TestInitVisual`, `Init_Line_Map`, `Init_Scale_Map`, `Init_Arbitrary_Map`.
  - `QuardCCWTest`, `TexelCCWTest`.
  - `XY2OFF`.
- [x] **M4.7 Packed CEL rasterizer.**
  - `DrawPackedCel_New`, all three texel paths.
  - `speedfixes`/`unknownflag11`/`sdf`/`sf` compatibility state.
- [x] **M4.8 Literal CEL rasterizer.**
  - `DrawLiteralCel_New`, all three paths.
  - `FIX_BIT_TIMING_6`.
- [x] **M4.9 LR CEL rasterizer.**
  - `DrawLRCel_New`, all three paths.
- [x] **M4.10 Texel draw primitives.**
  - `TexelDraw_Line`, `TexelDraw_Scale`, `TexelDraw_Arbitrary`.
  - `FIX_BIT_TIMING_3`, `FIX_BIT_GRAPHICS_STEP_Y`.
- [x] **M4.11 Matrix engine.**
  - Commands 0-3, 16.16 math, Nfrac, division-by-zero fallback.
  - Register defaults and result latching.
  - Implemented with the M3 register file; M4 adds command-0 and Nfrac coverage.
- [x] **M4.12 DMA/PBUS.**
  - `HandleDMA8`, `DMAPBus`, `KeyPressed`.
  - FIQ generation.
  - Implemented with the M3 register file; M4 adds multi-word and negative-length coverage.
- [x] **M4.13 CEL cycle accounting.**
  - `mread`/`mwrite`/`mreadh`/`mwriteh` CEL cycles.
  - `GetCELCycles` behavior.
  - Implemented in `CelMemory`; the M4.7-M4.10 rasterizers must route VRAM access through it.
- [x] **M4.14 Golden CEL tests.**
  - Hand-built CCB scenes for each bpp/path.
  - VRAM pixel hashes, CEL cycle counts.
  - A composite scene with multiple CCBs, PLUT changes, clipping, transparency.
  - Implemented in `tests/ThreeDo.Tests/Madam` (`CelRasterizerTests`, `CelLiteralRasterizerTests`, `CelGoldenSceneTests`): every bpp index, packed/literal/LR line/scale/arbitrary paths, clip-window corners, PLUT reloads, transparent texels, and a deterministic composite-scene VRAM hash with cycle counts.
- [~] **M4.15 BIOS UI validation.**
  - **In progress.** With the CD-ROM/XBUS device attached, a real BIOS (`bios/panafz1.bin`) passes its device poll, renders the boot logo and the console/disc animation (`CelGoldenSceneTests`/`BiosUiValidationTests`, 96+ distinct colors at frame 120), and starts the DSP.
  - The interactive menu still ends on a black frame and a BIOS halt loop (`PC 0x000205c4`), even with a synthetic disc; the local corpus in [`TestCorpus.md`](TestCorpus.md) now provides real CHDs and further CEL/VDLP tracing is the next step. `BiosUiValidationTests` keeps the opt-in stability/PNG-dump and DSP-start checks for the follow-up.
- [x] **M4.16 High-res decision.**
  - Keep `RESSCALE=0` in v1. Document the later feature work in M11.
  - Decision recorded in `docs/PortingNotes.md`; M11.2 owns the `RESSCALE=1` work.

**Deliverables**

- Full software CEL engine.
- Matrix engine.
- CEL golden tests.
- BIOS UI rendering.

**Acceptance**

- [ ] Pixel decoder/processor/projector tests match hand-computed values.
- [ ] Packed/literal/LR scenes produce stable VRAM hashes.
- [ ] Matrix engine tests match the reference.
- [ ] BIOS menu renders at 320x240 with correct colors and text.
- [ ] No per-frame allocations in CEL rendering.

**Risks**

- This is the largest port (~2,000 lines of dense C++). Mitigate by porting in the exact function order, testing each path, and keeping the original variable names in comments.
- The `speedfixes`/`unknownflag11` heuristics are undocumented. Port them as-is; do not invent a cleaner model.
- Floating-point casts in `TestInitVisual`/geometry. Add boundary tests and document saturation behavior.

---

### M5 - DSP and audio

**Goal:** execute the 3DO audio DSP and produce 44.1 kHz stereo samples.

**Tasks**

- [x] **M5.1 DSP memory and registers.**
  - `NMem[2048]`, `IMem[1024]`, register file, `REGCONV`, `RBASEx4`, `REGi`.
  - `DSPPCNT`/`DSPPRLD` 567, `AUDCNT`, semaphore status/data, noise.
- [x] **M5.2 Instruction decoder/precompute.**
  - Port `_dsp_Init`: `INSTTRAS[0x8000]`, request flags, `BS`, `REGCONV`, `BRCONDTAB`.
  - Tests for every request flag combination, plus a 32-opcode branch-table walk.
- [x] **M5.3 ALU and multiplier.**
  - ALU ops 0-15, flags, exact, 32-bit mask.
  - MULT1/MULT2 paths, ACSBU, M2SEL.
- [x] **M5.4 Barrel shifter.**
  - Cases 0-31, arithmetic/logical, CLIP, carry.
- [x] **M5.5 Operand loader.**
  - `OperandLoader`, `OperandLoaderNWB`, NMem/IMem/register windows, DI, NUMOPS.
- [x] **M5.6 Control flow.**
  - NOP, branch accum, set rbase/rmap, RTS, set op_mask, SLEEP, jumps, JSR, conditional branches.
  - Branch condition table.
- [x] **M5.7 Memory windows and semaphore.**
  - `ireadh`/`iwriteh`, EI/I, `_dsp_WriteIMem`, `_dsp_ReadIMem`.
  - ARM/DSP semaphore write/read/ACK.
- [x] **M5.8 Reset/running/PRNG.**
  - `_dsp_Reset`, `SetRunning`, `fastrand`.
- [x] **M5.9 Sample output and cadence.**
  - `(IMem[0x3FF] << 16) | IMem[0x3FE]`.
  - 567-cycle cadence.
  - Channel order verification (low half = left, high half = right).
- [x] **M5.10 Audio pipeline.**
  - `AudioSample`, packed-to-stereo conversion.
  - `AudioRingBuffer` (SPSC monotonic counters, no allocations).
  - `NullAudioSink`, `WavAudioSink`, `AudioPipeline`.
  - Host device output (OpenAL) remains M9.7.
- [x] **M5.11 Tests.**
  - Instruction decode table, ALU/shifter/operand/branch tests.
  - Semaphore tests.
  - Sample stream and WAV header tests.
  - Synthetic sine/square DSP program and manual WAV check remain with the real-content audio pass (M5.12/M11).
- [x] **M5.12 BIOS audio smoke.**
  - Run BIOS with DSP enabled; assert samples are produced and no underrun crash.
  - Real BIOS (`bios/panafz1.bin`) now starts the DSP and the frame loop pushes 567-cycle samples without crashing; sample content is silent until a valid disc/menu drives the DSP program.

**Deliverables**

- `ThreeDo.Dsp` with full core.
- `ThreeDo.Audio` ring buffer and sinks.
- DSP golden tests.

**Acceptance**

- [x] Decoder/ALU/shifter/branch tests green.
- [x] Sample cadence and packed format match the reference.
- [ ] WAV output is audible and correctly pitched (manual check pending a real disc/BIOS audio path).
- [x] No allocations per sample/frame (the DSP core and ring buffer allocate nothing in steady state).
- [~] BIOS produces audio (DSP runs and samples are produced; content is silent without a valid disc/menu).

**Risks**

- The DSP is bit-level and obscure. The precomputed branch table is easy to get wrong; test all 32 branch codes x 32 flag/exact combinations.
- Sample channel order is not obvious from the C++ (host writes the packed uint directly to DirectSound). Verify with a known WAV and document.

---

### M6 - XBUS, CD-ROM, and disc boot

**Goal:** read real disc images (ISO/BIN/CUE/CHD) through the emulated CD-ROM and boot a game.

**Tasks**

- [x] **M6.1 Disc abstraction.**
  - `IDiscSource` with `SectorCount` and `ReadSector(int lba, Span<byte>)`.
  - `IsoImage` (2048), `BinImage` (2352 + 0x10), `CueSheet` + factory.
  - **`ChdDiscImage`** backed by CHDSharp (pure C#).
  - Format detection by extension/header/size.
- [x] **M6.1a CHD image reader (required v1 format).**
  - Add `CHDSharp` 1.5.2 package reference to `ThreeDo.Storage`.
  - Open standalone and parent/child CD CHDs with `ChdFile.Open`.
  - Validate `chd.IsCd`; reject DVD/HDD/raw/audio-only CHDs with a clear error.
  - Read the first data track from `chd.Tracks`; determine payload offset:
    - cooked Mode1/2048 -> 0
    - raw Mode1/2352 -> 16
    - raw Mode2/Form1 -> 24
  - `SectorCount` from the first data track; 3DO volume header remains authoritative.
  - Enable `ConfigureReadAhead(4)` for sequential streaming.
  - Support optional parent CHD path for differential images.
  - Dispose the underlying `ChdFile` deterministically.
- [x] **M6.2 Disc metadata.**
  - Volume header parsing (blockCount at offset 80, big-endian).
  - Sector count fallback (container sector count when no volume header is present).
  - Game ID: MD5 of first two sectors.
  - Game database lookup is deferred to M7.6; unknown IDs return the computed hash.
- [x] **M6.3 XBUS controller.**
  - 16 slots, device 0 CD, device 0xF built-in responder.
  - Select/poll/command/data FIFO, status.
  - FIQ generation.
  - Save/load state section.
- [x] **M6.4 CD device state machine.**
  - `cdrom_Device` fields and reset/`InitCD`.
  - Commands 0x01, 0x02, 0x03, 0x04, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x10, 0x80, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x93.
  - Exact status/data lengths and byte layouts.
  - Sector streaming and `OnSector`/`Read2048` ordering.
- [x] **M6.5 MSF/LBA/TOC.**
  - BCD helpers, MSF2LBA, LBA2MSF, MSF2BLK, BLK2MSF.
  - TOC entry for track 1, disc info, session info.
- [x] **M6.6 CLIO DMA integration.**
  - XBUS DMA to RAM, 4-byte groups, FIQ 29.
  - `len` sentinel and data FIFO draining.
- [~] **M6.7 BIOS + disc boot.**
  - Boot a real disc to the title screen (user-provided).
  - **Unblocked:** the local corpus in [`TestCorpus.md`](TestCorpus.md) contains five real CHDs; the CD device, storage layer, and boot path are implemented and the BIOS boot animation runs. Record results in `docs/Compatibility.md`.
- [x] **M6.8 Tests.**
  - Command transcripts for every command (ID, data path check, capacity, read data, eject, save/load).
  - ISO/BIN/CUE fixtures.
  - **CHD fixtures**: generate small CD CHDs with CHDSharp's encoder and verify:
    - ISO vs BIN vs CUE vs CHD produce identical 2048-byte sector streams.
    - Mode1/2048 and Mode1/2352 payload offsets.
    - parent/child CHD opening.
    - invalid/non-CD CHD rejection.
    - read-ahead is enabled; cache/read-ahead behaviour is delegated to CHDSharp and is not separately observable.
  - Multi-sector read test.
  - DMA-to-RAM test (existing CLIO DMA test).
  - Save/load CD state.

**Deliverables**

- `ThreeDo.Xbus`, `ThreeDo.Cdrom`, `ThreeDo.Storage`.
- `IsoImage`, `BinImage`, `CueSheet`, and `ChdDiscImage`.
- Real disc/CHD boot path.

**Acceptance**

- [x] CD command transcripts match expected bytes.
- [x] ISO, BIN, CUE+BIN, and CHD sector reads return identical payloads for the same disc.
- [ ] A real CHD disc boots to the title screen (local corpus available; validation pending).
- [ ] BIOS boots a real disc to the title screen (local corpus available; validation pending).
- [ ] At least one game reaches gameplay (manual; local corpus available; validation pending).
- [x] CD state save/load works.

**Risks**

- The CD command state machine is full of undocumented status layouts. Use the C++ as the specification and transcript tests.
- Some games may rely on seek/spin timing. FreeDO's model is approximate; document compatibility.
- CUE parsing edge cases (pregap, multiple files) can wait; support single-track MODE1 first.
- CHD track layouts vary (Mode1/2048, Mode1/2352, Mode2/Form1, pregap). Validate the first data track and reject unsupported layouts instead of guessing.
- CHDSharp is a pure C# dependency but its CHD decoder can be CPU-heavy on slow arm64 devices. Enable read-ahead and cache; benchmark CHD boot vs ISO.

---

### M7 - Input, NVRAM, game database, compatibility fixes

**Goal:** make the emulator playable and persistent.

**Tasks**

- [x] **M7.1 Controller state and PBUS encoder.**
  - `ThreeDoController` buttons: A/B/C/P/X/L/R, Up/Down/Left/Right.
  - `PbusEncoder` 16-byte packet, 1-6 controllers, device order 1,3,2,5,4,6.
  - Tests for every button and combination.
- [x] **M7.2 Input profiles.**
  - Keyboard bindings, gamepad bindings, console hotkeys.
  - JSON serialization, defaults, conflict detection.
  - 1-6 player profiles.
- [~] **M7.3 Keyboard backend.**
  - `KeyboardInputMapper`/`InputManager` translate key names to controller state without a UI dependency; tests cover binding, release, and multi-player profiles.
  - Avalonia key-event forwarding and focus handling are wired with the M9.3 frontend shell.
- [~] **M7.4 Gamepad backend.**
  - `IGamepadBackend` abstraction and `NullGamepadBackend`; `InputManager` merges gamepad state with the keyboard.
  - The Avalonia/Silk.NET platform backend arrives with M9.6/M9.4 (hot-plug, deadzone, D-pad mapping).
- [x] **M7.5 NVRAM persistence.**
  - `NvramStore` under `ThreeDo.Host` with the standard NVRAM path supplied by the caller.
  - Debounced atomic write (250 ms via `TimeProvider`), flush on pause/stop/dispose, fresh-image initialization, corrupt-image backup.
- [x] **M7.6 Game database and fix modes.**
  - 4DO `GameDatabase.xml` ported as an embedded resource with attribution; MD5 → ID/name lookup and unknown-ID fallback.
  - Per-game settings and fix-mode application remain in the host/settings layer (M9.5); the core fix flags already exist in `CompatibilityState`.
- [x] **M7.7 BIOS detection and Anvil.**
  - `byte[28] == 234` -> `SetAnvilFix(30)`.
  - BIOS type is exposed through `BiosImage` and `ThreeDoMachine.Compatibility`; UI/CLI display arrives with M8/M9.
- [x] **M7.8 Tests.**
  - PBUS bytes for every button, six-controller ordering, NVRAM debounce/atomic/backup roundtrip, game DB lookup, input profile roundtrip, keyboard/gamepad merge.

**Deliverables**

- Playable input.
- Persistent NVRAM.
- Game-specific fixes.

**Acceptance**

- [ ] Keyboard and gamepad control a real game (local corpus available; validation pending).
- [x] NVRAM survives restart and is written atomically.
- [~] Known games with fix flags boot/behave correctly (database and compatibility flags are in place; per-game override UI is M9.5).
- [x] PBUS tests match 4DO's JohnnyInput output.

**Risks**

- Cross-platform gamepad APIs are fragmented. Keep the backend behind an interface; ship keyboard first if gamepad slips.
- Game database licensing/attribution. Port only with attribution and legal review.

---

### M8 - Save states, diagnostics, CLI

**Goal:** deterministic save/load, debugging tools, and a useful headless CLI.

**Tasks**

- [x] **M8.1 State format v1.**
  - Magic `TDO1`, version, header, section table, CRC32.
  - Explicit little-endian writer/reader.
  - BIOS hash, disc hash, emulator version, tuning flags.
  - Reject unknown versions; warn on hash mismatch (`ThreeDoMachine.StateWarnings`).
- [x] **M8.2 Per-chip state sections.**
  - CPU, memory (RAM/ROM/NVRAM), MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD, Quartz.
  - Derived tables (DSP precompute, REGCONV/BRCONDTAB) are rebuilt by the constructor; high-resolution mirrors are unused in v1.
  - The machine section also stores the derived VDL frame and compatibility flags so a load continues bit-identically.
- [x] **M8.3 Machine save/load.**
  - `ThreeDoMachine.SaveState(Stream)`, `LoadState(Stream)`.
  - Atomic file writes (`SaveStateToFile`/`LoadStateFromFile`); slot naming is a host/frontend concern (M9.8).
- [x] **M8.4 State tests.**
  - Save -> run 60 frames -> load -> run 60 frames; frame hash, PC, and frame count match the control run.
  - Corrupt/truncated state rejection.
  - BIOS hash mismatch is reported as a warning, not a hard failure.
- [~] **M8.5 Diagnostics.**
  - Frame dumpers (BMP/PNG), state inspector, CPU tracer with disassembly, and a raw memory/VRAM dumper are implemented; CPU counters are available through the machine.
  - A full CPU tracer, VRAM/memory dumpers, and allocation counters remain follow-up work; logging sinks already exist.
- [x] **M8.6 CLI commands.**
  - `run [disc] --bios ... [--frames N] [--state-in/out] [--frame-out] [--audio null|wav] [--wav-out]`.
  - `info <disc>` (game ID, name, sector count, volume header).
  - `bios info <path>` (size, MD5, Anvil).
  - `state inspect <path>`.
  - `bench`.
  - `selftest`.
  - Exit codes and machine-readable output (`--json`).
- [~] **M8.7 CLI tests.**
  - State inspector, frame dumper, and save/load tests run in the suite; `selftest` and `bench` were smoke-tested manually.
  - Automated CLI process-level tests (argument errors/exit codes) remain follow-up work.

**Deliverables**

- Versioned save states.
- Diagnostics/tracing.
- Full CLI.

**Acceptance**

- [x] Save/load roundtrip is deterministic across process restarts.
- [x] Corrupt states fail cleanly.
- [x] CLI runs a synthetic disc/ROM and dumps frames/states.
- [x] `bench` produces stable numbers.
- [x] No `BinaryFormatter`/`Marshal` state serialization.

**Risks**

- State completeness: missing a field causes subtle desync. Mitigate with a "state hash" that includes all emulated state and a roundtrip test that runs frames after load.
- Derived state (DSP tables, VRAM mirrors) must be rebuilt consistently.

---

### M9 - Avalonia frontend

**Goal:** a polished cross-platform desktop UI.

**Tasks**

- [~] **M9.1 Shell and navigation.**
  - Implemented a minimal shell: video surface, Open BIOS/Open Disc/Play/Reset/Screenshot buttons, and a status line. Library, Settings, Logs, and About pages remain.
  - Dark/light theme, Fluent, Inter.
  - Status bar: FPS, game, BIOS, audio/video state.
- [~] **M9.2 OpenGL video surface host.**
  - `VideoSurface` control based on Avalonia's `OpenGlControlBase`; hosts `OpenGlRenderer` through a Silk.NET `GL` API. A 6-second Windows launch smoke test passed.
  - Aspect ratio, integer scaling, letterbox; fullscreen, borderless, cursor hide, and escape handling remain.
  - Frame mailbox (`VdlFrameMailbox`): latest frame wins; snapshot copies are tested.
  - CPU `WriteableBitmap` fallback: `CpuVideoRenderer` exists, but the UI does not yet switch automatically when GL 3.3 is unavailable.
  - FPS overlay toggle remains.
- [~] **M9.3 BIOS setup.**
  - File picker, validation, and MD5/Anvil display in the status line.
  - First-run wizard, remembered paths, and error dialogs remain.
- [~] **M9.4 Game library.**
  - Open disc, replace/eject, and volume metadata via the file picker.
  - `GameLibraryScanner` recursively scans ISO/BIN/CUE/CHD files and resolves titles through the 4DO database; `AppSettings.RecentDiscs` keeps the last ten paths. The library view, cover art, and refresh UI remain.
- [~] **M9.5 Settings.**
  - Video: renderer (OpenGL/CPU fallback), filter (accurate/nearest/sharp-bilinear/CRT/xBR), scale, aspect, fullscreen, vsync.
  - Audio: device, buffer, volume/mute.
  - Input: keyboard/gamepad mapping per player, hotkeys.
  - Emulation: ARM clock, fix mode, high-res (disabled in v1), save slots, CPU backend (interpreter; JIT when available).
  - Paths: BIOS, saves, screenshots, library.
  - `AppSettings`/`AppSettingsStore` persist paths, renderer/aspect/vsync/integer scale, audio enable/volume, and input bindings; the settings UI and per-game overrides remain.
- [ ] **M9.6 Input UI.**
  - Binding capture dialog, conflict detection, reset defaults.
  - Controller preview (optional).
- [x] **M9.7 Audio backend.**
  - `OpenAlAudioOutput` via Silk.NET.OpenAL (or chosen backend).
  - Device selection, buffer sizing, underrun handling.
  - Null fallback if no device.
  - `OpenAlAudioOutput` uses a dedicated thread and four queued Stereo16 buffers; `TryCreate` returns null on any device/context failure. The shell enables it when `AppSettings.AudioEnabled` is set.
- [~] **M9.8 Save states/screenshots.**
  - Quick save/load, slot next/previous, slot manager.
  - Screenshot to PNG with game/time name; prefer GL readback, CPU frame fallback.
  - State metadata (game, BIOS, timestamp, thumbnail optional).
  - Quick Save/Load buttons and BMP screenshots are wired in the shell; slot manager, PNG/GL screenshots, and metadata UI remain.
- [ ] **M9.9 Logs/About.**
  - In-app log viewer with category filter.
  - About: version, credits, licenses, links.
- [~] **M9.10 ViewModel tests.**
  - Settings roundtrip, library scanning, frame mailbox, input mapping.
  - No UI thread dependency for logic.
  - Tests cover AppSettings roundtrip, the frame mailbox, renderer contracts, and input mapping; ViewModel-level tests remain.

**Deliverables**

- Full desktop frontend.

**Acceptance**

- [ ] Runs on Windows, Linux, macOS x64.
- [ ] 60 fps with 320x240 output, no UI thread stalls.
- [ ] Fullscreen and integer scaling work.
- [ ] Keyboard/gamepad mapping works.
- [ ] Save/load/screenshot work.
- [ ] Settings persist.
- [ ] No per-frame allocations in the presenter.

**Risks**

- Avalonia 12 API churn. Pin versions and keep the video surface isolated.
- Audio/gamepad native dependencies per RID. Test each platform early; provide null fallbacks.
- UI frame pacing: use latest-frame mailbox, not a queue.

---

### M9R - OpenGL/GLES renderer (required differentiator)

**Goal:** make the OpenGL renderer the default desktop presentation path, with pixel-accurate palette expansion and a high-quality, GPU-based scaling/filter pipeline.

This milestone is intentionally separate from the Avalonia shell. It can start once `VdlFrame` exists (M3/M4) and must be complete before v0.5.

**Tasks**

- [~] **M9R.1 Renderer contract and context adapter.**
  - `IVideoRenderer`, `RendererSettings`, `VideoFilter`, `AspectMode`, `RendererCapabilities` in `ThreeDo.Video`.
  - `GlContextAdapter` that wraps Avalonia's GL context/function loader without referencing Avalonia from `ThreeDo.Video.OpenGL`.
  - `OpenGlRenderer.Initialize/Resize/Present/Dispose`.
  - Capability detection: desktop GL 3.3 or GLES 3.0/3.1, `GL_R16UI`, `usampler2D`, `texelFetch`, integer textures, max texture size, and API type (GL vs GLES).
- [~] **M9R.2 Frame texture upload.**
  - Upload raw 16-bit indexed pixels (`R16UI`).
  - Upload per-line CLUT as an RGB8 texture (32 x height).
  - Upload per-line parameters as an RGBA32UI texture (background, out-control, flags).
  - Allocate immutable storage with `TexStorage2D` at the maximum source size; update with `TexSubImage2D`/PBO ring. **No `TexImage2D` reallocation in steady state.**
  - Cache uniform locations, sampler units, VAO, and the mailbox callback; no per-frame strings/delegates/arrays.
  - The uploader is width/height agnostic; tests cover 320x240 and small synthetic frames, plus an allocation assertion for repeated `Update`/`Present` calls.
- [~] **M9R.3 Palette-expansion pass at native resolution.**
  - Render the indexed frame into a **native-resolution RGB8 texture** (320x240 typical); do not expand the palette once per output pixel.
  - Direct port of `frame.cpp: Get_Frame_Bitmap`:
    - pixel `0` => 5-bit background B/G/R
    - fixed CLUT when `xOUTCONTROLL & 0x02000000` and pixel bit 15
    - otherwise per-line CLUT
  - Exact 5-bit to 8-bit expansion matching the CPU path.
  - Golden-image parity test against `FrameAssembler` for synthetic VDL frames (`GpuPalette` vs `FrameAssembler`, 320x240 x 240 lines).
  - The stage-1 target must be reused across frames; resizing the output must not resize or reallocate it.
  - GPU execution itself is not covered by CI because headless runners have no GL context; run parity on the self-hosted GPU job.
- [~] **M9R.4 Scaling and filter pipeline.**
  - **Stage 2** samples the native-resolution expanded RGB texture at output resolution.
  - **Accurate:** nearest, pixel-perfect, integer scaling.
  - **Nearest:** nearest with arbitrary scale/letterbox.
  - **Sharp-bilinear:** default enhanced mode; implement the full multi-tap version.
  - **CRT:** scanlines, aperture mask, curvature, bloom (opt-in; warn at 4K).
  - **xBR/HQx family:** multi-pass GPU filters (opt-in; benchmark at 4K before enabling).
  - Implemented accurate/nearest, an approximate sharp-bilinear center blend, and CRT scanlines. Full multi-tap sharp-bilinear, CRT aperture/curvature/bloom, and xBR/HQx passes remain.
  - Filter settings persist per game/global in the host settings layer (M9.5).
- [~] **M9R.5 Presentation.**
  - Aspect ratio: 4:3, integer, stretch, custom.
  - Fullscreen/borderless, vsync, frame pacing, latest-frame mailbox.
  - FPS/GPU-time overlay.
  - Window resize without recreating source textures.
  - Size the GL surface in **physical pixels** (`Bounds * RenderScaling`), not logical units; test 100/150/200% DPI.
  - Render only when a new frame is available (or the window/settings change); do not re-request rendering unconditionally at the end of every render.
  - Aspect 4:3/integer/stretch/custom and the latest-frame mailbox are implemented; fullscreen/vsync/overlay/physical-pixel sizing/render-on-demand remain.
- [~] **M9R.6 Screenshots.**
  - GL readback to PNG, respecting the active filter.
  - Optional "native frame" screenshot from the CPU path.
  - `ReadPixels` and the CPU `FrameDumper` are implemented; the UI writes CPU-frame BMP screenshots. GL PNG capture is follow-up.
- [~] **M9R.7 Context loss and fallback.**
  - Detect context loss/reset; recreate textures/shaders.
  - Fall back to CPU `WriteableBitmap` if neither desktop GL 3.3 nor GLES 3.0/3.1 is available; log once.
  - Do not crash on driver/context failure.
  - `Initialize` recreates all GL objects and `CpuVideoRenderer` exists; automatic capability fallback is not wired into the UI yet.
- [ ] **M9R.8 Performance.**
  - Measure upload bandwidth, CPU present time, GPU shader time, and p50/p95/p99 frame time.
  - Target: 4K60 with nearest/sharp-bilinear on Tier A x64; 1080p60 on Raspberry Pi 4/5 class arm64 with the GLES path.
  - Zero CPU per-pixel scaling; palette expansion at native resolution on the GPU.
  - Prove core/presenter CPU cost is independent of output resolution (1080p vs 4K).
  - Budgets and hardware definitions: [`Performance.md`](Performance.md).
- [~] **M9R.9 Tests.**
  - Shader compilation/link tests for both desktop GLSL 330 and GLSL ES 300 (skip if no matching context).
  - CPU-vs-GPU palette parity for all bpp/CLUT/background/fixed-CLUT cases, in both backend variants.
  - Filter screenshot hashes at fixed sizes.
  - Aspect/integer-scaling geometry tests, physical-pixel/DPI sizing tests.
  - Context-loss/fallback tests, including GL 3.3 unavailable -> GLES -> CPU.
  - Allocation assertions for upload/present/mailbox; render-on-demand tests.
  - Headless CI uses the CPU renderer; GPU tests run on a self-hosted or local job.
  - Implemented: uploader packing, palette parity, aspect/integer geometry, CPU renderer, mailbox snapshot. GPU shader link tests, GLES parity, DPI, and allocation tests are follow-up.
- [x] **M9R.10 Renderer spike (early, M3/M4).**
  - Before the full renderer, create a small Avalonia + Silk.NET.OpenGL window that displays a synthetic 320x240 indexed frame with the palette shader.
  - This de-risks context creation, shader loading, and presentation on all three OSes.
  - Implemented as the Avalonia `VideoSurface` + `OpenGlRenderer`; a 6-second Windows launch smoke test passed. Linux/macOS smoke validation remains.

**Deliverables**

- `ThreeDo.Video.OpenGL` with the full presentation pipeline.
- Accurate CPU/GPU parity tests.
- Filter/shader assets.
- CPU fallback path.

**Acceptance**

- [x] Accurate mode matches the CPU `FrameAssembler` pixel-for-pixel on synthetic frames (CPU reference parity; GPU not executed in CI).
- [ ] Two-stage pipeline: palette expansion at native resolution, then upscale/filter at output resolution.
- [ ] 4K output at 60 fps on Tier A x64; 1080p60 on Raspberry Pi 4/5 class arm64 via GLES. See [`Performance.md`](Performance.md).
- [x] Filters are opt-in, stable, and do not affect emulated state.
- [ ] Context loss recovers or falls back cleanly (GL 3.3 -> GLES -> CPU).
- [ ] No per-frame allocations in the upload/present path (uploader and mailbox reuse buffers; GL wrapper calls and uniform lookup are unmeasured).
- [ ] Physical-pixel sizing at 100/150/200% DPI.
- [ ] Runs on Windows, Linux, and macOS.

**Risks**

- GL context integration differs across Avalonia backends/OSes. Mitigate with the early spike and CPU fallback.
- Shader precision/rounding could differ from the CPU palette path. Mitigate with exact integer texture sampling and golden tests.
- Per-line CLUT texture lookups can be slow on weak GPUs. Mitigate with a 32xheight palette texture and `texelFetch`.
- Driver bugs with `R16UI`/integer textures. Mitigate with a fallback to `R16` normalized or CPU BGRA upload.
- xBR/HQx multi-pass filters can be expensive at 4K. Make them opt-in and expose a quality/performance setting.

---

### M9P - Runtime performance, pacing, and 4K presentation

**Goal:** make the 4K60 target structural rather than a late optimization: a dedicated
core thread, a two-stage presenter, an allocation-free upload path, a GL/GLES backend
matrix, and a benchmark gate that runs throughout development.

**Why this exists:** the M9/M9R prototype runs emulation on a 16 ms UI
`DispatcherTimer`, expands the palette once per output pixel, calls `TexImage2D` and
`GetUniformLocation` every frame, sizes the GL surface in logical pixels, and has a
stub benchmark project. Those are architectural shortcuts that cannot be fixed by
micro-optimizing at release time. See [`Performance.md`](Performance.md).

**Tasks**

- [~] **M9P.1 Baseline and budgets.**
  - Benchmark harness and a CPU-side baseline are recorded (synthetic 2.26 ms/frame,
    Lemmings 2.11 ms/frame). Tier B, p50/p95/p99, allocations/frame, and GPU time
    remain to be recorded.
- [x] **M9P.2 Dedicated core thread and frame pacer.**
  - Add `EmulatorSession` and `FramePacer` to `ThreeDo.Host`.
  - Run `RunFrame` on a dedicated core thread at a fixed 60 Hz; catch-up capped at
    one or two frames.
  - The UI thread never calls `RunFrame`; replace the `DispatcherTimer` emulation
    loop. Keep UI updates (status/FPS) at 4–10 Hz.
  - Pause/resume/reset/save/load and audio must be thread-safe at the session
    boundary; no locks in the core hot path.
- [~] **M9P.3 Two-stage presenter.**
  - Stage 1: palette expansion into a native-resolution RGBA8 FBO.
  - Stage 2: nearest/sharp-bilinear/CRT at output resolution (xBR still pending).
  - Desktop GL 3.3 path implemented; GLES 3.0/3.1 source and binding selection
    remain.
- [x] **M9P.4 Persistent upload and allocation-free present.**
  - `TexStorage2D` for the indexed/CLUT/parameter textures plus per-frame
    `TexSubImage2D`; no per-frame `TexImage2D`.
  - Uniform locations and the present delegate are cached; the status string is
    updated at 4 Hz on the UI thread, not in the frame path.
  - `VdlFrameMailbox` copies only the visible width and can copy into a caller-owned
    frame so the lock is not held during GPU submission.
  - CI allocation assertions are still to be added (M9P.9).
- [~] **M9P.5 Physical-pixel sizing and render-on-demand.**
  - Render-on-demand is implemented: the Avalonia surface renders only when the
    mailbox sequence or the output size changes.
  - Physical-pixel sizing from `Bounds * RenderScaling` is implemented.
  - Manual validation at 100/150/200% DPI and live resize remains.
- [~] **M9P.6 Renderer backend matrix.**
  - Desktop GL 3.3 (GLSL 330) and GLES 3.0 (GLSL ES 300) shader variants are
    selected from the reported context version.
  - Windows now prefers `Wgl` with `AngleEgl`/software fallbacks; ANGLE and
    Raspberry Pi 4/5 still need real-hardware validation, and the Silk.NET binding
    path under an ES context is unverified.
  - Capability detection distinguishes API type and version; vsync reporting remains.
- [ ] **M9P.7 CHD/disc prefetch.**
  - Add a bounded host-side prefetch worker/cache for CHD/ISO sectors.
  - Keep emulated CD timing deterministic; no wall-clock reads in the core.
  - Benchmark CHD vs ISO p99 frame time on Tier B.
- [ ] **M9P.8 Hot-path optimization (measurement-driven).**
  - Optimize the ARM interpreter (fast RAM path, dispatch/decode, fewer interface
    calls) before considering M12.
  - Profile and optimize CEL inner loops and DSP `Loop()` with golden output
    unchanged.
  - Do not add GPU CEL compositing for the 4K output target.
- [~] **M9P.9 Benchmark suite and CI gate.**
  - `ThreeDo.Benchmarks` now measures the machine core, video pack, and mailbox in
    synthetic and real-disc modes, with JSON output and a 60 Hz budget comparison.
  - CI runs a synthetic benchmark smoke and uploads the JSON baseline as an
    artifact.
  - p50/p95/p99, allocations, GPU timer queries, and the >10% regression gate
    remain.
- [ ] **M9P.10 Documentation and report.**
  - Publish the measured 4K/1080p results, hardware, and filter settings.
  - Update `Performance.md`, `TestCorpus.md`, and the compatibility matrix.

**Deliverables**

- Dedicated core thread and frame pacer.
- Two-stage 4K presenter with persistent upload.
- Desktop GL/GLES backend variants.
- Benchmark harness and CI performance gate.
- Measured 4K60 Tier A and 1080p60 Tier B report.

**Acceptance**

- [ ] 4K60 with nearest/sharp-bilinear on Tier A; 1080p60 on Raspberry Pi 4/5 class.
- [ ] Core frame time p99 ≤ 8 ms on Tier A and independent of output resolution.
- [ ] Presenter CPU ≤ 2 ms p99; zero managed allocations per frame in steady state.
- [ ] Physical-pixel sizing at 100/150/200% DPI.
- [ ] Render-on-demand verified at 60 Hz and 144 Hz.
- [ ] GL 3.3 and GLES 3.0/3.1 variants produce identical Accurate-mode output.
- [ ] No `DispatcherTimer` emulation loop remains; no per-frame status strings.
- [ ] Benchmark baseline and regression gate are in CI.

**Risks**

- Avalonia/WGL/ANGLE context differences. Mitigate with the backend matrix and a
  CPU fallback; validate on real hardware, not only a launch smoke test.
- Two-stage presenter adds an offscreen target. At native resolution it is tiny; the
  cost is one extra fullscreen sample, not a second 4K pass.
- Threading bugs at the session boundary. Keep the core single-threaded; only the
  host mailbox/ring buffer crosses threads.
- Tier B CHD decode cost. Prefetch/cache and measure; document minimum hardware.
- Performance CI noise. Gate PRs on allocations/unit budgets; run timing gates
  nightly on stable hardware.

---

### M10 - Packaging, CI, performance, arm64

**Goal:** shippable, reproducible builds for all target platforms.

**Tasks**

- [~] **M10.1 Publish profiles.**
  - `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.
  - Self-contained, single-file, `PublishTrimmed=false` initially.
  - Include native audio/gamepad assets and GLSL shader resources per RID.
- [x] **M10.2 CI matrix.**
  - Build/test on Windows/Linux/macOS.
  - Publish all RIDs; upload artifacts.
  - Cache NuGet; deterministic builds.
- [ ] **M10.3 Trimming/AOT evaluation.**
  - JSON source generation, no reflection.
  - Evaluate `PublishTrimmed`/NativeAOT per project; do not break Avalonia.
- [~] **M10.4 Icons/metadata.**
  - App icon, file associations (`.iso`, `.bin`, `.cue`, `.tdos`), version info.
- [ ] **M10.5 Performance validation and report.**
  - Consume the M9P.9 benchmark harness and baselines; do not defer structural optimization to this milestone.
  - Run Tier A (4K60) and Tier B (1080p60) with the default filters, CHD vs ISO, and the local corpus.
  - Verify core/presenter CPU cost is independent of output resolution and allocations are zero/frame.
  - Publish the performance report and update [`Performance.md`](Performance.md).
- [ ] **M10.6 arm64 validation.**
  - Run on Windows arm64 (if available), Linux arm64 (Raspberry Pi 4/5), macOS arm64.
  - Verify audio/gamepad backends.
- [ ] **M10.7 Installers (optional).**
  - Windows MSIX/zip, macOS dmg/zip, Linux tar.gz/AppImage.
  - Signing/notarization plan.

**Deliverables**

- Six RID packages.
- CI artifacts.
- Performance report.

**Acceptance**

- [ ] Every package starts and reaches the UI/CLI.
- [ ] OpenGL context initializes on supported systems; CPU fallback works otherwise.
- [ ] Tests green on CI.
- [ ] Performance targets met or documented.
- [ ] No native emulator code in any package.

---

### M11 - Compatibility hardening and release

**Goal:** real-game compatibility, high-res, filter polish, release.

**Tasks**

- [ ] **M11.1 Compatibility corpus.**
  - Test the matrix in `docs/Compatibility.md`.
  - Triage crashes/graphics/audio bugs.
  - Add per-game fixes only with evidence.
- [ ] **M11.2 High-resolution mode.**
  - `RESSCALE=1`: VRAM mirroring, VDLP line duplication, SPORT copies, CEL stepping, frame extraction.
  - Golden tests and real-game validation.
  - Expose as an "Enhanced internal resolution" option, separate from output upscaling.
  - This is not the 4K output target. 4K *internal* rendering requires GPU CEL compositing and is a separate post-v1 experiment.
- [ ] **M11.3 Filter polish (required family, optional per game).**
  - Finalize sharp-bilinear, CRT, and xBR/HQx GLSL pipelines.
  - Add per-game filter presets and performance warnings at 4K.
  - Keep the accurate path pixel-identical to the CPU oracle.
- [ ] **M11.4 CD timing/seek improvements.**
  - Only if real games require it; keep transcript tests.
- [-] **M11.5 CHD support.**
  - Moved to v1/M6: `ChdDiscImage` via CHDSharp, including parent/child and read-ahead.
- [ ] **M11.6 Documentation/release.**
  - User guide, compatibility list, known issues, changelog.
  - License/attribution review.
  - Release notes.
- [ ] **M11.7 Post-release backlog.**
  - Netplay, rewind, debugger UI, CD-DA, PAL, additional peripherals.
- [ ] **M11.8 Optional GPU CEL compositing experiment.**
  - Evaluate RTT-based or compute-shader CEL rendering against the software path.
  - Compute shaders require GL 4.3+ or Vulkan/Metal; they are outside the GL 3.3/GLES 3.1 v1 baseline.
  - Keep software CEL as the accuracy path; ship only if parity and performance are proven. This is relevant to 4K internal rendering, not the 4K output target.

**Acceptance**

- [ ] v1.0 checklist in §1.3 complete.
- [ ] No known crash bugs in the supported test set.
- [ ] Release artifacts and docs published.

---

### M12 - Optional guest ARM JIT (post-v1 unless profiling demands it)

**Goal:** add a pure managed IL JIT for the ARM60 behind `ICpuBackend`, with bit-identical behavior and a measured performance win. This is **not** required for 60 fps; the 12.5 MHz CPU is not expected to be the bottleneck.

**Tasks**

- [ ] **M12.1 Profile first.**
  - Measure interpreter cost per frame on the target devices.
  - Only proceed if CPU interpretation is a measured top-three cost.
- [ ] **M12.2 Block discovery and decoding.**
  - Identify basic blocks, terminators, PC-relative literals, and self-modifying code.
  - Invalidate blocks on RAM writes that overlap translated code.
- [ ] **M12.3 IL emission.**
  - `DynamicMethod`/`ILGenerator` per block, with guest registers mapped to locals/fields.
  - Call memory helpers for loads/stores and MMIO.
  - Preserve condition codes, mode switches, FIQ, cycle counts, and compatibility hacks.
- [ ] **M12.4 Differential tests.**
  - Run the full CPU instruction suite through both backends; compare registers, flags, memory, and cycles.
  - Run synthetic programs and real BIOS/disc smoke tests.
- [ ] **M12.5 Performance gate.**
  - Require a measurable frame-time or CPU-time win; otherwise keep the interpreter default.
- [ ] **M12.6 Safety.**
  - Bounded code cache, invalidation, fallback to interpreter on unsupported instructions.
  - No executable memory, no P/Invoke, no native code.

**Acceptance**

- [ ] Bit-identical architectural state and cycle counts vs the interpreter.
- [ ] Measured win on at least one target platform.
- [ ] No regression in save states or determinism.
- [ ] Disabled by default until proven.

---

## 6. First sprint (recommended order)

A concrete two-week start:

**Days 1-2 - M0**

- [ ] Create solution, projects, references, build props.
- [ ] Create CLI/Avalonia placeholders.
- [ ] Create `ThreeDo.Tests` and fake sinks.
- [ ] CI build/test.
- [ ] Commit the docs.

**Days 3-5 - M1**

- [ ] Core constants/interfaces.
- [ ] `ThreeDoMemory` + byte-order tests.
- [ ] Address decode + sentinel tests.
- [ ] `Quartz` + fixed-point tests.
- [ ] State writer/reader + CRC tests.

**Days 6-10 - M2.1-M2.6**

- [ ] CPU state/reset.
- [ ] ALU/shifter/conditions.
- [ ] Decoder skeleton.
- [ ] Data processing instructions + tests.
- [ ] Multiply/swap + tests.
- [ ] Single data transfer + tests.

**End of sprint demo**

- [ ] A synthetic ARM program executes data processing/loads/stores and writes a known result to RAM.
- [ ] `dotnet test` green.
- [ ] Plan updated with actuals.

---

## 7. Cross-cutting workstreams

### 7.1 Testing

- Unit tests accompany each ported function.
- Integration tests use synthetic ROMs and discs.
- Golden tests for CPU traces, frames, DSP samples, CD transcripts.
- Optional developer-only oracle against FreeDOCore.
- Fuzz tests for CPU/CD/CEL/state parsers.
- Benchmarks for CPU/CEL/DSP/frame loop.

See [`docs/TestStrategy.md`](docs/TestStrategy.md).

### 7.2 Documentation

- Keep `docs/PortingNotes.md` updated with every deliberate deviation.
- Keep `docs/Compatibility.md` updated with tested games.
- Add XML docs to public APIs.
- Add a `CHANGELOG.md` at v0.1.

### 7.3 Performance

- Performance is a continuous gate, not an M10 cleanup. [`Performance.md`](Performance.md) defines the target, budgets, hardware tiers, and measurement method.
- Measure before optimizing; record before/after numbers and identical golden output.
- No per-frame or per-instruction allocations in steady state. No LINQ, string formatting, or delegate allocation in hot paths.
- Prefer `Span<T>`, `Unsafe`, `MemoryMarshal`, and reusable buffers only where measurements justify them.
- Keep the core single-threaded and deterministic. The host owns the core thread, frame pacer, render thread, and audio thread.
- The 4K target is output upscaling. Do not add GPU CEL compositing for 4K output; 4K internal rendering is post-v1.
- Do not sacrifice accuracy for speed without golden evidence.

### 7.4 Legal/attribution

- Preserve FreeDO/4DO headers.
- Keep the About/Licenses page current.
- No BIOS/disc content in the repo or releases.
- Review the distribution model before v1.0.

---

## 8. Risk register

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | MADAM CEL port has subtle pixel/geometry bugs | High | High | Port in original order; exhaustive pixel tests; golden frames; real BIOS UI comparison |
| R2 | ARM decode fallthrough/flag bugs | Medium | High | Instruction matrix tests; disassembler; optional oracle traces |
| R3 | DSP branch/ALU table bugs | Medium | High | Test all branch codes/flags; sample golden tests; WAV listening |
| R4 | CD command state machine mismatches | Medium | High | Transcript tests for every command; real disc boot; compare with 4DO |
| R5 | Scheduler timing approximations cause game hangs | Medium | High | Preserve FreeDO semantics; add game-specific fixes only with evidence |
| R6 | Cross-platform audio/gamepad dependencies | Medium | Medium | Interfaces + null fallbacks; test per RID early; choose SDL/OpenAL after spike |
| R7 | Avalonia API churn / UI performance | Medium | Medium | Pin versions; latest-frame mailbox; OpenGL presenter with CPU fallback |
| R7a | OpenGL context/driver differences across Windows/Linux/macOS | High | High | Early M3.13 spike; GL 3.3 capability check; CPU fallback; context-loss recovery |
| R7b | GPU palette shader differs from CPU frame assembler | Medium | High | Integer textures, exact 5-bit expansion, golden parity tests |
| R7c | Optional ARM IL JIT diverges from interpreter | Medium | High | `ICpuBackend` differential suite; disabled by default; interpreter oracle |
| R8 | Save state incompleteness | Medium | High | State hash; roundtrip tests; section-by-section ownership |
| R9 | Licensing restrictions block distribution | Medium | High | Early legal review; non-commercial assumption; attribution |
| R10 | No redistributable BIOS/disc for CI | High | Medium | Synthetic ROM/disc fixtures; opt-in real-content tests |
| R11 | Scope creep (extra filters, netplay, rewind, GPU CEL) | High | Medium | Renderer filter family is fixed in M9R; everything else deferred to M11+ |
| R12 | Performance on arm64 | Low/Medium | Medium | Benchmark early; avoid allocations; profile CEL/DSP |
| R13 | Floating-point differences x64/arm64 | Low | Medium | Integer timing; golden frame hashes; replace hot float math if needed |
| R14 | Native audio/gamepad packaging complexity | Medium | Medium | Isolate per RID; ship null fallback; document optional backends |
| R15 | CHD track layouts / CHDSharp integration | Medium | High | Validate first data track; support Mode1/2048, Mode1/2352, Mode2/Form1; reject unsupported layouts; ISO/BIN parity tests |
| R16 | CHD decompression cost on slow arm64 | Medium | Medium | Read-ahead/cache; async prefetch; benchmark CHD vs ISO p99; document minimum hardware |
| R17 | 4K presentation is CPU/GPU-bound or jittery | High | High | Two-stage presenter; persistent upload; dedicated core thread; p99 budgets; M9P gate |
| R18 | Desktop GL 3.3 assumption fails under Avalonia ANGLE or on Pi4 | High | High | GL 3.3 + GLES 3.0/3.1 variants; WGL/ANGLE matrix; real-hardware validation |
| R19 | Performance work deferred to release and becomes a rewrite | High | High | M9P before M10; benchmark harness and allocation tests now |
| R20 | 4K internal rendering expectations are conflated with 4K output | Medium | Medium | Document the distinction; 4K internal is post-v1 and requires GPU CEL |

---

## 9. Quality gates / definition of done

A milestone is done only when:

- [ ] All tasks are checked or explicitly deferred with a reason.
- [ ] Unit tests cover new behavior.
- [ ] At least one integration test exercises the milestone through the machine.
- [ ] Golden/differential tests pass where applicable.
- [ ] Save/load roundtrip is tested for new state.
- [ ] No per-frame allocations in steady state.
- [ ] Performance budget met for the milestone; p50/p95/p99 frame time, allocations, and hardware recorded.
- [ ] Renderer backend matrix validated where applicable (desktop GL 3.3 and/or GLES 3.0/3.1).
- [ ] No mutable static state.
- [ ] XML docs and traceability comments are present.
- [ ] `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` pass.
- [ ] CI is green on all supported runners.
- [ ] The plan and compatibility docs are updated.

**Per-commit checklist**

- [ ] One logical change.
- [ ] Tests added/updated.
- [ ] No unrelated formatting churn.
- [ ] No copyrighted content.
- [ ] Commit message names the milestone/task (e.g., `M2.4: data processing instructions`).

---

## 10. Open decisions

These need a decision before the relevant milestone; the plan assumes the default in parentheses.

1. **Final product name and namespace prefix.** Default: solution `3DOEmulator`, namespace `ThreeDo`.
2. **Distribution license.** Default: non-commercial, source-available, pending FreeDO/4DO permission. See `docs/Licensing.md`.
3. **Renderer.** **Decided: OpenGL is required for the desktop frontend**, with two variants behind one contract: desktop GL 3.3/GLSL 330 via Silk.NET.OpenGL, and GLES 3.0/3.1/GLSL ES 300 via Silk.NET.OpenGLES. Windows uses WGL or ANGLE/GLES; Pi 4/5 uses GLES. The CPU `WriteableBitmap` path is only a fallback/oracle.
4. **Default filter.** Default: sharp-bilinear for enhanced mode; "Accurate" (nearest/integer) must be available and pixel-identical to the CPU assembler.
5. **Audio backend.** Default: OpenAL Soft via Silk.NET.OpenAL, with null/WAV fallbacks. Alternative: SDL2 audio.
6. **Gamepad backend.** Default: evaluate Avalonia 12 built-in; fall back to Silk.NET.Input/SDL2.
7. **4DO save-state compatibility.** Default: no; own versioned format. Legacy import can be added later.
8. **CHD support.** **Decided: required v1 format** via the pure C# CHDSharp library (M6.1a). ISO/BIN/CUE remain supported.
9. **PAL support.** Default: later; NTSC 60 Hz first.
10. **High-resolution CEL mode.** Default: later (M11); v1 renders 320x240. Output upscaling to 4K is always available via OpenGL/GLES. **4K internal CEL rendering is not a v1 goal.**
11. **Minimum OS versions.** Default: Windows 10+, macOS 12+, current Linux desktop with desktop OpenGL 3.3 or OpenGL ES 3.0/3.1.
12. **UI visual direction.** Default: clean dark/light Fluent, game library + settings, no skeuomorphic 3DO shell.
13. **Game database source/licensing.** Default: port 4DO data with attribution, pending review.
14. **Guest ARM JIT.** Default: optional managed IL JIT (M12), disabled by default, only if profiling shows the interpreter is a top cost after interpreter optimization. The .NET JIT is already used for all C# code.
15. **Whether to ship an optional native audio binary.** Default: yes for desktop, but the core and CLI must work without it.
16. **4K target definition.** **Decided: 4K output (3840x2160) at 60 fps on Tier A, not 4K internal CEL rendering.** See [`Performance.md`](Performance.md).
17. **Threading.** **Decided: single-threaded deterministic core on a dedicated core thread; render/UI and audio threads at the host boundary.** The current UI-timer prototype is replaced by M9P.2.
18. **Performance gate.** **Decided: continuous budgets and a nightly/self-hosted benchmark gate; no structural optimization deferred to M10.** See [`Performance.md`](Performance.md).

---

## 11. Effort estimate and sequencing

| Phase | Estimate | Depends on |
|---|---:|---|
| M0 Bootstrap | 3 dd | - |
| M1 Core/memory/scheduler | 5 dd | M0 |
| M2 ARM60 CPU | 12 dd | M1 |
| M3 Boot skeleton | 10 dd | M2 |
| M4 MADAM graphics | 20 dd | M3 |
| M5 DSP/audio | 12 dd | M3 (parallel with M4 after M3) |
| M6 XBUS/CD + CHD | 12 dd | M3 (parallel with M4/M5) |
| M7 Input/NVRAM/fixes | 6 dd | M4/M6 |
| M8 Save states/CLI | 10 dd | M4/M5/M6 |
| M9 Avalonia | 16 dd | M8 |
| M9R OpenGL renderer | 10 dd | M4/M9 (spike can start at M3) |
| M9P Performance/pacing | 10 dd | M9R |
| M10 Packaging/perf validation | 6 dd | M9P |
| M11 Compatibility/release | 25+ dd | M10 |
| **Subtotal** | **157 dd** | |
| Contingency (30%) | ~47 dd | |
| **Total** | **~204 dd** | |
| M12 optional ARM IL JIT | 10-20 dd | M2, only if profiling demands |

Suggested parallelization with two developers:

- Dev A: CPU, memory, CLIO, machine, save states.
- Dev B: MADAM, VDLP, SPORT, DSP, CD.
- Dev B (later): OpenGL/GLES renderer and shader pipeline.
- Both: tests, frontend, performance budgets, compatibility.
- Do not parallelize the same chip; keep one owner per subsystem.

---

## 12. Immediate next actions

1. ~~Finish M0 scaffolding and commit.~~ Done.
2. ~~Implement M1 memory + Quartz + state primitives.~~ Done.
3. ~~Start M2 with the CPU state/ALU/shifter and the test assembler.~~ Done.
4. ~~Create the first synthetic ROM integration test as soon as M2 data processing and loads/stores work.~~ Done.
5. ~~Start M3: BIOS/NVRAM loading, CLIO basics, VDLP, SPORT, and the frame loop.~~ Done.
6. ~~Start M4: MADAM CEL engine, pixel pipeline, matrix engine validation, and BIOS UI rendering.~~ M4.1-M4.14 are done. M4.15 renders the boot animation; the local corpus now provides real discs for the interactive-menu tracing.
7. ~~M5 DSP/audio and M6 XBUS/CD-ROM/storage.~~ Cores, sinks, disc containers, CHD parent/child, volume header, and command transcripts are implemented. The local corpus provides five CHDs; real-disc boot validation and audible WAV verification are the next tests.
8. ~~M7 input/NVRAM/game database, M8 save states/CLI, M9 basic shell, M9R core OpenGL presenter, and M10 CI/CLI publish.~~ Done as a prototype; next are M9P performance/pacing, the remaining M9 pages/audio/settings UI, M9R filters/GPU tests, M10 arm64/trimming/installers, M11, and real-disc boot validation.
9. Run the real-content smoke tests in [`TestCorpus.md`](TestCorpus.md) and record results in `docs/Compatibility.md`.
10. Start M9P.1/M9P.2 before more UI polish: benchmark baseline, dedicated core thread, and frame pacer.
11. Validate the renderer backend matrix (WGL desktop GL vs ANGLE GLES; Pi 4/5 GLES 3.1) and physical-pixel sizing before claiming 4K60.
12. Schedule a 30-minute legal/attribution review before any public build.

---

## 13. Progress log

| Date | Milestone | Task | Notes |
|---|---|---|---|
| 2026-10-23 | M0 | Plan/architecture written | Initial docs and scaffold |
| 2026-10-23 | M0.1-M0.7 | Scaffold complete | `dotnet build`, `dotnet test` (2 tests), and `dotnet format --verify-no-changes` pass; commit `e620ebb` |
| 2026-10-23 | Architecture | OpenGL renderer made required; optional ARM JIT added as M12 | `ThreeDo.Video.OpenGL` project added; docs updated |
| 2026-10-23 | Storage | CHD made a required v1 format via CHDSharp | `IDiscSource` + `ChdDiscImage` added; package wired into `ThreeDo.Storage` |
| 2026-10-23 | M1.1-M1.4 | Core contracts, memory, and bus | Constants/types/interfaces; `ThreeDoMemory` with word-swapped RAM/ROM, NVRAM quirk, bank select, and MMIO hooks |
| 2026-10-23 | M1.5-M1.7 | Quartz, state primitives, diagnostics | 8.24 scheduler; sectioned little-endian state with CRC32; filtered logging with interpolated-string handler |
| 2026-10-23 | M1.8 | M1 test suite | 95 tests pass; memory alignment/byte-order/NVRAM/decode, Quartz fixed-point/event counts, state truncation/CRC, logging filters; commit `9d730ca` |
| 2026-10-23 | M2.1-M2.16 | ARM60 CPU | Full `arm.cpp` interpreter port: state/banked registers, ALU/shifter, decode order, data processing, multiply/swap, LDR/STR, LDM/STM, branch/SWI/undefined, MRS/MSR, FIQ, cycle counts, disassembler, test assembler, synthetic ROM; M2.15 deferred (native oracle outside repo); 262 tests pass; commit `8fd8a9b` |
| 2026-10-23 | M3.1-M3.11 | Boot skeleton | BIOS/NVRAM images, CLIO registers/timers/FIFOs/DMA, MADAM registers/FSM/matrix/PBUS, VDLP parser, SPORT, DiagPort, Quartz frame loop, CPU frame assembler, XBUS stub, DSP stub; 321 tests pass; commit `958d33c` |
| 2026-10-23 | M3.12 | Real BIOS smoke | `bios/panafz1.bin` ran 300 frames without exception; optional test gated by `THREEDO_BIOS` |
| 2026-10-23 | M3.13 | OpenGL spike deferred | Requires desktop GL context; scheduled with M9R.1/M9R.10 and does not block M4-M8 |
| 2026-10-23 | M4.1-M4.3 | CEL bit reader, CCB parser, PLUT loader | `CelBitReader` (bit rates 1-32, positions, skip), `CcbControl`/`CcbHeader`/`CcbParser` (absolute/relative pointers, preamble, packed/skip/last), `Madam.LoadPlut`; 337 tests pass; commit `b600424` |
| 2026-10-23 | M4.4 | Pixel decoder | 1/2/4/6/8/16 bpp, linear/coded modes, palette-write bit, AMV tables, transparency; 346 tests pass; commit `7ce230d` |
| 2026-10-23 | M4.5 | Pixel processor and projector | `PixelProcessor` (PSCALAR, AV, PXOR, NOBLK, signed-char clipping) and `Projector` (PLUTPOS, SWAPHV/NOSWAP, B15/B0); differential vectors and grid hashes from the reference C++; 400 tests pass; commit `84dec5d` |
| 2026-10-23 | M4.6 | CEL geometry and clipping | `CelGeometry` (`TestInitVisual`, line/scale/arbitrary maps, CCW tests), `TexelFunction`; 300k-case differential hash from the reference C++; 404 tests pass; commit `2f5c106` |
| 2026-10-23 | M4.11-M4.12, M4.16 | Matrix engine, PBUS DMA, high-res decision | Matrix command 0 and Nfrac coverage; multi-word and negative-length PBUS DMA coverage; `RESSCALE=0` decision recorded; 408 tests pass; commit `0b1146c` |
| 2026-10-23 | M4.13 | CEL cycle accounting | `CelMemory` with +1/+2 halfword/word access and +4 PLUT reads, take-and-clear `GetCELCycles`; 411 tests pass; commit `e3d0eea` |
| 2026-10-23 | M4.7-M4.10 | CEL rasterizers and texel primitives | `CelEngine` CCB walker wired into `Madam.HandleCel`; packed/literal/LR rasterizers; line/scale/arbitrary texel primitives; `FIX_BIT_TIMING_3`/`FIX_BIT_TIMING_6`/`FIX_BIT_GRAPHICS_STEP_Y` and `speedfixes`/`unknownflag11`/`sdf` heuristics; bpp matrix (1/2/4/6/8/16), multi-CCB PLUT reload, and transparency/cycle tests; 432 tests pass; real BIOS 300-frame smoke test passes; commit `ed60120` |
| 2026-10-23 | M4.14 | Golden CEL tests | Clip-window corner tests, composite scene with three CCBs, PLUT reload, transparent texel, stable VRAM hash and CEL cycle count; optional BIOS boot-frame PNG dump (`BiosUiValidationTests`); 437 tests pass |
| 2026-10-23 | M5.1-M5.9 | DSP core | Full `DSP.cpp` port: instruction bit-fields, request/BS/REGCONV/BRCONDTAB precompute, ALU/MULT, barrel shifter, operand loader (nrof/immediate/r2/r3/DI), control flow, memory windows, semaphore, reset/PRNG, 567-cycle sample cadence; 19 DSP tests |
| 2026-10-23 | M5.10-M5.12 | Audio pipeline and BIOS smoke | `AudioRingBuffer` (SPSC), `AudioPipeline`, `NullAudioSink`, `WavAudioSink`; allocation-free ring buffer, WAV header, and conversion tests; real BIOS starts the DSP and the frame loop pushes samples; audio content remains silent without a valid disc/menu |
| 2026-10-23 | M9.4 partial | Library scanning | `GameLibraryScanner` with recursive scans, format/sector/metadata inspection, unknown-file fallback, and recent-disc persistence; tests cover valid/invalid/nested images |
| 2026-10-23 | M8.5, M10.4 | Diagnostics and metadata | CPU tracer/disassembler stepping, raw memory/VRAM dumper, PNG frame dumper, application icon and product metadata |
| 2026-10-23 | M9.5, M9.7 | Settings and live audio | `AppSettings` persistence, quick Save/Load and screenshot buttons, `OpenAlAudioOutput` (OpenAL Soft, four queued buffers, null fallback) wired into the shell; Windows launch smoke tests passed |
| 2026-10-23 | M9.1-M9.4, M9.8, M9R.10, M10.2 | Desktop shell and packaging | OpenGL `VideoSurface` hosts the renderer; file pickers, play/pause/reset, quick save/load, screenshots, keyboard input, persisted `AppSettings`; CI publishes CLI/frontend for six RIDs; win-x64 CLI publish smoke test passed; Windows app launch smoke test passed |
| 2026-10-23 | M9R.1-M9R.3, M9R.9-M9R.10 | OpenGL presenter | `IVideoRenderer`/settings/capabilities, CPU renderer, `VdlFrameUploader`, `GpuPalette` parity, aspect/integer viewport, GLSL 330 palette shader, `OpenGlRenderer`, Avalonia `VideoSurface`, frame mailbox; 527 tests pass; Windows launch smoke test |
| 2026-10-23 | M8.1-M8.4, M8.6 | Save states and CLI | Versioned `TDO1` state with CRC sections, per-chip state (incl. DSP/memory/frame/compatibility), deterministic save/load test, atomic files; CLI `run`, `info`, `bios info`, `state inspect`, `bench`, `selftest` with `--json`; state inspector and BMP dumper |
| 2026-10-23 | M7.1-M7.8 | Input, NVRAM, game database | `ControllerState`/`PbusEncoder`/`InputManager` with wiring order and every button; JSON `InputProfile` defaults/roundtrip; `KeyboardInputMapper`; `IGamepadBackend`/null backend; `NvramStore` debounced atomic writes and corrupt-image backup; embedded 4DO `GameDatabase.xml` with checksum/ID lookup; tests for PBUS matrix, profiles, NVRAM and database |
| 2026-10-23 | M6.1-M6.6, M6.8 | CD-ROM, XBUS, storage | `CdromDevice` command state machine and XBUS routing; `IsoImage`/`BinImage`/`CueSheet`/`ChdDiscImage` (CHDSharp 1.5.2, read-ahead, parent/child); `VolumeHeader`/`GameId`; command transcripts, ISO/BIN/CUE/CHD parity, CHD rejection and state roundtrip; BIOS passes the XBUS device poll and renders its boot animation |
| 2026-10-23 | M9P | Performance review and plan correction | Added M9P; two-stage native-resolution palette expansion, dedicated core thread/frame pacer, persistent upload, GL 3.3 + GLES 3.0/3.1 backend matrix, physical-pixel sizing, benchmark/CI gates; documented in `docs/Performance.md` |
| 2026-10-23 | Testing | Local real-content corpus | Documented `D:\Samples\3DO` and `bios/` in `docs/TestCorpus.md`; five CHDs inventoried (Alone in the Dark, Lemmings, Road Rash, Space Ace, Wolfenstein 3D); 600-frame smoke baseline and WAV sample counts recorded; boot still stops in the BIOS menu/halt path for most titles |
| 2026-10-23 | M9P.1-M9P.5, M9P.9 | Core thread, two-stage presenter, persistent upload, render-on-demand, benchmark harness | `CoreRunner` runs the machine on a dedicated 60 Hz thread with pause/invoke marshalling; `OpenGlRenderer` expands palettes into a native-resolution FBO and scales in a second pass; `TexStorage2D` + `TexSubImage2D`, cached uniforms, visible-width mailbox copies, and on-demand rendering; benchmark reports 2.26 ms/frame synthetic and 2.11 ms/frame Lemmings (CPU only); 537 tests pass |
| 2026-10-23 | Compatibility | CD disc-end fix and real-game boot | `InitCd` no longer double-adds the 150-frame lead-in; the CD DIPIR accepts Road Rash, Space Ace, and Wolfenstein. Lemmings renders CyberIntro, Space Ace renders the ReadySoft demo intro, Road Rash/Wolfenstein load their own OS and stop at the shared SAudio init failure. See `docs/TestCorpus.md` |
