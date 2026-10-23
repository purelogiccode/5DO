# Project Status and Next Steps

**Snapshot:** 2026-10-23, `master` (see `git log` for the exact commit).
**Scope:** pure C# 3DO emulator, desktop GL/GLES presenter, CLI, and deterministic tests.
**Reference:** `References/opera-libretro-master/libopera` is now the primary
behavioral reference for new compatibility work; see
[`ReferenceStrategy.md`](ReferenceStrategy.md).

This is the short, current answer to "where are we?" The detailed task list is in
[`ImplementationPlan.md`](ImplementationPlan.md), the performance contract in
[`Performance.md`](Performance.md), the local game corpus in
[`TestCorpus.md`](TestCorpus.md), and the next-session plan in
[`NextSession.md`](NextSession.md).

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

The 2026-10-23 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots and renders**; reaches CyberIntro and renders the 3D intro | Not re-tested this pass; expected to pass |
| Space Ace (USA) | **Boots and renders**; ReadySoft player renders the demo intro, then the CPU runs away at frame ~681 | **Plays the demo intro for 60 s, no runaway** |
| Road Rash (USA) | Loads the game OS (`grafmath 21.10.504`), then stops at SAudio channel 0 (`-6996`) | **Renders the intro FMV for 60 s** |
| Wolfenstein 3D (USA) | Loads Sherry/Operator/FS v24.225, then stops at SAudio channel 0 (`-6996`) | **Renders the intro for 60 s** |
| Alone in the Dark (USA) | Stops during AudioFolio initialization (`-6996`) | **Renders the intro/loading sequence for 60 s** |

The CD disc-end fix on 2026-10-23 is what unblocked Road Rash, Space Ace, and
Wolfenstein: `InitCd` no longer double-adds the 150-frame lead-in, so the CD DIPIR
accepts discs whose volume block count is less than the physical track length.

**The earlier "shared FreeDO limitation" conclusion was wrong.** Opera gets past
SAudio `-6996` and past Space Ace frame ~681, so those failures are in our port or in
the older FreeDO behavior we copied. The SAudio error is still undecoded; the next
session traces the guest call site and the instrument file read rather than assuming a
DSP limitation. Space Ace's runaway remains a separate low-memory corruption bug
(FIQ vector at `0x1C` zeroed, stack full of `0x11111111`).

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

### 4K60 feasibility

- **Desktop (Tier A): high confidence.** The core is ~2.1-2.3 ms/frame; the 4K pass is
  one fullscreen textured quad after the native-resolution palette expansion. The
  RX 580 is far above the required fill rate. The remaining risk is frame pacing and
  the Avalonia/WGL compositor, not the emulator core.
- **Pi 5: likely, but unmeasured.** The presenter should handle 4K60 via GLES; the
  ARM CPU core may be the limiting factor in complex scenes.
- **Pi 4: 1080p60 is the realistic target.** 4K60 output is not assumed.
- **4K internal CEL rendering is not a v1 goal.** The target is 4K output, not 4K
  internal rendering.

The next performance work is measurement: GPU timer queries, p99 frame time,
allocations/frame, and present-to-present at 1080p/1440p/4K. See
[`Performance.md`](Performance.md).

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

1. **Build the Opera differential harness and decode SAudio `-6996`.** This is the
   single biggest blocker: Road Rash, Wolfenstein, and Alone all stop here. Follow
   [`NextSession.md`](NextSession.md): freeze a reproducible baseline, find the guest
   call site, trace `AllocInstrument`/`LoadInsTemplate`, and compare the CD
   command/status stream and instrument file bytes with Opera. Do not assume a
   shared DSP limitation; Opera passes this point.
2. **Fix Space Ace's low-memory corruption.** Add a temporary watchpoint for writes
   to `0x0-0x100`, identify the writer (CPU, CEL, DMA8/PBUS, CD/XBUS, or DSP), and
   compare the same scene with Opera. The FIQ vector is zero by frame ~681; the
   player renders before it crashes, so this is a good differential target.
3. **Validate gameplay** in the two games that already render: input, pause, save
   states, screenshots, CD streaming, and audio output. Record results in
   [`Compatibility.md`](Compatibility.md).

### P1 - prove the 4K60 target

4. **Measure GPU time and p99** at 1080p/1440p/4K with nearest and sharp-bilinear;
   add allocation assertions around the steady-state frame. The CPU side already has
   ~14 ms of headroom, so desktop 4K60 is high confidence; Pi 5 is likely and Pi 4 is
   realistically 1080p60 until measured.
5. **Validate the backend matrix** on real hardware: WGL desktop GL, ANGLE GLES,
   Raspberry Pi 4/5 GLES 3.1, and 100/150/200% DPI. The shader sources and physical
   pixel sizing exist; they are not proven on device.
6. **Add CHD prefetch** on a bounded host worker so Tier B does not stall on
   decompression, then benchmark CHD vs ISO.

### P2 - finish v1

7. **M10 packaging:** arm64 validation, trimming, installers/signing plan, and the
   final performance report.
8. **Filter/UI polish:** xBR/HQx, settings/audio/gamepad pages, fullscreen/vsync.
9. **Legal/attribution review** before any public build (M0.8); add Opera to the
   FreeDO/4DO attribution and license review.

---

## 6. Next session

The next session is a focused debugging session, not a UI or performance pass. Start
with [`NextSession.md`](NextSession.md):

1. Freeze Road Rash and Space Ace baselines from the extracted ISO with KPRINT, CD,
   and FIQ traces.
2. Decode SAudio `-6996` from the guest call site; compare the instrument file read
   and DSP resource tables with Opera.
3. Add a low-memory write watchpoint for Space Ace and identify the writer.
4. Make one minimal fix with a test, or stop at a written diagnosis and a minimal
   reproducer.

Do not change `Arm60.Ncycle` as a default, do not port `opera_cdrom.c` wholesale, and
do not touch the UI or 4K tuning in that session.

---

## 7. Reproduce

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

# Opera reference: extract the data track to ISO, then run the bundled SimpleGui
dotnet run -c Release --project src/ThreeDo.Cli -- disc dump `
  "D:\Samples\3DO\Road Rash (USA).chd" --sector 0 --count 328130 `
  --out "$env:TEMP\roadrash.iso"
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\roadrash.iso"
```

The desktop frontend is `dotnet run -c Release --project src/ThreeDo.Frontend.Avalonia`.
See [`ReferenceStrategy.md`](ReferenceStrategy.md) for the full differential procedure.
