# Compatibility Notes

This document tracks what the emulator intends to support, what it deliberately does not support yet, and the game/BIOS quirks inherited from FreeDOCore/4DO. New compatibility work uses `References/opera-libretro-master/libopera` as the primary behavioral reference; see [`ReferenceStrategy.md`](ReferenceStrategy.md).

**Current position (2026-10-24, after the DSP and CCB fixes):** all five local corpus
titles boot, load their own OS, render real frames, and produce non-silent DSP audio.
Road Rash reaches its race scene, Wolfenstein 3D reaches gameplay (FLOOR 1-1),
Lemmings reaches its title menu, Alone in the Dark reaches its warning/play dialog,
and Space Ace plays its demo. Scripted input is now available and has been used to
pause/unpause Road Rash, navigate the Lemmings menu, and enter Wolfenstein gameplay.
Full gameplay validation (save states, long races, level completion) is still open.

The 2026-10-24 session found two root causes:

- **DSP immediate operands.** The loader shifted un-justified immediates left by two
  instead of zero. The AudioFolio tail program tests `Sema4Status & 8`; the extra
  shift turned the mask into `0x20`, so the DSP always skipped the ARM command and
  SAudio `AllocInstrument` timed out. The fix uses `IMMEDIATE << (JUSTIFY & 3)`.
  The DSP now runs instrument code and acknowledges the ARM.
- **CCB field order.** The CCB parser read `PRE0`/`PRE1` before the position, size,
  perspective, and pixel-processor fields, but FreeDO reads the geometry first and
  the preamble last. Every 3D CEL was decoded from the wrong offsets. Road Rash's
  race produced a bogus 1987x583 literal CEL whose arbitrary quads took minutes to
  rasterize; with the correct order the race renders at normal speed. The fix also
  corrects the CCB fields for all 3D titles.

Space Ace's former frame-681 runaway was a downstream consequence of the DSP
failure: the ReadySoft player's audio task copied a decoded buffer to a null
`[audioState+0x9C]` pointer, wiping the ARM exception vectors. With the handshake
working the copy no longer starts at address 0, and the title runs past 10,000
frames.

See [`Status.md`](Status.md) and [`NextSession.md`](NextSession.md).

---

## 1. BIOS ROMs

The 3DO boot ROM is **1 MB**. 4DO also accepts an optional second 1 MB ROM (BIOS2); if absent, the second bank is zero-filled.

| Item | Support |
|---|---|
| 1 MB BIOS1 | Required |
| Optional 1 MB BIOS2 | Supported |
| BIOS shorter than 1 MB | Rejected |
| BIOS larger than 1 MB | Rejected |
| Anvil BIOS detection (`byte[28] == 234`) | Supported; applies the FreeDO PC-skip workaround |
| Region-specific BIOS files | Loaded as-is; no region lock emulation |
| BIOS distribution | Not included; user must provide |

The BIOS picker shows the file name, size, MD5, and detected Anvil flag. The selected BIOS path is stored in settings. A missing/invalid BIOS prevents starting a session with a clear error.

---

## 2. Disc images

| Format | Status | Notes |
|---|---|---|
| ISO, 2048-byte sectors | v1 | Most common 3DO image format |
| BIN, 2352-byte sectors | v1 | Payload starts at offset `0x10` |
| CUE + BIN | v1 | Single data track, MODE1/2048 or MODE1/2352 |
| **CHD** | **v1** | **MAME CHD via pure C# CHDSharp; CD/GD-ROM track table; parent/child supported** |
| Multi-track / CD-DA | Not supported | 3DO games use a single data track; Red Book audio is out of scope |
| ZIP/7z archives | Later | Prefer extracting first |

Sector count is read from the 3DO volume header at sector 0 (big-endian `blockCount` at byte offset 80). If the header is invalid, fall back to `fileSize / sectorSize` for ISO/BIN or the CHD data track's frame count.

CHD specifics:

- CHDSharp's `ReadSector(lba)` returns the first 2352 bytes of the 2448-byte CD frame. `ChdDiscImage` extracts 2048 user-data bytes based on the first data track:
  - cooked Mode1/2048 -> offset 0
  - raw Mode1/2352 -> offset 16
  - raw Mode2/Form1 -> offset 24
- Standalone and parent/child CHDs are supported; an optional parent path can be configured.
- DVD, HDD, raw, and audio-only CHDs are rejected; 3DO software is CD-ROM based.
- Read-ahead is enabled by default (4 hunks) for sequential streaming.

Game ID is the MD5 of the first two 2048-byte sectors (4096 bytes), matching 4DO. This ID keys save states, screenshots, and optional per-game settings. It must never change, or existing save data will be orphaned.

---

## 3. NVRAM

- 32 KB, persisted as `NVRAM_SaveData.ram` (v1 global file) in the saves directory.
- Writes are debounced (250 ms, ported from 4DO) and written atomically (temp file + replace).
- The NVRAM address mapping has a FreeDO quirk: one byte per 32-bit word address. Do not "fix" it.
- A fresh NVRAM image is initialized with the 3DO volume header (record type 1, `ZZZZZ` sync, label `nvram`, 32768 blocks, root directory copy at `0x84`), matching 4DO.
- Per-game NVRAM is a possible later enhancement; v1 uses the 4DO-compatible global file.

---

## 4. Controllers

The 3DO pad has: **A, B, C, P, X, L, R**, plus D-pad directions. P is the pause button. There is no separate Start/Select in the standard pad protocol used by 4DO.

| Item | Support |
|---|---|
| 1-6 controllers | v1 |
| Keyboard mapping | v1 |
| Gamepad mapping | v1 target; backend selected in M7/M9 |
| PBUS packet | 16 bytes, device order 1,3,2,5,4,6 |
| Console hotkeys | Save/load state, slot next/previous, fullscreen, screenshot, pause, frame advance, reset |

PBUS encoding is documented in `docs/Architecture.md` and ported from 4DO's `JohnnyInputPlugin`.

---

## 5. Game-specific fixes

FreeDOCore carries a `fixmode` bitmask. 4DO maps known game IDs to flags. The C# port keeps the same names and behavior.

| Flag | Value | Used for | Behavior |
|---|---:|---|---|
| `FIX_BIT_TIMING_1` | `0x00000001` | Crash 'n Burn (JP/US) | LDM value/PC timing fixes at `0x94D60` and `0xF1000`/`0xEFE54` |
| `FIX_BIT_TIMING_2` | `0x00000002` | Street Fighter II (intro sync in 4DO DB) | Reserved in this FreeDOCore revision; keep the flag |
| `FIX_BIT_TIMING_3` | `0x00000004` | Dinopark Tycoon | `TexelDraw_Scale` multiplies Y by 5 |
| `FIX_BIT_TIMING_4` | `0x00000008` | Reserved | No core use |
| `FIX_BIT_TIMING_5` | `0x00000010` | Microcosm | VDLP current/previous bitmap offsets swapped |
| `FIX_BIT_TIMING_6` | `0x00000020` | Alone in the Dark | Literal CEL Y step uses `(y >> 16) << 1` |
| `FIX_BIT_TIMING_7` | `0x00000040` | Reserved | No core use |
| `FIX_BIT_GRAPHICS_STEP_Y` | `0x00080000` | Samurai Shodown (EU/US/JP) | Preserve Y instead of X between CELs |

The 4DO game database also maps:

- `F3AF1B13`, `217344B0` -> `FIX_BIT_TIMING_1`
- `DBB419FA`, `07C32F10`, `5282889F`, `7340307E` -> `FIX_BIT_TIMING_2`
- `CB8EE795` -> `FIX_BIT_TIMING_3`
- `1A370EBA`, `B35C911D` -> `FIX_BIT_TIMING_5`
- `0511D3D2`, `9F7D72BC`, `E843C635` -> `FIX_BIT_TIMING_6`
- `2AABA5B9`, `BF61BB32` -> `FIX_BIT_GRAPHICS_STEP_Y`

The game database is ported with attribution. Unknown game IDs get the first 8 hex characters of the MD5, with collision avoidance, matching 4DO. Users can override fixes per game in settings.

---

## 6. Video

| Feature | v1 | Later |
|---|---|---|
| 320x240 NTSC | Yes | |
| **OpenGL/GLES renderer** | **Required desktop path** | |
| GPU palette expansion | Yes (exact CPU parity in Accurate mode) | |
| 4:3 aspect ratio, letterbox | Yes | |
| Nearest / integer scaling | Yes | |
| Sharp-bilinear | Yes (default enhanced) | |
| CRT filter | Yes (opt-in) | |
| xBR/HQx GPU filters | Yes (opt-in) | |
| 1080p/1440p/4K output | Yes (GPU scaling); 4K60 on Tier A hardware | |
| Fullscreen | Yes | |
| 384/512/640/1024-wide VDLP modes | Parsed, may crop | Full presentation |
| High-resolution CEL (`RESSCALE`) | No | Planned as a coherent feature |
| PAL | No | Later if needed |
| CPU fallback renderer | Yes (no GL context) | |
| Screenshots | Yes (GL readback or CPU frame) | |
| Frame advance | Yes | |

---

## 7. Audio

| Feature | v1 | Later |
|---|---|---|
| DSP emulation | Yes | |
| 44.1 kHz stereo | Yes | |
| Null/WAV sink | Yes | |
| Desktop audio backend | OpenAL Soft via Silk.NET (v1 target) | Platform-specific backends possible |
| Volume/mute | Yes | |
| Audio-driven frame pacing | No (fixed 60 Hz) | Later refinement |
| CD-DA | No | Out of scope |

---

## 8. Save states

- Native C# format, versioned, explicit little-endian, CRC-protected.
- Includes CPU, RAM, ROM, NVRAM, MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD, Quartz.
- Does not include disc contents, input state, or derived frame buffers.
- Stores BIOS and disc hashes; a mismatch warns and can be rejected by default.
- 4DO `.4dosav` import/export is **not** a v1 goal. The native format is compiler/struct-layout dependent and includes precomputed DSP tables; a best-effort importer can be added later if there is demand.

---

## 9. Known limitations / out of scope for v1

- No netplay.
- No rewind.
- No CD-DA / Red Book audio.
- No multi-disc swapping.
- No PAL.
- No high-resolution CEL rendering (output upscaling via OpenGL is supported).
- No GPU CEL compositing in v1; CEL rendering is software, matching FreeDO. The OpenGL renderer accelerates presentation, palette expansion, scaling, and filters.
- No 3DO mouse/light gun/other peripherals.
- No 3DO modem/network emulation.
- No cycle-exact ARM bus timing yet. The interpreter preserves FreeDOCore's `Ncycle = 4`; Opera uses `Ncycle = 2` with datasheet references. The difference is under evaluation and must not be changed by default until a differential test proves the impact.
- No guarantee of 4DO save-state compatibility.
- The primary renderer requires desktop OpenGL 3.3 or OpenGL ES 3.0/3.1 (see `docs/Performance.md`); systems without either use the CPU fallback.
- CHD is supported for CD/GD-ROM images; DVD/HDD/raw CHDs are not 3DO discs.
- CHD decompression is pure managed code and may be slower than ISO on low-end arm64 devices; read-ahead/cache mitigates this.

---

## 10. Compatibility matrix template

Use this table when testing real discs. Record the disc hash/ID, BIOS, build, and result.

| Game | Region | Disc ID | BIOS | Build | Boot | In-game | Audio | Input | Notes | Opera reference |
|---|---|---|---|---|---|---|---|---|---|---|
| Lemmings | USA | `0be38b22` | `panafz1` | 0.0.0 | **Pass** | **Title menu reached** | Non-silent DSP output | **Works**: menu selection changes with D-pad | CyberIntro renders; 10,000-frame run reaches the Load/Options menu | Not re-tested this pass |
| Space Ace | USA | `112b53d0` | `panafz1` | 0.0.0 | **Pass** | **Demo plays past 10,000 frames** | Non-silent DSP output | Scripted taps supported | Former frame-681 null-buffer runaway is gone; demo video renders | Demo intro 60 s, no runaway |
| Road Rash | EU-US | `d7672550` | `panafz1` | 0.0.0 | **Pass** | **Race scene reached** | Non-silent DSP output | **Works**: P toggles the pause overlay | Loads the game OS, SAudio init succeeds, 3D race renders; 30,000-frame run reaches the race | Intro FMV 60 s |
| Wolfenstein 3D | USA | `3dfcdbbf` | `panafz1` | 0.0.0 | **Pass** | **Gameplay (FLOOR 1-1)** | Non-silent DSP output | **Works**: scripted taps enter gameplay | Loads Sherry/Operator/FS v24.225; 10,000-frame run with input shows the in-game HUD | Intro 60 s |
| Alone in the Dark | USA | `9f7d72bc` | `panafz1` | 0.0.0 | **Pass** | **Warning/play dialog** | DSP stops after the dialog | Scripted taps supported; dialog not yet advanced | AudioFolio init succeeds; the game shows a "Play Anyway" warning dialog | Intro/loading 60 s |

`Pass` means a real game frame was rendered and verified visually. The Opera reference
column is the bundled Windows OperaCore with the same BIOS and an ISO extracted from
the same CHD; it verifies game progress, not audio output. The DSP/CCB fixes are
described above; input results come from `--input-script` runs.

The 2026-10-23 CD fix is significant: `CdromDevice.InitCd` used FreeDO's
double-add of the 150-frame lead-in, so the disc end reported to the CD DIPIR was
150 frames larger than the real disc. The DIPIR rejects a disc when the reported
end exceeds the volume block count by more than 512 frames. Lemmings had enough
padding to pass; Road Rash, Space Ace, and Wolfenstein did not, and were
misidentified as non-3DO discs. Reporting the real end fixed all three.

Space Ace's former runaway is now explained. At frame ~681 the ReadySoft player's
audio task copies a decoded audio chunk to the pointer at `audioState+0x9C`. That
pointer was zero because the player's mixer/instrument setup failed
(`AllocInstrument` returned `d52bf113`). The 27,538-byte `memcpy` started at address
0, wiped the ARM exception vectors, and a timer FIQ then vectored through the zeroed
table. After the DSP handshake fix the pointer is valid, the copy no longer starts at
0, and Space Ace runs past 10,000 frames.

The DSP failure was an operand-decoding bug, not a resource-map or microcode issue.
The AudioFolio tail program uses `Sema4Status & 8` to detect an ARM command. The
port's immediate loader shifted by two when the one-bit `JUSTIFY` field was zero,
so the constant 8 became 0x20 and the AND always produced zero. The DSP therefore
took the "no command" branch, skipped `Sema4Data`, and went back to sleep without
writing the ACK. FreeDO and Opera both use `IMMEDIATE << (JUSTIFY & 3)`. A second
multiplier-rounding difference was fixed in the same pass
(`(MULT1 * (Y >> 15)) & ~1`, not `MULT1 * ((Y >> 15) & ~1)`).

The second root cause was in the CEL control-block parser. FreeDO reads the CCB
body in this order: next pointer, source pointer, PLUT pointer, position, size,
perspective, pixel-processor control, then the `DSPP` preamble. The port read the
preamble immediately after the pointers and then read the geometry from the
following words. Every 3D CEL was therefore built from shifted fields. Road Rash's
race scene decoded a 1987x583 literal CEL with a perspective delta of 560 pixels
per row; its arbitrary quads were pathological and the run appeared to hang.
Parsing the fields in the reference order fixed the race and also changed Space
Ace's 3,000-frame hash. The rasterizer's `cnt_cross > 2` x-point sort was also
missing; it is now ported from the reference.

**Opera plays the same titles past these points**, and the C# port now does too.
The next compatibility step is input-driven gameplay validation, not another DSP or
CD rewrite.

## 11. Reference comparison (2026-10-23, updated 2026-10-24)

The data track of each CHD was extracted to ISO with `3do disc dump`, then run with
the bundled Windows OperaCore (`References/CPP_SimpleGui`) and the same
`panafz1.bin` BIOS. Opera rendered Road Rash, Wolfenstein, Alone, and Space Ace for
60 s. After the 2026-10-24 DSP fixes the C# port also renders all five titles and
produces non-silent audio. The comparison method and commands are in
[`ReferenceStrategy.md`](ReferenceStrategy.md); the next-session plan is in
[`NextSession.md`](NextSession.md).

The 2026-10-24 pass added the diagnostics needed to make the comparison mechanical:
`--watch-lowmem`, `--dump-ram`, `--dump-dsp`, `--dsp-trace`, the `disasm` command,
frame-stamped CLIO traces, DSP state in the run JSON, and `--input-script` for
repeatable controller taps. The DSP trace and memory dumps identified the exact
`Sema4Status & 8` test that was being mis-decoded.

Local corpus (user-provided, not distributed; see [`TestCorpus.md`](TestCorpus.md)):

- BIOS-only boot and menu navigation: `panafz1.bin`.
- `Alone in the Dark (USA).chd` — exercises `FIX_BIT_TIMING_6` and literal CELs.
- `Lemmings (USA).chd` — 2D-heavy CEL/packed-sprite workload.
- `Road Rash (USA).chd` — 3D/matrix and streaming workload.
- `Space Ace (USA).chd` — FMV/streaming workload.
- `Wolfenstein 3D (USA).chd` — first-person 3D/matrix workload.
- Alternate BIOS variants (`goldstar.bin`, `panafz10.bin`, Anvil) for region/fix paths.

Suggested additional coverage:

- A CD-streaming title with FMV/audio.
- A title with a known 4DO fix flag not represented above.
- A homebrew/test disc if available.

---

## 12. Reporting a compatibility issue

Include:

- Emulator version/commit.
- BIOS MD5 and region.
- Disc ID (first 8 hex of the 2-sector MD5) and format (ISO/BIN/CUE).
- Fix mode flags and settings.
- Steps to reproduce.
- Save state and a short input recording if possible.
- Log output with the `Emulation` and `Cdrom` categories enabled.
