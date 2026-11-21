# 5DO 1.15.0

**Released:** 2026-11-21

5DO 1.15.0 is a feature release for Steam Deck/SteamOS setup and CRT presentation,
with physical optical-drive, JIT, and input fixes. Save states and disc formats are
unchanged from 1.14.2.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.15.0-win-x64.zip` |
| Windows on ARM | `5do-1.15.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.15.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.15.0_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.15.0_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.15.0-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.15.0-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.15.0-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.15.0-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.15.0-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.15.0-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.15.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Highlights

### Steam Deck and SteamOS first-run setup

The welcome guide now detects `SteamDeck`, `SteamOS`, `SteamGamepadUI`, or a `gamescope`
desktop and switches to a 960x680 layout with a 30 px heading, 18 px body text, and
54 px buttons. It shows the BIOS folder and adds **Scan** (re-runs discovery without
closing the guide) and **Open Folder**. The BIOS requirement remains visible for
keyboard-less sessions.

[Installation.md](Installation.md#steam-deck-and-steamos) documents Desktop Mode, the
BIOS folder, adding the AppImage/Flatpak as a non-Steam game, `--fullscreen`, and a
trackpad controller layout. The upstream EmuDeck/RetroDeck 3DO entry is still an
external pull request.

### Arcade CRT filter

A new `ArcadeCrt` preset is a stronger old-television/arcade-monitor look than the
existing `Crt` filter:

- More barrel curvature.
- Thick output- and source-row scanlines.
- A stronger RGB triad mask.
- Slight horizontal phosphor fringing.
- Warm 5x5 halation around bright areas.
- A heavier vignette.

It is available on OpenGL/GLES, Vulkan, and Direct3D 11, and the software presenter
applies a scanline/mask/vignette fallback after bilinear scaling. It uses the existing
**CRT bloom** control. Probe aliases `arcade-crt` and `old-crt` are accepted.

### Physical optical drives (issue #8)

Windows discovery no longer relies on `DriveInfo.IsReady`. A 3DO disc's custom
filesystem is not mountable, so the drive can have no ready volume or no drive letter even
though the media is present. **File > Boot Optical Drive...** now probes the optical
device with `IOCTL_STORAGE_CHECK_VERIFY2` (with a CD-ROM geometry fallback), keeps a
drive letter when one exists, and enumerates `\\.\CdRomN` for letterless drives. The
picker labels those entries **no drive letter**, and the CLI, startup path, and `doctor`
accept the same form.

A reporter booted burned CD-R games on a Pioneer BD-RW and reached their FMV intro
sequences, closing the SYS-21 hardware acceptance line. Physical CD-DA tracks remain
unsupported; use a BIN/CUE or CHD image for audio.

## Fixed

- **JIT soft reset.** A guest store that triggered the CLIO CSTAT soft-reset path could
  have the JIT block flush its interrupted PC/CPSR over the reset, leaving the CPU at an
  invalid address. The reset PC/CPSR and `R14_svc` now survive the block boundary.
- **One active joypad with disabled `None` ports** no longer forces the Opera
  daisy-chain PBUS packet. The legacy 16-byte packet is used unless an active peripheral
  or active ports 7-8 require the chain, restoring keyboard and gamepad input.
- **Automatic port counting ignores `None` slots**, so a trailing disabled port does not
  grow the polled chain.
- **Welcome dialog placement and height.** The guide is centered on the screen and grows
  when a discovered BIOS adds the extra panel, so it cannot be placed off-screen or clip
  the BIOS folder on a smaller desktop.
- **Arcade CRT Direct3D parity.** The Direct3D 11 scanline/mask clamps now match the
  OpenGL and Vulkan shaders.

## Verification

- 2,561/2,561 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, and `jb inspectcode` at warning severity.
- CI runs on Windows, Linux, and macOS, including the NativeAOT apphosts, trimmed
  publishes, Linux packages, and all five bundles.
- Windows x64 bundle: `selftest`, CLI smoke, and frontend `--smoke-test` report 1.15.0.
- Linux x64 AppImage: FUSE and `--appimage-extract-and-run`, frontend smoke, detached
  `.asc` verification, and `AppImageUpdate` resolution of the 1.15.0 release.
- Physical CD-R boot: issue #8 reporter, Windows 11 x64 + Pioneer BD-RW BDR-S13JX.

## Still in 1.14.2

- Windows bundles remain self-contained: the app-local MSVC runtime loads OpenAL on a
  clean image, and AppImage update metadata uses `gh-releases-zsync`.
- `--high-res` remains a presentation-only 2x upscale; the experimental `--true-2x`
  path is available for every title.
- Gamegun titles keep a joypad on port 1. **P** pauses, **Space** toggles fast-forward,
  **F1** connects the Gamegun, and **Alt+Enter** toggles fullscreen.
- macOS arm64 presents through the native Metal surface, and Linux artifacts remain
  GPG-signed with keyring fingerprint
  `E69FF266D1FB01D3411578775C4C1E8EAFD44075`.

## Known issues

- `--opera-soft-reset` remains opt-in and experimental; a full CSTAT/DIPIR boot
  transcript is still needed for accuracy sign-off.
- Physical CD-DA playback from a host drive is not implemented.
- The EmuDeck/RetroDeck 3DO entry and Flathub listing remain upstream/hosted-repository
  work.
- Arcade CRT is heavier than `Crt` at 4K; the software fallback omits bloom and phosphor
  fringing.
