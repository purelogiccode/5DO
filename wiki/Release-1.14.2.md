# 5DO 1.14.2

**Released:** 2026-11-21

5DO 1.14.2 is a packaging and diagnostics bug-fix release. Windows bundles now include
the MSVC runtime OpenAL Soft needs, AppImage update metadata points at the next release
correctly, and expected user conditions no longer create bug reports. It is a drop-in
update for 1.14.1; no emulation or save-state format changed.

Download from [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.14.2).

## Download

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.2-win-x64.zip` |
| Windows on ARM | `5do-1.14.2-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.2-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint | `5do_1.14.2_amd64.deb` (arm64: `5do_1.14.2_arm64.deb`) |
| Fedora, RHEL, openSUSE | `5do-1.14.2-1.linux.x86_64.rpm` (arm64: `...aarch64.rpm`) |
| Any Linux, no install | `5do-1.14.2-linux-x86_64.AppImage` (arm64: `...aarch64.AppImage`) |
| Any Linux, sandboxed | `5do-1.14.2-linux-x86_64.flatpak` (arm64: `...aarch64.flatpak`) |
| Any Linux, portable | `5do-1.14.2-linux-x64.tar.gz` (arm64: `5do-1.14.2-linux-arm64.tar.gz`) |

## Fixed

- **Windows bundles are self-contained for audio.** `vcruntime140.dll` and
  `msvcp140.dll` (plus the x64-only `vcruntime140_1.dll`) ship beside `OpenAL32.dll`,
  so a clean Windows image no longer needs the Visual C++ redistributable.
- **AppImage updates resolve the next release.** The embedded update information uses
  `gh-releases-zsync`, the `.zsync` target points at the versioned AppImage, and the
  build validates both. **1.14.1 users must download 1.14.2 manually** because the
  1.14.1 assets predate this fix.
- **Cover catalog HTTP failures fall back cleanly.** A transient catalog/server error is
  retried and then uses the cached catalog or local covers instead of surfacing as an
  application error.
- **Expected conditions no longer file bug reports.** Save-state BIOS/disc/option/clock
  mismatches, experimental true 2x CEL mode, and partial Windows extractions log their
  warning without creating a bug report.

## Verification

- 2,535/2,535 tests, build 0 warnings/0 errors, `dotnet format`, and `jb inspectcode`.
- Clean Windows ARM64 and x64 bundles load OpenAL without the VC++ redistributable;
  `selftest --json` passes 15/15, the 600-frame Theme Park hash is `0x110E7DC5` with PC
  `22892`, the desktop smoke reports `FRONTEND SMOKE OK 1.14.2`, and a 60-frame OpenAL
  null-driver run produces 44,144 samples.
- The x64 AppImage runs through FUSE and `--appimage-extract-and-run`, passes the
  desktop smoke on hardware OpenGL, and its detached `.asc` verifies with the release
  key.

## Known limits

- The published 1.14.1 AppImages cannot auto-update; download 1.14.2 manually.
- The 1.14.1 and older Windows bundles still require `vc_redist`; 1.14.2 is the first
  release with the app-local runtime.
- `--high-res` remains an output enlargement; `--true-2x` is experimental and per-title.
- macOS Retina 2x remains unvalidated, and Intel macOS is unsupported.
- No BIOS ROMs, game images, or extracted assets are included.
