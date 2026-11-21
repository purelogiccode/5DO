# 5DO 1.16.0

**Released:** 2026-11-21

5DO 1.16.0 adds CD-DA playback from a host optical drive and hardens the disc and
screenshot paths reported in issue #8. It is a drop-in update for 1.15.0; no emulation or
save-state format changed.

Download from [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.16.0).

## Download

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.16.0-win-x64.zip` |
| Windows on ARM | `5do-1.16.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.16.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint | `5do_1.16.0_amd64.deb` (arm64: `5do_1.16.0_arm64.deb`) |
| Fedora, RHEL, openSUSE | `5do-1.16.0-1.linux.x86_64.rpm` (arm64: `...aarch64.rpm`) |
| Any Linux, no install | `5do-1.16.0-linux-x86_64.AppImage` (arm64: `...aarch64.AppImage`) |
| Any Linux, sandboxed | `5do-1.16.0-linux-x86_64.flatpak` (arm64: `...aarch64.flatpak`) |
| Any Linux, portable | `5do-1.16.0-linux-x64.tar.gz` (arm64: `5do-1.16.0-linux-arm64.tar.gz`) |

## Added

- **Physical CD-DA.** A host drive's table of contents is exposed through
  `ITrackedDiscSource`. The TOC is read with `IOCTL_CDROM_READ_TOC` on Windows and
  `CDROMREADTOCHDR`/`CDROMREADTOCENTRY` on Linux; raw 2352-byte Red Book frames use
  `IOCTL_CDROM_RAW_READ` in CDDA mode or `CDROMREADAUDIO`. The 3DO CD player and games
  can list and play audio tracks, and `disc tracks`/`disc verify` include them.
- **Mixed-mode CD-DA end addressing** follows the physical track table rather than the
  3DO volume-header data limit, so audio tracks after the data track play.

## Fixed

- **A failed disc read no longer stops emulation.** The issue #8 CRC error at sector
  103777 faulted the core thread with **Emulation fault** and **Core thread stopped**.
  A failed host read is now zero-filled, reported to the guest as MEI receive-ECC, and
  logged while the transfer continues. The boot-time volume-header probe and CD-DA
  sample reads use the same guard.
- **Screenshot and save-slot thumbnail write failures** are reported as an OSD warning
  instead of crashing the UI thread.
- **`disc tracks` uses a 64-bit `dataSize`**, so multi-gigabyte discs no longer print an
  overflowed byte count.

## Verification

- 2,576/2,576 tests, build 0 warnings/0 errors, `dotnet format`, and `jb inspectcode`.
- `CdromReadErrorTests` covers the failed-sector, continued-transfer, and boot-probe
  cases; `PhysicalDiscTocTests` covers Windows TOC parsing, track coverage, and MSF
  conversion.
- The Windows TOC parse and cooked reads were validated on a mounted virtual optical
  drive. A real mixed-mode audio CD remains the hardware check.
- The Windows x64 bundle passes `selftest` 15/15 and `FRONTEND SMOKE OK 1.16.0`; the
  600-frame hash is `0x110E7DC5` and the 3,000-frame hash is `0xAD4F370C`.

## Known limits

- **Way of the Warrior from a burned CD-R** no longer crashes on the FMV; it pauses for a
  moment while the drive recovers the bad sector (CRC at sector 103777) and then
  continues. A CHD/image or a clean burn avoids the pause.
- **A Red Book audio CD reaches the FZ-10 CD player** with track 1 listed, but the
  transport remains at **STOP** (the known audio-only BIOS-player residual, SYS-01); the
  emulator keeps running at 59.9 fps.
- **The opt-in Opera DIPIR path** still blocks a physical boot. The default path boots
  burned CD-R games; the Opera soft-reset path is reported to boot on its own.
- Physical CD-DA awaits a real mixed-mode hardware pass.
- `--opera-soft-reset` remains opt-in and experimental.
- `--high-res` remains a presentation-only 2x upscale; `--true-2x` is experimental.
- No BIOS ROMs, game images, or extracted assets are included.
