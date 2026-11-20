# 5DO 1.14.0

**Released:** 2026-11-20

5DO 1.14.0 is an input and high-resolution fix release. The legacy `--high-res` option
is now a safe presentation-only 2x upscale, the OpenGL 480-line upload is corrected, and
the retail American Laser Games Gamegun titles keep a joypad alongside the gun. No save
state format changed, so 1.13.x states continue to load.

## Which file do I need?

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.0-win-x64.zip` |
| Windows on ARM | `5do-1.14.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.14.0_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.14.0_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.14.0-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.14.0-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.14.0-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.14.0-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.14.0-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.14.0-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.14.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

**Extract the archive before running it.** The apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. Running
`FiveDo.exe` from inside the ZIP, or copying only the executable, cannot start the
desktop app.

**Ignore the rest unless you are verifying a download:** the `.asc` files are detached
signatures, the `.zsync` files feed `AppImageUpdate`, and `SHA256SUMS-linux.txt` /
`SHA256SUMS.txt` list hashes. `5do-archive-keyring.gpg` is the public key those
signatures need.

## Fixed

- **`--high-res` no longer changes guest-visible emulation.** The guest machine stays at
  native 320x240: CEL coordinates, VRAM, CPU bitmap reads, and SPORT are unchanged.
  `Vdlp` samples each native line into a 640-wide line and emits it twice, which is an
  exact 2x nearest upscale for 320-wide modes and a resample rather than a crop for wider
  VDLP modes. This removes the left-half 2D CEL crop, stale bitmap/video planes, bottom
  rainbow rows, and the Road Rash gameplay hang caused by the old four-plane layout.
  Verified with 0 differing pixels against 2x native upscales on 20th Century Video
  Almanac, Road Rash, Dragon's Lair, and the Alone in the Dark demo.
- **OpenGL 480-line upload corrected.** The per-line parameter texture is allocated and
  uploaded as `RG32UI`/`RGInteger` (background and out-control, two words per line)
  instead of `RGBA32UI`. Native 240-line uploads previously stayed inside the backing
  array, but 480-line frames read past it and produced garbage background rows.
- **Database input-device defaults reach the desktop shell.** The shell passes the full
  32-character disc MD5 while `ResolveInputDevices` looked up only the eight-character
  4DO ID. Both forms now resolve, so curated `inputDevices` entries drive the frontend.
- **The legacy gun-only override migration runs once.** It no longer deletes an explicit
  gun-only layout on every settings load.
- **Clicking the game view restores keyboard focus**, so hotkeys and pad bindings keep
  working after using the library or another window.

## Changed

- **Retail Gamegun titles keep a joypad on port 1.** Crime Patrol, Drug Wars, Mad Dog
  McCree, Mad Dog II, Space Pirates, The Last Bounty Hunter, Who Shot Johnny Rock, and
  Corpse Killer use `1=joypad,2=lightgun`. The keyboard and gamepad drive menus and
  non-lightgun sections; the host mouse aims the Gamegun. Left click fires, right click
  is the option/start/calibrate button, and middle click reloads.
- **Demolition Man starts with the pad.** It ignores pad Start/A while a Gamegun is
  attached during boot, so its default is `1=joypad`. Press **F1** or use **Quick Menu >
  Connect Gamegun** after the game is running.
- **New default hotkeys.** **P** pauses/resumes; **Escape** keeps the two-step system
  pause/quit shortcut. **Space** toggles fast-forward, **Tab** still holds it, **F1**
  connects/disconnects the Gamegun, and **Alt+Enter** toggles fullscreen in addition to
  **F11**.
- **True 2x CEL mode is available for every title (experimental).** The CLI `--true-2x`,
  the Settings checkbox, and per-game `TrueHighResolution` no longer fall back to native
  for unvalidated IDs. Jurassic Park Interactive and Family Feud remain the validated
  references; other titles may misrender bitmap/SPORT or framebuffer-read content.

## Verification

- 2,475/2,475 tests, `dotnet format --verify-no-changes`, `jb inspectcode` at warning
  severity, and `mkdocs build --strict`.
- A 300-frame Road Rash run at `--high-res` produced a 640x480 frame with 0 differing
  pixels from a 2x nearest upscale of the native 320x240 frame.
- A 300-frame Road Rash `--true-2x` run completes and produces a 640x480 frame.
- CI builds the five bundles and the `.deb`/`.rpm`/AppImage/Flatpak sets, installs the
  `.deb`/`.rpm`, runs the sandboxed Flatpak `selftest`, and verifies the signed APT/RPM
  mirror trees.

## Known limits

- The legacy `--high-res` path is an output enlargement, not true internal 640x480 CEL
  rendering; vertical detail remains 240 lines. Use `--true-2x` for the experimental
  guest-visible path.
- Bitmap/SPORT sampling and framebuffer-read semantics in true 2x mode remain research
  work; test each title individually.
- macOS Retina 2x presentation is still unvalidated (no HiDPI host).
- The bundled OpenAL Soft runtime needs the Microsoft Visual C++ 2015-2022 ARM64
  redistributable on a clean Windows image.
- A Windows ARM64 machine with no Vulkan driver and no audio endpoint runs the software
  paths only; that is a host limitation, not an emulator defect.
- No hosted package repository or Flathub listing yet.
- Intel macOS is unsupported.
