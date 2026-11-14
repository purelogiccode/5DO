# 5DO — Consolidated Implementation Plan

**Snapshot:** 2026-11-13
**Release:** v1.8.0
**Status:** the compatibility program is complete. This file is the single actionable plan;
it tracks only **achievable post-1.8 work**.
**Merged from:** the former active plan and
[ImplementationPlanArchive.md](ImplementationPlanArchive.md). The archive remains the raw
evidence store, completed log, stable-ID index, and appendices.

> **Filter rule.** An item belongs in §3 only when it can be completed and evidenced with
> the current 378-title corpus, the current codebase, and obtainable hardware or CI. Items
> blocked by licensing, missing hardware, unavailable reference captures, or open-ended
> research are listed in §4 and are **not** checkboxes.
>
> **Evidence rule.** A task is complete only when its **Done when** line is satisfied and
> the capture, hash, frame count, BIOS/disc identity, test, or release artifact is committed.

---

## 1. Completed baseline

### 1.1 Compatibility definition (retained)

A title is **Playable** when all four are true:

1. It boots from its canonical image with a supported BIOS.
2. It renders at least one non-black frame.
3. A standard input script reaches a distinct interactive or content screen beyond the
   title/attract loop.
4. It does not hang or crash before that screen is captured.

Deep gameplay, ending validation, full-game completion, and frame-perfect peer parity are
not required. Claims are measured on the **378 unique games**; the remaining images are
loadability and duplicate coverage.

### 1.2 Results

| Area | Completed result | Evidence |
|---|---|---|
| Corpus | 1,371 images → 414 unique titles → **378 game test set** | `artifacts/corpus/inventory.json`, `docs/data/unique-corpus.csv` |
| 5DO playability | **375 / 378 Playable**; 2 `Title-only`; 1 `No video` | `docs/data/beyond-title-results.csv`, [BeyondTitleEvidence.md](BeyondTitleEvidence.md) |
| Peer comparison | **0 `Behind`**, **0 `Regression`**; strict superset of the **252-title** peer union (4DO **167**, Phoenix **233**) | [CompatibilityComparison.md](CompatibilityComparison.md) |
| Quality ledger | **101 rows: 91 `Good`, 10 `Playable`**; seven are media/reference/add-on discs, three are game leads (DeathKeep, Jurassic Park, Kingdom) | `docs/data/playability-grades.csv`, `playability-grades-baseline.json` |
| Accuracy panel | **50/50 frame hashes**, **150/150 audio fields** at frames 60/120/300 | [DifferentialHarness.md](DifferentialHarness.md) §8 |
| Long-run sample | 4/5 titles match at 3,000 frames; Road Rash first diverges at frame 600 | `artifacts/diff/roadrash-acc1-timers` |
| Release | **v1.8.0** six framework-dependent bundles (macOS as DMG) and `SHA256SUMS.txt`; MMPX 2x, bicubic, wider aspect presets, and the .NET 11 / C# 15 toolchain | [GitHub release](https://github.com/purelogiccode/5DO/releases/tag/v1.8.0) |
| Build gate | Debug build, full test suite, format, InspectCode, docs checks green | [Status.md](Status.md), [ReleaseChecklist.md](ReleaseChecklist.md) |
| Regression gates | Weekly beyond-title baseline and quality-ledger gate run unattended | `.github/workflows/compat-baseline.yml` |
| Platform | Windows x64 validated; macOS x64/arm64, Linux arm64, and Windows arm64 native runtime smoke passed; Linux GPU presentation remains preview/best-effort | [Compatibility.md](Compatibility.md) §10.3, [KnownIssues.md](KnownIssues.md) |
| Localization | English/German UI catalogs; `--locale`/`--list-locales`; translated smoke marker `firstMenuHeader=Datei` | [Localization.md](Localization.md), [TestCorpus.md](TestCorpus.md) 1.1bw |
| Metadata | External metadata lookup with deterministic cache and manual media attachments | [Metadata.md](Metadata.md), [TestCorpus.md](TestCorpus.md) 1.1bx |

The remaining non-playable rows are three non-retail/add-on images: `Joypad Test v1.0`
(static pad card), `LameNES Emulator` (minimal “HELLO, WORLD!” frame), and the standalone
*Return Fire – Maps O' Death* data disc (static box-art card).

### 1.3 Completed stable IDs

The compatibility and release plan is closed: **COMPAT-01 … COMPAT-16**, **REL-01 …
REL-03**, **QUAL-01 … QUAL-04**, **ACC-01**, **ACC-02**, **ACC-03**, **PLAT-01**,
**PLAT-03**, **PLAT-04**, **PLAT-07**, **PLAT-09**, **TEST-06/07/08**,
**CORE-11/16**, **FND-05**, **ECO-04**, **ENG-01/09**, and the shipped
**SYS-06/07/08/12/17/18/22**, **UX-09**, **UX-25**, **UX-26**, **UX-28**, **UX-29**,
**UX-30**, **UX-31**, **UX-32**, **VID-04/12/16/17/18** items. `SYS-21` and `SYS-23` are code-complete but remain open
for their named hardware evidence.

`ACC-01` and `ACC-02` are closed as documented residuals; their revisit conditions are in
§4. `QUAL-03` and `QUAL-04` closed the first quality pass. The follow-on scripts
`QUAL-03A`…`QUAL-03D` and `QUAL-05A` are now complete; `QUAL-05B` (the full Road Rash
race) is blocked in §4. The full completed-ID index, milestone aliases, old gates, and
dated evidence entries are in
[ImplementationPlanArchive.md](ImplementationPlanArchive.md). Do not reopen a completed ID
without new measurement.

---

## 2. How the remaining backlog works

- **Priority:** **P0** player-visible quality/evidence, **P1** release/platform hardening,
  **P2** accuracy features, **P3** product features.
- **Effort:** **S** under 3 developer-days, **M** 3–10, **L** 10–30, **XL** 30–90+.
- **One feature per change.** Do not mix accuracy, rendering, and UX work in one change.
- **One root cause per fix.** Every fix starts from a failing title, test, or panel row.
- **No speculative work.** If a task cannot change a status, a gate, or a release artifact,
  it does not belong in P0/P1.
- **No duplicate entries.** Reference an ID instead of restating the same work.
- **Blocked items stay blocked.** Moving an item from §4 to §3 requires the named blocking
  condition to be resolved first.

---

## 3. Missing features — achievable backlog

### 3.1 P0 — Quality closure

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **QUAL-03A** | **Done** | M | **BladeForce**: reach an interactive mission from `START NEW GAME` beyond the intro/cutscene loop. | The reference reaches the same Main Options menu; the remaining gap was a title-specific input path, not a proven core defect. | Reached the Training Mission, Recon Maps, and Supply Depot mission at 70,000/80,000/90,000 frames; promoted to `Good`. Evidence: [TestCorpus.md](TestCorpus.md) 1.1bp. |
| **QUAL-03B** | **Done** | M | **Jurassic Park Interactive**: break the Operations hub / pause-menu session loop and open a terminal or gameplay scene. | Input scripting and save states already exist; no core defect was found in triage. | Opened **Security Systems / Security Tracking** at 42,000/52,000 frames. No in-park gameplay yet; stays `Playable`. Evidence: [TestCorpus.md](TestCorpus.md) 1.1bp. |
| **QUAL-03C** | **Done** | S | **Off-world Interceptor**: pass the pre-mission shop into a mission. | Menu navigation only; no core defect is known. | Launched the first mission at 12,000 frames; 3D vehicle/HUD gameplay through 16,000; promoted to `Good`. Evidence: [TestCorpus.md](TestCorpus.md) 1.1bp. |
| **QUAL-03D** | **Done** | S | **PGA Tour 96**: break the Golf Bag → Pro Info cycle and start a round. | The game already reaches its menus; the gap was script/state sequencing. | Reached **Hole 1** at 64,000 frames; golf gameplay through 80,000; promoted to `Good`. Evidence: [TestCorpus.md](TestCorpus.md) 1.1bp. |
| **QUAL-05A** | **Done** | M | **Alone in the Dark long-play evidence**: extend the room sequence through exploration and the interaction menu. | The intro, save states, and scripted input already exist; this is script tuning and evidence capture. | Room exploration at 30,000-35,000 frames and the **Actions** menu (`Fight`/`Open/Search`/`Close`/`Push`) are scripted; the monster encounter is visible at 34,000. Evidence: [TestCorpus.md](TestCorpus.md) 1.1bp. |

`QUAL-05B` (the full 5.5-mile Road Rash race) moved to §4. The current autopilot still
ends with the rider off the bike after 150,000 frames; it is a driving/start-line and
traffic-avoidance limit, not the old CD wait.

The two reference-limited leads are **not** in this backlog: **DeathKeep** (Start Game is
black on both 5DO and OperaCore) and **Kingdom** (intro returns to the title on both
engines) have no known input path to gameplay. They stay documented until a reference
capture proves a reachable scene; see §4.

### 3.2 P0 — Evidence and validation

| ID | Status | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|---|
| **AUD-01** | **Done** | S | Run a 10-minute real-time audio soak on Windows and Linux with the null device and one real device; confirm zero dropped samples and zero underruns. | The counters, sync offset, and device-recreation tests already exist; only the platform soak was missing. | Windows: null and real devices pass with 0 drops/0 underruns (600.6 s each). Linux: null driver passes with 0 drops/0 underruns (600.6 s). The VM has no PCM playback endpoint, so a Linux real-device soak is a documented hardware limitation. Evidence: [Audio.md](Audio.md) §9. |

The quality-ledger gate remains enforced after this work:
`tools/verify-playability-grades.py` and `compat-baseline.yml` require a dated batch entry
and a linked capture for every promotion or documented limit.

### 3.3 P1 — Platform validation

| ID | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|
| **PLAT-02** | S | **Linux x64 GPU presentation**: run the GL probe on a real Linux GPU host, not llvmpipe over SSH; record presenter results. | The renderer and probe already exist; a GPU-capable Linux host or VM is the only missing resource. | A dated probe result is recorded in [Compatibility.md](Compatibility.md) §10.3; Linux GPU status moves from preview/best-effort to validated or a precise limitation. |
| **PLAT-03 — Done** | M | **macOS runtime validation**: execute the x64 and arm64 bundles on macOS and run CLI/frontend smoke. | Hosted macOS runners run both bundles natively. | Both bundles start, run `selftest`, and pass the frontend `--smoke-test`; results are recorded. Evidence: `platform-runtime.yml` run 42770783778 (2026-11-10), [Compatibility.md](Compatibility.md) §10.3. |
| **PLAT-04 — Done** | M | **Windows/Linux arm64 hardware validation**: run the bundles on arm64 hosts or CI runners. | Hosted arm64 runners run both bundles natively. | CLI and frontend smoke pass on both arm64 targets; results are recorded. Evidence: `platform-runtime.yml` run 42770783778 (2026-11-10). |
| **PLAT-05** | S | **macOS gamepad backend**: physical detection for the new IOKit HID source. The source and pure mapping layer are implemented; 21 mapping/profile tests pass. | The remaining check needs a macOS host with a connected controller. | A macOS gamepad is detected and mapped through the existing eight-player profile; the mapping is already unit-tested. |
| **PLAT-06 — Partial (documented)** | M | **GLES 3.1, DPI 100/150/200%, and Tier B timing validation**: complete the missing presentation/performance matrix. | The renderer and benchmark harness exist; the work is measurement on available hardware. | Results are recorded in [Performance.md](Performance.md) §8.8, with every residual labeled `validated` or `best-effort`. DPI math is validated by `PixelSizeTests` at 100/125/150/200%; GLES 3.0/ANGLE and 4K WGL are validated; real GLES 3.1 hardware and a real Tier B device remain best-effort. |
| **PLAT-07** | S | **Done (2026-11-10).** Frontend NativeAOT audio validation: both 600 s soaks report 0 drops and 0 underruns. | CLI AOT and trimmed IL audio were already validated; the frontend AOT path is now validated too. | Evidence recorded in [Audio.md](Audio.md) §9. |
| **PLAT-08** | L | **Vulkan non-Windows surfaces**: add X11/Wayland/Metal presentation and validate non-Windows Vulkan devices. | The Vulkan renderer and offscreen/readback path already exist; the surface layer and device validation are missing. | X11/Wayland/Metal surfaces present correctly or are documented as unsupported; non-Windows device results are recorded. |
| **PLAT-09** | M | **Done (2026-11-10).** Full PAL library validation: eight PAL-sensitive titles captured with the documented PAL BIOS, plus a PAL2 presentation probe. | PAL1/PAL2, 50 Hz pacing, and per-game BIOS selection are implemented. | Evidence: [PalValidation.md](PalValidation.md) and `docs/data/pal-validation.csv`. |

`PLAT-05` code is complete: `MacOsGamepadSource` uses IOKit HID Manager for Generic
Desktop joystick/gamepad/multi-axis devices, and `MacOsHidGamepadMapper` plus
`MacOsGamepadMapperTests` cover face buttons, sticks, triggers, hats, button-only
d-pads, and the default eight-player profile. Only physical detection on a macOS host
remains. `PLAT-07` is closed: the NativeAOT frontend ran the real machine/CoreRunner/
OpenAL path for 600 s on the null device and on the NVIDIA playback endpoint, both with
0 dropped samples and 0 underruns ([Audio.md](Audio.md) §9). `PLAT-09` is closed:
all eight Europe-only/PAL-sensitive titles produced dated PAL1 captures with
`panafz10e-anvil.bin`, and a Battle Chess PAL2 probe rendered 384x288 at 50 Hz
([PalValidation.md](PalValidation.md)). `PLAT-03` and `PLAT-04` are closed by
`.github/workflows/platform-runtime.yml`: macOS x64, macOS arm64, Linux arm64, and
Windows arm64 each publish the framework-dependent CLI/frontend, run `selftest`, and
pass the frontend `--smoke-test` (run 42770783778, 2026-11-10).

### 3.4 P1 — Release and packaging

| ID | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|
| **UX-16** | L | **Installer/package scaffolding (partial)**: Windows Inno Setup installer + associations, Linux `.deb`/`.rpm` + desktop entry/MIME, macOS `.app`/`.dmg`/`.pkg`. Signing/notarization remains conditional. | The unsigned Windows installer built, installed, launched, and association-checked; Linux packages built and metadata/MIME-validated; macOS `.app`/`.dmg`/`.pkg` are built natively in CI on 2026-11-10. | The unsigned artifacts install, launch, and associate `.iso`/`.cue`/`.chd` where built; a signed/notarized release remains in §4 until certificates/accounts exist. |

The packaging scaffolding is implemented and reproducible from `packaging/`. The Windows
installer, Linux packages, desktop entry, MIME/registry associations, and the macOS
`.app`/`.dmg`/`.pkg` are unsigned by default and built on their native CI runners.
Signing and notarization require valid certificates/accounts; if those are unavailable,
the unsigned installers and portable bundles remain the release artifacts and the signing
item stays in §4 until the credentials exist. Evidence: [Installers.md](Installers.md).

### 3.5 P2 — Optional accuracy features

| ID | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|
| **ACC-03 — Done** | M | **CD subcode support**: expose Q/subcode data and validate it with a synthetic test. | The CD sector path already existed; the 2352+96 wire size and Q packing are now implemented. | A synthetic subcode test passes and the Q payload decodes to the same control/track/index/MSF values. Evidence: `CdromMultiTrackTests` (2026-11-10); [TestCorpus.md](TestCorpus.md) 1.1bs. |

`SYS-01` is now in §4: the FZ-10 BIOS CD player lists the audio-only TOC after the
subcode fix, but its transport controls remain inert and the 4DO reference documents
the same BIOS limitation. `ACC-03` is complete; no matrix title requires subcode.

### 3.6 P3 — Product features

| ID | Effort | Missing work | Why it is achievable | Done when |
|---|---|---|---|---|
| **SYS-21** | M | **Physical CD-ROM drive support**: boot from a host optical drive. **Implemented 2026-11-13** in `PhysicalDiscSource`, `File > Boot Optical Drive...`, and the CLI drive-root path. The remaining check is hardware evidence on a machine with an attached drive and a 3DO disc. | The disc abstraction already supports multiple image formats; a host CD source is additive. | A physical 3DO disc boots and reaches beyond-title; the device/format path is documented. |
| **SYS-22 — Done** | M | **Disc image conversion**: convert between ISO/BIN/CUE/CHD from the CLI. | The readers and CHD encoder are available; conversion is orchestration. | `disc convert` round-trips a known image and the output boots with the same hash. Evidence: Lemmings (USA) CHD → ISO/BIN/CUE/CHD all report the same `discHash` and a 600-frame `frameHash` of `675291253`; [TestCorpus.md](TestCorpus.md) 1.1bs. |
| **UX-09 — Done** | L | **Localization**: externalize user-facing strings and add at least one non-English locale. | The UI is string-literal based today; the work is extraction plus resources. | The shell and CLI load a locale without changing emulation behavior; a translated smoke pass is recorded. Evidence: `LocalizationTests` and `LocalizationCoverageTests` (2026-11-13), `--locale de` smoke marker `firstMenuHeader=Datei`, [Localization.md](Localization.md), [TestCorpus.md](TestCorpus.md) 1.1bw. |
| **UX-25 — Done** | S | **Session focus behavior**: pause/unpause and input-capture behavior when the window loses or gains focus. | The desktop shell and input loop already existed; the policy and setting are implemented. | Focus loss pauses or releases input per the setting; tests cover the transition. Evidence: `FocusPausePolicyTests` and the Settings > Emulation checkbox (2026-11-10); [TestCorpus.md](TestCorpus.md) 1.1bs. |
| **VID-17 — Done** | S | **Void-area patterns**: replace the solid void color with selectable patterns (checker, grid, scanline). | The shell already owns the void background; patterns are a presentation brush shared by every renderer. | Patterns render behind OpenGL/GLES/Vulkan/D3D11 and are covered by `VoidPatternTests` and the settings round-trip; because the brush is outside the GL surface, no GL probe capture is required (2026-11-10). |
| **VID-16 — Done** | M | **VDLP pixel formats**: support 0RGB1555, RGB565, and XRGB8888 presentation paths. | The VDLP frame path already exposes pixel data; the formats are additive conversion paths. | Each format matches the CPU reference on a synthetic frame; no default hash changes. Evidence: `FrameAssemblerTests` (2026-11-10); [TestCorpus.md](TestCorpus.md) 1.1bs. |
| **UX-28 — Done** | M | **Additional metadata providers and media**: add at least one external metadata source and support manual media attachments. | The offline metadata editor and cover resolver already exist; providers are additive. | A lookup populates metadata offline/online with a deterministic cache; failures fall back cleanly. Evidence: `MetadataCatalogClient` and `metadata lookup`/`metadata media` (2026-11-13), `MetadataCatalogClientTests`, [Metadata.md](Metadata.md), [TestCorpus.md](TestCorpus.md) 1.1bx. |
| **UX-29 — Done** | L | **TAS Lua scripting and input timeline**: script input/movies and edit an input timeline in the shell. | The deterministic movie container, editor, and PBUS packet format already exist. | A Lua-authored movie replays deterministically; the timeline edits and round-trips a movie. Evidence: `FiveDo.Scripting` + `LuaInputScript` (sandboxed `frame`/`hold`/`press`/`release`), `InputTimeline`, `run`/`movie record --input-lua`, `movie timeline export`, `movie timeline set`, `movie timeline apply`, **Emulation > Input Timeline...**, `LuaTimelineTests` (5) and the CLI tests (2026-11-13); [Tas.md](Tas.md) §8-9. |
| **UX-30 — Done** | M | **4DO `.4dosav` import/export (best-effort)**: map the native state to/from 4DO’s format. | The native state sections are versioned and documented; the mapping is additive and optional. | A 4DO image imports with warnings where fields are unsupported and export round-trips in 5DO. Evidence: `FourDoSaveState`, `FourDoStateConverter`, `state import-4do`/`state export-4do`, `FourDoStateTests` (5 tests) and the CLI round-trip test (2026-11-13); [SaveStates.md](SaveStates.md) §9. |
| **UX-31 — Done** | S | **Controller preview**: show a 3DO pad diagram in Input Bindings with the selected button highlighted and mapped/pressed controls lit. | `ControllerState` already exposes the button bits, the eight-player profile is editable, and Avalonia can draw the diagram without new assets or core work. | Input Bindings shows the pad diagram; selecting a row highlights its 3DO button and a pressed key/gamepad control lights the mapped button; `ControllerPreviewTests` cover the button-bit mapping and the dialog still round-trips the profile. Evidence: `ControllerPadPreview` (2026-11-13); [TestCorpus.md](TestCorpus.md) 1.1bu. |
| **UX-32 — Done** | S | **Screen-saver/display-sleep inhibit**: keep the display awake while a game is running and unpaused; release it on pause, stop, and exit. | It is a host-level call with no core or state impact; the run/pause transitions are already centralized. | Running a game inhibits display sleep on Windows (best-effort on macOS/Linux) and releases it on pause/stop/exit; a policy test covers the transitions and the platform limitation is documented. Evidence: `DisplaySleepPolicy` (2026-11-13); [TestCorpus.md](TestCorpus.md) 1.1bv. |
| **VID-18 — Done** | S | **Automatic blank-border crop**: detect black borders on the presented frame and crop them with hysteresis; keep manual overscan as the override. | The presentation crop (`CropInsets`) and per-game overscan profiles already exist; only detection, a setting, and the hysteresis policy are missing. | Auto-crop reproduces the 4DO border on the capture set, never changes frame/state/audio hashes, respects a manual crop and per-game profile, and `AutoCropTests` cover detection and the 1-second hysteresis window. Evidence: `AutoCropDetector` (2026-11-13); [TestCorpus.md](TestCorpus.md) 1.1bv. |
| **SYS-23** | M | **Windows DirectInput/generic HID gamepad backend**: enumerate DirectInput/HID pads, including axes, POV hats, and triggers, alongside XInput. **Implemented 2026-11-13**: `DirectInputGamepadSource` and `WindowsGamepadSource` enumerate devices every ten seconds, filter duplicate XInput pads, calibrate trigger rest positions, and keep XInput as the fallback. Axis/POV/trigger tests pass; a physical DirectInput-only pad check remains hardware evidence. | `IRawGamepadSource`, `PlatformRawGamepadSource`, and `GamepadMapper` already abstract and test the mapping; a Windows source is additive. | A DirectInput-only pad is detected and mapped on Windows; axis/POV/trigger tests pass; XInput remains the default fallback; the backend and device selection are documented in [Configuration.md](Configuration.md). |

`UX-30` is explicitly best-effort; native 4DO state compatibility is **not** a goal.
`UX-28` uses the existing `library-metadata.json` schema as its provider format, writes a
canonical `metadata-cache.json`, and manages manual media paths through
`5do metadata media`; see [Metadata.md](Metadata.md).

### 3.7 P3 — Requested 4DO parity order (2026-11-11)

The agreed implementation order is below. These are references to the canonical rows above;
do not add a second entry for the same work. The order is a product priority, not a release
gate, and each item is still one change with its own tests and evidence.

1. **UX-09** — localization.
2. **SYS-21** — physical CD-ROM drive support.
3. **SYS-23** — Windows DirectInput/generic HID gamepad backend.

**UX-31, VID-18, and UX-32 are done (2026-11-13).** Input Bindings draws a live 3DO
pad (mapped/selected/pressed states), Settings > Video > Overscan has a 4DO-compatible
auto-crop with a one-second hysteresis, and a running game keeps the Windows display
awake. `ControllerPreviewTests`, `AutoCropTests`, and `DisplaySleepPolicyTests` cover
the logic; see [TestCorpus.md](TestCorpus.md) 1.1bu and 1.1bv.

**UX-09, SYS-21, and SYS-23 (the requested parity order) are implemented (2026-11-13).**
UX-09 is complete with English/German catalogs, a General settings tab, and
`--locale`/`--list-locales` in both executables; the translated smoke marker records
`firstMenuHeader=Datei`. SYS-21 and SYS-23 are code-complete with automated tests, but
their `Done when` lines still require a physical host drive and a physical DirectInput
pad respectively. `--gamepad-probe` records the active backend and raw samples for that
hardware check; the physical drive is reachable from **File > Boot Optical Drive...** or
a drive-root CLI argument. See [Localization.md](Localization.md),
[Configuration.md](Configuration.md) §3.1 and §6, and [TestCorpus.md](TestCorpus.md)
1.1bw.

---
## 4. Blocked, residual, or not achievable (do not add checkboxes)

These items stay out of §3 until the named condition changes. They are recorded here so the
plan does not silently lose them. “Residual” means the item was measured and closed with a
documented limitation, not that it is unachievable.

| Item | Why it is not in the achievable backlog | Revisit if |
|---|---|---|
| **SYS-05 embedded boot ROM** | GPL-2.0-only licensing risk; no distributable ROM exists. Retail titles still need a user BIOS. | Legal sign-off and a distributable ROM exist. |
| **UX-16 signing/notarization** | Requires valid Windows code-signing certificates and an Apple developer account; the unsigned bundles and installer/package scaffolding are the current release artifacts. | The certificates and accounts are available. |
| **SYS-16 six-player/multi-tap** | No matrix title requires it and the hardware is unavailable. | A matrix title requires six players and hardware is available. |
| **SYS-01 audio-only CD playback** | The FZ-10 BIOS CD player now lists the 14-track TOC after the 2352+96 subcode fix, but its transport controls remain inert; the 4DO reference documents the same BIOS limitation. A guest CD-player application or a reference capture is needed. | A 3DO CD-player application (or a hardware/Opera capture) starts playback from an audio-only disc. |
| **CORE-01 ARM60 cycle-class** | Closed: the panel proved frame-boundary phase, not a cycle-model gap; no game-visible effect. | A title changes status with the ARM timing model and no other cause explains it. |
| **CORE-05 threaded DSP** | Closed: DSP is ~14% of frame time; handoff/validation cost exceeds the work. | A title spends >30% of frame time in DSP. |
| **PERF-08 managed JIT** | Closed: the interpreter remains faster on real titles and is the accuracy oracle. | A measured 1.5× end-to-end win on a real title. |
| **VID-01 true 640×480 / 4K internal CEL** | Open-ended research; no target title and no bounded implementation path. 4K output upscaling already ships. | A bounded prototype shows a compatibility or visual win on a matrix title. |
| **Full 594/1,371-image differential** | Replaced by the 378-title unique corpus; regional copies add noise, not signal. | A region-specific defect is suspected. |
| **Top-100 audit** | Replaced by Tier A (378) + Tier B (~100) + Tier C (50). | The matrix shows a blind spot outside Tier B. |
| **Hardware test ROM suite** | Dropped; the differential oracle, 2,000+ tests, and fuzz targets already catch the regressions a ROM suite would. | A hardware-only defect appears that the oracle cannot reproduce. |
| **WebAssembly/browser, Android/iOS, Raspberry Pi, Steam Deck, libretro core** | Explicitly out of scope for the desktop product. | Product scope changes. |
| **Netplay, achievements, full debugger UI** | Out of scope for v1. | A post-compatibility product roadmap opens. |
| **QUAL-05B Road Rash full race** | A fresh 150,000-frame `--autopilot road` run still ends with the rider off the bike on the sidewalk; the remaining work is driving/start-line and traffic avoidance, not emulation. Evidence: `artifacts/p0-rr-auto-boot`. | A scripted driver completes the 5.5-mile race without a wreck, or a new autopilot strategy proves it can. |
| **ACC-01 Road Rash long-run divergence** | Documented residual: first behavioral divergence is frame 600 (guest read-request ordering); no game-visible impact has been shown. | A Road Rash gameplay scene (not intro/attract) diverges, or a matrix title changes status. |
| **CORE-04 CD command-cadence residual** | Documented residual: 11/50 panel titles still differ in CD command cadence at frame 300; video/audio match. | A title shows a player-visible CD/audio effect, or a matrix title changes status. |
| **Crash 'n Burn (Japan)** | The Japan dump/BIOS pair is the limit; every tested BIOS stays on the Anvil boot screen. USA/Korea revisions work. | A verified Japan dump/BIOS pair renders the game. |
| **DeathKeep / Kingdom gameplay** | Both match the reference’s black/title screen; no known input reaches gameplay. | A reference gameplay capture proves a reachable scene. |
| **480i real-title validation** | No local title is confirmed 480i; only synthetic processor tests exist. | A confirmed 480i title or hardware capture is available. |
| **Exact peer frame parity after beyond-title** | Not required by the playable definition. | A player-visible regression is traced to a peer divergence. |
| **Deep playthrough of all 378 titles** | The compatibility claim is beyond-title, not full-game completion. | A title’s status or quality grade depends on deeper play. |

---

## 5. Execution order

1. **P0 quality:** `QUAL-03A`…`QUAL-03D` and `QUAL-05A` are complete (BladeForce,
   Jurassic Park, Off-world, PGA Tour, and Alone). `QUAL-05B` (full Road Rash race) is in
   §4 as a driving/autopilot limit.
2. **P0 evidence:** `AUD-01` is complete on Windows (null + real) and Linux (null); the
   Linux real-device path is a hardware limitation. Keep the quality-ledger gate green
   after every batch.
3. **P1 platform:** `PLAT-02` remains blocked on a GPU-capable Linux host; `PLAT-03`
   and `PLAT-04` are **done** via `.github/workflows/platform-runtime.yml`; `PLAT-05`
   needs physical macOS controller detection; `PLAT-06` is documented in Performance §8.8
   (DPI math validated; GLES 3.1 hardware and Tier B timing remain best-effort);
   `PLAT-07` and `PLAT-09` are **done**; `PLAT-08` remains the Vulkan surface work.
4. **P1 packaging:** `UX-16` unsigned scaffolding is done; signed/notarized installers
   stay blocked in §4 until certificates/accounts exist.
5. **P2 accuracy:** `ACC-03` is **done**; `SYS-01` moved to §4 after the BIOS CD player
   listed the TOC but stayed inert. The `ACC-01` and `CORE-04` residuals stay in §4 until
   their revisit conditions are met.
6. **P3 product:** `SYS-22`, `UX-09`, `UX-25`, `UX-28`, `UX-29`, `UX-30`, `UX-31`,
   `VID-16`, `VID-17`, `VID-18`, and `UX-32` are **done**. `SYS-21` and `SYS-23` are implemented and tested but keep
   their `Done when` lines open until a physical optical drive and a physical
   DirectInput-only pad are available. The requested 4DO parity order `UX-09` →
   `SYS-21` → `SYS-23` is complete in code (see §3.7).

**Stop rule:** P0 is closed except for the blocked `QUAL-05B`; do not start P2/P3 while a
P1 platform smoke is failing.

---

## 6. Verification commands

```powershell
# Build, test, format, static analysis
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore
jb inspectcode 5DO.sln --severity=WARNING --no-updates --no-build --output=inspectcode.sarif

# Unique corpus and 3-way comparison
python tools/corpus-inventory.py --jobs 8 --out artifacts/corpus/inventory.json
python tools/generate-compat-comparison.py --inventory artifacts/corpus/inventory.json --sweep artifacts/compat-full/summary-merged.csv --sweep artifacts/compat-unique/summary.csv
python tools/generate-compat-comparison.py --check --inventory artifacts/corpus/inventory.json --sweep artifacts/compat-full/summary-merged.csv --sweep artifacts/compat-unique/summary.csv

# Beyond-title playability and quality gates
python tools/beyond-title-sweep.py --scope peer-gap --jobs 10 --out artifacts/beyond-title --ledger docs/data/beyond-title-results.csv
python tools/beyond-title-report.py --ledger docs/data/beyond-title-results.csv
python tools/verify-beyond-title-ledger.py --ledger docs/data/beyond-title-results.csv
python tools/beyond-title-baseline.py --ledger docs/data/beyond-title-results.csv --check --require-count 378
python tools/verify-playability-grades.py

# Input-script parser and pointer-device tests
dotnet test 5DO.sln -c Debug --filter FullyQualifiedName~InputScriptTests

# Accuracy panel (frame/audio)
dotnet run --project tools/FiveDo.DiffHarness -c Release -- run benchmarks/diff/panel50.json --core artifacts/diff/opera-build/out/OperaCore.dll

# Frontend smoke
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --smoke-test

# Native platform runtime smoke (macOS x64/arm64, Linux arm64, Windows arm64)
gh workflow run platform-runtime.yml

# Disc conversion round-trip
5do disc convert game.chd --out game.iso --json
5do disc convert game.chd --out game.chd --format chd --json

# Release bundles
pwsh -NoProfile -File packaging/package-bundle.ps1
```

For a release, also run the commands in [ReleaseChecklist.md](ReleaseChecklist.md). Keep a
local copy of each bundle in `src/FiveDo.Frontend.Avalonia/bin/Release`, remove PDB/XML
files, and publish `SHA256SUMS.txt`.

---

## 7. Reference strategy

| Reference | Role | Use it for |
|---|---|---|
| `References/opera-libretro-master` | Timing/behavior oracle | CD-ROM, DSP, peripherals, SWI HLE, boot ROM, accuracy fixes |
| `References/4DO-master` | Compatibility oracle | Game database, fix flags, known-bad titles, UI behavior |
| `References/fourdo-code-r389-trunk` | Historical 4DO snapshot | Older behavior and regression comparisons |
| `References/FourDO Ported` | Porting notes/code | 4DO-to-C# mapping and conventions |
| `References/freedo-master` | Original core reference | FreeDO quirks, ARM/CLIO/MADAM/DSP baseline |
| `References/CPP_SimpleGui` | Opera test harness | Differential runs and reference captures |
| `References/Docs` | Hardware/SDK documentation | ARM60, 3DO, CD-ROM, VDLP, and Portfolio details |
| `References/RMG-master`, `References/rpcs3-0.0.43` | UX benchmarks only | Library, controller profiles, packaging, per-game config |

**Rules:** port behavior, not code; one reference per change; tests first; preserve
determinism; keep the core single-threaded by default; do not anchor to Opera bugs; do not
ship copyrighted BIOS or game content; document intentional divergence in
[PortingNotes.md](PortingNotes.md).

---

## 8. Evidence and history

- [Status](Status.md) — current position and release state.
- [Compatibility](Compatibility.md) — supported set and platform evidence.
- [5DO vs 4DO vs Phoenix](CompatibilityComparison.md) — unique-title matrix.
- [BeyondTitleEvidence.md](BeyondTitleEvidence.md) — per-title captures and failure reasons.
- [KnownIssues.md](KnownIssues.md) — current limitations.
- [DifferentialHarness.md](DifferentialHarness.md) — Opera comparison and panel evidence.
- [TestCorpus.md](TestCorpus.md) — test inventory and dated batches.
- [ReleaseChecklist.md](ReleaseChecklist.md) — release gate.
- [ImplementationPlanArchive.md](ImplementationPlanArchive.md) — merged historical plan,
  completed log, stable-ID index, retired aliases, and appendices.

The archive is retained for traceability only. New work goes in this file; completed work
moves to §1 or the archive, not into a second backlog.
