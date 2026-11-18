# 5DO 1.13.1

**Released:** 2026-11-18

5DO 1.13.1 makes the Linux downloads self-describing: the RPM, AppImage, and Flatpak file
names now include the version and the platform, and every release starts with the table
below so you only download what you need.

## Which file do I need?

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.13.1-win-x64.zip` |
| Windows on ARM | `5do-1.13.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.13.1-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.13.1_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.13.1_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.13.1-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.13.1-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.13.1-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.13.1-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.13.1-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.13.1-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.13.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

**Ignore the rest unless you are verifying a download:** the `.asc` files are detached
signatures, the `.zsync` files feed `AppImageUpdate`, and `SHA256SUMS-linux.txt` /
`SHA256SUMS.txt` list hashes. `5do-archive-keyring.gpg` is the public key those
signatures need.

## Changed

- **Linux artifacts say what they are.** The names now carry the version and the
  platform:

  | Before (1.13.0) | Now (1.13.1) |
  |---|---|
  | `5do-1.13.0-1.x86_64.rpm` | `5do-1.13.1-1.linux.x86_64.rpm` |
  | `5do-x86_64.AppImage` | `5do-1.13.1-linux-x86_64.AppImage` |
  | `5do-aarch64.flatpak` | `5do-1.13.1-linux-aarch64.flatpak` |

  The `.deb` keeps the Debian `package_version_arch.deb` convention, and every `.asc`
  and `.zsync` follows the artifact it belongs to. Signatures, the signed manifest, and
  the keyring are unchanged in behaviour.
- **A download table leads every release page**, so the number of assets stops being a
  guessing game.

## Fixed

- `jb inspectcode` is clean again: two dead null checks in BIOS discovery, an unused
  OpenAL device pointer, a redundant `using`, and a redundant Direct3D clear argument.

## Verification

- 2,439/2,439 tests, `dotnet format`, `jb inspectcode`, and `mkdocs --strict`.
- CI builds `.deb`, `.rpm`, AppImage, and Flatpak for both architectures, installs the
  `.deb`/`.rpm`, runs the Flatpak `selftest`, and verifies the signed APT/RPM mirror
  trees.
- The release key is unchanged: `E69FF266D1FB01D3411578775C4C1E8EAFD44075`.

## Known limits

- macOS Retina 2x presentation is still unvalidated (no HiDPI host).
- The Linux arm64 AppImage is built and smoke-tested in CI but not run on arm64 hardware.
- No hosted package repository or Flathub listing yet.
- Intel macOS is unsupported.
