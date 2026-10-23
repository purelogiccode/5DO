# Local Test Corpus

**Status:** local developer machine only; not part of the repository.
**Last inventoried:** 2026-10-23

This document records the legally obtained BIOS and disc images available on the
development machine for manual compatibility, boot, audio, input, save-state, and
performance testing. None of these files may be committed, packaged, or uploaded.

- BIOS directory: `D:\Dropbox\source\repos\CSharp_3DOEmulator\bios\`
- Disc directory: `D:\Samples\3DO\`

Both locations are outside version control. `bios/` is already ignored by
`.gitignore`, and `*.chd`/`*.iso`/`*.bin`/`*.cue` are ignored as a second guard.
`D:\Samples\3DO` is outside the repository entirely.

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

### 1.1 Current smoke baseline

The following results were recorded on 2026-10-23 with `panafz1.bin`, a Release
build, and `run --frames 600` (the sample column uses `--audio wav`). They are a
baseline for regression testing, not a compatibility pass:

| Title | Final PC | Frame hash | Samples (WAV) | Observation |
|---|---|---|---|---|
| Alone in the Dark | `0x000057C4` | `1826602437` | 442,431 | Runs; title/gameplay not yet confirmed |
| Lemmings | `0x000205F8` | `286162373` | 444,460 | Stuck in the BIOS menu/halt path |
| Road Rash | `0x000205C4` | `286162373` | 443,345 | Known BIOS menu/halt path (`0x205C4`) |
| Space Ace | `0x000205C4` | `286162373` | 443,303 | Known BIOS menu/halt path (`0x205C4`) |
| Wolfenstein 3D | `0x000205C4` | `286162373` | 443,301 | Known BIOS menu/halt path (`0x205C4`) |

The DSP produces roughly 737 samples per frame (44.1 kHz at 60 fps) for all five
titles, and WAV capture is non-empty. Audio content still needs a listening check.
The headless core runs these 600-frame smoke tests in roughly 1.5–2.3 seconds on
the development machine; that is **not** a 4K presentation benchmark.

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
$env:THREEDO_BIOS = "D:\Dropbox\source\repos\CSharp_3DOEmulator\bios\panafz1.bin"
$env:THREEDO_DISC = "D:\Samples\3DO\Road Rash (USA).chd"
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
