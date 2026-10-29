# Glossary

Terms used throughout the 5DO documentation. Hardware names are capitalized as in the
3DO documentation; emulator-specific terms are marked.

---

## A

**Accurate (filter)** - The pixel-perfect nearest-neighbor presentation mode that
matches the CPU `FrameAssembler`. Used as the GPU parity oracle.

**AEL (Application Engineering Library)** - 3DO's portfolio/application library; not
emulated as a separate component.

**Anvil** - A BIOS variant detected by `byte[28] == 234`. The core applies a one-shot
PC adjustment (`+64`) on the first instruction and reports `anvilKind = 30`.

**ARM60** - The 3DO's 32-bit ARM CPU core, emulated by the `Arm60` interpreter at
~12.5 MHz. Not the same as the host .NET JIT.

**Aspect mode** - `Integer`, `FourThree`, `Stretch`, or `Custom`; controls the
presented rectangle (`ViewportCalculator`).

**Audio tap** - `FIVEDO_AUDIO_TAP=<prefix>` records the exact PCM queued to OpenAL
(`<prefix>.wav`) plus per-buffer timing (`<prefix>.csv`).

**Autopilot** - The Road Rash-specific input driver (`--autopilot road`); a test
harness, not a general game AI.

---

## B

**BIOS1 / BIOS2** - The two 1 MB ROM banks at `0x03000000`. BIOS1 is required; BIOS2 is
optional and selected by CLIO register `0x84`. `panafz1.bin` is the common BIOS1.

**BIN** - A raw CD image with 2352-byte sectors (sync/header + 2048-byte user data).
The 3DO payload is at offset 16 for Mode 1 and 24 for Mode 2 Form 1.

**Bitmap (VDLP)** - A display-list mode that copies indexed pixels from a VRAM bitmap
into a `VdlLine`.

**Bloom** - A CRT filter glow around bright pixels; 3x3 native-resolution kernel,
controlled by `RendererSettings.Bloom`/`BloomThreshold`.

**Bluetooth audio** - A downstream link after OpenAL. The tap isolates emulator/queue
problems from Bluetooth codec/link problems.

---

## C

**CCB (CEL Control Block)** - The command structure that describes a CEL: flags,
preamble words, source/destination, geometry, and next pointer. Parsed by `CcbParser`
and `CelEngine`.

**CD-ROM / MEI** - The 3DO's Mitsumi/MEI CD drive, modeled as an XBUS device by
`CdromDevice`. It implements the MEI command set, poll/status bits, TOC/MSF/LBA, and
sector streaming.

**CEL** - A 3DO sprite/bitmap primitive rendered by MADAM. CELs can be packed, literal,
LR-form, or arbitrary quads.

**CHD** - MAME's compressed disc image format. 5DO reads CD CHDs through the pure C#
CHDSharp library and extracts the 2048-byte user payload.

**CLIO** - The 3DO's I/O companion chip: timers, FIFOs, DMA, interrupts, XBUS window,
DSP interface, ROM bank select, and controller/PBUS handling.

**CLUT** - Color Lookup Table. Each VDL line has 32-entry R/G/B tables; pixel bits
select entries. A fixed grayscale CLUT is used when out-control bit 25 and pixel bit 15
are set.

**CUE** - A text sheet describing tracks/files. 5DO supports a single data track
(`MODE1/2048`, `MODE1/2352`, `MODE2/2352`, `MODE2/2336`).

**CoreRunner** - The host class that runs `FiveDoMachine.RunFrame` on a dedicated core
thread, paces it, and marshals UI actions through `Invoke`.

**CRC-32** - The section checksum used by `.tdos` (IEEE 802.3, reflected `0xEDB88320`).

**CRT** - The scanline/aperture/curvature/vignette/bloom presentation filter.

---

## D

**DiagPort** - The 3DO diagnostic port at `0x03180000`; word read/write only.

**Disc hash / Game ID** - MD5 of the first two 2048-byte disc sectors, lowercase hex.
Used by the 4DO database and save-state header.

**DMA** - Direct Memory Access. CLIO moves data between XBUS devices and RAM; MADAM has
its own CEL/PBUS DMA.

**DSP** - The 3DO audio DSP (a 16-bit fixed-point processor) with NMem/IMem, semaphore,
multiplier/ALU/shifter, and 44.1 kHz stereo output. Emulated by `Dsp`.

**DspSema** - CLI JSON field combining DSP semaphore status/data:
`(status << 16) | data`.

---

## E

**EASU** - AMD FSR 1.0's edge-adaptive spatial upsampling pass.

**EmulatorTuning** - Immutable machine configuration: ARM clock, fix mode, CPU backend,
high-resolution flag.

**External CEL** - A CEL whose pixel data is outside the CCB; the engine reads it
through the bit reader.

---

## F

**FIQ** - Fast Interrupt Request on the ARM60. CLIO raises FIQ reasons for V0/V1,
timers, DSP, CD, and DMA.

**Fix mode** - FreeDO/4DO compatibility flag bits (`BitTiming1`...`GraphicsStepY`) that
work around per-game timing/graphics quirks. Stored in save states and per-game
overrides.

**FrameAssembler** - The CPU palette-expansion/frame-assembly oracle. The GPU
`Accurate` mode must match it pixel-for-pixel.

**FrameMailbox** - See `VdlFrameMailbox`. Holds the latest completed frame; drops stale
frames instead of queueing.

**FSR (FidelityFX Super Resolution) 1.0** - AMD's spatial upscaler: EASU + RCAS.
5DO implements it as an opt-in two-pass GLSL filter.

---

## G

**Game ID** - See disc hash. The first eight uppercase characters are the 4DO database
key used for fixes.

**GL / GLES** - Desktop OpenGL 3.3 and OpenGL ES 3.0/3.1. The presenter has a shared
two-stage pipeline with GLSL 330 / GLSL ES 300 variants.

**GL probe** - `--gl-probe` measures the real Avalonia GL presentation path and writes
timings/capabilities/parity JSON.

---

## H

**High-resolution mode** - The opt-in 640x480 CEL/VDLP mode (`--high-res`), using four
1 MB VRAM planes. It is not the default and not 4K internal rendering.

**HQx** - hq2x/hq3x/hq4x pixel-art smoothing (Maxim Stepin/Cameron Zemek), implemented
as an opt-in CPU filter before sharp-bilinear finishing.

**HLE** - High-level emulation. 5DO does not use HLE for the BIOS or OS; it emulates the
hardware.

---

## I

**IMem** - DSP instruction memory (1024 words).

**Interrupt sink** - `IInterruptSink`, implemented by CLIO; chips raise FIQ through it
instead of calling CLIO directly.

**ISO** - A 2048-byte-sector disc image. 5DO maps LBA directly.

---

## J

**JIT** - In this project, an optional guest ARM dynamic recompiler. .NET's own JIT is
not the guest JIT. The interpreter is the default/reference backend; no guest JIT is
implemented.

---

## K

**KPRINT** - The 3DO kernel debug print register. `--kprint`/`--kprint-pc` capture it;
`--stop-kprint` stops a trace on a message.

---

## L

**LBA** - Logical Block Address, a 2048-byte sector index.

**LDM/STM** - ARM block load/store instructions; the interpreter preserves the
reference's LDM quirks.

**Libopera / Opera** - The libretro Opera core, used as the primary behavioral
reference for new compatibility work.

---

## M

**MADAM** - The 3DO graphics chip: CEL engine, pixel decoder/processor/projector,
matrix engine, PLUT, PBUS, and DMA. Emulated by `Madam`.

**Mailbox** - The latest-frame handoff from the core thread to the renderer.

**MEI** - The CD drive command set (see CD-ROM).

**Modulo** - The VDLP line width in bytes/pixels; selects 320/384/512/640/1024-wide
bitmap modes.

**MSF** - Minute:Second:Frame addressing used by CD commands/TOC.

---

## N

**NVRAM** - 32 KB of battery-backed console storage, mapped one byte per 32-bit word by
FreeDO. Stored as `nvram.ram`; included in save states.

**NMem** - DSP data memory (2048 words).

---

## O

**OpenAL Soft** - The bundled 1.23.1 audio runtime (`OpenAL32.dll` on Windows). The
context is made current on the audio fill thread.

**Out-control** - The VDL line control word; bit 25 enables the fixed grayscale CLUT.

---

## P

**PBUS** - The controller packet bus. The machine samples a 16-byte packet at V1;
controllers are ordered 1, 3, 2, 5, 4, 6.

**PLUT** - Palette Lookup Table used by CEL decoding.

**Portfolio** - The 3DO's multitasking OS; the BIOS runs it from ROM.

**Prefetch** - The opt-in bounded disc read-ahead cache (`--prefetch`).

**Presentation** - Turning a completed `VdlFrame` into a window image; GPU or CPU.

---

## Q

**Quartz** - The 8.24 fixed-point scheduler for ARM/VDL/timer events, plus the exact
44.1 kHz DSP clock. Ported from `quarz.cpp`.

---

## R

**RCAS** - AMD FSR 1.0's robust contrast-adaptive sharpening pass.

**RID** - .NET Runtime Identifier, e.g. `win-x64`, `linux-arm64`, `osx-arm64`.

**ROM bank** - BIOS1/BIOS2 at `0x03000000`; selected by CLIO register `0x84`.

---

## S

**Save state (.tdos)** - Native versioned machine snapshot with CRC'd sections. About
25 MB because it includes the full RAM backing and VDL frame.

**Sharp-bilinear** - The default enhanced filter: integer prescale plus bilinear
sub-texel blending.

**SPORT** - The 3DO VRAM fill/copy engine.

**SPSC** - Single-producer/single-consumer; the audio ring buffer design.

**State section** - One device's payload in a `.tdos`, framed by ID/version/length/CRC.

**Swap interval** - The platform vsync control (WGL/GLX/EGL). 5DO reports whether the
extension accepted the request.

---

## T

**TDO1** - The `.tdos` magic (`0x314F4454` little-endian).

**Thumbnail** - The PNG preview written next to a save slot (`slot-NN.png`).

**TOC** - Table of Contents on a CD; used by READ_TOC.

**Trimmed** - `PublishTrimmed=true`; removes unused IL. Both executables publish
trimmed by default.

---

## U

**Upscaling** - Rendering the native 320x240/640x480 frame at a larger output size. 4K
is output upscaling, not 4K internal CEL rendering.

---

## V

**VDL / VDLP** - The 3DO display-list processor. It parses VRAM display lists into
`VdlLine`s (pixels, CLUT, out-control, background).

**VdlFrame** - A reusable frame of 960 `VdlLine`s plus source dimensions.

**VdlLine** - One parsed scanline: up to 1280 indexed pixels, 32-entry R/G/B CLUT,
out-control, CLUT DMA, background.

**VRAM** - The 1 MB visible video RAM region inside the 3 MB DRAM/VRAM address space.
High-resolution mode uses four 1 MB planes.

**Vsync** - Vertical synchronization; see swap interval.

---

## W

**WAV capture** - 44.1 kHz/16-bit/stereo PCM written by `WavAudioSink`; exact raw DSP
output (no soft limiter).

**Watchpoint** - `--watch-lowmem` logs RAM writes in a range, with PC/registers/stack.

---

## X

**xBR** - Hyllian's edge-directed 4x pixel-art filter, implemented as an opt-in
GPU-palette-expanded 4x shader pass.

**XBUS** - The 3DO expansion/device bus. Device 0 is the CD-ROM; CLIO owns the
select/poll/FIFO window.

---

## Z

**Zero-allocation steady state** - The benchmark/probe target of 0 managed bytes/frame
after warm-up on the core/presenter paths.

---

## Release terms

**Documented-compatibility release** - v1.0 ships with a validated Windows x64 target
and a documented 12-title corpus; full 3DO library compatibility is not claimed.

**Golden corpus** - The five locally validated showcase titles: Wolfenstein 3D,
Lemmings, Road Rash, Alone in the Dark, Space Ace.

**Soak** - A long real-content run (typically 36,000 frames = 10 minutes) that checks
stability, DSP liveness, and deterministic state.

**Tier A / B / C** - Performance hardware classes: A = mid-range x64 desktop; B =
Raspberry Pi 4/5/integrated x64; C = older/low-power. See [`Performance.md`](Performance.md).

**Validated vs implemented** - Implemented means the code path exists and is tested;
validated means it was exercised on the stated hardware/content with recorded evidence.
