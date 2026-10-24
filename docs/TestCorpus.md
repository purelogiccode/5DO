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

- **Road Rash:** from the 30k state, holding `A` freezes the race view while the CPU
  keeps running, and releasing it reaches the WRECK FMV. `P` alone leaves the race
  running. A deterministic autopilot is still open. The race is visually correct and
  matches the Opera attract-mode road/rider composition.
- **Wolfenstein:** from the 12,000-frame demo state, `A` opens the Select Previews
  menu; D-pad/A navigate to Select a Level and start a fresh game at FLOOR 1-1 with
  `SCORE 0000000`. A live 60-frame state saved after that (`0xC9989182`) roundtrips:
  two independent 120-frame continuations both end at `0x370158AC`, and a zero-frame
  load reproduces `0xC9989182` exactly.
- **Alone:** the 16,000-frame state has the character upright at the staircase. The
  control run walks through the room; the scripted D-pad/A run reaches the
  Begin/Resume main menu.
- **Lemmings:** the 18,000-frame state is the title screen. `Down` + `A` opens the
  passcode screen ("Enter the access code"); `A`/`X` append the highlighted letter
  and `Down` changes the row. The title's `A`/`P`/`X`/Left/Right do not leave the
  attract loop, even over 20,000 further frames. The level-start path is still open.
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
mode is opt-in because the passcode screen still clips/scrolls and its pointer
hit-testing diverges from the low-res path. The five 3,000-frame low-res baseline
hashes are unchanged: `0xF4BDF79B`, `0x932C7722`, `0xEB31636F`, `0xA2B49CF0`, and
`0xC21BB6A1`.

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

Run the deterministic self-test and the synthetic benchmark:

```powershell
dotnet run --project src/ThreeDo.Cli -- selftest
dotnet run --project src/ThreeDo.Cli -- bench --frames 600
```

Desktop validation:

```powershell
dotnet run --project src/ThreeDo.Frontend.Avalonia
```

Open the BIOS, open a disc, press **Play**, and check video, audio, input, pause,
reset, quick save/load, and screenshots.

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
