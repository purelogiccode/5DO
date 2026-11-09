# Compatibility Notes

This document tracks what the emulator intends to support, what it deliberately does not support yet, and the game/BIOS quirks inherited from FreeDOCore/4DO. New compatibility work uses `References/opera-libretro-master/libopera` as the primary behavioral reference; see [`ReferenceStrategy.md`](ReferenceStrategy.md).

**Current position (2026-10-29, twenty-first pass):** all five golden-corpus titles boot,
load their own OS, render real frames, and produce non-silent DSP audio. Seven
additional titles were validated on 2026-10-26 (Gex, Need for Speed, Super Street
Fighter II Turbo, Total Eclipse, Killing Time, Theme Park, and Return Fire): each
boots, renders, and save-state roundtrips deterministically; six produce non-silent
44.1 kHz stereo audio, while Theme Park's captured menu is silent. The 2026-10-28 pass
fixed live OpenAL crackle, added the PCM tap and FSR 1.0; compatibility status is
unchanged. Road Rash's 3D race
renders correctly and the attract race continues past 60,000 frames with no allocator
error; Wolfenstein 3D starts a fresh game (FLOOR 1-1), walks/turns/fires, and its save
states roundtrip; Alone in the Dark's in-game character renders upright at the
staircase, and its Begin/Resume menu and intro are reachable; Lemmings reaches its
title menu and passcode screen, its 640x480 high-resolution title matches the
reference, and its Fun 4 passcode now starts gameplay with a save/load roundtrip after
being typed entirely with the D-pad; Space Ace plays its animated demo and starts its
interactive scene (`X` menu, `A` play) with a save/load roundtrip. The fifteenth pass
also added the `--gl-backend` selector and validated Windows ANGLE/GLES 3.0 with
pixel-identical CPU/GPU output. The tenth pass added CRT bloom and committed the D-pad
passcode script. The eighth pass fixed the GPU palette shader so real frames use
independent R/G/B CLUT indices and added the opt-in xBR-lv2 filter. The 2026-10-29
twenty-first pass added the optional Vulkan xBR/FSR GPU filters and HQx CPU path and
removed the CPU palette-upload workaround from the OpenGL xBR path; compatibility
status is unchanged.

> **Full-collection matrix (2026-10-30):** the curated evidence below is being
> expanded into [`GameCompatibilityList.md`](GameCompatibilityList.md), which contains
> the 241-title 4DO reference list, local filename matches from `J:\Panasonic 3DO` and
> `G:\MAME\MAME Software List CHDs\3do`, and empty 5DO status columns. The sweep
> procedure and status definitions are in
> [`CompatibilitySweep.md`](CompatibilitySweep.md). The high-resolution research plan is
> in [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

The ninth pass captured per-title save-state, PNG, and WAV evidence (see
`TestCorpus.md` 1.5a) and fixed the old Road Rash ~42,000/~92,000 stops. They were a
static game-side wait (only READ_ID polls, all input ignored), not wall/rider pins.
A CD media-access pulse now advances those screens to `BUSTED`, race select, and a
new race; the autopilot then cycles race -> wreck -> menu -> new race to 100k. The
Wolfenstein fresh-game walk, Lemmings passcode, Space Ace interactive scene, and
Alone movement loop are verified. The open Road Rash work is finishing a full race
without wrecking, not the old CD wait.

The CD wait fix is a media-access pulse: two consecutive READ_ID commands with
no intervening command and at least 50 frames between them set poll bit `0x40`.
Portfolio uses that event to finish the post-race/post-load transition. The fix
advances Road Rash's ~42k/~92k/100k screens to `BUSTED`, race select, and a new
race. The five 3,000-frame corpus hashes were unchanged by that fix at the time;
the exact DSP audio clock later re-baselined them (current keys in
[`TestCorpus.md`](TestCorpus.md) 1.1).

The 2026-10-24 sessions found four root causes:

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
- **NVRAM initialization.** The CLI never loaded an NVRAM image, so games that check
  the NVRAM volume for save space saw a zero-filled device. Alone in the Dark
  interpreted that as zero free slots, showed its "not enough space to save" warning,
  and shut down. The CLI now loads a fresh 32 KB volume by default and accepts
  `--nvram <path>` to persist it. Alone reaches its in-game room/inventory scene.
- **Arbitrary-CEL X/Y transposition.** The new rasterizer helpers take `(x, y)`, but
  the arbitrary-quad fill passed `(i, j)` (row, column), following FreeDO's
  `readPIX(src, i, j)` parameter order. Every asymmetric quad was written transposed.
  Road Rash's race CELs wrote texels into the top of VRAM where the OS keeps its VRAM
  free list, producing `AllocMem error, bad node address` and a frozen race; Alone in
  the Dark drew its character horizontally. Passing `(j, i)` fixes both. The same pass
  fixed the VDLP bitmap copy to use the modulo-derived line width instead of a fixed
  320 pixels, so 384/512/640-wide modes are no longer cropped, and made save-state
  loads restore the VDL pixel buffers immediately.

The 2026-10-25 eighth pass fixed a renderer-only issue: the GPU palette shader used
the low five bits of each pixel as one CLUT index for all three channels, so real
content appeared grayscale even though the CPU oracle was correct. The shader now
uses the red, green, and blue indices independently. Lemmings gameplay was captured
through sharp-bilinear and xBR with correct colors. The 2026-10-29 pass removed the
former CPU-expanded xBR source; xBR now samples the GPU palette texture directly and
the 320x240 WGL/ANGLE captures remain top-down and visually correct.

Space Ace's former frame-681 runaway was a downstream consequence of the DSP
failure: the ReadySoft player's audio task copied a decoded buffer to a null
`[audioState+0x9C]` pointer, wiping the ARM exception vectors. With the handshake
working the copy no longer starts at address 0, and the title runs past 10,000
frames.

See [`Status.md`](Status.md) and [`ImplementationPlan.md`](ImplementationPlan.md).

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
| Unsigned-disc / No-RSA boot | Opt-in `--allow-unsigned-discs` / Settings > Paths; a known No-RSA BIOS is preferred, otherwise the documented two media-RSA check sites are patched in memory. The BIOS file is never modified. |
| Region-specific BIOS files | Loaded as-is; no region lock emulation |
| BIOS distribution | Not included; user must provide |

The BIOS manager scans a folder, reports file name, size, MD5, region, video standard, Kanji, Anvil, patch state, and No-RSA state, and stores the selected primary/secondary paths. Auto-selection uses the game database region, known title requirements, and per-game MD5 overrides; the configured BIOS remains the fallback. A missing/invalid BIOS prevents starting a session with a clear error. With unsigned-disc support enabled, auto-selection prefers a No-RSA image and otherwise patches a compatible retail image in memory. See [`UserGuide.md`](UserGuide.md) and [`Configuration.md`](Configuration.md).

---

## 2. Disc images

| Format | Status | Notes |
|---|---|---|
| ISO, 2048-byte sectors | v1 | Most common 3DO image format |
| BIN, 2352-byte sectors | v1 | Payload starts at offset `0x10` |
| CUE + BIN | v1 | Multi-file/multi-track CUE, MODE1/2048, MODE1/2352, audio tracks, `INDEX`/`PREGAP`/`POSTGAP` |
| **CHD** | **v1** | **MAME CHD via pure C# CHDSharp; CD/GD-ROM track table incl. audio; flat 2048-byte ISO CHDs; parent/child supported** |
| Multi-track / CD-DA | Partial (2026-11-01) | Track table, raw CD-DA sectors, PLAY_AUDIO position, DSP mixing, persisted volume/mute, and M3U multi-disc swap are implemented; read-ahead/subcode and known-title evidence remain |
| ZIP/7z archives | Later | Prefer extracting first |

Sector count is read from the 3DO volume header at sector 0 (big-endian `blockCount` at byte offset 80). If the header is invalid, fall back to `fileSize / sectorSize` for ISO/BIN or the CHD data track's frame count.

CHD specifics:

- CHDSharp's `ReadSector(lba)` returns the first 2352 bytes of the 2448-byte CD frame. `ChdDiscImage` extracts 2048 user-data bytes from each track using its native layout:
  - cooked Mode1/2048 -> offset 0
  - raw Mode1/2352 -> offset 16
  - raw Mode2/Form1 -> offset 24
  - audio/2352 -> raw sector bytes (byte-swapped from CHD's Red Book order)
- Standalone and parent/child CHDs are supported; an optional parent path can be configured.
- Flat CHDs with 2048-byte units are also supported and exposed as one data track. This covers
  cooked ISOs converted with `chdman createdvd` (or `createhd` with 2048-byte units), which
  have no CD track table. CD audio is unavailable for these data-only images.
- DVD/HDD/raw CHDs with other unit sizes and audio-only CHDs are rejected; 3DO software is CD-ROM based.
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
| 1-8 controllers | v1 (six in the legacy packet; eight in the Opera chain) |
| Keyboard mapping | v1 |
| Gamepad mapping | v1 target; backend selected in M7/M9 |
| PBUS packet | Legacy 16-byte 4DO layout (device order 1,3,2,5,4,6) for all-joypad configurations on ports 1-6; Opera daisy-chain records when a peripheral is selected or ports 7-8 are active |
| Input devices | Joypad, flightstick, mouse, lightgun, arcade lightgun, Orbatak trackball, and none |
| Console hotkeys | Save/load state, slot next/previous, fullscreen, screenshot, pause, frame advance, reset |

PBUS encoding is documented in `docs/Architecture.md`. The legacy packet is ported
from 4DO's `JohnnyInputPlugin`; the device records follow `libopera/opera_pbus.c`.

Portfolio OS fixed-point math SWIs (`0x50000`-`0x50012`) can be handled on the host with
`--swi-hle` or Settings > Emulation > **Portfolio OS SWI HLE**. This is an opt-in
performance mode: the host routines match `opera_fixedpoint_math.c`, but HLE skips the
guest handler's cycles and register clobbers. Titles that poll cycle-sensitive hardware
around matrix calls can therefore change behavior; `AutoBahn Tokio` and `Ballz` boot and
run with HLE but produce different frame hashes. Leave it off for compatibility, or use a
per-game profile when a specific title benefits. See `docs/Performance.md` §8.2.

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

The game database is ported with attribution. Unknown game IDs get the first 8 hex characters of the MD5, with collision avoidance, matching 4DO. `GameCompatibility` applies the mapping automatically when the CLI or desktop shell opens a disc; `--fix-mode`/`--no-game-fixes` and the per-game override in Settings provide explicit control. The 3,000-frame Alone boot hash is unchanged with `FIX_BIT_TIMING_6` applied.

The database grew from 518 to 653 records in SYS-17. Record-level `fixMode`, `bios`,
`bios2`, and `inputDevices` fields now feed `GameCompatibility`, `BiosLibrary`, and
`GameControlDefaults`; `revision`, `discNumber`/`discTotal`, `nvram`, and `cheats` are
carried for profile seeding and diagnostics. On the local 1,366-image collection,
`gamedb stats --folder "J:\Panasonic 3DO"` reports 1,366 readable images and 100%
metadata coverage of those images. The three exceptions are audio-only `[CD-Audio]`
bonus discs with no data track, not game images. See
[`GameDatabase.md`](GameDatabase.md) for the schema and the `gamedb` contribution
workflow.

---

## 6. Video

| Feature | v1 | Later |
|---|---|---|
| 320x240 NTSC | Yes | |
| **OpenGL/GLES renderer** | **Required desktop path** | |
| Optional Vulkan renderer | Offscreen/readback backend and `--vk-probe`; accurate/nearest/sharp-bilinear/CRT/xBR/FSR/HQx | Native Vulkan surface/swapchain, vsync |
| GPU palette expansion | Yes (exact CPU parity in Accurate mode) | |
| 4:3 aspect ratio, letterbox | Yes | |
| Nearest / integer scaling | Yes | |
| Sharp-bilinear | Yes (default enhanced) | |
| CRT filter | Yes (opt-in) | |
| xBR 4x / HQx 2x/3x/4x filters | Yes (opt-in; all GPU passes) | |
| 1080p/1440p/4K output | Yes (GPU scaling); 4K60 on Tier A hardware | |
| Fullscreen | Yes | |
| 384/512/640/1024-wide VDLP modes | Parsed, may crop | Full presentation |
| High-resolution CEL (`RESSCALE`) | Opt-in `--high-res`; line-doubled 640x480 output | True internal 640x480 / supersampling research in M14 (post-1.0) |
| PAL | No | Later if needed |
| CPU fallback renderer | Yes (no GL context) | |
| Screenshots | Yes (GL readback or CPU frame) | |
| Frame advance | Yes | |

**GPU HQx (2026-11-07).** HQx 2x/3x/4x now run as GPU rule-table passes in OpenGL,
Direct3D 11, and Vulkan; the CPU `HqxFilter` remains the reference oracle. On the RTX
4070 the 4K WGL probe measured 0.23/0.32/0.25 ms present and 0.11/0.13/0.16 ms GPU for
2x/3x/4x, with 0 managed bytes/frame. WGL, ANGLE/GLES, Vulkan, and Direct3D 11 640x480
HQx captures are byte-identical to the CPU `HqxFilter` expansion. The opt-in
D3D/Vulkan tests compare all three scales with `HqxFilter.Expand` and match with 0
differing pixels. The GPU pattern uses `float` YUV while the CPU reference uses
`double`; a tiny fraction of colors can classify differently by one LSB, but the
synthetic probe frame and parity tests match exactly.

### 6.1 High-resolution mode

`EmulatorTuning.HighResolution` / `--high-res` is an opt-in 640x480 renderer. CEL
pixels address the 640-wide X coordinate and the 240-line Y coordinate; the even/odd
X column is stored in plane 0/1 (and mirrored to planes 2/3 for the second interlace
field). The VDLP interleaves planes 0/1 and 2/3 into two 640-wide scanlines and the
frame source becomes 640x480. The extra planes follow the active VRAM capacity
(`4 * VramSize`) and are not CPU-visible. SPORT fill/copy operations mirror the source page into the extra
planes. Save states, frame hashes, screenshots, the CPU assembler, and the OpenGL
uploader all carry the larger frame.

**Important limitation:** this is FreeDO-style line doubling, not true internal
640x480 rendering. Each 240-line source line is written to both interlace fields, so
the vertical detail is still 240 lines. The mode improves horizontal CEL sampling and
matches 640-wide titles such as Lemmings, but it does not add true vertical geometry.
See [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md) for the M14 research
options (true 640x480, 2x supersampling, and GPU CEL compositing). M14 is post-1.0 and
does not block v1.0.

Evidence: from a fresh Lemmings boot, the 640x480 title screen shows all four buttons
and the full logo and matches the reference screenshot; the five 3,000-frame low-res
corpus hashes were unchanged by high-res mode (the current keys are in
[`TestCorpus.md`](TestCorpus.md) 1.1). The mode is off by default because 320x240 titles must
not be upscaled. The Lemmings passcode screen is a 320-wide UI scaled 2x; its five
visible keyboard columns are the game's own window, not a framebuffer crop, so it
does not need a separate high-resolution input path.

---

## 7. Audio

| Feature | v1 | Later |
|---|---|---|
| DSP emulation | Yes | |
| 44.1 kHz stereo | Yes; exact rational sample clock (44,100 samples per ARM second) | |
| Null/WAV sink | Yes; streaming peak/RMS/clipping/DC statistics | |
| Desktop audio backend | OpenAL Soft 1.23.1 via Silk.NET, bundled in the x64 release bundles | Platform-specific backends possible |
| Live CLI audio | `--audio openal --volume`, `--realtime` pacing; trimmed win-x64 and linux-x64 validated with the null backend | |
| Volume/mute | Yes (output and CD-DA) | |
| Audio-driven frame pacing | Yes (audio clock with a 60 Hz fallback) | |
| Audio analysis | `audio analyze <wav> --json` | Spectrum/loudness comparison with Opera remains a manual check |
| AOT live audio | Validated for the win-x64 NativeAOT CLI with the bundled runtime | Frontend AOT audio remains unvalidated |
| CD-DA | Partial | Raw 2352-byte audio sectors, PLAY_AUDIO position, 44.1 kHz DSP mixing, persisted volume/mute, and Settings/CLI controls; read-ahead/subcode and known-title evidence remain |
| Multi-disc | Partial | M3U/M3U8 playlist, F3/UI/CLI swap, guest inject, and per-disc hash; guest ODE prompts and a known multi-disc title remain |

---

## 8. Save states

- Native C# format, versioned, explicit little-endian, CRC-protected.
- Includes CPU, the live RAM/VRAM backing range (stock 2 MB DRAM + four 1 MB VRAM planes;
  the largest layout is 14 MB DRAM + four 2 MB VRAM planes), ROM, NVRAM, MADAM, CLIO, DSP,
  SPORT, VDLP, XBUS, CD, Quartz, compatibility flags, and the parsed VDL frame. Large
  sections are Brotli-compressed; a typical state is 0.6-2.0 MB on disk (the uncompressed
  payload is ~10 MB for the stock layout and ~26 MB for the largest). Version-1 states
  still load.
- Does not include disc contents or input state.
- Includes the parsed VDL frame lines, so a zero-frame load is pixel-identical to the
  saved frame even in high-resolution mode.
- Stores BIOS and disc hashes. A BIOS or emulator-version mismatch produces a warning
  but the load continues; a differing expected disc hash adds a warning but still
  loads.
- 4DO `.4dosav` import/export is **not** a v1 goal and is not implemented. The native
  format is explicit and versioned; a best-effort importer can be added later if there
  is demand. See [`SaveStates.md`](SaveStates.md).

---

## 9. Known limitations / out of scope for v1

- No netplay.
- Rewind, fast-forward, cheats, TAS movies, OSD/quick menu, multi-track/CD-DA
  sectors, CD-DA volume/mute, and multi-disc playlists are implemented;
  CD-DA read-ahead/subcode, guest ODE prompts, and known-title disc audio evidence remain.
- No PAL.
- High-resolution CEL rendering is opt-in (`--high-res`) and validated for the
  Lemmings title; it is FreeDO-style line doubling, not true internal 640x480.
  Output upscaling via OpenGL is supported separately. See
  [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
- No GPU CEL compositing in v1; CEL rendering is software, matching FreeDO. The OpenGL renderer accelerates presentation, palette expansion, scaling, and filters.
- Mouse, light gun, arcade light gun, flightstick, and Orbatak trackball devices are
  implemented on the eight PBUS ports with an active-device cap of 0-8 and a 256-byte
  chain buffer; per-device calibration and scripted gameplay validation remain.
- No 3DO modem/network emulation.
- No cycle-exact ARM bus timing yet. The interpreter preserves FreeDOCore's `Ncycle = 4`; Opera uses `Ncycle = 2` with datasheet references. The difference is under evaluation and must not be changed by default until a differential test proves the impact.
- No guarantee of 4DO save-state compatibility.
- The primary renderer requires desktop OpenGL 3.3 or OpenGL ES 3.0/3.1 (see `docs/Performance.md`); systems without either use the CPU fallback.
- CHD is supported for CD/GD-ROM images and for flat 2048-byte-unit CHDs made from cooked ISOs; DVD/HDD/raw CHDs with other unit sizes are not 3DO discs.
- CHD decompression is pure managed code and may be slower than ISO on low-end arm64 devices; read-ahead/cache mitigates this.

---

## 10. Curated compatibility matrix

This is the validated showcase set. The full 241-title 4DO reference list and the
local collection matrix are in [`GameCompatibilityList.md`](GameCompatibilityList.md);
the unique-title 5DO/4DO/Phoenix comparison is in
[`CompatibilityComparison.md`](CompatibilityComparison.md).
Use this table when testing real discs. Record the disc hash/ID, BIOS, build, and result.

| Game | Region | Disc ID | BIOS | Build | Boot | In-game | Audio | Input | Notes | Opera reference |
|---|---|---|---|---|---|---|---|---|---|---|
| Lemmings | USA | `0be38b22` | `panafz1` | 0.0.0 | **Pass** | **Fun 4 gameplay via pure D-pad passcode (`HNLHCIOECW`) with save/load roundtrip** | Non-silent DSP output | **Works**: A types, B confirms, ten letters auto-confirm, D-pad moves the selection; entry is permuted across `0x96574-7B,7E-7F`; `docs/scripts/lem-fun4-dpad.txt` types the full code with only D-pad/A | Passcode `HNLHCIOECW` accepted (`Fun 4 / Level 1 / Save the Lemmings`); with the exact audio clock, gameplay at 23,000 frames is `PC 0x00083FE8`, hash `0x63C02DED`, and roundtrips (`PC 0x000230A4`, `0x4CA339CD` twice); 640x480 high-res title matches the reference | Not re-tested this pass |
| Space Ace | USA | `112b53d0` | `panafz1` | 0.0.0 | **Pass** | **Animated demo and interactive scene; X -> menu, A -> play; 15,000-frame save/load roundtrip** | Non-silent DSP output; 10 s peak 4096, RMS ~1.8k (mono duplicated) | **Works**: X returns to the title/menu, A starts the scene; P also changes state | Former frame-681 null-buffer runaway is gone; the 15,000 state roundtrips (`0x7E780D93` -> two 120-frame continuations at `PC 0x000284EC`, `0x70B16422`) | Demo intro 60 s, no runaway |
| Road Rash | EU-US | `d7672550` | `panafz1` | 0.0.0 | **Pass** | **3D race renders and runs past 60,000 frames; CD media-access pulse unblocks the old ~42k/~92k wait and reaches `BUSTED` -> race select -> new race; autopilot cycles race/wreck/menu to 100k** | Non-silent DSP output; 10 s peak 32768, RMS ~9.6k/8.8k, 378 clipped | **B accelerates, A brakes, D-pad steers, C attacks, P pauses**; `--autopilot road` adds close-traffic coast/steer, urgent preemption, emergency-brake steer hold, a throttle-only wreck/rider-run restart, and A pulses for post-race/start-line screens (`autopilotMenuAdvance`) | The CEL transposition fix removed the VRAM `AllocMem` error; the earlier "wall pin" was the race-over `BUSTED` screen. The old ~42k/~92k READ_ID-only wait is fixed; the remaining autopilot issue is finishing a full race without wrecking | Intro FMV, then a race |
| Wolfenstein 3D | USA | `3dfcdbbf` | `panafz1` | 0.0.0 | **Pass** | **Fresh game (FLOOR 1-1) and save/load roundtrip; A pulses pass the long fresh-game load** | Non-silent DSP output; 10 s peak 30743, RMS ~5.5k | **Works**: A opens the menu, D-pad/A navigate and fire; A every 300 frames passes character select | From a 12,000-frame demo state, A opens the preview menu; selecting a level starts a fresh game. `w3d-loadlong.state` + A reaches FLOOR 1-1 at 64,000 (hash `0x2F5B9864`) | Intro 60 s |
| Alone in the Dark | USA | `9f7d72bc` | `panafz1` | 0.0.0 | **Pass** | **In-game staircase/room with an upright character; Begin/Resume menu and intro; A pulses leave the intro wait; scripted room movement with save/load roundtrip** | Non-silent DSP output; 10 s peak 20617, RMS ~1.9k | **Works**: D-pad/A change the scene; `Up, Left, A, Down` reaches the menu, A starts the intro; A every 300 frames advances the cutscene; `Up, Left, A, Down, Right` moves the character in the room | The CEL transposition fix restored the character's upright orientation. The media pulse alone does not advance the intro; A input does. The 39,800 movement state roundtrips (`0x44C7576B` -> `0xE490C8C7` twice) | Intro/loading 60 s |
| Gex | USA/EU | `f73f7d08` | `panafz1` | 0.0.0 | **Pass** | **Attract/level renders at 12,000 frames (3D platform scene with HUD)** | Non-silent DSP output; 10 s peak 29733, RMS ~3.0k/3.1k, 0 clipped | Not yet scripted | 12,000-frame hash `0x6B8760C9`, `PC 0x000876E8`; 0/120-frame save-state roundtrips are deterministic | Not tested |
| Need for Speed | USA/EU | `9ec810c5` | `panafz1` | 0.0.0 | **Pass** | **Attract/menu renders at 12,000 frames (car FMV and menu)** | Non-silent DSP output; 10 s peak 29951, RMS ~5.3k/5.0k, 0 clipped | Not yet scripted | 12,000-frame hash `0x95730015`, `PC 0x0001055C`; roundtrips deterministic | Not tested |
| Super Street Fighter II Turbo | USA | `07c32f10` | `panafz1` | 0.0.0 | **Pass** | **Attract demo plays a CPU match (title, character intro, fight, ending) at 2,000-10,000 frames** | Non-silent DSP output; 10 s peak 21248, RMS ~1.6k/1.5k, 0 clipped | Not yet scripted | 12,000-frame transition hash `0x110E7DC5`, `PC 0x00010548`; the 4DO fix mask is `0x2`; roundtrips deterministic | Not tested |
| Total Eclipse | USA | `09c0efb1` | `panafz1` | 0.0.0 | **Pass** | **Intro/gameplay renders at 12,000 frames (spaceship over terrain)** | Non-silent DSP output; 10 s peak 16382, RMS ~2.8k (mono duplicated), 0 clipped | Not yet scripted | 12,000-frame hash `0x280E8346`, `PC 0x000106A4`; roundtrips deterministic | Not tested |
| Killing Time | USA (RE1) | `2ddbb4aa` | `panafz1` | 0.0.0 | **Pass** | **Title menu (START/OPTIONS/LOAD GAME) renders at 12,000 frames** | Non-silent DSP output; 10 s peak 32333, RMS ~5.6k/6.4k, 0 clipped | Not yet scripted | 12,000-frame hash `0xF23DE48C`, `PC 0x000A4128`; static menu; roundtrips deterministic | Not tested |
| Theme Park | USA | `33e5afe4` | `panafz1` | 0.0.0 | **Pass** | **Title/menu screen renders at 12,000 frames** | DSP runs, but the captured 10 s menu is silent (peak 0) | Not yet scripted | 12,000-frame hash `0x55B0AC11`, `PC 0x0002063C`; static menu; roundtrips deterministic | Not tested |
| Return Fire | USA | `076c742f` | `panafz1` | 0.0.0 | **Pass** | **Menu screen (controller/map panels) renders at 12,000 frames** | Non-silent DSP output; 10 s peak 29765, RMS ~2.6k (mono duplicated), 0 clipped | Not yet scripted | 12,000-frame hash `0xD57E56D7`, `PC 0x000284F4`; static menu; roundtrips deterministic | Not tested |

`Pass` means a real game frame was rendered and verified visually. The Opera reference
column is the bundled Windows OperaCore with the same BIOS and an ISO extracted from
the same CHD; it verifies game progress, not audio output. The DSP/CCB/NVRAM and CEL
transposition fixes are described above; input results come from `--input-script` runs.

### 10.1 Input validation (2026-10-24)

- **Alone in the Dark:** from the 16,000-frame in-game state, `Up`/`Down` and
  `Left`/`Right`/`A`/`B`/`C` produce different hashes and PCs; the D-pad/A sequence
  reached the Begin/Resume main menu. The control run walks the character through the
  room, so the in-game scene is live without input. From the intro, A every 300
  frames leaves the static wait and reaches the room/mansion/title scenes (29,200
  frames, `PC 0x00020640`, hash `0x62304BCF`). From `alone-intro-A2.state`
  (39,200), `Up, Left, A, Down, Right` moves the character in the room; 39,800
  is `PC 0x0008D0EC`, hash `0x44C7576B`, and the state roundtrips
  (`0x44C7576B` -> two 120-frame continuations at `PC 0x0008BF00`,
  `0xE490C8C7`).
- **Lemmings:** the title screen shows Play/New Game, but `A`/`Left`/`Right`/`P`/`X`
  do not leave the attract loop. `Down` + `A` opens the passcode screen ("Enter the
  access code"). The keyboard is a 9-column grid of which five columns are visible;
  `A` types the selected letter, `B` confirms early, and ten letters auto-confirm.
  A 15-frame D-pad hold moves the selection one key. A wrong code shows "Incorrect"
  and returns to the title. The 10-letter entry is permuted across RAM
  `0x96574-0x9657B` + `0x9657E-0x9657F` (display order `77,76,75,74,7B,7A,79,78,
  7F,7E`; `0x96576` is the preview slot refreshed from the selection). From the
  `N`-selected state, poking the other nine bytes of `HNLHCIOECW` (Fun 4) and
  pressing `B` is accepted (`Code for 1` / `Rating = Fun`), then `A` shows the
  briefing and `A` starts gameplay (21,640 frames, `PC 0x83FEC`, hash `0x18F38B4D`;
  zero-frame load roundtrips, two 120-frame continuations both `0x8D628FBD`). A
  sequential poke at `0x96574` alone renders scrambled and reports `Incorrect`.
- **Road Rash:** the attract race renders correctly from ~17,000 frames. The 3DO
  controls are B accelerate, A brake, D-pad left/right steer, C attack, and P pause.
  Holding B starts a player race; a hard left around frame 31,000 clears the first
  oncoming taxi. `--autopilot road` v9 steered to the road centre measured low
  (rows 190-214) with lookahead obstacle steering, closing-side crossover,
  sharp-bend braking, wall avoidance, emergency braking, urban throttle pacing, a
  coast floor, and stuck recovery. The previously reported terminal wall pin at
  ~34,000 frames is the race-over **`BUSTED`** screen: from `rr-auto-v8.state`,
  no-input plus a `B` proves the plainclothes-cop screen, and no input resumes a
  lost race. v10 adds a close-traffic coast/steer rule (the 33,820 racer filled
  the centre while every old rule required a 0.6-wide escape), urgent preemption
  only for close traffic (applying every obstacle bias immediately oversteered
  into a taxi at ~32,000), an emergency-brake steer hold (the old path chased a
  lineX jump into the 33,890 tree), and a stopped/wreck restart that holds B with
  straight bars for 180 frames based on a left-scenery + speed-readout signature.
  The restart recovers the rider-running phase (34,500/80,000 -> 52 mph) and wall
  stops, but a cyclic right/left search was tried and drove the 83,000 run into
  `BUSTED` (`0x9F44FAA2`), so throttle-only is the proven policy. Best evidence:
  91,000 live frames (`PC 0x00085F48`, hash `0x02ABE78E`, `cov=0.93`), zero-frame
  `0x02ABE78E`, two 120-frame continuations both `PC 0x000973EC` / `0x3F27B3D6`.
  The ninth pass fixed the old ~92,000/~42,000 stop: the CD device raises a
  media-access event (`0x40`) after two consecutive READ_ID polls with an idle
  gap. The game advances to `BUSTED`, the race-select menu, and a new race
  (`PC 0x0007DCBC`, hash `0x277CCED7` at 113,000). The five 3,000-frame keys
  were later re-baselined by the exact DSP clock; see
  [`TestCorpus.md`](TestCorpus.md) 1.1. The autopilot pulses A/releases throttle during long-static,
  low-coverage, CD-load, and 180-frame restart-stuck states
  (`autopilotMenuAdvance`). From the 60k state it starts a new race at 80,000
  (`PC 0x00092FF8`, hash `0x1074EF3F`) and cycles race/wreck/menu to 100,000
  (`PC 0x0008214C`, hash `0x0D46CF56`). The remaining work is finishing a full
  race without wrecking, not the old CD wait or another side search.
- **Wolfenstein 3D:** from the 12,000-frame demo state, `A` opens the Select Previews
  menu; D-pad/A navigate to Select a Level and start a fresh game at FLOOR 1-1 with
  `SCORE 0000000`. From a live 60-frame state, two independent 120-frame continuations
  both end at hash `0x370158AC`, and a zero-frame load reproduces the saved hash
  `0xC9989182`. The long fresh-game load (`w3d-loadlong.state`) stays static with no
  input, but A every 300 frames reaches character select and FLOOR 1-1 at 64,000
  (`PC 0x00069F3C`, hash `0x2F5B9864`).
- **Space Ace:** from a 12,000-frame state, `X` at 12,000 returns to the title/menu
  (`PC 0x0000E12C`, hash `0x596B6A81`); `A` at 12,200 starts the interactive scene.
  The run reaches 15,000 frames (`PC 0x000765F0`, hash `0x7E780D93`), the state
  roundtrips, and two independent 120-frame continuations both end at
  `PC 0x000284EC`, hash `0x70B16422`. `P` also changes state; `A`/`B`/`C` and the
  D-pad do not change the demo by themselves.

### 10.2 Renderer fixes and validation (2026-10-24)

- **Arbitrary CEL transposition.** `TexelDrawArbitrary` passed `(i, j)` to the
  `(x, y)` read/write helpers. FreeDO's `readPIX(src, i, j)` treats `i` as the row, so
  the port's renamed helpers inverted every texel. Asymmetric quads were written
  transposed. Road Rash's race CELs landed in the VRAM allocator's free list at
  `0x2FC000`, producing `AllocMem error, bad node address` and a frozen frame; Alone in
  the Dark drew its character horizontally. The fix passes `(j, i)`.
- **VDLP line width.** The bitmap copy always read 320 pixels, so 384/512/640-wide
  modes were cropped. It now uses the modulo parsed from the VDL control word, and
  the machine derives `SourceWidth` from the widest rendered line. The 320-wide corpus
  output is unchanged.
- **Save-state pixels.** Loading a state now restores the VDL pixel buffers instead of
  discarding them. A zero-frame load reproduces the saved frame hash exactly.
- **Audio measurements.** 600-frame WAV captures: Road Rash 44.1 kHz stereo, 10.03 s,
  peak 32768, RMS 9051/9146, 191 clipped samples; Wolfenstein 44.1 kHz stereo, 10.02 s,
  peak 30743, RMS 5620/5457, no clipping. Both run at ~737 samples/frame.

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

The third root cause was the CLI's NVRAM. A zero-filled NVRAM has no valid volume
header, so Alone in the Dark's save-space check saw zero free slots and displayed
its "There is only enough space on your 3DO system to save X games" warning. After
a timeout it entered its shutdown routine and spun on `VCNT`. Loading a fresh
32 KB NVRAM volume (the same image the desktop frontend already uses) removes the
warning and the game reaches its in-game scene. The CLI also gained `--nvram` to
persist the image.

**Opera plays the same titles past these points**, and the C# port now does too.
The next compatibility step is input-driven gameplay validation, not another DSP or
CD rewrite.

### 10.2 Release soak (1.0.0-rc.1, 2026-10-26)

Fresh 36,000-frame runs (10 minutes of emulated time) with `panafz1.bin`, Release
build, and no input; all exited 0 with the DSP running:

| Title | PC | Frame hash |
|---|---|---|
| Wolfenstein 3D | `0x0007C9C4` | `0x0814F039` |
| Lemmings | `0x0012DA2C` | `0x9939D345` |
| Road Rash | `0x00086A84` | `0xBDC2DBEA` |
| Space Ace | `0x00076560` | `0xFD0F5B72` |
| Alone in the Dark | `0x00020640` | `0xBA7F2C8C` |
| Theme Park (ISO) | `0x00048CF8` | `0x55B0AC11` |

The seven additional titles' source discs are not present on this machine, so the
release soak continued their archived 12,000-frame states to 36,000. All exited 0:

| Title | PC | Frame hash |
|---|---|---|
| Gex | `0x0004879C` | `0xDCE31DC5` |
| Need for Speed | `0x000189F4` | `0x0B4A8885` |
| Super Street Fighter II Turbo | `0x00020640` | `0x110E7DC5` |
| Total Eclipse | `0x000C5E68` | `0xC2C0E8D2` |
| Killing Time | `0x0009967C` | `0xF23DE48C` |
| Theme Park | `0x00048CF8` | `0x55B0AC11` |
| Return Fire | `0x000284F4` | `0xD57E56D7` |

Save/load is deterministic at the 36,000-frame states: the zero-frame load
reproduces the live hash, and two independent 120-frame continuations match.
Showcase examples: Wolfenstein `0x0814F039` -> `0x2853ADC5` twice,
Lemmings `0x9939D345` -> `0xB069CA91` twice, Road Rash `0xBDC2DBEA` ->
`0xA41DD4CD` twice, Space Ace `0xFD0F5B72` -> `0x2A9967FA` twice, and Alone
`0xBA7F2C8C` -> `0x666EEDB7` twice. The 600-frame WAV captures are 44.1 kHz
stereo and non-silent (Road Rash clips 74 samples in 10 s; Wolfenstein and
Lemmings do not clip).

Scripted gameplay evidence: Wolfenstein's fresh-boot path reaches FLOOR 1-1
(`0xF568EBCA` at 36,000 after the documented input scripts), Lemmings' archived
23,000-frame gameplay state runs to 36,000 (`0xD3961D3D`), and Road Rash's
60,000-frame race state runs to 80,000 with `--autopilot road`. Space Ace's
15,000-frame interactive state renders the scene; a long no-input continuation
reaches a black wait screen, which is documented in
[`KnownIssues.md`](KnownIssues.md).

### 10.3 Linux x64 validation (2026-11-02)

Validated on the local Hyper-V `LinuxMint` VM: Linux Mint 22.3, x86_64, kernel
`7.0.0-34-generic`, Mesa 25.2.8 `llvmpipe`, OpenGL 4.5 core / GLES 3.2, 1920x1080.
The guest has no .NET SDK; the self-contained `linux-x64` publish was copied into the
VM and run from `/home/vm/linux-smoke`.

| Check | Result |
|---|---|
| `5do --version` | `5DO 1.0.0-rc.1` |
| Full test suite | `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`: **1,473/1,473 passed** on Linux Mint 22.3 with .NET SDK 10.0.301 (plus the .NET 8 runtime required by the shader compiler) |
| `selftest --json` | 4/4 checks true (deterministic frame, non-empty frame, state roundtrip, no state warnings) |
| `bench --frames 120` | 466 fps, frame hash `0x6CDFBDC5` |
| `info "Wolfenstein 3D (USA).chd"` | 56,702 sectors, game ID `3dfcdbbff14ffb59e157e27f18d9df7b`, known, Interplay 1994 US |
| `bios info panafz1.bin` | MD5 `f47264dd47fe30f73ab3c010015c155b`, Panasonic FZ-1, USA NTSC |
| 3,000-frame run | 6.21 s, 320x240, PC `0x00028190`, frame hash `0x9A3B5131`, 2,205,093 samples |
| Audio | 50.0 s WAV, peak 29,951, RMS 4,808, 0 clipped frames, DSP running |
| Frontend `--smoke-test` | `FRONTEND SMOKE OK 1.0.0-rc.1`; OpenAL ready (44.1 kHz stereo) |
| GL probe | Timed out (backend null, 0 frames) under the VM's SSH/llvmpipe session; Windows WGL/ANGLE remains the validated GL path |

The 3,000-frame Wolfenstein capture shows the Interplay logo (non-black), confirming
disc boot and rendering on Linux x64. The full test suite also passes. Linux x64 is
shipped as a **preview/best-effort** build: the CLI, frontend launch, and audio path are
validated, while GPU presentation should be re-tested on a hardware-accelerated Linux
desktop before claiming full GL support. See [`KnownIssues.md`](KnownIssues.md).

**2026-11-08 compatibility-pass note:** the Windows x64 1.5.0 bundle extracted and
passed both `FiveDo.Cli.exe --help` and `FiveDo.Frontend.Avalonia.exe --smoke-test`
(OpenGL 4.0 + OpenAL ready). The Linux rerun could not start the local 8 GB `LinuxMint`
VM because the host could not allocate the memory; no runtime code changed in the
compatibility pass, so the 2026-11-02 Linux x64 gate above remains the current Linux
evidence. Re-run the Linux gate on a host with enough free memory before a new release.
macOS remains build-validated and best-effort.

## 11. Reference comparison (2026-10-23, updated 2026-10-24)

The data track of each CHD was extracted to ISO with `5do disc dump`, then run with
the bundled Windows OperaCore (`References/CPP_SimpleGui`) and the same
`panafz1.bin` BIOS. Opera rendered Road Rash, Wolfenstein, Alone, and Space Ace for
60 s. After the 2026-10-24 DSP fixes the C# port also renders all five titles and
produces non-silent audio. The comparison method and commands are in
[`ReferenceStrategy.md`](ReferenceStrategy.md); the compatibility completion plan and next actions are in
[`ImplementationPlan.md`](ImplementationPlan.md).

The 2026-10-24 pass added the diagnostics needed to make the comparison mechanical:
`--watch-lowmem`, `--dump-ram`, `--dump-dsp`, `--dsp-trace`, the `disasm` command,
frame-stamped CLIO traces, DSP state in the run JSON, and `--input-script` for
repeatable controller taps. The DSP trace and memory dumps identified the exact
`Sema4Status & 8` test that was being mis-decoded.

**2026-11-06 differential oracle.** `FiveDo.DiffHarness` now applies a fixed seed
to both engines and compares ARM PC/CPSR and the CD command sequence, not just
frame hashes. The five-title panel is deterministic across runs. At 3,000 frames
Lemmings matches the reference frame hash at 60/300/1000/3000; Road Rash and
Space Ace still show different intro animation frames/scenes, and a ±60-frame
window finds no matching neighboring frame. The first command-sequence divergence
is a READ_DATA (`0x10`) versus READ_ID (`0x83`) transition (Lemmings command 105,
Road Rash 559, Wolfenstein 389). These are tracked as `CORE-04`, `CORE-07`, and
`CORE-13` in [`ImplementationPlan.md`](ImplementationPlan.md); details are in
[`DifferentialHarness.md`](DifferentialHarness.md) §8 and
[`TestCorpus.md`](TestCorpus.md) 1.1aw.

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

---

## 13. Full-collection sweep (2026-10-30)

The local collection is:

- `J:\Panasonic 3DO` — 589 CHD images.
- `G:\MAME\MAME Software List CHDs\3do` — 5 CHD images.

The sweep is automated by `tools/compat-sweep.ps1`; the reference table is
[`GameCompatibilityList.md`](GameCompatibilityList.md), and the procedure/status
definitions are in [`CompatibilitySweep.md`](CompatibilitySweep.md).

Automated labels:

- `Not Loadable` — CLI error, timeout, invalid JSON, or a short frame count.
- `Loadable/No video` — completed the frame count but the final frame is black/blank.
- `Loadable/Rendered` — completed and the final frame is non-black with several colors.

Final labels (after visual/input review): `No data`, `Not Loadable`, `Unplayable`,
`Playable`, `Good`.

This sweep is the current compatibility gate. It is deliberately broader than the
12-title matrix above and does not require full gameplay completion. The 4DO list is
used to prioritize known problem titles, not to assume that a filename match is the
same disc revision.

### 13.1 First sweep results

The first 594-disc pass completed on 2026-10-30 and found 576 rendered, 13
black/blank, and 5 not loadable. The 2026-11-01 extended-boot retry reran all 18
non-rendered rows; the table below reflects the retried counts:

| Result | Count |
|---|---:|
| `Loadable/Rendered` | **593** |
| `Loadable/No video` | **1** |
| `Not Loadable` | **0** |

The per-disc table is [`GameCompatibilityResults.md`](GameCompatibilityResults.md).

### 13.2 Triage results (2026-10-30, extended 2026-11-01)

The baseline used `panafz1.bin`. Reruns with the other local BIOS images show that
several failures are BIOS-selection problems rather than emulator defects:

| Disc | `panafz1` | `panafz10-patched` | Anvil BIOS | Result |
|---|---|---|---|---|
| Crime Patrol (Arcade CD-ROM) | starfield / no game | 3,000 frames, live-action | not needed | Loadable with `panafz10-patched` |
| Mad Dog II (Arcade CD-ROM) | starfield / no game | 3,000 frames, live-action | not needed | Loadable with `panafz10-patched` |
| Orbatak (Arcade CD-ROM) | starfield / no game | 3,000 frames, gameplay | not needed | Loadable with `panafz10-patched` |
| Shootout at Old Tucson RE3 | starfield / no game | 3,000 frames, text screen | not needed | Loadable with `panafz10-patched` |
| Mirai Shounen Conan | transient CHD read error; 3,000 frames now stable | not tested | not tested | Loadable with `panafz1` |
| Crash 'n Burn (Japan) | invalid PC / black | invalid PC / black | purple boot screen, still stuck at 65,000 | Japan revision stuck; USA/Korea revisions render with `panafz1` |
| Powers Kingdom (Europe) | solid blue | solid blue | live-action intro by 65,000 | Anvil BIOS required, matching the 4DO note |
| Battle Chess (Europe) | "This is not a PAL TV system!" | same message | title screen at 6,000, board at 30,000 | European BIOS required; USA/Japan discs use `panafz10` |
| Slayer (USA) | black | title menu at 60,000 | black | `panafz10`/GoldStar/Sanyo required |
| WorldCup Super Stadium (Japan) | solid blue | solid blue | renders the "Story of the World Cup" screen with `panafz10j` + Kanji font (BIOS2) | Needs the Kanji font ROM as BIOS2; the library now classifies 1 MB Kanji images as secondary |
| Olympic Summer Games (USA) | "Insert a controller" | not tested | not tested | Fixed by the default headless pad; high-jump screen and A/P input reach the credits |

The Arcade CD-ROM rows need the **patched** BIOS; unpatched `panafz10` reaches the
3DO storage-manager shell instead of the game. The default pad alone does not fix
them, and the patched BIOS alone is not enough either: the headless PBUS fix is what
lets their boot code proceed past the controller probe.

Slayer has the opposite regional split: the Europe disc renders its credits with
`panafz1` by 12,000 frames, while the USA disc stays black with `panafz1` and needs
`panafz10` to reach the title menu at 60,000 frames.

Battle Chess (Europe) is the first confirmed disc that rejects the NTSC system
software outright. It prints "This is not a PAL TV system!" and halts at
`PC 0x00088EDC` with `panafz1`, `panafz10`, and the GoldStar/Sanyo BIOSes. The
European Anvil BIOS (`panafz10e-anvil.bin`) satisfies the region check: the title
screen appears at 6,000 frames and the board at 30,000 frames. 5DO still emits NTSC
320x240 timing, so this is a region/OS check, not proof of PAL 50 Hz emulation. The
USA and Japan discs boot with `panafz10`.

The 2026-11-01 retry reran all 18 non-rendered rows. At 20,000 frames with `panafz10`,
Ishida Yoshio Kudan no Igo Seiha, both Konpeki no Kantai discs, all three Myst discs,
Olympic Summer Games, all three Slayer discs, the four Arcade CD-ROM titles, and Mirai
Shounen Conan render. Powers Kingdom (Europe) renders its live-action intro at 65,000
frames with `panafz10e-anvil`. Only `Crash 'n Burn (Japan)` and `WorldCup Super
Stadium (Japan)` remain on the Anvil boot screen at 65,000 frames. The final counts
are 593 rendered and 1 known failure (Crash 'n Burn Japan); WorldCup Super Stadium
renders with the FZ-10J BIOS and its Kanji font ROM, and no title is `Not Loadable`.

Consequences for the sweep:

- The four Arcade CD-ROM rows are not timeouts. They are loadable with the patched
  BIOS plus the default headless pad, and should be recorded with that BIOS in their
  notes.
- The `Loadable/No video` set is not a stable failure class. Some titles need 20,000
  frames, and some need a different BIOS. The retry used `-RetryFrames 20000` in
  `tools/compat-sweep.ps1`; do not convert a row to `Unplayable` without a
  BIOS-aware extended-boot rerun.
- The final 5DO status must record the BIOS used, because a result is only meaningful
  for the BIOS that was tested.

The SYS-04 BIOS selector encodes the actionable part of this triage: arcade discs get
the patched FZ-10 image, European games prefer the FZ-10E Anvil image, Japanese games
prefer the FZ-10JA/Kanji image, and Slayer Europe/USA plus Powers Kingdom Europe have
explicit known requirements. `bios select` prints the reason for each choice, and
`run --json` records it in `biosSelection`. The full BIOS-aware sweep is still the
acceptance step; the selector does not claim a title is compatible merely because a
BIOS was chosen.

### 13.3 Beyond-title completion (2026-11-08)

The compatibility plan replaced the old quality-rank comparison with a binary
beyond-title rule. A title is Playable when it reaches a distinct menu, options,
character-select, gameplay, or FMV/content screen; a title/attract loop that never
leaves is not Playable. `tools/beyond-title-sweep.py` runs each canonical disc with
auto BIOS selection, a generic input script, capture checkpoints, and a no-input control
run. Exact checkpoint frame hashes prove whether scripted input changed the machine.

The full 378-game test set and the 147-title peer gap were run on 2026-11-08. The
per-title BIOS, disc hash, capture frame, screenshot, and failure reason are in
[`BeyondTitleEvidence.md`](BeyondTitleEvidence.md) and
`docs/data/beyond-title-results.csv`. The 5DO/4DO/Phoenix matrix in
[`CompatibilityComparison.md`](CompatibilityComparison.md) is generated from that
ledger. The machine-readable regression gate is
`docs/data/beyond-title-baseline.json`; `tools/run-beyond-title-gate.ps1` runs the full
pass and fails on a Playable regression.

Format checks: Night Trap Disc 1 was swapped to Disc 2 at frame 3,600 and completed
7,200 frames with the Disc 2 hash; the final capture is live-action video. Audio-only
CD-DA images reach the 3DO CD player but the track list is empty and the WAV capture is
silent; this is the documented SYS-01 CD-DA read-ahead/audio-only-TOC residual, not a
beyond-title blocker.
