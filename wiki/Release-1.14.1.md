# 5DO 1.14.1

**Released:** 2026-11-20

5DO 1.14.1 is a bug-fix release for the deep-review pass. It fixes CHD Mode 2 native
track sectors, stale cropped-frame dimensions after auto-crop stops, and persistence,
shutdown, and presentation races. It is a drop-in update for 1.14.0.

Download from [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.14.1).

## Download

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.1-win-x64.zip` |
| Windows on ARM | `5do-1.14.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.1-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint | `5do_1.14.1_amd64.deb` (arm64: `5do_1.14.1_arm64.deb`) |
| Fedora, RHEL, openSUSE | `5do-1.14.1-1.linux.x86_64.rpm` (arm64: `...aarch64.rpm`) |
| Any Linux, no install | `5do-1.14.1-linux-x86_64.AppImage` (arm64: `...aarch64.AppImage`) |
| Any Linux, sandboxed | `5do-1.14.1-linux-x86_64.flatpak` (arm64: `...aarch64.flatpak`) |
| Any Linux, portable | `5do-1.14.1-linux-x64.tar.gz` (arm64: `5do-1.14.1-linux-arm64.tar.gz`) |

## Fixed

- **CHD Mode 2 native track sectors** now start at frame offset zero for 2336-byte Mode 2
  and 2324-byte Form 2 tracks, while the 2048-byte user-data read still skips the
  subheader. Disc-health/DAT hashing and multi-track audio reads no longer see shifted
  sectors.
- **Clearing auto-crop or a manual crop** drops the cached cropped frame, so CPU
  presentation, screenshot metadata, and mouse pointer mapping use the live frame size.
- **Malformed settings and caches** recover instead of crashing. Null collections,
  invalid enums, out-of-range presentation values, null cache entries, and null BIOS
  overrides are repaired or ignored.
- **State and CD-ROM validation** rejects invalid FIFO pointers, track ranges, MADAM CEL
  FSM states, and truncated sections with `InvalidStateException`.
- **CDI, NRG, CUE, and CHD parsing** is stricter: declared CDI sessions, the 28-byte NRG
  ETN2 layout, Mode 2 Form Mix subheaders, and standalone BIN tracks are handled.
- **Shutdown and device-loss paths** no longer dispose OpenAL, RetroAchievements,
  bug-report, core, or recorder objects under a live worker. Vulkan surface acquisition
  is bounded, and Direct3D restarts only for real device-removal/hang/reset failures.
- **CD-DA keeps advancing while muted**, so unmuting does not replay the muted interval.
- **DirectInput Y axes are positive-up**, and the eight-device PBUS chain fits eight
  flightsticks.

## Changed

- Save slots, screenshots, NVRAM, BIOS, movies, caches, cover metadata, update checks,
  diagnostics, and achievement queues now use unique temporary files, replace the
  destination, and clean up after failure.
- OpenGL uses GL 3.3/GLES 3.0-compatible `TexImage2D` storage, Direct3D/Vulkan honor fixed
  output resolutions, Vulkan maps bicubic to its shader path, and the GPU void color is
  used for letterboxing.
- The CLI parses boolean options as flags, reports disc warnings/notes, releases discs and
  trace writers after a failed run, and honors the first supported locale in a `LANGUAGE`
  priority list.
- Tooltips cover content elements as well as controls, and internal types no longer
  expose public methods accidentally.

## Verification

- 2,534/2,534 tests, `dotnet format --verify-no-changes`, `jb inspectcode` at warning
  severity, and `mkdocs build --strict`.
- CI passes on Windows, Linux, and macOS; the release workflow publishes the five bundles
  and the signed Linux packages.

## Known limits

- `--high-res` remains a presentation-only 2x upscale; `--true-2x` is the experimental
  guest-visible path.
- macOS Retina 2x is unvalidated, and Intel macOS is unsupported.
- No BIOS ROMs or game images are included.
