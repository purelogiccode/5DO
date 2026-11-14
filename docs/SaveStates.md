# Save States

5DO uses a native, versioned save-state format with the `.tdos` extension. The format
is designed for deterministic continuation of the emulated machine. A best-effort
importer/exporter maps 4DO `.4dosav` files to and from the native format; see section 9.
Native states remain authoritative, and 4DO interoperability is explicitly not a
bit-for-bit compatibility guarantee.

- Writer/reader: `StateWriter` / `StateReader` (`FiveDo.Core`).
- Machine framing: `FiveDoMachine.SaveState` / `LoadState`.
- Slot management: `SaveStateSlotManager` (`FiveDo.Host`).
- Inspector: `StateInspector` (`FiveDo.Host`).
- Diff: `StateDiff` (`FiveDo.Host`) and `state diff`.
- CLI: `state inspect`, `state slots`, `state import-4do`, `state export-4do`;
  `run --state-in`/`--state-out`.
- 4DO interop: `FourDoSaveState` and `FourDoStateConverter` (`FiveDo.Host`).

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
|   version         u16   3 (v1/v2 are still readable)          |
|   headerFlags     u16   featureFlags and sectionTable bits    |
|   emulatorVersion string (i32 byte count + UTF-8 bytes)       |
|   armClock        i32   12,500,000                            |
|   fixMode         u32   FixMode flags                         |
|   biosHash        string (lowercase MD5 hex)                  |
|   discHash        string (game ID, lowercase MD5 hex)         |
|   framesExecuted  i64                                         |
|   featureFlags    u32   (v3; feature bits)                    |
|   sectionCount    u32   (v3; number of table entries)         |
|   section table   [id:u32][version:u16][flags:u16] * count    |
+---------------------------------------------------------------+
| Section 1                                                     |
|   id              u32                                         |
|   version         u16                                         |
|   flags           u16   0 = raw, 1 = Brotli                   |
|   length          u32   stored payload byte count             |
|   payload         byte[length]                                |
|   crc32           u32   CRC-32 of the uncompressed payload    |
+---------------------------------------------------------------+
| Section 2 ...                                                 |
+---------------------------------------------------------------+
```

- All integers are little-endian.
- Strings are a signed 32-bit UTF-8 byte count followed by exactly that many bytes.
  Invalid UTF-8 is rejected.
- `magic` is `TDO1` as ASCII; the little-endian `u32` is `0x314F4454`.
- `version` is `3` for new states. The reader also accepts versions `1` and `2` for
  backward compatibility; those headers have a zero `headerFlags` word and no feature
  word or section table.
- `armClock` is the tuning clock used when the state was written.
- `fixMode` is the applied compatibility mask.
- `biosHash` is the MD5 of BIOS bank 0 as restored from memory. When
  `AllowUnsignedDiscs` applies the in-memory RSA patch, this is the effective patched
  hash; loading the state with an unpatched BIOS reports the normal BIOS-hash warning.
- `discHash` is the host-supplied game ID (MD5 of the first two disc sectors) or empty.
  It is **recorded**, not used to reject a load.
- `framesExecuted` is the machine's frame counter at save time.
- `headerFlags` bit 0 declares the feature word; bit 1 declares the section table.
  Unknown header bits produce a warning instead of rejecting a newer state.
- `featureFlags` records the options snapshot, section table, Brotli eligibility,
  video-region snapshot, and unchecked-section eligibility. Unknown feature bits are
  warned about and ignored.
- The section table lists every section ID/version up front. Duplicate IDs, more than
  4096 entries, non-zero entry flags, and declared-versus-actual version mismatches are
  rejected. Declared sections that are absent produce a warning.

### Section framing

Each section is:

```text
[id:u32][version:u16][flags:u16][length:u32][payload:length][crc32:u32]
```

The CRC is CRC-32/IEEE 802.3 (reflected polynomial `0xEDB88320`, init `0xFFFFFFFF`,
final XOR `0xFFFFFFFF`). The reader verifies the CRC over the **uncompressed** payload
before exposing it. A truncated section, oversized length, or CRC mismatch throws
`InvalidStateException`.

`flags` is `0` for a raw payload and `1` for Brotli. A compressed payload starts with a
little-endian `i32` uncompressed length followed by the Brotli stream; `length` counts
the prefix plus the Brotli bytes. The writer compresses payloads of at least 4 KiB when
the result is smaller, and leaves small or incompressible payloads raw. Flag `2` marks an
unchecked section whose CRC trailer is zero and is not validated. The writer emits it only
for transient in-memory rewind snapshots; persisted `.tdos` files always carry and verify
a CRC. Unknown flag bits and malformed compressed payloads are rejected.

---

## 3. Section table

Sections are identified by stable IDs. `FiveDoMachine.SaveState` writes them in the
order CPU, Memory, MADAM, CLIO, DSP, SPORT, VDLP, XBUS, CD-ROM, Quartz, Options, and
Machine; the table below is sorted by ID. Never renumber an existing section.

| ID | Name | Version | Owner | Payload summary |
|---:|---|---:|---|---|
| 1 | CPU | 1 | `Arm60` | R0-R15, banked SVC/ABT/FIQ/IRQ/UND/SPSR register sets, CPSR, FIQ pending, second-ROM select, data-abort pending, carry-out, LDM scratch. |
| 2 | Memory | 4 | `FiveDoMemory` | ROM bank, PC-adjust flag, ROM overlay, the DRAM/VRAM configuration, the RAM backing allocation size, the live high-resolution RAM range, 2 MB ROM, 32 KB NVRAM. Version 3 and older store the full 22 MB backing; version 4 stores only the live DRAM plus high-resolution VRAM planes and clears the unused tail on load. Version 1-2 states use the historical 19 MB stock backing and load with the stock layout. |
| 3 | MADAM | 4 | `Madam` | 2048+64 register file, 32-entry PLUT, 256-byte PBUS queue, read/write modulo, CEL FSM, matrix result temporaries, pending CEL draw, busy-cycle deadline, and the MCTL-written gate. Version 1 is accepted with the pending flag derived from the FSM and no busy deadline; version 2 adds the busy deadline; version 3 adds the MCTL gate and infers it from MCTL for older states; version 4 expands the PBUS queue from 20 to 256 bytes (older states read 20 and fill the tail with `0xFF`). |
| 4 | CLIO | 3 | `Clio` | 65536-word register file, DSP window words, timer/line state, Opera cascade carry, input/output FIFOs and pointers, DMA state, and the separate 32-bit PRNG. Version 1 is accepted with a cleared carry; version 2 adds the carry; version 3 adds the CLIO PRNG. |
| 5 | DSP | 1 | `Dsp` | PC, noise/audio status, semaphore status/data, counters, interrupt/multiplier/ALU/shifter state, masks, running/FIQ flags, base/seed, IMem/DMem. |
| 6 | SPORT | 1 | `Sport` | Color, source, and destination registers. |
| 7 | VDLP | 4 | `Vdlp` | CLUT B/G/R, 24-bit background, head/current/successor VDL, modulo, current/previous bitmap, out-control, CLUT DMA, line delay. Version 4 stores the 24-bit background; versions 1-3 convert their packed 5-bit value on load. Version 3 separates the retained head from the active successor cursor; versions 1-2 migrate the cursor. |
| 8 | XBUS | 1 | `Xbus` | Select/poll state, status/command FIFOs, and attached-device state. The CD-ROM device is also written as section 9. |
| 9 | CD-ROM | 6 | `CdromDevice` | Poll/status, status/data buffers, command buffer, requested count, MEI status, current sector, readable-disc limit, disc state (tracks/MSF/TOC), cycle clock, next-ready cycle, pending read-status flag, MODE SET sector size/block flags, guest drive-speed mode, PLAY_AUDIO position, CD-DA sample cursor, volume, and mute. Version 1 is accepted with a zeroed clock and pending flag; v2 adds the cycle clock; v3 adds MODE SET and audio position; v4 adds the CD-DA mix; v5 adds the guest drive-speed mode and the volume-header readable-disc limit; v6 stores the full 2449-byte transfer buffer (subcode tail included). Versions 1-5 read their historical 2352-byte data buffer and zero the new tail; they also derive the speed mode from the XBUS 2x bit and the readable limit from the attached disc. The timing mode itself is recorded in the Options section. |
| 10 | Quartz | 4 | `Quartz` | ARM/VDL/timer accumulators, exact rational VDL accumulator, exact DSP cycle remainder, VDL line counter, Opera timer slot/gap flag and 16.16 slot accumulator. Version 4 adds the exact VDL accumulator; versions 1-3 convert their 8.24 VDL fraction on load, and versions 1-2 default the timer scheduler. |
| 11 | Machine | 3 | `FiveDoMachine` | Compatibility flags (fix mode, Anvil, speed fixes, sf/sdf, unknown flag 11, jw, cnbfix, high-res), frame source dimensions/pixel aspect, and the visible `VdlLine` objects (width, control words, 24-bit background, CLUT, and the full pixel backing store). Version 1 wrote all 576 lines; version 2 writes only `SourceHeight` lines and copies pixels as little-endian blocks, which cut the section from ~1.54 MB to ~0.64 MB for a 240-line frame. Version 3 stores the 24-bit VDL background; versions 1-2 are converted from their packed 5-bit value on load. |
| 12 | Options | 9 | `FiveDoMachine` | Every registered machine option: ARM timing mode, CLIO timer mode, MADAM CEL timing mode, CD-ROM timing mode, CD-ROM speed, DRAM/VRAM capacity, live video region, ARM clock, random seed (including whether the default was recorded), high-resolution mode, Opera DIPIR, Opera soft reset, Portfolio OS SWI HLE, fix mask, and CPU backend. Version 1 records only ARM timing; version 2 adds CLIO timers; version 3 adds CEL timing; version 4 adds CD timing; version 5 adds CD speed; version 6 adds the region; version 7 adds the remaining registry entries; version 8 adds the memory capacity; version 9 adds the SWI HLE flag. Loading a lower version leaves missing options at the machine default. |

### Why the state is large

The memory section stores the live RAM backing, including every high-resolution VRAM
plane that the active layout can address. Version 4 writes only the live range: the stock
2 MB DRAM / 1 MB VRAM layout produces a roughly **9.3 MB** uncompressed state, while the
largest 14 MB DRAM / 2 MB VRAM layout reaches about **25 MB**. Machine section v2 stores
only the visible source lines; the load path clears only the previously addressable RAM
tail, so repeated run-ahead/save-state restores do not scan the unused 22 MB allocation.

Version 3 keeps the v2 Brotli policy. On the local corpus the state shrinks to
roughly **0.6-2.0 MB** (a 300-frame Gex state measured 647,647 bytes, a 14,900-frame
state 2,001,229 bytes), so ten slots use a few megabytes instead of ~250 MB. Small
sections (under 4 KiB) and incompressible payloads stay raw. The CRC is always over the
uncompressed payload, and the state inspector reads the section table without
decompressing the payloads. The rewind history uses `CompressionLevel.Fastest` and skips
the CRC because each snapshot is consumed by the same process within seconds; persisted
files always use `CompressionLevel.Optimal` and validate every section.

Version-1 and v2 states are read unchanged; v1 remains uncompressed and ~25 MB. Their
memory section is the historical 19 MB stock backing, so the missing 3 MB is cleared and
the stock 2 MB DRAM / 1 MB VRAM layout is used.
Loading a state written by a newer format version is rejected with
`Unsupported save-state version N.`

### Section version compatibility

- Most sections still write version 1.
- CLIO writes version 2 (Opera cascade carry) and accepts version 1 with a cleared carry.
- MADAM writes version 4 and accepts versions 1-3. Version 2 adds the pending CEL draw
  and busy deadline; version 3 adds the MCTL-written gate; version 4 expands the PBUS
  queue from 20 to 256 bytes. Version 1 derives the pending flag from the FSM and has no
  busy deadline; versions 1-3 read the historical 20-byte queue and fill the tail with
  `0xFF`.
- Quartz writes version 4 (exact rational VDL accumulator plus the exact DSP clock and
  Opera timer slot state) and accepts versions 1-3. Version 1 converts the legacy 8.24
  fractional sample position to ARM cycles; versions 1-2 default the timer slot state to
  a reset scheduler; version 3 converts the legacy 8.24 VDL fraction to the exact
  accumulator.
- CD-ROM writes version 6 (MODE SET, guest drive-speed mode, readable-disc limit,
  PLAY_AUDIO position, CD-DA cursor, volume, mute, and the full 2449-byte transfer
  buffer) and accepts versions 1-5. Version 1 uses a zeroed clock and pending flag;
  versions 1-2 default the MODE SET/audio fields; version 3 defaults the CD-DA mix;
  version 4 derives the guest drive-speed mode from the XBUS 2x bit and the readable
  limit from the attached disc; versions 1-5 read the historical 2352-byte data buffer
  and zero the 97-byte subcode tail added in v6. The timing mode itself is in the
  Options section.
- Memory writes version 4 and accepts versions 1-3. Version 3 adds the DRAM/VRAM
  configuration and the 22 MB allocation size. Version 4 stores only the live
  high-resolution backing range and clears the unused tail on load. Older states carry
  the historical 19 MB stock backing and load with the stock layout.
- Options writes version 9 and accepts versions 1-8. Version 1 records ARM timing only;
  version 2 adds CLIO timers; version 3 adds CEL timing; version 4 adds CD timing;
  version 5 adds CD speed; version 6 adds the video region; version 7 adds the ARM clock,
  random seed, high-resolution mode, Opera DIPIR, Opera soft reset, fix mask, and CPU
  backend; version 8 adds the DRAM/VRAM capacity; version 9 adds the Portfolio OS SWI HLE
  flag. Missing fields stay at the machine default. Every registered machine option is
  covered, and loading a state whose value
  differs from the running machine adds a
  `State <option> <saved> differs from the machine <current>` warning. A differing video
  region or memory layout is applied to the machine; VRAM is located above DRAM, so the
  state's RAM contents only decode correctly with the layout that produced them. Timing
  profiles are a host-side shortcut over these options and are derived from them rather
  than stored separately.
- VDLP writes version 4 and accepts versions 1-3. Version 3 separates the retained head
  from the active successor cursor; version 4 stores the 24-bit background. Versions 1-3
  convert their packed 5-bit background on load.
- Machine writes version 3 and accepts versions 1-2. Version 3 stores the 24-bit
  `VdlLine.Background`; versions 1-2 convert their packed 5-bit value on load.
- Any other section version is rejected with
  `Unsupported section <id> version <n>.`
- Unknown section IDs are ignored and recorded as a warning:
  `Ignored unknown state section <id>.`
- All eleven core sections are required. The Options section is optional for
  backward compatibility. A missing required section throws
  `The save state is missing sections: ...`.
- The outer state version is 3. Versions 1 and 2 are accepted; version 2 adds Brotli
  section flags; version 3 adds feature flags and the section table. A future outer
  version is rejected.

---

## 4. Load-time validation and warnings

`FiveDoMachine.LoadState`:

1. Reads the whole stream into memory (a version-2/3 state is typically under 2 MB;
   a version-1 state is ~25 MB).
2. Verifies the magic and format version. Versions 1, 2, and 3 are accepted.
3. Reads the header strings and frame count.
4. Adds a warning when the writer's emulator version differs from the current
   `FiveDoVersion.Version`.
5. Compares the stored BIOS MD5 with the currently loaded BIOS; adds
   `State BIOS hash differs from the loaded BIOS.` when they differ. The load
   continues.
6. Reads each section, verifies its flags and version, decompresses Brotli payloads
   when flagged, verifies the CRC over the uncompressed payload, dispatches to the
   owner, and removes it from the required set.
7. Throws if any required section is missing.
8. Restores `Compatibility.HighResolution` from the Machine section; an explicit
   `--high-res` tuning flag is OR-ed in. Compares every Options entry with the running
   machine and adds mismatch warnings; a differing video region switches the machine and
   clears the stale frame before the Machine section restores it. A differing memory
   capacity re-applies the state layout after the Memory section has loaded, so DRAM/VRAM
   contents keep their original addresses.
9. Sets `_framesExecuted` from the header and clears `LastFrameCycles`.
10. Calls `AudioSink.Reset()` so live audio drops the old timeline.

**Disc hash:** the header stores `discHash`, and the slot manager uses it as the
displayed game ID. When the host supplies the inserted disc's game ID and it differs,
the loader adds `State disc hash differs from the loaded disc.` to `StateWarnings` but
still loads; verify the slot metadata/game ID before relying on the continuation. The BIOS hash mismatch is also a warning, not a rejection.

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

`state inspect` performs full CRC validation, including Brotli decompression. The text
output reports the file's stored size and marks compressed sections with their stored
size; `--json` includes `Compressed` and `StoredLength`. JSON is PascalCase (see
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
immediately. The `Machine` section stores every visible `VdlLine` (version 2+ writes
only `SourceHeight` lines, including high-resolution mirrors) with the full pixel
backing store and the 24-bit background.

Not included:

- The disc image itself (only the hash).
- Input device state (keyboard/gamepad); input is host state.
- OpenAL queue contents; live audio is reset on load.
- Screenshot/log metadata.
- The presentation field-handling mode, previous-field snapshot, and `VdlFrame.PixelAspect`.
  Loading always restarts from the raw 240/288-line field; the configured mode rebuilds
  its pair on the next two fields.
- Host settings (`settings.json`).

A state written by a future format version (header version other than 1, 2, or 3) is
rejected. A state written by a future section version (other than the supported set)
is rejected. Version-1 states load unchanged; version-2 states use Brotli section
compression; version-3 states add feature flags and a section table. Keep states with
the emulator version that wrote them; the version warning exists to make
cross-version loads visible, not to guarantee compatibility.

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `invalid magic value` | Not a `.tdos` file, or the file is corrupted/truncated. |
| `Unsupported save-state version N` | Written by a different format version; use a compatible build. |
| `Section 0x... failed its CRC check` | File corruption or partial copy; restore from a backup. |
| `Section 0x... could not be decompressed` | A compressed section is corrupt; the state is unusable. Restore from a backup. |
| `Section 0x... has unsupported flags` | Written by a newer format that uses unknown section flags; use a compatible build. |
| `The save state is missing sections: ...` | Truncated/incomplete file; re-save. |
| `State BIOS hash differs...` warning | The loaded BIOS is not the one used to write the state. The load continues; expect possible divergence. |
| `State was written by version ...` warning | Different emulator version. Verify the roundtrip before relying on it. |
| State loads but audio is silent for a moment | Live OpenAL was reset to drop the old timeline; the DSP resumes on the next frames. |
| State loads on the wrong game | The loader adds a `StateWarnings` entry when the recorded disc hash differs from the inserted disc, but still loads. Check the slot metadata/game ID. |
| Thumbnail missing | The CPU frame was unavailable or the PNG write failed; the state is still valid. |
| Metadata shows `(unnamed)` or unknown time | The sidecar is missing/invalid; the state header still has the game ID and frame count. |

For binary inspection, `state inspect --json` is the supported path; do not hand-edit
`.tdos` files. For format experiments, copy the file first and keep the original.

---

## 9. 4DO state interoperability (UX-30)

4DO writes `.4dosav` files as the raw `_3do_Save` buffer from FreeDOCore. The container
is versionless and depends on the C structure layout of the producing build. 5DO reads
the 32-bit and 64-bit ARM layouts used by current builds, maps the architectural fields,
and preserves the sections 4DO does not record.

### Container

```text
+---------------------------------------------------------------+
| 64-byte index table                                           |
|   int32[0]  magic 0x97970101                                  |
|   int32[1]  64 (ARM record start)                             |
|   int32[2]  VDL record start                                  |
|   int32[3]  DSP record start                                  |
|   int32[4]  CLIO record start                                 |
|   int32[5]  Quartz record start                               |
|   int32[6]  SPORT record start                                |
|   int32[7]  MADAM record start                                |
|   int32[8]  XBUS record start                                 |
|   int32[9]  end of file                                       |
+---------------------------------------------------------------+
| ARM record                                                    |
|   three pointer fields (12 or 24 bytes, ignored on load)      |
|   USER[16], CASH[7], SVC[2], ABT[2], FIQ[7], IRQ[2], UND[2],  |
|   SPSR[6], CPSR, nFIQ, SecondROM, MAS_Access_Exept            |
|   3 MB DRAM/VRAM, 2 MB ROM, 32 KB NVRAM                       |
+---------------------------------------------------------------+
| VDLP (132), DSP (73,029), CLIO (262,496), Quartz (32),         |
| SPORT (12), MADAM (8,544), XBUS (268)                         |
+---------------------------------------------------------------+
```

The ARM record size reveals the pointer width: 12 bytes means a 32-bit build and
24 bytes means a 64-bit build. The core ignores the pointer values and rebinds its own
buffers. The DSP record starts with `RBASEx4`, followed by the decoded `INSTTRAS` cache;
5DO reads the fixed tail from the end of the record, so a larger cache does not shift
the fields.

### Import

```powershell
5do state import-4do run.4dosav --bios bios/panafz1.bin --out run.tdos
5do state import-4do run.4dosav --bios-auto bios --out run.tdos --json
```

Import builds a base 5DO state from a freshly reset machine with the supplied BIOS and
then replaces the CPU, Memory, MADAM, CLIO, DSP, SPORT, VDLP, Quartz, and XBUS sections.
The base state's CD-ROM drive, derived video frame, machine options, and controller
chain are preserved because 4DO does not store them. The command prints a `note:` line
for every unsupported field; `--json` returns the same notes in `warnings`.

Mapped state:

- **CPU:** all register banks, CPSR, FIQ pending, second-ROM select, and the
  `MAS_Access_Exept` latch.
- **Memory:** the 3 MB DRAM/VRAM image is byte-swapped from FreeDOCore's big-endian
  words into 5DO's word representation; the stock 16 high-resolution mirror planes are
  rebuilt from the VRAM bank. The 2 MB ROM and 32 KB NVRAM are mapped directly.
- **MADAM:** 2048+64 registers, PLUT, the 20-byte PBUS queue prefix (the 256-byte 5DO
  queue is filled with `0xFF`), read/write modulo, and the CEL FSM.
- **CLIO:** 65,536 registers, DSP window words, input/output FIFO pointers and
  descriptors. The timer accumulator is cleared as FreeDOCore does on load, and the two
  scope-only timer fields plus the 5DO PRNG seed start at their defaults.
- **DSP:** PC, status/counters, ALU/multiplier/shifter latches, masks, running/FIQ flags,
  `RBASEx4`, register index, seed, CPU-supply flags, data memory, and instruction memory.
  The decoded instruction cache and convolution/branch tables are rebuilt from IMem.
- **SPORT and Quartz:** all persisted fields. 4DO's 8.24 DSP/VDL fractions are converted
  to 5DO's exact cycle/accumulator units; the timer slot restarts at zero.
- **VDLP:** CLUTs, background, head/current/modulo/bitmap/control/DMA/line-delay fields.
  4DO has no successor cursor, so 5DO resumes from the current cursor.
- **XBUS:** select/poll/FIFO fields are overlaid on the base state's controller chain.

### Export

```powershell
5do state export-4do run.tdos --out run.4dosav
5do state export-4do run.tdos --out run.4dosav --json
```

Export writes the 32-bit ARM structure because the 4DO reference application is x86,
zeroes the ignored pointer fields, and emits the VDLP, DSP, CLIO, Quartz, SPORT, MADAM,
and XBUS records. The 5DO-only fields (decoded VDL frame, CD-ROM drive, machine options,
controller attachments, exact timer slot, and matrix temporaries) are omitted and
reported as warnings. A stock `2 MB DRAM / 1 MB VRAM` state is required for a faithful
4DO image; expanded DRAM or VRAM layouts are reported and only the stock window is
exported.

### Tests and limits

`FourDoStateTests` covers container validation, pointer-layout detection, field mapping,
a full import/export/import round-trip, and malformed records. `CliProcessTests` covers
both CLI commands. The format is best-effort: a state from a modified FreeDOCore or a
future 4DO structure revision is rejected with a clear size error rather than silently
misread. 4DO input devices are host-side, so controller attachments do not transfer in
either direction.
