# What's new in 5DO 1.16.0

**Released:** 2026-11-21

5DO 1.16.0 adds CD-DA playback from a host optical drive and hardens the disc and
screenshot paths reported in issue #8. Save states and disc formats are unchanged from
1.15.0.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.16.0-win-x64.zip` |
| Windows on ARM | `5do-1.16.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.16.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.16.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.16.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.16.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.16.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.16.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Physical CD-DA

- A host drive's table of contents is exposed through `ITrackedDiscSource`. The TOC is
  read with `IOCTL_CDROM_READ_TOC` (Windows) or
  `CDROMREADTOCHDR`/`CDROMREADTOCENTRY` (Linux).
- Raw 2352-byte Red Book frames use `IOCTL_CDROM_RAW_READ` in CDDA mode on Windows and
  `CDROMREADAUDIO` on Linux.
- The 3DO CD player and games can list and play audio tracks on a burned mixed-mode CD,
  and `disc tracks`/`disc verify` include them.
- CD-DA end addressing follows the physical track table, not the volume-header data
  limit, so audio tracks after the data track play.
- Physical CD-DA still needs a real mixed-mode audio CD hardware pass; the Windows TOC
  and cooked paths were checked against a mounted virtual optical drive.

## Disc read errors no longer stop emulation

- A failed host sector read, such as the issue #8 CRC error at sector 103777, is now
  zero-filled and reported to the guest as MEI receive-ECC. The transfer continues with
  the next sector instead of faulting the core with **Emulation fault** and **Core
  thread stopped**.
- The rate-limited host log records the sector and failure count.
- The boot-time volume-header probe and CD-DA sample reads share the guard.

## Fixed

- **Screenshot and thumbnail writes.** A failed write, such as a redirected or
  unavailable Pictures folder, is reported as an OSD warning instead of crashing the UI
  thread.
- **`disc tracks` sizes.** `dataSize` is a 64-bit value, so physical discs and
  multi-gigabyte images no longer overflow.
- **Mixed-mode CD-DA end limit.** Playback uses the disc track table rather than the
  volume-header data count.
- **The 1.15.0 optical-drive discovery, JIT soft-reset, and single-pad input fixes are
  retained.**

## Verification

- 2,576/2,576 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, and `jb inspectcode` at warning severity.
- CI covers Windows, Linux, and macOS, the NativeAOT apphosts, trimmed publishes, Linux
  packages, and all five bundles.
- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.16.0>.
- `CdromReadErrorTests` covers the failed-sector, continued-transfer, and boot-probe
  cases; `PhysicalDiscTocTests` covers Windows TOC parsing, track coverage, and MSF
  conversion.
- Windows x64 bundle: `selftest` 15/15, 600-frame hash `0x110E7DC5`, 3,000-frame hash
  `0xAD4F370C`, and `FRONTEND SMOKE OK 1.16.0`.
- Linux x64 tarball: `selftest`, frontend smoke on hardware OpenGL 4.2, and matching
  600/3,000-frame hashes.

## Still in 1.15.0

- Steam Deck/SteamOS first-run setup and the Arcade CRT filter.
- Windows physical optical-drive discovery, including `\\.\CdRomN` for drives without a
  letter.
- The JIT soft-reset and single-pad PBUS input fixes.
- `--high-res` remains a presentation-only 2x upscale; the experimental `--true-2x`
  path is available for every title.
- Gamegun titles keep a joypad on port 1. **P** pauses, **Space** toggles fast-forward,
  **F1** connects the Gamegun, and **Alt+Enter** toggles fullscreen.
- macOS arm64 presents through the native Metal surface, and Linux artifacts remain
  GPG-signed with keyring fingerprint
  `E69FF266D1FB01D3411578775C4C1E8EAFD44075`.

## Known issues

- **Way of the Warrior from a burned CD-R** still crashes during an FMV on the issue
  reporter's machine. A host read failure at that point now becomes a guest read error
  instead of stopping the core; if the crash persists, the exact error text and log are
  needed.
- **The opt-in Opera DIPIR path** can still block a physical boot. The default path
  boots burned CD-R games; the Opera soft-reset path is reported to boot on its own.
- Physical CD-DA awaits a real mixed-mode hardware pass.
- `--opera-soft-reset` remains opt-in and experimental.
- The EmuDeck/RetroDeck 3DO entry and Flathub listing remain upstream/hosted-repository
  work.
- Arcade CRT is heavier than `Crt` at 4K; the software fallback omits bloom and phosphor
  fringing.
