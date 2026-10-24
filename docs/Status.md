# Project Status and Next Steps

**Snapshot:** 2026-10-24, second pass (see `git log` for the exact commit).
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
  sources are selected from the context version. The VDLP bitmap path now honors the
  modulo-derived line width (320/384/512/640/1024).
- **Performance:** the machine runs on a dedicated 60 Hz core thread with a frame
  pacer; the UI renders on demand; uploads use `TexStorage2D` + `TexSubImage2D`;
  uniform locations and the present delegate are cached; physical-pixel sizing is
  implemented.
- **Tooling:** CLI `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks`,
  `disasm`, plus KPRINT, CD, CLIO, FIQ, RAM-write watchpoints, RAM/DSP dumps,
  DSP-instruction rings, CEL command/geometry traces, PBUS packet traces, CPU
  instruction-ring diagnostics, scripted input, NVRAM load/save, periodic frame
  capture (`--capture-dir`/`--capture-every`), and RAM dumps at trace stops.
- **Audio:** the DSP executes in every run, including headless runs with no host
  audio sink. The AudioFolio semaphore handshake works, instrument programs run, and
  the five-title corpus produces non-silent 44.1 kHz stereo WAV output (~737 samples
  per frame). In-game captures: Road Rash peak 32768 / RMS ~9.1k (191 clipped samples
  in 10 s), Wolfenstein peak 30743 / RMS ~5.5k / no clipping.
- **Input scripting:** the CLI accepts `--input-script <path>` with
  `frame button[,button...]` lines for repeatable controller taps. Input has been
  validated in Road Rash (race state/wreck), Wolfenstein (menus, gameplay, level
  start), Alone (scene and menu), Lemmings (menu and name entry), and Space Ace (P/X).
- **Tests:** 552 passing (`dotnet test -c Release`).
- **Packaging:** CI builds/tests/formats and publishes CLI/frontend for six RIDs.
  A win-x64 CLI publish has been smoke-tested.

---

## 2. Real-game status (local corpus, `panafz1.bin`)

The 2026-10-24 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots, renders, plays DSP audio**; reaches the title screen (Play/New Game) and the account-name screen; D-pad/A/X operate the menus | Not re-tested this pass; expected to pass |
| Space Ace (USA) | **Boots, renders, plays DSP audio**; ReadySoft player runs the animated demo past 12,000 frames with no runaway | **Plays the demo intro for 60 s, no runaway** |
| Road Rash (USA) | **Boots, renders, plays DSP audio**; the 3D race renders correctly and the attract race runs past 60,000 frames with no allocator error; A/P alter the race | **Renders the intro FMV, then a race** |
| Wolfenstein 3D (USA) | **Boots, renders, plays DSP audio**; A opens the preview menu, selecting a level starts a fresh game (FLOOR 1-1), and save/load roundtrips in-game | **Renders the intro for 60 s** |
| Alone in the Dark (USA) | **Boots, renders, plays DSP audio**; reaches the in-game staircase/room with an upright character; D-pad/A change the scene and open the menu | **Renders the intro/loading for 60 s** |

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
5. **NVRAM initialization.** The CLI now loads a fresh 32 KB NVRAM volume by default
   (and `--nvram <path>` persists it). Without it, Alone in the Dark saw zero free
   save slots, displayed its save-space warning, and shut down. It now reaches the
   in-game room/inventory scene.
6. **Arbitrary-CEL transposition.** `TexelDrawArbitrary` passed `(i, j)` to `(x, y)`
   read/write helpers, so every asymmetric quad was written transposed. Road Rash's
   race CELs overwrote the OS VRAM free list at `0x2FC000` (`AllocMem error, bad node
   address`) and the race froze; Alone's character was drawn horizontally. Passing
   `(j, i)` fixed both. The VDLP bitmap copy now uses the modulo-derived line width
   (no 320-pixel crop), and state loads restore the VDL pixel buffers.

Space Ace's former frame-681 crash was a downstream consequence of the failed
`AllocInstrument`: the player copied a decoded buffer to a null pointer, wiping the
ARM vectors. It now runs past 12,000 frames.

**Gameplay is now reachable and renderer-correct.** Road Rash's 3D race renders like
the Opera reference and runs past 60,000 frames; Wolfenstein 3D starts a fresh game and
its save states roundtrip; Alone in the Dark's in-game character is upright and its
menu is reachable; Lemmings reaches its title menu and account-name screen; Space Ace
plays its animated demo. The next pass should complete a scripted race/level, finish
the Lemmings name entry, and capture a WAV/state/screenshot set for each title.

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

### P0 - complete the gameplay loops

1. **Complete a race/level in each title.** The renderer and input path are now
   validated: Road Rash's race renders correctly, Wolfenstein starts a fresh game,
   Alone's in-game character is upright, Lemmings reaches its name-entry screen, and
   Space Ace plays its demo. The remaining work is scripted play: a Road Rash
   autopilot that finishes a race, the Lemmings name-entry/level-start sequence, and
   an Alone movement/inventory sequence. Record the exact scripts and frame numbers
   in [`TestCorpus.md`](TestCorpus.md).
2. **Save states and screenshots in-game.** Wolfenstein already roundtrips a live
   gameplay state (saved hash `0xC9989182`, two 120-frame continuations end at
   `0x370158AC`). Repeat for each title and store screenshots at known frames.
3. **Audio in gameplay.** The 600-frame captures are non-silent and 44.1 kHz stereo;
   Road Rash slightly clips (191 samples in 10 s) and both titles run at ~737
   samples/frame. Compare the mix and cadence with Opera/hardware before changing the
   DSP clock.

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

The next session is a focused gameplay-validation session, not a UI or performance
pass. Start with [`NextSession.md`](NextSession.md):

1. Use `--input-script` to start and play each title: Lemmings level, Wolfenstein
   movement/fire, Road Rash race, Space Ace interactive demo, Alone movement.
2. Save/load at a gameplay frame and verify the frame hash/PC roundtrip.
3. Capture WAV during gameplay and check the audio mix.
4. Record the exact scripts and results in [`TestCorpus.md`](TestCorpus.md) and
   [`Compatibility.md`](Compatibility.md).

Do not re-enable the null audio-sink short-circuit, do not change the XBUS DMA byte
order (the raw `IRam` order is correct), do not port `opera_cdrom.c` wholesale, and do
not touch the UI or 4K tuning in that session.

---

## 7. Reproduce

```powershell
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release

# The corpus root is machine-specific; set it once per shell.
$env:THREEDO_GAMES = "D:\Samples\3DO"   # office; home: E:\Panasonic 3DO
$env:THREEDO_DISC  = "$env:THREEDO_GAMES\Road Rash (USA).chd"

# Build/test/format
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release
dotnet format --verify-no-changes

# Synthetic CPU-side baseline
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- --frames 600

# Real disc baseline
dotnet run -c Release --project benchmarks/ThreeDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin `
  --disc "$env:THREEDO_GAMES\Lemmings (USA).chd" --label lemmings

# Boot a disc and dump the last frame
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:THREEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --nvram artifacts/lemmings.ram --frame-out artifacts/lemmings.png

# Scripted input, frame sequence, and PBUS packet trace
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:THREEDO_GAMES\Wolfenstein 3D (USA).chd" --bios bios/panafz1.bin `
  --frames 12000 --input-script artifacts/input.txt `
  --capture-dir artifacts/w3d-frames --capture-every 1000 `
  --frame-out artifacts/w3d.png --state-out artifacts/w3d.state

# RAM dump at a KPRINT stop
dotnet run -c Release --project src/ThreeDo.Cli -- trace `
  "$env:THREEDO_DISC" --bios bios/panafz1.bin `
  --frames 16000 --trace-frames 2200 --stop-kprint "AllocMem error" --ring 40000 `
  --out artifacts/rr-trace.txt --dump-ram artifacts/rr-stop-ram.bin `
  --dump-ram-offset 0 --dump-ram-length 0x300000

# Opera reference: extract the data track to ISO, then run the bundled SimpleGui
dotnet run -c Release --project src/ThreeDo.Cli -- disc dump `
  "$env:THREEDO_DISC" --sector 0 --count 328130 `
  --out "$env:TEMP\roadrash.iso"
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\roadrash.iso"
```

The desktop frontend is `dotnet run -c Release --project src/ThreeDo.Frontend.Avalonia`.
See [`ReferenceStrategy.md`](ReferenceStrategy.md) for the full differential procedure.
