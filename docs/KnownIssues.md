# Known Issues

**Snapshot:** 2026-10-30. This list covers the supported local corpus and the current
prototype state. It is not a compatibility guarantee.

**Release scope:** v1.0 ships with these known issues documented; full-race completion,
extra title scripts, Pi/arm64 validation, installers, and the optional JIT are
post-1.0. See [`ReleaseChecklist.md`](ReleaseChecklist.md). **As of 2026-10-30 the
release is deferred** until the full-collection compatibility sweep (M13) and the
high-resolution CEL decision (M14) are complete.

## Compatibility sweep status

- The 12 titles in the matrix below are validated. The first full loadability sweep
  (`J:\Panasonic 3DO`, 589 CHDs, plus `G:\MAME\MAME Software List CHDs\3do`, 5 CHDs)
  completed on 2026-10-30: **576 rendered, 13 black/blank, 5 not loadable** out of 594.
  The procedure is in [`CompatibilitySweep.md`](CompatibilitySweep.md); the per-disc
  table is [`GameCompatibilityResults.md`](GameCompatibilityResults.md).
- The five not-loadable images are four Arcade CD-ROM timeouts (`Crime Patrol`,
  `Mad Dog II`, `Orbatak`, `Shootout at Old Tucson RE3`) and one CHD decompression
  failure (`Mirai Shounen Conan - Digital Library`, LBA 1). The arcade titles match the
  4DO notes about a patched BIOS; rerun with a longer timeout before classifying them.
- The 13 black/blank titles need the 12,000-frame in-game rerun: Crash 'n Burn (Japan),
  Ishida Yoshio Kudan no Igo Seiha (Japan), Konpeki no Kantai (Japan and Rev 1),
  Myst (Japan, Japan Rev A, USA), Olympic Summer Games (USA), Powers Kingdom (Europe),
  Slayer (Europe, USA, USA Action Pak), and WorldCup Super Stadium (Japan).
- The 4DO wiki list is a reference, not a guarantee. Filename matching does not prove
  the local image is the same revision, and a 4DO score of 100 does not mean 5DO has
  the same behavior.
- `--high-res` is FreeDO-style line doubling, not true internal 640x480 rendering. The
  vertical detail remains 240 lines. See
  [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
- Linux/macOS OpenGL launch validation is pending. The local Linux target is the
  `LinuxMint` Hyper-V VM (`D:\Hyper-V\VMs\LinuxMint`); results are not yet recorded.
- The project is **donationware** (non-commercial, source-available). Legal/attribution
  sign-off remains a release blocker; donations do not change the upstream
  non-commercial restriction.

## Real-game status

| Title | Status | Remaining issue |
|---|---|---|
| Wolfenstein 3D | Boots, starts FLOOR 1-1, walk/turn/fire, save/load roundtrip | The fresh-game level load can stay static; A every 300 frames advances it through character select to FLOOR 1-1 (64,000 frames, hash `0x2F5B9864`) |
| Lemmings | Boots, title/passcode, pure D-pad Fun 4 passcode, gameplay, save/load, 640x480 title | The passcode is typed from a passcode-screen state; the title-to-passcode transition still uses the existing `Down`/`A` script |
| Road Rash | Boots, 3D race renders, `BUSTED`/race-select/new-race flow works, autopilot cycles race -> wreck -> menu -> new race to 100k, save/load roundtrip | A full 5.5-mile race is not finished; the autopilot still wrecks or ends on a sidewalk. The old ~42k/~92k READ_ID wait is fixed by the CD media-access pulse |
| Alone in the Dark | Boots, in-game room, upright character, Begin/Resume menu, intro, scripted room movement with save/load roundtrip | The intro needs A input; inventory interaction and a longer exploration sequence are not scripted |
| Space Ace | Boots, animated demo, audio, interactive start via X->menu, A->play, save/load roundtrip | A long no-input continuation after the 15,000-frame interactive state reaches a black wait screen; longer input-driven scene/decision validation is still needed |

## Renderer

- HQx 2x/3x/4x are implemented as opt-in CPU expansions followed by sharp-bilinear
  finishing; GPU xBR and FSR are available in the optional Vulkan backend, while HQx
  remains CPU-bound. HQx 4x costs about 7.5 ms/frame on synthetic checkerboard content
  and about 15 ms/frame on random noise at 320x240, so it is not recommended with
  `--high-res` on slow CPUs.
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
  CPU oracle); Raspberry Pi 4/5/GLES 3.1, Tier B timing, and 100/150/200% DPI
  validation are not complete.
- The platform swap interval is wired (WGL/GLX/EGL) and reports whether the extension
  accepted the request. The Avalonia compositor paces the window independently, so a
  requested vsync may not cap presentation at 60 Hz; the probe reports the renderer
  cost separately.
- The GPU probe's residual ~2.3 KB/frame allocation is outside `OpenGlRenderer.Present`
  and comes from the Avalonia UI/compositor loop; the renderer/mailbox path itself is
  zero-allocation after warm-up.
- Linux/macOS OpenGL launch validation is not complete.
- The optional Vulkan backend (`--gl-backend vulkan` / `--vk-probe`) hosts a native
  Win32 child window inside the Avalonia window on Windows, so the desktop path has no
  CPU readback. The child window is outside Avalonia's compositor, so Avalonia-drawn
  overlays over the video rectangle are obscured; X11/Wayland/Metal surfaces and
  non-Windows Vulkan device validation are pending. The offscreen/readback path remains
  for probes, screenshots, and non-Windows fallback. xBR-lv2 and FSR 1.0
  (EASU + RCAS) are GPU passes; HQx 2x/3x/4x use the shared CPU expansion and are
  CPU-bound. See [`Vulkan.md`](Vulkan.md).

## Tooling and UI

- `fs list`/`fs extract` now parse the Opera file system directly from ISO, BIN,
  CUE+BIN, and CHD images. The parser handles multi-block directories and
  `burst`/`gap` file layouts; malformed or non-3DO images fail with a clear parse
  error. `disc tracks` reports ISO, BIN, and CUE images as a single 2048-byte
  user-data track.
- CLI JSON casing is split: `state inspect`/`state slots` are PascalCase for historical
  compatibility; other commands are camelCase.
- Save-state format version 2 compresses large sections with Brotli, so a state is
  typically 0.6-2.0 MB instead of the 24.9 MB uncompressed size. Version-1 states still
  load and remain ~25 MB. The state still contains the full 19 MB RAM backing and the
  VDL frame before compression; ten compressed slots use a few MB. Older builds that
  only understand version 1 reject new states with
  `Unsupported save-state version 2.`
- The save-state header records the disc hash. Loading with a different disc now adds
  a warning to `StateWarnings` (surfaced by the CLI and the desktop status line) but
  still loads; only BIOS/emulator-version mismatches are otherwise warned about.
  Verify slot metadata before continuing a run.
- Some code comments are stale (for example `EmulatorTuning.HighResolution` says the
  mode is reserved even though it is implemented). Treat behavior/tests as authoritative.

## Packaging and platforms

- CI publishes six RIDs, but only the win-x64 CLI and desktop app have been smoke-tested.
- arm64 (Windows/Linux/macOS) is not hardware-validated.
- **CLI and frontend trimming are enabled.** Both publish with `PublishTrimmed=true`
  (`TrimMode=full`). The CLI has a Windows CI smoke test (`--version`, `selftest`,
  `bench`); the frontend has a trimmed startup smoke test (`--smoke-test`) that opens
  the real window, runs one UI pass (GL hidden for headless CI), and exits 0. The only
  remaining trim warning is CHDSharp's vendored FLAC settings helper (`TypeDescriptor`),
  which is outside the 3DO data-track path and demoted to a visible warning. See
  [`Trimming.md`](Trimming.md).
- NativeAOT, installers, signing, and notarization are not complete. The opt-in
  win-x64 CLI AOT build is validated, including live OpenAL; the frontend AOT build
  starts/renders and its bundled OpenAL path was validated with OpenAL's null device
  (`ALSOFT_DRIVERS=null`) on 2026-10-29.
- The macOS gamepad backend is not implemented.
- The bundled OpenAL Soft 1.23.1 runtime adds an LGPL-2.0-or-later binary
  distribution obligation to the release. The notice, complete license text,
  three-year written source offer, and relinking instructions ship in
  [`OpenALSoft.md`](OpenALSoft.md) and
  [`licenses/OpenAL-Soft-COPYING.txt`](licenses/OpenAL-Soft-COPYING.txt); the
  owner/legal sign-off remains in [`ReleaseLegalReview.md`](ReleaseLegalReview.md).

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
- The former static READ_ID-only wait states are now addressed by a CD media-access
  pulse: two consecutive READ_ID polls with at least 50 frames between them set poll
  bit `0x40`, which Portfolio uses to finish a post-race/post-load transition. This
  advances Road Rash's ~42k/~92k/100k screens to `BUSTED`, race select, and a new
  race. Alone's intro and Wolfenstein's fresh-game load share the old signature and
  should be re-checked with this fix. The current 3,000-frame audio-clock keys are in
  [`TestCorpus.md`](TestCorpus.md) 1.1.
- High-resolution mode is opt-in and validated mainly with Lemmings. It line-doubles
  240 lines to 480; it is not true vertical high-resolution rendering. A true 640x240
  non-interlaced mode has not been identified. Research options are in
  [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
- PAL video, CD-DA/Red Book audio, multi-disc swapping, netplay, rewind, and additional
  peripherals are out of scope for v1.

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
