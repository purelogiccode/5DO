# 5DO 1.14.2

**Released:** 2026-11-21

5DO 1.14.2 is a packaging and diagnostics bug-fix release. It makes the Windows
bundles self-contained for OpenAL again, fixes AppImage update metadata so the next
release can be found, and stops expected user conditions from being filed as bug
reports. It is a drop-in update for 1.14.1; no emulation or save-state format changed.

## Which file do I need?

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.14.2-win-x64.zip` |
| Windows on ARM | `5do-1.14.2-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.14.2-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.14.2_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.14.2_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.14.2-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.14.2-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.14.2-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.14.2-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.14.2-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.14.2-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.14.2-linux-x64.tar.gz` (arm64: `linux-arm64`) |

**Extract the archive before running it.** The apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. Running
`FiveDo.exe` from inside the ZIP, or copying only the executable, cannot start the
desktop app.

**Ignore the rest unless you are verifying a download:** the `.asc` files are detached
signatures, the `.zsync` files feed `AppImageUpdate`, and `SHA256SUMS-linux.txt` /
`SHA256SUMS.txt` list hashes. `5do-archive-keyring.gpg` is the public key those
signatures need.

## Fixed

- **Windows bundles include the MSVC runtime OpenAL Soft needs.** `vcruntime140.dll`
  and `msvcp140.dll` (plus the x64-only `vcruntime140_1.dll`) now sit beside
  `OpenAL32.dll`. A clean Windows image no longer needs the Visual C++ redistributable
  for audio. `package-bundle.ps1` resolves the target-architecture files through
  Visual Studio, a matching system folder, or `-VcRuntimeDirectory`, and rejects a
  missing or wrong-architecture DLL.
- **AppImage update metadata resolves the next release.** The embedded update
  information now uses `gh-releases-zsync`, and the `.zsync` target points at the
  versioned AppImage instead of the `.zsync` file. `build-appimage.sh` validates both
  during the build. A local zsync update pass fetched only the changed blocks and
  matched the target hash.
- **Cover catalog HTTP failures fall back cleanly.** A transient catalog/server error
  (for example HTTP 500) no longer escapes as "Cover resolution failed unexpectedly";
  the service retries, then keeps the cached catalog or local covers.
- **Expected conditions no longer file bug reports.** Loading a save state written
  with a different BIOS, disc, machine option, or clock, opting into the experimental
  true 2x CEL mode, and running a partially extracted Windows bundle still log and
  display their warning, but are marked `WithoutBugReport()`. The missing-native
  message also names the application folder.

## Changed

- **Detached Linux signatures remain the release signature format.** The AppImage is
  covered by `5do-<version>-linux-<arch>.AppImage.asc` and the signed
  `SHA256SUMS-linux.txt`; CI does not embed an AppImage signature, so
  `--appimage-signature` is not used. Replacement and relinking notices are unchanged.

## Verification

- 2,535/2,535 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, and `jb inspectcode` at warning severity.
- The Windows ARM64 bundle loads OpenAL on a clean image with the VC++ redistributable
  removed; the x64 bundle does the same under x64 emulation. `selftest --json` passes
  15/15, the 600-frame Theme Park hash is `0x110E7DC5` with PC `22892`, the desktop
  smoke reports `FRONTEND SMOKE OK 1.14.2`, and a 60-frame OpenAL null-driver run
  produces 44,144 samples.
- The x64 AppImage runs through FUSE and `--appimage-extract-and-run`, passes the
  desktop smoke on hardware OpenGL, and its detached `.asc` verifies with the release
  key. A local zsync pass from the rebuilt 1.14.1 image to a test 1.14.2 image fetched
  27 KB and matched the target hash.
- CI publishes the five bundles, both Flatpaks, both AppImages with `.zsync`, the
  signed Linux packages, the keyring, and the checksum manifests.

## Known limits

- **1.14.1 users must download 1.14.2 manually.** The published 1.14.1 AppImages embed
  a version-pinned update URL that stops resolving once 1.14.1 is no longer the latest
  release, and their `.zsync` targets the `.zsync` file. The corrected metadata starts
  with 1.14.2, so `AppImageUpdate` works from 1.14.2 onward.
- The 1.14.1 and older Windows bundles still require `vc_redist.x64.exe` or
  `vc_redist.arm64.exe`; 1.14.2 is the first release with the app-local runtime.
- The legacy `--high-res` path is an output enlargement, not true internal 640x480 CEL
  rendering. The experimental `--true-2x` path remains opt-in and per-title.
- macOS Retina 2x presentation is still unvalidated, and Intel macOS is unsupported.
- No BIOS ROMs, game images, or extracted game assets are included. Supply legally
  obtained files.
