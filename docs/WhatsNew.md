# What's new in 5DO 1.0.0

**Released:** 2026-11-02

5DO 1.0.0 is the first **documented-compatibility** release of the C#/.NET 10 3DO
Interactive Multiplayer emulator. The release is backed by the full test suite
(1,473 tests on Windows and Linux), a 594-disc loadability sweep (593 rendered), and
the 12-title compatibility matrix.

## One bundle per system

The download is a single bundle per system. Each bundle contains **both** the desktop
app and the headless CLI:

| System | Download |
|---|---|
| Windows x64 | `5do-win-x64.zip` |
| Linux x64 | `5do-linux-x64.tar.gz` |
| macOS x64 | `5do-osx-x64.tar.gz` |

Every bundle also contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
`whatsnew.md`, the release notes, and the legal/OpenAL notices.

The apps are **single-file** executables. A private .NET 10 runtime is included once
under `runtime/` and shared by both apps; it is **not** embedded in either executable.
No separate .NET install is required. The OpenAL Soft runtime stays beside the apps so
it can be replaced as required by its LGPL license.

## Highlights

- Boots the tested BIOS and the 12 documented titles.
- 593 of 594 local discs render; `Crash 'n Burn` (Japan) is the one known boot failure.
- Deterministic save states, ten save slots, screenshots, and rewind.
- Avalonia desktop shell with a game library, settings, input mapping, logs, and a
  first-run guide.
- OpenGL/GLES two-stage presenter with accurate, nearest, sharp-bilinear, CRT, xBR,
  FSR, and HQx filters, plus a CPU fallback and 4K output.
- Optional Vulkan 1.0 backend with a headless probe and a Win32 swapchain probe.
- 44.1 kHz stereo DSP audio with an exact sample clock and bundled OpenAL Soft output.
- Headless CLI with `run`, `trace`, `bench`, `selftest`, `info`, `disc`, `fs`, `audio`,
  `disasm`, `bios`, and `state`, with JSON output.
- ISO, BIN, CUE+BIN, and CHD disc images, including parent/child CHDs.

## Requirements

- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Windows 10+, a current Linux desktop, or macOS 12+.
- OpenGL 3.3 or OpenGL ES 3.0/3.1 for GPU presentation; the CPU fallback is always
  available.

BIOS ROMs and game images are **not** included.

## Known issues

- `Crash 'n Burn` (Japan) remains the only known boot failure in the 594-disc local
  collection.
- Road Rash's full race and Alone in the Dark's inventory are not scripted end-to-end.
- Seven additional titles boot and render without scripted gameplay input.
- Linux GPU presentation is preview/best-effort under llvmpipe; the full test suite and
  CLI/frontend smoke tests pass.
- macOS and arm64 validation, M13.5/M13.6 visual review, M14 high-resolution CEL
  research, and installers are post-1.0.

See [`ReleaseNotes-1.0.0.md`](ReleaseNotes-1.0.0.md) and
[`KnownIssues.md`](KnownIssues.md) for the full lists.

## Licensing

5DO is **donationware**: non-commercial, source-available software under the FreeDO
modified GNU LGPL. Donations do not grant commercial rights. "3DO", "Panasonic", and
"GoldStar" are trademarks of their respective owners; 5DO is not affiliated with or
endorsed by them.
