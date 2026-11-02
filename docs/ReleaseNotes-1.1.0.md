# 5DO 1.1.0

**Release date:** 2026-11-02
**Status:** Maintenance and diagnostics update on the documented-compatibility 1.0.0
release.

## Highlights

- **Game library deduplication.** When the same disc exists in more than one scanned
  folder (for example the same collection on two drives), the library now shows the game
  once. Copies are matched by the 3DO game ID; a readable copy is preferred and the
  first deterministic path is kept.
- **Status bar cleanup.** The `frame ... PC ... cyc` counter was removed from the desktop
  status bar. The optional in-game overlay still shows frame, PC, cycle, renderer, and
  audio diagnostics.
- **OpenAL license layout.** `docs/licenses/OpenAL-Soft-COPYING.txt` moved to
  `docs/OpenAL-Soft-COPYING.txt`. The nested `docs/licenses/` folder is removed from the
  repository and from every release bundle.
- **Serilog logging.** The CLI and desktop app route all logging through Serilog. The
  remaining CLI/frontend catch blocks now log through Serilog, and warning-and-higher
  events are forwarded to the bug-report API with the required environment, error, and
  exception blocks.
- **Launch statistics.** Both hosts record the application id and version with the
  ApplicationStats service at launch. Only those two fields are sent. CI/test runs and
  `FIVEDO_STATS=0` disable it.

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

The release publishes one bundle per system:

- `5do-win-x64.zip`
- `5do-linux-x64.tar.gz`
- `5do-osx-x64.tar.gz`

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
- `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release --no-build --no-restore`
- `dotnet format 5DO.sln --verify-no-changes --no-restore`
- `jb inspectcode 5DO.sln --no-updates --severity=WARNING`
- Windows and Linux bundle smoke tests (CLI `selftest` and desktop `--smoke-test`)
- Bundle checks confirm no `runtime/`, `*.xml`, or `*.pdb` files are present

## Licensing

5DO is **donationware**: non-commercial, source-available software under the FreeDO
modified GNU LGPL. Donations do not grant commercial rights. "3DO", "Panasonic", and
"GoldStar" are trademarks of their respective owners; 5DO is not affiliated with or
endorsed by them.

See [`Licensing.md`](Licensing.md), [`OpenALSoft.md`](OpenALSoft.md), and
[`ReleaseLegalReview.md`](ReleaseLegalReview.md) for the full notices.
