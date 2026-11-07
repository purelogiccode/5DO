# Local Test Corpus

**Status:** local developer machine only; not part of the repository.
**Last inventoried:** 2026-10-30

This document records the legally obtained BIOS and disc images available on the
development machine for manual compatibility, boot, audio, input, save-state, and
performance testing. None of these files may be committed, packaged, or uploaded.

- BIOS directory: `bios/` in this repository (ignored by `.gitignore`)
- Disc directories: **machine-specific**, set `FIVEDO_GAMES` to the main root:
  - Main collection: `J:\Panasonic 3DO` (589 CHDs)
  - MAME software-list collection: `G:\MAME\MAME Software List CHDs\3do` (5 CHDs)
  - Older paths used in earlier handoffs: `D:\Samples\3DO` and
    `E:\Panasonic 3DO`

All locations are outside version control. `bios/` is already ignored by
`.gitignore`, and `*.chd`/`*.iso`/`*.bin`/`*.cue` are ignored as a second guard.
The full-collection sweep uses both roots explicitly; see
[`CompatibilitySweep.md`](CompatibilitySweep.md) and
[`GameCompatibilityList.md`](GameCompatibilityList.md). The corpus root is never
hard-coded in scripts; use `$env:FIVEDO_GAMES` and the `-Roots` parameter so the same
commands work on other machines.

**The corpus contents differ between machines.** Do not treat a title that is
missing on one machine as unavailable in the project. Use
`tools/release-soak.ps1` to discover the local discs, run the 36,000-frame release
soak for the titles that are present, and record absent titles as
`Missing on this machine`. If an archived 12k state is available for an absent
disc, the script can continue it to the target frame count; that is continuation
evidence, not a fresh-boot check.

> **2026-10-27 audio-clock rebaseline.** The DSP sample clock now produces exactly
> 44,100 samples per emulated ARM second (see
> [`PortingNotes.md`](PortingNotes.md) 12.4). Hashes recorded before this date were
> produced by FreeDO's 8.24 sample accumulator, which ran ~0.7% fast. Audio content
> and spectral peaks are unchanged, but FMV/loading frames can be one or more frames
> apart. The current 3,000-frame keys are in section 1.1; longer-run hashes in this
> document are historical unless they are explicitly marked as post-audio-clock.

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

Seven additional CHD titles (Gex, Need for Speed, Super Street Fighter II Turbo,
Total Eclipse, Killing Time, Theme Park, and Return Fire) were validated for boot,
render, save-state, and audio in section 1.7.

ISO/BIN/CUE parity testing still needs a legally obtained image in one of those
formats, or a locally generated fixture. Do not convert and commit a retail disc.

### 1.1 Current boot baseline

The following results were re-recorded on 2026-10-30 after the headless PBUS fix,
with `panafz1.bin`, a Release build, and `run --frames 3000 --audio null`. The CLI
now attaches a default player-1 pad, matching the desktop shell; the earlier keys
were recorded while headless runs reported no connected controller and are kept
below for historical comparison. The CLI loads a fresh 32 KB NVRAM volume by
default; pass `--nvram <path>` to persist it. They are the current regression
baseline, not a full compatibility pass:

| Title | Final PC | Frame hash | Frame at 3,000 | Observation |
|---|---|---|---|---|
| Alone in the Dark | `0x000C4C94` | `0x05225F76` | boot/logo | AudioFolio init succeeds; with input and NVRAM it reaches the in-game room/inventory scene by 10,000 frames |
| Lemmings | `0x0012B324` | `0x9C2E3236` | **rendered** | Reaches CyberIntro; at 10,000 frames with input it reaches the Load/Options title menu |
| Road Rash | `0x000BA0F4` | `0x5E9EE515` | **rendered** | Loads the game OS (`grafmath 21.10.504`); SAudio init succeeds; the 3,000-frame capture is an intro-FMV frame; the 30,000-frame run reaches the 3D race scene |
| Space Ace | `0x00076418` | `0xD666228E` | **rendered** | ReadySoft player finds the CD-ROM, plays the demo, and passes the former frame-681 crash; the 3,000-frame capture is the adjacent demo frame |
| Wolfenstein 3D | `0x00031F74` | `0x9A3B5131` | boot/logo | Loads Sherry/Operator/FS v24.225; differs from the pre-clock frame only in the star-field sparkle; with input it reaches gameplay (FLOOR 1-1) |

The pre-PBUS-fix 2026-10-27 keys were (`0x4EC2E1A7`, `0x6CCA83F5`,
`0xCE90FA5A`, `0xF349263B`, `0x9A3B5131`). The 2026-10-24 keys were
(`0xF4BDF79B`, `0x932C7722`, `0xEB31636F`, `0xA2B49CF0`, `0xC21BB6A1`); the
immediate pre-clock build at commit `46785b2` already produced
`0x4EC2E1A7`/`0x6CCA83F5` for Alone/Lemmings because of the VDLP pixel-stride
fix. Do not use the older keys after this date. The PBUS fix changes input-sensitive
hashes because games now see a connected pad; re-record longer-run hashes before
using them as regression keys.

### 1.1a ARM timing differential (2026-10-31; class ledger 2026-11-04)

The CORE-01 option was checked on the Lemmings boot with the same BIOS, disc, frame
count, and fresh NVRAM. The FreeDO-compatible default is unchanged; the ARM60
datasheet mode runs more instructions per frame because its N cycle is two ARM clocks
instead of four, so the guest reaches a different point at 3,000 frames.

| Mode | Final PC | Frame hash | Instructions | Cycles/frame |
|---|---|---|---:|---:|
| `--arm-timing freedo` | `0x0012B324` | `0x9C2E3236` | 194,135,650 | 208,356 |
| `--arm-timing arm60` | `0x0012A324` | `0xFBED38D3` | 272,410,816 | 208,341 |

Both modes completed the boot and produced a rendered frame. This is a single-title
smoke differential, not the full-collection report required to change the default.

The same pair of runs was repeated with `--cycle-classes` to audit the per-class ledger
introduced for CORE-01. The final frame hashes match the 2026-10-31 table above
(`0x9C2E3236` and `0xFBED38D3`), and the class averages show the expected N=2 reductions:

| Class | `freedo` cycles/instruction | `arm60` cycles/instruction |
|---|---:|---:|
| Multiply | 8.21 | 7.87 |
| Swap | 10.00 | 6.00 |
| DataProcessing | 1.06 | 1.03 |
| SingleDataTransfer | 6.38 | 3.98 |
| BlockTransfer | 10.07 | 7.42 |
| Branch | 4.18 | 3.11 |
| SoftwareInterrupt | 6.00 | 4.00 |

`Undefined` and `Coprocessor` did not occur during this boot. Every executed class is
within the exact per-instruction table in `PortingNotes.md` 12.28; the averages move only
because of operand-dependent multiply cost and condition-failed one-cycle slots. The
differential smoke (`benchmarks/diff/smoke.json`, 300 frames, accuracy profile) was
re-run after the ledger change and still reports **Match** with frame hash `0x110E7DC5`
on both engines.

#### 1.1a1 40-title ARM timing A/B (2026-11-04)

The CORE-01 option was measured across the 40-title sample
(`benchmarks/diff/sample40.json`, 300 frames, one checkpoint, `--no-game-fixes`,
`cdSpeed: unbounded`). Both 5DO runs used the same staged ISO that the Opera reference
captured from, the same BIOS, and a fresh NVRAM. The accuracy profile supplied the
CLIO/CEL/CD timing; only the ARM mode changed, selected by the new harness override
`--arm-timing freedo|arm60`.

| ARM mode | Matched | Diverged | Errors |
|---|---:|---:|---:|
| `freedo` (N=4) | 15 | 25 | 0 |
| `arm60` (N=2) | 17 | 23 | 0 |

Only two of the 40 final frame hashes changed, and both moved from Diverged to Match:

| Title | `freedo` hash | `arm60` hash | Opera hash |
|---|---|---|---|
| 20th Century Video Almanac (USA) | `0x6CDFBDC5` | `0x110E7DC5` | `0x110E7DC5` |
| Battle Chess (USA) | `0x6CDFBDC5` | `0x110E7DC5` | `0x110E7DC5` |

The other 38 hashes were identical. This is a 300-frame boot sample, not the 3,000-frame
594-disc report, so the 2026-11-04 decision keeps ARM60 opt-in. It does show that the
N=2 mode is not a regression risk on this sample and improves parity where the ARM
instruction stream reaches the frame boundary first. Command:

```powershell
tools/FiveDo.DiffHarness/bin/Release/net10.0/FiveDo.DiffHarness.exe `
  run artifacts/diff/sample40-iso.json --out artifacts/diff/sample40-arm60 `
  --arm-timing arm60 --no-images --resume
```

`sample40-iso.json` is the local sample40 manifest with each `disc`/`iso` pointing at the
staged ISO under `artifacts/diff/work/discs`; it is not committed because the paths are
machine-specific. The staged ISO is the data-sector image used by the reference core, so
CD-DA and subchannel behavior are outside this boot sample. The Opera captures are reused
by `--resume`, so the second run differs only in the 5DO ARM mode.

### 1.1b CLIO timer scheduler differential (2026-10-31)

The CORE-02 option was checked on the 3,000-frame boot with the same `panafz1.bin`,
fresh NVRAM, and disc files. The FreeDO-compatible default is unchanged; the Opera
per-slot scheduler changes when timer FIQs fire, so the guest reaches a different
instruction boundary at 3,000 frames.

| Title | Mode | Final PC | Frame hash | Instructions | Cycles/frame |
|---|---|---|---|---:|---:|
| Lemmings | `--clio-timers freedo` | `0x0012B324` | `0x9C2E3236` | 194,135,650 | 208,356 |
| Lemmings | `--clio-timers opera` | `0x0012DA44` | `0x44EEA93D` | 192,399,552 | 208,343 |
| Gex | `--clio-timers freedo` | `0x001105C4` | `0x05F38B50` | 209,334,971 | 208,338 |
| Gex | `--clio-timers opera` | `0x00110524` | `0xA9DE7B03` | 209,116,136 | 208,335 |
| Road Rash | `--clio-timers freedo` | `0x000BA0F4` | `0x5E9EE515` | 171,049,511 | 208,342 |
| Road Rash | `--clio-timers opera` | `0x00098AF0` | `0x36359402` | 170,826,129 | 208,350 |

All six runs completed and rendered. This is a smoke differential, not the full 594-disc
report required to change the default. The Lemmings and Road Rash FreeDO hashes match
the section 1.1 keys, confirming that the new option did not alter the default path.

#### 1.1b1 40-title CLIO timer A/B (2026-11-04)

The 40-title sample (`benchmarks/diff/sample40.json`, 300 frames, one checkpoint,
accuracy timing, `--no-game-fixes`, `cdSpeed: unbounded`) was captured twice with the
same build and the same Opera reference captures. Only the CLIO mode changed: the
accuracy timing selects `OperaScheduler`; the second run used the new harness override
`--clio-timers freedo`, which is forwarded to every 5DO capture.

| CLIO mode | Matched | Diverged | Errors |
|---|---:|---:|---:|
| `OperaScheduler` | 17 | 23 | 0 |
| `FreeDoCompatible` | 17 | 23 | 0 |

The same 17 titles match in both modes, and all 40 final frame hashes are identical.
The mode changes the checkpoint PC for 37 of the 40 titles but not the rendered frame at
300 frames, so the CLIO scheduler alone neither closes nor regresses a title in this
boot sample. The accuracy baseline is 17/40 rather than the previously recorded 18/40
because the CEL pixel-accounting fix changed the 5DO accuracy hashes; the Opera
reference captures are unchanged. The mode stays opt-in pending the full 594-disc
3,000-frame run.

Timer ledger and trace evidence: a 300-frame Lemmings boot with `--clio-timers opera
--clio-timer-stats --clio-timer-trace` reports timer 0 (141,814 ticks, 2 reloads),
timer 3 (70,688 ticks, 55 stops and 55 FIQs), and timer 4 (37,600 ticks, 93 reloads).
The trace shows timer 14 reloading `0x3d` every ~12,400 ARM cycles at slack `0x150`:
one scan is 15 two-cycle slots plus the `(4 + 336)` M25M-tick gap, or 200 ARM cycles,
so 62 executions take 12,400 cycles. The same title with `--clio-timers freedo` reports
183,177 timer-0 and 95,104 timer-3 ticks. That is the expected FreeDO all-timer scan
rate: 25 MHz/64 = 390.6 kHz per timer, versus Opera's 195.3 kHz at the default slack of
64 (62.5 kHz once the guest writes the `0x150` slack used by this title).

### 1.1c CEL busy timing differential (2026-10-31)

The CORE-03 option was checked on the 3,000-frame boot with the same `panafz1.bin`,
fresh NVRAM, and disc files. The FreeDO-compatible default is unchanged. Opera's busy
model defers the CEL draw and keeps the status register busy, so titles that render
large CELs early reach a different instruction boundary at 3,000 frames.

| Title | Mode | Final PC | Frame hash | Instructions | Cycles/frame |
|---|---|---|---:|---:|---:|
| Lemmings | `--cel-timing freedo` | `0x0012B324` | `0x9C2E3236` | 194,135,650 | 208,356 |
| Lemmings | `--cel-timing opera` | `0x0012B324` | `0x9C2E3236` | 194,135,650 | 208,356 |
| Gex | `--cel-timing freedo` | `0x001105C4` | `0x05F38B50` | 209,334,971 | 208,338 |
| Gex | `--cel-timing opera` | `0x001105C4` | `0x05F38B50` | 209,334,971 | 208,338 |
| Road Rash | `--cel-timing freedo` | `0x000BA0F4` | `0x5E9EE515` | 171,049,511 | 208,342 |
| Road Rash | `--cel-timing opera` | `0x000B9E9C` | `0xA39F031F` | 170,868,164 | 208,338 |

All six runs completed and rendered. Lemmings and Gex are pixel-identical because their
early boot path does not poll CEL status; Road Rash diverges at its early CEL-heavy boot
frames, which is the expected effect of serializing CEL work. This is a smoke
differential, not the full 594-disc report required to change the default.

Four more CEL-heavy titles completed 3,000 frames with `--cel-timing opera`:

| Title | Final PC | Frame hash |
|---|---|---|
| Alone in the Dark | `0x000C4CB4` | `0x328ABCD3` |
| Space Ace | `0x00076458` | `0xD666228E` |
| Wolfenstein 3D | `0x000320D8` | `0x9A3B5131` |
| Total Eclipse | `0x000C9870` | `0xAD0C92D2` |

Lemmings and Road Rash also completed 12,000 frames with the Opera model
(`0xCE7E7A74` and `0xAEE9963B` respectively), so the deferred draw and busy deadline do
not stall those boot paths.

#### 1.1c1 CEL busy work accounting (2026-11-04)

The 2026-10-31 runs used a work ledger that missed the 320x240 literal-line and
packed-repeat rasterizer paths: those paths wrote through `CelMemory.WriteHalfword`
without calling `CelEngine.AddPixels`, while the high-resolution paths went through
`WritePixel` and were counted. Opera counts every `process_pixel` call and its direct
`TexelDraw_Line` writes, so the busy deadline was too short for those lists.

After the fix, a 3,000-frame Road Rash boot with `--cel-timing opera --cel-trace` reports
five full-screen lists as `cels=1 pixels=76800 busy=106730` (two of them) and
`cels=2 pixels=153600 busy=213461`; the literal-line lists previously reported zero
pixels. The FreeDO path still produces the remembered golden frame hash `0x5E9EE515`
(PC `0x000BA0F0` on this fresh-NVRAM run; instruction totals vary with the initial NVRAM
bytes, so the frame hash is the regression key). The Opera path is now:

| Title | Mode | Final PC | Frame hash | Instructions |
|---|---|---|---:|---:|
| Road Rash | `--cel-timing opera` | `0x000B9D54` | `0x9512219E` | 170,818,687 |
| Lemmings | `--cel-timing opera` | `0x0012B324` | `0x9C2E3236` | 194,135,071 |
| Gex | `--cel-timing opera` | `0x001105C4` | `0x05F38B50` | 209,334,014 |

Road Rash also completed 12,000 frames in both modes (PC `0x00020640` for each):
FreeDO retained `0x2D05A53C` and Opera produced `0x81BDF30F`, so the longer busy
periods do not stall the boot. Lemmings and Gex do not poll CEL status in their early
boot, so they remain pixel-identical to the FreeDO path. The 300-frame accuracy-profile
smoke still matches the reference (`0x110E7DC5` on both engines). The work-accounting
change is covered by
`PackedLine_CountsDrawnPixelsForOperaBusy`,
`PackedRepeatLine_CountsEveryRetainedPixelForOperaBusy`, and
`LiteralLine_CountsDrawnPixelsForOperaBusy`.

A six-title, 3,000-frame, accuracy-profile differential on 2026-11-04 completed both
engines with no capture errors; all six still diverged at the final checkpoint. The
accounting fix changes 5DO's CEL timing but does not by itself close the remaining
scheduling/render gap, so the Opera busy mode stays opt-in.

| Title | 5DO frame hash | Opera frame hash |
|---|---|---|
| Road Rash | `0xACFEC90C` | `0x3F570FC0` |
| Gex | `0xD92DBD3C` | `0xBED174BD` |
| AutoBahn Tokio | `0x04C4738C` | `0x32A050A8` |
| Johnny Bazookatone | `0x5796BF92` | `0xA29AD2D1` |
| Primal Rage | `0xCD9812F9` | `0x8E116314` |
| DinoPark Tycoon | `0x266F986F` | `0xB45E532B` |

### 1.1d CD-ROM cycle timing differential (2026-10-31)

The CORE-04 option was checked with `panafz1.bin`, fresh NVRAM, and the same discs.
The FreeDO-compatible default is unchanged. The Opera cycle model schedules each
2048-byte data sector at the stock 2x drive speed (81,246 ARM cycles at 12.5 MHz
after Opera's 167 us overhead compensation), holds the data poll clear until the
sector is ready, and publishes the read-data status after the transfer is drained.
The XBUS CD FIQ is raised when the delayed data or status becomes ready.

| Title | Frames | Mode | Final PC | Frame hash | Observation |
|---|---|---:|---|---|---|
| Lemmings | 3,000 | `--cd-timing freedo` | `0x0012B324` | `0x9C2E3236` | unchanged baseline |
| Lemmings | 3,000 | `--cd-timing opera` | `0x00059710` | `0xB1CE635A` | still loading; expected slower CD |
| Lemmings | 12,000 | `--cd-timing freedo` | `0x00128490` | `0xCE7E7A74` | title/menu |
| Lemmings | 12,000 | `--cd-timing opera` | `0x0012918C` | `0x110E7DC5` | boot reaches the intro later |
| Lemmings | 24,000 | `--cd-timing opera` | `0x0012DE58` | `0x9D4DBD37` | intro animation rendered |
| Road Rash | 12,000 | `--cd-timing freedo` | `0x00020640` | `0x2D05A53C` | intro credits |
| Road Rash | 12,000 | `--cd-timing opera` | `0x00020640` | `0x37D5CCFE` | intro credits at a different FMV point |
| Gex | 12,000 | `--cd-timing freedo` | `0x0008D3BC` | `0x9B1F45AE` | boot/menu |
| Gex | 12,000 | `--cd-timing opera` | `0x000820F8` | `0x259870E0` | boot/menu, later CD phase |

All nine runs completed and rendered. The mode is intentionally slower than the
immediate FreeDO path because it models the real 2x drive; the important result is
that the boot, OS, and FMV paths continue rather than stalling. This is a
three-title smoke differential, not the full 594-disc report required to change the
default. The five-title 3,000-frame default regression keys are unchanged from
section 1.1 (Alone `0x05225F76`, Lemmings `0x9C2E3236`, Road Rash `0x5E9EE515`,
Space Ace `0xD666228E`, Wolfenstein `0x9A3B5131`), confirming that the new option did
not alter the FreeDO path.

At 10,000 frames with input and a fresh NVRAM (pre-transposition-fix):
 
| Title | Final PC | Frame hash | Observation |
|---|---|---|---|
| Alone in the Dark | `0x0004B6B4` | `0x67FD3C6F` | In-game room/inventory scene |
| Wolfenstein 3D | `0x0001F014` | `0xA5A33C4D` | Gameplay (FLOOR 1-1) |
 
These two hashes are retained for historical comparison; the current first-pass
numbers are in section 1.2.

### 1.1e CD speed and timing-profile differential (2026-10-31)

The SYS-09 speed option was checked with `panafz1.bin`, fresh NVRAM, and 3,000 frames.
The FreeDO path is independent of `--cd-speed`: Lemmings with `freedo`/`X2`, `freedo`/`X1`,
and `freedo`/`Unbounded` all end at `PC 0x0012B324`, hash `0x9C2E3236`, matching section
1.1. The cycle-based path changes with the speed setting, and every run completed and
rendered:

| Title | Speed | Final PC | Frame hash | CD poll |
|---|---|---:|---|---:|
| Lemmings | `1x` | `0x0012B1C0` | `0x50A882FC` | `0x4B` |
| Lemmings | `2x` | `0x00059710` | `0xB1CE635A` | `0x49` |
| Lemmings | `4x` | `0x00012A08` | `0xB5A493B9` | `0x4B` |
| Lemmings | `8x` | `0x0012DE7C` | `0x6D9321A4` | `0x49` |
| Lemmings | `32x` | `0x0012A308` | `0x1FCCAB07` | `0x49` |
| Lemmings | `unbounded` | `0x0012B42C` | `0x1F4E236C` | `0x49` |
| Gex | `1x` | `0x00013F7C` | `0xB87FA1D5` | `0x0F` |
| Gex | `2x` | `0x0010FD60` | `0x496B0DFE` | `0x0F` |
| Gex | `4x` | `0x0002A7D4` | `0x2BF57DC4` | `0x0D` |
| Gex | `8x` | `0x0000E19C` | `0xCF03CA3B` | `0x0C` |
| Gex | `unbounded` | `0x0010F908` | `0x05F38B50` | `0x0C` |
| Road Rash | `1x` | `0x000B9D08` | `0x8733E5EB` | `0x0F` |
| Road Rash | `2x` | `0x0001276C` | `0x6F93310D` | `0x0C` |
| Road Rash | `4x` | `0x000B9D80` | `0xB2B1325E` | `0x0F` |
| Road Rash | `8x` | `0x000BA0E0` | `0xA64C80AC` | `0x48` |
| Road Rash | `unbounded` | `0x000B9C40` | `0x7EBB3876` | `0x48` |

The CD poll column is diagnostic only; the `0x40` media-access pulse is expected on the
idle READ_ID paths. This is a three-title smoke differential, not the full-collection
report required to change the default timing model. The `--profile accuracy` path was
also smoke-tested through the CLI: it reports `profile: Accuracy`, `armTiming:
Arm60Datasheet`, `clioTimers: OperaScheduler`, `celTiming: OperaBusy`,
`cdromTiming: OperaCycleBased`, and `cdSpeed: X2`; the 3,000-frame Lemmings run reaches
`PC 0x000109AC`, hash `0xF303CBB4`. An explicit `--arm-timing freedo` override reports
`profile: Custom` and the FreeDO timing value. The individual option tests and
state-mismatch tests are in `tests/FiveDo.Tests`.

### 1.1f BIOS manager and auto-selection (2026-10-31)

`bios scan bios` finds 8 primary BIOS images, 2 secondary images (the FZ-1 Kanji font
and the 512 KB arcade support ROM), and 0 errors. The common Panasonic FZ-1/FZ-10/
FZ-10E/FZ-10JA, GoldStar, Sanyo, patched arcade, Kanji font, and arcade support hashes
are recognized by MD5; unknown images are classified from the file name and marked
unverified.

The five-title 3,000-frame boot corpus was rerun with `--bios-auto bios`, a fresh
NVRAM, and the Release build. Automatic selection chose the FZ-10 for these US titles;
the hashes differ from the FZ-1 section 1.1 keys because the BIOS image differs, but
every run completed and the final frame was non-black.

| Title | Selected BIOS | Final PC | Frame hash |
|---|---|---|---|
| Alone in the Dark (USA) | Panasonic FZ-10 | `0x00021BEC` | `0x175B8FE4` |
| Lemmings (USA) | Panasonic FZ-10 | `0x0012DA2C` | `0x0C5B2F8E` |
| Road Rash (USA) | Panasonic FZ-10 | `0x000BA13C` | `0x6D23530B` |
| Space Ace (USA) | Panasonic FZ-10 | `0x0007A160` | `0xF55471B4` |
| Wolfenstein 3D (USA) | Panasonic FZ-10 | `0x00018FAC` | `0x175B8FE4` |

Known-title requirements were checked at 12,000 frames. These are the BIOS choices the
BIOS-aware triage in `Compatibility.md` 13.2 identified; each final frame contained
rendered content.

| Title | Selected BIOS | Final PC | Frame hash |
|---|---|---|---|
| Powers Kingdom (Europe) | Panasonic FZ-10E (Anvil) | `0x00010F3C` | `0xD5F7AF94` |
| Slayer (USA) | Panasonic FZ-10 | `0x000EC8B8` | `0xA50A108B` |
| Slayer (Europe) | Panasonic FZ-1 | `0x00020640` | `0xAB8BC516` |
| Crime Patrol (USA) (Arcade CD-ROM) | Panasonic FZ-10 (patched) | `0x00070D20` | `0x76C2D1D1` |

This is smoke evidence, not the full-collection BIOS-aware sweep. The configured
`BiosPath` remains the fallback and is still used when `BiosFolder` is empty, so
existing settings keep their exact BIOS.

### 1.1g Per-game NVRAM smoke (2026-10-31)

Two titles were run at 3,000 frames with `panafz1.bin`, a fresh temporary NVRAM root,
`--nvram-mode per-game --nvram-version 1`, and `--audio null`. Each run created its own
32,768-byte image and left the other title untouched:

| Title | Final PC | Frame hash | NVRAM file |
|---|---|---|---|
| 3D Atlas (USA) | `0x000189C4` | `0x1187598D` | `83d70d9c6a4a525a6774ae22ddb1fa23.1.ram` |
| Lemmings (USA) | `0x0012B324` | `0x9C2E3236` | `0be38b22987b40b5d48f70a167656049.1.ram` |

Lemmings' hash matches section 1.1, confirming that per-game storage does not change
emulation. `nvram list --dir <root> --json` reported both images with the correct game
IDs, modes, versions, and lengths. The store and CLI tests cover shared-to-per-game
migration, version separation, corrupt-primary backup, and import/export/reset; see
`tests/FiveDo.Tests/Host/NvramLayoutTests.cs`, `NvramStoreTests.cs`, and
`tests/FiveDo.Tests/Cli/CliProcessTests.cs`.

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

### 1.1g Differential harness (2026-10-31)

The first automated 5DO-vs-OperaCore run uses
`tools/FiveDo.DiffHarness` and `benchmarks/diff/smoke.json`:

- Title: Lemmings (USA), `panafz1.bin`, 300 frames.
- 5DO: `--profile accuracy --cd-speed unbounded --no-game-fixes`,
  checkpoint 300, frame hash `0x110E7DC5`.
- OperaCore: same disc data (cached ISO), same BIOS, 300 frames,
  checkpoint 300, frame hash `0x110E7DC5`.
- Result: **Match**. 5DO ran 300 frames in 1.3 s; OperaCore ran in 5.1 s.
- The two engines also produced the same final frame hash at 300 frames but
  diverged at frames 100 and 200: Opera reached the Lemmings boot screen earlier.
  That is a real scheduling difference, not a renderer difference. The smoke
  manifest now checkpoints only the matched frame; the early-frame divergence is
  retained in the run history and will be triaged with CORE-01/02/04 evidence.

The five-title `benchmarks/diff/showcase.json` run (3000 frames per title) is the
next evidence set. It compares frame hashes only; the reference core seeds its PRNG
from wall-clock time, so audio hashes are recorded but not part of the pass/fail
gate. See [`DifferentialHarness.md`](DifferentialHarness.md) for the manifest,
staging rules, allowlist policy, and limitations.

**First showcase result (2026-10-31, 3,000 frames, checkpoint 3,000):**

| Title | 5DO | OperaCore | Result |
|---|---|---|---|
| Lemmings (USA) | `0x20C4E404` | `0xEBDB01CA` | Diverged |
| Gex (USA, Europe) | `0xC8A2B60F` | `0xBED174BD` | Diverged |
| Road Rash (USA) | `0x6D066B83` | `0x0528251F` | Diverged |
| Wolfenstein 3D (USA) | `0x175B8FE4` | `0x175B8FE4` | **Match** |
| Alone in the Dark (USA) | `0x175B8FE4` | `0x175B8FE4` | **Match** |

The two matches are exact pixel matches at the same frame. The three divergences
are at game-code PCs (`0x0012DE84`, `0x0011005C`, `0x000B9FAC`) with the same CD
status class, so they are candidates for the CORE-01/02/04 timing differences, not
a failure to load. The next pass adds `frameWindow: 2` diagnostics and checkpoint
images so the report can distinguish "same pixels one frame apart" from a real
pixel/state divergence.

**Strict PC gate (debug core, same captures):**

| Title | Video | PC delta (Opera - 5DO) |
|---|---|---:|
| Lemmings (USA) | Diverged | -11,076 |
| Gex (USA, Europe) | Diverged | 176 |
| Road Rash (USA) | Diverged | 284 |
| Wolfenstein 3D (USA) | **Match** | -184,696 |
| Alone in the Dark (USA) | **Match** | 739,184 |

With `--compare-pc`, every title is a divergence: Wolfenstein and Alone render
identical pixels at frame 3000 while executing completely different code paths.
This is the central lesson for CORE-16: a frame-hash match is necessary but not
sufficient. Use the video gate for renderer/CLUT regressions and the PC gate (with
the debug core) for instruction-stream timing.

**Checkpoint images (second pass, 2026-10-31):** the harness now writes
`f003000.png` for 5DO and `f003000.bmp` for Opera. The images confirm that the
divergences are state/animation differences, not palette or scaling bugs:

- Lemmings: the same intro bedroom scene, but the character's pose differs (6,160
  of 76,800 sampled pixels differ); the difference is stable across Opera frames
  2998-3000, so it is not a simple ±1/2-frame offset.
- Gex: a different camera/character pose in the same opening scene.
- The ±2-frame `frameWindow` did not find an exact hash match for either title,
  so the harness correctly reports `Diverged`, not `Offset`.

The next triage step is to compare the 5DO CEL/CD/CLIO traces at frame 3000 with
the Opera reference and determine whether the divergence starts in the CD load
handshake, CEL busy timing, or the ARM/timer scheduler.

### 1.1h SYS-15 input devices (2026-11-01)

Lemmings (USA), `panafz1.bin`, 300 frames:

| Configuration | Frame hash | PBUS packet (first frame) |
|---|---|---|
| Legacy all-joypad | `0x110E7DC5` (286,162,373) | `00480080000000000000000000800000` |
| `--pbus-format opera` | `0x110E7DC5` (286,162,373) | `8000FFFFFFFF...` |
| `--input-device 1=mouse` | `0x110E7DC5` (286,162,373) | `49000000FFFFFFFF...` |

The chain and legacy layouts render the same boot frame. Exact encoder tests cover
the joypad, flightstick (`01 7B 08` signature), mouse (`0x49`), lightgun (`0x4D`,
20-bit scan counter and `0x1F` hit count), arcade lightgun, Orbatak trackball (`0x49`),
and Orbatak buttons (`0xC0`), including `0xFF` padding and mixed-device port order.

Representative arcade titles, 3,000 frames, `panafz10-patched.bin`:

| Title | Device | Frame hash | PC | Image |
|---|---|---|---:|---|
| Crime Patrol (USA, Arcade CD-ROM) | `arcade-lightgun` | `0xFEC54535` (4,274,341,173) | `0x0008F9E0` | Non-black, differs from joypad |
| Crime Patrol (USA, Arcade CD-ROM) | joypad | `0x0430CFAD` (70,307,757) | `0x0008F744` | Reference |
| Mad Dog II (USA, Arcade CD-ROM) | `arcade-lightgun` | `0xC5F4A77C` (3,321,145,212) | `0x00090554` | Non-black |
| Orbatak (USA, Arcade CD-ROM) | `orbatak-trackball` | `0x9BCA1E9E` (2,613,714,590) | `0x000A7428` | Non-black, differs from joypad |
| Orbatak (USA, Arcade CD-ROM) | joypad | `0x322D23AB` (841,819,051) | `0x00083570` | Reference |
| Shootout at Old Tucson (USA, RE3) | `arcade-lightgun` | `0x9F199B5A` (2,669,255,514) | `0x00020600` | Non-black |

The different hashes and PCs between the joypad and peripheral runs show that the
games detect the selected device. These are boot/title checks, not scripted
playthroughs; calibration and gameplay input remain.

**Optional debug-core fields (2026-10-31):** a locally built OperaCore with the
read-only `sg_debug_*` exports records the reference PC, ARM state hash, and CD
FIFO status. On the Lemmings 300-frame smoke the frame hash matches while the PC
delta is 4 bytes (`0x000205F8` vs `0x000205FC`). At frame 3000 the delta is
`0x2B44` (11,076 bytes): 5DO `0x0012DE84`, Opera `0x0012B340`. That is a real
instruction-stream divergence, not a renderer bug. The CD FIFO status/poll values
are reference-internal and are captured for comparison but are not the same
fields as 5DO's `DriveStatus`/`Poll`; do not compare them directly.

**Opera parity batch 1 (2026-10-31):** the first source-level parity batch
(VDLP next/force-line, ARM immediate rotate carry, DSP CPU FIFO, MADAM
span/MARIA/BPP0/SKIPX/blue-LSB/REGCTL/PIXC, CLIO FIFO) was validated with the
full unit suite and the differential harness:

- `dotnet test 5DO.sln`: **1,140 passed**, 0 failed.
- Smoke: Lemmings (USA), 300 frames, checkpoint 300: **PASS**, 1 match,
  0 diverged, 5DO `0x110E7DC5` = Opera `0x110E7DC5`.
- Showcase: 3,000 frames, checkpoint 3,000: unchanged from the pre-batch
  baseline; Wolfenstein and Alone still match, Lemmings/Gex/Road Rash still
  diverge, and the 5DO hashes are identical to the pre-batch values.
- VDLP save states are now version 3; v1 and v2 states migrate to the
  head/successor cursor model.

**OP-14 retirement (2026-10-31):** the automatic `GraphicsStepY` (Samurai
Shodown) and `BitTiming6` (Alone in the Dark) mappings were removed. The enum
values remain loadable for old states, but they are no-ops. The literal-CEL
path now always reads the actual source row.

- Samurai Shodown (USA/EU): frames 3,000/4,000/5,000/6,000 are identical with
  and without `GraphicsStepY`.
- Alone in the Dark (USA): with `BitTiming6` disabled, the credits use the
  actual row and no longer stretch vertically. A 9,000-frame harness probe
  still diverges from Opera for unrelated CD/timing reasons.
- DinoPark Tycoon (USA): the 3,000-frame image renders correctly with the
  generic XYPOSH/XYPOSL origin model and its existing `BitTiming3` scale fix.
- VDLP now keeps the retained head separate from the active successor cursor
  and processes a zero-persist successor on the same line. The Opera
  scanline-5 boundary is deferred until OP-SYS-05 aligns 5DO's visible start
  line (16) with Opera's (21); the line-0 cadence keeps Lemmings at
  `0x110E7DC5`.

**OP-SYS-02 CD poll/local reset (2026-10-31):** the accuracy timing mode now uses
Opera's poll semantics. `POLREMASK` (`0x08`) performs a local reset that clears the
command/transfer FIFOs and sector position; `POLRE` (`0x80`) is a write-one-to-clear
media-access latch; status/data/command access is gated during reset; eject reports
`READY` without the FreeDO phantom disc bits; and a loaded state with reset asserted
runs the same reset. CD-DA read-ahead and the READ_ID heuristic remain for SYS-01.

- `dotnet test 5DO.sln`: **1,143 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; all 5DO hashes identical to the pre-batch baseline.

**OP-SYS-01 ROM overlay (2026-10-31):** the memory bus now starts with the BIOS
overlaid at address zero. Low reads return the active ROM bank; the first CPU write
to DRAM clears the overlay; VRAM writes do not; reset reasserts it; and memory state
version 2 saves/migrates the flag (v1 states load as post-boot). DIPIR latches,
access-abort rules, and XBUS media access remain.

- `dotnet test 5DO.sln`: **1,147 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; all 5DO hashes identical to the pre-batch baseline.

**OP-SYS-01 DIPIR/XBUS (2026-10-31):** CLIO now has DIPIR1/DIPIR2 latches, the
before-reset conversion, the DIPIR FIQ, and XBUS access-abort rules. The XBUS
selected-device address is available to CLIO, Opera mode stops masking the poll
high nibble, and poll/command writes can latch media access when the CD reports
`POLRE`. The DIPIR/abort path is opt-in (`EmulatorTuning.OperaDipir`) because
enabling it before OP-SYS-03's soft-reset/clear-DIPIR path changes the Lemmings
boot; the default and accuracy profiles leave it off.

- `dotnet test 5DO.sln`: **1,154 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; all 5DO hashes identical to the pre-batch baseline.

**OP-SYS-03 CSTAT soft reset (2026-10-31):** CLIO now implements the CSTAT reset
bits, clear-on-zero bits, DIPIR-reset bit, and a soft-reset callback. The machine
handler performs an ARM soft reset (SPSR_svc/R14_svc preserved, PC=0, ROM overlay
reasserted), MADAM/DSP soft resets, and optional XBUS controller/DIPIR clearing. The
full path is opt-in (`EmulatorTuning.OperaSoftReset`) because enabling it before a
boot transcript changes the Lemmings path; the register and ARM semantics are covered
by unit tests.

- `dotnet test 5DO.sln`: **1,157 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: unchanged; all 5DO hashes identical to the pre-batch baseline.

**OP-SYS-06 PRNG split (2026-10-31):** CLIO now has its own splitmix32 generator for
`0x17F0` instead of borrowing the DSP LCG. The state is seeded from
`EmulatorTuning.RandomSeed` when provided, saved in CLIO state version 3, and migrated
from v1/v2. The DSP keeps its separate FreeDO-compatible LCG state.

- `dotnet test 5DO.sln`: **1,159 passed**, 0 failed.
- Differential smoke (Lemmings, 300 frames): **PASS**, `0x110E7DC5`.
- Five-title showcase: 2 matches (Wolfenstein, Alone); Gex's hash changed because it
  reads `0x17F0`, and its 3,000-frame image still shows a valid game scene. The other
  5DO hashes are unchanged.

**OP-SYS-10 CHD-zstd (2026-10-31):** CHDSharp 1.6.0 already supports Zstd
(`ChdCodec.Zstd`/`Cdzstd`) and ships `VendoredZSTD.dll`. Opening all 589 local CHDs
succeeded; they use CDLZMA/CDZLIB/CDFLAC, so no zstd fixture is present. No native
dependency or code change is required; add a zstd CHD fixture when one is available.

**40-title differential sample (2026-10-31):** `benchmarks/diff/sample40.json` runs
the first 40 titles from the full manifest at 300 frames with the accuracy profile,
no game fixes, unbounded CD speed, and no images. Result: **18 match, 22 diverged,
0 error**; every 5DO run completed. This is the first broad post-parity baseline.
The full 594-title collection remains runner-bound.

The next step is to capture a CLIO CSTAT/DIPIR boot transcript, compare the
`OperaDipir`/`OperaSoftReset` path against Opera, and then enable it in the accuracy
profile.

**Opera-parity title probes (2026-10-31):** 5DO completed 3,000 frames for
Samurai Shodown (USA/EU), DinoPark Tycoon, Alone in the Dark, Johnny Bazookatone,
AutoBahn Tokio, and Primal Rage with the accuracy profile. The stock OperaCore
crashed on Samurai Shodown (`0xC0000005`) inside the harness; the other probes
diverged at frame 3000, consistent with the existing PC/timing gap. A Johnny
Bazookatone checkpoint image shows the game scene without the issue #291
horizontal-line artifact.

### 1.1h Rewind and fast-forward (2026-11-01)

Rewind and fast-forward are host features and do not change emulation output. The
evidence is the unit/integration suite plus the state-equality test:

| Test | Result |
|---|---|
| `RewindBufferTests.RewindTo_ReplaysInputAndRestoresTheExactMachineState` | PASS: a 90-frame run rewound to frame 37 produces a byte-identical save state to a fresh control run to frame 37. |
| `RewindBufferTests.RewindTo_ClampsToTheOldestRetainedFrame` | PASS: targets before the oldest keyframe clamp to frame 0. |
| `RewindBufferTests.Capture_ResetsHistoryWhenTheFrameCounterJumps` | PASS: a timeline jump clears stale keyframes. |
| `RewindBufferTests.MemoryLimit_KeepsAtLeastOneKeyframe` | PASS: a one-byte cap retains one keyframe. |
| `CoreRunnerTests.Rewind_RestoresAnEarlierFrameAndUpdatesTheRunnerCounter` | PASS: the runner and machine frame counters agree after rewind. |
| `CoreRunnerTests.SpeedMultiplier_RunsEveryEmulatedFrameAndMutesAudio` | PASS: 4x runs every emulated frame, pushes no audio samples, and resets the audio queue. |
| `FrameSkipStateTests.SkipFrame_OnlyChangesThePresentedFrameBuffer` | PASS: 60 frames with three of four rendered produce identical CPU, memory, MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD, Quartz, and options sections; only the Machine frame section differs. |
| `GameDatabaseTests.AddMissingDefaultHotkeys_PreservesCustomBindings` | PASS: older profiles gain the `Rewind`/`FastForward` defaults without overwriting a custom save key. |
| `HotkeyRouterTests.Resolve_ReturnsEveryDefaultHotkey` | PASS: `R` and `Tab` resolve to `Rewind` and `FastForward`. |

Command:

```powershell
dotnet test 5DO.sln -c Release --filter "FullyQualifiedName~RewindBufferTests|FullyQualifiedName~CoreRunnerTests|FullyQualifiedName~FrameSkipStateTests|FullyQualifiedName~HotkeyRouterTests|FullyQualifiedName~GameDatabaseTests"
```

The default timing profile and golden boot hashes are unchanged because the core
machine is untouched; the rewind buffer only observes and restores complete states.
The Lemmings (USA) 300-frame CLI smoke still reports `0x110E7DC5`:

```powershell
dotnet run -c Release --project src/FiveDo.Cli -- run `
  artifacts/diff/work/discs/lemmings-usa-smoke/lemmings-usa-smoke.iso `
  --bios bios/panafz1.bin --frames 300 --json
```

### 1.1i Disc browser and disc info (2026-11-01)

The disc browser is a read-only host model; it does not start the machine. Unit tests
cover synthetic Opera images (tree order/depth, metadata totals, file/directory/all
extraction, name sanitization, a real ISO file, and missing images). A manual probe
against the local Lemmings CHD confirmed real CHD and database metadata:

| Field | Value |
|---|---|
| Source | `artifacts/chdmaker/lem.chd` |
| Format | CHD |
| Volume label | CD-ROM |
| Game ID | `0be38b22987b40b5d48f70a167656049` |
| Sectors | 206,126 |
| Contents | 461 files, 20 directories, 374,239,333 bytes |
| Database | Lemmings, US, Psygnosis, 1993 |
| Automatic fixes | None |
| Extraction | One selected file (181 bytes) written and removed |

Command:

```powershell
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release --filter "FullyQualifiedName~DiscBrowserTests"
```

### 1.1j Multi-track, CD-DA, and volume (2026-11-01)

SYS-01/AUD-05 are covered by synthetic fixtures until a known CD-DA title is
validated:

- `CueSheet_ExposesMultiTrackTableOfContentsAndAudioSectors` builds one Mode 1 data
  track and one audio track, checks the logical start LBAs and native sector sizes,
  reads data through `ReadSector`, reads 2352-byte audio through `ReadTrackSector`, and
  verifies that an audio LBA has no 2048-byte user data.
- `ChdImage_ExposesAudioTracks` encodes the same layout to CHD and checks the CHD track
  table plus byte-swapped audio samples.
- `DiscInfoReportsEveryTrackAndTheLeadOut`, `ModeSetToDigitalAudioReadsRawAudioSectors`,
  `PlayAudioTrackMovesTheSubQPositionAndStopsAtTheEnd`, and
  `ModeSetRejectsAnOversizedBlock` cover the CD-ROM command path.
- `PlayAudioTrackMovesTheSubQPositionAndStopsAtTheEnd` also verifies the first CD-DA
  sample is decoded little-endian from the audio sector.
- `DiscTracks_ReportsAnIsoImage` also checks the `--json` track table.
- `CdAudioMixTests` verifies that a full-volume CD-DA stream changes the packed DSP
  output, that mute produces the same output as zero volume, and that volume/mute
  roundtrip through the CD save-state section.
- `AppSettingsStoreTests` covers FND-02 legacy migration, malformed-file backup,
  version roundtrip, invalid-value normalization, per-game profile roundtrip/apply, and
  `Inspect`/`Migrate`; the CLI tests cover `settings validate|migrate`.

The full suite is **1,381 tests** in Debug. A Release 10-disc smoke after the
track/TOC change (`tools/compat-sweep.ps1 -Limit 10 -Frames 3000`) reported
**10/10 Loadable/Rendered**, so the data-track path is unchanged. A real CD-DA title
and a 10-second DSP+CD-DA WAV capture are still required for SYS-01 completion.

### 1.1k Multi-disc playlist and live swap (2026-11-01)

SYS-02 coverage is synthetic and command-level:

- `DiscPlaylistTests` loads an extended M3U with relative paths and `#EXTINF` titles,
  checks wrapping next/previous selection, and rejects an entry-less playlist.
- `DiscPlaylist_ListsEntriesInOrder` checks the CLI text/JSON listing.
- `EjectAndInjectCycleTheVirtualTray` checks guest eject (`0x06`), inject (`0x07`), the
  Opera tray/disc/spin/ready bits, and the media-access poll.
- `Run_SwapsDiscsAtTheRequestedFrame` runs the CLI with `--swap-at 0 --swap-disc` and
  checks the swap log and successful result.
- A manual Release run also swapped from `Road Rash (USA).chd` to `Space Ace (USA).chd`
  at frame 0; the run completed and reported the new disc hash. A known multi-disc title
  roundtrip remains.

### 1.1l Portable mode (2026-11-01)

UX-15 coverage is path-resolution level:

- `PortablePathsTests` verifies that portable mode resolves to the application
  directory, non-portable mode resolves to `<LocalApplicationData>/5DO`, and an explicit
  `FIVEDO_DATA_DIR` override wins.
- A manual CLI run of `settings validate --portable --json` reported
  `<CLI output directory>/settings.json`, proving the flag is stripped before command
  dispatch and the default path follows the portable directory.
- Named profiles and a per-instance write lock remain.

### 1.1m Input display (2026-11-01)

UX-23 coverage is compile/status level:

- `CoreRunnerTests.Status_ReportsTheAudioDerivedTargetFrameRate` also asserts the new
  `SkippedFrames` and `Player1` fields are populated.
- `PbusDecoderTests` already cover the button formatting used by the overlay.
- The Avalonia overlay is build-validated. Lag detection counts frames without a PBUS
  DMA read (`CoreRunnerTests.LagFrames_...`, `MadamTests.PbusDma_...`), and the selected
  controller port is covered by `InputPlayer_RejectsPortsOutsideOneToSix`. A screenshot
  comparison remains optional future polish.

### 1.1n ODE eject and playlist editing (2026-11-01)

- `DiscPlaylistTests` covers create/add/remove/reorder/save with an extended-M3U
  roundtrip, empty-playlist rejection, and selection repair after removal.
- `CoreRunnerTests` proves a guest `0x06` raises `GuestDiscEjected` once when the tray
  opens and not again while it remains open; `CdromMultiTrackTests` verifies
  `TrayOpen`, `DiscPresent`, and the guest-eject counter across eject/inject.
- `SaveStateTests` saves and reloads after a disc swap and confirms the second disc's
  drive state and hash metadata survive.
- Manual headless evidence: an M3U for `D (USA, Europe)` disc 1/disc 2 was swapped at
  frame 300 in a 600-frame `run`; the disc hash changed to
  `a52a01c7b6d2c8740651ac952b291c0a`, 38.4 M instructions executed, and the run
  completed. This is a timed swap, not yet a game-requested swap.

### 1.1o PAL/NTSC region modes (2026-11-01)

- `VideoRegionTests` verifies NTSC/PAL1/PAL2 geometry and clocks, PAL Quartz wrapping,
  VDLP start-line mapping, 250,000-cycle PAL fields with 288 visible lines, PAL2 width,
  and the state region warning.
- `Battle Chess (Europe)` with `panafz10e-anvil.bin`: 600 frames, `region=Pal1`,
  `fieldRate=50`, `source=320x288`, frame hash `0x88883E88`. The captured PNG shows the
  3DO logo at the correct PAL size.
- `--bios-auto bios --region auto` selected `Panasonic FZ-10E (Anvil)` and PAL1.
- `FiveDoMachine.SetVideoRegion` switches NTSC/PAL1 between frames; `VideoRegionTests`
  verifies the 250,000-cycle PAL field and the return to the 208,333-cycle NTSC field.
- A 300-frame `--audio wav` run produced 264,609 samples = 6.0002 seconds at 44.1 kHz,
  confirming the 50 Hz audio/frame cadence.
- Full NTSC suite: 1,473 tests pass with no changed NTSC hashes.

### 1.1p Region overrides, instance lock, and CD transcripts (2026-11-01)

- `AppSettingsStoreTests.RegionOverrides_*` verifies that a per-game override wins over
  both the automatic and global region and persists through the settings file.
- `VideoRegionTests.StateSwitchesTheVideoRegionWhenTheStateDiffers` saves under PAL1,
  loads into an NTSC machine, and checks the switch, warning, 250,000-cycle field, and
  288-line output.
- `InstanceLockTests` acquires the lock twice, checks conflict ownership data, recovers
  after disposal, creates a missing directory, and reports an unwritable destination.
- `CdromTranscriptTests` covers command/status/poll capture, eject/inject, reset replay,
  and detach/clear. `CdromDeviceTests.UnsupportedCommandsPublishAnIllegalRequestStatus`
  checks the `0x17` READ ERROR payload, and `ResetCommandClearsTheTransfer...` restores
  the 2048-byte sector size after MODE SET.
- `PrefetchingDiscSourceTests.ReadTrackSector_CachesAndPrefetchesAudioSectors` proves a
  following audio sector comes from the native-sector cache without a second inner read.
- Manual evidence: `Battle Chess (Europe)` with `panafz10e-anvil.bin`, PAL1, 300 frames,
  `--cd-transcript artifacts/cd-transcript.txt` produced 393 entries. The first entries
  are READ_ID, READ_ERROR, MODE_SET, READ_DISC_INFO, READ_TOC, and READ_DATA; the frame
  hash `0x88883E88` is unchanged from the pre-transcript run.

### 1.1q Audio device and buffer latency (2026-11-01)

- `AudioBufferConfigurationTests` checks clamping at 1/20/100/500/10,000 ms, monotonic
  queue growth, and that a 100 ms target yields an 80-120 ms queued window.
- `AppSettingsStoreTests.AudioDeviceAndBuffer_RoundtripAndClamp` covers device-name
  trimming, buffer persistence, and 20/500 ms clamping.
- Manual evidence: `5do audio devices --json` listed
  `OpenAL Soft on Headphones (JBL DUET NC)` and
  `OpenAL Soft on PG34WQ15R2B (NVIDIA High Definition Audio)`. A 60-frame PAL
  Battle Chess run with `--audio openal --audio-buffer 20 --audio-device
  "OpenAL Soft on Headphones (JBL DUET NC)"` completed with `ok: true`, 52,921
  samples, and frame hash `0x88883E88`.

### 1.1r Diagnostics bundle (2026-11-01)

- `DiagnosticsBundleTests.Create_WritesScrubbedSettingsLogsAndManifest` builds a
  settings file and log containing the home path and account name, then verifies the
  zip entries, removed settings keys, placeholder scrubbing, and manifest fields.
- `Create_WorksWithoutOptionalEvidence` covers a minimal bundle.
- Manual evidence: `5do diagnostics bundle --out artifacts/diag-test.zip --json` wrote
  `environment.txt`, sanitized `settings.json`, five logs, and `manifest.json` with 22
  scrub replacements; the archive contained no `DiscPath` or raw user path.

### 1.1s Per-game input devices (2026-11-01)

- `AppSettingsStoreTests.InputDeviceOverrides_WinOverGlobalAndSurviveRoundtrip` checks
  override precedence, invalid-device normalization, settings roundtrip, and clearing the
  override back to the global list.
- `AutoInputDevices_SeedKnownLightgunAndTrackballTitles` verifies Crime Patrol and
  Orbatak seeding, idempotence, unknown titles, and the `AutoInputDevices` switch.
- The local `Crime Patrol (USA)` and `Orbatak (USA)` names match the prefix table; the
  auto override is written on first disc open and shown in the OSD.

### 1.1t Save-state format v3 (2026-11-01)

- `SaveState_WritesVersionThreeFeatureFlagsAndSectionTable` checks the v3 version, all
  twelve sections, and the Options/Machine entries.
- `LoadState_AcceptsVersionTwoStates` and `LoadState_AcceptsVersionOneStates` rewrite a
  v3 header as v2/v1 through `StateTestHelper.DowngradeToVersion` and confirm a
  deterministic load.
- `LoadState_UnknownSectionPolicyWarnsOrRejects` appends a declared unknown section and
  checks both `Warn` and `Reject`.
- `StateInspectorTests` covers v1, v2, and v3 headers, including the uncompressed v1
  path; `FrameSkipStateTests` still proves skip presentation changes only the Machine
  section.

### 1.1u State diff (2026-11-01)

- `StateDiffTests.Compare_IdenticalStatesReportsNoChanges` and
  `Compare_DifferentFramesReportsChangedSectionsAndOffsets` cover equal and diverging
  states, CPU hash/offset changes, and changed-section counts.
- `Compare_ReportsAnExtraSectionInOnlyOneState` appends a declared unknown section and
  checks the `only in b` shape.
- Manual evidence: Battle Chess (Europe) PAL1 states at frames 100 and 120 produced a
  CPU first difference at byte 4 (39 differing bytes) and a Memory first difference at
  byte 24,603 (4,184 differing bytes); both report format v3 and the same disc hash.

### 1.1v Screenshot gallery (2026-11-01)

- `ScreenshotLibraryTests.Parse_ReadsGameStampFrameAndNativeSuffix` covers a normal and
  native companion name.
- `List_SortsNewestFirstAndKeepsUnparsedNames` verifies metadata ordering and fallback for
  a manually named PNG.
- `Delete_RemovesTheFileAndListHandlesMissingDirectories` covers delete and an absent
  directory.
- The gallery itself is build-validated; thumbnail decoding uses
  `Bitmap.DecodeToWidth` and unreadable PNGs fall back to a blank tile.

### 1.1w GIF recording (2026-11-01)

- `GifRecorderTests` covers GIF89a header/trailer, 60-to-30 frame decimation, target-rate
  clamping, frame limits, size-change skipping, and an empty GIF.
- Manual evidence: a 40-frame PAL Battle Chess run with
  `--record-gif artifacts/test-recording.gif --record-fps 25` produced 20 frames at
  320x288. Pillow loaded every frame and the extracted pixel at (160,120) was a valid
  RGB color.
- The encoder uses a 3-3-2 global palette and a NETSCAPE2.0 loop extension; LZW output
  was validated against Pillow after the code-size transition was corrected.

### 1.1x Desktop WAV recording (2026-11-01)

- `TeeAudioSinkTests` verifies sample forwarding to both destinations, reset forwarding,
  and null-destination safety.
- The desktop path reuses `WavAudioSink`, whose WAV header/data are already covered by
  `AudioAnalyzerTests` and `AudioPipelineTests`.
- Manual smoke: start **File > Record Audio...**, run a game, stop, and analyze the WAV
  with `5do audio analyze` (same writer and sample clock as the CLI path).

### 1.1y Per-game profile editor (2026-11-01)

- `GameProfileJson_RoundtripsAndNormalizes` covers export/import and clamping of invalid
  clock/volume values.
- `CreateEffectiveForGame_AppliesProfileWithoutChangingLiveSettings` verifies the
  machine-construction copy and that global settings are untouched.
- `Run_GameProfileOption_AppliesTimingAndRegion` runs the CLI with a PAL1/Arm60/X8/1.5x
  profile and checks `region`, `armTiming`, `clioTimers`, `cdSpeed`, and `cpuClock` in
  `--json`.
- The desktop editor is build-validated; its Save/Import/Export paths use the same
  `GameProfileJson` and settings APIs.

### 1.1z Parser fuzzing (2026-11-01)

- `ParserFuzzTests.StateLoader_MutatedStatesFailWithTypedErrors` performs 80 seeded
  mutations and accepts only `InvalidStateException` (or a successful benign load).
- `IsoAndChdOpeners_RejectRandomHeadersWithoutCrashing` feeds 20 random 8 KB images to
  each container opener.
- `CueParser_MutatedTextNeverEscapesTypedErrors` mutates a valid CUE 80 times and
  requires successful parses to expose a non-negative sector count.
- The suite runs in normal `dotnet test` (~8 s) and is deterministic.

### 1.1aa Movie frame editor and real-state determinism (2026-11-01)

- `MovieEditorTests` covers per-button edits, six-controller replacement, insert/remove
  ordering and header counts, completion-evidence clearing, and range checks.
- Real save-state determinism after format v3: Battle Chess (Europe) PAL1 saved at frame
  300 with the disc attached and ran 300 more frames to hash `0x88883E88`, matching the
  uninterrupted 600-frame run. Gex saved at frame 300 and continued to `0x945B9A20`,
  also matching.
- The movie editor window is build-validated; its Load/Save path uses `MovieFile`, whose
  container roundtrip is covered by `MovieFileTests`.

### 1.1ab Game metadata editor (2026-11-01)

- `MetadataStore_UpsertReplacesByGameIdOrPathAndRemoveClearsIt` verifies case-insensitive
  game-ID replacement, path-key replacement, combined record coalescing, removal, and a
  second remove returning false.
- The editor window is build-validated; it uses the same catalog lookup and store calls.

### 1.1ac Profile selector (2026-11-01)

- `PortablePathsTests.DefaultProfile_IsStoredAndClearedInTheBaseDirectory` covers the
  normalized `default-profile.txt` roundtrip and removal.
- The Settings > Paths controls are build-validated; they use `AppSettingsStore` for
  create/import/export and `PortablePaths.SetDefaultProfile` for the next-launch choice.
- Existing portable, explicit-override, named-profile, and lock tests remain green.

### 1.1ad Disc integrity verifier (2026-11-01)

- `DiscVerifierTests` covers a clean disc and a stable hash, a mid-disc read failure that
  continues to the end, and a tracked data/audio disc using native sector sizes.
- Manual evidence: `disc verify` on Battle Chess (Europe) read 143,810/143,810 sectors,
  338,241,120 bytes, hash `0x9147e756840bc269`, with no errors.

### 1.1ae Extended-boot compatibility retry (2026-11-01)

- `tools/compat-sweep.ps1` gained `-Files` (explicit disc paths) and `-RetryFrames`
  (rerun a blank row at a deeper frame count and keep the rendered result).
- The 18 non-rendered rows from the 3,000-frame sweep were rerun:
  - All five former `Not Loadable` rows (`Crime Patrol`, `Mad Dog II`, Mirai Shounen
    Conan, `Orbatak`, `Shootout at Old Tucson RE3`) complete and render at 20,000 frames
    with `panafz10`.
  - Ishida Yoshio Kudan no Igo Seiha, both Konpeki no Kantai discs, all three Myst discs,
    Olympic Summer Games, and all three Slayer discs render at 20,000 frames.
  - Powers Kingdom (Europe) renders its live-action intro at 65,000 frames with
    `panafz10e-anvil` (3,404 colors, 86,442 non-black pixels).
  - WorldCup Super Stadium renders its "Story of the World Cup" screen with
    `panafz10j` plus the FZ-10JA Kanji font ROM as BIOS2 (2,425 colors at 20,000 frames).
  - Crash 'n Burn (Japan) remains black or on the Anvil boot screen at 65,000 frames
    with every local BIOS/font pairing.
- `BiosLibrary` now recognizes 1 MB Kanji font images by their 3DO volume-header
  signature and classifies them as secondary ROMs; the FZ-1J, FZ-1J (no RSA), FZ-10J,
  and FZ-1J Kanji font hashes are catalogued, and secondary selection prefers the
  matching FZ-1/FZ-10 family.
- Final loadability table: 594 discs, **593 Loadable/Rendered**, **1 Loadable/No video**
  (known boot failure), **0 Not Loadable**. Evidence is in `artifacts/compat-20k-10/`,
  `artifacts/compat-known/`, and the merged `artifacts/compat-full/summary-merged.csv`.
- No emulator regression: the full suite remains **1,473 tests**, build 0 warnings/0
  errors, format clean, inspectcode 0 results.

### 1.1af Overscan crop (2026-11-01)

- `CoreTypesTests.VdlFrame_CopyCroppedFromShiftsPixelsAndMetadata` verifies the pixel
  shift, palette/background/output-control/DMA metadata, and bounds checks.
- `AppSettingsStoreTests.Overscan_RoundtripsAndClamps` covers persistence and the
  0/32 px clamp.
- The desktop path applies the crop before OpenGL/Vulkan/software presentation and in
  `SaveNativeScreenshot`; it is presentation-only, so frame hashes and save states do
  not change. The full suite is **1,473 tests**.

### 1.1ai Screenshot gallery filters, compare, export, and sidecars (2026-11-02)

- `ScreenshotLibraryTests.List_FiltersByGameAndCaptureTime` covers case-insensitive
  game text, inclusive time bounds, native companions, and distinct game names.
- `ScreenshotLibraryTests.Export_CopiesToADestinationDirectory` covers directory
  creation, content copy, and a missing source.
- `ScreenshotMetadataTests` covers capture/save/load of renderer, filter, aspect,
  region, high-resolution, crop, border, profile, and CPU-clock fields; native-companion
  fallback; invalid JSON; and missing sidecars.
- The frontend `--smoke-test` constructs the updated gallery and the new compare window.
  **1,533 tests pass.**

### 1.1ah Per-edge overscan and void border (2026-11-02)

- `CropInsetsTests` covers uniform clamping, the 16-pixel visible-size rule, legacy
  `OverscanPixels` fallback, per-edge precedence, `SetOverscan` clearing the legacy
  value, and `#RRGGBB`/`#AARRGGBB` validation.
- `AppSettingsStoreTests.PerEdgeOverscanAndVoidBorder_RoundtripAndClamp` verifies the
  four edges, border 0-64 clamp, invalid-color fallback, and persistence.
- `AppSettingsStoreTests.GameProfile_FromSettingsCapturesPerGameOverrides` and
  `CreateEffectiveForGame_AppliesProfileWithoutChangingLiveSettings` verify per-game
  crop capture/application and live-settings isolation.
- The frontend `--smoke-test` constructs the updated Settings window. The crop is
  presentation-only; `CoreTypesTests.VdlFrame_CopyCroppedFromShiftsPixelsAndMetadata`
  keeps the frame-copy contract covered. **1,529 tests pass.**

### 1.1ag 4DO/Opera fix parity (2026-11-01)

- [`FixParity.md`](FixParity.md) maps every 4DO `FIX_BIT_*`: timing 1/2/3/5 are active;
  timing-6 (Alone in the Dark) and GraphicsStepY (Samurai Shodown) are retired in favor
  of the generic CEL origin/literal-row model.
- Local 3,000-frame evidence: DinoPark changes its title scene with the scale-Y fix
  (`0xA1F3E800` vs `0xF2607174`); Microcosm (`0x4E6A4651`) and Super Street Fighter II
  Turbo (`0x1FFBE3FA`) are unchanged with/without their bits; forcing timing-6 on Alone
  in the Dark (`0x175B8FE4`) or GraphicsStepY on Samurai Shodown (`0x86C04E50`) is
  byte-identical.
- `GameCompatibilityTests` covers every mapped ID, case-insensitive/full-MD5 lookups,
  unknown IDs, collisions, and explicit overrides. **1,473 tests pass.**

### 1.1ah Redump DAT matching (2026-11-01)

- `DiscHealthTests` parses a synthetic Redump DAT, checks hash normalization, matches a
  single track by MD5, reports a changed track as a mismatch, and matches two tracks.
- `disc verify artifacts/dat-test.iso --dat artifacts/dat-test.dat --json` returned
  `knownDump: true`, game `DAT Test Game`, CRC-32 `013C6AE4`, MD5
  `3224F6D33D1FB002DEAE06DD6BEBA153`, SHA-1
  `4EA34675DFFF1D66C21EC9052B387A3C82AAF7BE`. A one-byte change produced
  `knownDump: false` and exit 1.
- The full suite is **1,473 tests**.

### 1.1ai Full per-game profile (2026-11-01)

- `AppSettingsStoreTests.GameProfileJson_RoundtripsAndNormalizes` now covers BIOS MD5,
  fix mask, filter, high-resolution, six input devices, and NVRAM mode/version, including
  invalid-value normalization.
- `GameProfile_FromSettingsCapturesPerGameOverrides` verifies the profile snapshot
  captures the current per-game BIOS/fix/filter/input overrides and NVRAM choices.
- `CreateEffectiveForGame_AppliesProfileWithoutChangingLiveSettings` verifies the
  effective BIOS/fix/filter/high-res/input/NVRAM values and that the live settings and
  shared override maps are untouched (the clone is deep for the maps).
- `Run_GameProfileOption_AppliesTimingAndRegion` now includes `FixMode` and
  `HighResolution`; the CLI reports `fixMode: 16` and a 576-line PAL high-res frame.
- The full suite is **1,473 tests**.

### 1.1aj Interrupt, DMA, and DSP telemetry (2026-11-02)

The CORE-05/06/07 increment was checked with the Debug CLI, `panafz1.bin`, fresh NVRAM,
`--frames 3000 --audio null`, and the disc named in each row.

| Title | Frame hash | FIQ requests | FIQ entries | Matched | Unmatched | Coalesced | Mean latency | Max latency | DSP instructions | DSP ticks | Peak/tick | Mean/tick |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Gex (USA, Europe) | `0x05F38B50` | 21,183 | 14,345 | 13,855 | 490 | 7,328 | 7,429.8 | 859,234 | 88,955,550 | 2,205,094 | 106 | 51.7 |

The Gex frame hash matches the 3,000-frame key recorded in section 1.1b. A second
3,000-frame Gex run with `--clio-trace artifacts/clio-gex.txt` recorded **4,714** XBUS
DMA transfers, each with a paired `clio dma` start and `clio dma-done` line; no
`clio dma-blocked` line appears on the default FreeDO path, as expected.

Lemmings (USA) also booted 3,000 frames to the documented `0x9C2E3236` frame hash with
27,845 FIQ requests / 27,764 entries and a 257,935-cycle maximum latency. These runs are
smoke evidence only: the latency distribution still needs an Opera-side timeline before
CORE-07 can be marked complete.

### 1.1ak Profile coverage in bench and selftest (2026-11-04)

The CORE-08 increment closes the remaining CLI surfaces. `5do selftest --json` now runs
the profile-detection, determinism, non-empty frame, state round-trip, and empty-warning
checks for every named profile. The run passed **15/15 checks** on the Debug build:

| Profile | Checks | State round-trip |
|---|---|---:|
| `compatible` | 5/5 | PASS |
| `accuracy` | 5/5 | PASS |
| `performance` | 5/5 | PASS |

`5do bench --frames 600 --profile <name> --json` reports the derived profile and timing
modes. The synthetic program produces the same final frame hash (`0x6CDFBDC5`) in all
three profiles; the deterministic counters differ as expected:

| Profile | Profile reported | ARM timing | Cycles/frame | Instructions | CPU clock |
|---|---|---|---:|---:|---:|
| `--profile compatible` | `Compatible` | `FreeDoCompatible` | 208,336 | 127,084,960 | 12.5 MHz |
| `--profile accuracy` | `Accuracy` | `Arm60Datasheet` | 208,336 | 127,084,960 | 12.5 MHz |
| `--profile performance` | `Performance` | `FreeDoCompatible` | 312,512 | 190,632,320 | 18.75 MHz |

The measured frames-per-second values are host- and build-dependent and are not a gate;
use a Release build with `benchmarks/FiveDo.Benchmarks` for comparative performance.
The accuracy profile does not change this synthetic program's instruction count because
it does not poll CLIO timers or CEL status; the timing model is nevertheless active and
reported. The profile override in the differential harness is covered by
`MergeOptions_ProfileOverrideWinsOverDefaultsAndTitle`, and the CLI round-trip by
`Bench_ProfileOptionReportsTiming` and `SelfTest_CoversEveryNamedProfile`.

#### 1.1ak1 40-title Compatible vs Accuracy profile A/B (2026-11-04)

The CORE-08 profile A/B used the same 40-title, 300-frame staged-ISO sample and cached
Opera captures as the ARM timing A/B. Only `--profile` changed:

| Profile | Matched | Diverged | Errors |
|---|---:|---:|---:|
| `compatible` | 17 | 23 | 0 |
| `accuracy` | 17 | 23 | 0 |

Every title had the same status in both runs, so the Accuracy profile neither closed nor
regressed a title in this boot sample. The 594-title, 3,000-frame A/B is still required
before the profile can become the default, so Accuracy remains opt-in.

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
[`ImplementationPlan.md`](ImplementationPlan.md).

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
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --state-in artifacts/s2/lem-pass.state --frames 3 `
  --poke "0x96574=43414A4A4C444D424556" --frame-out artifacts/poke.png
```

`--autopilot road` is an experimental Road Rash driver. It holds B and steers
toward the gray road centre (target screen X 230). It follows the road through the
first section but does not avoid traffic or recover from a wreck yet.

```powershell
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --state-in artifacts/s2/rr8-30k.state --frames 10000 `
  --autopilot road --capture-dir artifacts/rr-auto --capture-every 1000 `
  --progress 1000 --json
```

The autopilot/steering tests are in
`tests/FiveDo.Tests/Host/RoadRashAutopilotTests.cs`; the RAM-patch parser tests
are in `tests/FiveDo.Tests/Host/MemoryPatchTests.cs`.

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
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
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
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Road Rash (USA).chd" --bios bios\panafz1.bin `
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
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
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
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "J:\Panasonic 3DO\Road Rash (USA).chd" --bios bios\panafz1.bin `
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
~737 samples/frame cadence in this table is the historical FreeDO 8.24 sample
clock; see 1.5a2 for the exact 44.1 kHz clock and new captures.

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

### 1.5a2 Exact DSP sample clock and live-audio validation (2026-10-27)

The DSP sample clock now accumulates the actual ARM cycle delta, producing exactly
44,100 samples per emulated second. This removes the FreeDO 8.24 over-count that
made audio ~0.7% sharp and kept the live ring overfilled.

| Check | Before | After | Reference |
|---|---:|---:|---|
| Lemmings, 600-frame boot samples | 444,097 | 441,015 | Opera 441,441 at 59.94 Hz |
| Lemmings, 600-frame boot duration | 10.070 s | 10.000 s | Opera 10.010 s |
| Road Rash, 6,000-frame WAV samples | 4,429,529 | 4,410,188 | 100.0 s at 44.1 kHz |
| Road Rash, spectral peak in 10-20 s | 442.1 Hz | 442.1 Hz | Opera 442.1 Hz |

The old and new Road Rash WAVs have the same peak (29,951), RMS within 0.2%, and
identical spectral peaks; only the sample count/timebase changed. Alone and
Lemmings 3,000-frame screenshots are pixel-identical before/after; Road Rash and
Space Ace show adjacent FMV frames; Wolfenstein differs only in the star field.

The Lemmings Fun 4 D-pad script still reaches gameplay at 23,000 frames with the
exact clock (`PC 0x00083FE8`, hash `0x63C02DED`, TIME 4:57); the old pre-clock key
`0xB7846C2D` is historical. The in-game timer differs by a few seconds because the
audio clock changed the frame/FMV timing.

600-frame WAV captures from live states with the exact clock:

| Title | State | Duration | Samples | Peak | RMS | Clipped | Silent |
|---|---|---:|---:|---:|---:|---:|---:|
| Wolfenstein 3D | `w3d_gameplay.state` (18,000) | 10.000 s | 441,020 | 18,100 | 2,168 | 0 | 8 |
| Lemmings | `lem-dpad-gameplay.state` (23,000) | 10.000 s | 441,020 | 8,192 | 1,661 | 0 | 27,903 |
| Gex | `gex-gameplay.state` (15,000) | 10.000 s | 441,019 | 12,964 | 2,089 | 0 | 6,923 |
| Theme Park | `tp-buttons.state` (15,000) | 10.000 s | 441,018 | 0 | 0 | 0 | 441,018 |
| Road Rash | `rr-t198-92k.state` (92,000) | 10.000 s | 441,019 | 32,768 | 11,012 | 2,036 | 0 |

Road Rash's race mix still reaches full scale and clips; that is the game's mix,
not the output path. Theme Park's captured menu is silent. `audio analyze <wav>
--json` prints the same peak/RMS/clipping/DC-offset statistics for any capture.
A 36,000-frame capture from `rr-t198-92k.state` produced 26,461,238 samples
(600.028 s at 44.1 kHz), only 0.005% above the nominal 36,000 x 735 samples; the
exact clock does not drift over ten minutes. That continuation reaches long static
wait sections (97.9% silent frames), so it is a cadence check rather than a
gameplay-audio sample.

**DSP start/stop.** Road Rash starts the DSP between 500 and 1,000 frames and keeps
it running through 6,000; Lemmings runs it from boot through 25,000. No title in the
current corpus was observed stopping the DSP mid-run. Resets and state loads call
`IAudioSink.Reset`, which drops the live ring/queue so a loaded timeline does not
replay old samples.

**Live audio.** `OpenAlAudioOutput.TryCreate` now brings the OpenAL context current
on the audio thread and the build/publish copies the bundled OpenAL Soft 1.23.1
runtime (`OpenAL32.dll` on Windows). A 300-frame real-time run with the OpenAL null
backend completed in 5.000 s with 220,510 samples (735.03/frame), 529 dropped
samples, 14 padded buffers, and ~89 ms output latency; the frame pacer followed the
44.1 kHz clock. The trimmed single-file CLI (win-x64 and linux-x64) and the opt-in
NativeAOT CLI both open the same backend; the Linux check used the published
`libopenal.so` and `ALSOFT_DRIVERS=null` under WSL. The opt-in
`FIVEDO_AUDIO_TESTS=1` xUnit test plays 1 s of a 440 Hz tone through the real
device.

**90-second Opera comparison (2026-10-28).** A 90 s Road Rash boot capture from
the exact-clock DSP (`artifacts/rr-90s-machine.wav`) was compared with a 95 s
WASAPI loopback of the Opera reference (`artifacts/loopback-opera-90.wav`). Both
are 44.1 kHz stereo and cover the same boot/attract sequence:

| Metric | 5DO DSP raw | Opera host |
|---|---:|---:|
| Duration | 90.0 s | 95.0 s |
| Peak | 0.914 | 0.914 |
| RMS | 0.0615 | 0.0644 |
| 20-200 Hz band | 52.7% | 50.4% |
| 200-2k Hz band | 30.4% | 33.4% |
| 2k-8k Hz band | 16.6% | 15.9% |
| 8k-20k Hz band | 0.3% | 0.3% |
| 400-500 Hz tone | 441.4 Hz | 441.4 Hz |

The log-power spectra are 0.9994 cosine-similar over 20 Hz-20 kHz. The 10 s RMS
envelopes correlate at r = 0.9685 after excluding the first window (the captures
start a few seconds apart in boot), and the per-window levels match from window 2
onward. This closes the loudness/spectrum comparison. The only remaining audio
check is a manual listen for clicks when a game stops/starts the DSP; no current
corpus title was observed stopping it mid-run.

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

The data track of each CHD was extracted to ISO with `5do disc dump`, then run with
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

The full method, reference hierarchy, and next actions are in
[`ReferenceStrategy.md`](ReferenceStrategy.md) and [`ImplementationPlan.md`](ImplementationPlan.md).

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
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 3000 `
  --kprint artifacts/kprint.txt --kprint-pc artifacts/kprint-pc.txt

# CD command/sector trace
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 3000 --cd-trace artifacts/cd.log

# CLIO timer/FIQ register trace
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 3000 --clio-trace artifacts/clio.log

# Instruction ring buffer around a stop condition
dotnet run --project src/FiveDo.Cli -- trace "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --trace-frames 600 --ring 8192 `
  --stop-kprint "Chunk XTRA" --out artifacts/trace.txt

# Low-memory write watchpoint (writer PC, value, size, registers, caller)
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 700 `
  --watch-lowmem artifacts/watch.txt --watch-from-frame 681 `
  --watch-start 0 --watch-end 0x100

# RAM and DSP memory dumps at the end of a run
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 3000 `
  --dump-ram artifacts/ram.bin --dump-ram-offset 0 --dump-ram-length 0x300000 `
  --dump-dsp artifacts/dsp

# DSP instruction ring at a KPRINT stop
dotnet run --project src/FiveDo.Cli -- trace "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 0 --trace-frames 3000 `
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
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 3000 `
  --input-script artifacts/input.txt --frame-out artifacts/input.png

# Script frames are absolute. With --state-in, either use the state's frame
# numbers (for example 91000) or add --script-base state to shift a script that
# starts at 0 by the loaded state's frame count.
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --state-in artifacts/live.state --frames 300 `
  --input-script artifacts/input.txt --script-base state --json

# Controller packet trace (one 16-byte PBUS packet per frame)
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 600 `
  --input-script artifacts/input.txt --pbus-trace artifacts/pbus.log

# CEL command/geometry trace
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" --frames 600 --cel-trace artifacts/cel.log

# Disassemble raw little-endian ARM words from a RAM dump
dotnet run --project src/FiveDo.Cli -- disasm artifacts/ram.bin `
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
dotnet run -c Release --project src/FiveDo.Cli -- run artifacts/s2/lem.iso `
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

**HQx 2x/3x/4x.** `VideoFilter.Hqx`, `Hqx3x`, and `Hqx4x` run Maxim
Stepin/Cameron Zemek's hq2x/hq3x/hq4x on the CPU-expanded BGRA frame, upload the
scaled result, and finish with sharp-bilinear. A 32x24 deterministic reference
image matches the C++ byte hashes `0xA400C421` (2x), `0x0C406F97` (3x), and
`0xBB17969C` (4x); a uniform image is unchanged. The reference's `RGBtoYUV` table
loop stops before the last entry, so pure white is deliberately excluded from the
reference image. A 320x240 synthetic benchmark measures about 2.3/4.2/7.5 ms per
frame for 2x/3x/4x on checkerboard content and about 12/13/15 ms on random noise,
so 3x/4x are opt-in and not recommended with high-res on slow CPUs.

### 1.7 Fifteenth pass: additional boot/render/audio corpus (2026-10-26)

Seven additional titles were booted with `panafz1.bin`, a fresh NVRAM, and 12,000
frames. Each rendered a real frame and produced a deterministic save state. The
frames are not committed; the hashes below are the regression keys.

| Title | File | Frames | Final PC | Frame hash | Observation | Audio (600 frames) |
|---|---|---:|---|---|---|---|
| Gex | `Gex (USA, Europe).chd` | 12,000 | `0x000876E8` | `0x6B8760C9` | 3D platform level with HUD | 10.03 s, peak 29733, RMS 3029/3145, 0 clipped |
| Need for Speed | `Road & Track Presents - The Need for Speed (USA, Europe).chd` | 12,000 | `0x0001055C` | `0x95730015` | Car FMV and menu | 10.02 s, peak 29951, RMS 5309/5022, 0 clipped |
| Super Street Fighter II Turbo | `Super Street Fighter II Turbo (USA).chd` | 12,000 | `0x00010548` | `0x110E7DC5` | Attract demo (title, character intro, CPU fight, ending) at 2,000-10,000; black transition at 12,000 | 10.10 s, peak 21248, RMS 1569/1502, 0 clipped |
| Total Eclipse | `Total Eclipse (USA).chd` | 12,000 | `0x000106A4` | `0x280E8346` | Spaceship intro/gameplay over terrain | 10.06 s, peak 16382, RMS 2760/2760 (mono duplicated), 0 clipped |
| Killing Time | `Killing Time (USA) (RE1).chd` | 12,000 | `0x000A4128` | `0xF23DE48C` | Title menu (`START`/`OPTIONS`/`LOAD GAME`) | 10.08 s, peak 32333, RMS 5609/6357, 0 clipped |
| Theme Park | `Theme Park (USA).chd` | 12,000 | `0x0002063C` | `0x55B0AC11` | Title/menu screen | 10.11 s, silent at this menu (peak 0), DSP running |
| Return Fire | `Return Fire (USA).chd` | 12,000 | `0x000284F4` | `0xD57E56D7` | Menu screen (controller/map panels) | 10.12 s, peak 29765, RMS 2600/2600 (mono duplicated), 0 clipped |

Commands:

```powershell
$g = $env:FIVEDO_GAMES
$b = "bios\panafz1.bin"
$t = "Gex (USA, Europe).chd"
dotnet run -c Release --project src/FiveDo.Cli -- run "$g\$t" --bios $b `
  --frames 12000 --frame-out artifacts\gex-12k.png `
  --state-out artifacts\gex-12k.state --json

# Save-state roundtrip: zero-frame load + two 120-frame continuations
dotnet run -c Release --project src/FiveDo.Cli -- run "$g\$t" --bios $b `
  --state-in artifacts\gex-12k.state --frames 0 --json
dotnet run -c Release --project src/FiveDo.Cli -- run "$g\$t" --bios $b `
  --state-in artifacts\gex-12k.state --frames 120 --json

# Audio capture from the 12,000-frame state
dotnet run -c Release --project src/FiveDo.Cli -- run "$g\$t" --bios $b `
  --state-in artifacts\gex-12k.state --frames 600 --audio wav `
  --wav-out artifacts\gex-12k.wav --json
```

Roundtrips (zero-frame load matches the saved hash; two independent 120-frame
continuations match each other):

| Title | Zero-frame hash | 120-frame PC / hash (twice) |
|---|---|---|
| Gex | `0x6B8760C9` | `PC 0x00079414`, `0xC682B436` |
| Need for Speed | `0x95730015` | `PC 0x0001281C`, `0xB1FB873D` |
| Super Street Fighter II Turbo | `0x110E7DC5` | `PC 0x00020648`, `0x110E7DC5` |
| Total Eclipse | `0x280E8346` | `PC 0x000205F4`, `0xDD148BD5` |
| Killing Time | `0xF23DE48C` | `PC 0x000284EC`, `0xF23DE48C` |
| Theme Park | `0x55B0AC11` | `PC 0x00019080`, `0x55B0AC11` |
| Return Fire | `0xD57E56D7` | `PC 0x0002E21C`, `0xD57E56D7` |

The 4DO game database applies fix mask `0x2` to Super Street Fighter II Turbo; the
other six titles use mask `0`. Theme Park's menu is silent; this is recorded as an
open audio question, not a pass for title music.

---

## 2. BIOS images

The local BIOS directory contains several variants. Only 1 MB images are accepted
by `BiosImage`; the 512 KB arcade image and the truncated Kanji image are not valid
inputs for the emulator.

| File | Size (bytes) | Intended use |
|---|---:|---|
| `panafz1.bin` | 1,048,576 | Primary NTSC test BIOS (MD5 `f47264dd47fe30f73ab3c010015c155b`) |
| `panafz10.bin` | 1,048,576 | Alternate NTSC BIOS |
| `panafz10-patched.bin` | 1,048,576 | Patched NTSC BIOS; required by the Arcade CD-ROM titles |
| `goldstar.bin` | 1,048,576 | GoldStar BIOS |
| `sanyotry.bin` | 1,048,576 | Sanyo BIOS |
| `panafz10e-anvil.bin` | 1,048,576 | European/PAL Anvil BIOS (MD5 `a48e6746bd7edec0f40cff078f0bb19f`); required by Battle Chess (Europe) |
| `panafz10e-anvil-patched.bin` | 1,048,576 | European/PAL Anvil BIOS with the RSA check disabled (MD5 `cf11bbb5a16d7af9875cca9de9a15e09`) |
| `panafz10ja-anvil-kanji.bin` | 1,048,576 | Japanese Anvil/Kanji BIOS |
| `panafz1-kanji.bin` | 933,636 | Not accepted (not 1 MB) |
| `3do_arcade_saot.bin` | 524,288 | Not accepted (arcade ROM, not a 1 MB BIOS) |
| `panafz10.bin.fstree` | 17,459 | Metadata, not a BIOS |

Use `panafz1.bin` for the default smoke test. Use the Anvil and GoldStar variants
only when validating compatibility-specific code paths. `panafz10e-anvil.bin` is the
European/PAL build and is the reference BIOS for European-only discs such as Battle
Chess (Europe), which rejects NTSC system software.

---

## 3. CLI commands

Set the environment variables used by the opt-in tests. `FIVEDO_GAMES` is the
corpus root and is different on each machine; everything else is derived from it.

```powershell
$env:FIVEDO_BIOS  = (Resolve-Path "bios\panafz1.bin")
$env:FIVEDO_GAMES = "J:\Panasonic 3DO"        # main collection
# $env:FIVEDO_MAME = "G:\MAME\MAME Software List CHDs\3do"
$env:FIVEDO_DISC  = "$env:FIVEDO_GAMES\Road Rash (USA).chd"
Get-ChildItem $env:FIVEDO_GAMES
```

Inspect a disc without booting it:

```powershell
dotnet run --project src/FiveDo.Cli -- info "$env:FIVEDO_DISC" --json
```

List or extract the 3DO (Opera FS) file system:

```powershell
dotnet run --project src/FiveDo.Cli -- fs list "$env:FIVEDO_DISC" --json
dotnet run --project src/FiveDo.Cli -- fs extract "$env:FIVEDO_DISC" `
  --out "$env:TEMP\fivedo-fs" --filter "Rash/"
```

`fs` uses the first-party `FiveDo.Storage.OperaFileSystem` parser and accepts ISO,
BIN, CUE+BIN, and CHD images; `info` and `disc dump` use the emulator's own readers.
The Lemmings ISO/CHD pair was cross-checked with 482 identical entries and 461
byte-identical extracted files.

Run a fixed number of frames and dump the last frame:

```powershell
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" `
  --frames 600 `
  --nvram artifacts/nvram.ram `
  --frame-out artifacts/road-rash-600.png
```

The CLI loads a fresh 32 KB NVRAM volume when `--nvram` is omitted. Pass `--nvram`
to keep the same save data across runs; games that check save-space (Alone in the
Dark) need a valid volume to get past their startup warning.

Capture audio for listening:

```powershell
dotnet run --project src/FiveDo.Cli -- run "$env:FIVEDO_DISC" `
  --bios "$env:FIVEDO_BIOS" `
  --frames 600 `
  --audio wav `
  --wav-out artifacts/road-rash-600.wav
```

Run the deterministic self-test, the synthetic benchmark, and the CHD/ISO
prefetch comparison:

```powershell
dotnet run --project src/FiveDo.Cli -- selftest
dotnet run --project src/FiveDo.Cli -- bench --frames 600
dotnet run --project benchmarks/FiveDo.Benchmarks -- --frames 600 `
  --bios bios/panafz1.bin --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" --json
dotnet run --project benchmarks/FiveDo.Benchmarks -- --frames 600 `
  --bios bios/panafz1.bin --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" --prefetch --json
```

Desktop validation:

```powershell
dotnet run --project src/FiveDo.Frontend.Avalonia
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

### 1.1af Library disc-health verification (2026-11-04)

- `DiscHealthCacheTests` covers the persistent cache roundtrip, malformed files, stale
  size/timestamp records, badge text for every status, summary counts, missing files,
  unreadable images, DAT matches and mismatches, and progress reporting.
- `CliLibraryVerifyTests` runs `library verify` in-process with a synthetic ISO and DAT:
  the JSON report says `verified`, the cache file is written, a corrupt image reports
  `ReadError` and exits 1, and an empty folder returns the documented error.
- Manual evidence: `5do library verify --folder "D:\Samples\3DO" --cache <temp>` read all
  five local CHDs (Alone in the Dark, Lemmings, Road Rash, Space Ace, Wolfenstein 3D;
  1.19 GB) in 91 seconds with 0 read errors and wrote a `NoDat` record for each; no DAT is
  available locally, so the DAT path is covered by the synthetic tests.

---

## 5. Rules

- Never commit BIOS ROMs, game images, extracted assets, screenshots containing
  copyrighted content, or WAV captures of copyrighted audio.
- Keep generated artifacts under `artifacts/`, which is gitignored.
- Do not add game-specific hacks without evidence; record the disc, BIOS, and
  observed behavior in `docs/Compatibility.md`.
- Real-content tests stay opt-in. CI must continue to pass with synthetic ROMs and
  discs when `FIVEDO_BIOS`/`FIVEDO_DISC` are unset.

#### 1.1al SYS-07 memory capacity configurations (2026-11-04)

`MemoryConfiguration` and `MemoryLayout` cover the eight Opera layouts (21, 22, 41, 42,
81, 82, E2, F1). `MemoryConfigurationTests` (34 cases) covers the derived geometry, the
MADAM RED SYSBITS values, all eight DRAM/VRAM windows, byte/halfword/word decoding at the
new boundaries, the stock 3 MB boundary, `TryParse`, CLI reporting, state round-trip, and
a legacy version-2 memory section.

- **Homebrew probe:** a synthetic ARM ROM writes `0x11`, `0x22`, `0x33`, and `0x44` to
  the top of DRAM (`0x003F0000`), the VRAM base (`0x00410000`), and the top of the second
  VRAM bank (`0x005F0000`), reads them back, and stores them at `0x1000`. It also records
  MADAM RED SYSBITS at `0x2004`. With `--memory 42` the results are the four values,
  marker `0xA5` at `0x2000`, and SYSBITS `0x32`.
- **State:** a 4 MB DRAM / 2 MB VRAM machine stores `0xDEADBEEF` at `0x00500000`; the
  state reloads that value and the layout into a stock machine, with the
  `State memory capacity ... switched to ...` warning.
- **Retail compatibility:** Lemmings (USA) boots unchanged with the stock layout
  (20,000-frame hash `0xE84356D0`). The 2 MB DRAM / 2 MB VRAM layout still runs and keeps
  the DSP active. Layouts with more than 2 MB of DRAM (4/1, 4/2, 8/1, 8/2) stop on the
  boot screen, which matches Opera's documentation that expanded memory is a homebrew
  option and can break retail software. The stock layout remains the default and its
  golden hashes are unchanged.

#### 1.1am SYS-15/SYS-16 eight-port PBUS (2026-11-04)

- `PbusEncoder.MaxPorts` is 8; `MaxControllers` remains 6 for the legacy 4DO packet.
  `InputDeviceSet`, `PbusSource`, `InputManager`, the chain encoder/decoder, settings,
  and the CLI accept ports 1-8. Ports 7-8, or any peripheral, select the Opera chain.
- `--active-devices 0-8` and Settings > Emulation > **Active input devices** cap the
  polled ports. Automatic (the default) keeps the historical one-joypad packet and grows
  to the highest configured peripheral port; `0` reports an empty chain.
- `Madam` now uses Opera's 256-byte PBUS buffer. The state section is version 4; v1-v3
  states read the historical 20-byte queue and fill the tail with `0xFF`.
- Tests: `EightPlayerPacket_ReachesTheMadamPbusQueue` sends eight joypads through
  `FiveDoMachine` and checks the eighth record at queue offset 14; `EightJoypads_UseTheOperaDaisyChain`
  checks the 20-byte chain packet; `ApplyInputDevices_ActiveCountControlsPolledPorts`
  covers 0, 3, and 8; `SaveState_RoundTripsTheExpandedPbusQueue` covers the 256-byte
  queue; `KeyPressed_PreservesAnEightDeviceChain` checks the tail fill.
- Hardware/test-ROM six-player and multi-tap verification remains; this is model-level
  packet and machine-path evidence, not a hardware capture.

#### 1.1an SYS-12 Portfolio OS SWI HLE (2026-11-04)

- `ArmSwiHle` ports Opera's 14 intercepted SWIs. `0x50003`, `0x50004`, `0x5000A`,
  `0x5000B`, and `0x5000D` return false so the normal guest trap runs.
- Tests: `ArmSwiHleTests` covers every intercepted function, the matrix/vector layouts,
  perspective divide, the `sqrt_frac16` restoring loop, unsupported-SWI fallback, the
  disabled trap, equal cycle cost for the SWI itself, and telemetry reset. The expected
  values for `MulVec3Mat33`, `MulMat33Mat33`, `MulVec3Mat33DivZ`, `MulVec4Mat44`,
  `MulManyVec4Mat44`, and `MulMat44Mat44` were generated with
  `References/opera-libretro-master/libopera/opera_fixedpoint_math.c` and match bit-for-bit.
- `SwiHle_IsWiredToMachineRam` executes `SWI #0x50000` through `FiveDoMachine` and checks
  the result at the guest destination.
- Options: `swiHle` is registered in `MachineOptionRegistry`, round-trips through
  Settings, per-game profiles, and save-state Options v9, and appears in
  `docs/MachineOptions.md`.
- CLI: `--swi-hle` enables the mode; `--swi-comments` emits a per-comment histogram.
- Real-BIOS benchmark (4,096 `MulMat44Mat44_F16` calls): 847,860 LLE steps /
  4,054,999 cycles / 0.025 s versus 24,578 HLE steps / 65,538 cycles / 0.002 s. See
  `Performance.md` §8.2.
- Title evidence: AutoBahn Tokio (3,891 calls in 3,000 frames) and Ballz (2,880 calls)
  boot and run with HLE. Their frame hashes differ from the default because HLE skips
  the guest handler's cycles/register clobbers; leaving the affected SWI to the guest
  restores the hash. Lemmings has no HLE calls and the differential smoke is an exact
  match. Default-off behavior is unchanged.

#### 1.1ao SYS-17 game database expansion (2026-11-05)

- `GameDatabaseNameParser` parses Redump, GoodTools, and No-Intro filenames, including
  release dates, compound `… & …` titles, region lists, language lists, disc numbers
  (`Disc 2 of 3`, `Disk B`), revisions (`Rev 1`, `RE2`, `v2.2`), and dump/edition tags.
  Unknown parenthetical groups stay in the title; uncertain fields stay empty.
- `GameDatabase` now parses and writes `revision`, `discNumber`, `discTotal`, `bios`,
  `bios2`, `fixMode`, `inputDevices`, `nvram`, and `cheats`. `Save`/`Load` roundtrip the
  full record, and `Merge` preserves curated records unless `--overwrite` is supplied.
- `gamedb stats|scan|export|merge` are covered by CLI tests. `scan` computes IDs for
  `.chd`/`.cue`/`.iso` files and writes a fragment; `merge` adds it to the embedded
  database and writes the reviewed result. Transient CHDSharp open failures are retried
  serially.
- Runtime integration: `GameCompatibility.ForGameId` reads the database `fixMode`;
  `BiosLibrary.SelectForGame` reads `bios`/`bios2` (per-game override still wins); and
  `GameControlDefaults` reads `inputDevices`. `info --json` exposes the new fields.
- Local coverage: `gamedb stats --folder "J:\Panasonic 3DO"` reports 1,366 images found,
  1,366 readable, 653 unique IDs, 1,366 matched, 100% coverage, and no errors. The three
  former exceptions were audio-only `[CD-Audio]` CHDs, which now open with TOC-derived IDs.
- Embedded database: 518 original 4DO records + 135 local records = 653. Known BIOS
  requirements (Powers Kingdom Europe, WorldCup Super Stadium Japan + Kanji), fix masks
  (Crash 'n Burn, Street Fighter II, DinoPark Tycoon, Microcosm), and lightgun/Orbatak
  input defaults are stored as data.

#### 1.1ap SYS-08 per-title CPU clock behavior (2026-11-05)

- Five titles were run for 3,000 frames at 0.5x, 1x, 2x, and 4x with `--audio null`.
  Every non-stock clock produced a different frame hash, so the clock is a compatibility
  variable rather than a speed switch. AutoBahn Tokio took 9.1 s at 1x, 84.4 s at 2x, and
  271.7 s at 4x for the same 3,000 frames.
- The stock 12.5 MHz default, the synthetic clock-invariant benchmark, and the decision
  not to seed per-title clock overrides are recorded in `Performance.md` §8.3.


#### 1.1aq SYS-01 audio-only CHD increment (2026-11-05)

- `ChdDiscImage` now opens a CHD whose track table has no data track. `ReadTrackSector`
  returns the native 2352-byte audio sectors; `ReadSector` still rejects data reads from
  an audio track.
- `GameId.Compute` gives an audio-only disc a stable MD5 derived from its track table of
  contents (`audio-cd|track:sectors|...`) instead of failing on the missing data sectors.
  The same TOC yields the same ID; a changed track length yields a different ID.
- `info` reports `audioCd = true` and `trackCount` and skips the volume-header read. The
  three local `[CD-Audio]` discs (`A&M Music Sampler`, `D's Diner - The Director's Cut`,
  `Oyaji Hunter Mahjong`) are now catalogued in the game database.
- The BIOS still requires a data track to boot, so the audio-only discs are not yet
  playable in-emulator; `PLAY_AUDIO_TRACK` and the CD-DA mix remain covered by the
  synthetic `CdromMultiTrackTests`/`CdAudioMixTests`.

#### 1.1ar SYS-18 auto per-game profiles (2026-11-05)

- `AppSettings.ResolveInputDevices` resolves a per-game override, then the game-database
  `inputDevices` spec, then the global list. CLI `run`/`trace` apply the database spec
  when `--input-device` is absent. `EnsureAutoInputDeviceOverride` skips database titles
  so a generated override cannot shadow future database updates.
- `SettingsWindow` changes flow through one `CreateEffectiveForGame` snapshot. Input,
  filter, and high-resolution changes apply live; BIOS, NVRAM, fix, and timing changes
  recreate the machine. `GameProfile.Clone` copies the device list, and the profile
  carries a `CheatBundle` name.
- Tests: database input resolution for Crime Patrol (`E8FBE61C`) and Orbatak
  (`B6B3F5B2`), no shadow override, profile clone independence, and immediate
  effective-profile changes for filter/input/fix/BIOS.
- Manual evidence, all with `--bios-auto bios` and no manual BIOS/region/fix/input flags:
  - WorldCup Super Stadium (Japan): selected `Panasonic FZ-10J` + Kanji ROM
    (`c23fb5d5e6bb1c240d02cf968972be37`) and rendered the "Story of the World Cup" screen
    at 3,000 frames.
  - Powers Kingdom (Europe): selected `Panasonic FZ-10E (Anvil)`, `region=Pal1`,
    `fieldRate=50`, `sourceHeight=288`.
  - Slayer (USA): selected `Panasonic FZ-10`.
  - Crash 'n Burn (USA): applied `fixMode=1` (`FIX_BIT_TIMING_1`).
  - Crime Patrol (USA): selected `1=lightgun` from the database and rendered its
    live-action intro at 3,000 frames.

#### 1.1as PERF-04 prefetch buffer increment (2026-11-05)

- `PrefetchingDiscSource` keeps a bounded pool of evicted sector buffers, so a full
  cache reuses its arrays instead of allocating a new `byte[]` per stored sector. The
  worker also reuses one 2048-byte data buffer and one 2352-byte audio buffer.
- `StoreData`/`StoreTrack` copy into pooled buffers; eviction returns the old array to
  the pool; `Dispose` clears the cache and pool.
- `CacheHits_DoNotAllocateOnTheCallerThread` warms a cached sector and then performs
  1,000 cache-hit reads, asserting `GC.GetAllocatedBytesForCurrentThread()` stays below 128 bytes (a one-time runtime allocation is tolerated).
- This is an increment, not completion: continuous miss/eviction streaming still needs
  an end-to-end allocation gate and frame-spike measurement.

#### 1.1at PERF-08 managed IL JIT (2026-11-05, partial)

- `Arm60.Jit.cs` compiles hot straight-line ARM60 blocks into `DynamicMethod` IL. Only
  data-processing, immediate-offset load/store, and branch instructions are compiled;
  multiply, swap, block transfer, SWI, MRS/MSR, mode changes, PC writes, and
  register-specified shifts stay on the interpreter.
- Stores and branches terminate a block. Every compiled instruction is verified against
  the current code word before it executes; a changed first word returns a stale sentinel
  and a changed later word returns the committed cycles so the next call recompiles or
  falls back. This covers self-modifying code, DMA, ROM-bank switches, and state loads.
- Blocks stop at the frame loop's 16-cycle batch boundary using the machine's current
  `batch`, so CLIO/CD/DSP/VDLP event timing matches the interpreter. Blocks compile only
  after 8,192 executions to keep one-shot boot/decompression code on the interpreter.
- `ArmJitTests` covers data processing, conditions, loads/stores, branches, link branches,
  LSL carry-out, stale-code refresh, synthetic boot, and a machine-level frame/RAM parity
  test. The CLI test compares interpreter/JIT `frameHash` and instruction counts.
- Lockstep evidence: 100,000,000 Lemmings instructions with identical PC, CPSR, registers,
  cycles, and instruction count. Full-frame hashes match for Lemmings 3,000 frames
  (`0x9C2D31F6`), Road Rash 1,000 frames (`0x41D2AC9A`), and Powers Kingdom Europe 1,000
  frames (`0xDCEB4C05`).
- Performance is a partial result: ALU/load microbenchmarks are 1.9-3.3x faster, but
  end-to-end boot is 1.17-1.85x slower because short blocks, CEL/VDLP work, and JIT code-cache
  pressure dominate. The interpreter remains the default. See `Performance.md` §8.4.

#### 1.1au SYS-06 RSA/unsigned-disc option (2026-11-06)

- `BiosRsaPatch` implements the community documented two-site media-RSA bypass:
  each `000313A0000111A0` sequence becomes `000313A0000011A0`; the
  `000213A0000111A0` guard sequence is left unchanged.
- Local verification with the retail images:
  - `panafz10.bin` (`51f2f43ae2f3508a14d9f56597e2d3ce`) + patch ->
    `1477bda80dc33731a65468c1f5bcbee9`, exactly `panafz10-patched.bin`.
  - `panafz10e-anvil.bin` (`a48e6746bd7edec0f40cff078f0bb19f`) + patch ->
    `cf11bbb5a16d7af9875cca9de9a15e09`, exactly
    `panafz10e-anvil-patched.bin`.
- `run --bios bios/panafz10.bin --allow-unsigned-discs --frames 0 --json` reports
  `biosMd5: 1477bda80dc33731a65468c1f5bcbee9`, `biosRsaPatched: true`, and
  `biosRsaDisabled: true`; the source BIOS file is unchanged.
- A 600-frame Wolfenstein 3D (USA) run produced the same frame hash
  `0x16B5C318` (381,025,496) with the retail BIOS and with the runtime-patched BIOS;
  a 600-frame Road Rash (USA) run produced the same hash `0x6CDB8B35` (1,826,602,437)
  with the retail FZ-1 and the runtime-patched FZ-1, so signed software is unaffected.
- `bios select "Wolfenstein 3D (USA).chd" --folder bios --allow-unsigned-discs`
  selects `Panasonic FZ-10 (No RSA)` (`1477bda8...`); the same command without the
  flag selects retail `Panasonic FZ-10` (`51f2f43a...`).
- Unit tests cover patch counting/idempotence, the guard sequence, in-memory-only
  patching, the no-site error, known-No-RSA catalog flags, a No-RSA file-name
  fallback, BIOS selection preference, settings round-trip, CLI JSON reporting, and
  `bios info` patch-site reporting. `bios/` local-file tests skip when the images are
  absent.
- The feature is complete: the BIOS patch is explicit and opt-in, the source image is
  never modified, known No-RSA images are preferred, and a compatible retail image is
  patched only when requested. No BIOS or game content is bundled.

#### 1.1av VID-12 latency options and PERF-02 AOT (2026-11-06)

- `bench --frames 120 --latency` (Release, this desktop): snapshot
  **9,348,438 bytes**, capture **3.6 ms/frame**, restore **2.0 ms/frame**. The synthetic
  bench hash remained `0x6CDFBDC5`. The uncompressed state is smaller than the previous
  ~10 MB because Machine section v2 writes only visible lines; RAM restore no longer
  clears the unused 22 MB tail.
- `LatencyTests` (10 tests) covers snapshot frame/hash round-trip, 1- and 2-frame run-ahead
  timeline parity against a normal run (`machineFrames = displayed + depth`), visible-only
  audio, frame-delay ring holding, range validation, and telemetry mean/max.
- Frontend Debug and AOT win-x64 runs of Lemmings (USA) with run-ahead 1, frame delay 1,
  VRR, and audio sync 40 ms logged the applied options, opened the disc, started
  emulation, and produced no core fault. The AOT run also logged `OpenAL audio ready
  (44100 Hz stereo, 100 ms, sync 40 ms, device default)` with `ALSOFT_DRIVERS=null`.
- AOT CLI: `--version` 69 ms, `bench --frames 2` 65 ms, 3,000-frame Lemmings boot
  `ok: true`, PC `0x0012B36C`, frame hash `0x0C5B2F8E`, `warnings: []`; 240-frame realtime
  OpenAL run with `--audio-sync 40` reported **0 dropped samples**, **0 underruns**, and
  frame hash `0x6CDFBDC5` matching the IL build.
  AOT frontend: 34.5 MB single file, `--version` 70 ms, smoke 1.2 s; trimmed frontend:
  27.9 MB single file, smoke 3.2 s; trimmed CLI: 19.4 MB. See `Trimming.md` and
  `Performance.md` §8.7.

#### 1.1aw TEST-07 reference oracle and five-title panel (2026-11-06)

- `benchmarks/diff/samples5.json` runs the local `D:/Samples/3DO` corpus:
  Alone in the Dark, Lemmings, Road Rash, Space Ace, and Wolfenstein 3D. Settings:
  300 frames, checkpoints 60/120/300, fixed seed **1488**, accuracy profile,
  `cdSpeed=unbounded`, `noGameFixes=true`, PC/CPSR/CD comparison, sample tolerance
  512, no images. The debug core is
  `artifacts/diff/opera-build-current/OperaCore.dll`, built from
  `artifacts/diff/opera-build` with MinGW gcc/g++ 15.2.0.
- **Determinism:** two consecutive runs with `--force` produced **identical
  checkpoint arrays** for both the 5DO and Opera captures (Compared with
  `ConvertFrom-Json` over the capture JSON). This is the fixed-seed gate that the
  previous wall-clock `std::rand`/`prng16`/`prng32` seeding could not provide.
- **Verdict:** 0 match, 5 diverged, 0 error. The report records
  `firstDivergence`; the console, JSON, and Markdown all show it.
- **First divergence:** ARM PC at frame 60 on all five titles:
  Alone `0x000057f8` vs `0x00005954`; Lemmings `0x00005aa0` vs `0x00005960`;
  Road Rash `0x000057e0` vs `0x0000597c`; Space Ace `0x0000595c` vs `0x00005948`;
  Wolfenstein `0x0000594c` vs `0x0000596c`.
  Maximum absolute PC deltas across the run: Alone 21,020; Road Rash 7,644;
  Space Ace 6,084; Wolfenstein 3,172; Lemmings 320 bytes.
- **Lemmings fields:**
  - f=60: frame hash matches `0x110e7dc5`; PC `0x00005aa0`/`0x00005960`;
    CPSR `0x600000d3`/`0x800000d3` (FIQ-disable bit); CD commands **9/9**,
    hash matches; samples 44,101/44,144 (within the 512 tolerance).
  - f=120: PC `0x00005988`/`0x00005974`; CPSR matches `0x800000d3`; CD commands
    **50/50**, hash matches; samples 88,202/88,288.
  - f=300: PC `0x000205f4`/`0x00020600`; CPSR matches `0x00000010`; CD commands
    **108/105**, hash diverges; samples 220,508/220,720.
- **CD capture semantics:** 5DO counts the opcode when the first command byte is
  written (`CommandOpcodeObserver`); the reference counts `XBP_SET_COMMAND` at
  the same phase. The earlier 7x count difference was a measurement artifact
  (the reference hook fires per command byte); counting only the first byte of
  each seven-byte packet removed it. A command in flight at a checkpoint can still
  leave a 1-3 count difference.
- **Audio:** the cumulative audio hash is recorded but not gated. The
  59.94 Hz reference field rate and 60 Hz 5DO field rate produce a systematic
  sample-count offset (43 samples at f=60, 212 at f=300), so the streams must be
  aligned or resampled before the hash is comparable.
- **3,000-frame follow-up (`samples5-long.json`):** same five titles, 3,000
  frames, checkpoints 60/300/1000/3000, no images. Lemmings matched the frame
  hash at all four checkpoints despite PC drift. Wolfenstein and Alone had
  transient frame-hash mismatches (Wolfenstein f=300; Alone f=1000) and recovered
  by f=3000. **Road Rash and Space Ace stayed visibly divergent**: f=1000 and
  f=3000 images show different animation frames/scenes, not a rendering error.
  A ±60-frame offset probe on Road Rash found **no neighboring hash match**, so
  the divergence is larger than one second of fields or the frame selection
  differs. CD command counts stayed close but not exact (Road Rash 611/598 at
  f=1000; 851/836 at f=3000). This is the first evidence that separates
  "PC drift with matching video" from "visible divergence" and gives CORE-04 and
  CORE-13 a concrete lead.
- **Full CD command sequence (`cdCommands`):** the final-checkpoint opcode
  sequence localizes the first divergence beyond count drift:
  - Lemmings command 105: 5DO `0x10` (READ_DATA) vs Opera `0x83` (READ_ID);
    5DO 364 opcodes, Opera 352.
  - Road Rash command 559: same pattern; 851/836.
  - Wolfenstein command 389: same pattern; 862/861.
  - Alone command 433: equal counts (932/932) but a shifted `0x10`/`0x83`.
  - Space Ace: prefixes match for 348 commands; 5DO has one extra `0x10`.
  This localizes CORE-04 to READ_DATA completion/read-ahead: after a few
  READ_DATA commands, Opera returns to READ_ID polling while 5DO keeps issuing
  READ_DATA. The earlier transcript-only build used a 7x byte count; the current
  export counts one opcode per seven-byte packet.
- **Panel generator:** `panel --from benchmarks/diff/full.json --out
  benchmarks/diff/panel50.json --count 50` generated a 50-title panel from the
  594-disc manifest. It selects evenly spaced titles (first and last included),
  preserves roots/BIOS/machine options, clears per-title frame overrides and
  allowlists, and applies the TEST-07 comparison policy. `validate` accepts it.
  The full panel has not been run because the `J:`/`G:` corpus is not mounted on
  every machine.
- **Tests:** 1,881 pass. New coverage: `HarnessCommonTests` (default seed, manifest
  hex seed, override precedence, random rejection), `DiffComparerTests`
  (CPSR gate, CD count/hash gate, full CD sequence divergence/prefix, sample
  tolerance, earliest first divergence), `CdromTranscriptTests` (issue-time
  hash/count, opcode list, and reset), and `DiffManifestBuilderTests` (panel
  selection, policy, and count edge cases).
- **Limitations:** five titles, not the 50-title panel; no scripted input yet; the
  audio hash is informational. See `DifferentialHarness.md` §8/§9 and
  `ImplementationPlan.md` TEST-07.

#### 1.1ax TEST-07 exact clock and 50-title panel (2026-11-06)

This entry supersedes the clock and first-divergence claims in 1.1aw. Both
statements were measurement artifacts of 5DO's 60.00 Hz NTSC clock:

- **Corrected CPSR reading:** `0x600000d3` vs `0x800000d3` differ in the N/Z/C
  condition flags (bits 31-29). The FIQ-disable bit (bit 6) is set in both
  values. The frame-60 PC/CPSR difference is a frame-boundary phase sample, not
  evidence of an FIQ-ordering bug.
- **Exact NTSC field rate.** NTSC now runs at 60000/1001 (208,541 cycles per
  field at 12.5 MHz) instead of 12,500,000/60 = 208,333. The DSP sample clock
  therefore produces 44,145 samples at frame 60 vs Opera's 44,144 (was 44,101),
  220,728 vs 220,720 at frame 300 (was 220,508/220,720), and 2,207,289 vs
  2,207,207 at frame 3,000. Every one of the 50 panel titles is within **16
  samples** at frame 300.
- **Rational VDL accumulator.** The VDL clock no longer adds the per-batch
  rounded 8.24 ARM fraction, which ran the scanline clock ~0.09% fast. Quartz
  accumulates `cycles * fieldLines * fieldRateNumerator` against
  `armClock * fieldRateDenominator`; 60 NTSC fields produce exactly 15,780
  scanlines. Quartz state version 4 stores the exact accumulator; v1-3 states
  convert their 8.24 fraction on load.
- **50-title panel (`artifacts/diff/test15-panel50/report.json`).** 50/50
  captured, **0 errors**. **32/50 titles match every frame hash** at frames
  60/120/300; 17 match one of three, and one bonus disc (`Icebreaker II`) has no
  video. **49/50 match the CD command count at frame 60** (delta -1..0); at
  frame 120 14/50 match (delta -2..0) and at frame 300 24/50 match (delta
  -2..+4). The first divergence is PC at frame 60 on 48/50 titles.
- **Instruction parity (new).** The reference core exports
  `sg_debug_arm_instructions`; the harness records it beside 5DO's
  `instructions`. At frame 60 Opera's absolute count is **89,338-90,063
  instructions higher** on all five sample titles (Lemmings 5,383,387 vs
  5,472,729), while the sample counts match. The offset is already present at
  frame 1 and stays fixed; the **per-frame retirement rate matches within 0.1%**
  over 240 frames on 4/5 titles, with Lemmings diverging only where its CD path
  diverges. This is a startup/first-field capture offset, not a cycle-model
  error, so it does not implicate CORE-01. FreeDO N=4 ARM timing is still far
  worse (3,855,498 instructions).
- **CD cycle stamps (new).** The debug core exports
  `sg_debug_cd_cycle_at`, recorded at the exact command boundary
  (`cmd_idx == 0`, also fixing the old byte-modulo-seven transcript for
  one-byte ABORT packets). The first 105 Lemmings opcodes match, but 5DO reaches
  command 104 at cycle 57,736,851 vs Opera's 57,621,696 (**+115,155 cycles,
  ~9.2 ms**). Road Rash +16,680, Space Ace +80,866, Wolfenstein +13,343, Alone
  +15,163 at their last common command. The remaining CORE-04 lead is a
  cadence/READ_DATA-completion difference, not the command table.
- **3,000-frame recheck.** With the exact clock, Space Ace matches Opera's
  frame hash at 3,000 (`0x46916c61`) and Road Rash matches Opera's frame 3002
  (`0xd5895ad5`). Wolfenstein (`0x175b8fe4`) and Alone (`0x175b8fe4`) already
  matched at 3,000.
- **Tests:** 1,884 pass. New coverage: exact-rational frame cycles, 60 fields =
  15,780 scanlines, sample count 44,144 +/- one, state v4 round-trip, behavioral
  divergence classification, and CD cycle capture. `dotnet format` is clean.
