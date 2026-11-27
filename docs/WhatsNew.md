# What's new in 5DO 1.18.1

**Released:** 2026-11-26

5DO 1.18.1 is a patch release that makes MP4 recording work on a clean install. When
FFmpeg is missing, 5DO now offers to download a pinned, SHA-256-verified build instead of
failing with a setup error. The 1.18.0 MDS/CCD, recording, interpolation, void-bar, and
live-clock features are unchanged.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.18.1-win-x64.zip` |
| Windows on ARM | `5do-1.18.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.18.1-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.18.1_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.18.1-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.18.1-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.18.1-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.18.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. The `.zsync`
files feed `AppImageUpdate`.

`SHA256SUMS.txt` and `SHA256SUMS-linux.txt` are the only validation files. There are no
`.asc` signatures and no keyring; run `sha256sum -c` after downloading.

## FFmpeg installation

- **Desktop.** Open **Configuration > Install FFmpeg**, or click **File > Record MP4...**
  when FFmpeg is not installed. The dialog shows the pinned version, size, source, and
  target folder, then downloads with progress and a cancel button.
- **CLI.** Run `5do ffmpeg install` or `5do ffmpeg install --json`. Use
  `5do ffmpeg status --json` to check the current executable.
- **Verification.** 5DO downloads over HTTPS, enforces a size limit, hashes while
  writing, and installs only after the pinned SHA-256 matches. It then runs `-version`
  and `-encoders` and requires H.264 and AAC. A failed check leaves no executable.
- **Location.** On Windows and portable Linux folders the executable is saved beside
  5DO. Read-only installs and macOS app bundles use the user data `tools/` folder
  (`~/Library/Application Support/5DO/tools` on macOS).
- **Platforms.** Windows 6.1.1, Linux 7.0.2, and macOS 6.0 pinned static builds. Windows
  arm64 uses the x64 build under Windows x64 emulation.

FFmpeg is still not bundled or redistributed. The download needs network access;
`PATH`, `FIVEDO_FFMPEG`, or a manually placed executable still work offline.

## Still included from 1.18.0

- Read-only Alcohol MDS/MDX and CloneCD CCD/IMG/SUB images.
- H.264/AAC MP4 recording with Low/Medium/High quality and DAR 4:3 output.
- High-refresh frame interpolation, patterned GPU void bars, and live CPU-clock changes.
- Rolling autosave with startup resume, direct ZIP/7z ISO/BIN/CUE+BIN containers, CD-DA
  extraction, physical CD-DA/read-error recovery, and the five bundles plus Linux
  packages.

## Verification

- Full Release suite **2,651/2,651**; build, `dotnet format`, `jb inspectcode`,
  and `mkdocs build --strict` are clean.
- The CLI installed FFmpeg on Windows x64, Linux x64, and macOS arm64. Windows installed
  beside the CLI; Linux marked the 7.0.2 binary executable; macOS used the
  `~/Library/Application Support/5DO/tools` fallback. A Windows MP4 recording with the
  installed binary produced a 10.010 s H.264/AAC file with DAR 4:3.
- Checksum-mismatch tests leave no executable or temporary file.

## Known issues

- The download needs network access and is explicit, not automatic at startup.
- The Windows arm64 entry uses x64 emulation. Supply a native arm64 build with
  `FIVEDO_FFMPEG` if required.
- MP4 still needs FFmpeg; pause omits the gap, and fast-forward/rewind stop the recording.
- Frame interpolation needs a fixed presentation rate above 60 Hz and is disabled with
  VRR.
- MDS/CCD real-image parity remains `SYS-29`; large prepared images can pause the desktop
  while they are joined or decoded.
