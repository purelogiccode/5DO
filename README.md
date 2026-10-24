# 3DOEmulator

A clean-room-style, **pure C# / .NET 10** 3DO emulator, originally ported from FreeDO/4DO, with **Opera/libopera as the primary behavioral reference** for new compatibility work, a cross-platform Avalonia frontend, a headless CLI, and a deterministic test harness.

> **Working title only.** The final product name is not decided yet. The solution is called `3DOEmulator`; source namespaces use the placeholder prefix `ThreeDo` because C# identifiers cannot begin with a digit.

## Status

**Current position (2026-10-24):** M0-M8 are complete. The M9 Avalonia shell, the
M9R two-stage OpenGL presenter, the M9P dedicated core thread/frame pacer, and the
M10 CI/publish pipeline are implemented as a working prototype. **539 tests pass.**

Real-content status with `panafz1.bin`: **Lemmings** boots to CyberIntro and renders
the 3D intro; **Space Ace** boots the ReadySoft player and renders the demo intro;
**Road Rash** and **Wolfenstein 3D** load their own OS but stop at SAudio channel 0
initialization; **Alone in the Dark** fails earlier in AudioFolio initialization.

The 2026-10-24 session fixed a real DSP-execution bug (headless runs never executed
the DSP when no audio sink was attached) and decoded the remaining blockers:
SAudio `-6996` is `SAudioErrAllocInstFailed`, and Space Ace's frame-681 runaway is
the player's audio task copying to a null output buffer after its mixer setup fails.
Both point at the DSP `AllocInstrument` semaphore handshake, which the ARM still
does not see acknowledged. **Opera runs all four problem titles past those points**
with the same BIOS and disc data, so the remaining failures are ours to fix, not a
shared FreeDO limitation. The next session is a focused DSP differential against
Opera, not a UI or 4K-tuning pass. CPU-side performance is ~2.1-2.3 ms/frame
(1.63 ms/frame on the 2026-10-24 synthetic bench), leaving ample headroom at 60 Hz;
desktop 4K60 is high confidence, while the GPU/p99 and Tier B numbers still need
measurement.

Remaining major work: differential port from Opera (DSP/audio first, then CD-ROM,
CLIO, MADAM, and ARM timing), real-game hardening, GPU 4K/p99 validation,
GLES/ANGLE/Pi validation, arm64/installers, filter polish, and the legal/attribution
review. See [`docs/Status.md`](docs/Status.md) for the completion estimate and
[`docs/NextSession.md`](docs/NextSession.md) for the next-session plan. The
implementation plan and architecture are in:

- [`docs/Status.md`](docs/Status.md) - current position, completion estimate, and next steps.
- [`docs/NextSession.md`](docs/NextSession.md) - first task, commands, and acceptance criteria for the next session.
- [`docs/ReferenceStrategy.md`](docs/ReferenceStrategy.md) - Opera/FreeDO reference hierarchy and differential procedure.
- [`docs/ImplementationPlan.md`](docs/ImplementationPlan.md) - milestones, tasks, acceptance criteria, risks.
- [`docs/Architecture.md`](docs/Architecture.md) - project layout, dependency rules, runtime composition.
- [`docs/PortingNotes.md`](docs/PortingNotes.md) - C++ to C# porting rules and reference quirks.
- [`docs/TestStrategy.md`](docs/TestStrategy.md) - unit, integration, golden, differential, and performance testing.
- [`docs/Compatibility.md`](docs/Compatibility.md) - BIOS, disc, input, and game-fix compatibility notes.
- [`docs/TestCorpus.md`](docs/TestCorpus.md) - local BIOS/disc corpus and real-content validation commands (not committed).
- [`docs/Performance.md`](docs/Performance.md) - 4K60 budgets, presentation pipeline, and measurement plan.

## Goals

- Emulate the Panasonic 3DO Interactive Multiplayer accurately enough to boot the BIOS and run real software.
- Keep the emulation core **100% managed C#**; no C++/CLI, no native emulator DLL, no P/Invoke into FreeDOCore.
- Run on **Windows, Linux, and macOS**, on **x64 and arm64**.
- Provide an **Avalonia** desktop frontend and a **headless CLI** for automation, CI, and debugging.
- Be **deterministic**: same BIOS + disc + input sequence + options produces the same frames and state.
- Keep the codebase modular: one project per console subsystem, one class per major responsibility.

## Disc formats

- ISO (2048-byte sectors)
- BIN (2352-byte sectors)
- CUE+BIN (single data track)
- **CHD** via the pure C# **CHDSharp** library, including parent/child images and read-ahead decompression

CHD is a first-class v1 format. The storage layer presents every container through the same `IDiscSource` 2048-byte-sector interface, so the CD-ROM emulation is identical regardless of file format.

## Differentiator: OpenGL/GLES presentation

The desktop frontend uses **Silk.NET.OpenGL/OpenGLES** through Avalonia's OpenGL control as the **required** presentation path:

- Two-stage pipeline: palette expansion at native resolution (320x240), then GPU upscaling/filtering at output resolution.
- Pixel-accurate "Accurate" mode that matches the CPU frame assembler.
- High-quality GPU upscaling to 1080p/1440p/4K: nearest, sharp-bilinear, CRT, and xBR/HQx-family filters.
- Integer scaling, aspect-ratio control, fullscreen, vsync, and screenshots.
- Desktop GL 3.3 (WGL/GLX/EGL/macOS) and GLES 3.0/3.1 (ANGLE, Raspberry Pi) variants.
- CPU `WriteableBitmap` fallback only when neither GL 3.3 nor GLES 3.0/3.1 is available.

The emulation core still renders CELs in software for accuracy. The GPU accelerates presentation, palette expansion, scaling, and filters; it does not change emulated state.

## Performance target

- **4K (3840x2160) 60 fps output** on a mid-range x64 desktop (Tier A) with nearest or sharp-bilinear filtering.
- **1080p 60 fps** on Raspberry Pi 4/5 class arm64 using the GLES 3.1 path.
- Native 320x240 emulation; 4K is **output upscaling**, not 4K internal CEL rendering.
- Core frame budget ≤ 8 ms p99 on Tier A, zero managed allocations per frame, and core cost independent of output resolution.
- CRT/xBR filters are opt-in and may require faster hardware.

The CPU side currently runs at ~2.1-2.3 ms/frame (core + pack + mailbox + read copy),
leaving ~14 ms of headroom at 60 Hz on the development machine. The 4K GPU pass,
p99 frame times, allocation gates, and Tier B numbers are not measured yet; see
[`docs/Performance.md`](docs/Performance.md) for the pipeline, measurement method,
and the remaining M9P work items.

## Optional guest CPU JIT

.NET already JIT-compiles the C# core. If profiling ever shows the ARM60 interpreter is a bottleneck, a **pure managed IL JIT** for guest ARM basic blocks can be added behind `ICpuBackend`. It is optional, disabled by default, and must be bit-identical to the interpreter. At ~12.5 MHz, the 3DO CPU is not expected to require it for 60 fps.

## Non-goals for the first release

- Native or JIT CPU backends. The ARM60 interpreter is the reference implementation.
- Netplay, rewinding, or a full debugger UI.
- CD-DA/Red Book audio, multi-track discs, or CD swapping.
- PAL video modes and high-resolution rendering in the first playable milestone (they are planned later).
- 4K *internal* CEL rendering; v1 targets 4K output upscaling only.
- Shipping BIOS ROMs, game discs, or any copyrighted 3DO material.

## Requirements

- .NET SDK **10.0.100** or newer (`global.json` rolls forward to the latest 10.0 feature band).
- A legally obtained 3DO BIOS ROM (1 MB, optionally a second 1 MB ROM).
- Legally obtained 3DO disc images in ISO (2048-byte sectors), BIN (2352-byte sectors), CUE+BIN, or CHD format. CHD support uses the pure C# CHDSharp library.
- For the desktop app: Windows 10+, a current Linux desktop, or macOS 12+.

## Build

```powershell
git clone https://github.com/purelogiccode/3DOEmulator.git
cd 3DOEmulator
dotnet build 3DOEmulator.sln
dotnet test 3DOEmulator.sln
```

## Run

Headless CLI:

```powershell
dotnet run --project src/ThreeDo.Cli -- run game.cue --bios panafz1.bin --frames 600 --frame-out frame.png
dotnet run --project src/ThreeDo.Cli -- info game.chd --json
dotnet run --project src/ThreeDo.Cli -- bios info panafz1.bin
dotnet run --project src/ThreeDo.Cli -- state inspect quick.tdos
dotnet run --project src/ThreeDo.Cli -- bench --frames 600
dotnet run --project src/ThreeDo.Cli -- selftest
dotnet run --project src/ThreeDo.Cli -- disasm artifacts/ram.bin --offset 0x12380 --count 64
```

Diagnostics: `run` accepts `--watch-lowmem`, `--dump-ram`, and `--dump-dsp`;
`trace` adds `--dsp-trace` and the existing CPU ring buffer. See
[`docs/TestCorpus.md`](docs/TestCorpus.md) for the full command set.

Desktop app:

```powershell
dotnet run --project src/ThreeDo.Frontend.Avalonia
```

Use **Open BIOS** and **Open Disc**, then **Play**. The default keyboard bindings are the arrow keys, Z (A), X (B), C (C), A (L), S (R), Enter (P), and Backspace (X). **Save State**/**Load State** use a quick slot; **Screenshot** writes a BMP to Pictures. Settings (last paths, filter/aspect, audio, bindings) persist under the user local application data folder.

## Legal and attribution

FreeDO/4DO are **not** standard OSI-licensed projects. The FreeDO source headers contain a modified LGPL with additional restrictions, including a prohibition on commercial use without the owners' approval. This repository does **not** include BIOS ROMs or game images. Read [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) and [`docs/Licensing.md`](docs/Licensing.md) before distributing builds.

"3DO" and "Panasonic" are trademarks of their respective owners. This project is not affiliated with or endorsed by them.
