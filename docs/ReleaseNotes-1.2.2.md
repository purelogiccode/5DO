# 5DO 1.2.2

**Release date:** 2026-11-06
**Status:** Performance, library, and compatibility update on the 1.2.1 arm64 release.

## Highlights

- **Faster CPU hot paths.** The interpreter now fetches instructions directly from
  DRAM/ROM, inlines LSL#0 and rotated-immediate operands, uses a flag-preserving ALU
  dispatch, and iterates LDM/STM with population-count/trailing-zero intrinsics. An
  interleaved A/B measured synthetic **1.58 → 1.33 ms/frame** and Lemmings (USA)
  **1.43 → 1.13 ms/frame** with unchanged hashes.
- **Faster high-resolution video.** High-resolution scanline DMA reads the raw VRAM word
  span instead of four `IRam.Read16` calls per pixel. Lemmings high-res fell from
  **2.42-2.47** to **1.30-1.43 ms/frame** with an identical high-res hash.
- **Rewind no longer stalls gameplay.** Rewind keyframes use fast compression and
  unchecked transient CRCs, the memory section stores only the live RAM/VRAM range,
  and the capture buffers are pooled. On Lemmings the worst keyframe fell from
  **139.3 ms** to **7.4-9.2 ms**, allocations from ~1.5 GB to **16 MB per 1,200
  frames**, and a stock state from ~26 MB to **~10 MB**.
- **Managed ARM JIT (opt-in, still experimental).** `--cpu-backend jit` compiles hot
  blocks, shares PC/CPSR/cycle state across blocks, compiles register-specified shifts,
  and invalidates blocks exactly on guest writes. It is bit-identical on the test
  corpus and is ~20% faster than the first 1.2.x JIT, but it is still ~1.25x slower
  than the interpreter on Lemmings boot. The interpreter remains the default.
- **Library and game database.** The library now reports both unique games and image
  counts (for example `653 games · 1,366 images`), lists duplicate copies on demand,
  edits game folders in Settings, and verifies every image against a Redump/No-Intro
  DAT with cached health badges. The embedded game database grew from 518 to 653
  records with revision, disc, BIOS, fix, input, NVRAM, and cheat metadata.
- **More machine and input options.** All eight Opera DRAM/VRAM layouts are selectable,
  input supports eight PBUS ports, and the opt-in Portfolio OS SWI HLE handles Opera's
  14 fixed-point math SWIs. Auto per-game profiles apply database input, BIOS, NVRAM,
  fix, and timing values.
- **Audio-only CHD cataloging.** CHDs with no data track now open, derive a stable
  TOC-based ID, and report `audioCd`/`trackCount`, so bonus audio discs appear in the
  library. In-emulator CD-DA playback still needs a data-track boot path.

## Bug fixes

The post-1.2.1 audit fixed eight concrete issues:

- JIT register-shift PC-relative operands now read `instruction+12` for `Rn` and `Rm`
  (`Rs` still reads `instruction+8`), matching the interpreter.
- ROM-bank switches, BIOS reloads, low-memory overlay changes, and soft resets now
  invalidate compiled blocks.
- JIT FIQ entry telemetry includes the current block's cycles, so `--fiq-trace` and
  FIQ latency are no longer short by one batch.
- Prefetched track sectors keep their actual byte count, so a 2048-byte data sector
  cannot return a 2352-byte length or stale tail bytes.
- Legacy 16-byte PBUS packets report slots 7-8 as disconnected.
- Pointer exit clears lightgun and mouse state for all eight PBUS ports.
- The central machine-option validator enforces the documented 0.5x-4x ARM clock range.

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

- `5do-1.2.2-win-x64.zip`
- `5do-1.2.2-win-arm64.zip`
- `5do-1.2.2-linux-x64.tar.gz`
- `5do-1.2.2-linux-arm64.tar.gz`
- `5do-1.2.2-osx-x64.tar.gz`
- `5do-1.2.2-osx-arm64.tar.gz`

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
- `dotnet test 5DO.sln -c Debug`: **1,815/1,815 passing**
- `dotnet format 5DO.sln --verify-no-changes --no-restore`: clean
- Synthetic benchmark gate: **pass** (0 bytes/frame, core/total p99 within budget)
- CLI `selftest`: pass
- JIT parity: 1,000-frame Road Rash `0x41D2A1DA` and Lemmings `0x110E7DC5`, identical
  instruction counts; 600-frame Lemmings benchmark hash `0x2A53D449`
- Six bundle builds; x64 CLI `selftest` and desktop `--smoke-test` pass; arm64 apphost
  machine types verified as PE ARM64, ELF aarch64, and Mach-O arm64
- Bundle checks confirm no `runtime/`, `*.xml`, or `*.pdb` files are present
- Release workflow
  <https://github.com/purelogiccode/5DO/actions/runs/42169040644> passed all six bundle
  jobs and the publish job; all six `SHA256SUMS.txt` entries verify. The docs workflow
  deployed the Pages site and published the wiki.

## Known issues

- The managed JIT is still slower than the interpreter on real boot/gameplay code
  (~1.25x on Lemmings). The interpreter remains the default and the accuracy oracle.
- CORE-05 threaded DSP remains pending. The DSP is only ~14% of frame time, and a
  per-sample worker handoff costs more than the DSP work; a speculative pipeline still
  needs FIFO/RAM validation before it can preserve FIQ and FIFO timing.
- Arm64 bundles are architecture-validated but are not executed on arm64 hardware in
  CI. Please report arm64 runtime findings with a diagnostics bundle.
- `Crash 'n Burn` (Japan) remains the single known no-video case in the local
  594-disc compatibility sweep (593/594 render).
- Linux GPU presentation is preview/best-effort under llvmpipe; the CPU fallback works.
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
