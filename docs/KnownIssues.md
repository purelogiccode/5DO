# Known Issues

**Snapshot:** 2026-10-25. This list covers the supported local corpus and the current
prototype state. It is not a compatibility guarantee.

## Real-game status

| Title | Status | Remaining issue |
|---|---|---|
| Wolfenstein 3D | Boots, starts FLOOR 1-1, walk/turn/fire, save/load roundtrip | The fresh-game level load can stay static; A every 300 frames advances it through character select to FLOOR 1-1 (64,000 frames, hash `0x2F5B9864`) |
| Lemmings | Boots, title/passcode, Fun 4 gameplay, save/load, 640x480 title | Passcode entry uses a diagnostic RAM poke; pure D-pad typing of all ten letters is not scripted |
| Road Rash | Boots, 3D race renders, `BUSTED`/race-select/new-race flow works, autopilot cycles race -> wreck -> menu -> new race to 100k, save/load roundtrip | A full 5.5-mile race is not finished; the autopilot still wrecks or ends on a sidewalk. The old ~42k/~92k READ_ID wait is fixed by the CD media-access pulse |
| Alone in the Dark | Boots, in-game room, upright character, Begin/Resume menu, intro scene; A pulses leave the intro wait | The intro still needs A input; a blind pulse cycles cutscenes but a full movement/inventory sequence is not scripted |
| Space Ace | Boots, animated demo, audio, interactive start via X->menu, A->play, save/load roundtrip | Longer scene/decision validation is still needed |

## Renderer

- xBR is a single 4x pass followed by sharp-bilinear finishing. HQx is not shipped.
  CRT bloom is not implemented.
- The xBR path expands the palette on the CPU at native resolution before uploading the
  source texture. Other filters use GPU palette expansion. This is a deliberate
  workaround for a driver readback issue with the FBO-attached palette texture.
- GPU timer queries, a real 4K present-to-present measurement, GLES/ANGLE validation,
  Raspberry Pi 4/5 validation, and 100/150/200% DPI validation are not complete.
- Vsync is reported as a preference; the platform swap-interval path is not wired on
  every backend.
- Linux/macOS OpenGL launch validation is not complete.

## Packaging and platforms

- CI publishes six RIDs, but only the win-x64 CLI and desktop app have been smoke-tested.
- arm64 (Windows/Linux/macOS) is not hardware-validated.
- **Trimming is not enabled.** A `win-x64` self-contained trimmed publish of the CLI
  fails the `IL2026` analyzer on `JsonSerializer.Serialize/Deserialize` in
  `InputProfile` (and the same pattern exists in `AppSettings`, save-slot metadata, and
  CLI result types). JSON source generation or `JsonTypeInfo` overloads are required
  before enabling trimming; Avalonia's XAML loader also needs a trim/AOT audit.
- Trimming/NativeAOT evaluation, installers, signing, and notarization are not complete.
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
