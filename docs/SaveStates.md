# Save States

5DO uses a native, versioned save-state format with the `.tdos` extension. The format
is designed for deterministic continuation of the emulated machine, not for
interoperability with FreeDO/4DO `.state` files. There is no legacy 4DO state import in
this release.

- Writer/reader: `StateWriter` / `StateReader` (`FiveDo.Core`).
- Machine framing: `FiveDoMachine.SaveState` / `LoadState`.
- Slot management: `SaveStateSlotManager` (`FiveDo.Host`).
- Inspector: `StateInspector` (`FiveDo.Host`).
- CLI: `state inspect`, `state slots`; `run --state-in`/`--state-out`.

---

## 1. Design goals

- **Deterministic:** loading a state and continuing produces the same frames as the
  uninterrupted machine.
- **Explicit:** every device writes its own section; no reflection or hidden globals.
- **Validated:** each section has a version and CRC-32; the header has a magic/version.
- **Portable within the emulator:** little-endian, length-prefixed strings, no pointers
  or native handles.
- **Atomic on disk:** writes go to a temporary file and are moved into place.

A state does **not** contain the disc image or BIOS bytes. It contains the emulated RAM,
ROM, NVRAM, device state, and the derived VDL frame, plus hashes for diagnostics.

---

## 2. File layout

```text
+---------------------------------------------------------------+
| Header                                                        |
|   magic           u32   "TDO1" (0x314F4454 little-endian)      |
|   version         u16   1                                     |
|   reserved        u16   0                                     |
|   emulatorVersion string (i32 byte count + UTF-8 bytes)       |
|   armClock        i32   12,500,000                            |
|   fixMode         u32   FixMode flags                         |
|   biosHash        string (lowercase MD5 hex)                  |
|   discHash        string (game ID, lowercase MD5 hex)         |
|   framesExecuted  i64                                         |
+---------------------------------------------------------------+
| Section 1                                                     |
|   id              u32                                         |
|   version         u16                                         |
|   reserved        u16   0                                     |
|   length          u32   payload byte count                    |
|   payload         byte[length]                                |
|   crc32           u32   CRC-32 of payload                     |
+---------------------------------------------------------------+
| Section 2 ...                                                 |
+---------------------------------------------------------------+
```

- All integers are little-endian.
- Strings are a signed 32-bit UTF-8 byte count followed by exactly that many bytes.
  Invalid UTF-8 is rejected.
- `magic` is `TDO1` as ASCII; the little-endian `u32` is `0x314F4454`.
- `version` must be `1`.
- `armClock` is the tuning clock used when the state was written.
- `fixMode` is the applied compatibility mask.
- `biosHash` is the MD5 of BIOS bank 0 as restored from memory.
- `discHash` is the host-supplied game ID (MD5 of the first two disc sectors) or empty.
  It is **recorded**, not used to reject a load.
- `framesExecuted` is the machine's frame counter at save time.

### Section framing

Each section is:

```text
[id:u32][version:u16][reserved:u16][length:u32][payload:length][crc32:u32]
```

The CRC is CRC-32/IEEE 802.3 (reflected polynomial `0xEDB88320`, init `0xFFFFFFFF`,
final XOR `0xFFFFFFFF`). The reader verifies the CRC before exposing the payload. A
truncated section, oversized length, or CRC mismatch throws `InvalidStateException`.

---

## 3. Section table

Sections are written in this order and identified by stable IDs. Never renumber an
existing section.

| ID | Name | Version | Owner | Payload summary |
|---:|---|---:|---|---|
| 1 | CPU | 1 | `Arm60` | R0-R15, banked SVC/ABT/FIQ/IRQ/UND/SPSR register sets, CPSR, FIQ pending, second-ROM select, data-abort pending, carry-out, LDM scratch. |
| 2 | Memory | 1 | `FiveDoMemory` | ROM bank, PC-adjust flag, the full 19 MB RAM backing (3 MB visible + 16 x 1 MB high-res mirrors), 2 MB ROM, 32 KB NVRAM. |
| 3 | MADAM | 1 | `Madam` | 2048+64 register file, 32-entry PLUT, PBUS queue, read/write modulo, CEL FSM, matrix result temporaries. |
| 4 | CLIO | 1 | `Clio` | 65536-word register file, DSP window words, timer/line state, input/output FIFOs and pointers, DMA state. |
| 5 | DSP | 1 | `Dsp` | PC, noise/audio status, semaphore status/data, counters, interrupt/multiplier/ALU/shifter state, masks, running/FIQ flags, base/seed, IMem/DMem. |
| 6 | SPORT | 1 | `Sport` | Color, source, and destination registers. |
| 7 | VDLP | 1 | `Vdlp` | CLUT B/G/R, background, head/current VDL, modulo, current/previous bitmap, out-control, CLUT DMA, line delay. |
| 8 | XBUS | 1 | `Xbus` | Select/poll state, status/command FIFOs, and attached-device state. The CD-ROM device is also written as section 9. |
| 9 | CD-ROM | 1 | `CdromDevice` | Poll/status, status/data buffers, command buffer, requested count, MEI status, current sector, disc state (tracks/MSF/TOC). |
| 10 | Quartz | 2 | `Quartz` | ARM/VDL/timer accumulators, exact DSP cycle remainder, VDL line counter. Version 1 is accepted and converted. |
| 11 | Machine | 1 | `FiveDoMachine` | Compatibility flags (fix mode, Anvil, speed fixes, sf/sdf, unknown flag 11, jw, cnbfix, high-res), frame source dimensions, and all 960 `VdlLine` objects (width, control words, CLUT, 1280 pixels each). |

### Why the state is large

The memory section stores the **entire RAM backing array**, including the 16 reserved
1 MB high-resolution mirrors, and the machine section stores all 960 VDL lines with
their full 1280-pixel buffers. A typical state is therefore about **24.9 MB**
(observed 24,902,560-24,902,597 bytes across the local corpus), not a few hundred
kilobytes. Plan disk space accordingly:

- 10 slots: about 250 MB.
- 100 slots (the maximum `SaveSlotCount`): about 2.5 GB.

The state is still deterministic and compresses well with a general-purpose archiver,
but 5DO does not compress `.tdos` files.

### Section version compatibility

- All sections currently write version 1.
- Quartz writes version 2 (exact rational DSP clock) and accepts version 1, converting
  the legacy 8.24 fractional sample position to ARM cycles.
- Any other section version is rejected with
  `Unsupported section <id> version <n>.`
- Unknown section IDs are ignored and recorded as a warning:
  `Ignored unknown state section <id>.`
- All eleven sections are required. A missing section throws
  `The save state is missing sections: ...`.

---

## 4. Load-time validation and warnings

`FiveDoMachine.LoadState`:

1. Reads the whole stream into memory (a state is ~25 MB).
2. Verifies the magic and format version.
3. Reads the header strings and frame count.
4. Adds a warning when the writer's emulator version differs from the current
   `FiveDoVersion.Version`.
5. Compares the stored BIOS MD5 with the currently loaded BIOS; adds
   `State BIOS hash differs from the loaded BIOS.` when they differ. The load
   continues.
6. Reads each section, verifies its version and CRC, dispatches to the owner, and
   removes it from the required set.
7. Throws if any required section is missing.
8. Restores `Compatibility.HighResolution` from the Machine section; an explicit
   `--high-res` tuning flag is OR-ed in.
9. Sets `_framesExecuted` from the header and clears `LastFrameCycles`.
10. Calls `AudioSink.Reset()` so live audio drops the old timeline.

**Disc hash:** the header stores `discHash`, and the slot manager uses it as the
displayed game ID, but the loader does not compare it with the inserted disc. Loading a
state from a different disc is therefore allowed; verify the slot metadata/game ID
yourself. The BIOS hash mismatch is also a warning, not a rejection.

**NVRAM:** the Memory section includes NVRAM. Loading a state restores it in the
machine; the host persists it through `NvramStore` on the next flush.

**Audio:** `IAudioSink.Reset` is called on state load. WAV capture intentionally spans
resets and state loads, so a WAV may contain both timelines; live OpenAL drops the old
queue.

---

## 5. Slot layout

The desktop shell uses `SaveStateSlotManager` with a directory and slot count:

```text
<save directory>/
  slot-00.tdos
  slot-00.json
  slot-00.png
  slot-01.tdos
  ...
```

- `NN` is zero-based and zero-padded to two digits (`slot-00` through `slot-99`).
- `slot-NN.tdos` is the native state.
- `slot-NN.json` is an optional metadata sidecar.
- `slot-NN.png` is an optional CPU-assembled thumbnail.
- The default directory is `<LocalApplicationData>/5DO/states`.
- The default slot count is 10; the shell clamps it to 1..100.
- `SaveSlot` selects the current slot and is clamped to the count.

### Metadata sidecar

```json
{
  "Slot": 0,
  "GameId": "0123456789abcdef0123456789abcdef",
  "GameName": "Road Rash (USA)",
  "SavedAtUtc": "2026-10-28T01:23:45.678+00:00",
  "FramesExecuted": 12000
}
```

The sidecar is metadata only. The state header remains authoritative for the game ID
and frame count; the slot inspector reads the header first and falls back to the
sidecar when the state cannot be parsed. A state copied from elsewhere may have no
sidecar or thumbnail.

### Save/load/delete behavior

- **Save:** writes the state atomically (`path + ".tmp"` then move), writes a PNG
  thumbnail from the current CPU frame (failure is non-fatal), writes the sidecar
  atomically, and selects the slot.
- **Load:** requires an existing `.tdos`; loads the state and selects the slot.
- **Delete:** removes the `.tdos`, `.json`, and `.png`.
- **Next/Previous:** wrap at the ends; the UI can skip empty slots.
- **List:** returns every slot, including empty ones, with `Exists`, `GameId`,
  `GameName`, `SavedAtUtc`, `FramesExecuted`, `FileSize`, `Error`, and
  `ThumbnailPath`.

---

## 6. CLI workflows

### Inspect a state

```powershell
dotnet run --project src/FiveDo.Cli -- state inspect artifacts/states/slot-00.tdos
dotnet run --project src/FiveDo.Cli -- state inspect artifacts/states/slot-00.tdos --json
```

`state inspect` performs full CRC validation. JSON is PascalCase (see
[`CliReference.md`](CliReference.md) section 10).

### List slots

```powershell
dotnet run --project src/FiveDo.Cli -- state slots artifacts/states --count 10 --json
```

### Save after a run

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 12000 --state-out artifacts/race.tdos --json
```

### Load and continue

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --state-in artifacts/race.tdos --frames 600 --json
```

`--state-in` restores ROM/RAM/NVRAM/device state, so `--bios` is optional; keep it for
clarity and for the BIOS hash warning. A disc is still required if the state expects
CD data.

### Roundtrip check

```powershell
# Save at frame N, continue uninterrupted for 120 frames
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 12120 --state-out artifacts/check.tdos --json

# Load the state and run 120 frames; compare frameHash
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --state-in artifacts/check.tdos --frames 120 --json
```

The `frameHash` after the load must match the uninterrupted result. The test suite
also covers zero-frame load + two 120-frame continuations for the showcase titles.

---

## 7. Determinism guarantees and limits

Guaranteed for the same:

- emulator version (or a version that only produces warnings),
- BIOS,
- disc image content,
- fix mode/high-resolution settings,
- input sequence,
- state file.

The state includes the derived VDL frame so a load renders the same picture
immediately. The `Machine` section stores all 960 lines, including the high-resolution
mirrors.

Not included:

- The disc image itself (only the hash).
- Input device state (keyboard/gamepad); input is host state.
- OpenAL queue contents; live audio is reset on load.
- Screenshot/log metadata.
- Host settings (`settings.json`).

A state written by a future format version (header version other than 1) is rejected.
A state written by a future section version (other than the supported set) is rejected.
Keep states with the emulator version that wrote them; the version warning exists to
make cross-version loads visible, not to guarantee compatibility.

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `invalid magic value` | Not a `.tdos` file, or the file is corrupted/truncated. |
| `Unsupported save-state version N` | Written by a different format version; use a compatible build. |
| `Section 0x... failed its CRC check` | File corruption or partial copy; restore from a backup. |
| `The save state is missing sections: ...` | Truncated/incomplete file; re-save. |
| `State BIOS hash differs...` warning | The loaded BIOS is not the one used to write the state. The load continues; expect possible divergence. |
| `State was written by version ...` warning | Different emulator version. Verify the roundtrip before relying on it. |
| State loads but audio is silent for a moment | Live OpenAL was reset to drop the old timeline; the DSP resumes on the next frames. |
| State loads on the wrong game | The loader adds a `StateWarnings` entry when the recorded disc hash differs from the inserted disc, but still loads. Check the slot metadata/game ID. |
| Thumbnail missing | The CPU frame was unavailable or the PNG write failed; the state is still valid. |
| Metadata shows `(unnamed)` or unknown time | The sidecar is missing/invalid; the state header still has the game ID and frame count. |

For binary inspection, `state inspect --json` is the supported path; do not hand-edit
`.tdos` files. For format experiments, copy the file first and keep the original.
