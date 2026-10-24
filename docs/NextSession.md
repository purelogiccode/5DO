# Next Session Handoff

**Date:** 2026-10-24
**Goal:** turn the now-bootable corpus into validated gameplay. The DSP and CCB
blockers are fixed; all five titles render menus or gameplay. This session is about
input, sustained play, save states, audio quality, and the remaining Alone in the
Dark dialog.

Read these first:

- [`Compatibility.md`](Compatibility.md) - current title matrix and decoded bugs.
- [`Status.md`](Status.md) - current position and ordered next steps.
- [`TestCorpus.md`](TestCorpus.md) - baselines, commands, and the `--input-script`
  format.
- [`PortingNotes.md`](PortingNotes.md) - DSP/CCB invariants.
- [`ReferenceStrategy.md`](ReferenceStrategy.md) - reference hierarchy.

---

## 1. What changed on 2026-10-24

### 1.1 DSP handshake (fixed)

`ThreeDoMachine.InternalFrame` now always executes `Dsp.Loop()`; the audio sink only
gates sample delivery. The DSP immediate loader now uses
`IMMEDIATE << (JUSTIFY & 3)` (0 or 3) instead of `~(JUSTIFY+1) & 3` (2 or 3). The
AudioFolio tail program's `Sema4Status & 8` test therefore works, `AllocInstrument`
is acknowledged, and the DSP runs instrument code. MUXB=3 now masks the product
(`(MULT1 * (Y >> 15)) & ~1`), matching FreeDO/Opera.

### 1.2 CCB field order (fixed)

`CcbParser` now reads the CCB body in FreeDO's order: next pointer, source pointer,
PLUT pointer, position, size, perspective, pixel-processor control, then `PRE0`/
`PRE1`. The old order shifted every 3D CEL's fields. Road Rash's race decoded a
bogus 1987x583 literal CEL with a 560-pixel-per-row perspective delta and appeared
to hang in `TexelDrawArbitrary`. The parser fix plus the missing `cnt_cross > 2`
x-point sort in `TexelDrawArbitrary` make the race render at normal speed.

### 1.3 New diagnostics

- `--input-script <path>`: `frame Button[,Button...]` lines. Frames are decimal or
  hex; buttons are `A,B,C,P,X,L,R,Up,Down,Left,Right`; `None` releases all. The
  state holds until the next line. Works with `--state-in`; frame numbers are
  absolute.
- `--progress N`: frame/PC/DSP semaphore lines on stderr every N frames.
- `--cel-trace <path>`: one line per CEL command with flags, preambles, geometry,
  clip, source/destination, plus per-draw timing and the first arbitrary-quad
  inputs. Huge CELs also dump 64 bytes of CCB data.
- `--dump-dsp`, `--dsp-trace`, `--watch-lowmem`, `--dump-ram`, `disasm` remain.

---

## 2. Current evidence

### Road Rash

```powershell
$t = "$env:TEMP\3do-next"
New-Item -ItemType Directory -Force $t | Out-Null

dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --frames 30000 --frame-out "$t\rr30k.png" --state-out "$t\rr30k.state"
```

- 30,000 frames: `PC 0x00016194`, frame hash `0xE2D08131`, race scene rendered.
- With `P` at frame 15900 the game shows the pause overlay; `P` again unpauses.
- A saved state at 15,800 plus `--input-script` reproduces the race in seconds.

### Wolfenstein 3D

- 10,000 frames with `--input-script input-taps.txt`: `PC 0x00052630`, frame hash
  `0xA5A33C4D`, in-game HUD `FLOOR 1-1`, `SCORE 0000000`, `ITEM 00`.
- Without input the title sits on boot/idle screens.

### Lemmings

- 10,000 frames with input: title menu with `LOAD NEW GAME`/`OPTIONS`.
- D-pad changes the highlighted item; `A` activates it. The 12,000-frame run with
  `Down`/`A` shows the `OPTIONS` panel.

### Alone in the Dark

- 10,000 frames with input: a warning dialog with a `Play Anyway` button.
- `A`/`Right`/`P`/`X` taps have not advanced the dialog. The DSP reports
  `running=False` after the dialog. This is the main open compatibility question.

### Space Ace

- Demo plays past 10,000 frames; no null-buffer copy and no FIQ at `0x1C`.
- 3,000-frame hash is now `0x2A9967FA` (CCB fix).

---

## 3. Ordered plan

### Step 1 - Finish Alone in the Dark's dialog

1. Save a state at the dialog (e.g., 10,000 frames with input).
2. Trace the dialog's input polling (`--trace --stop-kprint` or a PC ring) and
   identify which PBUS button/bit it reads.
3. Try every button, including combinations and held states.
4. Compare the same point with Opera; if Opera advances, differential the pad path
   or the warning condition.
5. Investigate why the DSP stops (`running=False`). Is the game intentionally
   stopping audio, or did an AudioFolio call fail?

### Step 2 - Complete a playable loop in each title

- Lemmings: navigate to `LOAD NEW GAME`, start a level, place a skill, verify input
  and frame updates.
- Wolfenstein: move, turn, fire, open a door, and check the HUD/score updates.
- Road Rash: accelerate, steer, finish or restart a race; verify CD streaming and
  audio during the race.
- Space Ace: start the game from the demo/attract loop and verify FMV/audio sync.
- Alone: get past the dialog and into the house.

Record the exact input scripts and frame numbers in `TestCorpus.md`.

### Step 3 - Save states and screenshots in-game

- Save/load at a gameplay frame for each title; compare frame hash and PC before and
  after a 60-frame continuation.
- Verify NVRAM and disc state survive the roundtrip.
- Capture screenshots at a known frame for each title.

### Step 4 - Audio quality

- Capture WAV for a race/level and compare loudness/spectrum with Opera.
- Check for clipping, DC offset, and the expected ~738 samples/frame.
- Verify the DSP stops/starts cleanly when the game changes audio modes.

### Step 5 - Performance

- Measure 3D CEL frame time on Road Rash's race. The CCB fix removed the pathological
  CEL, but the software rasterizer is still the likely bottleneck.
- Record p50/p95/p99 and allocations/frame; see [`Performance.md`](Performance.md).

---

## 4. Acceptance criteria

- [ ] Alone's dialog advances with a documented button/sequence.
- [ ] At least one title completes a full gameplay loop with input.
- [ ] Save/load works in-game for at least one title.
- [ ] WAV output is non-silent and plausibly matches the expected audio.
- [ ] `dotnet build -c Release`, `dotnet test`, and `dotnet format` pass.
- [ ] Docs and baselines are updated; repository is clean and pushed.

---

## 5. Do not do

- Do not re-enable the `AudioSink?.PushSample(Dsp.Loop())` short-circuit.
- Do not change the XBUS DMA byte order (`IRam` uses `b0,b1,b2,b3`).
- Do not port `opera_cdrom.c` wholesale; the CD path is working.
- Do not commit BIOS ROMs, game images, extracted assets, or WAV captures.
- Do not change `Arm60.Ncycle` from 4 without a differential test.

---

## 6. Useful commands

```powershell
# Build/test/format
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release
dotnet format --verify-no-changes

# Road Rash race from a saved state with pause/unpause
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --state-in "$env:TEMP\3do-next\rr30k.state" --frames 700 `
  --input-script "$env:TEMP\3do-next\rr-unpause.txt" `
  --progress 100 --frame-out "$env:TEMP\3do-next\rr-race.png"

# CEL trace at a known frame
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --state-in "$env:TEMP\3do-next\rr15800.state" --frames 400 `
  --input-script "$env:TEMP\3do-next\rr-late-taps.txt" `
  --cel-trace "$env:TEMP\3do-next\cel.log"

# Wolfenstein gameplay with scripted taps
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Wolfenstein 3D (USA).chd" --bios bios/panafz1.bin `
  --frames 10000 --input-script "$env:TEMP\3do-next\input-taps.txt" `
  --frame-out "$env:TEMP\3do-next\w3d.png"

# Opera reference
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\3do-opera\roadrash.iso"
```

---

## 7. Open questions

1. Which button/bit does Alone's `Play Anyway` dialog poll, and why does the DSP
   stop after it?
2. Does Wolfenstein's gameplay remain stable for 30,000+ frames with movement?
3. Does Road Rash's race finish, and does the CD stream music/SFX correctly?
4. Is the DSP sample cadence (567 vs Opera's 568) audible as pitch drift?
5. Are the CCB parser's relative-pointer and preamble paths correct for packed CELs
   with `CCB_LDPLUT` clear? The reference always reserves the PLUT word; tests cover
   this, but real discs should confirm it.
