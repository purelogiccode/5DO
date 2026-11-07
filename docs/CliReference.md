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
  `--high-res`, `--opera-dipir`, `--opera-soft-reset`, `--swi-hle`, `--swi-comments`,
  `--prefetch`, `--no-game-fixes`).
- A flag followed by a non-`--` token consumes that token as its value. Put boolean
  flags last or before another `--option` to avoid ambiguity.
- The first non-option token is the positional path for `run`, `trace`, `info`,
  `disc dump`, `disc tracks`, `disc playlist`, `bios info`, `audio analyze`, `state inspect`,
  `settings validate|migrate`, and `disasm` (disc or file, depending on the command). `fs` uses fixed positional
  subcommand/path positions.
- Integer options accept decimal and, where noted, `0x` hexadecimal. Some address
  options (`--watch-start`, `--watch-end`, `--stop-pc`, `--stop-cd`,
  `--dump-ram-offset`, `--base`) are parsed as **hexadecimal even without `0x`**.
- Paths may be absolute or relative to the current directory. Output directories are
  created when the command writes files.
- `--portable` stores the default settings/NVRAM/cheat/log directory beside the
  executable. `FIVEDO_DATA_DIR` overrides the data directory even when `--portable` is
  not used. `--config-profile <name>` selects `<data-directory>/profiles/<name>/` for
  an isolated named configuration; `FIVEDO_PROFILE` is the environment equivalent.
- `--json` is accepted by commands that have structured output. It is ignored by
  `trace` and `disasm`, which are text-only.
- On startup, an interactive CLI run without `--json` checks GitHub's public release feed
  and offers to open the download page when a newer release exists. Use
  `--no-update-check` or `FIVEDO_UPDATE_CHECK=0` to skip it; `5do update` checks on
  demand.
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
| `gamedb stats [folder]` | Embedded game-database count and, with a folder, image/unique/match coverage. |
| `gamedb scan <folder> --out <file>` | Derive XML records from disc IDs and filenames. |
| `gamedb export --out <file>` | Export the embedded database. |
| `gamedb merge <file> --out <file>` | Merge a fragment into the embedded database for review. |
| `disc dump <disc>` | Write raw 2048-byte sectors. |
| `disc tracks <disc>` | List the CUE/CHD track table (data and audio), or one data track for ISO/BIN. |
| `disc playlist <m3u>` | List multi-disc playlist entries and the selected disc. |
| `disc verify <disc> [--json] [--max-errors N] [--dat <path>]` | Read every native sector, report the first LBA errors, verified count, bytes, and content hash; with `--dat`, match the native track hashes against a Redump/No-Intro DAT. Exits 1 on a read error or DAT mismatch. |
| `library verify [--folder <dir>] [--dat <path>] [--cache <path>] [--json]` | Verify every disc in a library folder (or the configured library folders) and update the desktop known-dump cache. Exits 1 on a read error. |
| `fs list <disc>` | List the 3DO file tree (ISO, BIN, CUE, or CHD). |
| `fs extract <disc>` | Extract files from a 3DO disc image (ISO, BIN, CUE, or CHD). |
| `bios info <path>` | BIOS size, MD5, region, Kanji, Anvil, and RSA-patch detection. |
| `bios list\|scan <folder>` | Scan and validate a BIOS folder. |
| `bios select <disc> --folder <dir>` | Choose a region-appropriate BIOS for a disc; `--allow-unsigned-discs` prefers No-RSA images. |
| `nvram path [disc]` | Resolve the shared or per-game NVRAM image. |
| `nvram list` | List global and per-game NVRAM images. |
| `nvram export\|import\|reset` | Back up, restore, or clear an NVRAM image. |
| `settings validate\|migrate` | Check or normalize a `settings.json` file. |
| `settings profiles` | List named configuration profiles under the data directory. |
| `settings options` | Print the validated machine-option registry as Markdown, text, or JSON. |
| `cheat list\|add\|enable\|disable\|remove\|import\|export` | Manage per-game RAM cheat files. |
| `movie info\|record\|play\|verify\|branch` | Inspect, record, replay, verify, or branch `.5domovie` TAS movies. |
| `audio devices [--json]` | List OpenAL playback devices. |
| `diagnostics bundle [--out path]` | Write a privacy-scrubbed support zip. Options: `--state`, `--frame`, `--movie`, `--settings`, `--logs`, `--notes`, `--json`. |
| `state diff <a> <b> [--json]` | Compare two states section by section (presence, version, length, hash, first difference). |
| `audio analyze <wav>` | WAV format, peak, RMS, clipping, silence, DC offset. |
| `state inspect <path>` | Save-state header and section table (full CRC validation, including Brotli sections). |
| `state slots <dir>` | List numbered save slots and metadata. |
| `disasm <file>` | Disassemble raw little-endian ARM words. |
| `bench` | Synthetic-ROM frame benchmark (or real content with options); `--profile` selects the timing bundle. |
| `selftest` | Determinism and state roundtrip checks for every named timing profile. |
| `update` | Check GitHub for a newer 5DO release. |
| `--version`, `--help` | Identity/help. |

---

## 3. `run`

### Synopsis

```text
5do run [disc] --bios <path> [--bios2 <path>] [options]
5do run [disc] --bios-auto <folder> [--bios2 <path>] [options]
5do run [disc] --state-in <path> [options]
```

`--bios` or `--bios-auto` is required unless `--state-in` supplies the ROM/RAM state.
A disc is optional for a BIOS-only boot. `--bios-auto` scans the folder, computes the
game ID/region, and selects a primary BIOS plus an optional Kanji/BIOS2 image; explicit
`--bios2` overrides the selected secondary. `--bios` and `--bios-auto` are mutually
exclusive. `--state-in` restores ROM, RAM, NVRAM, device state, and the current VDL
frame. `--allow-unsigned-discs` disables the BIOS media RSA check in memory for unsigned,
homebrew, and arcade discs; it is independent of `--bios`/`--bios-auto` and never modifies
the BIOS file.

### Order of operations

1. Open the disc (if any) and compute its game ID.
2. Resolve the fix mode: explicit `--fix-mode`, else `--no-game-fixes`, else the 4DO
   database mapping for the game ID.
3. Apply `--profile` (if any), then create the machine with `--high-res`,
   `--deinterlace`, `--arm-timing`, `--clio-timers`, `--cel-timing`, `--cd-timing`,
   `--cd-speed`, `--memory`, `--cpu-clock`, `--random-seed`, `--opera-dipir`,
   `--opera-soft-reset`, `--swi-hle`, `--cpu-backend`, and the resolved fix mode.
   Explicit options override the profile.
4. Resolve `--bios`/`--bios-auto` and load BIOS1/BIOS2 (unless `--state-in`).
5. Create the audio backend and insert the disc.
6. Reset the machine.
7. Load NVRAM unless `--state-in`: the explicit `--nvram` file, or the image resolved
   by `--nvram-mode`/`--nvram-version`/`--nvram-dir` (with shared-image migration
   fallbacks). With no NVRAM option, a fresh image is used.
8. Load the input script, validate `--script-base`, and set up `--autopilot`.
9. Install trace writers/watchpoints/KPRINT hooks.
10. Load `--state-in` (and offset the script if `--script-base state`).
11. Apply `--poke` patches.
12. Load `--cheat`/`--cheat-auto` and/or add `--cheat-code`, then attach the per-frame
    cheat hook.
13. Run frames (paced only with `--realtime`).
14. Write RAM/DSP dumps, dispose traces, write `--frame-out`, write `--state-out`.
15. Emit the result and, when an explicit or layout NVRAM path was resolved, save NVRAM.

### Emulation options

| Option | Default | Meaning |
|---|---:|---|
| `--bios <path>` | none | BIOS1 path (exactly 1 MB). Required unless `--bios-auto` or `--state-in`. |
| `--bios2 <path>` | none | Optional BIOS2/Kanji font path. Requires `--bios` or `--bios-auto`. |
| `--bios-auto <folder>` | none | Scan a folder and select a region-appropriate BIOS for the disc. |
| `--allow-unsigned-discs` | off | Patch the media RSA check in memory so unsigned, homebrew, and arcade discs can boot. A known No-RSA BIOS is preferred by `--bios-auto`; otherwise a compatible retail 1 MB BIOS is patched with the documented two-site bypass. The BIOS file is not modified. The effective ROM hash is reported as `biosMd5` and `biosRsaPatched`. |
| `--frames N` | 600 | Number of frames to run. Decimal or `0x`. |
| `--state-in <path>` | none | Load a `.tdos` state instead of booting. |
| `--state-out <path>` | none | Save a `.tdos` state after the run. |
| `--nvram <path>` | none | Load a 32 KB NVRAM image before the run and save it after. Missing file means a fresh image. Cannot be combined with the layout options below. |
| `--nvram-mode global\|per-game` | `global` when a layout option is used | Resolve the shared or per-game NVRAM image. Per-game mode needs a disc for its game ID. |
| `--nvram-version 0-9` | `0` | Independent NVRAM version slot. |
| `--nvram-dir <path>` | settings directory | Root directory for the resolved NVRAM layout. |
| `--high-res` | off | Enable the opt-in 640x480 CEL/VDLP mode. |
| `--deinterlace auto\|bob\|weave\|blend` | `bob` | Presentation-side field handling. `auto` detects 480i fields and weaves them; `bob` keeps each 240-line field (reference-compatible hashes); `weave` always doubles to 480 lines; `blend` averages two fields into one frame. |
| `--game-profile <json>` | none | Load a per-game profile as the timing/region baseline before explicit flags. |
| `--region auto\|ntsc\|pal1\|pal2` | `auto` | Video region. `auto` chooses PAL1 when the selected BIOS is a PAL image, otherwise NTSC; `pal2` is the 384-wide PAL mode. |
| `--profile compatible\|accuracy\|performance\|custom` | `compatible` | Timing profile. `accuracy` enables ARM60 N=2, Opera CLIO/CEL/CD timing, and the stock 2x drive; `performance` uses a 1.5x CPU clock with FreeDO timing; `custom` keeps the individual options. Explicit timing options override the profile. |
| `--arm-timing freedo\|arm60` | `freedo` | ARM60 N-cycle class. `freedo` keeps the FreeDO-compatible N=4 baseline; `arm60` uses the ARM60 datasheet/SDK N=2 timing from Opera. |
| `--clio-timers freedo\|opera` | `freedo` | CLIO timer scan model. `freedo` services all sixteen timers per 25 MHz/64 event; `opera` walks one slot per four M25M ticks with the `0x220` slack gap and an explicit cascade carry. |
| `--cel-timing freedo\|opera` | `freedo` | MADAM CEL timing model. `freedo` draws synchronously and returns the FSM to idle; `opera` defers the draw to the next CPU tick and keeps the status register busy until the pixel/texel deadline (nine million pixels per second plus 64 setup cycles per list entry). |
| `--cd-timing freedo\|opera` | `freedo` | CD-ROM data-sector timing. `freedo` transfers each sector immediately; `opera` schedules each 2048-byte sector at the configured speed, clears the data/status poll bits while waiting, publishes status after the transfer drains, and raises the XBUS CD FIQ when ready. |
| `--cd-speed unbounded\|1x\|2x\|4x\|8x\|16x\|24x\|32x` | `2x` | Effective read speed for the `opera` timing model. `unbounded` publishes sectors immediately; `1x` is the slowest and `32x` the fastest. The option has no effect with `--cd-timing freedo`. |
| `--memory 21\|22\|41\|42\|81\|82\|E2\|F1` | `21` | DRAM/VRAM capacity. Codes are two hexadecimal nibbles (DRAM MB, then VRAM MB); `4/2` and `4MB/2MB` are also accepted. `21` is the stock 2 MB DRAM / 1 MB VRAM layout. Expanded layouts move the VRAM base above DRAM and report the capacity through MADAM RED SYSBITS. Recorded in save states. |
| `--input-device 1=joypad,2=mouse,...` | all joypads | PBUS device on each of the eight ports. Devices: `joypad`, `flightstick`, `mouse`, `lightgun`, `arcade-lightgun`, `orbatak-trackball`, `none`. Selecting a peripheral or a port above six switches the packet to the Opera chain format; the legacy 4DO packet is kept while ports 1-6 are all joypads. |
| `--active-devices 0-8` | automatic | Maximum polled PBUS devices. `0` reports an empty chain; `8` allows every port. Omit it to keep 5DO's automatic behavior (one joypad, growing to the highest configured peripheral port). |
| `--pbus-format legacy\|opera` | automatic | Force the PBUS packet layout. `opera` emits the 3DO daisy-chain records and is useful for differential tests; the chain is selected automatically when a peripheral is present or ports 7-8 are active. |
| `--cpu-clock 0.5x-4x\|6.25-50MHz` | 1x (12.5 MHz) | ARM clock multiplier or absolute MHz. Values outside 0.5x-4x (6.25-50 MHz) fail. |
| `--random-seed default\|random\|SEED` | `default` | Initial DSP PRNG seed. `default` keeps `0xa5a5a5a5`, `random` picks a fresh 32-bit value, and a decimal or `0x` value fixes the seed. |
| `--opera-dipir` | off | Enable CLIO DIPIR latches and XBUS access aborts. Recorded in save states; a per-game profile can also enable it. |
| `--opera-soft-reset` | off | Use Opera's ARM/MADAM/DSP reset path for CLIO CSTAT soft resets. Recorded in save states. |
| `--swi-hle` | off | Handle Opera's 14 Portfolio OS fixed-point math SWIs (`0x50000`-`0x50012`) on the host. Opt-in performance mode; it skips the guest handler's cycles and register clobbers, so some titles change behavior. Recorded in save states and per-game profiles. |
| `--swi-comments` | off | Collect a per-comment SWI histogram and include it as `swiComments` in `--json`. Use it to discover which math SWIs a title calls. Diagnostic only. |
| `--cpu-backend interpreter\|jit` | `interpreter` | Guest ARM60 backend. `jit` compiles hot blocks and falls back safely when dynamic code or a supported block is unavailable. |
| `--prefetch` | off | Enable the bounded disc read-ahead cache (32 sectors ahead, 128-sector capacity). |
| `--playlist <m3u>` | none | Open the first entry of an M3U/M3U8 playlist. Cannot be combined with a positional disc. A positional `.m3u`/`.m3u8` is also detected. |
| `--swap-at N` | none | With `--swap-disc`, replace the disc at emulated frame N without resetting the machine. |
| `--swap-disc <path>` | none | Disc opened at `--swap-at`; the reported disc hash updates after the swap. |
| `--fix-mode 0xMASK` | database | Explicit `FixMode` bits. Accepts hex with or without `0x`. Overrides `--no-game-fixes`. |
| `--no-game-fixes` | off | Disable automatic 4DO fixes. |
| `--poke ADDR=HEX[;...]` | none | Patch RAM bytes after state load. Address decimal or `0x`; bytes hex with optional spaces/commas. |
| `--cheat <path>` | none | Load a cheat document and apply its enabled entries every frame. The file must exist. |
| `--cheat-code <code>` | none | Add a one-off enabled cheat. Can be combined with `--cheat` or `--cheat-auto`. |
| `--cheat-auto` | off | Load `<gameId>.json` (or `global.json`) from `--cheats-dir`. A missing file is an empty set. |
| `--cheats-dir <path>` | default cheat directory | Directory used by `--cheat-auto`. Requires `--cheat-auto`. |
| `--movie-record <path>` | none | Record one PBUS packet per frame to a `.5domovie` file. Cannot be combined with `--movie-play`. |
| `--movie-play <path>` | none | Play a movie. The embedded state is loaded unless `--state-in` is supplied. Cannot be combined with `--input-script` or `--autopilot`. |
| `--movie-verify` | off | With `--movie-play`, replay every frame and compare the recorded frame/PC/state evidence. Exit code 2 on mismatch. |
| `--movie-no-state` | off | With `--movie-record`, omit the starting save state. |
| `--realtime` | off | Pace the run against the audio clock instead of running as fast as possible. |

### Audio options

| Option | Default | Meaning |
|---|---|---|
| `--audio null|wav|openal` | `null` | Audio backend. `null` still executes the DSP but discards samples. |
| `--wav-out <path>` | `audio.wav` | WAV destination for `--audio wav`. |
| `--volume 0..1` | 1.0 | OpenAL gain. Invalid values fail. |
| `--audio-device <name>` | default | OpenAL playback device; use `audio devices` to list names. |
| `--audio-buffer 20..500` | 100 | Target OpenAL buffer latency in milliseconds. |
| `--audio-sync -250..250` | 0 | Host audio/video offset in milliseconds. Positive delays audio; negative advances it. |
| `--record-gif <path>` | off | Record emulated frames as an animated GIF. |
| `--record-fps 1..60` | 30 | GIF capture rate; sources above 50 fps are decimated. |
| `--record-frames N` | 0 | Stop the GIF after N captured frames (0 = unlimited). |
| `--cd-volume 0..1` | 0.8 | CD-DA mix level. Invalid values fail. |
| `--mute-cd` | off | Keep CD-DA playback running but remove it from the mixed output. |

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

`run` and `trace` always attach a default player-1 pad, matching the desktop shell;
without `--input-script` the pad is connected but idle. This matters for titles such
as Olympic Summer Games that reject the boot when PBUS reports no controller.

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
`Start`, `Play`, and `Pause` are aliases for `P`; `Stop` is an alias for `X`.
Separators are comma, plus, or vertical bar. `None` or an empty field releases all
buttons. The state persists until the next entry. Duplicate frames keep the last entry.
With `--state-in`, a script starting at `0` applies its last entry immediately unless
`--script-base state` is used.

### Output options

| Option | Default | Meaning |
|---|---:|---|
| `--frame-out <path>` | none | Write the final frame. `.png` writes PNG; any other extension writes BMP. |
| `--capture-dir <dir>` | none | Write periodic frame PNGs. Requires `--capture-every > 0` or `--capture-frames`. |
| `--capture-every N` | 0 | Capture every N frames after execution. Files are `f000123.png`. |
| `--capture-frames N,N,...` | none | Capture the listed frames as `fNNNNNN.png` in `--capture-dir`. |
| `--dump-ram <path>` | none | Write raw CPU-visible RAM bytes after the run. |
| `--dump-ram-offset N` | 0 | Byte offset. Parsed as hex even without `0x`. |
| `--dump-ram-length N` | 0x1000 | Byte count. Decimal or `0x`. |
| `--dump-dsp <prefix>` | none | Write `<prefix>.imem.bin` and `<prefix>.dmem.bin` (little-endian `ushort` words). |
| `--checkpoints N,N,...` | none | Emit deterministic samples at the listed frames. Forces the frame-by-frame run path and enables audio hashing. |
| `--audio-hash` | off | Hash every DSP sample with FNV-1a and report `audioHash`. |
| `--state-hash` | off | Serialize and hash the machine state at every checkpoint (`stateHash`). |
| `--cycle-classes` | off | Record instructions and ARM cycles per instruction class and report `cycleClasses`. |
| `--clio-timer-stats` | off | Record the per-timer execution/wrap/reload/stop/FIQ ledger and report `timerStats`. |
| `--json` | off | Emit a structured result instead of text. |

`--frame-out` and `--capture-dir` use the CPU `FrameAssembler`, so the image is the
accuracy oracle, not the filtered GPU output. They capture the presented frame, so an
explicit `--deinterlace weave` writes a 480-line PNG. `run --json` adds `deinterlace`,
`interlaced` (the presented frame was woven), and `interlaceDetected` (the detector
verdict).

### Diagnostic options

| Option | Meaning |
|---|---|
| `--cd-trace <path>` | CD command and sector trace. |
| `--cd-transcript <path>` | Full CD command/status/poll transcript (one line per command). |
| `--clio-trace <path>` | CLIO register-write and DMA trace. |
| `--fiq-trace <path>` | FIQ reason trace. |
| `--kprint <path>` | KPRINT character stream (CR/LF become newlines). |
| `--kprint-pc <path>` | One line per KPRINT value with PC, LR, SP, and eight stack words. |
| `--watch-lowmem <path>` | RAM write watchpoint. |
| `--watch-from-frame N` | First frame to record. Decimal or `0x`. |
| `--watch-start N` | First watched address. Parsed as hex even without `0x`. |
| `--watch-end N` | One-past-last watched address. Parsed as hex even without `0x`. |
| `--cel-trace <path>` | CEL command/geometry/progress trace. |
| `--clio-timer-trace <path>` | State-changing CLIO timer trace: counter/control/slack writes, wraps, reloads, stops, and FIQs. Slot scans are not logged. |
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
bios:        Panasonic FZ-1 (f47264dd47fe30f73ab3c010015c155b)
bios region: USA (NTSC) [auto]
bios reason: Auto-selected Panasonic FZ-1 for US (USA (NTSC)).
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
| `sourceWidth`, `sourceHeight` | int | Visible source dimensions (640x480 in high-res; 320x288 or 384x288 in PAL). |
| `region` | string | `Ntsc`, `Pal1`, or `Pal2`. |
| `gifPath`, `gifFrames` | string?, int? | GIF output path and captured frame count when `--record-gif` is used. |
| `fieldRate` | int | `60` for NTSC or `50` for PAL. |
| `vdlModulo` | uint | VDLP line width. |
| `pc` | uint | CPU program counter at the end. |
| `instructions` | long | Instructions executed since reset/state load. |
| `frameHash` | uint | FNV-1a hash of the CPU-assembled BGRA frame. |
| `samples` | long | WAV stereo frames or OpenAL pushed+dropped samples. |
| `discHash` | string | Game ID (MD5 of first two sectors), or empty. |
| `gameId` | string? | Same game ID, or null when no disc. |
| `profile` | string | Timing profile derived from the actual options: `Compatible`, `Accuracy`, `Performance`, or `Custom`. |
| `armTiming` | string | `FreeDoCompatible` or `Arm60Datasheet`. |
| `clioTimers` | string | `FreeDoCompatible` or `OperaScheduler`. |
| `celTiming` | string | `FreeDoCompatible` or `OperaBusy`. |
| `celBusyCycles` | ulong | CEL busy cycles remaining at the end of the run. |
| `cdromTiming` | string | `FreeDoCompatible` or `OperaCycleBased`. |
| `cdSpeed` | string | `Unbounded`, `X1`, `X2`, `X4`, `X8`, `X16`, `X24`, or `X32`. |
| `memory` | string | Active DRAM/VRAM capacity code: `21`, `22`, `41`, `42`, `81`, `82`, `E2`, or `F1`. |
| `inputDevices` | string | Per-port device list, for example `1=joypad,2=mouse,3=lightgun,4=joypad,5=joypad,6=joypad`. |
| `cpuClock` | int | ARM clock in hertz (12,500,000 at the stock 1x setting). |
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
| `biosPath` | string? | Primary BIOS path used for the run. |
| `bios2Path` | string? | BIOS2/font path used for the run, if any. |
| `biosMd5`, `bios2Md5` | string? | Lowercase MD5 hashes of the loaded BIOS images. With unsigned-disc support, `biosMd5` is the effective patched hash. |
| `biosRsaPatched` | bool | Whether the in-memory RSA check patch was applied to this run. Omitted when false. |
| `biosRsaDisabled` | bool | Whether the effective BIOS has the RSA check disabled, including known No-RSA images. Omitted when false. |
| `biosName` | string? | Human-readable BIOS profile name. |
| `biosRegion` | string? | Region label such as `USA (NTSC)` or `Europe (PAL)`. |
| `biosSelection` | string? | Why automatic selection chose this image. |
| `biosSource` | string | `auto`, `configured`, or `none`. |
| `nvramPath` | string? | Resolved NVRAM path, or null when NVRAM persistence was not requested. |
| `nvramMode` | string | `disabled`, `explicit`, `global`, or `per-game`. |
| `nvramVersion` | int | Resolved NVRAM version 0-9. |
| `audioHash` | uint? | FNV-1a hash of all packed stereo samples, when hashing is enabled. |
| `checkpoints` | object[] | Samples at the requested frames. Each entry has `frame`, `pc`, `instructions`, `frameHash`, `samples`, `audioHash`, `stateHash`, `cdStatus`, `cdPoll`, `dspPc`, `dspSema`, `dspSeed`, and `celBusyCycles`. |
| `warnings` | string[] | `machine.StateWarnings` after a state load. |
| `cheatCount` | int | Enabled, valid cheats attached to the run. |
| `cheatWrites` | long | Bytes written by the cheat engine, including the initial apply. |
| `cheatPath` | string? | Loaded cheat document path, or null when no file was used. |
| `moviePath` | string? | Recorded or played `.5domovie` path, when movie options were used. |
| `movieFrames` | int? | Frames recorded or played by the movie. |
| `movieVerification` | string? | `pass` or `fail` when `--movie-verify` ran. |
| `movieFailures` | string[]? | Recorded evidence that differed during verification. |
| `fiq` | object | Request/entry telemetry: `requests`, `entries`, `matchedEntries`, `unmatchedEntries`, `coalescedRequests`, `lastLatencyCycles`, `maxLatencyCycles`, `averageLatencyCycles`. |
| `dspStats` | object | DSP sample-loop telemetry: `instructions`, `loopCalls`, `runningLoops`, `sleepingLoops`, `maxInstructionsPerLoop`, `averageInstructionsPerLoop`. |
| `cycleClasses` | object[]? | Per-class ARM cycle ledger, present only with `--cycle-classes`. Each entry has `instructionClass`, `instructions`, `cycles`, and `cyclesPerInstruction`. |
| `timerStats` | object[]? | Per-timer CLIO ledger, present only with `--clio-timer-stats`. Each entry has `timer`, `executions`, `underflows`, `reloads`, `stops`, and `fiqs`. |

`--fiq-trace` writes request and entry lines with ARM-cycle timestamps and matched
latency; see [`Diagnostics.md`](Diagnostics.md) §7 for the format and the measurement
invariants.

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
| `--bios <path>` | none | BIOS1. Required unless `--bios-auto` or `--state-in`. |
| `--bios2 <path>` | none | Optional BIOS2/Kanji font path. |
| `--bios-auto <folder>` | none | Auto-select a region-appropriate BIOS for the disc. |
| `--allow-unsigned-discs` | off | Patch the media RSA check in memory, as in `run`. |
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
| `--nvram <path>` | none | Load NVRAM before, save after. Cannot be combined with the layout options. |
| `--nvram-mode global\|per-game` | none | Resolve the shared or per-game NVRAM image. |
| `--nvram-version 0-9` | `0` | NVRAM version slot. |
| `--nvram-dir <path>` | settings directory | Root directory for the NVRAM layout. |
| `--input-script <path>` | none | Scripted input (absolute frames only). |
| `--high-res`, `--prefetch` | off | As in `run`. |
| `--deinterlace auto\|bob\|weave\|blend` | `bob` | As in `run`. |
| `--profile compatible\|accuracy\|performance\|custom` | `compatible` | As in `run`. |
| `--arm-timing freedo\|arm60` | `freedo` | As in `run`. |
| `--clio-timers freedo\|opera` | `freedo` | As in `run`. |
| `--cel-timing freedo\|opera` | `freedo` | As in `run`. |
| `--cd-timing freedo\|opera` | `freedo` | As in `run`. |
| `--cd-speed unbounded\|1x\|2x\|4x\|8x\|16x\|24x\|32x` | `2x` | As in `run`. |
| `--memory 21\|22\|41\|42\|81\|82\|E2\|F1` | `21` | As in `run`. |
| `--input-device 1=joypad,2=mouse,...` | all joypads | As in `run`. |
| `--active-devices 0-8` | automatic | As in `run`. |
| `--pbus-format legacy\|opera` | automatic | As in `run`. |
| `--cpu-clock 0.5x-4x\|6.25-50MHz` | 1x | As in `run`. |
| `--random-seed default\|random\|SEED` | `default` | As in `run`. |
| `--opera-dipir`, `--opera-soft-reset` | off | As in `run`. |
| `--swi-hle` | off | As in `run`. `--swi-comments` is only meaningful for `run --json`. |
| `--cpu-backend interpreter\|jit` | `interpreter` | As in `run`. |
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

Lists every track for CUE and CHD images:
`track <n>: type=<data|audio> format=<cue|chd> dataSize=<n> frames=<n> start=<lba>`.
`start` is the track's first logical block address (LBA 0 = MSF 00:02:00). Audio tracks
report `dataSize=2352`. For ISO and BIN images it reports the single 2048-byte user-data
track: `track 1: type=data format=<iso|bin> dataSize=<total bytes> frames=<n> start=0`.
`--json` emits `{ "ok": true, "path": "...", "tracks": [{ "number", "type", "dataSize",
"frames", "start" }] }`; `dataSize` is the native per-sector size in JSON and the
total payload size in the text line for ISO/BIN.

### `disc verify`

```powershell
5do disc verify game.chd
5do disc verify game.chd --json --max-errors 8
```

Reads every data and audio track sector through the native-sector path, so CHD/CUE/BIN
corruption surfaces without booting. The report includes the declared and verified
sector counts, tracks, native bytes read, an FNV-1a content hash, and up to
`--max-errors` failures with LBA numbers. The command exits 1 when any sector cannot be
read or the verified count is short.

With `--dat <path>`, the command also parses a Redump/No-Intro XML DAT and hashes each
native track with CRC-32, MD5, and SHA-1. A track matches when its size and every hash
present in the DAT entry agree. The JSON result adds `dat.knownDump`, `dat.gameName`,
`dat.matchedTracks`, and the per-track hashes; the command exits 1 on a mismatch.

### `disc playlist`

```powershell
5do disc playlist "3DO Multi-Disc.m3u" --json
```

Loads an M3U/M3U8 playlist and lists each entry with its title (from `#EXTINF`), resolved
path, and one-based position. Relative paths resolve against the playlist directory. The
text output marks the selected entry with `*`; `--json` returns `path`, `current`,
`count`, and an `entries` array of `index`, `path`, `title`, and `displayName`.

### `library verify`

```powershell
5do library verify --folder "D:\Samples\3DO" --cache artifacts\library-health.json
5do library verify --folder "D:\Samples\3DO" --dat 3do.dat --json
```

Reads every supported image in a library folder (or the configured `LibraryFolders` when
`--folder` is omitted), reports `Verified`, `Mismatch`, `ReadError`, or `NoDat` per disc,
and updates the same `library-health.json` cache the desktop uses for its known-dump
badges. `--cache` redirects the cache for CI or a portable run. `--dat` matches each disc
against a Redump/No-Intro DAT; when omitted, the configured `DiscDatPath` is used. The
command exits 1 when any disc has a read error.

### `covers` commands

```powershell
5do covers update
5do covers update --force --json
5do covers match "Battle Chess"
5do covers match "Blade Force" --threshold 0.90 --json
5do covers download --folder "D:\Games\3DO" --dry-run
5do covers download --folder "D:\Games\3DO"
5do covers download --all
5do covers verify --fix
5do covers cache path
5do covers cache clear
```

`update` downloads or revalidates the online catalog. `match` prints the best catalog
matches with their score, game IDs, and matched alias; the default threshold is 0.70 so
the command is useful for diagnosing a catalog entry without listing unrelated covers. `download` scans the configured
library (or `--folder`) and fetches only the covers that are not cached; `--all` downloads
the bulk pack instead. `verify` checks every cached image against its SHA-256; `--fix`
deletes corrupt files. `cache path` and `cache clear` manage the shared cover cache.

`--catalog <path>` accepts a local `catalog.json` and resolves relative image paths
beside it, which makes the commands usable offline and in tests. `--url` overrides the
configured catalog URL, `--cache` redirects the cache, and `--threshold` overrides the
fuzzy threshold. JSON output is available for every subcommand.

### `gamedb` commands

```powershell
5do gamedb stats --folder "D:\3DO" --json
5do gamedb scan "D:\3DO" --out local-gamedb.xml
5do gamedb export --out GameDatabase.xml
5do gamedb merge local-gamedb.xml --out src/FiveDo.Storage/Data/GameDatabase.xml
```

`stats` reports the embedded record count and image/unique/match coverage. `scan`
computes the 4DO ID for every `.chd`, `.cue`, or `.iso` below the folder, derives metadata
from the filename, and writes a fragment for records that are not already present.
`--include-known` writes every scanned record, and `--max-parallel N` caps the readers.
`export` writes the complete embedded database. `merge` combines a fragment with the
embedded database; existing records win unless `--overwrite` is supplied. The merged file
is written to `--out` so it can be reviewed before replacing the embedded XML. See
[`GameDatabase.md`](GameDatabase.md) for the schema and contribution workflow.

`info --json` includes the same optional metadata (`revision`, `discNumber`, `discTotal`,
`bios`, `bios2`, `inputDevices`, `nvram`, `cheats`) when the database provides it.

---

## 7. `fs`

```powershell
5do fs list game.chd
5do fs list game.iso --json
5do fs extract game.cue --out extracted/ --filter GAME/
```

`fs` mounts the 3DO Opera file system directly through `FiveDo.Storage.OperaFileSystem`
for ISO, BIN, CUE+BIN, and CHD images. The same parser handles every container, so the
tree and extracted bytes are identical regardless of format. Directory blocks are
chained through the on-disc `nextBlock` field, and files with `burst`/`gap` layouts are
read with the gaps skipped.

- `list` prints `volume=<name> entries=<n>` and a tree of `[D]`/`[F]` entries with size
  and LBA. JSON: `{ "ok": true, "entries": [{ "path", "directory", "size", "lba" }] }`.
- `extract` requires `--out`. `--filter` is a case-insensitive path prefix. Extraction
  refuses entries that would escape the output directory. JSON:
  `{ "ok": true, "extracted": N, "output": "<full path>" }`.

---

## 8. `bios` commands

```powershell
5do bios info panafz1.bin [--bios2 panafz1-kanji.bin] [--json]
5do bios list bios --recursive --json
5do bios scan bios --json
5do bios select "Lemmings (USA).chd" --folder bios --json
```

`bios info` reports `path`, `size`, `md5`, `anvil`, `anvilKind`, `name`, `kind`,
`territory`, `videoStandard`, `region`, `kanji`, `patched`, `rsaDisabled`,
`rsaPatchSites`, `preference`, `notes`, and `md5Bios2`. BIOS1 must be exactly 1 MB; a
smaller file is classified as a BIOS2/Kanji font or arcade support ROM. Anvil is detected
by `byte[28] == 234`. `rsaPatchSites` is the number of documented RSA check sites found in
the image; zero means the image is already No-RSA or has an unsupported layout.

`bios list`/`bios scan` scan `.bin` and `.rom` files, classify known MD5 hashes from the
built-in catalog, validate sizes, and report skipped files. Add `--recursive` to include
subfolders. JSON fields: `folder`, `primaryCount`, `secondaryCount`, `errorCount`,
`profiles`, and `errors`.

`bios select` opens a disc, computes its game ID/region, and prints the selected primary
and optional BIOS2 with the reason. Options: `--folder <dir>` (required),
`--recursive`, `--allow-patched`, `--allow-unsigned-discs`, `--require-kanji`,
`--prefer-anvil`, and `--md5 <hash>`. `--allow-unsigned-discs` permits No-RSA images for
non-arcade discs and prefers them when they match the region. Known title requirements
currently cover Slayer Europe/USA, Powers Kingdom (Europe), and Arcade CD-ROM discs;
other titles use region matching plus per-game overrides.

Known BIOS hashes include Panasonic FZ-1/FZ-10/FZ-10E/FZ-10JA, GoldStar, Sanyo TRY, the
No-RSA FZ-1J/FZ-10/FZ-10E images, the FZ-1 Kanji font, and the 3DO arcade support ROM.
Unknown images are still accepted, but their metadata is inferred from the file name and
marked unverified in `notes`. File names containing `norsa`, `no-rsa`, `rsa disabled`, or
`unsigned` are treated as No-RSA candidates for `--allow-unsigned-discs`.

---

## 8.1 `nvram` commands

```powershell
5do nvram path "Lemmings (USA).chd" --mode per-game --version 1 --dir data --json
5do nvram list --dir data --json
5do nvram export "Lemmings (USA).chd" --out lemmings.ram --mode per-game --dir data
5do nvram import "Lemmings (USA).chd" --in lemmings.ram --mode per-game --dir data
5do nvram reset "Lemmings (USA).chd" --mode per-game --dir data
```

The layout matches the desktop settings: shared version 0 is `<dir>/nvram.ram`, shared
version 1-9 is `<dir>/nvram.<version>.ram`, and per-game images are
`<dir>/nvram/per-game/<gameId>.<version>.ram`. `--mode` defaults to `global` and
`--version` to `0`; per-game mode requires a disc so the game ID can be computed.
`--dir` defaults to the desktop settings directory.

- `path` prints `path`, `mode`, `version`, `gameId`, `exists`, and `length`.
- `list` scans both layouts and reports `path`, `mode`, `version`, `gameId`, `length`,
  and `lastWriteTimeUtc`.
- `export` loads the resolved image (including shared-to-per-game migration fallbacks)
  and writes a 32 KB copy to `--out`.
- `import` validates `--in` as a 32 KB image and writes it atomically to the resolved
  path.
- `reset` writes a fresh initialized image to the resolved path. It does not delete the
  shared fallback, so the reset image remains authoritative.

JSON actions use `{ "ok": true, "action", "path", "source", "length" }`. A missing image
is not an error for `path` or `export`; both use a fresh image.

---

## 8.2 `cheat` commands

```powershell
5do cheat list --game ABCD1234 --dir data
5do cheat add --game ABCD1234 --name "Infinite health" --code "write16 0x00123456 = 0x0064"
5do cheat enable --game ABCD1234 --name "Infinite health"
5do cheat disable --game ABCD1234 --name "Infinite health"
5do cheat remove --game ABCD1234 --name "Infinite health"
5do cheat import --game ABCD1234 --in my-cheats.json
5do cheat export --game ABCD1234 --out my-cheats.json
```

`--game` is the 4DO game ID; a disc path may be supplied instead. `--dir` defaults to
the desktop cheat directory. `add` needs `--name` and `--code`; `--description` is
optional and `--disabled` adds the entry disabled. `import` merges by name and replaces
existing entries with the same name. The file format and code syntax are documented in
[`Cheats.md`](Cheats.md).

`cheat list --json` returns `{ "ok": true, "path", "gameId", "cheats": [...] }` with
`name`, `description`, `code`, `enabled`, and `error` per entry. Actions return
`{ "ok": true, "action", "path", "gameId", "name", "count" }`. Invalid code is an error
for `add` and is preserved with an `error` field when it is already in a file.

---

## 8.3 `movie` commands

```powershell
5do movie info run.5domovie --json
5do movie record "game.chd" --bios bios/panafz1.bin --frames 6000 --out run.5domovie
5do movie play run.5domovie --disc "game.chd" --bios bios/panafz1.bin
5do movie verify run.5domovie --json
5do movie branch run.5domovie --frame 1200 --out branch.5domovie
```

`movie record` forwards normal `run` options and writes a versioned `.5domovie`
container with one raw PBUS packet per frame, an optional starting state, and
frame/PC/state evidence. `movie play` restores the embedded state unless `--state-in`
is supplied. `movie verify` replays every frame and exits with code 2 on a mismatch.
`movie branch` keeps the prefix before `--frame` and clears completion evidence so a
new branch can be recorded. `movie info` reads only the header.

`movie info --json` returns the format/version, game/disc/BIOS metadata, frame range,
final hashes, state size, options, notes, and file size. `movie verify` uses the
normal `run --json` contract with `moviePath`, `movieFrames`, `movieVerification`
(`pass`/`fail`), and `movieFailures`. See [`Tas.md`](Tas.md).

---

## 8.4 `settings` commands

```powershell
5do settings validate
5do settings validate "C:\Users\me\AppData\Local\5DO\settings.json" --json
5do settings migrate --path settings.json
5do settings options --format markdown
5do settings options --json
```

`settings validate [path]` checks that the file exists and parses, and reports its
`SettingsVersion`. It exits 1 when the file is missing or malformed and never rewrites
it. `settings migrate [path]` normalizes a valid file at the current schema version and
rewrites it atomically; it exits 1 rather than overwrite a malformed file. The path
defaults to `AppSettingsStore.DefaultPath` (`%LOCALAPPDATA%\5DO\settings.json` on
Windows). JSON output: `{ "ok", "path", "exists", "valid", "version", "error" }` for
validate and `{ "ok", "path", "fromVersion", "toVersion", "changed" }` for migrate.

`settings options` prints the machine-option registry as generated Markdown (the source
for [`MachineOptions.md`](MachineOptions.md)), a compact text table with
`--format text`, or a JSON array with `--json`. Every option lists its CLI flag, settings
property, profile property, default, range, and save-state coverage.

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

Reads the full file and validates every section CRC, decompressing Brotli sections when
needed. JSON is PascalCase:

```json
{
  "Version": 2,
  "EmulatorVersion": "1.0.0",
  "ArmClock": 12500000,
  "FixMode": 0,
  "BiosHash": "....",
  "DiscHash": "....",
  "FramesExecuted": 12000,
  "Sections": [
    { "Id": 1, "Version": 1, "PayloadLength": 199, "Compressed": false, "StoredLength": 199 },
    { "Id": 2, "Version": 1, "PayloadLength": 22052866, "Compressed": true, "StoredLength": 640070 }
  ],
  "Compressed": true,
  "StoredLength": 647647
}
```

Text output prints the header fields, the stored file size, and one
`section <id>: v<version>, <bytes> bytes` line per section; compressed sections add
`(stored <n>, compressed)`. See [`SaveStates.md`](SaveStates.md) for the binary layout.

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
5do bench --frames 600 --profile accuracy --json
5do bench --frames 120 --latency
```

Runs a synthetic ARM program (MADAM/SPORT writes and a branch loop), warms up 10
frames, then measures `--frames` frames. `--profile` accepts `compatible`, `accuracy`,
`performance`, or `custom` and applies the same timing bundle as `run`; the default is
`compatible`. `--cpu-clock` and `--memory` accept the same values as `run` for the clock
matrix and capacity checks. Fields:
`ok`, `frames`, `seconds`, `framesPerSecond`, `cyclesPerFrame`, `instructions`,
`frameHash`, `profile` (derived from the active options), `armTiming`, `clioTimers`,
`celTiming`, `cdromTiming`, `cpuClock`, and `memory`. Run the compatible and accuracy
profiles back to back to measure the accuracy cost before changing the default; the
clock matrix is recorded in [`Performance.md`](Performance.md) §8.1. `--latency` adds
`snapshotBytes`, `snapshotCaptureMilliseconds`, and `snapshotRestoreMilliseconds` from an
uncompressed `MachineStateSnapshot`, which is the per-frame cost of run-ahead; see
[`Performance.md`](Performance.md) §8.7.

The separate benchmark project (`benchmarks/FiveDo.Benchmarks`) adds p50/p95/p99,
per-stage timings/allocations, the CI gate, and baseline comparison. See
[`BuildAndTest.md`](BuildAndTest.md) section 4.

### `selftest`

```powershell
5do selftest
5do selftest --json
```

Checks for each named profile (`compatible`, `accuracy`, `performance`):

1. the applied options detect as that profile;
2. two synthetic machines produce the same frame hash;
3. the frame hash is non-zero;
4. a save/load roundtrip then 30 frames matches the uninterrupted machine;
5. the loaded state has no warnings.

Check names are prefixed with the profile, for example `accuracy state roundtrip`.
Exit code is 1 if any check fails. JSON: `{ "ok": true, "checks": [{ "name", "ok" }] }`.

---

## 12a. `update`

```powershell
5do update
5do update --json
5do update --force
```

Checks the public GitHub release feed for `purelogiccode/5DO`. Successful responses are
cached under the 5DO data directory for six hours; `--force` bypasses the cache. The
command exits 0 when the feed was read (whether or not an update exists) and 1 when the
request failed.

JSON fields: `ok`, `error`, `currentVersion`, `latestVersion`, `updateAvailable`,
`releaseName`, `releasePageUrl`, `downloadUrl`, `publishedAtUtc`, `fromCache`.

The interactive startup check prints a one-line notice, asks `Open the download page now?
[y/N]`, and uses the system browser only after confirmation. `--json`, redirected
output, CI, `--no-update-check`, and `FIVEDO_UPDATE_CHECK=0` skip the prompt.

---

## 13. Diagnostic file formats

| Option | Format |
|---|---|
| `--cd-trace` | `cd f=<frame> pc=<pc> cmd <7 hex bytes>`; `cd read sector <n>`. |
| `--cd-transcript` | `f=<frame> cmd=<op> ... status=<hex> poll=<hex> drive=<hex>`; captures every command/status exchange. |
| `--clio-trace` | `clio f=<frame> write <addr:x3>=<value:x8> delay=<n>`; `clio dma f=... select=... target=... len=... first=...`. |
| `--clio-timer-trace` | `clio-timer f=<frame> cyc=<cycles> [slot=<n>] event=<slack\|control\|counter\|underflow> ...`; slot scans are omitted, wraps add `reload=` or `stop=1`, `fiq=<mask>`, and `carry=1`. |
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
- `fs` reads the 3DO Opera file system from ISO, BIN, CUE+BIN, and CHD images. A
  malformed volume/directory record fails with a clear parse error; `disc dump`
  remains available for raw sector inspection.
- `trace` and `disasm` have no JSON output.
- `state inspect`/`state slots` JSON is PascalCase; all other CLI JSON is camelCase.
- `--watch-start`, `--watch-end`, `--stop-pc`, `--stop-cd`, `--dump-ram-offset`, and
  `--base` are hexadecimal even without `0x`; do not pass a decimal address expecting
  decimal interpretation.
- `--poke` writes raw RAM bytes once, bypassing the MMIO map; it is a diagnostic tool.
  For persistent per-game cheats use `--cheat`, `--cheat-code`, or the `cheat`
  commands. See [`Cheats.md`](Cheats.md).
- A save state records the BIOS hash and disc hash, but the current loader only warns
  on a BIOS/emulator-version mismatch; it does not reject a different disc. See
  [`SaveStates.md`](SaveStates.md).

---

## 16. Full-collection sweep

`tools/compat-sweep.ps1` wraps `run` for the local collection. It starts a fresh CLI
process per disc, uses a fresh NVRAM image, records `result.json`/`stderr.txt`/`frame.png`,
enforces a timeout, and writes `summary.csv`/`summary.md`.

```powershell
# Smoke test
pwsh -File tools/compat-sweep.ps1 -Limit 2 -Frames 3000 -OutDir artifacts/compat-smoke

# Full collection, resume-safe
pwsh -File tools/compat-sweep.ps1 -Frames 3000 -Resume -OutDir artifacts/compat

# High-resolution pass
pwsh -File tools/compat-sweep.ps1 -Frames 3000 -HighRes -OutDir artifacts/compat-highres
```

The sweep uses the same `run --json` fields documented in §3, plus image metrics from
the final CPU-assembled PNG. See [`CompatibilitySweep.md`](CompatibilitySweep.md) for
status definitions and triage, and
[`GameCompatibilityList.md`](GameCompatibilityList.md) for the per-title matrix.
