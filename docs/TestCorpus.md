# Local Test Corpus

**Status:** local developer machine only; not part of the repository.
**Last inventoried:** 2026-10-23

This document records the legally obtained BIOS and disc images available on the
development machine for manual compatibility, boot, audio, input, save-state, and
performance testing. None of these files may be committed, packaged, or uploaded.

- BIOS directory: `bios/` in this repository (ignored by `.gitignore`)
- Disc directory: `E:\Panasonic 3DO\`

Both locations are outside version control. `bios/` is already ignored by
`.gitignore`, and `*.chd`/`*.iso`/`*.bin`/`*.cue` are ignored as a second guard.
`E:\Panasonic 3DO` is outside the repository entirely. The older `D:\Samples\3DO`
path used by previous handoffs no longer exists.

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

The following results were recorded on 2026-10-23 with `panafz1.bin`, a Release
build, and `run --frames 3000`. They are a regression baseline, not a full
compatibility pass:

| Title | Final PC | Frame hash | Frame at 3,000 | Observation |
|---|---|---|---|---|
| Alone in the Dark | `0x00010520` | `286162373` | blank | Boot log stops after the audio-folio init; `SAudio channel 0 failed; error = -6996` |
| Lemmings | `0x0012B36C` | `2469164834` | **rendered** | Reaches CyberIntro; the 3D intro renders correctly |
| Road Rash | `0x000BA124` | `2265977137` | blank | Loads the game OS (`grafmath 21.10.504`); waits on SAudio channel 0 (`-6996`) |
| Space Ace | `0xB323EEA0` | `1287603876` | **rendered** | ReadySoft player finds the CD-ROM and renders the demo intro; the audio task later copies to a null buffer and the CPU runs away |
| Wolfenstein 3D | `0x00077824` | `286162373` | blank | Loads Sherry/Operator/FS v24.225; waits on SAudio channel 0 (`-6996`) |

The final PCs changed on 2026-10-24 when the DSP was made to execute in headless
runs; the frame hashes did not. Use the frame hash as the stable regression key and
record the PC alongside the commit.

`PC 0x205C4`/`0x205C8` is the Portfolio **idle loop**, not a halt: VDL and timer
interrupts continue to run and the boot log advances. Earlier baselines that
described this address as a "halt path" were misreading the idle loop.

**Where this leaves us:** two titles render real frames (Lemmings, Space Ace), two
load their own OS and stop at audio initialization (Road Rash, Wolfenstein), and one
fails earlier (Alone in the Dark). No title has reached gameplay; input, save
states, audio, and CD streaming still need in-game validation. **Opera runs all four
problem titles past these points**, so the remaining failures are in our port. See
[`Status.md`](Status.md) and [`NextSession.md`](NextSession.md).

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

| Title | Opera result | Our port |
|---|---|---|
| Road Rash | Intro FMV rendered for 60 s | Stops at SAudio `-6996` |
| Wolfenstein 3D | Intro rendered for 60 s | Stops at SAudio `-6996` |
| Alone in the Dark | Intro/loading rendered for 60 s | Stops during AudioFolio init |
| Space Ace | Demo intro for 60 s, no runaway | Demo intro, then FIQ/runaway at frame ~681 |

Controls:

- The same ISO fails in our port, so the CHD reader is not the cause.
- `Arm60.Ncycle = 2` changed the execution point but did not fix SAudio or Space Ace.
- Opera's Space Ace mixer error `d52bf113` (`AF_ERR_BADFILETYPE`) is non-fatal; our
  runaway is a separate bug.

The full method, reference hierarchy, and next-session plan are in
[`ReferenceStrategy.md`](ReferenceStrategy.md) and [`NextSession.md`](NextSession.md).

The DSP produces roughly 737 samples per frame (44.1 kHz at 60 fps) for all five
titles, and WAV capture is non-empty. Audio content still needs a listening check.
The headless core runs a 3,000-frame smoke test in roughly 4-8 seconds on the
development machine; that is **not** a 4K presentation benchmark.

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

Set the two environment variables used by the opt-in tests:

```powershell
$env:THREEDO_BIOS = "C:\Users\HomePC\Dropbox\source\repos\CSharp_3DOEmulator\bios\panafz1.bin"
$env:THREEDO_DISC = "E:\Panasonic 3DO\Road Rash (USA).chd"
```

Inspect a disc without booting it:

```powershell
dotnet run --project src/ThreeDo.Cli -- info "$env:THREEDO_DISC" --json
```

Run a fixed number of frames and dump the last frame:

```powershell
dotnet run --project src/ThreeDo.Cli -- run "$env:THREEDO_DISC" `
  --bios "$env:THREEDO_BIOS" `
  --frames 600 `
  --frame-out artifacts/road-rash-600.png
```

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
