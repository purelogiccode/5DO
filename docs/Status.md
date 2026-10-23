# Project Status and Next Steps

**Snapshot:** 2026-10-23, code at `master`/`19b281f` (documentation may be newer).
**Scope:** pure C# 3DO emulator, desktop GL/GLES presenter, CLI, and deterministic tests.

This is the short, current answer to "where are we?" The detailed task list is in
[`ImplementationPlan.md`](ImplementationPlan.md), the performance contract in
[`Performance.md`](Performance.md), and the local game corpus in
[`TestCorpus.md`](TestCorpus.md).

---

## 1. What works today

- **Core:** ARM60 interpreter, 3 MB RAM/2 MB ROM, NVRAM, CLIO timers/FIQ/FIFO/DMA,
  MADAM CEL/pixel/matrix engines, VDLP, SPORT, DSP, Quartz scheduler, XBUS, CD-ROM,
  ISO/BIN/CUE/CHD storage, input/PBUS, game database, deterministic save states.
- **BIOS:** `panafz1.bin` boots, runs the splash/animation, initializes the OS, and
  reaches the shell. The earlier `PC 0x205C4` "halt" was the Portfolio idle loop.
- **Video:** the desktop two-stage presenter expands palettes at native resolution
  into an FBO, then scales/filters to the output. Desktop GL 3.3 and GLSL ES 300
  sources are selected from the context version.
- **Performance:** the machine runs on a dedicated 60 Hz core thread with a frame
  pacer; the UI renders on demand; uploads use `TexStorage2D` + `TexSubImage2D`;
  uniform locations and the present delegate are cached; physical-pixel sizing is
  implemented.
- **Tooling:** CLI `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks`,
  plus KPRINT, CD, CLIO, FIQ, and instruction-ring diagnostics.
- **Tests:** 538 passing (`dotnet test -c Release`).
- **Packaging:** CI builds/tests/formats and publishes CLI/frontend for six RIDs.
  A win-x64 CLI publish has been smoke-tested.

---

## 2. Real-game status (local corpus, `panafz1.bin`)

| Title | Status | Notes |
|---|---|---|
| Lemmings (USA) | **Boots and renders** | Reaches CyberIntro; the 3D intro renders correctly |
| Space Ace (USA) | **Boots and renders** | ReadySoft player finds the CD-ROM and renders the demo intro; CPU runs away at frame ~681 |
| Road Rash (USA) | **Partial** | Loads the game OS (`grafmath 21.10.504`), then stops at SAudio channel 0 (`-6996`) |
| Wolfenstein 3D (USA) | **Partial** | Loads Sherry/Operator/FS v24.225, then stops at SAudio channel 0 (`-6996`) |
| Alone in the Dark (USA) | **Fails early** | Stops during AudioFolio initialization (`-6996`) |

The CD disc-end fix on 2026-10-23 is what unblocked Road Rash, Space Ace, and
Wolfenstein: `InitCd` no longer double-adds the 150-frame lead-in, so the CD DIPIR
accepts discs whose volume block count is less than the physical track length.

The SAudio/audio-instrument failure is shared with the FreeDO reference. Space Ace's
runaway is narrowed to a FIQ with a zeroed vector table and a stack full of
`0x11111111`, which points at low-memory corruption (DMA or a missing fence) rather
than a CPU/video bug.

---

## 3. Performance snapshot

Release build, development machine, 600 frames, CPU side only:

| Case | Core | Pack | Mailbox write | Read copy | Total |
|---|---:|---:|---:|---:|---:|
| Synthetic | 2.177 ms | 42.8 µs | 23.4 µs | 27.2 µs | **2.270 ms/frame** |
| Lemmings | 2.061 ms | 40.7 µs | 17.1 µs | 18.1 µs | **2.137 ms/frame** |

This leaves ~14.4 ms of headroom against the 16.67 ms frame budget. The **4K GPU
pass, p99 frame times, allocation assertions, and Tier B (Pi 4/5) numbers are not
measured yet**.

---

## 4. How far from "full development"?

Using the 157 developer-day baseline in `ImplementationPlan.md` and counting the
prototype work as partial credit:

| Milestone | State | Done (dd) | Remaining (dd) |
|---|---|---:|---:|
| M0-M8 core, CPU, boot, MADAM, DSP, CD, input, save states | Complete | 90 | 0 |
| M9 Avalonia frontend | Partial: shell, library, video, input; settings/audio/gamepad pages and polish remain | 10 | 6 |
| M9R OpenGL presenter | Partial: two-stage, nearest/sharp-bilinear/CRT; xBR/HQx and GPU tests remain | 6 | 4 |
| M9P performance/pacing | Partial: core thread, two-stage, uploads, DPI, benchmark; CHD prefetch, hot-path, p99/GPU gates remain | 5 | 5 |
| M10 packaging/validation | Partial: CI and six-RID publish; arm64, trimming, installers, perf report remain | 2.5 | 3.5 |
| M11 compatibility/release | Not started: gameplay hardening, remaining games, release checklist | 0 | 25+ |
| **Baseline total** | | **~113.5 / 157** | **~43.5 + 30% ≈ 57** |

**Estimate: roughly 70% of the v1 effort is done.** For one experienced developer
that is about **9-12 focused weeks**; two developers can overlap the UI, renderer,
and packaging work for roughly **5-7 weeks**, but real-game compatibility is
serial and is the main uncertainty.

The estimate assumes the audio/DSP issue can be fixed without a rewrite. If it
requires a deep DSP/microcode effort, add **2-4 weeks**. If the target is "all five
corpus games playable" rather than "the v1 feature set is complete", add another
**2-3 weeks** for game-specific fixes.

---

## 5. What next

### P0 - unblock real gameplay

1. **Fix SAudio/DSP instrument initialization (`-6996`).** This is the single
   biggest blocker: Road Rash, Wolfenstein, and Alone all stop here, and Space Ace
   cannot open its mixer. Start with `AllocInstrument`/`LoadInsTemplate`, DSP
   program-memory accounting, and the ARM/DSP semaphore path. Compare against the
   FreeDO reference, which reaches the same point.
2. **Fix Space Ace's low-memory corruption.** Add the missing 3DO memory fence or
   find the DMA/PBUS write that overwrites `0x0-0x100`; the FIQ vector is zero by
   frame ~681. This is a good differential target because the player renders before
   it crashes.
3. **Validate gameplay** in the two games that already render: input, pause, save
   states, screenshots, CD streaming, and audio output. Record results in
   [`Compatibility.md`](Compatibility.md).

### P1 - prove the 4K60 target

4. **Measure GPU time and p99** at 1080p/1440p/4K with nearest and sharp-bilinear;
   add allocation assertions around the steady-state frame. The CPU side already has
   ~14 ms of headroom.
5. **Validate the backend matrix** on real hardware: WGL desktop GL, ANGLE GLES,
   Raspberry Pi 4/5 GLES 3.1, and 100/150/200% DPI. The shader sources and physical
   pixel sizing exist; they are not proven on device.
6. **Add CHD prefetch** on a bounded host worker so Tier B does not stall on
   decompression, then benchmark CHD vs ISO.

### P2 - finish v1

7. **M10 packaging:** arm64 validation, trimming, installers/signing plan, and the
   final performance report.
8. **Filter/UI polish:** xBR/HQx, settings/audio/gamepad pages, fullscreen/vsync.
9. **Legal/attribution review** before any public build (M0.8).

---

## 6. Reproduce

```powershell
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release

# Synthetic CPU-side baseline
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- --frames 600

# Real disc baseline
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin --disc "D:\Samples\3DO\Lemmings (USA).chd" `
  --label lemmings

# Boot a disc and dump the last frame
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "D:\Samples\3DO\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --frame-out artifacts/lemmings.png
```

The desktop frontend is `dotnet run -c Release --project src/ThreeDo.Frontend.Avalonia`.
