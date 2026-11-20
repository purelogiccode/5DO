# 5DO 1.14.0

**Released:** 2026-11-20

5DO 1.14.0 is an input and high-resolution fix release. The legacy `--high-res` option
is now a presentation-only 2x upscale that is safe for every title, the OpenGL 480-line
upload is corrected, and the retail American Laser Games Gamegun titles keep a joypad
alongside the gun. No save-state format changed.

Download from [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.14.0).

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.0-win-x64.zip` |
| Windows on ARM | `5do-1.14.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint | `5do_1.14.0_amd64.deb` (arm64: `5do_1.14.0_arm64.deb`) |
| Fedora, RHEL, openSUSE | `5do-1.14.0-1.linux.x86_64.rpm` (arm64: `...aarch64.rpm`) |
| Any Linux, no install | `5do-1.14.0-linux-x86_64.AppImage` (arm64: `...aarch64.AppImage`) |
| Any Linux, sandboxed | `5do-1.14.0-linux-x86_64.flatpak` (arm64: `...aarch64.flatpak`) |
| Any Linux, portable | `5do-1.14.0-linux-x64.tar.gz` (arm64: `5do-1.14.0-linux-arm64.tar.gz`) |

## What changed

- **`--high-res` is presentation-only.** The guest stays at native 320x240; `Vdlp`
  samples each native line into a 640-wide line and emits it twice. For 320-wide modes
  this is an exact 2x nearest upscale. The left-half CEL crop, stale bitmap/video planes,
  bottom rainbow rows, and the Road Rash gameplay hang are gone. A 300-frame Road Rash
  run matches a 2x nearest upscale of the native frame with 0 differing pixels.
- **OpenGL 480-line upload fixed.** The per-line parameter texture is `RG32UI`
  (background and out-control, two words per line), so 480-line frames no longer read
  past the upload array.
- **Gamegun defaults completed.** Crime Patrol, Drug Wars, Mad Dog McCree, Mad Dog II,
  Space Pirates, The Last Bounty Hunter, Who Shot Johnny Rock, and Corpse Killer use
  `1=joypad,2=lightgun`; the pad drives menus and non-lightgun sections while the mouse
  aims the gun. Demolition Man starts as `1=joypad` and connects the gun with **F1** or
  **Quick Menu > Connect Gamegun** after boot.
- **Database input defaults reach the desktop shell.** The full 32-character disc MD5
  and the eight-character 4DO ID both resolve curated `inputDevices` entries.
- **The legacy gun-only override migration runs once**, so an explicit gun-only layout
  is not deleted on the next launch.
- **New defaults:** **P** pauses/resumes, **Space** toggles fast-forward, **F1**
  connects/disconnects the Gamegun, and **Alt+Enter** toggles fullscreen in addition to
  **F11**. **Escape** keeps the two-step system pause/quit behavior.
- **True 2x CEL mode is available for every title (experimental).** Jurassic Park
  Interactive and Family Feud remain the visually validated references; unvalidated
  titles log a warning and may misrender bitmap/SPORT or framebuffer-read content.
- Clicking the game view restores keyboard focus.

## Verification

- 2,475/2,475 tests, `dotnet format`, `jb inspectcode`, and `mkdocs build --strict`.
- Road Rash 300-frame native, `--high-res`, and `--true-2x` boots complete; the
  `--high-res` frame is a 0-pixel-difference 2x nearest upscale.
- CI builds the five bundles and the Linux package sets, installs the `.deb`/`.rpm`,
  runs the sandboxed Flatpak `selftest`, and verifies the signed APT/RPM mirrors.

## Known limits

- `--high-res` remains output enlargement, not true internal 640x480 CEL rendering.
- True 2x bitmap/SPORT sampling and framebuffer-read semantics remain research work.
- macOS Retina 2x presentation is still unvalidated; Intel macOS is unsupported.
- The bundled OpenAL Soft runtime needs the Microsoft Visual C++ 2015-2022 ARM64
  redistributable on a clean Windows image.
