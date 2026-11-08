# 5DO 1.5.0

**Released:** 2026-11-08

5DO 1.5.0 is an accuracy and presentation update on the 1.4.0 feature release. It moves
HQx 2x/3x/4x to GPU rule-table passes, fixes the last panel-wide video divergence,
tightens CD interrupt and transfer behavior against Opera, and makes the differential
harness report instruction phase and CD cadence directly.

## Highlights

- **GPU HQx 2x/3x/4x.** HQx now runs as a shared rule-table GPU pass in OpenGL/GLES,
  Direct3D 11, and Vulkan. The CPU implementation remains the reference oracle. On the
  RTX 4070, 4K HQx presentation measures **0.14-0.34 ms** per frame and the GPU pass
  **0.09-0.17 ms**, down from **6.82/10.93 ms** CPU-bound.
- **Video-clean differential panel.** The 3DO background is kept as 24-bit RGB through
  the CPU assembler and all GPU palette shaders, and a zero VDL display-list word is a
  stop that retains the previous bitmap. The 50-title panel now matches **50/50 frame
  hashes** at frames 60/120/300 (up from 32/50).
- **Reference field boundary.** `RunFrame` ends on the 263/312-line VDL field boundary
  instead of a rounded ARM-cycle budget, and the harness captures Opera's ARM/CD state
  in the checkpoint's own video callback. The same-frame instruction delta is **11-20**
  at frame 60 and **23-2,223** at frame 120.
- **CD parity.** The Opera poll reset keeps the interrupt enables and POLRE latch, the
  XBUS CD FIQ is level-held, and READ_DATA validates addresses, aborts stale read-ahead,
  publishes status with the first sector, and accumulates sector deadlines like the
  reference. The command-50 boot handoff fell from **+207,707 cycles / +86,511
  instructions** to **-818 / -376**.
- **Diagnostics.** New `--cd-register-trace` and `--clio-read-trace` CLI traces, harness
  `--timer-trace`, instruction-phase and CD-cadence reports, atomic ISO staging, and
  per-title capture errors instead of panel aborts.
- **Launch alias.** The desktop shell accepts `-fullscreen` in addition to `-f` and
  `--fullscreen`; the smoke marker records `windowState=FullScreen`.

## Validation

- Full suite **1,939/1,939** passes in Debug.
- Build: 0 warnings, 0 errors. `dotnet format --verify-no-changes` and
  `jb inspectcode --severity=WARNING` are clean.
- Opt-in Direct3D 11 and Vulkan HQx 2x/3x/4x parity tests pass, as does the WGL/ANGLE
  HQx probe path.
- 50-title differential panel: **50/50 frame hashes**, **0 errors**. The 5-title panel
  matches **5/5** at 300 frames; the 3,000-frame showcase keeps **4/5** (Road Rash
  remains divergent).
- Windows x64 is the validated target. Linux x64 full-suite and CLI/frontend smoke
  validation pass; GPU presentation under llvmpipe/SSH remains preview/best-effort.
  macOS and arm64 bundles are cross-published and architecture-validated.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.5.0-win-x64.zip` |
| Windows arm64 | `5do-1.5.0-win-arm64.zip` |
| Linux x64 | `5do-1.5.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.5.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.5.0-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.5.0-osx-arm64.tar.gz` |

Each bundle contains the framework-dependent single-file CLI and desktop app, the
README/license/notices, `whatsnew.md`, and the release/legal docs. Install the **.NET 10
Runtime** first; the bundles do **not** include the runtime.

See the [full release notes](https://purelogiccode.github.io/5DO/ReleaseNotes-1.5.0.html)
and [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.5.0) for
`SHA256SUMS.txt`.

## Known limits

- CD command cadence is the remaining behavioral differential on the 50-title panel;
  the 3,000-frame showcase still diverges on Road Rash.
- HQx GPU parity is pixel-identical on the tested synthetic probes; unusual drivers may
  still report shader or texture-format limitations and fall back to the unfiltered
  scale path.
- VRR depends on the GPU, driver, monitor, and desktop; 5DO only avoids the blocking swap
  interval.
- The differential panel's frame-hash matches are strong evidence, not a full
  compatibility claim for every title.
