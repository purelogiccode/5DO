# 5DO Documentation

This directory is the documentation set for **5DO**, a clean-room-style, pure C#/.NET 10
emulator for the Panasonic 3DO Interactive Multiplayer. The desktop app is Avalonia; the
headless tool is `FiveDo.Cli`; the emulation core is split into one assembly per console
subsystem.

**Current snapshot:** `1.0.0-rc.1` source tree, 1,459 tests passing (2026-11-01), with
the v1.0 release deliberately deferred. The first full-collection loadability sweep is
complete: **594 discs tested, 576 rendered, 13 black/blank, 5 not loadable**. The
2026-10-31 performance pass brings the Road Rash 3D race to 4.20 ms/frame (p99 7.40 ms),
keeps Debug core builds optimized, and adds Auto presentation frame skip. The
2026-11-01 rewind/fast-forward pass adds deterministic 30-300 second rewind and
2x-10x turbo playback, the cheat-engine pass adds per-game RAM cheats with a
desktop manager and CLI, the TAS pass adds deterministic `.5domovie`
record/replay/verify with branching, and the disc-browser pass adds read-only
Opera file-system browsing, extraction, and a disc-info dialog. The active work is triage/in-game review (M13) and
high-resolution CEL research (M14). The project is donationware: non-commercial,
source-available, and funded by voluntary donations.
Windows x64 is the validated platform; the local Linux validation target is the
`LinuxMint` Hyper-V VM. See [`Status.md`](Status.md),
[`CompatibilitySweep.md`](CompatibilitySweep.md),
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md), and
[`KnownIssues.md`](KnownIssues.md).

---

## Start here

| If you want to... | Read |
|---|---|
| Run the desktop app or CLI | [`UserGuide.md`](UserGuide.md) |
| Understand the current project position | [`Status.md`](Status.md) |
| Build, test, benchmark, or publish | [`BuildAndTest.md`](BuildAndTest.md) |
| Look up every CLI command and option | [`CliReference.md`](CliReference.md) |
| Change settings, paths, or input bindings | [`Configuration.md`](Configuration.md) |
| Manage BIOS and NVRAM images | [`UserGuide.md`](UserGuide.md), [`Configuration.md`](Configuration.md) |
| Understand or inspect save states | [`SaveStates.md`](SaveStates.md) |
| Create or manage per-game cheats | [`Cheats.md`](Cheats.md) |
| Record, replay, verify, or branch a TAS movie | [`Tas.md`](Tas.md) |
| Work on audio or diagnose audio problems | [`Audio.md`](Audio.md), [`Troubleshooting.md`](Troubleshooting.md) |
| Work on video, filters, the OpenGL presenter, or the optional Vulkan backend | [`Video.md`](Video.md), [`Vulkan.md`](Vulkan.md) |
| Use traces, dumps, probes, and diagnostics | [`Diagnostics.md`](Diagnostics.md) |
| Understand the source layout and add a feature | [`DeveloperGuide.md`](DeveloperGuide.md) |
| Diagnose a failure | [`Troubleshooting.md`](Troubleshooting.md) |
| Check a term or acronym | [`Glossary.md`](Glossary.md) |

---

## Document map

### User and compatibility

| Document | Contents |
|---|---|
| [`UserGuide.md`](UserGuide.md) | Quick start, controls, display filters, save states, screenshots, library, settings, audio, logs, cheats, CLI overview, troubleshooting. |
| [`Cheats.md`](Cheats.md) | Per-game cheat files, raw/typed/conditional code syntax, CLI commands, desktop manager, and legal scope. |
| [`Tas.md`](Tas.md) | `.5domovie` container, deterministic record/replay/verify, CLI commands, desktop controls, branching, and current limits. |
| [`Compatibility.md`](Compatibility.md) | BIOS/disc/input compatibility, game-specific fixes, high-resolution mode, compatibility matrix and soak evidence. |
| [`GameCompatibilityList.md`](GameCompatibilityList.md) | 241-title 4DO reference list, local collection matches, and the working 5DO status columns. |
| [`GameCompatibilityResults.md`](GameCompatibilityResults.md) | Automated 594-disc loadability results: 576 rendered, 13 black/blank, 5 not loadable, with per-disc evidence paths. |
| [`CompatibilitySweep.md`](CompatibilitySweep.md) | Full-collection loadability/in-game sweep procedure, status definitions, triage, and Linux VM validation. |
| [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md) | True internal high-resolution CEL research: current mode, options, experiments, and decision criteria. |
| [`KnownIssues.md`](KnownIssues.md) | Current game, renderer, audio, packaging, emulation, and legal limitations. |
| [`Troubleshooting.md`](Troubleshooting.md) | Symptom-driven diagnosis with commands, log sources, and fixes. |
| [`Glossary.md`](Glossary.md) | 3DO hardware, emulator, renderer, audio, and release terminology. |
| [`ReleaseNotes-1.0.0-rc.1.md`](ReleaseNotes-1.0.0-rc.1.md) | Release-candidate highlights, supported platforms, tested titles, verification, and artifacts. |

### Reference

| Document | Contents |
|---|---|
| [`CliReference.md`](CliReference.md) | Every CLI command, option, default, exit code, JSON response, and diagnostic file format. |
| [`Configuration.md`](Configuration.md) | `settings.json` schema, per-game overrides, input profiles, NVRAM, paths, environment variables, precedence. |
| [`SaveStates.md`](SaveStates.md) | `.tdos` binary layout, section table, validation rules, slot layout, roundtrip workflow, compatibility. |
| [`Audio.md`](Audio.md) | DSP clock, 44.1 kHz pipeline, OpenAL output, soft limiter, WAV capture, audio tap, analysis, pacing. |
| [`Video.md`](Video.md) | VDLP/frame assembly, CPU oracle, OpenGL/GLES two-stage pipeline, every filter including FSR, backends, probes, screenshots. |
| [`Vulkan.md`](Vulkan.md) | Optional offscreen Vulkan backend: SPIR-V pipeline, filters, probe, measured results, and limitations. |
| [`Diagnostics.md`](Diagnostics.md) | Logs, CPU/DSP/CD/CLIO/CEL/PBUS traces, watchpoints, dumps, KPRINT, input scripts, audio tap, GPU probe, benchmark. |

### Developer and maintainer

| Document | Contents |
|---|---|
| [`Architecture.md`](Architecture.md) | Design goals, dependency rules, runtime composition, address map, frame execution, video/audio/input pipelines, state ownership, threading. |
| [`DeveloperGuide.md`](DeveloperGuide.md) | Source tree and project catalog, key types, invariants, extension points, coding conventions, debugging entry points, architectural debt. |
| [`BuildAndTest.md`](BuildAndTest.md) | Prerequisites, build/test/format commands, test layers and environment variables, benchmarks, GPU probe, publish/trim/AOT, CI jobs. |
| [`TestStrategy.md`](TestStrategy.md) | Test pyramid, actual test layout, unit/integration/golden/differential/fuzz/performance strategy, CI policy. |
| [`PortingNotes.md`](PortingNotes.md) | C++ to C# rules, memory model, bitfields, integer/float semantics, function-by-function reference notes. |
| [`ReferenceStrategy.md`](ReferenceStrategy.md) | Opera/libopera as primary behavioral reference, FreeDO/4DO hierarchy, differential procedure. |
| [`DifferentialHarness.md`](DifferentialHarness.md) | 5DO vs OperaCore manifests, checkpoint hashes, reports, allowlists, baseline gate, and current limitations. |
| [`Trimming.md`](Trimming.md) | Trim-safe JSON, trimmed CLI/frontend publishes, NativeAOT audit, remaining warnings. |
| [`ImplementationPlan.md`](ImplementationPlan.md) | **Single source of truth:** master checklist, current status, top next actions, full feature backlog, milestones, acceptance criteria, risk register, Opera parity audit, evidence, progress log. |
| [`NextSession.md`](NextSession.md) | Redirect only; the handoff now lives in `ImplementationPlan.md` Part IV. |
| [`TestCorpus.md`](TestCorpus.md) | Machine-specific BIOS/disc corpus, per-title hashes, real-content validation commands. |

### Release and legal

| Document | Contents |
|---|---|
| [`Status.md`](Status.md) | Current position, completion estimate, release path, reproduce commands. |
| [`Performance.md`](Performance.md) | 4K60 budgets, presentation pipeline, measured baselines, CI gates, remaining gaps. |
| [`ReleaseChecklist.md`](ReleaseChecklist.md) | v1.0 scope, hard gates, packaging, sign-off. |
| [`ReleaseLegalReview.md`](ReleaseLegalReview.md) | Attribution audit and remaining distribution decisions. |
| [`OpenALSoft.md`](OpenALSoft.md) | OpenAL Soft LGPL notice, source offer, relinking/replacement rights, and packaged files. |
| [`Licensing.md`](Licensing.md) | FreeDO/4DO/Opera licensing, third-party dependencies, trademarks. |
| [`../LICENSE`](../LICENSE) | Project license: FreeDO modified GNU LGPL. |
| [`../THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md) | Required attribution text for distributed builds. |
| [`../CHANGELOG.md`](../CHANGELOG.md) | User-visible changes by release. |

---

## Reading paths

### "I just want to play"

1. [`UserGuide.md`](UserGuide.md) - install, open BIOS/disc, controls, filters, saves.
2. [`Compatibility.md`](Compatibility.md) - supported BIOS/disc formats and tested titles.
3. [`KnownIssues.md`](KnownIssues.md) - what is not expected to work yet.
4. [`Troubleshooting.md`](Troubleshooting.md) - when something fails.

### "I want to contribute code"

1. [`DeveloperGuide.md`](DeveloperGuide.md) - source map and invariants.
2. [`Architecture.md`](Architecture.md) - dependency rules and runtime composition.
3. [`BuildAndTest.md`](BuildAndTest.md) - build/test/format/publish.
4. [`TestStrategy.md`](TestStrategy.md) - what a change must prove.
5. [`PortingNotes.md`](PortingNotes.md) and [`ReferenceStrategy.md`](ReferenceStrategy.md) - reference behavior.
6. [`Trimming.md`](Trimming.md) - keep the trimmed/AOT paths healthy.

### "I want to validate compatibility or release"

1. [`CompatibilitySweep.md`](CompatibilitySweep.md) - current collection-sweep procedure.
2. [`GameCompatibilityList.md`](GameCompatibilityList.md) - per-title reference and results.
3. [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md) - high-res research plan.
4. [`ReleaseChecklist.md`](ReleaseChecklist.md) - eventual release gates.
5. [`Status.md`](Status.md) - current position and remaining work.
6. [`Performance.md`](Performance.md) - budgets and measured evidence.
7. [`TestCorpus.md`](TestCorpus.md) - real-content commands and hashes.
8. [`ReleaseLegalReview.md`](ReleaseLegalReview.md) and [`Licensing.md`](Licensing.md) - legal gate.
9. [`BuildAndTest.md`](BuildAndTest.md) - CI, publish, packaging.

### "I need to debug an emulation problem"

1. [`Diagnostics.md`](Diagnostics.md) - choose a trace, dump, or probe.
2. [`CliReference.md`](CliReference.md) - exact options and output fields.
3. [`Troubleshooting.md`](Troubleshooting.md) - symptom-to-cause checklist.
4. [`TestCorpus.md`](TestCorpus.md) - known hashes and reproduction commands.

---

## Documentation conventions

- **Commands** are shown from the repository root. PowerShell examples use backticks for
  line continuation; shell examples use `\`.
- **Placeholders** are written as `<path>`, `<disc>`, `<bios>`, `N`, or `0x...`.
- **Hex values** are written with a `0x` prefix unless a field is explicitly decimal.
- **"Implemented"** means the code path exists and is covered by at least one test or a
  recorded real-content run. **"Validated"** means it has been exercised on the stated
  hardware/content and the evidence is recorded in this repository.
- **Dates** are ISO-8601 (`YYYY-MM-DD`) and describe when evidence was recorded, not
  when the document was last edited.
- **Paths** use forward slashes in prose and native separators in platform-specific
  commands.
- **No copyrighted content:** this documentation never includes BIOS ROMs, game images,
  or extracted assets. Hashes and frame counts are facts, not assets.

---

## Keeping documentation current

When behavior changes:

1. Update the user-visible doc (`UserGuide.md`, `CliReference.md`, `Configuration.md`,
   or `SaveStates.md`).
2. Update the implementation-facing doc (`Architecture.md`, `DeveloperGuide.md`,
   `Audio.md`, or `Video.md`).
3. Update [`CHANGELOG.md`](../CHANGELOG.md) for user-visible changes.
4. Update [`Status.md`](Status.md) / [`KnownIssues.md`](KnownIssues.md) when the release
   position changes.
5. Record new evidence in [`TestCorpus.md`](TestCorpus.md) or
   [`Compatibility.md`](Compatibility.md) instead of claiming it in prose.
6. Run `dotnet test` and, for docs that cite numbers, record the verified count or
   measurement rather than copying an old value.

The source code is the final authority. If a document and the code disagree, treat the
code as correct, then fix the document in the same change.
