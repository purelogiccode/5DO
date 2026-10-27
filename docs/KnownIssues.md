# Known Issues

**Snapshot:** 2026-10-27. This list covers the supported local corpus and the current
prototype state. It is not a compatibility guarantee.

**Release scope:** v1.0 ships with these known issues documented; full-race completion,
extra title scripts, Pi/arm64 validation, installers, and the optional JIT are
post-1.0. See [`ReleaseChecklist.md`](ReleaseChecklist.md).

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
  finishing; the GPU xBR/HQx variants are not shipped. HQx 4x costs about 7.5 ms/frame
  on synthetic checkerboard content and about 15 ms/frame on random noise at 320x240,
  so it is not recommended with `--high-res` on slow CPUs.
- CRT bloom is implemented as a 3x3 native-resolution glow in the CRT filter.
- The xBR path expands the palette on the CPU at native resolution before uploading the
  source texture. Other filters use GPU palette expansion. This is a deliberate
  workaround for a driver readback issue with the FBO-attached palette texture.
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
  starts/renders but its audio path is not yet validated.
- The macOS gamepad backend is not implemented.
- The bundled OpenAL Soft 1.23.1 runtime adds an LGPL-2.0-or-later binary
  distribution obligation to the release; see [`Licensing.md`](Licensing.md) and
  [`ReleaseLegalReview.md`](ReleaseLegalReview.md).

## Audio

- The DSP sample clock was corrected on 2026-10-27 to exactly 44,100 samples per
  emulated second. The old FreeDO 8.24 clock ran ~0.7% fast; audio content is the
  same, but the 3,000-frame boot keys and some FMV-timed frames changed. See
  [`TestCorpus.md`](TestCorpus.md) 1.1/1.5a2.
- Live OpenAL output requires a playback device. If none is present the app logs a
  warning and runs silently; the DSP still executes. The CI/automated audio smoke
  test uses OpenAL's null backend (`ALSOFT_DRIVERS=null`).
- Road Rash's loud race mix reaches full scale and clips (2,036 frames in a 10 s
  92k-state capture). This is the game mix, not the output path; an optional limiter
  would be non-authentic.
- A 30-60 s loudness/spectrum comparison against Opera and a manual DSP mode-change
  (stop/start) listening check remain.
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
- High-resolution mode is opt-in and validated mainly with Lemmings. A true 640x240
  non-interlaced mode has not been identified.
- PAL video, CD-DA/Red Book audio, multi-disc swapping, netplay, rewind, and additional
  peripherals are out of scope for v1.

## Legal

- The FreeDO/4DO license includes restrictions, including a prohibition on commercial
  use without the owners' approval. A distribution/attribution review is still pending
  before any public build.
- The repository does not include BIOS ROMs, game images, or extracted assets.
