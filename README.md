# 5DO

A clean-room-style, **pure C# / .NET 10** 3DO emulator, originally ported from FreeDO/4DO, with **Opera/libopera as the primary behavioral reference** for new compatibility work, a cross-platform Avalonia frontend, a headless CLI, and a deterministic test harness.

> **Naming:** The emulator is called **5DO**. The solution is `5DO.sln`; source projects and namespaces use the `FiveDo` prefix because C# identifiers cannot begin with a digit.

## Status

**Current position (2026-10-27, 1.0.0-rc.1 release preparation, sixteenth audio
pass):** M0-M8 are functionally complete. The
M9 Avalonia shell, the M9R two-stage OpenGL presenter, the M9P dedicated core
thread/frame pacer, and the M10 CI/publish pipeline are implemented as a working
prototype. **796 tests pass.** The sixteenth pass corrected the DSP sample clock to
an exact 44,100 samples per emulated second (the FreeDO 8.24 scheduler ran ~0.7%
fast), fixed live OpenAL output by bundling OpenAL Soft 1.23.1 and making the
context current on the audio thread, added audio-driven frame pacing, and added
WAV/audio statistics plus `audio analyze`. The fifteenth pass added an explicit
`--gl-backend auto|wgl|angle|software` selector and validated the Windows
ANGLE/GLES 3.0 path: all filters compile, 4K sharp-bilinear presents in 0.14 ms
mean / 0.47 ms p99 with zero renderer allocations, and a 320x240 Accurate capture
matches the CPU oracle pixel-for-pixel on both WGL and ANGLE. The fourteenth pass wired the platform swap interval
(WGL/GLX/EGL) and added a true-4K `--gl-probe`; on the RTX 4070 development machine,
4K sharp-bilinear measures GPU p99 0.089 ms and present p99 0.208 ms with zero
renderer/mailbox allocations per frame after warm-up. The shell now has fullscreen,
GL screenshots, an About
dialog, a Hi-Res toggle, automatic CPU fallback when OpenGL init or the context fails,
a game library browser with cover art, a settings dialog (video/audio/emulation/paths), six-player
keyboard and gamepad binding, ten numbered save slots with PNG thumbnails, PNG
screenshots named by game/frame/time, an optional FPS/status overlay, a first-run
guide, error dialogs, and an in-app diagnostic log viewer. The app also accepts a
positional disc path or `--disc <path>` (a Linux `.desktop` association file is in
`packaging/`). Settings include per-game fix-mode and filter overrides. Windows XInput and Linux
evdev gamepads are supported. The CLI applies the 4DO per-game fixes automatically and
accepts `--fix-mode`/`--no-game-fixes` overrides. The benchmark reports p50/p95/p99 and
zero steady-state allocations, and `--gate` enforces the CPU-side budgets in CI; an
opt-in bounded disc prefetch cache is available via `--prefetch`.

**Audio (2026-10-27):** the corpus now produces exactly 735 samples per 60 Hz
frame (441,015-441,020 samples per 600 frames = 10.000 s), matching Opera's 44.1 kHz
reference within the 59.94/60 Hz field-rate difference. Live playback uses the
bundled OpenAL Soft 1.23.1 runtime (no system OpenAL install required), the core
thread paces frames against the audio clock with a 60 Hz fallback, and the CLI
supports `--audio openal [--volume 0..1] [--realtime]` plus
`audio analyze <wav> --json` for peak/RMS/clipping/DC reports. The opt-in
NativeAOT CLI also opens the live backend. Audio content is unchanged: a
6,000-frame Road Rash capture has the same spectral peaks before/after; only the
timebase was corrected.

The renderer now has an opt-in **xBR-lv2** edge-directed 4x filter, **HQx 2x/3x/4x**
filters, and **CRT bloom** alongside accurate, nearest, sharp-bilinear, and CRT modes.
The HQx 3x/4x patterns match the C++ `hq3x_32_rb`/`hq4x_32_rb` reference hashes. The
twelfth pass made settings, input profiles, save-slot metadata, and every CLI `--json`
response source-generated and trim-safe, enabled `PublishTrimmed` for the CLI,
added a trimmed-CLI CI smoke test, and added a same-machine benchmark baseline
comparison; see [`docs/Trimming.md`](docs/Trimming.md). The thirteenth pass enabled
`PublishTrimmed` for the Avalonia frontend, removed the reflection-based
`ViewLocator`, and added a trimmed-frontend CI smoke test (`--smoke-test`); both
executables now publish trimmed for all six RIDs. The
eleventh pass made the benchmark a CI gate (0 bytes/frame, core p99 <= 8 ms,
total mean <= 8 ms, total p99 <= 16.667 ms). The tenth pass committed a pure D-pad
Lemmings Fun 4 passcode script (`docs/scripts/lem-fun4-dpad.txt`): the game accepts
`HNLHCIOECW`, shows the Fun 4 briefing, and starts gameplay at 23,000 frames with a
deterministic save/load roundtrip. The eighth pass fixed a real-content **GPU palette
bug** from the previous session: the shader now applies the red, green, and blue CLUT
indices independently, matching the CPU `FrameAssembler`; Lemmings gameplay was
captured through both sharp-bilinear and xBR with correct colors.

The ninth pass recorded per-title save-state, PNG, and WAV evidence (zero-frame and
120-frame hashes, deterministic roundtrips, 600-frame audio captures) in
[`docs/TestCorpus.md`](docs/TestCorpus.md) 1.5a. It also fixed the old Road Rash
~42,000/~92,000 READ_ID-only wait with a CD media-access pulse; the game now advances
to `BUSTED`, race select, and a new race. Space Ace's interactive path is validated
(`X` menu, `A` play) and roundtrips at 15,000 frames. The autopilot now cycles
race -> wreck -> menu -> new race to 100k, but a full 5.5-mile race is still not
finished.

Real-content status with `panafz1.bin`: the five golden-corpus titles boot, load their
own OS, render real frames, and produce non-silent 44.1 kHz stereo DSP audio. Seven
additional titles (Gex, Need for Speed, Super Street Fighter II Turbo, Total Eclipse,
Killing Time, Theme Park, and Return Fire) were also booted, rendered, and
save-state-roundtripped on 2026-10-26. Gex, Total Eclipse, and Super Street Fighter II
Turbo show gameplay/attract scenes; the others render menus. Six produce non-silent
44.1 kHz stereo audio; Theme Park's captured menu is silent. Hashes and commands are
in [`docs/TestCorpus.md`](docs/TestCorpus.md) 1.7.

- **Road Rash** renders its 3D race correctly (matching the Opera reference) and runs
  past 60,000 frames. A CD media-access pulse fixes the old ~42k/~92k READ_ID-only
  wait: those screens now advance to `BUSTED`, the race-select menu, and a new race.
  The autopilot passes `GAME LOADING`/`Get ready` and cycles race -> wreck -> menu ->
  new race to 100k, but still does not finish a full 5.5-mile race.
- **Wolfenstein 3D** starts a fresh game at FLOOR 1-1 from its preview menu, walks,
  turns, fires, and roundtrips a live gameplay save state. A pulses every 300 frames
  also advance the long fresh-game load through character select to FLOOR 1-1.
- **Alone in the Dark** reaches its in-game staircase/room with an upright character;
  the D-pad/A open the Begin/Resume menu and start the intro scene. A pulses every
  300 frames leave the intro's static READ_ID-only wait, and a scripted
  Up/Left/A/Down/Right sequence moves the character around the room with a
  save/load roundtrip.
- **Lemmings** reaches its title and passcode screens; the Fun 4 passcode
  (`HNLHCIOECW`) is now typed entirely with the D-pad/A by
  `docs/scripts/lem-fun4-dpad.txt`, starting gameplay at 23,000 frames with a
  deterministic roundtrip, and the 640x480 high-resolution title matches the
  reference.
- **Space Ace** plays its animated demo and its interactive scene: `X` returns to the
  title/menu and `A` starts the scene; the 15,000-frame state roundtrips and audio was
  captured.

The 2026-10-24/25 passes fixed DSP execution/immediates/rounding, the CCB field order,
the CLI NVRAM initialization, a **transposed arbitrary-CEL X/Y write** that corrupted
Road Rash's VRAM allocator and drew Alone horizontally, and VDLP bitmap widths for
384/512/640/1024-wide modes. The opt-in 640x480 high-resolution CEL/VDLP path was
added; save-state loads restore the VDL picture immediately.

**Release scope (2026-10-26):** v1.0 is a **documented-compatibility release**, not a
full-compatibility one. Windows x64 is the validated target; the 12 boot-validated
titles are listed with honest per-title status in
[`docs/Compatibility.md`](docs/Compatibility.md), and known limitations ship in
[`docs/KnownIssues.md`](docs/KnownIssues.md). Remaining release work is the
legal/attribution review, a stability soak, release notes/packaging, and optional
Linux/macOS launch validation. Full-race completion, scripts for the other seven
titles, Raspberry Pi/arm64 validation, installers, and the optional JIT are post-1.0.
Work through [`docs/ReleaseChecklist.md`](docs/ReleaseChecklist.md). Per-title
save-state, PNG, and WAV evidence is recorded
in [`docs/TestCorpus.md`](docs/TestCorpus.md) 1.5a. See
[`docs/Status.md`](docs/Status.md) for the completion estimate and
[`docs/NextSession.md`](docs/NextSession.md) for the next-session plan. The
implementation plan and architecture are in:

- [`docs/Status.md`](docs/Status.md) - current position, completion estimate, and next steps.
- [`docs/ReleaseChecklist.md`](docs/ReleaseChecklist.md) - v1.0 release scope, gates, and sign-off.
- [`docs/ReleaseLegalReview.md`](docs/ReleaseLegalReview.md) - attribution audit and pending distribution decisions.
- [`docs/NextSession.md`](docs/NextSession.md) - first task, commands, and acceptance criteria for the next session.
- [`docs/ReferenceStrategy.md`](docs/ReferenceStrategy.md) - Opera/FreeDO reference hierarchy and differential procedure.
- [`docs/ImplementationPlan.md`](docs/ImplementationPlan.md) - milestones, tasks, acceptance criteria, risks.
- [`docs/Architecture.md`](docs/Architecture.md) - project layout, dependency rules, runtime composition.
- [`docs/PortingNotes.md`](docs/PortingNotes.md) - C++ to C# porting rules and reference quirks.
- [`docs/TestStrategy.md`](docs/TestStrategy.md) - unit, integration, golden, differential, and performance testing.
- [`docs/Compatibility.md`](docs/Compatibility.md) - BIOS, disc, input, and game-fix compatibility notes.
- [`docs/TestCorpus.md`](docs/TestCorpus.md) - local BIOS/disc corpus and real-content validation commands (not committed).
- [`tools/release-soak.ps1`](tools/release-soak.ps1) - per-machine release soak: discovers present discs, reports missing titles, and records summaries.
- [`docs/Performance.md`](docs/Performance.md) - 4K60 budgets, presentation pipeline, and measurement plan.
- [`docs/Trimming.md`](docs/Trimming.md) - trim-safe JSON, trimmed CLI/frontend publishes, smoke tests, and the NativeAOT audit.
- [`docs/UserGuide.md`](docs/UserGuide.md) - quick start, controls, display filters, save states, logs, and CLI.
- [`docs/KnownIssues.md`](docs/KnownIssues.md) - current game/renderer/platform limitations.
- [`CHANGELOG.md`](CHANGELOG.md) - recent user-visible changes.

## Goals

- Emulate the Panasonic 3DO Interactive Multiplayer accurately enough to boot the BIOS and run real software.
- Keep the emulation core **100% managed C#**; no C++/CLI, no native emulator DLL, no P/Invoke into FreeDOCore.
- Run on **Windows, Linux, and macOS**, on **x64 and arm64**.
- Provide an **Avalonia** desktop frontend and a **headless CLI** for automation, CI, and debugging.
- Be **deterministic**: same BIOS + disc + input sequence + options produces the same frames and state.
- Keep the codebase modular: one project per console subsystem, one class per major responsibility.

## Disc formats

- ISO (2048-byte sectors)
- BIN (2352-byte sectors)
- CUE+BIN (single data track)
- **CHD** via the pure C# **CHDSharp** library, including parent/child images and read-ahead decompression

CHD is a first-class v1 format. The storage layer presents every container through the same `IDiscSource` 2048-byte-sector interface, so the CD-ROM emulation is identical regardless of file format.

## Differentiator: OpenGL/GLES presentation

The desktop frontend uses **Silk.NET.OpenGL/OpenGLES** through Avalonia's OpenGL control as the **required** presentation path:

- Two-stage pipeline: palette expansion at native resolution (320x240), then GPU upscaling/filtering at output resolution.
- Pixel-accurate "Accurate" mode that matches the CPU frame assembler.
- High-quality GPU upscaling to 1080p/1440p/4K: nearest, sharp-bilinear, CRT (scanlines, aperture mask, curvature, vignette, bloom), xBR-lv2 4x, and HQx 2x/3x/4x.
- Integer scaling, aspect-ratio control, fullscreen, vsync, and screenshots.
- Desktop GL 3.3 (WGL/GLX/EGL/macOS) and GLES 3.0/3.1 (ANGLE, Raspberry Pi) variants.
- CPU `WriteableBitmap` fallback only when neither GL 3.3 nor GLES 3.0/3.1 is available.

The emulation core still renders CELs in software for accuracy. The GPU accelerates presentation, palette expansion, scaling, and filters; it does not change emulated state.

## Performance target

- **4K (3840x2160) 60 fps output** on a mid-range x64 desktop (Tier A) with nearest or sharp-bilinear filtering.
- **1080p 60 fps** on Raspberry Pi 4/5 class arm64 using the GLES 3.1 path.
- Native 320x240 emulation; 4K is **output upscaling**, not 4K internal CEL rendering.
- Core frame budget ≤ 8 ms p99 on Tier A, zero managed allocations per frame, and core cost independent of output resolution.
- CRT/xBR filters are opt-in and may require faster hardware.

The CPU side currently runs at ~1.25-1.6 ms/frame (core + pack + mailbox + read copy;
600-frame benchmark, p99 1.6-4.6 ms depending on high-res), leaving ~15 ms of headroom
at 60 Hz on the development machine. The synthetic steady state is allocation-free.
The new `--gl-probe` measures the real Avalonia/OpenGL presentation path: on Windows
11 with an RTX 4070, 4K sharp-bilinear is GPU p99 0.089 ms and present p99 0.208 ms,
with zero renderer/mailbox allocations per frame after warm-up. The same probe forces
ANGLE/GLES 3.0 (`--gl-backend angle`): 4K sharp-bilinear presents in 0.14 ms mean /
0.47 ms p99 with zero renderer allocations, and a 320x240 Accurate capture matches
the CPU oracle on both WGL and ANGLE. Tier B numbers, Raspberry Pi/GLES 3.1, DPI,
and the CI relative gate are not measured yet; see
[`docs/Performance.md`](docs/Performance.md) for the pipeline, measurement method,
and the remaining M9P work items.

## Optional guest CPU JIT

.NET already JIT-compiles the C# core. If profiling ever shows the ARM60 interpreter is a bottleneck, a **pure managed IL JIT** for guest ARM basic blocks can be added behind `ICpuBackend`. It is optional, disabled by default, and must be bit-identical to the interpreter. At ~12.5 MHz, the 3DO CPU is not expected to require it for 60 fps.

## Non-goals for the first release

- Native or JIT CPU backends. The ARM60 interpreter is the reference implementation.
- Netplay, rewinding, or a full debugger UI.
- CD-DA/Red Book audio, multi-track discs, or CD swapping.
- PAL video modes. High-resolution CEL rendering is available as an opt-in (`--high-res`), not as the default v1 mode.
- 4K *internal* CEL rendering; v1 targets 4K output upscaling only.
- Shipping BIOS ROMs, game discs, or any copyrighted 3DO material.

## Requirements

- .NET SDK **10.0.100** or newer (`global.json` rolls forward to the latest 10.0 feature band).
- A legally obtained 3DO BIOS ROM (1 MB, optionally a second 1 MB ROM).
- Legally obtained 3DO disc images in ISO (2048-byte sectors), BIN (2352-byte sectors), CUE+BIN, or CHD format. CHD support uses the pure C# CHDSharp library.
- For the desktop app: Windows 10+, a current Linux desktop, or macOS 12+.
- Desktop audio uses the OpenAL Soft 1.23.1 runtime bundled by the build; no system
  OpenAL installation is required.

## Build

```powershell
git clone https://github.com/purelogiccode/3DOEmulator.git
cd 3DOEmulator
dotnet build 5DO.sln
dotnet test 5DO.sln
```

## Run

Headless CLI:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.cue --bios panafz1.bin --frames 600 --frame-out frame.png
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin --frames 600 --audio wav --wav-out capture.wav
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin --frames 600 --audio openal --volume 0.5 --realtime
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin --frames 600 --prefetch --high-res
dotnet run --project src/FiveDo.Cli -- info game.chd --json
dotnet run --project src/FiveDo.Cli -- audio analyze capture.wav --json
dotnet run --project src/FiveDo.Cli -- bios info panafz1.bin
dotnet run --project src/FiveDo.Cli -- state inspect quick.tdos
dotnet run --project src/FiveDo.Cli -- bench --frames 600
dotnet run --project src/FiveDo.Cli -- selftest
dotnet run --project src/FiveDo.Cli -- disasm artifacts/ram.bin --offset 0x12380 --count 64
```

Diagnostics: `run` accepts `--watch-lowmem`, `--dump-ram`, and `--dump-dsp`;
`trace` adds `--dsp-trace` and the existing CPU ring buffer. See
[`docs/TestCorpus.md`](docs/TestCorpus.md) for the full command set.

GPU presentation probe (developer/validation tool, requires a desktop GL context):

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-probe artifacts/gl-probe/4k-sharp.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen

# Force ANGLE/GLES 3.0, and capture a 1:1 Accurate frame for CPU/GPU parity
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend angle --gl-probe artifacts/gl-probe/4k-angle.json `
  --gl-probe-size 3840x2160 --gl-probe-filter sharp-bilinear --gl-probe-offscreen

dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-backend angle --gl-probe artifacts/gl-probe/parity-angle.json `
  --gl-probe-size 320x240 --gl-probe-filter accurate --gl-probe-offscreen `
  --gl-probe-capture artifacts/gl-probe/parity-angle.png
```

The probe writes per-frame present/GPU percentiles, capabilities, allocations, and
optional CPU/GPU parity as JSON. `--gl-backend auto|wgl|angle|software` also works
for a normal desktop launch. See [`docs/Performance.md`](docs/Performance.md) for
the measured WGL and ANGLE results.

Both the CLI and the Avalonia frontend publish trimmed by default
(`PublishTrimmed=true`). Settings/input/save-slot JSON and all CLI `--json` responses
use source-generated metadata, the frontend has no reflection-based view locator, and
CI runs a trimmed-frontend startup smoke test. The CLI also has an opt-in NativeAOT
publish (`-p:FiveDoNativeAot=true`): the win-x64 AOT single file is 9.97 MB and CI
runs the CLI smoke commands against it. The frontend has an opt-in NativeAOT
single-file publish (28.8 MB executable plus native Skia/HarfBuzz/ANGLE assets) that
passes the smoke test and the WGL/ANGLE parity probes; Silk.NET loader warnings remain
and frontend AOT audio is not yet validated. The CLI AOT path opens the bundled
OpenAL backend. See
[`docs/Trimming.md`](docs/Trimming.md) for the audit and the remaining AOT work.

CI also packages each published CLI/frontend folder as a portable archive:
`5do-<component>-<rid>.zip` on Windows and `.tar.gz` on Linux/macOS, with
README/notices/licensing and a generated `PACKAGE.txt`. See
[`packaging/README.md`](packaging/README.md).

Desktop app:

```powershell
dotnet run --project src/FiveDo.Frontend.Avalonia
dotnet run --project src/FiveDo.Frontend.Avalonia -- "game.chd"
```

Use **Open BIOS** and **Open Disc**, then **Play**. The default keyboard bindings are the arrow keys, Z (A), X (B), C (C), A (L), S (R), Enter (P), and Backspace (X). **Save State**/**Load State** use the selected slot; the **<**/**>** buttons change slots. **Screenshot** writes a PNG from the OpenGL output (or the CPU frame when GL is unavailable), named by game, time, and frame. **Library** scans configured folders through the 4DO database, **Settings** edits video/audio/emulation/paths, and **Input** captures keyboard bindings for all six players. **Fullscreen** toggles F11/Escape; **Hi-Res** restarts the machine in the opt-in 640x480 mode. Settings persist under the user local application data folder, including per-game fix-mode overrides.

## Legal and attribution

FreeDO/4DO are **not** standard OSI-licensed projects. The FreeDO source headers contain a modified LGPL with additional restrictions, including a prohibition on commercial use without the owners' approval. This repository does **not** include BIOS ROMs or game images. Read [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) and [`docs/Licensing.md`](docs/Licensing.md) before distributing builds.

"3DO" and "Panasonic" are trademarks of their respective owners. This project is not affiliated with or endorsed by them.
