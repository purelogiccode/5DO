# Porting Notes: FreeDOCore C++ to C#

This document is the working contract for porting `References/4DO-master/FreeDOCore` to managed C#. It records the representation choices, the C++ constructs that need care, and the known FreeDOCore quirks that must be preserved for compatibility.

**Primary reference:** `References/4DO-master/FreeDOCore/FreeDO`
**Secondary reference:** `References/opera-libretro-master/libopera` (a cleaned-up FreeDO derivative; useful for cross-checking structure and comments, not as a source of different behavior)
**Hardware documentation:** `References/Docs3/arm60.pdf`, `References/Docs3/tc528267.pdf`, `References/Docs3/emu-tech-spec.html`, `References/Docs3/uncd-rom.zip`

---

## 1. Non-negotiable porting rules

1. **Preserve observable behavior first.** Do not "clean up" timing, register semantics, or pixel math during the initial port. Refactors come after differential/golden tests exist.
2. **No native emulator code.** The product must not P/Invoke `FreeDOCore.dll`, use C++/CLI, or load a native core. The original DLL may be used only by an optional, developer-only oracle harness outside the product.
3. **No mutable global state.** Every C++ global/static becomes a field on the owning class. One `ThreeDoMachine` instance must be independent of another (tests run in parallel).
4. **No file I/O in the core.** BIOS, disc, NVRAM, and save-state bytes are supplied by the host.
5. **No wall clock in the core.** Time is advanced only by `Quartz.PushArmCycles`.
6. **Explicit serialization.** Never serialize a C# object with `Marshal`, `BinaryFormatter`, or field layout assumptions. Save states use explicit little-endian reads/writes.
7. **Comment traceability.** Every ported method starts with a comment such as `// FreeDO: Madam.cpp: _madam_HandleCEL`.
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

### 8.1 `_3do_sys.cpp`

- `_3do_Init` order matters: ARM init, ROM read + byte swap, VDLP init with VRAM pointer, SPORT init, MADAM init, XBUS init + device reset, CLIO init with reset reason `0x40`, DSP init, frame init, diag init, quartz init.
- `biosanvil`/`isanvil`: if `isanvil == 30`, set `biosanvil = 1` before ARM init. The ARM core then skips 64 bytes on the first instruction and sets `isanvil = 1`.
- `_3do_Frame`: the `cnt >> 4` threshold and `12_500_000 / 60` loop are compatibility behavior. Do not replace with a "more accurate" scheduler without golden tests.
- `_3do_SaveSize`/`_3do_Save`/`_3do_Load`: the native format is a 16-word offset table with magic `0x97970101`. The C# port uses a new versioned format (see `docs/ImplementationPlan.md`), not this layout.
- `_freedo_Interface`: replace with typed methods on `ThreeDoMachine` and `EmulatorSession`; do not keep an integer-procedure API in the product.

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
- `_mem_write8/16/32` and `_mem_read8/16/32` are raw RAM helpers, not bus accesses. Keep them on `ThreeDoMemory`.
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
- `_arm_Reset` also reset CLIO and MADAM in C++. In the port, `Arm60.Reset` only resets CPU state; `ThreeDoMachine` owns the reset order so each chip can be tested independently.
- `_arm_Init`/`_arm_Destroy` NVRAM callbacks are host responsibilities (`ThreeDoMemory`/`EmulatorSession`), not CPU responsibilities.
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
  - `CCBFLAGS` bits and `PRE0`/`PRE1` bits are documented in the C++ source; copy them as named constants.
  - `LoadPLUT` reads 16-bit values with `_mem_read16((((pnt >> 1) + i) ^ 1) << 1)`. Port the exact XOR/scale.
  - `HandleDMA8`/`DMAPBus` copy the PBUS queue to memory and raise FIQ.
  - `KeyPressed` copies up to 16 bytes and fills the rest with `0xFF`.
- High-resolution (`RESSCALE`) is not part of the first port. When added, it touches `ThreeDoMemory` mirroring, VDLP line duplication, SPORT copies, CEL stepping, and frame extraction. Do it as one milestone.

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
- `fastrand` is `g_seed = 69069 * g_seed + 1; return g_seed & 0xFFFF;` with seed `0xA5A5A5A5`.
- `DSPPCNT`/`DSPPRLD` = 567. Each `Loop` subtracts 567 and wraps; this controls sample cadence.
- Sample output is `(IMem[0x3FF] << 16) | IMem[0x3FE]`. The host interprets the packed 16-bit stereo pair; verify channel order with the WAV sink.

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
  - For `y = line - 16` in `[0, 240)`, if `enadma`, copy 320 pixels from `PREVIOUSBMP`/`CURRENTBMP` into the frame line (two lines in high-res), and copy CLUT/control/background words.
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
- `PushARMCycles` uses 8.24 fixed point:
  ```cpp
  arm = (clks << 24) / ARM_CLOCK;
  qrz_AccARM += arm * ARM_CLOCK;
  if ((qrz_AccARM >> 24) != clks) { arm++; qrz_AccARM += ARM_CLOCK; qrz_AccARM &= 0xFFFFFF; }
  qrz_AccDSP += arm * SND_CLOCK;
  qrz_AccVDL += arm * VDL_CLOCK;
  if (_clio_GetTimerDelay()) qrz_TCount += arm * (21000000 / _clio_GetTimerDelay());
  ```
- `clks << 24` is a 32-bit operation in C++; use `uint` and document the expected range. Add a debug assert that `clks < 256`; if a caller exceeds it, investigate before changing the math.
- `QueueVDL/QueueDSP/QueueTimer` each consume one `0x1000000` unit and return true at most once per call.

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
  - the CPU and GPU outputs must hash identically in "Accurate" mode.
- HQX/xBR filters are GPU shader passes in `ThreeDo.Video.OpenGL`. They are presentation-only and must never feed back into emulated state.
- Do not remove the CPU `FrameAssembler`; it is used by CLI/headless tests, as a fallback, and as the golden oracle for the shader.

### 8.12 CHD storage (new, no FreeDOCore equivalent)

FreeDOCore only knows about a flat 2048-byte-sector image supplied by the host. CHD support is a host/storage feature:

- `ThreeDo.Storage.Chd.ChdDiscImage` uses **CHDSharp** (`ChdFile`) to open MAME CHD files.
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
- [ ] `FIX_BIT_TIMING_3`: Dinopark Tycoon scale Y * 5.
- [ ] `FIX_BIT_TIMING_4`: reserved.
- [ ] `FIX_BIT_TIMING_5`: Microcosm VDL current/previous bitmap swap.
- [ ] `FIX_BIT_TIMING_6`: Alone in the Dark literal CEL Y step.
- [ ] `FIX_BIT_TIMING_7`: reserved.
- [ ] `FIX_BIT_GRAPHICS_STEP_Y`: Samurai Shodown CEL Y step.
- [ ] `cnbfix` one-shot PC jump at `0x94D60`.
- [ ] `speedfixes`, `unknownflag11`, `sdf`, `sf` CEL timing state.
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
- **High-resolution CEL rendering** is a later emulation feature (M11), separate from GPU output upscaling. Do not conflate the two: output upscaling is presentation-only; high-res CEL changes emulated rendering.

---

## 10. What not to port

- `Worker.cpp` (unused Win32 thread helper).
- `stdafx.h` / MSVC precompiled-header plumbing.
- `freedocore.h`'s integer-procedure C ABI. Replace with typed APIs.
- `__declspec(dllexport/dllimport)`.
- `#pragma pack` save-state structs. Replace with explicit state sections.
- `malloc/new` ownership patterns. Use managed arrays and classes.
- Windows-only logging (`OutputDebugString`, `sprintf` buffers). Use `ILogSink`.

---

## 11. Definition of a correct port

A ported function is correct when:

1. It has a C# counterpart with the same observable behavior.
2. Its tests cover normal, boundary, and malformed inputs.
3. It matches the FreeDO reference on a trace/golden test where one exists.
4. It does not introduce a new allocation in the steady-state hot path.
5. It does not change a global into shared mutable state.
6. Its comments identify the original function and any deliberate deviation.

If a deviation is required for C# safety (e.g., division-by-zero guard), document it in the test and in this file.

---

## 12. M1 implementation notes

These notes record decisions made while implementing the core/memory/scheduler milestone. They are not deviations from observable FreeDO behavior unless stated.

### 12.1 Memory allocation and lanes

- `ThreeDoMemory` backs RAM/VRAM with a `uint[]` sized to `3 MB + 16 MB` (19 MB total). Only the first 3 MB are CPU-visible; the extra 1 MB planes are reserved for the future `RESSCALE` mirrors (M11). `IRam.Size` reports the full backing size; `ThreeDoMemory.VisibleSize` reports the 3 MB window.
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

- `PushArmCycles` keeps the reference's 8.24 accumulators and its `arm++` correction. It expects small cycle batches (the reference computes `clks << 24` in 32 bits); `Debug.Assert` documents the `< 256` contract.
- One second of ARM cycles (12.5M) produces 15,780 VDL line events, i.e. 30 full 526-line cycles and **60 half-frame presentations**. Tests assert 60 half-frame transitions.
- The reference's coarse-batch rounding slightly over-counts DSP/VDL/timer events for a given batch size. Tests use 255-cycle batches and assert the exact reference counts (44,139 DSP samples, 15,794 VDL lines, 21,018 timer events for a 1,000-unit timer delay). Do not "fix" this rounding without golden evidence; the machine's normal batch size comes from `_3do_Frame`'s `cnt >> 4` threshold.

### 12.5 Diagnostics

- `Log` is an instance owned by the machine/host; there is no mutable static logger. `Log.Null` is a shared immutable instance.
- The interpolated-string overload uses `LogInterpolatedStringHandler`, so disabled messages do not format or allocate.
- `InMemoryLogSink` is bounded and thread-safe; `ConsoleLogSink` is intended for host/debug use.

### 12.6 M3 implementation notes

- `ThreeDoMachine` is the composition root. It owns the reset order and the FreeDOCore frame loop. `RunFrame` keeps the reference's `cnt >> 4` batching threshold; `LastFrameCycles` exposes the actual per-frame budget for tests.
- `Clio` implements `IClioBus`, `IInterruptSink`, `IClioFifo`, and `IStateSection`. FIFO descriptor writes use the reference's `value + 4` length encoding, including the zero-length special case for input FIFOs. The DMA loop keeps the `len >= 0` and `0xFFFFFFFC` sentinel semantics.
- `Madam` implements the register file, CEL FSM/status, VDL head hook, matrix engine, and PBUS DMA. The matrix engine latches results in temporary fields, so a following command `0` (NOP) exposes the previous computation, matching `Madam.cpp`.
- `Vdlp` reads the display list and bitmap through the +2 MB VRAM window. `DoLine` copies from `PREVIOUSBMP` (not `CURRENTBMP`), advances both bitmap pointers with the reference's `MODULO * 4 - 2` rule, and clears `OUTCONTROLL` bit 0 after each line.
- `Sport` adds the +2 MB VRAM window to every fill/copy address, matching `_sport_Init(Memory + 0x200000)`.
- `Xbus` is an M3 stub: it implements the controller, device slots, poll/status/data FIFOs, and the built-in device-15 `0x83` responder. The CD device arrives in M6.
- `DspStub` stores NMem/IMem writes, implements the reference PRNG (`69069 * seed + 1`), and returns silence. The full DSP core arrives in M5.
- `BiosImage`/`NvramImage` live in `ThreeDo.Storage`. BIOS2 is zero-filled to 1 MB by the machine when shorter; NVRAM initialization writes the big-endian volume header and the initial directory words from 4DO's `EmulationHelper`.
- `FrameAssembler` expands the 5-bit background to 8 bits and always writes an opaque alpha byte. Crop detection and HQx scaling remain presentation-layer work.
- `M3.13` (the early OpenGL window spike) is deferred to M9R because it needs a desktop GL context; the CPU frame path and machine loop are fully testable without one.
