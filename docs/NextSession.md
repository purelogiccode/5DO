# Next Session Handoff

**Date:** 2026-10-24
**Goal:** fix the DSP `AllocInstrument` handshake. It is the common blocker behind
SAudio `-6996` (Road Rash, Wolfenstein 3D, Alone in the Dark) and the Space Ace
frame-681 runaway.

This session is a focused DSP/audio differential against Opera. Do **not** start
with UI polish, 4K tuning, or a wholesale CD-ROM rewrite. The CD path and the
game-side audio code are now well enough understood; the open question is why the
DSP never acknowledges the ARM's AudioFolio command.

---

## 1. Preconditions

- Repository is on `master`, clean, and in sync with `origin/master`.
- `References/` is ignored and contains the reference trees; it is not committed.
- BIOS: `bios/panafz1.bin`.
- Discs: `E:\Panasonic 3DO\*.chd` (the previous `D:\Samples\3DO` path is stale).
- `dotnet build -c Release`, `dotnet test tests/ThreeDo.Tests -c Release`, and
  `dotnet format --verify-no-changes` pass.
- Opera reference: `References/CPP_SimpleGui/cmake-build-release/SimpleGui/SimpleGui.exe`
  and `References/opera-libretro-master/libopera/opera_dsp.c`.

Read these first:

- [`Compatibility.md`](Compatibility.md) - decoded error and crash mechanism.
- [`Status.md`](Status.md) - current position and ordered next steps.
- [`ReferenceStrategy.md`](ReferenceStrategy.md) - reference hierarchy and porting rules.
- [`PortingNotes.md`](PortingNotes.md) - C# porting contract.
- [`TestCorpus.md`](TestCorpus.md) - corpus, baselines, and diagnostic commands.

---

## 2. What changed on 2026-10-24

### 2.1 DSP execution bug (fixed)

`ThreeDoMachine.InternalFrame` used `AudioSink?.PushSample(Dsp.Loop())`. The
null-conditional operator short-circuits argument evaluation, so headless runs never
executed the DSP at all. The machine now always calls `Dsp.Loop()` and only gates
sample delivery on the sink. This is required for audio resource allocation, not
just for output. It did not, by itself, fix SAudio.

### 2.2 SAudio `-6996` decoded

The 3DO SDK's SAudio subscriber maps `AllocInstrument` failures to
`SAudioErrAllocInstFailed` (`-6996`) in `SAErrors.h`. The raw AudioFolio error is
discarded by `InitSAudioChannel`. The failing calls are:

```c
AllocInstrument(ctx->outputTemplateItem, 100);
AllocInstrument(ctx->envelopeTemplateItem, 100);
AllocInstrument(templateItem, 100);
```

### 2.3 Space Ace runaway decoded

At frame ~681 the ReadySoft player's audio task copies 27,538 bytes from `0x80034`
to `[audioState+0x9C]`. That pointer is zero because the player's mixer/instrument
setup failed (`d52bf113`). The copy starts at address 0 and wipes the ARM exception
vectors. A timer FIQ then vectors through the zeroed table at `0x1C`, and the CPU
walks off to `0x40000000`.

### 2.4 New diagnostics

The CLI now supports:

- `--watch-lowmem <path> [--watch-from-frame N] [--watch-start N] [--watch-end N]`
  - logs every RAM write in a range with the guest PC, value, size, registers, and
    caller (CPU, CLIO DMA, CEL, etc.).
- `--dump-ram <path> [--dump-ram-offset N] [--dump-ram-length N]` - raw RAM dump.
- `--dump-dsp <prefix>` - writes `<prefix>.imem.bin` and `<prefix>.dmem.bin`.
- `--dsp-trace <path>` (trace command only) - ring of the last 4,096 DSP
  instructions with PC, raw word, Sema4 status/data, and next PC.
- `disasm <file> [--offset N] [--count N] [--base 0xADDR]` - raw little-endian ARM
  words, useful on RAM dumps.
- CLIO traces now include the frame number, and XBUS DMA traces include the selected
  device and first FIFO word.

---

## 3. Current evidence

Run Road Rash and stop at the SAudio error:

```powershell
$t = "$env:TEMP\3do-next"
New-Item -ItemType Directory -Force $t | Out-Null

dotnet run -c Release --project src/ThreeDo.Cli -- trace `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --frames 0 --trace-frames 3000 --stop-kprint "SAudio channel" --ring 4 `
  --dsp-trace "$t\rr-dsp-trace.txt" --dump-dsp "$t\rr-saudio" `
  --out "$t\rr-trace.txt"
```

Observed at the stop:

- CPU `PC 0x00012328`, SAudio subscriber printing the error.
- DSP `running=true`, `PC 0x2D`, `Sema4Status=0x0008`, `Sema4Data=0x0000`.
- DSP trace repeats `0x000`-`0x02D`; it never reads `0xED` (Sema4Data) or writes
  `0x3EC` (Sema4ACK).
- The branch at `0x016` (`0xEC1E`) is taken every sample; it tests the `exact` flag
  produced by `0x014` (`0x2470`).

The DSP program is loaded (head/tail code plus relocations). The failure is not an
empty DSP memory. The suspect is the interpreter's flag/branch/relocation behavior,
or a missing AudioFolio command-entry mechanism.

---

## 4. Ordered plan

### Step 1 - Freeze the DSP baseline

Run the Road Rash command above and keep `rr-trace.txt`, `rr-dsp-trace.txt`,
`rr-saudio.imem.bin`, and `rr-saudio.dmem.bin`. Record the DSP PC, Sema4 status/data,
and the last 64 trace lines. Do the same for Wolfenstein and Alone; they should show
the same `AllocInstrument` failure.

### Step 2 - Decode the DSP head/tail programs

The AudioFolio loads `head.dsp` and `tail.dsp` from the SDK. The binary format is
IFF with `DSPPDHDR`, `DCOD`, `DRSC`, `DRLC`, and `DNMS` chunks. The `DRLC` chunk is
the relocation table; `DCOD` is the code.

- Fetch the SDK files from `trapexit/3do-devkit` under
  `takeme/System/Audio/dsp/` (`head.dsp`, `tail.dsp`, and the instrument files).
- Parse `DSPPDHDR`/`DCOD`/`DRLC` and compare the relocated code with the dump.
- Confirm the load address, entry point, and relocation semantics. The header's last
  word (`0x0020` for head, `0x0010` for tail) is the key field.
- Compare the result with `opera_dsp.c`'s `opera_dsp_load`/instrument handling.

### Step 3 - Compare the interpreter with Opera

Work through `References/opera-libretro-master/libopera/opera_dsp.c` and
`src/ThreeDo.Dsp/Dsp.cs` in this order:

1. `dsp_read`/`dsp_write` special registers (`0xEA`-`0xEF`, `0x3EB`-`0x3EF`).
2. The ALU flag calculation (`ADD_CFLAG`, `SUB_CFLAG`, `ADD_VFLAG`, `SUB_VFLAG`,
   `Flags.Zero/Nega/Carry/Over`).
3. The `exact` flag (`fExact = (Y & 0x0000F000) ? 0 : 1`).
4. The branch-condition table (`BRCONDTAB` vs `BuildBranchTable`).
5. The operand loader and the `NMem`/`IMem` split.
6. The head/tail command entry and the Sema4 ACK path.

Do not change multiple instruction families at once. Add a focused DSP unit test
for each fixed behavior; the existing tests already exercise the DSP.

### Step 4 - Prove the fix

- Re-run Road Rash, Wolfenstein, Alone, Lemmings, and Space Ace for at least 3,000
  frames.
- Confirm `SAudio channel 0 failed` is gone, or that the game advances past it.
- Confirm Space Ace reaches 3,000 frames without a FIQ at `0x1C` or a null-buffer
  `memcpy`.
- Record before/after DSP traces, frame hashes, and KPRINT logs.
- Update `Compatibility.md`, `TestCorpus.md`, and the progress log in
  `ImplementationPlan.md`.

---

## 5. Acceptance criteria

- [ ] The DSP `AllocInstrument` command is acknowledged (`Sema4Status` leaves `0x8`)
      or the exact reason it cannot be is documented with an Opera comparison.
- [ ] SAudio `-6996` is gone or the game reaches a later, specific failure.
- [ ] Space Ace runs past frame 681 without the null-buffer `memcpy`.
- [ ] At least one focused DSP/audio test is added.
- [ ] `dotnet build -c Release`, `dotnet test`, and `dotnet format` pass.
- [ ] Docs are updated and the repository is clean and pushed.

If the DSP port is deeper than one session, stop at a written diagnosis with a
minimal reproducer and the exact failing instruction sequence. Do not patch guest
memory or special-case a game title.

---

## 6. Do not do in this session

- Do not re-enable the `AudioSink?.PushSample(Dsp.Loop())` short-circuit. The DSP
  must run without an output sink.
- Do not "fix" the XBUS DMA byte order. `IRam` is the raw word-lane accessor, so
  `b0,b1,b2,b3` is correct here; Opera's `b3,b2,b1,b0` applies to its CPU-visible
  `opera_mem_write8`. A byte-order change was tried on 2026-10-24 and broke every
  CD-booting title.
- Do not port `opera_cdrom.c` wholesale. The CD trace already shows correct sectors.
- Do not touch the UI, renderer filters, 4K settings, or packaging.
- Do not commit BIOS ROMs, game images, extracted SDK files, or Opera binaries.
- Do not copy Opera source verbatim; reimplement the behavior in C# and preserve
  attribution.

---

## 7. Useful commands

```powershell
# Build/test/format
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release
dotnet format --verify-no-changes

# Baseline: all five corpus titles
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --kprint artifacts/rr-kprint.txt --frame-out artifacts/rr.png

# SAudio stop with DSP trace and memory dumps
dotnet run -c Release --project src/ThreeDo.Cli -- trace `
  "E:\Panasonic 3DO\Road Rash (USA).chd" --bios bios/panafz1.bin `
  --frames 0 --trace-frames 3000 --stop-kprint "SAudio channel" --ring 4 `
  --dsp-trace artifacts/rr-dsp-trace.txt --dump-dsp artifacts/rr-saudio `
  --out artifacts/rr-trace.txt

# Space Ace low-memory watch
dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:TEMP\3do-opera\spaceace.iso" --bios bios/panafz1.bin `
  --frames 700 --watch-lowmem artifacts/sa-watch.txt --watch-from-frame 681 `
  --watch-start 0 --watch-end 0x100

# Disassemble a RAM dump
dotnet run -c Release --project src/ThreeDo.Cli -- disasm `
  artifacts/rr-ram.bin --offset 0x12380 --count 100 --base 0x0

# Opera reference (same BIOS, extracted ISO)
dotnet run -c Release --project src/ThreeDo.Cli -- disc dump `
  "E:\Panasonic 3DO\Space Ace (USA).chd" --sector 0 --count 210370 `
  --out "$env:TEMP\3do-opera\spaceace.iso"
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\3do-opera\spaceace.iso"
```

---

## 8. Open questions

1. Does the DSP need to execute a relocated head/tail command entry, or is the
   AudioFolio command supposed to be handled by the resident sample loop?
2. Is the branch at `0x016` (`0xEC1E`) always taken because the `exact` flag from
   `0x014` (`0x2470`) is wrong, or because the program genuinely loops every sample?
3. Does Opera load `head.dsp`/`tail.dsp` at different addresses or apply different
   relocations than our AudioFolio path?
4. Is the `AllocInstrument` failure a DSP resource-map issue (instrument code not
   fitting) rather than an interpreter bug? The DSP trace currently points at the
   handshake, not at a full memory map.
5. Once `AllocInstrument` succeeds, does Space Ace's null buffer disappear, or is
   there a second game-side bug in the audio task cleanup?
