# 3DOEmulator - Detailed Implementation Plan

**Working title:** 3DOEmulator
**Target:** pure C# / .NET 10, cross-platform (Windows, Linux, macOS; x64 and arm64)
**Reference core:** `References/4DO-master/FreeDOCore` (primary), `References/opera-libretro-master/libopera` (secondary)
**Status:** M0 planning/scaffolding
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
- A **headless CLI** for running discs, dumping frames/states, testing, and benchmarking.
- An **Avalonia desktop frontend** for Windows, Linux, and macOS with a game library, settings, controller mapping, save states, screenshots, and fullscreen.
- A **unit/integration test suite** that can run without a copyrighted BIOS by using synthetic ROMs and fixtures.
- **Deterministic save states** and optional input recording/replay.

### 1.2 What "pure C#" means here

- No C++/CLI, no native `FreeDOCore.dll`, no P/Invoke into an emulator core.
- The emulation core, host, CLI, and frontend logic are C#.
- Host-only services may use platform/native libraries behind interfaces:
  - Avalonia rendering (its own native backend).
  - Audio output (OpenAL Soft, SDL, or platform APIs).
  - Gamepad input (SDL/GameController or platform HID).
- These host services are optional: the core, CLI headless mode, and tests must run with null audio/input backends and no native emulator code.

### 1.3 Success criteria

**v0.1 - Boot milestone**

- [ ] Boots a real 3DO BIOS to its menu.
- [ ] Renders the BIOS UI at 320x240, 60 Hz.
- [ ] Responds to keyboard input.
- [ ] Does not crash for 10 minutes of menu navigation.
- [ ] Synthetic-ROM integration tests pass in CI.

**v0.5 - Playable milestone**

- [ ] Boots at least 10 real discs to gameplay (user-provided test set).
- [ ] Audio works for DSP-driven music/SFX.
- [ ] NVRAM persists between sessions.
- [ ] Save states roundtrip deterministically.
- [ ] 60 fps on a mid-range x64 desktop and a Raspberry Pi 4 class arm64 device.
- [ ] Windows/Linux/macOS x64 builds; arm64 builds produced and smoke-tested.

**v1.0 - Release milestone**

- [ ] Compatibility matrix with documented results.
- [ ] Full Avalonia frontend (library, settings, input mapping, save states, screenshots, logs, about).
- [ ] CLI with `run`, `info`, `bench`, `state`, and `selftest` commands.
- [ ] Reproducible self-contained packages for all six RIDs.
- [ ] License/attribution review complete.
- [ ] No known crash bugs in the supported test set.

### 1.4 Non-goals (v1)

- Native/JIT CPU backends.
- Netplay.
- Rewind.
- CD-DA / Red Book audio.
- Multi-disc swapping.
- PAL video.
- High-resolution rendering (planned after v0.5; see M11).
- 3DO mouse, light gun, modem, or network peripherals.
- Bit-exact 4DO save-state compatibility.

---

## 2. Architecture summary

The full architecture is in [`docs/Architecture.md`](docs/Architecture.md). The short version:

```text
Frontends (Avalonia, CLI)
        │
        ▼
ThreeDo.Host (session, BIOS/disc/NVRAM, audio, input, save states, pacing)
        │
        ▼
ThreeDo.Machine (composition, frame loop, Quartz, state sections)
        │
        ├── ThreeDo.Cpu       ARM60 interpreter
        ├── ThreeDo.Memory    RAM/ROM/NVRAM, bus decode
        ├── ThreeDo.Madam     CEL engine, pixel pipeline, matrix engine
        ├── ThreeDo.Clio      timers, FIFOs, DMA, interrupts, XBUS/DSP windows
        ├── ThreeDo.Dsp       audio DSP
        ├── ThreeDo.Sport     VRAM fill/copy
        ├── ThreeDo.Video     VDLP, frame assembly, scalers
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
- **The core is single-threaded and deterministic.** The host owns pacing, audio, input, and UI threads.
- **Memory preserves FreeDOCore's word-swapped representation** for v1. See `docs/PortingNotes.md` §3.
- **Save states use a new explicit format**, not the native `memcpy` struct layout.
- **Video is software CEL rendering**, matching FreeDO. The frontend scales the 320x240 frame.

---

## 3. Reference-to-project map

| FreeDOCore file | C# project | Main C# types | Milestone |
|---|---|---|---|
| `_3do_sys.cpp` | `ThreeDo.Machine` | `ThreeDoMachine`, `FrameLoop`, `MachineOptions` | M3 |
| `arm.cpp`, `arm.h` | `ThreeDo.Cpu` | `Arm60`, `Arm60State`, `ArmDecoder`, `ArmAlu`, `ArmBarrelShifter`, `ArmDisassembler` | M2 |
| `Clio.cpp`, `Clio.h` | `ThreeDo.Clio` | `Clio`, `ClioRegisters`, `ClioTimers`, `ClioFifo`, `ClioDma`, `ClioInterrupts` | M3 |
| `Madam.cpp`, `Madam.h` | `ThreeDo.Madam` | `Madam`, `MadamRegisters`, `CelEngine`, `CelBitReader`, `PixelDecoder`, `PixelProcessor`, `Projector`, `CelRasterizer`, `MatrixEngine` | M4 |
| `vdlp.cpp`, `vdlp.h` | `ThreeDo.Video` | `Vdlp`, `VdlParser`, `VdlLine`, `VdlFrame`, `FrameAssembler` | M3/M4 |
| `DSP.cpp`, `DSP.h` | `ThreeDo.Dsp` | `Dsp`, `DspCore`, `DspInstructionDecoder`, `DspAlu`, `DspRegisters`, `DspMemory` | M5 |
| `SPORT.cpp`, `SPORT.h` | `ThreeDo.Sport` | `Sport` | M3 |
| `XBUS.cpp`, `XBUS.h`, `IsoXBUS.h` | `ThreeDo.Xbus` | `Xbus`, `XbusDevice`, `XbusFifo` | M6 |
| `Iso.cpp` | `ThreeDo.Cdrom` | `CdromDevice`, `CdromCommand`, `Toc`, `Msf` | M6 |
| `quarz.cpp`, `quarz.h` | `ThreeDo.Machine` | `Quartz`, `ClockDomain` | M3 |
| `DiagPort.cpp`, `DiagPort.h` | `ThreeDo.Clio` | `DiagPort` | M3 |
| `bitop.cpp`, `bitop.h` | `ThreeDo.Madam` | `CelBitReader` | M4 |
| `frame.cpp`, `frame.h` | `ThreeDo.Video` | `FrameAssembler`, `FrameBufferFactory` | M3/M4 |
| `Filters/hqx*` | `ThreeDo.Video` | `Hq2x`, `Hq3x`, `Hq4x` (optional) | M11 |
| `types.h`, `freedoconfig.h`, `freedocore.h` | `ThreeDo.Core` | constants, `FixMode`, `EmulatorTuning`, `VdlFrame`, `FrameBuffer` | M1 |
| `Worker.cpp` | - | not ported | - |

4DO host references:

| 4DO C# file | C# project | Main types |
|---|---|---|
| `Emulation/GameConsole.cs` | `ThreeDo.Host` | `EmulatorSession`, `FramePacer`, `AudioPipeline` |
| `Emulation/GameSource/*` | `ThreeDo.Storage`, `ThreeDo.Host` | `DiscImageFactory`, `GameLibraryService`, `GameDatabase` |
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
| M6 | XBUS + CD-ROM | Disc images and CD command state machine | Real disc boots to title screen | 10 dd |
| M7 | Input + NVRAM + fixes | Playable controls, persistence, game DB | Playable test set; NVRAM persists | 6 dd |
| M8 | Save states + diagnostics | Deterministic states, CLI tools, tracing | State roundtrip and CLI tests green | 10 dd |
| M9 | Avalonia frontend | Full desktop UI | Runs on win/linux/mac x64; 60 fps; fullscreen | 18 dd |
| M10 | Packaging + performance | Six RIDs, CI artifacts, optimization | All packages start; targets met | 6 dd |
| M11 | Compatibility + release | Real-game hardening, high-res, release | v1 checklist complete | 25+ dd |

**Total:** ~137 developer-days of focused work plus 30% contingency (~180 dd). This is a multi-month project for one experienced developer; correctness gates matter more than the estimate.

---

## 5. Detailed milestones

### M0 - Bootstrap and guardrails

**Goal:** a clean, buildable repository with the planned project graph, test harness, CI, and documentation.

**Tasks**

- [ ] **M0.1 Initialize the repository.**
  - `git init`, `.gitignore` for .NET/Avalonia/test artifacts.
  - `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`.
  - Solution `3DOEmulator.sln`.
- [ ] **M0.2 Create the production project skeleton.**
  - `src/ThreeDo.Core`, `Cpu`, `Memory`, `Madam`, `Clio`, `Dsp`, `Sport`, `Video`, `Xbus`, `Cdrom`, `Storage`, `Audio`, `Input`, `Diagnostics`, `Machine`, `Host`, `Cli`, `Frontend.Avalonia`.
  - Correct project references per `docs/Architecture.md`.
  - No placeholder public APIs that will be thrown away.
- [ ] **M0.3 Create the test and benchmark skeleton.**
  - `tests/ThreeDo.Tests` (xUnit).
  - `benchmarks/ThreeDo.Benchmarks` (BenchmarkDotNet).
  - Test utility folder: fake clock, fake memory, fake frame/audio/input sinks.
- [ ] **M0.4 Create the minimal CLI.**
  - `ThreeDo.Cli` prints version/usage and returns non-zero on unknown commands.
  - Wire `System.CommandLine` (or a small hand-written parser) with a `--version` command.
- [ ] **M0.5 Create the minimal Avalonia app.**
  - Window with title, version, and "No game loaded" placeholder.
  - `AvaloniaUseCompiledBindingsByDefault`, Fluent theme, Inter font.
  - No emulation logic yet.
- [ ] **M0.6 CI.**
  - GitHub Actions: build/test on Windows/Linux/macOS; `dotnet format --verify-no-changes`.
  - Publish matrix skeleton for six RIDs (may be `--no-restore` until code exists).
- [ ] **M0.7 Documentation.**
  - `README.md`, `docs/Architecture.md`, `docs/ImplementationPlan.md`, `docs/PortingNotes.md`, `docs/TestStrategy.md`, `docs/Compatibility.md`, `docs/Licensing.md`, `THIRD-PARTY-NOTICES.md`.
- [ ] **M0.8 Attribution and legal gate.**
  - Preserve FreeDO/4DO headers in ported files.
  - Decide project distribution model; record it in `docs/Licensing.md`.
  - Add About/credits placeholder.

**Deliverables**

- Buildable solution with 18 production projects, one test project, one benchmark project.
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

- [ ] **M1.1 Core primitives and constants.**
  - `EmulatorConstants`: clock rates, frame cycles, RAM/ROM/NVRAM sizes, address bases, sentinels.
  - `FixMode` flags, `EmulatorTuning`, `CompatibilityState`, `GameQuirks`.
  - `VdlLine`, `VdlFrame`, `FrameBuffer`, `PixelFormat`, `AudioSample`.
  - `EmulationException`, `InvalidStateException`.
- [ ] **M1.2 Core interfaces.**
  - `IAddressBus` (byte/halfword/word read/write).
  - `IRam` (raw RAM/VRAM access used by CEL/SPORT/VDLP).
  - `IInterruptSink`, `IFrameSink`, `IAudioSink`, `IPbusSource`, `IDiscSource`, `IXbusDevice`, `IVdlp`, `IMadamBus`.
  - `IStateReader`, `IStateWriter`, `IStateSection`.
  - `ILogSink`, `LogLevel`, `LogCategory`.
- [ ] **M1.3 Memory implementation.**
  - `ThreeDoMemory` with `uint[]` RAM (3 MB + high-res mirrors reserved), `uint[]` ROM (2 MB), `byte[]` NVRAM (32 KB).
  - Byte/halfword/word helpers matching FreeDOCore's word-swapped representation.
  - Unaligned reads/writes.
  - ROM loading from big-endian BIOS bytes.
  - NVRAM one-byte-per-word quirk.
  - `0xBADACCE5` sentinel.
- [ ] **M1.4 Bus/address decode.**
  - RAM, ROM, NVRAM, Diag, SPORT, MADAM, CLIO ranges in the same order as FreeDOCore.
  - ROM bank select hook.
  - `IAddressBus` implementation with device hooks.
  - No device references yet; use null/stub devices until M3.
- [ ] **M1.5 Quartz scheduler.**
  - `PushArmCycles`, `QueueDsp`, `QueueTimer`, `QueueVdl`, `VdCurrLine`, `VdHalfFrame`, `VdCurrOverline`.
  - 8.24 fixed-point arithmetic matching FreeDOCore.
  - Configurable `ArmClock` and constants for VDL/DSP/timer.
  - Save/load of accumulators.
- [ ] **M1.6 State primitives.**
  - `StateWriter`/`StateReader` with explicit little-endian primitives, bounds checks, section table, CRC32.
  - `StateSection` IDs and version constants.
  - No chip state yet.
- [ ] **M1.7 Diagnostics skeleton.**
  - `Log`, `ConsoleLogSink`, `InMemoryLogSink`, `NullLogSink`.
  - Category/level filtering; no allocations when disabled.
- [ ] **M1.8 Tests.**
  - Memory alignment/byte-order/NVRAM/decode tests.
  - Quartz fixed-point tests: 1 cycle, 15/16/17 cycles, 208333 cycles/frame, 60 frames -> 12.5M cycles.
  - State reader/writer roundtrip, truncation, CRC mismatch.
  - Logging filter tests.

**Deliverables**

- `ThreeDo.Core`, `ThreeDo.Memory`, `ThreeDo.Machine` (Quartz only), `ThreeDo.Diagnostics` compile with tests.
- A `TestMemory`/`TestBus` utility for later chip tests.

**Acceptance**

- [ ] Memory tests cover all alignments and the NVRAM quirk.
- [ ] Quartz produces exactly 60 VDL frames per 12.5M cycles (with the FreeDO event semantics).
- [ ] State primitives reject truncated/corrupt data cleanly.
- [ ] `dotnet test` green.

**Risks**

- The word-swapped representation is easy to get subtly wrong. Mitigate with a `MemoryByteOrderTests` table test that compares every byte of a known word against the C++ behavior.
- `clks << 24` overflow: document and assert the expected range; test with 1, 15, 16, 255.

---

### M2 - ARM60 CPU

**Goal:** a complete, faithful port of `arm.cpp`, validated instruction by instruction.

**Tasks**

- [ ] **M2.1 CPU state and reset.**
  - `Arm60State`: user registers, banked FIQ/IRQ/SVC/ABT/UND registers, SPSRs, CPSR, pending FIQ, ROM select, data-abort flag.
  - Reset: PC `0x03000000`, SVC mode, CPSR `0x13`, zero registers.
  - `IAddressBus` integration.
- [ ] **M2.2 ALU and barrel shifter.**
  - `ArmAlu` operations 0-31.
  - `ArmBarrelShifter` with `NSC`/`SC` variants, carry-out, shift types 0-4.
  - Condition code table and evaluation.
  - Flag helpers: N/Z/C/V, mode, I/F.
- [ ] **M2.3 Decoder skeleton.**
  - Instruction class order matching `_arm_Execute`: multiply, swap, ALU, undefined, single data transfer, block transfer, branch, SWI, coprocessor/undefined.
  - C# replacement for C++ fallthrough (ordered checks or `goto case`).
  - Condition check before execution.
- [ ] **M2.4 Data processing.**
  - Immediate and register operands, rotate, register-specified shift.
  - All 16 opcodes, S bit, PC destination, SPSR restore.
  - Carry-out for logical ops.
- [ ] **M2.5 Multiply and swap.**
  - MUL/MLA, S bit, cycle count based on `calcbits`.
  - SWP/SWPB, PC adjustment, unaligned word rotation.
- [ ] **M2.6 Single data transfer.**
  - LDR/STR byte/word, pre/post index, writeback, negative offset, shifted register.
  - PC-relative and PC destination.
  - Privileged/user-mode register selection.
- [ ] **M2.7 Block data transfer.**
  - LDM/STM IA/IB/DA/DB, writeback, user-mode bit, PC in list, empty list.
  - The `ldm_accur`/`stm_accur` timing quirks and `FIX_BIT_TIMING_1` value fixes.
- [ ] **M2.8 Branch, SWI, undefined, MRS/MSR.**
  - B/BL, SWI vector, undefined vector, coprocessor/undefined path.
  - MRS/MSR CPSR/SPSR, mode changes, banked register switching.
- [ ] **M2.9 FIQ and memory interface.**
  - FIQ entry after instruction when `!ISF && NeedFiq`.
  - `mreadb/mreadw/mwriteb/mwritew` decode order and sentinels.
  - `SelectROM`, raw RAM helpers.
  - `biosanvil` PC skip and `cnbfix` compatibility hacks.
- [ ] **M2.10 Cycle accounting.**
  - `NCYCLE=4`, `SCYCLE=1`, `ICYCLE=1`.
  - Multiply, swap, LDR/STR, LDM/STM, branch, SWI cycles.
  - `Step()` returns the same cycle count as `_arm_Execute`.
- [ ] **M2.11 Disassembler.**
  - Decode the implemented subset to readable ARM assembly.
  - Used by CLI trace and debug logs.
- [ ] **M2.12 Test assembler.**
  - `ArmTestAssembler` with helpers for data processing, loads/stores, branches, LDM/STM, SWI, MRS/MSR.
  - Test memory/bus with configurable mapping.
- [ ] **M2.13 Instruction test suite.**
  - One test class per instruction group (see `docs/TestStrategy.md`).
  - Condition-code matrix, shift matrix, flag matrix.
  - Banked-register/FIQ tests.
  - Cycle-count assertions.
- [ ] **M2.14 Synthetic boot program.**
  - A 1 MB synthetic ROM that computes a checksum, writes results to RAM, and loops.
  - Run 1M instructions; assert results and no unmapped accesses.
- [ ] **M2.15 Optional oracle trace.**
  - Developer-only harness to compare PC/registers/cycles with FreeDOCore for a small program.
  - Not part of the default test run.

**Deliverables**

- `ThreeDo.Cpu` with `Arm60`, state, ALU, shifter, decoder, disassembler.
- Instruction test suite with hundreds of cases.
- Synthetic boot program.

**Acceptance**

- [ ] Every implemented instruction has normal/boundary/flag tests.
- [ ] All 16 condition codes and all shift types are covered.
- [ ] Cycle counts match the reference for representative instructions.
- [ ] FIQ entry/exit and banked registers match the reference.
- [ ] Synthetic program runs 1M instructions without exception or out-of-range PC.
- [ ] `dotnet test` green; no per-instruction allocations.

**Risks**

- C++ switch fallthrough is the most likely source of decode bugs. Mitigate with a decode-order test table and a disassembler cross-check.
- Banked register switching is subtle. Mitigate with mode-transition tests that snapshot all banks.
- `int`/`uint` signedness in address arithmetic. Mitigate with explicit casts and tests at `0x7FFFFFFF`/`0x80000000` boundaries.

---

### M3 - Boot skeleton: BIOS, CLIO, VDLP, SPORT, frame loop

**Goal:** load a BIOS, execute it, service basic CLIO/MADAM/VDLP/SPORT behavior, and produce frames. Real BIOS may not fully boot until M4/M5/M6; this milestone proves the machine loop.

**Tasks**

- [ ] **M3.1 BIOS/NVRAM loading and reset.**
  - `BiosImage` (1 MB, optional 1 MB second, MD5, Anvil detection).
  - `NvramImage` (32 KB, volume header initialization).
  - `ThreeDoMachine.Reset()` order: ARM, ROM swap, VDLP, SPORT, MADAM, XBUS + CD reset, CLIO `0x40`, DSP, frame, diag, Quartz.
- [ ] **M3.2 CLIO basic register file.**
  - `uint[65536]` registers.
  - Defaults: `0x28=0x40`, `0x400=0x80`, `0x220=64`.
  - Interrupt registers and `NeedFiq`.
  - ROM select `0x84`.
  - `0x00` version read `0x02020000`.
- [ ] **M3.3 CLIO timers.**
  - 16 timers, control/reload/cascade/decrement.
  - `DoTimers`, FIQ generation for odd timers.
  - `0x120` timer-dependent write, `0x220` delay.
- [ ] **M3.4 CLIO FIFOs and DMA skeleton.**
  - 13 input/4 output FIFOs; start/next/length; status.
  - XBUS DMA path with a stub XBUS device (returns zeros).
  - FIQ 29 on DMA complete.
- [ ] **M3.5 MADAM register file and basic pokes.**
  - 2048+64 registers.
  - `0x04=0x29`, `0x00=0x01020000`, `0x574=0xFFFFFFFC`.
  - CEL status/FSM.
  - VDL head write hook to VDLP.
  - Matrix engine can be stubbed until M4; return previous results.
- [ ] **M3.6 VDLP startup and line parser.**
  - `Vdlp` state and startup VDL at `0xB0000 + 2 MB`.
  - `ProcessVdl`, `DoLine`, CLUT/background/out-control parsing.
  - `VdlFrame` line writes.
  - 320x240 output.
- [ ] **M3.7 SPORT.**
  - Color/fill/copy/masked operations.
  - 512-word page operations.
- [ ] **M3.8 DiagPort.**
  - Shift registers and `_diag_Init(-1)`.
- [ ] **M3.9 Quartz integration and frame loop.**
  - `ThreeDoMachine.RunFrame()` and `InternalFrame`.
  - VDL line 16 trigger, V0/V1 FIQ, PBUS copy, frame swap.
  - Single-threaded mode first; `skipFrame` path stub.
- [ ] **M3.10 Frame assembler.**
  - Convert `VdlFrame` to BGRA `FrameBuffer`.
  - Background/fixed CLUT/per-line CLUT.
  - Crop/alpha optional.
- [ ] **M3.11 Synthetic boot integration test.**
  - Tiny ARM program writes a VDL, enables SPORT/VDLP, draws a background, loops.
  - Assert frame dimensions, non-black pixels, stable frame hash.
- [ ] **M3.12 Real BIOS smoke test (optional).**
  - Run 300 frames with a user-provided BIOS.
  - Assert no exception, PC not stuck in a tight invalid loop, frame hash recorded.
  - Skip if `THREEDO_BIOS` is unset.

**Deliverables**

- `ThreeDo.Machine` can construct/reset/run frames.
- BIOS + NVRAM + VDLP + SPORT + CLIO + basic MADAM.
- Synthetic frame output.

**Acceptance**

- [ ] Synthetic program produces the expected background/frame.
- [ ] Real BIOS executes 300 frames without crash (if provided).
- [ ] Timer/FIFO/DMA unit tests green.
- [ ] Frame loop cycle count matches `12_500_000 / 60` per frame.
- [ ] No per-frame allocations.

**Risks**

- Real BIOS may wait on CD/DSP/CEL features not yet implemented. Treat "executes 300 frames" as the gate; full boot is M4/M5/M6.
- VDLP line timing is easy to off-by-one. Mitigate with line-by-line tests at 0, 16, 239, 240, 525.

---

### M4 - MADAM graphics: CEL engine, pixel pipeline, matrix engine

**Goal:** render real 3DO graphics: packed/literal/LR CELs, PLUT, pixel processing, projection, clipping, matrix transforms, PBUS DMA.

**Tasks**

- [ ] **M4.1 CEL bit reader.**
  - Port `BitReaderBig` (`bitop.cpp`) exactly.
  - Tests for bit rates 1-32, positions, skip, end-of-buffer.
- [ ] **M4.2 CCB parser.**
  - `CCBFLAGS` constants, `NEXTCCB`, `PDATA`, `PLUTDATA`, `PRE0`, `PRE1`.
  - Absolute/relative pointers, `CCB_LAST`, `CCB_SKIP`, `CCB_PACKED`, `CCB_LDPLUT`, etc.
  - FSM transitions.
- [ ] **M4.3 PLUT.**
  - `LoadPLUT` address math, 2/4/16/32 entries.
  - Palette write bit handling.
- [ ] **M4.4 Pixel decoder.**
  - 1/2/4/6/8/16 bpp.
  - Linear/uncoded vs PLUT-coded.
  - 16-bit palette-write bit.
  - AMV computation.
- [ ] **M4.5 Pixel processor and projector.**
  - `PPROC`: MS/MF/SF/2S/AV/2D, PXOR, NOBLK, clipping.
  - `PPROJ_OUTPUT`: PLUT position, SWAPHV, CFBDSUB (disabled), B15/B0 modes.
  - Exhaustive combination tests where feasible.
- [ ] **M4.6 CEL geometry and clipping.**
  - `TestInitVisual`, `Init_Line_Map`, `Init_Scale_Map`, `Init_Arbitrary_Map`.
  - `QuardCCWTest`, `TexelCCWTest`.
  - `XY2OFF`.
- [ ] **M4.7 Packed CEL rasterizer.**
  - `DrawPackedCel_New`, all three texel paths.
  - `speedfixes`/`unknownflag11`/`sdf`/`sf` compatibility state.
- [ ] **M4.8 Literal CEL rasterizer.**
  - `DrawLiteralCel_New`, all three paths.
  - `FIX_BIT_TIMING_6`.
- [ ] **M4.9 LR CEL rasterizer.**
  - `DrawLRCel_New`, all three paths.
- [ ] **M4.10 Texel draw primitives.**
  - `TexelDraw_Line`, `TexelDraw_Scale`, `TexelDraw_Arbitrary`.
  - `FIX_BIT_TIMING_3`, `FIX_BIT_GRAPHICS_STEP_Y`.
- [ ] **M4.11 Matrix engine.**
  - Commands 0-3, 16.16 math, Nfrac, division-by-zero fallback.
  - Register defaults and result latching.
- [ ] **M4.12 DMA/PBUS.**
  - `HandleDMA8`, `DMAPBus`, `KeyPressed`.
  - FIQ generation.
- [ ] **M4.13 CEL cycle accounting.**
  - `mread`/`mwrite`/`mreadh`/`mwriteh` CEL cycles.
  - `GetCELCycles` behavior.
- [ ] **M4.14 Golden CEL tests.**
  - Hand-built CCB scenes for each bpp/path.
  - VRAM pixel hashes, CEL cycle counts.
  - A composite scene with multiple CCBs, PLUT changes, clipping, transparency.
- [ ] **M4.15 BIOS UI validation.**
  - Real BIOS menu renders correctly (user-provided).
  - Compare against 4DO screenshots for layout/colors (manual).
- [ ] **M4.16 High-res decision.**
  - Keep `RESSCALE=0` in v1. Document the later feature work in M11.

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

- [ ] **M5.1 DSP memory and registers.**
  - `NMem[2048]`, `IMem[1024]`, register file, `REGCONV`, `RBASEx4`, `REGi`.
  - `DSPPCNT`/`DSPPRLD` 567, `AUDCNT`, semaphore status/data, noise.
- [ ] **M5.2 Instruction decoder/precompute.**
  - Port `_dsp_Init`: `INSTTRAS[0x8000]`, request flags, `BS`, `REGCONV`, `BRCONDTAB`.
  - Tests for every request flag combination.
- [ ] **M5.3 ALU and multiplier.**
  - ALU ops 0-15, flags, exact, 32-bit mask.
  - MULT1/MULT2 paths, ACSBU, M2SEL.
- [ ] **M5.4 Barrel shifter.**
  - Cases 0-31, arithmetic/logical, CLIP, carry.
- [ ] **M5.5 Operand loader.**
  - `OperandLoader`, `OperandLoaderNWB`, NMem/IMem/register windows, DI, NUMOPS.
- [ ] **M5.6 Control flow.**
  - NOP, branch accum, set rbase/rmap, RTS, set op_mask, SLEEP, jumps, JSR, conditional branches.
  - Branch condition table.
- [ ] **M5.7 Memory windows and semaphore.**
  - `ireadh`/`iwriteh`, EI/I, `_dsp_WriteIMem`, `_dsp_ReadIMem`.
  - ARM/DSP semaphore write/read/ACK.
- [ ] **M5.8 Reset/running/PRNG.**
  - `_dsp_Reset`, `SetRunning`, `fastrand`.
- [ ] **M5.9 Sample output and cadence.**
  - `(IMem[0x3FF] << 16) | IMem[0x3FE]`.
  - 567-cycle cadence.
  - Channel order verification.
- [ ] **M5.10 Audio pipeline.**
  - `AudioSample`, packed-to-stereo conversion.
  - `AudioRingBuffer` (SPSC, no allocations).
  - `NullAudioSink`, `WavAudioSink`.
  - Host `AudioPipeline` integration later.
- [ ] **M5.11 Tests.**
  - Instruction decode table, ALU/shifter/operand/branch tests.
  - Semaphore tests.
  - Sample stream golden test.
  - A tiny DSP program that generates a sine/square wave; WAV dump for manual check.
- [ ] **M5.12 BIOS audio smoke.**
  - Run BIOS with DSP enabled; assert samples are produced and no underrun crash.
  - If the BIOS menu has sound, verify manually.

**Deliverables**

- `ThreeDo.Dsp` with full core.
- `ThreeDo.Audio` ring buffer and sinks.
- DSP golden tests.

**Acceptance**

- [ ] Decoder/ALU/shifter/branch tests green.
- [ ] Sample cadence and packed format match the reference.
- [ ] WAV output is audible and correctly pitched.
- [ ] No allocations per sample/frame.
- [ ] BIOS produces audio (if applicable).

**Risks**

- The DSP is bit-level and obscure. The precomputed branch table is easy to get wrong; test all 32 branch codes x 32 flag/exact combinations.
- Sample channel order is not obvious from the C++ (host writes the packed uint directly to DirectSound). Verify with a known WAV and document.

---

### M6 - XBUS, CD-ROM, and disc boot

**Goal:** read real disc images through the emulated CD-ROM and boot a game.

**Tasks**

- [ ] **M6.1 Disc abstraction.**
  - `IDiscSource` with `SectorCount` and `ReadSector(int lba, Span<byte>)`.
  - `IsoImage` (2048), `BinImage` (2352 + 0x10), `CueSheet` + factory.
  - Format detection by extension/size.
- [ ] **M6.2 Disc metadata.**
  - Volume header parsing (blockCount at offset 80, big-endian).
  - Sector count fallback.
  - Game ID: MD5 of first two sectors.
  - Game database lookup and unknown-ID fallback.
- [ ] **M6.3 XBUS controller.**
  - 16 slots, device 0 CD, device 0xF built-in responder.
  - Select/poll/command/data FIFO, status.
  - FIQ generation.
  - Save/load state section.
- [ ] **M6.4 CD device state machine.**
  - `cdrom_Device` fields and reset/`InitCD`.
  - Commands 0x01, 0x02, 0x03, 0x04, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x10, 0x80, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x93.
  - Exact status/data lengths and byte layouts.
  - Sector streaming and `OnSector`/`Read2048` ordering.
- [ ] **M6.5 MSF/LBA/TOC.**
  - BCD helpers, MSF2LBA, LBA2MSF, MSF2BLK, BLK2MSF.
  - TOC entry for track 1, disc info, session info.
- [ ] **M6.6 CLIO DMA integration.**
  - XBUS DMA to RAM, 4-byte groups, FIQ 29.
  - `len` sentinel and data FIFO draining.
- [ ] **M6.7 BIOS + disc boot.**
  - Boot a real disc to the title screen (user-provided).
  - Record frame hashes and logs.
- [ ] **M6.8 Tests.**
  - Command transcripts for every command.
  - ISO/BIN/CUE fixtures.
  - Multi-sector read test.
  - DMA-to-RAM test.
  - Save/load CD state.

**Deliverables**

- `ThreeDo.Xbus`, `ThreeDo.Cdrom`, `ThreeDo.Storage`.
- Real disc boot path.

**Acceptance**

- [ ] CD command transcripts match expected bytes.
- [ ] ISO and BIN sector reads return identical payloads for the same disc.
- [ ] BIOS boots a real disc to the title screen.
- [ ] At least one game reaches gameplay (manual).
- [ ] CD state save/load works.

**Risks**

- The CD command state machine is full of undocumented status layouts. Use the C++ as the specification and transcript tests.
- Some games may rely on seek/spin timing. FreeDO's model is approximate; document compatibility.
- CUE parsing edge cases (pregap, multiple files) can wait; support single-track MODE1 first.

---

### M7 - Input, NVRAM, game database, compatibility fixes

**Goal:** make the emulator playable and persistent.

**Tasks**

- [ ] **M7.1 Controller state and PBUS encoder.**
  - `ThreeDoController` buttons: A/B/C/P/X/L/R, Up/Down/Left/Right.
  - `PbusEncoder` 16-byte packet, 1-6 controllers, device order 1,3,2,5,4,6.
  - Tests for every button and combination.
- [ ] **M7.2 Input profiles.**
  - Keyboard bindings, gamepad bindings, console hotkeys.
  - JSON serialization, defaults, conflict detection.
  - 1-6 player profiles.
- [ ] **M7.3 Keyboard backend.**
  - Avalonia key events -> input state.
  - Focus handling (pause input when unfocused).
- [ ] **M7.4 Gamepad backend.**
  - `IGamepadBackend` abstraction.
  - Evaluate Avalonia 12 built-in gamepad support; fall back to Silk.NET.Input/SDL2.
  - Hot-plug, axis deadzone, D-pad/analog mapping.
- [ ] **M7.5 NVRAM persistence.**
  - Global `NVRAM_SaveData.ram`.
  - Debounced atomic write (250 ms), flush on pause/stop.
  - Fresh-image initialization.
- [ ] **M7.6 Game database and fix modes.**
  - Port 4DO game database with attribution.
  - MD5 -> ID, name, fix flags.
  - Per-game overrides in settings.
  - Unknown-ID collision avoidance.
- [ ] **M7.7 BIOS detection and Anvil.**
  - `byte[28] == 234` -> `SetAnvilFix(30)`.
  - Expose detected BIOS type in UI/CLI.
- [ ] **M7.8 Tests.**
  - PBUS bytes, NVRAM roundtrip, game DB lookup, fix-mode mapping.
  - Input profile roundtrip.

**Deliverables**

- Playable input.
- Persistent NVRAM.
- Game-specific fixes.

**Acceptance**

- [ ] Keyboard and gamepad control a real game.
- [ ] NVRAM survives restart and is written atomically.
- [ ] Known games with fix flags boot/behave correctly.
- [ ] PBUS tests match 4DO's JohnnyInput output.

**Risks**

- Cross-platform gamepad APIs are fragmented. Keep the backend behind an interface; ship keyboard first if gamepad slips.
- Game database licensing/attribution. Port only with attribution and legal review.

---

### M8 - Save states, diagnostics, CLI

**Goal:** deterministic save/load, debugging tools, and a useful headless CLI.

**Tasks**

- [ ] **M8.1 State format v1.**
  - Magic `TDO1`, version, header, section table, CRC32.
  - Explicit little-endian writer/reader.
  - BIOS hash, disc hash, emulator version, tuning flags.
  - Reject unknown versions; warn on hash mismatch.
- [ ] **M8.2 Per-chip state sections.**
  - CPU, memory (RAM/ROM/NVRAM), MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD, Quartz.
  - Rebuild derived tables on load (DSP precompute, high-res mirrors).
- [ ] **M8.3 Machine save/load.**
  - `ThreeDoMachine.SaveState(Stream)`, `LoadState(Stream)`.
  - Atomic file writes; slots.
- [ ] **M8.4 State tests.**
  - Save -> run 60 frames -> load -> run 60 frames; compare frame/state hashes to a control run.
  - Corrupt/truncated state rejection.
  - Hash mismatch policy.
- [ ] **M8.5 Diagnostics.**
  - CPU tracer (PC/CPSR/registers/cycles), disassembler integration.
  - Memory dumper, VRAM dumper, frame dumper (BMP/PNG via host).
  - Performance counters: frames/sec, CPU MIPS, CEL pixels/frame, DSP samples/frame, allocations.
  - Log categories and file/console sinks.
- [ ] **M8.6 CLI commands.**
  - `run <disc> --bios ... [--frames N] [--state-in/out] [--frame-out] [--audio null|wav] [--trace]`.
  - `info <disc>` (game ID, name, sector count, format).
  - `bios info <path>` (size, MD5, Anvil).
  - `state inspect <path>`.
  - `bench`.
  - `selftest`.
  - Exit codes and machine-readable output (`--json`).
- [ ] **M8.7 CLI tests.**
  - Argument parsing, error messages, exit codes.
  - Headless run with synthetic ROM; frame dump hash.
  - State roundtrip via CLI.

**Deliverables**

- Versioned save states.
- Diagnostics/tracing.
- Full CLI.

**Acceptance**

- [ ] Save/load roundtrip is deterministic across process restarts.
- [ ] Corrupt states fail cleanly.
- [ ] CLI runs a synthetic disc/ROM and dumps frames/states.
- [ ] `bench` produces stable numbers.
- [ ] No `BinaryFormatter`/`Marshal` state serialization.

**Risks**

- State completeness: missing a field causes subtle desync. Mitigate with a "state hash" that includes all emulated state and a roundtrip test that runs frames after load.
- Derived state (DSP tables, VRAM mirrors) must be rebuilt consistently.

---

### M9 - Avalonia frontend

**Goal:** a polished cross-platform desktop UI.

**Tasks**

- [ ] **M9.1 Shell and navigation.**
  - Main window, Home, Library, Settings, Logs, About.
  - Dark/light theme, Fluent, Inter.
  - Status bar: FPS, game, BIOS, audio/video state.
- [ ] **M9.2 Video surface.**
  - `VideoSurface` control with `WriteableBitmap` (BGRA8888).
  - Aspect ratio, integer scaling, letterbox, nearest/bilinear.
  - Fullscreen, borderless, cursor hide, escape handling.
  - Frame mailbox: latest-frame wins; drop stale frames.
  - FPS overlay toggle.
- [ ] **M9.3 BIOS setup.**
  - First-run wizard: BIOS1 required, BIOS2 optional.
  - File picker, validation, MD5/Anvil display.
  - Remember path; error dialogs.
- [ ] **M9.4 Game library.**
  - Scan folders for `.iso`, `.bin`, `.cue`, later `.chd`.
  - Game ID/name from database; recent games; cover art optional.
  - Open disc, eject, refresh.
- [ ] **M9.5 Settings.**
  - Video: scale/filter/fullscreen/vsync.
  - Audio: device, buffer, volume/mute.
  - Input: keyboard/gamepad mapping per player, hotkeys.
  - Emulation: ARM clock, fix mode, high-res (disabled in v1), save slots.
  - Paths: BIOS, saves, screenshots, library.
  - Per-game overrides.
- [ ] **M9.6 Input UI.**
  - Binding capture dialog, conflict detection, reset defaults.
  - Controller preview (optional).
- [ ] **M9.7 Audio backend.**
  - `OpenAlAudioOutput` via Silk.NET.OpenAL (or chosen backend).
  - Device selection, buffer sizing, underrun handling.
  - Null fallback if no device.
- [ ] **M9.8 Save states/screenshots.**
  - Quick save/load, slot next/previous, slot manager.
  - Screenshot to PNG with game/time name.
  - State metadata (game, BIOS, timestamp, thumbnail optional).
- [ ] **M9.9 Logs/About.**
  - In-app log viewer with category filter.
  - About: version, credits, licenses, links.
- [ ] **M9.10 ViewModel tests.**
  - Settings roundtrip, library scanning, frame mailbox, input mapping.
  - No UI thread dependency for logic.

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

### M10 - Packaging, CI, performance, arm64

**Goal:** shippable, reproducible builds for all target platforms.

**Tasks**

- [ ] **M10.1 Publish profiles.**
  - `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.
  - Self-contained, single-file, `PublishTrimmed=false` initially.
  - Include native audio/gamepad assets per RID.
- [ ] **M10.2 CI matrix.**
  - Build/test on Windows/Linux/macOS.
  - Publish all RIDs; upload artifacts.
  - Cache NuGet; deterministic builds.
- [ ] **M10.3 Trimming/AOT evaluation.**
  - JSON source generation, no reflection.
  - Evaluate `PublishTrimmed`/NativeAOT per project; do not break Avalonia.
- [ ] **M10.4 Icons/metadata.**
  - App icon, file associations (`.iso`, `.bin`, `.cue`, `.tdos`), version info.
- [ ] **M10.5 Performance.**
  - Benchmark CPU, CEL, DSP, frame loop.
  - Optimize hot paths: memory helpers, CEL pixel loops, DSP operand loader.
  - Measure allocations; target zero/frame.
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
- [ ] Tests green on CI.
- [ ] Performance targets met or documented.
- [ ] No native emulator code in any package.

---

### M11 - Compatibility hardening and release

**Goal:** real-game compatibility, high-res, optional filters, release.

**Tasks**

- [ ] **M11.1 Compatibility corpus.**
  - Test the matrix in `docs/Compatibility.md`.
  - Triage crashes/graphics/audio bugs.
  - Add per-game fixes only with evidence.
- [ ] **M11.2 High-resolution mode.**
  - `RESSCALE=1`: VRAM mirroring, VDLP line duplication, SPORT copies, CEL stepping, frame extraction.
  - Golden tests and real-game validation.
- [ ] **M11.3 Optional HQ2X/3X/4X.**
  - Port filters or implement shader-based scaling.
  - Keep core accuracy path unaffected.
- [ ] **M11.4 CD timing/seek improvements.**
  - Only if real games require it; keep transcript tests.
- [ ] **M11.5 Optional CHD.**
  - Managed CHD reader or dependency; verify license.
- [ ] **M11.6 Documentation/release.**
  - User guide, compatibility list, known issues, changelog.
  - License/attribution review.
  - Release notes.
- [ ] **M11.7 Post-release backlog.**
  - Netplay, rewind, debugger UI, CD-DA, PAL, additional peripherals.

**Acceptance**

- [ ] v1.0 checklist in §1.3 complete.
- [ ] No known crash bugs in the supported test set.
- [ ] Release artifacts and docs published.

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

- Measure before optimizing.
- No per-frame allocations in steady state.
- Prefer `Span<T>`, `Unsafe`, and reusable buffers in hot paths.
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
| R7 | Avalonia API churn / UI performance | Medium | Medium | Pin versions; latest-frame mailbox; CPU blit for 320x240 |
| R8 | Save state incompleteness | Medium | High | State hash; roundtrip tests; section-by-section ownership |
| R9 | Licensing restrictions block distribution | Medium | High | Early legal review; non-commercial assumption; attribution |
| R10 | No redistributable BIOS/disc for CI | High | Medium | Synthetic ROM/disc fixtures; opt-in real-content tests |
| R11 | Scope creep (netplay, rewind, shaders) | High | Medium | Non-goals list; milestone gates; defer to M11+ |
| R12 | Performance on arm64 | Low/Medium | Medium | Benchmark early; avoid allocations; profile CEL/DSP |
| R13 | Floating-point differences x64/arm64 | Low | Medium | Integer timing; golden frame hashes; replace hot float math if needed |
| R14 | Native audio/gamepad packaging complexity | Medium | Medium | Isolate per RID; ship null fallback; document optional backends |

---

## 9. Quality gates / definition of done

A milestone is done only when:

- [ ] All tasks are checked or explicitly deferred with a reason.
- [ ] Unit tests cover new behavior.
- [ ] At least one integration test exercises the milestone through the machine.
- [ ] Golden/differential tests pass where applicable.
- [ ] Save/load roundtrip is tested for new state.
- [ ] No per-frame allocations in steady state.
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
3. **Audio backend.** Default: OpenAL Soft via Silk.NET.OpenAL, with null/WAV fallbacks. Alternative: SDL2 audio.
4. **Gamepad backend.** Default: evaluate Avalonia 12 built-in; fall back to Silk.NET.Input/SDL2.
5. **4DO save-state compatibility.** Default: no; own versioned format. Legacy import can be added later.
6. **CHD support.** Default: later (M11), ISO/BIN/CUE first.
7. **PAL support.** Default: later; NTSC 60 Hz first.
8. **High-resolution mode.** Default: later (M11); v1 renders 320x240.
9. **Minimum OS versions.** Default: Windows 10+, macOS 12+, current Linux desktop.
10. **UI visual direction.** Default: clean dark/light Fluent, game library + settings, no skeuomorphic 3DO shell.
11. **Game database source/licensing.** Default: port 4DO data with attribution, pending review.
12. **Whether to ship an optional native audio binary.** Default: yes for desktop, but the core and CLI must work without it.

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
| M6 XBUS/CD | 10 dd | M3 (parallel with M4/M5) |
| M7 Input/NVRAM/fixes | 6 dd | M4/M6 |
| M8 Save states/CLI | 10 dd | M4/M5/M6 |
| M9 Avalonia | 18 dd | M8 |
| M10 Packaging/perf | 6 dd | M9 |
| M11 Compatibility/release | 25+ dd | M10 |
| **Subtotal** | **137 dd** | |
| Contingency (30%) | ~41 dd | |
| **Total** | **~178 dd** | |

Suggested parallelization with two developers:

- Dev A: CPU, memory, CLIO, machine, save states.
- Dev B: MADAM, VDLP, SPORT, DSP, CD.
- Both: tests, frontend, compatibility.
- Do not parallelize the same chip; keep one owner per subsystem.

---

## 12. Immediate next actions

1. Finish M0 scaffolding and commit.
2. Implement M1 memory + Quartz + state primitives.
3. Start M2 with the CPU state/ALU/shifter and the test assembler.
4. Create the first synthetic ROM integration test as soon as M2 data processing and loads/stores work.
5. Schedule a 30-minute legal/attribution review before any public build.

---

## 13. Progress log

| Date | Milestone | Task | Notes |
|---|---|---|---|
| 2026-10-23 | M0 | Plan/architecture written | Initial docs and scaffold |
| | | | |
