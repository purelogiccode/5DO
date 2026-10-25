# Known Issues

**Snapshot:** 2026-10-25. This list covers the supported local corpus and the current
prototype state. It is not a compatibility guarantee.

## Real-game status

| Title | Status | Remaining issue |
|---|---|---|
| Wolfenstein 3D | Boots, starts FLOOR 1-1, walk/turn/fire, save/load roundtrip | The fresh-game level load can stay static for a long time; CD read pacing is still under investigation |
| Lemmings | Boots, title/passcode, Fun 4 gameplay, save/load, 640x480 title | Passcode entry uses a diagnostic RAM poke; pure D-pad typing of all ten letters is not scripted |
| Road Rash | Boots, 3D race renders, 91,000 live frames with `--autopilot road` v10 | The city race is not finished; ~92,000 wall/rider pin and a clean 30k->40k right-sidewalk wreck remain |
| Alone in the Dark | Boots, in-game room, upright character, menu | A full movement/inventory sequence is not scripted |
| Space Ace | Boots, animated demo, audio | The interactive path is not validated end-to-end |

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
- High-resolution mode is opt-in and validated mainly with Lemmings. A true 640x240
  non-interlaced mode has not been identified.
- PAL video, CD-DA/Red Book audio, multi-disc swapping, netplay, rewind, and additional
  peripherals are out of scope for v1.

## Legal

- The FreeDO/4DO license includes restrictions, including a prohibition on commercial
  use without the owners' approval. A distribution/attribution review is still pending
  before any public build.
- The repository does not include BIOS ROMs, game images, or extracted assets.
