# 5DO 1.13.0

**Released:** 2026-11-18

5DO 1.13.0 adds the **native Metal presenter** on macOS, four Phoenix/4DO presentation
options, and a **signed Linux release chain**. macOS arm64 now presents frames through a
`CAMetalLayer` surface instead of reading every frame back, the status bar reports
emulation health, and the Linux artifacts ship with GPG signatures and a published
keyring.

## Which file do I need?

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.13.0-win-x64.zip` |
| Windows on ARM | `5do-1.13.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.13.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.13.0_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.13.0_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.13.0-1.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.13.0-1.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.13.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

The `.asc` files are detached signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-linux.txt` / `SHA256SUMS.txt` list hashes; you only need those to verify a
download. `5do-archive-keyring.gpg` is the public key they use.

> The next release renames the RPM, AppImage, and Flatpak files to include the version
> and `linux`, so this table becomes self-explanatory on the page itself.

## Highlights

### macOS arm64 presents through Metal

The shell now owns an `NSView` + `CAMetalLayer` and presents the Vulkan output through
`VK_EXT_metal_surface`, so no per-frame CPU readback remains:

- **0 bytes/frame** allocated by the renderer at 1080p FIFO, 60.1 fps.
- **Live resize and fullscreen** keep a 4:3 viewport: 1060x466 window, 900x620 and
  1250x706 resizes, and 1440x900 fullscreen with 960x720 and 240 px pillarboxes.
- **320x240 capture parity** with the CPU oracle (`0` different pixels) and a 320x240
  native screenshot while the window is much larger.
- **2,429/2,429 tests** on the NoMac host; the suite is 2,439 in this release.
- **Retina 2x is not validated yet:** the rented host advertises only 1:1 display modes,
  so `backingScaleFactor` stays 1.0. `PLAT-17` keeps that one row open.

### Presentation options from Phoenix and 4DO

Settings > Video and > Audio gain four presentation-only options; none of them change
emulation, input, frame hashes, audio hashes, or save states:

- **Swap left and right audio channels** (also `--audio-swap`), applied to live output
  only so WAV captures and the audio hash keep the emulated channel order.
- **Named void patterns** — 4DO, Bumps, and Metal — next to solid, checkerboard, grid,
  and scanline fills.
- **Void color on the GPU presenters** — the letterbox and pillarbox now use the
  configured `#RRGGBB`/`#AARRGGBB` color instead of black on OpenGL, GLES, Vulkan, and
  Direct3D 11. Patterned fills stay shell-composited.
- **Emulation-health badge** — Idle/OK/Watch/Issue from the counters the FPS overlay
  already collects, with a tooltip that explains the verdict.

### Signed Linux artifacts

- **Release key:** ed25519 `Pure Logic Code (5DO releases)`, fingerprint
  `E69FF266D1FB01D3411578775C4C1E8EAFD44075`, expires 2029-11-18.
- **Per-artifact signatures:** `.deb`, `.rpm`, AppImage, Flatpak, and the portable
  tarballs get a detached `.asc`, plus a signed `SHA256SUMS-linux.txt` and a published
  `5do-archive-keyring.gpg`.
- **Continuous signing:** CI signs every push now, so the signing chain cannot rot
  between releases; `release.yml` publishes it with the tagged release.
- **Software-center metadata:** GNOME Software lists 5DO with name, developer, summary,
  description, screenshots, and an Open button; `appstreamcli validate --no-net` passes.

### Linux packaging fixes

- **Flatpak:** `finish-args` use `/media` and `/run/media` (the metadata parser rejects
  the `removable-media` nickname), and the build exports the `stable` branch so
  `build-bundle` finds the ref.
- **arm64 AppImage:** the release job retries the `appimagetool` download, which
  intermittently 504s on the ARM runner.
- **Mirror verification:** the publish job installs `dpkg-dev`, `apt-utils`,
  `createrepo-c`, and `rpm` before it verifies the signed APT/RPM trees.

## Verification

| Gate | Result |
|---|---|
| Test suite | 2,439/2,439 |
| CI (Windows, Ubuntu, macOS x64/arm64) | all jobs green |
| macOS presentation matrix | windowed, resize, fullscreen, 4K offscreen, 320x240 capture |
| Linux packages | `.deb`/`.rpm` install, Flatpak install + sandboxed `selftest`, AppImage, signed mirror trees |
| Docs | `mkdocs build --strict` |

## Known limits

- **macOS Retina 2x** remains unvalidated until a HiDPI host is available;
  `PLAT-17` keeps only that row open.
- **Linux arm64 AppImage** is built and smoke-tested in CI but has not been run on
  arm64 Linux hardware.
- **No hosted repository:** 5DO publishes GitHub release assets and optional signed
  mirror tooling; it does not run an `apt`/`dnf` server or a Flathub listing yet.
- **Signing key custody:** the release key is a single sign key stored only as a
  repository secret; rotation publishes a new key and keeps the old keyring available.
- **Intel macOS** remains unsupported.
- No BIOS ROMs, game images, or extracted assets are included.
