# Project Status and Next Steps

**Snapshot:** 2026-10-26, 1.0.0-rc.1 release preparation after the fifteenth code
pass (see `git log` for the exact commit).
**Release definition:** documented compatibility, not full compatibility; see
[`ReleaseChecklist.md`](ReleaseChecklist.md).
**Scope:** pure C# 3DO emulator, desktop GL/GLES presenter, CLI, and deterministic tests.
**Reference:** `References/opera-libretro-master/libopera` is now the primary
behavioral reference for new compatibility work; see
[`ReferenceStrategy.md`](ReferenceStrategy.md).

This is the short, current answer to "where are we?" The detailed task list is in
[`ImplementationPlan.md`](ImplementationPlan.md), the performance contract in
[`Performance.md`](Performance.md), the local game corpus in
[`TestCorpus.md`](TestCorpus.md), the release gate in
[`ReleaseChecklist.md`](ReleaseChecklist.md), and the next-session plan in
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
  sources are selected from the context version; the frontend can force the backend
  with `--gl-backend auto|wgl|angle|software`. Windows ANGLE/EGL OpenGL ES 3.0 is
  validated on the development RTX 4070: all filters compile and a 320x240 Accurate
  capture matches the CPU oracle pixel-for-pixel. The VDLP bitmap path now honors the
  modulo-derived line width (320/384/512/640/1024). The palette shader was fixed in the
  eighth pass to use independent red, green, and blue CLUT indices; the previous shader
  used only the low five bits and made real games grayscale. An opt-in high-resolution
  mode (`--high-res`, `EmulatorTuning.HighResolution`) renders CELs and bitmap lines
  into four 1 MB VRAM planes and presents 640x480 by line-doubling the 640x240 source;
  Lemmings' title screen now matches the 640x480 reference. The mode is off by default
  because 320x240 titles must not be upscaled, and the desktop shell exposes it as a
  Hi-Res checkbox that restarts the machine. An opt-in xBR-lv2 4x filter, HQx
  2x/3x/4x filters, and CRT bloom are available in Settings; xBR and HQx use a
  CPU-expanded native source and sharp-bilinear finishing, and CRT bloom adds a 3x3
  native-resolution glow in the CRT filter. The HQx 3x/4x switches match the C++
  reference hashes and are the most expensive opt-in modes.
- **Performance:** the machine runs on a dedicated 60 Hz core thread with a frame
  pacer; the UI renders on demand; uploads use `TexStorage2D` + `TexSubImage2D`;
  uniform locations and the present delegate are cached; physical-pixel sizing is
  implemented and unit-tested at 100/125/150/200% scaling. The benchmark reports
  p50/p95/p99 and per-stage allocations, and the
  synthetic steady state is 0 bytes/frame. An opt-in bounded prefetch cache is
  available for CHD/ISO streaming. The `--gl-probe` validates both WGL and
  ANGLE/GLES: 4K sharp-bilinear presents in 0.14 ms mean / 0.47 ms p99 with 0
  renderer allocations/frame on the RTX 4070; a 320x240 Accurate capture matches
  the CPU oracle on both backends.
- **Renderer robustness:** GL/GLES capability parsing reports the API and version,
  fullscreen/F11/Escape work, GL readback screenshots are implemented, and GL init
  failure or context loss switches the shell to the CPU `WriteableBitmap` presenter.
  An optional FPS/status overlay is available in the settings dialog.
- **Desktop shell:** the game library scans configured folders through the 4DO
  database, previews sibling cover art, and opens the selected disc; the settings dialog edits video, audio,
  emulation, and paths; keyboard bindings for all six players can be captured with
  automatic conflict removal and reset-to-defaults; gamepads are mapped through a
  Gamepad tab (Windows XInput, Linux evdev). Save states use ten numbered slots with
  next/previous, a list/save/load/delete window, JSON metadata, and PNG thumbnails;
  screenshots are PNG files named by game, time, and frame. The shell has a first-run
  guide, error dialogs, and an in-app diagnostic log viewer with level/category/search
  filters. The app accepts a positional disc path or `--disc <path>` and ships a Linux
  `.desktop` entry; Windows/macOS associations remain installer work. Per-game 4DO fix
  flags are applied automatically when a disc opens; explicit masks can be set in the
  settings dialog.
- **Per-game fixes:** `GameCompatibility` maps the 4DO game IDs to `FixMode` bits.
  The CLI exposes `--fix-mode 0xMASK` and `--no-game-fixes`, and `run --json` /
  `info` report the applied mask. Alone in the Dark's `FIX_BIT_TIMING_6` is applied
  automatically; the 3,000-frame boot hash is unchanged.
- **Tooling:** CLI `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks`,
  `fs list/extract`, `disasm`, plus KPRINT, CD, CLIO, FIQ, RAM-write watchpoints,
  RAM/DSP dumps, DSP-instruction rings, CEL command/geometry traces, PBUS packet
  traces, CPU instruction-ring diagnostics, scripted input, NVRAM load/save, periodic
  frame capture (`--capture-dir`/`--capture-every`), RAM patches at run start
  (`--poke ADDR=HEX`), an experimental Road Rash road-following driver
  (`--autopilot road`), an opt-in bounded disc prefetch cache (`--prefetch`), and RAM
  dumps at trace stops. `run --json` reports the source size, VDL modulo, road
  coverage, autopilot state (including `autopilotMenuAdvance`), and CD status/poll
  for mode/crop, driver, and wait-state investigations.
- **Audio:** the DSP executes in every run, including headless runs with no host
  audio sink. The AudioFolio semaphore handshake works, instrument programs run, and
  the five-title corpus produces non-silent 44.1 kHz stereo WAV output (~737 samples
  per frame). Ninth-pass 600-frame captures: Wolfenstein peak 18100 / RMS ~2.2k,
  Lemmings 22283 / ~4.5k, Alone 20617 / ~1.9k, Road Rash 32768 / ~9.2k (378 clipped),
  Space Ace 4096 / 1832 (mono duplicated); see `TestCorpus.md` 1.5a.
- **Input scripting:** the CLI accepts `--input-script <path>` with
  `frame button[,button...]` lines for repeatable controller taps. Input has been
  validated in Road Rash (race state/wreck), Wolfenstein (menus, gameplay, level
  start), Alone (scene/menu/intro), Lemmings (menu and passcode entry), and Space Ace
  (X menu, A play).
- **Tests:** 775 passing (`dotnet test -c Release`), including high-resolution CEL and
  VDLP regression coverage, RAM-patch and Road Rash autopilot contracts, CLI process
  tests, zero-allocation uploader/mailbox assertions, OpenGL capability parsing,
  bounded prefetch-cache tests, game-fix mapping, save-slot metadata/roundtrips and
  thumbnails, keyboard/gamepad binding capture, gamepad mapping, log filtering,
  source-generated CLI JSON contracts, frontend startup-argument parsing, renderer
  backend selection, GPU-probe capture/parity contracts, and shader source
  invariants.
- **Packaging:** CI builds/tests/formats and publishes CLI/frontend for six RIDs.
  Both executables are self-contained, single-file, and trimmed (`PublishTrimmed=true`,
  `TrimMode=full`). The CLI also has an opt-in NativeAOT win-x64 publish
  (`-p:FiveDoNativeAot=true`): 9.97 MB, CI-smoke-tested with `--version`, `selftest`,
  `bench`, `info`, `fs list`, and a golden 3,000-frame boot. The frontend has an
  opt-in NativeAOT single-file publish (28.8 MB executable plus native assets) that
  passes `--smoke-test` and the WGL/ANGLE parity probes; Silk.NET.Core loader warnings
  remain and AOT audio is not yet validated. A Windows CI smoke test runs the trimmed
  CLI and starts the trimmed frontend (`--smoke-test`); the win-x64 packages were
  validated with synthetic/ISO/CHD runs, state roundtrips, `fs list`, and a real
  window/render pass.
  CI also creates portable `5do-<component>-<rid>.zip`/`.tar.gz` archives
  with README/notices/licensing and a generated `PACKAGE.txt` via
  `packaging/package.ps1`. See [`Trimming.md`](Trimming.md) and
  [`../packaging/README.md`](../packaging/README.md).

---

## 2. Real-game status (local corpus, `panafz1.bin`)

The 2026-10-24 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots, renders, plays DSP audio**; reaches the title screen and the passcode screen; with `--high-res` the 640x480 title renders all four buttons and the full logo, matching the reference. The Fun 4 code `HNLHCIOECW` is now typed entirely with the D-pad (`docs/scripts/lem-fun4-dpad.txt`); gameplay starts at 23,000 frames (`PC 0x00083FD8`, hash `0xB7846C2D`) and roundtrips (`0xEDEB1B9D` twice). 600-frame WAV evidence in `TestCorpus.md` 1.5a | Not re-tested this pass; expected to pass |
| Space Ace (USA) | **Boots, renders, plays DSP audio**; ReadySoft player runs the animated demo past 12,000 frames. The interactive path is now validated: X returns to the title/menu, A starts the scene; the 15,000-frame state roundtrips (`0x7E780D93` -> `PC 0x000284EC`, `0x70B16422` twice) and the gameplay WAV is non-silent | **Plays the demo intro for 60 s, no runaway** |
| Road Rash (USA) | **Boots, renders, plays DSP audio**; the 3D race renders correctly and the attract race runs past 60,000 frames with no allocator error; B accelerates, A brakes, the D-pad steers, C attacks, and P pauses. The CD media-access pulse fixes the old ~42k/~92k READ_ID-only wait: those screens advance to `BUSTED`, the race-select menu, and a new race. The autopilot now passes `GAME LOADING`/`Get ready` and cycles race -> wreck -> menu -> new race to 100k, but still does not finish a full 5.5-mile race | **Renders the intro FMV, then a race** |
| Wolfenstein 3D (USA) | **Boots, renders, plays DSP audio**; A opens the preview menu, selecting a level starts a fresh game (FLOOR 1-1), the player can walk/turn/fire, and save/load roundtrips in-game (`0xF568EBCA` -> `PC 0x000782B8` twice); 600-frame WAV evidence captured. The fresh-game load advances with A pulses (character select -> FLOOR 1-1 at 64,000, hash `0x2F5B9864`) | **Renders the intro for 60 s** |
| Alone in the Dark (USA) | **Boots, renders, plays DSP audio**; reaches the in-game staircase/room with an upright character; the Up/Left/A/Down sequence reaches the Begin/Resume menu, A starts the intro scene, A pulses pass the intro, and a scripted room movement sequence roundtrips (`0x44C7576B` -> two 120-frame continuations at `PC 0x0008BF00`, `0xE490C8C7`) | **Renders the intro/loading for 60 s** |

**Additional corpus (2026-10-26).** Seven more titles were booted, rendered, and
save-state-roundtripped with `panafz1.bin`: Gex, Need for Speed, Super Street
Fighter II Turbo, Total Eclipse, Killing Time, Theme Park, and Return Fire. Gex,
Total Eclipse, and Super Street Fighter II Turbo show gameplay/attract scenes;
Need for Speed, Killing Time, Theme Park, and Return Fire render menus. Six produce
non-silent 44.1 kHz stereo audio; Theme Park's captured menu is silent. Hashes and
commands are in [`TestCorpus.md`](TestCorpus.md) 1.7 and the matrix in
[`Compatibility.md`](Compatibility.md) §10.

The CD disc-end fix on 2026-10-23 is what unblocked Road Rash, Space Ace, and
Wolfenstein: `InitCd` no longer double-adds the 150-frame lead-in, so the CD DIPIR
accepts discs whose volume block count is less than the physical track length.

The 2026-10-25 eighth pass fixed a real-content renderer bug: the GPU palette shader
used the low five bits of each pixel as a single CLUT index for all three channels,
so games appeared grayscale. It now uses the red, green, and blue indices independently
and matches the CPU frame assembler. Lemmings gameplay was captured through
sharp-bilinear and xBR with correct colors. The xBR path uses a CPU-expanded source to
avoid a driver readback issue with the FBO-attached palette texture.

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
is upright and its Begin/Resume menu is reachable; Lemmings completes a Fun 4 passcode
(`HNLHCIOECW`) end-to-end into gameplay with save/load roundtrip, and its 640x480
high-resolution title matches the reference; Space Ace starts its interactive scene
(X -> menu, A -> play) and plays. Per-title save-state, PNG, and WAV evidence is
recorded in `TestCorpus.md` 1.5a.

The ninth pass corrected the Road Rash diagnosis and fixed the wait: the ~42k and
~92k stops were a static READ_ID-only CD wait, not wall pins or driving failures.
The CD device now raises a media-access event (`0x40`) after two consecutive
READ_ID polls with an idle gap; this advances those screens to `BUSTED`, race
select, and a new race. `run --json` exposes `cdStatus`/`cdPoll`, and the autopilot
reports `autopilotMenuAdvance` while it pulses A through the post-race flow. The
five 3,000-frame hashes are unchanged. From the 60k state the autopilot now starts
a new race at 80k and cycles race -> wreck -> menu -> new race to 100k; the
remaining Road Rash work is finishing a full race without wrecking, not the CD
wait. That is post-1.0 backlog; it is not a v1 release gate.

---

## 3. Performance snapshot

Release build, development machine, 600 frames, CPU side only:

| Case | Total | p50 | p95 | p99 | Allocations |
|---|---:|---:|---:|---:|---:|
| Synthetic | **1.250 ms/frame** | 1.218 ms | 1.434 ms | 1.638 ms | **0 bytes/frame** |
| Lemmings CHD | **1.281 ms/frame** | 1.088 ms | 2.005 ms | 3.398 ms | ~5.9 KB/frame (CHD decode) |
| Lemmings CHD, high-res | **1.605 ms/frame** | 1.398 ms | 2.497 ms | 4.598 ms | ~5.8 KB/frame (CHD decode) |

This leaves ~15.4 ms of headroom against the 16.67 ms frame budget for the synthetic
case. CI now runs the synthetic benchmark with `--gate`: it fails on any steady-state
allocation, core p99 > 8 ms, total mean > 8 ms, or total p99 > 16.667 ms, and uploads
the JSON result as the baseline artifact. The benchmark's `--baseline` option also
implements the same-machine >10% relative comparison (core p99, total mean, total
p99; exit code 4); wiring it to CI awaits a stable/self-hosted runner. **Tier B
(Pi 4/5) numbers and GLES timing are not measured yet.** CHD decode allocations are
host-side and are attributed to the core stage; the synthetic
core/pack/mailbox/read path is allocation-free in steady state.

The desktop renderer uses a `GL_TIME_ELAPSED` query and reports GPU/present time in
the FPS overlay. The fourteenth pass added a true-4K offscreen `--gl-probe` and
measured the presentation path on Windows 11 (Ryzen 9 7900, RTX 4070, OpenGL 4.0):

| 4K case (3840x2160) | Present mean | Present p99 | GPU mean | GPU p99 | Renderer alloc |
|---|---:|---:|---:|---:|---:|
| Sharp-bilinear | **0.080 ms** | **0.208 ms** | **0.086 ms** | **0.089 ms** | **0 B/frame** |
| Accurate | 0.082 ms | 0.178 ms | 0.091 ms | 0.287 ms | 0 B/frame |
| CRT | 0.147 ms | 1.157 ms | 0.191 ms | 0.193 ms | 0 B/frame |
| xBR 4x | 0.425 ms | 1.788 ms | 0.203 ms | 0.205 ms | 0 B/frame |

The platform swap interval is wired through `SwapInterval.TryCreate` (WGL/GLX/EGL);
the Windows WGL path resolves `WGL_EXT_swap_control` and reports `vsyncActive: true`
when requested. The Avalonia compositor paces the window independently, so the probe
reports ~64 fps throughput even when vsync is requested; the renderer cost itself is
far below the 16.67 ms budget.

### 4K60 feasibility

- **Desktop (Tier A): measured.** The core is ~1.25-1.6 ms/frame; the 4K presentation
  pass is one fullscreen textured quad after native-resolution palette expansion.
  The RTX 4070 measured GPU p99 0.089 ms and present p99 0.208 ms for sharp-bilinear,
  leaving ample headroom for 60 Hz. The remaining risk is the Avalonia/WGL compositor
  and frame pacing, not the emulator core.
- **Pi 5: likely, but unmeasured.** The presenter should handle 4K60 via GLES; the
  ARM CPU core may be the limiting factor in complex scenes.
- **Pi 4: 1080p60 is the realistic target.** 4K60 output is not assumed.
- **4K internal CEL rendering is not a v1 goal.** The target is 4K output, not 4K
  internal rendering.

CPU p99 and per-stage allocations are measured, and Tier A 4K GPU/present p99 is now
recorded. Remaining performance work is post-1.0: Raspberry Pi/GLES 3.1 and DPI
validation, Tier B timing, and the CI relative gate. See
[`Performance.md`](Performance.md).

---

## 4. How far from a release?

The original 157 developer-day plan assumed full compatibility hardening and a full
platform matrix. **v1.0 adopts a documented-compatibility release scope instead.**
Ship the platforms and titles that are validated, publish the per-title matrix and
known issues, and move the rest to the post-1.0 backlog. Most emulators release this
way; full compatibility is not a v1 requirement.

| Release workstream | State | Remaining |
|---|---|---:|
| M0-M8 core, CPU, boot, MADAM, DSP, CD, input, save states | Functionally complete | 0 |
| M9 Avalonia frontend | Feature-complete for the tested workflow | 0 (optional polish is post-1.0) |
| M9R OpenGL presenter | Windows WGL + ANGLE validated; CPU fallback, filters, parity, 4K probe | 0 on Windows; 1-2 dd if Linux/macOS are claimed |
| M9P performance/pacing | Tier A measured, zero-allocation steady state, CI gate | 0 (Tier B/Pi and hot-path are post-1.0) |
| M10 packaging | Six-RID CI, trimmed/AOT builds, portable archives | 0-1 dd for release packaging |
| M11 compatibility | 12 titles boot/render; five have scripted gameplay evidence; matrix and known issues documented | 2-3 dd soak on the supported set |
| M11 release/legal | Attribution and distribution model not decided | Legal review + ~1 dd prep |

**Estimate: 5-10 developer-days plus the legal review.** That is **1-3 weeks** to a
v1.0 release candidate with Windows x64 as the validated target and portable archives.
Adding validated Linux/macOS builds and installers makes it **2-4 weeks**. Against
this release scope the project is roughly **85-90% done**; the earlier "three-quarters"
figure measured the original full-compatibility plan (124/157 dd, ~43 dd remaining).

**Release-candidate decisions (2026-10-26):** Windows x64 is the validated target;
the candidate version is `1.0.0-rc.1`; installers, signing, and notarization are
deferred; the compatibility set is the 12 documented titles, with five showcase
titles carrying scripted gameplay evidence.

The only hard blocker is the distribution/attribution decision: FreeDO/4DO carry a
modified LGPL with restrictions, including a prohibition on commercial use without
owner approval. See [`Licensing.md`](Licensing.md),
[`ReleaseLegalReview.md`](ReleaseLegalReview.md), and
[`../THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md).

Full-compatibility work is **post-1.0**, not a release gate: finishing Road Rash's
5.5-mile race, scripting Alone's inventory, adding gameplay scripts for the other
seven titles, and further CD/CLIO/MADAM/ARM timing triage.

---

## 5. Release path

### P0 - v1.0 release blockers

1. **Decide the release scope.** Pick the supported platforms and titles and record
   them in [`ReleaseChecklist.md`](ReleaseChecklist.md). Recommended: Windows x64 as
   the validated target; publish Linux/macOS x64 only after the launch/render smoke
   test passes; defer arm64 and Raspberry Pi to 1.1.
2. **Complete the legal/attribution review.** Decide the distribution model, keep the
   FreeDO/4DO/Opera attribution current, and confirm no BIOS/disc content is packaged.
3. **Run the stability soak.** Ten-minute runs on the five showcase titles
   (Wolfenstein, Lemmings, Road Rash, Alone, Space Ace) and the seven boot-validated
   titles; no new crashes or regressions. Document known hang points rather than
   fixing every game.
4. **Publish the honest compatibility matrix.** Use Playable / Boots-menus / Known
   issues columns in [`Compatibility.md`](Compatibility.md), refresh
   [`KnownIssues.md`](KnownIssues.md), and update the user guide.
5. **Cut the release.** Bump the version, update `CHANGELOG.md`, write release notes,
   produce checksums and portable archives for the supported RIDs, and run the clean
   build/test/format checks. Work through [`ReleaseChecklist.md`](ReleaseChecklist.md).

### P1 - post-1.0 backlog (not release-blocking)

6. **Gameplay depth:** finish a full Road Rash race, script Alone's inventory and a
   longer exploration sequence, and add input scripts for Gex, Need for Speed, Super
   Street Fighter II Turbo, Total Eclipse, Killing Time, Theme Park, and Return Fire.
7. **Platforms:** Raspberry Pi 4/5 GLES 3.1, arm64 builds on real hardware,
   100/150/200% DPI validation, and Linux/macOS OpenGL launch validation if not
   claimed for v1.
8. **Performance:** Tier B 1080p60 numbers, 3D CEL frame timing, hot-path
   optimization, and the same-machine relative benchmark gate on stable CI hardware.
9. **Packaging polish:** MSIX/dmg/AppImage installers, signing/notarization,
   Windows/macOS file associations, and AOT audio validation.
10. **Optional M12 guest ARM JIT:** profile first; not needed for v1.

---

## 6. Next session

The next session is **release prep**, not a compatibility pass. The fifteenth code pass
completed the Windows ANGLE/GLES backend validation and CPU/GPU parity work; start with
[`ReleaseChecklist.md`](ReleaseChecklist.md):

1. Decide and record the v1 platforms, supported titles, version, and distribution
   model.
2. Run the 10-minute soak on the supported set and record crashes, hangs, and hashes.
3. Refresh [`Compatibility.md`](Compatibility.md) and
   [`KnownIssues.md`](KnownIssues.md) with honest per-title statuses.
4. Cut the release: version, changelog, release notes, archives, checksums. Run the
   legal/attribution review in parallel because it is the hard blocker.

Do not re-enable the null audio-sink short-circuit, do not change the XBUS DMA byte
order (the raw `IRam` order is correct), do not port `opera_cdrom.c` wholesale, and do
not redo the trimmed CLI/frontend or source-generated JSON work; see
[`Trimming.md`](Trimming.md). Do not treat full-race completion, scripts for the other
seven titles, Pi/arm64 validation, or installers as v1 release gates; they are
post-1.0 backlog.

---

## 7. Reproduce

```powershell
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release

# The corpus root is machine-specific; set it once per shell.
$env:FIVEDO_GAMES = "D:\Samples\3DO"   # office; home: E:\Panasonic 3DO
$env:FIVEDO_DISC  = "$env:FIVEDO_GAMES\Road Rash (USA).chd"

# Build/test/format
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release
dotnet format --verify-no-changes

# Synthetic CPU-side baseline
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- --frames 600

# Real disc baseline
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin `
  --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" --label lemmings

# Boot a disc and dump the last frame
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --nvram artifacts/lemmings.ram --frame-out artifacts/lemmings.png

# Scripted input, frame sequence, and PBUS packet trace
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Wolfenstein 3D (USA).chd" --bios bios/panafz1.bin `
  --frames 12000 --input-script artifacts/input.txt `
  --capture-dir artifacts/w3d-frames --capture-every 1000 `
  --frame-out artifacts/w3d.png --state-out artifacts/w3d.state

# RAM dump at a KPRINT stop
dotnet run -c Release --project src/FiveDo.Cli -- trace `
  "$env:FIVEDO_DISC" --bios bios/panafz1.bin `
  --frames 16000 --trace-frames 2200 --stop-kprint "AllocMem error" --ring 40000 `
  --out artifacts/rr-trace.txt --dump-ram artifacts/rr-stop-ram.bin `
  --dump-ram-offset 0 --dump-ram-length 0x300000

# Opera reference: extract the data track to ISO, then run the bundled SimpleGui
dotnet run -c Release --project src/FiveDo.Cli -- disc dump `
  "$env:FIVEDO_DISC" --sector 0 --count 328130 `
  --out "$env:TEMP\roadrash.iso"
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\roadrash.iso"
```

The desktop frontend is `dotnet run -c Release --project src/FiveDo.Frontend.Avalonia`.
See [`ReferenceStrategy.md`](ReferenceStrategy.md) for the full differential procedure.
