# Project Status and Next Steps

**Snapshot:** 2026-10-25, sixth pass (see `git log` for the exact commit).
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
  modulo-derived line width (320/384/512/640/1024). An opt-in high-resolution mode
  (`--high-res`, `EmulatorTuning.HighResolution`) renders CELs and bitmap lines into
  four 1 MB VRAM planes and presents 640x480 by line-doubling the 640x240 source;
  Lemmings' title screen now matches the 640x480 reference. The mode is off by
  default because 320x240 titles must not be upscaled, and the desktop shell exposes
  it as a Hi-Res checkbox that restarts the machine.
- **Performance:** the machine runs on a dedicated 60 Hz core thread with a frame
  pacer; the UI renders on demand; uploads use `TexStorage2D` + `TexSubImage2D`;
  uniform locations and the present delegate are cached; physical-pixel sizing is
  implemented. The benchmark reports p50/p95/p99 and per-stage allocations, and the
  synthetic steady state is 0 bytes/frame. An opt-in bounded prefetch cache is
  available for CHD/ISO streaming.
- **Renderer robustness:** GL/GLES capability parsing reports the API and version,
  fullscreen/F11/Escape work, GL readback screenshots are implemented, and GL init
  failure or context loss switches the shell to the CPU `WriteableBitmap` presenter.
- **Tooling:** CLI `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks`,
  `fs list/extract`, `disasm`, plus KPRINT, CD, CLIO, FIQ, RAM-write watchpoints,
  RAM/DSP dumps, DSP-instruction rings, CEL command/geometry traces, PBUS packet
  traces, CPU instruction-ring diagnostics, scripted input, NVRAM load/save, periodic
  frame capture (`--capture-dir`/`--capture-every`), RAM patches at run start
  (`--poke ADDR=HEX`), an experimental Road Rash road-following driver
  (`--autopilot road`), an opt-in bounded disc prefetch cache (`--prefetch`), and RAM
  dumps at trace stops. `run --json` reports the source size, VDL modulo, road
  coverage, and autopilot state for mode/crop and driver investigations.
- **Audio:** the DSP executes in every run, including headless runs with no host
  audio sink. The AudioFolio semaphore handshake works, instrument programs run, and
  the five-title corpus produces non-silent 44.1 kHz stereo WAV output (~737 samples
  per frame). In-game captures: Road Rash peak 32768 / RMS ~9.1k (191 clipped samples
  in 10 s), Wolfenstein peak 30743 / RMS ~5.5k / no clipping.
- **Input scripting:** the CLI accepts `--input-script <path>` with
  `frame button[,button...]` lines for repeatable controller taps. Input has been
  validated in Road Rash (race state/wreck), Wolfenstein (menus, gameplay, level
  start), Alone (scene and menu), Lemmings (menu and passcode entry), and Space Ace (P/X).
- **Tests:** 605 passing (`dotnet test -c Release`), including high-resolution CEL and
  VDLP regression coverage, RAM-patch and Road Rash autopilot contracts, CLI process
  tests, zero-allocation uploader/mailbox assertions, OpenGL capability parsing, and
  bounded prefetch-cache tests.
- **Packaging:** CI builds/tests/formats and publishes CLI/frontend for six RIDs.
  A win-x64 CLI publish has been smoke-tested.

---

## 2. Real-game status (local corpus, `panafz1.bin`)

The 2026-10-24 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots, renders, plays DSP audio**; reaches the title screen and the passcode screen; with `--high-res` the 640x480 title renders all four buttons and the full logo, matching the reference. The passcode screen uses A to type, B to confirm, auto-confirms at ten letters, and a 15-frame D-pad hold moves one key | Not re-tested this pass; expected to pass |
| Space Ace (USA) | **Boots, renders, plays DSP audio**; ReadySoft player runs the animated demo past 12,000 frames with no runaway | **Plays the demo intro for 60 s, no runaway** |
| Road Rash (USA) | **Boots, renders, plays DSP audio**; the 3D race renders correctly and the attract race runs past 60,000 frames with no allocator error; B accelerates, A brakes, the D-pad steers, C attacks, and P pauses; `--autopilot road` v10 reaches 91,000 live frames from the 80k/83k states (roundtrip `0x02ABE78E`) but does not yet finish the race (~92k wall/rider pin; clean 30k runs pin at ~40k) | **Renders the intro FMV, then a race** |
| Wolfenstein 3D (USA) | **Boots, renders, plays DSP audio**; A opens the preview menu, selecting a level starts a fresh game (FLOOR 1-1), the player can walk/turn/fire, and save/load roundtrips in-game | **Renders the intro for 60 s** |
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
the Opera reference and runs past 60,000 frames; Wolfenstein 3D starts a fresh game,
walks/turns/fires, and its save states roundtrip; Alone in the Dark's in-game character
is upright and its menu is reachable; Lemmings completes a Fun 4 passcode
(`HNLHCIOECW`) end-to-end into gameplay with save/load roundtrip, and its 640x480
high-resolution title matches the reference; Space Ace plays its animated demo. The
next pass should keep improving the Road Rash driver (v10 reaches 91,000 live
frames from the 80k/83k states, but the 100k continuation pins at ~92,000 and a
clean final-code 30k run pins at ~40,000; the old "wall pin" was the `BUSTED`
screen) and capture a WAV/state/screenshot set for each title.

---

## 3. Performance snapshot

Release build, development machine, 600 frames, CPU side only:

| Case | Total | p50 | p95 | p99 | Allocations |
|---|---:|---:|---:|---:|---:|
| Synthetic | **1.250 ms/frame** | 1.218 ms | 1.434 ms | 1.638 ms | **0 bytes/frame** |
| Lemmings CHD | **1.281 ms/frame** | 1.088 ms | 2.005 ms | 3.398 ms | ~5.9 KB/frame (CHD decode) |
| Lemmings CHD, high-res | **1.605 ms/frame** | 1.398 ms | 2.497 ms | 4.598 ms | ~5.8 KB/frame (CHD decode) |

This leaves ~15.4 ms of headroom against the 16.67 ms frame budget for the synthetic
case. The **4K GPU pass, Tier B (Pi 4/5) numbers, and GLES timing are not measured
yet**. CHD decode allocations are host-side and are attributed to the core stage;
the synthetic core/pack/mailbox/read path is allocation-free in steady state.

### 4K60 feasibility

- **Desktop (Tier A): high confidence.** The core is ~1.25-1.6 ms/frame; the 4K pass
  is one fullscreen textured quad after the native-resolution palette expansion. The
  RX 580 is far above the required fill rate. The remaining risk is frame pacing and
  the Avalonia/WGL compositor, not the emulator core.
- **Pi 5: likely, but unmeasured.** The presenter should handle 4K60 via GLES; the
  ARM CPU core may be the limiting factor in complex scenes.
- **Pi 4: 1080p60 is the realistic target.** 4K60 output is not assumed.
- **4K internal CEL rendering is not a v1 goal.** The target is 4K output, not 4K
  internal rendering.

CPU p99 and per-stage allocations are now measured. The next performance work is
GPU timer queries, Tier B/GLES timing, and present-to-present at 1080p/1440p/4K. See
[`Performance.md`](Performance.md).

---

## 4. How far from "full development"?

Using the 157 developer-day baseline in `ImplementationPlan.md` and counting the
prototype work as partial credit:

| Milestone | State | Done (dd) | Remaining (dd) |
|---|---|---:|---:|
| M0-M8 core, CPU, boot, MADAM, DSP, CD, input, save states | Functionally complete; M0.8 legal review and minor M8 follow-ups are non-blocking | 90 | 0 |
| M9 Avalonia frontend | Partial: shell, library, video, input, fullscreen, About, Hi-Res; settings/audio/gamepad pages and polish remain | 11 | 5 |
| M9R OpenGL presenter | Partial: two-stage, sharp-bilinear/CRT, CPU fallback, screenshots; xBR/HQx and GPU tests remain | 7 | 3 |
| M9P performance/pacing | Partial: core thread, two-stage, uploads, DPI, p99/allocations, prefetch; hot-path, Tier B/GPU gates remain | 6 | 4 |
| M10 packaging/validation | Partial: CI and six-RID publish; arm64, trimming, installers, perf report remain | 2.5 | 3.5 |
| M11 compatibility/release | In progress: M11.0 done; gameplay and high-resolution validation underway | 2.5 | 22.5+ |
| **Baseline total** | | **~119 / 157** | **~38 + 30% ≈ 49** |

**Estimate: roughly three-quarters of the v1 effort is done.** For one experienced
developer that is about **9-11 focused weeks**; two developers can overlap the UI,
renderer, and packaging work for roughly **5-7 weeks**, but real-game compatibility
is serial and is the main uncertainty.

The estimate assumes no deep CD/timing rewrite. If the differential reveals one,
add **2-4 weeks**. If the target is "all five corpus games playable" rather than
"the v1 feature set is complete", add another **2-3 weeks** for game-specific fixes.

---

## 5. What next

### P0 - complete the gameplay loops

1. **Complete a race/level in each title.** The renderer and input path are now
   validated: Road Rash's race renders correctly and its controls are mapped (B
   accelerate, A brake, D-pad steer, `--autopilot road` v10 reaches 91,000 live
   frames from the 80k/83k states), Wolfenstein starts a fresh game and supports
   walk/turn/fire, Alone's in-game character is upright, Lemmings reaches its
   passcode screen (A types, B confirms, ten letters auto-confirm), and Space Ace
   plays its demo. The remaining work is scripted play: a Road Rash traffic-aware
   driver that finishes a race (the earlier "pin" was the `BUSTED` race-over
   screen; v10 recovers rider-run and wall stops but still pins at ~92k and in a
   clean 30k->40k run), a Lemmings ten-letter entry sequence, and an Alone
   movement/inventory sequence. Record the exact scripts and frame numbers in
   [`TestCorpus.md`](TestCorpus.md).
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
6. **Validate CHD prefetch on Tier B.** `PrefetchingDiscSource` is implemented and
   opt-in via `--prefetch`; x64 CHD/ISO p99 is recorded, but Pi 4/5 timing and the
   default policy remain.
7. **Validate high-resolution mode.** The 640x480 renderer works for Lemmings' title.
   The passcode screen is a 320-wide UI scaled 2x; its five visible keyboard columns
   are the game's own window rather than a framebuffer crop. The remaining question
   is whether any title uses a true 640x240 non-interlaced mode that should not be
   line-doubled before enabling high-res by default for that title.

### P2 - finish v1

8. **M10 packaging:** arm64 validation, trimming, installers/signing plan, and the
   final performance report.
9. **Filter/UI polish:** xBR/HQx, settings/audio/gamepad pages, vsync/FPS overlay.
10. **Legal/attribution review** before any public build (M0.8); add Opera to the
    FreeDO/4DO attribution and license review.

---

## 6. Next session

The next session is a focused gameplay-validation session, not a UI or performance
pass. Start with [`NextSession.md`](NextSession.md):

1. Use `--input-script` to start and play each title: Lemmings level (the passcode
   path is mapped; type a valid 10-letter code), Wolfenstein movement/fire, Road
   Rash race (B accelerates; improve `--autopilot road`), Space Ace interactive
   demo, Alone movement.
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
