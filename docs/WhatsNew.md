# What's new in 5DO 1.1.0

**Released:** 2026-11-02

5DO 1.1.0 is a maintenance and diagnostics update on top of the documented-compatibility
1.0.0 release.

## Changes in 1.1.0

- **Game library deduplication.** The same game found in more than one scanned folder is
  now listed once, matched by its 3DO game ID.
- **Simpler status bar.** The `frame ... PC ... cyc` counter was removed; the optional
  in-game overlay still shows detailed frame, PC, renderer, and audio timings.
- **OpenAL license location.** The LGPL text now lives at
  `docs/OpenAL-Soft-COPYING.txt`; the nested `docs/licenses/` folder was removed from the
  repository and from every bundle.
- **Serilog everywhere.** Both hosts use Serilog for all logging. Warning-and-higher
  events are forwarded to the bug-report API with the required environment, error, and
  exception blocks.
- **Launch statistics.** The CLI and desktop app record the application id and version
  with ApplicationStats at launch. Only those two fields are sent; CI/test runs and
  `FIVEDO_STATS=0` disable it.

## The 1.0.0 release

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

The apps are **framework-dependent single-file** executables. You must install the
**.NET 10 Runtime** from <https://dotnet.microsoft.com/download/dotnet/10.0>; no .NET
runtime is embedded in the executables or copied into the bundle. The OpenAL Soft
runtime stays beside the apps so it can be replaced as required by its LGPL license.

PDB and XML documentation files are omitted from the bundles; they are build outputs,
not runtime dependencies.

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

- The **.NET 10 Runtime** installed on the host. It is not included in the bundle.
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

See [`ReleaseNotes-1.1.0.md`](ReleaseNotes-1.1.0.md) and
[`KnownIssues.md`](KnownIssues.md) for the full lists.

## Licensing

5DO is **donationware**: non-commercial, source-available software under the FreeDO
modified GNU LGPL. Donations do not grant commercial rights. "3DO", "Panasonic", and
"GoldStar" are trademarks of their respective owners; 5DO is not affiliated with or
endorsed by them.
