# What's new in 5DO 1.2.0

**Released:** 2026-11-03

5DO 1.2.0 is a feature and accuracy update on top of the documented-compatibility
1.0.0 release and the 1.1.0 maintenance update. It adds a Windows Direct3D 11
presenter, a fuzz harness, display-refresh control, broader disc support, and a
substantial interrupt/DMA/DSP telemetry pass.

## Changes in 1.2.0

- **Direct3D 11 presenter (Windows).** Optional FL11_0 backend with runtime HLSL
  palette/filter passes, a DXGI flip-model child swapchain, `--gl-backend d3d11`,
  Settings selection, and `--d3d-probe`/`--d3d-window-probe`. OpenGL remains the
  default.
- **GitHub release checks.** The CLI and desktop app check the public release feed at
  startup (cached for six hours) and offer the download page. `5do update` checks on
  demand. The check is opt-out via Settings, `--no-update-check`, or
  `FIVEDO_UPDATE_CHECK=0`.
- **Fuzzing.** `tools/FiveDo.Fuzz` covers state, disc, CUE, Opera-FS, CD command
  streams, and ARM60 instruction streams with deterministic seeds and reproduction
  artifacts. A nightly workflow runs the longer sweep.
- **Video presentation controls.** Per-edge overscan (0-32 px), per-game crop profiles,
  and a configurable 0-64 px void border with `#RRGGBB`/`#AARRGGBB` color.
- **Screenshot gallery.** Game/date filters, side-by-side compare, export, and a settings
  sidecar written beside every capture.
- **Display refresh.** Settings > Video can present the latest frame at 30-480 Hz or
  match the display. The 3DO still outputs 60 fields per second (50 for PAL), so this
  lowers presentation latency but does not add motion.
- **Disc support.** Flat CHDs with 2048-byte cooked-ISO units are accepted as a single
  data track, and raw 2352-byte images named `.iso` are detected by their CD sync
  header.
- **Interrupt and bus accuracy.** FIQ request-to-vector latency is measured and traced,
  CLIO output-FIFO reload and DIPIR-protected XBUS DMA follow Opera, MADAM persists the
  MCTL-written gate, XBUS device-15 Opera poll/status semantics are implemented, and
  XBUS state loading rejects mismatched device slots. `run --json` reports FIQ and DSP
  timing counters.
- **DSP telemetry.** Per-tick loop, sleeping, peak, and mean instruction statistics are
  reported by `run --json` and the text result.
- **Scope.** WebAssembly/browser, Android/iOS, and Raspberry Pi targets were removed
  from the roadmap and release gates. Windows, Linux, and macOS desktop remain the
  supported platforms.

## One bundle per system

The download is a single versioned bundle per system. Each bundle contains **both** the
desktop app and the headless CLI:

| System | Download |
|---|---|
| Windows x64 | `5do-1.2.0-win-x64.zip` |
| Linux x64 | `5do-1.2.0-linux-x64.tar.gz` |
| macOS x64 | `5do-1.2.0-osx-x64.tar.gz` |

Every bundle contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`,
the release notes, and the legal/OpenAL notices. SHA-256 checksums are attached as
`SHA256SUMS.txt`.

The apps are **framework-dependent single-file** executables. Install the **.NET 10
Runtime** from <https://dotnet.microsoft.com/download/dotnet/10.0>; no .NET runtime is
embedded in the executables or copied into the bundle. The OpenAL Soft runtime stays
beside the apps so it can be replaced as required by its LGPL license.

PDB and XML documentation files are omitted from the bundles; they are build outputs,
not runtime dependencies.

## Requirements

- The **.NET 10 Runtime** installed on the host. It is not included in the bundle.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Windows 10+, a current Linux desktop, or macOS 12+.
- OpenGL 3.3 or OpenGL ES 3.0/3.1 for GPU presentation; the CPU fallback is always
  available.

BIOS ROMs and game images are **not** included.

## Verification

- Full suite: **1,597 tests passing** on Windows (Debug and Release).
- `dotnet build` reports 0 warnings and 0 errors.
- `dotnet format --verify-no-changes` is clean.
- `jb inspectcode --severity=WARNING` reports 0 results.
- CLI `selftest` and desktop `--smoke-test` pass; the three platform bundles are
  built and smoke-tested by CI.
- 3,000-frame regression hashes for Gex, Lemmings, and Road Rash remain at their
  documented values.

## Known issues

- `Crash 'n Burn` (Japan) remains the single known boot failure (593/594 local discs
  render).
- Linux GPU presentation is preview/best-effort under llvmpipe; the CPU fallback works.
- Display refresh above 60 Hz is re-presentation, not frame interpolation.
- Full Opera differential evidence for the interrupt, DMA, and CD timing audits remains
  post-1.2 work.

See the [project status](Status.md), [known issues](KnownIssues.md), and
[compatibility notes](Compatibility.md) for details.
