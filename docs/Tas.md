# TAS Movies

5DO movies (`.5domovie`) are deterministic input recordings for tool-assisted
speedruns, regression evidence, and bug reports. A movie stores the exact PBUS
packet sampled for every emulated frame plus an optional starting save state and a
JSON metadata header.

This is the first TAS increment (UX-06). It covers the movie container,
record/replay/verify, branching, CLI commands, and desktop shell controls. Input
display is shown in the status bar while a movie plays. Lua scripting, frame-by-frame
input editing, and a separate lag counter remain future work.

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

- No Lua scripting API yet.
- No frame-by-frame input editor or on-screen input timeline; use `movie branch`
  plus a new recording for now.
- No multi-player input display beyond the decoded packet; the movie itself records
  all six slots and future devices.
- A separate lag counter is not applicable because 3DO input is sampled every
  frame.
- Movie verification is deterministic only when the same BIOS, disc, options, and
  5DO version are used.
