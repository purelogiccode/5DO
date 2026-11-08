# What's new in 5DO 1.5.0

**Released:** 2026-11-08

5DO 1.5.0 is an accuracy and presentation update on the 1.4.0 feature release. It moves
the HQx 2x/3x/4x filters to GPU rule-table passes, fixes the last panel-wide video
divergence, tightens CD interrupt and transfer behavior against the Opera reference, and
makes the differential harness report instruction phase and CD cadence directly.

## Highlights

### GPU HQx 2x/3x/4x

- HQx now runs as a shared rule-table GPU pass in **OpenGL/GLES**, **Direct3D 11**, and
  **Vulkan**. The compact table is generated from the 4DO hq2x/hq3x/hq4x sources and
  checked in.
- The CPU `HqxFilter` remains the reference oracle. Rule-table tests, CPU-parity tests,
  synthetic GPU probes, and the WGL/ANGLE probe cover all three scales.
- On the RTX 4070, 4K HQx presentation measures **0.14-0.34 ms** per frame and the GPU
  pass **0.09-0.17 ms**, down from **6.82/10.93 ms** CPU-bound.

### Video-clean differential panel

- The 3DO background register is **24-bit RGB**. 5DO previously narrowed it to five bits
  and expanded it again, turning the reference's near-black `(1,1,1)` into `(0,0,0)`.
  The CPU assembler and all GPU palette shaders now keep all eight bits per channel.
- A zero VDL display-list word is a **stop** that retains the previous bitmap and control
  word, matching `opera_vdlp.c`. 5DO previously treated it as an entry and blanked the
  last visible scanline.
- The 50-title panel now matches **50/50 frame hashes** at frames 60/120/300 (up from
  32/50) with **0 errors**.

### Reference field boundary and checkpoint sampling

- `RunFrame` now ends on the **263/312-line VDL field boundary** instead of a rounded
  ARM-cycle budget, removing the accumulated per-field instruction-phase drift.
- The differential harness captures Opera's ARM/CD state in the checkpoint's **own video
  callback** and fills the audio hash after that field's audio callback. The same-frame
  retired-instruction delta is **11-20** at frame 60 and **23-2,223** at frame 120.

### CD interrupt and transfer parity

- The Opera CD poll reset keeps the three interrupt enables and the POLRE media latch,
  and the XBUS CD FIQ is **level-held** each clock batch so READ_ID/READ_ERROR
  completions are visible to the guest.
- READ_DATA rejects invalid MSF and past-end transfers, aborts stale read-ahead, publishes
  status with the first queued sector, and accumulates sector deadlines from the previous
  deadline like `cdrom_data_schedule_next`.
- The command-50 boot handoff fell from **+207,707 cycles / +86,511 instructions** to
  **-818 / -376**.

### Tooling and diagnostics

- The differential harness reports instruction-phase (`pcPhaseFrame`,
  `pcPhaseInstructionDelta`) and CD cadence (`cdCadence`) evidence in JSON, Markdown, and
  HTML.
- New CLI traces: `--cd-register-trace` and `--clio-read-trace`; new harness
  `--timer-trace`.
- ISO staging is atomic and retries once after a transient CHD decompression failure. A
  per-title capture failure is reported as an error instead of aborting a panel.

### Desktop launch alias

- The desktop shell accepts `-fullscreen` in addition to `-f` and `--fullscreen`. The
  smoke-test marker records `windowState=FullScreen` when the launch mode was applied.

## Changed

- VDLP save-state section is version 4 and Machine section is version 3. Both store the
  24-bit background and convert packed 5-bit backgrounds from older states.
- `--cd-commands` writes `opcode cycle pc instructions`; the differential harness
  understands the extended form.
- The 1.4.0 online cover art, latency controls, unsigned-disc support, exact NTSC clock,
  and six-bundle packaging model are unchanged.

## Validation

- Full suite **1,939/1,939** passes in Debug.
- `dotnet build` reports **0 warnings and 0 errors**; `dotnet format --verify-no-changes`
  and `jb inspectcode --severity=WARNING` are clean.
- Opt-in Direct3D 11 and Vulkan HQx parity tests pass for 2x/3x/4x, and the opt-in
  WGL/ANGLE GL probe passes the HQx rule-texture path.
- The 5-title panel matches **5/5** frame hashes at 300 frames; the 3,000-frame showcase
  keeps **4/5** (Road Rash remains divergent).
- Windows x64 remains the validated target. Linux x64 full-suite and CLI/frontend smoke
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

Each bundle contains both the desktop app and the CLI. The **.NET 10 Runtime** must be
installed on the host; the bundles are framework-dependent and do **not** contain the
runtime. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.5.0) for
`SHA256SUMS.txt`.

## Known limits

- CD command cadence is the remaining behavioral differential on the 50-title panel; the
  3,000-frame showcase still diverges on Road Rash.
- HQx GPU parity is pixel-identical on the tested synthetic probes; unusual drivers may
  still report shader or texture-format limitations and fall back to the unfiltered scale
  path.
- VRR depends on the GPU, driver, monitor, and desktop; 5DO only avoids the blocking swap
  interval.
- The differential panel's frame-hash matches are strong evidence, not a full
  compatibility claim for every title.
- See [Release notes 1.4.0](ReleaseNotes-1.4.0.md) for the cover-art, latency,
  unsigned-disc, and exact-clock features introduced in the previous release.
