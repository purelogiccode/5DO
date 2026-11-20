# What's new in 5DO 1.14.1

**Released:** 2026-11-20

5DO 1.14.1 is a bug-fix release for the deep-review pass. It fixes CHD Mode 2 native
track sectors, stale cropped-frame dimensions after auto-crop stops, and several
persistence, shutdown, and presentation races. It is a drop-in update for 1.14.0; no
save-state format changed.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.1-win-x64.zip` |
| Windows on ARM | `5do-1.14.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.1-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.14.1_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.14.1-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.14.1-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.14.1-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.14.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-*.txt` list hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Fixed

### CHD Mode 2 sectors and crop dimensions

- **CHD Mode 2 native track sectors are aligned correctly.** `ReadTrackSector` now
  returns the frame from offset zero for 2336-byte Mode 2 and 2324-byte Form 2 tracks.
  `ReadSector` still skips the subheader to reach the 2048-byte user data. This fixes
  raw-sector consumers, disc-health/DAT hashing, and multi-track audio reads.
- **Clearing auto-crop restores the live frame size.** When auto-crop stopped cropping
  or a manual crop was cleared, the cached cropped frame kept its old dimensions and
  could mis-size CPU presentation, screenshot metadata, and mouse pointer mapping.

### Persistence and bad input

- **Settings and caches recover from malformed JSON.** Null collections, invalid enums,
  out-of-range presentation values, null cache entries, and null BIOS overrides are
  repaired or ignored instead of reaching a caller that assumes non-null data.
- **State validation is strict.** Invalid CD-ROM FIFO pointers and track ranges, invalid
  MADAM CEL FSM states, and truncated sections now raise `InvalidStateException` with a
  clear diagnostic instead of reading outside the state.
- **Writes are atomic and collision-free.** Save slots, screenshots, NVRAM, BIOS,
  movies, library caches, cover metadata, update cache, diagnostics bundles, and the
  achievement queue now use unique temporary names, replace the destination, and clean
  up the temporary on both success and failure.

### Shutdown, devices, and audio

- **Workers are not disposed under a live thread.** The OpenAL fill thread,
  RetroAchievements unlock queue, bug-report sink, core thread, and audio recorder
  handoff are left intact when they cannot stop in time.
- **Vulkan and Direct3D failure paths are bounded.** Vulkan surface acquisition times out
  instead of blocking the UI, resize waits for the device to idle, and Direct3D restarts
  only for real device-removal/hang/reset failures.
- **CD audio keeps playing while muted.** The CD-DA cursor advances even at zero volume,
  so unmuting does not replay the muted interval or truncate the track.
- **DirectInput Y axes are positive-up**, matching the rest of the frontend, and the
  eight-device PBUS chain packet is large enough for eight flightsticks.

### Containers, CLI, and presentation

- **Container parsing is stricter.** CDI signatures require at least one track, the CDI
  parser reads exactly the declared session count, NRG ETN2 entries use the 28-byte
  layout, and CUE/CHD Mode 2 Form Mix tracks use the 2336-byte subheader layout.
- **The library and database scanners accept standalone `.bin` images** and single-letter
  GoodTools region tags.
- **Presentation polish.** OpenGL allocates GL 3.3/GLES 3.0-compatible texture storage
  with `TexImage2D`, Direct3D/Vulkan honor fixed output resolutions, Vulkan maps bicubic
  to its shader path, and the GPU void color is used for letterboxing.
- **CLI robustness.** Boolean options are parsed as flags, disc conversion reports
  warnings and notes, and a failed run releases the disc and trace writers. Language
  selection honors the first supported locale in a `LANGUAGE` priority list.

## Verification

- 2,534/2,534 tests, `dotnet format --verify-no-changes`, `jb inspectcode` at warning
  severity, and `mkdocs build --strict`.
- CI passes on Windows, Linux, and macOS. The release workflow publishes the five
  bundles and the signed Linux packages.
- A new 2336-byte CUE/CHD round-trip test compares both the native track sector and the
  2048-byte user data, covering the disc-hashing regression.

## Still in 1.14.0

- `--high-res` remains a presentation-only 2x upscale; the experimental `--true-2x`
  path is available for every title.
- Gamegun titles keep a joypad on port 1. **P** pauses, **Space** toggles fast-forward,
  **F1** connects the Gamegun, and **Alt+Enter** toggles fullscreen.
- macOS arm64 presents through the native Metal surface, and Linux artifacts remain
  GPG-signed with keyring fingerprint
  `E69FF266D1FB01D3411578775C4C1E8EAFD44075`.

## Known limits

- The legacy `--high-res` path is an output enlargement, not true internal 640x480 CEL
  rendering; vertical detail remains 240 lines. Use `--true-2x` for the experimental
  guest-visible path.
- Bitmap/SPORT sampling and framebuffer-read semantics in true 2x mode remain research
  work; test each title individually.
- macOS Retina 2x presentation is still unvalidated, and Intel macOS is unsupported.
