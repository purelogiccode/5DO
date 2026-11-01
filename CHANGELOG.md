# Changelog

All notable changes are recorded here. The project is pre-1.0; formats and behavior may
still change.

## [Unreleased]

### Fixed

- Opera parity batch 1 and OP-14: VDLP now keeps the retained head separate from the
  active successor cursor, follows zero entries, processes a zero-persist successor on
  the same line, and saves/migrates state version 3; ARM immediate rotates update the
  shifter carry; DSP CPU-supplied high FIFO reads return instruction memory instead of
  PRNG noise; MADAM uses the inclusive arbitrary span endpoint, stops MARIA
  scale/arbitrary work after the first pixel, treats BPP 0 as 1 bpp, honors packed
  SKIPX, reads literal LRFORM row pairs, applies blue-LSB substitution while preserving
  raw VH bits, gates CLUT/video DMA on MCTL, handles exhausted literal SKIPX, advances
  CEL origins through the XYPOSH/XYPOSL model, decodes REGCTL stride with Opera's byte
  groups, and uses decoded PIXC multiplier bits for `PDC_MFONLY`. The automatic
  `GraphicsStepY`/`BitTiming6` mappings are retired; the literal-CEL path reads the
  actual source row, and the enum values remain loadable for old states. CLIO input
  FIFOs complete before the end address is observed, reload only when the channel is
  enabled, and reset the pointer on a new start address. The Lemmings 300-frame
  differential still matches; the five-title showcase hashes are unchanged. See
  `Make5doTheBestOfAll.md` 13.11/13.12 and `docs/TestCorpus.md` 1.1g.

### Added

- Differential harness (FND-05/CORE-11/CORE-16). `run` adds `--checkpoints`,
  `--audio-hash`, and `--state-hash`; each checkpoint records frame hash, PC,
  instruction count, sample count, audio hash, state hash, CD status/poll, DSP
  state, and CEL busy cycles. `tools/FiveDo.DiffHarness` stages the bundled
  OperaCore reference and BIOS, extracts/caches a data ISO from CHD, runs both
  engines with identical options, compares normalized frame hashes, applies
  manifest allowances, and writes JSON/Markdown/HTML reports.
  `FiveDo.DiffHarness baseline` gates changed 5DO fields. The Lemmings (USA)
  300-frame smoke matches at frame 300 (`0x110E7DC5`); see
  `docs/DifferentialHarness.md` and `docs/TestCorpus.md` 1.1g.
- Per-game NVRAM and NVRAM versions (SYS-14). `NvramStorageMode` (`Global`, `PerGame`),
  `NvramVersion` (0-9), and `NvramDirectory` select the layout: shared version 0 keeps
  the legacy `nvram.ram`, shared versions 1-9 use `nvram.<version>.ram`, and per-game
  images use `nvram/per-game/<gameId>.<version>.ram`. `NvramStore` reads migration
  fallbacks (per-game version 0, shared version, legacy) without copying, writes
  atomically, and backs up a corrupt primary instead of silently loading an older image.
  Settings > Emulation exposes the storage mode and version and restarts the machine
  safely when they change; File > Export NVRAM..., Import NVRAM..., and Reset NVRAM
  manage the current image. The CLI adds `--nvram-mode`, `--nvram-version`, and
  `--nvram-dir` to `run`/`trace`, reports `nvramPath`/`nvramMode`/`nvramVersion` in
  `run --json`, and adds `nvram path|list|export|import|reset`. Documentation is in
  `docs/Configuration.md`, `docs/CliReference.md`, and `docs/UserGuide.md`.
- BIOS manager and automatic selection (SYS-04). `BiosCatalog` recognizes the common
  Panasonic FZ-1/FZ-10/FZ-10E/FZ-10JA, GoldStar, Sanyo, patched arcade, Kanji font, and
  arcade support MD5s. `BiosLibrary.Scan` validates a folder, classifies primary and
  secondary images, detects Anvil from the ROM and Kanji/patch state from metadata or
  the file name, and reports bad files. `SelectForGame` combines game-database regions,
  known title requirements (Slayer Europe/USA, Powers Kingdom Europe, Arcade CD-ROM),
  and per-game MD5 overrides. New CLI surface: `--bios2`, `--bios-auto <folder>`, and
  `bios list|scan|select`; `run --json` reports the selected BIOS path, MD5, name,
  region, reason, and source. Settings > Paths adds the BIOS folder, primary/secondary
  paths, auto-select, and scan; Settings > Emulation adds a per-game override; File adds
  assign/clear actions. The configured BIOS remains the fallback when no folder is set.
  The five-title 3,000-frame and four known-requirement 12,000-frame smoke runs are in
  `docs/TestCorpus.md` 1.1f.
- Timing profiles (`EmulationProfile`). **Compatible** keeps the FreeDO timing
  baseline, **Accuracy** enables ARM60 N=2, the Opera CLIO timer scheduler, CEL busy
  timing, cycle-based CD reads, and the stock 2x drive, and **Performance** uses a
  1.5x CPU clock with FreeDO timing. **Custom** keeps the individual options.
  Profiles never change per-game fixes, high-resolution rendering, or the random seed.
  They are available as `--profile compatible|accuracy|performance|custom` on `run` and
  `trace`, as Settings > Emulation > Timing profile, and as `run --json` `profile`
  (derived from the actual options, so an explicit override reports `Custom`). The
  individual options remain authoritative and are recorded in save states.
- CD-ROM speed control (`CdSpeed`). The default `X2` preserves the stock drive timing;
  `--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x` (or Settings > Emulation > CD speed)
  changes the effective read speed for the cycle-based `--cd-timing opera` model.
  Sector times follow Opera's per-speed overhead table, `unbounded` publishes sectors
  immediately, and the default FreeDO timing path is unchanged. `run --json` reports
  `cdSpeed`; save states record the speed in the `Options` section (version 5) and warn
  when a state was written with a different speed.
- CD-ROM cycle-based data timing option (`CdromTimingMode`). The default
  `FreeDoCompatible` mode keeps the immediate transfer. The opt-in `OperaCycleBased`
  mode schedules each 2048-byte data sector at the stock 2x drive speed (81,246 ARM
  cycles at 12.5 MHz after Opera's overhead compensation), keeps the data and status
  poll bits clear while the drive is busy, publishes the read-data status after the
  transfer drains, and raises the XBUS CD FIQ when delayed data or status becomes
  ready. It is available as `--cd-timing freedo|opera` on `run`/`trace` and in
  Settings > Emulation, and `run --json` reports the selected mode. Save states record
  the mode in the `Options` section (version 4) and the drive clock, next-ready cycle,
  and pending status in the CD-ROM section (version 2); older states still load. The
  default path is unchanged; the smoke differential is in `docs/TestCorpus.md` 1.1d.
- MADAM CEL busy timing option (`CelTimingMode`). The default `FreeDoCompatible` mode
  keeps the synchronous draw. The opt-in `OperaBusy` mode follows `opera_madam.c`:
  the list is drawn at the next CPU tick, the status register stays busy, and the
  deadline is estimated from rendered pixels, walked texels, and list entries (nine
  million pixels per second plus 64 setup cycles per entry). It is available as
  `--cel-timing freedo|opera` on `run`/`trace` and in Settings > Emulation, and
  `run --json` reports the selected mode and remaining busy cycles. Save states record
  the mode in the `Options` section (version 3) and the pending/busy fields in the
  Madam section (version 2); older states still load. The default path is unchanged;
  the 3,000-frame differential is in `docs/TestCorpus.md` 1.1c.
- CLIO timer scheduler option (`ClioTimerMode`). The default `FreeDoCompatible` mode
  keeps the original all-sixteen-timer scan. The opt-in `OperaScheduler` mode follows
  `opera_clock.c`/`opera_clio.c`: one timer slot per four 25 MHz ticks, the `0x220`
  slack gap after slot 15, and an explicit cascade carry. It is available as
  `--clio-timers freedo|opera` on `run`/`trace` and in Settings > Emulation, and
  `run --json` reports the selected mode. Save states record the mode in the `Options`
  section and add a warning on mismatch. Quartz section version 3 stores the Opera
  slot/accumulator and Clio section version 2 stores the carry; older states still
  load. The default path is unchanged; the 3,000-frame differential is in
  `docs/TestCorpus.md` 1.1b.
- ARM60 cycle-class timing option (`ArmTimingMode`). The default
  `FreeDoCompatible` mode keeps the original N=4 timing; the opt-in
  `Arm60Datasheet` mode uses N=2 from the ARM60 datasheet/SDK and Opera. It is
  available as `--arm-timing freedo|arm60` on `run`/`trace` and in Settings >
  Emulation, and `run --json` reports the selected mode. Save states record the mode
  in an optional `Options` section; loading a state written with another mode adds a
  warning instead of rejecting the state.
- CPU clock control for the CLI and desktop: `--cpu-clock 0.5x-4x` (or an absolute
  `6.25-50MHz` value) and Settings > Emulation > CPU clock. The stock 12.5 MHz default
  is unchanged, the existing save-state ARM-clock warning still applies, and `run --json`
  reports the active clock in hertz.
- Random-seed control for the DSP PRNG: `--random-seed default|random|SEED` and
  Settings > Emulation > Random seed. The default preserves FreeDO's `0xa5a5a5a5`
  seed; a fixed value makes fresh runs reproducible.
- Frontend command-line launch options for file associations and external emulator
  frontends: positional disc / `--disc <path>`, `--bios <path>`, `-f`/`--fullscreen`,
  `--gl-backend`, `--version`, and `-h`/`--help`. A missing explicit BIOS path exits
  with code 1. Frontend usage is documented in `README.md`, `docs/UserGuide.md`, and
  `docs/Configuration.md`.
- Presentation frame skipping (`FrameSkipMode`): **Auto** (default) plus fixed
  one-in-two through one-in-five cadences. Only VDLP rendering and host presentation
  are skipped; every emulated frame still runs the CPU, CEL, DSP, timers, CD-ROM, and
  input, so game speed, audio, and save states are unchanged. Settings > Video exposes
  the policy and its tooltip explains that it is not fast-forward. Auto uses measured
  per-presented-frame work and recovers after 120 good frames.
- Optional donation banner in the game library. It links to the 5DO donation page,
  can check a saved email through the PureLogicCode donation API, hides after a
  completed donation is confirmed, and can be dismissed until the next launch.
- Configurable shared cover-art directory for the game library. Covers are matched by
  the disc file's base name (for example `Battle Chess (Europe).png`) and can live
  either beside the disc or in a folder selected under **Configuration > Settings...
  > Paths > Game covers**. Images are decoded once per file at a bounded height and
  shared by path, so duplicate discs in different library folders reuse one bitmap.
- Full-collection compatibility tooling: `tools/compat-sweep.ps1` boots every local
  disc in a fresh CLI process, records JSON/PNG/timeout evidence, and writes
  `summary.csv`/`summary.md`; `tools/generate-compat-list.py` parses the saved 4DO
  compatibility wiki into a 241-title reference and matches it against the local
  collections; `tools/generate-compat-results.py` turns a sweep CSV into
  `docs/GameCompatibilityResults.md`.
- `docs/GameCompatibilityList.md`, `docs/CompatibilitySweep.md`, and
  `docs/HighResolutionCelResearch.md` document the compatibility status scale, the
  loadability/in-game sweep, the Linux VM validation procedure, and the true
  high-resolution CEL research options.
- The first full-collection loadability sweep completed: 594 discs tested, 576
  `Loadable/Rendered`, 13 `Loadable/No video`, and 5 `Not Loadable`. The per-disc
  results are in `docs/GameCompatibilityResults.md`.
- BIOS-aware compatibility tooling: `tools/bios-probe.ps1` retries the sweep's
  failures with `panafz10-patched`, the Anvil BIOS, or any other supplied image and
  records the BIOS that produced the best result. The sweep and probe now reject the
  3DO boot starfield by requiring at least 3% non-black sampled pixels and 8 distinct
  colors, and the compatibility-list generator preserves manually reviewed 5DO
  statuses across regeneration.
- Embedded Win32 Vulkan video surface for the desktop shell. `VulkanVideoSurface`
  hosts a click-through child HWND inside the Avalonia window and presents through
  the existing FIFO swapchain, eliminating the per-frame GPU-to-CPU readback and
  `WriteableBitmap` upload for `--gl-backend vulkan`. The child surface is
  Windows-only; non-Windows platforms keep the offscreen/readback fallback.
- `fs list`/`fs extract` now read the 3DO Opera file system from ISO, BIN, CUE+BIN,
  and CHD images through the first-party `FiveDo.Storage.OperaFileSystem` parser. It
  follows multi-block directory chains and `burst`/`gap` file layouts. The Lemmings
  ISO/CHD pair produces 482 identical entries and 461 byte-identical extracted files.
- Native Win32 Vulkan swapchain presentation and `--vk-window-probe`. The optional
  Vulkan backend now enables `VK_KHR_surface`/`VK_KHR_win32_surface`/`VK_KHR_swapchain`
  for a real window, blits its rendered output into the acquired swapchain image, and
  presents with FIFO vsync. The probe writes the same timing/parity JSON as `--vk-probe`
  with `offscreen: false`, `vsyncActive`, and `vsyncBackend`. On Windows 11 / RTX 4070,
  4K sharp-bilinear measures 5.96 ms mean / 6.53 ms p99 present (including the 165 Hz
  FIFO wait) and 0.077/0.088 ms GPU with 0 managed bytes/frame; a 320x240 Accurate
  capture still matches the CPU oracle with 0 different pixels. See
  [`docs/Vulkan.md`](docs/Vulkan.md).
- `AppSettings.PreferOpenGl` is now honored: unchecking "Use the OpenGL renderer" in
  Settings starts the CPU presenter without attempting a GL context, and re-enabling
  it restores OpenGL. The status line and log identify the explicit CPU mode.
- Custom aspect ratio editing in Settings (`CustomAspect`, a positive width/height
  field) alongside the existing presets.
- Save-state format version 2 with per-section Brotli compression. Large sections
  (4 KiB and up) are compressed when the result is smaller; the section CRC remains
  over the uncompressed payload, and version-1 states still load. A 24.9 MB local
  state becomes 647,647 bytes at 300 frames and 2,001,229 bytes at 14,900 frames.
  `state inspect` reports the stored file size and marks compressed sections with
  their stored size; `StateInfo`/`StateSectionInfo` expose `Compressed` and
  `StoredLength`.
- Optional Vulkan 1.0 offscreen backend (`FiveDo.Video.Vulkan`): a two-stage
  palette/scale pipeline with build-time SPIR-V shaders, accurate/nearest/
  sharp-bilinear/CRT filters, GPU timestamp queries, and host readback. `--vk-probe`
  writes the shared GPU probe JSON and PNG captures; `--gl-backend vulkan` selects
  the optional desktop offscreen/readback path. A 320x240 Accurate probe matches the
  CPU oracle (`0xD6BD89C5`, 0 different pixels); 4K sharp-bilinear measures 0.24 ms
  mean / 1.06 ms p99 present and 0.075/0.091 ms GPU with 0 managed bytes/frame on the
  RTX 4070. See
  [`docs/Vulkan.md`](docs/Vulkan.md).
- Vulkan xBR-lv2, FSR 1.0 (EASU + RCAS), and HQx 2x/3x/4x filters. xBR runs as a
  4x pass over the GPU-expanded native frame and FSR runs EASU plus RCAS; HQx uses
  the shared CPU `FrameAssembler` + `HqxFilter` expansion and a reusable upload path.
  All three preserve top-down orientation and report `filterFallback: false`. 4K xBR
  measures 0.32 ms mean / 0.80 ms p99 present and 0.13/0.14 ms GPU, 4K FSR 0.60/1.30 ms
  present and 0.40/0.42 ms GPU, and 4K HQx 2x 6.82/10.93 ms (CPU-bound), with 0 managed
  bytes/frame on the RTX 4070.
- The required OpenGL xBR path now samples the GPU-expanded palette texture directly
  instead of uploading a CPU `FrameAssembler` copy; the 4K WGL xBR probe measures
  present 0.28 ms mean / 1.47 ms p99 with 0 renderer allocations.
- Vulkan tests: generated SPIR-V module checks for all six shaders, probe option
  parsing, split-frame xBR/FSR orientation at 320x240 and 640x480, and opt-in
  `FIVEDO_VULKAN_TESTS=1` CPU-parity/source-size-change and 4K/xBR/FSR probe
  integration tests.
- Input recording: **Emulation > Record Input** captures player-1 input to a
  frame-ordered script while a game runs and saves it to the configured save-state
  directory (or the default 5DO directory) as `<game>-<timestamp>-input.txt`. The
  recording coalesces unchanged frames and is directly replayable with the CLI's
  `--input-script` option.
- Save-state disc identity check: loading a state now reports a warning when the
  disc/game hash stored in the state header differs from the disc currently
  inserted. The state still loads; the CLI `run`/`trace` commands, the desktop
  Load State action, and the slot manager all pass the current game ID through.
- Console hotkeys: the desktop shell now dispatches the `InputProfile.Hotkeys`
  bindings (default F5 save, F8 load, F6/F7 previous/next slot, F9 frame advance,
  F10 reset, F11 fullscreen, F12 screenshot, Escape pause) before forwarding a key
  to the emulated pad. F9 uses `CoreRunner.StepFrame` to run exactly one frame and
  leaves emulation paused. `HotkeyRouter` resolves the bindings case-insensitively
  and is unit-tested.
- `disc tracks` now reports ISO, BIN, and CUE images as a single 2048-byte
  user-data track in addition to the existing CHD track list, so the command no
  longer requires a CHD container.
- OpenAL Soft LGPL compliance: `docs/OpenALSoft.md` records the notice, exact
  upstream 1.23.1 source, three-year written source offer, and relinking/
  replacement instructions; `docs/licenses/OpenAL-Soft-COPYING.txt` contains the
  complete LGPL-2.0 text. `packaging/package.ps1` copies both into every portable
  archive and lists them in `PACKAGE.txt`.
- Root `LICENSE`: the project is now distributed under the FreeDO modified GNU LGPL,
  with the FreeDO notice reproduced verbatim, the GNU LGPL 2.1 text, and the additional
  non-commercial/attribution terms. `LICENSE` is included in portable archives.
- Application usage statistics: the CLI and desktop hosts POST the application id
  (`5do`) and version to the ApplicationStats service at launch. Only those two
  fields are sent. CI/test runs and `FIVEDO_STATS=0` disable it; override the
  endpoint, key, or id with `FIVEDO_STATS_ENDPOINT`, `FIVEDO_STATS_API_KEY`, and
  `FIVEDO_STATS_APPLICATION_ID`.
- Serilog logging across the CLI and desktop hosts: rolling daily files, stderr
  console output (desktop), the in-memory **Log Viewer**, and an asynchronous
  bug-report sink that forwards `Warning`-or-higher events with environment,
  error, and exception details. Submissions are deduplicated for five minutes and
  capped at ten per minute. `FIVEDO_BUG_REPORTS=0` disables submission,
  `FIVEDO_LOG_DIR` redirects log files, and CI/test runs disable submission.
- Audio tap diagnostics: set `FIVEDO_AUDIO_TAP=<prefix>` to record the exact PCM
  queued to OpenAL (`<prefix>.wav`) plus a per-buffer CSV (`<prefix>.csv`) with
  elapsed time, frames, ring/queue depth, underruns, peak, and OpenAL errors. The
  status overlay now shows a live peak meter beside the audio counters.
- AMD FSR 1.0 (`VideoFilter.Fsr`): opt-in EASU upscaling plus RCAS sharpening in
  the OpenGL presenter, with a Settings > Video sharpness slider and MIT
  attribution in `THIRD-PARTY-NOTICES.md`.
- `tools/release-soak.ps1`: discovers the discs present on the current machine, runs
  the 36,000-frame release soak, reports absent titles as missing instead of failing,
  and can continue archived 12k states for machine-specific corpus gaps.
- Audio diagnostics: `audio analyze <wav> --json` reports format, duration, peak,
  RMS, clipping, silent frames, and DC offset. `run --json` includes the same
  statistics for `--audio wav`, plus dropped/underrun/queued/buffered counters and
  estimated latency for `--audio openal`.
- Live audio from the CLI: `--audio openal` and `--volume 0..1`. `--realtime`
  paces a `run` against the audio clock for listening and glitch tests.
- Settings > Audio has a 1 s 440 Hz OpenAL test tone and reports the device error
  when no playback endpoint is available.
- Audio-driven frame pacing in `CoreRunner`: the frame period follows the DSP
  sample count (44.1 kHz), with a rolling average, clamping, and a 60 Hz fallback.
  The status overlay shows measured/target fps.
- Bundled OpenAL Soft 1.23.1 native runtime (`Silk.NET.OpenAL.Soft.Native`) for
  all six RIDs, copied as `OpenAL32.dll` on Windows beside the CLI/frontend
  executables and included in portable archives.
- Audio regression tests: exact 44,100-sample clock across batch patterns, Quartz
  state-v1 conversion, audio statistics/WAV analyzer roundtrips, frame-pacer
  behavior, reset/state-load audio flushing, CLI JSON contracts, and an opt-in
  `FIVEDO_AUDIO_TESTS=1` OpenAL tone.
- `IAudioSink.Reset`: live backends drop their ring/queue on machine reset or state
  load so the next timeline does not replay up to ~180 ms of the old one.

### Fixed

- **Input-script button aliases.** `Start`, `Play`, and `Pause` are now accepted as
  aliases for the 3DO `P` button, and `Stop` for `X`. Scripts that used the common
  `Start` name no longer fail with `unknown button 'Start'`.
- **CLI user errors no longer file bug reports.** Expected command-line errors
  (bad arguments, missing paths, offsets outside a file, and malformed input scripts)
  are still logged and printed to stderr, but are marked as expected so the bug-report
  sink ignores them. Unexpected exceptions still report at `Fatal`.
- **Desktop Stop button.** The toolbar, menu, and in-game Stop actions called
  `PauseEmulation`, so the core thread stayed paused with the previous machine state;
  a later Start reused that state and the game could fail to resume. Stop now joins
  the core thread, flushes NVRAM, clears RAM, resets the machine, and returns to the
  library. Start or selecting a game boots from a clean state; clearing the previous
  game's RAM prevents the next boot from sticking at the BIOS wait at `PC 0x1658`.
  Returning from the library also focuses the window again, so a hidden search box
  cannot swallow game keys.
- **BIOS switch cold boot.** Loading a different BIOS while a game was resident left
  the previous game's RAM in place, so the new BIOS could stall at `PC 0x1658`.
  `OpenBiosPath` now clears CPU RAM before resetting. Verified in the desktop shell
  with Battle Chess (Europe): the NTSC BIOS shows the game's "This is not a PAL TV
  system!" screen, and `panafz10e-anvil.bin` boots to the title screen.
- **Headless controller detection.** `run` and `trace` left PBUS empty unless
  `--input-script` or `--autopilot` was supplied, so games that check the controller
  ports saw no pad. Both commands now attach a default player-1 `PbusSource`, matching
  the desktop shell. Olympic Summer Games no longer stops at the "Insert a controller
  into controller port!" screen; input-sensitive 3,000-frame hashes were re-baselined
  in `docs/TestCorpus.md`.
- **Vulkan startup fallback.** `VulkanVideoSurface.Start()` threw
  "The Vulkan surface is not attached to a window." when the shell selected Vulkan
  before the control was attached. It now records a pending start and initializes on
  attachment, so the Vulkan presenter starts normally or reports a failure through
  the CPU-fallback path instead of failing startup.
- **CHD read-ahead shutdown race.** CHDSharp 1.6.0 defers read-ahead teardown until
  in-flight hunk decompression finishes, so closing a CHD no longer produces an
  unobserved `ObjectDisposedException` from `SemaphoreSlim.Release` on the finalizer
  thread.
- **Stop/start race in the core runner.** `CoreRunner.Pause`/`Resume`/`Invoke` used
  a manual-reset event pair that could return from `Pause` before the core thread had
  actually stopped, letting the UI reset or replace the machine while the core thread
  was still running. The handshake now uses a monitor with an explicit paused state,
  and `Invoke` cannot run concurrently with `RunFrame`. Rapid stop/start cycles now
  restart games reliably; a stress test covers 50 pause/invoke/resume cycles.
- **Vulkan readback cost.** The Vulkan renderer copied the full output image to a
  host-visible buffer every frame, even when no caller read it, and the GPU timestamp
  bracketed that transfer. The readback is now recorded only when `CaptureToFrame` is
  enabled; probes and screenshots use a one-shot copy, and the GPU timestamp covers the
  render passes only. On the RTX 4070, 4K sharp-bilinear GPU time drops from 1.34 ms to
  0.075 ms and present from 1.51 ms to 0.24 ms; 4K FSR GPU/present drops from
  1.69/1.87 ms to 0.40/0.60 ms. The desktop fallback also writes the captured frame
  directly into the locked `WriteableBitmap` instead of through two intermediate
  full-frame copies, and the swapchain blit no longer stalls the queue with
  `vkQueueWaitIdle`.
- Expected environmental warnings are no longer filed as bug reports. The
  "OpenAL audio is unavailable" fallback is marked with the new
  `WithoutBugReport()` context and is ignored by `BugReportSink`; `--smoke-test`,
  `--gl-probe`, `--vk-probe`, and `--vk-window-probe` disable bug reporting entirely.
  The bug-report sink still forwards actionable warnings and errors, including
  unexpected audio failures.
- **Live OpenAL crackle.** The fill loop chose the next buffer with
  `_buffers[queued]`. After OpenAL unqueues the oldest buffers, that index pointed
  at buffers still attached to the playing source; rewriting them corrupted audio
  that was about to play, which was heard as constant cracking even though the
  underrun and dropped-sample counters stayed at zero. The loop now rents only
  released buffers from a free-buffer pool, tracks queued frames exactly, checks
  `alGetError` after every queue, and queues variable-size blocks instead of
  padding to fixed 512-frame buffers.
- **Black game canvas.** Restoring the `(float)` casts on the `glUniform2/4`
  calls prevents Silk.NET from selecting the integer overloads, which left the
  presenter's float uniforms at zero and collapsed the quad to black.
- **DSP sample cadence.** The FreeDO 8.24 scheduler over-produced samples by
  ~0.7% (444,097 instead of 441,015 for a 600-frame Lemmings boot), making audio
  sharp and constantly overfilling the live ring. `Quartz` now uses an exact
  rational DSP clock (44,100 samples per ARM second) while keeping the reference
  VDL/timer accumulators. The 600-frame captures are now 10.000 s; Opera's
  reference is 10.010 s at 59.94 Hz and the spectral peaks are unchanged.
- **Live OpenAL output.** The OpenAL context is now made current on the audio
  thread (OpenAL's current context is thread-local) and the native runtime is
  shipped. Previously `TryCreate` returned null on Windows without a system
  OpenAL installation, so the desktop shell was silent even when audio was
  enabled.
- The WAV sink buffers PCM writes instead of one file write per stereo frame,
  and it records peak/RMS/clipping during capture.
- VDLP bitmap copies now advance four bytes per pixel, matching FreeDOCore's
  `unsigned int*` source increment. The 16-bit pixel occupies one halfword of a
  32-bit VRAM word and the two halves belong to adjacent scanlines; reading
  consecutive halfwords mixed two lines, halved the horizontal resolution, and
  shifted the picture right (Alone in the Dark's intro and menu were cut off).

### Changed

- Performance hot-path pass: Road Rash's 100k-state 3D race now measures **4.20
  ms/frame total** (p99 7.40 ms) instead of 5.46 ms (p99 13.14 ms), with the frame hash
  unchanged and zero allocations. The CEL rasterizer caches per-CCB registers, uses
  row-span address counters, and skips the per-texel PLUT length check; the DSP resolves
  its instruction-memory windows before the special-register switch.
- Debug builds now compile the emulator core projects with JIT optimizations. The same
  Road Rash race measured 24.98 ms/frame (about 40 fps) in Debug before this change and
  4.30 ms/frame after it. Frontend/UI projects remain unoptimized for normal XAML
  debugging; pass `-p:FiveDoOptimizeDebug=false` to opt out.
- The Avalonia video surface coalesces core-thread render requests, so a slow UI thread
  cannot accumulate a dispatcher backlog while the mailbox keeps the latest frame.
- Fresh settings default to the optimized video/output configuration: OpenGL,
  match-window output, sharp-bilinear filtering, vsync on, integer scaling, and Auto
  frame skip.
- Recorded the distribution model as **donationware**: non-commercial,
  source-available, voluntary donations only. The v1.0 release is deferred until the
  full-collection compatibility sweep (M13) and high-resolution CEL research (M14)
  produce a baseline.
- Removed the `VideoGameFileSystemParser` dependency. The `5do fs` command now uses
  the first-party `FiveDo.Storage.OperaFileSystem` reader for every container, so
  ISO, BIN, CUE+BIN, and CHD share one parser and one output contract. `fs list`
  reports the on-disc volume label (the corpus images use `CD-ROM`) rather than the
  CHD file base name.
- Tightened method visibility: `StateCrc32.Compute`, `GlBackendOption.TryParse`,
  `GlProbeOptions.TryParse`, and the `GlProbeWindow` constructor are now `internal`
  to match their internal containing types. Interface implementations, entry
  points, and the Avalonia designer hook remain public as required.
- Reorganized the production projects: domain models and enums now live in each
  project's `Models/` folder and interfaces in `Interfaces/`. Nested implementation
  details and interop structs stay with their owning types; namespaces are unchanged.
- Renamed every `async`/`Task`-returning method to the `Async` suffix, including
  Avalonia event handlers and their XAML bindings, for consistent async naming.
- Replaced the custom `Log`/`ILogSink`/`ConsoleLogSink`/`InMemoryLogSink`/`NullLogSink`
  facade with Serilog. `LogCategory` remains as the structured category property,
  and the desktop log viewer is fed by `InMemorySerilogSink`. Host entry points and
  existing catch blocks now log through Serilog; the CLI keeps JSON on stdout and
  concise errors on stderr while diagnostics go to the file and bug-report sinks.
- The Windows audio fill thread is registered as a "Pro Audio" multimedia thread
  (`AvSetMmThreadCharacteristics`) and runs above normal priority so UI and
  rendering work cannot starve it.
- Quartz save-state sections are version 2 (exact DSP cycle remainder). Version 1
  sections are still accepted and convert the legacy 8.24 sample fraction.
- The 3,000-frame boot hashes changed where the FMV/loading timing depends on the
  audio clock; the current keys and comparisons are in
  [`docs/TestCorpus.md`](docs/TestCorpus.md) 1.1 and 1.5a2.

## [1.0.0-rc.1] - 2026-10-26

Release candidate for the documented-compatibility v1.0 scope. Legal/attribution
sign-off is pending; see [`docs/ReleaseLegalReview.md`](docs/ReleaseLegalReview.md).

### Added

- Lemmings Fun 4 passcode `HNLHCIOECW` is now typed entirely with the D-pad/A
  (`docs/scripts/lem-fun4-dpad.txt`). The game shows the Fun 4 briefing at 22,600
  and starts gameplay at 23,000 (`PC 0x00083FD8`, hash `0xB7846C2D`); the state
  roundtrips (`0xEDEB1B9D` twice).
- CRT bloom in the OpenGL presenter: a 3x3 native-resolution glow around bright
  areas, controlled by `RendererSettings.Bloom`/`BloomThreshold` and a Settings
  slider.
- HQx 2x filter (`VideoFilter.Hqx`): a port of Maxim Stepin/Cameron Zemek's hq2x
  running on the CPU-expanded frame, with a C++ reference hash test.
- HQx 3x/4x filters (`VideoFilter.Hqx3x`/`Hqx4x`): generated from the 4DO hq3x/hq4x
  sources and verified against the C++ reference hashes. They are opt-in and cost
  more CPU time than HQx 2x.
- Benchmark budget gate: `FiveDo.Benchmarks --gate` and the CI job enforce
  0 bytes/frame, core p99 <= 8 ms, total mean <= 8 ms, and total p99 <= 16.667 ms.
- Optional native CPU-frame screenshot (`-native.png`) alongside the filtered GL
  screenshot, controlled from Settings > Video.
- Game-library cover art: a sibling image with the same base name as a disc
  (`game.png`/`.jpg`/`.jpeg`/`.webp` beside `game.iso`) is previewed in the
  Library window.
- Per-game filter overrides in `AppSettings.FilterOverrides`, edited on the Settings
  dialog's Emulation page and applied when a disc opens.
- Shader-source and settings tests for the CRT bloom uniforms.
- Trim-safe JSON: `AppSettings`, `InputProfile`, save-slot metadata, and every CLI
  `--json` response now use source-generated `JsonSerializerContext` metadata.
  The CLI result types are named records (`CliJsonModels.cs`) and no longer use
  anonymous-type reflection.
- Trimmed CLI publishes: `FiveDo.Cli` uses `PublishTrimmed=true` (`TrimMode=full`)
  for self-contained single-file builds, with a Windows CI smoke test. The win-x64
  audit covers `--version`, `selftest`, `bench`, `info`, `state inspect`,
  `state slots`, `fs list`, CHD boot/run, and a save-state roundtrip. See
  [`docs/Trimming.md`](docs/Trimming.md).
- `DiagnosticMethodInfo`-based RAM-write watchpoint descriptions (replaces
  `StackFrame.GetMethod`, which is trim-unsafe).
- `BenchmarkComparison` and benchmark `--baseline`/`--baseline-threshold` options:
  a same-machine relative check across core p99, total mean, and total p99 (default
  10%, exit code 4). Improvements pass; missing baseline metrics are ignored.
- Portable release archives: `packaging/package.ps1` builds
  `5do-<component>-<rid>.zip` for Windows and `.tar.gz` for Linux/macOS,
  including README/notices/licensing, a generated `PACKAGE.txt`, and the Linux
  desktop entry. CI publishes both raw folders and archives for all six RIDs.
- Trimmed Avalonia frontend: `FiveDo.Frontend.Avalonia` publishes with
  `PublishTrimmed=true` (`TrimMode=full`) for all six RIDs. The reflection-based
  `ViewLocator` was removed, every window is constructed explicitly, and all XAML
  bindings are compiled. The win-x64 single-file executable is 26.3 MB trimmed vs
  91.3 MB untrimmed.
- Frontend startup smoke test: `--smoke-test --smoke-output <path>` opens the real
  main window, constructs the settings dialog, runs one UI pass (GL hidden for
  headless CI), writes a `FRONTEND SMOKE OK` marker, and exits 0. `--version` exits before Avalonia
  initializes. CI runs the trimmed win-x64 frontend smoke test on Windows.
- Platform swap-interval support: `SwapInterval.TryCreate` resolves
  `WGL_EXT_swap_control`, GLX EXT/MESA/SGI, or `EGL_KHR_swap_interval`, and the
  renderer applies `RendererSettings.Vsync` with honest `SupportsVsync`/
  `VsyncActive`/backend reporting.
- `--gl-probe` GPU presentation probe: presents synthetic 320x240 frames through the
  real Avalonia OpenGL surface, supports a true 4K offscreen FBO, warm-up frames,
  per-stage allocation counters, and source-generated JSON. On Windows 11 with an
  RTX 4070, 4K sharp-bilinear measures GPU p99 0.089 ms and present p99 0.208 ms
  with zero renderer/mailbox allocations per frame after warm-up.
- `--gl-backend auto|wgl|angle|software` (alias `--gl-probe-backend`) selects the
  Windows renderer backend. `auto` keeps the previous WGL -> ANGLE -> software order.
- GPU-probe capture and parity: `--gl-probe-capture <path>` writes the presented
  frame as a PNG, and a 320x240 Accurate capture is compared with the CPU
  `FrameAssembler` oracle. WGL (OpenGL 4.0/GLSL 330) and ANGLE (OpenGL ES 3.0/
  GLSL ES 300) both match pixel-for-pixel (`0xD6BD89C5`), and ANGLE runs every
  filter with zero renderer allocations per frame.
- Seven additional real discs were validated for boot/render/save-state/audio:
  Gex, Need for Speed, Super Street Fighter II Turbo, Total Eclipse, Killing Time,
  Theme Park, and Return Fire. Gex, Total Eclipse, and Super Street Fighter II
  Turbo show gameplay/attract scenes; six produce non-silent 44.1 kHz stereo audio
  (Theme Park's menu is silent). Hashes and commands are in `TestCorpus.md` 1.7 and
  the matrix in `Compatibility.md` §10.
- Opt-in GPU integration test (`FIVEDO_GPU_TESTS=1`): runs the built frontend probe
  for WGL and ANGLE, checks Accurate parity and a short 4K pass. It found a
  synchronous-dispose crash when the capture consumer finished the probe while
  `OnOpenGlRender` was still running; the render path now returns safely after the
  capture callback.
- Opt-in NativeAOT publishes (`-p:FiveDoNativeAot=true`): the win-x64 CLI single
  file is 9.97 MB and passes the CLI smoke suite, `info`, `fs list`, and a 3,000-frame
  Lemmings boot matching the golden hash. The frontend publishes as a 28.8 MB
  single-file native executable plus native Skia/HarfBuzz/ANGLE assets and passes
  `--smoke-test` plus the WGL and ANGLE parity probes. Silk.NET.Core loader warnings
  remain and AOT audio is not yet validated. CI publishes and smoke-tests both AOT
  builds on Windows.

### Changed

- Adopted a documented-compatibility v1.0 release scope: supported platforms and
  titles are validated and documented, while full-compatibility work (Road Rash race
  completion, extra title scripts, Pi/arm64, installers, optional JIT) moved to the
  post-1.0 backlog. See [`docs/ReleaseChecklist.md`](docs/ReleaseChecklist.md).

- Windows ANGLE/GLES 3.0 is validated on the RTX 4070: 4K sharp-bilinear presents
  in 0.14 ms mean / 0.47 ms p99, 1080p in 0.13/0.40 ms, and HQx 4x is CPU-bound at
  10.44/14.22 ms. Raspberry Pi/GLES 3.1 and DPI validation remain.

- The CLI and the Avalonia frontend are both published trimmed for all six RIDs.
- CI runs a trimmed-CLI smoke test and a trimmed-frontend startup smoke test on
  Windows in addition to the benchmark gate.
- The Settings video page now exposes a CRT bloom slider and lists the HQx
  2x/3x/4x filter modes.
- CI runs the benchmark budget gate instead of only checking allocations.
- Docs record the tenth-pass Lemmings evidence, the HQx 3x/4x hashes, and the
  remaining work.

## [Unreleased] - 2026-10-25

### Added

- `run --json` now reports `cdStatus` and `cdPoll`, making the static
  READ_ID-only wait state visible without a CD trace.
- `run --script-base state` shifts an input script by the loaded save state's
  frame count, so scripts can be written from frame 0 after `--state-in`.
- Per-title save-state, PNG, and WAV evidence for the five-title corpus, recorded
  in `docs/TestCorpus.md` 1.5a. Zero-frame loads, 120-frame continuations, and
  roundtrips are deterministic for every title.
- Space Ace interactive path: `X` returns to the title/menu and `A` starts the
  scene; the 15,000-frame state roundtrips and a gameplay WAV was captured.
- Alone in the Dark Begin/Resume menu and intro path; the intro is a static
  READ_ID-only wait.
- A `ScriptedPbusSource` test covering `B+Left` as two button bytes.
- `--autopilot road` pulses A/releases throttle through long-static,
  low-coverage, CD-load, and 180-frame restart-stuck states and reports
  `autopilotMenuAdvance` in `run --json`. This gets through `GAME LOADING`/`Get
  ready`, starts a new race at 80k, and cycles race/wreck/menu to 100k.
- `CdromDevice.LastReadFrame` for host load probes.

- Desktop gamepad backend: Windows XInput and Linux evdev, with hot-plugging,
  six-player mapping, analog-stick D-pad fallback, and trigger-to-shoulder fallback.
- Gamepad binding tab in the input editor.
- In-app diagnostic log viewer with level, category, search, auto-refresh, and clear.
- First-run welcome guide and error dialogs for BIOS, disc, and emulation failures.
- Save-state PNG thumbnails, shown in the Slots window.
- xBR-lv2 edge-directed 4x filter (opt-in), with sharp-bilinear finishing.
- Desktop GL GPU timer query and CPU-present timing in the FPS overlay.
- Shader source tests for the palette, scale, and xBR programs.

### Fixed

- **CD media-access wait.** Road Rash's old ~42,000 and ~92,000/100,000 stops were a
  static READ_ID-only wait, not wall pins. The CD device now raises the Portfolio
  media-access event (`0x40`) after two consecutive READ_ID polls with an idle gap;
  the game advances to `BUSTED`, race select, and a new race. The five 3,000-frame
  corpus hashes are unchanged.
- **GPU palette expansion used one CLUT index for all three channels.** Real-content
  frames appeared grayscale because the shader fetched `(R,G,B)` from the low five bits
  only. The shader now uses the red, green, and blue indices independently, matching
  `FrameAssembler` and `GpuPalette`.
- **GL screenshot readback was black** after the palette shader error; the scale pass
  also had a `vec2` uniform type mismatch that produced `GL_INVALID_OPERATION`.
- xBR now uses a CPU-expanded native-resolution source texture. This avoids a driver
  readback issue with the FBO-attached palette texture and keeps palette expansion
  pixel-identical to the CPU oracle in xBR mode.

### Changed

- Settings expose the xBR filter again.
- `AppSettings` records whether the first-run guide has been shown.
- The About dialog reports connected gamepads.
- Corrected and fixed the Road Rash diagnosis: the ~42,000 and ~92,000 stops were
  a static READ_ID-only game-side wait, not wall/rider pins. The CD media-access
  pulse advances them to `BUSTED`, race select, and a new race.
- The deferred CD read-status experiments were reverted because they changed the
  golden boot hashes; the media-access pulse keeps all five 3,000-frame hashes
  unchanged.
- Alone in the Dark's intro no longer stays static forever: A every 300 frames
  advances the man-at-desk cutscene to the room/mansion/title scenes. The media
  pulse alone does not advance the intro.
- Wolfenstein's long fresh-game load advances with A every 300 frames through
  character select to FLOOR 1-1 at 64,000 frames.
- Alone in the Dark has a scripted room movement loop with a save/load roundtrip
  (`0x44C7576B` -> two 120-frame continuations at `PC 0x0008BF00`,
  `0xE490C8C7`).

## Earlier work

See [`docs/ImplementationPlan.md`](docs/ImplementationPlan.md) for the milestone history
and [`docs/Status.md`](docs/Status.md) for the current compatibility and performance
snapshot.
