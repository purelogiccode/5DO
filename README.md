# 3DOEmulator

A clean-room-style, **pure C# / .NET 10** port of the FreeDO/4DO 3DO emulator core, with a cross-platform Avalonia frontend, a headless CLI, and a deterministic test harness.

> **Working title only.** The final product name is not decided yet. The solution is called `3DOEmulator`; source namespaces use the placeholder prefix `ThreeDo` because C# identifiers cannot begin with a digit.

## Status

**M0 scaffold complete; M1 (core contracts, memory/bus, Quartz scheduler, state primitives, diagnostics) is complete; M2 (ARM60 CPU) is complete; M3 (boot skeleton: BIOS/NVRAM, CLIO, MADAM registers, VDLP, SPORT, frame loop) is complete; M4 (MADAM graphics) is complete through M4.14; M4.15's BIOS boot animation renders and starts the DSP, but the interactive menu needs a valid game disc; M5 (DSP and audio) is complete through the core, pipeline, and tests; M6 (XBUS, CD-ROM, ISO/BIN/CUE/CHD storage) is complete except real-disc boot validation, which needs a user-provided disc.** The M3.13 OpenGL spike is deferred to M9R. The implementation plan and architecture are in:

- [`docs/ImplementationPlan.md`](docs/ImplementationPlan.md) - milestones, tasks, acceptance criteria, risks.
- [`docs/Architecture.md`](docs/Architecture.md) - project layout, dependency rules, runtime composition.
- [`docs/PortingNotes.md`](docs/PortingNotes.md) - C++ to C# porting rules and FreeDOCore quirks.
- [`docs/TestStrategy.md`](docs/TestStrategy.md) - unit, integration, golden, differential, and performance testing.
- [`docs/Compatibility.md`](docs/Compatibility.md) - BIOS, disc, input, and game-fix compatibility notes.

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

## Differentiator: OpenGL presentation

The desktop frontend uses **Silk.NET.OpenGL** through Avalonia's OpenGL control as the **required** presentation path:

- GPU palette expansion from raw 3DO indexed pixels and per-line CLUTs.
- Pixel-accurate "Accurate" mode that matches the CPU frame assembler.
- High-quality GPU upscaling to 1080p/1440p/4K: nearest, sharp-bilinear, CRT, and xBR/HQx-family filters.
- Integer scaling, aspect-ratio control, fullscreen, vsync, and screenshots.
- CPU `WriteableBitmap` fallback only when a GL 3.3 context is unavailable.

The emulation core still renders CELs in software for accuracy. The GPU accelerates presentation, palette expansion, scaling, and filters; it does not change emulated state.

## Optional guest CPU JIT

.NET already JIT-compiles the C# core. If profiling ever shows the ARM60 interpreter is a bottleneck, a **pure managed IL JIT** for guest ARM basic blocks can be added behind `ICpuBackend`. It is optional, disabled by default, and must be bit-identical to the interpreter. At ~12.5 MHz, the 3DO CPU is not expected to require it for 60 fps.

## Non-goals for the first release

- Native or JIT CPU backends. The ARM60 interpreter is the reference implementation.
- Netplay, rewinding, or a full debugger UI.
- CD-DA/Red Book audio, multi-track discs, or CD swapping.
- PAL video modes and high-resolution rendering in the first playable milestone (they are planned later).
- Shipping BIOS ROMs, game discs, or any copyrighted 3DO material.

## Requirements

- .NET SDK **10.0.100** or newer (`global.json` rolls forward to the latest 10.0 feature band).
- A legally obtained 3DO BIOS ROM (1 MB, optionally a second 1 MB ROM).
- Legally obtained 3DO disc images in ISO (2048-byte sectors), BIN (2352-byte sectors), CUE+BIN, or CHD format. CHD support uses the pure C# CHDSharp library.
- For the desktop app: Windows 10+, a current Linux desktop, or macOS 12+.

## Build

```powershell
dotnet build 3DOEmulator.sln
dotnet test 3DOEmulator.sln
```

## Run

The CLI and frontend are scaffolds during M0. Once the core is runnable:

```powershell
dotnet run --project src/ThreeDo.Cli -- run game.cue --bios panafz1.bin
dotnet run --project src/ThreeDo.Frontend.Avalonia
```

## Legal and attribution

FreeDO/4DO are **not** standard OSI-licensed projects. The FreeDO source headers contain a modified LGPL with additional restrictions, including a prohibition on commercial use without the owners' approval. This repository does **not** include BIOS ROMs or game images. Read [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) and [`docs/Licensing.md`](docs/Licensing.md) before distributing builds.

"3DO" and "Panasonic" are trademarks of their respective owners. This project is not affiliated with or endorsed by them.
