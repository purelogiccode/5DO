# Next Session Handoff

**Date:** 2026-10-26 (thirteenth pass)
**Goal:** finish the frontend trimming/AOT audit and publish the Avalonia frontend trimmed.
The thirteenth pass enabled `PublishTrimmed` for the Avalonia frontend, removed the
reflection-based `ViewLocator`, added a trimmed-frontend startup smoke test, and now
publishes both executables trimmed for all six RIDs. **709 tests pass.** Do not redo
the trim-safe JSON work, the trimmed CLI/frontend publishes, the frontend smoke test,
the benchmark comparison, or the portable archives. The remaining work is finishing a
full Road Rash race, scripted Lemmings level play beyond the start, GPU/Tier B
validation, NativeAOT/arm64/installers, and the legal/attribution review.

Earlier passes added the presentation plumbing (GL/GLES capability reporting, CPU
fallback/context-loss recovery, fullscreen, screenshots, About, Hi-Res, benchmark
gates, and prefetch) and the game-specific fixes recorded below. Read section 1.17
for the thirteenth-pass changes before starting.

Read these first:

- [`Compatibility.md`](Compatibility.md) - current title matrix, renderer fixes, and
  input evidence.
- [`Status.md`](Status.md) - current position and ordered next steps.
- [`TestCorpus.md`](TestCorpus.md) - baselines, commands, and the `--input-script`
  format.
- [`PortingNotes.md`](PortingNotes.md) - DSP/CCB/CEL invariants.
- [`ReferenceStrategy.md`](ReferenceStrategy.md) - reference hierarchy.

---

## 1. What changed on 2026-10-24 (second and third passes)

### 1.1 Arbitrary-CEL transposition (fixed)

`TexelDrawArbitrary` passed `(i, j)` to the `(x, y)` `ReadPixel`/`WritePixel`
helpers. FreeDO's `readPIX(src, i, j)` treats `i` as the row (`y`) and `j` as the
column (`x`), so every asymmetric quad was written transposed. Road Rash's race
CELs wrote texels into the top of VRAM (`0x2FC000`), which is the OS's VRAM free
list; the OS then printed `AllocMem error, bad node address` and the race appeared
frozen. Alone in the Dark drew its character horizontally. The fix passes `(j, i)`.

Evidence: the RAM-write watchpoint showed `CelRasterizer.TexelDrawArbitrary`
writing 2-byte texels at `0x2FC000`-`0x2FC03E` from frames 16729-17030. After the
fix the race renders a level road and an upright rider, the allocator error is gone,
and the run reaches 60,000 frames.

A regression test (`PackedArbitrary_UsesTheColumnAsXAndTheRowAsY`) fails when the
arguments are transposed and passes with the fix.

### 1.2 VDLP line width (fixed)

The bitmap copy path read and stored exactly 320 pixels regardless of the display
mode. It now uses the modulo parsed from the VDL control word (320/384/512/640/1024),
sets the line width accordingly, and `ThreeDoMachine` derives `SourceWidth` from the
widest rendered line. 320-wide output is unchanged; the 3,000-frame corpus hashes are
identical.

### 1.3 Save-state frame pixels (fixed)

`MachineFrameSection.Load` read the line pixel buffers and discarded them. A state
load now restores them, so a zero-frame load reproduces the saved frame hash exactly.

### 1.4 New diagnostics

- `--capture-dir <path> --capture-every N`: write `fNNNNNN.png` every N frames.
- `trace --dump-ram ...`: dump RAM at a trace stop (for example, at a KPRINT or PC
  stop).
- The trace header now prints r4-r12 in addition to r0-r3.
- `--dump-ram`, `--dump-ram-offset`, `--dump-ram-length` work in `run` and `trace`.

### 1.5 High-resolution CEL/VDLP mode (new, opt-in)

`--high-res` enables `EmulatorTuning.HighResolution`. The CEL rasterizer addresses
four 1 MB VRAM planes: X selects the even/odd plane and Y remains a 240-line
coordinate. Each write is mirrored to the second interlace field (planes 2/3), and
the VDLP interleaves planes 0/1 and 2/3 into two 640-wide output lines. The frame
source becomes 640x480; `VdlFrameUploader`, the CPU assembler, screenshots, frame
hashes, and save-state frame sections all carry the larger frame. SPORT fill/copy
mirrors the source page to the extra planes.

Evidence: from a fresh Lemmings boot with the intro-skip script, the 640x480 title
shows all four buttons and the complete logo and matches the reference screenshot
(PC `0x00012B48`, hash `0xE79E18C5` at 18,100 frames). The five 3,000-frame low-res
corpus hashes are unchanged. High-res is off by default because 320x240 titles must
not be upscaled. The passcode screen is a 320-wide UI that the high-res mode scales
2x; its five visible keyboard columns are the game's own scrolling window, so that
screen does not exercise a separate 640-wide coordinate path.

### 1.6 Wolfenstein gameplay loop (new)

From `w3d-fix12k.state`, `w3d-play.txt` starts FLOOR 1-1 at 14,000 frames
(`PC 0x0007824C`, hash `0x07586A88`); `w3d-walk.txt` walks/turns/fires to 18,000
frames (`PC 0x00074A04`, hash `0xF568EBCA`), with the corridor, guard, HUD, and
pistol rendered. The 18,000-frame state roundtrips (zero-frame hash `0xF568EBCA`,
two 120-frame continuations both `PC 0x000782B8` / `0xF568EBCA`). A 4,000-frame WAV
capture is 66.86 s of 44.1 kHz stereo, peak 32767, RMS 2980, 18 clipped samples.

### 1.7 Diagnostic RAM patches and Road Rash driver (new)

`--poke ADDR=HEX` writes bytes into RAM after a state load; multiple patches are
separated by `;`. It is a diagnostic for input/state work, not a compatibility fix.
`--autopilot road` is an experimental Road Rash driver that holds B and steers
toward the gray road centre. Unit tests cover the patch parser and the steering
direction contract. The driver follows the road but does not yet detect traffic or
recover from a wreck, so it does not finish a race.

### 1.8 Road Rash driver v9 and the BUSTED discovery (2026-10-25)

The previous pass described a "terminal wall pin" around frame 34,000: speed 0,
frozen `lineX`, and all held inputs converging on one hash. That state is **the
race-over `BUSTED` screen**, not a physics wedge. From `rr-auto-v8.state`
(40,000 frames), 400 frames of no input followed by `B` produces the `BUSTED`
plainclothes-cop screen (hash `0x7D470158`); the same happens from
`rr-v10full.state` and `rr-v17-40k.state` (`0x9F44FAA2`). Once a race is lost, no
steering/throttle input resumes it, which is why every probe converged. The
driver's real failure is losing the race earlier by leaving the road, stalling,
and getting caught - not a stuck bike.

Driver v9 changes, all covered by tests (572 passing):

- **Road brightness ceiling.** `IsRoadPixel` now caps brightness at 120 (was 150).
  City sidewalks and plazas are brighter concrete and were being classified as
  asphalt; the lower band locked onto them and the bike rode the sidewalk.
- **Detector-upset hold.** A frame-to-frame `lineX` jump over 25 px means the
  detector re-locked onto a different gray surface, not road motion. The driver
  holds straight for 30 frames instead of chasing the false lock. The test is
  frame-to-frame so a frozen line cannot latch the hold (the first attempt used a
  long reference and did exactly that).
- **Closing-side steering.** If the driver is steering toward a side whose
  lookahead fraction is below 0.6 while the other side is above 0.8, it crosses
  to the open side and coasts during the crossing.
- **Sharp-bend braking.** Centre below 0.35 with one side open above 0.8 and the
  other below 0.4 brakes while steering to the open side; this replaces taking the
  0.39/0.29/0.94 bend flat out into the outside wall.
- **Urban pacing.** In built-up sections (centre visible, one edge below 0.6) the
  throttle is duty-cycled 40/60 frames instead of held flat out.
- **Coast floor.** Pacing, caution, and bend lift may never coast more than 20
  consecutive frames; the throttle is forced back on. A stopped bike is caught and
  busted, which is exactly how the pacing-only build failed.
- **Faster recovery.** Stuck recovery triggers after 150 stable off-centre frames
  (was 300), brakes for 30 frames, then re-applies throttle with the recovery
  steer.
- **Telemetry.** Progress lines add `la=<left>/<centre>/<right>` and `PACE`/`UPSET`
  markers; `run --json` adds `autopilotPacing`, `autopilotUpset`, and
  `autopilotLookahead`.

Best run: from `rr8-30k.state`, 10,000 autopilot frames end at 40,000 frames
(`PC 0x000125E4`, hash `0xE44B073D`). The bike keeps road detection to about
36,500 frames (the v8 run lost it at ~34,000), but then leaves the road and the
race is lost; the final screen is the pre-bust arrest frame. Zero-frame load
reproduces `0xE44B073D`; two 120-frame autopilot continuations both end at
`PC 0x000125E4` / `0xE44B073D`, and any input advances to `BUSTED`
(`0x9F44FAA2`). The 5.5-mile city race is still **not finished**, and the
remaining work is vision/control quality, not a pin-recovery rule.

### 1.9 Road Rash driver v10: 91,000 live frames and a new stop state (2026-10-25)

The v9 driver lost the first city race at ~34,000 frames. Dense captures at
33,800-34,000 showed the actual causes were **close traffic**, not a wall:
another racer filled the lookahead centre (0.43/0.07/0.17) while every existing
rule required a 0.6-wide escape, so the bike ran straight into it. A full brake
stopped the bike dead and got it busted; a coast/steer was the correct response.
Driver v10 adds:

- **Close-traffic escape.** Centre < 0.2, one side in [0.3, 0.6), side
  difference > 0.2: steer into the gap and lift (not brake). This runs even when
  `LineX` is NaN, because the lower-band run disappears exactly when the
  obstacle is close.
- **Urgent preemption only for close traffic.** Ordinary obstacle steering
  stays on the 16-frame decision interval. Applying every obstacle bias
  immediately oversteered into a taxi at ~32,000 in the clean 30k run.
- **Emergency-brake steer hold.** With the road fully blocked and no side target
  (all lookahead fractions < 0.35), keep the last steer and brake. The old path
  chased a `LineX` that had jumped to 240 and turned into the tree at 33,890.
- **Stopped/wreck restart.** A scene signature over the left scenery (rows
  96-156, x 20-140) plus the speed readout stays still for 40 frames -> hold B
  with straight bars for 180 frames, then retrigger if still stopped. The
  signature deliberately excludes the centre/rider: the remount animation kept
  resetting the first version. Manual probes showed B alone remounts (34,500 and
  80,000 -> 52 mph) while steering keeps the rider running in place.
- **Telemetry.** Progress adds `cov=<lower-band road fraction>` and `RESTART`;
  `run --json` adds `autopilotRoadCoverage`, `autopilotCrashRestart`, and
  `autopilotStaticFrames`.

A cyclic right/left escape search on later restart attempts was tried and
**reverted**: it drove the 83,000-frame rider run into the BUSTED screen
(`0x9F44FAA2`). The throttle-only policy is the proven one.

Best run chain (local states; not a single command):

```
rr8-30k.state (30,000)
  + 6,000 -> rr-v28-36000.state (36,000)
  + 44,000 -> rr-v34-80000.state (80,000, PC 0x00098004, hash 0xD3309E17)
  + 3,000 -> rr-v35-83000.state (83,000, PC 0x00091A70, hash 0x442D7D3D)
  + 8,000 -> rr-v36-91000.state (91,000, PC 0x00085F48, hash 0x02ABE78E)
```

The 30k->36k and 36k->80k segments used earlier v10 revisions (right-steer
crash restarts); the final throttle-only code was verified on the 80k->83k and
83k->91k segments and reproduces the 91,000 state exactly. A clean final-code run
from 30,000 reaches 42,000 (`PC 0x0001048C`, hash `0xCBDF6DDB`) and then enters
the **game-side wait** described below. The bike is on a sidewalk in the image,
but that is a symptom; input is ignored and no amount of steering/throttle frees
it. This is not a reason to lose the 91,000 result.

Roundtrip at 91,000:

- Live state: `PC 0x00085F48`, hash `0x02ABE78E`,
  `cov=0.93`, `lineX=197.9`.
- Zero-frame load: `0x02ABE78E` (pixel-identical).
- Two independent 120-frame autopilot continuations: both `PC 0x000973EC`,
  hash `0x3F27B3D6`.

A 100,000-frame continuation stays live through ~91,000 and then stops at
~92,000. The ninth-pass trace shows the frame is static, every input is ignored,
and the CD trace contains only READ_ID (`cmd 0x83`) every 60 frames after the
last read-data requests (sectors 109053-109071 and 43873-43888). `run --json`
reports `cdStatus=0xE1`, `cdPoll=0x0C`. This is the **same game-side wait** as
the 42,000-frame stop and Alone's static intro. Do not change the restart policy
for it.

A separate terminal stop was seen on one 45,500-frame path with the same
signature: reads 109053-109071, seeks to 43877-43888, then only READ_ID polls;
the frame hash stays identical and every button is ignored. **The ninth pass
fixed this class of stop** with the CD media-access pulse (section 1.12); the
old 91k/92k hashes are historical. The remaining Road Rash issue is finishing a
full race without wrecking.

### 1.10 Desktop shell and per-game fixes (2026-10-25)

The seventh pass filled the frontend and host gaps that do not depend on game
compatibility:

- **Game fixes.** `GameCompatibility` maps the 4DO IDs (`F3AF1B13`/`217344B0`,
  `DBB419FA`/`07C32F10`/`5282889F`/`7340307E`, `CB8EE795`, `1A370EBA`/`B35C911D`,
  `0511D3D2`/`9F7D72BC`/`E843C635`, `2AABA5B9`/`BF61BB32`) to the matching
  `FixMode` bits. The CLI applies them before `Reset`, reports the mask in
  `run --json`/`info`, and accepts `--fix-mode 0xMASK` or `--no-game-fixes`.
  `AppSettings` stores per-game overrides and an automatic-fixes toggle.
- **Save slots.** `SaveStateSlotManager` writes `slot-NN.tdos` plus a JSON sidecar
  (game name, ID, timestamp, frames) atomically; the shell has Save/Load and
  previous/next slot buttons and a Slots window that lists/saves/loads/deletes
  slots, and persists the current slot. `List()` tolerates empty and corrupt slots.
- **Library and settings.** The Library window scans configured folders through
  `GameLibraryScanner` and opens the selected disc. The Settings dialog edits
  filter/aspect/integer scaling/vsync, audio enable/volume, automatic fixes,
  high-res, per-game fix mask, and save/screenshot paths.
- **Input capture.** `KeyboardBindingEditor` and the Input dialog capture keys for
  all six players, remove a key from its previous button, detect conflicts, and
  reset to defaults. The optional controller preview remains.
- **Screenshots and overlay.** Screenshots are PNGs named
  `<game>-<timestamp>-f<frames>.png` from GL readback or the CPU frame; an optional
  FPS/status overlay is available in Settings.

The five 3,000-frame low-res hashes are unchanged; Alone's automatic
`FIX_BIT_TIMING_6` does not alter the boot frames. 659 tests pass.

---

### 1.11 Desktop completeness and renderer correctness (2026-10-25, eighth pass)

The eighth pass closed the remaining non-gameplay frontend gaps and fixed a
real-content renderer bug:

- **GPU palette expansion.** The shader used `pixel & 0x1f` as one CLUT index for all
  three channels. Real frames therefore appeared grayscale. The shader now fetches red,
  green, and blue independently (`pixel >> 10`, `pixel >> 5`, `pixel`) and matches
  `FrameAssembler`/`GpuPalette`. Lemmings gameplay was captured through sharp-bilinear
  and xBR with correct colors.
- **xBR.** Hyllian's xBR-lv2 edge-directed 4x pass is implemented as `VideoFilter.Xbr`,
  with a CPU-expanded native source and sharp-bilinear finishing. It is selectable in
  Settings and validated visually on Lemmings gameplay at 60 fps. HQx, CRT bloom, and
  4K timing remain optional follow-ups.
- **Gamepads.** `GamepadMapper` plus Windows XInput and Linux evdev backends; hot-plug
  rescanning, six-player mapping, left-stick D-pad fallback, trigger-to-shoulder
  fallback, and a Gamepad tab in the input editor. macOS falls back to null.
- **Logs.** In-app log viewer with level, category, search, auto-refresh, and clear;
  the shell records BIOS/disc loads, renderer fallback, save/load, screenshots, input
  changes, and emulation faults.
- **Save-state thumbnails.** Each slot writes a PNG thumbnail shown in the Slots window;
  thumbnail failures never fail the state save.
- **First run and errors.** A welcome guide explains the user-provided BIOS/disc
  requirement; BIOS, disc, and emulation failures show error dialogs in addition to the
  status line and log.
- **Docs.** Added `docs/UserGuide.md`, `docs/KnownIssues.md`, and `CHANGELOG.md`.

659 tests pass. The five 3,000-frame low-res hashes are unchanged. The next session
should return to the real-game compatibility work: Road Rash's ~92k/40k stops, Space
Ace's interactive path, and the CD-load/READ_ID freeze. Do not redo the palette, xBR,
gamepad, log, thumbnail, or first-run work.

---

### 1.12 Ninth pass: evidence, Space Ace interactive start, and the wait-state diagnosis (2026-10-25)

This pass did **not** change emulation behavior. It captured the evidence that was
missing and corrected the Road Rash diagnosis.

- **Per-title evidence.** Each live state was loaded with `--frames 0`, captured to
  PNG, then run 120 frames and captured again. The zero-frame hashes match the saved
  frames, and every 120-frame continuation is deterministic. A 600-frame WAV was
  captured from each gameplay state. The table and WAV stats are in
  [`TestCorpus.md`](TestCorpus.md) 1.5a.
- **Space Ace interactive path.** `X` at 12,000 returns to the title/menu; `A` at
  12,200 starts the interactive scene. The run reaches 15,000 frames (hash
  `0x7E780D93`), the state roundtrips, and two 120-frame continuations both end at
  `PC 0x000284EC`, hash `0x70B16422`. A 600-frame gameplay WAV is 10.04 s, peak
  4096, RMS 1832.
- **Alone menu/intro.** `Up, Left, A, Down` reaches the Begin/Resume menu; `A` starts
  the intro scene. The intro is a static, input-ignoring wait with only READ_ID
  polling. The media pulse alone does not advance it, but A every 300 frames moves
  the man-at-desk cutscene to the room/mansion/title scenes (29,200 frames,
  `PC 0x00020640`, hash `0x62304BCF`). A blind pulse is not yet a stable room.
- **Alone movement loop.** From `alone-intro-A2.state` (39,200), `Up, Left, A,
  Down, Right` moves the character around the room; at 39,800 `PC 0x0008D0EC`,
  hash `0x44C7576B`. The state roundtrips (`0x44C7576B` -> two 120-frame
  continuations at `PC 0x0008BF00`, `0xE490C8C7`). This is a scripted
  movement/save-load loop; inventory interaction remains.
- **Wolfenstein fresh-game load.** `w3d-loadlong.state` + A every 300 frames
  advances the static load through character select to FLOOR 1-1 at 64,000
  (`PC 0x00069F3C`, hash `0x2F5B9864`); no input stays at `0x07586A88`.
- **Road Rash wait state and the media-access fix.** The 42,000-frame state and the
  92,000/100,000-frame state were static, ignored every button combination, and
  showed only READ_ID polls. The CD device now sets poll bit `0x40` after two
  consecutive READ_ID commands with no intervening command and at least 50 frames
  between them. That is Portfolio's media-access event; it advances the screens to
  `BUSTED`, race select, and a new race. The five 3,000-frame hashes are unchanged.
  The old 91k hash `0x02ABE78E` was a frozen presentation; the current chain reaches
  a rider-run/BUSTED transition at `PC 0x00093AF4`, hash `0x70E1ED8E`.
- **Autopilot menu advance.** `--autopilot road` now pulses A/releases throttle
  during long-static, low-coverage (`<0.5`), CD-load, and 180-frame restart-stuck
  states, and reports `autopilotMenuAdvance` in `run --json`. It passes the
  `GAME LOADING`/`Get ready` screen and starts a new race at 80,000
  (`PC 0x00092FF8`, hash `0x1074EF3F`), then cycles race -> wreck -> menu -> new
  race to 100,000 (`PC 0x0008214C`, hash `0x0D46CF56`). The remaining problem is
  finishing a full race without wrecking.
- **Tests.** Added `ScriptedPbusSource`/`InputScript.Offset` tests, CD media-access
  tests, and an autopilot menu-advance test. 667 tests pass; format is clean.

---

### 1.13 Tenth pass: pure D-pad Lemmings passcode, CRT bloom, and HQx 2x (2026-10-26)

- **Lemmings pure D-pad passcode.** `docs/scripts/lem-fun4-dpad.txt` types
  `HNLHCIOECW` from a passcode-screen state with an empty entry and selection on
  `V` (`artifacts/lem_pass_fresh.state`, 21,700 frames). The script uses 15-frame
  direction holds and 5-frame A taps with a release frame between actions, so the
  CLI's duplicate-frame coalescing cannot swallow a release. The game shows the
  `Fun 4 / Level 1 / Save the Lemmings` briefing at 22,600 (`PC 0x00020628`, hash
  `0xC1DF57C5`) and starts gameplay at 23,000 (`PC 0x00083FD8`, hash `0xB7846C2D`).
  The zero-frame load is pixel-identical and two 120-frame continuations both end
  at `PC 0x000230A4`, hash `0xEDEB1B9D`. This closes the pure-D-pad entry item; the
  title-to-passcode transition is still the existing `Down`/`A` script.
- **CRT bloom.** `RendererSettings.Bloom` (default 0.35) and `BloomThreshold`
  (default 0.6) add a 3x3 native-resolution glow around bright pixels in the CRT
  filter. The Settings dialog exposes a CRT bloom slider. Bloom is ignored by the
  other filters and the CPU fallback. Shader-source and settings roundtrip tests
  cover the new uniforms; GPU timing is still pending.
- **HQx 2x.** `VideoFilter.Hqx` runs hq2x on the CPU-expanded BGRA frame, uploads
  the 2x result, and finishes with sharp-bilinear. A 32x24 reference image matches
  the C++ `hq2x_32_rb` hash `0xA400C421`; uniform images are unchanged. The
  reference's `RGBtoYUV` table loop skips the last entry, so pure white is excluded
  from the reference image. (hq3x/hq4x were added in the eleventh pass; see 1.14.)
- **Per-game filter overrides.** `AppSettings.FilterOverrides` stores a filter by
  game ID, `ResolveFilter`/`SetFilterOverride` handle case-insensitive lookup and
  clearing, and the Settings dialog's Emulation page exposes an Automatic/filter
  combo. The shell passes the current game ID to `ToRendererSettings`.
- **Tests.** 673 tests pass; the four HQx tests, the filter-override tests, and the
  steady-state allocation test are the only count change.

---

### 1.14 Eleventh pass: HQx 3x/4x and the benchmark budget gate (2026-10-26)

- **HQx 3x/4x.** `VideoFilter.Hqx3x` and `VideoFilter.Hqx4x` run generated
  hq3x/hq4x switches on the CPU-expanded BGRA frame. The same 32x24 reference image
  matches the C++ `hq3x_32_rb`/`hq4x_32_rb` hashes (`0x0C406F97`/`0xBB17969C`);
  uniform images are unchanged and destination-size validation accepts only 2x-4x.
  The renderer sizes its intermediate target from `HqxFilter.ScaleFor`, and the
  Settings dialog lists all three HQx modes. Measured 320x240 cost is
  ~2.3/4.2/7.5 ms/frame on checkerboard content and ~12.1/13.4/14.8 ms on random
  noise for 2x/3x/4x; 3x/4x stay opt-in.
- **Benchmark budget gate.** `BenchmarkGate.Evaluate` enforces 0 managed
  bytes/frame, core p99 <= 8 ms, total mean <= 8 ms, and total p99 <= 16.667 ms.
  The benchmark exposes `--gate` (exit code 3 on failure), and CI runs it for the
  synthetic smoke before uploading the JSON baseline. The gate is budget-based;
  the same-runner relative (>10%) comparison was added in the twelfth pass (see 1.15).
- **Desktop integration.** The frontend accepts a positional disc path or
  `--disc <path>` for file associations, ships `packaging/3doemulator.desktop`, can
  write a native CPU-frame screenshot next to a filtered GL capture, and previews
  sibling cover art in the Library window.
- **Tests.** 691 tests pass; the new tests cover the 3x/4x reference hashes,
  uniform-image preservation, unsupported scales, `ScaleFor`, cover-art discovery,
  and every gate failure mode.

---

### 1.15 Twelfth pass: trim-safe JSON and trimmed CLI publishes (2026-10-26)

- **Source-generated JSON.** `AppSettings`, `InputProfile`, save-slot metadata, and
  every CLI `--json` response now use `JsonSerializerContext` metadata instead of
  reflection. The contexts are `AppSettingsJsonContext`/`SaveStateSlotJsonContext`
  (`ThreeDo.Host`), `InputProfileJsonContext` (`ThreeDo.Input`), and `CliJsonContext`
  (`ThreeDo.Cli`). The CLI's anonymous response objects were replaced with named
  records in `src/ThreeDo.Cli/CliJsonModels.cs`; the JSON field names are unchanged.
- **Reflection removal.** `WriteRunResult` and `selftest` read typed properties
  instead of `Type.GetProperty`; the RAM-write watchpoint formatter uses
  `DiagnosticMethodInfo.Create` instead of `StackFrame.GetMethod`.
- **Trimmed CLI.** `ThreeDo.Cli` sets `PublishTrimmed=true` and `TrimMode=full`.
  CI publishes all six RIDs trimmed and adds a Windows smoke test (`--version`,
  `selftest --json`, `bench --frames 2 --json`). The win-x64 audit also covered
  `info`, `state inspect`, `state slots`, `fs list` on a generated CHD, a CHD boot,
  and a save-state roundtrip. The only remaining trim warning is CHDSharp's vendored
  FLAC settings helper (`TypeDescriptor`); it is outside the 3DO data-track path and
  is demoted to a visible warning. See [`Trimming.md`](Trimming.md).
- **Frontend.** Trimming was still disabled in this pass; it was completed in the
  thirteenth pass (section 1.17), which removed `ViewLocator` and added the trimmed
  startup smoke test.
- **Benchmark baseline comparison.** `BenchmarkComparison` and the benchmark
  `--baseline <json>`/`--baseline-threshold <fraction>` options compare core p99,
  total mean, and total p99 with a same-machine baseline; a >10% regression exits
  with code 4 and improvements pass. CI still uses the absolute `--gate` budgets;
  the relative check is intended for stable/self-hosted runs. See
  [`Performance.md`](Performance.md) §8.
- **Tests.** 704 tests pass; the new tests cover the camel-case CLI JSON contract,
  source-generated self-test/host-record roundtrips, and every benchmark-comparison
  failure mode.

---

### 1.16 Twelfth pass: portable release archives (2026-10-26)

- **Packaging script.** `packaging/package.ps1` takes a component (`cli`/`frontend`),
  RID, publish directory, and output directory. It stages the publish output plus
  `README.md`, `THIRD-PARTY-NOTICES.md`, `docs/Licensing.md`, and a generated
  `PACKAGE.txt` (component, RID, version, commit, UTC build time), and adds
  `3doemulator.desktop` for Linux. Windows archives are zip; Linux/macOS archives
  are tar.gz. PDBs are excluded from release archives.
- **CI.** Both six-RID publish jobs now create and upload a portable package in
  addition to the raw publish folder.
- **Local validation.** The win-x64 zip and linux-x64 tar.gz were built and
  inspected; both contain the executable, notices, licensing, and manifest. The
  win-x64 archive starts and passes `selftest`/`bench`.
- **Remaining.** MSIX/dmg/AppImage installers, signing/notarization, and
  Windows/macOS file associations (M10.7).

---

### 1.17 Thirteenth pass: trimmed frontend publishes and startup smoke test (2026-10-26)

- **ViewLocator removed.** The template `ViewLocator` (`Type.GetType` +
  `Activator.CreateInstance`) and its `Application.DataTemplates` registration were
  deleted. Every window is constructed explicitly, and all XAML bindings are compiled
  (`AvaloniaUseCompiledBindingsByDefault=true`; each window/`DataTemplate` declares
  `x:DataType`). This removed the only first-party frontend trim warning.
- **Trimmed frontend.** `ThreeDo.Frontend.Avalonia` sets `PublishTrimmed=true` and
  `TrimMode=full`. The six-RID CI publish job now uses `PublishTrimmed=true` instead
  of `false`. CHDSharp's vendored-FLAC `TypeDescriptor` warning is demoted to a
  visible warning with the same policy as the CLI; the data-track path does not decode
  CD-DA/FLAC.
- **Frontend smoke test.** `--smoke-test --smoke-output <path>` opens the real
  `MainWindow`, lets the first render pass run, writes
  `FRONTEND SMOKE OK <version>`, and exits with code 0. It skips the welcome dialog
  and configured BIOS/disc flow so CI needs no BIOS, disc, or user profile.
  `--version` exits before Avalonia initializes. The startup parser (`--disc`,
  positional disc, `--smoke-test`, `--smoke-output`, `--version`) has unit tests in
  `tests/ThreeDo.Tests/Frontend/FrontendStartupTests.cs`.
- **CI.** The Windows `build-test` job publishes the trimmed win-x64 frontend and runs
  the smoke test with a 60-second timeout, checking the exit code and marker file.
- **Size.** On win-x64 the single-file executable is 26.3 MB trimmed vs 91.3 MB
  untrimmed; the complete publish folder is 150.9 MB vs 215.9 MB because native
  Skia/HarfBuzz/ANGLE assets are unchanged. All six trimmed frontend RIDs were
  published and inspected locally.
- **Tests.** 709 tests pass; the five new tests cover flag/option parsing and disc-path
  detection (including ignoring the smoke-output value).
- **Remaining.** NativeAOT is still not attempted and is documented as post-v1 work;
  installers/signing/notarization remain M10.7. See [`Trimming.md`](Trimming.md).

---

## 2. Current evidence

### Wolfenstein 3D

From the 12,000-frame demo state (`w3d-fix12k.state`), the script

```
12000 A
12020 None
12200 Up
12320 None
12400 Up
12520 None
12600 Left
12660 None
12700 Up
12820 None
12900 A
12920 None
13000 Up
13120 None
13200 A
13220 None
13300 Up
13420 None
```

opens the Select Previews menu, navigates to Select a Level, and starts a fresh game
at FLOOR 1-1 with `SCORE 0000000`. The end state is at 14,000 frames.

Save/load:

- Live state saved at 14,060 frames: frame hash `0xC9989182`.
- Two independent 120-frame continuations: both end at `0x370158AC`.
- A zero-frame load: `0xC9989182` (pixel-identical).

The fresh game continues into a full movement loop. From the 14,000-frame state,
`w3d-walk.txt` walks, turns, and fires to 18,000 frames (`PC 0x00074A04`, hash
`0xF568EBCA`); the HUD, corridor, guard, and pistol are visible. A further
walk/turn/fire script reaches 19,200 frames (`PC 0x0001186C`, hash `0x74FBBF0C`).
The 18,000-frame state roundtrips exactly. The 4,000-frame WAV captured from it is
66.86 s, peak 32767, RMS 2980, with 18 clipped samples.

### Road Rash

No-input run: the intro FMV ends around 17,000 frames, the 3D race renders
correctly, and the attract race/FMV loop continues past 60,000 frames.

```
30,000 frames: PC 0x000681F0, hash 0xEB0AD59C, DSP running, no AllocMem error
60,000 frames: PC 0x00095E84, hash 0xBF01DF59, DSP running
```

From the live 26,000-frame state the attract race runs on its own and eventually
reaches the WRECK FMV. The 3DO controls are: **B accelerates, A brakes, the D-pad
steers left/right, C attacks, and P pauses**. Holding B starts a player race from
the `GAME LOADING` screen; the HUD speed rises and the camera follows the road.
Holding A or tapping any face button while the attract race is running freezes or
advances the current presentation, which is why earlier probes concluded that `A`
"changes the race". A hard left at about frame 31,000 clears the first oncoming
taxi; the next obstacle still ends the run.

`--autopilot road` is an experimental CLI driver that holds B and steers toward
the gray road centre (target screen X 230). v9 added a road-brightness ceiling,
detector-upset holds, closing-side steering, sharp-bend braking, urban throttle
pacing, a coast floor, and faster recovery. v10 adds close-traffic coast/steer,
urgent close-traffic preemption, emergency-brake steer hold, and a stopped/wreck
restart that holds B with straight bars until the scene moves. It reaches
**91,000 live frames** from the 80k/83k states (PC `0x00085F48`, hash
`0x02ABE78E`) and roundtrips there (zero-frame `0x02ABE78E`; two 120-frame
continuations both `PC 0x000973EC` / `0x3F27B3D6`). It still does not finish a
race: a 100,000-frame continuation pins at ~92,000 with lookahead
0.55/0.32/0.05, and a clean final-code run from 30,000 pins at the ~40,000
right-sidewalk wreck. The earlier "terminal pin" at ~34,000 frames was the
`BUSTED` screen itself (see sections 1.8 and 1.9). Unit tests cover the steering,
close-traffic, obstacle, brake, pacing, restart, and recovery contracts.

### Alone in the Dark

At 16,000 frames the character is upright at the staircase (`PC 0x0008C608`, hash
`0x3D257B5C`). The control run walks through the room; the D-pad/A script reaches
the Begin/Resume main menu.

### Lemmings

At 18,000 frames the title screen shows Play/New Game (`PC 0x000205F8`, hash
`0x5625A4CD`). With `--high-res` the same title renders at 640x480 with all four
buttons (Play, New Game, Load, Options) and the complete logo, matching the reference
(`PC 0x00012B48`, hash `0xE79E18C5`). The screen is an attract loop: `A`,
`Left`/`Right`, `P`, and `X` at the title do not leave it, even over 20,000 further
frames (the marquee cycles "SKILLS"). `Down` + `A` on New Game opens the
**passcode** screen (the string is "Enter the access code", not an account name).

The passcode keyboard is a 9-column grid (`A-I`, `J-R`, `S-Z` plus a backspace key)
of which five columns are visible at 320 pixels wide (`A-E`, `J-N`, `S-W`). The
selection starts at `N`; a 15-frame D-pad hold moves it one key. The entry field
starts with one `N` because the `A` press that opens the screen is seen by the
keyboard. `A` types the selected letter, `B` confirms the current entry early, and
the game **auto-confirms after the tenth letter**. A wrong entry shows "Incorrect"
and returns to the title. The earlier observation that `B` cancels was wrong: `B`
is the confirm key.

Valid 3DO codes are ten letters (for example Fun 2 `IJJLDNCCCN`, Tricky 1
`HCEONONPDX`, Tricky 3 `CAJJLDMBEV`). The entry field shows the typed text plus a
blinking preview of the selected key, which is why short captures alternate between
one and two characters. The high-resolution passcode screen is simply the 320-wide
UI scaled 2x; its five visible columns are the game's scrolling window, not a
framebuffer crop, so high-res pointer/hit-testing is not a separate coordinate
contract. The remaining work is an input sequence that reaches the ten specific
letters, not the renderer.

### CLI diagnostics

`run --json` now reports `sourceWidth`, `sourceHeight`, and `vdlModulo`, which makes
VDLP mode/crop investigations mechanical.

### Space Ace

12,000-frame run: `PC 0x0007D114`, hash `0xEF99FED7`, animated demo FMV, no runaway.

### Audio

600 frames captured with `--audio wav`:

| Title | Duration | Peak | RMS L/R | Clipping |
|---|---:|---:|---|---:|
| Road Rash | 10.03 s | 32768 | 9051 / 9146 | 191 samples |
| Wolfenstein | 10.02 s | 30743 | 5620 / 5457 | 0 |

Both are 44.1 kHz stereo at ~737 samples/frame. The nominal rate is 735/frame;
the +0.27% cadence is small but should be checked against Opera/hardware before
changing the DSP reload value. The 18,000-frame Wolfenstein gameplay capture is
66.86 s, peak 32767, RMS 2980, with 18 clipped samples.

---

## 3. Ordered plan

### Step 1 - complete the menus

- Wolfenstein: done for the fresh-game loop; the existing 14,000-18,000 script and
  save/load proof are in section 2.
- Lemmings: DONE (2026-10-24 late pass, pure D-pad on 2026-10-26). The Fun 4
  passcode `HNLHCIOECW` is now typed entirely with the D-pad/A by
  `docs/scripts/lem-fun4-dpad.txt` from a passcode-screen state; the game shows
  the Fun 4 briefing at 22,600 and starts gameplay at 23,000 (`PC 0x00083FD8`,
  hash `0xB7846C2D`; zero-frame load roundtrips, two 120-frame continuations both
  `0xEDEB1B9D`). The earlier poke path (`lem-pass-N.state` + 9-byte poke + `B`)
  remains valid and reaches 21,640 (`PC 0x83FEC`, hash `0x18F38B4D`). The entry
  buffer is permuted (display order `77,76,75,74,7B,7A,79,78,7F,7E`; the preview
  is the next untyped slot), not sequential at `0x96574`.
- Road Rash: the control map is known (B accelerate, A brake, D-pad steer, C
  attack, P pause). Driver v10 is traffic-aware (close-traffic coast/steer,
  urgent preemption for close traffic, obstacle steer, wall avoidance,
  sharp-bend braking, closing-side crossover, emergency brake, narrow-canyon
  coasting, urban throttle pacing, coast floor, detector-upset hold, side-based
  stuck recovery, stopped/wreck throttle-only restart, low-band centre at rows
  190-214, road brightness <= 120; 667 tests pass). The CD media-access pulse
  fixes the old ~42k/~92k READ_ID wait: those screens now advance to `BUSTED`,
  race select, and a new race. The autopilot also pulses A during long-static,
  low-coverage, CD-load, or 180-frame restart-stuck states (`autopilotMenuAdvance`).
  The 5.5-mile race is still not finished: from the 60k state the autopilot
  passes `GAME LOADING`/`Get ready` and starts a new race at 80k, then cycles
  race -> wreck -> menu -> new race to 100k. Next: finish a full race without
  wrecking, not CD work. The previous "terminal wall pin ~34,000" was the
  `BUSTED` screen, and the old "~40,000 right-sidewalk wreck" was the CD wait,
  now fixed.
- Space Ace: **done.** `X` at 12,000 returns to the menu; `A` at 12,200 starts
  the interactive scene. The 15,000-frame state roundtrips (`0x7E780D93` -> two
  120-frame continuations at `PC 0x000284EC`, `0x70B16422`) and a gameplay WAV
  was captured. Longer scene/decision validation remains optional.

### Step 2 - save states and screenshots

**Done in the ninth pass.** Each title loads its live state at zero frames with the
saved hash, runs 120 frames deterministically, and has a PNG screenshot. The table
is in [`TestCorpus.md`](TestCorpus.md) 1.5a. NVRAM and disc state survive the
roundtrip; the CLI loads a fresh NVRAM by default unless `--nvram` is given. Space
Ace's interactive 15,000-frame state and Alone's Begin/Resume menu are also
captured. The remaining evidence work is longer per-title gameplay clips, not the
save/load proof.

### Step 3 - audio quality

- The 600-frame captures are in `TestCorpus.md` 1.5a. Road Rash's 91k-state
  capture has 378 clipped samples in 10 s (loud race mix); the other titles do
  not clip. Space Ace's capture is mono duplicated to stereo.
- Capture 30-60 s of a race/level and compare loudness/spectrum with Opera.
- Check the 737 vs 735 samples/frame cadence (all five titles are ~737).
- Verify the DSP stops/starts cleanly when a game changes audio modes.

### Step 4 - performance

- Measure the 3D CEL frame time on Road Rash's race (the renderer is now correct).
- Record p50/p95/p99 and allocations/frame; see [`Performance.md`](Performance.md).

---

## 4. Acceptance criteria

- [x] At least one title completes a full gameplay loop with input (Wolfenstein:
  fresh game -> walk/turn/fire -> save/load; Lemmings: passcode -> briefing ->
  gameplay -> save/load).
- [x] Save/load works in-game (Wolfenstein: `0xC9989182` -> `0x370158AC` twice;
  Lemmings Fun 4: `0x18F38B4D` zero-frame, two 120-frame continuations both
  `0x8D628FBD`).
- [x] WAV output is non-silent and stereo 44.1 kHz; clipping/cadence recorded.
- [x] Road Rash's race renders correctly and runs past 60,000 frames.
- [x] The high-resolution 640x480 renderer matches Lemmings' title reference.
- [x] The Lemmings passcode confirm path is complete (Fun 4 gameplay + roundtrip).
- [x] The Lemmings passcode is typed entirely with the D-pad
  (`docs/scripts/lem-fun4-dpad.txt`, gameplay at 23,000 frames,
  `PC 0x00083FD8`, hash `0xB7846C2D`, roundtrip `0xEDEB1B9D` twice).
- [x] CRT bloom is implemented in the OpenGL presenter and exposed in Settings.
- [x] HQx 2x/3x/4x are implemented and match the C++ hq2x/hq3x/hq4x reference
  hashes.
- [x] The benchmark budget gate runs in CI (0 bytes/frame, core p99 <= 8 ms,
  total mean <= 8 ms, total p99 <= 16.667 ms).
- [x] CLI settings/input/save-slot JSON and every `--json` response are
  source-generated; the trimmed win-x64 CLI passes `--version`, `selftest`,
  `bench`, `info`, `state inspect`, `state slots`, `fs list`, CHD boot/run, and a
  save-state roundtrip.
- [x] Portable CLI archives build for win-x64 (zip) and linux-x64 (tar.gz) and
  contain the executable, notices, licensing, and `PACKAGE.txt`.
- [x] The Avalonia frontend publishes trimmed for all six RIDs; a trimmed win-x64
  build opens the real window and passes the `--smoke-test` marker/exit-code check.
  `ViewLocator` reflection is gone and all bindings are compiled.
- [ ] The Road Rash city race is finished. The CD media-access pulse fixes the
  old ~42k/~92k READ_ID-only wait and advances to `BUSTED`, race select, and a
  new race; the autopilot passes `GAME LOADING`/`Get ready` and cycles
  race/wreck/menu to 100k. It still does **not finish a full 5.5-mile race**.
  The old 91k hash `0x02ABE78E` was a frozen presentation; the current chain
  reaches `PC 0x00093AF4`, hash `0x70E1ED8E`. The high-res pointer path is still
  unproven.
- [x] Space Ace's interactive path is validated (X menu, A play, 15,000-frame
  roundtrip and WAV).
- [x] Per-title save-state, screenshot, and WAV evidence is recorded
  (`TestCorpus.md` 1.5a).
- [x] `dotnet build -c Release`, `dotnet test`, and `dotnet format --verify-no-changes` pass.
---

## 5. Do not do

- Do not redo the eighth-pass work: the GPU palette per-channel CLUT fix, xBR filter,
  gamepad backends/mapping UI, log viewer, save-state thumbnails, or first-run/error
  dialogs are complete and tested. Extend them only with new evidence.
- Do not re-enable the `AudioSink?.PushSample(Dsp.Loop())` short-circuit.
- Do not change the XBUS DMA byte order (`IRam` uses `b0,b1,b2,b3`).
- Do not port `opera_cdrom.c` wholesale; the CD path is working.
- Do not commit BIOS ROMs, game images, extracted assets, or WAV captures.
- Do not change `Arm60.Ncycle` from 4 without a differential test.
- Do not "fix" the arbitrary-quad read/write order back to `(i, j)`; the row is `i`,
  the column is `j`, and the helpers take `(x, y)`.
- Do not enable high-resolution mode by default. Keep `--high-res` opt-in until the
  passcode pointer/hit-test path is understood; the five 3,000-frame low-res hashes
  must stay unchanged.
- Do not remove the CD media-access pulse (two consecutive READ_ID polls with a
  >=50-frame gap set poll bit `0x40`). It fixes the old ~42k/~92k READ_ID-only wait
  and keeps the five 3,000-frame hashes unchanged. The old 91k hash `0x02ABE78E` is
  historical; do not use it as a regression key.
- Do not redo the ninth-pass evidence capture, Space Ace interactive start,
  `cdStatus`/`cdPoll` diagnostics, or the autopilot menu-advance plumbing. Extend
  them with new evidence only.
- Do not redo the tenth-pass Lemmings pure D-pad passcode script
  (`docs/scripts/lem-fun4-dpad.txt`), CRT bloom, or HQx 2x. Extend them with new
  evidence only.
- Do not redo the eleventh-pass HQx 3x/4x patterns (`HqxPatterns3.cs`,
  `HqxPatterns4.cs`) or the benchmark budget gate. Extend them with new evidence
  only.
- Do not redo the twelfth-pass trim-safe JSON contexts, named CLI result records,
  `DiagnosticMethodInfo` watchpoint formatter, trimmed CLI publish, benchmark
  baseline comparison, or portable packaging script. Do not reintroduce
  reflection-based `JsonSerializer` calls in settings, input, save-slot, or CLI
  output code; use the existing `JsonSerializerContext` metadata.
- Do not redo the thirteenth-pass frontend trimming: `PublishTrimmed=true`/
  `TrimMode=full`, the removed `ViewLocator`, the explicit window construction, the
  compiled `x:DataType` bindings, or the `--smoke-test`/`--smoke-output` startup
  path. Do not reintroduce a reflection-based view locator or reflection-based XAML
  lookup in the frontend.

---

## 6. Useful commands

```powershell
$o = "artifacts\s2"
$b = Join-Path $PWD "bios\panafz1.bin"
$g = $env:THREEDO_GAMES   # office: D:\Samples\3DO; home: E:\Panasonic 3DO

# Build/test/format
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release
dotnet format --verify-no-changes

# Trimmed CLI publish and smoke test (see docs/Trimming.md)
dotnet publish src/ThreeDo.Cli/ThreeDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -m:1 -o "$o\cli-trim"
& "$o\cli-trim\ThreeDo.Cli.exe" selftest --json

# Trimmed frontend publish and startup smoke test (see docs/Trimming.md)
dotnet publish src/ThreeDo.Frontend.Avalonia/ThreeDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -m:1 `
  -o "$o\frontend-trim"
$p = Start-Process -FilePath "$o\frontend-trim\ThreeDo.Frontend.Avalonia.exe" `
  -ArgumentList '--smoke-test', '--smoke-output', "$o\frontend-smoke.txt" -PassThru
$p.WaitForExit(60000)
Get-Content "$o\frontend-smoke.txt"

# Road Rash: render the race and capture every 1,000 frames
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$g\Road Rash (USA).chd" --bios $b --frames 30000 `
  --capture-dir "$o\rr-race" --capture-every 1000 `
  --frame-out "$o\rr-race.png" --state-out "$o\rr30k.state" --json

# Wolfenstein: start a fresh game from the 12k demo state
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-fix12k.state" `
  --frames 2000 --input-script "$o\w3d-play.txt" `
  --frame-out "$o\w3d-play.png" --state-out "$o\w3d-play.state" --json

# Wolfenstein: walk/turn/fire from the fresh-game state
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-play.state" `
  --frames 4000 --input-script "$o\w3d-walk.txt" `
  --frame-out "$o\w3d-gameplay.png" --state-out "$o\w3d-gameplay.state" --json

# Lemmings: full 640x480 high-resolution title (opt-in)
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$g\Lemmings (USA).chd" --bios $b --frames 18100 --high-res `
  --input-script "$o\lem-menu.txt" --frame-out "$o\lem-hires-title.png" --json

# Lemmings: type the Fun 4 passcode with the D-pad and start the level
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$g\Lemmings (USA).chd" --bios $b --state-in "$o\lem_pass_fresh.state" `
  --frames 1300 --input-script docs/scripts/lem-fun4-dpad.txt --script-base state `
  --frame-out "$o\lem-dpad-gameplay.png" --state-out "$o\lem-dpad-gameplay.state" --json

# Save/load roundtrip (replace the state with a live gameplay state)
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-live.state" `
  --frames 120 --json

# Audio capture from a live state
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$g\Road Rash (USA).chd" --bios $b --state-in "$o\rr30k.state" `
  --frames 600 --audio wav --wav-out "$o\rr-game.wav" --json

# RAM dump at a trace stop
dotnet run -c Release --project src/ThreeDo.Cli -- trace `
  "$g\Road Rash (USA).chd" --bios $b --frames 16000 --trace-frames 2200 `
  --stop-kprint "AllocMem error" --out "$o\stop.txt" `
  --dump-ram "$o\stop-ram.bin" --dump-ram-offset 0 --dump-ram-length 0x300000

# Opera reference (the bundled Windows OperaCore build)
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$o\roadrash.iso"
```

The data-track extraction command is in [`TestCorpus.md`](TestCorpus.md).

---

## 7. Open questions

1. ~~What input sequence reaches the ten letters of a valid Lemmings passcode most
   reliably, and does the level start immediately after confirmation?~~ Answered
   2026-10-24: from the `N`-selected state, the 9-byte poke for `HNLHCIOECW` plus
   `B` is accepted, `A` shows the briefing, and `A` starts gameplay (see
   `TestCorpus.md` 1.4). Pure D-pad typing of all ten letters was not needed.
2. What obstacle-avoidance and wreck-recovery policy lets `--autopilot road` finish
   the 5.5-mile city race? Answered in part 2026-10-25: the "stuck bike" was the
   `BUSTED` screen, and the CD media-access pulse fixes the old ~42k/~92k
   READ_ID-only wait. The game now advances to `BUSTED`, race select, and a new
   race. The remaining experiment is **start-line/right-edge handling** after the
   race-select flow, not more CD or pre-race tuning.
3. Does the 737 vs 735 samples/frame cadence cause audible pitch drift? Compare a
   captured WAV with Opera/hardware before changing `_dspPcnt`.
4. Is the 191-sample Road Rash clipping in the game mix or in our DSP output path?
5. Does the VDLP width fix expose any 384/512-mode title that previously appeared
   only because of the 320-pixel crop?
6. Why does Wolfenstein's **fresh game** stay on the static level screen for more than
   54,000 frames? The CD trace shows one 24-sector read every ~33 frames (about
   87 KB/s) and revisits the 05:11-05:3b region. Setting the drive 2X status bit and
   publishing the Opera-style read-completion status were both tried and reverted:
   they either did not change the load or broke the golden CD status (0xE1 -> 0xE3)
   and the 3,000-frame corpus hashes. The demo gameplay at 12,000 frames is unaffected.
   The 18,000-frame walk loop now proves the level is playable. Next: compare the CD
   task's timing with Opera, check whether the drive speed query or read-completion
   status is expected, and measure CHD read time separately from guest-side pacing.
7. What is the exact high-resolution coordinate contract? The current mode treats
   CEL X as 640-wide and Y as a 240-line field that the VDLP line-doubles; this
   matches the Lemmings title. The passcode screen is a 320-wide UI scaled 2x, so
   its five visible keyboard columns are the game's own window, not a framebuffer
   crop. The remaining question is whether any title uses a true 640x240
   non-interlaced mode that the current line-double path should not upscale.
