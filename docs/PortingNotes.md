# Porting Notes: Reference C++ to C#

This document is the working contract for the managed C# port. The original code was
ported from `References/4DO-master/FreeDOCore`; since 2026-10-23, new compatibility
work uses `References/opera-libretro-master/libopera` as the primary behavioral
reference. The representation choices and C++ constructs below still apply to both.

**Primary reference (new work):** `References/opera-libretro-master/libopera`
**Historical port baseline:** `References/4DO-master/FreeDOCore/FreeDO`
**Secondary/historical:** `References/freedo-master`
**Windows empirical harness:** `References/CPP_SimpleGui/Cores/OperaCore` (not product code)
**Hardware documentation:** `References/Docs3/arm60.pdf`, `References/Docs3/tc528267.pdf`, `References/Docs3/emu-tech-spec.html`, `References/Docs3/uncd-rom.zip`

See [`ReferenceStrategy.md`](ReferenceStrategy.md) for the reference hierarchy,
subsystem map, and differential test procedure.

---

## 1. Non-negotiable porting rules

1. **Preserve observable behavior first.** Do not "clean up" timing, register semantics, or pixel math during the initial port. Refactors come after differential/golden tests exist.
2. **No native emulator code.** The product must not P/Invoke `FreeDOCore.dll`, use C++/CLI, or load a native core. The original DLL may be used only by an optional, developer-only oracle harness outside the product.
3. **No mutable global state.** Every C++ global/static becomes a field on the owning class. One `FiveDoMachine` instance must be independent of another (tests run in parallel).
4. **No file I/O in the core.** BIOS, disc, NVRAM, and save-state bytes are supplied by the host.
5. **No wall clock in the core.** Time is advanced only by `Quartz.PushArmCycles`.
6. **Explicit serialization.** Never serialize a C# object with `Marshal`, `BinaryFormatter`, or field layout assumptions. Save states use explicit little-endian reads/writes.
7. **Comment traceability.** Every ported method starts with a comment naming the reference and function, such as `// FreeDO: Madam.cpp: _madam_HandleCEL` or `// Opera: opera_cdrom.c: cdrom_cmd_read_data`.
8. **One behavior per test.** Port a function, write its tests, then move on. Do not port a whole chip and test it only at the end.

---

## 2. Type mapping

| C++ | C# | Notes |
|---|---|---|
| `uint8`, `unsigned char` | `byte` | |
| `int8`, `char` | `sbyte` | Be careful: plain `char` signedness is compiler-dependent; FreeDO uses it for pixel components, where the values are small. Use `byte` for pixel data and `sbyte` only where signed math is intended. |
| `uint16`, `unsigned short` | `ushort` | |
| `int16`, `short` | `short` | |
| `uint32`, `unsigned int` | `uint` | |
| `int32`, `int` | `int` | |
| `uint64`, `unsigned long long` | `ulong` | |
| `int64`, `long long` | `long` | |
| `bool` | `bool` | C++ `bool` is 1 byte; C# `bool` is 1 byte in memory but do not depend on layout. |
| `float`, `real32` | `float` | |
| `double`, `real64` | `double` | |
| `void*` | typed reference, `Span<T>`, or index | No raw pointers in the product. |
| `unsigned char*` + length | `ReadOnlySpan<byte>` / `Span<byte>` | |
| function pointer callback | interface or delegate | Prefer interfaces for device wiring. |
| `__fastcall` | (ignored) | |
| `__int64` | `long` | |

Use `uint` for all emulated 32-bit registers and addresses unless the original explicitly uses signed arithmetic. Address arithmetic that wraps must be `unchecked`.

---

## 3. Memory model

FreeDOCore uses a byte array plus little-endian casts on x86. Its CPU-visible byte order is big-endian because byte accesses XOR the address with 3.

### 3.1 Representation chosen for the port

- RAM and ROM are `uint[]` words.
- A word value is exactly what FreeDOCore's little-endian C++ build sees in `pRam`/`pRom`.
- `Read8(offset)` returns byte lane `offset & 3` of the word.
- `Read16(offset)` returns the halfword at `offset & 3` (supporting unaligned offsets by composing bytes).
- `Write8`/`Write16` update the corresponding byte lanes.
- CPU `mreadb(addr)` calls `Read8(addr ^ 3)`.
- CPU `mreadw(addr)` aligns `addr &= ~3` and returns `Read32(addr)`.
- CPU `mwriteb(addr, value)` calls `Write8(addr ^ 3, value)`.
- CPU `mwritew(addr, value)` aligns and calls `Write32(addr, value)`.
- CEL `mreadh(addr)` calls `Read16(addr ^ 2)`; `mwriteh(addr, value)` calls `Write16(addr ^ 2, value)`.

This is intentionally the same representation as FreeDOCore. Do not replace it with a "clean" big-endian model during the first port.

### 3.2 ROM loading

FreeDOCore reads the BIOS bytes into `pRom`, then:

```cpp
for (int i = 2MB - 4; i >= 0; i -= 4)
    *(int*)(rom + i) = _bswap(*(int*)(rom + i));
```

The C# port does the equivalent:

```csharp
word[i] = BinaryPrimitives.ReadUInt32BigEndian(biosBytes.AsSpan(i * 4, 4));
```

This produces the same `uint` values that FreeDOCore's byte-swapped array produces on a little-endian host. Verify with the first BIOS instruction and a unit test.

### 3.3 NVRAM quirk

FreeDOCore's NVRAM word access is not a normal 32-bit access:

```cpp
// mreadw
val = (unsigned int)pNVRam[(index >> 2) & 32767];

// mwritew
pNVRam[(index >> 2) & 32767] = (unsigned char)val;
```

Each 32-bit word address advances the NVRAM byte index by one. Byte accesses also map through `(index >> 2) & 32767`. Preserve this exactly with a `byte[32768]` and the same index math. Do not "fix" it to a 32-bit NVRAM array without a compatibility test proving games are unaffected.

### 3.4 Unaligned access

FreeDOCore relies on x86 unaligned loads in a few places. The C# helpers must support unaligned `Read16`/`Read32` by composing bytes, even if most call sites are aligned. Add tests for every unaligned offset 0-3.

---

## 4. Bitfields, unions, and packed structs

FreeDOCore uses C bitfields extensively for:

- ARM instruction fields (in practice it mostly uses shifts/masks).
- MADAM pixel decoders (`pdeco`, `cp1btag`, `cp2btag`, `cp4btag`, `cp6btag`, `cp8btag`, `cp16btag`, `up8btag`, `up16btag`, `res16btag`).
- DSP instruction fields (`AIFTAG`, `CIFTAG`, `IOFTAG`, `NROFTAG`, `R2OFTAG`, `R3OFTAG`, `BRNTAG`, `BRNBITS`).
- VDL DMA word (`cdmaw`).
- CCB flags are plain `#define` masks.

### Rule

**Do not use C# bitfield emulation via `[StructLayout]` for emulated values.** Replace bitfields with explicit masks and shifts, and give them named properties/constants.

Example:

```csharp
// C++: struct cp6btag { unsigned short c:5; unsigned short pw:1; unsigned short pad:10; };
private const uint Cp6ColorMask = 0x001F;
private const uint Cp6PaletteWriteMask = 0x0020;

static uint Cp6Color(uint value) => value & Cp6ColorMask;
static uint Cp6PaletteWrite(uint value) => (value >> 5) & 1;
```

Use `[StructLayout(LayoutKind.Explicit)]` only for **file formats** whose byte layout is externally defined (e.g., a CUE/TOC structure if needed), never for runtime register/pixel state. Even then, prefer explicit readers.

### Packed structs

`#pragma pack(push,1)` is used for save-state structs and VDL frames. The C# port does not reproduce those structs. Save states use an explicit section format; VDL frames use arrays/classes.

---

## 5. Integer semantics

### 5.1 Wrapping

C# integer arithmetic wraps by default in unchecked contexts, but the project enables checked arithmetic? **No:** the project does not set `CheckForOverflowUnderflow`. Still, use `unchecked` explicitly around intentional wraparound so the intent is clear and future project settings cannot change behavior.

### 5.2 Signed right shift

C++ `>>` on a signed negative value is implementation-defined (arithmetic on MSVC/GCC). C# `>>` on `int` is arithmetic. This matches the compilers used by FreeDO. Keep `int` where the original uses signed shifts; do not switch to `uint` to avoid the sign.

### 5.3 `int` vs `uint` comparisons

C++ silently converts between signed and unsigned in comparisons, often producing surprising results. Port each expression deliberately:

- If the original relies on `(int)len <= DataLen` with `len` unsigned, keep the cast.
- If a loop is `while (len >= 0)` with `int len`, keep `int`.
- Add a comment when the signedness is a compatibility quirk.

### 5.4 Multiplication overflow

C++ signed overflow is undefined; unsigned overflow wraps. Use `unchecked` for unsigned wrapping. For signed math where the original could overflow, use `int` and `unchecked` to match the compiler's two's-complement behavior, and add a test.

### 5.5 Division by zero

C++ division by zero is undefined. FreeDOCore guards some cases (e.g., matrix engine `Rez2T == 0`) but not all. In C#, `int / 0` throws `DivideByZeroException`. Add guards that reproduce the original's fallback behavior, or log and return the same sentinel. Never let a guest program crash the emulator.

---

## 6. Floating point

FreeDOCore uses `double` in the MADAM matrix engine and `float`/`double` in the CEL geometry. The port keeps `double`/`float` for v1 to match the reference.

Rules:

- Use `Math`/`MathF` for the same operations; do not introduce `decimal`.
- Do not reorder floating-point expressions; IEEE results can change.
- Be careful with `(int)double` casts. C# conversion from an out-of-range `double` to `int` is unspecified; .NET typically saturates. The C++ original is undefined for out-of-range. Add a helper that documents the chosen behavior and test the matrix engine with large/small values.
- The scheduler and all timing are integer-only. Floating point is confined to MADAM math and frame scaling.
- Determinism across x64/arm64: basic IEEE 754 operations should match; transcendental functions are not used in the hot path. If a mismatch appears, replace the specific operation with integer math, not a global "fast math" switch.

---

## 7. Globals and statics

FreeDOCore has many file-scope globals:

- `curr_frame`, `scipframe`, `fixmode`, `biosanvil`, `isanvil`, `speedfixes`, `sf`, `sdf`, `unknownflag11`, `jw`, `cnbfix`, `HightResMode`, `__tex__scaler`, `ARM_CLOCK`, `TIMER_VAL`, `lsize`, `flagtime`, `pbus`, `Mem`, etc.

Port them as instance fields:

- Machine-wide tuning: `EmulatorTuning` (immutable options) plus `CompatibilityState` (mutable runtime flags such as `cnbfix`, `speedfixes`).
- Chip-specific mutable state: private fields on the chip class.
- Immutable lookup tables: `static readonly` arrays (e.g., `HOWMAYPIXELEXPECTPERLINE`, `arm_mode_table`, condition masks). These are safe to share.
- DSP precomputed tables: instance fields initially, because `BRCONDTAB` and `REGCONV` depend on initialization order. They can become `static readonly` after tests prove they are immutable and identical for every instance.

**Never** use `static` for emulated RAM, registers, or per-machine state.

---

## 8. Function-by-function porting notes

The subsections below describe the original FreeDOCore port. For new work, use the
Opera file in the same row as the behavioral reference; the existing C# type and test
layout stays the same.

| C# area | Original FreeDOCore file | Opera reference |
|---|---|---|
| CPU | `arm.cpp` | `opera_arm.c` |
| CLIO | `Clio.cpp` | `opera_clio.c` |
| MADAM | `Madam.cpp` | `opera_madam.c` |
| DSP | `DSP.cpp` | `opera_dsp.c` |
| VDLP | `vdlp.cpp` | `opera_vdlp.c` |
| XBUS/CD | `XBUS.cpp`, `Iso.cpp` | `opera_xbus.c`, `opera_cdrom.c` |
| Scheduler | `quarz.cpp` | `opera_clock.c` |
| SPORT | `SPORT.cpp` | `opera_sport.c` |
| PBUS/input | 4DO input layer | `opera_pbus.c` |

Port the smallest behavior the differential trace proves is wrong. Do not rewrite a
whole subsystem just because Opera's file is larger; Opera contains features (ODE,
subcode, personas, high-resolution VDLP) that may not be needed for the current bug.

### 8.1 `_3do_sys.cpp`

- `_3do_Init` order matters: ARM init, ROM read + byte swap, VDLP init with VRAM pointer, SPORT init, MADAM init, XBUS init + device reset, CLIO init with reset reason `0x40`, DSP init, frame init, diag init, quartz init.
- `biosanvil`/`isanvil`: if `isanvil == 30`, set `biosanvil = 1` before ARM init. The ARM core then skips 64 bytes on the first instruction and sets `isanvil = 1`.
- `_3do_Frame`: the `cnt >> 4` threshold and `12_500_000 / 60` loop are compatibility behavior. Do not replace with a "more accurate" scheduler without golden tests.
- `_3do_SaveSize`/`_3do_Save`/`_3do_Load`: the native format is a 16-word offset table with magic `0x97970101`. The C# port uses a new versioned format (see `docs/ImplementationPlan.md`), not this layout.
- `_freedo_Interface`: replace with typed methods on `FiveDoMachine` and `EmulatorSession`; do not keep an integer-procedure API in the product.

### 8.2 `arm.cpp`

- The CPU is an ARM60 (ARMv3, no Thumb, no halfword transfers, no coprocessor). The FreeDO interpreter is the specification.
- `CPSR` is kept as a `uint`; use named properties for N/Z/C/V/I/F/mode. Do not replace with separate bools unless every read/write is audited.
- Banked register switching (`ARM_RestUserRONS`, `ARM_RestFiqRONS`, etc.) must be ported exactly. These functions save/restore registers based on the *current* mode before changing mode. A seemingly cleaner banked-register model can change behavior.
- `_arm_Execute` increments PC before decoding, uses `pc_tmp` for some operations, and adjusts PC around multiply/swap. Preserve the order.
- The C++ switch uses fallthrough (`case 0x0` -> `case 0x1` -> `case 0x2/0x3`). C# does not allow implicit fallthrough. Re-implement as an ordered sequence of instruction-class checks, or use `goto case` with care. Do not accidentally change which class handles an instruction.
- `cond_flags_cross` table and condition evaluation must be exact, including the "never" condition `0xF`.
- `ARM_SHIFT_NSC` vs `ARM_SHIFT_SC`: one updates `carry_out`, the other updates CPSR.C. Preserve which one is called.
- Cycle counts (`NCYCLE=4`, `SCYCLE=1`, `ICYCLE=1`, multiply cycles, LDM/STM counts) feed the scheduler. Port exactly and test.
- FIQ entry happens after the instruction when `!ISF && _clio_NeedFIQ()`. The port uses `IInterruptSink.NeedFiq` and sets `gFIQ` appropriately.
- The `biosanvil` PC skip, `cnbfix` PC jump, and `FIX_BIT_TIMING_1` LDM value fixes are game/BIOS compatibility hacks. Keep them in `Arm60` behind named compatibility helpers, not scattered magic numbers.
- `_mem_write8/16/32` and `_mem_read8/16/32` are raw RAM helpers, not bus accesses. Keep them on `FiveDoMemory`.
- `mreadw`/`mwritew` are the bus. Preserve the decode order and sentinel `0xBADACCE5`.
- `MAS_Access_Exept` is mostly disabled in FreeDO. Keep a `DataAbortPending` flag for future accuracy, but do not raise a C# exception on unmapped access.

#### 8.2.1 Implementation notes for the C# ARM60

- `Arm60State` keeps the reference's `USER`/`CASH`/`SVC`/`ABT`/`FIQ`/`IRQ`/`UND` arrays. `SetCpsr`, `SetMode`, and the six `Rest*RONS` helpers are direct ports. Do not replace them with a conventional per-mode register file.
- `Arm60` owns the bus, the optional `IInterruptSink`, and a shared `CompatibilityState`. `ICpuBackend` is the backend contract; `IStateSection` writes the CPU section with explicit little-endian fields.
- `IPcAdjustSource` carries the CLIO `_clio_Poke` return value. `Arm60.WriteWord` consumes it immediately and adds four to PC, matching `mwritew`.
- FIQ entry checks `IInterruptSink.NeedFiq` after the instruction and also honours `Arm60.FiqPending` for deterministic tests. The reference's `gFIQ` flag is not used as a condition; do not start using it as one.
- The immediate data-processing path does **not** update `carry_out` in FreeDO. The port keeps a persistent `_carryOut` field, so an immediate `MOVS`/logical instruction with S set can observe the carry from an earlier shifter operation. This is a reference quirk; do not "fix" it without a compatibility test.
- A register-specified shift in a single data transfer (bit 4 set) matches `ARM_UND_MASK`/`ARM_UND_SIGN` and therefore enters the undefined vector before the SDT class. The port preserves that decode order; the register-shift code in the SDT method is kept for structural fidelity but is normally unreachable.
- For malformed encodings where FreeDOCore would loop forever between the `Undefine` label and the SDT class (for example a high-nibble 4/5 instruction with bit 4 set), the port raises the undefined vector instead of hanging. This is the only deliberate behavioural deviation in the CPU decode path and is covered by a test.
- `_arm_Reset` also reset CLIO and MADAM in C++. In the port, `Arm60.Reset` only resets CPU state; `FiveDoMachine` owns the reset order so each chip can be tested independently.
- `_arm_Init`/`_arm_Destroy` NVRAM callbacks are host responsibilities (`FiveDoMemory`/`EmulatorSession`), not CPU responsibilities.
- The CnB LDM heuristic uses three file-scope C++ globals (`addrr`, `vall`, `inuse`). They are private `Arm60` fields and are included in the CPU save-state section.

### 8.3 `Clio.cpp`

- `cregs[65536]` is a 256 KB register file. Keep it as `uint[65536]`.
- Interrupt registers: `0x40` active, `0x44` clear, `0x48` enable, `0x4C` disable, `0x60` high-active, `0x64` clear, `0x68` high-enable, `0x6C` disable. `NeedFiq = (0x40 & 0x48) || (0x60 & 0x68)`.
- `0x84` ROM select: bit 2 selects ROM bank 1.
- `0x300` clears FIFOs; `0x304` starts DMA; `0x308` stops DMA. The DMA `while (len >= 0)` loop and `len = 0xFFFFFFFC` sentinel are intentional; port with `int`.
- `0x400` XBUS direction; `0x500-0x53F` XBUS select; `0x540-0x57F` poll; `0x580-0x5BF` command FIFO; `0x5C0-0x5FF` data FIFO.
- DSP windows: `0x1800-0x1FFF` (2 words per ARM word), `0x2000-0x2FFF` (1 word), `0x3000-0x33FF` (2 instruction words), `0x3400-0x37FF` (1 instruction word), `0x3800-0x3BFF` (read 2), `0x3C00-0x3FFF` (read 1).
- `0x17E8` DSP reset, `0x17D0` semaphore, `0x17FC` DSP running, `0x17F0` random.
- Timers: 16 timers, `0x200/0x208` control banks, `0x100 + timer*8` counter, `+4` reload. `CASCADE`, `RELOAD`, `DECREMENT`, `FLABLODE`; only odd timers generate FIQ. Port `_clio_DoTimers` exactly.
- `0x120` has a timer-dependent write hack: `cregs[0x120] = (TIMER_VAL > 800) ? TIMER_VAL + (val / 0x30) : val;`. Preserve.
- `0x220` timer delay is masked with `0x3FF`; initial value is 64.
- `_clio_Init(0x40)` sets `cregs[0x28] = 0x40`, `cregs[0x400] = 0x80`, `cregs[0x220] = 64`.

### 8.4 `Madam.cpp`

This is the largest and riskiest port. Split it into classes but keep the original algorithm order.

- `mregs[2048 + 64]`; `mregs[0x28]` is the CEL status; `mregs[0x100..]` CEL registers; `0x130` modulo; `0x400-0x53F` FIFO window; `0x540/0x544` DMA source/length; `0x570/0x574/0x578` PBUS; `0x580` VDL head; `0x5A0` current CCB; `0x600-0x63F` matrix; `0x640-0x64F` vector; `0x660-0x66F` results; `0x680/0x684` Nfrac; `0x7FC` matrix command.
- `mregs[0x28]` status: idle `0x0`, suspended `0x30`, in-process `0x10`.
- `mregs[0x04]` forced to `0x29`; `mregs[0x574] = 0xFFFFFFFC`; `mregs[0x00] = 0x01020000` for the Green matrix engine autodetect.
- Matrix engine commands `0..3`:
  - `0` NOP copies previous results.
  - `1` 4x4 * 4 vector, divide by 65536.
  - `2` 3x3 * 3 vector, divide by 65536.
  - `3` 3x3 * vector, then `x*n/z`, `y*n/z`, with `n = (mregs[0x680] << 32) | mregs[0x684]` and a division-by-zero fallback.
  - Use `double` and preserve the cast order.
- `0x130` modulo register: `RMOD = ((val & 1) << 7) + ((val & 12) << 8) + ((val & 0x70) << 4)`, then `val >>= 8` for `WMOD`. Preserve.
- CEL engine:
  - `BitReaderBig` (`bitop.cpp`) is a big-endian bit reader over the word-swapped memory. Port `AttachBuffer`, `SetBitRate`, `SetPosition`, `SetPos`, `GetBytePose`, `Skip`, `Read()` exactly.
  - `PDEC` decodes 1/2/4/6/8/16 bpp pixels, PLUT entries, linear/uncoded modes, and returns an `AMV` value.
  - `PPROC` applies the pixel processor: source selection, AV signal, scale tables, XOR, add/subtract, clipping, `NOBLK` behavior.
  - `PPROJ_OUTPUT` applies PLUT position, SWAPHV, BDF/B0 position, and VH bits.
  - `DrawPackedCel_New` has three `TEXEL_FUN_NUMBER` paths: line (0), scale (1), arbitrary (2). The `speedfixes`, `unknownflag11`, `sdf`, `sf` variables are timing heuristics; port them as `CelTimingState` fields with the same values.
  - `DrawLiteralCel_New` and `DrawLRCel_New` have similar three paths.
  - `TestInitVisual`, `Init_Line_Map`, `Init_Scale_Map`, `Init_Arbitrary_Map` set up clipping and texture stepping. The clipping math is delicate; port exactly.
  - `TexelDraw_Line`, `TexelDraw_Scale`, `TexelDraw_Arbitrary` write pixels through `PPROC`/`PPROJ_OUTPUT` and `mwriteh`.
  - `XY2OFF(a,b,c) = ((b >> 1) * c) + ((b & 1) << 1) + a` is used everywhere. Keep a named helper.
  - **Argument order:** FreeDO's `readPIX(src, i, j)` / `writePIX(src, i, j, pix)` take the
    **row first** (`i` = y, `j` = x). The C# helpers take `(x, y)`, so the
    arbitrary-quad loops must call them with `(j, i)`. Passing `(i, j)` transposes every
    asymmetric quad; Road Rash then writes into the VRAM allocator metadata at
    `0x2FC000` (symptom: `AllocMem error, bad node address` and a frozen race) and Alone
    draws its character horizontally. Regression test:
    `PackedArbitrary_UsesTheColumnAsXAndTheRowAsY`.
  - `CCBFLAGS` bits and `PRE0`/`PRE1` bits are documented in the C++ source; copy them as named constants.
  - `LoadPLUT` reads 16-bit values with `_mem_read16((((pnt >> 1) + i) ^ 1) << 1)`. Port the exact XOR/scale.
  - `HandleDMA8`/`DMAPBus` copy the PBUS queue to memory and raise FIQ.
  - `KeyPressed` copies up to 16 bytes and fills the rest with `0xFF`.
- High-resolution (`RESSCALE`) is not part of the first port. When added, it touches `FiveDoMemory` mirroring, VDLP line duplication, SPORT copies, CEL stepping, and frame extraction. Do it as one milestone.

### 8.5 `DSP.cpp`

- `DSPDatum` contains a precomputed instruction table (`INSTTRAS[0x8000]`), `REGCONV[8][16]`, `BRCONDTAB[32][32]`, `NMem[2048]`, `IMem[1024]`, registers, and a PRNG seed.
- `_dsp_Init` builds `INSTTRAS` from the instruction fields and `BRCONDTAB` from all combinations of flags and `fExact`. Port this initialization exactly; it is the decoder.
- `_dsp_Loop` executes until `SLEEP`. The ALU is 32-bit (`ALUSIZEMASK 0xFFFFFFFF`); the 20-bit path is disabled in the reference.
- `Flags.Zero` checks `(Y & 0xFFFF0000) == 0`; `Flags.Nega` is bit 31; `fExact` checks `(Y & 0x0000F000) == 0`.
- The barrel shifter has arithmetic/logical shifts, `CLIP`, and the "load operand same self again" case. Port every case.
- `_Arithmetic_Debug` logs operand-count conflicts; keep it behind a debug flag.
- `_dsp_WriteMemory` masks to `0x3FF` (NMem), even though NMem is 2048 entries. This is a quirk; preserve it.
- `RegBase` maps 4-bit register numbers through `RMAP`; port the exact truth table.
- `ireadh`/`iwriteh` include EI/I memory and register windows; port their address decoding exactly.
- `_dsp_ARMwrite2sema4`/`_dsp_ARMread2sema4` implement the ARM/DSP semaphore; preserve the status bits and ACK behavior.
- The AudioFolio tail program detects an ARM command with `Sema4Status & 8`. This depends on immediate operands being shifted by `JUSTIFY & 3` (0 or 3). An incorrect shift turns the mask into `0x20` and silently skips every command.
- MUXB=3 computes `(MULT1 * (Y >> 15)) & ~1`; the mask is applied after the multiply. MUXA=3 masks `(Y >> 15)` before the multiply. The two paths are intentionally asymmetric in FreeDO and Opera.
- `fastrand` is `g_seed = 69069 * g_seed + 1; return g_seed & 0xFFFF;` with seed `0xA5A5A5A5`.
- `DSPPCNT`/`DSPPRLD` = 567. Each `Loop` subtracts 567 and wraps; this preserves FreeDO's DSPPCNT register behavior. The **host sample cadence** is controlled by the Quartz DSP clock, not by this reload value.
- Sample output is `(IMem[0x3FF] << 16) | IMem[0x3FE]`. The host interprets the packed 16-bit stereo pair as left in the low word and right in the high word; the WAV sink and CPU/GPU audio tests use that order.
- `Dsp.Loop()` must run even when no audio sink is attached. The guest uses the DSP for `AllocInstrument` and semaphore handshakes; gating the call on the sink makes headless runs time out in AudioFolio. Keep the sink null-conditional on `PushSample`, not on `Loop`.
- XBUS DMA writes go through `IRam`, the raw word-lane accessor, so the FIFO byte order is `b0,b1,b2,b3`. Opera's `b3,b2,b1,b0` order applies to its CPU-visible `opera_mem_write8`. Changing our order to match Opera's text breaks CD boot; the two accessors have different byte semantics.

### 8.6 `SPORT.cpp`

- `gSPORTCOLOR`, `gSPORTSOURCE`, `gSPORTDESTINATION`; VRAM pointer.
- Index `0x4000-0x5FFF` (mask `~0x1FFF == 0x4000`) is a flash write: fill 512 words with color, or masked blend.
- Index `0x0000-0x1FFF` is a page copy: copy 512 words from source to destination, or masked blend.
- Index `0x2000-0x3FFF` sets the color.
- High-res mirroring copies the 2 KB page to the other VRAM planes. Later.

### 8.7 `vdlp.cpp`

- `VDLDatum` fields: CLUT arrays, background, head VDL, modulo, current VDL, current/previous bitmap, output control, CLUT DMA word, line delay.
- `VDLExec` parses one display-list entry: CLUT DMA word, current/previous bitmap pointers, next VDL, then `numword` commands. Commands update CLUT entries, background, output control, or the fixed grayscale CLUT (`0xFFFFFFFF`).
- `_vdl_DoLineNew`:
  - At line 0, reload `CURRENTVDL = HEADVDL` and execute.
  - When `linedelay == 0`, execute the next VDL entry.
  - For `y = line - 16` in `[0, 240)`, if `enadma`, copy `MODULO` pixels from `PREVIOUSBMP`/`CURRENTBMP` into the frame line (two lines in high-res), and copy CLUT/control/background words. The 2026-10-24 fix uses the parsed modulo instead of a fixed 320, so 384/512/640/1024-wide modes are no longer cropped; `FiveDoMachine` derives the frame `SourceWidth` from the widest rendered line.
  - Each 16-bit pixel occupies the low or high halfword of a 32-bit VRAM word; the two halves of a word belong to adjacent scanlines. Advance the source by **four bytes per pixel** while copying (the reference's `unsigned int*` increment). Reading consecutive halfwords instead mixes two scanlines, halves the horizontal resolution, and shifts the image.
  - Advance `CURRENTBMP`/`PREVIOUSBMP` by 2 or `MODULO*4 - 2`, depending on bit 1 and `prevtick`.
  - Decrement `linedelay`, clear `OUTCONTROLL` bit 0.
- `VRAMOffEval` and `vmreadw` use the +2 MB VRAM offset and `RESSCALE`; preserve.
- Startup VDL is written at `0xB0000 + 2 MB`; the table is in `_vdl_Init`. Port it as a static array.
- `FIX_BIT_TIMING_5` swaps the current/previous bitmap pointer offsets; preserve.

### 8.8 `XBUS.cpp` and `Iso.cpp`

- `XBUS` has 16 device slots; device 0 is the CD. `XBSEL` selects the device, `XBSELH` is the high nibble of the select register.
- Poll bits: `POLST=0x10`, `POLDT=0x20`, `POLMA=0x40`, `POLRE=0x80`; mask bits `0x01/0x02/0x04/0x08`.
- `_xbus_SetCommandFIFO` calls the device; if the device reports FIQ, raise CLIO FIQ reason 4.
- Device 0xF is a built-in "no device" responder (status `0x83`, 12 bytes of `0x01`, etc.). Port `ExecuteCommandF`.
- `_xbus_SaveSize/Save/Load` include each attached device's save data. The C# state format has a device section instead.
- `cdrom_Device` fields: poll, XbusStatus, status/data FIFOs, command buffer, data buffer, requested sector count, MEI status, DISC state, current sector.
- Commands: `0x01` seek, `0x02` spin up, `0x03` spin down, `0x04` diagnostics, `0x06` eject, `0x07` inject, `0x08` abort, `0x09` mode set, `0x0A` reset, `0x0B` flush, `0x10` read data, `0x80` data path check, `0x82` read error, `0x83` read ID, `0x84` mode sense, `0x85` read capacity, `0x86` read header, `0x87` read subq, `0x88` read UPC, `0x89` read ISRC, `0x8A` read disc code, `0x8B` read disc information, `0x8C` read TOC, `0x8D` read session information, `0x8E` read device driver, `0x93` unknown. Port the exact status lengths and byte layouts.
- `SendCommand` executes when 7 bytes are buffered or when command 0 is `0x08` (abort).
- `GetDataFifo` streams 2048-byte sectors; when a sector is exhausted, it calls `OnSector(curr_sector++)` then `Read2048(Data)` for the next requested sector.
- `MSF2LBA` subtracts 150; `LBA2MSF` adds 150. Preserve the integer math and BCD helpers.
- `InitCD` uses `discSize + 150` for the total MSF and `discSize` for the session MSF; `Init` uses a hardcoded 150. `XBP_RESET` calls `InitCD`.
- The CD device's native save state is a raw `memcpy` of the class; do not reproduce it.

### 8.9 `quarz.cpp`

- `QDatum`: `qrz_AccARM`, `qrz_AccDSP`, `qrz_AccVDL`, `qrz_TCount`, `VDL_CLOCK`, `qrz_vdlline`, `VDL_HS`, `VDL_FS`.
- `VDL_FS = 526`, `VDL_CLOCK = VDL_FS * 30 = 15780`, `VDL_HS = VDL_FS / 2 = 263`.
- The ARM and VDL accumulators keep FreeDO's 8.24 fixed point, including the `arm++` correction:
  ```cpp
  arm = (clks << 24) / ARM_CLOCK;
  qrz_AccARM += arm * ARM_CLOCK;
  if ((qrz_AccARM >> 24) != clks) { arm++; qrz_AccARM += ARM_CLOCK; qrz_AccARM &= 0xFFFFFF; }
  qrz_AccVDL += arm * VDL_CLOCK;
  if (_clio_GetTimerDelay()) qrz_TCount += arm * (21000000 / _clio_GetTimerDelay());
  ```
- **Deliberate deviation: exact DSP sample clock.** FreeDO also updates `qrz_AccDSP` with `arm * SND_CLOCK`, so its 8.24 rounding over-produces samples for the 16-32 cycle batches this emulator uses. The C# `Quartz` instead accumulates the actual ARM cycle delta toward the next sample:
  ```csharp
  _dspCycleRemainder += cycles * SoundClock;      // 44,100 samples per ARM second
  if (_dspCycleRemainder >= armClock) { subtract; return true; }
  ```
  This matches Opera's `CYCLES_PER_SND` clock and produces exactly 44,100 samples per emulated second. The VDL and timer accumulators intentionally keep the FreeDO semantics so video/timer event timing is unchanged. See 12.4 for the evidence.
- `clks << 24` is a 32-bit operation in C++; use `uint` and document the expected range. Add a debug assert that `clks < 256`; if a caller exceeds it, investigate before changing the math.
- `QueueVDL/QueueDSP/QueueTimer` each consume one `0x1000000` unit (or one exact DSP cycle threshold) and return true at most once per call.

### 8.10 `DiagPort.cpp`

- Two 16-bit shift registers (`SNDDebugFIFO0/1`, `RCVDebugFIFO0/1`) and pointers.
- `_diag_Send` shifts bits in; `_diag_Get` shifts bits out.
- `_diag_Init(-1)` sets `GetPTR=SendPTR=16` and zeroes the receive FIFOs. A non-negative test code is XORed with `0xFF` and ORed with `0xA000`.
- This is a diagnostic/test port, not used by normal games. Port for BIOS diagnostics and tests.

### 8.11 `frame.cpp` and `Filters/`

- `Get_Frame_Bitmap` converts `VdlFrame` lines to BGRA-ish bytes:
  - Pixel 0 => background color (5-bit B/G/R from `xBACKGROUND`).
  - `xOUTCONTROLL & 0x2000000` and pixel bit 15 => fixed grayscale CLUT.
  - Otherwise use per-line `xCLUTB/G/R`.
  - Optional crop detection and alpha byte.
  - Optional HQ2X/3X/4X.
- The C# `FrameAssembler` does the same conversion into a `FrameBuffer` with a configurable pixel format. It is the **accuracy oracle and CPU fallback**, not the primary desktop path.
  - **M3 note:** the background is expanded from 5 bits to 8 bits with `(v << 3) | (v >> 2)`; FreeDO's `frame.cpp` writes the raw 5-bit value. The expansion is the documented CPU-oracle behaviour and the GPU shader must match it. CLUT entries are already 8-bit in both paths.
- The **required OpenGL renderer** ports the same palette logic into a fragment shader:
  - raw 16-bit pixels (`R16UI`) + per-line CLUT (`RGB8`) + line-param texture (`RGBA32UI`)
  - exact 5-bit-to-8-bit expansion
  - fixed CLUT and background paths
  - **Per-channel CLUT indices are mandatory.** The per-line CLUT is a 32-entry
    `(R,G,B)` table, but a 16-bit pixel uses `pixel >> 10` for red, `pixel >> 5` for
    green, and `pixel` for blue. The shader must fetch those three indices
    independently; using one index for `.rgb` makes real content grayscale. This bug
    was fixed on 2026-10-25 and is covered by shader-source and real-content checks.
  - the CPU and GPU outputs must hash identically in "Accurate" mode.
- **xBR** is an optional 4x edge-directed pass. It uses a CPU-expanded
  native-resolution `FrameAssembler` output uploaded as an RGBA texture rather than
  the FBO-attached palette texture, because the latter sampled black in the xBR
  program on the development driver. This is presentation-only and does not affect
  emulated state; the other filters keep GPU palette expansion.
- **HQx 2x/3x/4x** are optional CPU expansions of the same `FrameAssembler` output,
  followed by the sharp-bilinear scale. The pattern switches are derived from the
  4DO hq2x/hq3x/hq4x sources (LGPL-2.1+); see `THIRD-PARTY-NOTICES.md`. All three
  match the reference `hqNx_32_rb` hashes. The 3x/4x switches are generated with
  `tools/generate_hqx34.py`; the generated files are checked in. xBR and HQx are the
  supported filter-family members.
- Do not remove the CPU `FrameAssembler`; it is used by CLI/headless tests, as a fallback, and as the golden oracle for the shader.

### 8.12 CHD storage (new, no FreeDOCore equivalent)

FreeDOCore only knows about a flat 2048-byte-sector image supplied by the host. CHD support is a host/storage feature:

- `FiveDo.Storage.Chd.ChdDiscImage` uses **CHDSharp** (`ChdFile`) to open MAME CHD files.
- `ChdFile.ReadSector(lba, buffer)` returns 2352 raw bytes; `ChdDiscImage` extracts the 2048-byte user payload:
  - cooked Mode1/2048 -> offset 0
  - raw Mode1/2352 -> offset 16
  - raw Mode2/Form1 -> offset 24
- Validate `chd.IsCd`; reject DVD/HDD/raw/audio-only CHDs.
- `SectorCount` comes from the first data track, but the 3DO volume header remains authoritative.
- Enable `ConfigureReadAhead(4)` for sequential streaming.
- Support parent/child CHDs via `ChdFile.Open(path, parentPath, ...)`.
- CHDSharp is pure C#; no native dependency. Do not expose CHD types to the emulation core; the core only sees `IDiscSource`.
- Add ISO/BIN/CUE/CHD parity tests. CHD is a required v1 format, not an optional extra.

### 8.13 `Worker.cpp`

Not used by the core path. Do not port unless a future multitask mode needs it; use `Task`/`Thread` in the host instead.

---

## 9. Compatibility quirks checklist

Keep these in one place (`CompatibilityState` / `EmulatorTuning`) and test each:

- [ ] Anvil BIOS detection: `bios[28] == 234`; first ARM instruction PC += 64; `isanvil` set.
- [ ] `FIX_BIT_TIMING_1`: Crash 'n Burn LDM value/PC fixes.
- [ ] `FIX_BIT_TIMING_2`: reserved (game database flag; not used in this FreeDOCore revision).
- [x] `FIX_BIT_TIMING_3`: Dinopark Tycoon scale Y * 5.
- [ ] `FIX_BIT_TIMING_4`: reserved.
- [ ] `FIX_BIT_TIMING_5`: Microcosm VDL current/previous bitmap swap.
- [x] `FIX_BIT_TIMING_6`: Alone in the Dark literal CEL Y step.
- [ ] `FIX_BIT_TIMING_7`: reserved.
- [x] `FIX_BIT_GRAPHICS_STEP_Y`: Samurai Shodown CEL Y step.
- [ ] `cnbfix` one-shot PC jump at `0x94D60`.
- [x] `speedfixes`, `unknownflag11`, `sdf`, `sf` CEL timing state.
- [ ] `mregs[0x00] = 0x01020000` matrix engine autodetect.
- [ ] `mregs[0x04] = 0x29`, `mregs[0x574] = 0xFFFFFFFC`.
- [ ] NVRAM one-byte-per-word mapping.
- [ ] ROM bank select via CLIO `0x84`.
- [ ] CD `Init` vs `InitCD` filesize difference.
- [ ] `DataLen`/`Requested` sector streaming and `OnSector` ordering.
- [ ] CLIO `0x120` timer-dependent write.
- [ ] CLIO DMA `while (len >= 0)` and `len = 0xFFFFFFFC`.
- [ ] DSP `NMem` writes masked to `0x3FF`.
- [ ] DSP 567-cycle sample cadence.
- [ ] VDLP startup VDL at `0xB0000 + 2 MB`.
- [ ] PBUS device order 1,3,2,5,4,6 and `0x48`/`0x80` markers.
- [ ] `ARM_CLOCK` default 12,500,000; `VDL_FS` 526; `SND_CLOCK` 44,100; timer clock 21,000,000.

---

## 9a. Renderer and JIT deviations

These are deliberate additions, not part of FreeDOCore:

- **OpenGL presentation** is a new host-level feature. It consumes `VdlFrame` output and must not alter emulated state. Its palette-expansion shader is verified against `frame.cpp: Get_Frame_Bitmap`.
- **Optional guest ARM JIT** is a new CPU backend behind `ICpuBackend`. It is not part of the initial port. It must produce bit-identical state and cycle counts to the interpreter and remains disabled until proven.
- **High-resolution CEL rendering** is now opt-in (`--high-res`, `EmulatorTuning.HighResolution`), separate from GPU output upscaling. Do not conflate the two: output upscaling is presentation-only; high-res CEL changes emulated rendering and uses four 1 MB VRAM planes. Keep it off by default until the passcode-style pointer/hit-test path is validated.

---

## 10. What not to port

- `Worker.cpp` (unused Win32 thread helper).
- `stdafx.h` / MSVC precompiled-header plumbing.
- `freedocore.h`'s integer-procedure C ABI. Replace with typed APIs.
- `__declspec(dllexport/dllimport)`.
- `#pragma pack` save-state structs. Replace with explicit state sections.
- `malloc/new` ownership patterns. Use managed arrays and classes.
- Windows-only logging (`OutputDebugString`, `sprintf` buffers). Use Serilog through `FiveDo.Diagnostics.Logging`.

---

## 11. Definition of a correct port

A ported function is correct when:

1. It has a C# counterpart with the same observable behavior.
2. Its tests cover normal, boundary, and malformed inputs.
3. It matches the selected reference (Opera for new behavior, FreeDOCore for the
   original baseline) on a trace/golden test where one exists.
4. It does not introduce a new allocation in the steady-state hot path.
5. It does not change a global into shared mutable state.
6. Its comments identify the reference file/function and any deliberate deviation.

If a deviation is required for C# safety (e.g., division-by-zero guard), document it in the test and in this file.

---

## 11a. Differential checkpoint contracts

- The frame hash is FNV-1a over the assembled BGRA8888 pixels. It is
  endianness-independent because it hashes bytes, not `ushort`/`uint` words.
- The OperaCore host converts its XRGB8888 presenter frame to the same BGRA byte
  order before hashing, and includes alpha `0xFF`.
- The PCM hash is FNV-1a over each packed stereo sample as four little-endian
  bytes (left low 16 bits, right high 16 bits). It is cumulative from reset/state
  load.
- A checkpoint is taken after the completed frame's CPU/VDL work and its DSP
  samples. For OperaCore, the sample is finalized at the next video callback so
  its audio timeline matches the 5DO end-of-frame definition.
- `stateHash` hashes the serialized `.tdos` bytes (compressed v2 by default), not
  the live objects. It is a 5DO-side golden only until the reference export
  exposes an equivalent.
- Do not compare audio hashes across engines while the reference seeds its PRNG
  from wall-clock time; record them as informational evidence instead.

---

## 12. M1 implementation notes

These notes record decisions made while implementing the core/memory/scheduler milestone. They are not deviations from observable FreeDO behavior unless stated.

### 12.1 Memory allocation and lanes

- `FiveDoMemory` backs RAM/VRAM with a `uint[]`. (Superseded by 12.32: the backing is now the maximum 22 MB and the CPU-visible window comes from the active `MemoryLayout`.) `IRam.Size` reports the full backing size; `FiveDoMemory.VisibleSize` reports the stock 3 MB window.
- Raw RAM/ROM helpers use the reference's little-endian word representation. `IRam.Read8(offset)` returns lane `offset & 3`; unaligned `Read16`/`Read32` compose lanes across words.
- The CPU bus applies the reference XOR conventions: `Read8/Write8` use `addr ^ 3`; `Read16/Write16` use `addr ^ 2` for RAM/ROM; `Read32/Write32` align to four bytes. FreeDO has no halfword bus access, but the contract is defined consistently for future devices and tests.
- Unmapped byte/halfword reads return the low byte/halfword of `0xBADACCE5`, matching the value callers see after the reference's usual `& 0xFF`/`& 0xFFFF` masking.

### 12.2 ROM and NVRAM

- `LoadRom(bank, data)` requires exactly 1 MB and converts each four-byte big-endian BIOS word with `BinaryPrimitives.ReadUInt32BigEndian`, matching the `_bswap` loop in `_3do_Init`.
- NVRAM is a `byte[32768]` with the one-byte-per-word mapping `(index >> 2) & 32767`. Word writes store only the low byte; byte writes at all four byte addresses of a word hit the same NVRAM byte. The 32 KB wrap is preserved.

### 12.3 State framing

M1 implements the section primitive used by all later chips:

```text
[id:u32][version:u16][reserved:u16][length:u32][payload][crc32:u32]
```

- All primitives are explicit little-endian; no `Marshal`, `BinaryFormatter`, or field-layout assumptions.
- The reader bounds-checks every primitive and section, rejects truncated data, and validates the CRC before exposing the section payload.
- The outer `TDO1` header (magic, format version, BIOS/disc hashes, section table) is added in M8. M1 tests exercise the section layer directly.

### 12.4 Quartz event semantics

- `PushArmCycles` keeps the reference's ARM/VDL 8.24 accumulators and its `arm++` correction. It expects small cycle batches (the reference computes `clks << 24` in 32 bits); `Debug.Assert` documents the `< 256` contract.
- One second of ARM cycles (12.5M) produces 15,780 VDL line events, i.e. 30 full 526-line cycles and **60 half-frame presentations**. Tests assert 60 half-frame transitions.
- The DSP sample clock is exact: one ARM second produces **44,100** samples regardless of the batch size. The FreeDO 8.24 path produced 44,139 samples for 255-cycle batches, and real 16-32 cycle boot batches produced ~740-744 samples per 60 Hz frame (~44.4 kHz). That made audio ~0.7% sharp and constantly overfilled the live ring.
- Evidence (2026-10-27): a 600-frame Lemmings boot changed from 444,097 to 441,015 samples and 10.070 s to 10.000 s; Opera's 600-frame capture is 441,441 samples at its 59.94 Hz field rate. A 6,000-frame Road Rash capture has identical spectral peaks (442.1 Hz etc.) before and after, with the new 4,410,188 samples matching 100.0 s at 44.1 kHz. The audio content is unchanged; only the timebase was corrected.
- The 3,000-frame boot hashes changed for titles whose FMV/loading animation is audio-timed: Alone in the Dark and Lemmings remained pixel-identical, Road Rash and Space Ace show adjacent FMV frames, and Wolfenstein 3D differs only in the star-field sparkle. The Lemmings Fun 4 D-pad script still reaches gameplay at 23,000 frames. Re-baseline with the new hashes in [`TestCorpus.md`](TestCorpus.md) 1.2.
- VDL (15,794) and timer (25,022 for a 1,000-unit delay) reference counts are unchanged by the DSP correction.

### 12.5 Diagnostics

- The hosts configure Serilog once at startup through `LoggingSetup`/`AppLogging`; libraries use the `Serilog.Log` facade or an injected `ILogger`.
- `InMemorySerilogSink` is bounded and thread-safe and feeds the desktop log viewer; the rolling file, console, and bug-report sinks share the same event stream.
- Warning-or-higher events are submitted asynchronously by `BugReportSink` with environment, error, and exception blocks; CI/test runs disable submission.

### 12.6 M3 implementation notes

- `FiveDoMachine` is the composition root. It owns the reset order and the FreeDOCore frame loop. `RunFrame` keeps the reference's `cnt >> 4` batching threshold; `LastFrameCycles` exposes the actual per-frame budget for tests.
- `Clio` implements `IClioBus`, `IInterruptSink`, `IClioFifo`, and `IStateSection`. FIFO descriptor writes use the reference's `value + 4` length encoding, including the zero-length special case for input FIFOs. The DMA loop keeps the `len >= 0` and `0xFFFFFFFC` sentinel semantics.
- `Madam` implements the register file, CEL FSM/status, VDL head hook, matrix engine, and PBUS DMA. The matrix engine latches results in temporary fields, so a following command `0` (NOP) exposes the previous computation, matching `Madam.cpp`.
- `Vdlp` reads the display list and bitmap through the +2 MB VRAM window. `DoLine` copies from `PREVIOUSBMP` (not `CURRENTBMP`) using the reference's four-byte pixel stride, advances both bitmap pointers with the reference's `MODULO * 4 - 2` rule, and clears `OUTCONTROLL` bit 0 after each line.
- `Sport` adds the +2 MB VRAM window to every fill/copy address, matching `_sport_Init(Memory + 0x200000)`.
- `Xbus` implements the controller, device slots, poll/status/data FIFOs, and the built-in device-15 `0x83` responder. `FiveDoMachine` attaches the CD-ROM device to slot 0, which is the device the BIOS polls at boot.
- `Dsp` (M5) is the full `DSP.cpp` port. The former `DspStub` was removed; CLIO talks to it through `IDsp`, and it talks to CLIO through `IDspHost` for FIFO/FIQ services.
- `BiosImage`/`NvramImage` live in `FiveDo.Storage`. BIOS2 is zero-filled to 1 MB by the machine when shorter; NVRAM initialization writes the big-endian volume header and the initial directory words from 4DO's `EmulationHelper`.
- `FrameAssembler` expands the 5-bit background to 8 bits and always writes an opaque alpha byte. Crop detection and HQx scaling remain presentation-layer work.
- `M3.13` (the early OpenGL window spike) is deferred to M9R because it needs a desktop GL context; the CPU frame path and machine loop are fully testable without one.

### 12.7 M4 implementation notes (in progress)

- `CelBitReader` is a direct port of `BitReaderBig`: it reads through `buffer + (point ^ 3)`, treats a zero buffer as disabled, and clamps the bit rate to 1-32. Tests cover 1/3/4/8/16/32-bit reads, byte-boundary crossings, `Skip`, `SetPos`, and `GetBytePose`.
- `CcbControl`, `CcbHeader`, and `CcbParser` split the inline CCB parsing in `Madam.cpp: _madam_HandleCEL` into a testable header parser. The body order is significant and matches FreeDO: next pointer, source pointer, always-present PLUT-pointer word, position, size, perspective, pixel-processor control, then `PRE0`/`PRE1`. Reading the preamble before the geometry shifts every 3D CEL's fields. Absolute and relative pointers, the `NEXTCCB == 0`/out-of-range last-CEL rule, source/PLUT pointer validation, packed preambles, and CCB preambles are covered by tests. The rasterizer consumes the parsed header.
- `Madam.LoadPlut` preserves the reference's `_mem_read16((((pnt >> 1) + i) ^ 1) << 1)` address math. The palette-write bit is decoded by the pixel decoder in M4.4.
- `PixelDecoder` ports `PDEC`: 1/2/4/6/8/16 bpp, linear/coded modes, the 6 bpp palette-write bit, 16 bpp coded bit 15, and the `MAPu8b`/`MAPc8bAMV`/`MAPc16bAMV` tables built in `_madam_Init`. The caller supplies the current PLUT and CCB flags, so the decoder stays stateless. Transparency uses `(pres & 0x7FFF) == 0` and the inverse of `CCB_BGND`.
- `PixelProcessor` ports `PPROC`: PIXC source selection, the `PSCALAR` scale table (with FreeDO's `PDV` shift mapping), AV signal fields (`NEG`, `XTEND`, `nCLIP`, `dv3`), PXOR, signed-char component arithmetic, clipping, and the `CCB_NOBLK` black-pixel fixup. The pmode masks are derived from the CCB flags, and the adjusted pixel (not the original decoder output) is used for the frame-source and `ms=2` paths, matching the reference's in-place `pixel` update.
- `Projector` ports `PPROJ_OUTPUT`: `CCB_PLUTPOS`, `SWAPHV` with `PRE1_NOSWAP`, `B15POS`/`B0POS`, and the `0x7FFE` processor-output mask. `CFBDSUB` is disabled in the reference and is intentionally not applied.
- The pixel processor and projector were differential-tested against a standalone build of FreeDO's `PPROC`/`PPROJ_OUTPUT` (2.29M PPROC vectors plus a deterministic grid hash). The test constants are generated from the reference C++ with signed-char semantics.
- `CelGeometry` ports `TestInitVisual`, `Init_Line_Map`, `Init_Scale_Map`, `Init_Arbitrary_Map`, `QuardCCWTest`, and `TexelCCWTest`. It keeps the reference's 16.16 arithmetic, arithmetic shifts, half-pixel origin adjustment, and the retained `TEXTURE_WI_LIM` quirk. The `CCB_ACW`/`CCB_ACCW` flags are exposed as `CcbControl.Clockwise`/`CounterClockwise`. A 300k-case differential run against the reference matched every field.
- `XY2OFF` is intentionally left as a small helper in the rasterizer (`((y >> 1) * modulo) + ((y & 1) << 1) + x`) rather than a public API; the address math is shared by the CEL and SPORT paths.
- High-resolution rendering was initially left off (`RESSCALE = 0`); the opt-in line-doubled mode now uses four `VramSize` planes (see 12.32). True vertical 640x480 remains VID-01 research.
- `CelMemory` ports the CEL memory-access cycle costs (`mread` +1, `mwrite` +2, `mreadh` +1, `mwriteh` +2, `readPLUTDATA` +4) and the `GetCELCycles` take-and-clear behavior. The rasterizers route all CCB and pixel access through this class, so the cycle total reported by `Madam.HandleCel` matches the reference.
- `CelEngine` ports `_madam_HandleCEL`: CCB walking, last-CCB handling, applying the parsed position/size/perspective/pixel-control fields, PLUT loading, packed/literal/LR dispatch, and the `SPRON` status bit. `CcbParser` accepts an optional `CelMemory` so header reads are charged to the CEL cycle accumulator; `CcbHeader` is a struct to keep the steady-state path allocation-free. The parsed fields live on the engine instead of file-scope globals.
- `CelRasterizer` ports `DrawPackedCel_New`, `DrawLiteralCel_New`, `DrawLRCel_New`, and their three texel paths. The packed raster's run types (`literal`, `transparent`, `repeat`), width clipping, skipped rows, per-row data offsets, and `SPRWI` carry-over match the reference. The literal and LR paths preserve the `PRE1` width/offset fields, the LR `offset += 2` tile bias, and `SPRHI = (vcnt << 1) + 2`.
- `TexelDraw_Line`, `TexelDraw_Scale`, and `TexelDraw_Arbitrary` preserve the reference's integer shifts, `TESTCLIP`, `XY2OFF`, cache-on-frame-pixel-change behavior, and direct (non-cycle-counted) arbitrary-path VRAM access. `FIX_BIT_TIMING_3`, `FIX_BIT_TIMING_6`, `FIX_BIT_GRAPHICS_STEP_Y`, `speedfixes`, `unknownflag11`, `sdf`, and `sf` are applied through `CompatibilityState`.
- Deliberate deviation: `TexelDraw_Arbitrary` guards division by a zero edge length (returns a zero numerator contribution) instead of relying on undefined C++ behavior. It also initializes the second crossing-point slot to zero; the reference reads an uninitialized stack slot when a scanline has a single crossing. These changes only affect degenerate quads and keep the core deterministic.

### 12.8 M5 implementation notes

- `Dsp` owns the reference's `DSPDatum` state as instance fields: NMem/IMem, REGCONV, the 0x8000-entry request/BS tables, BRCONDTAB, dregs, ALU/multiplier latches, semaphore, PRNG, and CPUSupply. No mutable statics.
- Instruction decode is a readonly `DspInstruction` bit-field view over the raw 16-bit word. One-bit signed C fields (`DI`, `JUSTIFY`, `PAD`, `M2SEL`) are exposed as -1/0 so comparisons and copies keep the reference semantics.
- `BRCONDTAB` is precomputed exactly as `_dsp_Init` does. The reference packs the four condition flags into the second index by multiplying the 32-bit flag word by 0x10080402; the equivalent C# packing is Zero<<4 | Nega<<3 | Carry<<2 | Over<<1 | fExact.
- The ALU keeps the reference's `signed short` MULT1/MULT2/ALU1/ALU2 latches, ACSBU override for ALU ops 3/5, the 32-bit wrap mask, and flags computed before the barrel shifter. Shifter case 8/24 updates Carry from the pre-shift sign bit.
- Operand loading preserves the reference's request bits, NUMOPS promotion to 4, DI indirection, immediate JUSTIFY shift (0 or 3), write-back priority (GWRITEBACK only when the pool contains more operands than requests), and the BS=8 rule that loads the shift count from an extra operand. The one-bit `JUSTIFY` field is sign-extended to 0/-1 and masked with `& 3`; it is **not** `~(JUSTIFY+1) & 3`.
- `ireadh`/`iwriteh` keep the reference's asymmetric address mapping: reads of 0x300+ wrap into the low 0x80 words, while writes of 0x300+ stay in 0x300+. This is how DSP code reads a coefficient and writes an audio sample through the same nominal address.
- Deliberate deviations: instruction fetches mask PC to 10 bits (deterministic on malformed programs); the two `fastrand()` calls in one C expression are made left to right; debug prints from `_Arithmetic_Debug`/FIFO-direct writes are omitted.
- `FiveDo.Audio` adds a monotonic-counter SPSC ring buffer, a normalized-float pipeline, a null sink, a buffered 44.1 kHz/16-bit stereo WAV writer with streaming statistics, a WAV analyzer, and the OpenAL live sink. The ring buffer never allocates after construction; full rings drop the newest sample rather than blocking the emulation thread.
- `FiveDoMachine.Reset` and `LoadState` call `IAudioSink.Reset`. Live sinks clear their ring and source queue so a reset/load does not replay up to ~180 ms of the previous timeline; WAV captures intentionally continue.

### 12.9 M6 implementation notes

- `CdromDevice` ports `cdrom_Device` from `Iso.cpp`: poll/status/data FIFOs, all documented command status layouts, sector streaming, and the reference's MSF/LBA quirk (`InitCD` adds 150 frames, then `LBA2MSF` adds them again). The device is attached to XBUS slot 0 and talks to storage only through `IDiscSource`.
- Sector reads call `ReadSectorIntoData` before advancing `_currentSector`. This matches the reference's effective host behaviour: `_3do_OnSector(curr_sector++)` selects the current sector and `_3do_Read2048` fills the FIFO from it.
- `IsoImage` reads 2048-byte sectors; `BinImage` strips the 2352-byte sync/header at offset 16 (Mode 1) or 24 (Mode 2 Form 1); `CueSheet` supports the first MODE1/MODE2 data track (single file); `DiscImageFactory` also sniffs CHD/ISO/BIN headers.
- `ChdDiscImage` uses CHDSharp 1.6.0, rejects non-CD images, maps the first data track's LBA space, enables 4-hunk read-ahead, and supports an optional parent CHD for differential images.
- `VolumeHeader` parses the big-endian 132-byte record (blockCount at offset 80); `GameId` is the lowercase MD5 of sectors 0 and 1. Game database lookup is M7.6.
- Tests generate synthetic ISO/BIN/CUE/CHD fixtures with CHDSharp's encoder and verify identical sector streams, parent/child opening, non-CD rejection, command transcripts, and CD state roundtrip.

### 12.10 M7 implementation notes

- `PbusEncoder` reproduces 4DO's 16-byte PBUS packet: header 0x00/0x48, controllers in wired order 1,3,2,5,4,6 at offsets 2,4,6,8,10,14, low byte L/R/X/P/C/B, high byte A/Left/Right/Up/Down plus the connectivity bit 0x80. The packet size is fixed and encoding is allocation-free.
- `ControllerState` is an immutable button set; `InputManager` merges the keyboard mapper with an `IGamepadBackend` and exposes `IPbusSource` for the machine. The Avalonia event plumbing is intentionally outside the core and lands with M9.3/M9.6.
- `InputProfile` stores per-player keyboard/gamepad maps and hotkeys as JSON. Default profiles bind the keyboard only for player 1 so a single key does not press the same button on all players; gamepad bindings are available for all six.
- `NvramStore` lives in `FiveDo.Host` because it is host I/O; the core still only accepts the 32 KB image. Writes are debounced through an injected `TimeProvider` (250 ms), use a temporary file plus atomic replace, and back up a wrong-sized file as `.bad` before creating a fresh image.
- The 4DO `GameDatabase.xml` is an embedded resource with an attribution comment; lookups accept either case and unknown checksums return null. Per-game fix overrides remain a settings-layer concern.

### 12.11 M8 implementation notes

- Save states use the existing `TDO1` header plus per-section `[id][version][flags][length][payload][crc32]` frames. Header fields are the emulator version, ARM clock, fix mode, BIOS MD5, optional disc hash, and frame count. Format version 2 sets `flags` bit 0 for Brotli-compressed payloads; the stored payload begins with its uncompressed length and the CRC stays over the uncompressed bytes. Version 1 (raw sections) remains readable. Unknown format/section versions and unknown flag bits fail; BIOS hash mismatch is a warning.
- `FiveDoMemory` now implements `IStateSection` and serializes RAM, both ROM banks, NVRAM, the selected bank, and the PC-adjust latch. RAM/ROM are written as raw little-endian words because the emulated representation is word-swapped; all supported runtimes are little-endian.
- `Dsp` serializes its architectural state and NMem/IMem; REGCONV, request/BS tables, and BRCONDTAB are immutable precomputed tables rebuilt in the constructor, so they are not duplicated in the file.
- The machine state also stores the derived VDL frame and the mutable `CompatibilityState`. Without the latter, the CPU's runtime Anvil detection (which changes `IsAnvil`/`BiosAnvil`) made a loaded run diverge after a few frames even though the CPU registers matched at load.
- `SaveStateToFile` writes a temporary file then atomically replaces the target. `StateInspector` reads the header and section table without constructing a machine. `FrameDumper` writes 32-bit BMPs for headless diagnostics.
- The CLI uses a small hand-written option parser (the System.CommandLine package is reserved for a later UX pass). All commands share `--json`; errors return exit code 1, unknown commands 2. Audio output is null or a 44.1 kHz WAV sink; there is still no live device backend.

### 12.12 M9/M9R implementation notes

- The OpenGL presenter lives entirely in the host layer. `FiveDo.Video` owns the backend-neutral `IVideoRenderer` contract, settings, aspect/integer viewport math, the frame uploader, and `GpuPalette` (the CPU reference for the shader). `FiveDo.Video.OpenGL` owns Silk.NET GL/GLES resources and the GLSL 330 / GLSL ES 300 shader variants. The target design and current gaps are in [`Performance.md`](Performance.md).
- `VdlFrameUploader` packs R16UI indexed pixels, an RGB8 32xheight CLUT, and an RGBA32UI per-line background/out-control texture. Buffers are reused. The shader uses `texelFetch` with nearest sampling for the accurate path.
- `GpuPalette` exists so the palette algorithm can be tested without a GL context: tests compare it against `FrameAssembler` for a 320x240 synthetic VDL frame including background, fixed-CLUT bit 15, and per-line CLUT cases. The shader mirrors this logic.
- `OpenGlRenderer` creates the programs, VAO/VBO, textures, and a native-resolution palette FBO, draws a fullscreen triangle strip into the supplied framebuffer, and implements `ReadPixels` with an RGBA-to-BGRA swap. `VideoSurface` calls `GL.GetApi(gl.GetProcAddress)` from Avalonia's `OpenGlControlBase` and renders a latest-frame mailbox snapshot. The two-stage palette/scale pipeline, `TexStorage2D`/`TexSubImage2D`, cached uniforms, and physical-pixel sizing are implemented. **Remaining deviations:** the GLES/ANGLE/Pi path is not validated on hardware, and the HQx 2x/3x/4x filters run on the CPU rather than the GPU.
- The Windows frontend now prefers WGL with ANGLE/software fallbacks. `OpenGlRenderer` detects an OpenGL ES context from the version string and selects GLSL ES 300 shader sources with the required precision qualifiers; desktop contexts keep GLSL 330. Real-hardware ANGLE and Raspberry Pi 4/5 validation remains.
- Context loss is handled by re-running `Initialize`. The UI does not yet automatically fall back to `CpuVideoRenderer` when the context or integer textures are unavailable; that is a frontend decision for M9.2/M9R.7.
- The desktop shell runs the machine on `FiveDo.Host.CoreRunner`'s dedicated 60 Hz thread. UI actions (reset, save/load, disc change, screenshot) pause the loop via `CoreRunner.Invoke`; the UI consumes the latest-frame mailbox and updates status/FPS at 4 Hz. Screenshots use the CPU frame dumper, and keyboard input is mapped through the default `InputProfile`.
- `AppSettings`/`AppSettingsStore` persist BIOS/disc paths, save/screenshot directories, renderer filter/aspect/vsync, audio enable/volume, and the JSON input profile. The shell auto-loads the last BIOS/disc when the files still exist and saves settings on close.
- CI now publishes both the CLI and the Avalonia frontend for the six target RIDs; a local win-x64 CLI publish and a short Windows frontend launch were smoke-tested. The launch smoke test did not prove 4K60, GLES compatibility, or sustained frame pacing; those are M9P gates. Live audio output and arm64 validation remain.
- `OpenAlAudioOutput` is host I/O: a dedicated thread fills three 512-frame OpenAL Stereo16 buffers from a 4096-frame SPSC ring. The OpenAL context is made current **on the audio thread** (the current context is thread-local); failing to do this leaves all fill calls on a thread with no context and produces silence. Underruns are padded with silence and counted. `TryCreate` returns null with a diagnostic when no device/context exists, keeping headless and CI runs silent and stable. The OpenAL Soft 1.23.1 native runtime is bundled (`Silk.NET.OpenAL.Soft.Native`); Windows copies `soft_oal.dll` as `OpenAL32.dll`.

### 12.13 ARM timing mode (2026-10-31)

- `ArmTimingMode.FreeDoCompatible` keeps the original port's `Ncycle = 4` and remains
  the default. `ArmTimingMode.Arm60Datasheet` selects `Ncycle = 2`, matching
  `References/opera-libretro-master/libopera/opera_arm.c` and the ARM60 datasheet/SDK
  rationale documented there. S and I cycles are unchanged.
- The interpreter stores the selected N-cycle length per instance; no mutable static
  state is involved, so machines with different modes can run in parallel.
- The CLI exposes `--arm-timing freedo|arm60` on `run` and `trace`, and the desktop
  Settings dialog exposes the same choice. Changing the mode recreates the machine.
- Save states include an optional `Options` section (id 12) that records the ARM timing
  mode. Older states load with the FreeDO-compatible default. A mismatch is reported in
  `StateWarnings` and does not reject the state. Older 5DO builds ignore the unknown
  section, preserving backward compatibility without a format-version bump.
- This is a timing option, not a compatibility fix. Do not apply the ARM60 mode by
  default until a full-collection differential shows it is safe.

### 12.14 CPU clock control (2026-10-31)

- `EmulatorTuning.ArmClock` was already the single source of truth for the frame budget
  and Quartz. The CLI now accepts `--cpu-clock` as a multiplier (`0.5x`-`4x`) or an
  absolute clock (`6.25MHz`-`50MHz`), and `AppSettings.CpuClockMultiplier` exposes the
  same range in the desktop shell. The stock 12.5 MHz clock remains the default.
- The save-state header already records the absolute ARM clock; loading a state written
  at another clock adds the existing mismatch warning and does not reject the state.
- Overclocking is a performance option, not an accuracy option. Do not change the
  default based on compatibility anecdotes; measure a title at each clock first.

### 12.15 Random seed control (2026-10-31)

- FreeDO seeds the DSP PRNG once in `_dsp_Init` (`0xa5a5a5a5`) and leaves it alone in
  `_dsp_Reset`. `Dsp.SetSeed` preserves that lifecycle; `FiveDoMachine` applies
  `EmulatorTuning.RandomSeed` after construction and before the first instruction.
- `--random-seed default` keeps the reference seed, `random` chooses a fresh 32-bit
  value at launch, and a decimal/`0x` value fixes the seed. The same option is stored in
  `AppSettings.RandomSeed` and edited in Settings > Emulation.
- Save states already serialize the DSP seed, so a loaded state continues from its
  recorded PRNG position rather than the configured initial seed.

### 12.16 CLIO timer scheduler (2026-10-31)

- `ClioTimerMode.FreeDoCompatible` keeps the original port's `DoTimers()` all-timer
  scan: one 25 MHz/64 event decrements every enabled timer. It remains the default so
  golden frames and existing states are unchanged.
- `ClioTimerMode.OperaScheduler` follows `opera_clock.c` and `opera_clio.c`: the
  scheduler walks timer slots 0-15, each slot lasting four 25 MHz ticks, then waits the
  `0x220` slack plus four ticks before slot 0 of the next scan. Only the due timer is
  executed, and `DoTimers(int)` keeps the explicit `TIMER_CARRY` so a cascade timer
  decrements only after the previous timer underflows. A non-reload underflow writes
  `0xFFFF` and clears `DECREMENT`; a reload underflow reloads from `+4`.
- The scheduler accumulates raw ARM cycles in 16.16, matching Opera's `timer_acc`. At
  the stock 12.5 MHz clock a slot is exactly two ARM cycles; with the default slack of
  64, one scan is 64 ARM cycles (15 slot intervals plus the 34-cycle gap).
- The CLI exposes `--clio-timers freedo|opera` on `run` and `trace`; Settings >
  Emulation exposes the same choice. `run --json` reports `clioTimers`. Changing the
  mode recreates the desktop machine.
- Save states record the mode in the optional `Options` section (version 2 at the time;
  the section is version 3 after the CEL timing pass). Quartz section version 3 stores
  the Opera slot, gap flag, and 16.16 accumulator; Clio section version 2 stores the
  cascade carry. Version-1/2 sections still load with a reset timer scheduler. A mode
  mismatch is a warning, not a rejection.
- Do not make the Opera scheduler the default until a full-collection differential
  shows it is safe. The 3,000-frame smoke keys in `TestCorpus.md` show the expected
  timing divergence.

### 12.17 MADAM CEL busy timing (2026-10-31)

- `CelTimingMode.FreeDoCompatible` keeps the original synchronous model: `RunFrame`
  draws the CEL list as soon as it sees `Fsm == InProcess` and returns the FSM to idle.
  It remains the default so golden frames and existing states are unchanged.
- `CelTimingMode.OperaBusy` follows `opera_madam.c`:
  `opera_madam_cel_tick()` runs before each ARM instruction. A `SPRSTRT` write sets a
  pending-draw flag; the next tick draws the whole list, then keeps `Fsm == InProcess`
  and the status register at `0x10` until the estimated deadline expires. A `SPRSTRT`
  while busy replaces the pending period; `SPRPAUS` freezes the countdown and
  `SPRCNTU` resumes it.
- The deadline mirrors Opera's work estimate: the larger of rendered pixels and walked
  source texels, divided by nine million pixels per second and scaled by the ARM clock,
  plus 64 setup cycles per list entry. The rasterizer counts one pixel per framebuffer
  write (one logical pixel in high-resolution mode, not two planes) and one texel per
  arbitrary-quad walk.
- The CEL work runs in parallel with the CPU, as it does on hardware and in Opera; the
  estimate is compared against elapsed CPU cycles and is not added to the Quartz
  accumulators. The `RunFrame` loop ticks the CEL state before each instruction, so
  the guest observes the busy status at instruction granularity.
- The CLI exposes `--cel-timing freedo|opera` on `run` and `trace`; Settings >
  Emulation exposes the same choice. `run --json` reports `celTiming` and the remaining
  `celBusyCycles`. The CEL trace adds a `cel-list` line with the list entry, pixel,
  texel, and busy-cycle totals.
- Save states record the mode in the Options section (version 3) and the pending/busy
  fields in the Madam section (version 2). Version-1 Madam sections derive the pending
  flag from the FSM; version-1/2 Options sections default the mode to FreeDO-compatible.
  A mode mismatch is a warning, not a rejection.
- Do not make the Opera busy model the default until a full-collection differential
  shows it is safe. The 3,000-frame smoke keys in `TestCorpus.md` show the expected
  divergence: Lemmings and Gex are unchanged, while Road Rash reaches a different boot
  frame because its early CELs now consume time.

### 12.18 CD-ROM cycle-based data timing (2026-10-31)

- `CdromTimingMode.FreeDoCompatible` keeps the original immediate transfer: the first
  requested sector is read during the command, and each following sector is read as soon
  as the previous buffer drains. It remains the default so golden frames and existing
  states are unchanged.
- `CdromTimingMode.OperaCycleBased` follows the data path in
  `opera_cdrom.c`: `cdrom_cmd_read_data` schedules the first sector at
  `cycles + cdrom_data_sector_cycles()`, clears both poll bits, and
  `cdrom_data_load_next_if_ready()` publishes a sector only after its deadline. The
  sector time is `ARM clock / (75 * speed)` minus Opera's measured overhead
  compensation (1,660-1,704 x 0.1 us depending on speed), giving 81,246 cycles at the
  stock 2x clock and 164,591/39,554/18,715/8,286/4,826/3,090 cycles at
  1x/4x/8x/16x/24x/32x. `CdSpeed.Unbounded` returns zero cycles and publishes sectors
  immediately. The configured speed is treated as the effective speed; 5DO does not
  advertise the XBUS 2x status bit, so Opera's `cdrom_effective_speed` reduction to 1x
  does not apply. That status-byte divergence is intentional for now: changing the
  default status byte would alter every golden CD transcript.
- The 0x10 read-data status response is deferred until the guest has drained the last
  requested sector, matching `cdrom_data_publish_read_status_if_ready()`. A zero-length
  read publishes status immediately. While waiting, `POLDT` and `POLST` are clear, so
  the guest polls instead of reading stale zeroes.
- `CdromDevice.Tick(cycles)` is called from `FiveDoMachine.InternalFrame` for every
  16-255-cycle CPU batch. The device raises the XBUS CD FIQ through
  `InterruptRequested` when delayed data or status becomes ready; without that callback
  the guest could see the poll bit but never receive the interrupt.
- The CLI exposes `--cd-timing freedo|opera` on `run` and `trace`; Settings >
  Emulation exposes the same choice. `run --json` reports `cdromTiming`.
- Save states record the mode and speed in the Options section (version 5) and the device
  clock, next-ready cycle, and pending-status flag in the CD-ROM section (version 2).
  Version-1 CD-ROM sections load with a zeroed clock; version-1/2/3/4 Options sections
  default the mode to FreeDO-compatible and the speed to 2x. A mode or speed mismatch is a
  warning, not a rejection.
- This is the first CORE-04 increment, not the whole CD port: command latency, TOC from
  the disc source, read-ahead, subcode, DIPIR tagging, CD-DA, and ODE remain. SYS-09 adds
  the speed selector without changing the default 2x timing. Do not make the cycle model
  the default until the full-collection differential shows it is safe. The smoke keys are
  in `TestCorpus.md` 1.1d/1.1e.

### 12.19 CD speed and timing profiles (2026-10-31)

- `CdSpeed` values map directly to Opera's `cd_speed` option: `Unbounded`, `X1`, `X2`,
  `X4`, `X8`, `X16`, `X24`, `X32`. The default is `X2`, the stock 3DO drive.
- `CdromDevice.DataSectorCycles` uses `clock / (75 * speed)` and subtracts the
  per-speed overhead table from `cdrom_data_sector_overhead_compensation_x10_usec`:
  1,660 (1x), 1,670 (2x), 1,690 (4x), 1,695 (8x), 1,704 (16x), and 1,695 for 24x/32x.
  `Unbounded` returns zero so the requested sector is published immediately. The FreeDO
  timing path ignores the speed and remains immediate.
- The drive status does not advertise the XBUS 2x bit in 5DO, so the configured speed is
  treated as the effective speed instead of following Opera's reduction to 1x when the
  bit is clear. Do not set the 2x bit as part of a speed change without re-baselining the
  CD transcripts; it changes every status response.
- `EmulationProfile` is a host-side shortcut over the timing options, not a new machine
  mode. `EmulationProfiles.Apply` preserves fixes, high-res, backend, and seed.
  `Detect` compares the actual options (including the ARM clock) and returns `Custom` on
  any mismatch. `AppSettings.Profile` is persisted for the dialog, but the individual
  options remain the save-state source of truth.
- The `Performance` profile uses a 1.5x ARM clock with FreeDO timing. It is an explicit
  speed-over-accuracy choice; do not present it as a compatibility mode.
- The three-title 3,000-frame smoke differential is in `TestCorpus.md` 1.1e. All speeds
  complete and render; the FreeDO default is bit-identical across speed settings. A
  full-collection speed sweep and CD-DA speed behavior remain.

### 12.20 BIOS manager and auto-selection (2026-10-31)

- `BiosCatalog.Default` stores published MD5 identifiers, not ROM bytes: Panasonic FZ-1
  (`f47264dd...`), FZ-10 (`51f2f43a...`), FZ-10E Anvil (`a48e6746...`), FZ-10JA Anvil
  Kanji (`42857725...`), GoldStar (`8639fd5e...`), Sanyo TRY (`35fa1a1e...`), patched
  FZ-10 (`1477bda8...`), patched FZ-10E (`cf11bbb5...`), the FZ-1 Kanji font
  (`b8dc97f7...`), and the 512 KB arcade support ROM (`8970fc98...`).
- `BiosLibrary.Describe` treats exactly 1 MB as a primary BIOS and a smaller non-empty
  file as a secondary/font/arcade ROM. Other sizes are rejected. Unknown images are
  accepted; their region/video metadata is inferred from the file name and the notes
  explicitly say it is unverified.
- `BiosLibrary.SelectForGame` scores candidates by territory, video standard, Kanji,
  Anvil, patch state, and a catalog preference. Patched images are excluded for
  non-arcade discs unless explicitly allowed. Arcade detection uses the game name or
  disc file name containing `Arcade`, because some arcade discs are absent from the
  game database and have no region string.
- Known title requirements are intentionally small and evidence-based: Slayer Europe
  -> FZ-1, Slayer USA -> FZ-10, Powers Kingdom Europe -> FZ-10E Anvil. Arcade discs use
  the patched image through the scoring rules. Do not add title hacks without the same
  evidence standard used by `Compatibility.md`.
- `BiosResolver` runs only when `AutoBios` is true **and** `BiosFolder` is non-empty.
  This preserves existing `BiosPath`-only configurations. Per-game overrides still use
  the folder if present, then fall back to the configured path.
- `run --json` adds `biosPath`, `bios2Path`, `biosMd5`, `bios2Md5`, `biosName`,
  `biosRegion`, `biosSelection`, and `biosSource`. `bios select` uses the same selector;
  `bios list/scan` validates a folder. The smoke evidence is in `TestCorpus.md` 1.1f.
  SYS-06 later added `biosRsaPatched`/`biosRsaDisabled`; see 12.34.
- Save-state BIOS mismatch warnings already cover the primary BIOS hash. A BIOS2 hash is
  not in the v2 header; adding one would require a state-format change.

### 12.21 Per-game NVRAM and versions (2026-10-31)

- Opera's `opera_nvram_storage` and `opera_nvram_version` options map to
  `NvramStorageMode` (`Global`, `PerGame`) and `NvramVersion` (0-9). 5DO keeps the
  historical shared file for `Global` version 0 so existing installations are untouched.
- Paths follow `NvramLayout`: shared v0 is `<root>/nvram.ram`, shared v1-9 is
  `<root>/nvram.<n>.ram`, and per-game is
  `<root>/nvram/per-game/<gameId>.<n>.ram`. The game ID is the lowercase 4DO MD5 of the
  first two sectors; unsafe file-name characters are replaced with `_`.
- Migration is read-only. When the primary per-game image is missing, `NvramStore`
  tries per-game version 0, then the shared version, then legacy `nvram.ram`. The next
  save writes the per-game primary. A corrupt primary is moved to `.bad` and replaced
  with a fresh image; it does not fall through to an older image, because that would
  hide data loss.
- The desktop shell flushes the outgoing image before switching games, then loads the
  incoming image on the core thread before reset. Changing the mode/version restarts the
  machine through the normal options path. The CLI resolves the same layout and reports
  it in `run --json`.
- `nvram path|list|export|import|reset` are host-side utilities; they do not run the
  machine. Import validates exactly 32 KB and writes atomically. Reset writes a fresh
  initialized volume, which also prevents a shared fallback from reappearing.
- Smoke evidence is in `TestCorpus.md` 1.1g. Per-game storage does not change the
  Lemmings 3,000-frame hash.

### 12.22 Opera parity batch 1 (2026-10-31)

- VDLP keeps the retained `head_vdl` separate from the active successor cursor and
  follows a zero entry through its next-pointer word instead of stopping for 511 lines.
  A zero-persist successor is processed on the same line, matching Opera's
  `line_cnt <= 0` rule. The VDLP state section is version 3; v1 and v2 states migrate
  to the head/successor model. Opera latches the boundary at scanline 5 because its
  visible area starts at 21; 5DO keeps the boundary at line 0 until OP-SYS-05 aligns
  the region timing, which preserves the Lemmings smoke hash.
- ARM immediate data-processing instructions use the barrel-shifter no-carry path, so
  rotate=0 preserves C and a rotated immediate produces bit 31 of the result.
- DSP CPU-supplied FIFO reads `0xF0-0xFC` return the mirrored `0x70-0x7C` instruction
  word and no longer consume the PRNG.
- MADAM now uses the inclusive arbitrary-CEL span endpoint, stops MARIA scale/arbitrary
  drawing after the first pixel, treats `PRE0_BPP=0` as the 1-bpp group, honors packed
  `SKIPX`, reads literal LRFORM row pairs, applies blue-LSB substitution before the
  projector, carries the pre-substitution VH bits through every texel path, decodes
  REGCTL stride with the Opera byte groups, uses the decoded pixel's high components
  for `PDC_MFONLY`, and gates CLUT/video DMA on the MADAM MCTL enable bits. A literal
  `SKIPX` that exhausts the span draws nothing and still advances the origin.
- CEL origins use the Opera XYPOSH/XYPOSL model. YOXY=0 continues from the hardware
  origin, and every CEL (including culled/empty draws) advances it by VDX/VDY times
  sprite height. The `GraphicsStepY` (Samurai Shodown) and `BitTiming6` (Alone in the
  Dark) automatic mappings are retired; the enum values remain loadable for old states
  but are no-ops. The literal-CEL path always reads the actual source row.
- CLIO input FIFOs complete before the CPU observes the end address, reload only when
  the channel request bit is enabled, and reset the pointer when a new start address is
  written.
- The differential smoke still matches Lemmings at frame 300; the five-title showcase
  hashes are unchanged from the pre-batch baseline. See `TestCorpus.md` 1.1g.
- The differential harness runs the external Release CLI. Rebuild
  `src/FiveDo.Cli` in Release before collecting evidence, or the harness will silently
  exercise the previous core build.
- Remaining parity work: CD/XBUS/DIPIR/reset (OP-SYS-01, OP-SYS-03), CD-DA read-ahead,
  then region timing (OP-SYS-05) so the VDLP scanline-5 boundary can be enabled.

### 12.23 OP-SYS-02 CD poll and local reset (2026-10-31)

- The accuracy profile (`CdromTimingMode.OperaCycleBased`) now uses Opera's poll model.
  The default FreeDO-compatible mode keeps the historical register behavior.
- `POLREMASK` (`0x08`) asserts a local reset: command and transfer FIFOs, status/data
  poll bits, pending read status, and sector position are cleared; logical media
  presence is untouched. Status/data reads return zero and commands are ignored while
  reset is asserted.
- `POLRE` (`0x80`) is the media-access latch. Init sets it, releasing local reset sets
  it again, and a poll write with `0x80` clears it (write-one-to-clear). Media-access
  FIQ uses `POLMA`/`POLMAMASK`.
- Opera-mode eject reports `READY` without FreeDO's phantom tray/disc/spin bits.
- A loaded state with local reset asserted runs the same reset after the payload is
  restored.
- CD-DA read-ahead (48 sectors) and audio burst scheduling remain with SYS-01. The
  `0x40` READ_ID idle heuristic remains until command transcripts replace it.

### 12.24 OP-SYS-01 ROM overlay (2026-10-31)

- `FiveDoMemory` starts with the BIOS overlaid at address zero. While the overlay is
  active, CPU low reads return the active ROM bank and low DRAM writes clear it. The
  clear limit is the 2 MB DRAM size, so a write to VRAM alone does not clear it.
- `Reset()` reasserts the overlay. The memory state section is version 2 and saves the
  flag; version-1 states load as post-boot (overlay cleared).
- The CPU still starts at `0x03000000`; the overlay covers low-address reads until the
  BIOS's first DRAM write.
- CLIO now implements DIPIR1/DIPIR2 latches, the before-reset conversion, the DIPIR
  FIQ, and XBUS access-abort rules. The XBUS selected-device address includes the high
  select bit, Opera mode returns the full poll high nibble, and poll/command writes
  latch media access when the CD reports `POLRE`.
- DIPIR/aborts are opt-in via `EmulatorTuning.OperaDipir`. Enabling them before
  OP-SYS-03's soft-reset/clear-DIPIR path makes Lemmings stall after the first CD
  command burst, so the default and accuracy profiles leave the flag off.
- CSTAT soft reset is implemented (`OperaSoftReset`): the register follows Opera's
  reset/clear-on-zero/DIPIR-reset bits; the machine handler performs an ARM soft reset
  (SPSR_svc/R14_svc preserved, PC=0, overlay reasserted), MADAM/DSP soft resets, and
  optional XBUS/DIPIR clearing. It is also opt-in until a boot transcript validates
  the combined `OperaDipir` + `OperaSoftReset` path.
- CLIO has its own splitmix32 PRNG for register `0x17F0`; it no longer borrows the DSP
  LCG. The state is seeded from `EmulatorTuning.RandomSeed` when provided and saved in
  CLIO state version 3. The DSP LCG remains separate and FreeDO-compatible.
- CHDSharp 1.6.0 supports Zstd (`ChdCodec.Zstd`/`Cdzstd`) and ships `VendoredZSTD.dll`.
  All 589 local CHDs use CDLZMA/CDZLIB/CDFLAC, so no zstd fixture is present; no native
  dependency or code change is required.

### 12.25 FIQ latency telemetry (2026-11-02)

- The 3DO has one interrupt path: every device raises CLIO's FIQ; there is no IRQ line.
  `Arm60.Step` checks `!CPSR.F && NeedFiq` after the instruction, exactly like FreeDO's
  `_arm_Execute` and Opera's `opera_arm_execute`. Vector entry charges no extra cycles in
  either reference; do not "fix" that without a differential trace.
- `Arm60.CyclesExecuted` is telemetry only. It is not serialized, is not reset by
  `SoftReset`, and continues across state loads. `Arm60.FiqEntered` reports the cycle at
  the end of the interrupted instruction and the `LR_fiq` value.
- CLIO reports a request when `NeedFiq` changes from false to true. The observer also
  runs after writes to `0x40/0x44/0x48/0x4C/0x60/0x64/0x68/0x6C`, because enabling an
  already-active reason is when the request actually becomes pending. Repeated
  `GenerateFiq` calls while pending are level-triggered duplicates, not new requests.
- `FiqLatencyTracker` keeps at most one pending request (coalescing later reasons) and
  records the matched latency. Entries with no tracked request are counted as unmatched
  and reported with `matched=0`; this is how level-triggered re-entries after a guest
  clears F remain visible.
- `--fiq-trace` and `run --json` consume the same counters. The maximum observed on a
  3,000-frame Gex boot is 859,234 cycles; treat long windows as candidates for CD/BIOS
  analysis, not as a reason to add artificial latency.

### 12.26 DMA, FIFO, and XBUS audit decisions (2026-11-02)

- CLIO output FIFOs reload a next descriptor only when the `0x304` channel request bit is
  set. The input FIFO already had this gate; `OP-11` is now complete for both directions.
- A DIPIR-protected XBUS DMA returns before clearing `0x304` bit 20. The transfer remains
  enabled, matching Opera's `clio_handle_dma`, and the trace writes `clio dma-blocked`.
- MADAM's `_mctlWritten` flag is guest-visible state: it decides whether an unwritten MCTL
  defaults the CLUT/video DMA gates to enabled. It is cleared by cold `Reset`, kept by
  `SoftReset` (which does not clear MCTL), and serialized in MADAM section version 3.
  Version-2 states infer it from a non-zero MCTL register.
- XBUS device 15 under `OperaSemantics` now matches Opera's poll register: the status
  nibble comes from `_pollDevice`, `POLREMASK` clears the built-in status/command FIFOs,
  `POLRE` clears the ready bit, only `0x07` is stored as interrupt enables, and the READ_ID
  status is `[0x83,0,0x10,0,0x01,...,0xE1]`. The FreeDO path keeps the historical raw poll
  and `0x83`/`0x01` status layout.
- XBUS state load rejects an attached-device slot that the live controller does not have.
  The old code skipped the payload and left the section stream misaligned.
- PBUS DMA stays on the FreeDO layout: packet words begin at `DEST + 4`, `LEN` is
  decremented before the first word, and the first control word is untouched. Opera writes
  a `0xFFFFFFFF` control-port sludge word at the original `DEST` and writes packet bytes in
  the opposite order. The FreeDO layout is the shipped default and is locked by tests; an
  Opera PBUS mode needs full-collection evidence before it can be offered.

### 12.27 DSP per-tick telemetry (2026-11-02)

- `Dsp.Loop` counts every host sample tick, separates running from sleeping ticks, and
  records the peak and mean DSP instructions per running tick. The counters are telemetry
  only and are not serialized.
- The observed 3,000-frame Gex cadence is 2,205,094 ticks (735 per frame, matching
  44.1 kHz / 60 Hz), 88,955,550 instructions, mean 51.7 and peak 106 per tick. Use this as
  the baseline for CORE-05's optional worker thread and for spotting DSP hot loops.

### 12.28 ARM cycle-class ledger (2026-11-04)

- CORE-01 follow-up: `Arm60.CycleClassTelemetryEnabled` records instructions and cycles
  per `ArmInstructionClass`. It is disabled by default so the hot loop is unchanged.
  `run --cycle-classes` enables it, `run --json` emits `cycleClasses`, and the text output
  prints an `arm classes:` table.
- The ledger follows the interpreter's decode order: `Multiply`, `Swap`,
  `DataProcessing`, `Undefined`, `SingleDataTransfer`, `BlockTransfer`, `Branch`,
  `SoftwareInterrupt`, and `Coprocessor`. A condition-failed slot is counted in its
  decoded class with the one-S fetch cost, matching the reference's
  `CYCLES = -SCYCLE` entry.
- The documented parity tolerance is **zero cycles per instruction class**. The exact
  costs at N=2 (ARM60 datasheet) and N=4 (FreeDO-compatible) are:

  | Class | N=2 | N=4 |
  |---|---|---|
  | Multiply | `1 + min(16, ((bits + 5) >> 1) - 1)` on Rs | same |
  | Swap | 6 | 10 |
  | Data processing (immediate/register) | 1 | 1 |
  | Data processing (register-shifted operand) | 2 | 2 |
  | Data processing to PC (unshifted operand) | 4 | 6 |
  | Single data transfer LDR | 4 | 6 |
  | Single data transfer LDR to PC | 7 | 11 |
  | Single data transfer STR | 4 | 8 |
  | Block load, `n` registers | `n + 3` | `n + 5` |
  | Block load with PC in the list | `n + 6` | `n + 10` |
  | Block store, `n` registers | `n + 3` | `n + 7` |
  | Branch, SWI, undefined, coprocessor | 4 | 6 |
  | FIQ vector entry | 0 extra | 0 extra |

  The `n + k` rows apply the reference's wrap when `n` is zero; an empty LDM is 3/5 and
  an empty STM is 3/7.
- The ledger is telemetry only. It is not serialized, is cleared by cold reset and state
  load (when enabled from that point it sums exactly to
  `InstructionsExecuted`/`CyclesExecuted`), and `SoftReset` leaves it alone.
- Evidence: a 3,000-frame Lemmings boot with `--cycle-classes` reproduces the recorded
  ARM60 hash `0xFBED38D3` and 272,410,816 instructions. The measured averages show the
  expected N=2 reduction: SingleDataTransfer 3.98 cycles/instruction versus 6.38,
  BlockTransfer 7.42 versus 10.07, Swap exactly 6 versus 10, and SWI exactly 4 versus 6.
  See `TestCorpus.md` 1.1a.
- The ARM60 mode remains opt-in. This increment makes every class auditable and locks the
  class costs with tests; the full 594-title differential and the default decision remain
  open.
- The 2026-11-04 40-title A/B (300 frames, accuracy CLIO/CEL/CD timing, identical Opera
  captures) reports 17/40 matches with `--arm-timing arm60` versus 15/40 with
  `--arm-timing freedo`. Only two frame hashes changed, both from Diverged to Match
  (20th Century Video Almanac and Battle Chess USA, both `0x6CDFBDC5` -> `0x110E7DC5`);
  the other 38 were identical. See `TestCorpus.md` 1.1a1.
- **Decision (2026-11-04): keep the FreeDO-compatible default.** The five-title showcase
  still reports three video divergences under the accuracy profile, and the 594-title
  differential has not been run, so plan rule 7 (no default timing change without
  full-collection evidence) still blocks promoting ARM60 to the default.

### 12.29 CEL busy work accounting (2026-11-04)

- CORE-03 follow-up: Opera paces the CEL engine at 9 million rendered pixels per second
  plus 64 setup cycles per SCoB, using the larger of `g_CEL_PIXELS_DRAWN` and
  `g_CEL_TEXELS_WALKED`. 5DO already used that formula, but two 320x240 rasterizer paths
  wrote pixels through `CelMemory.WriteHalfword` without calling `AddPixels`:
  `DrawPixel` (literal-line and packed-literal packets) and `TexelDrawLine`
  (packed-repeat packets). The high-resolution branches went through `WritePixel`, which
  counted, so the error only affected the normal 320x240 path and understated busy time.
- Both direct-write branches now call `CelEngine.AddPixels(1)`. The remaining writers
  already count: `TexelDrawScale` and `FillArbitrarySpan` count in their low-resolution
  loops, and `WritePixel` counts for high resolution. `AddPixels` is still gated by
  `TrackWork`, which is only enabled in `CelTimingMode.OperaBusy`, so the FreeDO-compatible
  default is untouched.
- The deadline remains Opera's integer formula:
  `work * ArmClock / 9,000,000 + cels * 64`, with `work = max(pixels, texels)`.
  `TickCel` subtracts instruction cycles before each ARM instruction, matching Opera's
  cycle-count comparison in `opera_madam_cel_tick`.
- Regression tests: `CelRasterizerTests.PackedLine_CountsDrawnPixelsForOperaBusy`,
  `CelRasterizerTests.PackedRepeatLine_CountsEveryRetainedPixelForOperaBusy`, and
  `CelLiteralRasterizerTests.LiteralLine_CountsDrawnPixelsForOperaBusy`.
- Evidence: a 3,000-frame Road Rash boot with `--cel-timing opera --cel-trace` now reports
  full-screen lists as `cels=1 pixels=76800 busy=106730` (previously those pixels were not
  counted). The FreeDO path still produces the remembered golden frame hash `0x5E9EE515`.
  On the same fresh-NVRAM run the Opera path ended at PC `0x000B9D54`, hash `0x9512219E`,
  170,818,687 instructions. The CEL busy mode remains opt-in until a full-collection
  differential shows a net accuracy gain.

### 12.30 CLIO timer scheduler parity (2026-11-04)

- CORE-02 follow-up. The per-slot scheduler, slack gap, and cascade carry were already
  implemented behind `ClioTimerMode.OperaScheduler`; this pass closes the remaining
  register-width and observability gaps and adds a reference-model conformance test.
- Opera masks every aligned write to the timer counter/reload block (`0x100..0x17C`) to
  16 bits and reads it back masked (`opera_clio.c`, `clio_timer_reg_addr` and
  `opera_clio_peek`). 5DO stored the raw 32-bit write, so a guest that wrote a timer
  register with upper bits set could read them back. The mask now applies in
  `OperaScheduler` mode. The FreeDO-compatible default keeps FreeDO's raw `cregs`
  behavior, including the `0x120` `TIMER_VAL` compatibility write; in Opera mode `0x120`
  is timer 4's counter and follows the timer-register rule. Unaligned writes fall through
  to the plain register store in both modes, matching `clio_timer_reg_addr`.
- `Clio.TimerTelemetryEnabled` and `Clio.GetTimerUsage()` record per-timer executions,
  underflows, reloads, stops, and odd-timer FIQs for both the FreeDO `DoTimers()` scan
  and the Opera `DoTimers(int)` slot. `Clio.TimerTraceWriter` records only state-changing
  events (counter/control/slack writes, wraps, reloads, stops, and FIQs); logging every
  slot scan would emit about 3.1 million lines per emulated second at the stock clock.
- CLI: `run --clio-timer-stats` adds `timerStats` to `run --json`; `run
  --clio-timer-trace <path>` writes the bounded trace. The differential harness gained
  `--clio-timers freedo|opera` so one manifest can drive both sides of an A/B run.
- `ClioTimerSchedulerParityTests` transcribes `opera_clock_timer_queued` and
  `opera_clio_timer_execute`, including the int32 timer accumulator, slack
  recalculation, slot/gap thresholds, cascade carry, and FIQ masks. It drives both
  engines through 6,000 randomized cycle batches, timer/control writes, and slack
  changes, and requires the slot sequence, every timer/control register, the carry, and
  the telemetry ledger to match at every event (more than 50,000 slots exercised).
- Evidence: the FreeDO default still produces the Lemmings 3,000-frame golden frame hash
  `0x9C2E3236` (PC `0x00128550`, 194,135,071 instructions), identical to the
  pre-change build. A 300-frame Lemmings run with `--clio-timers opera
  --clio-timer-stats --clio-timer-trace` reports timer 14 reloading `0x3d` every
  ~12,400 ARM cycles at slack `0x150`: one scan is 15 two-cycle slots plus the
  `(4 + 336)` M25M-tick gap (200 ARM cycles), so 62 executions take 12,400 cycles.
  The mode remains opt-in until the full 594-disc differential is green; see
  `TestCorpus.md` 1.1b for the 40-title A/B result.

### 12.31 Differential timing-mode overrides (2026-11-04)

- The differential harness now accepts `--arm-timing freedo|arm60`,
  `--cel-timing freedo|opera`, and `--cd-timing freedo|opera` in addition to
  `--clio-timers` and `--profile`. Each override replaces the manifest option for every
  5DO capture, so one manifest drives both sides of a timing A/B; the Opera side is
  unchanged. Use a fresh `--out` directory (or `--force`) when changing an override,
  because `--resume` reuses cached 5DO captures without re-reading the machine options.
- Every override is recorded in report provenance (`armTimingOverride`,
  `clioTimersOverride`, `celTimingOverride`, `cdTimingOverride`, `profileOverride`) and
  in the Markdown/HTML headers, so an A/B report is self-describing.
- The 2026-11-04 CORE-08 A/B on the same 40-title, 300-frame sample reports 17/40
  matches for both `compatible` and `accuracy`, with identical per-title statuses. The
  Accuracy profile remains opt-in pending the 594-title run; see `TestCorpus.md` 1.1ak1.

### 12.32 DRAM/VRAM capacity configurations (2026-11-04)

- `MemoryConfiguration` mirrors Opera's `opera_mem_cfg_t` nibble encoding and exposes
  only the eight supported layouts: 21, 22, 41, 42, 81, 82, E2, and F1. `MemoryLayout`
  derives the DRAM size, VRAM size, VRAM base (`DramSize`), VRAM mask, and the
  high-resolution backing size.
- VRAM sits directly above DRAM, as in Portfolio OS `FindRamSize` and Opera
  `_setup_dram_vram`. Address decoding uses `Layout.RamSize` instead of the historical
  fixed 3 MB boundary. VDLP, SPORT, CEL pointer validation, and the CEL high-resolution
  plane stride all read the live layout.
- MADAM register 0x004 (RED SYSBITS) is capacity-derived and read-only. The values match
  `opera_mem_madam_red_sysbits(MADAM_SYSBITS_RED)`: 21→0x29, 22→0x2A, 41→0x31,
  42→0x32, 81→0x71, 82→0x72, E2→0x62, F1→0x61. The BIOS/Portfolio `FindMemSize`
  routine reads this register, so no BIOS patch is required for a homebrew program to
  discover expanded memory.
- The RAM backing is always the maximum 22 MB (14 MB DRAM plus four 2 MB VRAM planes).
  The memory state section is version 3 and records the layout plus the allocation size.
  Version-1/2 sections carry the historical 19 MB stock backing and load with the stock
  layout. The Options section version 8 records the capacity; loading a state with a
  different layout switches the machine after the memory payload is restored.
- The ROM overlay clear limit stays at `DramSize`, matching Opera's
  `g_ROM_OVERLAY_CLEAR_LIMIT`. For the stock layout this is still 2 MB, so existing
  golden hashes are unchanged.
- Retail titles are not expected to boot with expanded memory (Opera documents the
  option as homebrew-only); the `MemoryConfigurationTests.HomebrewProbe_*` test ROM
  verifies that a guest sees and uses 4 MB DRAM plus 2 MB VRAM.

### 12.33 Portfolio OS SWI HLE (2026-11-04)

- `ArmSwiHle` is a direct port of `opera_swi_hle_0x5XXXX.h` and
  `opera_fixedpoint_math.c`. The SWI comment is masked with `0x000FFFFF` exactly as
  Opera's `decode_swi_hle`; unknown comments fall through to the normal `0x08` trap.
- Opera's switch leaves `0x50003`, `0x50004`, `0x5000A`, `0x5000B`, and `0x5000D` to
  the guest. Do not add no-op HLE cases for them: the reference `break`s out to
  `decode_swi_lle`, so the guest handler must run.
- The host routines use `IRam.Read32`/`Write32` rather than the CPU bus. The guest
  pointers are byte addresses into the same little-endian RAM representation, and
  `IRam` handles unaligned accesses the way the C reference's byte pointers do.
- All arithmetic is 64-bit intermediate, arithmetic `>> 16`, and truncating division,
  matching the C reference and `MulSF16` ("lower bits are truncated"). `sqrt_frac16`
  intentionally keeps the reference's 17-iteration `do/while` and signed shifts; it is
  not a general-purpose square root.
- `Arm60` charges the same SWI instruction cost as the trap (`Scycle + NCycle`) and
  then returns with the next PC in place. It does not charge the skipped guest
  handler's cycles or reproduce its register clobbers. That is why HLE is a
  performance mode, not a timing model.
- Evidence: the C reference and 5DO agree on `MulVec3Mat33`, `MulMat33Mat33`,
  `MulVec3Mat33DivZ`, `MulVec4Mat44`, `MulManyVec4Mat44`, and `MulMat44Mat44` for
  non-trivial signed inputs. The real-BIOS 4,096-call benchmark is in
  `Performance.md` §8.2.

### 12.34 RSA/unsigned-disc BIOS patch (2026-11-06)

- The retail BIOS checks the media RSA signature at two sites. The community documented
  patch changes each site's second word from `000111A0` to `000011A0`; the similar
  `000213A0000111A0` sequence is a guard and must not be changed. `BiosRsaPatch` searches
  and patches only the first pattern. Reference: Boojakascha's FZ-1 RSA-check writeup.
- Applying the patch to retail `panafz10.bin` and `panafz10e-anvil.bin` reproduces
  `1477bda80dc33731a65468c1f5bcbee9` and `cf11bbb5a16d7af9875cca9de9a15e09`, the
  known No-RSA images catalogued by Opera/libretro. This is checked by local-file tests
  when the images are present.
- 5DO never writes the BIOS file. The patch is applied to the in-memory copy only when
  `--allow-unsigned-discs` or Settings > Paths > Allow unsigned discs is enabled. The
  effective ROM MD5 is what `FiveDoMachine` hashes into save states, so a state saved with
  a patched BIOS and loaded with an unpatched one reports the normal BIOS-hash mismatch
  warning.
- Selection prefers a recognized No-RSA image for `--bios-auto`/`bios select` when the
  option is on. Unknown file names containing `norsa`, `no-rsa`, `rsa disabled`, or
  `unsigned` are accepted as already No-RSA; a retail image with no patch site is
  rejected with an explicit error rather than silently claiming support.

### 12.35 Exact NTSC field clock and rational VDL (2026-11-06)

- FreeDO's integer `ARM_CLOCK / 60` (208,333 cycles) and `VDL_CLOCK = 526 * 30`
  (15,780 lines/s) model NTSC at exactly 60.00 Hz. The hardware and Opera use the
  NTSC color field rate 60000/1001 (59.94 Hz): a field is 208,541.67 ARM cycles
  and the reference clock accumulates scanlines with a 16.16 rational.
- 5DO now carries `FieldRateNumerator`/`FieldRateDenominator` on
  `VideoRegionProfile` and computes the frame budget as
  `armClock * denominator / numerator`. PAL stays 50/1. `FramesPerSecond`
  exposes the exact value for host pacing; the nominal `FieldRate` remains
  60/50 for display and JSON compatibility.
- `Quartz` replaced FreeDO's 8.24 VDL accumulator with an exact rational one:
  each cycle adds `fieldLines * numerator` to a 64-bit accumulator and a
  scanline is due at `armClock * denominator`. This also removes the per-batch
  ceil() error in FreeDO's 8.24 scheme, which ran the scanline clock ~0.09%
  fast (15,794 events per second instead of 15,780).
- Quartz state is version 4. The historical 8.24 VDL word is still written for
  older readers, and version 1-3 states convert that fraction on load. The
  exact accumulator is appended for version 4.
- Evidence: at 300 frames the REFERENCE and 5DO sample counts now differ by
  1-8 samples instead of 43-212; all 50 panel titles are within 9 samples at
  frame 300. Space Ace matches Opera's 3,000-frame hash and Road Rash matches
  Opera frame 3002. The remaining PC/CPSR frame-boundary difference is a
  phase sample, not a clock offset; see `TestCorpus.md` 1.1ax.

### 12.36 VDLP blanking and background color (2026-11-06)

- Opera's `vdlp_render_line_*` writes a **black** line when bitmap DMA is
  disabled (`!bitmap_dma_enabled()`). 5DO kept the previous field's pixels and
  only refreshed the line metadata, so a blank boot/loading field retained an
  old green background. `Vdlp.DoLine` now clears the visible pixels and the
  background in both the low- and high-resolution paths when video DMA is off.
- The background command (`0xE?xxxxxx`) is 8 bits each of red, green, and blue
  in bits 23-16, 15-8, and 7-0. The old parser byte-swapped the word and stored
  an 8-bit-per-channel value, while `FrameAssembler` reads the field as a 5-bit
  0RGB1555 color. That swapped red/blue and mixed the green channel with its
  neighbour. The parser now narrows each 8-bit component to 5 bits with the same
  shift as Opera's `background_to_0RGB1555`.
- Remaining difference: Opera's XRGB8888 renderer returns the raw background
  word for pixel 0, preserving 8-bit precision, while 5DO's line format is
  5-bit. A near-black background is `(1,1,1)` in Opera and `(0,0,0)` in 5DO, so
  frame hashes differ on otherwise-black boot screens. This is a color-precision
  difference, not a game-visible defect; a future 8-bit background field would
  remove it without changing the rest of the pipeline.
