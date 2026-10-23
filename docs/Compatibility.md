# Compatibility Notes

This document tracks what the emulator intends to support, what it deliberately does not support yet, and the game/BIOS quirks inherited from FreeDOCore/4DO.

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
| CHD | Later | Optional managed reader; not required for v1 |
| Multi-track / CD-DA | Not supported | 3DO games use a single data track; Red Book audio is out of scope |
| ZIP/7z archives | Later | Prefer extracting first |

Sector count is read from the 3DO volume header at sector 0 (big-endian `blockCount` at byte offset 80). If the header is invalid, fall back to `fileSize / sectorSize`.

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
| 4:3 aspect ratio, letterbox | Yes | |
| Nearest/bilinear scaling | Yes | |
| Integer scaling | Yes | |
| Fullscreen | Yes | |
| 384/512/640/1024-wide VDLP modes | Parsed, may crop | Full presentation |
| High-resolution (`RESSCALE`) | No | Planned as a coherent feature |
| PAL | No | Later if needed |
| HQ2X/3X/4X | No | Optional filters |
| Shaders/OpenGL backend | No | Optional later |
| Screenshots | Yes (PNG/BMP via host) | |
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
- No high-resolution rendering.
- No hardware-accelerated 3DO renderer; CEL rendering is software, matching FreeDO.
- No 3DO mouse/light gun/other peripherals.
- No 3DO modem/network emulation.
- No cycle-exact ARM bus timing beyond FreeDOCore's approximation.
- No guarantee of 4DO save-state compatibility.

---

## 10. Compatibility matrix template

Use this table when testing real discs. Record the disc hash/ID, BIOS, build, and result.

| Game | Region | Disc ID | BIOS | Build | Boot | In-game | Audio | Input | Notes |
|---|---|---|---|---|---|---|---|---|---|
| | | | | | | | | | |

Suggested test set (user-provided, not distributed):

- BIOS-only boot and menu navigation.
- A 2D-heavy title (CEL/packed sprites).
- A 3D-heavy title (matrix engine, arbitrary CELs).
- A CD-streaming title (FMV/audio).
- A title with a known 4DO fix flag.
- A homebrew/test disc if available.

---

## 11. Reporting a compatibility issue

Include:

- Emulator version/commit.
- BIOS MD5 and region.
- Disc ID (first 8 hex of the 2-sector MD5) and format (ISO/BIN/CUE).
- Fix mode flags and settings.
- Steps to reproduce.
- Save state and a short input recording if possible.
- Log output with the `Emulation` and `Cdrom` categories enabled.
