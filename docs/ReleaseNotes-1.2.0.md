# 5DO 1.2.0

**Release date:** 2026-11-03
**Status:** Feature, accuracy, and presentation update on the documented-compatibility
1.0.0 release and the 1.1.0 maintenance update.

## Highlights

- **Direct3D 11 on Windows.** Optional FL11_0 presenter with runtime HLSL palette/filter
  passes, a DXGI flip-model child swapchain, `--gl-backend d3d11`, Settings selection,
  and `--d3d-probe`/`--d3d-window-probe`. OpenGL remains the default.
- **FIQ and bus accuracy.** Request-to-vector latency telemetry, `--fiq-trace`
  request/entry lines, CLIO output-FIFO reload gating, DIPIR-protected XBUS DMA,
  MADAM MCTL state persistence, and XBUS Opera poll/status semantics.
- **Disc support.** Flat 2048-byte-unit CHDs and raw 2352-byte images named `.iso` now
  open correctly.
- **Display refresh.** Settings > Video can re-present the latest frame at 30-480 Hz or
  match the display. This lowers presentation latency; it does not add motion.
- **Quality tooling.** Deterministic fuzz harness, per-edge overscan/void border,
  screenshot gallery filters/compare/export, and startup GitHub release checks.
- **Scope.** WebAssembly/browser, Android/iOS, and Raspberry Pi were removed from the
  roadmap and release gates. Desktop Windows, Linux, and macOS remain supported.

## Requirements

- The **.NET 10 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. The .NET 10 SDK is only needed for source builds.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Windows 10+, a current Linux desktop, or macOS 12+.
- An OpenGL 3.3 or OpenGL ES 3.0/3.1 context for the GPU presenter; the CPU
  `WriteableBitmap` fallback is used when OpenGL is unavailable.

BIOS ROMs and game images are **not** included.

## Artifacts

The release publishes one versioned bundle per system:

- `5do-1.2.0-win-x64.zip`
- `5do-1.2.0-linux-x64.tar.gz`
- `5do-1.2.0-osx-x64.tar.gz`

Sizes and SHA-256 checksums are recorded in `SHA256SUMS.txt` next to the bundles.

Each bundle contains both the desktop app and the CLI, `README.md`, `LICENSE`,
`THIRD-PARTY-NOTICES.md`, `whatsnew.md`, `docs/Licensing.md`,
`docs/ReleaseLegalReview.md`, `docs/OpenALSoft.md`, `docs/OpenAL-Soft-COPYING.txt`,
these release notes, and `PACKAGE.txt`.

The apps are **framework-dependent single-file executables** and require the .NET 10
runtime on the host. No .NET runtime is embedded in the executables or copied into the
bundle. PDB and XML documentation files are omitted because they are build outputs, not
runtime dependencies. Native libraries (OpenAL Soft, Skia, and ANGLE) stay beside the
apps so the LGPL replacement rights remain practical.

## Verification

- `dotnet build 5DO.sln -c Release`
- `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release --no-build --no-restore`:
  **1,597/1,597 passing**
- `dotnet format 5DO.sln --verify-no-changes --no-restore`
- `jb inspectcode 5DO.sln --severity=WARNING`: **0 results**
- Windows and Linux bundle smoke tests (CLI `selftest` and desktop `--smoke-test`)
- Bundle checks confirm no `runtime/`, `*.xml`, or `*.pdb` files are present
- Release workflow
  <https://github.com/purelogiccode/5DO/actions/runs/41581528432> passed all bundle jobs
  and published <https://github.com/purelogiccode/5DO/releases/tag/v1.2.0>; every
  `SHA256SUMS.txt` entry verifies
- 3,000-frame regression hashes for Gex (`0x05F38B50`), Lemmings (`0x9C2E3236`), and
  Road Rash (`0x5E9EE515`) remain at their documented values.

## Known issues

- `Crash 'n Burn` (Japan) remains the single known boot failure (593/594 local discs
  render).
- Linux GPU presentation is preview/best-effort under llvmpipe.
- Display refresh above 60 Hz re-presents the latest frame; frame interpolation is not
  implemented.
- Full Opera differential evidence for the interrupt, DMA, and CD timing audits remains
  post-1.2 work.
- WebAssembly, Android/iOS, and Raspberry Pi are out of scope.

## Licensing

5DO is **donationware**: non-commercial, source-available software under the FreeDO
modified GNU LGPL. Donations do not grant commercial rights. "3DO", "Panasonic", and
"GoldStar" are trademarks of their respective owners; 5DO is not affiliated with or
endorsed by them.

See [`Licensing.md`](Licensing.md), [`OpenALSoft.md`](OpenALSoft.md), and
[`ReleaseLegalReview.md`](ReleaseLegalReview.md) for the full notices.
