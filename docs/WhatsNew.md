# What's new in 5DO 1.13.2

**Released:** 2026-11-19

5DO 1.13.2 fixes movie verification and turns a partial extraction into a clear message
instead of a Skia crash. It is a drop-in update for 1.13.1; no settings or save states
change.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.13.2-win-x64.zip` |
| Windows on ARM | `5do-1.13.2-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.13.2-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.13.2_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.13.2-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.13.2-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.13.2-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.13.2-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-*.txt` list hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## What changed

- **Movie verify matches the recording again.** A frame boundary without a V1 input
  sample made a 600-frame recording store 599 packets, so verification reported
  `frames 598 != 599`. The movie header now records the emulated frame span, playback
  runs it, and verification compares it. Discs and BIOS images are resolved
  automatically (or reported with an actionable message), and `movie record --disc`
  works like `movie play --disc`. A 600-frame disc movie and a 300-frame `--state-in`
  branch both verify with exit 0.
- **A partial extraction tells you what is missing.** Running the apphost without
  `libSkiaSharp`/`libHarfBuzzSharp` now shows a **5DO cannot start** dialog that names
  the missing files and explains how to extract the archive, instead of crashing deep
  inside Skia or filing a bug report (issues #69923/#69924).
- **CHDSharp 1.7.1** is the new dependency baseline.

## Still in 1.13.1

- Native macOS Metal presenter (`CAMetalLayer`) at 60 fps, all eight filters, and the
  drag-to-Applications DMG.
- Signed Linux release chain: keyring, signed `SHA256SUMS-linux.txt`, and a detached
  `.asc` for every Linux artifact.
- Self-contained single-file apphosts for Windows, Linux, and macOS on both
  architectures; no separate .NET runtime install.
- ARM64 packages validated on real ARM64 hardware (Linux and Windows).
