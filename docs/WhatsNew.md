# What's new in 5DO 1.18.0

**Released:** 2026-11-26

5DO 1.18.0 adds more disc formats and desktop features while keeping the documented
320x240 compatibility path unchanged. It opens read-only Alcohol MDS/MDX and CloneCD
CCD/IMG/SUB images, records H.264/AAC MP4 through an optional external FFmpeg, blends
high-refresh presentation frames, draws patterned GPU void bars, and applies CPU-clock
changes without restarting the machine.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.18.0-win-x64.zip` |
| Windows on ARM | `5do-1.18.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.18.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.18.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.18.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.18.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.18.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.18.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder. The `.zsync`
files feed `AppImageUpdate`.

`SHA256SUMS.txt` and `SHA256SUMS-linux.txt` are the only validation files. There are no
`.asc` signatures and no keyring; run `sha256sum -c` after downloading.

## New features

- **Alcohol and CloneCD images.** The desktop, CLI, library, and game database now open
  read-only Alcohol MDS/MDF/MDX and CloneCD CCD/IMG/SUB images. Prepared CUE sheets are
  cached, malformed inputs fail with typed errors, and encrypted, compressed, or
  subchannel-padded MDS images are prepared in the temp cache. Representative real-image
  parity is still tracked as `SYS-29`.
- **MP4 recording.** **File > Record MP4...** and CLI `--record-mp4` capture the presented
  frame and DSP/CD audio mix as H.264/AAC through an optional external FFmpeg.
  Low/Medium/High quality is available in **Settings > Video > MP4 quality** or
  `--record-quality`. FFmpeg is not bundled; install it on `PATH`, set `FIVEDO_FFMPEG`,
  or place `ffmpeg`/`ffmpeg.exe` beside 5DO.
- **High-refresh frame interpolation.** **Settings > Video > Frame interpolation** blends
  the two most recent completed fields when the display runs above 60 Hz. It needs a
  fixed rate and is disabled with VRR. Endpoint frames stay at full precision; gameplay,
  audio, save states, and frame hashes are unchanged.
- **Patterned GPU void bars.** The letterbox/pillarbox area can draw Solid, Checker, Grid,
  Scanline, FourDO, Bumps, or Metal patterns on OpenGL, Direct3D 11, and Vulkan.
- **Live CPU clock.** Changing the CPU-clock setting applies at the next frame boundary
  without recreating the machine or reloading the disc. Save states record the clock in
  use.

## Licensing

The 2026-11-25 `REL-13` decision is retained: the project remains under the FreeDO
modified GNU LGPL as non-commercial donationware. The 4DO game database is a separate
**GPL-2.0** data component; its complete text ships in `docs/GPL-2.0.txt`. Official
Debian/Ubuntu/Fedora/openSUSE archives and Flathub are not eligible under those terms.

## Still included from 1.17.x

- Rolling autosave with startup resume, written on pause, clean exit, and a configurable
  interval and restored only when the disc game ID and BIOS MD5 match.
- Direct ZIP/7z ISO, BIN, and CUE+BIN containers, with in-memory decompression and
  extraction guidance for CHD/CDI/NRG, encrypted, and multi-volume archives.
- CD-DA extraction via `disc audio`, `disc verify` progress and mixed-mode totals, and
  guest-visible disc read-error reporting.
- The five self-contained bundles (Windows/Linux x64+arm64 and macOS arm64), Linux
  `.deb`/`.rpm`/AppImage/Flatpak packages, ROM-free packaging, and `SHA256SUMS`
  verification.

## Verification

- The full test suite passes in Release; build, `dotnet format`, `jb inspectcode`, and
  `mkdocs build --strict` are clean.
- MP4 recording produced 60.06 s H.264/AAC files at 60000/1001 with non-silent audio on
  Windows x64, Linux x64, and macOS arm64. Linux Low/Medium/High sizes were
  1.72/2.97/5.19 MB, and the desktop selector round-tripped High through `settings.json`.
- High-refresh interpolation held emulation at 59.4-60.4 fps while presentation measured
  148-167 Hz on OpenGL, 132 Hz on Vulkan, and 83 Hz on Direct3D 11, with no audio
  underruns.
- MDS/CCD readers pass synthetic data/audio, malformed-input, and library/database tests;
  real-image boot-hash parity remains `SYS-29`.
- The release workflow builds all five bundles, the Linux packages, and both checksum
  manifests. The Windows and Linux bundles run CLI `selftest` and the frontend smoke;
  the macOS arm64 bundle verifies its Mach-O apphosts and bundled MoltenVK runtime.

## Known issues

- MDS/MDX and CCD/IMG/SUB are read-only and synthetically tested; representative real
  images have not yet been compared with the equivalent BIN/CUE (`SYS-29`).
- MP4 recording requires an external FFmpeg and is not available without it. Pause omits
  the gap; fast-forward and rewind stop the recording before audio/video diverge.
- Frame interpolation needs a fixed presentation rate above 60 Hz and is disabled with
  VRR. It adds up to one field of display latency by design.
- `--high-res` remains a presentation-only 2x upscale; true 2x CEL completeness is
  `VID-20`.
- A ZIP/7z holds one image or CUE+BIN set. CHD/CDI/NRG/MDS/CCD, encrypted, and
  multi-volume archives must be extracted first.
- The FZ-10 audio-only BIOS player transport remains at STOP (`SYS-01`). Use `disc audio`
  to verify CD-DA.
- `--opera-soft-reset` remains opt-in/experimental; `--opera-dipir` still blocks the
  reporter's boot and needs a CSTAT/DIPIR transcript.
