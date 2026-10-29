# 5DO Documentation

This directory is the documentation set for **5DO**, a clean-room-style, pure C#/.NET 10
emulator for the Panasonic 3DO Interactive Multiplayer. The desktop app is Avalonia; the
headless tool is `FiveDo.Cli`; the emulation core is split into one assembly per console
subsystem.

**Current snapshot:** `1.0.0-rc.1`, documented-compatibility release scope, 906 tests
passing (verified 2026-10-29). Windows x64 is the validated platform. See
[`Status.md`](Status.md), [`ReleaseChecklist.md`](ReleaseChecklist.md), and
[`KnownIssues.md`](KnownIssues.md) for the honest release position.

---

## Start here

| If you want to... | Read |
|---|---|
| Run the desktop app or CLI | [`UserGuide.md`](UserGuide.md) |
| Understand the current project position | [`Status.md`](Status.md) |
| Build, test, benchmark, or publish | [`BuildAndTest.md`](BuildAndTest.md) |
| Look up every CLI command and option | [`CliReference.md`](CliReference.md) |
| Change settings, paths, or input bindings | [`Configuration.md`](Configuration.md) |
| Understand or inspect save states | [`SaveStates.md`](SaveStates.md) |
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
| [`UserGuide.md`](UserGuide.md) | Quick start, controls, display filters, save states, screenshots, library, settings, audio, logs, CLI overview, troubleshooting. |
| [`Compatibility.md`](Compatibility.md) | BIOS/disc/input compatibility, game-specific fixes, high-resolution mode, compatibility matrix and soak evidence. |
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
| [`Trimming.md`](Trimming.md) | Trim-safe JSON, trimmed CLI/frontend publishes, NativeAOT audit, remaining warnings. |
| [`ImplementationPlan.md`](ImplementationPlan.md) | Full milestone plan, acceptance criteria, risk register, progress log. |
| [`NextSession.md`](NextSession.md) | Handoff notes, ordered next steps, evidence, useful commands. |
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

### "I want to validate or release"

1. [`ReleaseChecklist.md`](ReleaseChecklist.md) - release gates.
2. [`Status.md`](Status.md) - current position and remaining work.
3. [`Performance.md`](Performance.md) - budgets and measured evidence.
4. [`TestCorpus.md`](TestCorpus.md) - real-content commands and hashes.
5. [`ReleaseLegalReview.md`](ReleaseLegalReview.md) and [`Licensing.md`](Licensing.md) - legal gate.
6. [`BuildAndTest.md`](BuildAndTest.md) - CI, publish, packaging.

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
