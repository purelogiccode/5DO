# Local Test Corpus

**Status:** local developer machine only; not part of the repository.
**Last inventoried:** 2026-10-24

This document records the legally obtained BIOS and disc images available on the
development machine for manual compatibility, boot, audio, input, save-state, and
performance testing. None of these files may be committed, packaged, or uploaded.

- BIOS directory: `bios/` in this repository (ignored by `.gitignore`)
- Disc directory: **machine-specific**, set `THREEDO_GAMES` to the root:
  - Office machine: `D:\Samples\3DO`
  - Home machine: `E:\Panasonic 3DO`

Both locations are outside version control. `bios/` is already ignored by
`.gitignore`, and `*.chd`/`*.iso`/`*.bin`/`*.cue` are ignored as a second guard.
The corpus root is never hard-coded in scripts; use `$env:THREEDO_GAMES` so the same
commands work on both machines. The older `D:\Samples\3DO` path used by earlier
handoffs and the `E:\Panasonic 3DO` path used by others are both aliases for the
same corpus root depending on the machine.

---

## 1. Disc images

| Title | File | Format | Size (bytes) | Game ID | Known fixes |
|---|---|---|---:|---|---|
| Alone in the Dark (USA) | `Alone in the Dark (USA).chd` | CHD | 184,385,537 | `9f7d72bc` | `FIX_BIT_TIMING_6` |
| Lemmings (USA) | `Lemmings (USA).chd` | CHD | 299,910,255 | `0be38b22` | none in the 4DO database |
| Road Rash (USA) | `Road Rash (USA).chd` | CHD | 506,330,721 | `d7672550` | none in the 4DO database |
| Space Ace (USA) | `Space Ace (USA).chd` | CHD | 229,432,527 | `112b53d0` | none in the 4DO database |
| Wolfenstein 3D (USA) | `Wolfenstein 3D (USA).chd` | CHD | 58,013,447 | `3dfcdbbf` | none in the 4DO database |

All five are CHD images. They are the primary real-content corpus for:

- BIOS + disc boot validation.
- CHD sector streaming and read-ahead.
- Title/menu rendering and input.
- DSP/audio validation.
- In-game save-state roundtrips.
- End-to-end performance and frame-pacing measurements.

ISO/BIN/CUE parity testing still needs a legally obtained image in one of those
formats, or a locally generated fixture. Do not convert and commit a retail disc.

### 1.1 Current boot baseline

The following results were recorded on 2026-10-24 after the DSP, CCB, and NVRAM
fixes, with `panafz1.bin`, a Release build, and `run --frames 3000`. The CLI now
loads a fresh 32 KB NVRAM volume by default; pass `--nvram <path>` to persist it.
They are a regression baseline, not a full compatibility pass:

| Title | Final PC | Frame hash | Frame at 3,000 | Observation |
|---|---|---|---|---|
| Alone in the Dark | `0x000C4CB4` | `0xF4BDF79B` | boot/logo | AudioFolio init succeeds; with input and NVRAM it reaches the in-game room/inventory scene by 10,000 frames |
| Lemmings | `0x0012B3C8` | `0x932C7722` | **rendered** | Reaches CyberIntro; at 10,000 frames with input it reaches the Load/Options title menu |
| Road Rash | `0x000B9F84` | `0xEB31636F` | **rendered** | Loads the game OS (`grafmath 21.10.504`); SAudio init succeeds; the 30,000-frame run reaches the 3D race scene |
| Space Ace | `0x00076418` | `0xA2B49CF0` | **rendered** | ReadySoft player finds the CD-ROM, plays the demo, and passes the former frame-681 crash |
| Wolfenstein 3D | `0x000699E8` | `0xC21BB6A1` | boot/logo | Loads Sherry/Operator/FS v24.225; with input it reaches gameplay (FLOOR 1-1) |

At 10,000 frames with input and a fresh NVRAM (pre-transposition-fix):
 
| Title | Final PC | Frame hash | Observation |
|---|---|---|---|
| Alone in the Dark | `0x0004B6B4` | `0x67FD3C6F` | In-game room/inventory scene |
| Wolfenstein 3D | `0x0001F014` | `0xA5A33C4D` | Gameplay (FLOOR 1-1) |
 
These two hashes are retained for historical comparison; the current first-pass
numbers are in section 1.2.

The 30,000-frame Road Rash run after the CCB fix originally ended at
`PC 0x00016194` with frame hash `0xE2D08131` and rendered the race scene when run
without NVRAM. That result was produced before the arbitrary-CEL transposition fix;
the transposed CELs were corrupting the OS VRAM free list, so the "frozen race" was
actually a failed allocator. After the fix the same run reaches 60,000 frames with
`PC 0x006940C0`-class game code, DSP running, no `AllocMem` error, and a correct race
render. See section 1.2 for the new baselines.

Use the frame hash as the stable regression key and record the PC alongside the
commit. The pre-NVRAM 3,000-frame hashes were (`0x85EF87FF`, `0x932C7722`,
`0xEB31636F`, `0x2A9967FA`, `0x1A940707`); the pre-CCB hashes were
(`0x85EF87FF`, `0x932C7722`, `0xEB31636F`, `0x6AA8B441`, `0x1A940707`). The NVRAM
fix changed Wolfenstein, Alone, and Space Ace because their save-space checks now
see a valid volume; the CCB fix changed Space Ace's rendering. The 3,000-frame
hashes are **unchanged by the transposition and VDLP fixes** because those frames
are all boot/FMV screens.

`PC 0x205C4`/`0x205C8` is the Portfolio **idle loop**, not a halt: VDL and timer
interrupts continue to run and the boot log advances. Earlier baselines that
described this address as a "halt path" were misreading the idle loop.

### 1.2 Second pass: renderer-correct gameplay (2026-10-24)

All runs use the Release build, `panafz1.bin`, and a fresh NVRAM. The 3,000-frame
regression hashes are identical to section 1.1; the longer runs changed because the
transposition fix corrected the 3D CEL output.

| Title | Frames | Final PC | Frame hash | Observation |
|---|---:|---|---|---|
| Road Rash | 30,000 | `0x000681F0` | `0xEB0AD59C` | Race renders correctly; DSP running; no allocator error |
| Road Rash | 60,000 | `0x00095E84` | `0xBF01DF59` | Attract race/FMV loop still alive; DSP running |
| Road Rash (state, 1,200 frames) | 31,200 | `0x00096300` | `0x274021FE` | Control continuation of the 30k state |
| Wolfenstein 3D | 12,000 | `0x00074B6C` | `0xC97CA7A5` | FLOOR 1-1, SCORE 2800, ITEM 01 |
| Alone in the Dark | 16,000 | `0x0008C608` | `0x3D257B5C` | Upright character at the staircase |
| Lemmings | 18,000 | `0x000205F8` | `0x5625A4CD` | Title menu (Play/New Game) |
| Space Ace | 12,000 | `0x0007D114` | `0xEF99FED7` | Animated demo, no runaway |

The exact hashes should be re-read from the `--json` output rather than copied from
memory; the stable observations are:

- **Road Rash:** the 3DO controls are B accelerate, A brake, D-pad left/right
  steer, C attack, and P pause. From the live 26,000-frame state the attract race
  auto-drives and eventually reaches the WRECK FMV; holding B starts a player race
  from the GAME LOADING screen. A hard left around frame 31,000 clears the first
  oncoming taxi. `--autopilot road` holds B and follows the gray road centre, but
  it does not yet avoid traffic or recover from a wreck. The race is visually
  correct and matches the Opera attract-mode road/rider composition.
- **Wolfenstein:** from the 12,000-frame demo state, `A` opens the Select Previews
  menu; D-pad/A navigate to Select a Level and start a fresh game at FLOOR 1-1 with
  `SCORE 0000000`. A live 60-frame state saved after that (`0xC9989182`) roundtrips:
  two independent 120-frame continuations both end at `0x370158AC`, and a zero-frame
  load reproduces `0xC9989182` exactly.
- **Alone:** the 16,000-frame state has the character upright at the staircase. The
  control run walks through the room; the scripted D-pad/A run reaches the
  Begin/Resume main menu.
- **Lemmings:** the 18,000-frame state is the title screen. `Down` + `A` opens the
  passcode screen ("Enter the access code"). The keyboard is a 9-column grid with
  five visible columns; `A` types the selected letter, `B` confirms early, and ten
  letters auto-confirm. A 15-frame D-pad hold moves the selection one key. A wrong
  code shows "Incorrect" and returns to the title. The title's `A`/`P`/`X`/Left/Right
  do not leave the attract loop, even over 20,000 further frames. The remaining task
  is a deterministic path to a valid code such as `IJJLDNCCCN` (Fun 2).
- **Space Ace:** the demo renders animated FMV frames past 12,000 frames.
- **Wolfenstein fresh-game load (open issue):** starting a new game from the preview
  menu reaches a static level screen at 14,000 frames, but it is still there at
  54,000 frames. The CD trace shows one 24-sector read every ~33 frames (~87 KB/s)
  and revisits the 05:11-05:3b region. The attract-mode gameplay at 12,000 frames is
  unaffected. The Opera-style read-completion status and the 2X drive status bit were
  tried and reverted: they did not fix the load and changed the golden CD status.
- **Audio (600 frames from the live states):** Road Rash 44.1 kHz stereo, 10.03 s,
  peak 32768, RMS 9051/9146, 191 clipped samples; Wolfenstein 44.1 kHz stereo,
  10.02 s, peak 30743, RMS 5620/5457, no clipping. Both are ~737 samples/frame.

**Where this leaves us:** all five titles boot, render, and produce non-silent DSP
audio; four of them have been driven through menus and into gameplay. The remaining
gameplay milestone is a scripted race/level completion, plus the Lemmings passcode
confirm and an Alone movement/inventory script. See [`Status.md`](Status.md) and
[`NextSession.md`](NextSession.md).

### 1.3 Third pass: gameplay loop and high-resolution rendering (2026-10-24)

All runs use the Release build, `panafz1.bin`, and a fresh NVRAM.

**Wolfenstein 3D gameplay loop.** From `w3d-fix12k.state` (the 12,000-frame demo),
`w3d-play.txt` opens the preview menu and starts FLOOR 1-1 at 14,000 frames. From
that state, `w3d-walk.txt` walks, turns, and fires through 18,000 frames:

```
14,000 frames: PC 0x0007824C, hash 0x07586A88 (FLOOR 1-1, SCORE 0000000)
18,000 frames: PC 0x00074A04, hash 0xF568EBCA (corridor, guard, pistol)
19,200 frames: PC 0x0001186C, hash 0x74FBBF0C (after walk/turn/fire script)
```

The HUD, first-person view, guard sprite, and weapon render, and the player moves.
The exact values should be re-read from `--json`; the stable observations are the
FLOOR 1-1/score HUD and the visible movement.

Save/load at 18,000 frames:

- Live state saved at 18,000: frame hash `0xF568EBCA`.
- Zero-frame load: `0xF568EBCA` (pixel-identical).
- Two independent 120-frame continuations: both `PC 0x000782B8`, hash `0xF568EBCA`
  (the level is waiting for input; the static hash is expected).

Audio from the 18,000-frame gameplay state, 4,000 frames captured with
`--audio wav`: 66.86 s, 44.1 kHz stereo, peak 32767, RMS 2980, 18 clipped samples.

**Lemmings high-resolution title.** `--high-res` renders the frontend's 640x480
high-resolution mode:

```
Low-res  (18,100 frames): 320x240, PC 0x00012B48, hash 0x5CB3039D
High-res (18,100 frames): 640x480, PC 0x00012B48, hash 0xE79E18C5
```

The high-res capture shows all four title buttons (Play, New Game, Load, Options),
the complete logo, and the marquee; it matches the 640x480 reference screenshot. The
mode is opt-in because 320x240 titles must not be upscaled. The passcode screen is a
320-wide UI scaled 2x; its five visible keyboard columns are the game's own window,
not a framebuffer crop. The five 3,000-frame low-res baseline hashes are unchanged:
`0xF4BDF79B`, `0x932C7722`, `0xEB31636F`, `0xA2B49CF0`, and `0xC21BB6A1`.

#### Diagnostic input and RAM patches (2026-10-24 late pass)

`--poke ADDR=HEX` writes bytes into machine RAM after the state loads. It is a
diagnostic for input/state investigations, not a game fix. Multiple patches are
separated by `;`.

```powershell
# Patch the Lemmings passcode scratch buffer and inspect the next frame
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:THREEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --state-in artifacts/s2/lem-pass.state --frames 3 `
  --poke "0x96574=43414A4A4C444D424556" --frame-out artifacts/poke.png
```

`--autopilot road` is an experimental Road Rash driver. It holds B and steers
toward the gray road centre (target screen X 230). It follows the road through the
first section but does not avoid traffic or recover from a wreck yet.

```powershell
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:THREEDO_GAMES\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --state-in artifacts/s2/rr8-30k.state --frames 10000 `
  --autopilot road --capture-dir artifacts/rr-auto --capture-every 1000 `
  --progress 1000 --json
```

The autopilot/steering tests are in
`tests/ThreeDo.Tests/Host/RoadRashAutopilotTests.cs`; the RAM-patch parser tests
are in `tests/ThreeDo.Tests/Host/MemoryPatchTests.cs`.

### 1.4 Fourth pass: Lemmings Fun4 gameplay and a traffic-aware Road Rash driver (2026-10-24)

All runs use the Release build, `panafz1.bin`, and a fresh NVRAM. Tests are at
572 passing (563 + closing-side, sharp-bend, pacing, coast-floor, and
detector-upset cases); `dotnet format --verify-no-changes` is clean.

**Lemmings Fun4 gameplay loop.** The passcode screen is reachable from
`lem-new-end.state` (`Down` + `A`, see `lem-pass-open.txt`); the investigation
base `lem-pass.state` is at 21,200 frames (`PC 0x20628`, hash `0x4321A4A5`)
with selection on `M` and one `M` already typed by the opening `A` press.
`Right` held 10 frames moves `M` -> `N`:

```
21,200 frames: PC 0x20628, hash 0x4321A4A5 (passcode, selection M, typed M)
21,240 frames: PC 0x230A4, hash 0x73B0DEAD (selection N, typed M, preview N)
```

`lem-toN.txt` (`21200 Right` / `21210 None`) reproduces the second line and
saves `lem-pass-N.state`. The 10-letter entry buffer is ASCII but permuted
across RAM: display order maps to `0x96577, 0x96576, 0x96575, 0x96574,
0x9657B, 0x9657A, 0x96579, 0x96578, 0x9657F, 0x9657E`, with a `JC` prefix at
`0x96572-73`. `0x96576` is the blinking preview slot and is refreshed from the
current selection every frame, so it must already hold the code's 2nd letter
when `B` is pressed. From the `N`-selected state, poking the other nine bytes
of `HNLHCIOECW` (Fun 4, 3DO list) and pressing `B` is accepted:

```powershell
# From lem-pass-N.state (selection N): poke Fun4 (2nd letter N matches), confirm
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:THREEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --state-in artifacts/s2/lem-pass-N.state --frames 400 `
  --poke "0x96574=48;0x96575=4C;0x96577=48;0x96578=45;0x96579=4F;0x9657A=49;0x9657B=43;0x9657E=57;0x9657F=43" `
  --input-script artifacts/s2/lem-code-HN.txt `
  --frame-out artifacts/s2/lem-fun4-gameplay.png `
  --state-out artifacts/s2/lem-fun4.state --json
```

`lem-code-HN.txt` presses `B` (confirm), `A` (briefing), `A` (start). The game
shows `Code for 1` / `Rating = Fun` (21,320 frames, `PC 0x12B74`, hash
`0xEA7E3E8D`), then the briefing (`Press Any Button`, 21,440 frames,
`PC 0x59DD4`, hash `0x64C5DEA5`), then gameplay (21,640 frames, `PC 0x83FEC`,
hash `0x18F38B4D`) with pillars, green ground, lemmings, and the skill HUD.

Save/load at 21,640 frames:

- Live state saved at 21,640: frame hash `0x18F38B4D`.
- Zero-frame load: `0x18F38B4D` (pixel-identical).
- Two independent 120-frame continuations: both `PC 0x83FD8`, hash `0x8D628FBD`
  (deterministic; the level animates without input).

Typing ten `M`s via `A` still auto-confirms and correctly reports `Incorrect`
(`MMMMMMMMMM` is not a valid code), proving the confirm path works with and
without pokes. A sequential poke at `0x96574` alone is NOT sufficient: the
validation reads the permuted slots, so a sequential poke renders scrambled
and reports `Incorrect`.

**Road Rash driver (v8).** `--autopilot road` now holds `B`, steers to the gray
road centre (target X 230) measured in the lower band (rows 190-214, so
building facades cannot outvote the asphalt), and adds: lookahead obstacle
steering (rows 120-150, steers to the clearer side when the centre is blocked),
wall avoidance (does not steer into a walled side when the other side is open),
emergency braking (`A`, no `B`) when the road ahead is fully blocked, coasting
(no throttle) through narrow walled canyons, and stuck recovery (stable
off-centre road for 300 frames steers toward the side with visibly more road
for 90 frames). Progress lines and `run --json` report `autopilotObstacle`
and `autopilotRecovery` alongside `autopilotLineX`/`autopilotSteering`.

Best run from `rr8-30k.state` (30,000 frames) with 10,000 autopilot frames ends
at 40,000 frames (`PC 0x12850`, hash `0x1B9CCE1D`). The driver visibly corrects
away from the first city wall (`obst=1` at 31,000 frames) and triggers stuck
recovery (`RECOVERY` at 33,000-34,000 and 37,000-39,000 frames), but the bike
still pins against a city wall around 34,000 frames (speed 0, lineX frozen at
~239, `RECOVERY` cycling without movement; sustained `B`+steer/`A` inputs from
the pinned state all converge to the same hash, i.e. the pin is terminal and
input has no effect once wedged). The 5.5-mile city race is therefore NOT
finished yet; the remaining work is earlier wall anticipation (the lower band
still locks onto sidewalks/shadows before the lookahead sees the wall) and/or a
track-specific racing line through the 32,000-34,000-frame city section.

#### Road Rash driver v9: the "pin" was the BUSTED screen (2026-10-25)

The terminal pin above is the race-over `BUSTED` screen, not a physics wedge.
From `rr-auto-v8.state` (40,000 frames), 400 frames with no input followed by
`B` produce the plainclothes-cop `BUSTED` screen (hash `0x7D470158`); the same
happens from `rr-v10full.state` and `rr-v17-40k.state` (`0x9F44FAA2`). No input
resumes a lost race, so the earlier input probes could only converge.

Driver v9, covered by tests (572 passing):

- Road pixels are capped at brightness 120 (was 150), so bright concrete
  sidewalks and plazas no longer count as asphalt in the lower band.
- A frame-to-frame `lineX` jump over 25 px is treated as a detector re-lock:
  the driver holds straight for 30 frames instead of chasing the false lock.
  The test is frame-to-frame, so a frozen line cannot latch the hold.
- Closing-side steering crosses away when the steering-side lookahead fraction
  is below 0.6 while the other side is above 0.8, coasting during the crossing.
- Sharp bends (centre below 0.35, one side above 0.8, the other below 0.4) are
  braked into while steering to the open side.
- Built-up sections duty-cycle the throttle 40/60 frames, but pacing, caution,
  and bend lift may never coast more than 20 consecutive frames; the throttle
  is forced back on because a stopped bike is caught and busted.
- Stuck recovery triggers after 150 stable off-centre frames (was 300), brakes
  30 frames, then re-applies throttle with the recovery steer.
- Progress lines add `la=<left>/<centre>/<right>` plus `PACE`/`UPSET`;
  `run --json` adds `autopilotPacing`, `autopilotUpset`, and
  `autopilotLookahead`.

```powershell
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:THREEDO_GAMES\Road Rash (USA).chd" --bios bios\panafz1.bin `
  --state-in artifacts\s2\rr8-30k.state --frames 10000 --autopilot road `
  --progress 1000 --frame-out artifacts\s2\rr-v17-40k.png `
  --state-out artifacts\s2\rr-v17-40k.state --json
```

Best v9 run: 40,000 frames, `PC 0x000125E4`, hash `0xE44B073D`. The bike keeps
live road detection to about 36,500 frames (the v8 run lost it at ~34,000), then
leaves the road and the race is lost. Roundtrip: zero-frame load reproduces
`0xE44B073D`; two 120-frame autopilot continuations both end at
`PC 0x000125E4` / `0xE44B073D`; pressing a button advances to `BUSTED`
(`0x9F44FAA2`). The 5.5-mile city race is still NOT finished; the next work is
vision/control quality (lane tracking through bends, police/traffic avoidance),
not pin recovery.

### 1.4a Seventh pass: per-game fixes and shell features (2026-10-25)

`GameCompatibility` now applies the 4DO fix mapping automatically. Alone in the
Dark (`9f7d72bccaacfc84f230c8d1a7189329`) runs with `FIX_BIT_TIMING_6` (`0x20`);
its 3,000-frame hash remains `0xF4BDF79B`, so the fix does not change the boot
screen. The other four corpus IDs have no 4DO fix bits. The CLI reports `gameId`
and `fixMode` in `run --json` and `info`, and accepts `--fix-mode 0xMASK` /
`--no-game-fixes`. The desktop shell gained the game library, settings dialog,
six-player binding capture, numbered save slots, PNG screenshots, and the FPS
overlay; 639 tests pass. The Input dialog edits all six players.

### 1.4b Eighth pass: renderer correctness and desktop validation (2026-10-25)

The five 3,000-frame low-res regression hashes are unchanged after the eighth-pass
renderer and desktop work:

| Title | Final PC | Frame hash |
|---|---|---|
| Alone in the Dark | `0x000C4CB4` | `0xF4BDF79B` |
| Lemmings | `0x0012B3C8` | `0x932C7722` |
| Road Rash | `0x000B9F84` | `0xEB31636F` |
| Space Ace | `0x00076418` | `0xA2B49CF0` |
| Wolfenstein 3D | `0x000699E8` | `0xC21BB6A1` |

Commands:

```powershell
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:THREEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --json
```

Desktop renderer validation (Windows, OpenGL 4.0, 1500x978 output):

- Loaded `artifacts/s2/lem-fun4.state` through the shell, ran at 60 fps, and captured
  PNG screenshots through the OpenGL readback path.
- Sharp-bilinear and xBR screenshots show the Lemmings level with correct colors;
  before the palette fix the same frames appeared grayscale.
- The xBR screenshot is edge-smoothed and retains the game image. It is an opt-in
  filter and is not part of the regression hash.
- The in-app Logs window showed the renderer-ready and shell-start events; the first-run
  guide, save-slot thumbnail, and error-dialog paths were exercised manually.

The screenshots are local artifacts and are not committed. The 659-test suite covers
the gamepad mapper, log filter, shader source invariants, and slot thumbnails.

### 1.5 Fifth pass: Road Rash v10 reaches 91,000 live frames (2026-10-25)

All runs use the Release build, `panafz1.bin`, and the local artifacts under
`artifacts/s2`. Tests are at **578 passing** at the time of this pass (572 +
close-traffic, urgent-preemption, emergency-steer-hold, stopped/wreck restart,
moving-scene, and open-road restart cases); the current suite is 605.
`dotnet format --verify-no-changes` is clean. The five
3,000-frame low-res hashes are unchanged:

```
Alone 0xF4BDF79B  Lemmings 0x932C7722  Road Rash 0xEB31636F
Space Ace 0xA2B49CF0  Wolfenstein 0xC21BB6A1
```

**Driver v10 changes.** Dense captures at 33,800-34,000 showed the loss was close
traffic, not a wall: another racer filled the lookahead centre (0.43/0.07/0.17)
while every v9 escape rule required a 0.6-wide opening. v10 adds:

- Close-traffic coast/steer: centre < 0.2, one side in [0.3, 0.6), side
  difference > 0.2 -> steer into the gap and lift. No brake: a 25-frame full
  brake from speed 43 stopped the bike and got it busted.
- Urgent preemption only for close traffic; ordinary obstacle steering stays on
  the 16-frame decision interval. Applying every obstacle bias immediately
  oversteered into a taxi at ~32,000.
- Emergency-brake steer hold: with all lookahead fractions < 0.35, keep the last
  steer and brake instead of chasing a `LineX` jump into the tree at 33,890.
- Stopped/wreck restart: a signature over the left scenery (rows 96-156,
  x 20-140) and the speed readout stays still for 40 frames -> hold B with
  straight bars for 180 frames, then retrigger. The centre is excluded because
  the rider animation reset the first signature. Manual probes: B alone
  remounts (34,500 and 80,000 -> 52 mph); steering keeps the rider running.
  A cyclic right/left search was tried and drove the 83,000 run into BUSTED
  (`0x9F44FAA2`), so it was reverted.

**Best run chain** (states are local, gitignored artifacts):

```
rr8-30k.state (30,000)
  + 6,000  -> rr-v28-36000.state (36,000)
  + 44,000 -> rr-v34-80000.state (80,000, PC 0x00098004, hash 0xD3309E17)
  + 3,000  -> rr-v35-83000.state (83,000, PC 0x00091A70, hash 0x442D7D3D)
  + 8,000  -> rr-v36-91000.state (91,000, PC 0x00085F48, hash 0x02ABE78E)
```

The 30k->36k and 36k->80k segments used earlier v10 revisions (right-steer crash
restarts); the final throttle-only code was verified on 80k->83k and 83k->91k
and reproduces the 91,000 state exactly:

```powershell
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios\panafz1.bin `
  --state-in artifacts\s2\rr-v35-83000.state --frames 8000 --autopilot road `
  --frame-out artifacts\s2\rr-v43-91000.png `
  --state-out artifacts\s2\rr-v43-91000.state --json
```

**Save/load at 91,000 frames:**

- Live state: `PC 0x00085F48`, hash `0x02ABE78E`, `cov=0.93`, `lineX=197.9`.
- Zero-frame load: `0x02ABE78E` (pixel-identical).
- Two independent 120-frame autopilot continuations: both `PC 0x000973EC`,
  hash `0x3F27B3D6`, `lineX=197.875`.

**Wait states fixed by the media-access pulse (ninth pass).**

- The ~42,000 and ~92,000/100,000 stops were a static, input-ignoring wait; the
  CD trace contained only READ_ID polls. The fix is in the CD device: two
  consecutive READ_ID commands with no intervening command and at least 50
  frames between them set the media-access bit (`0x40`) in the poll register.
  Portfolio uses that event to finish the post-race/post-load transition.
- From the 42,000 state, the fix advances to the `BUSTED` screen (~43,500); with
  A presses it reaches the race-select menu ("The City", "Sierra Nevada", "Napa
  Valley", "The Peninsula") and starts a new race.
- From the 100,000 state, the fix advances to `BUSTED` at ~102,000. From there,
  A pulses at 300-frame intervals pass the cutscenes and race select and start a
  new race at ~112,500.
- The five 3,000-frame corpus hashes are **unchanged** by the fix. The old 91k
  autopilot state/hash (`0x02ABE78E`) was a frozen presentation; the current
  chain reaches a rider-run/BUSTED transition at 91,000 (`PC 0x00093AF4`, hash
  `0x70E1ED8E`). Do not use the old hash as a regression key.
- The autopilot now pulses A during long-static, low-road-coverage, or CD-load
  states (`autopilotMenuAdvance` in `run --json`). This gets through the
  BUSTED/race-select flow, but it still needs work at the **new race start**:
  the bike can spawn at the right edge and stay there with the throttle held.
  That is a driving/start-line problem, not the CD wait.

**Telemetry.** Progress lines add `cov=<lower-band road fraction>` and
`RESTART`; `run --json` adds `autopilotRoadCoverage`,
`autopilotCrashRestart`, and `autopilotStaticFrames` alongside the v9 fields.
The ninth pass also reports `cdStatus` and `cdPoll` in `run --json`; a
READ_ID-only wait has `cdStatus=0xE1` and `cdPoll=0x0C`.

### 1.5a Ninth pass: per-title save-state, screenshot, and WAV evidence (2026-10-25)

All runs use `panafz1.bin` and the Release build. Each case loads a live state,
captures the zero-frame frame hash, runs 120 frames, and captures a PNG. The
states are local, gitignored artifacts under `artifacts/s2` and `artifacts/`.

| Title | State | Zero-frame PC/hash | 120-frame PC/hash |
|---|---|---|---|
| Wolfenstein 3D | `w3d_gameplay.state` (18,000) | `PC 0x00074A04`, `0xF568EBCA` | `PC 0x000782B8`, `0xF568EBCA` |
| Lemmings | `lem-fun4.state` (21,640) | `PC 0x00083FEC`, `0x18F38B4D` | `PC 0x00083FD8`, `0x8D628FBD` |
| Alone in the Dark | `alone-fix16k.state` (16,000) | `PC 0x0008C608`, `0x3D257B5C` | `PC 0x00095AC0`, `0x2ECCE988` |
| Road Rash | `rr-v36-91000.state` (91,000) | `PC 0x00085F48`, `0x02ABE78E` | `PC 0x000821E0`, `0x34BAD64C` |
| Space Ace | `sa-fix12k.state` (12,000) | `PC 0x0007D114`, `0xEF99FED7` | `PC 0x000284EC`, `0xA76D03E4` |

600-frame WAV captures from the same states:

| Title | Duration | Rate | Peak | RMS L/R | Clipped |
|---|---:|---|---:|---|---:|
| Wolfenstein 3D | 10.03 s | 44.1 kHz stereo | 18100 | 2112 / 2229 | 0 |
| Lemmings | 10.04 s | 44.1 kHz stereo | 22283 | 4510 / 4526 | 0 |
| Alone in the Dark | 10.06 s | 44.1 kHz stereo | 20617 | 1879 / 1926 | 0 |
| Road Rash | 10.03 s | 44.1 kHz stereo | 32768 | 9561 / 8787 | 378 |
| Space Ace | 10.04 s | 44.1 kHz stereo | 4096 | 1832 / 1832 | 0 |

Road Rash still clips in the first 10 s of the 91k state; the game mix is loud
there. Space Ace's capture is a mono signal duplicated to both channels. The
cadence is ~737 samples/frame for all five titles, matching the earlier
observations.

**Space Ace interactive path (new).** From `sa-fix12k.state`, `X` at 12,000
returns to the title/menu (`PC 0x0000E12C`, hash `0x596B6A81`), and `A` at
12,200 starts the interactive scene. 3,000 frames later the run is at 15,000
(`PC 0x000765F0`, hash `0x7E780D93`). The 15,000 state roundtrips: zero-frame
load `0x7E780D93`; two independent 120-frame continuations both
`PC 0x000284EC`, hash `0x70B16422`. A 600-frame WAV from the gameplay state is
10.04 s, peak 4096, RMS 1832.

**Alone in the Dark menu/intro.** From `alone-fix16k.state`, the sequence
`Up, Left, A, Down` reaches the `Begin a new game / Resume a saved game` menu
at 16,380 (`PC 0x000125E4`, hash `0x22631207`). `A` at 16,600 starts the
intro/character scene (`PC 0x00095A24` at 17,200, hash `0x9C607707`). The
intro then stays completely still and ignores every button, with only READ_ID
polling in the CD trace; it has the same wait signature as the Road Rash stops.

**Road Rash 42k wait.** A fresh run from `rr8-30k.state` reaches 42,000 with
`PC 0x0001048C`, hash `0xCBDF6DDB`, `lineX=177.8`, `cov=0.90`. Before the
media-access fix, `B`, `A`, `B+Left`, `B+Right`, `A+Left`, `A+Right`, and bare
D-pad inputs all left the frame hash unchanged and the CD trace contained only
READ_ID. See 1.5b: the media-access pulse now advances this screen to `BUSTED`
and the race-select/new-race flow.

### 1.5b Ninth pass: CD media-access pulse unblocks the post-race wait (2026-10-25)

The wait states at Road Rash ~42k/~92k/100k and Alone's intro share a CD
signature: the CD task polls READ_ID (`cmd 0x83`) every 60 frames with no other
commands, the frame is static, and every input is ignored. The ninth pass found
that Portfolio expects a **media-access event** after the drive has been idle.
The CD device now sets poll bit `0x40` when two consecutive READ_ID commands
arrive with no intervening command and at least 50 frames between them. Boot
traffic never has that pattern, so the five 3,000-frame hashes are unchanged.

Evidence:

- Road Rash `rr-42k.state` + 3,000 frames with A: `BUSTED` by ~43,500; final
  `PC 0x00020640`, hash `0x5B3159A9` (the BUSTED screen).
- Road Rash `rr-100k-base.state` + 3,000 frames with A: `BUSTED` by ~102,000.
  From there A every 300 frames passes the cutscenes and race-select menu
  ("The City" / "Sierra Nevada" / "Napa Valley" / "The Peninsula") and reaches
  a new race at 113,000 (`PC 0x0007DCBC`, hash `0x277CCED7`).
- The autopilot now pulses A during long-static, low-road-coverage (coverage
  below 0.5), frozen-road-line, or CD-load states; `run --json` reports
  `autopilotMenuAdvance`. A fresh 30k run reaches the post-race cutscene and a
  new race around 70-80k, but still needs start-line handling.
- Alone's intro uses the same wait signature, but the media pulse alone does not
  advance it: `alone-intro.state` + 3,000 frames with no input stays at hash
  `0x9C607707`. A every 300 frames advances the man-at-desk cutscene to the
  room/mansion/title scenes: 29,200 frames, `PC 0x00020640`, hash `0x62304BCF`;
  a further 10,000 frames ends at 39,200 (`PC 0x0008BCA4`, hash `0xD1FABFD3`).
  A blind A pulse is enough to leave the wait but not yet enough to reach a
  stable gameplay room.
- **Alone movement loop.** From `alone-intro-A2.state` (39,200), the sequence
  `Up, Left, A, Down, Right` moves the character around the room (the sprite
  visibly walks between frames). At 39,800: `PC 0x0008D0EC`, hash `0x44C7576B`.
  The state roundtrips: zero-frame load `0x44C7576B`; two independent 120-frame
  continuations both `PC 0x0008BF00`, hash `0xE490C8C7`. This is the first
  scripted Alone movement/save-load loop.
- Wolfenstein's fresh-game load (`w3d-loadlong.state`, 54,000 frames) also stays
  static with no input (`0x07586A88`). A every 300 frames advances to the
  character-select screen and FLOOR 1-1 at 64,000 (`PC 0x00069F3C`, hash
  `0x2F5B9864`). The media pulse alone does not advance it; A is required.
- **Road Rash race/menu cycling.** From `rr-fix-60k.state`, the autopilot now
  passes the `GAME LOADING`/`Get ready` screen and is racing at 80,000
  (`PC 0x00092FF8`, hash `0x1074EF3F`). A further 20,000 frames cycle through
  race -> wreck/menu -> new race several times and end at 100,000
  (`PC 0x0008214C`, hash `0x0D46CF56`) with the bike on a sidewalk, not a
  finished race. The `restartStuck` rule (release throttle and pulse A after
  180 restart frames) is what gets through the start-line screens.

The 91,000-frame autopilot chain now reaches a rider-run/BUSTED transition
(`PC 0x00093AF4`, hash `0x70E1ED8E`) instead of the old frozen `0x02ABE78E`
frame. The old hash is historical, not a regression key. The five 3,000-frame
corpus hashes remain the regression gate and are unchanged.

#### CD disc-end fix (2026-10-23)

`CdromDevice.InitCd` originally reproduced FreeDO's double-add of the 150-frame
lead-in (`filesize = sectors + 150` followed by `LBA2MSF`, which adds 150 again).
The CD DIPIR compares the reported disc end with the volume header's block count
and rejects the disc when the end exceeds it by more than 512 frames. Lemmings
has 302 blocks of padding and passed; Road Rash (450), Space Ace (450), and
Wolfenstein (382) were rejected and fell into the shell's InsertDisc path. The
device now reports `MSF = sectorCount + 150`, i.e. the real end. This is an
intentional divergence from the FreeDO reference, which still has the bug.

#### Reference comparison

A local FreeDO harness (`References/freedo-master`, not committed) was built from
the same source revision and run against an ISO dump of the same CHD. Before the
disc-end fix, Road Rash's kernel debug (`KPRINT`) stream was byte-for-byte
identical to FreeDO through 30,000 frames, including
`GetCDType:Got error d556f106`, `FFS-Severe-System-extended-Volume offline`, and
the `$panasplash/PanaSplash`, `$insertdisc/InsertDisc`, and `$eldemo/ELDemo`
loads. That parity confirmed the failure was in the shared CD layer rather than
in the ARM or video port. FreeDO still has the double-add and still fails on
these discs; the C# port now intentionally diverges.

After applying the same disc-size correction to the harness host (returning
`sectorCount - 150`), FreeDO also reaches Road Rash's own OS and stops at the
audio path with `>>>ARM TO DSP FIFO DIRECT!!!`. The C# port reaches the slightly
later `SAudio channel 0 failed; error = -6996` message.

**This is not a shared DSP limitation.** The 2026-10-23 Opera comparison (below)
shows Opera running the same titles past this point. The FreeDO harness result only
describes the older FreeDO baseline; it is not the compatibility ceiling.

#### Opera reference run (2026-10-23)

The data track of each CHD was extracted to ISO with `3do disc dump`, then run with
the bundled Windows OperaCore (`References/CPP_SimpleGui/Cores/OperaCore`) and the
same `panafz1.bin` BIOS. The SimpleGui wrapper was launched with
`--debug-messages -n <iso>`; screenshots were captured at 15/35/60 s.

| Title | Opera result | Our port after 2026-10-24 DSP fix |
|---|---|---|
| Road Rash | Intro FMV rendered for 60 s | Intro FMV renders; DSP/audio runs; 30,000-frame run is stable |
| Wolfenstein 3D | Intro rendered for 60 s | Boot/OS screens render; DSP/audio runs; 30,000-frame run is stable |
| Alone in the Dark | Intro/loading rendered for 60 s | Boot/logo renders; DSP/audio runs; 10,000-frame run is stable |
| Space Ace | Demo intro for 60 s, no runaway | Demo intro renders past 10,000 frames; no runaway |

Controls:

- The same ISO now also runs in our port, so the CHD reader is not the cause.
- `Arm60.Ncycle = 2` changed the execution point but was not needed for the fix.
- The DSP immediate-shift bug was the common cause of SAudio `-6996` and the Space
  Ace null-buffer copy. See [`Compatibility.md`](Compatibility.md).

The full method, reference hierarchy, and next-session plan are in
[`ReferenceStrategy.md`](ReferenceStrategy.md) and [`NextSession.md`](NextSession.md).

The DSP produces roughly 738 samples per frame (44.1 kHz at 60 fps) for all five
titles, and WAV capture is non-silent (Road Rash peaks around 8,800; Space Ace
around 4,096 in the first 1,200 frames). Audio content still needs a listening
check against the real hardware or Opera. The headless core runs a 3,000-frame
smoke test in roughly 4-8 seconds on the development machine; that is **not** a
4K presentation benchmark.

#### Diagnostics used

The CLI now exposes the tools used for the comparison:

```powershell
# Kernel debug output and the PC of every character (plus LR/SP/stack words)
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 3000 `
  --kprint artifacts/kprint.txt --kprint-pc artifacts/kprint-pc.txt

# CD command/sector trace
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 3000 --cd-trace artifacts/cd.log

# CLIO timer/FIQ register trace
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 3000 --clio-trace artifacts/clio.log

# Instruction ring buffer around a stop condition
dotnet run --project src/ThreeDo.Cli -- trace "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --trace-frames 600 --ring 8192 `
  --stop-kprint "Chunk XTRA" --out artifacts/trace.txt

# Low-memory write watchpoint (writer PC, value, size, registers, caller)
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 700 `
  --watch-lowmem artifacts/watch.txt --watch-from-frame 681 `
  --watch-start 0 --watch-end 0x100

# RAM and DSP memory dumps at the end of a run
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 3000 `
  --dump-ram artifacts/ram.bin --dump-ram-offset 0 --dump-ram-length 0x300000 `
  --dump-dsp artifacts/dsp

# DSP instruction ring at a KPRINT stop
dotnet run --project src/ThreeDo.Cli -- trace "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 0 --trace-frames 3000 `
  --stop-kprint "SAudio channel" --dsp-trace artifacts/dsp-trace.txt `
  --dump-dsp artifacts/dsp-stop --out artifacts/trace.txt

# Scripted controller taps: frame, then a comma-separated button list.
# Buttons are A,B,C,P,X,L,R,Up,Down,Left,Right; None releases all.
@'
# frame buttons
0    None
120  P
130  None
240  A
250  None
'@ | Set-Content artifacts/input.txt
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 3000 `
  --input-script artifacts/input.txt --frame-out artifacts/input.png

# Script frames are absolute. With --state-in, either use the state's frame
# numbers (for example 91000) or add --script-base state to shift a script that
# starts at 0 by the loaded state's frame count.
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --state-in artifacts/live.state --frames 300 `
  --input-script artifacts/input.txt --script-base state --json

# Controller packet trace (one 16-byte PBUS packet per frame)
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 600 `
  --input-script artifacts/input.txt --pbus-trace artifacts/pbus.log

# CEL command/geometry trace
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" --frames 600 --cel-trace artifacts/cel.log

# Disassemble raw little-endian ARM words from a RAM dump
dotnet run --project src/ThreeDo.Cli -- disasm artifacts/ram.bin `
  --offset 0x12380 --count 100 --base 0x0
```

### 1.6 Tenth pass: Lemmings pure D-pad passcode and CRT bloom (2026-10-26)

**Lemmings Fun 4 passcode typed with the D-pad.** The ninth-pass path used a
diagnostic RAM poke for the passcode. The tenth pass types `HNLHCIOECW` entirely
with the controller from a passcode-screen state with an empty entry and the
selection on `V` (`artifacts/lem_pass_fresh.state`, 21,700 frames). The script is
committed as [`scripts/lem-fun4-dpad.txt`](scripts/lem-fun4-dpad.txt); it uses
15-frame direction holds and 5-frame `A` taps, with one release frame between
actions so the CLI's frame coalescing cannot swallow the release.

```powershell
dotnet run -c Release --project src/ThreeDo.Cli -- run artifacts/s2/lem.iso `
  --bios bios/panafz1.bin --state-in artifacts/lem_pass_fresh.state --frames 1300 `
  --input-script docs/scripts/lem-fun4-dpad.txt --script-base state `
  --frame-out artifacts/lem-dpad-gameplay.png `
  --state-out artifacts/lem-dpad-gameplay.state --json
```

The game shows the `Fun 4 / Level 1 / Save the Lemmings` briefing at 22,600
(`PC 0x00020628`, hash `0xC1DF57C5`) and starts gameplay after the `A` tap at
22,650. At 23,000 the run is in the level (`PC 0x00083FD8`, hash `0xB7846C2D`)
with the HUD and lemmings visible. Roundtrip:

- Zero-frame load of `lem-dpad-gameplay.state`: `PC 0x00083FD8`, hash
  `0xB7846C2D` (pixel-identical).
- Two independent 120-frame continuations: both `PC 0x000230A4`, hash
  `0xEDEB1B9D`.

This closes the "pure D-pad typing of all ten letters is not scripted" item in
[`KnownIssues.md`](KnownIssues.md). The script starts at the passcode screen, not
at boot; the title-to-passcode transition is still the existing
`lem-pass-open.txt` (`Down`, `A`) path.

**CRT bloom.** `RendererSettings.Bloom` (0-1, default 0.35) and
`BloomThreshold` (0-1, default 0.6) add a 3x3 native-resolution glow around
bright areas in the CRT filter. The settings dialog exposes a CRT bloom slider.
Bloom is ignored by the non-CRT filters and by the CPU fallback. The shader is
covered by source-invariant tests; hardware GPU timing for the extra samples is
still pending.

**HQx 2x.** `VideoFilter.Hqx` runs Maxim Stepin/Cameron Zemek's hq2x on the
CPU-expanded BGRA frame, uploads the 2x result, and finishes with sharp-bilinear.
A 32x24 deterministic reference image matches the C++ `hq2x_32_rb` byte hash
`0xA400C421`; a uniform image is unchanged. The reference's `RGBtoYUV` table loop
stops before the last entry, so pure white is deliberately excluded from the
reference image. hq3x/hq4x are not shipped.

---

## 2. BIOS images

The local BIOS directory contains several variants. Only 1 MB images are accepted
by `BiosImage`; the 512 KB arcade image and the truncated Kanji image are not valid
inputs for the emulator.

| File | Size (bytes) | Intended use |
|---|---:|---|
| `panafz1.bin` | 1,048,576 | Primary NTSC test BIOS (MD5 `f47264dd47fe30f73ab3c010015c155b`) |
| `panafz10.bin` | 1,048,576 | Alternate NTSC BIOS |
| `panafz10-patched.bin` | 1,048,576 | Alternate NTSC BIOS |
| `goldstar.bin` | 1,048,576 | GoldStar BIOS |
| `sanyotry.bin` | 1,048,576 | Sanyo BIOS |
| `panafz10e-anvil.bin` | 1,048,576 | Anvil BIOS path |
| `panafz10e-anvil-patched.bin` | 1,048,576 | Anvil BIOS path |
| `panafz10ja-anvil-kanji.bin` | 1,048,576 | Japanese Anvil/Kanji BIOS |
| `panafz1-kanji.bin` | 933,636 | Not accepted (not 1 MB) |
| `3do_arcade_saot.bin` | 524,288 | Not accepted (arcade ROM, not a 1 MB BIOS) |
| `panafz10.bin.fstree` | 17,459 | Metadata, not a BIOS |

Use `panafz1.bin` for the default smoke test. Use the Anvil and GoldStar variants
only when validating compatibility-specific code paths.

---

## 3. CLI commands

Set the environment variables used by the opt-in tests. `THREEDO_GAMES` is the
corpus root and is different on each machine; everything else is derived from it.

```powershell
$env:THREEDO_BIOS  = (Resolve-Path "bios\panafz1.bin")
$env:THREEDO_GAMES = "D:\Samples\3DO"          # office machine
# $env:THREEDO_GAMES = "E:\Panasonic 3DO"      # home machine
$env:THREEDO_DISC  = "$env:THREEDO_GAMES\Road Rash (USA).chd"
Get-ChildItem $env:THREEDO_GAMES
```

Inspect a disc without booting it:

```powershell
dotnet run --project src/ThreeDo.Cli -- info "$env:THREEDO_DISC" --json
```

List or extract the 3DO (Opera FS) file system:

```powershell
dotnet run --project src/ThreeDo.Cli -- fs list "$env:THREEDO_DISC" --json
dotnet run --project src/ThreeDo.Cli -- fs extract "$env:THREEDO_DISC" `
  --out "$env:TEMP\threedo-fs" --filter "Rash/"
```

`fs` uses the `VideoGameFileSystemParser` package (MIT) and currently accepts CHD
images; `info` and `disc dump` use the emulator's own readers.

Run a fixed number of frames and dump the last frame:

```powershell
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" `
  --frames 600 `
  --nvram artifacts/nvram.ram `
  --frame-out artifacts/road-rash-600.png
```

The CLI loads a fresh 32 KB NVRAM volume when `--nvram` is omitted. Pass `--nvram`
to keep the same save data across runs; games that check save-space (Alone in the
Dark) need a valid volume to get past their startup warning.

Capture audio for listening:

```powershell
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" `
  --frames 600 `
  --audio wav `
  --wav-out artifacts/road-rash-600.wav
```

Run the deterministic self-test, the synthetic benchmark, and the CHD/ISO
prefetch comparison:

```powershell
dotnet run --project src/ThreeDo.Cli -- selftest
dotnet run --project src/ThreeDo.Cli -- bench --frames 600
dotnet run --project benchmarks/ThreeDo.Benchmarks -- --frames 600 `
  --bios bios/panafz1.bin --disc "$env:THREEDO_GAMES\Lemmings (USA).chd" --json
dotnet run --project benchmarks/ThreeDo.Benchmarks -- --frames 600 `
  --bios bios/panafz1.bin --disc "$env:THREEDO_GAMES\Lemmings (USA).chd" --prefetch --json
```

Desktop validation:

```powershell
dotnet run --project src/ThreeDo.Frontend.Avalonia
```

Open the BIOS, open a disc, press **Play**, and check video, audio, input, pause,
reset, quick save/load, screenshots, fullscreen (button or F11/Escape), the Hi-Res
restart path, and the About dialog. If OpenGL initialization fails, the status line
should report the fallback and the CPU `WriteableBitmap` path should render.

---

## 4. Test matrix

Record the BIOS, disc, and commit for every result. A pass means the expected
screen/audio/input behavior was observed; "boots without crashing" alone is not a
compatibility pass.

| Area | Cases |
|---|---|
| BIOS boot | `panafz1.bin`, `panafz10.bin`, `goldstar.bin`, Anvil BIOS |
| Disc boot | Alone in the Dark, Lemmings, Road Rash: BIOS animation -> title/menu |
| Gameplay | Reach gameplay in each title; exercise controller buttons |
| Audio | Title music/SFX and in-game audio; WAV capture is non-silent and correctly pitched |
| Save states | Save in-game, run 60 frames, load, verify frame/state hash and continued play |
| CHD streaming | Boot, seek, and stream from each CHD without frame stalls or read errors |
| Rendering | Accurate/nearest, sharp-bilinear, CRT, integer scaling, fullscreen, window resize |
| Performance | Native, 1080p, and 4K output; record p50/p95/p99 frame time and GPU time |
| Lifecycle | Pause/resume, reset, open/replace disc, close during play, reopen |

---

## 5. Rules

- Never commit BIOS ROMs, game images, extracted assets, screenshots containing
  copyrighted content, or WAV captures of copyrighted audio.
- Keep generated artifacts under `artifacts/`, which is gitignored.
- Do not add game-specific hacks without evidence; record the disc, BIOS, and
  observed behavior in `docs/Compatibility.md`.
- Real-content tests stay opt-in. CI must continue to pass with synthetic ROMs and
  discs when `THREEDO_BIOS`/`THREEDO_DISC` are unset.
