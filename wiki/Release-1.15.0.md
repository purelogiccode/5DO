# 5DO 1.15.0

**Released:** 2026-11-21

5DO 1.15.0 adds Steam Deck/SteamOS first-run setup and the Arcade CRT filter, and fixes
physical optical-drive discovery, a JIT soft-reset state overwrite, and single-pad PBUS
input.

Download from [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.15.0).

## Download

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.15.0-win-x64.zip` |
| Windows on ARM | `5do-1.15.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.15.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint | `5do_1.15.0_amd64.deb` (arm64: `5do_1.15.0_arm64.deb`) |
| Fedora, RHEL, openSUSE | `5do-1.15.0-1.linux.x86_64.rpm` (arm64: `...aarch64.rpm`) |
| Any Linux, no install | `5do-1.15.0-linux-x86_64.AppImage` (arm64: `...aarch64.AppImage`) |
| Any Linux, sandboxed | `5do-1.15.0-linux-x86_64.flatpak` (arm64: `...aarch64.flatpak`) |
| Any Linux, portable | `5do-1.15.0-linux-x64.tar.gz` (arm64: `5do-1.15.0-linux-arm64.tar.gz`) |

## Highlights

- **Steam Deck and SteamOS first-run setup:** the welcome guide detects
  `SteamDeck`/`SteamOS`/`SteamGamepadUI`/gamescope and switches to a 960x680 layout with
  54 px buttons, the BIOS folder, **Scan**, and **Open Folder**.
- **Arcade CRT filter:** `ArcadeCrt` adds thicker scanlines, an RGB triad mask, phosphor
  fringing, warm halation, more curvature, and a heavier vignette on OpenGL/GLES,
  Vulkan, Direct3D 11, and the software fallback.
- **Physical optical drives (issue #8):** Windows probes the optical device with
  `IOCTL_STORAGE_CHECK_VERIFY2` and falls back to `\\.\CdRomN`, so a 3DO disc whose
  custom filesystem cannot be mounted still boots. Burned CD-R games reached their FMV
  intros, closing SYS-21.

## Fixed

- **JIT soft reset** no longer loses its reset PC/CPSR/`R14_svc` across a compiled block.
- **Single-pad PBUS input:** disabled `None` ports no longer force the Opera daisy chain.
- **Welcome dialog placement and height** for the discovered-BIOS panel.
- **Arcade CRT Direct3D parity** with the OpenGL and Vulkan shaders.

## Known limits

- Physical CD-DA playback from a host drive is not implemented; use a BIN/CUE or CHD
  image for audio tracks.
- `--opera-soft-reset` remains opt-in and experimental.
- The EmuDeck/RetroDeck 3DO entry and Flathub listing remain upstream work.
- No BIOS ROMs, game images, or extracted assets are included.
