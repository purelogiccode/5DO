# 5DO 1.7.0

**Released:** 2026-11-10

5DO 1.7.0 is the **platform and media-polish update**. It makes the desktop shell
focus-aware, adds selectable void-area patterns, exposes CD subcode/Q data to guest
software, adds `disc convert` between the supported data-only containers, extends input
scripts with pointer devices, and closes the native runtime smoke matrix on macOS x64,
macOS arm64, Linux arm64, and Windows arm64. The macOS release artifact is now a
drag-to-Applications **DMG**.

## Highlights

### CD-ROM and disc tools

- **2449-byte CD wire size.** `MODE SET` accepts Opera's maximum transfer: a 2352-byte
  audio/data sector plus the 96-byte subcode block (and the optional error byte).
- **READ_DATA subcode/Q.** The Q subchannel is packed into bit 6 of each subcode byte
  (MSB first) with control/ADR, track, index, absolute MSF, relative MSF, and the
  inverted CRC-16/CCITT. `READ SUB-Q` still returns its ten-byte payload.
- **CD-ROM save-state section v6.** New states preserve the full 2449-byte transfer
  buffer. Version 1-5 states still load by reading their historical 2352-byte buffer and
  zeroing the subcode tail.
- **`disc convert`.** Converts data-only sources between ISO, BIN, CUE+BIN, and flat CHD,
  then re-opens the output and verifies that the 3DO game ID matches.

### Desktop experience

- **Focus-loss pausing.** Settings > Emulation adds an opt-in pause-on-focus-loss option.
  Deactivation releases keyboard input, and activation resumes only an automatic pause;
  manual pause/stop/start clears the automatic state.
- **Void-area patterns.** Solid, checkerboard, grid, and scanline patterns replace the
  single solid void color. Screenshot sidecars record the selected pattern.
- **Pixel-format helpers.** The CPU frame assembler can emit packed 0RGB1555, RGB565, and
  XRGB8888 frames for presenters and tools.
- **Pointer input scripts.** Scripts accept `@mouse`, `@lightgun`, `@arcadegun`, and
  `@trackball` tokens with one-frame relative motion and persistent buttons.
- **macOS gamepad input.** A native IOKit HID source maps Generic Desktop joystick,
  gamepad, and multi-axis controllers through the existing eight-player profile. The
  mapping is unit-tested; physical controller detection remains a macOS-host check.

### Packaging and platform evidence

- **macOS DMG.** `package-bundle.ps1` now builds `5do-<version>-osx-<arch>.dmg` on
  macOS, and the release workflow uploads the DMG instead of a tarball.
- **Installer scaffolding.** Unsigned Windows Inno Setup, Linux `.deb`/`.rpm`, and macOS
  `.app`/`.dmg`/`.pkg` scripts register `.iso`, `.cue`, `.chd`, and `.bin` associations.
  Signing and notarization remain conditional on certificates/accounts.
- **Native runtime smoke.** macOS x64 (`macos-15-intel`), macOS arm64
  (`macos-latest`), Linux arm64 (`ubuntu-24.04-arm`), and Windows arm64
  (`windows-11-arm`) each run the CLI `selftest` and the frontend `--smoke-test`.
- **PAL validation.** Eight Europe-only/PAL-sensitive titles were captured with the PAL
  BIOS, and a PAL2 probe rendered 384x288 at 50 Hz.

### Fixed

- CHD conversion no longer writes through the natural `<name>.bin` path, so converting
  `game.bin` to `game.chd` cannot overwrite or delete the source image.
- A CUE conversion that would overwrite the source BIN is rejected.
- The frontend audio soak now resolves `--region auto` from the loaded BIOS instead of
  always using NTSC.
- READ_DATA and READ SUB-Q share one Q-subchannel builder, so control/ADR, track, index,
  MSF, and CRC values stay consistent.

## Validation

- Full suite **2,124/2,124** passes in Debug.
- `dotnet build` reports **0 warnings and 0 errors**; `dotnet format
  --verify-no-changes` and `jb inspectcode --severity=WARNING` are clean.
- A 1.6.0-era CD-ROM section v5 is loaded by a dedicated regression test; current states
  write v6.
- `disc convert` round-trips every data sector for ISO, BIN, CUE, and flat CHD; the CLI
  keeps the source BIN when a CUE output would collide with it.
- Native runtime smoke passes on the four arm64/macOS targets listed above.
- All six bundles contain the CLI, desktop app, README, license, notices, `whatsnew.md`,
  and release/legal docs, with no runtime, PDB, or XML files.

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

- Playability follows the documented binary beyond-title rule (a distinct screen beyond
  the title with input), not a full playthrough or a claim for every regional revision.
- CD command cadence remains the residual differential on the 50-title panel; the
  3,000-frame showcase still diverges on Road Rash.
- Windows x64 remains the validated GPU target. Linux x64 full-suite and CLI/frontend
  smoke validation pass; GPU presentation under llvmpipe/SSH is preview/best-effort.
- macOS gamepad mapping is unit-tested; physical controller detection still needs a
  macOS host with a connected pad.
- Signing and notarization remain conditional on a Windows certificate and an Apple
  Developer ID/keychain profile.
