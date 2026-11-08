# Known Issues

**Snapshot:** 2026-11-08, v1.5.0. This list covers the supported local corpus and the
current prototype state. It is not a compatibility guarantee.

**Release scope:** v1.0.0 ships with these known issues documented; full-race completion,
extra title scripts, M13.5/M13.6 visual/high-resolution review, M14 research, Pi/arm64
validation, installers, and the optional JIT are post-1.0. See
[`ReleaseChecklist.md`](ReleaseChecklist.md). **As of 2026-11-02 v1.0.0 is published on
GitHub**: the source is tagged `v1.0.0`, the unified Windows/Linux/macOS x64 bundles and
checksums are attached, and legal/attribution sign-off is recorded.

## Online cover art (2026-11-07)

- The online catalog is optional and hosted separately from the application bundles.
  Cover art remains the property of the respective publishers; see
  [`CoverArt.md`](CoverArt.md) for the takedown policy.
- The pack currently maps **645/653** 4DO game IDs. Eight homebrew/emulator entries have
  no cover, and five mappings are marked weak for review. Manual overrides are in
  `packaging/cover-overrides.json`.
- The client prefers no cover over a low-confidence wrong cover. Raise the threshold in
  **Settings > Covers** if a fuzzy match is wrong, or name a local cover exactly.
- WebP decoding uses the platform Skia backend. If a platform cannot decode a cover, the
  library shows the placeholder and logs a debug message; local PNG/JPG covers are
  unaffected.
- The catalog and images are fetched over HTTPS from the configured server. The request
  contains no library, game, or user data. Offline mode uses the cached catalog and
  images.

## Differential parity (2026-11-07, video-clean panel)

- The exact NTSC clock fix aligns the emulated sample clock: all 50 panel titles
  are within 9 samples of the reference at frame 300. After the 24-bit VDL
  background and zero-entry VDL stop fixes, **50/50 titles match every frame
  hash** at frames 60/120/300. The 5-title panel matches **5/5** at 300 frames.
- The universal frame-60 PC/CPSR phase is resolved. The harness now captures
  Opera's ARM/CD state in the checkpoint's own field (it previously read one
  field late), and `RunFrame` ends a field on the 263-line scanline boundary
  like Opera instead of a rounded cycle budget. On the 50-title panel the
  same-frame retired-instruction delta is **11-20** at f=60 (average 15.3) and
  **23-2,223** at f=120 (most titles around 200). The old "5DO frame 60 aligns
  with Opera frame 59" description was a harness sampling artifact.
- The CD command count matches on **50/50 titles at f=60 and f=120**. The first
  behavioral divergence moved to **11 titles at f=300**: 8 count differences
  and 3 one-command sequence differences (Creature Shock, Foes of Ali, N.O.B.).
  The other **39/50 titles are phase-only** at the checkpoints. The final
  transcript classification is still 39 Equal / 8 PrefixAhead / 3
  SequenceDiverged. Tracked in `ImplementationPlan.md` CORE-04.
- At 3,000 frames, Alone, Lemmings, Space Ace, and Wolfenstein match the
  reference frame hash. Road Rash still diverges at 1,000 and 3,000; it is the
  remaining long-run video divergence. CD counts at 3,000 are Lemmings
  365/352, Road Rash 851/836, Space Ace 349/348, Wolfenstein 862/861, and
  Alone 932/932.

## Differential parity (2026-11-06, superseded by the video-clean update above)


The five-title TEST-07 panel is deterministic and Lemmings matches the reference
frame hash at 60/300/1000/3000 frames. Road Rash and Space Ace still show
different intro animation frames/scenes from Opera at 1,000/3,000 frames; a
±60-frame window finds no matching neighboring frame. The first CD
command-sequence divergence is a READ_DATA (`0x10`) versus READ_ID (`0x83`)
transition. These are accuracy gaps against the reference core, not crashes;
tracked in `ImplementationPlan.md` CORE-04/CORE-07/CORE-13.

## Compatibility sweep status

- The 12 titles in the matrix below are validated. The first full loadability sweep
  (`J:\Panasonic 3DO`, 589 CHDs, plus `G:\MAME\MAME Software List CHDs\3do`, 5 CHDs)
  completed on 2026-10-30 and was extended on 2026-11-01: **593 rendered, 1 known
  boot failure, 0 not loadable** out of 594.
  The procedure is in [`CompatibilitySweep.md`](CompatibilitySweep.md); the per-disc
  table is [`GameCompatibilityResults.md`](GameCompatibilityResults.md).
- The 2026-11-01 extended-boot retry reran all 18 non-rendered rows. The five former
  not-loadable rows and 12 black/blank titles now render; only Crash 'n Burn (Japan)
  remains. WorldCup Super Stadium renders with the FZ-10J BIOS plus its Kanji font
  ROM, which is now classified as BIOS2. The old triage notes are kept below.
- The five not-loadable rows were triaged on 2026-10-30:
  - `Mirai Shounen Conan - Digital Library` reported a CHD sector-read error at LBA 1
    once during the sweep. Repeated 3,000-frame runs now complete with a stable menu
    frame (`PC 0x000284EC`, hash `0xBE2A023B`); treat the sweep failure as a transient
    disc/read error, not a reproducible emulator defect.
  - The four Arcade CD-ROM titles (`Crime Patrol`, `Mad Dog II`, `Orbatak`,
    `Shootout at Old Tucson RE3`) stall at a starfield or the storage-manager shell
    with `panafz1.bin`, but complete 3,000 frames and render game content with
    `panafz10-patched.bin` plus the default pad. The 4DO notes about a patched BIOS
    match this result.
- The 12,000-frame rerun of the 13 black/blank titles shows that Ishida Yoshio Kudan
  no Igo Seiha, both Konpeki no Kantai discs, and all three Myst discs render by
  6,000-9,000 frames. The remaining titles are BIOS-sensitive or still under
  investigation:
  - Crash 'n Burn (Japan) renders a purple boot screen only with the Anvil BIOS and is
    still stuck there at 60,000 frames; `panafz1`, `panafz10`, GoldStar, and Sanyo all
    end in an invalid PC, and the Japanese Anvil BIOS gives a green screen. The
    USA/Korea and USA Not-for-Resale revisions render with `panafz1`, so this is a
    Japan-revision issue, not a general Crash 'n Burn failure.
  - Powers Kingdom (Europe) renders its animated intro with the Anvil BIOS by
    30,000 frames, matching the 4DO note "Use Anvil-bios to load EU version of game".
  - Battle Chess (Europe) is PAL-only and refuses NTSC system software with "This is
    not a PAL TV system!" at `PC 0x00088EDC`. The European Anvil BIOS
    (`panafz10e-anvil.bin`) reaches the title screen at 6,000 frames and the board at
    30,000; the USA and Japan discs boot with `panafz10`.
  - Slayer is region-sensitive: the Europe disc renders its credits with `panafz1` at
    12,000 frames, while the USA disc stays black with `panafz1` and needs `panafz10`
    (also GoldStar/Sanyo) to reach its title menu at 60,000 frames.
  - WorldCup Super Stadium changes from a solid blue screen to the same Anvil boot
    screen as Crash 'n Burn (Japan); both are still stuck at 60,000 frames, so the
    Anvil BIOS is not a fix for these two.
- Olympic Summer Games now proceeds past the "Insert a controller into controller
  port!" screen because headless `run`/`trace` attach a default player-1 pad. A/P
  input reaches the credits screen. This is a CLI input fix, not a disc-specific
  patch.
- The baseline sweep used `panafz1.bin` only, so its `Loadable/No video` and
  `Not Loadable` labels must not be treated as final. A BIOS-aware sweep with
  `panafz10-patched` and the Anvil BIOS is required before the 5DO status columns are
  filled.
- The 4DO wiki list is a reference, not a guarantee. Filename matching does not prove
  the local image is the same revision, and a 4DO score of 100 does not mean 5DO has
  the same behavior.
- `--high-res` is FreeDO-style line doubling, not true internal 640x480 rendering. The
  vertical detail remains 240 lines. See
  [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md). M14 is post-1.0.
- Linux x64 validation (2026-11-02): the full test suite (**1,473/1,473**), CLI
  `selftest`, `bench`, `info`, `bios info`, a 3,000-frame Wolfenstein boot, non-silent
  audio, and the frontend `--smoke-test` pass on Linux Mint 22.3 (Mesa llvmpipe). The GL
  probe timed out in the VM's SSH/llvmpipe session, so GPU presentation on Linux is
  unvalidated; the CLI/frontend ship as a **preview/best-effort** build. macOS
  validation is post-1.0. The local Linux target is the Hyper-V `LinuxMint` VM
  (`D:\Hyper-V\VMs\LinuxMint`).
- The project is **donationware** (non-commercial, source-available). Legal/attribution
  sign-off is recorded (2026-11-02); donations do not change the upstream non-commercial
  restriction.

## Real-game status

| Title | Status | Remaining issue |
|---|---|---|
| Wolfenstein 3D | Boots, starts FLOOR 1-1, walk/turn/fire, save/load roundtrip | The fresh-game level load can stay static; A every 300 frames advances it through character select to FLOOR 1-1 (64,000 frames, hash `0x2F5B9864`) |
| Lemmings | Boots, title/passcode, pure D-pad Fun 4 passcode, gameplay, save/load, 640x480 title | The passcode is typed from a passcode-screen state; the title-to-passcode transition still uses the existing `Down`/`A` script |
| Road Rash | Boots, 3D race renders, `BUSTED`/race-select/new-race flow works, autopilot cycles race -> wreck -> menu -> new race to 100k, save/load roundtrip | A full 5.5-mile race is not finished; the autopilot still wrecks or ends on a sidewalk. The old ~42k/~92k READ_ID wait is fixed by the CD media-access pulse |
| Alone in the Dark | Boots, in-game room, upright character, Begin/Resume menu, intro, scripted room movement with save/load roundtrip | The intro needs A input; inventory interaction and a longer exploration sequence are not scripted |
| Space Ace | Boots, animated demo, audio, interactive start via X->menu, A->play, save/load roundtrip | A long no-input continuation after the 15,000-frame interactive state reaches a black wait screen; longer input-driven scene/decision validation is still needed |

## Renderer

- HQx 2x/3x/4x are GPU rule-table passes over the palette texture, followed by
  sharp-bilinear finishing, in OpenGL, Direct3D 11, and Vulkan. The 256x32
  `RG32UI`/`R32G32_UInt` rule table is uploaded once. 4K HQx now costs 0.14-0.34 ms
  present and 0.09-0.17 ms GPU on the RTX 4070, with 0 managed bytes/frame; it is no
  longer CPU-bound. The GPU pattern uses `float` YUV while the CPU reference uses
  `double`, so a tiny fraction of colors (~0.07%) can classify differently by one
  LSB; the opt-in parity tests match the CPU expansion on the synthetic frame with 0
  differing pixels, and WGL/ANGLE/Vulkan/D3D 640x480 HQx captures are byte-identical
  to the CPU expansion. The shaders use explicit `trunc` for both the YUV and
  `fetchColor` conversions because NVIDIA's GLSL compiler rounded `int(float)` and
  ANGLE's GLES float-to-uint conversion rounded `k + 0.5` in the first version.
- CRT bloom is implemented as a 3x3 native-resolution glow in the CRT filter.
- FSR 1.0 is opt-in (EASU + RCAS, two GPU passes). It has a sharpness setting and
  MIT attribution. The 4K WGL probe measures present 0.132 ms mean / 1.191 ms p99 and
  GPU 0.441/0.483 ms; the optional Vulkan backend measures 0.604/1.303 ms present and
  0.404/0.415 ms GPU. The Vulkan figures exclude the optional per-frame readback; the
  desktop `--gl-backend vulkan` path still renders offscreen and copies the frame back.
- The xBR path now samples the GPU-expanded palette texture directly; it no longer
  uploads a CPU `FrameAssembler` copy. WGL and ANGLE 4K xBR probes pass, and the
  320x240 xBR capture is within one mean channel level of the accurate frame.
- Tier A 4K presentation is measured with `--gl-probe` (RTX 4070: sharp-bilinear GPU
  p99 0.089 ms, present p99 0.208 ms, zero renderer/mailbox allocations per frame).
  Windows ANGLE/GLES 3.0 is validated (4K present p99 0.47 ms, pixel parity with the
  CPU oracle); GLES 3.1, Tier B desktop timing, and 100/150/200% DPI
  validation are not complete.
- The platform swap interval is wired (WGL/GLX/EGL) and reports whether the extension
  accepted the request. The Avalonia compositor paces the window independently, so a
  requested vsync may not cap presentation at 60 Hz; the probe reports the renderer
  cost separately.
- **Display refresh above 60 Hz is re-presentation, not interpolation.** The 3DO outputs
  60 fields per second (50 for PAL), so `DisplayRefreshRate` (Settings > Video) re-presents
  the latest frame more often and lowers presentation latency, but motion stays at the
  console rate. The achieved rate is also bounded by the platform compositor (Avalonia 12
  raises its render timer to the monitor frequency on Windows; other platforms may stay
  at 60). Frame interpolation is not implemented.
- **Run-ahead is CPU-bound and opt-in.** One frame costs a full state capture/restore
  (~5.6 ms on the current desktop) plus one extra emulated frame. Titles that cannot hold
  the audio-derived deadline are auto-disabled after 45 overruns. Rewind history is cleared
  and deterministic movie recording/playback is refused while run-ahead is active. Frame
  delay above zero intentionally increases input-to-photon latency. VRR only removes the
  blocking swap interval; the driver, monitor, and desktop must enable adaptive sync.
- The GPU probe's residual ~2.3 KB/frame allocation is outside `OpenGlRenderer.Present`
  and comes from the Avalonia UI/compositor loop; the renderer/mailbox path itself is
  zero-allocation after warm-up.
- Linux/macOS OpenGL launch validation is not complete.
- The optional Vulkan backend (`--gl-backend vulkan` / `--vk-probe`) hosts a native
  Win32 child window inside the Avalonia window on Windows, so the desktop path has no
  CPU readback. The child window is outside Avalonia's compositor, so Avalonia-drawn
  overlays over the video rectangle are obscured; X11/Wayland/Metal surfaces and
  non-Windows Vulkan device validation are pending. The offscreen/readback path remains
  for probes, screenshots, and non-Windows fallback. xBR-lv2, FSR 1.0
  (EASU + RCAS), and HQx 2x/3x/4x are GPU passes. See [`Vulkan.md`](Vulkan.md).

## Tooling and UI

- `fs list`/`fs extract` now parse the Opera file system directly from ISO, BIN,
  CUE+BIN, and CHD images. The parser handles multi-block directories and
  `burst`/`gap` file layouts; malformed or non-3DO images fail with a clear parse
  error. `disc tracks` reports ISO, BIN, and CUE images as a single 2048-byte
  user-data track.
- CLI JSON casing is split: `state inspect`/`state slots` are PascalCase for historical
  compatibility; other commands are camelCase.
- Save states Brotli-compress large sections, so a state is typically 0.6-2.0 MB instead
  of the ~10 MB stock-layout uncompressed size (the largest layout is ~26 MB). Version-1
  states still load and remain ~25 MB. Ten compressed slots use a few MB. Older builds
  that only understand version 1 reject section versions they do not know with
  `Unsupported section N version M.`
- The save-state header records the disc hash. Loading with a different disc now adds
  a warning to `StateWarnings` (surfaced by the CLI and the desktop status line) but
  still loads; only BIOS/emulator-version mismatches are otherwise warned about.
  Verify slot metadata before continuing a run.
- Some code comments are stale (for example `EmulatorTuning.HighResolution` says the
  mode is reserved even though it is implemented). Treat behavior/tests as authoritative.

## Packaging and platforms

- CI builds three x64 bundles: Windows, Linux, and macOS. Windows and Linux run the CLI
  and frontend smoke tests; the macOS x64 bundle is cross-published on Linux and
  validated as Mach-O x86_64, but it is not executed, so macOS runtime validation is
  post-1.0.
- arm64 (Windows/Linux/macOS) is not hardware-validated.
- **Release bundles are framework-dependent and not trimmed.** The CLI and desktop app
  publish as single-file apphosts (`PublishTrimmed=false`); the .NET 10 runtime must be
  installed on the host and is **not** bundled. PDB and XML documentation files are
  removed from the bundles. Trimming remains available for developer and NativeAOT
  experiments. See
  [`packaging/README.md`](https://github.com/purelogiccode/5DO/blob/master/packaging/README.md).
- NativeAOT, installers, signing, and notarization are not complete. The opt-in
  win-x64 CLI AOT build is validated, including live OpenAL; the frontend AOT build
  starts/renders and its bundled OpenAL path was validated with OpenAL's null device
  (`ALSOFT_DRIVERS=null`) on 2026-10-29.
- The macOS gamepad backend is not implemented.
- The bundled OpenAL Soft 1.23.1 runtime adds an LGPL-2.0-or-later binary
  distribution obligation to the release. The notice, complete license text,
  three-year written source offer, and relinking instructions ship in
  [`OpenALSoft.md`](OpenALSoft.md) and
  [`OpenAL-Soft-COPYING.txt`](OpenAL-Soft-COPYING.txt); the
  project-owner legal sign-off is recorded in
  [`ReleaseLegalReview.md`](ReleaseLegalReview.md) (2026-11-02).

## Audio

- The DSP sample clock was corrected on 2026-10-27 to exactly 44,100 samples per
  emulated second. The old FreeDO 8.24 clock ran ~0.7% fast; audio content is the
  same, but the 3,000-frame boot keys and some FMV-timed frames changed. See
  [`TestCorpus.md`](TestCorpus.md) 1.1/1.5a2.
- Live OpenAL output requires a playback device. If none is present the app logs a
  warning and runs silently; the DSP still executes. The CI/automated audio smoke
  test uses OpenAL's null backend (`ALSOFT_DRIVERS=null`).
- Road Rash's loud race mix reaches full scale (2,036 clipped frames in a 10 s
  92k-state capture). WAV captures keep the raw DSP output; live OpenAL playback
  applies a soft peak limiter (0.8 threshold / 0.9 ceiling) so the resampler does
  not hard-clip. The 2026-10-28 pass fixed the remaining live-output crackle
  (buffer ownership), and `FIVEDO_AUDIO_TAP=<prefix>` records the exact queued PCM
  plus a per-buffer timing log for future diagnosis.
- The 90 s Road Rash capture matches Opera's spectrum and RMS envelope (spectral
  cosine 0.9994, 441.4 Hz tone identical). The only remaining audio check is a
  manual DSP mode-change listen, and no current title was observed stopping the DSP
  mid-run.
- Frontend NativeAOT audio is not validated; the trimmed IL and CLI AOT paths are.

## Emulation

- CD command/status timing is approximate. The README/plan describe the remaining
  `libopera` differential work; do not port `opera_cdrom.c` wholesale.
- The READ_ID media-access heuristic was retired. It raised a false `POLMA` level FIQ
  and shifted Space Ace's 3,000-frame hash; the reference's poll enables plus the
  level-held XBUS FIQ are the current model. The CD register trace now shows matching
  command bytes, status bytes, and sector data through the first command-sequence
  divergence on 20th Century Video Almanac; the residual there is the guest file-system
  read-request schedule, not CD readiness, FIQ delivery, or timer/SPORT wake timing.
  5DO and Opera deliver identical active-FIQ masks during the wait; 5DO's loader issues
  `READ_DATA` bursts between the periodic `READ_ID` polls while Opera's loader only
  polls. The current 3,000-frame audio-clock keys are in
  [`TestCorpus.md`](TestCorpus.md) 1.1.
- High-resolution mode is opt-in and validated mainly with Lemmings. It line-doubles
  240 lines to 480; it is not true vertical high-resolution rendering. A true 640x240
  non-interlaced mode has not been identified. Research options are in
  [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
- 480i field handling is a presentation feature, not a new VDLP field model. `auto`
  detects complementary fields by content (12-field confirm, 30-field release) and can
  be overridden with `weave`/`blend`; no local title is confirmed 480i, so the synthetic
  processor tests and bob-vs-auto hash comparison are the current evidence. A woven capture
  is 320x480 (PAL 320x576) with half-height pixels, and the settings dialog and CLI
  report the configured mode and detector verdict.
- PAL video, guest ODE prompts, CD-DA read-ahead/subcode, netplay, and additional
  peripherals are out of scope for v1. Multi-track TOC, raw CD-DA sectors with
  volume/mute, and M3U multi-disc playlists are implemented; known-title disc audio
  evidence remains pending.
- Portfolio OS SWI HLE (`--swi-hle`) is an opt-in performance mode, not a timing model.
  The host math matches `opera_fixedpoint_math.c`, but HLE skips the guest handler's
  cycles and register clobbers. AutoBahn Tokio and Ballz boot and run with HLE but
  produce different frame hashes; leave it off for compatibility or use a per-game
  profile. `--swi-comments` shows which SWIs a title calls.

## Logging and bug reports

- Serilog writes rolling daily files under the per-user log directory and keeps
  seven days. The desktop Logs dock/viewer is fed by the same pipeline.
- `Warning`-or-higher events are submitted asynchronously to the configured
  bug-report API with the required environment, error, and exception details.
  Submission is deduplicated for five minutes, capped at ten per minute, and
  disabled when `FIVEDO_BUG_REPORTS=0` or in CI/test runs. Expected environmental
  conditions such as a missing audio device are logged as warnings for the user but
  marked with `WithoutBugReport()` so they are not filed as bugs. Automated smoke and
  GPU-probe runs disable bug reporting entirely. If the endpoint is unreachable, the
  local file remains the source of truth and the submission is dropped after the HTTP
  timeout.
- The submit API key is embedded per the service's client instructions; override
  it with `FIVEDO_BUGREPORT_API_KEY` for a different deployment.
- Launch statistics send only the application id (`5do`) and version to the
  ApplicationStats service. No user or game data is included; set
  `FIVEDO_STATS=0` to opt out.

## Legal

- The project is licensed under the FreeDO modified GNU LGPL (see the root `LICENSE`).
  That license includes a prohibition on commercial use without the owners' approval.
  A distribution/attribution review is still pending before any public build.
- The repository does not include BIOS ROMs, game images, or extracted assets.
- Unsigned-disc support (`--allow-unsigned-discs` / Settings > Paths) is an explicit
  in-memory RSA-check bypass. It does not bundle or distribute a BIOS, a patched BIOS, or
  game images. The user must supply a legally obtained BIOS and is responsible for
  ensuring that the software they boot may lawfully be run. The patch is the community
  documented two-site change and reproduces the known No-RSA FZ-10/FZ-10E images exactly;
  the source BIOS file is never modified.

## Memory capacity and CPU clock options

- Expanded DRAM/VRAM layouts (41, 42, 81, 82, E2, F1) are implemented and validated by
  unit tests plus a homebrew probe ROM, but retail titles are not expected to boot with
  them. Opera documents the memory-capacity option as homebrew-only, and the local
  Lemmings (USA) test stops on the boot screen with more than 2 MB of DRAM. The stock 21
  layout remains the default and its golden hashes are unchanged.
- A 2 MB VRAM layout (22) keeps the stock DRAM amount and still runs, but changes the
  VRAM base the guest sees and therefore the rendered frame; do not use it for
  compatibility captures.
- The CPU clock option supports 0.5x-4x. It is a timing override, not a performance
  switch with a compatibility guarantee. The stock 12.5 MHz clock is unchanged and is
  the only validated setting for the compatibility matrix. Save states record the clock
  and warn on a mismatch.
