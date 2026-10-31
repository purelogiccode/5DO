# Changelog

All notable changes are recorded here. The project is pre-1.0; formats and behavior may
still change.

## [Unreleased]

### Added

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
