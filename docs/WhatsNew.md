# What's new in 5DO 1.14.0

**Released:** 2026-11-20

5DO 1.14.0 makes high-resolution presentation safe for every title, fixes the OpenGL
480-line upload, and finishes the Gamegun input defaults. It is a drop-in update for
1.13.2; no save-state format changed.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.0-win-x64.zip` |
| Windows on ARM | `5do-1.14.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.14.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.14.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.14.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.14.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.14.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-*.txt` list hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## What changed

### High-resolution presentation is safe everywhere

- **`--high-res` is now presentation-only.** The guest machine stays at native 320x240:
  CEL coordinates, VRAM, CPU bitmap reads, and SPORT are unchanged. `Vdlp` samples each
  native line into a 640-wide line and emits it twice. For 320-wide modes this is a
  pixel-perfect 2x nearest upscale; wider VDLP modes are resampled instead of cropped.
- **The old corruption and hang are gone.** This removes the left-half 2D CEL crop,
  stale bitmap/video planes, bottom rainbow rows, and the Road Rash gameplay hang caused
  by the old FreeDO-style four-plane layout. Output was verified with 0 differing pixels
  against 2x native upscales on 20th Century Video Almanac, Road Rash, Dragon's Lair,
  and the Alone in the Dark demo.
- **OpenGL 480-line upload fixed.** The per-line parameter texture is `RG32UI` (two
  words per line: background and out-control) instead of `RGBA32UI`, so 480-line frames
  no longer read past the upload array and paint uninitialized background rows.
- **True 2x CEL mode is available for every title (experimental).** `--true-2x`, the
  Settings checkbox, and per-game `TrueHighResolution` no longer fall back to native for
  unvalidated IDs. Jurassic Park Interactive and Family Feud remain the visually
  validated references; other titles log a warning and may misrender bitmap/SPORT or
  framebuffer-read content.

### Gamegun and controls

- **Retail American Laser Games titles keep a joypad on port 1.** Crime Patrol, Drug
  Wars, Mad Dog McCree, Mad Dog II, Space Pirates, The Last Bounty Hunter, Who Shot
  Johnny Rock, and Corpse Killer use `1=joypad,2=lightgun`, so the keyboard and gamepad
  drive menus and non-lightgun sections while the mouse aims the Gamegun. Left click
  fires, right click is the option/start/calibrate button, and middle click reloads.
- **Demolition Man starts with the pad.** It ignores pad Start/A when a Gamegun is
  attached during boot, so its default profile is `1=joypad`. Press **F1** or use
  **Quick Menu > Connect Gamegun** after the game is running to attach the gun for the
  shooting-gallery sections.
- **New default hotkeys.** **P** pauses/resumes; **Escape** keeps its two-step system
  pause/quit behavior. **Space** toggles fast-forward (Tab still holds it), **F1**
  connects/disconnects the Gamegun, and **Alt+Enter** toggles fullscreen in addition to
  **F11**.
- **Database input defaults now reach the desktop shell.** The shell passes the full
  32-character disc MD5; `ResolveInputDevices` now accepts either the full checksum or
  the eight-character 4DO ID, so curated `inputDevices` entries apply in every host.
- **The legacy gun-only override migration runs once.** A gun-only layout you choose on
  purpose is no longer deleted on the next launch.

### Desktop polish

- Clicking the game view restores keyboard focus, so hotkeys and pad bindings keep
  working after using the library or another window.

## Verification

- 2,475/2,475 tests, `dotnet format --verify-no-changes`, `jb inspectcode` at warning
  severity, and `mkdocs build --strict`.
- A 300-frame Road Rash run at `--high-res` produced a 640x480 frame with 0 differing
  pixels from a 2x nearest upscale of the native 320x240 frame.
- The 1.13.x ARM64 and GPU evidence still applies; this release does not change the
  emulation core's default 320x240 path.

## Still in 1.13.x

- Native macOS Metal presenter (`CAMetalLayer`) at 60 fps, all eight filters, and the
  drag-to-Applications DMG.
- Signed Linux release chain: keyring, signed `SHA256SUMS-linux.txt`, and a detached
  `.asc` for every Linux artifact.
- Self-contained single-file apphosts for Windows, Linux, and macOS on both
  architectures; no separate .NET runtime install.
- Movie verification records the full emulated frame span, and partial extractions
  explain which files are missing.

## Known limits

- The legacy `--high-res` path is an output enlargement, not true internal 640x480 CEL
  rendering; vertical detail remains 240 lines. Use `--true-2x` for the experimental
  guest-visible path.
- Bitmap/SPORT sampling and framebuffer-read semantics in true 2x mode remain research
  work; test each title individually.
- macOS Retina 2x presentation is still unvalidated (no HiDPI host).
- Intel macOS is unsupported.
