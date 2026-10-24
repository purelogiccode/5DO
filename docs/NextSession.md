# Next Session Handoff

**Date:** 2026-10-24 (fourth pass)
**Goal:** finish the scripted gameplay loops now that the 3D CEL renderer is correct
and the high-resolution frontend path exists. Road Rash's race renders like the Opera
reference and runs past 60,000 frames; Wolfenstein starts a fresh game, walks/turns/
fires, and its save states roundtrip; Alone's in-game character is upright; Lemmings
renders its 640x480 high-resolution title; Space Ace plays its demo. The remaining
work is scripted play, the Lemmings passcode entry sequence, screenshots/audio
comparison, and documentation. This pass added RAM patches (`--poke`) and an
experimental Road Rash road-following driver (`--autopilot road`).

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
the gray road centre (target screen X 230). It follows the road and passes the
first section, but it does not yet detect traffic or recover from a wreck, so it
does not finish a race. The unit tests cover the road-centre steering contract.

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
- Lemmings: the passcode screen is understood (A types, B confirms, ten letters
  auto-confirm, 15-frame D-pad holds move one key). The remaining task is a
  deterministic path to the ten letters of a valid code such as `IJJLDNCCCN`
  (Fun 2) or `CAJJLDMBEV` (Tricky 3), plus level start and a first move.
- Road Rash: the control map is known (B accelerate, A brake, D-pad steer, C
  attack, P pause). Improve the experimental `--autopilot road` with obstacle
  detection and wreck recovery, then finish the 5.5-mile city race and record the
  script/state/hash.
- Space Ace: start the interactive game from the demo (P/X are the current leads)
  and verify FMV/audio sync.

### Step 2 - save states and screenshots

- Repeat the Wolfenstein save/load proof for Road Rash, Alone, Lemmings, and Space
  Ace at gameplay frames.
- Capture a known screenshot for each title with `--frame-out` (or
  `--capture-dir`).
- Verify NVRAM and disc state survive the roundtrip.

### Step 3 - audio quality

- Capture 30-60 s of a race/level and compare loudness/spectrum with Opera.
- Check the 191-sample Road Rash clipping and the 737 vs 735 samples/frame cadence.
- Verify the DSP stops/starts cleanly when a game changes audio modes.

### Step 4 - performance

- Measure the 3D CEL frame time on Road Rash's race (the renderer is now correct).
- Record p50/p95/p99 and allocations/frame; see [`Performance.md`](Performance.md).

---

## 4. Acceptance criteria

- [x] At least one title completes a full gameplay loop with input (Wolfenstein:
  fresh game -> walk/turn/fire -> save/load).
- [x] Save/load works in-game (Wolfenstein: `0xC9989182` -> `0x370158AC` twice, and
  the 18,000-frame gameplay state roundtrips).
- [x] WAV output is non-silent and stereo 44.1 kHz; clipping/cadence recorded.
- [x] Road Rash's race renders correctly and runs past 60,000 frames.
- [x] The high-resolution 640x480 renderer matches Lemmings' title reference.
- [ ] The Lemmings passcode confirm and high-res pointer path are complete.
- [x] `dotnet build -c Release`, `dotnet test`, and `dotnet format --verify-no-changes` pass.
---

## 5. Do not do

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

1. What input sequence reaches the ten letters of a valid Lemmings passcode most
   reliably, and does the level start immediately after confirmation?
2. What obstacle-avoidance and wreck-recovery policy lets `--autopilot road` finish
   the 5.5-mile city race?
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
