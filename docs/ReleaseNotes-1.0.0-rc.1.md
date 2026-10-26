# 3DOEmulator 1.0.0-rc.1

**Release date:** 2026-10-26
**Status:** release candidate. The local engineering release gates pass; CI and the
distribution/attribution sign-off are still pending. See
[`ReleaseLegalReview.md`](ReleaseLegalReview.md).

This is a **documented-compatibility** release: the emulator is supported on the
validated platform and against the tested title set, with known issues listed below.
Full compatibility with the 3DO library is not claimed and is not a v1 gate.

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
- Headless CLI with `run`, `trace`, `bench`, `selftest`, `info`, `disc`, `fs`,
  `disasm`, `bios`, `state`, and JSON output.
- ISO, BIN, CUE+BIN, and CHD disc images, including parent/child CHDs.

## Supported platforms

| Platform | Status |
|---|---|
| Windows x64 | **Validated** for this release candidate |
| Linux x64 / macOS x64 | Publish outputs are produced; runtime validation is post-1.0 unless separately verified |
| Windows/Linux/macOS arm64, Raspberry Pi 4/5 | Post-1.0 |

## Tested titles

These are the documented statuses for this release candidate. "Boots/menus" means
the title renders and round-trips save states, but gameplay input has not been
scripted end-to-end.

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

See [`Compatibility.md`](Compatibility.md) and [`KnownIssues.md`](KnownIssues.md) for
details and evidence.

## Requirements

- .NET 10 SDK for source builds. The published packages are self-contained.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Windows 10+ for the validated desktop package.
- An OpenGL 3.3 or OpenGL ES 3.0/3.1 context for the GPU presenter; the CPU
  `WriteableBitmap` fallback is used when OpenGL is unavailable.

BIOS ROMs and game images are **not** included.

## Verification

The release checklist is in [`ReleaseChecklist.md`](ReleaseChecklist.md).

- [x] `dotnet build 3DOEmulator.sln -c Release`: 0 warnings, 0 errors.
- [x] `dotnet test tests/ThreeDo.Tests/ThreeDo.Tests.csproj -c Release`: 775/775
      passing.
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
- [x] Portable win-x64 CLI and frontend archives with README/notices/licensing.
- [ ] Legal/attribution sign-off (owner action; see
      [`ReleaseLegalReview.md`](ReleaseLegalReview.md)).

The per-title soak hashes and roundtrip evidence are in
[`Compatibility.md`](Compatibility.md) §10.2.

## Known issues

See [`KnownIssues.md`](KnownIssues.md). The main limitations are:

- Road Rash's full 5.5-mile race is not completed by the autopilot; the race and
  post-race flow are documented.
- Alone in the Dark's inventory interaction is not scripted.
- The seven additional titles boot/render but do not have scripted gameplay input.
- Raspberry Pi/GLES 3.1, arm64 hardware, DPI, Tier B timing, and installers are
  post-1.0.
- The Avalonia compositor may pace presentation independently of the requested
  vsync; renderer cost is reported separately.

## Licensing

This project is a C# port/derivative of FreeDO/4DO and uses Opera/libopera as a
behavioral reference. FreeDO/4DO are not standard OSI-licensed projects; their
modified LGPL includes a non-commercial restriction. Read
[`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md) and
[`Licensing.md`](Licensing.md) before distributing builds.

"3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. This
project is not affiliated with or endorsed by them.

## Artifacts

The win-x64 release artifacts are:

- `3doemulator-cli-win-x64.zip`
- `3doemulator-frontend-win-x64.zip`

Sizes and SHA-256 checksums are recorded in `SHA256SUMS.txt` next to the packages.
Each archive contains the executable, `README.md`, `THIRD-PARTY-NOTICES.md`,
`docs/Licensing.md`, `docs/ReleaseLegalReview.md`, these release notes, and
`PACKAGE.txt`.
