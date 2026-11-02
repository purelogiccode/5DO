# 5DO 1.0.0

**Release date:** 2026-11-02 (GitHub release `v1.0.0`)
**Status:** v1.0.0 documented-compatibility release, published on GitHub. The Windows x64
engineering gates pass; Linux x64 full test suite and CLI/frontend smoke passed, with
GPU presentation documented as preview/best-effort. One unified bundle per system
(Windows, Linux, macOS x64) and SHA-256 checksums are attached to the GitHub release.
See [`ReleaseChecklist.md`](ReleaseChecklist.md) and [`WhatsNew.md`](WhatsNew.md).

This is a **documented-compatibility** release: the emulator is supported on the
validated platform and against the tested title set, with known issues listed below.
Full compatibility with the 3DO library is not claimed and is not a v1 gate. M13.5/M13.6
visual/high-resolution review and M14 high-resolution CEL research are post-1.0.

---

## Highlights

- Pure C# / .NET 10 3DO emulator with an ARM60 interpreter, 3 MB RAM / 2 MB ROM,
  NVRAM, CLIO, MADAM CEL and matrix engines, VDLP, SPORT, DSP, Quartz, XBUS, and
  CD-ROM emulation.
- Boots the tested BIOS and the 12 titles in the compatibility matrix below.
- Deterministic save states with JSON metadata and PNG thumbnails.
- Avalonia desktop shell with game library, settings, keyboard/gamepad mapping,
  ten save slots, screenshots, fullscreen, logs, and first-run guide.
- OpenGL/GLES two-stage presenter with accurate, nearest, sharp-bilinear, CRT, xBR,
  and HQx filters, CPU fallback, and 4K output support.
- Optional Vulkan 1.0 backend with build-time SPIR-V shaders,
  accurate/nearest/sharp-bilinear/CRT/xBR/FSR/HQx modes, an embedded Win32 child
  surface for the desktop path, CPU readback fallback, a headless `--vk-probe`, and a
  native Win32 swapchain probe (`--vk-window-probe`). It matches the CPU oracle at
  320x240 (`0xD6BD89C5`) and measures 0.24 ms mean / 1.06 ms p99 present at 4K
  sharp-bilinear, 0.32/0.80 ms at 4K xBR, 0.60/1.30 ms at 4K FSR, and 6.82/10.93 ms at
  4K HQx 2x (CPU-bound) on the RTX 4070. OpenGL remains the required desktop presenter;
  see [`Vulkan.md`](Vulkan.md).
- 44.1 kHz stereo DSP audio with an exact sample clock, bundled OpenAL Soft live
  output, audio-driven frame pacing, and WAV capture/analysis
  (`audio analyze <wav> --json`).
- Headless CLI with `run`, `trace`, `bench`, `selftest`, `info`, `disc`, `fs`,
  `audio`, `disasm`, `bios`, `state`, and JSON output.
- ISO, BIN, CUE+BIN, and CHD disc images, including parent/child CHDs.

## Supported platforms

| Platform | Status |
|---|---|
| Windows x64 | **Validated** for v1.0 |
| Linux x64 | Full test suite and CLI/frontend smoke passed; GPU presentation preview/best-effort |
| Linux/macOS arm64, macOS x64, Raspberry Pi 4/5 | Post-1.0 |

## Tested titles

These are the documented statuses for v1.0. "Boots/menus" means the title renders and
round-trips save states, but gameplay input has not been scripted end-to-end.

| Title | Status |
|---|---|
| Wolfenstein 3D (USA) | Playable: fresh game, movement/fire, save/load |
| Lemmings (USA) | Playable: passcode path, gameplay start, save/load |
| Road Rash (USA) | Playable race; full-race completion is a known limitation |
| Alone in the Dark (USA) | Playable room/menu/intro; inventory not scripted |
| Space Ace (USA) | Playable interactive scene start |
| Gex | Boots/attract scene; input not scripted |
| Need for Speed | Boots/menus; input not scripted |
| Super Street Fighter II Turbo | Boots/attract match; input not scripted |
| Total Eclipse | Boots/attract gameplay; input not scripted |
| Killing Time | Boots/title menu; input not scripted |
| Theme Park | Boots/title menu; captured menu audio is silent |
| Return Fire | Boots/menu; input not scripted |

The full local collection was loadability-tested: **594 discs, 593 rendered, 1 known
boot failure (`Crash 'n Burn` Japan), 0 not loadable**. See
[`Compatibility.md`](Compatibility.md), [`KnownIssues.md`](KnownIssues.md), and
[`GameCompatibilityResults.md`](GameCompatibilityResults.md).

## Requirements

- The **.NET 10 Runtime** must be installed on the host; the published bundles are
  framework-dependent and do **not** include the runtime. The .NET 10 SDK is only
  needed for source builds.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Windows 10+ for the validated desktop bundle.
- An OpenGL 3.3 or OpenGL ES 3.0/3.1 context for the GPU presenter; the CPU
  `WriteableBitmap` fallback is used when OpenGL is unavailable.

BIOS ROMs and game images are **not** included.

## Verification

The release checklist is in [`ReleaseChecklist.md`](ReleaseChecklist.md).

- [x] `dotnet build 5DO.sln -c Release`: 0 warnings, 0 errors.
- [x] `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`: 1,473/1,473
      passing.
- [x] `dotnet format 5DO.sln --verify-no-changes`: clean.
- [x] Synthetic benchmark budget gate: 2.28 ms/frame core mean, 4.03 ms core p99,
      4.66 ms total p99, 0 managed bytes/frame.
- [x] Trimmed win-x64 CLI and frontend smoke tests (`selftest`, `bench`, `info`,
      300-frame disc run, `--smoke-test`).
- [x] Six fresh 36,000-frame soaks and seven archived-state 36,000-frame soaks; all
      exited 0 with the DSP running.
- [x] Save/load roundtrip on the showcase titles: zero-frame hash matches and two
      120-frame continuations are identical.
- [x] WGL and ANGLE/GLES 3.0 Accurate parity: 0 different pixels; 0 renderer
      allocations per frame.
- [x] Unified win-x64/linux-x64/osx-x64 bundles with README/notices/licensing/
      whatsnew; no bundled .NET runtime, PDB, or XML docs.
- [x] Linux x64 validation: full test suite **1,473/1,473**, `selftest`, `bench`, `info`,
      `bios info`, a 3,000-frame Wolfenstein boot, non-silent audio, and the frontend
      `--smoke-test` on Linux Mint 22.3. The GL probe timed out under llvmpipe, so GPU
      presentation is preview/best-effort.
- [x] Legal/attribution sign-off: FreeDO modified GNU LGPL, donationware (2026-11-02).
- [x] Tag `v1.0.0` and GitHub release created; unified win-x64/linux-x64/osx-x64
      bundles and SHA-256 checksums attached.

The per-title soak hashes and roundtrip evidence are in
[`Compatibility.md`](Compatibility.md) §10.2.

## Known issues

See [`KnownIssues.md`](KnownIssues.md). The main limitations are:

- Crash 'n Burn (Japan) remains the only known boot failure in the 594-disc local
  collection.
- Road Rash's full 5.5-mile race is not completed by the autopilot; the race and
  post-race flow are documented.
- Alone in the Dark's inventory interaction is not scripted.
- The seven additional titles boot/render but do not have scripted gameplay input.
- M13.5/M13.6 visual/high-resolution review and M14 high-resolution CEL research are
  post-1.0.
- Raspberry Pi/GLES 3.1, arm64 hardware, macOS validation, DPI, Tier B timing, and
  installers are post-1.0.
- The Avalonia compositor may pace presentation independently of the requested
  vsync; renderer cost is reported separately.

## Licensing

This project is a C# port/derivative of FreeDO/4DO and uses Opera/libopera as a
behavioral reference. It is distributed under the FreeDO modified GNU LGPL; see
[`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE). 5DO is
**donationware** (non-commercial, source-available; voluntary donations only).
FreeDO/4DO are not standard OSI-licensed projects; their modified LGPL includes a
non-commercial restriction. Read
[`THIRD-PARTY-NOTICES.md`](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md)
and [`Licensing.md`](Licensing.md) before distributing builds.

"3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. This
project is not affiliated with or endorsed by them.

## Artifacts

The v1.0.0 release artifacts are attached to the GitHub release:

- `5do-win-x64.zip`
- `5do-linux-x64.tar.gz`
- `5do-osx-x64.tar.gz`

Sizes and SHA-256 checksums are recorded in `SHA256SUMS.txt` next to the bundles.

Each bundle contains both the desktop app and the CLI, `README.md`, `LICENSE`,
`THIRD-PARTY-NOTICES.md`, `whatsnew.md`, `docs/Licensing.md`,
`docs/ReleaseLegalReview.md`, `docs/OpenALSoft.md`,
`docs/OpenAL-Soft-COPYING.txt`, these release notes, and `PACKAGE.txt`.

The apps are **framework-dependent single-file executables** and require the .NET 10
runtime on the host. No .NET runtime is embedded in the executables or copied into the
bundle. PDB and XML documentation files are omitted because they are build outputs, not
runtime dependencies. Native libraries (OpenAL Soft, Skia, and ANGLE) stay beside the
apps so the LGPL replacement rights remain practical.
