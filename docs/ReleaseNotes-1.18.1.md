# 5DO 1.18.1

**Released:** 2026-11-26

5DO 1.18.1 is a patch release for clean installs. MP4 recording no longer fails with
“FFmpeg was not found”: the desktop, the recording prompt, and the CLI can now download a
pinned, SHA-256-verified FFmpeg build and install it automatically. The 1.18.0 disc,
recording, interpolation, void-bar, and live-clock features are unchanged.

## Download

| System | File |
|---|---|
| Windows 10/11 x64 | [`5do-1.18.1-win-x64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-win-x64.zip) |
| Windows on ARM | [`5do-1.18.1-win-arm64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-win-arm64.zip) |
| macOS on Apple Silicon | [`5do-1.18.1-osx-arm64.dmg`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-osx-arm64.dmg) |
| Debian/Ubuntu/Mint x64 | [`5do_1.18.1_amd64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do_1.18.1_amd64.deb) |
| Debian/Ubuntu/Mint arm64 | [`5do_1.18.1_arm64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do_1.18.1_arm64.deb) |
| Fedora/RHEL/openSUSE x64 | [`5do-1.18.1-1.linux.x86_64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-1.linux.x86_64.rpm) |
| Fedora/RHEL/openSUSE arm64 | [`5do-1.18.1-1.linux.aarch64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-1.linux.aarch64.rpm) |
| Any Linux, no install (x64) | [`5do-1.18.1-linux-x86_64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-linux-x86_64.AppImage) |
| Any Linux, no install (arm64) | [`5do-1.18.1-linux-aarch64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-linux-aarch64.AppImage) |
| Any Linux, sandboxed (x64) | [`5do-1.18.1-linux-x86_64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-linux-x86_64.flatpak) |
| Any Linux, sandboxed (arm64) | [`5do-1.18.1-linux-aarch64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-linux-aarch64.flatpak) |
| Any Linux, portable folder | [`5do-1.18.1-linux-x64.tar.gz`](https://github.com/purelogiccode/5DO/releases/download/v1.18.1/5do-1.18.1-linux-x64.tar.gz) (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is
embedded in the self-contained single-file apphosts. Extract the archive before running
it. `SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## FFmpeg installation

- **Configuration > Install FFmpeg** opens a consent dialog with the pinned version,
  download size, source, and target folder, then shows progress and a cancel button.
- **File > Record MP4...** offers the same dialog when FFmpeg is missing, then continues
  to the save picker after installation.
- `5do ffmpeg install` and `5do ffmpeg status --json` provide the same behavior for
  scripts. `--force` replaces an existing installed copy.
- The downloader enforces HTTPS and a size limit, streams to a temporary file, verifies
  the pinned SHA-256, tests `-version` and `-encoders`, and only then moves the executable
  into place. A failure leaves no executable or temporary file.
- Pinned builds: FFmpeg 6.1.1 Windows, 7.0.2 Linux, 6.0 macOS. The Windows arm64 entry
  uses the x64 build under Windows x64 emulation.
- Location: beside 5DO when that folder is writable; otherwise the user data `tools/`
  folder. On macOS this is `~/Library/Application Support/5DO/tools`.
- FFmpeg is still not bundled or redistributed. The download needs network access; a
  manual `PATH`, `FIVEDO_FFMPEG`, or apphost copy still works offline.

## Verification

- Full Release suite **2,651/2,651**; build 0 warnings, `dotnet format`,
  `jb inspectcode --severity=WARNING` (0 results), and `mkdocs build --strict` clean.
- Windows x64: installed 82,797,568 bytes with SHA-256
  `04e1307997530f9cf2fe35cba2ca7e8875ca91da02f89d6c7243df819c94ad00`, version
  `6.1.1-essentials_build-www.gyan.dev`, beside the CLI; `ffmpeg status --json` passed,
  and a 600-frame run produced a 10.010 s H.264/AAC MP4 with DAR 4:3.
- Linux x64: installed the 7.0.2 build beside the CLI and marked it executable.
- macOS arm64: installed the 6.0 build into
  `~/Library/Application Support/5DO/tools/ffmpeg`, exercising the read-only app-bundle
  fallback.
- `FfmpegProvisionerTests` covers the five pinned platform entries and checksum
  rejection.
- CI builds the five bundles and Linux packages and runs the Windows/Linux CLI and
  frontend smokes.

## Known issues

- The FFmpeg download is explicit and needs network access; it is not bundled.
- Windows arm64 uses x64 emulation for FFmpeg. A native arm64 build can be supplied with
  `FIVEDO_FFMPEG`.
- MP4 pause omits the gap; fast-forward and rewind stop the recording.
- Frame interpolation requires a fixed rate above 60 Hz and is disabled with VRR.
- MDS/CCD real-image parity remains `SYS-29`; preparing a large split/MDX image can pause
  the desktop while it is joined or decoded.

## Links

- [Installation](https://purelogiccode.github.io/5DO/Installation.html)
- [What's new](https://purelogiccode.github.io/5DO/WhatsNew.html)
- [Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html)
- [Full changelog](https://github.com/purelogiccode/5DO/blob/master/CHANGELOG.md)
- [License](https://github.com/purelogiccode/5DO/blob/master/LICENSE) and
  [third-party notices](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md)
