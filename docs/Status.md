# Project Status and Next Steps

**Snapshot:** 2026-11-01, after the first full-collection sweep, the performance pass,
the ARM/CLIO/CEL/CD timing-option pass, the CD speed/timing-profile pass, the BIOS
manager, the per-game NVRAM pass, the rewind/fast-forward pass, and the per-game cheat
engine. Road Rash's
100k-state race now measures
4.20 ms/frame total (p99 7.40 ms),
Debug core builds are optimized, and Auto presentation frame skip is the default.
**594 discs tested: 576 Loadable/Rendered, 13 Loadable/No video, 5 Not Loadable.**
The five not-loadable rows are resolved or explained: Mirai Shounen Conan now runs
(transient CHD read error), and the four Arcade CD-ROM titles load with
`panafz10-patched.bin` plus the default headless pad. The 12,000-frame rerun shows that six of the
13 black/blank discs render by 6,000-9,000 frames; the rest are BIOS-sensitive or
still under investigation. Headless `run`/`trace` now attach a default player-1 pad,
fixing Olympic Summer Games' controller check. Fifteen triaged titles now have a
reviewed 5DO status in [`GameCompatibilityList.md`](GameCompatibilityList.md); the
rest remain `No data`. The CORE-01 ARM timing option (`--arm-timing freedo|arm60`), its
save-state snapshot, `--cpu-clock 0.5x-4x`, and `--random-seed` are implemented with
tests. The CORE-02 CLIO timer option (`--clio-timers freedo|opera`) now implements
Opera's per-slot scan and cascade carry behind the safe FreeDO-compatible default, with
a state snapshot and a 3,000-frame smoke differential in `TestCorpus.md` 1.1b.
The CORE-03 CEL option (`--cel-timing freedo|opera`) now implements Opera's deferred
draw and pixel/texel busy deadline behind the unchanged synchronous default, with a
Madam section v2 busy snapshot, an Options v3 mode snapshot, and a three-title
3,000-frame smoke differential in `TestCorpus.md` 1.1c.
The CORE-04 CD option (`--cd-timing freedo|opera`) now implements Opera's cycle-based
2048-byte data-sector readiness at the stock 2x drive speed, including delayed data
polling, deferred read-data status, the XBUS CD FIQ, a CD-ROM section v2 clock
snapshot, and an Options v4 mode snapshot. The immediate FreeDO path remains the
default; the three-title smoke differential is in `TestCorpus.md` 1.1d.
SYS-09 adds `--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x` (Settings > Emulation > CD
speed), Opera's per-speed overhead table, an Options v5 speed snapshot with a mismatch
warning, and the three-title 3,000-frame smoke differential in `TestCorpus.md` 1.1e.
CORE-08 adds `EmulationProfile` (`Compatible`, `Accuracy`, `Performance`, `Custom`),
`--profile`, and Settings > Emulation > Timing profile; the accuracy profile enables the
ARM60, Opera timer, CEL busy, and CD cycle options together, and `run --json` reports the
profile derived from the actual options.
SYS-14 adds per-game NVRAM and versions. `NvramStorageMode` (`Global`, `PerGame`),
`NvramVersion` (0-9), and `NvramDirectory` select shared `nvram.ram`, versioned
`nvram.<n>.ram`, or `nvram/per-game/<gameId>.<n>.ram` images. The store reads migration
fallbacks from per-game version 0, the shared version, and the legacy image; saves stay
atomic, and a corrupt primary is backed up rather than replaced by an older image.
Settings > Emulation exposes the mode and version. The CLI adds `--nvram-mode`,
`--nvram-version`, and `--nvram-dir`, reports the resolved path in `run --json`, and
adds `nvram path|list|export|import|reset`.
SYS-04 adds the BIOS manager: a built-in MD5 catalog, `BiosLibrary` scanning/validation,
region/Kanji/Anvil/patch metadata, `--bios2` and `--bios-auto`, `bios list|scan|select`,
Settings > Paths BIOS folder/auto-select/scan, per-game BIOS overrides, and the
Settings > Emulation override combo. Auto-selection uses the game database region plus
known requirements for Slayer Europe/USA, Powers Kingdom (Europe), and Arcade CD-ROM
discs; the configured BIOS remains the fallback when no BIOS folder is set. The
five-title 3,000-frame smoke run and four known-requirement 12,000-frame runs are in
[`TestCorpus.md`](TestCorpus.md) 1.1f. The first Opera parity batch (VDLP, ARM carry, DSP FIFO, MADAM span/MARIA/BPP0/SKIPX/LRFORM/blue-LSB/REGCTL/PIXC, CLIO FIFO), OP-14 (XYPOSH/XYPOSL origin, retired `GraphicsStepY`/`BitTiming6`), OP-SYS-02 (Opera CD poll/local reset), OP-SYS-01/03's ROM overlay, DIPIR, XBUS, and CSTAT soft-reset work, and OP-SYS-06's separate CLIO PRNG are complete (the DIPIR/soft-reset path remains opt-in pending a boot transcript); see `Make5doTheBestOfAll.md` 13.11–13.17.
UX-01/UX-02 add rewind and fast-forward. `RewindBuffer` keeps compressed keyframes plus
the per-frame PBUS packet, replays input deterministically after the nearest keyframe,
clears on timeline jumps, and enforces a 256 MB default cap; `CoreRunner.Rewind` restores
the machine and presents the frame. `CoreRunner.SpeedMultiplier` runs 2x-10x frames per
presentation tick with intermediate rendering skipped and audio muted. The shell binds
`R` (one-second rewind), `Shift+F9` (one-frame rewind), and hold-`Tab` (fast-forward);
Settings > Emulation controls the history length and multiplier. UX-04 adds per-game RAM
cheats: `FiveDo.Cheats` parses raw, typed, conditional, and button-activated codes;
`CheatStore` owns the per-game JSON files; `FiveDoMachine.BeforeFrame` applies them every
frame (including rewind replay and fast-forward); the shell adds **Emulation > Cheats...**
and Settings > Paths > Cheats; the CLI adds `--cheat`, `--cheat-code`, `--cheat-auto`,
`--cheats-dir`, and `cheat list|add|enable|disable|remove|import|export`. **1199 tests pass.**
The differential harness (FND-05/CORE-11/CORE-16) is implemented:
`tools/FiveDo.DiffHarness` stages the bundled OperaCore and BIOS, runs both engines
with identical options, compares checkpoint frame hashes, applies manifest
allowances, and writes JSON/Markdown/HTML reports. The Lemmings (USA) 300-frame
smoke matches at frame 300 (`0x110E7DC5` on both engines); the five-title showcase
manifest is `benchmarks/diff/showcase.json`, and its first 3,000-frame run
video-matched Wolfenstein 3D and Alone in the Dark while Lemmings, Gex, and Road
Rash diverged. The strict PC gate shows 0/5 matches: the video matches still have
PC deltas of 184,696 and 739,184 bytes, so frame hashes alone are not sufficient
evidence. The `manifest` command generated the portable 594-title
`benchmarks/diff/full.json`. See
[`DifferentialHarness.md`](DifferentialHarness.md) and
[`TestCorpus.md`](TestCorpus.md) 1.1g. The remaining work is the full-collection
run, state/CD transcript fields, and the nightly baseline.
The v1.0 release is deliberately deferred: the next work is a BIOS-aware sweep and
5DO status merge (M13), then high-resolution CEL research (M14). The project is
**donationware** (non-commercial, source-available, voluntary donations only). See
[`CompatibilitySweep.md`](CompatibilitySweep.md),
[`GameCompatibilityList.md`](GameCompatibilityList.md), and
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
**Release definition:** documented compatibility, not full compatibility; see
[`ReleaseChecklist.md`](ReleaseChecklist.md).
**Scope:** pure C# 3DO emulator, desktop GL/GLES presenter, optional Vulkan backend, CLI, and deterministic tests.
**Reference:** `References/opera-libretro-master/libopera` is now the primary
behavioral reference for new compatibility work; see
[`ReferenceStrategy.md`](ReferenceStrategy.md).

> **2026-10-30 priority change:** the v1.0 release is deliberately deferred. The next
> active work is **M13, a full-collection loadability and in-game sweep** of
> `J:\Panasonic 3DO` (589 CHDs) and `G:\MAME\MAME Software List CHDs\3do` (5 CHDs),
> followed by **M14, research into true internal high-resolution CEL rendering**.
> Gameplay-depth work (full races, inventory interaction, long playthroughs) is not
> part of this phase. The project is **donationware**: non-commercial,
> source-available, and funded by voluntary donations rather than sales. The Linux
> validation target is the local Hyper-V VM `LinuxMint`
> (`D:\Hyper-V\VMs\LinuxMint`). See [`CompatibilitySweep.md`](CompatibilitySweep.md),
> [`GameCompatibilityList.md`](GameCompatibilityList.md), and
> [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

This is the short, current answer to "where are we?" The documentation index is
[`README.md`](README.md). The detailed task list is in
[`ImplementationPlan.md`](ImplementationPlan.md), the performance contract in
[`Performance.md`](Performance.md), the local game corpus in
[`TestCorpus.md`](TestCorpus.md), the release gate in
[`ReleaseChecklist.md`](ReleaseChecklist.md), and the next-session plan in
[`NextSession.md`](NextSession.md).

---

## 1. What works today

- **Core:** ARM60 interpreter, 3 MB RAM/2 MB ROM, shared/per-game NVRAM, CLIO timers/FIQ/FIFO/DMA,
  MADAM CEL/pixel/matrix engines, VDLP, SPORT, DSP, Quartz scheduler, XBUS, CD-ROM,
  ISO/BIN/CUE/CHD storage, input/PBUS, game database, deterministic save states.
- **Save states:** state format version 2 compresses large section payloads with Brotli
  while keeping the CRC over the uncompressed data. Version-1 states still load. A
  24,902,592-byte local state becomes 647,647 bytes (300-frame Gex) or 2,001,229 bytes
  (14,900-frame Gex); the section inspector reads the header without decompressing
  payloads, and `state inspect` reports stored/compressed sizes.
- **BIOS:** `panafz1.bin` boots, runs the splash/animation, initializes the OS, and
  reaches the shell. The earlier `PC 0x205C4` "halt" was the Portfolio idle loop.
  SYS-04 adds a BIOS manager: `BiosLibrary` scans `.bin`/`.rom` files, computes MD5, and
  classifies region, video standard, Kanji, Anvil, and patch state; the built-in catalog
  covers the common FZ-1/FZ-10/FZ-10E/FZ-10JA, GoldStar, Sanyo, patched arcade, Kanji
  font, and arcade support images. `--bios-auto` and Settings > Paths > Auto-select BIOS
  choose a region-appropriate primary plus optional BIOS2; `--bios2` loads an explicit
  secondary. `bios list|scan|select` expose the catalog, and per-game BIOS overrides are
  stored by 4DO game ID. The configured `BiosPath` remains the fallback, so existing
  settings keep their exact BIOS.
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
  Lemmings' title screen now matches the 640x480 reference. This is not true vertical
  640x480 rendering; the internal high-resolution research is M14. The mode is off by default
  because 320x240 titles must not be upscaled, and the desktop shell exposes it as a
  Hi-Res checkbox that restarts the machine. An opt-in xBR-lv2 4x filter, HQx
  2x/3x/4x filters, and CRT bloom are available in Settings; xBR and HQx use a
  CPU-expanded native source and sharp-bilinear finishing, and CRT bloom adds a 3x3
  native-resolution glow in the CRT filter. The HQx 3x/4x switches match the C++
  reference hashes and are the most expensive opt-in modes.
- **Performance:** the machine runs on a dedicated core thread paced by the 44.1 kHz
  audio clock (60 Hz fallback) with a frame pacer; the UI renders on demand; uploads use `TexStorage2D` + `TexSubImage2D`;
  uniform locations and the present delegate are cached; physical-pixel sizing is
  implemented and unit-tested at 100/125/150/200% scaling. The benchmark reports
  p50/p95/p99 and per-stage allocations, and the
  synthetic steady state is 0 bytes/frame. An opt-in bounded prefetch cache is
  available for CHD/ISO streaming. The `--gl-probe` validates both WGL and
  ANGLE/GLES: 4K sharp-bilinear presents in 0.14 ms mean / 0.47 ms p99 with 0
  renderer allocations/frame on the RTX 4070; a 320x240 Accurate capture matches
  the CPU oracle on both backends.
- **Optional Vulkan:** `FiveDo.Video.Vulkan` is an offscreen Vulkan 1.0 two-stage
  presenter with build-time SPIR-V shaders, sampled R16_UINT/CLUT/parameter
  textures, accurate/nearest/sharp-bilinear/CRT/xBR/FSR/HQx filters, GPU timestamp
  queries, and a host readback buffer. xBR-lv2 runs as a 4x GPU pass; FSR 1.0 runs
  EASU plus RCAS; HQx uses the shared CPU expansion. `--vk-probe` writes the shared
  GPU probe JSON and PNG captures; `--vk-window-probe` creates a native Win32
  swapchain, presents through FIFO vsync, and writes the same report with
  `offscreen: false`; `--gl-backend vulkan` hosts the optional desktop Win32 child
  surface. The 320x240 Accurate probe matches the CPU oracle
  (`0xD6BD89C5`, 0 different pixels); 4K sharp-bilinear measures 0.24 ms mean /
  1.06 ms p99 present and 0.075/0.091 ms GPU, 4K xBR 0.32/0.80 ms present and
  0.13/0.14 ms GPU, and 4K FSR 0.60/1.30 ms present and 0.40/0.42 ms GPU, all with 0
  managed bytes/frame on the RTX 4070. The 4K window probe reports 0.077/0.088 ms GPU
  and 5.96/6.53 ms present (including the 165 Hz FIFO wait), 0 managed bytes/frame.
  HQx 2x/3x/4x use the shared CPU expansion (4K HQx present 6.82/10.93 ms, CPU-bound),
  and compositor-embedded/X11/Wayland/Metal surfaces are future work. See
  [`Vulkan.md`](Vulkan.md).
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
  settings dialog. The shell dispatches the configurable `InputProfile.Hotkeys`
  bindings (F5 save, F8 load, F6/F7 previous/next slot, F9 frame advance, F10
  reset, F11 fullscreen, F12 screenshot, Escape pause by default) before forwarding
  keys to the emulated pad. **Emulation > Record Input** captures player-1 input to
  a replayable script. Loading a state
  whose recorded disc hash differs from the current disc reports a warning in the
  status line and log instead of loading silently.
- **Logging and bug reports:** Serilog is configured once by each host through
  `FiveDo.Diagnostics.Logging`. The pipeline writes rolling daily files, stderr
  (desktop), the in-memory **Log Viewer**, and an asynchronous bug-report sink for
  `Warning`-or-higher events. Reports include the required environment, error, and
  exception blocks; submission is deduplicated for five minutes, capped at ten per
  minute, and disabled in CI/test runs. `FIVEDO_BUG_REPORTS=0` disables it and
  `FIVEDO_LOG_DIR` redirects the files.
- **Cheats:** per-game JSON cheat files are applied before every frame by
  `FiveDoMachine.BeforeFrame`, so state loads, rewind replay, and fast-forward keep the
  configured entries active. The engine supports raw byte writes, typed
  `write8`/`write16`/`write32` writes, memory conditions, player-1 button conditions, and
  `and`/`or`/`not` combinations. Emulation > Cheats... and Settings > Paths > Cheats
  manage files; the CLI exposes `cheat list|add|enable|disable|remove|import|export` and
  `run --cheat`/`--cheat-code`/`--cheat-auto`. See [`Cheats.md`](Cheats.md).
- **Per-game fixes:** `GameCompatibility` maps the 4DO game IDs to `FixMode` bits.
  The CLI exposes `--fix-mode 0xMASK` and `--no-game-fixes`, and `run --json` /
  `info` report the applied mask. Alone in the Dark's `FIX_BIT_TIMING_6` is applied
  automatically; the current 3,000-frame boot keys are in
  [`TestCorpus.md`](TestCorpus.md) 1.1 after the audio-clock rebaseline.
- **Tooling:** CLI `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks`
  (`disc tracks` covers ISO/BIN/CUE as well as CHD), `fs list/extract` (the
  first-party Opera parser covers ISO/BIN/CUE/CHD),
  `disasm`, plus KPRINT, CD, CLIO, FIQ, RAM-write watchpoints,
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
  the corpus produces non-silent 44.1 kHz stereo WAV output. The DSP sample clock is
  now exact: 44,100 samples per emulated second (600 frames = 441,015-441,020
  samples, 10.000 s), replacing FreeDO's 8.24 accumulator that ran ~0.7% fast
  (444,097 samples / 10.070 s). Road Rash's 6,000-frame capture has the same peak,
  RMS, and spectral peaks as before; only the timebase changed. Live output uses
  a bundled OpenAL Soft 1.23.1 runtime (no system OpenAL install required), makes
  the context current on the audio thread, pads underruns, and reports
  dropped/underrun/queued counters. The fill loop now rents only buffers OpenAL
  has released (the old queued-count index rewrote buffers still attached to the
  playing source and caused constant crackle), and `FIVEDO_AUDIO_TAP=<prefix>`
  records the exact queued PCM plus a per-buffer timing log. `CoreRunner` paces frames against the audio
  clock (with a 60 Hz fallback), the CLI can play live with `--audio openal
  [--realtime]`, and `audio analyze <wav> --json` reports peak/RMS/clipping/DC
  statistics. Exact-clock 600-frame captures: Wolfenstein 18,100 / 2,168,
  Lemmings 8,192 / 1,661, Gex 12,964 / 2,089, Road Rash 32,768 / 11,012 (2,036
  clipped), Theme Park silent; see `TestCorpus.md` 1.5a2. The trimmed win-x64 and
  linux-x64 CLI and the opt-in NativeAOT CLI open the same backend.
- **Input scripting:** the CLI accepts `--input-script <path>` with
  `frame button[,button...]` lines for repeatable controller taps. Input has been
  validated in Road Rash (race state/wreck), Wolfenstein (menus, gameplay, level
  start), Alone (scene/menu/intro), Lemmings (menu and passcode entry), and Space Ace
  (X menu, A play).
- **Tests:** 1199 passing (`dotnet test -c Release`), including high-resolution CEL and
  VDLP regression coverage, RAM-patch and Road Rash autopilot contracts, CLI process
  tests, zero-allocation uploader/mailbox assertions, OpenGL capability parsing,
  bounded prefetch-cache tests, game-fix mapping, save-slot metadata/roundtrips and
  thumbnails, keyboard/gamepad binding capture, gamepad mapping, log filtering,
  source-generated CLI JSON contracts, frontend startup-argument parsing, renderer
  backend selection, GPU-probe capture/parity contracts, shader source invariants,
  Vulkan xBR/FSR/HQx split-frame orientation, save-state disc-hash warnings, hotkey
  routing, input-recorder coalescing/roundtrip, ISO/BIN `disc tracks`, Opera FS parsing
  (multi-block directories, `burst`/`gap` reads, invalid headers, and CLI ISO
  extraction), and BIOS catalog/scan/selection/override tests.
- **Packaging:** CI builds/tests/formats and publishes CLI/frontend for six RIDs.
  Both executables are self-contained, single-file, and trimmed (`PublishTrimmed=true`,
  `TrimMode=full`). The CLI also has an opt-in NativeAOT win-x64 publish
  (`-p:FiveDoNativeAot=true`): 10.54 MB, CI-smoke-tested with `--version`, `selftest`,
  `bench`, `info`, `fs list`, a golden 3,000-frame boot, and live OpenAL audio. The
  frontend has an opt-in NativeAOT single-file publish (28.8 MB executable plus native
  assets) that passes `--smoke-test` and the WGL/ANGLE parity probes; the bundled
  OpenAL path was validated with `ALSOFT_DRIVERS=null`, and Silk.NET.Core loader
  warnings remain. A Windows CI smoke test runs the trimmed
  CLI and starts the trimmed frontend (`--smoke-test`); the win-x64 packages were
  validated with synthetic/ISO/CHD runs, state roundtrips, `fs list`, and a real
  window/render pass.
  CI also creates portable `5do-<component>-<rid>.zip`/`.tar.gz` archives
  with README/notices/licensing, the OpenAL Soft LGPL notice/source offer
  (`docs/OpenALSoft.md`), the full LGPL-2.0 text
  (`docs/licenses/OpenAL-Soft-COPYING.txt`), and a generated `PACKAGE.txt` via
  `packaging/package.ps1`. See [`Trimming.md`](Trimming.md) and
  [`../packaging/README.md`](../packaging/README.md).

---

## 1a. Latest compatibility sweep (2026-10-30)

The first full loadability pass over both collections is complete. It booted every
disc for 3,000 frames and recorded JSON/PNG evidence under `artifacts/compat-full/`.
The committed summary is [`GameCompatibilityResults.md`](GameCompatibilityResults.md).

| Result | Count | Meaning |
|---|---:|---|
| `Loadable/Rendered` | **576** | Completed 3,000 frames; final frame is non-black. |
| `Loadable/No video` | **13** | Completed 3,000 frames; final frame is black/blank. |
| `Not Loadable` | **5** | CLI error or 90-second timeout. |
| **Total** | **594** | 589 main collection + 5 MAME software-list images. |

The five `Not Loadable` results:

- `Crime Patrol (USA) (Arcade CD-ROM)` — 90-second timeout.
- `Mad Dog II - The Lost Gold (USA) (Arcade CD-ROM)` — 90-second timeout.
- `Mirai Shounen Conan - Digital Library (Japan)` — CHD decompression failure at LBA 1.
- `Orbatak (USA) (Arcade CD-ROM)` — 90-second timeout.
- `Shootout at Old Tucson (USA) (RE3) (Arcade CD-ROM)` — 90-second timeout.

The four arcade CD-ROM timeouts match the 4DO notes that these titles need a patched
BIOS and have control limitations. They need a longer-timeout rerun and a reference
comparison before being called unplayable. The Mirai Shounen Conan image needs a CHD
integrity check or re-acquisition.

The 13 `Loadable/No video` titles need the 12,000-frame in-game rerun: `Crash 'n Burn
(Japan)`, `Ishida Yoshio Kudan no Igo Seiha (Japan)`, `Konpeki no Kantai (Japan)`
(original and Rev 1), `Myst` (Japan, Japan Rev A, USA), `Olympic Summer Games (USA)`,
`Powers Kingdom (Europe)`, `Slayer` (Europe, USA, USA Action Pak), and `WorldCup Super
Stadium (Japan)`.

`Loadable/Rendered` is **not** a final compatibility grade; it still needs the visual
and input review described in [`CompatibilitySweep.md`](CompatibilitySweep.md).

## 2. Real-game status (local corpus, `panafz1.bin`)

The table below is the curated five-title showcase set. The full local collection
(594 images across `J:\Panasonic 3DO` and `G:\MAME\MAME Software List CHDs\3do`) is
being swept in M13; the working matrix is
[`GameCompatibilityList.md`](GameCompatibilityList.md).

The 2026-10-24 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots, renders, plays DSP audio**; reaches the title screen and the passcode screen; with `--high-res` the 640x480 title renders all four buttons and the full logo, matching the reference. The Fun 4 code `HNLHCIOECW` is now typed entirely with the D-pad (`docs/scripts/lem-fun4-dpad.txt`); gameplay starts at 23,000 frames (`PC 0x00083FE8`, hash `0x63C02DED`) and roundtrips (`PC 0x000230A4`, `0x4CA339CD` twice). 600-frame WAV evidence in `TestCorpus.md` 1.5a | Not re-tested this pass; expected to pass |
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
sharp-bilinear and xBR with correct colors. The 2026-10-29 pass removed the former
CPU-expanded xBR source; xBR now samples the GPU palette texture directly.

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
3,000-frame keys were later re-baselined by the exact audio clock; see
[`TestCorpus.md`](TestCorpus.md) 1.1. From the 60k state the autopilot now starts
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
| Road Rash 3D race (100k state) | **4.20 ms/frame** | 2.15 ms | 7.13 ms | 7.40 ms | **0 bytes/frame** |

This leaves ~15.4 ms of headroom against the 16.67 ms frame budget for the synthetic
case. The 2026-10-31 hot-path pass reduced the Road Rash race from 5.46 to 4.20 ms
mean and from 13.14 to 7.40 ms p99 (frame hash unchanged). The core projects are also
compiled with JIT optimizations in Debug, so an IDE Debug run is no longer limited to
~40 fps. CI now runs the synthetic benchmark with `--gate`: it fails on any steady-state
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
| xBR 4x | 0.283 ms | 1.466 ms | 0.185 ms | 0.324 ms | 0 B/frame |
| FSR (EASU + RCAS) | 0.132 ms | 1.191 ms | 0.441 ms | 0.483 ms | 0 B/frame |

The xBR row now uses the GPU-expanded palette texture directly; the previous path
uploaded a CPU `FrameAssembler` copy. The optional Vulkan offscreen backend was also
measured on the same machine: 4K sharp-bilinear present 0.24 ms mean / 1.06 ms p99,
GPU 0.075/0.091 ms, 0 managed bytes/frame; 4K xBR present 0.32/0.80 ms, GPU
0.13/0.14 ms; 4K FSR present 0.60/1.30 ms, GPU 0.40/0.42 ms; 4K HQx 2x present
6.82/10.93 ms, GPU 0.13/0.18 ms; 1080p sharp-bilinear present 0.17/0.79 ms, 1080p xBR
0.21/0.38 ms, and 1080p FSR 0.27/0.78 ms, all with 0 managed bytes/frame. GPU time
excludes the optional per-frame readback. Its 320x240 Accurate output matches the CPU
oracle (`0xD6BD89C5`, 0 different pixels). See [`Vulkan.md`](Vulkan.md).

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

**As of 2026-10-30 the release is deferred.** The project is not cutting a candidate
until M13 gives every local disc a loadability/in-game status and M14 records a
high-resolution CEL decision. The old 5-10 day release estimate is therefore historical;
the current work is compatibility and video quality, not release mechanics.

| Release workstream | State | Remaining |
|---|---|---:|
| M0-M8 core, CPU, boot, MADAM, DSP, CD, input, save states | Functionally complete | 0 |
| M9 Avalonia frontend | Feature-complete for the tested workflow | 0 (optional polish is post-1.0) |
| M9R OpenGL presenter | Windows WGL + ANGLE validated; CPU fallback, filters, parity, 4K probe | 0 on Windows; Linux VM validation in M13.7 |
| M9P performance/pacing | Tier A measured, zero-allocation steady state, CI gate | 0 (Tier B/Pi and hot-path are post-1.0) |
| M10 packaging | Six-RID CI, trimmed/AOT builds, portable archives | 0-1 dd for release packaging |
| M11 compatibility | 12 titles boot/render; five have scripted gameplay evidence; matrix and known issues documented | Full-collection sweep in M13 |
| **M13 compatibility sweep** | **Tooling + 241-title reference list ready** | **10-15 dd: sweep, triage, visual check, Linux VM** |
| **M14 high-res CEL research** | **Research plan written** | **10-20 dd: measure, prototype, decide** |
| M11 release/legal | OpenAL notice/source offer/relinking record and license text ship in every archive; distribution model decided | Rights-holder/owner legal sign-off |

**Historical estimate:** 5-10 developer-days plus legal review for the old release
scope, i.e. 1-3 weeks to a v1.0 candidate. Against that scope the project was roughly
**85-90% done**. That estimate no longer sets the schedule; M13/M14 are the active
workstreams.

**Release-candidate decisions (2026-10-26, deferred):** Windows x64 is the validated
target; the candidate version is `1.0.0-rc.1`; installers, signing, and notarization
are deferred; the compatibility set is the 12 documented titles, with five showcase
titles carrying scripted gameplay evidence. The distribution model is now recorded as
**donationware** (non-commercial, source-available).

The project license is recorded in [`../LICENSE`](../LICENSE) as the FreeDO modified
GNU LGPL, so release builds are **donationware**: non-commercial, source-available, and
funded by voluntary donations rather than sales. The remaining hard blocker is
rights-holder clarification and commercial-use sign-off: FreeDO/4DO carry a modified
LGPL with restrictions, including a prohibition on commercial use without owner
approval. The OpenAL Soft LGPL-2.0-or-later binary-distribution obligation is handled
on the engineering side: the notice, complete license text, three-year source offer,
and relinking instructions ship in [`OpenALSoft.md`](OpenALSoft.md) and
[`licenses/OpenAL-Soft-COPYING.txt`](licenses/OpenAL-Soft-COPYING.txt). See
[`Licensing.md`](Licensing.md), [`ReleaseLegalReview.md`](ReleaseLegalReview.md), and
[`../THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md).

Gameplay-depth and full-compatibility work is **post-release**, not a current gate:
finishing Road Rash's 5.5-mile race, scripting Alone's inventory, adding gameplay
scripts for the other seven titles, and further CD/CLIO/MADAM/ARM timing triage. The
current gates are the M13 collection sweep and the M14 high-resolution decision.

---

## 5. Current path (compatibility and video quality)

### P0 - active work

1. **M13 loadability sweep: complete.** 594 discs tested: 576 `Loadable/Rendered`,
   13 `Loadable/No video`, 5 `Not Loadable`. See
   [`GameCompatibilityResults.md`](GameCompatibilityResults.md).
2. **Failure triage: first pass complete (2026-10-30).**
   - `Mirai Shounen Conan` no longer reproduces its LBA 1 CHD error; repeated
     3,000-frame runs reach the menu with hash `0xBE2A023B`.
   - The four Arcade CD-ROM rows are not timeouts: they render game content with
     `panafz10-patched.bin` and the default pad. Unpatched `panafz1`/`panafz10` reach
     only a starfield or the 3DO storage-manager shell.
   - The 12,000-frame rerun shows Ishida, both Konpeki no Kantai discs, and all three
     Myst discs rendering by 6,000-9,000 frames.
   - Crash 'n Burn, Powers Kingdom (Europe), Slayer, and WorldCup Super Stadium are
     BIOS-sensitive. Crash/Powers/WorldCup change their boot screens with the Anvil
     BIOS; Powers reaches a live-action intro with Anvil, Slayer Europe renders with
     `panafz1`, and Slayer USA reaches its title menu at 60,000 frames with `panafz10`.
     Crash and WorldCup are still stuck at 60,000, so the Anvil BIOS is not a fix for
     them.
   - Olympic Summer Games was blocked by the headless PBUS bug, now fixed by the
     default player-1 pad.
3. **Run the BIOS-aware sweep and fill the status columns.** Rerun the `Not Loadable`
   and `Loadable/No video` subsets with `tools/bios-probe.ps1` or `--bios-auto bios`
   (`panafz10-patched` and the Anvil BIOS), record the BIOS used per disc, then assign
   `Unplayable`/`Playable`/`Good` in
   [`GameCompatibilityList.md`](GameCompatibilityList.md). Keep Crash 'n Burn (Japan)
   and WorldCup Super Stadium on the investigation list.
4. **Run the high-resolution pass.** Repeat the sweep with `-HighRes`, compare
   screenshots and speed, and feed the results into M14.
5. **Validate Linux.** Build/test/smoke the CLI in the `LinuxMint` Hyper-V VM, run a
   reduced sweep, and record the GL renderer/fallback result.

### P1 - research and later work

6. **M14 high-resolution CEL research:** measure the current mode, prototype true
   640x480 and 2x supersampling, and write the GPU CEL feasibility note.
7. **Compatibility fixes:** only after the failure classes are triaged and compared
   with the 4DO/Opera reference.
8. **Gameplay depth (deferred):** finish a full Road Rash race, script Alone's
   inventory, and add input scripts for the other seven titles.
9. **Platforms/performance/packaging:** Pi/GLES 3.1, arm64 hardware, DPI, Tier B
   timing, installers/signing, file associations, and the optional M12 JIT.
10. **Release blockers remain:** legal/attribution sign-off and green CI. Do not cut a
    release until M13/M14 are complete and the distribution model is signed off.

---

## 6. Next session

The next session is the **BIOS-aware compatibility pass**, not release prep:

1. Read [`CompatibilitySweep.md`](CompatibilitySweep.md),
   [`GameCompatibilityList.md`](GameCompatibilityList.md), and the triage results in
   [`Compatibility.md`](Compatibility.md) 13.2.
2. Rerun the `Not Loadable` and `Loadable/No video` subsets with
   `-Bios bios/panafz10-patched.bin` and `-Bios bios/panafz10e-anvil.bin`. Record
   which BIOS each title needs; do not assume `panafz1` is the compatibility
   reference.
3. Investigate Crash 'n Burn and WorldCup Super Stadium, which still stop on a boot
   screen with the Anvil BIOS at 12,000 frames.
4. Fill the 5DO status columns from the BIOS-aware results and the 12,000-frame
   screenshots. Mark the BIOS used in the notes.
5. Run the high-resolution subset with `-HighRes` and record the comparison.
6. Start the M14 measurement work from
   [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

Do not re-enable the null audio-sink short-circuit, do not change the XBUS DMA byte
order (the raw `IRam` order is correct), do not port `opera_cdrom.c` wholesale, and do
not redo the trimmed CLI/frontend or source-generated JSON work; see
[`Trimming.md`](Trimming.md). Do not treat full-race completion, scripts for the other
seven titles, Pi/arm64 validation, or installers as current gates; they are later
backlog.

---

## 7. Reproduce

```powershell
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release

# The corpus roots are machine-specific; set them once per shell.
$env:FIVEDO_GAMES = "J:\Panasonic 3DO"
$env:FIVEDO_MAME  = "G:\MAME\MAME Software List CHDs\3do"
$env:FIVEDO_DISC  = "$env:FIVEDO_GAMES\Road Rash (USA).chd"

# Full-collection loadability sweep (3000 frames, resume-safe)
pwsh -File tools/compat-sweep.ps1 -Frames 3000 -Resume -OutDir artifacts/compat

# Regenerate the 4DO reference table after changing the collection
python tools/generate-compat-list.py

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
