# 5DO 1.2.1

**Release date:** 2026-11-03
**Status:** Arm64 packaging update on the 1.2.0 feature and accuracy release.

## Highlights

- **Windows arm64, Linux arm64, and macOS arm64 bundles.** All six RIDs are now built
  from the same source and packaging script as the x64 bundles.
- **Architecture verification.** CI checks the PE, ELF, and Mach-O machine type of the
  arm64 apphosts. x64 bundles keep their runtime `selftest` and desktop smoke tests;
  arm64 bundles are architecture-validated build artifacts because no arm64 runner is
  available.
- **No emulator behavior changes.** 1.2.1 is the same 1.2.0 code with the arm64
  packaging and release matrix added.

## Requirements

- The **.NET 10 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Windows 10+, a current Linux desktop, or macOS 12+.
- An OpenGL 3.3 or OpenGL ES 3.0/3.1 context for the GPU presenter; the CPU
  `WriteableBitmap` fallback is used when OpenGL is unavailable.

BIOS ROMs and game images are **not** included.

## Artifacts

The release publishes six versioned bundles and `SHA256SUMS.txt`:

- `5do-1.2.1-win-x64.zip`
- `5do-1.2.1-win-arm64.zip`
- `5do-1.2.1-linux-x64.tar.gz`
- `5do-1.2.1-linux-arm64.tar.gz`
- `5do-1.2.1-osx-x64.tar.gz`
- `5do-1.2.1-osx-arm64.tar.gz`

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

- `dotnet build 5DO.sln -c Release`: 0 warnings and 0 errors
- `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`: **1,597/1,597 passing**
- `dotnet format 5DO.sln --verify-no-changes --no-restore`: clean
- `jb inspectcode 5DO.sln --severity=WARNING`: **0 results**
- Six bundle builds; x64 CLI `selftest` and desktop `--smoke-test` pass
- arm64 apphost machine types verified: PE ARM64, ELF aarch64, Mach-O arm64
- Bundle checks confirm no `runtime/`, `*.xml`, or `*.pdb` files are present
- Release workflow
  <https://github.com/purelogiccode/5DO/actions/runs/41583317988> passed all six
  bundle jobs and published <https://github.com/purelogiccode/5DO/releases/tag/v1.2.1>;
  all six `SHA256SUMS.txt` entries verify
- 3,000-frame regression hashes for Gex, Lemmings, and Road Rash remain unchanged

## Known issues

- Arm64 bundles are architecture-validated but are not executed on arm64 hardware in
  CI. Please report arm64 runtime findings with a diagnostics bundle.
- `Crash 'n Burn` (Japan) remains the single known boot failure (593/594 local discs
  render).
- Linux GPU presentation is preview/best-effort under llvmpipe.
- Display refresh above 60 Hz re-presents the latest frame; frame interpolation is not
  implemented.
- WebAssembly, Android/iOS, and Raspberry Pi are out of scope.

## Licensing

5DO is **donationware**: non-commercial, source-available software under the FreeDO
modified GNU LGPL. Donations do not grant commercial rights. "3DO", "Panasonic", and
"GoldStar" are trademarks of their respective owners; 5DO is not affiliated with or
endorsed by them.

See [`Licensing.md`](Licensing.md), [`OpenALSoft.md`](OpenALSoft.md), and
[`ReleaseLegalReview.md`](ReleaseLegalReview.md) for the full notices.
