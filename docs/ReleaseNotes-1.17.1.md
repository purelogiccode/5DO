# 5DO 1.17.1

**Released:** 2026-11-25

5DO 1.17.1 is a bug-fix release for the 1.17.0 autosave and archive-container release.
It fixes a malformed-ZIP crash in the game library, makes `OpenGL FBO` compositor
failures fall back cleanly to the CPU presenter, and waits longer before reporting a
slow emulation stop. Linux releases now publish checksum manifests only; GPG signatures
and the keyring were removed.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.17.1-win-x64.zip` |
| Windows on ARM | `5do-1.17.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.17.1-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.17.1_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.17.1_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.17.1-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.17.1-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.17.1-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.17.1-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.17.1-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.17.1-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.17.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. The `.zsync`
files feed `AppImageUpdate`.

Linux release downloads are validated with `SHA256SUMS.txt` and
`SHA256SUMS-linux.txt` only. No `.asc` signatures or keyring are published.

## Fixes

- **A malformed ZIP no longer aborts the game-library refresh (#70180).**
  SharpCompress can throw `IndexOutOfRangeException` while enumerating a corrupt central
  directory. `ArchiveDiscImage` now translates archive parser failures into
  `DiscImageException`, and the scanner keeps the file as an error entry instead of
  faulting the parallel refresh.
- **`OpenGL FBO` compositor failures fall back to the CPU presenter (#70015, #70377).**
  Avalonia can raise the framebuffer failure from a render task whose exception is never
  observed. The unobserved-task handler now routes it through the existing presenter
  fallback and does not file a bug report for a handled driver limitation.
- **Slow stops wait five seconds and file one report (#70299, #70300).** A blocking disc
  read or audio-driver callback on a slow machine can outlast the previous two-second
  join window. The core runner logs the timeout at Debug and lets the desktop shell
  report the user-visible failure once.

## Linux release integrity

The release pipeline no longer creates detached GPG signatures or publishes
`5do-archive-keyring.gpg`. `SHA256SUMS-linux.txt` covers the `.deb`, `.rpm`, AppImage,
and Flatpak artifacts; `SHA256SUMS.txt` covers all five bundles and the Linux packages.
Validate a download with `sha256sum -c`.

## Dependencies

Avalonia 12.2.0 (with Desktop, Fluent, and Fonts.Inter), CHDSharp 1.7.2,
Meziantou.Analyzer 3.0.342, mkdocs-material 9.8.2, and the current GitHub Actions pins.

## Verification

- 2,602/2,602 tests pass in Release; build, `dotnet format`, `jb inspectcode`, and
  `mkdocs build --strict` are clean.
- The same release bundles were exercised on Windows x64, Windows arm64, Linux x64,
  Linux arm64, and macOS arm64 with Wolfenstein 3D (USA) and `panafz1.bin`: all produced
  frame hash `0x110E7DC5`, PC `366944`, game ID `3dfcdbbff14ffb59e157e27f18d9df7b`,
  and identical 600-frame PNGs.
- `selftest` reports 15/15 and the frontend smoke passed on every target (the
  pre-release bundles carry the 1.17.0 version string; the release workflow rebuilds
  the same sources as 1.17.1).
- Windows x64 OpenGL and Direct3D 11 probes pass on the Radeon RX 580; Windows arm64
  GL/ANGLE and Direct3D probes pass on the Microsoft Basic Render Driver.

## Known issues

- A ZIP/7z holds one image or CUE+BIN set. Entries are decompressed in memory with a
  1.5 GB per-entry limit; CHD/CDI/NRG, encrypted, and multi-volume archives must be
  extracted or decrypted first.
- The FZ-10 audio-only BIOS player transport remains at **STOP** (SYS-01). Use
  `disc audio` to verify the drive's CD-DA path.
- `--opera-soft-reset` remains opt-in/experimental; `--opera-dipir` still blocks the
  reporter's boot and needs a CSTAT/DIPIR transcript.
