# TAS Movies

5DO movies (`.5domovie`) are deterministic input recordings for tool-assisted
speedruns, regression evidence, and bug reports. A movie stores the exact PBUS
packet sampled for every emulated frame plus an optional starting save state and a
JSON metadata header.

This is the TAS increment (UX-06), with the UX-29 scripting and timeline increment. It
covers the movie container, record/replay/verify, branching, CLI commands, desktop shell
controls, a per-frame movie editor, deterministic Lua input authoring, and an editable
input timeline (CLI and desktop). Input display is shown in the status bar while a movie
plays. A separate lag counter remains inapplicable because 3DO input is sampled every
frame.

## 1. Why per-frame PBUS packets

The 3DO polls the controller chain once per frame at the V1 line. Recording the raw
PBUS bytes instead of decoded buttons means the format works unchanged for joypads,
flightsticks, mice, lightguns, and the Orbatak trackball when those devices land.
It also avoids any lossy interpretation: playback returns exactly what the machine
saw.

`FiveDo.Tas.Movie` exposes decoded player states through
`FiveDo.Input.PbusDecoder` for the status bar and tools. Disconnected slots decode as
empty states.

## 2. File format

The container is versioned and checksummed. All integers are little-endian.

| Offset | Size | Field |
|---:|---:|---|
| 0 | 8 | magic `5DOMOVIE` |
| 8 | 4 | format version (`1`) |
| 12 | 4 | JSON header length |
| 16 | 8 | compressed input payload length |
| 24 | 8 | uncompressed input payload length |
| 32 | 8 | embedded state length (0 when absent) |
| 40 | 4 | header CRC-32 |
| 44 | 4 | compressed payload CRC-32 |
| 48 | 4 | state CRC-32 |
| 52 | ... | UTF-8 JSON header |
| ... | ... | Brotli-compressed input payload |
| ... | ... | optional raw save state |

The input payload is:

```
int32  frame count
repeat frame count times:
  uint16 packet length
  byte[] packet bytes
```

The JSON header records:

- `formatVersion`, `emulatorVersion`, and `createdUtc`
- `gameName`, `gameId`, `discHash`, and the disc file name only
- primary BIOS MD5 and display name
- `startFrame`, `frameCount`, and `endFrame`
- final frame hash, final ARM program counter, and final state hash
- `hasState` and the emulation options active during recording

A movie is rejected when its magic, version, CRC, lengths, frame count, or trailing
data are inconsistent. Version 1 is the only format this build writes; older
versions remain readable through `MovieFormat.MinSupportedVersion`.

The embedded state is optional. A movie with a state can be replayed without a BIOS
as long as the game's CD data is not required. A movie without a state needs the
same BIOS and disc used for recording. The header's `discHash`/`biosMd5` are
compared and reported when they differ, but verification ultimately relies on the
recorded frame/PC/state evidence.

## 3. Determinism contract

A movie verifies only when all of these match the recorded header:

- the number of played frames
- the final assembled frame hash (`FiveDo.Video.FrameHasher`)
- the final ARM program counter
- the final deterministic state hash, when one was recorded

The state hash uses the same FNV-1a contract as the CLI checkpoint/state hash. State
hashes include the PBUS queue, so a tampered input frame changes the evidence even
when a synthetic test program does not read input.

Movies are sensitive to the same settings as save states: ARM timing, CLIO timer
mode, CEL timing, CD timing/speed, CPU clock, fix mask, random seed, high-resolution
mode, and any per-game overrides. The header records these options and
`movie verify` prints differences as warnings. For bit-identical verification, use
the same BIOS, disc, 5DO version, and options.

3DO hardware polls input every frame; there is no input-lag window to count. A
separate lag counter is therefore not meaningful for this console, and the status
bar shows the current frame and player-1 buttons instead.

## 4. CLI

### Record

```powershell
5do movie record "game.chd" --bios bios/panafz1.bin --frames 6000 --out run.5domovie
```

`movie record` forwards every normal `run` option, including `--input-script`,
`--state-in`, `--profile`, `--cd-speed`, and `--cheats`. The starting state is
embedded unless `--movie-no-state` is supplied. To record a branch from a save state:

```powershell
5do movie record --state-in run.state --frames 1200 --out branch.5domovie
```

### Play

```powershell
5do movie play run.5domovie
5do movie play run.5domovie --disc "game.chd" --bios bios/panafz1.bin
```

If the movie has an embedded state, it is loaded before playback. If `--frames` is
omitted, the whole movie plays; a shorter `--frames` plays a prefix.

### Verify

```powershell
5do movie verify run.5domovie --json
```

Verification replays every frame and exits with code `2` when the recorded evidence
does not match. `run --json` reports `moviePath`, `movieFrames`,
`movieVerification` (`pass`/`fail`), and `movieFailures`.

### Inspect

```powershell
5do movie info run.5domovie
5do movie info run.5domovie --json
```

`info` reads only the header and does not start the emulator. It reports the format,
game/disc/BIOS metadata, frame range, hashes, state size, options, and file size.

### Branch and trim

```powershell
5do movie branch run.5domovie --frame 1200 --out branch.5domovie
```

`branch` keeps frames before the requested frame, clears the old completion
evidence, and preserves the starting state. The result can be extended with a new
recording. `trim` is an alias.

### Direct `run` options

The same features are available on `run` for scripted experiments:

```powershell
5do run "game.chd" --bios bios/panafz1.bin --movie-record out.5domovie
5do run --movie-play out.5domovie --movie-verify --json
```

`--movie-play` cannot be combined with `--input-script` or `--autopilot`.

## 5. Desktop shell

**Emulation > Record Movie...** asks for a destination, captures the current
machine state, and records from the next frame. **Emulation > Play Movie...** loads
a movie, restores its embedded state when present, and starts playback.
**Emulation > Stop Movie** finishes a recording or detaches playback. Stopping
emulation or closing the window also finalizes an active recording.

While a movie plays, the status bar shows `Movie <position>/<frameCount>` and the
current player-1 buttons. Playback pauses automatically at the end of the movie.

## 6. Legal and preservation notes

Movies contain input data, a save state, and metadata. They do not contain BIOS
ROMs, disc images, or copyrighted game assets. A save state can contain guest RAM
and VRAM from the user's own session; treat movies as user data and do not publish
them without the same care as screenshots or save states.

## 7. Current limits

- Lua input scripts emit player-1 button entries; multi-player Lua authoring is not
  exposed yet.
- The per-frame movie editor supports player-1 button edits, set/insert/remove, trim,
  and Save As. The Input Timeline window draws per-button segments and applies player-1
  edits. Multi-player timeline editing remains future work.
- No multi-player input display beyond the decoded packet; the movie itself records
  all six slots and future devices.
- A separate lag counter is not applicable because 3DO input is sampled every
  frame.
- Movie verification is deterministic only when the same BIOS, disc, options, and
  5DO version are used.

---

## 8. Lua input scripting (UX-29)

`FiveDo.Scripting` compiles a sandboxed Lua script into ordinary
`InputScriptEntry` values. The script runs once at authoring time; replay uses the
normal movie or scripted-PBUS path, so determinism comes from the recorded movie rather
than from the Lua runtime.

```lua
-- A short scripted opening
local holdFrames = 12
frame(0, "A")
press(30, "P", 2)
hold(60, 60 + holdFrames, "Down,A")
release(120)
```

The global functions are:

| Function | Meaning |
|---|---|
| `frame(n, buttons)` | Set player 1's buttons at frame `n`. |
| `hold(from, to, buttons)` | Hold buttons from `from` through `to`, then release at `to + 1`. |
| `press(n, buttons, count)` | Hold buttons for `count` frames starting at `n`. |
| `release(n)` | Release every player-1 button at frame `n`. |

The same functions are available as `input.frame`, `input.hold`, `input.press`, and
`input.release`. Buttons use the normal names and `,`/`+`/`|` separators; `None` or an
empty string releases. Consecutive identical states are coalesced, so the entry list
contains only real changes.

Use it through the normal run/movie options:

```powershell
5do run "game.chd" --bios bios/panafz1.bin --input-lua opening.lua --frames 3600
5do movie record "game.chd" --bios bios/panafz1.bin --input-lua opening.lua `
  --frames 3600 --out opening.5domovie
```

`--input-lua` cannot be combined with `--input-script`, `--autopilot`, or
`--movie-play`. `--script-base state` also applies to Lua entries when `--state-in` is
used.

The sandbox uses MoonSharp's hard preset: no file, process, clock, network, or dynamic
loading APIs. Scripts may use `math`, `string`, `table`, loops, locals, and functions.
Frame numbers must be non-negative and at most 10,000,000.

## 9. Input timeline (UX-29)

`InputTimeline` stores player-1 state changes and can be built from a movie, saved as an
input-script document, edited, and applied back to a movie. Applying a timeline rewrites
player 1's buttons on every frame while preserving the other controller slots, the
embedded starting state, and the header metadata; completion evidence is cleared because
the input changed.

```powershell
# Export the movie's player-1 changes as editable frame/buttons lines
5do movie timeline export run.5domovie --out run.timeline.txt

# Change one frame
5do movie timeline set run.timeline.txt --frame 300 --buttons Down,A `
  --out run-edited.timeline.txt

# Apply the edited timeline and record a new movie
5do movie timeline apply run-edited.timeline.txt run.5domovie --out run-edited.5domovie
```

The desktop shell exposes the same model through **Emulation > Input Timeline...**.
The window draws one row per 3DO button, a one-second grid, and a playhead. Clicking the
strip or dragging the frame slider selects a frame; the checkboxes edit player 1's
buttons on that frame. **Apply to Movie...** writes a new movie with the timeline input.

Timeline scripts are plain input-script text, so they can also be edited by hand or
generated by a Lua script. Multi-player timeline editing remains future work.
