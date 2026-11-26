# 5DO 1.18.0

**Released:** 2026-11-26

5DO 1.18.0 is a feature release built on the 1.17.1 bug-fix baseline. It adds read-only
Alcohol MDS/MDX and CloneCD CCD/IMG/SUB disc images, optional FFmpeg-based H.264/AAC MP4
recording with Low/Medium/High quality, high-refresh frame interpolation, patterned GPU
void bars, and live CPU-clock changes. The documented 320x240 compatibility path and the
378-title test set are unchanged.

## Download

| System | File |
|---|---|
| Windows 10/11 x64 | [`5do-1.18.0-win-x64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-win-x64.zip) |
| Windows on ARM | [`5do-1.18.0-win-arm64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-win-arm64.zip) |
| macOS on Apple Silicon | [`5do-1.18.0-osx-arm64.dmg`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-osx-arm64.dmg) |
| Debian/Ubuntu/Mint x64 | [`5do_1.18.0_amd64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do_1.18.0_amd64.deb) |
| Debian/Ubuntu/Mint arm64 | [`5do_1.18.0_arm64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do_1.18.0_arm64.deb) |
| Fedora/RHEL/openSUSE x64 | [`5do-1.18.0-1.linux.x86_64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-1.linux.x86_64.rpm) |
| Fedora/RHEL/openSUSE arm64 | [`5do-1.18.0-1.linux.aarch64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-1.linux.aarch64.rpm) |
| Any Linux, no install (x64) | [`5do-1.18.0-linux-x86_64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-linux-x86_64.AppImage) |
| Any Linux, no install (arm64) | [`5do-1.18.0-linux-aarch64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-linux-aarch64.AppImage) |
| Any Linux, sandboxed (x64) | [`5do-1.18.0-linux-x86_64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-linux-x86_64.flatpak) |
| Any Linux, sandboxed (arm64) | [`5do-1.18.0-linux-aarch64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-linux-aarch64.flatpak) |
| Any Linux, portable folder | [`5do-1.18.0-linux-x64.tar.gz`](https://github.com/purelogiccode/5DO/releases/download/v1.18.0/5do-1.18.0-linux-x64.tar.gz) (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is
embedded in the self-contained single-file apphosts. Extract the archive before running
it. `SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## Highlights

- **MDS/MDX and CCD/IMG/SUB.** Read-only Alcohol MDS/MDF/MDX and CloneCD CCD/IMG/SUB
  images open through the desktop, CLI, library, and game database. MDS CUE sheets are
  prepared in a temp cache when the image is split, compressed, subchannel-bearing,
  pregap-padded, or encrypted. Malformed descriptors fail with typed errors. The
  representative real-image boot-hash comparison remains open as `SYS-29`.
- **MP4 recording.** **File > Record MP4...** and `--record-mp4` capture the presented
  frame and DSP/CD audio mix as H.264/AAC. FFmpeg is an optional external executable
  (`FIVEDO_FFMPEG`, the apphost folder, or `PATH`), not a bundled codec. Low/Medium/High
  quality is available through `--record-quality` and Settings > Video > MP4 quality.
- **High-refresh frame interpolation.** Presentation can blend the two most recent
  completed fields above 60 Hz, using the fixed 15-bit direct-color path. Endpoint frames
  keep full precision, screenshots force the current frame, and gameplay/audio/save-state
  hashes are unchanged.
- **Patterned GPU void bars.** Solid, Checker, Grid, Scanline, FourDO, Bumps, and Metal
  patterns render in the letterbox/pillarbox area on OpenGL, Direct3D 11, and Vulkan.
- **Live CPU clock.** A CPU-clock change is applied at the next frame boundary without
  recreating the machine or reloading the disc; save states record the active clock.
- **Licensing.** The FreeDO modified GNU LGPL is retained as non-commercial donationware.
  The 4DO game database is a separate GPL-2.0 data component with its full text in the
  bundle.

## Reliability

- **MDS/CCD paths and cache.** Relative descriptor paths now resolve against the current
  directory, stored first-track pregaps no longer shift LBA 0, split MDS volumes are
  joined instead of truncated, the preparation cache includes companion data-file
  identity and a format version, and a case-insensitive `.img` lookup handles
  Windows-ripped `GAME.CCD`/`GAME.IMG` sets on Linux.
- **MP4 recording.** Saving Settings during a recording no longer restores frame skip,
  the encoder starts inside the guarded path and reports its real failure, the mux
  writes a sibling temp file and moves it only after success, failed finalization
  removes the temp WAV/video, `--record-frames` caps the WAV, screenshots and rewind
  reset interpolation, and a zero-frame desktop stop is canceled cleanly. Region
  changes stop the recording instead of mixing 50 Hz and 59.94 Hz timestamps.
- **Presentation.** The Vulkan void pattern is no longer vertically mirrored, the
  OpenGL void program is deleted with the renderer, interpolation resets after pause,
  step, rewind, state load, and disc change, and an outlier submit gap resets the
  interval instead of ghosting for several frames.
- **Live clock.** Changing the CPU clock clears rewind history and rescales the Quartz
  and Clio cycle accumulators so the switch does not emit a catch-up burst.
- MDS/CCD library scans are additive and do not change existing BIN/CUE, ISO, CHD, CDI,
  NRG, or archive behavior.

## Verification

- Full Release suite **2,644/2,644**; build 0 warnings, `dotnet format`, `jb inspectcode`
  at warning severity, and `mkdocs build --strict` are clean.
- MP4: 3,600-frame runs produced 60.06 s H.264/AAC at 60000/1001 with non-silent audio on
  Windows x64, Linux x64, and macOS arm64. Linux Low/Medium/High sizes were
  1.72/2.97/5.19 MB. Desktop UI automation selected High and persisted it. A post-fix
  600-frame Road Rash recording produced 10.010 s with DAR 4:3, and the no-frame failure
  test leaves no temp directory.
- Interpolation: Road Rash held 59.4-60.4 fps with 148-167 Hz presentation on OpenGL,
  132 Hz on Vulkan, and 83 Hz on Direct3D 11, `audio ... u0 d0`, and ~3 ms
  input-to-submit latency. The toggle-off run presented at 167 Hz without blending.
- MDS/CCD: synthetic data/audio, malformed-input, factory, library, and database tests
  pass; real-image parity is tracked as `SYS-29`.
- Bundles: five RID builds, bundle documentation checks, OpenAL/VC++/MoltenVK runtime
  checks, CLI `selftest`, and frontend smoke run in the release workflow.

## Known issues

- Representative real MDS/MDF and CCD/IMG/SUB images are not yet boot-hash compared with
  the equivalent BIN/CUE (`SYS-29`).
- MP4 needs an external FFmpeg. Pause omits the wall-clock gap; fast-forward and rewind
  stop the recording so audio and video never desynchronize.
- Frame interpolation requires a fixed rate above 60 Hz and is disabled with VRR; it adds
  up to one field of display latency.
- `--high-res` remains presentation-only; true 2x CEL completeness is `VID-20`.
- Archive support covers ISO/BIN/CUE+BIN only. Extract CHD/CDI/NRG/MDS/CCD, encrypted,
  and multi-volume archives first.
- The FZ-10 audio-only BIOS CD-player transport remains at STOP (`SYS-01`); use
  `disc audio` for CD-DA verification.
- RetroAchievements hardcore client approval is still awaiting external review
  (`REL-14`).

## Links

- [Installation](https://purelogiccode.github.io/5DO/Installation.html)
- [What's new](https://purelogiccode.github.io/5DO/WhatsNew.html)
- [Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html)
- [Full changelog](https://github.com/purelogiccode/5DO/blob/master/CHANGELOG.md)
- [License](https://github.com/purelogiccode/5DO/blob/master/LICENSE) and
  [third-party notices](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md)
