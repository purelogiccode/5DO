# 5DO 1.13.2

**Released:** 2026-11-19

5DO 1.13.2 is a bug-fix release: movie playback verification now matches the recorded
evidence exactly, and a partial extraction of the release archive explains itself instead
of crashing inside Skia. It also carries the CHDSharp 1.7.1 dependency update.

## Which file do I need?

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.13.2-win-x64.zip` |
| Windows on ARM | `5do-1.13.2-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.13.2-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.13.2_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.13.2_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.13.2-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.13.2-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.13.2-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.13.2-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.13.2-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.13.2-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.13.2-linux-x64.tar.gz` (arm64: `linux-arm64`) |

**Extract the archive before running it.** The apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. Running
`FiveDo.exe` from inside the ZIP, or copying only the executable, cannot start the
desktop app.

**Ignore the rest unless you are verifying a download:** the `.asc` files are detached
signatures, the `.zsync` files feed `AppImageUpdate`, and `SHA256SUMS-linux.txt` /
`SHA256SUMS.txt` list hashes. `5do-archive-keyring.gpg` is the public key those
signatures need.

## Fixed

- **Movie verification no longer stops one frame early.** A 600-frame recording stores
  599 input packets because one frame boundary has no V1 sample; playback now runs the
  full emulated span, and verification compares that span instead of the packet counter.
  A 600-frame Theme Park movie and a 300-frame `--state-in` branch both verify with exit
  code 0. Playback and verification also resolve the recorded disc (beside the movie or
  in the working directory) and the BIOS (by MD5 in the default folders), and fail with
  an actionable message instead of silently replaying without them. `movie record
  --disc` now matches `movie play --disc` (`SYS-26`).
- **Partial extractions explain themselves.** The desktop frontend checks for
  `libSkiaSharp` and `libHarfBuzzSharp` before Avalonia starts and shows a "5DO cannot
  start" message naming the missing files and the extraction steps, then exits without a
  fatal crash or a bug report. This covers the `SkiaSharp.SKImageInfo` /
  `Dll was not found` reports in issues #69923 and #69924 (`UX-36`).

## Changed

- **CHDSharp 1.7.1** replaces 1.7.0. The lock files were refreshed for the central
  package version.

## Verification

- 2,449/2,449 tests, `dotnet format`, `jb inspectcode`, and `mkdocs --strict`.
- The published `linux-arm64` and `win-arm64` 1.13.1 assets were validated on real ARM64
  hosts (full suite, CLI gate, packaging, and desktop presentation); see
  [Compatibility.md](Compatibility.md) §10.5–§10.6.
- CI builds the five bundles and the `.deb`/`.rpm`/AppImage/Flatpak sets, installs the
  `.deb`/`.rpm`, runs the sandboxed Flatpak `selftest`, and verifies the signed APT/RPM
  mirror trees.

## Known limits

- macOS Retina 2x presentation is still unvalidated (no HiDPI host).
- The bundled OpenAL Soft runtime needs the Microsoft Visual C++ 2015–2022 ARM64
  redistributable on a clean Windows image; the next release ships or detects it
  (`REL-11`).
- A Windows ARM64 machine with no Vulkan driver and no audio endpoint runs the software
  paths only; that is a host limitation, not an emulator defect.
- No hosted package repository or Flathub listing yet.
- Intel macOS is unsupported.
