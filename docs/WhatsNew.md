# What's new in 5DO 1.13.1

**Released:** 2026-11-18

5DO 1.13.1 is a naming and documentation release: the Linux downloads now say what they
are in the file name, and every release page starts with a table that maps your system to
the one file you need. No BIOS, game images, or extracted assets are included.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.13.1-win-x64.zip` |
| Windows on ARM | `5do-1.13.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.13.1-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.13.1_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.13.1-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.13.1-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.13.1-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.13.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-*.txt` list hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## What changed

- **Linux artifacts carry the platform and version in the name.** For example
  `5do-1.13.1-1.linux.x86_64.rpm` instead of `5do-1.13.0-1.x86_64.rpm`, and
  `5do-1.13.1-linux-x86_64.AppImage` instead of `5do-x86_64.AppImage`. The `.deb` keeps
  the Debian `package_version_arch.deb` convention, and every `.asc`/`.zsync` follows
  its artifact.
- **A download table leads each release page**, so you no longer have to guess among
  signatures and update files.
- **Inspector cleanup:** `jb inspectcode` is clean again.

## Still in 1.13.0

- **Native Metal presenter on macOS:** `CAMetalLayer` surface, 60 fps at 1080p FIFO, 0
  bytes/frame, live resize and fullscreen, 320x240 capture parity, 4K offscreen output.
- **Phoenix/4DO presentation options:** audio channel swap, 4DO/Bumps/Metal void
  patterns, void color on the GPU presenters, and the emulation-health badge.
- **Signed Linux releases:** the ed25519 key (`E69FF266D1FB01D3411578775C4C1E8EAFD44075`)
  signs the manifest, every artifact, and the mirror metadata.

## Known limits

- macOS Retina 2x remains unvalidated (no HiDPI host).
- The Linux arm64 AppImage is built and smoke-tested in CI but not run on arm64 hardware.
- No hosted package repository or Flathub listing yet.
- Intel macOS is unsupported.
