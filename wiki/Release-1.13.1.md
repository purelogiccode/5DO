# 5DO 1.13.1

**Released:** 2026-11-18

5DO 1.13.1 makes the Linux downloads self-describing: the RPM, AppImage, and Flatpak file
names now include the version and `linux`, and every release page opens with a table that
maps your system to the one file you need.

Download from [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.13.1).
No BIOS ROMs or game images are included.

## Which file do I need?

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.13.1-win-x64.zip` |
| Windows on ARM | `5do-1.13.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.13.1-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint | `5do_1.13.1_amd64.deb` (arm64: `5do_1.13.1_arm64.deb`) |
| Fedora, RHEL, openSUSE | `5do-1.13.1-1.linux.x86_64.rpm` (arm64: `...aarch64.rpm`) |
| Any Linux, no install | `5do-1.13.1-linux-x86_64.AppImage` (arm64: `...aarch64.AppImage`) |
| Any Linux, sandboxed | `5do-1.13.1-linux-x86_64.flatpak` (arm64: `...aarch64.flatpak`) |
| Any Linux, portable | `5do-1.13.1-linux-x64.tar.gz` (arm64: `5do-1.13.1-linux-arm64.tar.gz`) |

The `.asc` files are detached signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-linux.txt` / `SHA256SUMS.txt` list hashes; they are for verification, not for
running 5DO. `5do-archive-keyring.gpg` is the public key those signatures use.

## Changed

- **Linux artifact names carry the platform and version**, for example
  `5do-1.13.1-1.linux.x86_64.rpm` instead of `5do-1.13.0-1.x86_64.rpm`, and
  `5do-1.13.1-linux-x86_64.AppImage` instead of `5do-x86_64.AppImage`. The `.deb` keeps
  the Debian `package_version_arch.deb` convention; every `.asc` and `.zsync` follows its
  artifact.
- **A download table leads each release page**, so the asset list is no longer a guessing
  game.
- **`jb inspectcode` is clean** after removing two dead null checks, an unused OpenAL
  device pointer, a redundant `using`, and a redundant Direct3D clear argument.

## Still in 1.13.0

Native Metal presenter on macOS, the Phoenix/4DO presentation options (audio channel
swap, named void patterns, GPU void color, emulation-health badge), and signed Linux
releases with the `E69FF266D1FB01D3411578775C4C1E8EAFD44075` key.

## Known limits

- macOS Retina 2x is unvalidated (no HiDPI host).
- The Linux arm64 AppImage is built and smoke-tested in CI but not run on arm64 hardware.
- No hosted package repository or Flathub listing yet.
- Intel macOS is unsupported.
