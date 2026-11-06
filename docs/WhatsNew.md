# What's new in 5DO 1.2.2

**Released:** 2026-11-06

5DO 1.2.2 is a performance, library, and compatibility update on the 1.2.1 arm64
release. It keeps the six-bundle packaging model: Windows, Linux, and macOS on x64 and
arm64, with the CLI and desktop app in one framework-dependent bundle.

## Changes in 1.2.2

### Performance

- **Interpreter hot paths.** Direct DRAM/ROM instruction fetch, inline LSL#0 and
  rotated-immediate operands, a flag-preserving ALU dispatch, and
  `BitOperations.PopCount`/`TrailingZeroCount` LDM/STM iteration. Interleaved A/B:
  synthetic **1.58 → 1.33 ms/frame**, Lemmings **1.43 → 1.13 ms/frame**, hashes
  unchanged.
- **High-resolution VDLP.** Scanline DMA reads the raw VRAM word span instead of four
  `IRam.Read16` calls per pixel. Lemmings high-res **2.42-2.47 → 1.30-1.43 ms/frame**
  with an identical high-res hash.
- **Rewind capture.** Fast compression, unchecked transient CRCs, a live-range memory
  section, and pooled capture buffers cut the worst Lemmings keyframe from **139.3 ms**
  to **7.4-9.2 ms**, allocations from ~1.5 GB to **16 MB per 1,200 frames**, and a
  stock state from ~26 MB to **~10 MB**.
- **Prefetch reuse.** Prefetched sector/audio buffers are pooled, so cache hits do not
  allocate per read.
- **Managed JIT (opt-in).** `--cpu-backend jit` shares PC/CPSR/cycle state across
  compiled blocks, compiles register-specified shifts, and invalidates blocks exactly
  on guest writes. It is ~20% faster than the first 1.2.x JIT but still ~1.25x slower
  than the interpreter on Lemmings boot, so the interpreter remains the default.

### Library and database

- **Unique games and images.** The library identifies discs by the 4DO game ID, reports
  both counts (for example `653 games · 1,366 images`), and lists duplicate copies on
  demand.
- **Game folders in Settings.** Edit library folders directly, browse for them, and
  refresh the library when the list changes. Covers decode on bounded background tasks
  at 256 px; a cached 2,732-image listing fell from 2.8 s to 0.33 s.
- **Disc health and DAT verification.** **File > Verify Library Discs...** and
  `5do library verify` read every image, match it against a Redump/No-Intro DAT, and
  show known-dump, not-in-DAT, or read-error badges with cached results.
- **Game database expansion.** The embedded database grew from 518 to 653 records with
  revision, disc number/total, BIOS/BIOS2, fix mode, input devices, NVRAM, and cheat
  fields. `5do gamedb stats|scan|export|merge` provide a repeatable workflow.

### Accuracy, input, and machine options

- **Memory capacity.** All eight Opera DRAM/VRAM layouts (2/4/8/14/15 MB DRAM and
  1/2 MB VRAM) are selectable with `--memory`, Settings, per-game profiles, and save
  states. The stock 2 MB/1 MB default and its hashes are unchanged.
- **Eight PBUS ports.** Input supports eight ports; ports 7-8 use the Opera
  daisy-chain packet, and the MADAM PBUS queue grew from 20 to 256 bytes.
- **Portfolio OS SWI HLE.** Opt-in `--swi-hle` handles Opera's 14 fixed-point math SWIs
  (`0x50000`-`0x50012`); default-off hashes are unchanged.
- **Auto per-game profiles.** Database input, BIOS, NVRAM, fix, and timing values apply
  through one effective snapshot. Input, filter, and high-resolution changes apply live;
  BIOS/NVRAM/fix/timing changes restart the machine.
- **Audio-only CHDs.** CHDs with no data track open, derive a stable TOC-based ID, and
  report `audioCd`/`trackCount`, so bonus audio discs appear in the library.
- **CLIO timers and CEL.** The Opera timer scheduler masks counter/reload registers to
  16 bits, `run --clio-timer-stats`/`--clio-timer-trace` expose a per-timer ledger, and
  CEL busy timing now counts the 320x240 literal-line and packed-repeat rasterizer
  pixels.
- **Hardening.** Desktop `async void` handlers catch exceptions instead of escaping the
  dispatcher, missing-BIOS prompts open the BIOS picker first, and the CLI explains
  stale/mixed build output instead of printing a generic fatal error.
- **Update checks.** Cross-platform asset selection now matches the versioned
  `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`
  bundle names.

### Dependencies

- Avalonia **12.1.5**, CHDSharp **1.6.3**, and Meziantou.Analyzer **3.0.321**.

## Six bundles

Each versioned bundle contains **both** the desktop app and the headless CLI:

| System | Download |
|---|---|
| Windows x64 | `5do-1.2.2-win-x64.zip` |
| Windows arm64 | `5do-1.2.2-win-arm64.zip` |
| Linux x64 | `5do-1.2.2-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.2.2-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.2.2-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.2.2-osx-arm64.tar.gz` |

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

- Full suite: **1,815 tests passing**.
- `dotnet build` reports 0 warnings and 0 errors; `dotnet format --verify-no-changes`
  is clean.
- Synthetic benchmark gate passes with 0 bytes/frame and p99 within budget.
- CLI `selftest` passes.
- JIT parity: 1,000-frame Road Rash `0x41D2A1DA` and Lemmings `0x110E7DC5`, identical
  instruction counts; 600-frame Lemmings benchmark hash `0x2A53D449`.
- Six bundles build; x64 CLI `selftest` and desktop `--smoke-test` pass; arm64 apphosts
  are architecture-verified as PE ARM64, ELF aarch64, and Mach-O arm64.

## Known issues

- The managed JIT is still slower than the interpreter on real boot/gameplay code
  (~1.25x on Lemmings). The interpreter remains the default and the accuracy oracle.
- CORE-05 threaded DSP remains pending; the DSP is ~14% of frame time and a per-sample
  worker handoff costs more than the DSP work.
- `Crash 'n Burn` (Japan) remains the single known no-video case in the local
  594-disc sweep (593/594 render).
- Linux GPU presentation is preview/best-effort under llvmpipe; the CPU fallback works.
- Display refresh above 60 Hz re-presents the latest frame; frame interpolation is not
  implemented.
- Arm64 bundles are architecture-validated but not executed on arm64 hardware in CI.
- WebAssembly, Android/iOS, and Raspberry Pi are out of scope.

See the [project status](Status.md), [known issues](KnownIssues.md), and
[compatibility notes](Compatibility.md) for details.

## Changes in 1.2.1

- Windows, Linux, and macOS arm64 bundles, built from the same source and packaging
  script as the x64 bundles.
- PE ARM64, ELF aarch64, and Mach-O arm64 apphosts are verified by CI; x64 bundles keep
  their runtime smoke tests.
- No emulator behavior changes; 1.2.1 is the 1.2.0 code with the arm64 matrix added.

## Changes in 1.2.0

5DO 1.2.0 is a feature and accuracy update on top of the documented-compatibility
1.0.0 release and the 1.1.0 maintenance update. It adds a Windows Direct3D 11
presenter, a fuzz harness, display-refresh control, broader disc support, and a
substantial interrupt/DMA/DSP telemetry pass.

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
