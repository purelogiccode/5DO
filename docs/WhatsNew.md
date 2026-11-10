# What's new in 5DO 1.7.0

**Released:** 2026-11-10

5DO 1.7.0 is the **platform and media-polish update**. It adds CD subcode/Q data and
`disc convert`, makes the desktop shell focus-aware with selectable void patterns,
extends input scripts with pointer devices, closes the native runtime smoke matrix on
macOS and arm64, and ships macOS as a drag-to-Applications **DMG**.

## Highlights

### CD-ROM parity and disc tools

- **Opera-sized CD transfers.** `MODE SET` accepts the 2449-byte maximum: a 2352-byte
  audio/data sector plus the 96-byte subcode block (and optional error byte).
- **READ_DATA subcode/Q.** Q is packed into bit 6 of each subcode byte (MSB first) with
  control/ADR, track, index, absolute MSF, relative MSF, and the inverted CRC-16/CCITT.
  `READ SUB-Q` keeps its ten-byte payload.
- **CD-ROM state section v6.** New states preserve the full transfer buffer; version 1-5
  states still load by reading the historical 2352-byte buffer.
- **`disc convert`.** Data-only ISO/BIN/CUE+BIN/flat CHD conversion verifies the 3DO game
  ID after reopening the output.

### Desktop and input

- **Focus-loss pause.** An opt-in Settings > Emulation option pauses when the window is
  deactivated and resumes only an automatic pause when focus returns. Manual
  pause/start/stop clears the automatic state.
- **Void-area patterns.** Solid, checkerboard, grid, and scanline patterns join the
  configurable void color, and screenshots record the selected pattern in their sidecar.
- **Pixel formats.** The frame assembler emits packed 0RGB1555, RGB565, and XRGB8888.
- **Pointer input scripts.** `@mouse`, `@lightgun`, `@arcadegun`, and `@trackball`
  tokens add one-frame motion and persistent buttons to the scripted PBUS source.
- **macOS gamepad input.** A native IOKit HID source maps joystick/gamepad/multi-axis
  controllers through the eight-player profile; the pure mapper is unit-tested.

### Packaging and platforms

- **macOS DMG.** The macOS bundles are published as `.dmg` files, built natively in CI.
- **Installer scaffolding.** Unsigned Windows, Linux `.deb`/`.rpm`, and macOS
  `.app`/`.dmg`/`.pkg` scripts register `.iso`, `.cue`, `.chd`, and `.bin`.
  Signing/notarization remains conditional on certificates/accounts.
- **Native runtime smoke.** macOS x64/arm64, Linux arm64, and Windows arm64 pass the CLI
  `selftest` and frontend `--smoke-test`.
- **PAL validation.** Eight PAL-sensitive titles were captured with the PAL BIOS, plus a
  PAL2 384x288/50 Hz probe.

## Fixed

- CHD conversion no longer overwrites or deletes a same-stem source BIN; the encoder uses
  a unique temporary input.
- A CUE conversion that would overwrite the source BIN is rejected.
- The frontend audio soak resolves `--region auto` from the loaded BIOS instead of always
  using NTSC.
- READ_DATA and READ SUB-Q now share one Q-subchannel builder, keeping control, track,
  index, MSF, and CRC consistent.

## Validation

- Full suite **2,124/2,124** passes in Debug.
- `dotnet build` reports **0 warnings and 0 errors**; `dotnet format
  --verify-no-changes` and `jb inspectcode --severity=WARNING` are clean.
- A 1.6.0-era CD-ROM v5 section loads in a dedicated regression test.
- ISO/BIN/CUE/CHD conversion round-trips every data sector.
- Native runtime smoke passes on macOS x64/arm64, Linux arm64, and Windows arm64.
- Six bundles contain the CLI, desktop app, README, license, notices, `whatsnew.md`, and
  release/legal docs, with no runtime, PDB, or XML files.

## Requirements

- The **.NET 10 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Optional: network access for the online cover catalog. Local covers and the core
  emulator work offline.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.7.0-win-x64.zip` |
| Windows arm64 | `5do-1.7.0-win-arm64.zip` |
| Linux x64 | `5do-1.7.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.7.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.7.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.7.0-osx-arm64.dmg` |

Each bundle contains the framework-dependent single-file CLI and desktop app, `README.md`,
`LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.7.0) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- Physical macOS gamepad detection still needs a macOS host with a connected controller.
- Signing and notarization require a Windows certificate and an Apple developer account.
