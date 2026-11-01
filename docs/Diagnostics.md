# Diagnostics

This document is the workflow guide for diagnosing emulation, renderer, audio, and
state problems. It complements the option reference in [`CliReference.md`](CliReference.md)
and the symptom checklist in [`Troubleshooting.md`](Troubleshooting.md).

---

## 1. Choose the right tool

| Question | Tool |
|---|---|
| Did the machine boot and what state is it in? | `run --json`, `--progress`, `--frame-out` |
| What is the CPU doing? | `trace`, `--stop-pc`, `--stop-invalid`, instruction ring |
| What is the guest printing? | `--kprint`, `--kprint-pc`, `trace --stop-kprint` |
| What is the CD doing? | `--cd-trace`, `--progress`, `run --json` `cdStatus`/`cdPoll` |
| What are CLIO timers/FIQ/DMA doing? | `--clio-trace`, `--fiq-trace` |
| What is the CEL engine drawing? | `--cel-trace`, `--capture-dir`, `--frame-out` |
| What input is the guest seeing? | `--pbus-trace`, `--input-script` |
| Where did a RAM value change? | `--watch-lowmem` |
| What is in RAM/DSP memory? | `--dump-ram`, `--dump-dsp`, `disasm` |
| Is the audio queue clean? | `FIVEDO_AUDIO_TAP`, `audio analyze`, OpenAL counters |
| Is the GPU presenter correct/fast? | `--gl-probe`, `--gl-probe-capture`, parity |
| Is the CPU frame budget healthy? | `benchmarks/FiveDo.Benchmarks --gate` |
| Is the save state consistent? | `state inspect`, `run --state-in`, roundtrip hashes |
| What did the app log? | in-app **Logs** / **Log Viewer** or the rolling file |

---

## 2. Logs

Both hosts configure Serilog through `FiveDo.Diagnostics.Logging.LoggingSetup`:

- Levels: Serilog `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`.
- Categories: `Cpu`, `Memory`, `Madam`, `Clio`, `Dsp`, `Sport`, `Video`, `Xbus`,
  `Cdrom`, `Storage`, `Audio`, `Input`, `Machine`, `Host`, `State`, `Diagnostics`,
  `All`. The category is attached as the `Category` property.
- Sinks: rolling file, stderr console (desktop), in-memory viewer, and the
  bug-report API for `Warning` and above.

Log files are written to `%LOCALAPPDATA%\5DO\logs\5do-YYYYMMDD.log` on Windows,
`~/Library/Logs/5DO/` on macOS, and `$XDG_STATE_HOME/5DO/logs` (or
`~/.local/state/5DO/logs`) on Linux. Seven daily files are retained.

The desktop shell feeds **Logs** and the separate **Log Viewer** from the same
Serilog pipeline (capacity 4000) with level/category/search/auto-refresh and clear
controls. The log records BIOS/disc loads, renderer initialization/fallback,
save/load, screenshots, input changes, and emulation faults. The CLI keeps its
stdout/stderr contract (JSON and concise errors) and writes diagnostics to the file
and bug-report sinks.

Warning-or-higher events are submitted asynchronously to the bug-report API with
the required environment, error, and exception blocks. Expected command-line usage
errors (bad arguments, missing paths, malformed input scripts, and out-of-range
offsets) are logged with `WithoutBugReport` so they do not create bug reports.
Submissions are deduplicated for five minutes and capped at ten per minute. Set `FIVEDO_BUG_REPORTS=0` to
disable submission, `FIVEDO_LOG_DIR` to redirect log files, or
`FIVEDO_BUGREPORT_API_KEY` to override the API key. CI and test runs disable
submission automatically.

To capture a desktop log for a bug report, reproduce the issue, open **Log Viewer**,
or attach the rolling log file. For CLI issues, redirect both streams:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 600 --json *> artifacts/run.log
```

---

## 3. `run --json` as a first check

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 600 --json > artifacts/run.json
```

Look at:

- `frames`, `pc`, `cyclesPerFrame`: did it execute the expected amount?
- `sourceWidth`/`sourceHeight`, `vdlModulo`: is the video mode plausible?
- `frameHash`: is the frame non-zero and stable across runs?
- `samples`, `dspRunning`, `dspPc`, `dspSema`, `dspSeed`: is the DSP alive?
- `cdStatus`, `cdPoll`: is the guest stuck polling the CD?
- `warnings`: state-load BIOS/version warnings.
- `audio*`: capture/queue health.
- `autopilot*`: only when `--autopilot road`.

A static READ_ID-only wait historically showed `cdStatus=0xE1`, `cdPoll=0x0C`. The CD
media-access pulse sets poll bit `0x40` after two READ_ID polls at least 50 frames
apart, which advances the post-race/post-load transitions.

For a live view, use `--progress N`:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 100000 --progress 1000
```

The stderr line includes frame, PC, DSP PC/running/semaphore, audio sample count, and
autopilot fields when active.

---

## 4. CPU tracing

`trace` runs a warm-up then records an instruction ring:

```powershell
dotnet run --project src/FiveDo.Cli -- trace game.chd --bios panafz1.bin `
  --frames 12000 --trace-frames 2 --ring 4096 `
  --out artifacts/trace.txt
```

Stop conditions:

| Option | Stops when |
|---|---|
| `--stop-pc 0xADDR` | The PC equals the address (before executing it). |
| `--stop-kprint text` | The rolling KPRINT line ends with the text. |
| `--stop-cd 0xCMD` | The CD executes the command byte. |
| `--stop-invalid` | PC >= `0x04000000`. |
| `--count N` | N instructions have been observed. |

The output starts with a stop comment (if stopped), a register/CPSR line, a DSP line,
a 16-word stack line, then the ring:

```text
# stopped at 0x0012B3C8 after 4096 instructions
# pc=0012B3C8 cpsr=60000010 r0=... r1=... ... sp=... lr=...
# dsp running=True pc=0000 sema=0000/0000 prld=... audcnt=... seed=...
# stack: 00000000 ...
0012B3B0 e6000010 00000000 mov r0, r0
```

Use `--dump-ram` with the same stop to disassemble around the PC:

```powershell
dotnet run --project src/FiveDo.Cli -- disasm artifacts/ram.bin `
  --offset 0x12380 --count 64 --base 0x12380
```

Note: `--dump-ram-offset`, `--stop-pc`, `--stop-cd`, and `--base` are parsed as
hexadecimal even without `0x`.

---

## 5. KPRINT (guest debug output)

The 3DO Portfolio kernel prints debug text through the MADAM KPRINT register.

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 3000 --kprint artifacts/kprint.txt --kprint-pc artifacts/kprint-pc.txt
```

- `--kprint` writes decoded characters; CR/LF become newlines; control characters
  below space are skipped.
- `--kprint-pc` writes one line per KPRINT value:
  `<pc> <byte> lr=<lr> sp=<sp> <8 stack words>`.

To stop at a message:

```powershell
dotnet run --project src/FiveDo.Cli -- trace game.chd --bios panafz1.bin `
  --frames 1000 --trace-frames 2 --stop-kprint "SAudioErr" `
  --out artifacts/trace.txt
```

`--stop-kprint` resets its rolling text at CR/LF and matches a suffix, so it works for
line-oriented kernel output.

---

## 6. CD-ROM tracing

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 100000 --cd-trace artifacts/cd.txt --progress 1000
```

`--cd-trace` writes:

```text
cd f=<frame> pc=<pc> cmd <7 hex bytes>
cd read sector <lba>
```

Use `--stop-cd 0x83` to stop on READ_ID, or `--stop-cd 0x10` for READ_DATA. Combine
with `run --json` `cdStatus`/`cdPoll` and `--progress` to see whether the guest is
polling a static state. With `--cd-timing opera`, `cd read sector <lba>` appears when
the scheduled sector deadline expires rather than at command time, so the gap between
the command and the read line is the modeled 2x drive time.

`--cd-transcript <path>` writes the richer command/status record used for parity work:

```text
f=15 cmd=83 00 00 00 00 00 00 status=8300100001000000000000e1 poll=1f drive=e1
f=15 cmd=10 00 02 00 00 00 01 status=10e1 poll=3f drive=e1
```

Each line contains the frame, all seven command bytes, the status response, poll, and
drive status after the command handler. `Battle Chess (Europe)` produces 393 entries
over its first 300 frames. Unsupported/unknown commands return an illegal-request
status (`0x17` in the READ ERROR payload), and the guest RESET command restores the
default 2048-byte transfer.

`disc tracks game.chd` lists the CHD track table; `info game.chd` shows the volume
header and game ID. `disc dump` writes raw 2048-byte sectors for manual inspection.

---

## 7. CLIO, FIQ, and DMA tracing

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 3000 --clio-trace artifacts/clio.txt --fiq-trace artifacts/fiq.txt
```

`--clio-trace` records selected register writes and DMA starts:

```text
clio f=<frame> write <addr:x3>=<value:x8> delay=<n>
clio dma f=<frame> select=<device> target=<addr> len=<n> first=<4 bytes>
```

The trace covers timer/FIQ registers (`0x200`-`0x20C`, `0x220`), the timer block
(`0x100`-`0x17C`), and `0x304`. `--fiq-trace` records:

```text
f=<frame> pc=<pc> reason1=<hex> reason2=<hex>
```

FIQ reason bit 0 is V0, bit 1 is V1, and other bits come from CLIO/DSP/CD sources.
Use these when the guest hangs waiting for an interrupt or a DMA completion.

---

## 8. CEL tracing and frame capture

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 3000 --cel-trace artifacts/cel.txt `
  --capture-dir artifacts/frames --capture-every 300
```

`--cel-trace` writes one line per CEL command with CCB/next pointers, flags, PRE0/PRE1,
data address, bpp, size, positions, deltas, clip window, source/destination, plus
progress/arbitrary/`ccbdata`/`done` lines. With `--cel-timing opera`, each list also
writes a `cel-list` line with the list entry, pixel, texel, and busy-cycle totals. It is
verbose; use a short frame range or a specific state.

`--capture-dir` + `--capture-every N` writes `f%06d.png` at frame multiples. Captures
use the CPU `FrameAssembler`, so they are the oracle image, not the selected GPU filter.
`--frame-out` writes the final frame (`.png` or `.bmp`).

Typical checks:

- Does the CEL have a plausible `size`, `bpp`, and `data` address?
- Are the CLUT/background/out-control values sane?
- Does the captured frame match the reference at the same frame?
- Is the CEL repeating forever (`progress` with no `done`)?

---

## 9. RAM watchpoints and dumps

### Watchpoint

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 5000 --watch-lowmem artifacts/watch.txt `
  --watch-start 0x0 --watch-end 0x200 --watch-from-frame 1000
```

The line format is:

```text
f=<frame> pc=<pc> addr=<addr> value=<value> size=<bytes>
r0=... r1=... r2=... r3=... sp=... lr=...
stack=<6 words>caller=<managed caller>
```

`--watch-start`/`--watch-end` are hexadecimal even without `0x`. The observer sees CPU
and DMA writes. It is a developer diagnostic; leaving it enabled slows emulation.

### RAM dump

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 3000 --dump-ram artifacts/ram.bin `
  --dump-ram-offset 0x0 --dump-ram-length 0x300000
```

`--dump-ram-offset` is hexadecimal even without `0x`; `--dump-ram-length` accepts
decimal or `0x`. The dump is raw CPU-visible RAM bytes.

### DSP dump

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 3000 --dump-dsp artifacts/dsp
```

Writes:

- `dsp.imem.bin`: 1024 instruction words, little-endian 16-bit.
- `dsp.dmem.bin`: 2048 data words, little-endian 16-bit.

Use with the DSP trace or a disassembler.

---

## 10. Input scripts and PBUS tracing

Input scripts are frame-stamped button states:

```text
# frame  buttons
0        None
120      A
180      A,Down
240      None
```

Button names are `A`, `B`, `C`, `P`, `X`, `L`, `R`, and the D-pad directions.
`Start`, `Play`, and `Pause` are accepted aliases for `P`, and `Stop` for `X`.

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 3000 --input-script docs/scripts/lem-fun4-dpad.txt
```

With a state load, use `--script-base state` to shift a script written from frame 0:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --state-in artifacts/passcode.state --frames 1200 `
  --input-script docs/scripts/lem-fun4-dpad.txt --script-base state
```

`--pbus-trace` records the 20-byte PBUS queue sampled at V1:

```text
f=<frame> pbus=<40 hex chars>
```

Use it to confirm that the guest sees the buttons you expect. The legacy packet carries
controllers in the wired order 1, 3, 2, 5, 4, 6; when a peripheral is selected the
queue contains the Opera daisy-chain records instead. See
[`Architecture.md`](Architecture.md) section 9.

`--autopilot road` is a Road Rash-specific driver and cannot be combined with
`--input-script`. It is a test harness, not a general AI.

---

## 11. Audio diagnostics

### Live queue health

`run --json` with `--audio openal` reports:

- `audioDroppedSamples`: ring-full drops.
- `audioUnderrunBuffers`: silent filler blocks.
- `audioQueuedBuffers`: buffers attached to the source.
- `audioBufferedFrames`: frames waiting in the ring.
- `audioLatencyMilliseconds`: estimated output latency.

The desktop overlay shows the same counters plus a peak meter.

### Tap vs loopback

Use the OpenAL backend (desktop, or CLI `--audio openal`) and set the tap:

```powershell
$env:FIVEDO_AUDIO_TAP = "$PWD\artifacts\tap"
dotnet run --project src/FiveDo.Frontend.Avalonia
```

Compare `tap.wav` (exact queued PCM) with a loopback recording:

- Tap clean + loopback crackles -> driver/Bluetooth/speaker issue.
- Tap crackles -> emulator/queue issue; inspect `tap.csv` for underruns, queue depth,
  and OpenAL errors at the timestamp.
- Tap silent -> DSP/volume/backend issue; check `dspRunning`, `samples`, and the WAV
  analyzer.

`tap.csv` columns:
`elapsedMs,buffer,frames,ringFrames,queuedBuffers,queuedFrames,underrun,peak,openAlError`.

### Offline analysis

```powershell
dotnet run --project src/FiveDo.Cli -- audio analyze artifacts/tap.wav --json
```

See [`Audio.md`](Audio.md) for metric definitions and expected 10.000 s duration.

---

## 12. Renderer diagnostics

### Backend selection

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- --gl-backend wgl
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- --gl-backend angle
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- --gl-backend software
```

`auto` tries WGL, then ANGLE/EGL, then software. The **About** dialog and Logs report
the backend/version. If GL init fails, the shell switches to the CPU `WriteableBitmap`
presenter and logs the reason.

### GPU probe

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-probe artifacts/gl-probe/4k.json `
  --gl-probe-size 3840x2160 --gl-probe-frames 120 --gl-probe-warmup 30 `
  --gl-probe-filter sharp-bilinear --gl-probe-offscreen
```

Use `--gl-probe-capture` with `--gl-probe-size 320x240 --gl-probe-filter accurate` for
CPU/GPU parity. The report includes capabilities, vsync, present/GPU percentiles,
allocations, and parity hashes. See [`Video.md`](Video.md) section 9.

### Frame hash / capture

`run --json` `frameHash` is an FNV-1a hash of the CPU-assembled BGRA frame. Compare it
across runs/states to detect divergence. `--frame-out`/`--capture-dir` write images for
visual comparison against a reference.

---

## 13. Save-state diagnostics

```powershell
dotnet run --project src/FiveDo.Cli -- state inspect artifacts/state.tdos --json
dotnet run --project src/FiveDo.Cli -- state slots artifacts/states --json
```

`state inspect` validates every section CRC and reports the header/section table.
`state slots` uses the fast header path and reports metadata/thumbnail paths.

For divergence:

1. Save at frame N.
2. Continue uninterrupted for M frames and record `frameHash`.
3. Load the state, run M frames, and compare `frameHash`.
4. Check `warnings` for BIOS/version mismatches.
5. If the hashes differ, capture frames/audio around the divergence and inspect the
   affected subsystem (CPU trace, CD trace, CEL trace).

See [`SaveStates.md`](SaveStates.md) for the format and limitations.

---

## 14. Performance diagnostics

```powershell
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- --frames 600 --json
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- `
  --state-in artifacts/roadrash-race.state --frames 600 --label race --json
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- --frames 120 --gate
dotnet run --project benchmarks/FiveDo.Benchmarks -c Release -- `
  --frames 600 --baseline artifacts/baseline.json --baseline-threshold 0.10
```

The report separates core, video pack, mailbox, and read-copy time and reports
allocations per stage. The gate enforces 0 bytes/frame, core p99 <= 8 ms, total mean
<= 8 ms, total p99 <= 16.667 ms. The baseline comparison is same-machine only; >10%
regression exits 4.

For GPU cost, use `--gl-probe`; for core-only cost, use the benchmark. Do not compare
numbers from different machines or power profiles.

---

## 15. Common investigation recipes

### "It boots but the screen is black"

1. `run --json`: check `frames`, `sourceWidth/Height`, `frameHash`, `pc`.
2. `--frame-out`: is the CPU frame black too?
3. If CPU frame is black, inspect `--cel-trace` and `--dump-ram`.
4. If CPU frame is fine but the window is black, use `--gl-backend software` and check
   Logs; run the GPU probe parity capture.

### "It hangs on a loading/race screen"

1. `--progress 1000` and `run --json` `cdStatus`/`cdPoll`.
2. `--cd-trace` and `--stop-cd 0x83`/`0x10`.
3. Check for a static READ_ID-only wait (`cdStatus=0xE1`, `cdPoll=0x0C`).
4. `trace --stop-kprint` for a kernel message.
5. `--dump-ram` + `disasm` around the PC.

### "Graphics are corrupted"

1. Capture the CPU frame (`--frame-out`) and a GPU screenshot.
2. Compare with the reference at the same frame.
3. `--cel-trace` for geometry/bpp/CLUT.
4. Try `--gl-backend software`; if CPU is correct and GPU is not, the presenter/shader
   is the suspect (check parity probe).
5. Check high-res mode: `--high-res` changes CEL/VDLP/SPORT behavior.

### "Audio crackles"

1. Set `FIVEDO_AUDIO_TAP`, reproduce, compare tap vs loopback.
2. Check `tap.csv` for underruns/OpenAL errors.
3. Check `run --json` dropped/underrun/queued/latency.
4. Try `--audio wav` to capture raw DSP output; analyze it.
5. If the WAV is clean and live is not, focus on OpenAL/device/Bluetooth.

### "State load diverges"

1. `state inspect` for CRC/header warnings.
2. Verify the BIOS hash and emulator version.
3. Roundtrip with a short continuation and compare `frameHash`.
4. Check the disc hash (the loader does not reject a different disc).
5. Capture traces around the first divergent frame.

### "The benchmark gate fails"

1. Close background CPU/GPU work.
2. Run the benchmark alone; check p99 vs mean.
3. Use `--baseline` to distinguish a real regression from machine noise.
4. Profile the stage that grew (core/pack/mailbox/read).
5. Do not relax the gate without recorded evidence and a docs update.

---

## 16. Artifact hygiene

- Keep diagnostics under `artifacts/` (ignored by git) rather than in `docs/`.
- Do not commit BIOS ROMs, game images, or extracted assets.
- Record only hashes, commands, frame counts, and measurements in docs.
- Name captures by title/frame/state so they can be reproduced.
- Compress large trace files; a CEL or CPU trace can grow quickly.
- Re-run the same command on a clean state before filing a bug.

For release evidence, record the exact commit, BIOS/disc hash, command, frame count,
and output hash in [`TestCorpus.md`](TestCorpus.md) or [`Compatibility.md`](Compatibility.md).
