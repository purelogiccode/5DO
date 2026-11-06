# 5DO 1.2.2

**Released:** 2026-11-06

5DO 1.2.2 is a performance, library, and compatibility update on the 1.2.1 arm64
release. The six-bundle packaging model is unchanged: Windows, Linux, and macOS on x64
and arm64, with the CLI and desktop app in one framework-dependent bundle.

## Highlights

- **Interpreter hot paths.** Direct DRAM/ROM instruction fetch, inline LSL#0 and
  rotated-immediate operands, a flag-preserving ALU dispatch, and
  `BitOperations.PopCount`/`TrailingZeroCount` LDM/STM iteration. Interleaved A/B:
  synthetic **1.58 → 1.33 ms/frame**, Lemmings **1.43 → 1.13 ms/frame**, hashes
  unchanged.
- **High-resolution VDLP.** Scanline DMA reads the raw VRAM word span. Lemmings
  high-res **2.42-2.47 → 1.30-1.43 ms/frame** with an identical high-res hash.
- **Rewind capture.** Fast compression, unchecked transient CRCs, a live-range memory
  section, and pooled buffers cut the worst Lemmings keyframe from **139.3 ms** to
  **7.4-9.2 ms**, allocations from ~1.5 GB to **16 MB per 1,200 frames**, and a stock
  state from ~26 MB to **~10 MB**.
- **Managed JIT (opt-in).** `--cpu-backend jit` shares PC/CPSR/cycle state across
  compiled blocks, compiles register-specified shifts, and invalidates blocks exactly
  on guest writes. It is ~20% faster than the first 1.2.x JIT but still ~1.25x slower
  than the interpreter on Lemmings boot, so the interpreter remains the default.
- **Library and database.** The library reports **653 games across 1,366 images**,
  lists duplicate copies on demand, edits game folders in Settings, and verifies every
  image against a Redump/No-Intro DAT. The embedded database grew from 518 to 653
  records with revision, disc, BIOS, fix, input, NVRAM, and cheat metadata.
- **Machine and input options.** Eight Opera DRAM/VRAM layouts, eight PBUS ports,
  opt-in Portfolio OS SWI HLE, auto per-game profiles, and audio-only CHD cataloging.
- **Hardening.** Async `void` UI handlers catch exceptions, missing-BIOS prompts open
  the BIOS picker first, and the CLI explains stale/mixed build output.

## Bundles

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.2.2-win-x64.zip` |
| Windows arm64 | `5do-1.2.2-win-arm64.zip` |
| Linux x64 | `5do-1.2.2-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.2.2-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.2.2-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.2.2-osx-arm64.tar.gz` |

Each bundle contains the desktop app, the CLI, `README.md`, `LICENSE`,
`THIRD-PARTY-NOTICES.md`, `whatsnew.md`, the release notes, and the legal/OpenAL
notices. `SHA256SUMS.txt` is attached to the release.

The apps are framework-dependent single-file executables. Install the **.NET 10
Runtime** from <https://dotnet.microsoft.com/download/dotnet/10.0>; no runtime is
embedded in the bundle.

## Verification

- Full suite: **1,815 tests passing**.
- Build, format, benchmark gate, and CLI `selftest` are clean.
- JIT parity: 1,000-frame Road Rash `0x41D2A1DA` and Lemmings `0x110E7DC5`, identical
  instruction counts.
- Six bundles build; x64 runtime smoke tests pass; arm64 apphosts are
  architecture-verified.

## Known issues

- The managed JIT is still slower than the interpreter on real boot/gameplay code.
- CORE-05 threaded DSP remains pending.
- `Crash 'n Burn` (Japan) remains the single known no-video case in the 594-disc sweep.
- Linux GPU presentation is preview/best-effort under llvmpipe.
- Arm64 bundles are architecture-validated but not executed on arm64 hardware in CI.

See the [full release notes](https://purelogiccode.github.io/5DO/ReleaseNotes-1.2.2.html)
and [What's new](https://purelogiccode.github.io/5DO/WhatsNew.html).
