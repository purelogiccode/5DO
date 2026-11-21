# 5DO 1.16.0

**Released:** 2026-11-21

5DO 1.16.0 adds CD-DA playback from a host optical drive and hardens the disc and
screenshot paths that were reported in issue #8. Save states and disc formats are
unchanged from 1.15.0.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.16.0-win-x64.zip` |
| Windows on ARM | `5do-1.16.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.16.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.16.0_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.16.0_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.16.0-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.16.0-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.16.0-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.16.0-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.16.0-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.16.0-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.16.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Highlights

### Physical CD-DA

A host optical drive's table of contents is now exposed through `ITrackedDiscSource`.
The drive TOC is read with `IOCTL_CDROM_READ_TOC` on Windows and
`CDROMREADTOCHDR`/`CDROMREADTOCENTRY` on Linux; raw 2352-byte Red Book frames use
`IOCTL_CDROM_RAW_READ` in CDDA mode on Windows and `CDROMREADAUDIO` on Linux. The 3DO
CD player and games can list and play audio tracks on a burned mixed-mode CD, and
`disc tracks`/`disc verify` include the audio tracks.

CD-DA end addressing follows the physical track table rather than the 3DO volume
header's data-volume limit, so audio tracks that follow the data track are playable.
A drive TOC that reports a short lead-out is extended to the drive's addressable data
length.

Physical CD-DA is implemented but has not yet been validated on real mixed-mode media
on the development host, which has no optical drive. The Windows TOC and cooked read
paths were checked against a mounted virtual optical drive; a burned audio CD remains
the hardware acceptance step.

### Disc read errors no longer stop emulation

Issue #8's CRC report (`DiscImageException` at sector 103777) propagated out of the
CD-ROM device and faulted the core thread with **Emulation fault** and **Core thread
stopped**. A failed host read is now zero-filled, recorded as MEI receive-ECC for the
guest's `READ_ERROR` command, and logged (rate-limited per sector); the transfer
continues with the next sector. The boot-time volume-header probe and CD-DA sample
reads use the same guard, so a scratched or failing disc cannot stop the core.

### Screenshot and thumbnail writes are safe

A `FileNotFoundException` from the Pictures path, such as a redirected or unavailable
cloud folder, is caught and reported as an OSD warning instead of faulting the UI
thread. Native screenshots and save-slot thumbnails share the guard.

## Fixed

- **`disc tracks` reports 64-bit sizes.** The `dataSize` field is a 64-bit value, so
  physical discs and multi-gigabyte images no longer print an overflowed byte count.
- **The mixed-mode CD-DA end limit** uses the disc's track table, not the volume-header
  data count, so a disc whose header declares only the data volume still plays its
  audio tracks.
- **The issue #8 optical-drive discovery and input fixes from 1.15.0 are retained.**

## Verification

- 2,576/2,576 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, and `jb inspectcode` at warning severity.
- Windows virtual optical drive: the TOC parse reported the mounted image's data track,
  and `info`/`disc dump` read the cooked sectors.
- The read-error guard is covered by `CdromReadErrorTests` (failed sector, continued
  transfer, boot-probe fallback), and the TOC/coverage logic by
  `PhysicalDiscTocTests`.
- CI runs on Windows, Linux, and macOS, including the NativeAOT apphosts, trimmed
  publishes, Linux packages, and all five bundles. Master CI
  <https://github.com/purelogiccode/5DO/actions/runs/44917916785>, Docs
  <https://github.com/purelogiccode/5DO/actions/runs/44917916755>, and Platform runtime
  validation <https://github.com/purelogiccode/5DO/actions/runs/44917916784> passed.
- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.16.0>; release run
  <https://github.com/purelogiccode/5DO/actions/runs/44917920441> passed all five bundle
  jobs, both AppImage/Flatpak package jobs, signing, and the publish job.
- The five downloaded bundles match `SHA256SUMS.txt`.
- Windows x64 bundle: `5DO 1.16.0`, `selftest --json` reports `ok: true` for all 15
  checks, and the desktop smoke writes `FRONTEND SMOKE OK 1.16.0`
  (`locale=en`, `firstMenuHeader=File`, `windowState=Normal`).
- Linux x64 bundle: the release workflow's CLI and frontend smoke passed, and the
  `.deb`/`.rpm` installs were smoke-tested in CI. The Linux x64 tarball and both
  AppImages are GPG-signed with the release key.
- AppImage `.zsync`/embedded update metadata was built and signed by CI; a local
  `appimageupdatetool` pass was not run this cycle.

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
  reporter's machine. A host read failure at that point is now reported to the guest
  instead of stopping the core; if the crash persists, the exact error text and 5DO log
  are needed to separate a disc read from a title-specific decoder fault.
- **The opt-in Opera DIPIR path** can still block a physical boot. The default path
  boots burned CD-R games; `--opera-dipir` needs a CSTAT/DIPIR boot transcript before it
  can be recommended. The Opera soft-reset path is reported to boot when used on its own.
- Physical CD-DA playback awaits a real mixed-mode audio CD hardware pass; image-based
  CD-DA remains covered by the CUE/CHD fixtures.
- `--opera-soft-reset` remains opt-in and experimental.
- The EmuDeck/RetroDeck 3DO entry and Flathub listing remain upstream/hosted-repository
  work.
