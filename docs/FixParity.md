# 4DO and Opera Fix Parity Audit

**Snapshot:** 2026-11-01. This document closes the 4DO/Opera fix-bit inventory and records
how each known per-game quirk maps to 5DO.

## Sources

- 4DO `FourDO/Emulation/GameConsole.cs`, `Start`: the `FIX_BIT_*` assignments after the
  game source opens.
- 4DO `FreeDOCore` `FixMode`: the bit values consumed by the ARM/MADAM/CEL paths.
- Opera `libopera`: no per-game fix-bit table. Opera uses generic accuracy models for the
  behaviors below; its ARM cycle classes, CEL origin, and VDLP code are the reference for
  the generic replacements.

## Mapping

| 4DO bit | Value | 4DO IDs | 5DO mapping | Status |
|---|---:|---|---|---|
| `FIX_BIT_TIMING_1` | `0x00000001` | `F3AF1B13` Crash 'n Burn (JP), `217344B0` Crash 'n Burn (US) | `FixMode.BitTiming1` | Mapped. LDM value/PC timing. |
| `FIX_BIT_TIMING_2` | `0x00000002` | `DBB419FA`, `07C32F10`, `5282889F`, `7340307E` (Street Fighter II family) | `FixMode.BitTiming2` | Mapped. Intro sync. |
| `FIX_BIT_TIMING_3` | `0x00000004` | `CB8EE795` DinoPark Tycoon | `FixMode.BitTiming3` | Mapped. Scale Y * 5. |
| `FIX_BIT_TIMING_5` | `0x00000010` | `1A370EBA`, `B35C911D` Microcosm | `FixMode.BitTiming5` | Mapped. VDLP current/previous bitmap offsets. |
| `FIX_BIT_TIMING_6` | `0x00000020` | `0511D3D2`, `9F7D72BC`, `E843C635` Alone in the Dark | `FixMode.None` (generic replacement) | Retired. The generic XYPOSH/XYPOSL CEL origin and literal-row model replaced the title-specific Y step. |
| `FIX_BIT_GRAPHICS_STEP_Y` | `0x00080000` | `2AABA5B9` Samurai Shodown (EU/US), `BF61BB32` Samurai Shodown (JP) | `FixMode.None` (generic replacement) | Retired. The generic CEL origin model replaced the title-specific Y step. |
| `FIX_BIT_TIMING_4` | `0x00000008` | none | n/a | Reserved; no 4DO title uses it. |
| `FIX_BIT_TIMING_7` | `0x00000040` | none | n/a | Reserved; no 4DO title uses it. |

`GameCompatibilityTests` locks every row, including case-insensitive and full-MD5
lookups, unknown IDs, collision behavior, and explicit overrides.

## Evidence

The comparisons below used `run --bios bios/panafz10.bin --frames 3000 --json` and the
default automatic fix mode unless a `--fix-mode` value is shown.

| Title | Auto fix | Comparison | Result |
|---|---:|---|---|
| DinoPark Tycoon (USA) | `0x4` | no fix | Frame hashes differ (`0xA1F3E800` vs `0xF2607174`), confirming the scale-Y fix is active. Both frames render. |
| Microcosm (USA) | `0x10` | no fix | Identical hash `0x4E6A4651` at the 3,000-frame title scene; the mapping is retained and does not regress the scene. |
| Super Street Fighter II Turbo (USA) | `0x2` | no fix | Identical hash `0x1FFBE3FA` at 3,000 frames; the intro-sync path is retained for later frames. |
| Alone in the Dark (USA) | none | forced `0x20` | Identical hash `0x175B8FE4`; the retired timing-6 bit is not needed. |
| Samurai Shodown (USA, Europe) | none | forced `0x80000` | Identical hash `0x86C04E50`; the retired graphics-step-Y bit is not needed. |

The local `Alone in the Dark` matrix also reaches gameplay and save/load roundtrips, and
`Super Street Fighter II Turbo` plays a full CPU-vs-CPU attract match past 10,000 frames.
Those results are recorded in [`Compatibility.md`](Compatibility.md).

## Result

Every known 4DO fix bit is either mapped to the matching `FixMode` value or explicitly
replaced by a generic model. There are no unmapped IDs and no silent mismatches. The
remaining work is game-database metadata (SYS-17), not fix behavior.
