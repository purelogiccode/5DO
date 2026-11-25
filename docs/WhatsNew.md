# What's new in 5DO 1.17.1

**Released:** 2026-11-25

5DO 1.17.1 is a bug-fix release for the 1.17.0 autosave and archive-container release.
It fixes a malformed-ZIP library crash, cleanly falls back from `OpenGL FBO` compositor
failures, and waits longer before reporting a slow stop. Linux releases now use
`SHA256SUMS` only.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.17.1-win-x64.zip` |
| Windows on ARM | `5do-1.17.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.17.1-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.17.1_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.17.1-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.17.1-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.17.1-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.17.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. The `.zsync`
files feed `AppImageUpdate`.

`SHA256SUMS.txt` and `SHA256SUMS-linux.txt` are the only validation files. There are no
`.asc` signatures and no keyring; run `sha256sum -c` after downloading.

## Fixes

- **Malformed ZIP files no longer abort the library refresh.** SharpCompress index
  errors are translated into a readable disc-image error, and the file stays in the
  library with its error instead of faulting the whole scan.
- **OpenGL framebuffer failures fall back to the CPU presenter.** The compositor
  failure can arrive through an unobserved task; it now uses the same presenter
  fallback as a UI-thread failure and no longer generates a bug report for a handled
  driver limitation.
- **Stopping emulation waits five seconds.** A slow disc read or audio callback no
  longer trips the old two-second timeout, and the timeout is reported once instead of
  twice.

## Linux release integrity

Linux releases no longer publish detached GPG signatures or a keyring. The `.deb`,
`.rpm`, AppImage, and Flatpak files are covered by `SHA256SUMS-linux.txt`; the five
bundles and the Linux packages are covered by `SHA256SUMS.txt`.

## Still included from 1.17.0

- Rolling autosave with startup resume, written on pause, clean exit, and a configurable
  interval and restored only when the disc game ID and BIOS MD5 match.
- Direct ZIP/7z ISO, BIN, and CUE+BIN containers, with in-memory decompression and
  extraction guidance for CHD/CDI/NRG, encrypted, and multi-volume archives.
- CD-DA extraction via `disc audio`, `disc verify` progress and mixed-mode totals, and
  guest-visible disc read-error reporting.

## Verification

- 2,602/2,602 tests pass in Release; build, `dotnet format`, `jb inspectcode`, and
  `mkdocs build --strict` are clean.
- Windows x64, Windows arm64, Linux x64, Linux arm64, and macOS arm64 ran the same
  bundle test: Wolfenstein 3D (USA) with `panafz1.bin`, 600 frames, frame hash
  `0x110E7DC5`, PC `366944`, and identical PNG SHA-256.
- `selftest` 15/15 and the frontend smoke passed on every target.
- Windows x64 OpenGL/Direct3D 11 probes pass; Windows arm64 GL/ANGLE and Direct3D 11
  probes pass.

## Known issues

- A ZIP/7z holds one image or CUE+BIN set. Entries are decompressed in memory with a
  1.5 GB per-entry limit, and CHD/CDI/NRG, encrypted, and multi-volume archives must be
  extracted or decrypted first.
- The FZ-10 audio-only BIOS player transport remains at STOP (SYS-01). Use `disc audio`
  to verify the drive's CD-DA path.
- `--opera-soft-reset` remains opt-in/experimental; `--opera-dipir` still blocks the
  reporter's boot and needs a CSTAT/DIPIR transcript.
