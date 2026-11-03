# Test Strategy

The emulator is a large, stateful port. Tests are the only way to keep a 600 KB C++ reference and a multi-thousand-line C# port in agreement. The strategy is deliberately layered: fast unit tests for every subsystem, deterministic integration tests for the machine, and optional oracle/golden tests for real software.

---

## 1. Test pyramid

```text
                    ┌─────────────────────────────┐
                    │  Compatibility / real discs │  manual + optional CI
                    └──────────────┬──────────────┘
                 ┌─────────────────▼─────────────────┐
                 │  Integration: machine, frames,    │  deterministic, no BIOS
                 │  save states, CLI, host services  │
                 └─────────────────┬─────────────────┘
          ┌────────────────────────▼────────────────────────┐
          │  Golden / differential: CPU traces, frames,     │  fixtures checked in
          │  DSP samples, CD command transcripts            │
          └────────────────────────┬────────────────────────┘
     ┌─────────────────────────────▼─────────────────────────────┐
     │  Unit: CPU instructions, memory, MADAM pixels, DSP ops,    │  fast, thousands
     │  CLIO timers/FIFOs, VDLP parsing, CD commands, storage      │
     └────────────────────────────────────────────────────────────┘
```

**Default `dotnet test` must run the unit + integration layers and finish in a few minutes.** Real-BIOS and real-disc tests are opt-in via environment variables. The current suite is **1014 tests** (verified 2026-10-31).

---

## 2. Test project layout

There is one xUnit project:

```text
tests/FiveDo.Tests/
  Core/
  Cpu/
  Memory/
  Madam/
  Clio/
  Dsp/
  Sport/
  Video/
  Xbus/
  Cdrom/
  Storage/
  Audio/
  Input/
  Tas/
  Machine/
  Host/
  Diagnostics/
  Frontend/
  Cli/
  TestUtilities/
```

`Diagnostics/`, `Frontend/`, and `Cli/` cover the host/tooling/frontend surfaces that
were added after M0. If the suite becomes slow or a subsystem needs a different runner,
split it into `FiveDo.<Subsystem>.Tests` projects later; the folders already mirror the
production projects.

---

## 3. Test data policy

- **No copyrighted BIOS ROMs or game discs in the repository.**
- Checked-in fixtures are synthetic: generated ARM programs, synthetic CD sector images, generated CHD images (via CHDSharp's encoder), hand-built VDL/CCB data, generated WAV expectations.
- Real-BIOS tests are enabled by environment variables:
  - `FIVEDO_BIOS` = path to a 1 MB BIOS ROM.
  - `FIVEDO_FRAME_DUMP` = directory for optional boot-frame PNGs.
  - `FIVEDO_GPU_TESTS=1` = run the real WGL/ANGLE GPU probe integration test.
  - `FIVEDO_AUDIO_TESTS=1` = open a real OpenAL device (use `ALSOFT_DRIVERS=null` on headless machines).
- `FIVEDO_DISC` is used by some release/corpus scripts, not by the default test suite.
- Tests that require missing data are skipped with a clear message, not failed.
- Golden frame hashes for real BIOS/disc content are stored only if the content is redistributable; otherwise they are local developer artifacts.

### 3.1 Local real-content corpus

The development machine has a legally obtained corpus documented in
[`TestCorpus.md`](TestCorpus.md):

- BIOS images: `bios/` (use `panafz1.bin` by default; Anvil/GoldStar variants for
  compatibility checks).
- Disc images: `J:\Panasonic 3DO` (589 CHDs) and
  `G:\MAME\MAME Software List CHDs\3do` (5 CHDs); older handoffs used
  `D:\Samples\3DO` and `E:\Panasonic 3DO`.

The corpus is local-only and must never be committed, packaged, or uploaded. Use it
for manual boot/gameplay/audio/input/save-state validation and for end-to-end
performance measurements. The full-collection loadability sweep is
`tools/compat-sweep.ps1`; see [`CompatibilitySweep.md`](CompatibilitySweep.md). CI
continues to use synthetic fixtures and skips real-content tests when the environment
variables are unset.

---

## 4. Unit tests by subsystem

### 4.1 Core

- `FrameBuffer` dimensions, stride, pixel format conversion.
- `VdlLine`/`VdlFrame` buffer reuse and clearing.
- `FixMode` flag composition and game-quirk mapping.
- `EmulatorTuning` defaults and validation.
- State reader/writer primitives: little-endian roundtrip, bounds checks, CRC.

### 4.2 Memory

- `Read8/16/32` and `Write8/16/32` at all alignments 0-3.
- Word-swapped byte order: write a word, read each byte; compare with a hand-computed big-endian expectation.
- ROM loading: big-endian BIOS word conversion; byte reads at all four lanes.
- RAM/VRAM mirroring when high-res is enabled (later).
- NVRAM one-byte-per-word quirk, 32 KB wrap, byte writes.
- Address decode order: RAM, MADAM, CLIO, SPORT, ROM, NVRAM, Diag.
- ROM bank select via CLIO `0x84`.
- Unmapped reads return `0xBADACCE5`.
- Byte/halfword writes to ROM are ignored.

### 4.3 CPU

Build a small `ArmTestAssembler` in `TestUtilities` so tests read like assembly, not hex.

Instruction groups:

- **Data processing:** AND, EOR, SUB, RSB, ADD, ADC, SBC, RSC, TST, TEQ, CMP, CMN, ORR, MOV, BIC, MVN; S bit set/clear; all 16 condition codes; immediate rotate; register shifts LSL/LSR/ASR/ROR/RRX; carry-out behavior.
- **Multiply:** MUL, MLA, signed/unsigned operands, S bit, cycle counts.
- **MRS/MSR:** CPSR/SPSR read/write, control bits, mode changes, user-mode masks.
- **Single data transfer:** LDR/STR byte/word, pre/post index, writeback, negative offset, shifted register offset, PC-relative, PC as destination.
- **Block transfer:** LDM/STM IA/IB/DA/DB, writeback, user-mode bit, PC in list, empty list, cycle counts.
- **Swap:** SWP/SWPB.
- **Branch:** B/BL, signed offset, link register.
- **SWI:** mode switch, SPSR save, PC vector.
- **Undefined:** vector and mode.
- **FIQ:** pending FIQ entry/exit, banked registers, CPSR I/F bits, entry cycle
  timestamp, matched request-to-vector latency, level-triggered re-entry, and return.
- **Memory interface:** `0xBADACCE5` reads, ignored writes, unaligned word behavior.
- **Cycle counts:** each instruction class returns the same count as FreeDOCore; vector
  entry itself charges no extra cycles (matching FreeDO and Opera).
- **Interrupt telemetry:** request/entry counters, coalescing, unmatched re-entry, and
  the CLI trace line shape.

CPU test harness:

```csharp
var memory = new FiveDoMemory();
var cpu = new Arm60(memory.Bus, tuning: EmulatorTuning.Default);
memory.Ram.Write32(0x1000, ArmTestAssembler.Mov(0, 1));
memory.Ram.Write32(0x1004, ArmTestAssembler.Nop());
cpu.ProgramCounter = 0x1000;
cpu.Step();
Assert.Equal(1u, cpu.ReadRegister(0));
```

Implemented CPU suites: `Arm60StateTests`, `ArmBarrelShifterTests`, `ArmConditionTests`, `ArmDataProcessingTests`, `ArmMultiplySwapTests`, `ArmMemoryTests`, `ArmBlockTransferTests`, `ArmControlFlowTests`, `ArmModeBankTests`, `ArmCycleTests`, `ArmInterruptLatencyTests`, `ArmDisassemblerTests`, and `ArmSyntheticBootTests`. The synthetic boot test builds a 1 MB ROM, runs one million instructions, and checks the computed checksum in RAM.

Implemented M3 suites: `ClioTests`, `MadamTests`, `SportTests`, `VdlpTests`, `FrameAssemblerTests`, `BiosImageTests`, `NvramImageTests`, and `FiveDoMachineTests`. The machine suite covers the synthetic VDL/SPORT program, frame determinism, the reference cycle budget, zero steady-state allocations, and an optional 300-frame real-BIOS smoke test gated by `FIVEDO_BIOS`.

Implemented M4 suites so far: `CelBitReaderTests`, `CcbParserTests`, `PixelDecoderTests`, `PixelProcessorTests`, `ProjectorTests`, `CelGeometryTests`, `CelMemoryTests`, and the PLUT-load/matrix/PBUS tests in `MadamTests`.

For each instruction, assert registers, flags, memory, and cycles. Where possible, also assert the disassembler text.

### 4.4 MADAM

- Register read/write defaults (`0x04=0x29`, `0x00=0x01020000`, `0x574=0xFFFFFFFC`).
- CEL status FSM: idle/suspended/in-process.
- Matrix engine commands 0-3, including division by zero and large/small values.
- `BitReaderBig`: bit rates 1-32, byte/bit positions, skip, end-of-buffer behavior.
- `PDEC` for 1/2/4/6/8/16 bpp, linear/coded, PLUT, palette write bit.
- `PPROC`: all `MS`, `MF`, `SF`, `2S`, `AV`, `2D`, `PXOR`, `NOBLK` combinations that can be enumerated; compare to hand-computed values.
- `PPROJ_OUTPUT`: PLUT position, SWAPHV, CFBDSUB (disabled in reference), B15/B0 modes.
- `TestInitVisual`: clipping and line/scale/arbitrary selection.
- `TexelDraw_Line`, `TexelDraw_Scale`, `TexelDraw_Arbitrary`: known coordinates, clipping, write masks.
- Packed/literal/LR CEL streams: hand-built CCB + preamble + data; assert VRAM pixels and CEL cycles.
- `LoadPLUT` address math.
- PBUS DMA and FIQ; FreeDO length/DEST/PROGRESS/byte-order behavior, MCTL gate reset and
  state round trip.
- `FIX_BIT_TIMING_3/5/6`, `FIX_BIT_GRAPHICS_STEP_Y` behavior.
- Golden VRAM hashes for a small set of CEL scenes.

### 4.5 CLIO

- Register read/write defaults and mirrors.
- Interrupt mask/active/high behavior; `NeedFiq`.
- Timer modes: decrement, reload, cascade, odd-only FIQ, stop on zero without reload.
- Timer reload values and cycle counts.
- FIFO input/output: start/next address, length, wrap, FIQ, status, and reload gated by
  the `0x304` channel request bit.
- DMA: source/length, XBUS data FIFO, word order, `len` sentinel, FIQ 29, DIPIR blocking,
  start/blocked/done trace lines.
- FIQ request transitions: active/enable intersection, repeated level raises, re-raise
  after clear, timer source, and cycle stamping.
- DSP windows: 2-word/1-word writes and reads at all four windows.
- ROM select.
- VCNT update.
- `0x120` timer-dependent write.
- XBUS select/poll/command/data register routing.

### 4.6 DSP

- `_dsp_Init` tables: `INSTTRAS` request flags, `BS`, `REGCONV`, `BRCONDTAB` for all 32 branch codes x 32 flag/exact combinations.
- ALU ops 0-15, carry/overflow flags, zero/negative/exact.
- Multiplier paths: `MULT1`, `MULT2`, `M2SEL`, signed 16-bit products, ACSBU.
- Barrel shifter cases 0-31.
- Operand loader: NMem, IMem, register windows, immediate, DI modes, NUMOPS.
- Branch conditions: all 32 codes, flags, exact.
- Control instructions: NOP, branch accum, set rbase, set rmap, RTS, set op_mask, SLEEP, jumps, JSR, conditional branches.
- Semaphore: ARM write/read, DSP ACK, status bits.
- Reset/running.
- PRNG sequence.
- Sample output: known IMem values -> packed uint; 567-cycle cadence.
- Per-tick metrics: running/sleeping loop counts, peak and mean instructions per tick.
- Golden DSP program trace: instruction-by-instruction PC/accumulator/flags.

### 4.7 SPORT

- Set color, fill 512 words, masked fill, page copy, masked copy.
- Source/destination index masking; unknown write offsets are ignored.
- High-res mirroring for full/masked fill and copy across all four planes.
- FreeDO XOR-mask semantics for zero/full masks (locked against the Opera alternative).
- Save/load state.

### 4.8 Video / VDLP

- Startup VDL is written at `0xB0000 + 2 MB`.
- VDL command parsing: CLUT entries, background, output control, fixed grayscale CLUT, unknown opcodes.
- `numword`, `lines`, `prevover`, `currover`, `prevtick`, `abs`, `enadma`, `modulo` fields.
- `DoLine` at line 0, line 16, line 239, line 240, line 525.
- Current/previous bitmap stepping with modulo values 320/384/512/640/1024.
- CLUT/control/background copied to the correct line.
- Frame assembler: background, fixed CLUT, per-line CLUT, crop, alpha, width/height.
- Golden BGRA hashes for synthetic VDL frames.
- High-res line duplication (later).

### 4.9 XBUS

- 16 device slots; attach/detach.
- Device 0xF built-in responder: command `0x83`, 12-byte status, poll behavior.
- FreeDO status/poll shape and Opera MEI/CD/OK status, pending status nibble,
  `POLREMASK`/`POLRE`, interrupt-enable storage, and poll FIQ.
- Select/poll/command/data register routing.
- FIQ generation when a device reports it.
- Device save/load section; missing attached-device slots are rejected.
- Unknown device returns zero/`0x30`.

### 4.10 CD-ROM

- Command transcript tests for every implemented command: input 7 bytes, expected status FIFO bytes, poll bits, data FIFO.
- `0x10` read data: MSF/LBA conversion, sector count, streaming across multiple sectors, `OnSector` ordering, FIQ.
- `0x83` read ID, `0x8B` disc info, `0x8C` TOC, `0x85` capacity, `0x87` subq, `0x8D` session.
- Eject/inject/spin up/down/error paths.
- MSF/LBA/BLK conversions, BCD helpers.
- `DataLen`/`Requested` edge cases (0, 1, 2048, multiple sectors).
- Save/load state.

### 4.11 Storage

- ISO 2048: sector count, read at 0/last/out-of-range.
- BIN 2352: offset `0x10`, 2048-byte payload, sector count.
- CUE parsing: `FILE`, `TRACK`, `INDEX 01`, `PREGAP`, MODE1/2048, MODE1/2352, unsupported tracks.
- **CHD**:
  - open a synthetic CD CHD generated by CHDSharp's encoder
  - Mode1/2048 and Mode1/2352 payload offsets
  - Mode2/Form1 payload offset
  - ISO/BIN/CUE/CHD sector-stream parity for the same disc
  - parent/child CHD opening
  - flat 2048-byte ISO CHD acceptance
  - non-3DO (DVD/HDD/raw with other unit sizes) and audio-only CHD rejection
  - invalid/missing/truncated CHD errors
  - read-ahead/cache behavior
- Extension/size/header auto-detection.
- BIOS validation: 1 MB, optional 1 MB second, short second ROM, bad size.
- BIOS hash and Anvil detection.
- NVRAM 32 KB read/write/atomic save.
- Game ID: MD5 of first two sectors; known synthetic fixture.
- Game database lookup and unknown-ID fallback.

### 4.12 Audio

- Ring buffer: write/read, wrap, underrun, overrun, single producer/single consumer.
- Sample conversion: packed uint -> left/right shorts; channel order.
- Exact DSP clock: 44,100 samples per ARM second across batch patterns; Quartz
  state-v1 conversion and v2 validation.
- WAV sink: header, sample count, byte order, buffered flushing, peak/RMS/clipping/
  silence/DC statistics.
- WAV analyzer: PCM mono/stereo, odd-sized chunk padding, truncation rejection.
- Live OpenAL: opt-in `FIVEDO_AUDIO_TESTS=1` 1 s tone test; reset/state-load flushes
  the ring and source queue.
- Frame pacing: `AudioFramePacer` 735/740/zero/implausible deltas, rolling average,
  and `CoreRunner` target-rate reporting.
- Null sink: no allocations, no exceptions.

### 4.13 Input

- PBUS encoding for every button, combinations, 1-6 controllers.
- PBUS decoding and button formatting for input display.
- Device order 1,3,2,5,4,6.
- Header bytes `0x00/0x48/0x80`.
- Keyboard mapping and gamepad mapping (fake backend).
- Profile serialization.

### 4.13a TAS movies

- `.5domovie` header/payload/state roundtrip, empty movies, atomic replacement.
- Corrupt CRC, unsupported version, trailing data, malformed lengths, and frame-count
  mismatches are rejected.
- Recorder packet limits and recording-source wrapping.
- Playback order, seek, step-back, finish behavior, and decoded controller display.
- Branch/trim/append clears completion evidence and preserves the starting state.
- Frame/PC/state verification, option comparison, and machine-level record/replay
  equality on a synthetic state.

### 4.14 Machine

- Construction/wiring with fake devices.
- Reset sequence: PC `0x03000000`, SVC mode, NVRAM loaded, devices initialized.
- Frame loop: synthetic CPU program; expected cycle count, DSP/timer/VDL event counts.
- VDL line events at V0/V1; PBUS copied; frame swapped.
- Multitask `skipFrame` path.
- Save/load roundtrip: state hash before/after.
- Determinism: run the same 120 frames twice; compare frame hashes and state hash.

### 4.15 Host

- `EmulatorSession` lifecycle: start/pause/resume/stop/reset on a dedicated core thread.
- Frame pacer with a fake clock: target 60 Hz, catch-up cap, no drift, no UI-thread `RunFrame`.
- Latest-frame mailbox: triple-buffer/seqlock behavior, visible-pixel copy, no per-frame delegate allocation.
- Status/FPS update throttling (4–10 Hz) and no per-frame string formatting.
- BIOS loader errors.
- Disc loader errors.
- NVRAM debounce/atomic write (fake file system or temp dir).
- Settings roundtrip, unknown fields, migration.
- Save-state slots, hash mismatch, corrupt file.
- CLI argument parsing and exit codes.

### 4.16 OpenGL renderer

- **Palette parity:** for synthetic `VdlFrame` scenes, the GPU "Accurate" output must hash identically to the CPU `FrameAssembler` output.
  - all bpp values (1/2/4/6/8/16)
  - per-line CLUT changes
  - background pixel 0
  - fixed CLUT flag (`xOUTCONTROLL & 0x02000000`) and pixel bit 15
  - multiple lines with different CLUTs
- **Upload correctness:** raw `R16UI` texture contents match `VdlLine.Pixels`; palette texture matches per-line CLUT; line-param texture matches background/out-control.
- **Filter tests:** screenshot hashes at fixed output sizes for nearest, sharp-bilinear, CRT, xBR, HQx 2x/3x/4x, and FSR modes. Filters are allowed to differ from the CPU path; Accurate mode is not. Shader-source tests cover the GLSL 330/ES 300 variants, CRT uniforms, and FSR EASU/RCAS bodies.
- **Geometry:** aspect ratio, integer scaling, letterbox, fullscreen, window resize.
- **Context lifecycle:** initialize, resize, context loss/recreate, dispose, fallback to CPU.
- **Shader compilation:** all shader programs compile/link; log source hash and GL version.
- **Performance:** two-stage palette expansion at native resolution, persistent upload, no per-frame `TexImage2D`/uniform lookup, upload/present CPU time, GPU frame time, 1080p/1440p/4K output, no CPU per-pixel scaling.
- **DPI/geometry:** physical-pixel sizing at 100/150/200% scaling, resize, integer scaling, letterbox. `PixelSize.Compute` is unit-tested for 100/125/150/200%, fractional bounds, and invalid inputs; on-device DPI/resize validation remains.
- **Backends:** desktop GL 3.3 and GLES 3.0/3.1 shader variants compile and produce identical Accurate-mode output. The `--gl-probe` tool validates this with `--gl-backend wgl` and `--gl-backend angle`; `--gl-probe-capture` writes the presented frame and the JSON `Parity` block compares it with `FrameAssembler` (WGL and ANGLE/GLES 3.0 both report 0 different pixels on the development RTX 4070). The opt-in `GpuProbeIntegrationTests` test (`FIVEDO_GPU_TESTS=1`) runs the real probe for both backends, checks parity, and runs a short 4K pass.
- **Render-on-demand:** no continuous rendering at monitor refresh rate when the emulator is producing 60 fps.
- **Headless CI:** run CPU-renderer tests everywhere; GPU tests run on a local/self-hosted job with a real GL context. Skip cleanly when no context is available.

### 4.17 Optional Vulkan renderer

- **SPIR-V:** generated `Frame.vert`, `Palette.frag`, `Scale.frag`, `Xbr.frag`,
  `FsrEasu.frag`, and `FsrRcas.frag` modules are non-empty SPIR-V with the correct
  magic number (`VulkanShaderTests`).
- **Palette parity:** 320x240 and 640x480 Accurate renders match `FrameAssembler`,
  including background pixel 0, the fixed CLUT flag/pixel bit 15, and per-line CLUTs
  (`VulkanRendererTests`, opt-in `FIVEDO_VULKAN_TESTS=1`).
- **Source/output changes:** changing the source size recreates the expanded target
  and rebinds the scale/xBR/FSR descriptors; changing the output size recreates the
  target, FSR target, and readback buffer.
- **Filter orientation:** a top-red/bottom-blue split frame rendered through xBR and
  FSR at 320x240 and 640x480 stays top-down (`VulkanRendererTests`).
- **Probe:** `--vk-probe` writes the shared JSON shape, supports PNG capture, and
  performs the 320x240 CPU/GPU parity check. `VulkanProbeIntegrationTests`
  (`FIVEDO_VULKAN_TESTS=1`) runs the built frontend for parity, a 4K pass, and
  640x480 xBR/FSR passes with `filterFallback: false`.
- **Filters:** xBR and FSR are GPU passes; HQx 2x/3x/4x use the shared CPU expansion
  and are CPU-bound. All three preserve top-down orientation and report
  `filterFallback: false` in the Vulkan backend.
- **Headless CI:** SPIR-V and option tests run everywhere; device tests skip cleanly
  without a Vulkan loader/device.

### 4.18 CPU backends / optional JIT

- Run the full instruction suite through `ICpuBackend` and compare interpreter vs JIT:
  - registers, CPSR, banked registers
  - memory writes
  - cycle counts
  - FIQ/mode transitions
- Block cache invalidation on self-modifying code.
- Fallback to interpreter on unsupported instructions.
- Determinism: same input sequence produces the same frame/state hashes.
- Performance gate: JIT must show a measured win and no accuracy regressions; otherwise it stays disabled.

---

## 5. Integration tests without a BIOS

The emulator can execute a synthetic ROM at the reset vector without a real BIOS:

1. Build a 1 MB ROM with a tiny ARM program at offset 0.
2. Program writes known values to RAM, MADAM registers, CLIO registers, SPORT, and a VDL.
3. Set PC `0x03000000` and run N frames.
4. Assert RAM values, device registers, frame output, and no exceptions.

This tests the CPU, bus, memory, CLIO, MADAM, VDLP, SPORT, and frame loop together. It is the primary CI integration test until a redistributable BIOS/test image exists.

### Synthetic ROM programs

- **Hello RAM:** MOV/STR loop; assert RAM.
- **Timer:** configure CLIO timer, wait for FIQ, assert handler ran.
- **VDL:** write a startup VDL, enable VDLP, assert frame lines/background.
- **SPORT:** fill/copy a VRAM page, assert pixels.
- **CEL:** a minimal CCB/preamble that draws a 16x16 packed sprite; assert VRAM.
- **DSP:** write a small DSP program to IMem, start it, assert samples/semaphore.
- **CD:** issue a read command through XBUS/CLIO, assert sector bytes in RAM.

---

## 6. Golden tests

Golden tests compare a deterministic artifact against a checked-in or locally generated expectation.

### 6.1 CPU traces

For a synthetic program, record:

```text
PC CPSR R0..R15 cycles
```

Run the C# interpreter and compare to a trace generated by the FreeDO oracle (developer-only) or a hand-verified expectation. Use the first divergence to localize bugs.

### 6.2 Frame hashes

For synthetic VDL/CEL scenes, hash the BGRA frame:

```text
SHA256(width, height, stride, pixels)
```

Store the expected hash in the test. If a rendering change is intentional, update the hash and record why in the commit.

### 6.3 DSP sample streams

Run a small DSP program for N samples; compare the packed sample stream or a hash. Also compare a WAV file for manual listening.

### 6.4 CD command transcripts

Record each command's status/data/poll bytes as a compact transcript. These are stable and catch accidental command-layout changes.

---

## 7. Differential / oracle testing

The most efficient way to validate a 600 KB port is to compare against a maintained
reference implementation.

- **Primary oracle (new work): Opera/libopera.** Use `tools/FiveDo.DiffHarness`
  with a manifest from `benchmarks/diff/`. It stages the bundled `OperaCore` and
  BIOS, runs both engines with the same options, and compares normalized frame
  hashes at checkpoints; reports include PC, audio, state, and CD fields when the
  engine exposes them. The manifest schema and allowlist policy are in
  [`DifferentialHarness.md`](DifferentialHarness.md); the manual procedure is in
  [`ReferenceStrategy.md`](ReferenceStrategy.md).
- **Historical oracle: FreeDOCore/4DO.** Keep the original C# port's golden tests and
  use the FreeDO harness for behavior that predates the Opera migration.
- **Optional, developer-only tool** (`tools/FiveDo.Oracle`, not shipped): load the
  original `FreeDOCore.dll` (Windows x64) or build the C++ reference, run a synthetic
  program/disc, and dump CPU/RAM/VRAM/frame hashes.
- Do not add a native dependency to the product or to the default test run.
- If an oracle is unavailable, use hand-verified traces and real-BIOS smoke tests.
- If Opera and FreeDO disagree, follow the hardware documentation when it is clear;
  otherwise prefer Opera for game compatibility and record the divergence in
  `docs/Compatibility.md` and `docs/PortingNotes.md`.

---

## 8. Fuzzing

The standalone [`FiveDo.Fuzz`](Fuzzing.md) harness covers the untrusted parsers and
the guest-driven CD command boundary:

- **Save states:** mutate a valid state and random bytes; require clean
  `InvalidStateException`/`InvalidDataException` rejections and no out-of-bounds reads.
- **Disc containers:** mutate ISO images and random CHD headers; require typed
  `DiscImageException`/`InvalidDataException` failures.
- **CUE:** mutate valid text and companion BIN data; require clean parser errors.
- **Opera file system:** mutate a synthetic valid volume; require bounded directory
  traversal, cycle/depth limits, and typed rejections.
- **CD:** random command/poll/tick/data streams in both FreeDO and Opera timing modes;
  require bounded status/data values and no unhandled exception.

The bounded xUnit smoke run is part of the default suite; the nightly workflow runs the
longer time-limited sweep and uploads any reproduction. CPU, CEL, DSP, and GPU fuzzing
remain future targets because their deterministic conformance and golden suites already
cover the instruction/rendering boundaries.

---

## 9. Performance tests

The full target, budgets, hardware tiers, and measurement method are in
[`Performance.md`](Performance.md). The stopwatch harness in
`benchmarks/FiveDo.Benchmarks` currently measures the machine core, video pack,
mailbox write, and read copy for a synthetic machine or a real BIOS/disc. It reports
JSON and a 60 Hz budget comparison. BenchmarkDotNet microbenchmarks are planned for:

- `Arm60.Step` on a mixed instruction stream (MIPS).
- `CelEngine.HandleCel` on representative CCB scenes (pixels/ms).
- `Dsp.Loop` (samples/second).
- `FiveDoMachine.RunFrame` on a synthetic scene (ms/frame, allocations/frame).
- `FrameAssembler` (MB/s).
- `AudioRingBuffer` (samples/second, allocation count).
- `VdlFrameUploader`/`OpenGlRenderer` upload/present (MB/s, CPU ms, GPU time).
- Output scaling cost at 1080p/1440p/4K (CPU should stay flat; GPU scales).

Targets (Tier A = mid-range x64; Tier B = integrated x64 GPU/older desktop CPU):

| Metric | Target |
|---|---|
| CPU interpreter | ≥ 100 MIPS on a modern x64 core; 3DO needs 12.5 MHz |
| Core frame time | ≤ 8 ms p99 on Tier A; ≤ 12 ms p99 on Tier B; independent of output resolution |
| Presenter CPU | ≤ 2 ms p99; zero allocations/frame |
| DSP | < 1 ms/frame |
| Output | 4K60 nearest/sharp-bilinear on Tier A; 1080p60 on Tier B via GLES |
| GPU upload | < 0.5 ms CPU/frame at 320x240 indexed (~175 KB) |
| Allocations | 0 bytes/frame in steady state (core and renderer) |
| Startup | < 1 s to first frame |
| Memory | < 150 MB resident (including GL textures) |

Current CPU-side baseline (Release, development machine, 600 frames):

| Case | Total | p50 | p95 | p99 | Allocations |
|---|---:|---:|---:|---:|---:|
| Synthetic | **1.250 ms/frame** | 1.218 ms | 1.434 ms | 1.638 ms | **0 bytes/frame** |
| Lemmings CHD | **1.281 ms/frame** | 1.088 ms | 2.005 ms | 3.398 ms | ~5.9 KB/frame (CHD) |
| Lemmings CHD, high-res | **1.605 ms/frame** | 1.398 ms | 2.497 ms | 4.598 ms | ~5.8 KB/frame (CHD) |

This is ~15.4 ms of headroom at 60 Hz for the synthetic case. GPU time, Tier B,
and the 4K pass are not measured yet; CHD decode allocations are host-side.

A performance change is accepted only with before/after numbers and identical golden output.

### 9.1 End-to-end harness

`benchmarks/FiveDo.Benchmarks` accepts `--bios`/`--disc` for a real title, plus
`--high-res` and `--prefetch`, and reports core, video pack, mailbox write, read
copy, p50/p95/p99/max frame time, per-stage allocations, instructions/s, and a
60 Hz headroom figure. The CLI `bench` command runs the synthetic machine. GPU time,
Tier B, and a CI regression gate are still pending. The synthetic benchmark remains
for CI, but it is not a substitute for real-content measurements.

### 9.2 CI gate

- Per-PR: build, unit/integration tests, format check, and a 120-frame synthetic
  benchmark artifact. Allocation assertions and CPU-renderer parity are still
  pending.
- Nightly/self-hosted: BenchmarkDotNet baselines, real-content smoke, 4K GPU timing.
- Fail on a sustained >10% regression or a missed budget after warm-up.
- Store the baseline JSON and hardware description with each run. The CI workflow
  uploads the synthetic JSON as `benchmark-baseline` but does not gate on it yet.

---

## 10. CI

- **Build:** Windows x64, Linux x64, macOS x64/arm64.
- **Test:** `dotnet test` on Windows x64 and Linux x64; macOS if runners are available.
- **GPU tests:** CPU-renderer parity tests run everywhere; real OpenGL context tests run on a self-hosted/local job or a dedicated GPU runner. Skip cleanly when no context is available.
- **Publish:** `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` (cross-publish where runners are unavailable).
- **Format:** `dotnet format --verify-no-changes`.
- **Analyzers:** warnings as errors.
- **Artifacts:** CLI + frontend archives per RID, including shaders and native audio assets.
- **Benchmark artifact:** Ubuntu CI runs a 120-frame synthetic benchmark and uploads
  `benchmark-baseline` (JSON). It is a trend artifact, not a timing gate.
- **Nightly/self-hosted:** performance baseline (p50/p95/p99, allocations, GPU time), fuzz, and real-BIOS/disc smoke on the local corpus. Performance gates are not run on noisy per-PR runners.

---

## 11. Definition of done for a ported subsystem

A subsystem is done when:

- [ ] All public behavior has unit tests.
- [ ] At least one integration test exercises it through the machine.
- [ ] Save/load roundtrip is tested (if it has state).
- [ ] Golden/differential tests pass where applicable.
- [ ] No per-frame allocations in the steady state.
- [ ] Performance budget met and recorded (frame time p99, allocations, hardware).
- [ ] No mutable static state.
- [ ] GPU Accurate mode matches the CPU frame assembler where applicable.
- [ ] CPU fallback works when no GL context is available.
- [ ] XML docs and traceability comments are present.
- [ ] The plan's acceptance criteria for that milestone are met.
