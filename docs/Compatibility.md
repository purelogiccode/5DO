# Compatibility Notes

This document tracks what the emulator intends to support, what it deliberately does not support yet, and the game/BIOS quirks inherited from FreeDOCore/4DO. New compatibility work uses `References/opera-libretro-master/libopera` as the primary behavioral reference; see [`ReferenceStrategy.md`](ReferenceStrategy.md).

**Current release (2026-11-28):** v1.20.0 adds the emulator-agnostic VPS lobby prototype:
room/join codes, WebSocket signaling, TCP control relay, UDP punch/relay, the
`5do netplay --lobby` CLI, and `FiveDo.LobbyProbe`. The public service at
`https://lobby.purelogiccode.com` was validated end to end on 2026-11-27; see
[Lobby.md](Lobby.md) §7.4. The 1.19.0 direct-IP netplay and the 1.18.x feature set remain:
read-only Alcohol MDS/MDX and CloneCD CCD/IMG/SUB images, optional H.264/AAC MP4 recording
with Low/Medium/High quality, one-click FFmpeg installation, high-refresh frame
interpolation, patterned GPU void bars, and live CPU-clock changes. It retains the 1.17.x
rolling autosave with validated startup resume (UX-37), direct ZIP/7z ISO, BIN, and
CUE+BIN containers (SYS-27), the `disc audio`/`disc verify` diagnostics, physical CD-DA and
read-error recovery, and the Steam Deck first-run setup. The default 320x240 compatibility
path and the documented title matrix are unchanged.

**Earlier position (2026-10-29, twenty-first pass):** all five golden-corpus titles boot,
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
| **DiscJuggler CDI** | **Current (2026-11-15)** | `CDISharp` parses the trailing descriptor, session/track table, and 2048/2336/2352-byte layouts, including 16-byte PQ and 96-byte P-W subchannel strides (2368/2448). The 150-sector lead-in is skipped when present. |
| **Nero NRG** | **Current (2026-11-15)** | `NRGSharp` parses NERO/NER5 footers, DAOI/DAOX and TINF/ETNF/ETN2 chunks, and per-track sector sizes 2048, 2324, 2336, 2352, 2353, 2368, 2448, and 2449. The extra error byte and subchannel tails are ignored when extracting user data. |
| **Alcohol MDS/MDF/MDX** | **Current (2026-11-26)** | `MDSSharp` parses the Alcohol descriptor, v2/MDX containers, split volumes, per-track pregaps, and compressed/encrypted track data (password-protected images require the password). The adapter prepares a CUE and opens it through `CueDiscImage`; plain raw/cooked images are referenced in place. |
| **CloneCD CCD/IMG/SUB** | **Current (2026-11-26)** | `CCDSharp` parses `[CloneCD]`, `[Disc]`, `[Track]`, and TOC-only descriptors, including INDEX 00/02+, FLAGS, ISRC, and catalog data. The raw `.img` is referenced by a generated CUE and opened through `CueDiscImage`; the `.sub` file is optional and ignored because the 3DO CD-ROM device does not need subcode. Scrambled data tracks are rejected. |
| Multi-track / CD-DA | Partial (2026-11-22) | Track table, raw CD-DA sectors, PLAY_AUDIO position, DSP mixing, persisted volume/mute, READ_DATA subcode/Q packing, M3U multi-disc swap, and physical-drive TOC/raw CDDA (v1.16.0) are implemented; the raw CDDA path is verified on real hardware (v1.16.1), and only the FZ-10 audio-only BIOS transport remains (SYS-01) |
| **ZIP/7z (ISO, BIN, CUE+BIN)** | **Current (2026-11-21)** | `ArchiveDiscImage` opens one ISO/BIN or one CUE+BIN set directly from a ZIP or 7z entry; entries are decompressed in memory (1.5 GB per-entry limit) through SharpCompress. CHD/CDI/NRG/MDS/CCD must be extracted; encrypted or multi-volume archives are rejected with guidance. |

Sector count is read from the 3DO volume header at sector 0 (big-endian `blockCount` at byte offset 80). If the header is invalid, fall back to `fileSize / sectorSize` for ISO/BIN, the CHD data track's frame count, or the CDI/NRG/MDS/CCD track table.

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
- DVD/HDD/raw CHDs with other unit sizes are rejected. Audio-only CHDs open with a
  TOC-derived ID and a track table but no 3DO data track; 3DO software is CD-ROM based.
- Read-ahead is enabled by default (4 hunks) for sequential streaming.

ZIP/7z specifics (2026-11-21):

- `ArchiveDiscImage` reads ZIP and 7z entries through SharpCompress, caches each
  uncompressed entry in memory, and presents it as an `IDiscSource`/`ITrackedDiscSource`.
  The per-entry limit is 1.5 GB; larger images must be extracted.
- The archive may contain one `.iso`, one `.bin`, or one `.cue` with its BIN files. A CUE
  resolves `FILE` entries relative to the CUE entry, including subfolders. Multiple CUEs
  or multiple images without a CUE are rejected as ambiguous.
- CHD, CDI, and NRG entries are rejected with extraction guidance because those
  containers already have their own compression/random-access layout.
- Encrypted entries/archives and multi-volume archives fail with an actionable message;
  they are never partially read.
- The desktop library, drag/drop, recent discs, file picker, playlist editor, and CLI
  (`run`, `info`, `disc ...`, `fs ...`) accept the archive path directly.
- Parity evidence: Theme Park (`33e5afe454a7c4da20b419a09e2045e2`) produced the same
  600-frame hash `0x110E7DC5`, PC `0x0000596C`, and identical PNG SHA-256 when run from
  the extracted ISO, a ZIP containing that ISO, and a 7z CUE+BIN set.
- `ArchiveDiscImageTests` covers ZIP ISO reads, 7z CUE+BIN data/audio tracks, nested CUE
  paths, multiple-image and CHD rejection, PREGAP rejection, encrypted archives, and
  unrelated entries. `GameLibraryScannerTests` covers library discovery.

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

The game database is derived with attribution from the 4DO project and is distributed as a separate GPL-2.0 data component; the complete text is in [`GPL-2.0.txt`](GPL-2.0.txt). Unknown game IDs get the first 8 hex characters of the MD5, with collision avoidance, matching 4DO. `GameCompatibility` applies the mapping automatically when the CLI or desktop shell opens a disc; `--fix-mode`/`--no-game-fixes` and the per-game override in Settings provide explicit control. The 3,000-frame Alone boot hash is unchanged with `FIX_BIT_TIMING_6` applied.

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
| Optional Vulkan renderer | Native Win32/X11/Metal swapchain for the shell and `--vk-window-probe`, offscreen `--vk-probe`; accurate/nearest/sharp-bilinear/bicubic/CRT/xBR/FSR/HQx/MMPX; FIFO vsync | Mailbox/immediate as a user setting |
| GPU palette expansion | Yes (exact CPU parity in Accurate mode) | |
| 4:3 aspect ratio, letterbox | Yes | |
| Nearest / integer scaling | Yes | |
| Sharp-bilinear | Yes (default enhanced) | |
| CRT filter | Yes (opt-in) | |
| xBR 4x / HQx 2x/3x/4x filters | Yes (opt-in; all GPU passes) | |
| 1080p/1440p/4K output | Yes (GPU scaling); 4K60 on Tier A hardware | |
| Fullscreen | Yes | |
| 384/512/640/1024-wide VDLP modes | Parsed, may crop | Full presentation |
| High-resolution output | Opt-in `--high-res` (presentation-only 2x nearest upscale to 640x480) or `--true-2x` (experimental independent 480-line CEL fields) | Bitmap/SPORT high-resolution sampling and full-collection validation remain VID-01 research |
| PAL | Yes: PAL1 320x288 and PAL2 384x288 at 50 Hz, with per-game BIOS selection | |
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

`EmulatorTuning.HighResolution` / `--high-res` is an opt-in 640x480 **presentation**
mode. The guest machine remains byte-identical to native 320x240: CEL coordinates,
VRAM layout, CPU-visible bitmap reads, and SPORT behavior do not change. When each
frame is assembled, `Vdlp` samples each native line to the region's double-width
frame (640 pixels for NTSC/PAL1, 768 for PAL2) and emits it twice, so the presented
frame is a clean 2x nearest-neighbor enlargement for native-width modes. Wider VDLP
modes (384/512/640/1024) are resampled into that frame instead of being cropped.

This makes the mode safe for every title: 2D bitmaps, bitmap video, packed CELs, and
3D arbitrary CELs all follow the native path. The earlier FreeDO-style implementation
changed the guest-visible VRAM/CEL/SPORT layout and could crop 2D CEL content to the
left half, leave stale planes on video lines, or hang titles when gameplay started; it
is no longer used by `--high-res`. A pixel-perfect comparison on 20th Century Video
Almanac, Road Rash, Dragon's Lair, and the Alone in the Dark demo shows a 0-pixel
difference between the 640x480 high-resolution frame and a 2x nearest upscale of the
native frame at the same emulated PC. The OpenGL presenter also uploads the two-word
per-line parameter texture as `RG32UI` now; the old `RGBA32UI` upload overran the
480-line array and produced the bottom rainbow rows.

**Important limitation:** the legacy `--high-res` path is an output enlargement, not
true internal 640x480 rendering. Vertical detail is still 240 lines. For experimental
guest-visible 640x480 CEL fields, see the opt-in **`--true-2x`** path below.

The opt-in **`--true-2x`** path (`EmulatorTuning.TrueHighResolution`, Settings >
Emulation > **True 2x CEL fields**, and per-game **TrueHighResolution**) is the
experimental internal path. It keeps the four-plane layout but writes even output lines
to planes 0/1 and odd output lines to planes 2/3. Arbitrary CEL geometry evaluates at
640x480 and packed/scaled 2D CELs expand to 2x2 blocks, so the VDLP presents independent
480-line CEL fields. Bitmap and SPORT content still follows the line-doubled layout.
The mode is validated on Jurassic Park Interactive's Raptor Maze (38.41% of pixels
differ from a 2x nearest upscale) and Family Feud (0.00% difference, a clean 2x scale).
It is available for every title now; unvalidated titles are experimental and may
misrender bitmap/SPORT or framebuffer-read content. Native output and the
presentation-only legacy mode are unchanged. True 2× costs about 30% more emulation
time on the Raptor Maze and remains opt-in.
See [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md) for the remaining
VID-01 production work (bitmap/SPORT sampling, framebuffer-read semantics, and a
full-collection sweep).

Evidence: from a fresh Lemmings boot, the 640x480 title screen shows all four buttons
and the full logo and matches the reference screenshot; the five 3,000-frame low-res
corpus hashes were unchanged by high-res mode (the current keys are in
[`TestCorpus.md`](TestCorpus.md) 1.1). The mode is off by default because a 320x240
title should not silently switch its internal CEL coordinates. The Lemmings passcode
screen is a 320-wide UI scaled 2x; its five visible keyboard columns are the game's own
window, not a framebuffer crop, so it does not need a separate high-resolution input
path.

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
| AOT live audio | Validated for the win-x64 NativeAOT CLI and frontend with the bundled runtime; frontend null/real soaks report 0 drops/0 underruns | Linux/macOS AOT audio is not claimed |
| CD-DA | Partial | Raw 2352-byte audio sectors from images and host drives, PLAY_AUDIO position, 44.1 kHz DSP mixing, persisted volume/mute, READ_DATA subcode/Q packing, and Settings/CLI controls; audio-only BIOS playback remains blocked by the guest player |
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
- 4DO `.4dosav` import/export is **best-effort** and implemented through
  `state import-4do` and `state export-4do`. The native `.tdos` format remains the
  supported format; native 4DO state compatibility is not guaranteed. See
  [`SaveStates.md`](SaveStates.md) §9.

---

## 9. Known limitations / out of scope for v1

- Netplay is in progress (`NET-01`): the loopback probe passed, but no network mode
  ships yet.
- Rewind, fast-forward, cheats, TAS movies, OSD/quick menu, multi-track/CD-DA
  sectors, CD-DA volume/mute, READ_DATA subcode/Q packing, and multi-disc playlists
  are implemented; audio-only BIOS playback, guest ODE prompts, and known-title disc
  audio evidence remain.
- PAL1/PAL2 are implemented and the Europe-only matrix titles have dated captures in
  [PalValidation.md](PalValidation.md).
- High-resolution output is opt-in. The legacy `--high-res` path is a presentation-only
  2x nearest upscale and is validated for 2D, video, and 3D titles; `--true-2x` adds
  independent 480-line CEL fields and is validated on Jurassic Park Interactive's Raptor
  Maze and Family Feud. Bitmap/SPORT content remains line-doubled in true 2×. Output
  upscaling via OpenGL is supported separately. See
  [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
- No GPU CEL compositing in v1; CEL rendering is software, matching FreeDO. The OpenGL renderer accelerates presentation, palette expansion, scaling, and filters.
- Mouse, light gun, arcade light gun, flightstick, and Orbatak trackball devices are
  implemented on the eight PBUS ports with an active-device cap of 0-8 and a 256-byte
  chain buffer; per-device calibration and scripted gameplay validation remain.
- No 3DO modem/network emulation.
- The ARM bus model keeps FreeDOCore's `Ncycle = 4`; Opera uses `Ncycle = 2` with
  datasheet references. `CORE-01` closed that comparison after the panel showed a
  frame-boundary phase difference rather than a game-visible cycle-model gap. Do not
  change the default without a new differential test.
- No guarantee of 4DO save-state compatibility.
- The primary renderer requires desktop OpenGL 3.3 or OpenGL ES 3.0/3.1 (see `docs/Performance.md`); systems without either use the CPU fallback.
- CHD is supported for CD/GD-ROM images and for flat 2048-byte-unit CHDs made from cooked ISOs; DVD/HDD/raw CHDs with other unit sizes are not 3DO discs.
- CHD decompression is pure managed code and may be slower than ISO on low-end arm64 devices; read-ahead/cache mitigates this.
- CDI images are accepted only when the trailing descriptor passes the DiscJuggler signature checks; NRG images require a valid NERO/NER5 footer and a known first metadata chunk. MDS/MDX images are parsed by MDSSharp and CloneCD descriptors by CCDSharp. All four readers are format parsers, not burners, and do not write CDI/NRG/MDS/CCD output.
- MDS images with encrypted track data need the image password when they were not encrypted password-less; 5DO reports the preparation failure with the MDSSharp reason. CloneCD scrambled data tracks are rejected with a descramble message. The `.sub` subchannel file is optional and ignored.
- Audio-only CDI/NRG/MDS/CCD images open at the library layer but are rejected for 3DO boot, matching the CUE/CHD data-track requirement.

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

**2026-11-13 UX-29/UX-30 Linux rerun:** the self-contained `linux-x64` 1.8.0 publish
was copied to `~/5do-linux-ux29` on the same Linux Mint 22.3 VM. `FiveDo.Cli --version`
reported `5DO 1.8.0`, `selftest --json` passed, a Lua-authored 2-frame movie recorded
and verified (`movieVerification: pass`), `state export-4do` produced a 5,620,420-byte
container, and `state import-4do` produced an 8,897-byte native state that
`state inspect` accepted. The frontend smoke under `xvfb-run` wrote:

```text
FRONTEND SMOKE OK 1.8.0
locale=en
firstMenuHeader=File
windowState=Normal
```

The self-contained frontend then aborted during Avalonia/OpenAL teardown **after** the
marker; the 1.7.1 bundle reproduces the same teardown abort on this VM, so it is not
introduced by UX-29/UX-30. Linux GPU presentation remains preview/best-effort.

**2026-11-15 v1.10.1 Linux CI:** CI run
<https://github.com/purelogiccode/5DO/actions/runs/43760004443> built both Linux
bundles, verified the ELF architecture and bundle contents (`PACKAGE.txt`, exact
release notes, no runtime/PDB/XML), and passed CLI `selftest` plus the Xvfb frontend
smoke with the renamed `FiveDo` apphost. Platform runtime validation run
<https://github.com/purelogiccode/5DO/actions/runs/43760004576> also passed. GPU
presentation under llvmpipe remains preview/best-effort.

**2026-11-14 v1.9.0 Linux CI rerun:** the `linux-x64` bundle job in CI run
<https://github.com/purelogiccode/5DO/actions/runs/43558322127> built the single-file
CLI/frontend, verified the ELF architecture and bundle contents, and passed CLI
`selftest` plus the Xvfb frontend smoke. The platform runtime validation run
<https://github.com/purelogiccode/5DO/actions/runs/43558322082> also passed. GPU
presentation under llvmpipe remains preview/best-effort.

**2026-11-08 compatibility-pass note:** the Windows x64 1.5.0 bundle extracted and
passed both `FiveDo.Cli.exe --help` and `FiveDo.exe --smoke-test`
(OpenGL 4.0 + OpenAL ready). The Linux rerun could not start the local 8 GB `LinuxMint`
VM because the host could not allocate the memory; no runtime code changed in the
compatibility pass, so the 2026-11-02 Linux x64 gate above remains the current Linux
evidence. Re-run the Linux gate on a host with enough free memory before a new release.
macOS is runtime-validated for the CLI/frontend on both architectures. GPU presentation
is validated on arm64 through Vulkan/MoltenVK (§10.4); accelerated OpenGL is not
available and physical gamepad detection remains best-effort.

**2026-11-08 1.6.0 release note:** the six framework-dependent 1.6.0 bundles were built
locally and verified. The Windows x64 bundle passed `FiveDo.Cli.exe --version` (1.6.0),
`selftest --json` (all checks `ok: true`), and `FiveDo.exe
--smoke-test` (`FRONTEND SMOKE OK 1.6.0`, OpenGL 4.0 + OpenAL ready). The bundle tree has
no runtime folder, PDB, or XML files, and all six `SHA256SUMS.txt` entries verify. Local
PE/ELF/Mach-O inspection confirms `win-x64`/`win-arm64`, `linux-x64`/`linux-arm64`, and
`osx-x64`/`osx-arm64` apphosts. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42552318341> repeated the Linux x64
smoke under xvfb, validated every bundle tree and apphost architecture, and published
all six bundles with `SHA256SUMS.txt`; all published checksums verify.

**2026-11-10 platform/packaging note:** the macOS gamepad source now uses IOKit HID
and its mapping layer is covered by 21 tests; physical detection still needs a macOS
host. The frontend NativeAOT audio soak reports 0 drops/0 underruns for the null and
real Windows devices ([Audio.md](Audio.md) §9). The unsigned Windows installer and
Linux `.deb`/`.rpm` packages were built and validated, including `.iso`/`.cue`/`.chd`
associations and the Linux MIME/desktop entry ([Installers.md](Installers.md)). Full
PAL evidence is in [PalValidation.md](PalValidation.md).

**2026-11-10 native runtime smoke:** `.github/workflows/platform-runtime.yml` ran the
framework-dependent CLI and frontend on native macOS x64 (`macos-15-intel`), macOS arm64
(`macos-latest`), Linux arm64 (`ubuntu-24.04-arm`), and Windows arm64
(`windows-11-arm`). Every target reported `5DO 1.6.0`, `selftest ok: true`, and
`FRONTEND SMOKE OK 1.6.0` (Linux under Xvfb). Run
<https://github.com/purelogiccode/5DO/actions/runs/42770783778> passed all four jobs.
The smoke hides the GL surface, so GPU presentation on macOS/Linux remains
preview/best-effort; Windows WGL/ANGLE remains the validated GL path.

**2026-11-10 1.7.0 release note:** the release adds CD subcode/Q, `disc convert`,
focus-loss pausing, void patterns, pointer input scripts, macOS IOKit HID gamepads, and
native macOS/arm64 runtime smoke. The six framework-dependent bundles are published with
macOS as a drag-to-Applications DMG; the internal `5do-1.7.0-osx-*.tar.gz` is only the CI
extraction/smoke intermediate. The Windows x64 bundle passes `FiveDo.Cli.exe --version`
(1.7.0), `selftest --json`, and the frontend `--smoke-test` (`FRONTEND SMOKE OK 1.7.0`).
The bundle tree has no runtime, PDB, or XML files, and the published `SHA256SUMS.txt`
entries verify. CI run <https://github.com/purelogiccode/5DO/actions/runs/42877150967>
passed every required job, including the six bundle jobs and both native macOS DMG
builds; platform runtime smoke run
<https://github.com/purelogiccode/5DO/actions/runs/42876332881> passed macOS x64/arm64,
Linux arm64, and Windows arm64. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42878260384> published the six assets
at <https://github.com/purelogiccode/5DO/releases/tag/v1.7.0>; the published
`SHA256SUMS.txt` entries verify. Release and docs run links are recorded in the release
checklist.

**2026-11-16 bare-metal Linux GPU validation (`PLAT-02`):** a physical Linux Mint 22.3
machine (Intel Core i7-3615QM, Intel HD 4000 / Ivy Bridge, Mesa 25.2.8, kernel
`7.0.0-43-generic`) replaces the llvmpipe-only VM for platform work. It runs a live
X11/Cinnamon session at 1920x1080 with hardware OpenGL 4.2 core
(`Mesa Intel(R) HD Graphics 4000 (IVB GT2)`, crocus). The self-contained `linux-x64`
1.10.2 frontend passed `--smoke-test` (`FRONTEND SMOKE OK 1.10.2`, `locale=en`,
`firstMenuHeader=File`, `windowState=Normal`) and logged `OpenGL renderer ready:
OpenGL 4.2 (OpenGl), max texture 16384`. The GL probe at 1920x1080 sharp-bilinear
(120 frames, 30 warm-up) reports `ok: true`, 61.6 fps, present mean 2.528 ms
(p99 8.688), GPU mean 1.300 ms (p99 1.703), `filterFallback: false`, and zero renderer
allocations per frame; a 1280x720 accurate run with `--gl-probe-vsync` reports
`vsyncActive: true` at 61.8 fps (62.3 fps and `vsyncBackend: "GLX_EXT_swap_control"`
after the 2026-11-16 swap-interval fix). This is the first hardware-accelerated Linux
GPU presentation result, so **Linux GL presentation is validated on this device**
instead of llvmpipe-only preview. Vulkan 1.2 is available through `hasvk` (Mesa warns that Ivy
Bridge Vulkan support is incomplete): the offscreen probe reaches the Intel device with
`ok: true` and `filterFallback: false` for accurate, xBR, FSR, CRT, HQx 2x, and MMPX at
1080p (46-103 fps), but only when the Intel ICD is forced with `VK_ICD_FILENAMES`; the
default selection picked llvmpipe before the fix. The same build adds the X11 Vulkan
surface: `--vk-window-probe` presents through `VK_KHR_xlib_surface` at 1920x1080 FIFO
(60.1 fps, 16.405 ms present, 6.287 ms GPU, 0 renderer bytes/frame) and a 320x240
accurate capture matches the CPU oracle with **0 different pixels** (`0x5DD1D7C5`);
device selection now ranks discrete > integrated > virtual > other > CPU, so the Intel
device wins without `VK_ICD_FILENAMES`. The desktop shell also presents through a Linux
X11 child window: `--gl-backend vulkan` logs `swapchain 1060x466, output 1060x466,
viewport 370,113 320x240`, displays the Joypad Test card centered in the video area,
and rescales when the window is resized to 1400x820. Wayland-only sessions have no X11
surface and use the OpenGL presenter (native Wayland Vulkan is documented unsupported). Linux also holds an
`org.freedesktop.ScreenSaver` display-sleep inhibitor for the duration of play through
a `python3-dbus` helper; acquire and release were traced with matching cookies
(`1965464518`, `1192968952`, `307216972`). A desktop exit segfault (`PLAT-13`) found in
the same pass was traced to the bundled OpenAL Soft 1.23.1 PipeWire backend being
unloaded by Silk.NET before its `atexit` handler ran; `OpenAlAudioOutput` now pins the
library on Linux, and software, OpenGL, and Vulkan sessions close with exit code 0
(2026-11-17). The same pass fixed the evdev ioctl encoding, so the Linux gamepad backend
now detects a virtual uinput pad (`connected=1`, `buttons=South`) and the two-second
`--gamepad-probe`; the 600 s real-device audio soak reports 0 dropped samples and 0
underruns on the default PipeWire endpoint. The full acceptance pass (`PLAT-10`) also
passed on 2026-11-17: boot on software/OpenGL/Vulkan, `F11` fullscreen round-trip,
`F5`/`F8` slot save/load, `Escape` pause, `F12` 320x240 screenshots, a virtual evdev pad
driving the 3DO, display-sleep acquire/release, library listing, and exit code 0. The
pass found and fixed a screenshot bug where software/Vulkan sessions took the OpenGL
capture branch and wrote a 1x1 PNG. Optical-drive boot is N/A on this host (no
`/dev/sr*`); the DPI matrix remains `PLAT-06`. Reproducible commands and numbers are in
[TestCorpus.md](TestCorpus.md) 1.1cm.

### 10.4 macOS arm64 validation (2026-11-17/18)

Validated on a NoMac cloud Mac: Apple M5 Pro (Virtual), macOS 26.6.2 (build 25G83),
arm64, Apple Paravirtual GPU with unified memory and 16 GB reported VRAM. The host is
Metal-only for acceleration; its OpenGL implementation is Apple Software Renderer 4.1
and rejects an accelerated CGL pixel format. The .NET 11.0.100 SDK was installed into
`~/.dotnet`; the XenoAtom shader compiler is rolled forward, so the .NET 8 runtime is
optional. The repository was cloned from GitHub; the BIOS folder and Wolfenstein 3D
CHD were uploaded over SSH.

| Check | Result |
|---|---|
| Full test suite | `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`: **2,403/2,403 passed** in 11 s (native-surface build, 2026-11-18) |
| CLI version / self-test | `5DO 1.12.0 (FiveDo)`; `selftest --json` reports `ok: true` |
| 600-frame Wolfenstein boot | 0.888 s (~676 fps), 320x240, non-black final frame written to `artifacts/macos/wolf-frame.png` |
| Frontend `--smoke-test` | `FRONTEND SMOKE OK 1.12.0`; real Avalonia window opened and shut down cleanly |
| OpenGL | Avalonia's macOS backend has no `OpenGlControlBase`; `--gl-probe` now fails immediately with that explanation. CGL reports `4.1 APPLE-23.1.1 / Apple Software Renderer`; accelerated GL is unavailable |
| Metal | `MTLCreateSystemDefaultDevice()` returns `Apple Paravirtual device`, unified memory, 1024x1024x1024 max threadgroup |
| Vulkan / MoltenVK | Homebrew `vulkan-loader` 1.4.363 + MoltenVK 1.4; `--vk-probe` reports `ok: true`, `Apple Paravirtual device`, Vulkan 1.0, max texture 16,384, 0 managed bytes/frame |
| Vulkan filters | `Accurate`, `Nearest`, `SharpBilinear`, `Bicubic`, `xBR`, `HQx`, `MMPX`, and `FSR` all pass with `filterFallback: false` at 640x480 (529-975 fps offscreen) |
| Native Metal surface | `--vk-window-probe` at 1920x1080, 60 measured frames after 10 warm-up: `ok: true`, `offscreen: false`, `Apple Paravirtual device`, **60.1 fps**, `vsyncActive: true` (`fifo`), **0 renderer bytes/frame** |
| Native surface parity | 320x240 Accurate window-probe capture: `matches: true`, `differentPixels: 0`, CPU and GPU hash `4167670981` |
| Desktop presentation | `--gl-backend vulkan` runs the shell through the native `CAMetalLayer` (`VK_EXT_metal_surface`); the status bar reports `Vulkan · SharpBilinear · 59.9 fps (auto)` and gameplay renders in the window |
| Resize / fullscreen | Live resize 1060x466 → 900x438 → 1250x524 recreated the swapchain each time (`Vulkan surface resized` log) with the 4:3 viewport recomputed; `Emulation > Fullscreen` moved the swapchain to 1440x900 with a 960x720 viewport and the title screen rendered full-screen |
| Bundle / DMG | `5do-1.12.0-osx-arm64.dmg`, `.pkg`, and `.tar.gz` built on the Mac; the 104 MB tarball is self-contained, contains `libvulkan.1.dylib`, `libMoltenVK.dylib`, `MoltenVK_icd.json`, and both Apache-2.0 notices; CLI `selftest` and frontend `--smoke-test` pass without `DOTNET_ROOT` from the tarball **and** the mounted DMG; SHA-256 verifies |
| Bundled Vulkan | With no `DYLD_LIBRARY_PATH`, `DYLD_PRINT_LIBRARIES=1` loads `/tmp/5do-self2/libvulkan.1.dylib` and `/tmp/5do-self2/libMoltenVK.dylib`; `--vk-probe` returns `ok: true` with `Apple Paravirtual device` |
| Audio | All three `FIVEDO_AUDIO_TESTS=1` cases pass, including live output replacement: outputs share one OpenAL context per device because macOS lacks `ALC_EXT_thread_local_context`; Windows audio tests also pass |

The macOS-specific renderer changes are:

- Vulkan instances on macOS enable `VK_KHR_portability_enumeration` with
  `InstanceCreateFlags.EnumeratePortabilityBitKhr`; without it MoltenVK is not
  enumerated and `vkCreateInstance` returns `ErrorIncompatibleDriver`. Device creation
  also enables `VK_KHR_portability_subset` whenever MoltenVK advertises it.
- Silk.NET prefers the bundled `libvulkan.1.dylib`/`libMoltenVK.dylib`; the
  `MoltenVK_icd.json` path is installed from the renderer's static constructor so the
  loader never falls back to Homebrew. `DYLD_LIBRARY_PATH` is not required.
- The desktop shell presents through a native `CAMetalLayer` view
  (`VK_EXT_metal_surface`) added above Avalonia's content view; the view is click-through
  (`hitTest:` returns `nil`), tracks the video rectangle in points, and sizes its drawable
  from the backing scale. The swapchain is recreated on resize and `OUT_OF_DATE`.
- The offscreen `VulkanOffscreenRenderer` (`CaptureToFrame=true`) and the CPU presenter
  remain as fallbacks when the native surface cannot start.
- `--gl-probe` fails fast on macOS instead of waiting for the 30 s timeout. OpenGL is
  therefore **not** a macOS presentation path in this release; the validated macOS GPU
  path is Vulkan over MoltenVK/Metal.

The `.dmg` remains unsigned (no Developer ID identity was supplied); this is the same
unsigned drag-to-Applications DMG shape used by the release workflow. The release run
<https://github.com/purelogiccode/5DO/actions/runs/44141799807> built all five
self-contained bundles; the macOS job verified the bundled Vulkan/MoltenVK files, and
every published checksum verified after download. `PLAT-15`, `PLAT-16`, and `PLAT-18`
are closed by this pass, and so is the native Metal/Vulkan surface (`PLAT-14`).
Retina/DPI (`PLAT-17`) is validated at 1x — windowed, resize, fullscreen, the 320x240
capture, and 4K offscreen output — and keeps only its 2x row open because the rented
host's display advertises 1x modes only (2026-11-18).

### 10.5 Linux arm64 validation (2026-11-19)

Validated on a real ARM64 Azure VM: `Standard_D2ps_v6` (Neoverse-N2, 2 vCPU, 7 GiB),
Ubuntu 24.04.5 LTS, kernel `7.0.0-1020-azure`, aarch64, and no GPU. Mesa exposes only
`llvmpipe` for OpenGL and `lavapipe` (Vulkan 1.4, LLVM 20.1.2) for Vulkan. The desktop
session was Xvfb at 1920x1080 with XFCE and xrdp. The .NET 11.0.100 SDK was installed
into `~/.dotnet`, and the `v1.13.1` tag was cloned from GitHub.

The tested artifact is the published `5do-1.13.1-linux-arm64.tar.gz`
(SHA-256 `f44d2e629c0374c14d3b1780a52ac6fec9e4aac1214d53ab604a29b05afc234c`, matching
`SHA256SUMS.txt`). Both apphosts are stripped aarch64 ELF binaries, and the bundle
contains no PDB or XML documentation files.

| Check | Result |
|---|---|
| Full test suite | `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`: **2,439/2,439 passed** in 30 s |
| `selftest --json` | `ok: true`; 15/15 checks (five for each compatible/accuracy/performance profile) |
| `bench --frames 120` | 240.3 fps, frame hash `0x76175DC5` |
| `bench --frames 600 --profile accuracy` | 325.9 fps |
| `bench --frames 120 --latency` | 260.0 fps; snapshot 9,348,653 bytes; capture 1.152 ms/frame; restore 1.142 ms/frame |
| `bios info panafz1.bin` | MD5 `f47264dd47fe30f73ab3c010015c155b`, Panasonic FZ-1, USA NTSC |
| `info "themepark.iso"` | 96,706 sectors, game ID `33e5afe454a7c4da20b419a09e2045e2`, known (Theme Park, Electronic Arts 1994) |
| 3,000-frame boot | 7.67 s, 320x240, PC `0x0007AEF8`, frame hash `0xAD4F370C`, 2,207,205 samples, DSP running, no warnings |
| Cross-architecture parity | the 600-frame hash `0x110E7DC5` and the 3,000-frame hash/PC are **identical** to Windows x64 for the same disc and BIOS |
| Audio capture | 50.05 s WAV, peak 4,809, RMS 321, 0 clipped frames; `audio analyze` reports the DSP running |
| OpenAL | `--audio-soak` with `ALSOFT_DRIVERS=null`: 0 dropped samples, 0 underruns, 66.7 ms latency. The PulseAudio software sink reports 428 ms latency and drops 58,661 of 441,441 samples |
| Frontend `--smoke-test` | `FRONTEND SMOKE OK 1.13.1`; `locale=en`, `firstMenuHeader=File`, `windowState=Normal`; `de`, `pt-br`, and `ja` report `Datei`, `Arquivo`, and `ファイル` |
| Desktop shell | the XFCE window renders the toolbar, game list, and status bar; the software presenter runs the Theme Park intro at 59.9 fps; `--fullscreen` covers 1920x1080 |
| Vulkan presentation | `--vk-probe` 52.0 fps offscreen and `--vk-window-probe` 30.8 fps with `fifo` vsync on lavapipe; `--gl-backend vulkan` presents the game in a 1060x466 swapchain at 59.9 fps |
| OpenGL | `--gl-probe` times out after 30 s (backend null, 0 frames) under llvmpipe, matching the Linux x64 result; see [KnownIssues.md](KnownIssues.md) |
| Autopilot | Road Rash, 20,000 frames in 102.9 s (~194 fps), crash restart/menu advance/pacing active, no warnings, frame hash `0x898FAD6E` |
| Disc convert | Theme Park ISO to CHD in 4.53 s; source and output game ID match |
| 4DO state roundtrip | `state export-4do` writes 5,620,484 bytes and `state import-4do` writes 813,275 bytes; `state inspect` accepts the result |
| Packages | the AppImage (FUSE and `--appimage-extract-and-run`) reports 1.13.1 and passes the frontend smoke; `5do_1.13.1_arm64.deb` installs `/usr/bin/5do` and `/usr/bin/5do-cli` and passes CLI `selftest` plus the smoke; the `aarch64.rpm` metadata and file list are correct; the Flatpak installs its 26.08 runtime, runs the CLI `selftest`, and renders the game at 59.9 fps |

The renderer, audio, and TAS findings from this host are:

- Linux arm64 has no validated GPU: every frame came from llvmpipe/lavapipe. Hardware
  GPU presentation remains validated on the bare-metal Intel HD 4000 host (§10.3), not
  here. The auto backend selects the OpenGL video presenter, which leaves a 1x1 black
  surface under llvmpipe; `--gl-backend software` (CPU) and `--gl-backend vulkan`
  (lavapipe) both present correctly, so the VM needs one of those explicit backends.
- Vulkan video presentation honours `RendererSettings.IntegerScale`, so the 320x240
  source is shown 1:1 in a pane too short for a 2x scale. That is the documented default
  rather than a regression; the CPU and fullscreen paths scale to fit.
- The VM has no sound card. OpenAL runs through a PulseAudio null sink, so the 428 ms
  latency and dropped samples belong to the software sink; the OpenAL null backend
  produces 0 drops. Real-device audio remains validated on the bare-metal host.
- `movie verify` reported a one-frame shortfall (`frames 598 != 599`) and matching
  hash/PC/state mismatches for both disc and `--state-in` recordings in 1.13.1. The same
  numbers reproduced on Windows x64, so the defect was platform-neutral; `SYS-26` fixed
  it in 1.13.2 by recording the emulated frame span and resolving the recorded
  disc and BIOS.

### 10.6 Windows arm64 validation (2026-11-19)

Validated on an Azure `Standard_D2ps_v6` (Cobalt 100, 2 vCPU / 8 GB) running Windows 11
Pro 25H2 build 26200, ARM64, with the Microsoft Basic Render Driver and no audio
endpoint. The .NET 11.0.100 SDK was installed into `C:\Users\azureuser\dotnet`; the
`v1.13.1` tag was uploaded as a `git archive` ZIP because the image has no `git`.

The tested artifact is the published `5do-1.13.1-win-arm64.zip`
(SHA-256 `c9925bfb6efc2d0e125fecf6e485ac1cbe9395b04a121d16022d21516d7c9e0a`, matching
`SHA256SUMS.txt`). `FiveDo.exe`, `FiveDo.Cli.exe`, `libSkiaSharp.dll`,
`libHarfBuzzSharp.dll`, `OpenAL32.dll`, `soft_oal.dll`, and `av_libglesv2.dll` all
report the ARM64 PE machine type, and the bundle contains no PDB or XML files.

| Check | Result |
|---|---|
| Full test suite | `dotnet test tests\FiveDo.Tests\FiveDo.Tests.csproj -c Release`: **2,439/2,439 passed** in 51 s |
| `selftest --json` | `ok: true`; 15/15 checks across compatible, accuracy, and performance |
| `bench --frames 120` | 297.0 fps, frame hash `0x76175DC5` |
| `bench --frames 600 --profile accuracy` | 332.1 fps |
| `bios info panafz1.bin` | MD5 `f47264dd47fe30f73ab3c010015c155b`, Panasonic FZ-1, USA NTSC |
| `info "themepark.iso"` | 96,706 sectors, game ID `33e5afe454a7c4da20b419a09e2045e2`, known |
| 3,000-frame boot | 7.43 s, 320x240, PC `0x0007AEF8`, frame hash `0xAD4F370C`, no warnings |
| Cross-architecture parity | 600-frame hash `0x110E7DC5` and 3,000-frame hash/PC identical to Windows x64 and Linux arm64 |
| Audio capture | 50.05 s WAV, peak 4,809, RMS 321.45, 0 clipped frames |
| Frontend `--smoke-test` | `FRONTEND SMOKE OK 1.13.1`; `locale=pt-br` reports `firstMenuHeader=Arquivo` |
| Desktop shell | windowed Theme Park at 61.5 fps through ANGLE (`OpenGL ES 3.0`, Microsoft Basic Render Driver); `--fullscreen --gl-backend software` fills 1024x768 |
| Probes | `--gl-probe` ok at 17.9 fps (640x480, 30 frames); `--d3d-probe` ok at 17.1 fps; `--vk-probe` fails with `vkCreateInstance ErrorIncompatibleDriver` (no driver) |
| Autopilot | Road Rash, 20,000 frames in 215.6 s, frame hash `0x898FAD6E`, PC `521772`, no warnings — identical to Linux arm64 |
| Disc convert | Theme Park ISO to CHD in 9.08 s; source and output game ID match |
| TAS | state save/inspect/resume, `movie record` and `movie play` (300 frames) pass; `movie verify` reproduced the 1.13.1 one-frame shortfall, fixed in 1.13.2 by `SYS-26` |
| Packages / CLI | the ZIP is the only Windows arm64 asset; portable mode writes `nvram.ram` beside the apphost, and `update --json` selects `5do-1.13.1-win-arm64.zip` |
| Doctor | OpenGL, the Vulkan loader, and OpenAL load; the BIOS warning is expected because the folder contained only discs |

The Windows-specific findings are:

- **The bundled OpenAL Soft runtime now loads on a clean image.** The 1.13.x bundle
  needed the Visual C++ 2015-2022 ARM64 redistributable: without it, OpenAL reported
  `Could not load from any of the possible library names` and the emulator ran silently.
  The current Windows bundles ship the app-local MSVC runtime (`vcruntime140.dll` and
  `msvcp140.dll`, plus `vcruntime140_1.dll` on x64) beside `OpenAL32.dll`. On a clean
  Windows 11 ARM64 image with the redistributable uninstalled, the old bundle reported
  `OpenAL: not found (OpenAL32.dll)` while the published 1.14.2 ARM64 bundle reported
  `OpenAL: loaded OpenAL32.dll`; the x64 bundle also loaded its app-local runtime under
  x64 emulation. A 600-frame Theme Park run on the clean ARM64 image produced frame hash
  `0x110E7DC5` and PC `22892`, identical to Windows x64, and `selftest --json` passed
  all 15 checks. A 60-frame `--audio openal` run with `ALSOFT_DRIVERS=null` exited 0
  and produced 44,144 samples, and the desktop `--smoke-test` reported
  `FRONTEND SMOKE OK 1.14.2` with a clean shell shutdown. The VM still has no audio
  endpoint, so real-device playback remains unvalidated; `ALSOFT_DRIVERS=null` is still
  required for audio-path tests.
- **The VM has no Vulkan driver.** `--vk-probe` fails with `ErrorIncompatibleDriver`;
  the validated presentation paths are ANGLE/GL ES 3.0 and Direct3D 11 on the Basic
  Render Driver. Hardware GPU evidence remains the Windows x64 desktop.
- **GUI automation needs an interactive session.** SSH lands in session 0 with no
  desktop; the shell was exercised through autologon plus `schtasks /IT`, and the
  fullscreen D3D surface blocks GDI capture, so the fullscreen evidence uses the
  software presenter.

### 10.7 Windows x64 physical optical drive (2026-11-21, issue #8)

Validated with the v1.15.0 optical-drive work (commit `57c8c20`) on the issue reporter's Windows 11 x64
machine and a Pioneer BD-RW BDR-S13JX. The drive has a 3DO disc's custom filesystem, so
Windows does not expose a mounted volume and may not assign a drive letter; the old
`DriveInfo.IsReady` filter reported "No optical drive with media was found."

| Check | Result |
|---|---|
| Drive discovery | `\\.\CdRomN` device probe with `IOCTL_STORAGE_CHECK_VERIFY2`; lettered drives keep their letter |
| Virtual letterless drive | `doctor` reported `\\.\CdRom0`; `info \\.\CdRom0` read all 22 sectors |
| Burned CD-R boot | Reporter confirmed the games boot and reach their FMV intro sequences |
| Physical CD-DA | v1.16.0: the drive TOC is exposed through `ITrackedDiscSource`, and raw 2352-byte frames use `IOCTL_CDROM_RAW_READ` (CDDA) or Linux `CDROMREADAUDIO`. v1.16.1: `disc tracks E:` lists all six tracks of the reporter's Red Book audio CD and `disc audio E:` extracts track 1 (164.28 s, peak 31659, RMS 7248.66, 0 clipped); the FZ-10 player transport remains inert (SYS-01) |
| Host read errors | v1.16.0 confirmed on the reporter's machine: the sector-103777 CRC is a Warning, emulation continues, and the FMV resumes after the drive's recovery pause |
| Follow-up: JIT soft reset | Reset PC/CPSR/R14_svc survive the compiled-block boundary (`ArmJitSoftResetTests`) |
| Follow-up: input | One active joypad with disabled `None` ports uses the legacy 16-byte PBUS packet (`InputManagerTests`) |
| Full suite | **2,576/2,576** in Debug, build 0 warnings/0 errors |

The reporter's CD-R boot closes the SYS-21 `Done when` line. Physical CD-DA is
implemented from the drive TOC and raw-audio requests; the Windows TOC and cooked read
paths were validated on a mounted virtual optical drive during 1.15.0, and the reporter's
real Red Book audio CD exercised the raw CDDA path on hardware during 1.16.1 (see §10.8).
The v1.16.0 release workflow repeated the Windows x64 (CLI + desktop smoke) and Linux x64
(CLI + desktop smoke, `.deb`/`.rpm` install) checks; all passed.

### 10.8 3DO collection optical sweep (2026-11-21, Windows x64)

The local `J:\Panasonic 3DO` collection has **1,366 CHD images** and **496 unique game
IDs**. Every image opened and every track's first, middle, and last native sector read
successfully. One representative per unique game ID, including all three audio-only
discs, was fully sector-verified:

| Check | Result |
|---|---|
| Images opened / track TOC parsed | **1,366/1,366**, 0 errors |
| Unique game IDs | **496**, all database-known |
| Track layouts | 1,363 single-data; 3 audio-only (4, 14, and 99 tracks); no mixed-mode images |
| First/middle/last native-sector probe | **0 errors** across every track |
| Full native-sector verification | **496/496 unique-title representatives**, 0 read errors |
| Audio-only full verification | A&M 102,265; Oyaji 55,745; D's Diner 96,962 sectors, all verified |
| BIOS boot matrix | **26/26 cases** at 3,000 frames, `ok: true`, no errors |
| US BIOSes | FZ-1, FZ-10, GoldStar, Sanyo; auto selected FZ-10 |
| Arcade BIOSes | patched FZ-10 + `3do_arcade_saot`; Crime Patrol auto-selected patched |
| Japan BIOSes | FZ-10J; WorldCup Super Stadium and Shiru Miru also loaded the Kanji BIOS2 |
| PAL BIOSes | FZ-10E Anvil; auto selected Anvil |
| Audio-only player | TOC lists tracks, transport remains STOP (SYS-01; 4DO has the same BIOS limitation) |

Non-JSON `disc verify` now prints opening/progress lines to stderr; `--json` stays a
single document. The verifier uses the native track total for mixed-mode discs, so audio
tracks after the data volume do not make a healthy disc look short. The new
`disc audio <disc> --track N --out trackN.wav` command extracts a CD-DA track to a
44.1 kHz WAV from an image or a physical drive; a synthetic data+audio CUE is covered by
`CliProcessTests`. Full suite **2,579/2,579**, build/format/InspectCode clean.

The issue #8 reporter's Way of the Warrior CD-R produced one data track at 157,996
frames and `disc verify E:` read **157,996/157,996 sectors**, 0 errors, hash
`0x84b2bef01d157a9f`, matching the CHD full-verification hash exactly. That confirms the
physical drive input path. The burn has no CD-DA track (`disc audio --track 1` correctly
reports data); the physical CD-DA test used a Red Book audio CD instead (§10.9), and the
`A&M Music Sampler` image track produced a non-silent 112.33 s WAV.

### 10.9 Physical Red Book CD-DA (2026-11-22, Windows x64)

The issue #8 reporter inserted a Red Book audio CD into the same `E:` drive and ran the
1.16.1 CLI:

| Check | Result |
|---|---|
| `disc tracks E:` | 6 tracks, all `type=audio format=raw`; no data track |
| Track 1 geometry | 12,321 frames, 28,978,992 bytes, start 0 |
| `disc audio E: --out track1.wav` | wrote 12,321 sectors (164.3 s) |
| `audio analyze track1.wav --json` | 44.1 kHz, stereo, 16-bit, 164.28 s, **peak 31659**, **RMS 7248.66**, 0 clipped frames |

The non-silent peak/RMS confirms the Windows `IOCTL_CDROM_RAW_READ` CDDA path works on a
real drive and that the WAV is real music, not zero-filled sectors. The direct
`disc audio` path is the definitive hardware CD-DA test because the FZ-10 BIOS player
still shows STOP (SYS-01).

**No known 3DO game uses CD-DA tracks.** All **672** cue sheets in the Redump Panasonic
3DO set (<http://redump.org/cues/3do/>, downloaded 2026-11-22) contain only
`TRACK 01 MODE1/2352`; zero contain a `TRACK nn AUDIO` entry. The
local collection agrees: 1,363 single-data images and 3 audio-only images, with no
mixed-mode game. Retail 3DO titles stream music as data-track audio (ADPCM/Cinepak), so
there is no game disc to use for an in-game CD-DA test; a normal music CD is the correct
test disc, and the reporter's result above is that test.

### 10.10 Cross-platform 1.17.1 release-bundle validation (2026-11-25)

The release bundles built from commit `9003dea` (CI run 45662940544) were exercised on
all five targets with **Wolfenstein 3D (USA).chd**, **panafz1.bin**, 600 frames, and
`--audio null`. Every host produced frame hash `0x110E7DC5` (decimal `286162373`),
PC `366944`, game ID `3dfcdbbff14ffb59e157e27f18d9df7b`, and the same 600-frame PNG
SHA-256 `57580743a08a3f4ebdf57240cbcc997002ef63b7234531f946127f1495973f43`.
`selftest` reported 15/15 and the frontend smoke passed on every target
(`FRONTEND SMOKE OK 1.17.0` for these pre-release bundles).

| Target | Host | Full suite | Probes / notes |
|---|---|---|---|
| Windows x64 | PETERSONPC (Ryzen 9 7900, Radeon RX 580) | 2,602/2,602 | OpenGL and Direct3D 11 probes passed |
| Windows arm64 | Azure `winarm-test` (Cobalt 100) | 2,602/2,602 | GL/ANGLE and Direct3D 11 probes passed; both apphosts and bundled native DLLs are PE `0xAA64` |
| Linux x64 | Hyper-V `LinuxMint` (x86_64) | 2,602/2,602 | CPU bundle and X11 smoke |
| Linux arm64 | Azure `linuxarm-test` (Neoverse-N2) | 2,602/2,602 | `xvfb-run` smoke; no GPU |
| macOS arm64 | NoMac Apple M5 Pro (virtual) | 2,602/2,602 | CLI and smoke run from the mounted DMG |

The five bundle SHA-256 values were recorded locally:

| Bundle | SHA-256 |
|---|---|
| `5do-1.17.0-win-x64.zip` | `7B505721BAD095BC4FBC2F12B1A3AAEDCBA30363E61704BE241B079217A5586D` |
| `5do-1.17.0-win-arm64.zip` | `E4AFC7A047452107540A6CF9F2C3E10F230E24353A1F95039FFFC6197AF2799F` |
| `5do-1.17.0-linux-x64.tar.gz` | `C9E0F0C1D7D394D861C9D772479B0A5806164D3166039E2B46917A8F66053712` |
| `5do-1.17.0-linux-arm64.tar.gz` | `ADE0798EBA2F7692B8A4A06B653A9E99B4910AD47AACE0A6F97D6975450BA29B` |
| `5do-1.17.0-osx-arm64.dmg` | `74730B804BF5363226CA081194A3D6F569DD2071B2F136C913EE6E85AA9B3E9E` |

The run used the 1.17.0 version string because it validated the post-1.17.0 master
before the 1.17.1 version bump; the release workflow rebuilds the same sources as
1.17.1. The published v1.17.1 release (run
<https://github.com/purelogiccode/5DO/actions/runs/45690464736>) contains 17 assets; both
checksum manifests verify, and no signature or keyring assets are present.

### 10.11 Physical CD-R re-burn, Way of the Warrior (2026-11-25, Windows x64)

The issue #8 reporter burned a second copy of **Way of the Warrior** and retested on the
published 1.17.0 Windows x64 build (the read-error handler has been in place since 1.16.0):

| Observation | Result |
|---|---|
| Boot from the burned CD-R | Boots; FMV intro sequence plays |
| Sector-103777 fault | **Does not recur**; no "Emulation fault" / core stop |
| FMV behavior | First re-test: brief pause under one second. Repeat run on 1.17.0: no pause, no warning, FMV completed |
| Gameplay after the FMV | Reaches the first match with no further errors |
| `disc verify E: --max-errors 8` | 157,996/157,996 sectors, 0 errors, hash `0x84b2bef01d157a9f` |

The re-burned disc produces **the same hash as the first burn**, so the two copies are
byte-identical. The earlier core stop was therefore intermittent read reliability at
sector 103777, not corrupt disc data. The first burn reproduced a recoverable CRC at that
sector and, on 1.15.0/1.16.0, either stopped the core or forced a warning-level retry; the
second copy does not fault.

The final log from the 1.17.0 run contains **no warnings, no read errors, and no
sector retries**, and the FMV played through to the pause. That confirms the remaining
sub-second pause is intermittent host read latency (a slow drive read on a marginal
spot), not an emulator fault; `PhysicalDiscSource` reads a drive synchronously on the
emulation thread and deliberately skips the sector prefetcher so media errors surface at
the offending sector. No image or CHD shows it, and it did not reproduce on the final run.

### 10.12 High-refresh frame interpolation (2026-11-26, Windows x64)

Windows 11, NVIDIA RTX 4070, 3440x1440 at 164 Hz, Road Rash (EU) with `panafz1.bin`,
640x480 CRT output. The **Settings > Video > Frame interpolation** toggle requires a
fixed rate above 60 Hz and is disabled with VRR:

| Renderer | Emulated | Presentation | Notes |
|---|---:|---:|---|
| OpenGL 4.0 | 59.5-60.4 fps | 148-167 Hz | 164-165 Hz typical; `gpu 0.14-0.18 ms`, `present 0.41-2.76 ms` |
| Vulkan 1.2 (RTX 4070) | 59.8 fps | 132 Hz | swapchain 1590x697, live-resized to 1978x751 |
| Direct3D 11 | 60.0 fps | 83 Hz | 640x480 child surface |

The status bar showed `interp`, the overlay reported `audio ... u0 d0` (no underruns or
dropped samples), and input-to-submit telemetry stayed near 3.0-3.4 ms mean. The
toggle-off run presented at 167 Hz with no `interp` marker and no interpolation log.
The emulated machine, input sampling, audio clock, frame/state/audio hashes, and save
states are unchanged: the 36,000-frame headless run kept `0x8929E80A` / PC `0x0006826C`,
and two independent 600-frame runs matched at `0x76175DC5` / PC `0x00001BD0`. Blended
frames use the 15-bit direct-color path, and the visible output lags the newest field by
up to one field period; see [Performance.md](Performance.md) §8.9 and
[TestCorpus.md](TestCorpus.md) 1.1cv for the full evidence and limits.

### 10.13 Patterned GPU bars and live CPU clock (2026-11-26, Windows x64)

Windows 11, RTX 4070, 1612x986 window. The Settings > Video void pattern now renders in
the GPU bars: Checker was verified on OpenGL (direct and fixed-output blit paths),
Direct3D 11, and Vulkan; FourDO was verified on OpenGL; Solid kept the configured
`#FF101010` base. The pattern is presentation-only and the settings round-trip through
`RendererSettings.VoidPattern`. The CPU clock multiplier now applies at the next frame
boundary without recreating the machine; six `LiveClockTests` prove the live switch
matches a clock override of the same pre-switch state, a post-switch save state is
deterministic, stock-speed runs are unchanged, and every component clock follows the
machine. Full suite **2,638/2,638**; build and `dotnet format` clean. See
[TestCorpus.md](TestCorpus.md) 1.1cw and [Video.md](Video.md) "Void patterns".

### 10.14 MP4 video recording (2026-11-26, Windows x64 / Linux x64 / macOS arm64)

Windows 11, RTX 4070, FFmpeg 8.1.1 with libx264/h264_nvenc/AAC. `--record-mp4` on a
3,600-frame Road Rash run produced a 3,724,569-byte 60.060 s MP4 (H.264 320x240 at
60000/1001, AAC 44.1 kHz stereo; decoded audio peak 15,551 / RMS 1,860.2, 0 clipped).
The desktop **File > Record MP4...** path produced a 17.601 s 640x480 MP4 with 1,056
frames and non-silent AAC (peak 29,702 / RMS 4,267.8). A UI-automation pass selected High
in Settings > Video > MP4 quality, pressed OK, and persisted `"Mp4Quality": "High"` to an
isolated settings file.

Linux x64 (Ubuntu 24.04, FFmpeg 6.1.1) ran the same Road Rash source at 3,600 frames:
a 3,728,689-byte 60.060 s H.264/AAC MP4, and decoded audio peak 15,491 / RMS 1,787.9 on
the 3,000-frame high test. Three 3,000-frame runs measured **Low 1,720,357 / Medium
2,967,726 / High 5,190,590 bytes**. The frontend `--smoke-test` passed with OpenGL 4.2.

macOS arm64 (macOS 26.6.2, FFmpeg 9.0.2 with libx264/h264_videotoolbox/AAC) recorded
Wolfenstein 3D at 3,600 frames: a 4,123,123-byte 60.060 s H.264/AAC MP4 with decoded
audio peak 32,400 / RMS 5,066.3 and 0 clipped. `Mp4RecorderTests` passed on both hosts,
and the macOS `--smoke-test` reported `FRONTEND SMOKE OK 1.17.1`.

All runs used an external FFmpeg; 5DO does not bundle an encoder. Fast-forward and rewind
stop the recording and pause omits the gap. The desktop menu was exercised on Windows;
Linux and macOS ran the frontend smoke and the same validated `Mp4Recorder` CLI path.
See [TestCorpus.md](TestCorpus.md) 1.1cx.

### 10.15 v1.18.0 release validation (2026-11-26)

The 1.18.0 source gate passed the full Release suite **2,644/2,644**, build,
`dotnet format`, `jb inspectcode --severity=WARNING`, and `mkdocs build --strict`. The
release adds read-only Alcohol MDS/MDX and CloneCD CCD/IMG/SUB readers with synthetic
data/audio, malformed-input, factory, library, and database tests; representative
real-image parity remains `SYS-29`. The review fixed relative descriptor paths, stored
first-track pregaps, split-volume truncation, cache identity, and cache publication, and
added regression tests for relative paths and pregaps.

A post-fix 600-frame Road Rash CLI recording produced a 10.010 s H.264/AAC MP4 with a
4:3 display aspect (320x240, SAR 1:1, 60000/1001). MP4 finalization now uses a sibling
temp file, failed recordings remove their temp WAV/video, and `--record-frames` caps the
WAV. Interpolation resets after pause/step/rewind/state load/disc change; the Vulkan void
pattern is no longer vertically mirrored; the OpenGL void program is released; and a live
clock change clears rewind history and rescales the Quartz/Clio accumulators.

The published Windows x64 bundle reports 1.18.0, passes `selftest` 15/15, reproduces the
600-frame Wolfenstein 3D hash `0x110E7DC5` / PC `0x00059960`, writes a 10.010 s H.264/AAC
MP4 with DAR 4:3, and reports `FRONTEND SMOKE OK 1.18.0`. The tag workflow
<https://github.com/purelogiccode/5DO/actions/runs/45855166556> built all five bundles and
the Linux packages and published 17 assets. Both `SHA256SUMS.txt` and
`SHA256SUMS-linux.txt` verified with **0 mismatches**. The local copy is
`src/FiveDo.Frontend.Avalonia/bin/Release/5do-1.18.0-win-x64.zip`.

### 10.16 Automatic FFmpeg installation (2026-11-26)

`FfmpegProvisioner` pins FFmpeg 6.1.1 Windows, 7.0.2 Linux, and 6.0 macOS builds for
Windows x64/arm64, Linux x64/arm64, and macOS arm64. A download is explicit
(**Configuration > Install FFmpeg**, the MP4 prompt,
or `5do ffmpeg install`), HTTPS-only, size-limited, streamed to a temporary file, and
verified against the pinned SHA-256 before it is moved into place. The installer then
runs `-version` and `-encoders` and requires libx264 (or a platform H.264 encoder) plus
AAC.

On Windows x64 the CLI installed 82,797,568 bytes with SHA-256
`04e1307997530f9cf2fe35cba2ca7e8875ca91da02f89d6c7243df819c94ad00`, version
`6.1.1-essentials_build-www.gyan.dev`, beside `FiveDo.Cli.exe`; `ffmpeg status --json`
reported it ready, and a 600-frame run produced a 10.010 s H.264/AAC MP4 with DAR 4:3.
Linux x64 installed the 7.0.2 build (`e7e7fb…3eb99`) beside the CLI and marked it
executable. macOS arm64 installed the 6.0 build (`a90e3d…6584`) into
`~/Library/Application Support/5DO/tools/ffmpeg`, the intended read-only-app-bundle
fallback. `FfmpegProvisionerTests` covers the five platform entries and checksum
rejection.

The v1.18.1 tag workflow
<https://github.com/purelogiccode/5DO/actions/runs/45916148140> built all five bundles and
the Linux packages and published 17 assets. Both `SHA256SUMS.txt` and
`SHA256SUMS-linux.txt` verified with **0 mismatches**. The published win-x64 bundle
reports 1.18.1, passes `selftest`, reproduces hash `0x110E7DC5` / PC `0x00059960`,
reports `FRONTEND SMOKE OK 1.18.1`, and installed FFmpeg 6.1.1 beside the CLI; its
600-frame MP4 is 10.010 s H.264/AAC with DAR 4:3. The local copy is
`src/FiveDo.Frontend.Avalonia/bin/Release/5do-1.18.1-win-x64.zip`.

The v1.19.0 tag workflow
<https://github.com/purelogiccode/5DO/actions/runs/45960321450> built all five bundles and
the Linux packages and published 17 assets. Both `SHA256SUMS.txt` and
`SHA256SUMS-linux.txt` verified with **0 mismatches**. The published win-x64 bundle
reports 1.19.0, passes `selftest` 15/15, reproduces hash `0x110E7DC5` / PC `0x00059960`,
reports `FRONTEND SMOKE OK 1.19.0`, and completes a 600-frame netplay host/join with
matching frame/audio hashes (`0x110E7DC5` / `0x035F6315`); its 600-frame MP4 is 10.010 s
H.264/AAC with DAR 4:3. The local copy is
`src/FiveDo.Frontend.Avalonia/bin/Release/5do-1.19.0-win-x64.zip`. Raw reports:
[host](data/netplay-release-host-600.json), [join](data/netplay-release-join-600.json).

### 10.17 v1.20.0 lobby prototype and release bundles (2026-11-28)

The v1.20.0 tag workflow
<https://github.com/purelogiccode/5DO/actions/runs/46108633103> built the five bundles and
Linux packages and published 17 assets. Both `SHA256SUMS.txt` and `SHA256SUMS-linux.txt`
verified with **0 mismatches** (the win-x64 hash was rechecked locally). The published
win-x64 bundle reports 1.20.0, passes `selftest` 15/15, reproduces the 600-frame hash
`0x110E7DC5` / PC `0x00059960`, and reports `FRONTEND SMOKE OK 1.20.0`. The local copy is
`src/FiveDo.Frontend.Avalonia/bin/Release/5do-1.20.0-win-x64.zip`. The lobby service at
`https://lobby.purelogiccode.com` completed room create/join/delete, WebSocket signaling,
TCP relay pairing, and a UDP relay probe (7/8 replies) on 2026-11-27; see
[Lobby.md](Lobby.md) §7.4.

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
- Disc ID (first 8 hex of the 2-sector MD5) and format (ISO/BIN/CUE/CHD/CDI/NRG/MDS/CCD).
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
CD-DA images now reach the FZ-10 BIOS CD player and list track 1 (the 2352+96 subcode
mode is accepted), but the transport controls remain inert and the WAV capture is
silent; this is the documented SYS-01 audio-only BIOS-player residual, not a
beyond-title blocker.
