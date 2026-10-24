# Project Status and Next Steps

**Snapshot:** 2026-10-24, `master` (see `git log` for the exact commit).
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
  `disasm`, plus KPRINT, CD, CLIO, FIQ, RAM-write watchpoints, RAM/DSP dumps,
  DSP-instruction rings, and CPU instruction-ring diagnostics.
- **Audio:** the DSP executes in every run, including headless runs with no host
  audio sink. The AudioFolio semaphore handshake works, instrument programs run, and
  the five-title corpus produces non-silent 44.1 kHz stereo WAV output (~738 samples
  per frame).
- **Input scripting:** the CLI accepts `--input-script <path>` with
  `frame button[,button...]` lines for repeatable controller taps.
- **Tests:** 550 passing (`dotnet test -c Release`).
- **Packaging:** CI builds/tests/formats and publishes CLI/frontend for six RIDs.
  A win-x64 CLI publish has been smoke-tested.

---

## 2. Real-game status (local corpus, `panafz1.bin`)

The 2026-10-24 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots, renders, plays DSP audio**; reaches the Load/Options title menu; D-pad selection changes the highlighted item | Not re-tested this pass; expected to pass |
| Space Ace (USA) | **Boots, renders, plays DSP audio**; ReadySoft player runs the demo past 10,000 frames with no runaway | **Plays the demo intro for 60 s, no runaway** |
| Road Rash (USA) | **Boots, renders, plays DSP audio**; loads the game OS, SAudio init succeeds, and the 3D race scene renders by 30,000 frames; P toggles the pause overlay | **Renders the intro FMV for 60 s** |
| Wolfenstein 3D (USA) | **Boots, renders, plays DSP audio**; with scripted input it reaches gameplay (FLOOR 1-1) and shows the in-game HUD | **Renders the intro for 60 s** |
| Alone in the Dark (USA) | **Boots, renders, plays DSP audio**; reaches a warning/play dialog; the DSP stops after the dialog and input has not yet advanced it | **Renders the intro/loading sequence for 60 s** |

The CD disc-end fix on 2026-10-23 is what unblocked Road Rash, Space Ace, and
Wolfenstein: `InitCd` no longer double-adds the 150-frame lead-in, so the CD DIPIR
accepts discs whose volume block count is less than the physical track length.

The 2026-10-24 session found and fixed the common blockers:

1. **DSP execution.** `AudioSink?.PushSample(Dsp.Loop())` skipped the DSP entirely
   when the host had no audio sink, which is the default headless path. The machine
   now always executes the DSP; the sink only receives samples.
2. **DSP immediate operands.** The loader shifted un-justified immediates left by
   two instead of zero. The AudioFolio tail program's `Sema4Status & 8` test became
   `Sema4Status & 0x20`, so the DSP always skipped the ARM command. The fix uses the
   reference's `IMMEDIATE << (JUSTIFY & 3)`. SAudio `-6996` is gone and the DSP now
   runs instrument code.
3. **DSP multiplier rounding.** MUXB=3 now masks the product
   (`(MULT1 * (Y >> 15)) & ~1`) instead of masking the shifted accumulator first.
4. **CCB field order.** The parser now reads position, size, perspective, and
   pixel-processor control before `PRE0`/`PRE1`, matching FreeDO. The old order
   mis-decoded every 3D CEL; Road Rash's race produced a bogus 1987x583 CEL and
   appeared to hang. The rasterizer's missing `cnt_cross > 2` x-point sort was also
   ported.

Space Ace's former frame-681 crash was a downstream consequence of the failed
`AllocInstrument`: the player copied a decoded buffer to a null pointer, wiping the
ARM vectors. It now runs past 10,000 frames.

**Gameplay is now reachable.** Road Rash renders its race scene, Wolfenstein 3D
reaches FLOOR 1-1 with scripted input, Lemmings reaches its title menu, and Alone in
the Dark reaches its warning dialog. The next pass should validate sustained
gameplay, input across all buttons, save states, screenshots, and in-game CD
streaming.

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

The 2026-10-24 synthetic bench, which now includes DSP execution, measured
**0.977 s for 600 frames (614 fps, 1.63 ms/frame)** on the development machine. The
detailed core/pack/mailbox breakdown above predates the DSP-execution fix and should
be re-measured before it is quoted again.

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

1. **Fix the DSP `AllocInstrument` handshake.** This is the single biggest blocker:
   Road Rash, Wolfenstein, and Alone all stop at
   `SAudioErrAllocInstFailed` (`-6996`), and Space Ace's null-buffer crash is the
   same failure on a different code path. Follow [`NextSession.md`](NextSession.md):
   capture the DSP trace at the SAudio stop, compare the head/tail program loading
   and relocations with the 3DO SDK, and differential the interpreter against
   `opera_dsp.c` (flags, branch table, special registers, Sema4 ACK path).
2. **Prove Space Ace is fixed by the same change.** Re-run it to at least 3,000
   frames and confirm there is no `memcpy` to address 0 and no FIQ at `0x1C`.
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

The next session is a focused DSP/audio debugging session, not a UI or performance
pass. Start with [`NextSession.md`](NextSession.md):

1. Capture the DSP trace, DSP memory, and Sema4 state at the Road Rash SAudio stop.
2. Parse `head.dsp`/`tail.dsp` from the 3DO SDK and compare their load addresses,
   relocations, and entry points with the dumped DSP memory.
3. Differential the interpreter against `opera_dsp.c`: flags, exact/branch table,
   special registers, operand loading, and the Sema4 ACK path.
4. Make one minimal fix with a DSP test, then re-run the five-title corpus and prove
   Space Ace reaches 3,000 frames without the null-buffer `memcpy`.

Do not re-enable the null audio-sink short-circuit, do not change the XBUS DMA byte
order (the raw `IRam` order is correct), do not port `opera_cdrom.c` wholesale, and do
not touch the UI or 4K tuning in that session.

---

## 7. Reproduce

```powershell
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release

# Synthetic CPU-side baseline
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- --frames 600

# Real disc baseline
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin --disc "E:\Panasonic 3DO\Lemmings (USA).chd" `
  --label lemmings

# Boot a disc and dump the last frame
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --frame-out artifacts/lemmings.png

# SAudio stop with DSP trace and memory dumps
dotnet run -c Release --project src/ThreeDo.Cli -- trace `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --frames 0 --trace-frames 3000 --stop-kprint "SAudio channel" --ring 4 `
  --dsp-trace artifacts/rr-dsp-trace.txt --dump-dsp artifacts/rr-saudio `
  --out artifacts/rr-trace.txt

# Opera reference: extract the data track to ISO, then run the bundled SimpleGui
dotnet run -c Release --project src/ThreeDo.Cli -- disc dump `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --sector 0 --count 328130 `
  --out "$env:TEMP\roadrash.iso"
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\roadrash.iso"
```

The desktop frontend is `dotnet run -c Release --project src/ThreeDo.Frontend.Avalonia`.
See [`ReferenceStrategy.md`](ReferenceStrategy.md) for the full differential procedure.
