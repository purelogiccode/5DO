# What's new in 5DO 1.15.0

**Released:** 2026-11-21

5DO 1.15.0 adds Steam Deck/SteamOS first-run setup and an Arcade CRT filter, and fixes
physical optical-drive discovery, a JIT soft-reset state loss, and single-pad PBUS input.
Save states and disc formats are unchanged from 1.14.2.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.15.0-win-x64.zip` |
| Windows on ARM | `5do-1.15.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.15.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.15.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.15.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.15.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.15.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.15.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Steam Deck first-run setup

- The welcome guide detects `SteamDeck`, `SteamOS`, `SteamGamepadUI`, or a `gamescope`
  desktop and uses a 960x680 layout with 30 px headings, 18 px body text, and 54 px
  buttons.
- It shows the BIOS folder, a **Scan** action that re-runs discovery in place, and
  **Open Folder**. The BIOS requirement stays visible for keyboard-less sessions.
- [Installation.md](Installation.md#steam-deck-and-steamos) covers Desktop Mode, the
  BIOS folder, adding the AppImage/Flatpak as a non-Steam game, `--fullscreen`, and a
  trackpad controller layout. The upstream EmuDeck/RetroDeck entry remains external.

## Arcade CRT filter

- `ArcadeCrt` is a stronger old-television/arcade-monitor preset than `Crt`: more
  curvature, thick output- and source-row scanlines, an RGB triad mask, phosphor
  fringing, warm 5x5 halation, and a heavier vignette.
- It is available on OpenGL/GLES, Vulkan, and Direct3D 11, with a scanline/mask/vignette
  software fallback. It uses the existing **CRT bloom** control.
- Probe aliases are `arcade-crt` and `old-crt`.

## Fixed

- **Physical optical drives (issue #8).** Windows no longer filters on
  `DriveInfo.IsReady`. A 3DO disc's custom filesystem is not mountable, so the drive can
  have no ready volume or no drive letter. Discovery probes the optical device with
  `IOCTL_STORAGE_CHECK_VERIFY2`, keeps a letter when present, and falls back to
  `\\.\CdRomN`. A reporter booted burned CD-R games and reached their FMV intros,
  closing SYS-21. Physical CD-DA remains unsupported.
- **JIT soft reset.** A CLIO CSTAT soft reset triggered inside a compiled block no
  longer has its reset PC/CPSR overwritten by the interrupted block state.
- **Single-pad input.** Disabled ports 7-8 left at `None` no longer force the Opera
  daisy-chain packet, and automatic port counting ignores `None`, so one active joypad
  uses the legacy 16-byte packet.
- **Welcome dialog.** Centered on screen, with height adjusted for the discovered-BIOS
  panel.
- **Arcade CRT Direct3D parity.** Direct3D scanline/mask clamps match OpenGL and Vulkan.

## Verification

- 2,561/2,561 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, and `jb inspectcode` at warning severity.
- CI covers Windows, Linux, and macOS, the NativeAOT apphosts, trimmed publishes, Linux
  packages, and all five bundles.
- Windows x64 bundle: `selftest`, CLI smoke, and frontend smoke report 1.15.0.
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
