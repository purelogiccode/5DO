# CLI Reference

`FiveDo.Cli` is the headless command-line interface. It is intended for automation,
CI, frame/state/audio capture, and debugging. The desktop app is documented in
[`UserGuide.md`](UserGuide.md).

- Executable name in published packages: `FiveDo.Cli` (`.exe` on Windows).
- From source: `dotnet run --project src/FiveDo.Cli -- <command> ...`
- Version: `--version` or `-v`.
- Help: `--help`, `-h`, or `help`.
- Exit codes: `0` success, `1` handled error, `2` unknown command.
- The benchmark has additional exit codes: `3` performance gate failure, `4` baseline
  regression failure.

---

## 1. Argument rules

- Options are `--name value`. Boolean flags are bare (`--json`, `--realtime`,
  `--high-res`, `--prefetch`, `--no-game-fixes`).
- A flag followed by a non-`--` token consumes that token as its value. Put boolean
  flags last or before another `--option` to avoid ambiguity.
- The first non-option token is the positional path for `run`, `trace`, `info`,
  `disc dump`, `disc tracks`, `bios info`, `audio analyze`, `state inspect`,
  `disasm` (disc or file, depending on the command). `fs` uses fixed positional
  subcommand/path positions.
- Integer options accept decimal and, where noted, `0x` hexadecimal. Some address
  options (`--watch-start`, `--watch-end`, `--stop-pc`, `--stop-cd`,
  `--dump-ram-offset`, `--base`) are parsed as **hexadecimal even without `0x`**.
- Paths may be absolute or relative to the current directory. Output directories are
  created when the command writes files.
- `--json` is accepted by commands that have structured output. It is ignored by
  `trace` and `disasm`, which are text-only.
- JSON field casing is mixed by design: most command results are camelCase
  (`run`, `info`, `bios`, `audio`, `bench`, `selftest`, `fs`); `state inspect` and
  `state slots` preserve PascalCase property names for historical compatibility.
- Errors with `--json` produce `{ "ok": false, "error": "..." }` and exit 1.
  Unknown commands produce the same shape (or a plain stderr line) and exit 2.

---

## 2. Command summary

| Command | Purpose |
|---|---|
| `run [disc]` | Boot a BIOS/disc or load a state, run frames, capture output. |
| `trace [disc]` | Run with a diagnostic instruction ring and stop conditions. |
| `info <disc>` | Disc geometry, volume header, game ID, database metadata, fixes. |
| `disc dump <disc>` | Write raw 2048-byte sectors. |
| `disc tracks <disc>` | List CHD track table entries, or one data track for ISO/BIN/CUE. |
| `fs list <disc>` | List the 3DO file tree (CHD container). |
| `fs extract <disc>` | Extract files from a 3DO disc image (CHD container). |
| `bios info <path>` | BIOS size, MD5, Anvil detection. |
| `audio analyze <wav>` | WAV format, peak, RMS, clipping, silence, DC offset. |
| `state inspect <path>` | Save-state header and section table (full CRC validation). |
| `state slots <dir>` | List numbered save slots and metadata. |
| `disasm <file>` | Disassemble raw little-endian ARM words. |
| `bench` | Synthetic-ROM frame benchmark (or real content with options). |
| `selftest` | Determinism and state roundtrip checks. |
| `--version`, `--help` | Identity/help. |

---

## 3. `run`

### Synopsis

```text
5do run [disc] --bios <path> [options]
5do run [disc] --state-in <path> [options]
```

`--bios` is required unless `--state-in` supplies the ROM/RAM state. A disc is optional
for a BIOS-only boot. `--state-in` restores ROM, RAM, NVRAM, device state, and the
current VDL frame.

### Order of operations

1. Open the disc (if any) and compute its game ID.
2. Resolve the fix mode: explicit `--fix-mode`, else `--no-game-fixes`, else the 4DO
   database mapping for the game ID.
3. Create the machine with `--high-res` and the resolved fix mode.
4. Load BIOS1/BIOS2 (unless `--state-in`).
5. Create the audio backend and insert the disc.
6. Reset the machine.
7. Load NVRAM (`--nvram` if it exists, else a fresh image) unless `--state-in`.
8. Load the input script, validate `--script-base`, and set up `--autopilot`.
9. Install trace writers/watchpoints/KPRINT hooks.
10. Load `--state-in` (and offset the script if `--script-base state`).
11. Apply `--poke` patches.
12. Run frames (paced only with `--realtime`).
13. Write RAM/DSP dumps, dispose traces, write `--frame-out`, write `--state-out`.
14. Emit the result and, if `--nvram` was supplied, save NVRAM.

### Emulation options

| Option | Default | Meaning |
|---|---:|---|
| `--bios <path>` | none | BIOS1 path (exactly 1 MB). Required unless `--state-in`. |
| `--frames N` | 600 | Number of frames to run. Decimal or `0x`. |
| `--state-in <path>` | none | Load a `.tdos` state instead of booting. |
| `--state-out <path>` | none | Save a `.tdos` state after the run. |
| `--nvram <path>` | none | Load a 32 KB NVRAM image before the run and save it after. Missing file means a fresh image. |
| `--high-res` | off | Enable the opt-in 640x480 CEL/VDLP mode. |
| `--prefetch` | off | Enable the bounded disc read-ahead cache (32 sectors ahead, 128-sector capacity). |
| `--fix-mode 0xMASK` | database | Explicit `FixMode` bits. Accepts hex with or without `0x`. Overrides `--no-game-fixes`. |
| `--no-game-fixes` | off | Disable automatic 4DO fixes. |
| `--poke ADDR=HEX[;...]` | none | Patch RAM bytes after state load. Address decimal or `0x`; bytes hex with optional spaces/commas. |
| `--realtime` | off | Pace the run against the audio clock instead of running as fast as possible. |

### Audio options

| Option | Default | Meaning |
|---|---|---|
| `--audio null|wav|openal` | `null` | Audio backend. `null` still executes the DSP but discards samples. |
| `--wav-out <path>` | `audio.wav` | WAV destination for `--audio wav`. |
| `--volume 0..1` | 1.0 | OpenAL gain. Invalid values fail. |

`--audio wav` writes 44.1 kHz, 16-bit stereo PCM and reports capture statistics.
`--audio openal` opens the default OpenAL Soft device; if no device is available it
prints a warning and runs silently. `--realtime` only affects pacing, not the backend.

### Input and automation options

| Option | Default | Meaning |
|---|---|---|
| `--input-script <path>` | none | Frame-stamped controller script. |
| `--script-base absolute|state` | `absolute` | `state` shifts script frames by the loaded state's frame count. Requires `--state-in`. |
| `--autopilot road` | none | Road Rash autopilot. Cannot be combined with `--input-script`. |
| `--progress N` | 0 (off) | Print a progress line to stderr every N frames. |

Input script format:

```text
# frame  buttons
0        None
120      A
180      A,Down
240      None
```

Frames are absolute emulated frame numbers (decimal or `0x`). Button names are
case-insensitive `A`, `B`, `C`, `P`, `X`, `L`, `R`, `Up`, `Down`, `Left`, `Right`.
Separators are comma, plus, or vertical bar. `None` or an empty field releases all
buttons. The state persists until the next entry. Duplicate frames keep the last entry.
With `--state-in`, a script starting at `0` applies its last entry immediately unless
`--script-base state` is used.

### Output options

| Option | Default | Meaning |
|---|---:|---|
| `--frame-out <path>` | none | Write the final frame. `.png` writes PNG; any other extension writes BMP. |
| `--capture-dir <dir>` | none | Write periodic frame PNGs. Requires `--capture-every > 0`. |
| `--capture-every N` | 0 | Capture every N frames after execution. Files are `f000123.png`. |
| `--dump-ram <path>` | none | Write raw CPU-visible RAM bytes after the run. |
| `--dump-ram-offset N` | 0 | Byte offset. Parsed as hex even without `0x`. |
| `--dump-ram-length N` | 0x1000 | Byte count. Decimal or `0x`. |
| `--dump-dsp <prefix>` | none | Write `<prefix>.imem.bin` and `<prefix>.dmem.bin` (little-endian `ushort` words). |
| `--json` | off | Emit a structured result instead of text. |

`--frame-out` and `--capture-dir` use the CPU `FrameAssembler`, so the image is the
accuracy oracle, not the filtered GPU output.

### Diagnostic options

| Option | Meaning |
|---|---|
| `--cd-trace <path>` | CD command and sector trace. |
| `--clio-trace <path>` | CLIO register-write and DMA trace. |
| `--fiq-trace <path>` | FIQ reason trace. |
| `--kprint <path>` | KPRINT character stream (CR/LF become newlines). |
| `--kprint-pc <path>` | One line per KPRINT value with PC, LR, SP, and eight stack words. |
| `--watch-lowmem <path>` | RAM write watchpoint. |
| `--watch-from-frame N` | First frame to record. Decimal or `0x`. |
| `--watch-start N` | First watched address. Parsed as hex even without `0x`. |
| `--watch-end N` | One-past-last watched address. Parsed as hex even without `0x`. |
| `--cel-trace <path>` | CEL command/geometry/progress trace. |
| `--pbus-trace <path>` | One PBUS packet per frame. |

File formats are documented in [`Diagnostics.md`](Diagnostics.md) and summarized in
section 12 below.

### `run` text output

```text
frames:      600
elapsed:     0.123 s
cycles/frm:  208333
source:      320x240
vdl modulo:  320
pc:          0x0012B3C8
frame hash:  0x932C7722
samples:     441015
audio:       10.000 s, peak 32767, RMS 8123.4, clipped 0, silent 0
disc hash:   0123456789abcdef0123456789abcdef
game id:     0123456789abcdef0123456789abcdef
fix mode:    0x00000000
dsp running: True
dsp pc:      0x0000
dsp sema:    0x00000000
dsp seed:    0x00000000
```

`openal:` is printed instead of `audio:` when `--audio openal` is used. It reports
dropped samples, underruns, queued buffers, buffered frames, and estimated latency.

### `run --json` schema

All fields are camelCase. Nullable fields are omitted or `null` when not applicable.

| Field | Type | Meaning |
|---|---|---|
| `ok` | bool | Always `true` on success. |
| `frames` | long | `machine.FramesExecuted` after the run. |
| `elapsedSeconds` | double | Wall-clock time of the run loop. |
| `cyclesPerFrame` | int | ARM cycles in the last frame. |
| `sourceWidth`, `sourceHeight` | int | Visible source dimensions (640x480 in high-res). |
| `vdlModulo` | uint | VDLP line width. |
| `pc` | uint | CPU program counter at the end. |
| `instructions` | long | Instructions executed since reset/state load. |
| `frameHash` | uint | FNV-1a hash of the CPU-assembled BGRA frame. |
| `samples` | long | WAV stereo frames or OpenAL pushed+dropped samples. |
| `discHash` | string | Game ID (MD5 of first two sectors), or empty. |
| `gameId` | string? | Same game ID, or null when no disc. |
| `fixMode` | uint | Applied fix-mode bits. |
| `dspRunning` | bool | DSP running flag. |
| `dspPc` | uint | DSP program counter. |
| `dspSema` | uint | `(status << 16) | data`. |
| `dspSeed` | uint | DSP PRNG seed. |
| `cdStatus` | byte | CD drive status byte. |
| `cdPoll` | byte | CD poll byte. |
| `autopilot` | string? | `"road"` when the autopilot ran. |
| `autopilotLineX` | double? | Detected road line X. |
| `autopilotRoadCoverage` | double? | Road coverage estimate. |
| `autopilotSteering` | int? | Steering command. |
| `autopilotObstacle` | int? | Obstacle avoidance steering. |
| `autopilotRecovery` | bool? | Recovery mode active. |
| `autopilotCrashRestart` | bool? | Crash restart active. |
| `autopilotMenuAdvance` | bool? | Menu advance active. |
| `autopilotStaticFrames` | int? | Static-frame counter. |
| `autopilotPacing` | bool? | Urban pacing active. |
| `autopilotUpset` | bool? | Detector upset flag. |
| `autopilotLookahead` | double[]? | Left/centre/right lookahead. |
| `audioDurationSeconds` | double? | WAV capture duration. |
| `audioPeak` | int? | Peak absolute sample (0..32768). |
| `audioRms` | double? | Combined RMS in sample units. |
| `audioClippedFrames` | long? | Frames where either channel hit full scale. |
| `audioSilentFrames` | long? | Frames where both channels were zero. |
| `audioDcOffsetLeft`, `audioDcOffsetRight` | double? | DC offset in sample units. |
| `audioDroppedSamples` | long? | OpenAL ring-full drops. |
| `audioUnderrunBuffers` | long? | Silent filler buffers queued after a dry ring. |
| `audioQueuedBuffers` | long? | Buffers attached to the OpenAL source at the end. |
| `audioBufferedFrames` | int? | Frames waiting in the emulation ring. |
| `audioLatencyMilliseconds` | double? | `(queuedFrames + bufferedFrames) * 1000 / 44100`. |
| `warnings` | string[] | `machine.StateWarnings` after a state load. |

`cdStatus=0xE1` and `cdPoll=0x0C` is the historical static READ_ID-only wait signature.

---

## 4. `trace`

### Synopsis

```text
5do trace [disc] --bios <path> [--frames N] [--trace-frames N] [options]
```

`trace` runs `--frames` frames first, then traces up to `--trace-frames` frames with an
instruction ring buffer. It stops early on a stop condition or `--count` limit.

### Options

| Option | Default | Meaning |
|---|---:|---|
| `--bios <path>` | none | BIOS1. Required unless `--state-in`. |
| `--state-in <path>` | none | Load a state before tracing. |
| `--frames N` | 0 | Frames to run before tracing. |
| `--trace-frames N` | 1 | Frames to trace. |
| `--ring N` | 4096 | Instruction ring capacity. |
| `--count N` | 0 | Stop after N observed instructions; 0 means no limit. |
| `--stop-pc 0xADDR` | none | Stop before executing this PC. Parsed as hex even without `0x`. |
| `--stop-kprint <text>` | none | Stop when the KPRINT line ends with this text. |
| `--stop-cd 0xCMD` | none | Stop when the CD executes this command byte. Parsed as hex. |
| `--stop-invalid` | off | Stop when PC >= `0x04000000`. |
| `--out <path>` | stdout | Write the trace text to a file. |
| `--dsp-trace <path>` | none | Write the DSP instruction ring. |
| `--dump-dsp <prefix>` | none | Write DSP IMem/DMem after the trace. |
| `--dump-ram <path>` | none | Write RAM after the trace. |
| `--dump-ram-offset N` | 0 | Hex byte offset. |
| `--dump-ram-length N` | 0x1000 | Byte count. |
| `--nvram <path>` | none | Load NVRAM before, save after. |
| `--input-script <path>` | none | Scripted input (absolute frames only). |
| `--high-res`, `--prefetch` | off | As in `run`. |
| `--fix-mode`, `--no-game-fixes` | database | As in `run`. |

### Output format

```text
# stopped at 0x0012B3C8 after 4096 instructions
# pc=0012B3C8 cpsr=60000010 r0=... r1=... ... sp=... lr=...
# dsp running=True pc=0000 sema=0000/0000 prld=... audcnt=... seed=...
# stack: 00000000 00000000 ...
0012B3B0 e6000010 00000000 mov r0, r0
...
```

Each ring line is `<pc> <cpsr> <instruction> <disassembly>`. The ring holds the most
recent N instructions. `--stop-kprint` matches the rolling text after the last CR/LF;
`--stop-cd` observes the command opcode; `--stop-invalid` catches a PC outside the
mapped low region.

`trace` has no JSON mode.

---

## 5. `info`

```powershell
dotnet run --project src/FiveDo.Cli -- info game.chd
dotnet run --project src/FiveDo.Cli -- info game.chd --json
```

Fields: `path`, `sectorCount`, `volumeLabel`, `volumeBlockCount`, `blockSize`,
`gameId`, `fixMode`, `known`, `name`, `publisher`, `releaseYear`, `regions`.
`gameId` is the lowercase MD5 of the first two 2048-byte sectors. The volume header's
block count is the authoritative disc size for the emulated CD device.

Text output includes the path, sector count, label, blocks, game ID, fix mode, and
either the database title or `unknown`.

---

## 6. `disc`

### `disc dump`

```powershell
5do disc dump game.chd --out sector0.bin --sector 0 --count 1
```

Writes `count` raw 2048-byte user-data sectors starting at `--sector` (default 0) to
`--out`. `--sector` and `--count` accept decimal or `0x`. Works for ISO/BIN/CUE/CHD.
No JSON.

### `disc tracks`

```powershell
5do disc tracks game.chd
```

Lists the CHD track table: `track <n>: type=<type> dataSize=<n> frames=<n> start=<n>`.
For ISO, BIN, and CUE images it reports the single 2048-byte user-data track:
`track 1: type=data format=<iso|bin|cue> dataSize=<n> frames=<n> start=0`.
No JSON.

---

## 7. `fs`

```powershell
5do fs list game.chd
5do fs list game.chd --json
5do fs extract game.chd --out extracted/ --filter GAME/
```

`fs` mounts the 3DO file system through `VideoGameFileSystemParser`'s `ChdContainer`.
**The current implementation is CHD-only.** Use `disc dump` for raw ISO/BIN sector
inspection.

- `list` prints `volume=<name> entries=<n>` and a tree of `[D]`/`[F]` entries with size
  and LBA. JSON: `{ "ok": true, "entries": [{ "path", "directory", "size", "lba" }] }`.
- `extract` requires `--out`. `--filter` is a case-insensitive path prefix. Extraction
  refuses entries that would escape the output directory. JSON:
  `{ "ok": true, "extracted": N, "output": "<full path>" }`.

---

## 8. `bios info`

```powershell
5do bios info panafz1.bin
5do bios info panafz1.bin --json
```

Fields: `path`, `size`, `md5`, `anvil`, `anvilKind`. BIOS1 must be exactly 1 MB. Anvil
is detected by `byte[28] == 234`; `anvilKind` is 30 when true, else 0. Text output
prints the same fields.

---

## 9. `audio analyze`

```powershell
5do audio analyze capture.wav
5do audio analyze capture.wav --json
```

Supports 16-bit signed PCM, mono or stereo, any sample rate. Fields: `path`,
`sampleRate`, `channels`, `bitsPerSample`, `frames`, `durationSeconds`, `dataBytes`,
`peak`, `rms`, `clippedFrames`, `silentFrames`, `dcOffsetLeft`, `dcOffsetRight`.

Definitions:

- `peak`: largest absolute sample, 0..32768.
- `rms`: `sqrt((sum(left^2) + sum(right^2)) / (2 * frames))`, in sample units.
- `clippedFrames`: frames where either channel is `short.MinValue` or `short.MaxValue`.
- `silentFrames`: frames where both channels are exactly zero.
- `dcOffset*`: mean sample value per channel.

See [`Audio.md`](Audio.md) for interpretation.

---

## 10. `state`

### `state inspect`

```powershell
5do state inspect quick.tdos
5do state inspect quick.tdos --json
```

Reads the full file and validates every section CRC. JSON is PascalCase:

```json
{
  "Version": 1,
  "EmulatorVersion": "1.0.0-rc.1",
  "ArmClock": 12500000,
  "FixMode": 0,
  "BiosHash": "....",
  "DiscHash": "....",
  "FramesExecuted": 12000,
  "Sections": [
    { "Id": 1, "Version": 1, "PayloadLength": 0 }
  ]
}
```

Text output prints the header fields and one `section <id>: v<version>, <bytes> bytes`
line per section. See [`SaveStates.md`](SaveStates.md) for the binary layout.

### `state slots`

```powershell
5do state slots artifacts/states
5do state slots artifacts/states --count 10 --json
```

Lists `slot-00` through `slot-(count-1)` in the directory. `--count` defaults to 10.
JSON is a PascalCase array of `SaveStateSlotInfo`:

| Field | Meaning |
|---|---|
| `Slot` | Zero-based slot number. |
| `StatePath` | `.tdos` path. |
| `Exists` | Whether the state file exists. |
| `GameId` | `DiscHash` from the state header, or sidecar metadata. |
| `GameName` | Sidecar display name, or empty. |
| `SavedAtUtc` | Sidecar timestamp, or null. |
| `FramesExecuted` | Frame count from the state header, or -1 on error. |
| `FileSize` | State file size. |
| `Error` | Why the state could not be inspected, or empty. |
| `ThumbnailPath` | PNG thumbnail path, or empty. |

---

## 11. `disasm`

```powershell
5do disasm artifacts/ram.bin --offset 0x12380 --count 64 --base 0x12380
```

Reads raw little-endian 32-bit ARM words. `--offset` is a byte offset (decimal or
`0x`), `--count` is a word count (default 32), and `--base` is the address printed for
the first word (parsed as hex even without `0x`, default 0). Output:

```text
00012380 e3a00000 mov r0, #0
```

No JSON.

---

## 12. `bench` and `selftest`

### `bench`

```powershell
5do bench --frames 600
5do bench --frames 600 --json
```

Runs a synthetic ARM program (MADAM/SPORT writes and a branch loop), warms up 10
frames, then measures `--frames` frames. Fields: `ok`, `frames`, `seconds`,
`framesPerSecond`, `cyclesPerFrame`, `instructions`, `frameHash`.

The separate benchmark project (`benchmarks/FiveDo.Benchmarks`) adds p50/p95/p99,
per-stage timings/allocations, the CI gate, and baseline comparison. See
[`BuildAndTest.md`](BuildAndTest.md) section 4.

### `selftest`

```powershell
5do selftest
5do selftest --json
```

Checks:

1. two synthetic machines produce the same frame hash;
2. the frame hash is non-zero;
3. a save/load roundtrip then 30 frames matches the uninterrupted machine;
4. the loaded state has no warnings.

Exit code is 1 if any check fails. JSON: `{ "ok": true, "checks": [{ "name", "ok" }] }`.

---

## 13. Diagnostic file formats

| Option | Format |
|---|---|
| `--cd-trace` | `cd f=<frame> pc=<pc> cmd <7 hex bytes>`; `cd read sector <n>`. |
| `--clio-trace` | `clio f=<frame> write <addr:x3>=<value:x8> delay=<n>`; `clio dma f=... select=... target=... len=... first=...`. |
| `--fiq-trace` | `f=<frame> pc=<pc> reason1=<hex> reason2=<hex>`. |
| `--kprint` | Decoded characters; CR/LF become newlines; control characters below space are skipped. |
| `--kprint-pc` | `<pc> <byte> lr=<lr> sp=<sp> <8 stack words>`. |
| `--watch-lowmem` | `f=... pc=... addr=... value=... size=... r0=... r1=... r2=... r3=... sp=... lr=... stack=<6 words>caller=<frames>`. |
| `--cel-trace` | `cel <n> <kind> ccb=... next=... flags=... pre0=... pre1=... data=... bpp=... size=WxH pos=... h=... v=... hd=... clip=... src=... dst=...`; plus `progress`, `arb`, `ccbdata`, `done` lines. |
| `--pbus-trace` | `f=<frame> pbus=<32 hex chars>`. |
| `--progress` | stderr: `frame <n> pc=... dsp=... run=... sema=... audio=...` plus autopilot fields. |
| `--dump-ram` | Raw bytes from CPU-visible RAM. |
| `--dump-dsp` | `<prefix>.imem.bin` (1024 words) and `<prefix>.dmem.bin` (2048 words), little-endian 16-bit. |
| `--frame-out` | PNG if the extension is `.png`, otherwise BMP. |
| `--capture-dir` | `f%06d.png` at frame multiples of `--capture-every`. |
| `--wav-out` | 44.1 kHz, 16-bit, stereo RIFF/WAVE. |
| `--state-out` | Native `.tdos` save state. |
| `--gl-probe` | JSON report; see [`Video.md`](Video.md). |

---

## 14. Examples

### Boot and capture a frame

```powershell
dotnet run --project src/FiveDo.Cli -- run game.cue --bios panafz1.bin `
  --frames 600 --frame-out artifacts/frame.png --json
```

### Capture 10 seconds of audio and analyze it

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 600 --audio wav --wav-out artifacts/capture.wav --json

dotnet run --project src/FiveDo.Cli -- audio analyze artifacts/capture.wav --json
```

### Live OpenAL playback paced at the audio clock

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 600 --audio openal --volume 0.5 --realtime
```

### Load a state, apply a script, save a new state

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --state-in artifacts/race.state --frames 1200 `
  --input-script docs/scripts/lem-fun4-dpad.txt --script-base state `
  --state-out artifacts/after.tdos --json
```

### Trace a stuck READ_ID wait

```powershell
dotnet run --project src/FiveDo.Cli -- trace game.chd --bios panafz1.bin `
  --frames 40000 --trace-frames 2 --stop-cd 0x83 --count 200000 `
  --out artifacts/trace.txt --dump-ram artifacts/ram.bin --dump-dsp artifacts/dsp
```

### Inspect a state and its slot metadata

```powershell
dotnet run --project src/FiveDo.Cli -- state inspect artifacts/states/slot-00.tdos --json
dotnet run --project src/FiveDo.Cli -- state slots artifacts/states --count 10 --json
```

### Disassemble a RAM dump

```powershell
dotnet run --project src/FiveDo.Cli -- disasm artifacts/ram.bin `
  --offset 0x12380 --count 64 --base 0x12380
```

---

## 15. Limitations and gotchas

- `run --audio openal` on a machine with no device prints a warning and runs silently;
  it does not fail the run.
- `--realtime` is pacing only; without it, live OpenAL audio is a short burst.
- `--capture-dir` does nothing unless `--capture-every` is greater than zero.
- `--frame-out` and captures use the CPU assembler, not the selected GPU filter.
- `fs` is CHD-only in this release. `disc tracks` now covers ISO/BIN/CUE as a
  single data track in addition to CHD.
- `trace` and `disasm` have no JSON output.
- `state inspect`/`state slots` JSON is PascalCase; all other CLI JSON is camelCase.
- `--watch-start`, `--watch-end`, `--stop-pc`, `--stop-cd`, `--dump-ram-offset`, and
  `--base` are hexadecimal even without `0x`; do not pass a decimal address expecting
  decimal interpretation.
- `--poke` writes raw RAM bytes, bypassing the MMIO map. It is a diagnostic tool, not a
  cheat engine.
- A save state records the BIOS hash and disc hash, but the current loader only warns
  on a BIOS/emulator-version mismatch; it does not reject a different disc. See
  [`SaveStates.md`](SaveStates.md).
