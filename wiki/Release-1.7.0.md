# 5DO 1.7.0

**Released:** 2026-11-10

5DO 1.7.0 is the platform and media-polish update. It adds CD subcode/Q data and
`disc convert`, makes the desktop shell focus-aware with selectable void patterns,
extends input scripts with pointer devices, closes the native runtime smoke matrix on
macOS and arm64, and ships macOS as a drag-to-Applications **DMG**.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.7.0-win-x64.zip` |
| Windows arm64 | `5do-1.7.0-win-arm64.zip` |
| Linux x64 | `5do-1.7.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.7.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.7.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.7.0-osx-arm64.dmg` |

The apps are framework-dependent. Install the **.NET 10 Runtime** from
<https://dotnet.microsoft.com/download/dotnet/10.0> first; the bundles do not include it.
See the [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.7.0) for
`SHA256SUMS.txt`.

## Highlights

- **CD-ROM subcode/Q.** READ_DATA returns the 96-byte subcode block with Q packed into
  bit 6 of each byte (control/ADR, track, index, absolute and relative MSF, inverted
  CRC-16/CCITT). The CD wire accepts Opera's 2449-byte maximum.
- **`disc convert`.** Data-only ISO/BIN/CUE+BIN/flat CHD conversion verifies the output
  game ID. CHD encoding uses a unique temporary BIN and no longer collides with a
  same-stem source image.
- **CD-ROM state v6.** New states preserve the full transfer buffer; v1-5 states still
  load with their historical 2352-byte data buffer.
- **Desktop.** Opt-in focus-loss pausing, solid/checker/grid/scanline void patterns,
  0RGB1555/RGB565/XRGB8888 frame assembly, and pointer input scripts.
- **macOS gamepad input.** Native IOKit HID source with a unit-tested mapper for
  joystick/gamepad/multi-axis controllers.
- **Packaging.** macOS is published as a DMG. Unsigned Windows/Linux/macOS installer
  scaffolding registers `.iso`, `.cue`, `.chd`, and `.bin` associations.
- **Platforms.** macOS x64/arm64, Linux arm64, and Windows arm64 pass the native CLI
  `selftest` and frontend `--smoke-test`.

## Validation

- Full suite **2,124/2,124** passes in Debug.
- Build, format, and InspectCode are clean.
- ISO/BIN/CUE/CHD conversion round-trips every data sector.
- The six framework-dependent bundles contain the apphosts and legal/docs set with no
  runtime, PDB, or XML files.

## Known limits

- Playability follows the binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation is
  preview/best-effort.
- Physical macOS gamepad detection still needs a macOS host with a connected controller.
- Signing and notarization require a Windows certificate and an Apple developer account.
