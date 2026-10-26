# Known Issues

**Snapshot:** 2026-10-25. This list covers the supported local corpus and the current
prototype state. It is not a compatibility guarantee.

## Real-game status

| Title | Status | Remaining issue |
|---|---|---|
| Wolfenstein 3D | Boots, starts FLOOR 1-1, walk/turn/fire, save/load roundtrip | The fresh-game level load can stay static; A every 300 frames advances it through character select to FLOOR 1-1 (64,000 frames, hash `0x2F5B9864`) |
| Lemmings | Boots, title/passcode, pure D-pad Fun 4 passcode, gameplay, save/load, 640x480 title | The passcode is typed from a passcode-screen state; the title-to-passcode transition still uses the existing `Down`/`A` script |
| Road Rash | Boots, 3D race renders, `BUSTED`/race-select/new-race flow works, autopilot cycles race -> wreck -> menu -> new race to 100k, save/load roundtrip | A full 5.5-mile race is not finished; the autopilot still wrecks or ends on a sidewalk. The old ~42k/~92k READ_ID wait is fixed by the CD media-access pulse |
| Alone in the Dark | Boots, in-game room, upright character, Begin/Resume menu, intro, scripted room movement with save/load roundtrip | The intro needs A input; inventory interaction and a longer exploration sequence are not scripted |
| Space Ace | Boots, animated demo, audio, interactive start via X->menu, A->play, save/load roundtrip | Longer scene/decision validation is still needed |

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
  GLES/ANGLE validation, Raspberry Pi 4/5 validation, Tier B timing, and
  100/150/200% DPI validation are not complete.
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
- NativeAOT, installers, signing, and notarization are not complete. NativeAOT is not
  on the v1 critical path; the Avalonia and Silk.NET interop paths still need an AOT
  audit.
- The macOS gamepad backend is not implemented.

## Emulation

- CD command/status timing is approximate. The README/plan describe the remaining
  `libopera` differential work; do not port `opera_cdrom.c` wholesale.
- The former static READ_ID-only wait states are now addressed by a CD media-access
  pulse: two consecutive READ_ID polls with at least 50 frames between them set poll
  bit `0x40`, which Portfolio uses to finish a post-race/post-load transition. This
  advances Road Rash's ~42k/~92k/100k screens to `BUSTED`, race select, and a new
  race. The five 3,000-frame corpus hashes are unchanged. Alone's intro and
  Wolfenstein's fresh-game load share the old signature and should be re-checked
  with this fix.
- High-resolution mode is opt-in and validated mainly with Lemmings. A true 640x240
  non-interlaced mode has not been identified.
- PAL video, CD-DA/Red Book audio, multi-disc swapping, netplay, rewind, and additional
  peripherals are out of scope for v1.

## Legal

- The FreeDO/4DO license includes restrictions, including a prohibition on commercial
  use without the owners' approval. A distribution/attribution review is still pending
  before any public build.
- The repository does not include BIOS ROMs, game images, or extracted assets.
