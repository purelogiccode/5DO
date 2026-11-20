# 5DO 1.14.1

**Released:** 2026-11-20

5DO 1.14.1 is a bug-fix release for the deep-review pass across the host, emulation
core, storage, achievements, diagnostics, and desktop frontend. It fixes CHD Mode 2
native track sectors, stale cropped-frame dimensions after auto-crop stops, and several
persistence, shutdown, and presentation races. It is a drop-in update for 1.14.0; no
save-state format changed.

## Which file do I need?

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.1-win-x64.zip` |
| Windows on ARM | `5do-1.14.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.1-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.14.1_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.14.1_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.14.1-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.14.1-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.14.1-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.14.1-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.14.1-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.14.1-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.14.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

**Extract the archive before running it.** The apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. Running
`FiveDo.exe` from inside the ZIP, or copying only the executable, cannot start the
desktop app.

**Ignore the rest unless you are verifying a download:** the `.asc` files are detached
signatures, the `.zsync` files feed `AppImageUpdate`, and `SHA256SUMS-linux.txt` /
`SHA256SUMS.txt` list hashes. `5do-archive-keyring.gpg` is the public key those
signatures need.

## Fixed

- **CHD Mode 2 native track sectors are aligned correctly.** `ChdDiscImage` now returns
  the native frame from offset zero for 2336-byte Mode 2 and 2324-byte Form 2 tracks,
  while `ReadSector` still applies the subheader offset for the 2048-byte user data.
  Disc-health/DAT hashing and multi-track audio reads no longer see shifted sectors.
- **Clearing auto-crop no longer leaves stale frame dimensions.** The cached cropped
  frame is dropped when no crop applies, so CPU presentation, screenshot metadata, and
  mouse pointer mapping follow the live source size.
- **Malformed persisted data is repaired or ignored.** Settings with null collections,
  invalid enums, or out-of-range presentation values; library, disc-health, and update
  caches with null entries; null BIOS overrides; and null state-document values no
  longer reach callers that assume non-null data.
- **State and CD-ROM validation is strict.** Invalid CD-ROM FIFO pointers and track
  ranges, invalid MADAM CEL FSM states, and truncated state sections raise
  `InvalidStateException` instead of reading outside the state.
- **CDI, NRG, CUE, and CHD parsing is stricter.** CDI signatures require at least one
  track and the parser reads exactly the declared session count; NRG ETN2 entries use the
  28-byte layout; CUE/CHD Mode 2 Form Mix tracks use the 2336-byte subheader layout; and
  standalone BIN images expose a data track.
- **Shutdown and device-loss paths no longer race with live workers.** The OpenAL fill
  thread, RetroAchievements unlock queue, bug-report sink, core thread, and audio
  recorder handoff are left intact when they cannot stop in time. Vulkan surface
  acquisition is bounded and waits for device idle on resize; Direct3D restarts only for
  real device-removal/hang/reset failures.
- **CD-DA keeps advancing while muted.** Unmuting no longer replays the muted interval
  or cuts off the end of a track.
- **DirectInput Y axes are positive-up**, matching the frontend convention, and the
  eight-device PBUS chain packet grows to 80 bytes so eight flightsticks fit.

## Changed

- **Persistence writes are atomic and collision-free.** Settings, save slots and
  metadata, screenshots, NVRAM, BIOS, movies, library caches, cover metadata, update
  cache, diagnostics bundles, and achievement queues use unique temporary names, replace
  the destination atomically, and clean up the temporary on failure.
- **The last non-Serilog debug output is gone.** `DisplaySleep` uses Serilog for its
  screen-saver and PATH fallbacks, and previously silent fallback catches now log at
  `Debug` with the exception detail.
- **Tooltips and visibility are consistent.** Every interactive control plus every
  `TextBlock` and `Image` in the view XAML carries a tooltip directly or from an
  ancestor, and public methods declared by internal types are now internal unless they
  implement an interface, override, or framework entry point.
- **Presentation and CLI polish.** OpenGL uses GL 3.3/GLES 3.0-compatible `TexImage2D`
  allocation, Direct3D/Vulkan honor fixed output resolutions, Vulkan maps bicubic to its
  shader path, and the GPU void color is used for letterboxing. The CLI parses boolean
  options as flags, reports disc warnings and notes, releases discs and trace writers
  after a failed run, and honors the first supported locale in a `LANGUAGE` priority
  list.
- **Library and database scanners accept standalone `.bin` images** and single-letter
  GoodTools region tags.

## Verification

- 2,534/2,534 tests, `dotnet format --verify-no-changes`, `jb inspectcode` at warning
  severity, and `mkdocs build --strict`.
- CI passes on Windows, Linux, and macOS. The release workflow publishes the five
  bundles and the signed Linux packages.
- A new 2336-byte CUE/CHD round-trip test compares both the native track sector and the
  2048-byte user data.

## Known limits

- The legacy `--high-res` path is an output enlargement, not true internal 640x480 CEL
  rendering; vertical detail remains 240 lines. Use `--true-2x` for the experimental
  guest-visible path.
- Bitmap/SPORT sampling and framebuffer-read semantics in true 2x mode remain research
  work; test each title individually.
- macOS Retina 2x presentation is still unvalidated, and Intel macOS is unsupported.
- No BIOS ROMs, game images, or extracted game assets are included. Supply legally
  obtained files.
