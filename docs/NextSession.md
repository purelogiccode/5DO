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

### 1.3 NVRAM (fixed in the CLI)

The CLI now loads a fresh 32 KB NVRAM volume by default and accepts
`--nvram <path>` to persist it. Without a valid volume, Alone in the Dark's
save-space check saw zero free slots, displayed its "There is only enough space on
your 3DO system to save X games" warning, and shut down. With a fresh volume it
reaches the in-game room/inventory scene.

### 1.4 New diagnostics

- `--input-script <path>`: `frame Button[,Button...]` lines. Frames are decimal or
  hex; buttons are `A,B,C,P,X,L,R,Up,Down,Left,Right`; `None` releases all. The
  state holds until the next line. Works with `--state-in`; frame numbers are
  absolute.
- `--progress N`: frame/PC/DSP semaphore lines on stderr every N frames.
- `--cel-trace <path>`: one line per CEL command with flags, preambles, geometry,
  clip, source/destination, plus per-draw timing and the first arbitrary-quad
  inputs. Huge CELs also dump 64 bytes of CCB data.
- `--pbus-trace <path>`: one line per frame with the 16-byte controller packet.
- `--nvram <path>`: load/persist the 32 KB NVRAM image.
- `--dump-dsp`, `--dsp-trace`, `--watch-lowmem`, `--dump-ram`, `disasm` remain.

---

## 2. Current evidence

### Road Rash

```powershell
$t = "$env:TEMP\3do-next"
New-Item -ItemType Directory -Force $t | Out-Null

dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --frames 30000 --nvram "$t\rr.ram" --frame-out "$t\rr30k.png" --state-out "$t\rr30k.state"
```

- 30,000 frames without input: `PC 0x0001965C`, frame hash `0x52346C14`; the demo
  eventually exits through the OS shutdown loop (`DSP running=False`).
- 30,000 frames with periodic input: `PC 0x00015278`, frame hash `0x0EBB2EDB`, DSP
  running, race scene with the pause overlay.
- `P` at frame 15,900 toggles the pause overlay; `P` again unpauses.
- A saved state at 15,800 plus `--input-script` reproduces the race in seconds.

### Wolfenstein 3D

- 5,000 frames with early `P`/`A` taps: in-game HUD with `FLOOR 1-1`,
  `SCORE 0000100`, `ITEM 00`.
- From a 5,000-frame state, `Up`/`Down`/`Left`/`Right`/`A`/`B`/`C` produce different
  frame hashes and PCs, so movement and actions are live.
- Without input the title sits on boot/idle screens.

### Lemmings

- 10,000 frames with input: title menu with `LOAD NEW GAME`/`OPTIONS`.
- D-pad changes the highlighted item; `A` activates it. The 12,000-frame run with
  `Down`/`A` shows the `OPTIONS` panel.

### Alone in the Dark

- With a fresh NVRAM (`--nvram`), 10,000 frames with input: `PC 0x0004B6B4`,
  frame hash `0x67FD3C6F`, in-game room/inventory scene.
- From that state, `Up`/`Down` and `Left`/`Right`/`A`/`B`/`C` produce different
  frame hashes and PCs, so the pad reaches the game.
- Without a valid NVRAM the game shows its "There is only enough space..." warning
  and shuts down; that is now understood and fixed in the CLI.

### Space Ace

- Demo plays past 10,000 frames; no null-buffer copy and no FIQ at `0x1C`.
- From a 10,000-frame state, `P` and `X` produce different hashes and PCs; `A`/`B`/`C`
  and the D-pad do not change the demo. `P`/`X` likely open the player's menu/control
  path; the interactive game still needs a dedicated script.
- 3,000-frame hash is now `0xA2B49CF0` (NVRAM/CCB fixes).

---

## 3. Ordered plan

### Step 1 - Complete a playable loop in each title

- Lemmings: navigate to `LOAD NEW GAME`, start a level, place a skill, verify input
  and frame updates.
- Wolfenstein: move, turn, fire, open a door, and check the HUD/score updates.
- Road Rash: accelerate, steer, finish or restart a race; verify CD streaming and
  audio during the race.
- Space Ace: start the game from the demo/attract loop and verify FMV/audio sync.
- Alone: move the character, pick up an item, and verify the inventory updates.

Record the exact input scripts and frame numbers in `TestCorpus.md`.

### Step 2 - Save states and screenshots in-game

- Save/load at a gameplay frame for each title; compare frame hash and PC before and
  after a 60-frame continuation.
- Verify NVRAM and disc state survive the roundtrip.
- Capture screenshots at a known frame for each title.

### Step 3 - Audio quality

- Capture WAV for a race/level and compare loudness/spectrum with Opera.
- Check for clipping, DC offset, and the expected ~738 samples/frame.
- Verify the DSP stops/starts cleanly when the game changes audio modes.

### Step 4 - Performance

- Measure 3D CEL frame time on Road Rash's race. The CCB fix removed the pathological
  CEL, but the software rasterizer is still the likely bottleneck.
- Record p50/p95/p99 and allocations/frame; see [`Performance.md`](Performance.md).

---

## 4. Acceptance criteria

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

1. Does Wolfenstein's gameplay remain stable for 30,000+ frames with movement?
2. Does Road Rash's race finish, and does the CD stream music/SFX correctly?
3. Is the DSP sample cadence (567 vs Opera's 568) audible as pitch drift?
4. Are the CCB parser's relative-pointer and preamble paths correct for packed CELs
   with `CCB_LDPLUT` clear? The reference always reserves the PLUT word; tests cover
   this, but real discs should confirm it.
5. Does the CLI's fresh NVRAM default match the desktop frontend's persisted image,
   and do in-game saves survive a `--nvram` roundtrip?
