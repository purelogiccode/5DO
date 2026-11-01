# Next Session Handoff

## 0. Current handoff (2026-10-30)

**2026-11-01 update:** UX-19 OSD notifications and the in-game quick menu are
implemented. `FiveDo.Host.Osd` adds a bounded, time-expiring notification list and
validated position/opacity/duration options; the game view shows colour-coded toasts
for save/load, slot changes, rewind, fast-forward, screenshots, reset, and settings.
**F4** or the gamepad Select+Start chord opens a quick menu that pauses the game and
offers resume/pause, save/load, rewind, fast-forward, screenshot, reset, cheats,
settings, stop, and close; Up/Down/Home/End and Enter navigate, and closing resumes
only when the game was running before it opened. Settings > Video exposes OSD enable,
position, opacity, duration, and quick-menu enable controls. **Do not redo the
notification model, quick-menu controls, gamepad chord, or settings surface; remaining
UX-19 polish is full gamepad navigation of the menu (UX-08) and richer OSD types such
as achievement/recording badges.** The Lemmings 300-frame differential smoke passes
(1 match, 0 diverged). **1336 tests pass.**

**2026-11-01 update:** UX-12 BIOS/NVRAM manager is implemented. **File > BIOS / NVRAM
Manager...** opens a two-tab window. The BIOS tab scans and classifies images, imports
validated `.bin`/`.rom` files without overwriting a different image, sets BIOS1/BIOS2,
and pins or clears the current game's BIOS MD5. The NVRAM tab lists shared and per-game
images with validation, game metadata, version, size, and modification time; it backs
up one image or the whole layout, imports a validated 32 KB image, resets an image to a
fresh volume, and deletes an image. Reset, delete, and import-overwrite require a
second click on the same button. `BiosInventory` and `NvramInventory` isolate the file
operations and use atomic NVRAM writes. **Do not redo the inventory helpers, the
manager window, the menu wiring, or the backup/import/reset/delete semantics; remaining
UX-12 polish is per-game NVRAM assignment from the list and drag-and-drop import.**
**1309 tests pass.**

**2026-11-01 update:** SYS-15 input devices are implemented. `InputDeviceSet` selects
a joypad, flightstick, mouse, lightgun, arcade lightgun, Orbatak trackball, or no
device on each of six PBUS ports; `PbusChainEncoder` emits Opera's exact daisy-chain
records (2/9/4/5/5/4/3 bytes plus `0xFF` padding) and the legacy 4DO packet is kept
while every port is a joypad. Settings > Emulation > Input devices and the CLI
`--input-device 1=mouse,3=lightgun` select devices; `--pbus-format opera` forces the
chain for differential tests; `run --json` reports `inputDevices`. The desktop pointer
drives the first mouse/lightgun/trackball port, with a crosshair overlay for lightguns.
**Do not redo the device models, encoder, CLI/settings surface, pointer plumbing, or
`PbusDecoder` chain support; remaining SYS-15 work is per-device calibration,
multiplayer pointer routing, and scripted gameplay validation. Crime Patrol, Mad Dog
II, Orbatak, and Shootout at Old Tucson boot and detect the selected arcade device.**
**1294 tests pass.**

**2026-11-01 update:** UX-11 disc browser and disc info are implemented.
`FiveDo.Host.DiscBrowser` mounts `OperaFileSystem` from an ISO/BIN/CUE/CHD without
starting the machine, builds a sorted tree with sizes/LBAs, extracts a selected
file, directory, or the whole disc with cross-platform name sanitization and
destination-root containment, and exposes volume/game-database/fix metadata.
File > Disc Browser... and File > Disc Info... use the new windows; both are
constructed by the trimmed frontend smoke test. **Do not redo the browser model,
extraction, or the two windows; remaining UX work was UX-19 OSD/quick menu (now
implemented) and the TAS input editor/Lua work.** **1246 tests pass.**

**2026-11-01 update:** UX-06 TAS movies are implemented as a partial first increment.
`FiveDo.Tas` owns the versioned `.5domovie` container (per-frame PBUS packets, optional
starting state, CRC-32/Brotli), deterministic record/replay, branch/trim/append, and
frame/PC/state verification. The CLI adds `movie info|record|play|verify|branch` and
`run --movie-record/--movie-play/--movie-verify/--movie-no-state`; verification failure
returns exit 2. The shell adds Emulation > Record Movie.../Play Movie.../Stop Movie and a
player-1 input display, and playback pauses at the end. `PbusDecoder` decodes standard
PBUS packets. **Do not redo the movie container, recorder/playback sources, verifier,
editor, CLI commands, or shell controls; remaining TAS work is Lua, a frame-by-frame
input editor, an input timeline, and multiplayer input display. A separate lag counter is
not applicable because 3DO PBUS input is sampled every frame.** **1239 tests pass.**

**2026-11-01 update:** UX-04 per-game cheats are implemented. `FiveDo.Cheats` parses
raw/typed writes, memory/button conditions, and `and`/`or`/`not` combinations;
`CheatStore` reads/writes atomic `<gameId>.json` documents; `FiveDoMachine.BeforeFrame`
applies enabled entries every frame (normal play, fast-forward, and rewind replay);
the shell adds Emulation > Cheats... and Settings > Paths > Cheats; the CLI adds
`--cheat`, `--cheat-code`, `--cheat-auto`, `--cheats-dir`, and
`cheat list|add|enable|disable|remove|import|export`. 3DO has no standard RAM cheat
format (Game Guru is a save editor), so the documented 5DO syntax is used and no code
databases are shipped. **Do not redo the cheat parser, store, per-frame hook, manager,
CLI commands, or docs; remaining cheat work is optional code-type extensions and a
community code format once one is documented.** **1199 tests pass.**

**2026-10-31 update:** the differential harness (FND-05/CORE-11/CORE-16) is
implemented. `tools/FiveDo.DiffHarness` stages the bundled OperaCore and BIOS,
extracts/caches a data ISO from the disc, runs 5DO and OperaCore with the same
options, compares checkpoint frame hashes, applies manifest allowances, and writes
JSON/Markdown/HTML reports. `run` adds `--checkpoints`, `--audio-hash`, and
`--state-hash`; `FiveDo.DiffHarness baseline` gates report changes. The Lemmings
(USA) 300-frame smoke matches at `0x110E7DC5`; the early boot frames diverge and
are recorded as a scheduling finding. Manifests are
`benchmarks/diff/smoke.json` and `benchmarks/diff/showcase.json`. **Do not redo the
checkpoint contract, hash algorithms, staging, comparer, reports, or baseline
command; remaining work is the full-collection manifest, reference-side PC/state/
CD-transcript exports, visual artifacts, and the nightly baseline.** See
[`DifferentialHarness.md`](DifferentialHarness.md).

**2026-10-31 update:** SYS-14 per-game NVRAM and versions are implemented.
`NvramStorageMode` (`Global`, `PerGame`), `NvramVersion` (0-9), and `NvramDirectory`
select shared `nvram.ram`, versioned `nvram.<n>.ram`, or
`nvram/per-game/<gameId>.<n>.ram` images. `NvramLayout` owns path resolution and
migration fallbacks; `NvramStore` reads the fallbacks without copying, writes atomically,
and backs up a corrupt primary. Settings > Emulation exposes the mode and version; the
CLI adds `--nvram-mode`, `--nvram-version`, `--nvram-dir`, `run --json`
`nvramPath`/`nvramMode`/`nvramVersion`, and `nvram path|list|export|import|reset`.
Unit, store, settings, JSON-contract, and CLI tests pass. **Do not redo the NVRAM layout,
store, settings controls, or CLI commands; remaining work is the UX-12 manager UI
(library listing, per-game assignment, and backup/restore dialogs) and portable-mode
path selection.**
**1125 tests pass.**

**2026-10-31 update:** SYS-04 BIOS manager and auto-selection are implemented.
`BiosCatalog`/`BiosLibrary` scan `.bin`/`.rom` files, compute MD5, validate sizes, and
classify primary/secondary, region, video standard, Kanji, Anvil, and patch state.
`--bios2` and `--bios-auto` work on `run`/`trace`; `bios list|scan|select` expose the
catalog and selection; `run --json` reports `biosPath`, `biosMd5`, `biosName`,
`biosRegion`, `biosSelection`, and `biosSource`. Settings > Paths adds the BIOS folder,
primary/secondary paths, auto-select, and scan; Settings > Emulation adds a per-game
BIOS override; File adds assign/clear actions. Auto-selection uses game-database
regions, known requirements (Slayer Europe/USA, Powers Kingdom Europe, Arcade CD-ROM),
and per-game MD5 pins; the configured BIOS remains the fallback when no folder is set.
The five-title 3,000-frame and four known-requirement 12,000-frame smoke runs are in
[`TestCorpus.md`](TestCorpus.md) 1.1f. **Do not redo the BIOS catalog, scanner,
selector, CLI commands, or settings UI; remaining work is the full-collection
BIOS-aware sweep and per-game auto profiles (SYS-18).** **1125 tests pass.**

**2026-10-31 update:** SYS-09 CD speed control and CORE-08 timing profiles are
implemented. `CdSpeed` (`Unbounded`, `X1`-`X32`, default `X2`) changes the effective
speed of the cycle-based CD model with Opera's per-speed overhead table;
`--cd-speed`/Settings expose it, `run --json` reports `cdSpeed`, and Options section v5
records it with a mismatch warning. `EmulationProfile` (`Compatible`, `Accuracy`,
`Performance`, `Custom`) bundles the timing options through `--profile` and Settings >
Emulation; the accuracy profile enables ARM60, Opera timers, CEL busy timing, and cycle
CD reads. The default FreeDO path is unchanged, and the three-title smoke differential is
in [`TestCorpus.md`](TestCorpus.md) 1.1e. Do not redo the CD speed selector or the profile
mapping; remaining work is per-profile benchmark deltas, a full-collection accuracy run,
and CD-DA speed behavior with SYS-01. **1039 tests pass.**

**2026-10-31 update:** the CORE-04 CD cycle-timing option is implemented as the first
increment of the CD port. The default `--cd-timing freedo` path is unchanged;
`--cd-timing opera` schedules each 2048-byte data sector at the stock 2x drive speed,
clears the data/status poll bits while busy, publishes the read-data status after the
transfer drains, and raises the XBUS CD FIQ when ready. Settings > Emulation exposes
the choice, `run --json` reports `cdromTiming`, the CD-ROM section is version 2, and the
Options section is version 4. The three-title 12,000/24,000-frame smoke differential is
in [`TestCorpus.md`](TestCorpus.md) 1.1d. Do not redo this option; the remaining CD work
is command latency, disc-derived TOC, read-ahead, subcode, DIPIR tagging, CD-DA, and ODE;
SYS-09 later added CD speed control. **1014 tests passed at that point.**

**2026-10-31 update:** the CORE-03 MADAM CEL busy option is implemented. The default
`--cel-timing freedo` path is unchanged; `--cel-timing opera` defers the CEL draw to the
next CPU tick and holds the status register busy for the estimated pixel/texel work.
Settings > Emulation exposes the same choice, `run --json` reports `celTiming` and
`celBusyCycles`, and save states record the mode in the Options section (v3) plus the
pending/busy fields in the Madam section (v2). The 3,000-frame smoke differential is in
[`TestCorpus.md`](TestCorpus.md) 1.1c. Do not redo this option; the remaining work is the
full-collection differential, a CEL-status polling title, and a frame-time check before
considering it the default. **1003 tests pass.**

**Goal:** finish the BIOS-aware compatibility triage, fill the 5DO statuses, and then
start high-resolution CEL research. Keep the differential harness green on the
showcase and extend it to the full collection. Do not work on gameplay depth and do
not cut a release yet.

**Read first:**

- [`GameCompatibilityList.md`](GameCompatibilityList.md) — 241 4DO reference titles,
  local matches, empty 5DO status columns.
- [`CompatibilitySweep.md`](CompatibilitySweep.md) — sweep procedure, status
  definitions, triage, and Linux VM validation.
- [`Compatibility.md`](Compatibility.md) §13 — sweep and 2026-10-30 triage results.
- [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md) — current
  `--high-res` limitations and the M14 research options.
- [`Status.md`](Status.md) — current position and the deferred release path.

**Inputs:** `J:\Panasonic 3DO` (589 CHDs), `G:\MAME\MAME Software List CHDs\3do`
(5 CHDs), `bios/panafz1.bin`, `bios/panafz10.bin`, `bios/panafz10-patched.bin`,
`bios/panafz10e-anvil.bin`, `bios/panafz10ja-anvil-kanji.bin`, and the saved 4DO wiki
MHTML in `References/4DO-master/`.

**What changed on 2026-10-30:**

- The 594-disc baseline sweep is complete: 576 `Loadable/Rendered`,
  13 `Loadable/No video`, 5 `Not Loadable`.
- `Mirai Shounen Conan` was a transient CHD read error; it now reaches its menu with
  hash `0xBE2A023B`. The four Arcade CD-ROM rows need `panafz10-patched.bin` and the
  default headless pad; unpatched BIOSes reach only a starfield or the 3DO
  storage-manager shell.
- The 12,000-frame rerun shows Ishida, both Konpeki no Kantai discs, and all three Myst
  discs rendering. Powers Kingdom (Europe) reaches a live-action intro with Anvil;
  Slayer Europe renders credits with `panafz1`, while Slayer USA needs `panafz10` for
  its title menu at 60,000 frames. Crash 'n Burn (Japan) and WorldCup Super Stadium
  show the same Anvil boot screen and are still stuck at 60,000 frames.
- Headless `run`/`trace` now attach a default player-1 pad. Olympic Summer Games
  proceeds past its controller check. The 3,000-frame corpus keys were re-baselined in
  [`TestCorpus.md`](TestCorpus.md) 1.1. **954 tests pass.**

**Ordered tasks:**

1. Build the Release CLI:
   `dotnet build src/FiveDo.Cli/FiveDo.Cli.csproj -c Release`.
2. Rerun the `Not Loadable` and `Loadable/No video` subsets with
   `tools/bios-probe.ps1` or `--bios-auto bios`, recording which BIOS each disc needs.
   `bios select <disc> --folder bios` prints the chosen image and reason. Use a separate
   output directory; do not overwrite the `panafz1` baseline.
3. Investigate Crash 'n Burn (Japan) and WorldCup Super Stadium, which still stop on
   the Anvil boot screen at 60,000 frames. Keep the smallest repro and a trace; compare
   the Japan revision with the working Crash USA/Korea revisions.
4. Inspect the rendered 12,000-frame captures and fill the 5DO status/notes in
   [`GameCompatibilityList.md`](GameCompatibilityList.md), including the BIOS used.
5. Run the high-resolution subset with `-HighRes -OutDir artifacts/compat-highres`.
6. Validate the CLI in the `LinuxMint` Hyper-V VM
   (`D:\Hyper-V\VMs\LinuxMint`); see [`CompatibilitySweep.md`](CompatibilitySweep.md) §7.
7. Start M14 measurements from [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

**Status scale:** No data / Not Loadable / Unplayable / Playable / Good, matching the
4DO wiki. `Loadable/Rendered` and `Loadable/No video` are intermediate automated labels,
not final statuses.

**Do not redo:** the 4DO MHTML parser, the sweep harness, the OpenAL/Serilog/trimming
work, the renderer/filter work, the ARM/CLIO/CEL/CD timing options, the CD speed selector and
timing-profile mapping, the BIOS catalog/scanner/selector or its CLI/settings surface,
the NVRAM layout/store/settings controls or the `nvram` CLI commands, the TAS movie
container/recorder/verifier/editor or its CLI/shell surface, the OSD notification model
or the quick menu/chord/settings surface, or
the 2026-10-31 performance pass (CEL pipeline caching, Debug core optimization,
render-request coalescing, Auto frame skip). Do not treat full-race completion, inventory
scripting, Pi/arm64 validation, or installers as current gates.

**Project position:** donationware, non-commercial, source-available. The legal
sign-off and the GitHub Actions billing issue remain release blockers, but release is
deferred until M13/M14 complete.

---

**Date:** 2026-10-29 (twenty-fifth pass: F6/F7 slot selection, F9 single-frame
advance, and desktop input recording to a replayable `--input-script` file; the
twenty-fourth pass added first-party Opera file-system parsing for
ISO/BIN/CUE/CHD in `fs list`/`fs extract`, multi-block directory chaining, and
`burst`/`gap` file reads; the twenty-third pass added native Win32 Vulkan swapchain and
`--vk-window-probe`, `PreferOpenGl` enforcement, custom aspect editing, frontend AOT
audio validation, and bug-report hygiene; the twenty-second pass added save-state
format version 2 with per-section Brotli compression; the twenty-first pass added
Vulkan xBR/FSR/HQx filters and the GPU-palette xBR path in the OpenGL presenter; the
twentieth pass added the optional offscreen Vulkan backend and `--vk-probe`; the
nineteenth pass added save-state disc warnings, console hotkeys, ISO/BIN `disc tracks`,
and OpenAL Soft LGPL packaging; the eighteenth pass added Serilog logging and bug
reporting; the seventeenth pass added the OpenAL crackle fix, PCM tap, and race profile)

**Vulkan performance follow-up (2026-10-30, twenty-sixth pass):** the offscreen Vulkan
readback is now on demand. Probes and screenshots use a one-shot copy, and the GPU
timestamp covers the render passes only; 4K sharp-bilinear GPU time is 0.075 ms and
present 0.24 ms. The desktop fallback writes the captured frame directly into the
locked `WriteableBitmap`, and the swapchain blit uses a fence instead of
`vkQueueWaitIdle`. The desktop path is still offscreen plus per-frame readback, so
OpenGL remains the full-performance presenter. **924 tests pass.**

**Vulkan embedded surface and core-runner fix (2026-10-30, twenty-seventh pass):**
`VulkanVideoSurface` hosts a click-through Win32 child HWND inside the Avalonia window
and presents through the FIFO swapchain, so `--gl-backend vulkan` no longer reads frames
back to the CPU or uploads a `WriteableBitmap`. `CoreRunner` now uses a monitor
handshake with an explicit paused state, fixing rapid stop/start cycles. **925 tests
pass.**

**Goal:** make the audio path correct and testable. The sixteenth pass replaced the
FreeDO 8.24 DSP sample accumulator with an exact 44,100-samples-per-ARM-second clock
(600 frames now produce 441,015-441,020 samples / 10.000 s instead of 444,097 /
10.070 s), preserved VDL/timer reference timing, bumped the Quartz state section to
version 2 with v1 conversion, and re-baselined the 3,000-frame boot keys. It also
fixed live OpenAL output: the runtime is bundled (OpenAL Soft 1.23.1), the context
is made current on the audio thread, underruns are padded/counted, and the CLI has
`--audio openal`, `--volume`, `--realtime`, and `audio analyze <wav> --json`. Core
frames are paced by `AudioFramePacer` (audio clock with a 60 Hz fallback), WAV writes
are buffered with streaming peak/RMS/clipping statistics, and the NativeAOT CLI now
opens the live backend. **906 tests pass.** The twenty-third pass added native Win32
Vulkan swapchain presentation and `--vk-window-probe` (4K FIFO: 1.34/1.43 ms GPU,
5.99/6.24 ms present including the display wait, 0 managed bytes/frame), honored
`PreferOpenGl` as an explicit CPU mode, added custom aspect editing, validated the
frontend NativeAOT OpenAL path with `ALSOFT_DRIVERS=null`, and stopped expected
environmental warnings (such as a missing audio device) from filing bug reports.
The twenty-second pass added state format
version 2: large section payloads are Brotli-compressed, the CRC stays over the
uncompressed data, version-1 states still load, and `state inspect` reports stored and
compressed sizes. A 24,902,592-byte local state becomes 647,647 bytes (300-frame Gex)
or 2,001,229 bytes (14,900-frame Gex). The twenty-first pass added the Vulkan
xBR/FSR/HQx filters and made the OpenGL xBR path sample the GPU-expanded palette
texture directly. The eighteenth pass replaced the custom
logger with Serilog and added the asynchronous bug-report sink. The seventeenth pass fixed live OpenAL
crackle by giving the fill loop real buffer ownership, added the `FIVEDO_AUDIO_TAP`
PCM/timing recorder and overlay peak meter, added the FSR 1.0 filter, and recorded
the Road Rash 3D-race profile. Do not redo the Serilog/bug-report work, the trim-safe JSON work, the
trimmed CLI/frontend publishes, the frontend smoke test, the benchmark comparison,
the portable archives, the swap-interval/GPU-probe work, the ANGLE/GLES parity work,
this audio pass, the 2026-10-29 release-hardening pass (save-state disc warnings,
console hotkeys, ISO/BIN `disc tracks`, and OpenAL Soft LGPL packaging), the
twenty-first Vulkan xBR/FSR/HQx filter pass, the twenty-second save-state
compression pass, the twenty-third Vulkan-window/release-polish pass, the
twenty-fourth Opera file-system pass, or the twenty-fifth hotkey/input-recording
pass. The remaining
**release** work is the legal/attribution review
(now including OpenAL Soft's LGPL runtime), a stability soak, release
packaging/notes, and optional Linux/macOS launch validation. GitHub Actions is
currently blocked before job start by a billing/spending-limit annotation; that is an
external account action, not a code failure. Full-race completion,
extra title scripts, GLES 3.1/Pi/DPI, Tier B timing, arm64/installers, and the
optional JIT are post-1.0. See
[`ReleaseChecklist.md`](ReleaseChecklist.md).

> **Release-scope update (2026-10-26):** v1.0 ships a documented compatibility set,
> not full compatibility. Start with [`ReleaseChecklist.md`](ReleaseChecklist.md);
> the gameplay work in sections 2-3 is post-1.0 backlog.

Earlier passes added the presentation plumbing (GL/GLES capability reporting, CPU
fallback/context-loss recovery, fullscreen, screenshots, About, Hi-Res, benchmark
gates, prefetch, and the 4K probe) and the game-specific fixes recorded below. Read
section 1.18 for the fourteenth-pass changes before starting.

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
sets the line width accordingly, and `FiveDoMachine` derives `SourceWidth` from the
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
  `--disc <path>` for file associations, ships `packaging/5do.desktop`, can
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
  (`FiveDo.Host`), `InputProfileJsonContext` (`FiveDo.Input`), and `CliJsonContext`
  (`FiveDo.Cli`). The CLI's anonymous response objects were replaced with named
  records in `src/FiveDo.Cli/CliJsonModels.cs`; the JSON field names are unchanged.
- **Reflection removal.** `WriteRunResult` and `selftest` read typed properties
  instead of `Type.GetProperty`; the RAM-write watchpoint formatter uses
  `DiagnosticMethodInfo.Create` instead of `StackFrame.GetMethod`.
- **Trimmed CLI.** `FiveDo.Cli` sets `PublishTrimmed=true` and `TrimMode=full`.
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
  `5do.desktop` for Linux. Windows archives are zip; Linux/macOS archives
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
- **Trimmed frontend.** `FiveDo.Frontend.Avalonia` sets `PublishTrimmed=true` and
  `TrimMode=full`. The six-RID CI publish job now uses `PublishTrimmed=true` instead
  of `false`. CHDSharp's vendored-FLAC `TypeDescriptor` warning is demoted to a
  visible warning with the same policy as the CLI; the data-track path does not decode
  CD-DA/FLAC.
- **Frontend smoke test.** `--smoke-test --smoke-output <path>` opens the real
  `MainWindow`, runs one UI pass, writes `FRONTEND SMOKE OK <version>`, and exits
  with code 0. The GL surface is hidden during the smoke so headless CI does not
  need a desktop OpenGL context. It skips the welcome dialog and configured
  BIOS/disc flow so CI needs no BIOS, disc, or user profile. `--version` exits
  before Avalonia initializes. The startup parser (`--disc`, positional disc,
  `--smoke-test`, `--smoke-output`, `--version`) has unit tests in
  `tests/FiveDo.Tests/Frontend/FrontendStartupTests.cs`.
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

### 1.18 Fourteenth pass: swap interval and true-4K GPU probe (2026-10-26)

- **Swap interval.** `FiveDo.Video.OpenGL.SwapInterval.TryCreate` resolves the
  platform extension in priority order: `WGL_EXT_swap_control`,
  `GLX_EXT_swap_control`, `GLX_MESA_swap_control`, `GLX_SGI_swap_control`, then
  `EGL_KHR_swap_interval`. `OpenGlRenderer` applies `RendererSettings.Vsync`
  (`1`/`0`) during `Initialize` and when the setting changes, and reports
  `VsyncActive`/`VsyncBackend`. `RendererCapabilities.SupportsVsync` is now honest
  instead of always false. Unit tests cover selection priority, missing extensions,
  and the capability overload.
- **GPU probe.** `--gl-probe <path>` opens a code-only `GlProbeWindow`, presents
  synthetic 320x240 VDL frames through the real Avalonia `VideoSurface`, and writes
  per-frame JSON. Options: `--gl-probe-size WxH`, `--gl-probe-frames N`,
  `--gl-probe-warmup N`, `--gl-probe-filter`, `--gl-probe-vsync`,
  `--gl-probe-offscreen`, `--gl-probe-timeout`, `--gl-probe-capture`, and
  `--gl-backend`/`--gl-probe-backend`. The offscreen path renders into
  an RGBA8 FBO (`OffscreenRenderTarget`), so a true 4K pass works on a smaller
  desktop. The probe reports vendor/renderer/version, output size, filter, present
  and GPU p50/p95/p99/max, total and per-stage allocations, and vsync state.
- **Measurements.** Windows 11, Ryzen 9 7900, RTX 4070, OpenGL 4.0, 120 measured
  frames after 30 warm-up frames. 4K sharp-bilinear: GPU mean 0.086 ms / p99
  0.089 ms; present mean 0.080 ms / p99 0.208 ms; 0 renderer/mailbox allocations
  per frame. 4K accurate: GPU p99 0.287 ms, present p99 0.178 ms. 4K CRT: GPU p99
  0.193 ms, present p99 1.157 ms. 4K xBR: GPU p99 0.205 ms, present p99 1.788 ms.
  1080p sharp: GPU p99 0.146 ms, present p99 0.335 ms. Full table in
  [`Performance.md`](Performance.md).
- **Vsync note.** The WGL extension is found and accepted (`vsyncActive: true`),
  but the Avalonia compositor paces the window independently; the probe reports
  ~64 fps throughput even with vsync requested. The renderer cost remains far
  below the 16.67 ms budget.
- **Allocation note.** After warm-up, `OpenGlRenderer.Present` and the mailbox
  report **0 managed bytes/frame**. The probe's residual ~2.3 KB/frame is outside
  the renderer and comes from the Avalonia UI/compositor loop; it is a host-side
  follow-up.
- **Tests.** 741 tests pass. New tests cover probe option parsing/filter aliases,
  the JSON contract, swap-interval selection, vsync capability reporting, and
  offscreen-target validation.

---

### 1.19 Fifteenth pass: ANGLE/GLES validation and CPU/GPU parity (2026-10-26)

- **Backend selection.** The frontend accepts `--gl-backend auto|wgl|angle|software`
  (alias `--gl-probe-backend`) and maps it to `Win32PlatformOptions.RenderingMode`.
  `auto` preserves the previous WGL -> ANGLE -> software order. The probe report
  records `requestedBackend`.
- **Probe capture/parity.** `--gl-probe-capture <path>` writes the presented frame as
  a PNG. For a 320x240 Accurate capture, the probe compares the GPU pixels with
  `FrameAssembler` and records `parity` (`matches`, `differentPixels`, `cpuHash`,
  `gpuHash`, `verticalFlip`). Measurement allocations are snapshotted before the
  capture so the readback buffer does not distort the steady-state allocation result.
- **ANGLE results.** On Windows 11 / Ryzen 9 7900 / RTX 4070, ANGLE reported
  OpenGL ES 3.0 via Direct3D 11, integer textures, and `EGL_KHR_swap_interval`.
  4K sharp-bilinear: present mean 0.14 ms, p99 0.47 ms; accurate 0.15/0.48; CRT
  0.14/0.43; xBR 0.37/0.83; 1080p sharp 0.13/0.40; windowed 720p sharp 0.13/0.49;
  all with 0 renderer/mailbox bytes per frame. HQx 4x is CPU-bound at 10.44/14.22 ms.
  ANGLE has no `GL_TIME_ELAPSED`, so GPU timing is `null`; present timing is valid.
- **Parity.** WGL (OpenGL 4.0/GLSL 330) and ANGLE (OpenGL ES 3.0/GLSL ES 300) both
  matched the CPU oracle exactly: 0 different pixels, FNV-1a hash `0xD6BD89C5`
  (`3602745797`), no vertical flip.
- **Additional corpus.** Seven more discs booted, rendered, and save-state-roundtripped
  at 12,000 frames: Gex, Need for Speed, Super Street Fighter II Turbo, Total Eclipse,
  Killing Time, Theme Park, and Return Fire. Gex, Total Eclipse, and Super Street
  Fighter II Turbo show gameplay/attract scenes; six produce non-silent 44.1 kHz
  stereo audio (Theme Park's menu is silent). The hashes, WAV stats, and commands are
  in [`TestCorpus.md`](TestCorpus.md) 1.7 and the matrix is in
  [`Compatibility.md`](Compatibility.md) §10.
- **Capture reentrancy fix.** The opt-in GPU integration test
  (`FIVEDO_GPU_TESTS=1`) runs the built frontend probe for WGL and ANGLE, checks
  Accurate parity and a 4K pass, and found a real crash: the capture consumer can
  finish the probe and dispose the surface synchronously while `OnOpenGlRender` is
  still using the adapter. The render path now returns after the capture callback
  when the surface has been disposed.
- **NativeAOT CLI/frontend audit.** `FiveDo.Cli` publishes with NativeAOT via the
  opt-in `-p:FiveDoNativeAot=true` property. The win-x64 single file is 9.97 MB
  (trimmed IL is 26.3 MB) and passes `--version`, `selftest`, `bench`, `info`,
  `fs list` on Lemmings (482 files), and a 3,000-frame boot matching the golden
  `PC 0x0012B3C8` / hash `0x932C7722`. The frontend publishes as a 28.8 MB
  single-file native executable plus native Skia/HarfBuzz/ANGLE assets and passes
  `--smoke-test` plus the WGL and ANGLE 320x240 parity probes. Silk.NET.Core loader
  warnings remain and AOT audio is not yet validated. CI publishes and smoke-tests
  both AOT builds on Windows. The default six-RID matrix remains trimmed IL.
- **Tests.** 775 tests pass. New tests cover backend aliases/mode ordering, probe
  capture-path parsing, the opt-in GPU integration path, and the physical-pixel
  sizing math (100/125/150/200%, fractional bounds, invalid inputs). The existing
  JSON contract test now includes the requested backend and parity fields.

---

### 1.20 Sixteenth pass: exact DSP clock, live OpenAL, and audio analysis (2026-10-27)

- **Exact DSP sample clock.** `Quartz` now accumulates actual ARM cycles toward the
  next 44.1 kHz sample (`_dspCycleRemainder += cycles * 44100`, threshold
  `ArmClock`) instead of FreeDO's `arm * SND_CLOCK` 8.24 rounding. The ARM/VDL/timer
  accumulators are unchanged. One ARM second is exactly 44,100 samples; a 600-frame
  Lemmings boot is 441,015-441,020 samples (10.000 s) instead of 444,097 (10.070 s).
  Opera's 600-frame capture is 441,441 samples / 10.010 s at its 59.94 Hz field
  rate. The old/new 6,000-frame Road Rash WAVs have the same peak (29,951), RMS
  within 0.2%, and the same spectral peaks (442.1 Hz); only the timebase changed.
- **Quartz state v2.** The exact cycle remainder is serialized; version-1 sections
  are accepted and convert the legacy 8.24 fraction. Existing archived states still
  load (`lem_pass_fresh.state`, `w3d_gameplay.state`, Road Rash states).
- **3,000-frame re-baseline.** Alone `0x4EC2E1A7`, Lemmings `0x6CCA83F5`, Road Rash
  `0xCE90FA5A`, Space Ace `0xF349263B`, Wolfenstein `0x9A3B5131`. Alone/Lemmings
  screenshots are pixel-identical before/after; Road Rash/Space Ace are adjacent FMV
  frames; Wolfenstein differs only in the star field. The Lemmings Fun 4 D-pad script
  still reaches gameplay at 23,000 frames.
- **Live OpenAL fixed.** `OpenAlAudioOutput` makes the context current on the fill
  thread (OpenAL context state is thread-local), bundles OpenAL Soft 1.23.1 via
  `Silk.NET.OpenAL.Soft.Native`, copies `soft_oal.dll` as `OpenAL32.dll` on Windows,
  pre-fills two buffers, pads/counts underruns, and retries a stopped source. The
  trimmed single-file CLI and the opt-in NativeAOT CLI both open the backend; the
  opt-in `FIVEDO_AUDIO_TESTS=1` test plays a real 440 Hz tone.
- **Audio-driven pacing.** `FiveDo.Host.AudioFramePacer` averages 30 frames of
  `FiveDoMachine.AudioSamplesProduced`, sets the period to `samples / 44100`, clamps
  to 50-70 fps, and falls back to 60 Hz. `CoreRunner` uses it for the core thread and
  reports the target rate in `CoreStatus`; the overlay shows measured/target fps.
  `run --realtime` applies the same idea in the CLI (300 frames = 5.000 s with the
  OpenAL null backend; 220,510 samples, 529 dropped, 14 padded buffers, ~89 ms
  output latency).
- **WAV and diagnostics.** `WavAudioSink` buffers PCM writes and tracks frames, peak,
  RMS, clipping, silence, and DC offset. `AudioAnalyzer` validates/analyzes PCM WAV
  files; `audio analyze <wav> --json` prints the report. `run --json` includes the
  stats and OpenAL queue counters; `--elapsedSeconds` was added for pacing checks.
- **Tests.** 798 pass. New coverage: exact 44,100-sample count across batch patterns,
  Quartz v1 conversion/v2 validation, audio statistics and WAV analyzer roundtrips
  (including mono/odd chunk/truncation), buffered sink flushing, frame-pacer behavior,
  CoreRunner target-rate reporting, reset/state-load audio flushing, CLI audio
  JSON, and the opt-in OpenAL tone.

Do not revert the exact clock to the FreeDO 8.24 DSP accumulator. It is the
difference between 44.1 kHz and ~44.4 kHz audio. Do not remove the bundled OpenAL
runtime or move the context-current call back to the UI thread.

### 1.21 Seventeenth pass: OpenAL crackle root cause, PCM tap, and race profile (2026-10-28)

- **Crackle root cause.** The OpenAL fill loop selected the next buffer with
  `_buffers[queued]`. OpenAL unqueues the oldest buffers first, so that index
  named buffers still attached to the playing source; `BufferData` and
  `SourceQueueBuffers` then rewrote audio that was about to play, producing
  constant clicks while the underrun and dropped counters stayed at zero.
  Buffers now come from an `AudioBufferPool` free list, variable-size blocks are
  queued, queued frames are tracked exactly, and `alGetError` is checked after
  every queue.
- **Threading.** The fill thread is registered with
  `AvSetMmThreadCharacteristics("Pro Audio")` and runs at above-normal priority.
- **Diagnostics.** `FIVEDO_AUDIO_TAP=<prefix>` records the exact queued PCM
  (`<prefix>.wav`) and a per-buffer CSV (elapsed time, frames, ring/queue depth,
  underrun, peak, OpenAL error). The status overlay shows a live peak meter.
  Tap validation: 0 OpenAL errors, 0 underruns, and 0 discontinuities across
  2,345 buffer boundaries; the tap crack metric is 4.5/s versus the DSP baseline
  15.6/s and the pre-fix frontend 320.7/s.
- **Video.** Restored the `(float)` casts on `glUniform2/4` so Silk.NET selects
  the float overloads (the integer overloads left the presenter uniforms at zero
  and collapsed the canvas to black). Added the opt-in AMD FSR 1.0 EASU/RCAS
  filter with a sharpness setting and MIT attribution.
- **Benchmark.** Added `--warmup` and `--state-in`; the Road Rash race profile is
  recorded in [`Performance.md`](Performance.md).
- **Tests.** 818 pass (was 798). New coverage: buffer-pool ownership/rotation,
  PCM tap WAV/CSV, and the soft peak limiter.

### 1.22 Eighteenth pass: Serilog logging and bug reporting (2026-10-28)

- **Serilog pipeline.** `FiveDo.Diagnostics.Logging.LoggingSetup` configures the
  rolling daily file, stderr console (desktop), in-memory viewer, and bug-report
  sinks. `AppLogging` owns the process-wide logger and flushes it on exit. The
  custom `Log`, `ILogSink`, `ConsoleLogSink`, `InMemoryLogSink`, `NullLogSink`,
  `LogLevel`, and interpolated-string handler were removed; `LogCategory` remains
  as the structured `Category` property.
- **Log viewer.** `InMemorySerilogSink` (capacity 4000) feeds the desktop **Logs**
  dock and **Log Viewer**; levels use Serilog's `LogEventLevel`.
- **Bug reports.** `BugReportSink` forwards `Warning`-or-higher events
  asynchronously to the configured endpoint with the required environment, error,
  and exception blocks. Submissions are deduplicated for five minutes, capped at
  ten per minute, and disabled when `FIVEDO_BUG_REPORTS=0` or in CI/test runs.
  `FIVEDO_LOG_DIR` redirects files and `FIVEDO_BUGREPORT_API_KEY` overrides the
  key. The CLI keeps JSON on stdout and concise errors on stderr; diagnostics go
  to the file/API.
- **Catch coverage.** Host entry points (`Program.Main`, Avalonia startup,
  `MainWindow` shutdown/dispose), the core thread, the OpenAL fill loop, video
  surface, GPU probe, settings/NVRAM/save-slot/library scanners, and prefetch/gamepad
  boundaries now log through Serilog. Expected CLI user errors are logged at
  `Warning`; unexpected exceptions are logged at `Fatal` before exit.
- **Tests.** 818 pass. Coverage includes the in-memory sink, category/level/search
  filter, the required bug-report blocks, and an HTTP handler test proving the
  warning payload reaches the configured endpoint with the `X-API-KEY` header.

### 1.23 Nineteenth pass: release hardening (2026-10-29)

- **Save-state disc identity.** `FiveDoMachine.LoadState`/`LoadStateFromFile` accept
  the current game ID and add "State disc hash differs from the loaded disc." to
  `StateWarnings` when both hashes are present and differ. The state still loads.
  The CLI `run`/`trace` commands, `SaveStateSlotManager.Load`, and the desktop
  Load State/Slots actions pass the current game ID; the desktop shows the warning
  in the status line and log. Tests cover mismatch, match, and empty-hash cases.
- **Console hotkeys.** `HotkeyRouter` resolves the persisted `InputProfile.Hotkeys`
  action names (SaveState, LoadState, Screenshot, Fullscreen, Pause, Reset) against
  a key name, case-insensitively, and ignores unknown action names. The shell
  dispatches the resolved action before forwarding the key to the emulated pad:
  F5 save, F8 load, F10 reset, F11 fullscreen, F12 screenshot, Escape pause. F11
  works from the library; Escape leaves fullscreen first.
- **`disc tracks` for ISO/BIN/CUE.** The command still prints the CHD track table;
  other containers now print one 2048-byte user-data track with
  `format=<iso|bin|cue>`, `dataSize`, `frames`, and `start=0`. In-process CLI tests
  cover ISO and raw BIN images.
- **OpenAL Soft LGPL packaging.** `docs/OpenALSoft.md` records the LGPL notice,
  copyright, exact upstream 1.23.1 source, a three-year written source offer, and
  relinking/replacement instructions. `docs/licenses/OpenAL-Soft-COPYING.txt`
  contains the complete LGPL-2.0 text. `packaging/package.ps1` copies both into
  every portable archive and names them in `PACKAGE.txt`; the win-x64 archive was
  inspected and the extracted CLI passed `--version` and `selftest --json`.
- **Tests.** 845 pass (was 821). New coverage: three disc-hash warning cases, one
  slot-manager warning case, eighteen hotkey-router cases, and two `disc tracks`
  CLI cases.

### 1.24 Twentieth pass: optional Vulkan backend and probe (2026-10-29)

- **Offscreen Vulkan renderer.** `FiveDo.Video.Vulkan` implements the two-stage
  presenter with Vulkan 1.0: R16_UINT indexed pixels, R8G8B8A8 CLUT, R32G32_UINT
  line parameters, a native-resolution palette pass, an output-resolution
  scale/filter pass, GPU timestamp queries, and a host readback buffer. GLSL is
  compiled to SPIR-V at build time by `XenoAtom.ShaderCompiler.Build`; no Vulkan SDK
  or shader compiler is required at runtime. `Silk.NET.Vulkan` provides the bindings.
- **Filters.** Accurate/nearest, sharp-bilinear, and CRT are implemented. xBR, HQx,
  and FSR are not in the Vulkan shader; they fall back to sharp-bilinear and set
  `filterFallback` in the probe report.
- **Probe.** `--vk-probe <path>` runs the renderer headlessly (no Avalonia window) and
  writes the same GPU probe JSON as `--gl-probe`, including `present`/`gpu` timing,
  allocations, capabilities, optional PNG capture, and CPU/GPU parity. Options:
  `--vk-probe-size`, `--vk-probe-frames`, `--vk-probe-warmup`, `--vk-probe-filter`,
  `--vk-probe-capture`, `--vk-probe-timeout`.
- **Desktop path.** `--gl-backend vulkan` hides the OpenGL surface and presents the
  Vulkan output through the existing `WriteableBitmap` path (`CaptureToFrame`).
  OpenGL remains the required full-performance presenter; the Vulkan path is optional
  and offscreen/readback-based.
- **Results.** 320x240 Accurate parity: 0 different pixels, FNV-1a `0xD6BD89C5`
  (`3602745797`), no vertical flip. RTX 4070 / Vulkan 1.4, 4K sharp-bilinear:
  present 1.51 ms mean / 1.80 ms p99, GPU 1.34 ms mean / 1.62 ms p99, 0 managed
  bytes/frame; 1080p sharp-bilinear present 0.49 ms mean / 0.50 ms p99. A 640x480
  source/output case and a 320x240 -> 640x480 source-size change also match the CPU
  oracle.
- **Tests.** `VulkanShaderTests`, `VulkanRendererTests` (opt-in
  `FIVEDO_VULKAN_TESTS=1`), `VulkanProbeIntegrationTests` (same opt-in), and
  `VkProbeOptionsTests`. The opt-in tests are skipped without a Vulkan device.

Do not replace the OpenGL presenter with the offscreen Vulkan path, and do not treat
the Vulkan probe as a 4K window-system benchmark. The backend is a validated optional
path; a native surface/swapchain and non-Windows device validation are future work.

### 1.25 Twenty-first pass: Vulkan xBR/FSR filters and GPU-palette xBR (2026-10-29)

- **Vulkan xBR-lv2.** `Xbr.frag` runs Hyllian's xBR-lv2 4x expansion over the
  GPU-expanded native frame, then the existing scale pass finishes with
  sharp-bilinear. The offscreen backend no longer reports `filterFallback` for xBR.
- **Vulkan FSR 1.0.** `FsrEasu.frag` upscales the native frame into a viewport-sized
  target, and `FsrRcas.frag` sharpens it into the output. Both are Vulkan 450 GPU
  passes with the shared push-constant block and one-image descriptor layout.
- **Vulkan HQx.** The CPU `HqxFilter` moved from `FiveDo.Video.OpenGL` to
  `FiveDo.Video` so both presenters share it. The Vulkan backend assembles the frame
  with `FrameAssembler`, expands it with `HqxFilter`, uploads the result through a
  reusable staging buffer, and finishes with sharp-bilinear. HQx 2x/3x/4x are
  CPU-bound and opt-in; 4K HQx 2x present measures 7.66 ms mean / 10.36 ms p99.
- **GPU-palette xBR in OpenGL.** The required OpenGL xBR path now samples
  `_paletteTexture` directly instead of running `FrameAssembler` and uploading a CPU
  RGBA copy. The 320x240 WGL xBR capture is within one mean channel level of the
  accurate frame, and the flipped comparison is far worse, confirming top-down
  orientation. The CPU `_xbrSourceTexture`/`_xbrCpuFrame` path was removed.
- **Measurements.** RTX 4070 / Vulkan 1.4, 60 frames after 15 warm-up: 4K xBR present
  1.55 ms mean / 1.92 ms p99, GPU 1.39/1.73 ms; 4K FSR present 1.87/2.15 ms, GPU
  1.69/1.97 ms; both 0 managed bytes/frame. 1080p xBR 0.61/0.85 ms and 1080p FSR
  0.56/0.60 ms present. OpenGL WGL 4K xBR: present 0.28/1.47 ms, GPU 0.19/0.32 ms;
  ANGLE 4K xBR: present 0.21/0.59 ms with no GPU timer.
- **Tests.** 861 pass. `VulkanRendererTests` adds a top-red/bottom-blue split-frame
  orientation check for xBR, FSR, and HQx at 320x240 and 640x480; `VulkanShaderTests`
  checks all six SPIR-V modules; the probe integration test runs xBR, FSR, and HQx at
  640x480 with `filterFallback: false`.

Do not redo the Vulkan xBR/FSR/HQx filter work or reintroduce the CPU xBR upload. The
remaining Vulkan work is a native surface/swapchain, vsync, and non-Windows device
validation.

### 1.26 Twenty-second pass: save-state version 2 and Brotli compression (2026-10-29)

- **Format version 2.** `StateFormat.Version` is now 2; the reader accepts versions
  1 and 2. The outer header layout is unchanged.
- **Per-section compression.** `StateWriter.WriteSection` compresses payloads of at
  least 4 KiB with Brotli when the result is smaller and sets the section `flags`
  field to 1. The stored payload is `[uncompressedLength i32][Brotli bytes]`; the
  section `length` counts both. The section CRC remains over the uncompressed
  payload. `StateReader` validates the flags, decompresses, and verifies the CRC
  before exposing the section.
- **Backward compatibility.** Version-1 states (raw sections) load unchanged.
  A version-2 state is rejected by older builds with
  `Unsupported save-state version 2.` The section inspector's fast header path reads
  the table without decompressing payloads, so slot listing stays cheap.
- **Inspector/CLI.** `StateInfo`/`StateSectionInfo` add `Compressed` and
  `StoredLength`; `state inspect` prints the stored size and marks compressed
  sections with their stored size. `--json` includes the new PascalCase fields.
- **Evidence.** A 24,902,592-byte version-1 state becomes 647,647 bytes at 300 frames
  and 2,001,229 bytes at 14,900 frames. `SaveStateTests`,
  `StateWriterReaderTests`, `StateInspectorTests`, and `SaveStateSlotManagerTests`
  cover compressed/uncompressed roundtrips, version-1 loads, corrupt compressed
  payloads, invalid lengths/flags, and slot metadata. **876 tests pass.**
- **Do not** change the CRC to cover compressed bytes, remove the version-1 read
  path, or make the inspector decompress every section to list slots.

### 1.27 Twenty-fourth pass: Opera file-system reader for all containers (2026-10-29)

- **First-party parser.** `FiveDo.Storage.OperaFileSystem` reads the volume header,
  directory headers/entries, copies, and file data directly through `IDiscSource`.
  ISO, BIN, CUE+BIN, and CHD now share one parser; the `VideoGameFileSystemParser`
  package was removed.
- **Directory chaining.** Multi-block directories are followed through the relative
  `nextBlock` field and the last-in-block/last-in-directory flags. The parser rejects
  block cycles, duplicate directory references, traversal names, and malformed
  record offsets.
- **File layout.** `ReadFile` maps logical offsets through `burst`/`gap` layouts and
  honors partial reads and EOF. Special entries such as `Disc label` are exposed as
  files, matching the previous CHD output.
- **Evidence.** On the Lemmings pair (`artifacts/s2/lem.iso` and
  `artifacts/chdmaker/lem.chd`), both formats produce 482 identical entries (461
  files, 21 directories) and 461 byte-identical extracted files. Road Rash, Alone,
  and Theme Park ISOs also parse (260/247/291 entries). `OperaFileSystemTests` covers
  nested directories, multi-block directories, offsets/EOF, burst/gap reads, special
  entries, invalid volume headers, and traversal names; `FsListAndExtract_ReadAnIsoImage`
  covers the CLI path. **906 tests pass.**
- **Do not** re-add the `VideoGameFileSystemParser` dependency or make `fs` CHD-only
  again. Extend the parser with new on-disc evidence only.

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

The 2026-10-25 captures above are historical; they used the FreeDO 8.24 sample
clock (~737 samples/frame). The 2026-10-27 captures use the exact clock and are
10.000 s per 600 frames (441,018-441,020 samples): Wolfenstein 18,100 peak / 2,168
RMS, Lemmings 8,192 / 1,661, Gex 12,964 / 2,089, Road Rash 32,768 / 11,012 (2,036
clipped frames), Theme Park silent. `audio analyze <wav> --json` prints the report;
see [`TestCorpus.md`](TestCorpus.md) 1.5a2. The 18,000-frame Wolfenstein gameplay
capture from the older clock was 66.86 s, peak 32767, RMS 2980, 18 clipped.

---

## 3. Ordered plan

### Step 1 - complete the menus

- Wolfenstein: done for the fresh-game loop; the existing 14,000-18,000 script and
  save/load proof are in section 2.
- Lemmings: DONE (2026-10-24 late pass, pure D-pad on 2026-10-26). The Fun 4
  passcode `HNLHCIOECW` is now typed entirely with the D-pad/A by
  `docs/scripts/lem-fun4-dpad.txt` from a passcode-screen state; the game shows
  the Fun 4 briefing at 22,600 and starts gameplay at 23,000 (`PC 0x00083FE8`,
  hash `0x63C02DED`; zero-frame load roundtrips, two 120-frame continuations both
  `PC 0x000230A4`, hash `0x4CA339CD`). The earlier poke path (`lem-pass-N.state` + 9-byte poke + `B`)
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

**Done in the sixteenth pass (2026-10-27).**

- The exact 44.1 kHz clock fixed the 737-vs-735 cadence; 600 frames are 10.000 s.
  Old/new Road Rash captures have identical spectral peaks and peak/RMS; the
  content did not change.
- Live OpenAL output works in the trimmed and NativeAOT CLI; underruns are padded
  and counted. Audio-driven frame pacing is implemented in `CoreRunner` and the
  CLI `--realtime` path.
- WAV captures are buffered and instrumented, and `audio analyze` reports
  peak/RMS/clipping/silence/DC. Captures are in `TestCorpus.md` 1.5a2.
- The 90 s Road Rash boot capture matches Opera's loopback: identical 0.914 peak,
  RMS within 4.7%, spectral cosine 0.9994 over 20 Hz-20 kHz, the same 441.4 Hz
  tone, and a 10 s-envelope correlation of 0.9685 after the capture-offset window.
  See `TestCorpus.md` 1.5a2.
- Remaining: a manual check that a game changing DSP modes stops/starts without a
  click. No current corpus title was observed stopping the DSP mid-run.
- Road Rash's clipping is the game's loud race mix (2,036 frames in a 10 s 92k-state
  capture), not the output path; live output applies a soft peak limiter.

### Step 4 - performance

**Done in the seventeenth pass (2026-10-28).**

- Road Rash's 3D race was measured from `artifacts/rr-race2-100k.state` with the
  BIOS and disc inserted: 600 frames after 120 warm-up frames, **5.46 ms/frame
  total (p50 2.91, p95 10.60, p99 13.14), 0 bytes/frame, 183 fps**. Core-only is
  5.39 ms mean / 13.01 ms p99; pack/mailbox/read add ~25/21/21 µs. This is inside
  the 16.67 ms budget at p99 with ~3.5 ms headroom. See
  [`Performance.md`](Performance.md) and the `--state-in`/`--warmup` benchmark
  options.

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
  `PC 0x00083FE8`, hash `0x63C02DED`, roundtrip `PC 0x000230A4` / `0x4CA339CD`
  twice with the exact audio clock).
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
- Do not revert the exact DSP sample clock to FreeDO's 8.24 `qrz_AccDSP` path. It is
  the difference between 44.1 kHz and ~44.4 kHz audio. Keep VDL/timer on the FreeDO
  accumulators; only the DSP clock is exact.
- Do not remove the bundled OpenAL Soft runtime, the `OpenAL32.dll` copy targets, or
  the audio-thread `alcMakeContextCurrent` call. They fix silent live audio on
  machines without a system OpenAL install.
- Do not remove audio-driven pacing or its 60 Hz fallback. The exact clock makes the
  nominal period 16.67 ms; the fallback protects reset/state-load frame deltas.
- Do not make the WAV sink write one file write per sample again; the buffered sink
  is required for long captures.
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
- Do not redo the fourteenth-pass swap-interval wiring or the `--gl-probe` tool:
  `SwapInterval.TryCreate`, `OffscreenRenderTarget`, the probe options/JSON
  context, and the measured 4K results are complete. Extend the probe with new
  evidence only; do not replace it with a reflection-based JSON path.
- Do not redo the fifteenth-pass `--gl-backend` selection, `--gl-probe-capture`
  parity path, or the measured WGL/ANGLE/GLES 3.0 results. Extend them with
  GLES 3.1/Pi/DPI evidence only.
- Do not redo the twentieth-pass Vulkan backend or `--vk-probe`: the offscreen
  two-stage pipeline, build-time SPIR-V shaders, 320x240 CPU parity, 4K/1080p probe
  results, and optional `--gl-backend vulkan` desktop path are implemented. Do not
  redo the twenty-first-pass Vulkan xBR/FSR GPU passes or the GPU-palette OpenGL xBR
  source; extend the backend with a native surface/swapchain, HQx, or non-Windows
  device evidence only.

---

## 6. Useful commands

```powershell
$o = "artifacts\s2"
$b = Join-Path $PWD "bios\panafz1.bin"
$g = $env:FIVEDO_GAMES   # main collection: J:\Panasonic 3DO

# Build/test/format
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release
dotnet format --verify-no-changes

# Trimmed CLI publish and smoke test (see docs/Trimming.md)
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -m:1 -o "$o\cli-trim"
& "$o\cli-trim\FiveDo.Cli.exe" selftest --json

# Trimmed frontend publish and startup smoke test (see docs/Trimming.md)
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -m:1 `
  -o "$o\frontend-trim"
$p = Start-Process -FilePath "$o\frontend-trim\FiveDo.Frontend.Avalonia.exe" `
  -ArgumentList '--smoke-test', '--smoke-output', "$o\frontend-smoke.txt" -PassThru
$p.WaitForExit(60000)
Get-Content "$o\frontend-smoke.txt"

# GPU presentation probe (WGL, ANGLE/GLES, or software)
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend angle `
  --gl-probe artifacts/gl-probe/angle-4k-sharp.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen

# CPU/GPU parity: capture a 1:1 Accurate frame and compare it with FrameAssembler
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend wgl --gl-probe artifacts/gl-probe/parity-wgl.json `
  --gl-probe-size 320x240 --gl-probe-filter accurate --gl-probe-offscreen `
  --gl-probe-capture artifacts/gl-probe/parity-wgl.png

# Road Rash: render the race and capture every 1,000 frames
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Road Rash (USA).chd" --bios $b --frames 30000 `
  --capture-dir "$o\rr-race" --capture-every 1000 `
  --frame-out "$o\rr-race.png" --state-out "$o\rr30k.state" --json

# Wolfenstein: start a fresh game from the 12k demo state
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-fix12k.state" `
  --frames 2000 --input-script "$o\w3d-play.txt" `
  --frame-out "$o\w3d-play.png" --state-out "$o\w3d-play.state" --json

# Wolfenstein: walk/turn/fire from the fresh-game state
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-play.state" `
  --frames 4000 --input-script "$o\w3d-walk.txt" `
  --frame-out "$o\w3d-gameplay.png" --state-out "$o\w3d-gameplay.state" --json

# Lemmings: full 640x480 high-resolution title (opt-in)
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Lemmings (USA).chd" --bios $b --frames 18100 --high-res `
  --input-script "$o\lem-menu.txt" --frame-out "$o\lem-hires-title.png" --json

# Lemmings: type the Fun 4 passcode with the D-pad and start the level
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Lemmings (USA).chd" --bios $b --state-in "$o\lem_pass_fresh.state" `
  --frames 1300 --input-script docs/scripts/lem-fun4-dpad.txt --script-base state `
  --frame-out "$o\lem-dpad-gameplay.png" --state-out "$o\lem-dpad-gameplay.state" --json

# Save/load roundtrip (replace the state with a live gameplay state)
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Wolfenstein 3D (USA).chd" --bios $b --state-in "$o\w3d-live.state" `
  --frames 120 --json

# Audio capture from a live state
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$g\Road Rash (USA).chd" --bios $b --state-in "$o\rr30k.state" `
  --frames 600 --audio wav --wav-out "$o\rr-game.wav" --json

# RAM dump at a trace stop
dotnet run -c Release --project src/FiveDo.Cli -- trace `
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
3. ~~Does the 737 vs 735 samples/frame cadence cause audible pitch drift?~~
   **Answered 2026-10-27:** yes, the FreeDO 8.24 scheduler over-produced samples
   by ~0.7%. The exact DSP clock now matches Opera's 44.1 kHz rate; `_dspPcnt` was
   not the cause and was left at 567.
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
