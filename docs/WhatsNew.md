# What's new in 5DO 1.14.2

**Released:** 2026-11-21

5DO 1.14.2 is a packaging and diagnostics bug-fix release. The Windows bundles now
include the MSVC runtime OpenAL needs, AppImage update metadata points at the next
release correctly, and expected user conditions no longer create bug reports. It is a
drop-in update for 1.14.1; no emulation or save-state format changed.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.2-win-x64.zip` |
| Windows on ARM | `5do-1.14.2-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.2-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.14.2_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.14.2-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.14.2-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.14.2-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.14.2-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-*.txt` list hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Fixed

### Windows bundles are self-contained again

- `vcruntime140.dll` and `msvcp140.dll` (plus the x64-only `vcruntime140_1.dll`) now
  ship beside `OpenAL32.dll`. A clean Windows image no longer needs the Visual C++
  redistributable for audio.
- `package-bundle.ps1` finds the target-architecture DLLs through Visual Studio, a
  matching system folder, or `-VcRuntimeDirectory`, and rejects missing or
  wrong-architecture files. `build-bundle.yml` verifies the extracted bundle.

### AppImage updates resolve the next release

- The embedded update information now uses `gh-releases-zsync`, and the `.zsync` target
  points at the versioned AppImage instead of the `.zsync` file.
- `build-appimage.sh` validates the embedded update information and the `.zsync` URL
  during the build. A local zsync pass fetched only the changed blocks and matched the
  target hash.
- **1.14.1 users must download 1.14.2 manually.** The 1.14.1 AppImages predate this
  fix; automatic updates work from 1.14.2 onward.

### Cleaner diagnostics and bug reports

- A cover catalog HTTP failure (for example HTTP 500) is retried and then falls back to
  the cached catalog or local covers instead of surfacing as an application error.
- Loading a save state written with a different BIOS, disc, machine option, or clock,
  opting into experimental true 2x CEL mode, and running a partially extracted Windows
  bundle now log their warning without filing a bug report.
- The missing-native startup message names the application folder.

## Verification

- 2,535/2,535 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, and `jb inspectcode` at warning severity.
- Clean Windows ARM64 and x64 bundle checks load OpenAL without the VC++
  redistributable; `selftest --json` passes 15/15, the 600-frame Theme Park hash is
  `0x110E7DC5` with PC `22892`, the desktop smoke reports
  `FRONTEND SMOKE OK 1.14.2`, and a 60-frame OpenAL null-driver run produces 44,144
  samples.
- The x64 AppImage runs through FUSE and `--appimage-extract-and-run`, passes the
  desktop smoke on hardware OpenGL, and its detached `.asc` verifies with the release
  key. `appimageupdatetool` resolved the latest 1.14.2 `.zsync`, and a local
  1.14.2-to-test-1.14.3 pass matched the target hash.
- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.14.2>; the five
  bundles match `SHA256SUMS.txt`.

## Still in 1.14.1

- `--high-res` remains a presentation-only 2x upscale; the experimental `--true-2x`
  path is available for every title.
- Gamegun titles keep a joypad on port 1. **P** pauses, **Space** toggles fast-forward,
  **F1** connects the Gamegun, and **Alt+Enter** toggles fullscreen.
- macOS arm64 presents through the native Metal surface, and Linux artifacts remain
  GPG-signed with keyring fingerprint
  `E69FF266D1FB01D3411578775C4C1E8EAFD44075`.
